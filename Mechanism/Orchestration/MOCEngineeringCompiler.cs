using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    public sealed class MOCStepOrigin
    {
        internal MOCStepOrigin(string step, MRIObjectAddress address, Guid block,
            IEnumerable<Guid> callPath, int? iteration, string role)
        {
            StepId = step; ContentAddress = address; BlockId = block;
            CallPath = Array.AsReadOnly(callPath.ToArray()); Iteration = iteration; Role = role;
        }
        public string StepId { get; private set; }
        public MRIObjectAddress ContentAddress { get; private set; }
        public Guid BlockId { get; private set; }
        public IReadOnlyList<Guid> CallPath { get; private set; }
        public int? Iteration { get; private set; }
        public string Role { get; private set; }
    }

    public sealed class MOCSourceMap
    {
        private readonly IReadOnlyDictionary<string, MOCStepOrigin> _steps;
        internal MOCSourceMap(IEnumerable<MOCStepOrigin> origins)
        {
            _steps = new ReadOnlyDictionary<string, MOCStepOrigin>(
                origins.ToDictionary(s => s.StepId, s => s, StringComparer.Ordinal));
        }
        public IReadOnlyDictionary<string, MOCStepOrigin> Steps { get { return _steps; } }
        public bool TryResolve(string compiledStepId, out MOCStepOrigin origin)
        { return _steps.TryGetValue(compiledStepId, out origin); }
        public IList<MOCStepOrigin> Find(Guid blockId)
        {
            // 循环同一Action会生成多个Step；按真实迭代序号稳定呈现，
            // 而不是按编译ID的字典序列出（该ID包含运行结构前缀）。
            return _steps.Values.Where(o => o.BlockId == blockId)
                .OrderBy(o => o.Iteration.HasValue ? 0 : 1)
                .ThenBy(o => o.Iteration ?? int.MaxValue)
                .ThenBy(o => o.StepId, StringComparer.Ordinal).ToList().AsReadOnly();
        }
    }

    public sealed class MOCObservedStep
    {
        internal MOCObservedStep(MRKExecutionTransition transition, MOCStepOrigin origin)
        { Transition = transition; Origin = origin; }
        public MRKExecutionTransition Transition { get; private set; }
        public MOCStepOrigin Origin { get; private set; }
    }
    public sealed class MOCTraceView
    {
        internal MOCTraceView(IList<MOCObservedStep> events, long cursor, long lost)
        { Events = new ReadOnlyCollection<MOCObservedStep>(events); NextCursor = cursor; LostCount = lost; }
        public IList<MOCObservedStep> Events { get; private set; }
        public long NextCursor { get; private set; }
        public long LostCount { get; private set; }
    }

    public sealed class MOCCompiledPlan
    {
        internal MOCCompiledPlan(MRKExecutionPlan plan, MOCSourceMap source, Guid projectId, int revision)
        { Plan = plan; SourceMap = source; ProjectId = projectId; EngineeringRevision = revision; }
        public MRKExecutionPlan Plan { get; private set; }
        public MOCSourceMap SourceMap { get; private set; }
        public Guid ProjectId { get; private set; }
        public int EngineeringRevision { get; private set; }
        public MOCTraceView ReadTrace(MRKFlowHandle handle, long afterSequence)
        {
            if (handle == null) throw new ArgumentNullException("handle");
            if (handle.Context.PlanId != Plan.Id) throw new InvalidOperationException("moc.foreign_plan");
            var batch = handle.Context.Trace.ReadSince(afterSequence);
            var result = new List<MOCObservedStep>();
            foreach (var change in batch.Changes)
            {
                MOCStepOrigin from;
                if (!SourceMap.TryResolve(change.StepId, out from))
                    throw new InvalidOperationException("moc.missing_source:" + change.StepId);
                result.Add(new MOCObservedStep(change, from));
            }
            return new MOCTraceView(result, batch.NextCursor, batch.LostCount);
        }
    }

    // Evaluates only typed, bounded declarative operands. Never compiles user text as C#.
    internal static class MOCExpressions
    {
        internal static bool Evaluate(MOLExpression expr, MRKExecutionContext context, int iteration, string resolvedStepId = null)
        {
            if (expr == null) return true;
            object actual = null;
            switch (expr.Source)
            {
                case MOLExpressionSource.Constant: actual = expr.Name; break;
                case MOLExpressionSource.FlowInput:
                    if (!context.Inputs.TryGetValue(expr.Name, out actual))
                        throw new InvalidOperationException("moc.input_missing:" + expr.Name);
                    break;
                case MOLExpressionSource.StepResult:
                    if (resolvedStepId == null || !context.TryGetAbsoluteResult(resolvedStepId, out actual))
                        throw new InvalidOperationException("moc.step_result_missing:" + expr.Name);
                    break;
                case MOLExpressionSource.Iteration: actual = iteration; break;
                default: throw new InvalidOperationException("moc.expression_source");
            }
            if (expr.Comparison == MOLComparison.Truthy)
            {
                if (actual is bool) return (bool)actual;
                if (actual is string)
                {
                    bool value;
                    if (bool.TryParse((string)actual, out value)) return value;
                }
                if (actual is int) return (int)actual != 0;
                throw new InvalidOperationException("moc.truth_type");
            }
            int order;
            decimal numberA, numberB;
            string left = Convert.ToString(actual, CultureInfo.InvariantCulture);
            string right = expr.Expected;
            bool booleanA, booleanB;
            if (bool.TryParse(left, out booleanA) && bool.TryParse(right, out booleanB))
                order = booleanA.CompareTo(booleanB);
            else if (decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out numberA)
                && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out numberB))
                order = numberA.CompareTo(numberB);
            else order = string.CompareOrdinal(left, right);
            switch (expr.Comparison)
            {
                case MOLComparison.Equal: return order == 0;
                case MOLComparison.NotEqual: return order != 0;
                case MOLComparison.LessThan: return order < 0;
                case MOLComparison.LessOrEqual: return order <= 0;
                case MOLComparison.GreaterThan: return order > 0;
                case MOLComparison.GreaterOrEqual: return order >= 0;
                default: throw new InvalidOperationException("moc.expression_compare");
            }
        }
    }

    internal sealed class MOCResolver : IMRKCapabilityResolver
    {
        private readonly IMRKCapabilityResolver _underlying;
        private readonly Dictionary<string, MRKResolvedCapability> _internal =
            new Dictionary<string, MRKResolvedCapability>(StringComparer.Ordinal);
        internal MOCResolver(IMRKCapabilityResolver underlying)
        {
            _underlying = underlying;
            _internal.Add("moc.internal.noop", new MRKResolvedCapability(
                (c,t) => Task.FromResult((object)true), new MRKExecutionOutput(typeof(bool))));
        }
        internal string Register(string id, Func<MRKExecutionContext, object> execute, Type type)
        {
            if (_internal.ContainsKey(id)) throw new InvalidOperationException("moc.internal_duplicate");
            _internal.Add(id, new MRKResolvedCapability(
                (ctx, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(execute(ctx)); },
                new MRKExecutionOutput(type, true)));
            return id;
        }
        public bool TryResolve(string capabilityId, out MRKResolvedCapability capability)
        {
            if (capabilityId != null && capabilityId.StartsWith("moc.internal.", StringComparison.Ordinal))
                return _internal.TryGetValue(capabilityId, out capability);
            return _underlying.TryResolve(capabilityId, out capability);
        }
    }


    /// <summary>Developer and Studio share the same Content Logic Edit transaction; no parallel runtime builder.</summary>
    public sealed class MOCFlowBuilder
    {
        private readonly List<MOLControlBlock> _blocks=new List<MOLControlBlock>();
        private readonly List<MOLLink> _links=new List<MOLLink>();
        private readonly Dictionary<Guid,IEnumerable<MPCPortDescriptor>> _ports=
            new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>();
        private Guid _entry;
        private MOCFlowBuilder(MOLWiringDefinition current)
        {
            if(current==null)return;
            _entry=current.EntryBlockId;
            _blocks.AddRange(current.Blocks);
            _links.AddRange(current.Links);
            foreach(var pair in current.Ports)_ports.Add(pair.Key,pair.Value.ToArray());
        }
        public Guid LastBlockId{get{return _blocks.Count==0?Guid.Empty:_blocks[_blocks.Count-1].Id;}}
        public Guid Step(MOCCapabilityDescriptor descriptor)
        {
            if(descriptor==null)throw new ArgumentNullException("descriptor");
            return Step(descriptor.Id,descriptor.Ports);
        }
        public Guid Step(string capabilityId,IEnumerable<MPCPortDescriptor> extraPorts=null)
        {
            if(string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("能力ID不能为空","capabilityId");
            var action=MOLControlBlock.Action(capabilityId);
            var declared=new List<MPCPortDescriptor>{
                new MPCPortDescriptor("Start",MPCPortDirection.Input,"signal",
                    MPCPortKind.Control,MPCPortMultiplicity.Many),
                new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control)
            };
            if(extraPorts!=null)declared.AddRange(extraPorts);
            if(declared.Any(x=>x==null)||declared.GroupBy(x=>x.MemberId,StringComparer.Ordinal)
                .Any(x=>x.Count()>1))
                throw new ArgumentException("重复端口","extraPorts");
            _blocks.Add(action);_ports.Add(action.Id,declared.ToArray());
            if(_entry==Guid.Empty)_entry=action.Id;
            return action.Id;
        }
        public Guid Connect(Guid source,string outPort,Guid target,string inPort,
            MPCPortKind kind=MPCPortKind.Control)
        {
            var link=new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(source,outPort),
                new MOLBlockEndpoint(target,inPort),kind);
            _links.Add(link);return link.Id;
        }
        public void Disconnect(Guid linkId)
        {
            int removed=_links.RemoveAll(x=>x.Id==linkId);
            if(removed!=1)throw new InvalidOperationException("moc.wiring.link_missing");
        }
        public void Remove(Guid blockId)
        {
            if(_blocks.RemoveAll(x=>x.Id==blockId)!=1)
                throw new InvalidOperationException("moc.wiring.block_missing");
            _ports.Remove(blockId);
            _links.RemoveAll(x=>x.Source.BlockId==blockId||x.Target.BlockId==blockId);
            if(_entry==blockId)_entry=_blocks.Count==0?Guid.Empty:_blocks[0].Id;
        }

        /// <summary>查询正式端口，UI可按PortKind/Direction显示候选，不写第二份目录。</summary>
        public IReadOnlyList<MPCPortDescriptor> GetPorts(Guid blockId)
        {
            IEnumerable<MPCPortDescriptor> source;
            if(!_ports.TryGetValue(blockId,out source))
                throw new InvalidOperationException("moc.wiring.block_missing");
            return source.ToList().AsReadOnly();
        }
        public IReadOnlyList<MOLControlBlock> Blocks
        {get{return _blocks.ToList().AsReadOnly();}}
        public IReadOnlyList<MOLLink> Links
        {get{return _links.ToList().AsReadOnly();}}
        public void AddPort(Guid blockId,MPCPortDescriptor descriptor)
        {
            if(descriptor==null)throw new ArgumentNullException("descriptor");
            IEnumerable<MPCPortDescriptor> existing;
            if(!_ports.TryGetValue(blockId,out existing))
                throw new InvalidOperationException("moc.wiring.block_missing");
            var updated=existing.ToList();
            if(updated.Any(x=>x.MemberId==descriptor.MemberId))
                throw new InvalidOperationException("moc.wiring.duplicate_port");
            updated.Add(descriptor);
            _ports[blockId]=updated.ToArray();
        }
        public void RemovePort(Guid blockId,string memberId)
        {
            IEnumerable<MPCPortDescriptor> existing;
            if(!_ports.TryGetValue(blockId,out existing))
                throw new InvalidOperationException("moc.wiring.block_missing");
            if(_links.Any(x=>(x.Source.BlockId==blockId&&x.Source.PortMemberId==memberId)||
                (x.Target.BlockId==blockId&&x.Target.PortMemberId==memberId)))
                throw new InvalidOperationException("moc.wiring.port_in_use");
            var updated=existing.Where(x=>x.MemberId!=memberId).ToArray();
            if(updated.Length==existing.Count())
                throw new InvalidOperationException("moc.wiring.port_missing");
            _ports[blockId]=updated;
        }
        /// <summary>只修改能力身份，不更换BlockId或已有Port/Link身份。</summary>
        public void SetCapability(Guid blockId,string capabilityId)
        {
            if(string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("moc.wiring.capability_missing","capabilityId");
            int index=_blocks.FindIndex(x=>x.Id==blockId);
            if(index<0)throw new InvalidOperationException("moc.wiring.block_missing");
            _blocks[index]=new MOLControlBlock(blockId,MOLControlKind.Action,capabilityId);
        }
        public void SetEntry(Guid blockId)
        {
            if(!_blocks.Any(x=>x.Id==blockId))
                throw new InvalidOperationException("moc.wiring.entry_missing");
            _entry=blockId;
        }
        public MOLWiringDefinition Build()
        {return new MOLWiringDefinition(_entry,_blocks,_ports,_links);}
        public static void Edit(MEMEngineeringSession session,MEMContent content,
            Action<MOCFlowBuilder> configure)
        {
            if(session==null||content==null||configure==null)
                throw new ArgumentNullException("session/content/configure");
            int revision=session.Revision;
            MEMNodeSnapshot node=session.GetSnapshot().Find(content.Id);
            if(node==null||node.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("moc.wiring.foreign_content");
            MOLWiringDefinition old=MOLWiringCodec.Get(node);
            if(old==null&&MOLLogicCodec.Get(node)!=null)
                throw new InvalidOperationException("moc.wiring.legacy_requires_explicit_conversion");
            var builder=new MOCFlowBuilder(old);
            configure(builder);
            var aspect=MOLWiringCodec.CreateAspect(builder.Build());
            session.Edit("编辑自由编排",revision,e=>{e.SetAspect(content,aspect);return true;})
                .RequireValue();
        }
    }

    public sealed class MOCEngineeringCompiler
    {
        private const int MaxSteps = 4096;
        private const int MaxDepth = 24;

        public MOCCompiledPlan Compile(MEMProjectSnapshot snapshot, Guid entryContentId,
            IMRKCapabilityResolver resolver, IEnumerable<Guid> instancePath = null)
        {
            if (snapshot == null || resolver == null)
                throw new ArgumentNullException("snapshot/resolver");
            MEMNodeSnapshot entry = snapshot.Find(entryContentId);
            if (entry == null || entry.Kind != MEMNodeKind.Content)
                throw new InvalidOperationException("moc.entry_must_be_content");
            Guid[] path = (instancePath ?? new Guid[0]).ToArray();
            if (path.Length > 32 || path.Any(x => x == Guid.Empty))
                throw new InvalidOperationException("moc.instance_path");
            var known = snapshot.Nodes.SelectMany(n => n.References).ToDictionary(r => r.Id);
            foreach (Guid id in path)
                if (!known.ContainsKey(id)) throw new InvalidOperationException("moc.unknown_instance:" + id);

            MOLWiringDefinition wired = MOLWiringCodec.Get(entry);
            if (wired != null) return CompileWiring(snapshot, entry, path, wired, resolver);

            var build = new Compilation(snapshot, resolver, path);
            build.EmitContent(entry, new string[0], new Guid[0], null, 0);
            var compiled = new MRKExecutionPlanCompiler().Compile(
                "moc." + snapshot.ProjectId.ToString("N") + "." + entryContentId.ToString("N") +
                ".r" + snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                build.Steps, build.Resolver);
            if (!compiled.Succeeded)
                throw new InvalidOperationException("moc.compile_rejected:" +
                    string.Join(";", compiled.Diagnostics.Select(d => d.Code + "@" + d.StepId)));
            return new MOCCompiledPlan(compiled.Plan, new MOCSourceMap(build.Origins),
                snapshot.ProjectId, snapshot.Revision);
        }


        // Stage 2: V2自由连接实际参与MRK依赖及数据绑定；复杂分支/循环不做假执行。
        private static MOCCompiledPlan CompileWiring(MEMProjectSnapshot snapshot, MEMNodeSnapshot entry,
            Guid[] instancePath, MOLWiringDefinition wiring, IMRKCapabilityResolver resolver)
        {
            wiring.ValidateExecutable();
            var blocks=wiring.Blocks.ToDictionary(x=>x.Id);
            var stepIds=blocks.Keys.ToDictionary(x=>x,x=>"w"+x.ToString("N"));
            var requests=new List<MRKStepCompileRequest>();
            var origins=new List<MOCStepOrigin>();
            foreach(var block in wiring.Blocks)
            {
                var entering=wiring.Links.Where(x=>x.Target.BlockId==block.Id).ToArray();
                var depends=entering.Select(x=>stepIds[x.Source.BlockId])
                    .Distinct(StringComparer.Ordinal).ToArray();
                var inputs=new List<MRKExecutionInput>();
                foreach(var link in entering.Where(x=>x.Kind==MPCPortKind.Data))
                {
                    if(entering.Count(x=>x.Kind==MPCPortKind.Data &&
                        x.Target.PortMemberId==link.Target.PortMemberId)>1)
                        throw new InvalidOperationException("moc.wiring.data_fanin_requires_aggregator:"+
                            link.Target.BlockId+":"+link.Target.PortMemberId);
                    var sourcePorts=wiring.Ports[link.Source.BlockId];
                    var from=sourcePorts.Single(x=>x.MemberId==link.Source.PortMemberId);
                    var target=wiring.Ports[block.Id].Single(x=>x.MemberId==link.Target.PortMemberId);
                    var resolvedCapability=blocks[link.Source.BlockId];
                    MRKResolvedCapability actual;
                    if(!resolver.TryResolve(resolvedCapability.CapabilityId,out actual)||actual==null)
                        throw new InvalidOperationException("moc.wiring.unknown_producer:"+resolvedCapability.CapabilityId);
                    MRKInputSource sourceKind;
                    string sourceName=stepIds[link.Source.BlockId];
                    Type valueType=ResolveWiringType(target.TypeId);
                    if(actual.DataOutputs.ContainsKey(from.MemberId))
                    {
                        sourceKind=MRKInputSource.StepOutput;
                        sourceName+="."+from.MemberId;
                    }
                    else if(sourcePorts.Count(x=>x.Kind==MPCPortKind.Data&&
                        x.Direction!=MPCPortDirection.Input)==1)
                        sourceKind=MRKInputSource.StepResult;
                    else
                        throw new InvalidOperationException("moc.wiring.ambiguous_output:"+
                            link.Source.BlockId+":"+from.MemberId);
                    inputs.Add(new MRKExecutionInput(target.MemberId,sourceKind,sourceName,
                        valueType,target.Required));
                }
                requests.Add(new MRKStepCompileRequest(stepIds[block.Id],block.CapabilityId,
                    depends,inputs));
                origins.Add(new MOCStepOrigin(stepIds[block.Id],
                    new MRIObjectAddress(snapshot.ProjectId,entry.StorageRootId,entry.Id,instancePath),
                    block.Id,new Guid[0],null,"Action"));
            }
            var compiled=new MRKExecutionPlanCompiler().Compile(
                "moc."+snapshot.ProjectId.ToString("N")+"."+entry.Id.ToString("N")+
                ".r"+snapshot.Revision.ToString(CultureInfo.InvariantCulture)+".wired",
                requests,resolver);
            if(!compiled.Succeeded)
                throw new InvalidOperationException("moc.wiring.compile_rejected:"+
                    string.Join(";",compiled.Diagnostics.Select(x=>x.Code+"@"+x.StepId)));
            return new MOCCompiledPlan(compiled.Plan,new MOCSourceMap(origins),
                snapshot.ProjectId,snapshot.Revision);
        }
        private static Type ResolveWiringType(string id)
        {
            switch(id)
            {
                case "bool":case "System.Boolean":return typeof(bool);
                case "int":case "System.Int32":return typeof(int);
                case "long":case "System.Int64":return typeof(long);
                case "double":case "System.Double":return typeof(double);
                case "string":case "System.String":return typeof(string);
                case "byte[]":case "System.Byte[]":return typeof(byte[]);
                // 自定义组合数据契约由能力Provider和运行时适配器校验；首批不反射加载陌生类型。
                default:return typeof(object);
            }
        }

        private sealed class IterationScope
        {
            internal int Index;
            internal string IndexStep;
            internal string ItemStep;
            internal string Input;
        }
        private sealed class Compilation
        {
            internal readonly List<MRKStepCompileRequest> Steps = new List<MRKStepCompileRequest>();
            internal readonly List<MOCStepOrigin> Origins = new List<MOCStepOrigin>();
            internal readonly MOCResolver Resolver;
            private readonly MEMProjectSnapshot _snapshot;
            private readonly Guid[] _instances;
            private int _serial;
            private readonly HashSet<Guid> _activeContents = new HashSet<Guid>();
            internal Compilation(MEMProjectSnapshot snapshot, IMRKCapabilityResolver resolver, Guid[] instances)
            { _snapshot = snapshot; Resolver = new MOCResolver(resolver); _instances = instances; }
            private string NewId(MOLControlBlock block)
            {
                return "m" + (++_serial).ToString(CultureInfo.InvariantCulture) +
                    "_" + block.Id.ToString("N");
            }
            private string Add(MOLControlBlock block, MEMNodeSnapshot owner, Guid[] calls,
                int? iteration, string role, string capability, IEnumerable<string> dependencies,
                Func<MRKExecutionContext,bool> condition = null, bool join = false,
                IEnumerable<MRKExecutionInput> inputs = null)
            {
                if (Steps.Count >= MaxSteps)
                    throw new InvalidOperationException("moc.step_budget");
                string id = NewId(block);
                Steps.Add(new MRKStepCompileRequest(id, capability, dependencies,
                    inputs: inputs, condition: condition, allowSkippedDependencies: join));
                var address = new MRIObjectAddress(_snapshot.ProjectId,
                    owner.StorageRootId, owner.Id, _instances);
                Origins.Add(new MOCStepOrigin(id, address, block.Id, calls, iteration, role));
                return id;
            }
            private Func<MRKExecutionContext, bool> Predicate(MOLExpression expression, int iteration)
            {
                if (expression == null) return ctx => true;
                string resolved = null;
                if (expression.Source == MOLExpressionSource.StepResult)
                {
                    Guid id;
                    if (!Guid.TryParse(expression.Name, out id))
                        throw new InvalidOperationException("moc.step_result_requires_block_id");
                    var origin = Origins.LastOrDefault(o => o.BlockId == id && o.Role == "Action");
                    if (origin == null)
                        throw new InvalidOperationException("moc.step_result_not_preceding:" + expression.Name);
                    resolved = origin.StepId;
                }
                string stepId = resolved;
                return ctx => MOCExpressions.Evaluate(expression, ctx, iteration, stepId);
            }
            private static string[] Merge(IEnumerable<string> first, IEnumerable<string> second)
            { return first.Concat(second).Distinct(StringComparer.Ordinal).ToArray(); }
            internal string[] EmitContent(MEMNodeSnapshot content, string[] after,
                Guid[] calls, IterationScope scope, int depth)
            {
                if (depth > MaxDepth || !_activeContents.Add(content.Id))
                    throw new InvalidOperationException("moc.recursive_or_deep_subflow");
                try
                {
                    var logic = MOLLogicCodec.Get(content);
                    if (logic == null) throw new InvalidOperationException("moc.content_logic_missing:" + content.Id);
                    return Emit(logic.Root, content, after, calls, scope, depth + 1);
                }
                finally { _activeContents.Remove(content.Id); }
            }
            private string[] Emit(MOLControlBlock block, MEMNodeSnapshot owner,
                string[] before, Guid[] calls, IterationScope scope, int depth)
            {
                if (depth > MaxDepth) throw new InvalidOperationException("moc.depth_budget");
                switch (block.Kind)
                {
                    case MOLControlKind.Action:
                    {
                        var dependencies = new List<string>(before);
                        var inputs = new List<MRKExecutionInput>();
                        if (scope != null)
                        {
                            if (!dependencies.Contains(scope.IndexStep)) dependencies.Add(scope.IndexStep);
                            inputs.Add(new MRKExecutionInput("iteration", MRKInputSource.StepResult,
                                scope.IndexStep, typeof(int)));
                            if (scope.ItemStep != null)
                            {
                                if (!dependencies.Contains(scope.ItemStep)) dependencies.Add(scope.ItemStep);
                                inputs.Add(new MRKExecutionInput("item", MRKInputSource.StepResult,
                                    scope.ItemStep, typeof(object)));
                            }
                        }
                        return new[] {Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                            "Action", block.CapabilityId, dependencies, inputs: inputs)};
                    }
                    case MOLControlKind.Sequence:
                    {
                        var exits = before;
                        foreach (var child in block.Children)
                            exits = Emit(child, owner, exits, calls, scope, depth + 1);
                        return exits;
                    }
                    case MOLControlKind.Parallel:
                    {
                        var exits = new List<string>();
                        foreach (var child in block.Children)
                            exits.AddRange(Emit(child, owner, before, calls, scope, depth + 1));
                        return new[]{Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                            "ParallelJoin", "moc.internal.noop", exits)};
                    }
                    case MOLControlKind.Branch:
                    {
                        var joins = new List<string>();
                        var cases = block.Cases.ToArray();
                        var casePredicates = cases.Select(c =>
                            c.Condition == null ? null : Predicate(c.Condition,
                                scope == null ? 0 : scope.Index)).ToArray();
                        for(int i=0;i<cases.Length;i++)
                        {
                            int chosen = i;
                            Func<MRKExecutionContext,bool> guard = ctx =>
                            {
                                for(int earlier=0;earlier<chosen;earlier++)
                                    if (casePredicates[earlier] == null || casePredicates[earlier](ctx))
                                        return false;
                                return casePredicates[chosen] == null || casePredicates[chosen](ctx);
                            };
                            string gate=Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                                "Case." + i, "moc.internal.noop", before, guard);
                            joins.AddRange(Emit(cases[i].Body, owner, new[]{gate}, calls, scope, depth+1));
                        }
                        return new[]{Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                            "BranchJoin", "moc.internal.noop", joins,
                            condition: ctx => !before.Any(d => {
                                MRKStepState state;
                                return ctx.StatesSnapshot().TryGetValue(d, out state) &&
                                    state == MRKStepState.Skipped;
                            }), join:true)};
                    }
                    case MOLControlKind.Loop:
                    case MOLControlKind.ForEach:
                    {
                        MOLLoopDefinition l = block.Loop;
                        string[] exits = before;
                        string previousGuard = null;
                        for(int i=0;i<l.MaxIterations;i++)
                        {
                            int index = i;
                            string prior = previousGuard;
                            Func<MRKExecutionContext,bool> test = (l.Mode == MOLLoopMode.DoWhile && index == 0)
                                ? (Func<MRKExecutionContext,bool>)(ctx => true)
                                : Predicate(l.Condition, index);
                            Func<MRKExecutionContext,bool> condition = ctx =>
                            {
                                if (before.Any(d => {
                                    MRKStepState state;
                                    return ctx.StatesSnapshot().TryGetValue(d, out state) &&
                                        state == MRKStepState.Skipped;
                                })) return false;
                                if (prior != null)
                                {
                                    object valid;
                                    if (!ctx.TryGetAbsoluteResult(prior, out valid)) return false;
                                }
                                if (block.Kind == MOLControlKind.ForEach)
                                {
                                    object data;
                                    if (!ctx.Inputs.TryGetValue(l.CollectionInput, out data) || !(data is IList))
                                        throw new InvalidOperationException("moc.foreach_requires_IList:" + l.CollectionInput);
                                    return index < ((IList)data).Count;
                                }
                                if (l.Mode == MOLLoopMode.DoWhile && index == 0) return true;
                                return test(ctx);
                            };
                            string gate=Add(block, owner, calls, index, "IterationGuard",
                                "moc.internal.noop", exits, condition, join:true);
                            previousGuard = gate;
                            string idxName="moc.internal.index." + gate;
                            Resolver.Register(idxName, ctx => index, typeof(int));
                            string idx=Add(block, owner, calls, index, "IterationIndex",
                                idxName, new[]{gate});
                            string item=null;
                            if(block.Kind == MOLControlKind.ForEach)
                            {
                                string capability="moc.internal.item." + gate;
                                Resolver.Register(capability, ctx => ((IList)ctx.Inputs[l.CollectionInput])[index],
                                    typeof(object));
                                item=Add(block,owner,calls,index,"IterationItem",capability,new[]{idx});
                            }
                            var iterationScope=new IterationScope{Index=index,IndexStep=idx,ItemStep=item,
                                Input=l.CollectionInput};
                            string[] bodyStart=item==null?new[]{idx}:new[]{item};
                            string[] body=Emit(l.Body,owner,bodyStart,calls,iterationScope,depth+1);
                            exits=new[]{Add(block, owner, calls, index, "IterationJoin",
                                "moc.internal.noop", body, join:true)};
                        }
                        return exits;
                    }
                    case MOLControlKind.Invoke:
                    {
                        Guid target;
                        if (!Guid.TryParse(block.TargetFlow, out target))
                            throw new InvalidOperationException("moc.invoke_target_must_be_content_guid");
                        var content=_snapshot.Find(target);
                        if(content==null||content.Kind!=MEMNodeKind.Content)
                            throw new InvalidOperationException("moc.invoke_missing_content");
                        var nextCalls=calls.Concat(new[]{block.Id}).ToArray();
                        return EmitContent(content,before,nextCalls,scope,depth+1);
                    }
                    default:
                        throw new InvalidOperationException("moc.control_not_yet_supported:" + block.Kind);
                }
            }
        }
    }
}
