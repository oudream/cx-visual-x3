using System;
using System.Linq;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.UI;
using CxVisual.Mechanism.Connectivity;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTWorkspaceRegistrationCases
    {
        [TCTTest("TCT.UI.WORKSPACE.002",
            "创建实例独立ID、保存重开、计划选择隔离与删除后不误复用模块MHS配置",
            TCTTestAspect.Integration, "TestCenter / Workspace / Instances",
            ObjectId = "TCTCreatedWorkspaceStore")]
        public static void CreatedWorkspaceLifecycle(TCTTestContext test)
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "tct-workspaces-" + Guid.NewGuid().ToString("N"));
            string file = System.IO.Path.Combine(directory, "sessions.xml");
            try
            {
                TCTCreatedWorkspaceStore original = new TCTCreatedWorkspaceStore(file);
                TCTCreatedWorkspace a = original.Create(
                    "manual.module.connector", "Connector 01", "ModuleConnector01");
                TCTCreatedWorkspace b = original.Create(
                    "manual.module.connector", "Connector 02", "ModuleConnector02");
                TCTCreatedWorkspace plan = original.Create("auto.catalog", "回归计划01", "");
                original.UpdateCases(plan.Id, new[] { "TCT.M2.GEOMETRY.001", "M-OBJ-007" });
                test.AssertTrue(a.Id != b.Id && a.ConfigurationId != b.ConfigurationId,
                    "不同实例有独立身份与MHS Scope", true, true);

                TCTCreatedWorkspaceStore reopened = new TCTCreatedWorkspaceStore(file);
                reopened.Load(new[] { "manual.module.connector", "auto.catalog" });
                test.AssertTrue(reopened.Items.Count == 3 &&
                    reopened.Items.Single(x => x.Id == plan.Id).CaseIds.Length == 2,
                    "关闭重开后仍保留模块及计划CaseId", true, true);

                TCTWorkspaceRegistry registry = TCTWorkspaceRegistry.CreateDefaults(
                    id => { throw new InvalidOperationException("分类Factory不得隐式调用"); });
                foreach (TCTCreatedWorkspace item in reopened.Items)
                    registry.RegisterCreated(item, () => new System.Windows.Controls.UserControl());
                registry.Validate();
                test.AssertTrue(registry.Children("manual.module.connector").Count() == 2 &&
                    registry.Children("auto.catalog").Count() == 1,
                    "四根树之下的类型节点保有对应实例叶节点", true, true);
                test.AssertTrue(!registry.RemoveCreated("manual.module.connector") &&
                    registry.RemoveCreated(a.Id),
                    "不得删除模块类别但允许删除已创建实例", true, true);

                reopened.Delete(a.Id);
                TCTCreatedWorkspace replacement = reopened.Create(
                    "manual.module.connector", "Connector 01", "ModuleConnector01");
                test.AssertTrue(replacement.Id != a.Id &&
                    replacement.ConfigurationId != a.ConfigurationId,
                    "删除再创建不能复用旧MHS实例身份", true, true);

                bool rejected = false;
                try { new TCTCreatedWorkspaceStore(file).Load(new[] { "auto.catalog" }); }
                catch (System.IO.InvalidDataException) { rejected = true; }
                test.AssertTrue(rejected, "缺失模板注册时拒绝悄悄创建悬空实例", true, rejected);
            }
            finally
            {
                try { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true); }
                catch (System.IO.IOException) { }
            }
        }

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
