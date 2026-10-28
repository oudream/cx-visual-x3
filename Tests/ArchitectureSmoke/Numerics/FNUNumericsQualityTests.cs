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
            AdvancedNumericsQuality();
            TensorShapeAndBufferReuse();
            TensorBroadcastInto();
            TensorViewQuality();
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

        private static void AdvancedNumericsQuality()
        {
            var random=new Random(20261027);
            for(int run=0;run<16;run++)
            {
                var taps=new double[1+run%7];
                for(int i=0;i<taps.Length;i++)taps[i]=random.NextDouble()-.5;
                var stream=new FNUFirStream(taps);
                var samples=new double[67];
                for(int i=0;i<samples.Length;i++)samples[i]=(random.NextDouble()-.5)*20;
                var batch=FNUFirFilter.Apply(samples,taps);
                taps[0]=123456; // coefficients are copied
                for(int i=0;i<samples.Length;i++)Near(stream.Process(samples[i]),batch[i],1e-11,"FIR stream agreement");
                stream.Reset();
                for(int i=0;i<samples.Length;i++)Near(stream.Process(samples[i]),batch[i],1e-11,"FIR stream reset");
            }
            var history=new FNUFirStream(new double[]{1,1});
            Near(history.Process(2),2,0,"FIR initial");
            Throws<ArgumentOutOfRangeException>(()=>history.Process(double.NaN),"FIR invalid sample");
            Near(history.Process(3),5,0,"FIR atomic invalid sample");
            var overflow=new FNUFirStream(new double[]{double.MaxValue,1});
            Throws<OverflowException>(()=>overflow.Process(2),"FIR overflow");
            Near(overflow.Process(0),0,0,"FIR atomic overflow");
            var tensor=new FNUTensorBuffer(new FNUTensorShape(2,3),
                new double[]{1e16,1,-1e16,7,8,9});
            var reduced=FNUTensorMath.ReduceSum(tensor,1);
            Near(reduced.Get(0),1,0,"Neumaier axis cancellation");
            Near(reduced.Get(1),24,0,"Neumaier axis normal");
            var multiplied=FNUTensorMath.MatMul(tensor,
                new FNUTensorBuffer(new FNUTensorShape(3,1),new double[]{1,1,1}));
            Near(multiplied.Get(0,0),1,0,"Neumaier MatMul cancellation");
            Near(multiplied.Get(1,0),24,0,"Neumaier MatMul normal");
            var empty=FNUTensorMath.ReduceSum(new FNUTensorBuffer(new FNUTensorShape(0,3)),0);
            Check(empty.Shape.ElementCount==3,"empty axis output");
            Near(FNUTensorMath.Sum(empty),0,0,"empty axis sum");
            var fit=FNULinearAlgebra.LeastSquares(new double[,]{{1000,0},{0,1}},
                new double[]{2000,3});
            Near(fit.ConditionNumber,1000,1e-8,"SVD condition");
            Near(fit.Coefficients[0],2,1e-12,"SVD scaled coefficient");
        }

        private static void TensorShapeAndBufferReuse()
        {
            // Empty shapes are valid even with giant dimensions: nothing can be
            // indexed, so their hypothetical strides must never overflow.
            var emptyFirst = new FNUTensorShape(0, int.MaxValue, int.MaxValue);
            var emptyMiddle = new FNUTensorShape(int.MaxValue, 0, int.MaxValue);
            Check(emptyFirst.ElementCount == 0 && emptyMiddle.ElementCount == 0,
                "empty giant shapes");
            Check(emptyFirst.Rank == 3 && emptyMiddle.Rank == 3,
                "empty giant rank");
            Check(new FNUTensorShape(2, 0, 3).StrideAt(1) == 3,
                "empty axis contiguous trailing stride");
            Throws<OverflowException>(() => new FNUTensorShape(int.MaxValue, 2),
                "nonempty giant shape rejects");
            Throws<ArgumentOutOfRangeException>(() => emptyFirst.Offset(0, 0, 0),
                "empty tensor cannot be indexed");

            var a = new FNUTensorBuffer(new FNUTensorShape(2, 3),
                new double[] { 1e16, 1, -1e16, 1, 2, 3 });
            var b = new FNUTensorBuffer(new FNUTensorShape(3, 2),
                new double[] { 1, 0, 1, 1, 1, 2 });
            var output = new FNUTensorBuffer(new FNUTensorShape(2, 2));
            FNUTensorMath.MatMulInto(a, b, output);
            Near(output.Get(0, 0), 1, 0, "MatMulInto compensated column");
            Near(output.Get(1, 0), 6, 0, "MatMulInto normal column");
            Near(output.Get(1, 1), 8, 0, "MatMulInto second column");
            var allocated = FNUTensorMath.MatMul(a, b);
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 2; col++)
                    Near(output.Get(row, col), allocated.Get(row, col), 0,
                        "MatMulInto equals MatMul");

            // The same output can be safely used by a subsequent operation.
            var identity = new FNUTensorBuffer(new FNUTensorShape(3, 3),
                new double[] { 1,0,0, 0,1,0, 0,0,1 });
            Throws<ArgumentException>(() => FNUTensorMath.MatMulInto(a, identity, a),
                "MatMulInto alias rejected");
            Throws<ArgumentException>(() => FNUTensorMath.MatMulInto(a, b,
                new FNUTensorBuffer(new FNUTensorShape(1, 4))),
                "MatMulInto shape rejected");
            // Verify destination remains unchanged after argument validation.
            Near(output.Get(0, 0), 1, 0, "invalid calls preserve destination");

            var random = new Random(20261028);
            for (int iteration = 0; iteration < 30; iteration++)
            {
                int rows = 1 + random.Next(5);
                int inner = 1 + random.Next(6);
                int cols = 1 + random.Next(4);
                var leftData = new double[rows * inner];
                var rightData = new double[inner * cols];
                for (int i = 0; i < leftData.Length; i++)
                    leftData[i] = random.NextDouble() * 2 - 1;
                for (int i = 0; i < rightData.Length; i++)
                    rightData[i] = random.NextDouble() * 2 - 1;
                var left = new FNUTensorBuffer(new FNUTensorShape(rows, inner), leftData);
                var right = new FNUTensorBuffer(new FNUTensorShape(inner, cols), rightData);
                var destination = new FNUTensorBuffer(new FNUTensorShape(rows, cols));
                FNUTensorMath.MatMulInto(left, right, destination);
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        double reference = 0;
                        for (int k = 0; k < inner; k++)
                            reference += leftData[i * inner + k] * rightData[k * cols + j];
                        Near(destination.Get(i,j), reference, 1e-12,
                            "random MatMulInto reference");
                    }
            }
            Console.WriteLine("PASS FNU tensor shape & MatMulInto: empty giant shape / zero alias / seeded reference");
        }

        private static void TensorBroadcastInto()
        {
            var source = new FNUTensorBuffer(new FNUTensorShape(2, 3),
                new double[] { 1, 2, 3, 4, 5, 6 });
            var row = new FNUTensorBuffer(new FNUTensorShape(3),
                new double[] { 10, 20, 30 });
            var output = new FNUTensorBuffer(new FNUTensorShape(2, 3));
            FNUTensorMath.AddInto(source, row, output);
            Near(output.Get(0, 0), 11, 0, "AddInto first");
            Near(output.Get(1, 2), 36, 0, "AddInto last");
            var allocated = FNUTensorMath.Add(source, row);
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 3; j++)
                    Near(output.Get(i, j), allocated.Get(i, j), 0, "AddInto reference");

            FNUTensorMath.MultiplyInto(source, row, output);
            Near(output.Get(0, 0), 10, 0, "MultiplyInto first");
            Near(output.Get(1, 2), 180, 0, "MultiplyInto last");
            var allocatedProduct = FNUTensorMath.Multiply(source, row);
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 3; j++)
                    Near(output.Get(i, j), allocatedProduct.Get(i, j), 0,
                        "MultiplyInto reference");

            // Pre-validation failures must not change the previously computed output.
            Throws<ArgumentException>(() => FNUTensorMath.AddInto(source, row, source),
                "AddInto alias");
            Throws<ArgumentException>(() => FNUTensorMath.MultiplyInto(source, row,
                new FNUTensorBuffer(new FNUTensorShape(3, 2))), "MultiplyInto shape");
            Near(output.Get(1, 2), 180, 0, "output unchanged after rejected call");

            var zero = new FNUTensorBuffer(new FNUTensorShape(0, 3));
            var emptyOutput = new FNUTensorBuffer(new FNUTensorShape(0, 3));
            FNUTensorMath.AddInto(zero, row, emptyOutput);
            Check(emptyOutput.Shape.ElementCount == 0, "empty broadcasting output");

            var leftEmpty = new FNUTensorBuffer(new FNUTensorShape(2, 0));
            var rightEmpty = new FNUTensorBuffer(new FNUTensorShape(0, 3));
            var zeroDot = new FNUTensorBuffer(new FNUTensorShape(2, 3));
            FNUTensorMath.MatMulInto(leftEmpty, rightEmpty, zeroDot);
            Near(FNUTensorMath.Sum(zeroDot), 0, 0, "zero-inner-dimension matmul");
            Console.WriteLine("PASS FNU tensor Into: add/multiply broadcasting and zero-size cases");
        }

        private static void TensorViewQuality()
        {
            var owner = new FNUTensorBuffer(new FNUTensorShape(2,3),
                new double[] { 1,2,3,4,5,6 });
            var original = owner.AsView();
            var transposed = original.Transpose(0,1);
            Check(transposed.Rank == 2 && transposed.DimensionAt(0) == 3 &&
                transposed.DimensionAt(1) == 2, "view transpose shape");
            Check(transposed.StrideAt(0) == 1 && transposed.StrideAt(1) == 3,
                "view stride permutation");
            Near(transposed.Get2D(2,1),6,0,"transposed index");
            for (int i = 0; i < transposed.ElementCount; ++i)
                Near(transposed.GetFlat(i),transposed.Get2D(i/2,i%2),0,"view flat index");
            var slice = transposed.Slice(0,1,2);
            Near(slice.Get2D(0,1),5,0,"view slice index");
            Near(slice.Get2D(1,0),3,0,"view slice last");
            var snapshot = slice.CopyToBuffer();
            owner.Set(50,1,1);
            Near(slice.Get2D(0,1),50,0,"live view sees owner update");
            Near(snapshot.Get(0,1),5,0,"explicit snapshot is independent");
            Near(FNUTensorMath.SumView(slice),61,0,"sum noncontiguous view");
            Near(original.Transpose(0,1).Transpose(0,1).Get(1,1),50,0,
                "double transpose");

            var data = new FNUTensorBuffer(new FNUTensorShape(2,3),
                new double[] { 1,2,3,4,5,6 });
            var left = data.AsView().Transpose(0,1);
            var right = data.AsView();
            var product = FNUTensorMath.MatMulViews(left,right);
            Check(product.Shape.DimensionAt(0)==3 && product.Shape.DimensionAt(1)==3,
                "view matrix product shape");
            for (int row=0;row<3;row++)
                for (int col=0;col<3;col++)
                {
                    double value=0;
                    for(int k=0;k<2;k++)
                        value+=left.Get2D(row,k)*right.Get2D(k,col);
                    Near(product.Get(row,col),value,1e-12,"view MatMul oracle");
                }
            var outBuffer = new FNUTensorBuffer(new FNUTensorShape(3,3));
            FNUTensorMath.MatMulViewsInto(left,right,outBuffer);
            Near(outBuffer.Get(0,0),product.Get(0,0),0,"view MatMulInto");
            Near(outBuffer.Get(2,2),product.Get(2,2),0,"view MatMulInto last");
            Throws<ArgumentException>(()=>FNUTensorMath.MatMulViewsInto(left,right,data),
                "view output alias");
            Throws<ArgumentOutOfRangeException>(()=>transposed.Get2D(3,0),
                "view bounds");
            Throws<ArgumentException>(()=>original.Get(0),"view wrong index rank");
            Throws<ArgumentOutOfRangeException>(()=>original.Slice(1,3,1),
                "view invalid slice");
            var empty=original.Slice(1,3,0);
            Check(empty.ElementCount==0,"view empty slice");
            Throws<ArgumentOutOfRangeException>(()=>empty.GetFlat(0),"empty view access");
            Near(FNUTensorMath.SumView(empty),0,0,"empty view sum");
            Console.WriteLine("PASS FNU read-only live view / slice / transpose / matmul");
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
