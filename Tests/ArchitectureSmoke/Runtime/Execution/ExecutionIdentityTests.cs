using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionIdentityTests
    {
        internal static void Run() { MetadataFollowsFlowAndSnapshotsIdentifyWaitingWork(); AggregateFlowProgressAndCancellationRequest(); }
        private static void Check(bool yes, string text)
        { if (!yes) throw new InvalidOperationException("身份快照：" + text); }
        private static T Await<T>(Task<T> task)
        {
            Task done = Task.WhenAny(task, Task.Delay(7000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(done, task)) throw new TimeoutException("身份测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void MetadataFollowsFlowAndSnapshotsIdentifyWaitingWork()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan = new MRKExecutionPlan("V4", new[]
            {
                new MRKExecutionStep("AI", new string[0], async (c, ct) =>
                {
                    Check(c.Metadata.CorrelationId == "req-101" &&
                        c.Metadata.PlanRevision == 45, "node metadata lost");
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return (object)1;
                })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            MRKFlowMetadata metadata = new MRKFlowMetadata(45, "product-01", "req-101", "session-A");
            MRKFlowHandle flow = engine.StartFlowWithMetadata(plan, metadata);
            Await(entered.Task);
            Check(flow.State == MRKFlowState.Running, "state not running");
            Check(SpinWait.SpinUntil(() =>
                flow.Context.StatesSnapshot()["AI"] == MRKStepState.Waiting, 3000),
                "work not waiting");
            MRKStepObservation item = flow.Context.ObservationsSnapshot()["AI"];
            Check(item.WorkId.HasValue && item.WorkId.Value != Guid.Empty &&
                item.StepId == "AI" && item.State == MRKStepState.Waiting &&
                item.ChangedUtc.Kind == DateTimeKind.Utc,
                "missing WorkId or state/time");
            release.TrySetResult(true);
            MRKFlowResult result = Await(flow.Completion);
            Check(result.State == MRKFlowState.Completed &&
                result.FlowId == flow.FlowId &&
                result.Metadata.BusinessKey == "product-01" &&
                result.Metadata.SessionId == "session-A", "flow identity changed");
            Task complete = engine.StopAsync();
            if (!object.ReferenceEquals(Task.WhenAny(complete, Task.Delay(7000))
                .GetAwaiter().GetResult(), complete)) throw new TimeoutException("identity stop hang");
            complete.GetAwaiter().GetResult();
        }
        private static void AggregateFlowProgressAndCancellationRequest()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan = new MRKExecutionPlan("monitor", new[]
            {
                new MRKExecutionStep("start", new string[0],
                    (ctx, ct) => Task.FromResult((object)1)),
                new MRKExecutionStep("await", new []{"start"}, async (ctx,ct) =>
                {
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite,ct).ConfigureAwait(false);
                    return (object)2;
                }),
                new MRKExecutionStep("end", new []{"await"},
                    (ctx,ct) => Task.FromResult((object)3))
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1,4));
            MRKFlowHandle handle = engine.StartFlowWithMetadata(plan,
                new MRKFlowMetadata(3,"sn01","req99","host"));
            Await(entered.Task);
            MRKFlowSnapshot snapshot = handle.GetSnapshot();
            Check(snapshot.FlowId == handle.FlowId && snapshot.Metadata.PlanRevision == 3 &&
                snapshot.TotalSteps == 3 && snapshot.CompletedSteps == 1 &&
                snapshot.WaitingSteps >= 1 && snapshot.EndedUtc == null,
                "Flow progress count or metadata wrong");
            handle.RequestCancel();
            Check(handle.GetSnapshot().CancelRequested,
                "requested cancellation not observable");
            MRKFlowResult result = Await(handle.Completion);
            MRKFlowSnapshot finalSnapshot = handle.GetSnapshot();
            Check(result.State == MRKFlowState.Cancelled &&
                finalSnapshot.State == MRKFlowState.Cancelled &&
                finalSnapshot.EndedUtc.HasValue &&
                finalSnapshot.EndedUtc.Value == result.CompletedUtc &&
                handle.GetSnapshot().EndedUtc.Value == finalSnapshot.EndedUtc.Value,
                "request and confirmed termination conflated");
            Task stop = engine.StopAsync();
            if (!object.ReferenceEquals(Task.WhenAny(stop,Task.Delay(7000)).GetAwaiter().GetResult(),
                stop)) throw new TimeoutException("flow monitor stop");
            stop.GetAwaiter().GetResult();
        }

    }
}
