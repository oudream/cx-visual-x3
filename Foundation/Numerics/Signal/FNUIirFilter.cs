using System;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>有状态 IIR 直接型 I。分子 b[0..]、分母 a[0..]，构造时按 a[0] 归一化。非线程安全。</summary>
    public sealed class FNUIirFilter
    {
        private readonly double[] _b, _a, _inputs, _outputs;

        public FNUIirFilter(double[] numerator, double[] denominator)
        {
            if (numerator == null) throw new ArgumentNullException("numerator");
            if (denominator == null) throw new ArgumentNullException("denominator");
            if (numerator.Length == 0 || denominator.Length == 0)
                throw new ArgumentException("IIR requires numerator and denominator.");
            if (double.IsNaN(denominator[0]) || double.IsInfinity(denominator[0]) ||
                denominator[0] == 0) throw new ArgumentOutOfRangeException("denominator");
            double divisor = denominator[0];
            _b = new double[numerator.Length]; _a = new double[denominator.Length];
            for (int i = 0; i < numerator.Length; i++)
            {
                Check(numerator[i], "numerator");
                _b[i] = numerator[i] / divisor;
                Check(_b[i], "numerator");
            }
            for (int i = 0; i < denominator.Length; i++)
            {
                Check(denominator[i], "denominator");
                _a[i] = denominator[i] / divisor;
                Check(_a[i], "denominator");
            }
            _inputs = new double[_b.Length - 1];
            _outputs = new double[_a.Length - 1];
        }

        public double Process(double sample)
        {
            Check(sample, "sample");
            double value = _b[0] * sample;
            for (int i = 1; i < _b.Length; i++) value += _b[i] * _inputs[i - 1];
            for (int i = 1; i < _a.Length; i++) value -= _a[i] * _outputs[i - 1];
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new OverflowException("IIR output overflow.");
            for (int i = _inputs.Length - 1; i > 0; i--) _inputs[i] = _inputs[i - 1];
            for (int i = _outputs.Length - 1; i > 0; i--) _outputs[i] = _outputs[i - 1];
            if (_inputs.Length != 0) _inputs[0] = sample;
            if (_outputs.Length != 0) _outputs[0] = value;
            return value;
        }

        public void Reset()
        {
            Array.Clear(_inputs, 0, _inputs.Length);
            Array.Clear(_outputs, 0, _outputs.Length);
        }
        private static void Check(double x, string name)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
