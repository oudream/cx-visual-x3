using System;
using System.Collections.Generic;
using System.Threading;

namespace CxVisual.Foundation.Imaging
{
    public sealed class FIMImageStatistics
    {
        internal FIMImageStatistics(long count, double min, double max, double mean, double deviation)
        {
            PixelCount = count; Min = min; Max = max;
            Mean = mean; StandardDeviation = deviation;
        }
        public long PixelCount { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Mean { get; private set; }
        public double StandardDeviation { get; private set; }
    }
}
