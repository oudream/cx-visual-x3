using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTStructuredObservationCases
    {
        [TCTTest("TCT.M1.OBS.001", "真实几何Owner结构化观察通过Worker保存", TCTTestAspect.Foundation,
            "Geometry / Observations", ObjectId = "FGERect2D")]
        public static void RealOwnerObservation(TCTTestContext test)
        {
            FGERect2D rect = new FGERect2D(0, 0, 100, 80);
            bool actual = rect.Contains(new FGEPoint2D(25, 30));
            test.AssertTrue(actual, "真实FGERect2D.Contains执行结果", true, actual);
            if (test.Attempt == null) throw new InvalidOperationException("This case needs real Attempt");
            byte[] payload = Encoding.UTF8.GetBytes("FGERect2D(0,0,100,80);Contains(25,30)=True");
            TCTObservationEnvelope record = test.ObserveStructured(
                "Geometry", "矩形Contains真实执行", "point=(25,30), inside=true",
                "FGERect2D", "Foundation.Geometry/FGERect2D.Contains",
                null, payload, "text/plain");
            test.AssertEqual(TCTObservationEnvelope.CurrentSchema, record.SchemaVersion, "观察版本");
            test.AssertEqual(test.AttemptId, record.AttemptId, "观察Attempt");
            test.AssertEqual(test.RunId, record.RunId, "观察Run");
            test.AssertEqual("FGERect2D", record.SourceObjectId, "真实Owner对象");
            TCTObservationEnvelope replay = TCTObservationCodec.Replay(
                test.Observations.Last().Evidence,
                new TCTTestArtifactStore(TCTTestArtifactStore.RunRoot(test.RunId)),
                test.AttemptId);
            test.AssertEqual(record.DefinitionAddress, replay.DefinitionAddress, "序列化后Owner定位");
            test.AssertEqual("text/plain", replay.PayloadRef.Mime, "负载类型");
        }

        [TCTTest("TCT.M1.OBS.002", "结构化证据跨Attempt、错误Schema与篡改拒绝",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTObservationCodec")]
        public static void ReplayMustFailClosed(TCTTestContext test)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                TCTTestArtifactStore store = new TCTTestArtifactStore(sandbox.Root);
                var record = new TCTObservationEnvelope(1, "run", "attempt-A",
                    "Runtime", "actual", "Actual", "safe", "MDSDataStack", "", "instance-0", null);
                byte[] data = TCTObservationCodec.Serialize(record);
                TCTArtifactRef artifact = store.Save(data, TCTObservationCodec.Mime, "attempt-A");
                test.AssertEqual("MDSDataStack",
                    TCTObservationCodec.Replay(artifact, store, "attempt-A").SourceObjectId, "正常回放");
                test.AssertThrows<InvalidDataException>(() =>
                    TCTObservationCodec.Replay(artifact, store, "attempt-B"), "拒绝跨Attempt");
                XElement bad = XElement.Parse(Encoding.UTF8.GetString(data));
                bad.SetAttributeValue("schema", 999);
                test.AssertThrows<NotSupportedException>(() =>
                    TCTObservationCodec.Deserialize(Encoding.UTF8.GetBytes(bad.ToString())),
                    "未知Schema拒绝解析");
                test.AssertThrows<XmlException>(() =>
                    TCTObservationCodec.Deserialize(Encoding.UTF8.GetBytes(
                        "<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///tmp/secret'>]>" +
                        "<Observation schema='1'/>")), "禁止DTD外部实体");
                string file = Path.Combine(sandbox.Root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                byte[] tampered = (byte[])data.Clone();
                tampered[tampered.Length - 2] ^= 1;
                File.WriteAllBytes(file, tampered);
                test.AssertThrows<InvalidDataException>(() =>
                    TCTObservationCodec.Replay(artifact, store, "attempt-A"), "真实SHA256篡改拒绝");
            }
        }

        [TCTTest("TCT.M1.OBS.003", "结构化Wire往返与历史文本观察兼容",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTWireCodec")]
        public static void WireAndHistory(TCTTestContext test)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                var attempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"), "TCT.LOCAL.OBS", 0, 42);
                var store = new TCTTestArtifactStore(sandbox.Root);
                var ctx = new TCTTestContext(default(System.Threading.CancellationToken), 42, attempt, store);
                ctx.AssertEqual(42, 42, "original assertion");
                ctx.Observe("Geometry", "legacy", "plain text");
                ctx.ObserveStructured("Runtime", "real", "state=Completed",
                    "MDSDataStack", "Flow/NodeA", "instance-1",
                    new byte[] { 1, 2, 3 }, "application/octet-stream");
                var descriptor = new TCTTestCaseDescriptor("TCT.LOCAL.OBS", "wire",
                    TCTTestAspect.Foundation, "TestFramework", "Unmapped", "Unmapped", "TCTWireCodec");
                var result = TCTTestCaseResult.FromTransport(descriptor, TCTTestStatus.Passed,
                    TimeSpan.Zero, "", ctx.Assertions, ctx.Observations, attempt, ctx.Artifacts);
                string path = sandbox.PathFor("wire.xml");
                TCTWireCodec.Write(path, result);
                TCTTestCaseResult restored = TCTWireCodec.Read(path, descriptor, attempt);
                test.AssertEqual(2, restored.Observations.Count, "两种Observation共存");
                test.AssertTrue(restored.Observations[0].Envelope == null,
                    "历史文字未伪装结构化", true, true);
                test.AssertEqual("MDSDataStack", restored.Observations[1].Envelope.SourceObjectId,
                    "Wire跨进程Owner对象保留");
                test.AssertEqual("instance-1", restored.Observations[1].Envelope.RuntimeInstanceId,
                    "运行实例未丢");
                IList<TCTObservationEnvelope> replay = TCTObservationReplay.Read(restored, store);
                test.AssertEqual(1, replay.Count, "只回放结构化证据");
                test.AssertEqual("Flow/NodeA", replay[0].DefinitionAddress, "Replay指向定义地址");
                XDocument tamperedWire = XDocument.Load(path);
                tamperedWire.Root.Element("Observations").Elements("Observation").Last()
                    .Value = "pretend state=Passed";
                tamperedWire.Save(path);
                TCTTestCaseResult forged = TCTWireCodec.Read(path, descriptor, attempt);
                test.AssertThrows<InvalidDataException>(() =>
                    TCTObservationReplay.Read(forged, store),
                    "Wire显示内容与已保存证据不符时禁止回放");
            }
        }
    }
}
