using System;
namespace CxVisual.Foundation.Numerics
{
    public static class FNUWindowFunction
    {
        /// <summary>对称 Hann 窗。长度1定义为1，长度2的两个端点均为0。</summary>
        public static double[] Hann(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException("count");
            var values = new double[count];
            if (count == 1) { values[0] = 1; return values; }
            for (int i = 0; i < count; i++)
                values[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (count - 1)));
            return values;
        }
    }
}
