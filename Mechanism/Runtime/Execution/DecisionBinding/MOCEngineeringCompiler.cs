using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
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
        internal MOCCompiledPlan(MRKExecutionPlan plan, MOCSourceMap source, Guid projectId, int revision,
            IDictionary<string,object> inputDefaults=null)
        {
            Plan=plan;SourceMap=source;ProjectId=projectId;EngineeringRevision=revision;
            InputDefaults=new ReadOnlyDictionary<string,object>(
                new Dictionary<string,object>(inputDefaults??new Dictionary<string,object>(),
                    StringComparer.Ordinal));
        }
        /// <summary>由MOC已校验字面量生成的每次执行输入默认值；运行请求可显式覆盖。</summary>
        public IReadOnlyDictionary<string,object> InputDefaults{get;private set;}
        public MRKExecutionPlan Plan { get; private set; }
        public MOCSourceMap SourceMap { get; private set; }
        public Guid ProjectId { get; private set; }
        public int EngineeringRevision { get; private set; }

        /// <summary>
        /// R08：工程已编译Plan在发布/模拟执行前核对Host实际注册的原MRK执行组。
        /// 不把组容量复制进MOL节点或MOC Plan；Host仍拥有真实组容量及作用域。
        /// </summary>
        public MOCCompiledPlan RequireExecutionGroups(
            IEnumerable<MRKExecutionGroupDefinition> registered)
        {
            if (registered == null) throw new ArgumentNullException("registered");
            var groups = new Dictionary<string, MRKExecutionGroupDefinition>(StringComparer.Ordinal);
            foreach (MRKExecutionGroupDefinition definition in registered)
            {
                if (definition == null)
                    throw new ArgumentException("moc.execution_group_null", "registered");
                if (groups.ContainsKey(definition.Id))
                    throw new ArgumentException(
                        "moc.execution_group_duplicate:" + definition.Id, "registered");
                groups.Add(definition.Id, definition);
            }
            ValidateExecutionGroups(Plan, groups);
            return this;
        }

        private static void ValidateExecutionGroups(MRKExecutionPlan plan,
            IDictionary<string, MRKExecutionGroupDefinition> groups)
        {
            foreach (MRKExecutionStep step in plan.Steps)
            {
                if (!string.IsNullOrWhiteSpace(step.ExecutionGroup) &&
                    !groups.ContainsKey(step.ExecutionGroup))
                    throw new InvalidOperationException(
                        "moc.execution_group_unregistered:" + step.Id + ":" +
                        step.ExecutionGroup);
                if (step.LoopRegion != null)
                    ValidateExecutionGroups(step.LoopRegion.Body, groups);
                if(step.TryRegion!=null)
                {
                    ValidateExecutionGroups(step.TryRegion.Body,groups);
                    if(step.TryRegion.CatchBody!=null)
                        ValidateExecutionGroups(step.TryRegion.CatchBody,groups);
                    if(step.TryRegion.FinallyBody!=null)
                        ValidateExecutionGroups(step.TryRegion.FinallyBody,groups);
                }
            }
        }

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
                {
                    // 动态循环在运行时限定路径：loop.iN.originalStepId。
                    // SourceMap仍只有一次编译的原始Block映射，不制造每轮副本。
                    string local=change.StepId.Substring(change.StepId.LastIndexOf('.')+1);
                    if(!SourceMap.TryResolve(local,out from))
                        throw new InvalidOperationException("moc.missing_source:"+change.StepId);
                }
                result.Add(new MOCObservedStep(change, from));
            }
            return new MOCTraceView(result, batch.NextCursor, batch.LostCount);
        }
    }

    // Evaluates only typed, bounded declarative operands. Never compiles user text as C#.
    internal static class MOCExpressions
    {
        internal static bool Evaluate(MOLExpression expr,MRKExecutionContext context,
            int iteration,string resolvedStepId=null)
        {
            return Evaluate(expr,context,iteration,leaf=>resolvedStepId);
        }
        internal static bool Evaluate(MOLExpression expr,MRKExecutionContext context,
            int iteration,Func<MOLExpression,string> resolvedStep)
        {
            if(expr==null)return true;
            return MOLScalarExpression.EvaluateTree(expr,leaf=>{
                object actual;
                switch(leaf.Source)
                {
                    case MOLExpressionSource.Constant:return leaf.Name;
                    case MOLExpressionSource.FlowInput:
                        if(!context.Inputs.TryGetValue(leaf.Name,out actual))
                            throw new InvalidOperationException("moc.input_missing:"+leaf.Name);
                        return actual;
                    case MOLExpressionSource.StepResult:
                        string id=resolvedStep==null?null:resolvedStep(leaf);
                        if(id==null||!context.TryGetScopedResult(id,out actual))
                            throw new InvalidOperationException("moc.step_result_missing:"+leaf.Name);
                        return actual;
                    case MOLExpressionSource.Iteration:return iteration;
                    default:throw new InvalidOperationException("moc.expression_source");
                }
            });
        }
        internal static int CompareScalar(object actual,string expected)
        {return MOLScalarExpression.Compare(actual,expected);}
        
        internal static bool ScalarEquals(object actual, string expected)
        { return CompareScalar(actual, expected) == 0; }
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
        /// <summary>复用MRK能力编译，等待不占用业务线程且遵从Flow取消。</summary>
        internal string RegisterAsync(string id,
            Func<MRKExecutionContext,CancellationToken,Task<object>> run,Type type)
        {
            if(_internal.ContainsKey(id))throw new InvalidOperationException("moc.internal_duplicate");
            _internal.Add(id,new MRKResolvedCapability(run,
                new MRKExecutionOutput(type,true)));
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
    public sealed class MOCEngineeringCompiler
    {
        private const int MaxSteps = 4096;
        private const int MaxDepth = 24;

        public MOCCompiledPlan Compile(MEMProjectSnapshot snapshot, Guid entryContentId,
            IMRKCapabilityResolver resolver, IEnumerable<Guid> instancePath = null)
        {
            return CompileCore(snapshot,entryContentId,resolver,instancePath,null);
        }

        /// <summary>Lower Decision's immutable canonical logic payload through the one
        /// existing MRK compiler. The engineering snapshot supplies identity,
        /// authorized Content references and SourceMap addresses, not logic bytes.</summary>
        internal MOCCompiledPlan Compile(MEMProjectSnapshot snapshot,MDCDeclarativePlan plan,
            IMRKCapabilityResolver resolver,IEnumerable<Guid> instancePath = null)
        {
            if(plan==null)throw new ArgumentNullException("plan");
            if(snapshot==null || plan.ProjectId!=snapshot.ProjectId ||
                plan.EngineeringRevision!=snapshot.Revision)
                throw new InvalidOperationException("moc.plan.source_identity_changed");
            return CompileCore(snapshot,plan.RootContentId,resolver,instancePath,
                new MOCPlanLogicReader(plan));
        }

        private MOCCompiledPlan CompileCore(MEMProjectSnapshot snapshot, Guid entryContentId,
            IMRKCapabilityResolver resolver, IEnumerable<Guid> instancePath,
            MOCPlanLogicReader source)
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

            MOLWiringDefinition wired = source==null ?
                MOLWiringCodec.Get(entry) : source.Wiring(entry);
            if (wired != null) return CompileWiring(snapshot, entry, path, wired, resolver,source);

            var build = new Compilation(snapshot, resolver, path,source);
            build.EmitContent(entry, new string[0], new Guid[0], null, 0);
            var compiled = new MRKExecutionPlanCompiler().Compile(
                "moc." + snapshot.ProjectId.ToString("N") + "." + entryContentId.ToString("N") +
                ".r" + snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                build.Steps, build.Resolver, regions:build.Regions);
            if (!compiled.Succeeded)
                throw new InvalidOperationException("moc.compile_rejected:" +
                    string.Join(";", compiled.Diagnostics.Select(d => d.Code + "@" + d.StepId)));
            return new MOCCompiledPlan(compiled.Plan, new MOCSourceMap(build.Origins),
                snapshot.ProjectId, snapshot.Revision);
        }


        // 只在MOC编译期间构造瞬时MRK子流程目录，不保存第二份Graph。
        private sealed class MOCInlineContentCatalog : IMRKFlowCompileCatalog
        {
            private readonly Dictionary<string,MRKFlowCompileDefinition> _items=
                new Dictionary<string,MRKFlowCompileDefinition>(StringComparer.Ordinal);
            internal MOCInlineContentCatalog(string rootId,
                IEnumerable<MRKStepCompileRequest> root,
                IDictionary<string,MRKFlowCompileDefinition> children,
                IDictionary<string,string> rootExports=null)
            {
                _items.Add(rootId,new MRKFlowCompileDefinition(rootId,root,rootExports));
                foreach(var child in children)_items.Add(child.Key,child.Value);
            }
            public bool TryGet(string flowId,out MRKFlowCompileDefinition result)
            {return _items.TryGetValue(flowId,out result);}
        }

        /// <summary>受限布尔/标量Switch条件。执行上下文读取真实上游StepResult，绝不执行任意表达式。</summary>
        private static bool EvaluateWiringGuard(MRKExecutionContext context,
            string producer,string guard,IReadOnlyList<string> earlierGuards,
            IReadOnlyList<string> alternatives)
        {
            object value;
            if(!context.TryGetResult(producer,out value))
                throw new InvalidOperationException("moc.branch.result_unavailable:"+producer);
            if(guard=="bool:true"||guard=="bool:false")
            {
                if(!(value is bool))throw new InvalidOperationException("moc.branch.bool_required:"+producer);
                return (bool)value==(guard=="bool:true");
            }
            if(value==null)throw new InvalidOperationException("moc.branch.null_result");
            if(!(value is string||value is bool||value is int||value is long||
                value is double||value is float||value is decimal))
                throw new InvalidOperationException("moc.branch.scalar_required");
            if(guard.StartsWith("case:",StringComparison.Ordinal))
            {
                // 与V1 Branch的有序Case一致：等价的数值别名只能命中第一条。
                if(earlierGuards.Any(x=>x.StartsWith("case:",StringComparison.Ordinal)&&
                    MOCExpressions.ScalarEquals(value,x.Substring(5))))return false;
                return MOCExpressions.ScalarEquals(value,guard.Substring(5));
            }
            if(guard=="default")
                return !alternatives.Any(x=>MOCExpressions.ScalarEquals(value,x));
            throw new InvalidOperationException("moc.branch.unknown_guard");
        }

        // Stage 2: V2自由连接实际参与MRK依赖及数据绑定；复杂分支/循环不做假执行。

        private static string WireId(Guid blockId) {return "w"+blockId.ToString("N");}
        private static IDictionary<string,string> BuildPublicExports(
            MOLWiringDefinition wiring,IMRKCapabilityResolver resolver,MEMProjectSnapshot snapshot=null,
            MOCPlanLogicReader source=null)
        {
            var exports=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var pair in wiring.FlowOutputs)
            {
                MOLControlBlock block=wiring.Blocks.Single(b=>b.Id==pair.Value.BlockId);
                MRKResolvedCapability ability;
                if(block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))
                {
                    if(snapshot==null)throw new InvalidOperationException("moc.flow_export_requires_snapshot");
                    Guid id;
                    if(!Guid.TryParseExact(block.CapabilityId.Substring("moc.invoke:".Length),
                        "N",out id))throw new InvalidOperationException("moc.flow_export_child_id");
                    var target=snapshot.Find(id);
                    var nested=target==null?null:(source==null ?
                        MOLWiringCodec.Get(target):source.Wiring(target));
                    MOLBlockEndpoint endpoint;
                    if(nested==null||!nested.FlowOutputs.TryGetValue(pair.Value.PortMemberId,out endpoint))
                        throw new InvalidOperationException("moc.flow_export_child_missing:"+pair.Key);
                    // The nested MRK barrier publishes this alias; the caller never reads private child nodes.
                }
                else if(!resolver.TryResolve(block.CapabilityId,out ability)||ability==null||
                        !ability.DataOutputs.ContainsKey(pair.Value.PortMemberId))
                    throw new InvalidOperationException("moc.flow_output_not_declared:"+pair.Key);
                exports.Add(pair.Key,WireId(block.Id)+"."+pair.Value.PortMemberId);
            }
            return exports;
        }
        private static IEnumerable<MRKExecutionInput> ExposedInputs(
            MOLWiringDefinition wiring,Guid blockId)
        {
            foreach(var pair in wiring.FlowInputs)
            {
                if(pair.Value.BlockId!=blockId)continue;
                MPCPortDescriptor port=wiring.Ports[blockId].Single(p=>
                    p.MemberId==pair.Value.PortMemberId);
                yield return new MRKExecutionInput(port.MemberId,MRKInputSource.FlowInput,
                    pair.Key,ResolveWiringType(port.TypeId),port.Required);
            }
        }
        private static IEnumerable<MRKExecutionInput> InternalDataInputs(
            MOLWiringDefinition wiring,Guid blockId,IMRKCapabilityResolver resolver,
            MEMProjectSnapshot snapshot=null,MOCPlanLogicReader planReader=null)
        {
            foreach(var link in wiring.Links.Where(l=>l.Target.BlockId==blockId&&
                (l.Kind==MPCPortKind.Data||l.Kind==MPCPortKind.Stream)))
            {
                var from=wiring.Ports[link.Source.BlockId].Single(p=>p.MemberId==link.Source.PortMemberId);
                var to=wiring.Ports[blockId].Single(p=>p.MemberId==link.Target.PortMemberId);
                var producer=wiring.Blocks.Single(b=>b.Id==link.Source.BlockId);
                MRKResolvedCapability ability=null;
                bool invoked=producer.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal);
                if(invoked)
                {
                    if(snapshot==null)throw new InvalidOperationException("moc.nested_source_needs_snapshot");
                    Guid childId;
                    if(!Guid.TryParseExact(producer.CapabilityId.Substring("moc.invoke:".Length),
                        "N",out childId))throw new InvalidOperationException("moc.nested_source_id");
                    var content=snapshot.Find(childId);
                    var child=content==null?null:(planReader==null ?
                        MOLWiringCodec.Get(content):planReader.Wiring(content));
                    if(child==null||!child.FlowOutputs.ContainsKey(from.MemberId))
                        throw new InvalidOperationException("moc.nested_source_unpublished:"+from.MemberId);
                }
                else if(!resolver.TryResolve(producer.CapabilityId,out ability)||ability==null)
                    throw new InvalidOperationException("moc.nested_data_unknown_producer");
                MRKInputSource kind;
                string source=WireId(producer.Id);
                if(invoked)
                {kind=MRKInputSource.StepOutput;source+="."+from.MemberId;}
                else if(ability.DataOutputs.ContainsKey(from.MemberId))
                {kind=MRKInputSource.StepOutput;source+="."+from.MemberId;}
                else if(wiring.Ports[producer.Id].Count(p=>(p.Kind==MPCPortKind.Data||
                    p.Kind==MPCPortKind.Stream)&&p.Direction==MPCPortDirection.Output)==1)
                    kind=MRKInputSource.StepResult;
                else throw new InvalidOperationException("moc.nested_data_ambiguous_output");
                yield return new MRKExecutionInput(to.MemberId,kind,source,
                    ResolveWiringType(to.TypeId),to.Required);
            }
        }

        // Recursively adapt a persisted nested Content; all execution still belongs to MRK.
        private static void BuildDeepFlow(MEMProjectSnapshot snapshot,MEMNodeSnapshot content,
            IMRKCapabilityResolver resolver,IDictionary<string,MRKFlowCompileDefinition> catalog,
            HashSet<Guid> active,MOCPlanLogicReader planReader)
        {
            string id=content.Id.ToString("N");
            if(catalog.ContainsKey(id))return;
            if(active.Count>=16||!active.Add(content.Id))
                throw new InvalidOperationException("moc.subflow.recursive_or_too_deep");
            try
            {
                var wiring=planReader==null ? MOLWiringCodec.Get(content):planReader.Wiring(content);
                if(wiring==null)throw new InvalidOperationException("moc.subflow.requires_v2");
                wiring.ValidateExecutable();
                if(wiring.StateMachines.Values.Any(m=>m.States.Any(st=>
                    st.EntryCapability.Length>0||st.ExitCapability.Length>0)))
                    throw new InvalidOperationException("moc.state_actions_not_supported");
                // Nested defaults are not silently hoisted into unrelated caller scopes.
                // Existing G04-F direct subflows retain their isolated typed defaults.
                if(wiring.Parameters.Count!=0)
                    throw new InvalidOperationException("moc.deep.parameters_need_explicit_mapping");
                var definitions=new List<MRKStepCompileRequest>();
                foreach(var block in wiring.Blocks)
                {
                    var entering=wiring.Links.Where(l=>l.Target.BlockId==block.Id).ToArray();
                    var incomingControl=entering.Where(l=>l.Kind==MPCPortKind.Control).ToArray();
                    Func<MRKExecutionContext,bool> branchCondition=null;
                    if(incomingControl.Length==1&&incomingControl[0].ConditionId.Length>0)
                    {
                        MOLLink guarded=incomingControl[0];
                        string source=WireId(guarded.Source.BlockId);
                        string guard=guarded.ConditionId;
                        var order=wiring.Links.Where(l=>l.Source.BlockId==guarded.Source.BlockId&&
                            l.Kind==MPCPortKind.Control).ToArray();
                        string[] earlier=order.TakeWhile(l=>l.Id!=guarded.Id)
                            .Select(l=>l.ConditionId).ToArray();
                        string[] alternatives=order.Where(l=>l.ConditionId.StartsWith(
                            "case:",StringComparison.Ordinal))
                            .Select(l=>l.ConditionId.Substring(5)).ToArray();
                        branchCondition=ctx=>EvaluateWiringGuard(ctx,source,guard,earlier,alternatives);
                    }
                    bool allowSkippedJoin=incomingControl.Length>1;
                    var dependencies=entering.Select(l=>WireId(l.Source.BlockId))
                        .Distinct(StringComparer.Ordinal).ToArray();
                    var inputs=new List<MRKExecutionInput>();
                    inputs.AddRange(ExposedInputs(wiring,block.Id));
                    inputs.AddRange(InternalDataInputs(wiring,block.Id,resolver,snapshot,planReader));
                    string childId=null;
                    if(block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))
                    {
                        Guid nestedId;
                        if(!Guid.TryParseExact(block.CapabilityId.Substring("moc.invoke:".Length),
                            "N",out nestedId)||nestedId==content.Id)
                            throw new InvalidOperationException("moc.deep.invalid_target");
                        if(!content.References.Any(r=>r.Kind==MRIReferenceKind.Content&&r.TargetId==nestedId))
                            throw new InvalidOperationException("moc.deep.missing_content_ref");
                        var nested=snapshot.Find(nestedId);
                        if(nested==null||nested.Kind!=MEMNodeKind.Content)
                            throw new InvalidOperationException("moc.deep.missing_content");
                        BuildDeepFlow(snapshot,nested,resolver,catalog,active,planReader);
                        childId=nestedId.ToString("N");
                    }
                    MOLStepPolicy stepPolicy;
                    MRKExecutionPolicy policy=null;
                    if(wiring.Policies.TryGetValue(block.Id,out stepPolicy))
                    {
                        if(childId!=null)
                            throw new InvalidOperationException("moc.deep.invoke_policy_not_supported");
                        policy=new MRKExecutionPolicy(stepPolicy.MaxAttempts,
                            TimeSpan.FromMilliseconds(stepPolicy.RetryDelayMs),
                            stepPolicy.TimeoutMs>0?(TimeSpan?)TimeSpan.FromMilliseconds(stepPolicy.TimeoutMs):null,
                            stepPolicy.Idempotent);
                    }
                    definitions.Add(childId==null?
                        new MRKStepCompileRequest(WireId(block.Id),block.CapabilityId,dependencies,
                            inputs,executionGroup:stepPolicy==null?null:stepPolicy.ExecutionGroup,
                            policy:policy,condition:branchCondition,
                            allowSkippedDependencies:allowSkippedJoin):
                        new MRKStepCompileRequest(WireId(block.Id),null,dependencies,inputs,
                            condition:branchCondition,allowSkippedDependencies:allowSkippedJoin,
                            executionGroup:stepPolicy==null?null:stepPolicy.ExecutionGroup,
                            subflowId:childId));
                }
                catalog.Add(id,new MRKFlowCompileDefinition(id,definitions,
                    BuildPublicExports(wiring,resolver,snapshot,planReader)));
            }
            finally{active.Remove(content.Id);}
        }

        private static void AppendDeepOrigins(MEMProjectSnapshot snapshot,MEMNodeSnapshot content,
            string prefix,Guid[] calls,Guid[] instancePath,ICollection<MOCStepOrigin> output,
            HashSet<Guid> active,MOCPlanLogicReader source)
        {
            if(!active.Add(content.Id))throw new InvalidOperationException("moc.origin.recursion");
            try
            {
                var wiring=MOLWiringCodec.Get(content);
                foreach(var block in wiring.Blocks)
                {
                    string step=prefix+WireId(block.Id);
                    if(!output.Any(x=>x.StepId==step))
                        output.Add(new MOCStepOrigin(step,new MRIObjectAddress(snapshot.ProjectId,
                            content.StorageRootId,content.Id,instancePath),block.Id,
                            calls,null,"InvokedAction"));
                    if(!block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))continue;
                    Guid nestedId;
                    if(!Guid.TryParseExact(block.CapabilityId.Substring("moc.invoke:".Length),
                        "N",out nestedId))throw new InvalidOperationException("moc.origin.bad_target");
                    var nested=snapshot.Find(nestedId);
                    if(nested==null)throw new InvalidOperationException("moc.origin.missing_target");
                    string gate=step+"$entry";
                    if(!output.Any(x=>x.StepId==gate))
                        output.Add(new MOCStepOrigin(gate,new MRIObjectAddress(snapshot.ProjectId,
                            content.StorageRootId,content.Id,instancePath),block.Id,
                            calls,null,"InvokeEnter"));
                    var nextCalls=calls.Concat(new[]{block.Id}).ToArray();
                    if((source==null ? MOLWiringCodec.Get(nested):source.Wiring(nested)).FlowOutputs.Count>0)
                    {
                        string commit=step+".__commit";
                        if(!output.Any(x=>x.StepId==commit))
                            output.Add(new MOCStepOrigin(commit,new MRIObjectAddress(snapshot.ProjectId,
                                nested.StorageRootId,nested.Id,instancePath),block.Id,
                                nextCalls,null,"InvokeCommit"));
                    }
                    AppendDeepOrigins(snapshot,nested,step+".",nextCalls,instancePath,output,active,source);
                }
            }
            finally{active.Remove(content.Id);}
        }
        private static MOCCompiledPlan CompileWiring(MEMProjectSnapshot snapshot, MEMNodeSnapshot entry,
            Guid[] instancePath, MOLWiringDefinition wiring, IMRKCapabilityResolver resolver,
            MOCPlanLogicReader source)
        {
            wiring.ValidateExecutable();
            var blocks=wiring.Blocks.ToDictionary(x=>x.Id);
            var stepIds=blocks.Keys.ToDictionary(x=>x,x=>"w"+x.ToString("N"));
            var requests=new List<MRKStepCompileRequest>();
            var origins=new List<MOCStepOrigin>();
            var inputDefaults=new Dictionary<string,object>(StringComparer.Ordinal);
            var subflowDefinitions=new Dictionary<string,MRKFlowCompileDefinition>(StringComparer.Ordinal);
            var transientResolver=new MOCResolver(resolver);
            foreach(var block in wiring.Blocks)
            {
                var entering=wiring.Links.Where(x=>x.Target.BlockId==block.Id).ToArray();
                var depends=entering.Select(x=>stepIds[x.Source.BlockId])
                    .Distinct(StringComparer.Ordinal).ToArray();
                var incomingControl=entering.Where(l=>l.Kind==MPCPortKind.Control).ToArray();
                Func<MRKExecutionContext,bool> branchCondition=null;
                if(incomingControl.Length==1&&incomingControl[0].ConditionId.Length>0)
                {
                    var guardedLink=incomingControl[0];
                    string sourceStep=stepIds[guardedLink.Source.BlockId];
                    string guard=guardedLink.ConditionId;
                    MOLLink[] ordered=wiring.Links.Where(l=>l.Source.BlockId==guardedLink.Source.BlockId&&
                        l.Kind==MPCPortKind.Control).ToArray();
                    string[] earlierGuards=ordered.TakeWhile(l=>l.Id!=guardedLink.Id)
                        .Select(l=>l.ConditionId).ToArray();
                    string[] cases=ordered.Where(l=>l.ConditionId.StartsWith(
                        "case:",StringComparison.Ordinal)).Select(l=>l.ConditionId.Substring(5)).ToArray();
                    branchCondition=ctx=>EvaluateWiringGuard(
                        ctx,sourceStep,guard,earlierGuards,cases);
                }
                bool allowSkippedJoin=incomingControl.Length>1;
                var inputs=new List<MRKExecutionInput>();
                inputs.AddRange(ExposedInputs(wiring,block.Id));
                foreach(var link in entering.Where(x=>x.Kind==MPCPortKind.Data||x.Kind==MPCPortKind.Stream))
                {
                    if(entering.Count(x=>(x.Kind==MPCPortKind.Data||x.Kind==MPCPortKind.Stream) &&
                        x.Target.PortMemberId==link.Target.PortMemberId)>1)
                        throw new InvalidOperationException("moc.wiring.data_fanin_requires_aggregator:"+
                            link.Target.BlockId+":"+link.Target.PortMemberId);
                    var sourcePorts=wiring.Ports[link.Source.BlockId];
                    var from=sourcePorts.Single(x=>x.MemberId==link.Source.PortMemberId);
                    var target=wiring.Ports[block.Id].Single(x=>x.MemberId==link.Target.PortMemberId);
                    var resolvedCapability=blocks[link.Source.BlockId];
                    MRKResolvedCapability actual=null;
                    bool sourceIsInvoke=resolvedCapability.CapabilityId.StartsWith(
                        "moc.invoke:",StringComparison.Ordinal);
                    MOLWiringDefinition published=null;
                    if(sourceIsInvoke)
                    {
                        Guid contentId;
                        if(!Guid.TryParseExact(resolvedCapability.CapabilityId.Substring(
                            "moc.invoke:".Length),"N",out contentId))
                            throw new InvalidOperationException("moc.invoke.source_id");
                        var content=snapshot.Find(contentId);
                        if(content==null)throw new InvalidOperationException("moc.invoke.missing_source_content");
                        published=source==null ?
                            MOLWiringCodec.Get(content):source.Wiring(content);
                        if(published==null||!published.FlowOutputs.ContainsKey(from.MemberId))
                            throw new InvalidOperationException("moc.invoke.unknown_output:"+from.MemberId);
                    }
                    else if(!resolver.TryResolve(resolvedCapability.CapabilityId,out actual)||actual==null)
                        throw new InvalidOperationException("moc.wiring.unknown_producer:"+resolvedCapability.CapabilityId);
                    MRKInputSource sourceKind;
                    string sourceName=stepIds[link.Source.BlockId];
                    Type valueType=ResolveWiringType(target.TypeId);
                    if(sourceIsInvoke)
                    {
                        var publicSource=published.FlowOutputs[from.MemberId];
                        var declared=published.Ports[publicSource.BlockId].Single(p=>
                            p.MemberId==publicSource.PortMemberId);
                        if(ResolveWiringType(declared.TypeId)!=valueType)
                            throw new InvalidOperationException("moc.invoke.output_type_mismatch");
                        sourceKind=MRKInputSource.StepOutput;
                        sourceName+="."+from.MemberId;
                    }
                    else if(actual.DataOutputs.ContainsKey(from.MemberId))
                    {
                        sourceKind=MRKInputSource.StepOutput;
                        sourceName+="."+from.MemberId;
                    }
                    else if(sourcePorts.Count(x=>(x.Kind==MPCPortKind.Data||x.Kind==MPCPortKind.Stream)&&
                        x.Direction!=MPCPortDirection.Input)==1)
                        sourceKind=MRKInputSource.StepResult;
                    else
                        throw new InvalidOperationException("moc.wiring.ambiguous_output:"+
                            link.Source.BlockId+":"+from.MemberId);
                    inputs.Add(new MRKExecutionInput(target.MemberId,sourceKind,sourceName,
                        valueType,target.Required));
                }
                IReadOnlyDictionary<string,string> parameters;
                if(wiring.Parameters.TryGetValue(block.Id,out parameters))
                foreach(var parameter in parameters)
                {
                    var port=wiring.Ports[block.Id].Single(p=>p.MemberId==parameter.Key);
                    Type type=ResolveWiringType(port.TypeId);
                    if(type==typeof(object)||type==typeof(byte[]))
                        throw new InvalidOperationException("moc.wiring.parameter_unknown_type");
                    string inputKey=stepIds[block.Id]+"."+parameter.Key;
                    object literal=type==typeof(string)?(object)parameter.Value:
                        Convert.ChangeType(parameter.Value,type,CultureInfo.InvariantCulture);
                    inputs.Add(new MRKExecutionInput(parameter.Key,MRKInputSource.FlowInput,
                        inputKey,type,port.Required));
                    inputDefaults.Add(inputKey,literal);
                }
                string childId=null;
                string executeCapability=block.CapabilityId;
                if(block.CapabilityId=="moc.state-machine")
                {
                    MOLStateMachineDefinition machine;
                    if(!wiring.StateMachines.TryGetValue(block.Id,out machine))
                        throw new InvalidOperationException("moc.state.missing_definition");
                    if(machine.States.Any(x=>x.EntryCapability.Length>0||x.ExitCapability.Length>0))
                        throw new InvalidOperationException("moc.state_actions_not_supported");
                    string inputName="moc.state.events."+block.Id.ToString("N");
                    string checkpointName="moc.state.checkpoint."+block.Id.ToString("N");
                    executeCapability="moc.internal.state."+block.Id.ToString("N");
                    var definition=machine;
                    transientResolver.Register(executeCapability,ctx=>{
                        object events;
                        if(!ctx.Inputs.TryGetValue(inputName,out events)||
                            !(events is IEnumerable<string>))
                            throw new InvalidOperationException("moc.state.events_input_missing:"+inputName);
                        object previous;
                        IReadOnlyDictionary<string,string> checkpoint=null;
                        if(ctx.Inputs.TryGetValue(checkpointName,out previous))
                        {
                            checkpoint=previous as IReadOnlyDictionary<string,string>;
                            if(checkpoint==null)
                                throw new InvalidOperationException(
                                    "moc.state.invalid_checkpoint_type:"+checkpointName);
                        }
                        return (object)definition.Run((IEnumerable<string>)events,
                            ctx.Inputs,checkpoint);
                    },typeof(object));
                }
                if(block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))
                {
                    // Declared child inputs are mapped by name through the existing MRK entry Gate.
                    // G04-F call-local typed defaults remain below.
                    Guid targetId;
                    if(!Guid.TryParseExact(block.CapabilityId.Substring("moc.invoke:".Length),
                        "N",out targetId)||targetId==entry.Id)
                        throw new InvalidOperationException("moc.invoke.invalid_target");
                    if(!entry.References.Any(r=>r.TargetId==targetId&&
                        r.Kind==MRIReferenceKind.Content))
                        throw new InvalidOperationException("moc.invoke.missing_content_ref");
                    var target=snapshot.Find(targetId);
                    if(target==null||target.Kind!=MEMNodeKind.Content)
                        throw new InvalidOperationException("moc.invoke.missing_content");
                    var nested=source==null ?
                        MOLWiringCodec.Get(target):source.Wiring(target);
                    if(nested==null)throw new InvalidOperationException("moc.invoke.requires_v2");
                    nested.ValidateExecutable();
                    childId=targetId.ToString("N");
                    if(nested.Blocks.Any(b=>b.CapabilityId.StartsWith(
                            "moc.invoke:",StringComparison.Ordinal)))
                        BuildDeepFlow(snapshot,target,resolver,subflowDefinitions,
                            new HashSet<Guid>(),source);
                    // 每次调用单独命名运行默认值；依赖MRK入口Gate做类型安全映射。
                    // 既不扩散子Content全局变量，也不将参数改为并行Graph。
                    foreach(var nestedValues in nested.Parameters)
                    foreach(var parameter in nestedValues.Value)
                    {
                        var port=nested.Ports[nestedValues.Key].Single(p=>
                            p.MemberId==parameter.Key);
                        Type type=ResolveWiringType(port.TypeId);
                        if(type==typeof(object)||type==typeof(byte[]))
                            throw new InvalidOperationException("moc.invoke.parameter_type");
                        string name="p"+nestedValues.Key.ToString("N")+"_"+parameter.Key;
                        string uniqueKey=stepIds[block.Id]+"."+name;
                        object value=type==typeof(string)?(object)parameter.Value:
                            Convert.ChangeType(parameter.Value,type,CultureInfo.InvariantCulture);
                        inputs.Add(new MRKExecutionInput(name,MRKInputSource.FlowInput,
                            uniqueKey,type,port.Required));
                        inputDefaults.Add(uniqueKey,value);
                    }
                    if(!subflowDefinitions.ContainsKey(childId))
                    {
                        var innerSteps=nested.Blocks.Select(b=>{
                            MOLStepPolicy stepPolicy;
                            MRKExecutionPolicy policy=null;
                            if(nested.Policies.TryGetValue(b.Id,out stepPolicy))
                                policy=new MRKExecutionPolicy(stepPolicy.MaxAttempts,
                                    TimeSpan.FromMilliseconds(stepPolicy.RetryDelayMs),
                                    stepPolicy.TimeoutMs>0?
                                        (TimeSpan?)TimeSpan.FromMilliseconds(stepPolicy.TimeoutMs):null,
                                    stepPolicy.Idempotent);
                            var innerInputs=new List<MRKExecutionInput>();
                            innerInputs.AddRange(ExposedInputs(nested,b.Id));
                            innerInputs.AddRange(InternalDataInputs(nested,b.Id,resolver,snapshot,source));
                            IReadOnlyDictionary<string,string> values;
                            if(nested.Parameters.TryGetValue(b.Id,out values))
                            foreach(var parameter in values)
                            {
                                var port=nested.Ports[b.Id].Single(p=>p.MemberId==parameter.Key);
                                Type type=ResolveWiringType(port.TypeId);
                                if(type==typeof(object)||type==typeof(byte[]))
                                    throw new InvalidOperationException("moc.invoke.parameter_type");
                                innerInputs.Add(new MRKExecutionInput(parameter.Key,
                                    MRKInputSource.FlowInput,"p"+b.Id.ToString("N")+
                                    "_"+parameter.Key,type,port.Required));
                            }
                            return new MRKStepCompileRequest("w"+b.Id.ToString("N"),
                                b.CapabilityId,nested.Links.Where(l=>l.Target.BlockId==b.Id)
                                    .Select(l=>"w"+l.Source.BlockId.ToString("N")),
                                inputs:innerInputs,policy:policy,
                                executionGroup:stepPolicy==null?null:stepPolicy.ExecutionGroup);
                        }).ToArray();
                        subflowDefinitions.Add(childId,
                            new MRKFlowCompileDefinition(childId,innerSteps,
                                BuildPublicExports(nested,resolver,snapshot,source)));
                    }
                    if(nested.FlowOutputs.Count>0)
                        origins.Add(new MOCStepOrigin(stepIds[block.Id]+".__commit",
                            new MRIObjectAddress(snapshot.ProjectId,target.StorageRootId,
                                target.Id,instancePath),block.Id,new[]{block.Id},null,"InvokeCommit"));
                    origins.Add(new MOCStepOrigin(stepIds[block.Id]+"$entry",
                        new MRIObjectAddress(snapshot.ProjectId,entry.StorageRootId,
                            entry.Id,instancePath),block.Id,new Guid[0],null,"InvokeEnter"));
                    foreach(var inner in nested.Blocks)
                    {
                        origins.Add(new MOCStepOrigin(stepIds[block.Id]+".w"+inner.Id.ToString("N"),
                            new MRIObjectAddress(snapshot.ProjectId,target.StorageRootId,
                                target.Id,instancePath),
                            inner.Id,new[]{block.Id},null,"InvokedAction"));
                    }
                    if(nested.Blocks.Any(b=>b.CapabilityId.StartsWith(
                        "moc.invoke:",StringComparison.Ordinal)))
                        AppendDeepOrigins(snapshot,target,stepIds[block.Id]+".",
                            new[]{block.Id},instancePath,origins,new HashSet<Guid>(),source);
                }
                MOLStepPolicy configuration;
                MRKExecutionPolicy executionPolicy=null;
                if(wiring.Policies.TryGetValue(block.Id,out configuration))
                {
                    if(childId!=null)
                        throw new InvalidOperationException("moc.invoke.workflow_policy_not_supported");
                    executionPolicy=new MRKExecutionPolicy(configuration.MaxAttempts,
                        TimeSpan.FromMilliseconds(configuration.RetryDelayMs),
                        configuration.TimeoutMs>0?
                            (TimeSpan?)TimeSpan.FromMilliseconds(configuration.TimeoutMs):null,
                        configuration.Idempotent);
                }
                requests.Add(childId==null?
                    new MRKStepCompileRequest(stepIds[block.Id],executeCapability,
                        depends,inputs,condition:branchCondition,
                        allowSkippedDependencies:allowSkippedJoin,policy:executionPolicy,
                        executionGroup:configuration==null?null:configuration.ExecutionGroup):
                    new MRKStepCompileRequest(stepIds[block.Id],null,
                        depends,inputs,condition:branchCondition,
                        allowSkippedDependencies:allowSkippedJoin,subflowId:childId,
                        executionGroup:configuration==null?null:configuration.ExecutionGroup));
                origins.Add(new MOCStepOrigin(stepIds[block.Id],
                    new MRIObjectAddress(snapshot.ProjectId,entry.StorageRootId,entry.Id,instancePath),
                    block.Id,new Guid[0],null,
                    block.CapabilityId=="moc.state-machine"?"StateMachine":"Action"));
            }
            string planId="moc."+snapshot.ProjectId.ToString("N")+"."+
                entry.Id.ToString("N")+".r"+
                snapshot.Revision.ToString(CultureInfo.InvariantCulture)+".wired";
            var publicExports=BuildPublicExports(wiring,resolver,snapshot,source);
            var compiled=subflowDefinitions.Count==0?
                new MRKExecutionPlanCompiler().Compile(planId,requests,transientResolver,
                    publicExports):
                new MRKExecutionOrchestrationCompiler().Compile(planId,
                    new MOCInlineContentCatalog(planId,requests,subflowDefinitions,publicExports),
                    transientResolver);
            if(!compiled.Succeeded)
                throw new InvalidOperationException("moc.wiring.compile_rejected:"+
                    string.Join(";",compiled.Diagnostics.Select(x=>x.Code+"@"+x.StepId+":"+x.Message)));
            return new MOCCompiledPlan(compiled.Plan,new MOCSourceMap(origins),
                snapshot.ProjectId,snapshot.Revision,inputDefaults);
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
                case "stream.bytes":return typeof(CxVisual.Mechanism.Data.RuntimeData.MDSBoundedByteStream);
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
            internal readonly List<MRKControlRegion> Regions = new List<MRKControlRegion>();
            internal readonly List<MOCStepOrigin> Origins = new List<MOCStepOrigin>();
            internal readonly MOCResolver Resolver;
            private readonly MEMProjectSnapshot _snapshot;
            private readonly MOCPlanLogicReader _source;
            private readonly Guid[] _instances;
            private int _serial;
            private readonly HashSet<Guid> _activeContents = new HashSet<Guid>();
            internal Compilation(MEMProjectSnapshot snapshot, IMRKCapabilityResolver resolver, Guid[] instances,
                MOCPlanLogicReader source)
            { _snapshot = snapshot; _source=source; Resolver = new MOCResolver(resolver); _instances = instances; }
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
            private Func<MRKExecutionContext,bool> Predicate(MOLExpression expression,int iteration)
            {
                if(expression==null)return ctx=>true;
                var stepIds=new Dictionary<MOLExpression,string>();
                Action<MOLExpression> bind=null;
                bind=expr=>{
                    if(expr.Operator!=MOLLogicalOperator.Scalar)
                    {
                        foreach(MOLExpression term in expr.Terms)bind(term);
                        return;
                    }
                    if(expr.Source!=MOLExpressionSource.StepResult)return;
                    Guid id;
                    if(!Guid.TryParse(expr.Name,out id))
                        throw new InvalidOperationException("moc.step_result_requires_block_id");
                    var origin=Origins.LastOrDefault(o=>o.BlockId==id&&o.Role=="Action");
                    if(origin==null)
                        throw new InvalidOperationException("moc.step_result_not_preceding:"+expr.Name);
                    stepIds.Add(expr,origin.StepId);
                };
                bind(expression);
                return ctx=>MOCExpressions.Evaluate(expression,ctx,
                    iteration<0?ctx.IterationIndex:iteration,
                    leaf=>{
                        string id;
                        return stepIds.TryGetValue(leaf,out id)?id:null;
                    });
            }
            private string ResolveLoopConditionStepId(MOLExpression expression)
            {
                if(expression==null||expression.Source!=MOLExpressionSource.StepResult)
                    return null;
                Guid id;
                if(!Guid.TryParse(expression.Name,out id))
                    throw new InvalidOperationException("moc.loop_condition_step_id");
                MOCStepOrigin action=Origins.LastOrDefault(o=>
                    o.BlockId==id&&o.Role=="Action");
                if(action==null)
                    throw new InvalidOperationException("moc.loop_condition_step_missing");
                return action.StepId;
            }

            /// <summary>把Try/Catch/Finally分别编译为不可编辑的MRK嵌套Plan；共用同一Resolver/SourceMap。</summary>
            private MRKExecutionPlan CompileProtectedBody(MOLControlBlock body,
                MEMNodeSnapshot owner,Guid[] calls,int depth,string regionId,
                IterationScope outerScope)
            {
                if(body==null)return null;
                int first=Steps.Count,firstRegion=Regions.Count;
                IterationScope localScope=null;
                string[] dependencies=new string[0];
                if(outerScope!=null)
                {
                    // Try内部Plan不得依赖不在自己Plan内的Step。重建轻量迭代入口，
                    // 从当前MRK Invocation读取Index/Item，仍归同一MDS作用域。
                    string indexCapability="moc.internal.try.index."+NewId(body);
                    Resolver.Register(indexCapability,ctx=>ctx.IterationIndex,typeof(int));
                    string indexStep=Add(body,owner,calls,null,"TryIterationIndex",
                        indexCapability,new string[0]);
                    string itemStep=null;
                    if(outerScope.ItemStep!=null)
                    {
                        string input=outerScope.Input;
                        string itemCapability="moc.internal.try.item."+NewId(body);
                        Resolver.Register(itemCapability,ctx=>
                            ((IList)ctx.Inputs[input])[ctx.IterationIndex],typeof(object));
                        itemStep=Add(body,owner,calls,null,"TryIterationItem",
                            itemCapability,new[]{indexStep});
                    }
                    localScope=new IterationScope{Index=-1,IndexStep=indexStep,
                        ItemStep=itemStep,Input=outerScope.Input};
                    dependencies=itemStep==null?new[]{indexStep}:new[]{itemStep};
                }
                Emit(body,owner,dependencies,calls,localScope,depth+1);
                MRKStepCompileRequest[] requests=Steps.Skip(first).ToArray();
                MRKControlRegion[] nested=Regions.Skip(firstRegion).ToArray();
                Steps.RemoveRange(first,Steps.Count-first);
                Regions.RemoveRange(firstRegion,Regions.Count-firstRegion);
                var compiled=new MRKExecutionPlanCompiler().Compile(
                    regionId,requests,Resolver,regions:nested);
                if(!compiled.Succeeded)
                    throw new InvalidOperationException("moc.try_body_compile:"+
                        string.Join(";",compiled.Diagnostics.Select(
                            d=>d.Code+":"+d.Message)));
                return compiled.Plan;
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
                    var logic = _source==null ?
                        MOLLogicCodec.Get(content):_source.Structured(content);
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
                        return new[] {Add(block, owner, calls, scope == null || scope.Index < 0 ? (int?)null : scope.Index,
                            "Action", block.CapabilityId, dependencies, inputs: inputs)};
                    }
                    case MOLControlKind.Wait:
                    {
                        if(block.Wait==null)throw new InvalidOperationException("moc.wait_missing");
                        if(block.Wait.Event!=null&&!block.Wait.Event.IsLegacyTask)
                            throw new InvalidOperationException("moc.event_delivery_not_supported");
                        int milliseconds=block.Wait.DurationMilliseconds;
                        string eventInput=block.Wait.EventInput;
                        string capability="moc.internal.wait."+NewId(block);
                        Resolver.RegisterAsync(capability,async (context,token)=>{
                            if(string.IsNullOrEmpty(eventInput))
                            {
                                await Task.Delay(milliseconds,token).ConfigureAwait(false);
                                token.ThrowIfCancellationRequested();
                                return (object)true;
                            }
                            object source;
                            if(!context.Inputs.TryGetValue(eventInput,out source)||
                                !(source is Task))
                                throw new InvalidOperationException(
                                    "moc.wait.event_input_missing_or_invalid:"+eventInput);
                            Task signal=(Task)source;
                            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token))
                            {
                                Task limit=Task.Delay(milliseconds,linked.Token);
                                Task winner=await Task.WhenAny(signal,limit).ConfigureAwait(false);
                                token.ThrowIfCancellationRequested();
                                if(winner!=signal)
                                    throw new TimeoutException("moc.wait.event_timeout:"+eventInput);
                                linked.Cancel();
                                await signal.ConfigureAwait(false);
                                token.ThrowIfCancellationRequested();
                                return (object)true;
                            }
                        },typeof(bool));
                        return new[]{Add(block,owner,calls,null,"Wait",capability,before)};
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
                        return new[]{Add(block, owner, calls, scope == null || scope.Index < 0 ? (int?)null : scope.Index,
                            "ParallelJoin", "moc.internal.noop", exits)};
                    }
                    case MOLControlKind.Branch:
                    {
                        int first=Steps.Count;
                        string entry=Add(block,owner,calls,scope == null || scope.Index < 0 ? (int?)null : scope.Index,
                            "BranchEntry","moc.internal.noop",before);
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
                            string gate=Add(block, owner, calls, scope == null || scope.Index < 0 ? (int?)null : scope.Index,
                                "Case." + i, "moc.internal.noop", new[]{entry}, guard);
                            joins.AddRange(Emit(cases[i].Body, owner, new[]{gate}, calls, scope, depth+1));
                        }
                        string exit=Add(block,owner,calls,scope == null || scope.Index < 0 ? (int?)null : scope.Index,
                            "BranchJoin","moc.internal.noop",joins,
                            condition: ctx => !before.Any(d => {
                                MRKStepState state;
                                return ctx.TryGetScopedState(d, out state) &&
                                    state == MRKStepState.Skipped;
                            }), join:true);
                        string regionId="moc.branch."+block.Id.ToString("N")+
                            (scope==null?"":".i"+scope.Index.ToString(CultureInfo.InvariantCulture))+
                            (calls.Length==0?"":".c"+string.Join(".",calls.Select(id=>id.ToString("N"))));
                        Regions.Add(new MRKControlRegion(regionId,entry,exit,
                            Steps.Skip(first).Select(step=>step.Id)));
                        return new[]{exit};
                    }
                    case MOLControlKind.Try:
                    {
                        if(block.TryRegion==null)
                            throw new InvalidOperationException("moc.try_missing_definition");
                        // MOL可声明具名错误；MRK当前只支持Catch-all。
                        // 未获得类型化错误分派合同前明确拒绝，不能悄悄忽略具名Catch。
                        if(block.TryRegion.Catches.Count>0)
                            throw new InvalidOperationException("moc.try_typed_catch_not_supported");
                        string baseId="moc.try."+block.Id.ToString("N");
                        MRKExecutionPlan tryBody=CompileProtectedBody(
                            block.TryRegion.TryBody,owner,calls,depth,baseId+".body",scope);
                        MRKExecutionPlan catchBody=CompileProtectedBody(
                            block.TryRegion.CatchBody,owner,calls,depth,baseId+".catch",scope);
                        MRKExecutionPlan finallyBody=CompileProtectedBody(
                            block.TryRegion.FinallyBody,owner,calls,depth,baseId+".finally",scope);
                        var region=new MRKTryRegion(tryBody,catchBody,finallyBody);
                        string id=NewId(block);
                        string capability="moc.internal.try."+id;
                        Resolver.Register(capability,ctx=>(object)true,typeof(bool));
                        Steps.Add(new MRKStepCompileRequest(id,capability,before,
                            tryRegion:region));
                        Origins.Add(new MOCStepOrigin(id,new MRIObjectAddress(
                            _snapshot.ProjectId,owner.StorageRootId,owner.Id,_instances),
                            block.Id,calls,null,"TryRegion"));
                        return new[]{id};
                    }
                    case MOLControlKind.Loop:
                    case MOLControlKind.ForEach:
                    {
                        // R05：循环体只编译一次。临时Step列表编译到MRK嵌套Plan，
                        // 再从外层移走；绝不能按MaxIterations生成大量Step。
                        MOLLoopDefinition loop=block.Loop;
                        int first=Steps.Count,firstRegion=Regions.Count;
                        string entry=Add(block,owner,calls,null,"IterationEntry",
                            "moc.internal.noop",new string[0]);
                        string indexCapability="moc.internal.index."+entry;
                        Resolver.Register(indexCapability,ctx=>ctx.IterationIndex,typeof(int));
                        string indexStep=Add(block,owner,calls,null,"IterationIndex",
                            indexCapability,new[]{entry});
                        string itemStep=null;
                        if(block.Kind==MOLControlKind.ForEach)
                        {
                            string itemCapability="moc.internal.item."+entry;
                            Resolver.Register(itemCapability,ctx=>
                                ((IList)ctx.Inputs[loop.CollectionInput])[ctx.IterationIndex],
                                typeof(object));
                            itemStep=Add(block,owner,calls,null,"IterationItem",
                                itemCapability,new[]{indexStep});
                        }
                        var iterationScope=new IterationScope{
                            Index=-1,IndexStep=indexStep,ItemStep=itemStep,
                            Input=loop.CollectionInput};
                        string[] bodyStart=itemStep==null?
                            new[]{indexStep}:new[]{itemStep};
                        string[] exits=Emit(loop.Body,owner,bodyStart,calls,
                            iterationScope,depth+1);
                        Add(block,owner,calls,null,"IterationJoin","moc.internal.noop",
                            exits,join:true);
                        MRKStepCompileRequest[] requests=Steps.Skip(first).ToArray();
                        MRKControlRegion[] nestedRegions=Regions.Skip(firstRegion).ToArray();
                        Steps.RemoveRange(first,Steps.Count-first);
                        Regions.RemoveRange(firstRegion,Regions.Count-firstRegion);
                        var compiledBody=new MRKExecutionPlanCompiler().Compile(
                            "moc.loop."+block.Id.ToString("N"),requests,Resolver,
                            regions:nestedRegions);
                        if(!compiledBody.Succeeded)
                            throw new InvalidOperationException("moc.loop_body_compile:"+
                                string.Join(";",compiledBody.Diagnostics.Select(d=>d.Code+":"+d.Message)));
                        // 迭代条件可引用输入、迭代索引或上一轮已提交的StepResult。
                        Func<MRKExecutionContext,int,bool> predicate=loop.Condition==null?
                            (Func<MRKExecutionContext,int,bool>)null:
                            (ctx,i)=>MOCExpressions.Evaluate(loop.Condition,ctx,i,
                                ResolveLoopConditionStepId(loop.Condition));
                        MRKLoopRegion region=new MRKLoopRegion(compiledBody.Plan,
                            loop.MaxIterations,predicate,
                            block.Kind==MOLControlKind.ForEach?loop.CollectionInput:null,
                            loop.Mode==MOLLoopMode.DoWhile);
                        string step=NewId(block);
                        // 控制Step在MRK中返回实际完成的迭代次数(int)；
                        // 编译期端口类型也必须为int，不能继续冒用noop的bool。
                        string loopCapability="moc.internal.loop."+step;
                        Resolver.Register(loopCapability,ctx=>(object)0,typeof(int));
                        Steps.Add(new MRKStepCompileRequest(step,loopCapability,
                            before,loopRegion:region));
                        Origins.Add(new MOCStepOrigin(step,new MRIObjectAddress(
                            _snapshot.ProjectId,owner.StorageRootId,owner.Id,_instances),
                            block.Id,calls,null,"DynamicLoop"));
                        return new[]{step};
                    }
                    case MOLControlKind.Exit:
                    {
                        if(block.ExitKind==MOLExitKind.Throw)
                            throw new InvalidOperationException("moc.throw_not_supported");
                        string signal=block.ExitKind==MOLExitKind.Break?"break":
                            block.ExitKind==MOLExitKind.Continue?"continue":"return";
                        string marker="moc.internal.exit."+NewId(block);
                        Resolver.Register(marker,ctx=>{
                            ctx.SignalControlExit(signal);return (object)signal;
                        },typeof(string));
                        return new[]{Add(block,owner,calls,
                            scope == null || scope.Index < 0 ? (int?)null : scope.Index,
                            "LoopExit",marker,before)};
                    }
                    case MOLControlKind.Invoke:
                    {
                        Guid target;
                        if (!Guid.TryParse(block.TargetFlow, out target))
                            throw new InvalidOperationException("moc.invoke_target_must_be_content_guid");
                        var content=_snapshot.Find(target);
                        if(content==null||content.Kind!=MEMNodeKind.Content)
                            throw new InvalidOperationException("moc.invoke_missing_content");
                        if(!owner.References.Any(r=>r.Kind==MRIReferenceKind.Content&&
                            r.TargetId==target))
                            throw new InvalidOperationException("moc.invoke_missing_content_ref");
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
