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
