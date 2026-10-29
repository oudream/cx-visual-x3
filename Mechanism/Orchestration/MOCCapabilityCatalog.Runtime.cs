using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>
    /// Compatibility boundary: historical resolver probing remains in Orchestration.
    /// The static catalog source has no direct MRK types or execution dependencies.
    /// A resolved delegate does not imply device availability or authorization.
    /// </summary>
    public sealed partial class MOCCapabilityCatalog
    {
        public MOCCapabilityCatalog(IEnumerable<IMOCCapabilityProvider> providers,
            IMRKCapabilityResolver runtime)
            : this(providers, GetAvailability(runtime)) { }

        private static Func<string,bool> GetAvailability(IMRKCapabilityResolver runtime)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            return id => {
                MRKResolvedCapability action;
                return runtime.TryResolve(id,out action) && action != null;
            };
        }
    }
}
