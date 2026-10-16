using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionCompensationTests
    {
        internal static void Run()
        {
            ExplicitReverseCompensationAndIdempotence();
            ContinueReportingEvenWhenOneCompensationFails();
            SamePlanNameCannotSubstituteOriginalVersion();
            AnotherEngineCannotTakeOverCompensation();
        }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("流程补偿超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Check(bool ok, string text)
        { if (!ok) throw new InvalidOperationException("补偿测试：" + text); }
        private static void ExplicitReverseCompensationAndIdempotence()
        {
            List<string> order = new List<string>();
            MRKExecutionPlan plan = new MRKExecutionPlan("compensation", new[]
            {
                new MRKExecutionStep("A", new string[0], (ctx, ct) =>
                    Task.FromResult((object)1),
                    compensateAsync: (ctx, ct) => { order.Add("A"); return Task.FromResult(true); }),
                new MRKExecutionStep("B", new[] { "A" }, (ctx, ct) =>
                    Task.FromResult((object)2),
                    compensateAsync: (ctx, ct) => { order.Add("B"); return Task.FromResult(true); }),
                new MRKExecutionStep("C", new[] { "B" }, (ctx, ct) =>
                { throw new InvalidOperationException("C失败"); })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 5));
            MRKFlowHandle flow = engine.StartFlow(plan);
            Check(Await(flow.Completion).State == MRKFlowState.Failed && order.Count == 0,
                "失败后发生未经授权的自动补偿");
            MRKCompensationReport report = Await(engine.CompensateAsync(plan, flow));
            Check(report.Succeeded && order.Count == 2 &&
                order[0] == "B" && order[1] == "A" &&
                report.SucceededSteps[0] == "B", "补偿逆序错误");
            bool repeated = false;
            try { Await(engine.CompensateAsync(plan, flow)); }
            catch (InvalidOperationException) { repeated = true; }
            Check(repeated, "允许同一流程重复补偿");
            Await(engine.StopAsync());
        }
        private static void ContinueReportingEvenWhenOneCompensationFails()
        {
            int a = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("compensation-failure", new[]
            {
                new MRKExecutionStep("A", new string[0], (ctx, ct) =>
                    Task.FromResult((object)1),
                    compensateAsync: (ctx, ct) =>
                    { Interlocked.Increment(ref a); return Task.FromResult(true); }),
                new MRKExecutionStep("B", new[] { "A" }, (ctx, ct) =>
                    Task.FromResult((object)2),
                    compensateAsync: (ctx, ct) =>
                    { throw new InvalidOperationException("补偿B失败"); }),
                new MRKExecutionStep("C", new[] { "B" }, (ctx, ct) =>
                { throw new InvalidOperationException("C失败"); })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 4));
            MRKFlowHandle flow = engine.StartFlow(plan);
            Await(flow.Completion);
            MRKCompensationReport report = Await(engine.CompensateAsync(plan, flow));
            Check(!report.Succeeded && report.Failures.ContainsKey("B") && a == 1,
                "补偿失败被掩盖或导致其他合法补偿跳过");
            Await(engine.StopAsync());
        }

        private static void SamePlanNameCannotSubstituteOriginalVersion()
        {
            int legitimate = 0, substituted = 0;
            MRKExecutionPlan original = new MRKExecutionPlan("same-plan", new[]
            {
                new MRKExecutionStep("A", new string[0],
                    (ctx,ct) => Task.FromResult((object)1),
                    compensateAsync: (ctx,ct) =>
                    { Interlocked.Increment(ref legitimate); return Task.FromResult(true); }),
                new MRKExecutionStep("B", new[] { "A" },
                    (ctx,ct) => { throw new InvalidOperationException("failure"); })
            });
            MRKExecutionPlan altered = new MRKExecutionPlan("same-plan", new[]
            {
                new MRKExecutionStep("A", new string[0],
                    (ctx,ct) => Task.FromResult((object)1),
                    compensateAsync: (ctx,ct) =>
                    { Interlocked.Increment(ref substituted); return Task.FromResult(true); }),
                new MRKExecutionStep("B", new[] { "A" },
                    (ctx,ct) => { throw new InvalidOperationException("failure"); })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 4));
            MRKFlowHandle flow = engine.StartFlowWithMetadata(original, new MRKFlowMetadata(7));
            Check(Await(flow.Completion).State == MRKFlowState.Failed, "失败流程未结束");
            bool refused = false;
            try { Await(engine.CompensateAsync(altered, flow)); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused && substituted == 0 && legitimate == 0,
                "同名异版计划错误接管补偿并产生副作用");
            MRKCompensationReport report = Await(engine.CompensateAsync(original, flow));
            Check(report.Succeeded && legitimate == 1 && substituted == 0,
                "原始Plan的合法补偿未成功");
            Await(engine.StopAsync());
        }

        private static void AnotherEngineCannotTakeOverCompensation()
        {
            int compensated = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("owner-isolation", new[]
            {
                new MRKExecutionStep("A", new string[0],
                    (ctx, ct) => Task.FromResult((object)1),
                    requiredResources: new[] { "robot-axis" },
                    compensateAsync: (ctx, ct) =>
                    { Interlocked.Increment(ref compensated); return Task.FromResult(true); }),
                new MRKExecutionStep("B", new[] { "A" },
                    (ctx, ct) => { throw new InvalidOperationException("业务失败"); })
            });
            MRKExecutionEngine owner = new MRKExecutionEngine(new FCRWorkerOptions(1, 4));
            MRKExecutionEngine unrelated = new MRKExecutionEngine(new FCRWorkerOptions(1, 4));
            try
            {
                MRKFlowHandle flow = owner.StartFlow(plan);
                Check(Await(flow.Completion).State == MRKFlowState.Failed, "原始流程没有失败结算");
                bool rejected = false;
                try { Await(unrelated.CompensateAsync(plan, flow)); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && compensated == 0,
                    "另一Engine绕过原有资源仲裁执行了补偿动作");
                MRKCompensationReport report = Await(owner.CompensateAsync(plan, flow));
                Check(report.Succeeded && compensated == 1,
                    "归属校验错误消耗补偿名额或阻断了原始Engine");
            }
            finally
            {
                Await(unrelated.StopAsync());
                Await(owner.StopAsync());
            }
        }

        private static void Await(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("流程补偿超时");
            task.GetAwaiter().GetResult();
        }
    }
}
