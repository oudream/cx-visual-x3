using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Runtime.Host
{
    // 无外设的模拟能力与流程使用代码优先Blueprint构建；
    // 正式执行依然必须经过授权Publisher、PlanSlot和Session。
    internal sealed class MRHSimulationPublishPolicy : IMRKPlanPublicationPolicy
    {
        public bool Authorize(string principal, MRKPlanPublishAction action,
            MRKExecutionPlan plan, long revision)
        { return principal == "local-simulation"; }
        public bool IsSafeToActivate(MRKExecutionPlan plan, long revision)
        { return true; } // 没有外设，仅模拟内存算法；生产Host必须替换此策略。
    }

    internal sealed class MRHSimulationAccessPolicy : IMRKRuntimeAccessPolicy
    {
        public bool IsAuthorized(string principal, MRKRuntimeAccessAction action,
            string sessionId)
        {
            return principal == "local-simulation" &&
                (action == MRKRuntimeAccessAction.Observe ||
                 action == MRKRuntimeAccessAction.Start ||
                 action == MRKRuntimeAccessAction.Stop ||
                 action == MRKRuntimeAccessAction.Cancel);
        }
    }

    /// <summary>
    /// 单进程RunHost：独占本地Journal，启动前扫描悬而未决的运行；
    /// 发现未知Flow则拒绝自动运行，交给正式恢复确认工作流。
    /// </summary>
    internal sealed class MRHUnifiedRuntimeHost : IDisposable
    {
        private readonly MRKFileExecutionJournal _journal;
        private readonly MRKExecutionEngine _engine;
        private readonly MRKRuntimeSession _session;
        private readonly MRKRuntimeAccessGateway _access;
        private readonly object _flowGate = new object();
        private readonly Dictionary<Guid, MRKRuntimeFlowTicket> _tickets =
            new Dictionary<Guid, MRKRuntimeFlowTicket>();
        private bool _stopped;
        private MRHUnifiedRuntimeHost(MRKFileExecutionJournal journal,
            MRKExecutionEngine engine, MRKRuntimeSession session,
            MRKRuntimeAccessGateway access)
        { _journal = journal; _engine = engine; _session = session; _access = access; }

        internal static MRHUnifiedRuntimeHost Open(string journalPath)
        {
            if (string.IsNullOrWhiteSpace(journalPath))
                throw new ArgumentException("Journal路径不能为空", "journalPath");
            string path = Path.GetFullPath(journalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path);
            try
            {
                IList<MRKJournalRecord> history = journal.ReadAll();
                foreach (MRKRecoveryDecision decision in
                    MRKJournalRecovery.EvaluateAll(history))
                    if (decision.Disposition != MRKRecoveryDisposition.Closed)
                        throw new InvalidOperationException(
                            "宿主启动隔离：上次Flow未安全结案 " + decision.FlowId +
                            "，禁止自动重放和新任务接管");
                MRKExecutionPlan baseline = new MRKExecutionPlan("host-bootstrap", new[]
                {
                    new MRKExecutionStep("bootstrap",new string[0],
                        (ctx,ct)=>Task.FromResult((object)0))
                });
                MRKExecutionPlanSlot slot =
                    new MRKExecutionPlanSlot(baseline, 1, requireAuthorizedPublication:true);
                MRKExecutionPlanPublisher publisher =
                    new MRKExecutionPlanPublisher(slot, baseline, 1,
                        new MRHSimulationPublishPolicy());
                MRKCodeRuntimeBlueprint blueprint = new MRKCodeRuntimeBuilder()
                    .Capability<int>("Prepare",(ctx,ct)=>Task.FromResult(2))
                    .Capability<int>("Calculate",async (ctx,ct)=>
                    {
                        object value;
                        if (!ctx.TryGetResult("prepare", out value))
                            throw new InvalidOperationException("子流程输入没有传递");
                        await Task.Delay(20,ct).ConfigureAwait(false);
                        return (int)value + 3;
                    })
                    .Flow("calculate-pipeline",flow => flow
                        .Step("prepare","Prepare")
                        .Step("calculate","Calculate","prepare"))
                    .Flow("host-simulation",flow => flow
                        .Subflow("process","calculate-pipeline"))
                    .Freeze();
                MRKPlanPublicationReceipt receipt = publisher.CompileAndStageBlueprint(
                    "local-simulation", "host-simulation", blueprint, 2);
                if (!receipt.Accepted || !publisher.TryActivate("local-simulation"))
                    throw new InvalidOperationException("Host模拟计划发布未通过安全验证");
                MRKExecutionEngine engine =
                    new MRKExecutionEngine(new FCRWorkerOptions(2,16), journal:journal);
                MRKRuntimeSession session =
                    new MRKRuntimeSession("host-simulation",engine,slot,maxInFlight:4);
                session.Start();
                MRKRuntimeAccessGateway gateway=new MRKRuntimeAccessGateway(
                    engine,session,new MRHSimulationAccessPolicy());
                return new MRHUnifiedRuntimeHost(journal,engine,session,gateway);
            }
            catch { journal.Dispose(); throw; }
        }

        internal MRKRuntimeFlowTicket Start()
        {
            if (_stopped) throw new InvalidOperationException("Host已停止");
            MRKRuntimeFlowTicket flow = _access.TryStart("local-simulation",
                correlationId: Guid.NewGuid().ToString("N"));
            if (flow == null) throw new InvalidOperationException("Session拒绝新Flow");
            lock (_flowGate) _tickets.Add(flow.FlowId, flow);
            Task cleanup = flow.Completion.ContinueWith(completed =>
            {
                lock (_flowGate) _tickets.Remove(flow.FlowId);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return flow;
        }
        internal MRKRuntimeSessionSnapshot Snapshot()
        { return _access.Observe("local-simulation"); }
        internal IList<MRKFlowSnapshot> ActiveFlowSnapshots()
        {
            lock (_flowGate)
                return _tickets.Values.Select(x => x.GetSnapshot()).ToArray();
        }
        internal bool TryCancel(Guid flowId)
        {
            MRKRuntimeFlowTicket ticket;
            lock (_flowGate)
                if (!_tickets.TryGetValue(flowId, out ticket)) return false;
            return _access.RequestCancel("local-simulation", ticket);
        }
        internal async Task StopAsync(bool cancel = false)
        {
            if (_stopped) return;
            _stopped = true;
            await _access.StopSessionAsync("local-simulation",cancel).ConfigureAwait(false);
            await _engine.StopAsync(cancel).ConfigureAwait(false);
        }
        public void Dispose()
        {
            try { StopAsync(true).GetAwaiter().GetResult(); }
            finally { _journal.Dispose(); }
        }

        internal static int RunOne(string journalPath)
        {
            using (MRHUnifiedRuntimeHost host = Open(journalPath))
            {
                MRKRuntimeFlowTicket flow = host.Start();
                MRKFlowResult result = flow.Completion.GetAwaiter().GetResult();
                host.StopAsync().GetAwaiter().GetResult();
                Console.WriteLine("Flow=" + result.FlowId + " PlanRevision=" +
                    result.Metadata.PlanRevision + " State=" + result.State);
                if (result.State != MRKFlowState.Completed ||
                    (int)result.Results["process.calculate"] != 5)
                    return 1;
                Console.WriteLine("RuntimeHost PASS code blueprint / authorized publish / nested plan / session / journal");
                return 0;
            }
        }

        internal static int Recover(string journalPath)
        {
            if (!File.Exists(journalPath))
            {
                Console.WriteLine("NoRecord: Journal文件不存在");
                return 2;
            }
            using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(journalPath))
            {
                IList<MRKJournalRecord> records = journal.ReadAll();
                int unresolved = 0;
                foreach (MRKRecoveryDecision decision in
                    MRKJournalRecovery.EvaluateAll(records))
                {
                    Console.WriteLine("Flow=" + decision.FlowId + " Recovery=" +
                        decision.Disposition + " Reason=" + decision.Reason);
                    if (decision.Disposition != MRKRecoveryDisposition.Closed) unresolved++;
                }
                if (unresolved != 0)
                {
                    Console.Error.WriteLine("Recovery blocked: " + unresolved +
                        "个未确认Flow，禁止自动重放");
                    return 3;
                }
                Console.WriteLine("RuntimeHost PASS recovery scan");
                return 0;
            }
        }

        internal static int Serve(string journalPath)
        {
            using (MRHUnifiedRuntimeHost host = Open(journalPath))
            {
                Console.WriteLine("RuntimeHost ready (simulation). Commands: run, status, cancel <FlowId>, stop, stop cancel");
                string command;
                while ((command = Console.ReadLine()) != null)
                {
                    command = command.Trim().ToLowerInvariant();
                    if (command == "stop" || command == "stop cancel")
                    {
                        host.StopAsync(command == "stop cancel").GetAwaiter().GetResult();
                        Console.WriteLine("RuntimeHost stopped cleanly");
                        return 0;
                    }
                    if (command == "run")
                    {
                        MRKRuntimeFlowTicket flow = host.Start();
                        Console.WriteLine("Accepted Flow=" + flow.FlowId);
                    }
                    else if (command == "status")
                    {
                        MRKRuntimeSessionSnapshot state = host.Snapshot();
                        Console.WriteLine("Host=" + state.State + " Active=" +
                            state.ActiveFlows + " PlanRevision=" + state.PlanRevision);
                        foreach (MRKFlowSnapshot flow in host.ActiveFlowSnapshots())
                            Console.WriteLine("Flow=" + flow.FlowId + " State=" + flow.State +
                                " CancelRequested=" + flow.CancelRequested);
                    }
                    else if (command.StartsWith("cancel ", StringComparison.Ordinal))
                    {
                        Guid flowId;
                        if (!Guid.TryParse(command.Substring(7).Trim(), out flowId))
                            Console.WriteLine("Invalid FlowId");
                        else
                            Console.WriteLine(host.TryCancel(flowId)
                                ? "Cancel requested for " + flowId
                                : "Flow not active or already settled: " + flowId);
                    }
                    else Console.WriteLine("Unknown command");
                }
                host.StopAsync().GetAwaiter().GetResult();
                Console.WriteLine("RuntimeHost stopped cleanly");
                return 0;
            }
        }
    }
}
