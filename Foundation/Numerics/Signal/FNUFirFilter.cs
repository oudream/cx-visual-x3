using System;
using System.Collections.Generic;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>因果 FIR 卷积：y[n]=Σ b[k]x[n-k]，历史零填充。</summary>
    public static class FNUFirFilter
    {
        public static double[] Apply(IReadOnlyList<double> values, IReadOnlyList<double> coefficients)
        {
            if (values == null) throw new ArgumentNullException("values");
            var result = new double[values.Count];
            Write(values, coefficients, result);
            return result;
        }

        public static void Write(IReadOnlyList<double> values, IReadOnlyList<double> coefficients, double[] output)
        {
            if (values == null) throw new ArgumentNullException("values");
            if (coefficients == null) throw new ArgumentNullException("coefficients");
            if (output == null) throw new ArgumentNullException("output");
            if (coefficients.Count == 0) throw new ArgumentException("FIR coefficients must not be empty.", "coefficients");
            if (output.Length != values.Count) throw new ArgumentException("Output length mismatch.", "output");
            if (ReferenceEquals(values, output)) throw new ArgumentException("In-place filtering is not supported.", "output");
            for (int k = 0; k < coefficients.Count; k++) Check(coefficients[k], "coefficients");
            for (int i = 0; i < values.Count; i++) Check(values[i], "values");
            for (int i = 0; i < values.Count; i++)
            {
                double sum = 0;
                for (int k = 0; k < coefficients.Count && k <= i; k++)
                    sum += coefficients[k] * values[i - k];
                if (double.IsNaN(sum) || double.IsInfinity(sum)) throw new OverflowException("FIR overflow.");
                output[i] = sum;
            }
        }
        private static void Check(double x, string name)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
