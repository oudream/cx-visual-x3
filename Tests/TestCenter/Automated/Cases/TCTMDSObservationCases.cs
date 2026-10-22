using System;
using System.Collections.Generic;
using System.Text;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTMDSObservationCases
    {
        [TCTTest("TCT.M1.OBS.004", "真实MDSDataStack输出提交转结构化证据",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataStack")]
        public static void DataStackResultEvidence(TCTTestContext test)
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                using (MDSDataFrame frame = stack.OpenFrame("M1-Node",
                    stack.RootScope.CreateChild("M1-Node"),
                    new Dictionary<string, object> { { "input", 6 } },
                    new Dictionary<string, Type> { { "answer", typeof(int) } }))
                {
                    int input = frame.Data.Get<int>("input");
                    frame.Data.Return("answer", input * 7);
                    frame.CommitOutputs();
                }
                int actual;
                bool exists = stack.TryReadOutput<int>("M1-Node", "answer", out actual);
                test.AssertTrue(exists, "生产Stack提交后输出可读", true, exists);
                test.AssertEqual(42, actual, "生产Stack返回42");
                byte[] bytes = Encoding.UTF8.GetBytes(
                    "Owner=MDSDataStack;Node=M1-Node;answer=" + actual);
                TCTObservationEnvelope observation = test.ObserveStructured(
                    "Runtime", "DataStack节点输出完成",
                    "M1-Node.answer=" + actual,
                    "MDSDataStack", "Flow/M1-Node/answer", null,
                    bytes, "text/plain");
                test.AssertEqual("MDSDataStack", observation.SourceObjectId, "源Owner唯一");
                test.AssertEqual("Flow/M1-Node/answer", observation.DefinitionAddress, "节点地址");
                test.AssertEqual("", observation.RuntimeInstanceId,
                    "生产Owner未提供实例ID时明确留空、不伪造");
            }
        }
    }
}
