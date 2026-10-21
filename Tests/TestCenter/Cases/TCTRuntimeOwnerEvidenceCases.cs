using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Xml.Linq;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTRuntimeOwnerEvidenceCases
    {
        private sealed class WaitResolver : IMRKCapabilityResolver
        {
            internal readonly TaskCompletionSource<object> Gate =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool TryResolve(string id, out MRKResolvedCapability resolved)
            {
                resolved = id == "tct.runtime.wait" ?
                    new MRKResolvedCapability((ctx, token) => Gate.Task) : null;
                return resolved != null;
            }
        }

        [TCTTest("TCT.M3.RUNTIME.001", "真实MRKSession容量、Step状态、Journal与恢复归档",
            TCTTestAspect.Mechanism, "Runtime / Session / Journal", ObjectId = "MRKRuntimeSession")]
        public static void RealSessionJournal(TCTTestContext test)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                MEMEngineeringSession engineering = MEMEngineering.Create("TCT MRK Session");
                try
                {
                    MEMPage page = engineering.AddPage("Runtime");
                    MOLControlBlock action = MOLControlBlock.Action("tct.runtime.wait");
                    MOLLogicCodec.Set(engineering, page.RootContent, new MOLLogicDefinition(action));
                    MEMProjectSnapshot snapshot = engineering.GetSnapshot();
                    WaitResolver resolver = new WaitResolver();
                    MOCBuildResult build = new MOCBuildService().Build(snapshot, page.RootContent.Id, resolver);
                    test.AssertTrue(build.Succeeded, "原生MOC构建成功", true, build.Succeeded);
                    if (!build.Succeeded) return;
                    string path = sandbox.PathFor("runtime-owner-journal.bin");
                    using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path))
                    {
                        MRKExecutionEngine engine = new MRKExecutionEngine(
                            new FCRWorkerOptions(2, 8), journal: journal,
                            groups: new[] { new MRKExecutionGroupDefinition("VirtualPool", 1, 2) });
                        MRKRuntimeSession runtime = new MRKRuntimeSession("TCT-Session",
                            engine, new MRKExecutionPlanSlot(build.Plan.Plan, snapshot.Revision), 1);
                        try
                        {
                            runtime.Start();
                            MRKFlowHandle handle = runtime.TryStartFlow(
                                businessKey: "software-test", correlationId: "flow-001");
                            test.AssertTrue(handle != null, "MRK实际创建Flow", true, handle != null);
                            MRKRuntimeSessionSnapshot active = runtime.Snapshot();
                            test.AssertEqual(1, active.ActiveFlows, "Flow执行中会话真实计数");
                            test.AssertTrue(runtime.TryStartFlow() == null, "容量已满不能伪造第二Flow", true, true);
                            test.AssertEqual(1L, runtime.Snapshot().Rejected, "MRKSession真实拒绝次数");
                            resolver.Gate.SetResult((object)7);
                            if (!handle.Completion.Wait(TimeSpan.FromSeconds(10)))
                                throw new TimeoutException("MRK Runtime Session did not finish");
                            MRKFlowResult result = handle.Completion.GetAwaiter().GetResult();
                            MRKRuntimeSessionSnapshot settled = runtime.Snapshot();
                            MRKFlowSnapshot flow = handle.GetSnapshot();
                            test.AssertEqual(MRKFlowState.Completed, result.State, "MRK原生完成状态");
                            test.AssertEqual(0, settled.ActiveFlows, "运行结束Session释放Flow");
                            IList<MRKJournalRecord> records = journal.ReadAll();
                            test.AssertTrue(records.Any(x => x.Kind == MRKJournalKind.FlowStarted) &&
                                records.Any(x => x.Kind == MRKJournalKind.FlowFinished) &&
                                records.Any(x => x.Kind == MRKJournalKind.StepTransition),
                                "真实Journal含开始、Step和终态", true, true);
                            test.AssertEqual(MRKRecoveryDisposition.Closed,
                                MRKJournalRecovery.Evaluate(result.FlowId, records).Disposition,
                                "Flow记录闭合无未知外部操作");
                            IDictionary<string, MRKStepObservation> observed =
                                handle.Context.ObservationsSnapshot();
                            byte[] evidence = TCTRuntimeOwnerEvidence.Serialize(build.Plan, active,
                                settled, flow, result, observed, records, engine.GroupSnapshot());
                            XDocument checkedEvidence = TCTRuntimeOwnerEvidence.Read(evidence);
                            test.AssertEqual(result.FlowId.ToString("D"),
                                (string)checkedEvidence.Root.Attribute("flow"), "Flow身份跨文件");
                            test.AssertEqual(records.Count,
                                checkedEvidence.Root.Element("Journal").Elements("Entry").Count(),
                                "真实Journal有序证据");
                            test.ObserveStructured("Runtime", "MRKSession / Journal / Step / Group",
                                "真实MRK Session容量拒绝、Flow状态、SourceMap定位及Journal回放",
                                snapshot.ProjectId.ToString("D"),
                                engineering.GetAddress(page.RootContent).ToString(),
                                result.FlowId.ToString("D"), evidence, TCTRuntimeOwnerEvidence.Mime);
                        }
                        finally
                        {
                            resolver.Gate.TrySetResult((object)7);
                            runtime.StopAsync(true).GetAwaiter().GetResult();
                            engine.StopAsync(true).GetAwaiter().GetResult();
                        }
                    }
                }
                finally { engineering.Close(true); }
            }
        }

        [TCTTest("TCT.M3.RUNTIME.002", "MRKSession归档拒绝跨Flow/乱序Journal与非法状态",
            TCTTestAspect.Mechanism, "Runtime / Session / Journal", ObjectId = "TCTRuntimeOwnerEvidence")]
        public static void RejectForgedRuntimeEvidence(TCTTestContext test)
        {
            Guid flow = Guid.NewGuid(), project = Guid.NewGuid(), block = Guid.NewGuid();
            XDocument original = new XDocument(new XElement("RuntimeOwner",
                new XAttribute("schema", 1), new XAttribute("project", project),
                new XAttribute("revision", 1), new XAttribute("plan", "plan"),
                new XAttribute("flow", flow), new XAttribute("state", "Completed"),
                new XAttribute("recovery", "Closed"),
                new XElement("Session", new XAttribute("id", "s"),
                    new XAttribute("activeState", "Ready"), new XAttribute("endState", "Ready"),
                    new XAttribute("active", 1), new XAttribute("settled", 0),
                    new XAttribute("rejected", 1)),
                new XElement("Flow", new XAttribute("createdUtc", DateTime.UtcNow.ToString("o")),
                    new XAttribute("endedUtc", DateTime.UtcNow.ToString("o")),
                    new XAttribute("total", 1), new XAttribute("completed", 1),
                    new XAttribute("waiting", 0), new XAttribute("failed", 0),
                    new XAttribute("skipped", 0), new XAttribute("cancelRequested", false)),
                new XElement("Observations", new XElement("Step", new XAttribute("id", "step"),
                    new XAttribute("block", block), new XAttribute("state", "Completed"),
                    new XAttribute("reason", ""), new XAttribute("invocation", ""),
                    new XAttribute("address", ""), new XAttribute("utc", DateTime.UtcNow.ToString("o")))),
                new XElement("Journal", new XElement("Entry", new XAttribute("seq", 1),
                    new XAttribute("kind", "StepTransition"), new XAttribute("flow", flow),
                    new XAttribute("plan", "plan"), new XAttribute("revision", 1),
                    new XAttribute("step", "step"), new XAttribute("state", "Completed"),
                    new XAttribute("correlation", ""), new XAttribute("operation", Guid.Empty),
                    new XAttribute("utc", DateTime.UtcNow.ToString("o")))),
                new XElement("Groups")));
            Func<XDocument, byte[]> bytes = d => Encoding.UTF8.GetBytes(d.ToString(SaveOptions.DisableFormatting));
            test.AssertEqual(1, TCTRuntimeOwnerEvidence.Read(bytes(original))
                .Root.Element("Journal").Elements("Entry").Count(), "合法只读数据可解析");
            XDocument tampered = new XDocument(original);
            tampered.Root.Element("Journal").Element("Entry").SetAttributeValue("flow", Guid.NewGuid());
            test.AssertThrows<InvalidDataException>(() => TCTRuntimeOwnerEvidence.Read(bytes(tampered)),
                "跨Flow Journal拒绝");
            tampered = new XDocument(original);
            tampered.Root.Element("Journal").Element("Entry").SetAttributeValue("seq", 0);
            test.AssertThrows<InvalidDataException>(() => TCTRuntimeOwnerEvidence.Read(bytes(tampered)),
                "Journal序号无效拒绝");
            tampered = new XDocument(original);
            tampered.Root.Element("Observations").Element("Step").SetAttributeValue("state", "FakePassed");
            test.AssertThrows<InvalidDataException>(() => TCTRuntimeOwnerEvidence.Read(bytes(tampered)),
                "伪造MRK状态拒绝");
            test.AssertThrows<System.Xml.XmlException>(() => TCTRuntimeOwnerEvidence.Read(
                Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY z SYSTEM 'file:///secret'>]>" +
                    original.ToString())), "禁用DTD实体");
        }
    }
}