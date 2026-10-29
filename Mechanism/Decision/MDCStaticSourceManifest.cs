using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>
    /// A source reference into the one authoritative MOL definition. This is
    /// not an executable MRK step and does not own a delegate or runtime state.
    /// </summary>
    public sealed class MDCStaticCapabilityUse
    {
        internal MDCStaticCapabilityUse(Guid contentId, Guid blockId, string capabilityId)
        { ContentId=contentId; BlockId=blockId; CapabilityId=capabilityId; }
        public Guid ContentId { get; private set; }
        public Guid BlockId { get; private set; }
        public string CapabilityId { get; private set; }
    }

    /// <summary>Immutable provenance for one Core Content's existing Logic Aspect.
    /// The original payload remains in Core, never duplicated here.</summary>
    public sealed class MDCStaticContentSource
    {
        internal MDCStaticContentSource(Guid id, int schema, string sourceHash,
            IEnumerable<Guid> references, IEnumerable<MDCStaticCapabilityUse> uses)
        {
            ContentId=id; SchemaVersion=schema; LogicFingerprint=sourceHash;
            ReferencedContentIds=new ReadOnlyCollection<Guid>(references.ToArray());
            CapabilityUses=new ReadOnlyCollection<MDCStaticCapabilityUse>(uses.ToArray());
        }
        public Guid ContentId { get; private set; }
        public int SchemaVersion { get; private set; }
        public string LogicFingerprint { get; private set; }
        public IReadOnlyList<Guid> ReferencedContentIds { get; private set; }
        public IReadOnlyList<MDCStaticCapabilityUse> CapabilityUses { get; private set; }
    }

    /// <summary>
    /// P0 source manifest only. No MRKExecutionPlan, compiled instructions,
    /// runtime capability bindings, live memory handles or duplicate Core graph.
    /// Unknown metadata prevents readiness but not source inspection.
    /// </summary>
    public sealed class MDCStaticSourceManifest
    {
        internal MDCStaticSourceManifest(Guid projectId, int revision, Guid root,
            string catalogHash, string fingerprint, IEnumerable<MDCStaticContentSource> sources,
            IEnumerable<string> unknown)
        {
            ProjectId=projectId; EngineeringRevision=revision; RootContentId=root;
            CatalogFingerprint=catalogHash; Fingerprint=fingerprint;
            Contents=new ReadOnlyCollection<MDCStaticContentSource>(sources.ToArray());
            UnknownCapabilityIds=new ReadOnlyCollection<string>(unknown.ToArray());
        }
        public Guid ProjectId { get; private set; }
        public int EngineeringRevision { get; private set; }
        public Guid RootContentId { get; private set; }
        public string CatalogFingerprint { get; private set; }
        public string Fingerprint { get; private set; }
        public IReadOnlyList<MDCStaticContentSource> Contents { get; private set; }
        public IReadOnlyList<string> UnknownCapabilityIds { get; private set; }
        public bool HasCompleteStaticMetadata { get { return UnknownCapabilityIds.Count==0; } }
        /// <summary>Does not check Runtime, permissions or current equipment state.</summary>
        public void RequireCurrent(MEMProjectSnapshot project, MDCStaticCatalogSnapshot catalog)
        {
            if(project==null)throw new ArgumentNullException("project");
            if(catalog==null)throw new ArgumentNullException("catalog");
            // 工程修订已不同，必须先报告Stale；新版源可能尚未形成合法可编译图，
            // 不能在重建Manifest时以图结构异常掩盖已经确定的版本失效。
            if(project.ProjectId!=ProjectId || project.Revision!=EngineeringRevision ||
                !StringComparer.Ordinal.Equals(CatalogFingerprint,catalog.Fingerprint))
                throw new InvalidOperationException("decision.source_manifest.stale");
            var fresh=new MDCStaticSourceManifestBuilder().Create(project,RootContentId,catalog);
            if (!StringComparer.Ordinal.Equals(Fingerprint,fresh.Fingerprint))
                throw new InvalidOperationException("decision.source_manifest.stale");
        }
    }

    /// <summary>
    /// Read-only facade over the genuine Core Snapshot and MOL V1/V2 codecs.
    /// It walks only the subflow closure, never expands loops or builds a new
    /// graph/Plan. All registered capabilities are verified by metadata only.
    /// </summary>
    public sealed class MDCStaticSourceManifestBuilder
    {
        public MDCStaticSourceManifest Create(MEMProjectSnapshot project, Guid rootContentId,
            MDCStaticCatalogSnapshot catalog)
        {
            if (project==null) throw new ArgumentNullException("project");
            if (catalog==null) throw new ArgumentNullException("catalog");
            if (rootContentId==Guid.Empty) throw new ArgumentException("decision.source_manifest.empty_root");
            var items=new Dictionary<Guid,MDCStaticContentSource>();
            var active=new HashSet<Guid>();
            var unknown=new SortedSet<string>(StringComparer.Ordinal);
            int blocks=0;
            Action<Guid,int> visit=null;
            visit=(id,depth)=>{
                if (depth>64 || items.Count>=1024)
                    throw new InvalidOperationException("decision.source_manifest.scope_budget");
                if (active.Contains(id))
                    throw new InvalidOperationException("decision.source_manifest.invoke_cycle");
                if (items.ContainsKey(id)) return;
                var node=project.Find(id);
                if (node==null || node.Kind!=MEMNodeKind.Content)
                    throw new InvalidOperationException("decision.source_manifest.missing_content:"+id.ToString("N"));
                var aspects=node.Aspects.Where(x=>x.Kind==MPMAspectKind.Logic &&
                    x.ProviderId==MOLLogicCodec.ProviderId).ToArray();
                if (aspects.Length!=1 || aspects[0].RequiresRemap)
                    throw new InvalidOperationException("decision.source_manifest.missing_or_unsupported_logic:"+id.ToString("N"));
                MPMAspect aspect=aspects[0];
                var uses=new List<MDCStaticCapabilityUse>();
                var children=new SortedSet<Guid>();
                active.Add(id);
                try
                {
                    MOLWiringDefinition wiring=MOLWiringCodec.Get(node);
                    if (wiring!=null)
                    {
                        wiring.ValidateExecutable();
                        foreach(var block in wiring.Blocks)
                        {
                            if(++blocks>131072)
                                throw new InvalidOperationException("decision.source_manifest.block_budget");
                            if(block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))
                            {
                                Guid target;
                                if (!Guid.TryParseExact(block.CapabilityId.Substring(11),"N",out target))
                                    throw new InvalidOperationException("decision.source_manifest.bad_invoke");
                                children.Add(target);
                            }
                            else if(block.CapabilityId!="moc.state-machine")
                                uses.Add(new MDCStaticCapabilityUse(id,block.Id,block.CapabilityId));
                        }
                        foreach(var machine in wiring.StateMachines)
                            foreach(var state in machine.Value.States)
                            {
                                if(state.EntryCapability.Length>0)
                                    uses.Add(new MDCStaticCapabilityUse(id,machine.Key,state.EntryCapability));
                                if(state.ExitCapability.Length>0)
                                    uses.Add(new MDCStaticCapabilityUse(id,machine.Key,state.ExitCapability));
                            }
                    }
                    else
                    {
                        MOLLogicDefinition legacy=MOLLogicCodec.Get(node);
                        if (legacy==null)
                            throw new InvalidOperationException("decision.source_manifest.unknown_schema");
                        foreach(var block in MDCLogicTraversal.Flatten(legacy.Root))
                        {
                            if(++blocks>131072)
                                throw new InvalidOperationException("decision.source_manifest.block_budget");
                            if(block.Kind==MOLControlKind.Invoke)
                            {
                                Guid target;
                                if (!Guid.TryParseExact(block.TargetFlow,"N",out target))
                                    throw new InvalidOperationException("decision.source_manifest.bad_invoke");
                                children.Add(target);
                            }
                            else if(block.Kind==MOLControlKind.Action)
                                uses.Add(new MDCStaticCapabilityUse(id,block.Id,block.CapabilityId));
                        }
                    }
                    foreach(var use in uses)
                        if(catalog.Find(use.CapabilityId)==null) unknown.Add(use.CapabilityId);
                    string sourceHash=HashAspect(aspect);
                    items.Add(id,new MDCStaticContentSource(id,aspect.SchemaVersion,sourceHash,
                        children,uses.OrderBy(x=>x.BlockId)
                        .ThenBy(x=>x.CapabilityId,StringComparer.Ordinal)));
                    foreach(Guid child in children)
                    {
                        var target=project.Find(child);
                        if(target==null || target.Kind!=MEMNodeKind.Content)
                            throw new InvalidOperationException(
                                "decision.source_manifest.missing_content:"+child.ToString("N"));
                        // Match the existing MOC compiler's mandatory Core ownership
                        // rule: a plain capability string is NOT an authorized reference.
                        if(!node.References.Any(r=>r.Kind==MRIReferenceKind.Content &&
                            r.TargetId==child))
                            throw new InvalidOperationException(
                                "decision.source_manifest.missing_content_reference:"+
                                node.Id.ToString("N")+":"+child.ToString("N"));
                        visit(child,depth+1);
                    }
                }
                finally { active.Remove(id); }
            };
            visit(rootContentId,0);
            var contents=items.Values.OrderBy(x=>x.ContentId).ToArray();
            string fingerprint=ComputeFingerprint(project.ProjectId,project.Revision,
                rootContentId,catalog.Fingerprint,contents);
            return new MDCStaticSourceManifest(project.ProjectId,project.Revision,rootContentId,
                catalog.Fingerprint,fingerprint,contents,unknown);
        }

        /// <summary>Pure, deterministic fingerprint of the immutable source
        /// manifest, also used to reject altered persisted Plan packages.</summary>
        internal static string ComputeFingerprint(Guid projectId,int revision,Guid root,
            string catalogFingerprint,IEnumerable<MDCStaticContentSource> sources)
        {
            var contents=sources.OrderBy(x=>x.ContentId).ToArray();
            return Hash(writer=>{
                writer.Write(projectId.ToByteArray());
                writer.Write(revision);
                writer.Write(root.ToByteArray());
                Write(writer,catalogFingerprint);
                writer.Write(contents.Length);
                foreach(var content in contents)
                {
                    writer.Write(content.ContentId.ToByteArray());
                    writer.Write(content.SchemaVersion);
                    Write(writer,content.LogicFingerprint);
                    writer.Write(content.ReferencedContentIds.Count);
                    foreach(Guid child in content.ReferencedContentIds)
                        writer.Write(child.ToByteArray());
                }
            });
        }

        private static string HashAspect(MPMAspect aspect)
        {
            return HashLogicPayload(aspect.SchemaVersion,aspect.Required,aspect.Payload);
        }

        /// <summary>Identical Core Aspect SHA256 proof for exported Plan payloads;
        /// the provider identity is always the one authoritative MOL codec.</summary>
        public static string HashLogicPayload(int schema,bool required,byte[] bytes)
        {
            if(bytes==null)throw new ArgumentNullException("bytes");
            return Hash(writer=>{
                Write(writer,MOLLogicCodec.ProviderId);
                writer.Write(schema);
                writer.Write(required);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            });
        }
        private static void Write(BinaryWriter writer,string value)
        {
            var bytes=Encoding.UTF8.GetBytes(value??"");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        private static string Hash(Action<BinaryWriter> writer)
        {
            using(var stream=new MemoryStream())
            {
                using(var binary=new BinaryWriter(stream,Encoding.UTF8,true))
                { writer(binary); binary.Flush(); }
                using(var sha=SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(stream.ToArray()))
                        .Replace("-","").ToLowerInvariant();
            }
        }
    }
}
