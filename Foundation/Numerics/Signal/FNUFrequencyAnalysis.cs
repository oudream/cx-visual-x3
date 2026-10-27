using System;
using System.Collections.Generic;
using System.Numerics;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>实数采样的一侧振幅频谱；包含DC和可用时的Nyquist点。</summary>
    public sealed class FNUFrequencySpectrum
    {
        private readonly double[] _frequencies, _amplitudes;
        internal FNUFrequencySpectrum(double[] frequencies, double[] amplitudes)
        {
            _frequencies = frequencies; _amplitudes = amplitudes;
        }
        public int Length { get { return _frequencies.Length; } }
        public double[] CopyFrequencies() { return (double[])_frequencies.Clone(); }
        public double[] CopyAmplitudes() { return (double[])_amplitudes.Clone(); }
        public double FrequencyAt(int index) { return _frequencies[index]; }
        public double AmplitudeAt(int index) { return _amplitudes[index]; }
    }

    public static class FNUFrequencyAnalysis
    {
        /// <summary>以真实Hz采样频率提供幅值；单边谱乘2，但DC与偶数长度Nyquist不乘2。</summary>
        public static FNUFrequencySpectrum OneSidedAmplitude(IReadOnlyList<double> samples, double sampleRate)
        {
            if (samples == null) throw new ArgumentNullException("samples");
            if (double.IsNaN(sampleRate) || double.IsInfinity(sampleRate) || sampleRate <= 0)
                throw new ArgumentOutOfRangeException("sampleRate");
            Complex[] data = FNUFourier.ForwardReal(samples);
            int n = data.Length, bins = n / 2 + 1;
            var frequencies = new double[bins];
            var amplitudes = new double[bins];
            for (int i = 0; i < bins; i++)
            {
                frequencies[i] = (sampleRate / n) * i;
                double value = data[i].Magnitude / n;
                if (i != 0 && !(n % 2 == 0 && i == n / 2)) value *= 2;
                if (double.IsInfinity(value) || double.IsNaN(value) ||
                    double.IsInfinity(frequencies[i]) || double.IsNaN(frequencies[i]))
                    throw new OverflowException("Spectrum overflow.");
                amplitudes[i] = value;
            }
            return new FNUFrequencySpectrum(frequencies, amplitudes);
        }
    }
}
