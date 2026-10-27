using System;
using System.Collections.Generic;
namespace CxVisual.Foundation.Numerics {
    /// <summary>不可变统计结果；总体分母N，样本分母N-1，单样本SampleVariance为NaN。</summary>
    public sealed class FNUStatisticsSnapshot {
        internal FNUStatisticsSnapshot(long count,double min,double max,double mean,double m2) {
            Count=count;Min=min;Max=max;Mean=mean;
            PopulationVariance=Math.Max(0,m2/count);
            SampleVariance=count>1?Math.Max(0,m2/(count-1)):double.NaN;
        }
        public long Count{get;private set;}
        public double Min{get;private set;}
        public double Max{get;private set;}
        public double Mean{get;private set;}
        public double PopulationVariance{get;private set;}
        public double SampleVariance{get;private set;}
        public double PopulationStandardDeviation{get{return Math.Sqrt(PopulationVariance);}}
    }
    /// <summary>Welford流式累计；单实例非线程安全。数值溢出不会发布中间状态。</summary>
    public sealed class FNURunningStatistics {
        private long _n;
        private double _mean,_m2,_min=double.PositiveInfinity,_max=double.NegativeInfinity;
        public long Count{get{return _n;}}
        public void Add(double value) {
            if(double.IsNaN(value)||double.IsInfinity(value))
                throw new ArgumentOutOfRangeException("value");
            long count=checked(_n+1);
            double d=value-_mean, mean=_mean+d/count, m2=_m2+d*(value-mean);
            if(double.IsInfinity(mean)||double.IsNaN(mean)||double.IsInfinity(m2)||double.IsNaN(m2))
                throw new OverflowException("Nonfinite statistical accumulator");
            _n=count;_mean=mean;_m2=m2;
            _min=Math.Min(_min,value);_max=Math.Max(_max,value);
        }
        public void Merge(FNURunningStatistics other) {
            if(other==null)throw new ArgumentNullException("other");
            if(ReferenceEquals(this,other))throw new ArgumentException("Cannot merge self.","other");
            if(other._n==0)return;
            if(_n==0) { _n=other._n;_mean=other._mean;_m2=other._m2;
                _min=other._min;_max=other._max;return; }
            long count=checked(_n+other._n);
            double delta=other._mean-_mean;
            double mean=_mean+delta*((double)other._n/count);
            double m2=_m2+other._m2+delta*delta*((double)_n/count)*other._n;
            if(double.IsInfinity(mean)||double.IsNaN(mean)||double.IsInfinity(m2)||double.IsNaN(m2))
                throw new OverflowException("Nonfinite statistical merge");
            _n=count;_mean=mean;_m2=m2;
            _min=Math.Min(_min,other._min);_max=Math.Max(_max,other._max);
        }
        public FNUStatisticsSnapshot Snapshot() {
            if(_n==0)throw new InvalidOperationException("Empty statistical series");
            return new FNUStatisticsSnapshot(_n,_min,_max,_mean,_m2);
        }
    }
    public static class FNUStatistics {
        public static FNUStatisticsSnapshot Describe(IReadOnlyList<double> data) {
            if(data==null)throw new ArgumentNullException("data");
            var calc=new FNURunningStatistics();
            for(int i=0;i<data.Count;i++)calc.Add(data[i]);
            return calc.Snapshot();
        }
        public static double Mean(IReadOnlyList<double> data){return Describe(data).Mean;}
    }
}
