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
            SolverConditionAndViewTraversal();
            TensorViewBroadcastArithmetic();
            TensorViewAxisReduction();
            RandomizedViewArithmeticAndReduction();
            RandomizedContiguousBroadcast();
            ContiguousReductionAndSliceQuality();
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

        private static void SolverConditionAndViewTraversal()
        {
            double[,] illConditioned = new double[,] { { 1, 0 }, { 0, 1e-8 }, { 0, 0 } };
            double[] observations = new double[] { 2, 3e-8, 0 };
            var permitted = FNULinearAlgebra.LeastSquares(illConditioned, observations);
            Near(permitted.ConditionNumber, 1e8, 1, "diagnose ill-conditioned matrix");
            Near(permitted.Coefficients[0], 2, 1e-10, "ill-conditioned coefficient one");
            Near(permitted.Coefficients[1], 3, 1e-8, "ill-conditioned coefficient two");
            var accepted = FNULinearAlgebra.LeastSquares(illConditioned, observations, 1e9);
            Near(accepted.Coefficients[1], 3, 1e-8, "explicit condition guard permits");
            Throws<InvalidOperationException>(() => FNULinearAlgebra.LeastSquares(
                illConditioned, observations, 1e6), "guard rejects ill-conditioned matrix");
            Throws<ArgumentOutOfRangeException>(() => FNULinearAlgebra.LeastSquares(
                illConditioned, observations, 0.5), "condition limit below one");
            Throws<ArgumentOutOfRangeException>(() => FNULinearAlgebra.LeastSquares(
                illConditioned, observations, double.NaN), "NaN condition limit");
            Throws<InvalidOperationException>(() => FNULinearAlgebra.LeastSquares(
                new double[,] { { 1, 1 }, { 2, 2 }, { 3, 3 } },
                new double[] { 1, 2, 3 }, 1e9), "exact rank deficiency remains rejected");

            var random = new Random(20261028);
            for (int trial = 0; trial < 50; trial++)
            {
                int x = 1 + random.Next(5), y = 1 + random.Next(4), z = 1 + random.Next(6);
                var samples = new double[x*y*z];
                for (int i = 0; i < samples.Length; ++i)
                    samples[i] = random.NextDouble()*2 - 1;
                var owner = new FNUTensorBuffer(new FNUTensorShape(x,y,z), samples);
                var view = owner.AsView().Transpose(0,2).Slice(1,0,y);
                var copied = view.CopyToBuffer();
                Check(copied.Shape.ElementCount == view.ElementCount, "view copy dimensions");
                double reference = 0, correction = 0;
                double[] copiedValues = copied.CopyValues();
                for (int i = 0; i < view.ElementCount; ++i)
                {
                    double v = view.GetFlat(i);
                    Near(copiedValues[i], v, 0, "view cursor matches flat oracle");
                    double next = reference + v;
                    correction += Math.Abs(reference) >= Math.Abs(v)
                        ? (reference-next)+v : (v-next)+reference;
                    reference = next;
                }
                Near(FNUTensorMath.SumView(view), reference+correction,
                    1e-11, "cursor sum versus independent flat oracle");
            }
            var baseBuffer = new FNUTensorBuffer(new FNUTensorShape(2,3),
                new double[] { 1e16, 1, -1e16, 7, 8, 9 });
            var transposed = baseBuffer.AsView().Transpose(0,1);
            Near(FNUTensorMath.SumView(transposed),25,0,
                "cursor sum preserves cancellation accuracy");
            // Independent ridge oracle for a diagonal design: (A^T A+lambda I)x=A^T b.
            var ridge = FNULinearAlgebra.RidgeLeastSquares(
                new double[,] { { 1, 0 }, { 0, 2 } },
                new double[] { 3, 8 }, 1);
            Near(ridge.Coefficients[0], 1.5, 1e-12, "ridge diagonal first coefficient");
            Near(ridge.Coefficients[1], 3.2, 1e-12, "ridge diagonal second coefficient");
            Check(ridge.Rank == 2, "ridge full rank");
            var singularRidge = FNULinearAlgebra.RidgeLeastSquares(
                new double[,] { { 1, 1 }, { 2, 2 } },
                new double[] { 2, 4 }, 2);
            Near(singularRidge.Coefficients[0], 5.0/6, 1e-12, "ridge rank-deficient coefficient a");
            Near(singularRidge.Coefficients[1], 5.0/6, 1e-12, "ridge rank-deficient coefficient b");
            Check(singularRidge.Rank < 2 && double.IsPositiveInfinity(singularRidge.ConditionNumber),
                "ridge reports rank deficiency honestly");
            var zeroDesign = FNULinearAlgebra.RidgeLeastSquares(
                new double[,] { { 0, 0 }, { 0, 0 } },
                new double[] { 4, 5 }, 1);
            Near(zeroDesign.Coefficients[0], 0, 0, "zero-design ridge first");
            Near(zeroDesign.Coefficients[1], 0, 0, "zero-design ridge second");
            Check(double.IsPositiveInfinity(zeroDesign.ConditionNumber), "zero-design rank");
            Throws<ArgumentOutOfRangeException>(() => FNULinearAlgebra.RidgeLeastSquares(
                new double[,]{{1}}, new double[]{1}, 0), "ridge requires positive lambda");
            Throws<ArgumentOutOfRangeException>(() => FNULinearAlgebra.RidgeLeastSquares(
                new double[,]{{1}}, new double[]{1}, double.NaN), "ridge rejects NaN lambda");
            Throws<ArgumentOutOfRangeException>(() => FNULinearAlgebra.RidgeLeastSquares(
                new double[,]{{1}}, new double[]{1}, double.PositiveInfinity), "ridge rejects infinite lambda");
            var wide = FNULinearAlgebra.RidgeLeastSquares(
                new double[,]{{1,1}}, new double[]{2}, 2);
            Near(wide.Coefficients[0], 0.5, 1e-12, "ridge underdetermined coefficient a");
            Near(wide.Coefficients[1], 0.5, 1e-12, "ridge underdetermined coefficient b");
            Check(wide.Rank == 1 && double.IsPositiveInfinity(wide.ConditionNumber),
                "wide rank-deficient design reports infinite condition");
            Throws<ArgumentException>(() => FNULinearAlgebra.RidgeLeastSquares(
                new double[,]{{1,2}}, new double[]{1,2}, 1),
                "ridge observations length mismatch");
            var ridgeSamples = new double[,]{{1,0},{0,2}};
            var ridgeY = new double[]{3,8};
            FNULinearAlgebra.RidgeLeastSquares(ridgeSamples, ridgeY, 1);
            Near(ridgeSamples[1,1],2,0,"ridge does not mutate design");
            Near(ridgeY[1],8,0,"ridge does not mutate observations");
            // A non-contiguous submatrix times a transposed, shared backing
            // view must produce the same answer as independent index arithmetic.
            var matrixSource = new FNUTensorBuffer(new FNUTensorShape(4,5),
                new double[] { 1,2,3,4,5, 6,7,8,9,10, 11,12,13,14,15, 16,17,18,19,20 });
            var leftView = matrixSource.AsView().Slice(0,1,2).Slice(1,1,3);
            var rightView = matrixSource.AsView().Slice(0,0,3).Slice(1,0,2).Transpose(0,1);
            // left: (2,3), right: (2,3) -> invalid; explicitly verify guard.
            Throws<ArgumentException>(() => FNUTensorMath.MatMulViews(leftView,rightView),
                "incompatible strided view multiplication");
            var compatible = matrixSource.AsView().Slice(0,0,2).Slice(1,0,3).Transpose(0,1);
            // left (2,3), compatible (3,2): no contiguous shortcut is possible.
            var stridedProduct = FNUTensorMath.MatMulViews(leftView,compatible);
            for(int row=0;row<2;row++)
                for(int col=0;col<2;col++)
                {
                    double independent=0;
                    for(int inner=0;inner<3;inner++)
                        independent += leftView.Get2D(row,inner)*compatible.Get2D(inner,col);
                    Near(stridedProduct.Get(row,col),independent,1e-11,
                        "strided physical cursor MatMul oracle");
                }
            Console.WriteLine("PASS FNU quality: condition guard / strided cursor oracle");
        }

        private static void TensorViewBroadcastArithmetic()
        {
            var owner = new FNUTensorBuffer(new FNUTensorShape(3,4),
                new double[]{1,2,3,4, 5,6,7,8, 9,10,11,12});
            var left = owner.AsView().Transpose(0,1).Slice(0,1,3);
            var row = new FNUTensorBuffer(new FNUTensorShape(3),
                new double[]{10,20,30});
            var right = row.AsView();
            var allocated = FNUTensorMath.AddViews(left,right);
            Check(allocated.Shape.Rank==2 && allocated.Shape.DimensionAt(0)==3 &&
                allocated.Shape.DimensionAt(1)==3,"view broadcast inferred shape");
            var output = new FNUTensorBuffer(new FNUTensorShape(3,3));
            FNUTensorMath.AddViewsInto(left,right,output);
            var multiplied = FNUTensorMath.MultiplyViews(left,right);
            for(int i=0;i<3;i++)
                for(int j=0;j<3;j++)
                {
                    double v = left.Get2D(i,j), w = right.Get(j);
                    Near(allocated.Get(i,j),v+w,0,"view broadcast add independent oracle");
                    Near(output.Get(i,j),v+w,0,"view broadcast Into");
                    Near(multiplied.Get(i,j),v*w,0,"view broadcast multiply independent oracle");
                }
            FNUTensorMath.MultiplyViewsInto(left,right,output);
            Near(output.Get(2,2),360,0,"view broadcast reusable multiply");
            var aliased = owner.AsView().Transpose(0,1);
            Throws<ArgumentException>(() => FNUTensorMath.AddViewsInto(
                aliased,right,owner),"view owner alias rejected");
            Throws<ArgumentException>(() => FNUTensorMath.MultiplyViewsInto(
                left,right,row),"broadcast rhs owner alias rejected");
            Throws<ArgumentException>(() => FNUTensorMath.AddViewsInto(
                left,right,new FNUTensorBuffer(new FNUTensorShape(3,2))),
                "broadcast output shape rejected");
            Near(output.Get(2,2),360,0,"invalid broadcast call does not change output");
            Throws<ArgumentException>(() => FNUTensorMath.AddViews(left,
                new FNUTensorBuffer(new FNUTensorShape(2)).AsView()),
                "incompatible broadcast dimensions");

            // Non-contiguous 3D view and lower-rank singleton broadcast axes.
            var data = new double[24];
            for(int i=0;i<data.Length;i++)data[i]=i+1;
            var high = new FNUTensorBuffer(new FNUTensorShape(2,3,4),data)
                .AsView().Transpose(0,2).Slice(1,1,2); // [4,2,2]
            var weights = new FNUTensorBuffer(new FNUTensorShape(2,1),
                new double[]{2,3}).AsView(); // [1,2,1]
            var result = FNUTensorMath.MultiplyViews(high,weights);
            Check(result.Shape.DimensionAt(0)==4 && result.Shape.DimensionAt(1)==2 &&
                result.Shape.DimensionAt(2)==2,"3D broadcast shape");
            for(int i=0;i<4;i++)
                for(int j=0;j<2;j++)
                    for(int k=0;k<2;k++)
                        Near(result.Get(i,j,k),high.Get(i,j,k)*weights.Get(j,0),0,
                            "3D noncontiguous broadcast oracle");

            var empty = new FNUTensorBuffer(new FNUTensorShape(0,3)).AsView();
            var emptyResult = FNUTensorMath.AddViews(empty,right);
            Check(emptyResult.Shape.ElementCount==0 &&
                emptyResult.Shape.DimensionAt(0)==0,"zero-length view broadcasting");
            var emptyInto = new FNUTensorBuffer(new FNUTensorShape(0,3));
            FNUTensorMath.MultiplyViewsInto(empty,right,emptyInto);
            Check(emptyInto.Shape.ElementCount==0,"zero-length view Into");
            var huge = new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{double.MaxValue}).AsView();
            var anotherHuge = new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{double.MaxValue}).AsView();
            var sentinel = new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{123});
            Throws<OverflowException>(() => FNUTensorMath.AddViewsInto(
                huge,anotherHuge,sentinel),"nonfinite broadcast result rejected");
            Near(sentinel.Get(0),123,0,"overflow before first write preserves output");
            owner.Set(100,0,1); // live source view, row 0 column 0
            Near(FNUTensorMath.AddViews(left,right).Get(0,0),110,0,
                "view broadcast reflects live owner mutation");
            Console.WriteLine("PASS FNU strided views: broadcast Add/Multiply/Into/zero/alias");
        }

        private static void TensorViewAxisReduction()
        {
            var samples=new double[24];
            for(int i=0;i<samples.Length;i++)samples[i]=i+1;
            var owner=new FNUTensorBuffer(new FNUTensorShape(2,3,4),samples);
            var view=owner.AsView().Transpose(0,2).Slice(1,1,2); // 4 x 2 x 2
            var result=FNUTensorMath.ReduceSumView(view,1); // 4 x 2
            Check(result.Shape.Rank==2 && result.Shape.DimensionAt(0)==4 &&
                result.Shape.DimensionAt(1)==2,"view axis reduction shape");
            var destination=new FNUTensorBuffer(new FNUTensorShape(4,2));
            FNUTensorMath.ReduceSumViewInto(view,1,destination);
            for(int i=0;i<4;i++)
                for(int j=0;j<2;j++)
                {
                    double reference=view.Get(i,0,j)+view.Get(i,1,j);
                    Near(result.Get(i,j),reference,0,"view axis reduction oracle");
                    Near(destination.Get(i,j),reference,0,"view reduction Into");
                }

            var cancellation=new FNUTensorBuffer(new FNUTensorShape(3),
                new double[]{1e16,1,-1e16});
            var one=FNUTensorMath.ReduceSumView(cancellation.AsView(),0);
            Check(one.Shape.Rank==1 && one.Shape.ElementCount==1,"rank1 reduction contract");
            Near(one.Get(0),1,0,"axis view compensation");
            var emptyOwner=new FNUTensorBuffer(new FNUTensorShape(2,0,3));
            var emptyAxis=FNUTensorMath.ReduceSumView(emptyOwner.AsView(),1);
            Check(emptyAxis.Shape.Rank==2 && emptyAxis.Shape.ElementCount==6,
                "empty reduced axis preserves other dimensions");
            Near(FNUTensorMath.Sum(emptyAxis),0,0,"empty reduced axis identity");
            var emptyOutput=new FNUTensorBuffer(new FNUTensorShape(0));
            FNUTensorMath.ReduceSumViewInto(
                new FNUTensorBuffer(new FNUTensorShape(0,3)).AsView(),1,emptyOutput);
            Check(emptyOutput.Shape.ElementCount==0,"remaining zero axis result");
            Throws<ArgumentOutOfRangeException>(()=>FNUTensorMath.ReduceSumView(view,3),
                "view reduction invalid axis");
            Throws<ArgumentException>(()=>FNUTensorMath.ReduceSumViewInto(view,1,owner),
                "view reduction owner alias");
            Throws<ArgumentException>(()=>FNUTensorMath.ReduceSumViewInto(view,1,
                new FNUTensorBuffer(new FNUTensorShape(2,4))),
                "view reduction output shape mismatch");

            var huge = new FNUTensorBuffer(new FNUTensorShape(1,2),
                new double[]{double.MaxValue,double.MaxValue});
            var unchanged = new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{42});
            Throws<OverflowException>(()=>FNUTensorMath.ReduceSumViewInto(
                huge.AsView(),1,unchanged),"view reduction overflow rejected");
            Near(unchanged.Get(0),42,0,"view reduction overflow atomic output");

            owner.Set(100,1,1,0);
            var changed=FNUTensorMath.ReduceSumView(view,1);
            for(int i=0;i<4;i++)
                for(int j=0;j<2;j++)
                    Near(changed.Get(i,j),view.Get(i,0,j)+view.Get(i,1,j),0,
                        "view reduction live owner update");
            Console.WriteLine("PASS FNU view reduction: strided axis/Into/empty/overflow atomic");
        }

        private static void RandomizedViewArithmeticAndReduction()
        {
            var random = new Random(20261028);
            for (int trial=0;trial<30;trial++)
            {
                int x=1+random.Next(4),y=1+random.Next(5),z=1+random.Next(4);
                double[] samples=new double[x*y*z];
                for(int i=0;i<samples.Length;i++)samples[i]=random.NextDouble()*2-1;
                var tensor=new FNUTensorBuffer(new FNUTensorShape(x,y,z),samples);
                var view=tensor.AsView().Transpose(0,2); // [z,y,x] non-contiguous
                var rhsData=new double[y];
                for(int i=0;i<y;i++)rhsData[i]=random.NextDouble()*2-1;
                var rhs=new FNUTensorBuffer(new FNUTensorShape(y,1),rhsData).AsView();
                var allocated=FNUTensorMath.AddViews(view,rhs);
                var into=new FNUTensorBuffer(new FNUTensorShape(z,y,x));
                FNUTensorMath.AddViewsInto(view,rhs,into);
                var product=FNUTensorMath.MultiplyViews(view,rhs);
                for(int i=0;i<z;i++)
                    for(int j=0;j<y;j++)
                        for(int k=0;k<x;k++)
                        {
                            double expected=view.Get(i,j,k), factor=rhs.Get(j,0);
                            Near(allocated.Get(i,j,k),expected+factor,1e-12,
                                "random view add oracle");
                            Near(into.Get(i,j,k),expected+factor,1e-12,
                                "random view add Into");
                            Near(product.Get(i,j,k),expected*factor,1e-12,
                                "random view multiplication oracle");
                        }
                for(int axis=0;axis<3;axis++)
                {
                    var reduced=FNUTensorMath.ReduceSumView(view,axis);
                    var output=new FNUTensorBuffer(reduced.Shape);
                    FNUTensorMath.ReduceSumViewInto(view,axis,output);
                    for(int i=0;i<reduced.Shape.DimensionAt(0);i++)
                        for(int j=0;j<reduced.Shape.DimensionAt(1);j++)
                        {
                            double reference=0;
                            for(int step=0;step<view.DimensionAt(axis);step++)
                            {
                                if(axis==0)reference+=view.Get(step,i,j);
                                else if(axis==1)reference+=view.Get(i,step,j);
                                else reference+=view.Get(i,j,step);
                            }
                            Near(reduced.Get(i,j),reference,1e-11,
                                "random view reduction reference");
                            Near(output.Get(i,j),reference,1e-11,
                                "random view reduction Into");
                        }
                }
            }
            Console.WriteLine("PASS FNU seeded randomized view broadcast / axis reduction: 30 cases");
        }

        private static void RandomizedContiguousBroadcast()
        {
            var rng = new Random(20261028);
            for (int trial=0; trial<60; ++trial)
            {
                int d0=1+rng.Next(4), d1=1+rng.Next(5), d2=1+rng.Next(6);
                var samples = new double[d0*d1*d2];
                var factors = new double[d1];
                for(int i=0;i<samples.Length;++i) samples[i]=rng.NextDouble()*20-10;
                for(int i=0;i<factors.Length;++i) factors[i]=rng.NextDouble()*10-5;
                var left = new FNUTensorBuffer(new FNUTensorShape(d0,d1,d2),samples);
                var right = new FNUTensorBuffer(new FNUTensorShape(d1,1),factors);
                var intoAdd = new FNUTensorBuffer(new FNUTensorShape(d0,d1,d2));
                var intoMultiply = new FNUTensorBuffer(new FNUTensorShape(d0,d1,d2));
                FNUTensorMath.AddInto(left,right,intoAdd);
                FNUTensorMath.MultiplyInto(left,right,intoMultiply);
                for(int i=0;i<d0;++i)
                    for(int j=0;j<d1;++j)
                        for(int k=0;k<d2;++k)
                        {
                            double x=samples[(i*d1+j)*d2+k],y=factors[j];
                            Near(intoAdd.Get(i,j,k),x+y,1e-12,
                                "buffer cursor addition independent reference");
                            Near(intoMultiply.Get(i,j,k),x*y,1e-12,
                                "buffer cursor multiplication independent reference");
                        }
            }
            // Rank-one broadcast into 3D and genuinely empty dimensions.
            var left3=new FNUTensorBuffer(new FNUTensorShape(2,1,3),
                new double[]{1,2,3,4,5,6});
            var right1=new FNUTensorBuffer(new FNUTensorShape(3),
                new double[]{10,20,30});
            var sum=FNUTensorMath.Add(left3,right1);
            Near(sum.Get(1,0,2),36,0,"rank-one trailing broadcast");
            var empty=new FNUTensorBuffer(new FNUTensorShape(2,0,3));
            var emptyOutput=new FNUTensorBuffer(new FNUTensorShape(2,0,3));
            FNUTensorMath.AddInto(empty,right1,emptyOutput);
            Check(emptyOutput.Shape.ElementCount==0,"empty buffer cursor output");
            var overflowA=new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{double.MaxValue});
            var overflowB=new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{double.MaxValue});
            var sentinel=new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{123});
            Throws<OverflowException>(()=>FNUTensorMath.AddInto(
                overflowA,overflowB,sentinel),"buffer cursor overflow");
            Near(sentinel.Get(0),123,0,"buffer cursor first overflow is atomic");
            Console.WriteLine("PASS FNU buffer cursor: 60 seeded shapes / exact broadcast / zero / overflow");
        }

        private static void ContiguousReductionAndSliceQuality()
        {
            var random=new Random(20261028);
            for(int trial=0;trial<45;++trial)
            {
                int x=1+random.Next(5), y=1+random.Next(6), z=1+random.Next(4);
                var samples=new double[x*y*z];
                for(int i=0;i<samples.Length;i++)samples[i]=random.NextDouble()*8-4;
                var tensor=new FNUTensorBuffer(new FNUTensorShape(x,y,z),samples);
                for(int axis=0;axis<3;++axis)
                {
                    var actual=FNUTensorMath.ReduceSum(tensor,axis);
                    var reused=new FNUTensorBuffer(actual.Shape);
                    FNUTensorMath.ReduceSumInto(tensor,axis,reused);
                    for(int i=0;i<actual.Shape.DimensionAt(0);++i)
                        for(int j=0;j<actual.Shape.DimensionAt(1);++j)
                        {
                            double reference=0;
                            for(int k=0;k<tensor.Shape.DimensionAt(axis);++k)
                            {
                                if(axis==0) reference+=tensor.Get(k,i,j);
                                else if(axis==1) reference+=tensor.Get(i,k,j);
                                else reference+=tensor.Get(i,j,k);
                            }
                            Near(actual.Get(i,j),reference,1e-11,"buffer axis reduce oracle");
                            Near(reused.Get(i,j),reference,1e-11,"buffer reduce Into");
                        }
                    int originalLength=tensor.Shape.DimensionAt(axis);
                    int begin=random.Next(originalLength+1);
                    int length=random.Next(originalLength-begin+1);
                    var sliced=tensor.Slice(axis,begin,length);
                    var view=tensor.AsView().Slice(axis,begin,length);
                    Check(sliced.Shape.ElementCount==view.ElementCount,"buffer slice elements");
                    var independent=sliced.CopyValues();
                    for(int i=0;i<sliced.Shape.ElementCount;++i)
                        Near(independent[i],view.GetFlat(i),0,"buffer slice oracle");
                    if(sliced.Shape.ElementCount>0)
                    {
                        var idx=new int[3];
                        sliced.Set(777,idx);
                        Near(view.GetFlat(0),independent[0],0,
                            "buffer slice stays independent of owner");
                    }
                }
            }
            var a=new FNUTensorBuffer(new FNUTensorShape(3),
                new double[]{1e16,1,-1e16});
            var result=new FNUTensorBuffer(new FNUTensorShape(1));
            FNUTensorMath.ReduceSumInto(a,0,result);
            Near(result.Get(0),1,0,"buffer reduction cancellation");
            Throws<ArgumentException>(()=>FNUTensorMath.ReduceSumInto(a,0,a),
                "buffer reduction owner alias");
            var stale=new FNUTensorBuffer(new FNUTensorShape(1),
                new double[]{99});
            Throws<ArgumentException>(()=>FNUTensorMath.ReduceSumInto(a,0,
                new FNUTensorBuffer(new FNUTensorShape(2))),
                "buffer reduction wrong output dimensions");
            var huge=new FNUTensorBuffer(new FNUTensorShape(2),
                new double[]{double.MaxValue,double.MaxValue});
            Throws<OverflowException>(()=>FNUTensorMath.ReduceSumInto(huge,0,stale),
                "buffer reduction nonfinite output");
            Near(stale.Get(0),99,0,"failed reduction output stays untouched");
            var zero=new FNUTensorBuffer(new FNUTensorShape(0,4));
            var zeroResult=FNUTensorMath.ReduceSum(zero,0);
            Check(zeroResult.Shape.ElementCount==4,"zero axis retains remaining size");
            Near(FNUTensorMath.Sum(zeroResult),0,0,"zero axis reduction is additive identity");
            Console.WriteLine("PASS FNU buffer reduction/slice cursor: 45 random cases / overflow atomic");
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
