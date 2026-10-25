using System;
using System.Collections.Generic;
using System.Threading;

namespace CxVisual.Foundation.Imaging
{
    public sealed class FIMImageAnalysisResult
    {
        internal FIMImageAnalysisResult(FIMImageStatistics statistics, FIMImageHistogram histogram)
        {
            Statistics = statistics; Histogram = histogram;
        }
        public FIMImageStatistics Statistics { get; private set; }
        public FIMImageHistogram Histogram { get; private set; }
    }
}
