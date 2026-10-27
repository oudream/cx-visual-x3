using System;
using System.Collections.Generic;
namespace CxVisual.Foundation.Numerics {
    /// <summary>因果移动平均，起始不足窗口时以已采集的数量为分母。</summary>
    public static class FNUMovingAverage {
        public static double[] Calculate(IReadOnlyList<double> values,int window) {
            if(values==null)throw new ArgumentNullException("values");
            var result=new double[values.Count];
            Write(values,window,result);return result;
        }
        public static void Write(IReadOnlyList<double> values,int window,double[] output) {
            if(values==null)throw new ArgumentNullException("values");
            if(output==null)throw new ArgumentNullException("output");
            if(window<=0)throw new ArgumentOutOfRangeException("window");
            if(output.Length!=values.Count)throw new ArgumentException("Output length mismatch","output");
            if(ReferenceEquals(values,output))throw new ArgumentException("Cannot filter in-place","output");
            double sum=0;
            for(int i=0;i<values.Count;i++){
                double x=values[i];
                if(double.IsNaN(x)||double.IsInfinity(x))throw new ArgumentOutOfRangeException("values");
                sum+=x;
                if(i>=window)sum-=values[i-window];
                if(double.IsNaN(sum)||double.IsInfinity(sum))throw new OverflowException("Moving average overflow");
                output[i]=sum/Math.Min(i+1,window);
            }
        }
    }
}
