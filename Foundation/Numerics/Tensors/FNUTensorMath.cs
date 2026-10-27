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
        private static FNUTensorBuffer Binary(FNUTensorBuffer a, FNUTensorBuffer b, bool multiply)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            int rank = Math.Max(a.Shape.Rank, b.Shape.Rank);
            var dimensions = new int[rank];
            for (int axis = 0; axis < rank; axis++)
            {
                int leftAxis = axis - (rank - a.Shape.Rank);
                int rightAxis = axis - (rank - b.Shape.Rank);
                int left = leftAxis < 0 ? 1 : a.Shape.DimensionAt(leftAxis);
                int right = rightAxis < 0 ? 1 : b.Shape.DimensionAt(rightAxis);
                if (left != right && left != 1 && right != 1)
                    throw new ArgumentException("Tensor dimensions cannot broadcast.");
                dimensions[axis] = left == 1 ? right : left;
            }
            var output = new FNUTensorBuffer(new FNUTensorShape(dimensions));
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
            return output;
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
            for (int flat = 0; flat < input.Shape.ElementCount; flat++)
            {
                int remaining = flat, target = 0, axisOut = 0;
                for (int k = 0; k < input.Shape.Rank; k++)
                {
                    int coordinate = remaining / input.Shape.StrideAt(k);
                    remaining %= input.Shape.StrideAt(k);
                    if (k != axis) target += coordinate * output.Shape.StrideAt(axisOut++);
                }
                double value = output.At(target) + input.At(flat);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new OverflowException("Tensor reduction overflow.");
                output.Put(target, value);
            }
            return output;
        }

        /// <summary>二维矩阵乘法：左(m,k) × 右(k,n) -> (m,n)。</summary>
        public static FNUTensorBuffer MatMul(FNUTensorBuffer a, FNUTensorBuffer b)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (a.Shape.Rank != 2 || b.Shape.Rank != 2 ||
                a.Shape.DimensionAt(1) != b.Shape.DimensionAt(0))
                throw new ArgumentException("MatMul requires 2D tensors with a shared inner dimension.");
            int m = a.Shape.DimensionAt(0), k = a.Shape.DimensionAt(1), n = b.Shape.DimensionAt(1);
            var result = new FNUTensorBuffer(new FNUTensorShape(m, n));
            for (int row = 0; row < m; row++)
                for (int col = 0; col < n; col++)
                {
                    double sum = 0;
                    for (int inner = 0; inner < k; inner++)
                        sum += a.At(row * k + inner) * b.At(inner * n + col);
                    if (double.IsNaN(sum) || double.IsInfinity(sum))
                        throw new OverflowException("Matrix multiplication overflow.");
                    result.Put(row * n + col, sum);
                }
            return result;
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
