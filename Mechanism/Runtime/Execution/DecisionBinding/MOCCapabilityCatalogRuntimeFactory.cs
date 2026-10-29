using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>Runtime-facing availability adapter. The authoritative catalog stays in Decision.</summary>
    public static class MOCCapabilityCatalogRuntimeFactory
    {
        public static MOCCapabilityCatalog Create(IEnumerable<IMOCCapabilityProvider> providers,
            IMRKCapabilityResolver runtime)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            return new MOCCapabilityCatalog(providers, id =>
            {
                MRKResolvedCapability capability;
                return runtime.TryResolve(id, out capability) && capability != null;
            });
        }
    }
}
