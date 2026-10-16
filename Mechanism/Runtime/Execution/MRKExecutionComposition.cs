using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>外部编排Owner提供的临时只读定义；Runtime不保存第二棵CxNode工程树。</summary>
    public sealed class MRKFlowCompileDefinition
    {
        public MRKFlowCompileDefinition(string id, IEnumerable<MRKStepCompileRequest> steps)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("流程ID不能为空", "id");
            if (steps == null) throw new ArgumentNullException("steps");
            Id = id;
            Steps = new ReadOnlyCollection<MRKStepCompileRequest>(
                new List<MRKStepCompileRequest>(steps));
        }
        public string Id { get; private set; }
        public IList<MRKStepCompileRequest> Steps { get; private set; }
    }
    public interface IMRKFlowCompileCatalog
    {
        bool TryGet(string flowId, out MRKFlowCompileDefinition definition);
    }

    /// <summary>
    /// 统一编译的递归展开阶段：具名子流程按调用点展开，添加入口Guard和出口Join节点，
    /// 之后全部交由原MRKExecutionEngine/FCRWorker执行。拒绝递归引用和过大展开。
    /// </summary>
    public sealed class MRKExecutionOrchestrationCompiler
    {
        private const int MaxDepth = 16;
        private const int MaxExpandedSteps = 4096;

        public MRKCompileResult Compile(string rootId, IMRKFlowCompileCatalog catalog,
            IMRKCapabilityResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(rootId)) throw new ArgumentException("入口不能为空","rootId");
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (resolver == null) throw new ArgumentNullException("resolver");
            List<MRKCompileDiagnostic> diagnostics = new List<MRKCompileDiagnostic>();
            List<MRKExecutionStep> steps = new List<MRKExecutionStep>();
            try
            {
                int expanded = 0;
                steps = Expand(rootId, catalog, resolver,
                    new HashSet<string>(StringComparer.Ordinal), diagnostics, ref expanded);
                if (diagnostics.Count != 0) return new MRKCompileResult(null, diagnostics);
                return new MRKCompileResult(new MRKExecutionPlan(rootId, steps), diagnostics);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                diagnostics.Add(new MRKCompileDiagnostic(rootId, "INVALID_ORCHESTRATION", ex.Message));
                return new MRKCompileResult(null, diagnostics);
            }
        }

        private static List<MRKExecutionStep> Expand(string id,
            IMRKFlowCompileCatalog catalog, IMRKCapabilityResolver resolver,
            HashSet<string> active, IList<MRKCompileDiagnostic> diagnostics,
            ref int expanded)
        {
            if (active.Count >= MaxDepth)
                throw new InvalidOperationException("嵌套子流程深度超过" + MaxDepth);
            if (!active.Add(id))
                throw new InvalidOperationException("递归子流程循环引用：" + id);
            try
            {
                MRKFlowCompileDefinition definition;
                if (!catalog.TryGet(id, out definition) || definition == null)
                    throw new ArgumentException("找不到子流程定义：" + id);
                List<MRKExecutionStep> steps = new List<MRKExecutionStep>();
                foreach (MRKStepCompileRequest request in definition.Steps)
                {
                    if (request == null)
                    {
                        diagnostics.Add(new MRKCompileDiagnostic(id, "NULL_STEP", "编排节点为null"));
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(request.Id))
                    {
                        diagnostics.Add(new MRKCompileDiagnostic(id, "EMPTY_STEP", "节点ID不能为空"));
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(request.SubflowId))
                    {
                        if (!string.IsNullOrWhiteSpace(request.CapabilityId) ||
                            request.Inputs.Count != 0 ||
                            request.ExecutionGroup != null ||
                            request.RequiredResources.Count != 0 ||
                            request.DedicatedExecutorId != null)
                        {
                            diagnostics.Add(new MRKCompileDiagnostic(request.Id,
                                "UNSUPPORTED_SUBFLOW_BINDING",
                                "子流程不能隐式映射能力、资源或数据；须明确边界适配"));
                            continue;
                        }
                        List<MRKExecutionStep> nested=Expand(request.SubflowId,
                            catalog,resolver,active,diagnostics,ref expanded);
                        if (nested.Count == 0)
                        {
                            diagnostics.Add(new MRKCompileDiagnostic(request.Id,
                                "EMPTY_SUBFLOW", "不允许空子流程"));
                            continue;
                        }
                        MRKExecutionPlan nestedPlan = new MRKExecutionPlan(
                            request.SubflowId,nested);
                        string gateId = request.Id + "$entry";
                        MRKExecutionStep gate = new MRKExecutionStep(gateId,
                            request.Dependencies,
                            (ctx,ct) => Task.FromResult((object)true),
                            condition: request.Condition);
                        steps.Add(gate);
                        IList<MRKExecutionStep> inline = MRKExecutionPlan.InlineSubflow(
                            request.Id,nestedPlan,new[]{gateId});
                        steps.AddRange(inline);
                        HashSet<string> depended = new HashSet<string>(
                            nestedPlan.Steps.SelectMany(x => x.Dependencies),
                            StringComparer.Ordinal);
                        string[] exits = nestedPlan.Steps
                            .Where(x => !depended.Contains(x.Id))
                            .Select(x => request.Id + "." + x.Id).ToArray();
                        MRKExecutionStep barrier = new MRKExecutionStep(request.Id,
                            exits,(ctx,ct)=>Task.FromResult((object)true),
                            condition: ctx => { object admitted; return ctx.TryGetResult(gateId,
                                out admitted) && object.Equals(admitted, true); },
                            allowSkippedDependencies:true);
                        steps.Add(barrier);
                        expanded += 2;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(request.CapabilityId))
                        {
                            diagnostics.Add(new MRKCompileDiagnostic(request.Id,
                                "EMPTY_CAPABILITY", "未选择能力或子流程"));
                            continue;
                        }
                        MRKResolvedCapability ability;
                        if (!resolver.TryResolve(request.CapabilityId,out ability) || ability==null)
                        {
                            diagnostics.Add(new MRKCompileDiagnostic(request.Id,
                                "UNKNOWN_CAPABILITY","找不到能力："+request.CapabilityId));
                            continue;
                        }
                        steps.Add(new MRKExecutionStep(request.Id,request.Dependencies,
                            ability.ExecuteAsync,request.SerialKey,request.Policy,
                            request.RequiredResources,ability.CompensateAsync,
                            request.ExecutionGroup,request.Inputs,ability.Output,
                            request.Condition,request.AllowSkippedDependencies,
                            request.DedicatedExecutorId,ability.SynchronousSdk,
                            request.Priority,request.EstimatedPendingBytes));
                        expanded++;
                    }
                    if (expanded > MaxExpandedSteps)
                        throw new InvalidOperationException("编排展开节点超过" + MaxExpandedSteps);
                }
                return steps;
            }
            finally { active.Remove(id); }
        }
    }
}
