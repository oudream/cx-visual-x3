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
            return _steps.Values.Where(o => o.BlockId == blockId)
                .OrderBy(o => o.StepId, StringComparer.Ordinal).ToList().AsReadOnly();
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
