using System;
using System.Collections.Generic;
namespace CxVisual.Foundation.Numerics
{
    public static class FNUConvolution
    {
        /// <summary>完整离散卷积：结果长度N+M-1，不裁剪输出；空输入拒绝。</summary>
        public static double[] Full(IReadOnlyList<double> signal, IReadOnlyList<double> kernel)
        {
            if (signal == null) throw new ArgumentNullException("signal");
            if (kernel == null) throw new ArgumentNullException("kernel");
            if (signal.Count == 0 || kernel.Count == 0) throw new ArgumentException("Convolution inputs must be nonempty.");
            int size = checked(signal.Count + kernel.Count - 1);
            for (int i = 0; i < signal.Count; i++) Check(signal[i]);
            for (int i = 0; i < kernel.Count; i++) Check(kernel[i]);
            var result = new double[size];
            for (int i = 0; i < signal.Count; i++)
                for (int j = 0; j < kernel.Count; j++)
                    result[i + j] += signal[i] * kernel[j];
            for (int i = 0; i < size; i++)
                if (double.IsInfinity(result[i]) || double.IsNaN(result[i]))
                    throw new OverflowException("Convolution overflow.");
            return result;
        }
        private static void Check(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException("signal");
        }
    }
}
