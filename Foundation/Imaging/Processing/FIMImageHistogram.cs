using System;
using System.Collections.Generic;
using System.Threading;

namespace CxVisual.Foundation.Imaging
{
    public sealed class FIMImageHistogram
    {
        internal FIMImageHistogram(double min, double max, long[] bins)
        {
            Minimum = min; Maximum = max; Bins = Array.AsReadOnly(bins);
        }
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public IReadOnlyList<long> Bins { get; private set; }
    }
}
