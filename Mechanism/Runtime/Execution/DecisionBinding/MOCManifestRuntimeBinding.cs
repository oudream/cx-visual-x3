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
        /// Transitional Host adapter for the pure declarative Plan. For now the
        /// original MOC compiler remains the only lowering authority. This
        /// overload consumes a versioned Plan but does NOT translate its block
        /// index into a second MRK scheduler or claim Plan/Runtime decoupling.
        /// </summary>
        /// <summary>
        /// Optional expectedCapabilityVersions pins every used professional capability
        /// to a Host-declared version and requires an IMRKVersionedCapabilityResolver.
        /// Version drift during compilation fails closed before returning a bound Plan.
        /// This is a compile-time fence, not a substitute for publish-time authorization.
        /// </summary>
        public MOCCompiledPlan Bind(MEMProjectSnapshot project,
            MDCDeclarativePlan plan, MDCStaticCatalogSnapshot catalog,
            IMRKCapabilityResolver resolver,
            IReadOnlyDictionary<string,Type> professionalTypes = null,
            bool allowUnverifiedLegacyContracts = false,
            IReadOnlyDictionary<string,string> expectedCapabilityVersions = null)
        {
            if(plan==null)throw new ArgumentNullException("plan");
            plan.RequireCurrent(project,catalog);
            return Bind(project,plan.SourceManifest,catalog,resolver,
                professionalTypes,allowUnverifiedLegacyContracts,expectedCapabilityVersions);
        }

        /// <summary>
        /// Fail closed BEFORE invoking a provider's resolver for stale, incomplete
        /// or structurally invalid source. A resolver is probed only once per
        /// capability ID within this binding attempt and never executed.
        /// </summary>
        public MOCCompiledPlan Bind(MEMProjectSnapshot project,
            MDCStaticSourceManifest manifest, MDCStaticCatalogSnapshot catalog,
            IMRKCapabilityResolver resolver,
            IReadOnlyDictionary<string,Type> professionalTypes = null,
            bool allowUnverifiedLegacyContracts = false,
            IReadOnlyDictionary<string,string> expectedCapabilityVersions = null)
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

            // A Host-supplied pin must cover the entire static capability closure.
            // Reject missing, extra or invalid versions BEFORE TryResolve is called.
            // It is not authorization: Stage/Activate still require live Host checks.
            var capabilityIds=manifest.Contents.SelectMany(x=>x.CapabilityUses)
                .Select(x=>x.CapabilityId).Distinct(StringComparer.Ordinal)
                .OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            IMRKVersionedCapabilityResolver versioned =
                resolver as IMRKVersionedCapabilityResolver;
            if(expectedCapabilityVersions!=null)
            {
                if(versioned==null)
                    throw new InvalidOperationException("moc.bind.versioned_resolver_required");
                if(expectedCapabilityVersions.Count!=capabilityIds.Length ||
                    capabilityIds.Any(id=>!expectedCapabilityVersions.ContainsKey(id)) ||
                    expectedCapabilityVersions.Any(x=>x.Key==null ||
                        string.IsNullOrWhiteSpace(x.Value) || x.Value.Length>128 ||
                        x.Value.Any(char.IsControl)))
                    throw new InvalidOperationException("moc.bind.version_pin_incomplete");
            }
            var pinnedVersions=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string capabilityId in capabilityIds)
            {
                if(versioned==null)continue;
                string actualVersion;
                bool known=versioned.TryGetCapabilityVersion(capabilityId,out actualVersion);
                if(!known || string.IsNullOrWhiteSpace(actualVersion) ||
                    actualVersion.Length>128 || actualVersion.Any(char.IsControl))
                {
                    if(expectedCapabilityVersions!=null)
                        throw new InvalidOperationException(
                            "moc.bind.provider_version_unavailable:"+capabilityId);
                    continue; // Legacy versionless providers keep the old behavior.
                }
                string required;
                if(expectedCapabilityVersions!=null &&
                    (!expectedCapabilityVersions.TryGetValue(capabilityId,out required) ||
                     !StringComparer.Ordinal.Equals(actualVersion,required)))
                    throw new InvalidOperationException(
                        "moc.bind.provider_version_mismatch:"+capabilityId);
                pinnedVersions.Add(capabilityId,actualVersion);
            }

            // A compilation-local cache closes the preflight-to-compiler resolver
            // identity gap without creating a shared registration or execution store.
            CachedResolver pinned = resolver as IMRKVersionedCapabilityResolver != null
                ? (CachedResolver)new CachedVersionedResolver(
                    (IMRKVersionedCapabilityResolver)resolver)
                : new CachedResolver(resolver);
            var inspector = new MOCBindingContractInspector();
            foreach(string capabilityId in capabilityIds)
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
            // A provider may swap an algorithm/configuration between initial
            // resolution and compiler lowering. Never return a mixed-version Plan.
            foreach(var pinnedVersion in pinnedVersions)
            {
                string observed;
                if(!versioned.TryGetCapabilityVersion(pinnedVersion.Key,out observed) ||
                    !StringComparer.Ordinal.Equals(observed,pinnedVersion.Value))
                    throw new InvalidOperationException(
                        "moc.bind.provider_version_changed:"+pinnedVersion.Key);
            }
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
