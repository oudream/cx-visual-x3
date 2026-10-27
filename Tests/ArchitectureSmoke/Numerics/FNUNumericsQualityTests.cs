using System;
using System.Numerics;
using CxVisual.Foundation.Numerics;

namespace CxVisual.Tests.ArchitectureSmoke
{
    // Deterministic independent numerical oracles and boundary checks.
    internal static class FNUNumericsQualityTests
    {
        public static void Run()
        {
            InterpolationExtremes();
            CancellationPrecision();
            FourierAgainstNaiveDft();
            ConvolutionAndFirAgreement();
            TensorIndexAndOperations();
            Console.WriteLine("PASS FNU numerical-quality: extremes / cancellation / DFT / FIR / tensor");
        }

        private static void InterpolationExtremes()
        {
            Near(FNUInterpolation.Linear(-1e308, 1e308, 0.5), 0, 0, "finite convex blend");
            Near(FNUInterpolation.Linear(1e308, 1e308, .5), 1e308, 1e294, "large equal ends");
            Near(FNUInterpolation.Linear(-1e308, 1e308, 0), -1e308, 0, "large t=0");
            Near(FNUInterpolation.Linear(-1e308, 1e308, 1), 1e308, 0, "large t=1");
            Throws<OverflowException>(() => FNUInterpolation.Linear(1e308, -1e308, 3),
                "genuinely overflowing extrapolation");
            var random = new Random(113);
            for (int i = 0; i < 200; i++)
            {
                double a = random.NextDouble() * 100 - 50, b = random.NextDouble() * 100 - 50;
                double t = random.NextDouble();
                Near(FNUInterpolation.Linear(a,b,t),a+(b-a)*t,1e-12,"original arithmetic");
            }
        }

        private static void CancellationPrecision()
        {
            var tensor = new FNUTensorBuffer(new FNUTensorShape(3),
                new double[] { 1e16, 1, -1e16 });
            Near(FNUTensorMath.Sum(tensor),1,0,"Neumaier cancellation");
            Near(FNUTensorMath.Sum(new FNUTensorBuffer(new FNUTensorShape(0))),0,0,"empty sum");
        }

        private static void FourierAgainstNaiveDft()
        {
            var random = new Random(43);
            foreach (int length in new int[] { 1, 3, 4, 7, 8, 11, 16 })
            {
                double[] data = new double[length];
                for(int i=0;i<length;i++) data[i]=random.NextDouble()*2-1;
                Complex[] actual=FNUFourier.ForwardReal(data);
                for(int k=0;k<length;k++)
                {
                    Complex expected=Complex.Zero;
                    for(int i=0;i<length;i++)
                    {
                        double phase=-2*Math.PI*i*k/length;
                        expected+=new Complex(Math.Cos(phase),Math.Sin(phase))*data[i];
                    }
                    Near((actual[k]-expected).Magnitude,0,1e-8,"naive DFT length "+length);
                }
                Complex[] inverted=FNUFourier.Inverse(actual);
                for(int i=0;i<length;i++)
                {
                    Near(inverted[i].Real,data[i],1e-9,"FFT roundtrip real");
                    Near(inverted[i].Imaginary,0,1e-9,"FFT roundtrip imaginary");
                }
            }
        }

        private static void ConvolutionAndFirAgreement()
        {
            var random=new Random(241);
            for(int n=1;n<=12;n++)
            {
                double[] samples=new double[n], coefficients=new double[4];
                for(int i=0;i<n;i++)samples[i]=random.NextDouble()-.5;
                for(int i=0;i<4;i++)coefficients[i]=random.NextDouble()-.5;
                double[] causal=FNUFirFilter.Apply(samples,coefficients);
                double[] full=FNUConvolution.Full(samples,coefficients);
                for(int i=0;i<n;i++)Near(causal[i],full[i],1e-12,"FIR convolution reference");
            }
        }

        private static void TensorIndexAndOperations()
        {
            var shape=new FNUTensorShape(2,3,4);
            for(int i=0;i<2;i++)
                for(int j=0;j<3;j++)
                    for(int k=0;k<4;k++)
                        Check(shape.Offset(i,j,k)==i*12+j*4+k,"3D offset");
            var values=new double[24];
            for(int i=0;i<24;i++)values[i]=i;
            var src=new FNUTensorBuffer(shape,values);
            var cut=src.Slice(1,1,2);
            Check(cut.Shape.ElementCount==16,"axis1 slice shape");
            Near(cut.Get(1,1,3),src.Get(1,2,3),0,"axis1 slice value");
            var rhs=new FNUTensorBuffer(new FNUTensorShape(4),new double[]{2,3,4,5});
            var multiplied=FNUTensorMath.Multiply(src,rhs);
            for(int i=0;i<2;i++)
                for(int j=0;j<3;j++)
                    for(int k=0;k<4;k++)
                        Near(multiplied.Get(i,j,k),src.Get(i,j,k)*(k+2),1e-12,"broadcast");
            var reduced=FNUTensorMath.ReduceSum(src,1);
            for(int i=0;i<2;i++)
                for(int k=0;k<4;k++)
                    Near(reduced.Get(i,k),src.Get(i,0,k)+src.Get(i,1,k)+src.Get(i,2,k),
                        1e-12,"axis1 reduction");
        }

        private static void Near(double value,double expected,double tolerance,string what)
        {
            if(double.IsNaN(value)||double.IsInfinity(value)||Math.Abs(value-expected)>tolerance)
                throw new InvalidOperationException("Numerics quality failed: "+what+
                    " actual="+value+" expected="+expected);
        }
        private static void Check(bool ok,string what)
        {
            if(!ok)throw new InvalidOperationException("Numerics quality failed: "+what);
        }
        private static void Throws<T>(Action action,string what) where T:Exception
        {
            try{action();}catch(T){return;}
            throw new InvalidOperationException("Numerics quality failed: missing "+typeof(T).Name+" for "+what);
        }
    }
}
