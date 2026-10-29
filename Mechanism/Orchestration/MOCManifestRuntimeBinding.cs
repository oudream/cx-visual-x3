using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>
    /// Runtime-facing compatibility Adapter for the existing MOC compiler.
    /// Decision's source manifest is pure; this class deliberately belongs
    /// to the MRK-facing side and must NOT migrate to a Runtime-free Decision DLL.
    /// No execution or publication occurs here.
    /// </summary>
    public sealed class MOCManifestRuntimeBinding
    {
        /// <summary>
        /// Fail closed BEFORE invoking a provider's resolver for stale, incomplete
        /// or structurally invalid source. A resolver is probed only once per
        /// capability ID within this binding attempt and never executed.
        /// </summary>
        public MOCCompiledPlan Bind(MEMProjectSnapshot project,
            MDCStaticSourceManifest manifest, MDCStaticCatalogSnapshot catalog,
            IMRKCapabilityResolver resolver,
            IReadOnlyDictionary<string,Type> professionalTypes = null,
            bool allowUnverifiedLegacyContracts = false)
        {
            if(project == null) throw new ArgumentNullException("project");
            if(manifest == null) throw new ArgumentNullException("manifest");
            if(catalog == null) throw new ArgumentNullException("catalog");
            if(resolver == null) throw new ArgumentNullException("resolver");

            manifest.RequireCurrent(project,catalog);
            if(!manifest.HasCompleteStaticMetadata)
                throw new InvalidOperationException("moc.bind.unknown_static_capability:"+
                    string.Join(",",manifest.UnknownCapabilityIds));

            // Reuse exactly the same static wiring assessment as Studio/SDK.
            // A correct Runtime CLR signature cannot compensate for a stale or
            // incompatible port declaration inside the original MOL Content.
            var preflight = new MDCStaticBuildPreflight();
            foreach(var content in manifest.Contents)
            {
                MEMNodeSnapshot node = project.Find(content.ContentId);
                MOLWiringDefinition wiring = MOLWiringCodec.Get(node);
                if(wiring == null) continue; // V1 structural logic remains Core-owned.
                var assessment=preflight.Assess(wiring,catalog);
                if(assessment.Verdict == MDCVerdict.Rejected)
                    throw new InvalidOperationException("moc.bind.static_wiring_rejected:"+
                        content.ContentId.ToString("N")+":"+
                        string.Join(",",assessment.ReasonCodes));
                if(assessment.Verdict == MDCVerdict.Unknown &&
                    !allowUnverifiedLegacyContracts)
                    throw new InvalidOperationException("moc.bind.static_wiring_unverified:"+
                        content.ContentId.ToString("N")+":"+
                        string.Join(",",assessment.ReasonCodes));
            }

            // A compilation-local cache closes the preflight-to-compiler resolver
            // identity gap without creating a shared registration or execution store.
            CachedResolver pinned = resolver as IMRKVersionedCapabilityResolver != null
                ? (CachedResolver)new CachedVersionedResolver(
                    (IMRKVersionedCapabilityResolver)resolver)
                : new CachedResolver(resolver);
            var inspector = new MOCBindingContractInspector();
            foreach(var capabilityId in manifest.Contents
                .SelectMany(x => x.CapabilityUses)
                .Select(x => x.CapabilityId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x,StringComparer.Ordinal))
            {
                MOCCapabilityDescriptor definition = catalog.Find(capabilityId);
                if(definition == null)
                    throw new InvalidOperationException("moc.bind.unknown_static_capability:"+capabilityId);
                MRKResolvedCapability binding;
                if(!pinned.TryResolve(capabilityId,out binding) || binding == null)
                    throw new InvalidOperationException("moc.bind.runtime_capability_missing:"+capabilityId);
                var assessment = inspector.Assess(definition,binding,professionalTypes);
                if(assessment.Verdict == MDCVerdict.Rejected)
                    throw new InvalidOperationException("moc.bind.contract_rejected:"+
                        capabilityId+":"+string.Join(",",assessment.ReasonCodes));
                if(assessment.Verdict == MDCVerdict.Unknown &&
                    !allowUnverifiedLegacyContracts)
                    throw new InvalidOperationException("moc.bind.contract_unverified:"+
                        capabilityId+":"+string.Join(",",assessment.ReasonCodes));
            }

            // Keep *one* MOC compiler and its existing MRK binding/SourceMap
            // semantics. Actual Host authorization and live resource leases
            // remain required when this plan is published/executed.
            MOCCompiledPlan compiled = new MOCEngineeringCompiler().Compile(
                project,manifest.RootContentId,pinned);
            if(compiled.ProjectId != manifest.ProjectId ||
                compiled.EngineeringRevision != manifest.EngineeringRevision)
                throw new InvalidOperationException("moc.bind.compiled_source_version_changed");
            return compiled;
        }

        private class CachedResolver : IMRKCapabilityResolver
        {
            private readonly IMRKCapabilityResolver _origin;
            private readonly Dictionary<string,MRKResolvedCapability> _cache =
                new Dictionary<string,MRKResolvedCapability>(StringComparer.Ordinal);
            private readonly HashSet<string> _missing =
                new HashSet<string>(StringComparer.Ordinal);

            internal CachedResolver(IMRKCapabilityResolver origin) { _origin=origin; }
            public bool TryResolve(string id,out MRKResolvedCapability capability)
            {
                if(id == null) { capability=null; return false; }
                if(_cache.TryGetValue(id,out capability)) return true;
                if(_missing.Contains(id)) { capability=null; return false; }
                if(_origin.TryResolve(id,out capability) && capability != null)
                {
                    _cache.Add(id,capability);
                    return true;
                }
                _missing.Add(id);
                capability=null;
                return false;
            }
        }

        private sealed class CachedVersionedResolver : CachedResolver,
            IMRKVersionedCapabilityResolver
        {
            private readonly IMRKVersionedCapabilityResolver _versioned;
            internal CachedVersionedResolver(IMRKVersionedCapabilityResolver origin)
                : base(origin) { _versioned=origin; }
            public bool TryGetCapabilityVersion(string id,out string version)
            { return _versioned.TryGetCapabilityVersion(id,out version); }
        }
    }
}
