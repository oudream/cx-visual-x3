using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CxVisual.Mechanism.Core
{
    // Only the Content owns this definition. Blocks are value-like records, not MEMNodes.
    public enum MOLControlKind { Action=1, Sequence=2, Branch=3, Loop=4, ForEach=5, Parallel=6, Invoke=7, Wait=8, Try=9, Exit=10 }
    public enum MOLBranchMode { If=1, Switch=2 }
    public enum MOLLoopMode { For=1, While=2, DoWhile=3 }
    public enum MOLExitKind { Break=1, Continue=2, Return=3 }
    public enum MOLExpressionSource { Constant=1, FlowInput=2, StepResult=3, Iteration=4 }
    public enum MOLComparison { Truthy=1, Equal=2, NotEqual=3, LessThan=4, LessOrEqual=5, GreaterThan=6, GreaterOrEqual=7 }

    public sealed class MOLExpression
    {
        public MOLExpression(MOLExpressionSource source, string name = null,
            MOLComparison comparison = MOLComparison.Truthy, string expected = null)
        {
            if (!Enum.IsDefined(typeof(MOLExpressionSource), source) ||
                !Enum.IsDefined(typeof(MOLComparison), comparison))
                throw new ArgumentException("Unknown expression operator");
            if ((source == MOLExpressionSource.FlowInput || source == MOLExpressionSource.StepResult) &&
                string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Expression source must have a name");
            if (comparison != MOLComparison.Truthy && expected == null)
                throw new ArgumentException("Comparison requires an expected value");
            Source = source; Name = name ?? ""; Comparison = comparison; Expected = expected;
        }
        public MOLExpressionSource Source { get; private set; }
        public string Name { get; private set; }
        public MOLComparison Comparison { get; private set; }
        public string Expected { get; private set; }
        public static MOLExpression InputEquals(string input, string value)
        { return new MOLExpression(MOLExpressionSource.FlowInput, input, MOLComparison.Equal, value); }
        public static MOLExpression InputTrue(string input)
        { return new MOLExpression(MOLExpressionSource.FlowInput, input); }
        public static MOLExpression Always(bool value)
        { return new MOLExpression(MOLExpressionSource.Constant, value ? "true" : "false"); }
    }

    public sealed class MOLBranchCase
    {
        public MOLBranchCase(MOLExpression condition, MOLControlBlock body, string label = null)
        { Body = body ?? throw new ArgumentNullException("body"); Condition = condition; Label = label ?? ""; }
        public MOLExpression Condition { get; private set; } // null = else/default
        public MOLControlBlock Body { get; private set; }
        public string Label { get; private set; }
    }

    public sealed class MOLLoopDefinition
    {
        public MOLLoopDefinition(MOLLoopMode mode, int maxIterations, MOLControlBlock body,
            MOLExpression condition = null, string collectionInput = null)
        {
            if (!Enum.IsDefined(typeof(MOLLoopMode), mode)) throw new ArgumentException("Unknown loop kind");
            if (maxIterations < 1 || maxIterations > 1024) throw new ArgumentOutOfRangeException("maxIterations");
            if (body == null) throw new ArgumentNullException("body");
            Mode = mode; MaxIterations = maxIterations; Body = body; Condition = condition;
            CollectionInput = collectionInput ?? "";
        }
        public MOLLoopMode Mode { get; private set; }
        public int MaxIterations { get; private set; }
        public MOLControlBlock Body { get; private set; }
        public MOLExpression Condition { get; private set; }
        public string CollectionInput { get; private set; }
    }

    public sealed class MOLControlBlock
    {
        public MOLControlBlock(Guid id, MOLControlKind kind, string capabilityId = null,
            IEnumerable<MOLControlBlock> children = null, IEnumerable<MOLBranchCase> cases = null,
            MOLLoopDefinition loop = null, MOLExpression condition = null, string targetFlow = null,
            MOLExitKind exitKind = MOLExitKind.Return)
        {
            if (id == Guid.Empty || !Enum.IsDefined(typeof(MOLControlKind), kind))
                throw new ArgumentException("Invalid logical block identity");
            Id = id; Kind = kind; CapabilityId = capabilityId ?? ""; Condition = condition;
            TargetFlow = targetFlow ?? ""; ExitKind = exitKind; Loop = loop;
            Children = Array.AsReadOnly((children ?? new MOLControlBlock[0]).ToArray());
            Cases = Array.AsReadOnly((cases ?? new MOLBranchCase[0]).ToArray());
        }
        public Guid Id { get; private set; }
        public MOLControlKind Kind { get; private set; }
        public string CapabilityId { get; private set; }
        public string TargetFlow { get; private set; }
        public MOLExpression Condition { get; private set; }
        public MOLLoopDefinition Loop { get; private set; }
        public MOLExitKind ExitKind { get; private set; }
        public IReadOnlyList<MOLControlBlock> Children { get; private set; }
        public IReadOnlyList<MOLBranchCase> Cases { get; private set; }
        public static MOLControlBlock Action(string capability)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Action, capability); }
        public static MOLControlBlock Sequence(params MOLControlBlock[] children)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Sequence, children: children); }
        public static MOLControlBlock Parallel(params MOLControlBlock[] children)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Parallel, children: children); }
        public static MOLControlBlock If(MOLExpression condition, MOLControlBlock yes, MOLControlBlock no = null)
        {
            if (condition == null) throw new ArgumentNullException("condition");
            var paths = new List<MOLBranchCase> { new MOLBranchCase(condition, yes, "True") };
            if (no != null) paths.Add(new MOLBranchCase(null, no, "Else"));
            return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Branch, cases: paths);
        }
        public static MOLControlBlock Repeat(MOLLoopMode mode, int count, MOLControlBlock body, MOLExpression condition = null)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Loop, loop: new MOLLoopDefinition(mode, count, body, condition)); }
        public static MOLControlBlock Each(string input, int maxItems, MOLControlBlock body)
        { return new MOLControlBlock(Guid.NewGuid(), MOLControlKind.ForEach, loop:
            new MOLLoopDefinition(MOLLoopMode.For, maxItems, body, null, input)); }
    }

    public sealed class MOLLogicDefinition
    {
        public MOLLogicDefinition(MOLControlBlock root)
        { Root = root ?? throw new ArgumentNullException("root"); Validate(); }
        public MOLControlBlock Root { get; private set; }
        public void Validate()
        {
            var ids = new HashSet<Guid>();
            int count = 0;
            Action<MOLControlBlock, int> walk = null;
            walk = (block, depth) =>
            {
                if (block == null || depth > 32 || ++count > 4096 || !ids.Add(block.Id))
                    throw new InvalidOperationException("logic.invalid_tree_or_identity");
                if (!Enum.IsDefined(typeof(MOLControlKind), block.Kind))
                    throw new InvalidOperationException("logic.unknown_kind");
                if (block.Kind != MOLControlKind.Sequence && block.Kind != MOLControlKind.Parallel &&
                    block.Children.Count != 0)
                    throw new InvalidOperationException("logic.unexpected_children");
                if (block.Kind != MOLControlKind.Branch && block.Cases.Count != 0)
                    throw new InvalidOperationException("logic.unexpected_cases");
                if (block.Kind != MOLControlKind.Loop && block.Kind != MOLControlKind.ForEach &&
                    block.Loop != null)
                    throw new InvalidOperationException("logic.unexpected_loop");
                switch (block.Kind)
                {
                    case MOLControlKind.Action:
                        if (string.IsNullOrWhiteSpace(block.CapabilityId)) throw new InvalidOperationException("logic.capability_missing");
                        break;
                    case MOLControlKind.Sequence:
                    case MOLControlKind.Parallel:
                        if (block.Children.Count == 0) throw new InvalidOperationException("logic.empty_container");
                        break;
                    case MOLControlKind.Branch:
                        if (block.Cases.Count == 0) throw new InvalidOperationException("logic.no_cases");
                        int defaults = 0;
                        for (int i = 0; i < block.Cases.Count; i++)
                        {
                            if (block.Cases[i] == null) throw new InvalidOperationException("logic.null_case");
                            if (block.Cases[i].Condition == null && (++defaults > 1 || i != block.Cases.Count - 1))
                                throw new InvalidOperationException("logic.default_must_be_last");
                            walk(block.Cases[i].Body, depth + 1);
                        }
                        break;
                    case MOLControlKind.Loop:
                    case MOLControlKind.ForEach:
                        if (block.Loop == null) throw new InvalidOperationException("logic.loop_missing");
                        if (block.Kind == MOLControlKind.Loop && block.Loop.Mode != MOLLoopMode.For &&
                            block.Loop.Condition == null)
                            throw new InvalidOperationException("logic.while_needs_condition");
                        if (block.Kind == MOLControlKind.ForEach && string.IsNullOrWhiteSpace(block.Loop.CollectionInput))
                            throw new InvalidOperationException("logic.collection_missing");
                        walk(block.Loop.Body, depth + 1);
                        break;
                    case MOLControlKind.Invoke:
                        if (string.IsNullOrWhiteSpace(block.TargetFlow)) throw new InvalidOperationException("logic.invoke_missing");
                        break;
                }
                foreach (var child in block.Children) walk(child, depth + 1);
            };
            walk(Root, 0);
        }
    }

    // Xml payload is written into the existing optional Logic Aspect using the ordinary Session transaction.
    public static class MOLLogicCodec
    {
        public const string ProviderId = "cx.logic.structured";
        public const int SchemaVersion = 1;
        public static void Set(MEMEngineeringSession session, MEMContent content, MOLLogicDefinition definition)
        {
            if (session == null || content == null || definition == null)
                throw new ArgumentNullException("session/content/definition");
            session.SetAspect(content, new MPMAspect(MPMAspectKind.Logic, ProviderId,
                SchemaVersion, Serialize(definition)));
        }
        public static MOLLogicDefinition Get(MEMNodeSnapshot content)
        {
            if (content == null) throw new ArgumentNullException("content");
            var aspect = content.Aspects.SingleOrDefault(a =>
                a.Kind == MPMAspectKind.Logic && a.ProviderId == ProviderId);
            if (aspect == null) return null;
            if (aspect.SchemaVersion != SchemaVersion || aspect.RequiresRemap)
                throw new InvalidDataException("logic.unsupported_schema_or_remap");
            return Deserialize(aspect.Payload);
        }
        public static byte[] Serialize(MOLLogicDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            definition.Validate();
            var root = new XElement("MOL", new XAttribute("v", SchemaVersion), WriteBlock(definition.Root));
            var bytes = Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            if (bytes.Length > 4 * 1024 * 1024) throw new InvalidDataException("logic.payload_too_large");
            return bytes;
        }
        public static MOLLogicDefinition Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 4 * 1024 * 1024)
                throw new InvalidDataException("logic.invalid_payload");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 };
            using (var input = new MemoryStream(payload))
            using (var reader = XmlReader.Create(input, settings))
            {
                var root = XDocument.Load(reader).Root;
                if (root == null || root.Name != "MOL" || (string)root.Attribute("v") != "1")
                    throw new InvalidDataException("logic.unsupported_schema");
                var blocks = root.Elements("Block").ToArray();
                if (blocks.Length != 1 || root.Elements().Count() != 1)
                    throw new InvalidDataException("logic.invalid_root");
                return new MOLLogicDefinition(ReadBlock(blocks[0], 0));
            }
        }
        private static XElement WriteExpression(MOLExpression value)
        {
            if (value == null) return null;
            return new XElement("Expression", new XAttribute("source", value.Source),
                new XAttribute("name", value.Name), new XAttribute("compare", value.Comparison),
                new XAttribute("expected", value.Expected ?? ""));
        }
        private static MOLExpression ReadExpression(XElement node)
        {
            if (node == null) return null;
            return new MOLExpression((MOLExpressionSource)Enum.Parse(typeof(MOLExpressionSource),
                (string)node.Attribute("source")), (string)node.Attribute("name"),
                (MOLComparison)Enum.Parse(typeof(MOLComparison), (string)node.Attribute("compare")),
                (string)node.Attribute("compare") == "Truthy" ? null : (string)node.Attribute("expected"));
        }
        private static XElement WriteBlock(MOLControlBlock b)
        {
            var e = new XElement("Block", new XAttribute("id", b.Id.ToString("N")),
                new XAttribute("kind", b.Kind), new XAttribute("cap", b.CapabilityId),
                new XAttribute("target", b.TargetFlow), new XAttribute("exit", b.ExitKind));
            if (b.Condition != null) e.Add(new XElement("Guard", WriteExpression(b.Condition)));
            foreach (var c in b.Children) e.Add(WriteBlock(c));
            foreach (var c in b.Cases)
            {
                var branch = new XElement("Case", new XAttribute("label", c.Label));
                if (c.Condition != null) branch.Add(WriteExpression(c.Condition));
                branch.Add(WriteBlock(c.Body)); e.Add(branch);
            }
            if (b.Loop != null)
            {
                var l = b.Loop;
                var loop = new XElement("Loop", new XAttribute("mode", l.Mode),
                    new XAttribute("max", l.MaxIterations),
                    new XAttribute("collection", l.CollectionInput));
                if (l.Condition != null) loop.Add(WriteExpression(l.Condition));
                loop.Add(WriteBlock(l.Body)); e.Add(loop);
            }
            return e;
        }
        private static MOLControlBlock ReadBlock(XElement e, int depth)
        {
            if (depth > 32 || e.Name != "Block") throw new InvalidDataException("logic.depth_or_type");
            if (e.Elements().Any(x => x.Name != "Block" && x.Name != "Case" &&
                x.Name != "Loop" && x.Name != "Guard"))
                throw new InvalidDataException("logic.unknown_field");
            if (e.Elements("Loop").Count() > 1 || e.Elements("Guard").Count() > 1)
                throw new InvalidDataException("logic.duplicate_field");
            Guid id;
            if (!Guid.TryParseExact((string)e.Attribute("id"), "N", out id))
                throw new InvalidDataException("logic.bad_id");
            MOLControlKind kind = (MOLControlKind)Enum.Parse(typeof(MOLControlKind),
                (string)e.Attribute("kind"));
            var blocks = e.Elements("Block").Select(c => ReadBlock(c, depth + 1)).ToArray();
            var cases = e.Elements("Case").Select(c => new MOLBranchCase(
                ReadExpression(c.Element("Expression")),
                ReadBlock(c.Elements("Block").Single(), depth + 1),
                (string)c.Attribute("label"))).ToArray();
            MOLLoopDefinition loop = null;
            var le = e.Element("Loop");
            if (le != null)
                loop = new MOLLoopDefinition((MOLLoopMode)Enum.Parse(typeof(MOLLoopMode),
                    (string)le.Attribute("mode")), (int)le.Attribute("max"),
                    ReadBlock(le.Elements("Block").Single(), depth + 1),
                    ReadExpression(le.Element("Expression")), (string)le.Attribute("collection"));
            return new MOLControlBlock(id, kind, (string)e.Attribute("cap"), blocks, cases,
                loop, ReadExpression(e.Element("Guard") == null ? null : e.Element("Guard").Element("Expression")),
                (string)e.Attribute("target"), (MOLExitKind)Enum.Parse(typeof(MOLExitKind),
                    (string)e.Attribute("exit")));
        }
    }

    // V2自由连线：逻辑块是Content内的值记录，不扩展MEM工程对象树。
    public sealed class MOLBlockEndpoint : IEquatable<MOLBlockEndpoint>
    {
        public MOLBlockEndpoint(Guid blockId, string portMemberId)
        { if(blockId==Guid.Empty)throw new ArgumentException("wiring.empty_block");
          BlockId=blockId;PortMemberId=MEMNode.NormalizeName(portMemberId); }
        public Guid BlockId{get;private set;}
        public string PortMemberId{get;private set;}
        public bool Equals(MOLBlockEndpoint other)
        {return other!=null&&BlockId==other.BlockId&&PortMemberId==other.PortMemberId;}
        public override bool Equals(object obj){return Equals(obj as MOLBlockEndpoint);}
        public override int GetHashCode(){return BlockId.GetHashCode()*397^PortMemberId.GetHashCode();}
    }
    public sealed class MOLLink
    {
        public MOLLink(Guid id,MOLBlockEndpoint source,MOLBlockEndpoint target,
            MPCPortKind kind,string conditionId=null,int? order=null)
        {
            if(id==Guid.Empty||source==null||target==null||!Enum.IsDefined(typeof(MPCPortKind),kind)||
                (order.HasValue&&order.Value<0))throw new ArgumentException("wiring.invalid_link");
            Id=id;Source=source;Target=target;Kind=kind;ConditionId=conditionId??"";Order=order;
        }
        public Guid Id{get;private set;}
        public MOLBlockEndpoint Source{get;private set;}
        public MOLBlockEndpoint Target{get;private set;}
        public MPCPortKind Kind{get;private set;}
        public string ConditionId{get;private set;}
        public int? Order{get;private set;}
    }
    /// <summary>每个Content只有一个Logic槽。V2的自由连接是权威定义，不额外保存AST。</summary>
    public sealed class MOLWiringDefinition
    {
        public MOLWiringDefinition(Guid entryBlockId,IEnumerable<MOLControlBlock> blocks,
            IDictionary<Guid,IEnumerable<MPCPortDescriptor>> ports,IEnumerable<MOLLink> links)
        {
            EntryBlockId=entryBlockId;
            Blocks=new ReadOnlyCollection<MOLControlBlock>((blocks??new MOLControlBlock[0]).ToArray());
            Links=new ReadOnlyCollection<MOLLink>((links??new MOLLink[0]).ToArray());
            var map=new Dictionary<Guid,IReadOnlyList<MPCPortDescriptor>>();
            if(ports!=null)foreach(var kv in ports)
                map.Add(kv.Key,new ReadOnlyCollection<MPCPortDescriptor>((kv.Value??Enumerable.Empty<MPCPortDescriptor>()).ToArray()));
            Ports=new ReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>>(map);
            Validate();
        }
        public Guid EntryBlockId{get;private set;}
        public IReadOnlyList<MOLControlBlock> Blocks{get;private set;}
        public IReadOnlyList<MOLLink> Links{get;private set;}
        public IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> Ports{get;private set;}
        public void Validate()
        {
            if(EntryBlockId==Guid.Empty||Blocks.Count<1||Blocks.Count>4096||Links.Count>32768)
                throw new InvalidOperationException("wiring.budget");
            if(Blocks.Any(x=>x==null||x.Kind!=MOLControlKind.Action||string.IsNullOrWhiteSpace(x.CapabilityId)))
                throw new InvalidOperationException("wiring.action_only");
            var ids=new HashSet<Guid>();
            foreach(var b in Blocks)if(!ids.Add(b.Id))throw new InvalidOperationException("wiring.duplicate_block");
            if(!ids.Contains(EntryBlockId)||Ports.Count!=ids.Count||Ports.Keys.Any(x=>!ids.Contains(x)))
                throw new InvalidOperationException("wiring.scope");
            var schemas=new Dictionary<Guid,Dictionary<string,MPCPortDescriptor>>();
            foreach(var b in Blocks)
            {
                IReadOnlyList<MPCPortDescriptor> defs;
                if(!Ports.TryGetValue(b.Id,out defs))throw new InvalidOperationException("wiring.missing_schema");
                var lookup=new Dictionary<string,MPCPortDescriptor>(StringComparer.Ordinal);
                foreach(var p in defs)
                {
                    if(p==null||lookup.ContainsKey(p.MemberId))throw new InvalidOperationException("wiring.duplicate_port");
                    lookup.Add(p.MemberId,p);
                }
                schemas.Add(b.Id,lookup);
            }
            var linkIds=new HashSet<Guid>();
            var edgeKeys=new HashSet<string>();
            var incoming=new Dictionary<string,int>();
            var successors=ids.ToDictionary(x=>x,x=>new List<Guid>());
            var controlSuccessors=ids.ToDictionary(x=>x,x=>new List<Guid>());
            foreach(var link in Links)
            {
                if(link==null||!linkIds.Add(link.Id))throw new InvalidOperationException("wiring.duplicate_link");
                if((link.Kind!=MPCPortKind.Control&&link.Kind!=MPCPortKind.Data)||
                    !string.IsNullOrEmpty(link.ConditionId)||link.Order.HasValue)
                    throw new InvalidOperationException("wiring.unsupported_semantics");
                if(!ids.Contains(link.Source.BlockId)||!ids.Contains(link.Target.BlockId)||
                    link.Source.BlockId==link.Target.BlockId)
                    throw new InvalidOperationException("wiring.foreign_endpoint");
                MPCPortDescriptor from,to;
                if(!schemas[link.Source.BlockId].TryGetValue(link.Source.PortMemberId,out from)||
                   !schemas[link.Target.BlockId].TryGetValue(link.Target.PortMemberId,out to))
                    throw new InvalidOperationException("wiring.unknown_port");
                if(from.Kind!=link.Kind||to.Kind!=link.Kind||
                    from.Direction==MPCPortDirection.Input||to.Direction==MPCPortDirection.Output||
                    !string.Equals(from.TypeId,to.TypeId,StringComparison.Ordinal)||
                    !object.Equals(from.Contract,to.Contract))
                    throw new InvalidOperationException("wiring.port_contract");
                string edge=link.Source.BlockId+":"+link.Source.PortMemberId+">"+
                    link.Target.BlockId+":"+link.Target.PortMemberId;
                if(!edgeKeys.Add(edge))throw new InvalidOperationException("wiring.duplicate_edge");
                string toKey=link.Target.BlockId+":"+link.Target.PortMemberId;
                int n;incoming.TryGetValue(toKey,out n);incoming[toKey]=++n;
                if(n>1&&to.Multiplicity==MPCPortMultiplicity.One)
                    throw new InvalidOperationException("wiring.port_multiplicity");
                successors[link.Source.BlockId].Add(link.Target.BlockId);
                if(link.Kind==MPCPortKind.Control)controlSuccessors[link.Source.BlockId].Add(link.Target.BlockId);
            }
            // 未完成连线的编辑草稿可以保存；构建/运行时由ValidateExecutable拒绝。
            // 连接类型、方向、重复、环路等无效结构仍严格拒绝。
            var colors=ids.ToDictionary(x=>x,x=>0);
            Action<Guid> visit=null;
            visit=id=>{
                if(colors[id]==1)throw new InvalidOperationException("wiring.cycle");
                if(colors[id]==2)return;
                colors[id]=1;foreach(var next in successors[id])visit(next);colors[id]=2;
            };
            foreach(var id in ids)visit(id);
        }
        /// <summary>只有可执行的完整自由连线才能提交给MOC/MRK，草稿不能运行。</summary>
        public void ValidateExecutable()
        {
            Validate();
            var incoming=Links.GroupBy(x=>x.Target.BlockId.ToString("N")+":"+x.Target.PortMemberId)
                .ToDictionary(x=>x.Key,x=>x.Count(),StringComparer.Ordinal);
            foreach(var b in Blocks)foreach(var port in Ports[b.Id])
                if(port.Required&&port.Direction!=MPCPortDirection.Output&&
                    !incoming.ContainsKey(b.Id.ToString("N")+":"+port.MemberId))
                    throw new InvalidOperationException("wiring.required_port:"+b.Id+":"+port.MemberId);
            var reached=new HashSet<Guid>();
            var pending=new Queue<Guid>();pending.Enqueue(EntryBlockId);
            var controls=Links.Where(x=>x.Kind==MPCPortKind.Control)
                .ToLookup(x=>x.Source.BlockId,x=>x.Target.BlockId);
            while(pending.Count>0)
            {
                var id=pending.Dequeue();
                if(!reached.Add(id))continue;
                foreach(var next in controls[id])pending.Enqueue(next);
            }
            if(reached.Count!=Blocks.Count)
                throw new InvalidOperationException("wiring.unreachable_action");
        }
    }
    /// <summary>V1(cx.logic.structured/1)原样保留；V2在同一Logic Aspect中原子替换。</summary>
    public static class MOLWiringCodec
    {
        public const int SchemaVersion=2;
        public static MPMAspect CreateAspect(MOLWiringDefinition definition)
        {return new MPMAspect(MPMAspectKind.Logic,MOLLogicCodec.ProviderId,2,Serialize(definition));}
        public static void Set(MEMEngineeringSession session,MEMContent content,MOLWiringDefinition definition)
        {if(session==null||content==null)throw new ArgumentNullException("session/content");
         session.SetAspect(content,CreateAspect(definition));}
        public static MOLWiringDefinition Get(MEMNodeSnapshot node)
        {
            if(node==null)throw new ArgumentNullException("node");
            var found=node.Aspects.SingleOrDefault(x=>x.Kind==MPMAspectKind.Logic&&x.ProviderId==MOLLogicCodec.ProviderId);
            if(found==null||found.SchemaVersion==1)return null;
            if(found.SchemaVersion!=2||found.RequiresRemap)throw new InvalidDataException("wiring.unknown_schema");
            return Deserialize(found.Payload);
        }
        public static byte[] Serialize(MOLWiringDefinition def)
        {
            if(def==null)throw new ArgumentNullException("def");def.Validate();
            var root=new XElement("MOL",new XAttribute("v",2),new XAttribute("entry",def.EntryBlockId.ToString("N")),
                new XElement("Blocks",def.Blocks.Select(b=>new XElement("Block",
                    new XAttribute("id",b.Id.ToString("N")),new XAttribute("cap",b.CapabilityId),
                    new XElement("Ports",def.Ports[b.Id].Select(p=>new XElement("Port",
                        new XAttribute("id",p.MemberId),new XAttribute("dir",p.Direction),
                        new XAttribute("type",p.TypeId),new XAttribute("kind",p.Kind),
                        new XAttribute("mul",p.Multiplicity),new XAttribute("required",p.Required),
                        new XAttribute("unit",p.Contract.UnitId),
                        new XAttribute("frame",p.Contract.CoordinateFrameId),
                        new XAttribute("time",p.Contract.TimeBaseId))))))),
                new XElement("Links",def.Links.Select(l=>new XElement("Link",
                    new XAttribute("id",l.Id.ToString("N")),new XAttribute("kind",l.Kind),
                    new XAttribute("source",l.Source.BlockId.ToString("N")),
                    new XAttribute("out",l.Source.PortMemberId),
                    new XAttribute("target",l.Target.BlockId.ToString("N")),
                    new XAttribute("in",l.Target.PortMemberId),
                    new XAttribute("condition",l.ConditionId),
                    new XAttribute("order",l.Order.HasValue?l.Order.Value.ToString(CultureInfo.InvariantCulture):"")))));
            byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            if(bytes.Length>4*1024*1024)throw new InvalidDataException("wiring.payload_budget");
            return bytes;
        }
        public static MOLWiringDefinition Deserialize(byte[] bytes)
        {
            if(bytes==null||bytes.Length<1||bytes.Length>4*1024*1024)throw new InvalidDataException("wiring.payload_invalid");
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024};
            using(var memory=new MemoryStream(bytes))using(var reader=XmlReader.Create(memory,settings))
            {
                var root=XDocument.Load(reader).Root;
                if(root==null||root.Name!="MOL"||(string)root.Attribute("v")!="2"||
                    root.Elements().Count()!=2||root.Element("Blocks")==null||root.Element("Links")==null)
                    throw new InvalidDataException("wiring.schema");
                Guid entry=ReadGuid(root,"entry");
                var blocks=new List<MOLControlBlock>();
                var ports=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>();
                foreach(var b in root.Element("Blocks").Elements("Block"))
                {
                    if(b.Element("Ports")==null||b.Elements().Count()!=1)
                        throw new InvalidDataException("wiring.block_schema");
                    Guid id=ReadGuid(b,"id");
                    blocks.Add(new MOLControlBlock(id,MOLControlKind.Action,(string)b.Attribute("cap")));
                    ports.Add(id,b.Element("Ports").Elements("Port").Select(p=>
                        new MPCPortDescriptor((string)p.Attribute("id"),ReadEnum<MPCPortDirection>(p,"dir"),
                            (string)p.Attribute("type"),ReadEnum<MPCPortKind>(p,"kind"),
                            ReadEnum<MPCPortMultiplicity>(p,"mul"),
                            new MTUContractReference((string)p.Attribute("unit"),(string)p.Attribute("frame"),
                                (string)p.Attribute("time")),false,(bool?)p.Attribute("required")??false)).ToArray());
                }
                var links=root.Element("Links").Elements("Link").Select(l=>new MOLLink(
                    ReadGuid(l,"id"),new MOLBlockEndpoint(ReadGuid(l,"source"),(string)l.Attribute("out")),
                    new MOLBlockEndpoint(ReadGuid(l,"target"),(string)l.Attribute("in")),
                    ReadEnum<MPCPortKind>(l,"kind"),(string)l.Attribute("condition"),
                    string.IsNullOrEmpty((string)l.Attribute("order"))?(int?)null:
                        int.Parse((string)l.Attribute("order"),CultureInfo.InvariantCulture))).ToArray();
                return new MOLWiringDefinition(entry,blocks,ports,links);
            }
        }
        private static Guid ReadGuid(XElement x,string name)
        {Guid id;if(!Guid.TryParseExact((string)x.Attribute(name),"N",out id)||id==Guid.Empty)
            throw new InvalidDataException("wiring.guid:"+name);return id;}
        private static T ReadEnum<T>(XElement x,string name)where T:struct
        {T v;if(!Enum.TryParse<T>((string)x.Attribute(name),false,out v)||!Enum.IsDefined(typeof(T),v))
            throw new InvalidDataException("wiring.enum:"+name);return v;}
    }

}
