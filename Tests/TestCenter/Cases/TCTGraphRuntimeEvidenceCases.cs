using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTGraphRuntimeEvidenceCases
    {
        private sealed class CpuResolver : IMRKCapabilityResolver
        {
            internal int Executed;
            public bool TryResolve(string id, out MRKResolvedCapability capability)
            {
                if (id != "tct.m3.capture" && id != "tct.m3.inspect")
                { capability = null; return false; }
                capability = new MRKResolvedCapability((context, cancellation) => {
                    Interlocked.Increment(ref Executed);
                    return Task.FromResult((object)id);
                });
                return true;
            }
        }

        [TCTTest("TCT.M3.GRAPHRUNTIME.001", "MOL/MOC/MRK真实SourceMap和Trace双Viewer证据",
            TCTTestAspect.Mechanism, "Graph / Runtime", ObjectId = "MOCCompiledPlan")]
        public static void RealMocMrk(TCTTestContext test)
        {
            MEMEngineeringSession session = MEMEngineering.Create("M3 Graph Runtime");
            try
            {
                MEMPage page = session.AddPage("Inspect");
                MOLControlBlock first = MOLControlBlock.Action("tct.m3.capture");
                MOLControlBlock second = MOLControlBlock.Action("tct.m3.inspect");
                MOLLogicDefinition definition = new MOLLogicDefinition(MOLControlBlock.Sequence(first, second));
                MOLLogicCodec.Set(session, page.RootContent, definition);
                MEMProjectSnapshot snapshot = session.GetSnapshot();
                CpuResolver resolver = new CpuResolver();
                MOCBuildResult build = new MOCBuildService().Build(snapshot, page.RootContent.Id, resolver);
                test.AssertTrue(build.Succeeded, "原生MOC构建成功", true, build.Succeeded);
                if (!build.Succeeded) return;
                test.AssertTrue(build.Plan.SourceMap.Find(first.Id).Count > 0 &&
                    build.Plan.SourceMap.Find(second.Id).Count > 0,
                    "真实Step可回到MOL Block", true, true);
                MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 8));
                try
                {
                    MRKFlowHandle handle = engine.StartFlow(build.Plan.Plan);
                    if (!handle.Completion.Wait(TimeSpan.FromSeconds(8)))
                    {
                        handle.RequestCancel();
                        throw new TimeoutException("MRK test Flow timed out");
                    }
                    MRKFlowResult actual = handle.Completion.GetAwaiter().GetResult();
                    MOCTraceView trace = build.Plan.ReadTrace(handle, 0);
                    test.AssertEqual(MRKFlowState.Completed, actual.State, "原生MRK执行完成");
                    test.AssertEqual(2, resolver.Executed, "执行原生Provider恰好两次");
                    test.AssertTrue(trace.Events.Any(x => x.Origin.BlockId == first.Id) &&
                        trace.Events.Any(x => x.Origin.BlockId == second.Id),
                        "Trace真实映射两个MOL Block", true, true);
                    byte[] bytes = TCTGraphRuntimeEvidence.Serialize(snapshot, definition,
                        build.Plan, actual, trace);
                    XDocument replay = TCTGraphRuntimeEvidence.Read(bytes);
                    test.AssertEqual(actual.FlowId.ToString("D"),
                        (string)replay.Root.Attribute("flowId"), "真实FlowId持久证据");
                    string owner = snapshot.ProjectId.ToString("D");
                    string address = session.GetAddress(page.RootContent).ToString();
                    string instance = actual.FlowId.ToString("D");
                    test.ObserveStructured("Graph", "MEM/MOL → MOC SourceMap",
                        "原生Definition/Plan/Step/Dependency/SourceMap；证据经过SHA256核验",
                        owner, address, instance, bytes, TCTGraphRuntimeEvidence.Mime);
                    test.ObserveStructured("Runtime", "MRK Flow → StepState / Timeline",
                        "同一FlowId的原生MRK执行状态与Trace，允许独立历史回放",
                        owner, address, instance, bytes, TCTGraphRuntimeEvidence.Mime);
                }
                finally { engine.StopAsync(true).GetAwaiter().GetResult(); }
            }
            finally { session.Close(true); }
        }

        [TCTTest("TCT.M3.GRAPHRUNTIME.003", "MOL V2类型化Port/Link到MOC/MRK结构化Viewer",
            TCTTestAspect.Mechanism, "Graph / Port", ObjectId = "MOLWiringDefinition")]
        public static void TypedMpcLinks(TCTTestContext test)
        {
            MEMEngineeringSession session = MEMEngineering.Create("M3 Typed Links");
            try
            {
                MEMPage page = session.AddPage("Typed Inspect");
                Guid first = Guid.Empty, second = Guid.Empty;
                MOCFlowBuilder.Edit(session, page.RootContent, builder => {
                    first = builder.Step("tct.m3.capture");
                    second = builder.Step("tct.m3.inspect");
                    builder.Connect(first, "Done", second, "Start", MPCPortKind.Control);
                    builder.Move(first, 40, 60);
                    builder.Move(second, 350, 60);
                });
                MEMProjectSnapshot snapshot = session.GetSnapshot();
                MOLWiringDefinition wiring = MOLWiringCodec.Get(snapshot.Find(page.RootContent.Id));
                test.AssertTrue(wiring != null && wiring.Links.Count == 1, "真实MOL V2编辑提交",
                    true, wiring != null && wiring.Links.Count == 1);
                CpuResolver resolver = new CpuResolver();
                MOCBuildResult build = new MOCBuildService().Build(snapshot, page.RootContent.Id, resolver);
                test.AssertTrue(build.Succeeded, "MPC连接经过原生MOC构建", true, build.Succeeded);
                if (!build.Succeeded) return;
                MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 8));
                try
                {
                    MRKFlowHandle handle = engine.StartFlow(build.Plan.Plan);
                    if (!handle.Completion.Wait(TimeSpan.FromSeconds(8)))
                    {
                        handle.RequestCancel();
                        throw new TimeoutException("MPC graph run timeout");
                    }
                    MRKFlowResult result = handle.Completion.GetAwaiter().GetResult();
                    test.AssertEqual(MRKFlowState.Completed, result.State, "原生MRK执行成功");
                    test.AssertEqual(2, resolver.Executed, "两个真实能力由连接约束依次执行");
                    byte[] raw = TCTGraphRuntimeEvidence.Serialize(snapshot, wiring, build.Plan,
                        result, build.Plan.ReadTrace(handle, 0));
                    XDocument evidence = TCTGraphRuntimeEvidence.Read(raw);
                    XElement link = evidence.Root.Element("Definition").Element("Link");
                    test.AssertEqual(wiring.Links[0].Id.ToString("D"),
                        (string)link.Attribute("id"), "证据保留原MOL Link Id");
                    test.AssertEqual(first.ToString("D"),
                        (string)link.Attribute("sourceBlock"), "连接源BlockId不被编译Step替换");
                    test.AssertEqual(second.ToString("D"),
                        (string)link.Attribute("targetBlock"), "连接目标BlockId保留");
                    test.AssertTrue(evidence.Root.Element("Definition").Elements("Block")
                        .SelectMany(x => x.Elements("Port")).Any(), "真实MPC端口采样", true, true);
                    string owner = snapshot.ProjectId.ToString("D");
                    string address = session.GetAddress(page.RootContent).ToString();
                    string flow = result.FlowId.ToString("D");
                    test.ObserveStructured("Graph", "MOL V2 Port / Connection / MOC SourceMap",
                        "真实持久化MPC控制端口和Link、MOC编译Step及MRK Trace",
                        owner, address, flow, raw, TCTGraphRuntimeEvidence.Mime);
                    test.ObserveStructured("Runtime", "MPC驱动的MRK执行Timeline",
                        "真实Flow及依赖，不把工程连线和编译依赖混成一套图",
                        owner, address, flow, raw, TCTGraphRuntimeEvidence.Mime);
                }
                finally { engine.StopAsync(true).GetAwaiter().GetResult(); }
            }
            finally { session.Close(true); }
        }

        [TCTTest("TCT.M3.GRAPHRUNTIME.002", "GraphRuntime未知Schema及SourceMap篡改拒绝",
            TCTTestAspect.Mechanism, "Graph / Runtime", ObjectId = "TCTGraphRuntimeEvidence")]
        public static void RejectCorruptGraphRuntime(TCTTestContext test)
        {
            string block = Guid.NewGuid().ToString("D");
            XDocument original = new XDocument(new XElement("GraphRuntime",
                new XAttribute("schema", 1),
                new XAttribute("projectId", Guid.NewGuid()),
                new XAttribute("revision", 1),
                new XAttribute("planId", "p"),
                new XAttribute("flowId", Guid.NewGuid()),
                new XAttribute("flowState", "Completed"),
                new XElement("Definition", new XElement("Block", new XAttribute("id", block),
                    new XAttribute("kind", "Action"), new XAttribute("capability", "test"))),
                new XElement("Plan", new XElement("Step", new XAttribute("id", "a"))),
                new XElement("SourceMap", new XElement("Map", new XAttribute("step", "a"),
                    new XAttribute("block", block), new XAttribute("address", ""),
                    new XAttribute("role", "Action"))),
                new XElement("Execution", new XElement("State", new XAttribute("step", "a"),
                    new XAttribute("value", "Completed"))),
                new XElement("Timeline", new XAttribute("lost", 0), new XElement("Event",
                    new XAttribute("sequence", 1), new XAttribute("step", "a"),
                    new XAttribute("block", block), new XAttribute("state", "Completed"),
                    new XAttribute("utc", DateTime.UtcNow.ToString("o"))))));
            Func<XDocument, byte[]> bytes = x => Encoding.UTF8.GetBytes(x.ToString(SaveOptions.DisableFormatting));
            test.AssertEqual(1, TCTGraphRuntimeEvidence.Read(bytes(original))
                .Root.Element("Timeline").Elements("Event").Count(), "合法Schema读取");
            XDocument bad = new XDocument(original);
            bad.Root.Element("SourceMap").Element("Map").SetAttributeValue("block", Guid.NewGuid());
            test.AssertThrows<InvalidDataException>(() => TCTGraphRuntimeEvidence.Read(bytes(bad)),
                "不属于MOL定义的SourceMap不得渲染");
            bad = new XDocument(original);
            bad.Root.SetAttributeValue("schema", 999);
            test.AssertThrows<InvalidDataException>(() => TCTGraphRuntimeEvidence.Read(bytes(bad)),
                "未知Schema拒绝");
            bad = new XDocument(original);
            bad.Root.Element("Timeline").Element("Event").SetAttributeValue("step", "foreign");
            test.AssertThrows<InvalidDataException>(() => TCTGraphRuntimeEvidence.Read(bytes(bad)),
                "外来MRK Step无映射拒绝");
            test.AssertThrows<System.Xml.XmlException>(() => TCTGraphRuntimeEvidence.Read(
                Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY z SYSTEM 'file:///secret'>]>" + original.ToString())),
                "DTD实体拒绝");
        }
    }
}