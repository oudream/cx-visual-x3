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
            Console.WriteLine("PASS FNU Numerics - interpolation/statistics/SVD/moving-average/FFT");
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
