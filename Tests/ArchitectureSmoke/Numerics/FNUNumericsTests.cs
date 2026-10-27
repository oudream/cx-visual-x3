using System;
using System.Numerics;
using CxVisual.Foundation.Numerics;
namespace CxVisual.Tests.ArchitectureSmoke {
    internal static class FNUNumericsTests {
        public static void Run(){
            Near(FNUInterpolation.Linear(10,20,.25),12.5,"linear");
            Near(FNUInterpolation.Linear(10,20,-1),0,"extrapolation");
            Near(FNUInterpolation.Bilinear(0,2,4,6,.5,.5),3,"bilinear");
            Near(FNUInterpolation.Trilinear(0,1,2,3,4,5,6,7,.5,.5,.5),3.5,"trilinear");
            Throws<ArgumentOutOfRangeException>(()=>FNUInterpolation.Linear(0,1,double.NaN),"invalid interpolation");
            double[] samples={1,2,3,4};
            var stats=FNUStatistics.Describe(samples);
            Assert(stats.Count==4,"count");
            Near(stats.Mean,2.5,"mean");Near(stats.Min,1,"min");Near(stats.Max,4,"max");
            Near(stats.PopulationVariance,1.25,"population variance");
            Near(stats.SampleVariance,5.0/3,"sample variance");
            var a=new FNURunningStatistics();a.Add(1);a.Add(2);
            var b=new FNURunningStatistics();b.Add(3);b.Add(4);a.Merge(b);
            Near(a.Snapshot().Mean,2.5,"merge mean");
            Near(a.Snapshot().PopulationVariance,1.25,"merge variance");
            Throws<InvalidOperationException>(()=>new FNURunningStatistics().Snapshot(),"empty series");
            Throws<ArgumentOutOfRangeException>(()=>a.Add(double.PositiveInfinity),"nonfinite series");
            Assert(a.Count==4,"failure state");
            var single=new FNURunningStatistics();single.Add(7);
            Assert(double.IsNaN(single.Snapshot().SampleVariance),"one sample variance undefined");
            double[] avg=FNUMovingAverage.Calculate(samples,2);
            Near(avg[0],1,"start");Near(avg[1],1.5,"second");
            Near(avg[2],2.5,"third");Near(avg[3],3.5,"last");
            Assert(samples[0]==1 && samples[3]==4,"input preserved");
            double[] target=new double[4];
            FNUMovingAverage.Write(samples,2,target);
            Near(target[3],3.5,"output buffer");
            Throws<ArgumentException>(()=>FNUMovingAverage.Write(samples,2,samples),"alias rejected");
            Throws<ArgumentOutOfRangeException>(()=>FNUMovingAverage.Calculate(samples,0),"zero window");
            double[,] design={{1,0},{1,1},{1,2},{1,3}};
            double[] observations={1,3,5,7};
            var fit=FNULinearAlgebra.LeastSquares(design,observations);
            Assert(fit.Rows==4&&fit.Columns==2&&fit.Rank==2,"rank and shape");
            Near(fit.Coefficients[0],1,"intercept");Near(fit.Coefficients[1],2,"slope");
            Near(fit.ResidualNorm,0,"residual");
            double[] changed=fit.Coefficients;changed[0]=999;
            Near(fit.Coefficients[0],1,"snapshot isolation");
            Throws<InvalidOperationException>(()=>FNULinearAlgebra.LeastSquares(
                new double[,]{{1,1},{2,2},{3,3}},new double[]{1,2,3}),"rank deficient");
            Throws<ArgumentOutOfRangeException>(()=>FNULinearAlgebra.LeastSquares(
                new double[,]{{1,double.NaN},{1,2}},new double[]{1,2}),"nonfinite fit");
            Complex[] fft=FNUFourier.ForwardReal(new double[]{1,0,0,0});
            Assert(fft.Length==4,"FFT size");
            for(int i=0;i<4;i++)Near(fft[i].Real,1,"FFT impulse");
            Complex[] inverse=FNUFourier.Inverse(fft);
            Near(inverse[0].Real,1,"FFT inverse origin");
            for(int i=1;i<4;i++)Near(inverse[i].Magnitude,0,"FFT inverse remaining");
            Near(fft[0].Real,1,"FFT input preservation");
            Throws<ArgumentException>(()=>FNUFourier.ForwardReal(new double[0]),"empty FFT");
            Throws<ArgumentOutOfRangeException>(()=>FNUFourier.ForwardReal(new double[]{double.NaN}),"bad FFT");
            Throws<ArgumentOutOfRangeException>(()=>FNUInterpolation.Bilinear(double.NaN,2,3,4,.5,.5),"nonfinite bilinear corner");
            Throws<ArgumentOutOfRangeException>(()=>FNUInterpolation.Trilinear(
                0,1,2,3,4,5,6,double.PositiveInfinity,.5,.5,.5),"nonfinite trilinear corner");
            Throws<ArgumentException>(()=>a.Merge(a),"self-merge invalid");
            Throws<ArgumentException>(()=>FNUMovingAverage.Write(samples,2,new double[3]),"wrong output buffer");
            Throws<ArgumentException>(()=>FNULinearAlgebra.LeastSquares(
                new double[,]{{1,2}},new double[]{1}),"underdetermined design");
            Throws<ArgumentException>(()=>FNULinearAlgebra.LeastSquares(
                new double[,]{{1},{1}},new double[]{1}),"incompatible observation length");
            Throws<ArgumentOutOfRangeException>(()=>FNUFourier.Inverse(
                new Complex[]{new Complex(double.NaN,0)}),"nonfinite inverse spectrum");
            Throws<ArgumentException>(()=>FNUFourier.Inverse(new Complex[0]),"empty inverse spectrum");
            Near(FNUPolynomial.Evaluate(new double[]{1,2,3},2),17,"polynomial Horner");
            double[] derived=FNUPolynomial.Derivative(new double[]{1,2,3});
            Near(derived[0],2,"derivative c0"); Near(derived[1],6,"derivative c1");
            Near(FNUPolynomial.Evaluate(new double[]{7},-12),7,"constant polynomial");
            Throws<ArgumentException>(()=>FNUPolynomial.Evaluate(new double[0],2),"empty polynomial");
            FNUHistogram hist=FNUHistogram.Calculate(new double[]{0,1,2,3,4},4,0,4);
            Assert(hist.BinCount==4&&hist.GetCount(0)==1&&hist.GetCount(1)==1&&
                hist.GetCount(2)==1&&hist.GetCount(3)==2,"histogram endpoint");
            long[] oldBins=hist.CopyBins();oldBins[0]=99;
            Assert(hist.GetCount(0)==1,"histogram defensive bins");
            Throws<ArgumentOutOfRangeException>(()=>FNUHistogram.Calculate(new double[]{5},4,0,4),"histogram out of range");
            var root=FNURootFinding.Bisection(x=>x*x-2,0,2);
            Near(root.Root,Math.Sqrt(2),"bisection sqrt2");
            Assert(root.Iterations>0&&Math.Abs(root.Residual)<1e-8,"bisection residual");
            Throws<ArgumentException>(()=>FNURootFinding.Bisection(x=>x*x+1,-1,1),"unbracketed root");
            Throws<ArithmeticException>(()=>FNURootFinding.Bisection(x=>double.NaN,0,1),"nonfinite root function");
            using(var cancel=new System.Threading.CancellationTokenSource()){
                cancel.Cancel();
                Throws<OperationCanceledException>(()=>FNURootFinding.Bisection(x=>x,0,1,
                    cancellationToken:cancel.Token),"root cancellation");
            }
            Near(FNUNumericalIntegration.Simpson(x=>x*x,0,1),1.0/3,"Simpson x squared");
            Near(FNUNumericalIntegration.Simpson(x=>x*x,1,0),-1.0/3,"Simpson reverse");
            Throws<ArgumentOutOfRangeException>(()=>FNUNumericalIntegration.Simpson(x=>x,0,1,3),"Simpson odd intervals");
            var polynomial=FNUCurveFitting.Polynomial(new double[]{0,1,2,3},
                new double[]{1,4,9,16},2);
            Near(polynomial.Coefficients[0],1,"fit polynomial intercept");
            Near(polynomial.Coefficients[1],2,"fit polynomial linear");
            Near(polynomial.Coefficients[2],1,"fit polynomial quadratic");
            Throws<ArgumentException>(()=>FNUCurveFitting.Polynomial(
                new double[]{0,1},new double[]{1,2},2),"insufficient fit observations");
            double[] fir=FNUFirFilter.Apply(new double[]{1,2,3},new double[]{.5,.5});
            Near(fir[0],.5,"FIR first");Near(fir[1],1.5,"FIR second");Near(fir[2],2.5,"FIR third");
            Throws<ArgumentException>(()=>FNUFirFilter.Write(samples,new double[]{1},samples),"FIR alias");
            Throws<ArgumentException>(()=>FNUFirFilter.Apply(samples,new double[0]),"FIR no coefficients");
            var iir=new FNUIirFilter(new double[]{.5},new double[]{1,-.5});
            Near(iir.Process(2),1,"IIR n0");Near(iir.Process(0),.5,"IIR n1");
            iir.Reset();Near(iir.Process(0),0,"IIR reset");
            Throws<ArgumentOutOfRangeException>(()=>new FNUIirFilter(new double[]{1},new double[]{0}),"IIR zero a0");
            Throws<ArgumentOutOfRangeException>(()=>iir.Process(double.PositiveInfinity),"IIR invalid input");
            var sampled=FNUResampling.Linear(new double[]{0,10},3);
            Near(sampled[0],0,"resampling first");Near(sampled[1],5,"resampling center");
            Near(sampled[2],10,"resampling last");
            Near(FNUResampling.Linear(new double[]{7},4)[3],7,"resampling singleton");
            Throws<ArgumentException>(()=>FNUResampling.Linear(new double[0],3),"resampling empty");
            var convolved=FNUConvolution.Full(new double[]{1,2},new double[]{3,4});
            Near(convolved[0],3,"convolution first");
            Near(convolved[1],10,"convolution middle");
            Near(convolved[2],8,"convolution last");
            Throws<ArgumentException>(()=>FNUConvolution.Full(new double[0],new double[]{1}),"convolution empty");
            var hann=FNUWindowFunction.Hann(3);
            Near(hann[0],0,"hann start");Near(hann[1],1,"hann center");Near(hann[2],0,"hann end");
            Near(FNUWindowFunction.Hann(1)[0],1,"hann singleton");
            var spectrum=FNUFrequencyAnalysis.OneSidedAmplitude(new double[]{0,1,0,-1},4);
            Assert(spectrum.Length==3,"one-sided bins");
            Near(spectrum.FrequencyAt(1),1,"frequency Hz");
            Near(spectrum.AmplitudeAt(1),1,"sine amplitude");
            Near(spectrum.AmplitudeAt(0),0,"DC amplitude");
            double[] tampered=spectrum.CopyAmplitudes();tampered[1]=99;
            Near(spectrum.AmplitudeAt(1),1,"spectrum copy");
            Throws<ArgumentOutOfRangeException>(()=>FNUFrequencyAnalysis.OneSidedAmplitude(samples,0),"invalid sample rate");
            var shape=new FNUTensorShape(2,3);
            Assert(shape.Rank==2&&shape.ElementCount==6&&shape.StrideAt(0)==3&&
                shape.StrideAt(1)==1,"tensor shape and strides");
            Assert(shape.Offset(1,2)==5,"tensor flat offset");
            int[] detached=shape.CopyDimensions();detached[0]=999;
            Assert(shape.DimensionAt(0)==2,"tensor dimension ownership");
            Throws<ArgumentOutOfRangeException>(()=>shape.Offset(2,0),"tensor index bounds");
            Throws<OverflowException>(()=>new FNUTensorShape(int.MaxValue,2),"tensor size overflow");
            var tensor=new FNUTensorBuffer(shape,new double[]{1,2,3,4,5,6});
            Near(tensor.Get(1,2),6,"tensor get");
            var copied=tensor.CopyValues();copied[0]=100;
            Near(tensor.Get(0,0),1,"tensor copy isolation");
            FNUTensorBuffer cut=tensor.Slice(1,1,2);
            Assert(cut.Shape.DimensionAt(0)==2&&cut.Shape.DimensionAt(1)==2,"tensor slice shape");
            Near(cut.Get(0,0),2,"tensor slice value");
            cut.Set(20,0,0);
            Near(tensor.Get(0,1),2,"tensor slice ownership");
            var transposed=FNUTensorMath.Transpose2D(tensor);
            Assert(transposed.Shape.DimensionAt(0)==3&&transposed.Shape.DimensionAt(1)==2,"transpose shape");
            Near(transposed.Get(2,1),6,"transpose value");
            var row=new FNUTensorBuffer(new FNUTensorShape(1,3),new double[]{10,20,30});
            var plus=FNUTensorMath.Add(tensor,row);
            Near(plus.Get(0,0),11,"broadcast row");
            Near(plus.Get(1,2),36,"broadcast last");
            var column=new FNUTensorBuffer(new FNUTensorShape(2,1),new double[]{2,3});
            var times=FNUTensorMath.Multiply(tensor,column);
            Near(times.Get(0,2),6,"multiply broadcast row0");
            Near(times.Get(1,2),18,"multiply broadcast row1");
            Near(FNUTensorMath.Sum(tensor),21,"tensor sum");
            var reduced=FNUTensorMath.ReduceSum(tensor,1);
            Assert(reduced.Shape.Rank==1&&reduced.Shape.ElementCount==2,"tensor reduce shape");
            Near(reduced.Get(0),6,"tensor reduce row0");
            Near(reduced.Get(1),15,"tensor reduce row1");
            var scalarReduction=FNUTensorMath.ReduceSum(
                new FNUTensorBuffer(new FNUTensorShape(3),new double[]{1,2,3}),0);
            Near(scalarReduction.Get(0),6,"tensor rank1 reduce");
            var emptyTensor=new FNUTensorBuffer(new FNUTensorShape(0,3));
            Near(FNUTensorMath.Sum(emptyTensor),0,"tensor empty sum");
            Assert(FNUTensorMath.Add(emptyTensor,row).Shape.ElementCount==0,"zero dimension broadcast");
            Throws<ArgumentException>(()=>FNUTensorMath.Add(tensor,
                new FNUTensorBuffer(new FNUTensorShape(2,2))),"broadcast shape mismatch");
            Throws<ArgumentException>(()=>new FNUTensorBuffer(shape,new double[3]),"tensor buffer length");
            Throws<ArgumentOutOfRangeException>(()=>tensor.Set(double.NaN,0,0),"tensor nonfinite setter");
            var optimize=FNUOptimization.GoldenSection(x=>(x-3)*(x-3),0,8);
            Near(optimize.Position,3,"golden-section optimum");
            Near(optimize.Value,0,"golden-section objective");
            Throws<InvalidOperationException>(()=>FNUOptimization.GoldenSection(
                x=>(x-3)*(x-3),0,8,maxIterations:1),"nonconvergent optimization");
            Throws<ArgumentOutOfRangeException>(()=>FNUOptimization.GoldenSection(
                x=>x*x,1,0),"optimization reversed interval");
            Near(FNUFiniteDifferences.CentralDerivative(x=>x*x,2,1e-4),4,"central derivative");
            Throws<ArgumentOutOfRangeException>(()=>FNUFiniteDifferences.CentralDerivative(
                x=>x,1,0),"finite difference zero step");
            var correlated=FNUCorrelation.Full(new double[]{1,2},new double[]{1,1});
            Near(correlated[0],1,"correlation lag minus1");
            Near(correlated[1],3,"correlation lag 0");
            Near(correlated[2],2,"correlation lag plus1");
            var matrixProduct=FNUTensorMath.MatMul(tensor,
                new FNUTensorBuffer(new FNUTensorShape(3,2),new double[]{1,0,0,1,1,1}));
            Assert(matrixProduct.Shape.DimensionAt(0)==2&&matrixProduct.Shape.DimensionAt(1)==2,"MatMul shape");
            Near(matrixProduct.Get(0,0),4,"MatMul row0 col0");
            Near(matrixProduct.Get(0,1),5,"MatMul row0 col1");
            Near(matrixProduct.Get(1,1),11,"MatMul row1 col1");
            var reshaped=tensor.Reshape(new FNUTensorShape(3,2));
            Near(reshaped.Get(2,1),6,"reshape order");
            reshaped.Set(100,0,0);
            Near(tensor.Get(0,0),1,"reshape owns data");
            Throws<ArgumentException>(()=>tensor.Reshape(new FNUTensorShape(3,3)),"reshape size mismatch");
            Throws<ArgumentException>(()=>FNUTensorMath.MatMul(tensor,tensor),"MatMul shape mismatch");
            Near(FNUDistribution.Median(new double[]{4,1,3,2}),2.5,"median even count");
            Near(FNUDistribution.Quantile(new double[]{4,1,3,2},.25),1.75,"quantile interpolated");
            Near(FNUDistribution.Quantile(new double[]{4,1,3,2},0),1,"quantile min");
            Throws<ArgumentOutOfRangeException>(()=>FNUDistribution.Quantile(samples,-.1),"invalid quantile");
            Assert(FNUPrecision.NearlyEqual(1,1+1e-11),"near equality");
            Assert(!FNUPrecision.NearlyEqual(1,2),"distinct values");
            Assert(!FNUPrecision.NearlyEqual(double.NaN,double.NaN),"NaN never equal");
            Throws<ArgumentOutOfRangeException>(()=>FNUPrecision.NearlyEqual(1,1,-1),"invalid tolerance");
            var weighted=FNUWeightedLeastSquares.Solve(
                new double[,]{{1,0},{1,1},{1,2},{1,3}},
                new double[]{1,3,100,7},new double[]{1,1,0,1});
            Near(weighted.Coefficients[0],1,"weighted intercept excludes outlier");
            Near(weighted.Coefficients[1],2,"weighted slope excludes outlier");
            Throws<ArgumentOutOfRangeException>(()=>FNUWeightedLeastSquares.Solve(
                new double[,]{{1},{1}},new double[]{1,2},new double[]{1,-1}),"negative weight");
            var ode=FNUOdeSolver.RungeKutta4((time,y)=>y,0,1,.01,100);
            Assert(ode.Count==101,"RK4 sample count");
            Near(ode.TimeAt(100),1,"RK4 final time");
            Near(ode.ValueAt(100),Math.E,"RK4 exponential growth");
            var odeValues=ode.CopyValues();odeValues[0]=999;
            Near(ode.ValueAt(0),1,"RK4 copy isolation");
            Throws<ArgumentOutOfRangeException>(()=>FNUOdeSolver.RungeKutta4(
                (time,y)=>y,0,1,0,10),"RK4 zero step");
            Throws<OverflowException>(()=>FNUOdeSolver.RungeKutta4(
                (time,y)=>double.PositiveInfinity,0,1,.1,10),"RK4 invalid derivative");
            FNUNumericsQualityTests.Run();
            Console.WriteLine("PASS FNU Numerics - F03/F04/F10/F13 numerical and contract checks");
        }
        private static void Assert(bool ok,string name){
            if(!ok)throw new InvalidOperationException("FNU failed: "+name);
        }
        private static void Near(double v,double expect,string name){
            if(double.IsNaN(v)||Math.Abs(v-expect)>1e-9*Math.Max(1,Math.Abs(expect)))
                throw new InvalidOperationException("FNU failed: "+name+" ="+v+", expected="+expect);
        }
        private static void Throws<T>(Action action,string name) where T:Exception{
            try{action();}catch(T){return;}
            throw new InvalidOperationException("FNU failed, missing "+typeof(T).Name+": "+name);
        }
    }
}
