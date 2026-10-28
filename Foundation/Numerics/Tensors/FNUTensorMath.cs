using System;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>CPU基础张量逐元素、尾轴广播、归约；不冒充GPU、AI计算图或FDT持久化。</summary>
    public static class FNUTensorMath
    {
        public static FNUTensorBuffer Add(FNUTensorBuffer a, FNUTensorBuffer b)
        {
            return Binary(a, b, false);
        }
        public static FNUTensorBuffer Multiply(FNUTensorBuffer a, FNUTensorBuffer b)
        {
            return Binary(a, b, true);
        }
        /// <summary>
        /// Elementwise addition into an existing output buffer with trailing-axis
        /// broadcasting. Inputs and output may not be the same instance.
        /// An arithmetic overflow can leave partial output.
        /// </summary>
        public static void AddInto(FNUTensorBuffer a, FNUTensorBuffer b, FNUTensorBuffer output)
        {
            BinaryInto(a, b, output, false);
        }

        /// <summary>Elementwise multiplication into a caller-owned output buffer.</summary>
        public static void MultiplyInto(FNUTensorBuffer a, FNUTensorBuffer b, FNUTensorBuffer output)
        {
            BinaryInto(a, b, output, true);
        }

        private static FNUTensorBuffer Binary(FNUTensorBuffer a, FNUTensorBuffer b, bool multiply)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            int rank = Math.Max(a.Shape.Rank, b.Shape.Rank);
            var dims = new int[rank];
            for (int axis = 0; axis < rank; axis++)
            {
                int la = axis - (rank - a.Shape.Rank);
                int ra = axis - (rank - b.Shape.Rank);
                int left = la < 0 ? 1 : a.Shape.DimensionAt(la);
                int right = ra < 0 ? 1 : b.Shape.DimensionAt(ra);
                if (left != right && left != 1 && right != 1)
                    throw new ArgumentException("Tensor dimensions cannot broadcast.");
                dims[axis] = left == 1 ? right : left;
            }
            var output = new FNUTensorBuffer(new FNUTensorShape(dims));
            BinaryInto(a, b, output, multiply);
            return output;
        }

        // For Into calls validation and indexing use only scalars. No temporary
        // dimensions arrays are allocated for each call or each element.
        private static void BinaryInto(FNUTensorBuffer a, FNUTensorBuffer b,
            FNUTensorBuffer output, bool multiply)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (output == null) throw new ArgumentNullException("output");
            if (ReferenceEquals(output, a) || ReferenceEquals(output, b))
                throw new ArgumentException("Output cannot alias an input tensor.", "output");
            int rank = Math.Max(a.Shape.Rank, b.Shape.Rank);
            if (output.Shape.Rank != rank)
                throw new ArgumentException("Output rank differs from broadcast result.", "output");

            for (int axis = 0; axis < rank; axis++)
            {
                int la = axis - (rank - a.Shape.Rank);
                int ra = axis - (rank - b.Shape.Rank);
                int left = la < 0 ? 1 : a.Shape.DimensionAt(la);
                int right = ra < 0 ? 1 : b.Shape.DimensionAt(ra);
                if (left != right && left != 1 && right != 1)
                    throw new ArgumentException("Tensor dimensions cannot broadcast.");
                int expected = left == 1 ? right : left;
                if (output.Shape.DimensionAt(axis) != expected)
                    throw new ArgumentException("Output dimensions differ from broadcast result.", "output");
            }

            for (int i = 0; i < output.Shape.ElementCount; i++)
            {
                int remaining = i, left = 0, right = 0;
                for (int axis = 0; axis < rank; axis++)
                {
                    int coordinate = remaining / output.Shape.StrideAt(axis);
                    remaining %= output.Shape.StrideAt(axis);
                    int la = axis - (rank - a.Shape.Rank);
                    int ra = axis - (rank - b.Shape.Rank);
                    if (la >= 0 && a.Shape.DimensionAt(la) != 1)
                        left += coordinate * a.Shape.StrideAt(la);
                    if (ra >= 0 && b.Shape.DimensionAt(ra) != 1)
                        right += coordinate * b.Shape.StrideAt(ra);
                }
                double value = multiply ? a.At(left) * b.At(right) : a.At(left) + b.At(right);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new OverflowException("Tensor operation overflow.");
                output.Put(i, value);
            }
        }

        public static double Sum(FNUTensorBuffer input)
        {
            if (input == null) throw new ArgumentNullException("input");
            // Neumaier accumulation also retains tiny values in
            // large-cancellation sequences such as 1e16 + 1 - 1e16.
            double sum = 0, correction = 0;
            for (int i = 0; i < input.Shape.ElementCount; i++)
            {
                double value = input.At(i), next = sum + value;
                if (Math.Abs(sum) >= Math.Abs(value))
                    correction += (sum - next) + value;
                else
                    correction += (value - next) + sum;
                sum = next;
            }
            double result = sum + correction;
            if (double.IsNaN(result) || double.IsInfinity(result))
                throw new OverflowException("Tensor sum overflow.");
            return result;
        }

        /// <summary>按轴求和。Rank=1的输出保留形状[1]而不是新建标量类型。</summary>
        public static FNUTensorBuffer ReduceSum(FNUTensorBuffer input, int axis)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (axis < 0 || axis >= input.Shape.Rank) throw new ArgumentOutOfRangeException("axis");
            int[] dims = new int[Math.Max(1, input.Shape.Rank - 1)];
            int j = 0;
            for (int i = 0; i < input.Shape.Rank; i++)
                if (i != axis) dims[j++] = input.Shape.DimensionAt(i);
            if (input.Shape.Rank == 1) dims[0] = 1;
            var output = new FNUTensorBuffer(new FNUTensorShape(dims));
            // Preserve small contributions using Neumaier compensation for each cell.
            double[] sums = new double[output.Shape.ElementCount];
            double[] corrections = new double[sums.Length];
            for (int flat = 0; flat < input.Shape.ElementCount; ++flat)
            {
                int remaining = flat, target = 0, outAxis = 0;
                for (int axisIndex = 0; axisIndex < input.Shape.Rank; axisIndex++)
                {
                    int coordinate = remaining / input.Shape.StrideAt(axisIndex);
                    remaining %= input.Shape.StrideAt(axisIndex);
                    if (axisIndex != axis) target += coordinate * output.Shape.StrideAt(outAxis++);
                }
                double value = input.At(flat), sum = sums[target], next = sum + value;
                corrections[target] += Math.Abs(sum) >= Math.Abs(value)
                    ? (sum - next) + value : (value - next) + sum;
                sums[target] = next;
            }
            for (int i = 0; i < sums.Length; ++i)
            {
                double value = sums[i] + corrections[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new OverflowException("Tensor reduction overflow.");
                output.Put(i, value);
            }
            return output;
        }

        /// <summary>二维矩阵乘法：左(m,k) × 右(k,n) -> (m,n)。</summary>
        public static FNUTensorBuffer MatMul(FNUTensorBuffer a, FNUTensorBuffer b)
        {
            int m, k, n;
            ValidateMatMulInputs(a, b, out m, out k, out n);
            var result = new FNUTensorBuffer(new FNUTensorShape(m, n));
            MatMulInto(a, b, result);
            return result;
        }

        /// <summary>
        /// 写入调用方提供的输出张量，避免每次分配新矩阵。
        /// output 须为 (m,n) 且不能与任一输入为同一实例。
        /// 计算溢出可能产生部分输出；出错后不得使用不完整结果。
        /// </summary>
        public static void MatMulInto(FNUTensorBuffer a, FNUTensorBuffer b,
            FNUTensorBuffer output)
        {
            int m, k, n;
            ValidateMatMulInputs(a, b, out m, out k, out n);
            if (output == null) throw new ArgumentNullException("output");
            if (ReferenceEquals(output, a) || ReferenceEquals(output, b))
                throw new ArgumentException("Output cannot alias an input tensor.", "output");
            if (output.Shape.Rank != 2 ||
                output.Shape.DimensionAt(0) != m ||
                output.Shape.DimensionAt(1) != n)
                throw new ArgumentException("Output shape must be (rows, columns).", "output");

            for (int row = 0; row < m; row++)
                for (int col = 0; col < n; col++)
                {
                    double sum = 0, correction = 0;
                    for (int inner = 0; inner < k; ++inner)
                    {
                        double value = a.At(row * k + inner) * b.At(inner * n + col);
                        double next = sum + value;
                        correction += Math.Abs(sum) >= Math.Abs(value)
                            ? (sum - next) + value : (value - next) + sum;
                        sum = next;
                    }
                    double total = sum + correction;
                    if (double.IsNaN(total) || double.IsInfinity(total))
                        throw new OverflowException("Matrix multiplication overflow.");
                    output.Put(row * n + col, total);
                }
        }

        private static void ValidateMatMulInputs(FNUTensorBuffer a, FNUTensorBuffer b,
            out int m, out int k, out int n)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (a.Shape.Rank != 2 || b.Shape.Rank != 2 ||
                a.Shape.DimensionAt(1) != b.Shape.DimensionAt(0))
                throw new ArgumentException(
                    "MatMul requires 2D tensors with a shared inner dimension.");
            m = a.Shape.DimensionAt(0);
            k = a.Shape.DimensionAt(1);
            n = b.Shape.DimensionAt(1);
        }

        /// <summary>
        /// Elementwise addition of read-only, potentially non-contiguous views,
        /// using trailing-axis broadcasting. Returns an independent buffer.
        /// </summary>
        public static FNUTensorBuffer AddViews(FNUTensorView a, FNUTensorView b)
        {
            return BinaryViews(a, b, false);
        }

        /// <summary>Elementwise multiplication of strided views with broadcasting.</summary>
        public static FNUTensorBuffer MultiplyViews(FNUTensorView a, FNUTensorView b)
        {
            return BinaryViews(a, b, true);
        }

        /// <summary>
        /// Add strided views into caller-owned output. Rejects any alias of an
        /// input backing buffer. Overflow may leave partially updated output.
        /// </summary>
        public static void AddViewsInto(FNUTensorView a, FNUTensorView b,
            FNUTensorBuffer output)
        {
            BinaryViewsInto(a, b, output, false);
        }

        /// <summary>Multiply strided views into caller-owned output.</summary>
        public static void MultiplyViewsInto(FNUTensorView a, FNUTensorView b,
            FNUTensorBuffer output)
        {
            BinaryViewsInto(a, b, output, true);
        }

        private static FNUTensorBuffer BinaryViews(FNUTensorView a, FNUTensorView b, bool multiply)
        {
            int[] dimensions = BroadcastViewDimensions(a, b);
            var output = new FNUTensorBuffer(new FNUTensorShape(dimensions));
            BinaryViewsInto(a, b, output, multiply);
            return output;
        }

        private static int[] BroadcastViewDimensions(FNUTensorView a, FNUTensorView b)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            int rank = Math.Max(a.Rank, b.Rank);
            var dimensions = new int[rank];
            for (int axis = 0; axis < rank; ++axis)
            {
                int la = axis - (rank - a.Rank), ra = axis - (rank - b.Rank);
                int left = la < 0 ? 1 : a.DimensionAt(la);
                int right = ra < 0 ? 1 : b.DimensionAt(ra);
                if (left != right && left != 1 && right != 1)
                    throw new ArgumentException("Tensor view dimensions cannot broadcast.");
                dimensions[axis] = left == 1 ? right : left;
            }
            return dimensions;
        }

        private static void BinaryViewsInto(FNUTensorView a, FNUTensorView b,
            FNUTensorBuffer output, bool multiply)
        {
            // All validation precedes writes, including backing-buffer aliases.
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (output == null) throw new ArgumentNullException("output");
            if (ReferenceEquals(output, a.Owner) || ReferenceEquals(output, b.Owner))
                throw new ArgumentException("Output aliases an input view's owner.", "output");

            int[] dimensions = BroadcastViewDimensions(a, b);
            if (output.Shape.Rank != dimensions.Length)
                throw new ArgumentException("Output rank differs from broadcast result.", "output");
            int rank = dimensions.Length;
            // Zero step indicates a broadcast singleton or absent leading axis.
            var aSteps = new long[rank];
            var bSteps = new long[rank];
            for (int axis = 0; axis < rank; ++axis)
            {
                if (output.Shape.DimensionAt(axis) != dimensions[axis])
                    throw new ArgumentException("Output shape differs from broadcast result.", "output");
                int la = axis - (rank - a.Rank), ra = axis - (rank - b.Rank);
                if (la >= 0 && a.DimensionAt(la) != 1) aSteps[axis] = a.StrideAt(la);
                if (ra >= 0 && b.DimensionAt(ra) != 1) bSteps[axis] = b.StrideAt(ra);
            }

            // A single logical cursor handles transposed/sliced and broadcast axes.
            // No per-element arrays, no per-element division or modulo.
            var coordinates = new int[rank];
            long aAddress = a.BaseOffset, bAddress = b.BaseOffset;
            int length = output.Shape.ElementCount;
            for (int flat = 0; flat < length; ++flat)
            {
                double left = a.ReadPhysical(aAddress), right = b.ReadPhysical(bAddress);
                double value = multiply ? left * right : left + right;
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new OverflowException("Tensor view arithmetic overflow.");
                output.Put(flat, value);
                if (flat + 1 == length) break;
                for (int axis = rank - 1; axis >= 0; --axis)
                {
                    coordinates[axis]++;
                    aAddress += aSteps[axis];
                    bAddress += bSteps[axis];
                    if (coordinates[axis] < dimensions[axis]) break;
                    aAddress -= (long)coordinates[axis] * aSteps[axis];
                    bAddress -= (long)coordinates[axis] * bSteps[axis];
                    coordinates[axis] = 0;
                }
            }
        }

        /// <summary>
        /// Sum a potentially non-contiguous view over one axis. Rank-one input
        /// returns shape [1], matching the existing buffer reduction contract.
        /// </summary>
        public static FNUTensorBuffer ReduceSumView(FNUTensorView view, int axis)
        {
            int[] dimensions = ViewReductionDimensions(view, axis);
            var result = new FNUTensorBuffer(new FNUTensorShape(dimensions));
            ReduceSumViewInto(view, axis, result);
            return result;
        }

        /// <summary>
        /// Accumulate a strided view along one axis into independent output.
        /// The destination must not own the input view's backing storage.
        /// Validation and accumulation finish before any output is written.
        /// </summary>
        public static void ReduceSumViewInto(FNUTensorView view, int axis,
            FNUTensorBuffer output)
        {
            int[] dimensions = ViewReductionDimensions(view, axis);
            if (output == null) throw new ArgumentNullException("output");
            if (ReferenceEquals(output, view.Owner))
                throw new ArgumentException("Output aliases the input view's owner.", "output");
            if (output.Shape.Rank != dimensions.Length)
                throw new ArgumentException("Output rank does not match reduced view.", "output");
            for (int i = 0; i < dimensions.Length; ++i)
                if (output.Shape.DimensionAt(i) != dimensions[i])
                    throw new ArgumentException("Output dimensions do not match reduced view.", "output");

            var sums = new double[output.Shape.ElementCount];
            var corrections = new double[sums.Length];
            var coordinates = new int[view.Rank];
            var outputSteps = new int[view.Rank];
            int outputAxis = 0;
            for (int axisIndex = 0; axisIndex < view.Rank; ++axisIndex)
                if (axisIndex != axis)
                    outputSteps[axisIndex] = output.Shape.StrideAt(outputAxis++);

            long address = view.BaseOffset;
            int target = 0, count = view.ElementCount;
            for (int flat = 0; flat < count; ++flat)
            {
                double value = view.ReadPhysical(address);
                double previous = sums[target], next = previous + value;
                corrections[target] += Math.Abs(previous) >= Math.Abs(value)
                    ? (previous - next) + value : (value - next) + previous;
                sums[target] = next;
                if (flat + 1 == count) break;
                for (int i = view.Rank - 1; i >= 0; --i)
                {
                    coordinates[i]++;
                    address += view.StrideAt(i);
                    target += outputSteps[i];
                    if (coordinates[i] < view.DimensionAt(i)) break;
                    address -= (long)coordinates[i] * view.StrideAt(i);
                    target -= coordinates[i] * outputSteps[i];
                    coordinates[i] = 0;
                }
            }
            // Read all inputs and reject overflow before modifying the caller's
            // output, including when an intermediate sum becomes non-finite.
            for (int i = 0; i < sums.Length; ++i)
            {
                double total = sums[i] + corrections[i];
                if (double.IsNaN(total) || double.IsInfinity(total))
                    throw new OverflowException("Tensor view axis reduction overflow.");
                sums[i] = total;
            }
            for (int i = 0; i < sums.Length; ++i) output.Put(i, sums[i]);
        }

        private static int[] ViewReductionDimensions(FNUTensorView view, int axis)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (axis < 0 || axis >= view.Rank) throw new ArgumentOutOfRangeException("axis");
            if (view.Rank == 1) return new int[] { 1 };
            var dimensions = new int[view.Rank - 1];
            int offset = 0;
            for (int i = 0; i < view.Rank; ++i)
                if (i != axis) dimensions[offset++] = view.DimensionAt(i);
            return dimensions;
        }

        /// <summary>Compensated sum with one coordinate cursor per invocation.</summary>
        public static double SumView(FNUTensorView view)
        {
            if (view == null) throw new ArgumentNullException("view");
            return view.SumCompensated();
        }

        public static FNUTensorBuffer MatMulViews(FNUTensorView a, FNUTensorView b)
        {
            int m, k, n;
            ValidateViewMatMul(a, b, out m, out k, out n);
            var output = new FNUTensorBuffer(new FNUTensorShape(m, n));
            MatMulViewsInto(a, b, output);
            return output;
        }

        /// <summary>Multiply arbitrary-stride views; overflow may partially update output.</summary>
        public static void MatMulViewsInto(FNUTensorView a, FNUTensorView b, FNUTensorBuffer output)
        {
            int m, k, n;
            ValidateViewMatMul(a, b, out m, out k, out n);
            if (output == null) throw new ArgumentNullException("output");
            if (ReferenceEquals(output, a.Owner) || ReferenceEquals(output, b.Owner))
                throw new ArgumentException("Output cannot alias backing input storage.", "output");
            if (output.Shape.Rank != 2 ||
                output.Shape.DimensionAt(0) != m || output.Shape.DimensionAt(1) != n)
                throw new ArgumentException("Output must have shape (m,n).", "output");
            // Physical-index cursors avoid repeated row/column multiplications and
            // public Get2D validation inside the innermost numerical loop.
            long aBase = a.BaseOffset, bBase = b.BaseOffset;
            int aRowStride = a.StrideAt(0), aColStride = a.StrideAt(1);
            int bRowStride = b.StrideAt(0), bColStride = b.StrideAt(1);
            for (int row = 0; row < m; ++row)
                for (int col = 0; col < n; ++col)
                {
                    long ai = aBase + (long)row * aRowStride;
                    long bi = bBase + (long)col * bColStride;
                    double sum = 0, correction = 0;
                    for (int inner = 0; inner < k; ++inner)
                    {
                        double value = a.ReadPhysical(ai) * b.ReadPhysical(bi);
                        double next = sum + value;
                        correction += Math.Abs(sum) >= Math.Abs(value)
                            ? (sum - next) + value : (value - next) + sum;
                        sum = next;
                        ai += aColStride;
                        bi += bRowStride;
                    }
                    double answer = sum + correction;
                    if (double.IsNaN(answer) || double.IsInfinity(answer))
                        throw new OverflowException("View matrix multiplication overflow.");
                    output.Put(row * n + col, answer);
                }
        }

        private static void ValidateViewMatMul(FNUTensorView a, FNUTensorView b,
            out int m, out int k, out int n)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (a.Rank != 2 || b.Rank != 2 || a.DimensionAt(1) != b.DimensionAt(0))
                throw new ArgumentException("View MatMul requires (m,k) and (k,n).");
            m = a.DimensionAt(0); k = a.DimensionAt(1); n = b.DimensionAt(1);
        }

        public static FNUTensorBuffer Transpose2D(FNUTensorBuffer input)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (input.Shape.Rank != 2) throw new ArgumentException("2D tensor required.", "input");
            int rows = input.Shape.DimensionAt(0), cols = input.Shape.DimensionAt(1);
            var result = new FNUTensorBuffer(new FNUTensorShape(cols, rows));
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                    result.Put(col * rows + row, input.At(row * cols + col));
            return result;
        }
    }
}
