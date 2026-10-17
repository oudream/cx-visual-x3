using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    // Real owner tests: Heap/Stack/Context behavior, not a test-only implementation.
    public static class TCTRuntimeDataCases
    {
        [TCTTest("TCT.MDS.001", "真实DataHeap复制移动与别名隔离",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataHeap")]
        public static void HeapMoveCopy(TCTTestContext test)
        {
            using (MDSDataHeap source = new MDSDataHeap())
            using (MDSDataHeap target = new MDSDataHeap())
            {
                byte[] input = { 1, 2, 3 };
                source.Set("pixels", input);
                input[0] = 99;
                test.AssertEqual((byte)1, source.Get<byte[]>("pixels")[0],
                    "Heap入库捕获快照，不共享调用方数组");
                byte[] read = source.Get<byte[]>("pixels");
                read[0] = 55;
                test.AssertEqual((byte)1, source.Get<byte[]>("pixels")[0],
                    "Get不能暴露可写别名");

                source.CopyTo("pixels", target, "copy");
                test.AssertTrue(source.Contains("pixels"), "Copy仍保留源", true, true);
                test.AssertEqual((byte)1, target.Get<byte[]>("copy")[0], "目标独立副本");
                test.AssertThrows<MDSDataException>(() =>
                    source.CopyTo("pixels", target, "copy"), "默认禁止同名覆盖");

                source.MoveTo("pixels", target, "moved");
                test.AssertTrue(!source.Contains("pixels"), "Move移出源Heap", false, false);
                test.AssertEqual((byte)1, target.Get<byte[]>("moved")[0], "Move完整承接");
                test.AssertEqual(2, target.Count, "两个独立目标名称");
                test.Observe("Runtime", "MDSDataHeap真实复制和转移",
                    "Copy preserves source; Move detaches source; aliases are isolated");
            }
        }

        [TCTTest("TCT.MDS.002", "DataStack输入只读、输出先提交后可见",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataStack")]
        public static void CommitIsAtomic(TCTTestContext test)
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                using (MDSDataFrame frame = stack.OpenFrame("node-a",
                    stack.RootScope.CreateChild("node-a"),
                    new Dictionary<string, object> { { "input", 7 } },
                    new Dictionary<string, Type> { { "answer", typeof(int) } }))
                {
                    test.AssertEqual(7, frame.Data.Get<int>("input"), "读取映射输入");
                    test.AssertThrows<MDSDataException>(() =>
                        frame.Data.Set("input", 8), "同名Input为只读");
                    frame.Data.Return("answer", 42);
                    int observed;
                    test.AssertTrue(!stack.TryReadOutput<int>("node-a", "answer", out observed),
                        "Return只是暂存，不能提前发布", false, false);
                    test.AssertEqual(42, (int)frame.CommitOutputs()["answer"], "Commit结果");
                    test.AssertTrue(stack.TryReadOutput<int>("node-a", "answer", out observed),
                        "Commit后才能读取输出", true, true);
                    test.AssertEqual(42, observed, "持久输出值");
                }
                test.AssertEqual(0, stack.ActiveFrames, "Frame释放后不留下活动调用");
                test.Observe("Runtime", "MDSDataStack节点输出提交",
                    "Pending 42 -> Commit -> Flow output node-a.answer=42");
            }
        }

        [TCTTest("TCT.MDS.003", "缺失输出禁止部分提交，取消不泄漏",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataFrame")]
        public static void MissingOutputAndAbort(TCTTestContext test)
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                using (MDSDataFrame frame = stack.OpenFrame("node-incomplete",
                    stack.RootScope.CreateChild("node-incomplete"),
                    new Dictionary<string, object>(),
                    new Dictionary<string, Type> {
                        { "first", typeof(int) }, { "second", typeof(int) } }))
                {
                    frame.Data.Return("first", 1);
                    test.AssertThrows<MDSDataException>(() =>
                        frame.CommitOutputs(), "声明两个输出但只返回一个，必须整体拒绝");
                    int value;
                    test.AssertTrue(!stack.TryReadOutput<int>("node-incomplete", "first", out value),
                        "失败不能泄漏一部分结果", false, false);
                    frame.AbortOutputs();
                    test.AssertThrows<InvalidOperationException>(() =>
                        frame.CommitOutputs(), "Abort后不能再次提交");
                }
                test.AssertEqual(0, stack.ActiveFrames, "Abort也必须完成Frame清理");
            }
        }

        [TCTTest("TCT.MDS.004", "Heap外部副作用阻止不安全重试",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataContext")]
        public static void HeapMutationBlocksRetry(TCTTestContext test)
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                using (MDSDataFrame frame = stack.OpenFrame("node-side-effect",
                    stack.RootScope.CreateChild("node-side-effect"),
                    new Dictionary<string, object>(),
                    new Dictionary<string, Type>()))
                {
                    frame.Data.Set("number", 5);
                    frame.Data.CopyToHeap("number", "shared");
                    test.AssertEqual(5, stack.FlowHeap.Get<int>("shared"), "真实Heap副作用");
                    test.AssertThrows<InvalidOperationException>(() =>
                        ((MDSDataContext)frame.Data).PrepareRetry(),
                        "写入Heap后不能自动当作幂等重试");
                    frame.AbortOutputs();
                }
                test.AssertEqual(0, stack.ActiveFrames, "有副作用仍能释放Frame");
            }
        }
    }
}
