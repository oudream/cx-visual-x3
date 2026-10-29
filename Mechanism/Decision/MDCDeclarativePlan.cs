using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>
    /// D05 runtime-free descriptive intermediate representation.
    /// This is a transient compilation artifact, NOT a second editable Core graph.
    /// Every original MOL control/data semantic remains in the canonical Logic payload.
    /// No Runtime/Execution type, delegate, device handle or live allocation exists.
    /// </summary>
    public sealed class MDCPlanBlock
    {
        internal MDCPlanBlock(Guid id, MOLControlKind kind, string capability,
            Guid target, Guid parent, string role, int ordinal,
            int maxIterations, string collectionInput)
        {
            BlockId=id;Kind=kind;CapabilityId=capability??"";
            TargetContentId=target;ParentBlockId=parent;Role=role??"";
            Ordinal=ordinal;MaxIterations=maxIterations;
            CollectionInput=collectionInput??"";
        }
        public Guid BlockId {get;private set;}
        public MOLControlKind Kind {get;private set;}
        public string CapabilityId {get;private set;}
        public Guid TargetContentId {get;private set;}
        public Guid ParentBlockId {get;private set;}
        /// <summary>Original structural relation: Root, Child, Case, LoopBody,
        /// TryBody, Catch, Finally, NamedCatch; never an MRK dependency.</summary>
        public string Role {get;private set;}
        public int Ordinal {get;private set;}
        /// <summary>Dynamic loop budget is preserved, never expanded into steps.</summary>
        public int MaxIterations {get;private set;}
        public string CollectionInput {get;private set;}
    }

    public sealed class MDCPlanLink
    {
        internal MDCPlanLink(MOLLink link)
        {
            LinkId=link.Id;SourceBlockId=link.Source.BlockId;
            SourcePort=link.Source.PortMemberId;TargetBlockId=link.Target.BlockId;
            TargetPort=link.Target.PortMemberId;Kind=link.Kind;
            ConditionId=link.ConditionId;Order=link.Order;
        }
        public Guid LinkId {get;private set;}
        public Guid SourceBlockId {get;private set;}
        public string SourcePort {get;private set;}
        public Guid TargetBlockId {get;private set;}
        public string TargetPort {get;private set;}
        public MPCPortKind Kind {get;private set;}
        public string ConditionId {get;private set;}
        public int? Order {get;private set;}
    }

    public sealed class MDCPlanPort
    {
        internal MDCPlanPort(Guid blockId,MPCPortDescriptor port)
        {
            BlockId=blockId;MemberId=port.MemberId;TypeId=port.TypeId;
            Direction=port.Direction;Kind=port.Kind;Multiplicity=port.Multiplicity;
            Required=port.Required;IsPublic=port.IsPublic;
            UnitId=port.Contract==null?"":port.Contract.UnitId;
            CoordinateFrameId=port.Contract==null?"":port.Contract.CoordinateFrameId;
            TimeBaseId=port.Contract==null?"":port.Contract.TimeBaseId;
        }
        public Guid BlockId {get;private set;}
        public string MemberId {get;private set;}
        public string TypeId {get;private set;}
        public MPCPortDirection Direction {get;private set;}
        public MPCPortKind Kind {get;private set;}
        public MPCPortMultiplicity Multiplicity {get;private set;}
        public bool Required {get;private set;}
        public bool IsPublic {get;private set;}
        public string UnitId {get;private set;}
        public string CoordinateFrameId {get;private set;}
        public string TimeBaseId {get;private set;}
    }

    public sealed class MDCPlanNamedPort
    {
        internal MDCPlanNamedPort(string name,MOLBlockEndpoint endpoint)
        {
            Name=name;BlockId=endpoint.BlockId;PortMemberId=endpoint.PortMemberId;
        }
        public string Name {get;private set;}
        public Guid BlockId {get;private set;}
        public string PortMemberId {get;private set;}
    }

    /// <summary>Literal input assignments are definitions, not live MDS values.</summary>
    public sealed class MDCPlanLiteral
    {
        internal MDCPlanLiteral(Guid block,string name,string value)
        {BlockId=block;PortMemberId=name;Value=value;}
        public Guid BlockId {get;private set;}
        public string PortMemberId {get;private set;}
        public string Value {get;private set;}
    }

    public sealed class MDCPlanPolicy
    {
        internal MDCPlanPolicy(Guid block,MOLStepPolicy policy)
        {
            BlockId=block;MaxAttempts=policy.MaxAttempts;RetryDelayMs=policy.RetryDelayMs;
            TimeoutMs=policy.TimeoutMs;Idempotent=policy.Idempotent;
            ExecutionGroup=policy.ExecutionGroup;
        }
        public Guid BlockId {get;private set;}
        public int MaxAttempts {get;private set;}
        public int RetryDelayMs {get;private set;}
        public int TimeoutMs {get;private set;}
        public bool Idempotent {get;private set;}
        /// <summary>Declared group ID only; resource/permission owner remains Host.</summary>
        public string ExecutionGroup {get;private set;}
    }

    public sealed class MDCPlanContent
    {
        private readonly byte[] _source;
        internal MDCPlanContent(Guid contentId,int schemaVersion,bool isWiring,
            Guid entry,IEnumerable<MDCPlanBlock> blocks,IEnumerable<MDCPlanLink> links,
            IEnumerable<MDCPlanPort> ports,IEnumerable<MDCPlanNamedPort> inputs,
            IEnumerable<MDCPlanNamedPort> outputs,IEnumerable<MDCPlanLiteral> literals,
            IEnumerable<MDCPlanPolicy> policies,MDCPlanSemanticIndex semantics,byte[] source)
        {
            ContentId=contentId;SchemaVersion=schemaVersion;IsWiring=isWiring;
            EntryBlockId=entry;
            Blocks=new ReadOnlyCollection<MDCPlanBlock>(blocks.ToArray());
            Links=new ReadOnlyCollection<MDCPlanLink>(links.ToArray());
            Ports=new ReadOnlyCollection<MDCPlanPort>(ports.ToArray());
            FlowInputs=new ReadOnlyCollection<MDCPlanNamedPort>(inputs.ToArray());
            FlowOutputs=new ReadOnlyCollection<MDCPlanNamedPort>(outputs.ToArray());
            Literals=new ReadOnlyCollection<MDCPlanLiteral>(literals.ToArray());
            Policies=new ReadOnlyCollection<MDCPlanPolicy>(policies.ToArray());
            Semantics=semantics??throw new ArgumentNullException("semantics");
            _source=(byte[])source.Clone();
        }
        public Guid ContentId {get;private set;}
        public int SchemaVersion {get;private set;}
        public bool IsWiring {get;private set;}
        public Guid EntryBlockId {get;private set;}
        public IReadOnlyList<MDCPlanBlock> Blocks {get;private set;}
        public IReadOnlyList<MDCPlanLink> Links {get;private set;}
        public IReadOnlyList<MDCPlanPort> Ports {get;private set;}
        public IReadOnlyList<MDCPlanNamedPort> FlowInputs {get;private set;}
        public IReadOnlyList<MDCPlanNamedPort> FlowOutputs {get;private set;}
        public IReadOnlyList<MDCPlanLiteral> Literals {get;private set;}
        public IReadOnlyList<MDCPlanPolicy> Policies {get;private set;}
        public MDCPlanSemanticIndex Semantics {get;private set;}
        /// <summary>
        /// A defensive copy of Core's authoritative MOL codec payload.
        /// Preserves V1 expressions/branches/loop/try and V2 state machines,
        /// link guards, named data contracts etc. without inventing equivalents.
        /// It is source evidence, NOT executable instructions.
        /// </summary>
        public byte[] CopyCanonicalLogicPayload() {return (byte[])_source.Clone();}
    }

    public sealed class MDCDeclarativePlan
    {
        private readonly MDCStaticSourceManifest _source;
        internal MDCDeclarativePlan(MDCStaticSourceManifest source,
            IEnumerable<MDCPlanContent> contents)
        {
            _source=source;
            Contents=new ReadOnlyCollection<MDCPlanContent>(contents.ToArray());
            ProjectId=source.ProjectId;EngineeringRevision=source.EngineeringRevision;
            RootContentId=source.RootContentId;
            Fingerprint=source.Fingerprint;CatalogFingerprint=source.CatalogFingerprint;
        }
        public Guid ProjectId {get;private set;}
        public int EngineeringRevision {get;private set;}
        public Guid RootContentId {get;private set;}
        /// <summary>SHA256 of the Core Logic closure and static Capability catalog.</summary>
        public string Fingerprint {get;private set;}
        public string CatalogFingerprint {get;private set;}
        public IReadOnlyList<MDCPlanContent> Contents {get;private set;}
        public void RequireCurrent(MEMProjectSnapshot project,MDCStaticCatalogSnapshot catalog)
        { _source.RequireCurrent(project,catalog); }
        /// <summary>Current compatibility bridge can use the underlying source
        /// identity without owning a second editable engineering definition.</summary>
        public MDCStaticSourceManifest SourceManifest {get{return _source;}}
    }

    /// <summary>
    /// Static compiler phase 1, independent of MRK. It indexes the original
    /// structured V1 and port/edge-based V2 source, preserving canonical source
    /// for exact semantics not yet lowered to a runtime-free executable IR.
    /// </summary>
    public sealed class MDCDeclarativePlanCompiler
    {
        private sealed class Walk
        {
            internal MOLControlBlock Block;
            internal Guid Parent;
            internal string Role;
            internal int Ordinal;
        }

        public MDCDeclarativePlan Compile(MEMProjectSnapshot project,Guid root,
            MDCStaticCatalogSnapshot catalog)
        {
            if(project==null)throw new ArgumentNullException("project");
            if(catalog==null)throw new ArgumentNullException("catalog");
            var manifest=new MDCStaticSourceManifestBuilder().Create(project,root,catalog);
            if(!manifest.HasCompleteStaticMetadata)
                throw new InvalidOperationException("decision.plan.unknown_capability:"+
                    string.Join(",",manifest.UnknownCapabilityIds));
            var plan=new List<MDCPlanContent>();
            foreach(var description in manifest.Contents)
            {
                var node=project.Find(description.ContentId);
                var logic=node.Aspects.Single(x=>x.Kind==MPMAspectKind.Logic &&
                    x.ProviderId==MOLLogicCodec.ProviderId);
                var wiring=MOLWiringCodec.Get(node);
                if(wiring!=null)
                {
                    wiring.ValidateExecutable();
                    var blocks=wiring.Blocks.Select(x=>new MDCPlanBlock(
                        x.Id,x.Kind,x.CapabilityId,
                        GetInvoke(x.CapabilityId),Guid.Empty,"Wiring",0,0,""))
                        .OrderBy(x=>x.BlockId).ToArray();
                    var links=wiring.Links.Select(x=>new MDCPlanLink(x))
                        .OrderBy(x=>x.LinkId).ToArray();
                    var ports=wiring.Ports.SelectMany(x=>x.Value.Select(
                        p=>new MDCPlanPort(x.Key,p)))
                        .OrderBy(x=>x.BlockId).ThenBy(x=>x.MemberId,StringComparer.Ordinal);
                    var literals=wiring.Parameters.SelectMany(x=>x.Value.Select(
                        p=>new MDCPlanLiteral(x.Key,p.Key,p.Value)))
                        .OrderBy(x=>x.BlockId).ThenBy(x=>x.PortMemberId,StringComparer.Ordinal);
                    var policies=wiring.Policies.Select(x=>new MDCPlanPolicy(x.Key,x.Value))
                        .OrderBy(x=>x.BlockId);
                    plan.Add(new MDCPlanContent(node.Id,logic.SchemaVersion,true,
                        wiring.EntryBlockId,blocks,links,ports,
                        wiring.FlowInputs.Select(x=>new MDCPlanNamedPort(x.Key,x.Value))
                            .OrderBy(x=>x.Name,StringComparer.Ordinal),
                        wiring.FlowOutputs.Select(x=>new MDCPlanNamedPort(x.Key,x.Value))
                            .OrderBy(x=>x.Name,StringComparer.Ordinal),
                        literals,policies,MDCPlanSemanticCompiler.FromWiring(wiring),logic.Payload));
                }
                else
                {
                    var structured=MOLLogicCodec.Get(node);
                    if(structured==null)
                        throw new InvalidOperationException("decision.plan.unknown_logic_schema");
                    var blocks=FlattenStructured(structured.Root).ToArray();
                    plan.Add(new MDCPlanContent(node.Id,logic.SchemaVersion,false,
                        structured.Root.Id,blocks,new MDCPlanLink[0],
                        new MDCPlanPort[0],new MDCPlanNamedPort[0],
                        new MDCPlanNamedPort[0],new MDCPlanLiteral[0],
                        new MDCPlanPolicy[0],MDCPlanSemanticCompiler.FromStructured(structured),
                        logic.Payload));
                }
            }
            return new MDCDeclarativePlan(manifest,plan.OrderBy(x=>x.ContentId));
        }

        private static Guid GetInvoke(string capability)
        {
            if(capability==null ||
                !capability.StartsWith("moc.invoke:",StringComparison.Ordinal))
                return Guid.Empty;
            Guid target;
            if(!Guid.TryParseExact(capability.Substring("moc.invoke:".Length),
                "N",out target))
                throw new InvalidOperationException("decision.plan.bad_invoke");
            return target;
        }

        private static IEnumerable<MDCPlanBlock> FlattenStructured(MOLControlBlock root)
        {
            var work=new Stack<Walk>();
            var seen=new HashSet<Guid>();
            work.Push(new Walk{Block=root,Parent=Guid.Empty,Role="Root",Ordinal=0});
            while(work.Count>0)
            {
                var entry=work.Pop();
                var b=entry.Block;
                if(!seen.Add(b.Id))
                    throw new InvalidOperationException("decision.plan.duplicate_block");
                if(seen.Count>4096)
                    throw new InvalidOperationException("decision.plan.block_budget");
                Guid target=Guid.Empty;
                if(b.Kind==MOLControlKind.Invoke &&
                    !Guid.TryParseExact(b.TargetFlow,"N",out target))
                    throw new InvalidOperationException("decision.plan.bad_invoke");
                yield return new MDCPlanBlock(b.Id,b.Kind,b.CapabilityId,target,
                    entry.Parent,entry.Role,entry.Ordinal,
                    b.Loop==null?0:b.Loop.MaxIterations,
                    b.Loop==null?"":b.Loop.CollectionInput);
                var next=new List<Walk>();
                for(int i=0;i<b.Children.Count;i++)
                    next.Add(new Walk{Block=b.Children[i],Parent=b.Id,Role="Child",Ordinal=i});
                for(int i=0;i<b.Cases.Count;i++)
                    next.Add(new Walk{Block=b.Cases[i].Body,Parent=b.Id,Role="Case",
                        Ordinal=i});
                if(b.Loop!=null)
                    next.Add(new Walk{Block=b.Loop.Body,Parent=b.Id,Role="LoopBody"});
                if(b.TryRegion!=null)
                {
                    next.Add(new Walk{Block=b.TryRegion.TryBody,Parent=b.Id,Role="TryBody"});
                    if(b.TryRegion.CatchBody!=null)
                        next.Add(new Walk{Block=b.TryRegion.CatchBody,Parent=b.Id,Role="Catch"});
                    if(b.TryRegion.FinallyBody!=null)
                        next.Add(new Walk{Block=b.TryRegion.FinallyBody,Parent=b.Id,Role="Finally"});
                    for(int i=0;i<b.TryRegion.Catches.Count;i++)
                        next.Add(new Walk{Block=b.TryRegion.Catches[i].Body,
                            Parent=b.Id,Role="NamedCatch",Ordinal=i});
                }
                for(int i=next.Count-1;i>=0;i--)work.Push(next[i]);
            }
        }
    }
}
