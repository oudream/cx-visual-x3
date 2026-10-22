using System;
using System.Linq;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.UI;
using CxVisual.Mechanism.Connectivity;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTWorkspaceRegistrationCases
    {
        [TCTTest("TCT.UI.WORKSPACE.001", "四根注册树、重复与循环父节点拒绝、生产Virtual收发",
            TCTTestAspect.Integration, "TestCenter / Workspace / Manual", ObjectId = "TCTWorkspaceRegistry")]
        public static void RegistryAndVirtualOwner(TCTTestContext test)
        {
            TCTWorkspaceRegistry registry = TCTWorkspaceRegistry.CreateDefaults(
                id => { throw new InvalidOperationException("Factory must be lazy"); });
            test.AssertEqual(4, registry.Items.Count(x => x.Kind == TCTWorkspaceNodeKind.Root),
                "四个根是唯一注册描述符");
            test.AssertTrue(registry.Find("manual.channel").Factory != null,
                "人工通道有真实Frame工厂", true, true);
            test.AssertTrue(registry.Find("manual.protocol").Factory == null,
                "未接入规约不会创建虚假人工页面", true, true);
            bool duplicateRejected = false;
            try
            {
                registry.Register(new TCTWorkspaceDescriptor("manual.root", null, "重复",
                    "非法", TCTWorkspaceNodeKind.Root));
            }
            catch (InvalidOperationException) { duplicateRejected = true; }
            test.AssertTrue(duplicateRejected, "同ID重复注册必须拒绝", true, duplicateRejected);
            TCTWorkspaceRegistry missing = new TCTWorkspaceRegistry();
            missing.Register(new TCTWorkspaceDescriptor("orphan", "missing", "悬空",
                "", TCTWorkspaceNodeKind.Leaf));
            bool missingRejected = false;
            try { missing.Validate(); }
            catch (InvalidOperationException) { missingRejected = true; }
            test.AssertTrue(missingRejected, "父节点丢失必须拒绝", true, missingRejected);
            TCTWorkspaceRegistry cycle = new TCTWorkspaceRegistry();
            cycle.Register(new TCTWorkspaceDescriptor("a", "b", "A", "",
                TCTWorkspaceNodeKind.Group));
            cycle.Register(new TCTWorkspaceDescriptor("b", "a", "B", "",
                TCTWorkspaceNodeKind.Group));
            bool cycleRejected = false;
            try { cycle.Validate(); }
            catch (InvalidOperationException) { cycleRejected = true; }
            test.AssertTrue(cycleRejected, "父子环必须拒绝", true, cycleRejected);

            // Manual UI与本用例共用同一个生产Owner。Virtual不是TCP/PLC，也不写Manual Passed。
            MCCVirtualChannel[] pair = MCCVirtualChannel.CreatePair("workspace-tx", "workspace-rx");
            MPALengthFrameCodec codec = new MPALengthFrameCodec();
            pair[0].Open(); pair[1].Open();
            byte[] payload = new byte[] { 0x01, 0x02, 0xC5, 0x3A, 0x7F };
            pair[0].Send(codec.Encode(payload), "manual-positive", 2);
            MCCChannelFrame receive = pair[1].ReadFrames().Single(x => x.Direction == MCCFrameDirection.Rx);
            test.AssertTrue(receive.DecodedPayload.SequenceEqual(payload),
                "Owner虚拟通道分片解析还原原始负载", true, true);
            pair[1].Close();
            bool peerClosedRejected = false;
            try { pair[0].Send(codec.Encode(payload), "manual-negative"); }
            catch (System.IO.IOException) { peerClosedRejected = true; }
            test.AssertTrue(peerClosedRejected, "虚拟接收端关闭后不能伪造RX", true, peerClosedRejected);
            pair[0].Close();
        }
    }
}
