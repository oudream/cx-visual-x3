using System;
using System.Collections.Generic;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
namespace CxVisual.Foundation.Numerics {
    /// <summary>Matlab约定：正变换不归一化，逆变换除以N；不修改调用者数据。</summary>
    public static class FNUFourier {
        public static Complex[] ForwardReal(IReadOnlyList<double> values) {
            if(values==null)throw new ArgumentNullException("values");
            if(values.Count==0)throw new ArgumentException("Empty signal","values");
            var data=new Complex[values.Count];
            for(int i=0;i<values.Count;i++){
                if(double.IsNaN(values[i])||double.IsInfinity(values[i]))
                    throw new ArgumentOutOfRangeException("values");
                data[i]=new Complex(values[i],0);
            }
            Fourier.Forward(data,FourierOptions.Matlab);return data;
        }
        public static Complex[] Inverse(IReadOnlyList<Complex> spectrum) {
            if(spectrum==null)throw new ArgumentNullException("spectrum");
            if(spectrum.Count==0)throw new ArgumentException("Empty spectrum","spectrum");
            var data=new Complex[spectrum.Count];
            for(int i=0;i<spectrum.Count;i++){
                Complex c=spectrum[i];
                if(double.IsNaN(c.Real)||double.IsNaN(c.Imaginary)||
                    double.IsInfinity(c.Real)||double.IsInfinity(c.Imaginary))
                    throw new ArgumentOutOfRangeException("spectrum");
                data[i]=c;
            }
            Fourier.Inverse(data,FourierOptions.Matlab);return data;
        }
    }
}
