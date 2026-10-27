using System;
using System.Collections.Generic;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>Stateful zero-per-sample-allocation FIR; caller-owned, not thread-safe.</summary>
    public sealed class FNUFirStream
    {
        private readonly double[] _coefficients, _history;
        private int _next;
        public FNUFirStream(IReadOnlyList<double> coefficients)
        {
            if (coefficients == null) throw new ArgumentNullException("coefficients");
            if (coefficients.Count == 0) throw new ArgumentException("Empty coefficients.", "coefficients");
            _coefficients = new double[coefficients.Count];
            _history = new double[coefficients.Count];
            for (int i = 0; i < coefficients.Count; ++i)
            {
                double value = coefficients[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentOutOfRangeException("coefficients");
                _coefficients[i] = value;
            }
        }
        public int TapCount { get { return _coefficients.Length; } }
        /// <summary>On invalid input or overflow the prior state remains unchanged.</summary>
        public double Process(double sample)
        {
            if (double.IsNaN(sample) || double.IsInfinity(sample))
                throw new ArgumentOutOfRangeException("sample");
            double value = _coefficients[0] * sample;
            for (int k = 1; k < _coefficients.Length; k++)
            {
                int index = _next - k;
                if (index < 0) index += _history.Length;
                value += _coefficients[k] * _history[index];
            }
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new OverflowException("FIR output overflow.");
            _history[_next] = sample;
            _next = (_next + 1) % _history.Length;
            return value;
        }
        public void Reset() { Array.Clear(_history, 0, _history.Length); _next = 0; }
    }
}
