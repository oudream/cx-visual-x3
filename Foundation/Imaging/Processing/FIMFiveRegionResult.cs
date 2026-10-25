using System;
using System.Collections.Generic;
using System.Threading;

namespace CxVisual.Foundation.Imaging
{
    public sealed class FIMFiveRegionResult
    {
        internal FIMFiveRegionResult(FIMPixelRegion[] regions, double[] means)
        {
            Regions = Array.AsReadOnly(regions);
            Means = Array.AsReadOnly(means);
        }
        public IReadOnlyList<FIMPixelRegion> Regions { get; private set; }
        public IReadOnlyList<double> Means { get; private set; }
    }
}
