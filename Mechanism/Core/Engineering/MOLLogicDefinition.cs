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
            // R05动态区域只编译一次：迭代上限用于运行时安全预算，不再限制为静态展开的1024次。
            if (maxIterations < 1) throw new ArgumentOutOfRangeException("maxIterations");
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
        /// <summary>R02：按输入顺序选择第一个成立的Case；最后允许一个Else/Default。</summary>
        public static MOLControlBlock Branch(params MOLBranchCase[] cases)
        {
            if(cases==null)throw new ArgumentNullException("cases");
            var branch=new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Branch,cases:cases);
            new MOLLogicDefinition(branch); // 立即检查Default次序和非法分支，不留坏定义。
            return branch;
        }
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
    /// <summary>编排画布位置，只负责展示；不参与MRK计划或执行依赖。</summary>
    public sealed class MOLBlockPosition
    {
        public MOLBlockPosition(double x,double y)
        {
            if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(y)||double.IsInfinity(y)||
                x<0||y<0||x>100000||y>100000)
                throw new ArgumentOutOfRangeException("wiring.position");
            X=x;Y=y;
        }
        public double X {get;private set;}
        public double Y {get;private set;}
    }

    /// <summary>仅为Logic画布编辑组；没有执行语义，不等于可调用Content子流程。</summary>
    public sealed class MOLSelectionGroup
    {
        public MOLSelectionGroup(Guid id,string name,IEnumerable<Guid> members)
        {
            if(id==Guid.Empty||members==null)throw new ArgumentException("wiring.group_identity");
            Guid[] ids=members.ToArray();
            if(ids.Length<2||ids.Any(x=>x==Guid.Empty)||ids.Distinct().Count()!=ids.Length)
                throw new ArgumentException("wiring.group_members");
            Id=id;Name=MEMNode.NormalizeName(name);
            BlockIds=Array.AsReadOnly(ids);
        }
        public Guid Id{get;private set;}
        public string Name{get;private set;}
        public IReadOnlyList<Guid> BlockIds{get;private set;}
    }

    /// <summary>状态及分区是同一Logic Aspect中的值对象，不是第二棵CxNode树。</summary>
    public sealed class MOLStateDescriptor
    {
        public MOLStateDescriptor(string id,string region,string parent=null,bool initial=false)
        {
            Id=Check(id);Region=Check(region);
            ParentId=string.IsNullOrEmpty(parent)?"":Check(parent);
            if(Id==ParentId)throw new ArgumentException("moc.state.self_parent");
            Initial=initial;
        }
        internal static string Check(string input)
        {
            if(string.IsNullOrWhiteSpace(input)||input.Length>128||input.Any(char.IsControl))
                throw new ArgumentException("moc.state.bad_id");
            return input.Trim().Normalize();
        }
        public string Id{get;private set;}
        public string Region{get;private set;}
        public string ParentId{get;private set;}
        public bool Initial{get;private set;}
    }
    public sealed class MOLStateTransition
    {
        public MOLStateTransition(string source,string eventId,string target)
        {
            Source=MOLStateDescriptor.Check(source);
            EventId=MOLStateDescriptor.Check(eventId);
            Target=MOLStateDescriptor.Check(target);
        }
        public string Source{get;private set;}
        public string EventId{get;private set;}
        public string Target{get;private set;}
    }
    /// <summary>G05d：确定性层级状态及正交Region，同一事件按Region键顺序分别转移。</summary>
    public sealed class MOLStateMachineDefinition
    {
        public IReadOnlyList<MOLStateDescriptor> States{get;private set;}
        public IReadOnlyList<MOLStateTransition> Transitions{get;private set;}
        public MOLStateMachineDefinition(IEnumerable<MOLStateDescriptor> states,
            IEnumerable<MOLStateTransition> transitions)
        {
            var entries=(states??Enumerable.Empty<MOLStateDescriptor>()).ToArray();
            var edges=(transitions??Enumerable.Empty<MOLStateTransition>()).ToArray();
            if(entries.Length==0||entries.Length>256||edges.Length>512||
                entries.Any(x=>x==null)||edges.Any(x=>x==null)||
                entries.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=entries.Length)
                throw new InvalidOperationException("moc.state.definition_budget_or_duplicate");
            var lookup=entries.ToDictionary(x=>x.Id,StringComparer.Ordinal);
            foreach(var state in entries)
            {
                var current=state;
                var ancestors=new HashSet<string>(StringComparer.Ordinal);
                while(current.ParentId.Length>0)
                {
                    if(!ancestors.Add(current.Id)||
                        !lookup.ContainsKey(current.ParentId)||
                        lookup[current.ParentId].Region!=state.Region)
                        throw new InvalidOperationException("moc.state.parent_cycle_or_region");
                    current=lookup[current.ParentId];
                }
            }
            foreach(var region in entries.GroupBy(s=>s.Region,StringComparer.Ordinal))
            {
                if(region.Count(s=>s.ParentId.Length==0&&s.Initial)!=1)
                    throw new InvalidOperationException("moc.state.missing_region_initial");
            }
            foreach(var parent in entries)
            {
                var children=entries.Where(s=>s.ParentId==parent.Id).ToArray();
                if(children.Length>0&&children.Count(s=>s.Initial)!=1)
                    throw new InvalidOperationException("moc.state.missing_nested_initial");
            }
            var transitionKeys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var edge in edges)
            {
                MOLStateDescriptor from,to;
                if(!lookup.TryGetValue(edge.Source,out from)||
                    !lookup.TryGetValue(edge.Target,out to)||from.Region!=to.Region||
                    !transitionKeys.Add(edge.Source+"\u001f"+edge.EventId))
                    throw new InvalidOperationException("moc.state.invalid_transition");
            }
            States=Array.AsReadOnly(entries);Transitions=Array.AsReadOnly(edges);
        }
        private string ResolveLeaf(string id,IDictionary<string,MOLStateDescriptor> map)
        {
            while(true)
            {
                var initial=States.SingleOrDefault(s=>s.ParentId==id&&s.Initial);
                if(initial==null)return id;
                id=initial.Id;
            }
        }
        /// <summary>每个输入事件严格处理一次；最多1万条事件，取消逐事件可观察。</summary>
        public IReadOnlyDictionary<string,string> Run(IEnumerable<string> events,
            System.Threading.CancellationToken cancellation=
                default(System.Threading.CancellationToken))
        {
            if(events==null)throw new ArgumentNullException("events");
            var map=States.ToDictionary(s=>s.Id,StringComparer.Ordinal);
            var active=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var region in States.GroupBy(s=>s.Region,StringComparer.Ordinal))
                active.Add(region.Key,ResolveLeaf(region.Single(s=>
                    s.ParentId.Length==0&&s.Initial).Id,map));
            int count=0;
            foreach(string eventId in events)
            {
                cancellation.ThrowIfCancellationRequested();
                if(++count>10000)throw new InvalidOperationException("moc.state.event_budget");
                string eventName=MOLStateDescriptor.Check(eventId);
                foreach(var region in active.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray())
                {
                    var current=map[active[region]];
                    while(true)
                    {
                        var rule=Transitions.FirstOrDefault(x=>x.Source==current.Id&&
                            x.EventId==eventName);
                        if(rule!=null)
                        {
                            active[region]=ResolveLeaf(rule.Target,map);
                            break;
                        }
                        if(current.ParentId.Length==0)break;
                        current=map[current.ParentId];
                    }
                }
            }
            return new ReadOnlyDictionary<string,string>(active);
        }
    }

    /// <summary>G05c：幂等工作流重试/超时的工程配置；执行仍归MRKExecutionPolicy。</summary>
    public sealed class MOLStepPolicy
    {
        public MOLStepPolicy(int maxAttempts=1,int retryDelayMs=0,int timeoutMs=0,
            bool idempotent=false)
        {
            if(maxAttempts<1||maxAttempts>10||retryDelayMs<0||retryDelayMs>60000||
                timeoutMs<0||timeoutMs>600000||(maxAttempts>1&&!idempotent))
                throw new ArgumentException("wiring.invalid_workflow_policy");
            MaxAttempts=maxAttempts;RetryDelayMs=retryDelayMs;TimeoutMs=timeoutMs;
            Idempotent=idempotent;
        }
        public int MaxAttempts{get;private set;}
        public int RetryDelayMs{get;private set;}
        public int TimeoutMs{get;private set;}
        public bool Idempotent{get;private set;}
    }

    public sealed class MOLWiringDefinition
    {
        public MOLWiringDefinition(Guid entryBlockId,IEnumerable<MOLControlBlock> blocks,
            IDictionary<Guid,IEnumerable<MPCPortDescriptor>> ports,IEnumerable<MOLLink> links,
            IDictionary<Guid,MOLBlockPosition> positions=null,
            IEnumerable<MOLSelectionGroup> groups=null,
            IDictionary<Guid,IDictionary<string,string>> parameters=null,
            IDictionary<Guid,MOLStepPolicy> policies=null,
            IDictionary<Guid,MOLStateMachineDefinition> stateMachines=null,
            IDictionary<string,MOLBlockEndpoint> flowInputs=null,
            IDictionary<string,MOLBlockEndpoint> flowOutputs=null)
        {
            EntryBlockId=entryBlockId;
            Blocks=new ReadOnlyCollection<MOLControlBlock>((blocks??new MOLControlBlock[0]).ToArray());
            Links=new ReadOnlyCollection<MOLLink>((links??new MOLLink[0]).ToArray());
            var map=new Dictionary<Guid,IReadOnlyList<MPCPortDescriptor>>();
            if(ports!=null)foreach(var kv in ports)
                map.Add(kv.Key,new ReadOnlyCollection<MPCPortDescriptor>((kv.Value??Enumerable.Empty<MPCPortDescriptor>()).ToArray()));
            Ports=new ReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>>(map);
            var locations=new Dictionary<Guid,MOLBlockPosition>();
            if(positions!=null)foreach(var pair in positions)
            {
                if(pair.Value==null)throw new ArgumentException("wiring.null_position");
                locations.Add(pair.Key,pair.Value);
            }
            Positions=new ReadOnlyDictionary<Guid,MOLBlockPosition>(locations);
            Groups=Array.AsReadOnly((groups??Enumerable.Empty<MOLSelectionGroup>()).ToArray());
            var values=new Dictionary<Guid,IReadOnlyDictionary<string,string>>();
            if(parameters!=null)foreach(var group in parameters)
            {
                if(group.Value==null)throw new ArgumentException("wiring.params_null");
                values.Add(group.Key,new ReadOnlyDictionary<string,string>(
                    new Dictionary<string,string>(group.Value,StringComparer.Ordinal)));
            }
            Parameters=new ReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>>(values);
            var strategy=new Dictionary<Guid,MOLStepPolicy>();
            if(policies!=null)foreach(var entry in policies)
            {
                if(entry.Value==null)throw new ArgumentException("wiring.null_policy");
                strategy.Add(entry.Key,entry.Value);
            }
            Policies=new ReadOnlyDictionary<Guid,MOLStepPolicy>(strategy);
            var machines=new Dictionary<Guid,MOLStateMachineDefinition>();
            if(stateMachines!=null)foreach(var item in stateMachines)
            {
                if(item.Value==null)throw new ArgumentException("wiring.null_state_machine");
                machines.Add(item.Key,item.Value);
            }
            StateMachines=new ReadOnlyDictionary<Guid,MOLStateMachineDefinition>(machines);
            FlowInputs=new ReadOnlyDictionary<string,MOLBlockEndpoint>(
                new Dictionary<string,MOLBlockEndpoint>(flowInputs??
                    new Dictionary<string,MOLBlockEndpoint>(),StringComparer.Ordinal));
            FlowOutputs=new ReadOnlyDictionary<string,MOLBlockEndpoint>(
                new Dictionary<string,MOLBlockEndpoint>(flowOutputs??
                    new Dictionary<string,MOLBlockEndpoint>(),StringComparer.Ordinal));
            Validate();
        }
        public Guid EntryBlockId{get;private set;}
        public IReadOnlyList<MOLControlBlock> Blocks{get;private set;}
        public IReadOnlyList<MOLLink> Links{get;private set;}
        public IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> Ports{get;private set;}
        public IReadOnlyDictionary<Guid,MOLBlockPosition> Positions{get;private set;}
        public IReadOnlyList<MOLSelectionGroup> Groups{get;private set;}
        /// <summary>端口字面量参数，按BlockId绑定到已有Data Input；不是另一个参数真源。</summary>
        public IReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>> Parameters{get;private set;}
        public IReadOnlyDictionary<Guid,MOLStepPolicy> Policies{get;private set;}
        public IReadOnlyDictionary<Guid,MOLStateMachineDefinition> StateMachines{get;private set;}
        /// <summary>对外公开的子流程端口别名→内部节点端口（仍属同一 Logic Aspect）。</summary>
        public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowInputs{get;private set;}
        public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowOutputs{get;private set;}
        public void Validate()
        {
            // G04：合法空草稿可保存，不能交给MOC/MRK执行。
            if(Blocks.Count==0)
            {
                if(EntryBlockId!=Guid.Empty||Links.Count!=0||Ports.Count!=0||
                    Positions.Count!=0||Groups.Count!=0||Parameters.Count!=0||
                    Policies.Count!=0||StateMachines.Count!=0||FlowInputs.Count!=0||FlowOutputs.Count!=0)
                    throw new InvalidOperationException("wiring.empty_draft_has_references");
                return;
            }
            if(EntryBlockId==Guid.Empty||Blocks.Count>4096||Links.Count>32768)
                throw new InvalidOperationException("wiring.budget");
            if(Blocks.Any(x=>x==null||x.Kind!=MOLControlKind.Action||string.IsNullOrWhiteSpace(x.CapabilityId)))
                throw new InvalidOperationException("wiring.action_only");
            var ids=new HashSet<Guid>();
            foreach(var b in Blocks)if(!ids.Add(b.Id))throw new InvalidOperationException("wiring.duplicate_block");
            if(!ids.Contains(EntryBlockId)||Ports.Count!=ids.Count||Ports.Keys.Any(x=>!ids.Contains(x))||
                Positions.Keys.Any(x=>!ids.Contains(x)))
                throw new InvalidOperationException("wiring.scope");
            if(Groups.Count>1024)throw new InvalidOperationException("wiring.group_budget");
            var groupIds=new HashSet<Guid>();
            var groupedMembers=new HashSet<Guid>();
            foreach(var group in Groups)
            {
                if(group==null||!groupIds.Add(group.Id)||
                    group.BlockIds.Any(id=>!ids.Contains(id)||!groupedMembers.Add(id)))
                    throw new InvalidOperationException("wiring.group_overlap_or_scope");
            }
            if(StateMachines.Count>128||StateMachines.Keys.Any(id=>!ids.Contains(id))||
                StateMachines.Keys.Any(id=>Blocks.Single(b=>b.Id==id).CapabilityId!="moc.state-machine"))
                throw new InvalidOperationException("wiring.state_machine_scope");
            if(Policies.Count>4096||Policies.Keys.Any(id=>!ids.Contains(id)))
                throw new InvalidOperationException("wiring.policy_scope");
            if(Parameters.Keys.Any(id=>!ids.Contains(id))||
                Parameters.Sum(group=>group.Value.Count)>4096)
                throw new InvalidOperationException("wiring.parameters_scope");
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
            if(FlowInputs.Count+FlowOutputs.Count>256)
                throw new InvalidOperationException("wiring.public_ports_budget");
            Action<IReadOnlyDictionary<string,MOLBlockEndpoint>,MPCPortDirection> checkPublic =
                (publicPorts,direction)=>{
                    foreach(var exposed in publicPorts)
                    {
                        string name=exposed.Key;
                        if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.Contains(".")||
                           name.Any(char.IsControl)||exposed.Value==null)
                            throw new InvalidOperationException("wiring.invalid_public_port");
                        Dictionary<string,MPCPortDescriptor> registered;
                        MPCPortDescriptor port;
                        if(!schemas.TryGetValue(exposed.Value.BlockId,out registered)||
                           !registered.TryGetValue(exposed.Value.PortMemberId,out port)||
                           port.Kind!=MPCPortKind.Data||port.Direction!=direction)
                            throw new InvalidOperationException("wiring.public_port_contract");
                        if(direction==MPCPortDirection.Input &&
                           (Links.Any(l=>(l.Kind==MPCPortKind.Data||l.Kind==MPCPortKind.Stream)&&
                               l.Target.Equals(exposed.Value)) ||
                            (Parameters.ContainsKey(exposed.Value.BlockId)&&
                             Parameters[exposed.Value.BlockId].ContainsKey(exposed.Value.PortMemberId))))
                            throw new InvalidOperationException("wiring.public_input_conflict");
                    }
                };
            checkPublic(FlowInputs,MPCPortDirection.Input);
            checkPublic(FlowOutputs,MPCPortDirection.Output);
            if(FlowInputs.Values.GroupBy(x=>x).Any(x=>x.Count()!=1))
                throw new InvalidOperationException("wiring.public_input_duplicate_target");
            foreach(var group in Parameters)
            foreach(var entry in group.Value)
            {
                MPCPortDescriptor port;
                if(string.IsNullOrWhiteSpace(entry.Key)||entry.Key.Length>128||
                    entry.Key.Any(char.IsControl)||entry.Value==null||
                    entry.Value.Length>8192||
                    !schemas[group.Key].TryGetValue(entry.Key,out port)||
                    port.Kind!=MPCPortKind.Data||port.Direction!=MPCPortDirection.Input)
                    throw new InvalidOperationException("wiring.invalid_parameter");
                if(Links.Any(l=>l.Kind==MPCPortKind.Data&&
                    l.Target.BlockId==group.Key&&l.Target.PortMemberId==entry.Key))
                    throw new InvalidOperationException("wiring.parameter_link_conflict");
                switch(port.TypeId)
                {
                    case "string":case "System.String":break;
                    case "bool":case "System.Boolean":
                        bool b;if(!bool.TryParse(entry.Value,out b))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    case "int":case "System.Int32":
                        int iv;if(!int.TryParse(entry.Value,NumberStyles.Integer,
                            CultureInfo.InvariantCulture,out iv))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    case "long":case "System.Int64":
                        long lv;if(!long.TryParse(entry.Value,NumberStyles.Integer,
                            CultureInfo.InvariantCulture,out lv))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    case "double":case "System.Double":
                        double dv;if(!double.TryParse(entry.Value,NumberStyles.Float,
                            CultureInfo.InvariantCulture,out dv)||double.IsInfinity(dv)||
                            double.IsNaN(dv))
                            throw new InvalidOperationException("wiring.parameter_type");break;
                    default:throw new InvalidOperationException("wiring.parameter_unknown_type");
                }
            }
            var linkIds=new HashSet<Guid>();
            var edgeKeys=new HashSet<string>();
            var incoming=new Dictionary<string,int>();
            var successors=ids.ToDictionary(x=>x,x=>new List<Guid>());
            var controlSuccessors=ids.ToDictionary(x=>x,x=>new List<Guid>());
            foreach(var link in Links)
            {
                if(link==null||!linkIds.Add(link.Id))throw new InvalidOperationException("wiring.duplicate_link");
                if((link.Kind!=MPCPortKind.Control&&link.Kind!=MPCPortKind.Data&&
                     link.Kind!=MPCPortKind.Stream)||
                    link.Order.HasValue||link.ConditionId.Length>128||
                    (link.Kind!=MPCPortKind.Control&&!string.IsNullOrEmpty(link.ConditionId))||
                    (!string.IsNullOrEmpty(link.ConditionId)&&
                     link.ConditionId!="bool:true"&&link.ConditionId!="bool:false"&&
                     link.ConditionId!="default"&&
                     !(link.ConditionId.StartsWith("case:",StringComparison.Ordinal)&&
                       link.ConditionId.Length>5)))
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
            // 同一条件源不得混用无条件出口；Switch case/default必须互斥。
            foreach(var group in Links.Where(l=>l.Kind==MPCPortKind.Control)
                .GroupBy(l=>l.Source.BlockId))
            {
                var guarded=group.Where(l=>l.ConditionId.Length>0).ToArray();
                if(guarded.Length==0)continue;
                if(guarded.Length!=group.Count()||
                    guarded.Select(l=>l.ConditionId).Distinct(StringComparer.Ordinal).Count()!=guarded.Length||
                    (guarded.Any(l=>l.ConditionId.StartsWith("bool:",StringComparison.Ordinal))&&
                     guarded.Any(l=>!l.ConditionId.StartsWith("bool:",StringComparison.Ordinal)))||
                    (guarded.Any(l=>l.ConditionId.StartsWith("case:",StringComparison.Ordinal)||l.ConditionId=="default")&&
                     guarded.Any(l=>l.ConditionId.StartsWith("bool:",StringComparison.Ordinal))))
                    throw new InvalidOperationException("wiring.invalid_branch_guards");
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
            if(Blocks.Count==0)throw new InvalidOperationException("wiring.empty_executable");
            if(Links.Where(l=>l.Kind==MPCPortKind.Control&&l.ConditionId.Length>0)
                .Any(l=>Links.Count(x=>x.Kind==MPCPortKind.Control&&
                    x.Target.BlockId==l.Target.BlockId)>1))
                throw new InvalidOperationException("wiring.conditional_join_requires_explicit_node");
            var incoming=Links.GroupBy(x=>x.Target.BlockId.ToString("N")+":"+x.Target.PortMemberId)
                .ToDictionary(x=>x.Key,x=>x.Count(),StringComparer.Ordinal);
            foreach(var b in Blocks)foreach(var port in Ports[b.Id])
                if(port.Required&&port.Direction!=MPCPortDirection.Output&&
                    !incoming.ContainsKey(b.Id.ToString("N")+":"+port.MemberId)&&
                    !FlowInputs.Values.Any(x=>x.BlockId==b.Id&&x.PortMemberId==port.MemberId)&&
                    !(port.Kind==MPCPortKind.Data&&Parameters.ContainsKey(b.Id)&&
                      Parameters[b.Id].ContainsKey(port.MemberId)))
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
                    def.Positions.ContainsKey(b.Id)?
                        new XAttribute("x",def.Positions[b.Id].X.ToString("R",CultureInfo.InvariantCulture)):null,
                    def.Positions.ContainsKey(b.Id)?
                        new XAttribute("y",def.Positions[b.Id].Y.ToString("R",CultureInfo.InvariantCulture)):null,
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
            if(def.StateMachines.Count>0)
                root.Add(new XElement("StateMachines",def.StateMachines.Select(item=>
                    new XElement("Machine",new XAttribute("block",item.Key.ToString("N")),
                        new XElement("States",item.Value.States.Select(state=>
                            new XElement("State",new XAttribute("id",state.Id),
                                new XAttribute("region",state.Region),
                                new XAttribute("parent",state.ParentId),
                                new XAttribute("initial",state.Initial)))),
                        new XElement("Transitions",item.Value.Transitions.Select(edge=>
                            new XElement("Transition",new XAttribute("from",edge.Source),
                                new XAttribute("event",edge.EventId),
                                new XAttribute("to",edge.Target))))))));
            if(def.Policies.Count>0)
                root.Add(new XElement("Policies",def.Policies.Select(item=>
                    new XElement("Policy",new XAttribute("block",item.Key.ToString("N")),
                        new XAttribute("attempts",item.Value.MaxAttempts),
                        new XAttribute("delay",item.Value.RetryDelayMs),
                        new XAttribute("timeout",item.Value.TimeoutMs),
                        new XAttribute("idempotent",item.Value.Idempotent)))));
            if(def.Parameters.Count>0)
                root.Add(new XElement("Parameters",def.Parameters.Select(group=>
                    new XElement("Block",new XAttribute("id",group.Key.ToString("N")),
                        group.Value.Select(item=>new XElement("Value",
                            new XAttribute("port",item.Key),
                            new XAttribute("text",item.Value)))))));
            if(def.FlowInputs.Count>0)
                root.Add(new XElement("FlowInputs",def.FlowInputs.Select(x=>
                    new XElement("Port",new XAttribute("name",x.Key),
                        new XAttribute("block",x.Value.BlockId.ToString("N")),
                        new XAttribute("member",x.Value.PortMemberId)))));
            if(def.FlowOutputs.Count>0)
                root.Add(new XElement("FlowOutputs",def.FlowOutputs.Select(x=>
                    new XElement("Port",new XAttribute("name",x.Key),
                        new XAttribute("block",x.Value.BlockId.ToString("N")),
                        new XAttribute("member",x.Value.PortMemberId)))));
            if(def.Groups.Count>0)
                root.Add(new XElement("Groups",def.Groups.Select(g=>new XElement("Group",
                    new XAttribute("id",g.Id.ToString("N")),new XAttribute("name",g.Name),
                    g.BlockIds.Select(id=>new XElement("Member",
                        new XAttribute("id",id.ToString("N"))))))));
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
                    root.Element("Blocks")==null||root.Element("Links")==null||
                    root.Elements().Count()<2||root.Elements().Count()>8||
                    root.Elements().Any(x=>x.Name!="Blocks"&&x.Name!="Links"&&
                        x.Name!="Groups"&&x.Name!="Parameters"&&x.Name!="Policies"&&
                        x.Name!="StateMachines"&&x.Name!="FlowInputs"&&x.Name!="FlowOutputs")||
                    root.Elements().GroupBy(x=>x.Name).Any(x=>x.Count()!=1))
                    throw new InvalidDataException("wiring.schema");
                string rawEntry=(string)root.Attribute("entry");
                Guid entry=root.Element("Blocks").Elements("Block").Any()?
                    ReadGuid(root,"entry"):
                    rawEntry==Guid.Empty.ToString("N")?Guid.Empty:ReadGuid(root,"entry");
                var blocks=new List<MOLControlBlock>();
                var ports=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>();
                var positions=new Dictionary<Guid,MOLBlockPosition>();
                foreach(var b in root.Element("Blocks").Elements("Block"))
                {
                    if(b.Element("Ports")==null||b.Elements().Count()!=1)
                        throw new InvalidDataException("wiring.block_schema");
                    Guid id=ReadGuid(b,"id");
                    string sx=(string)b.Attribute("x"),sy=(string)b.Attribute("y");
                    if(sx!=null||sy!=null)
                    {
                        double x,y;
                        if(sx==null||sy==null||
                            !double.TryParse(sx,NumberStyles.Float,CultureInfo.InvariantCulture,out x)||
                            !double.TryParse(sy,NumberStyles.Float,CultureInfo.InvariantCulture,out y))
                            throw new InvalidDataException("wiring.invalid_position");
                        positions.Add(id,new MOLBlockPosition(x,y));
                    }
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
                var groups=root.Element("Groups")==null?new MOLSelectionGroup[0]:
                    root.Element("Groups").Elements("Group").Select(g=>
                        new MOLSelectionGroup(ReadGuid(g,"id"),(string)g.Attribute("name"),
                            g.Elements("Member").Select(m=>ReadGuid(m,"id")))).ToArray();
                var parameters=new Dictionary<Guid,IDictionary<string,string>>();
                if(root.Element("Parameters")!=null)
                foreach(var group in root.Element("Parameters").Elements("Block"))
                {
                    Guid id=ReadGuid(group,"id");
                    var values=new Dictionary<string,string>(StringComparer.Ordinal);
                    foreach(var p in group.Elements("Value"))
                    {
                        string name=(string)p.Attribute("port");
                        if(string.IsNullOrWhiteSpace(name)||p.Attribute("text")==null)
                            throw new InvalidDataException("wiring.parameter_schema");
                        values.Add(name,(string)p.Attribute("text"));
                    }
                    parameters.Add(id,values);
                }
                var policies=new Dictionary<Guid,MOLStepPolicy>();
                if(root.Element("Policies")!=null)
                foreach(var policy in root.Element("Policies").Elements("Policy"))
                {
                    Guid id=ReadGuid(policy,"block");
                    var instance=new MOLStepPolicy(
                        int.Parse((string)policy.Attribute("attempts"),CultureInfo.InvariantCulture),
                        int.Parse((string)policy.Attribute("delay"),CultureInfo.InvariantCulture),
                        int.Parse((string)policy.Attribute("timeout"),CultureInfo.InvariantCulture),
                        bool.Parse((string)policy.Attribute("idempotent")));
                    policies.Add(id,instance);
                }
                var stateMachines=new Dictionary<Guid,MOLStateMachineDefinition>();
                if(root.Element("StateMachines")!=null)
                foreach(var machine in root.Element("StateMachines").Elements("Machine"))
                {
                    if(machine.Element("States")==null||machine.Element("Transitions")==null||
                        machine.Elements().Count()!=2)
                        throw new InvalidDataException("wiring.state_machine_schema");
                    Guid id=ReadGuid(machine,"block");
                    var states=machine.Element("States").Elements("State").Select(state=>
                        new MOLStateDescriptor((string)state.Attribute("id"),
                            (string)state.Attribute("region"),(string)state.Attribute("parent"),
                            (bool?)state.Attribute("initial")??false)).ToArray();
                    var transitions=machine.Element("Transitions").Elements("Transition").Select(edge=>
                        new MOLStateTransition((string)edge.Attribute("from"),
                            (string)edge.Attribute("event"),(string)edge.Attribute("to"))).ToArray();
                    stateMachines.Add(id,new MOLStateMachineDefinition(states,transitions));
                }
                Func<string,Dictionary<string,MOLBlockEndpoint>> readPublic=tag=>{
                    var result=new Dictionary<string,MOLBlockEndpoint>(StringComparer.Ordinal);
                    XElement node=root.Element(tag);
                    if(node!=null)
                    {
                        if(node.Elements().Any(x=>x.Name!="Port"))throw new InvalidDataException("wiring.public_port_schema");
                        foreach(var item in node.Elements("Port"))
                        {
                            string name=(string)item.Attribute("name");
                            if(string.IsNullOrWhiteSpace(name))throw new InvalidDataException("wiring.public_port_name");
                            result.Add(name,new MOLBlockEndpoint(ReadGuid(item,"block"),
                                (string)item.Attribute("member")));
                        }
                    }
                    return result;
                };
                return new MOLWiringDefinition(entry,blocks,ports,links,positions,groups,parameters,
                    policies,stateMachines,readPublic("FlowInputs"),readPublic("FlowOutputs"));
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
