using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Visual;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Cases
{
    // V4.2: Tests existing owner APIs directly; no Graph/DataSpace mock implementation.
    public static class TCTMechanismActualCases
    {
        [FCMConfigSection("TCT.Mechanism.Config", SchemaVersion = 1)]
        public sealed class TCTMachineConfiguration
        {
            [FCMConfigItem("Exposure", MemberId = "Exposure", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
            [Range(1, 1000)]
            public int ExposureMs { get; set; } = 80;
        }

        [TCTTest("TCT.MVIS.001", "Visual投影与命中必须跟随Document事务", TCTTestAspect.Mechanism,
            "Visual / Projection", ObjectId = "CxSceneProjection")]
        public static void ProjectionAndHitTest(TCTTestContext context)
        {
            MEMEngineeringSession document = TCTRealCases.CreateOrganizationSample();
            IList<CxSceneItem> scene = CxSceneProjection.Project(document);
            int ownedElements = document.Project.Pages
                .SelectMany(page => page.DescendantsAndSelf()).OfType<MRCElement>().Count();
            MEMContent content = document.Project.Pages[0].RootContent;
            MEMSymbol symbol = document.Project.Symbols[0];
            int referencedElements = content.References.Count *
                symbol.RootGroup.DescendantsAndSelf().OfType<MRCElement>().Count();
            context.AssertEqual(ownedElements + referencedElements, scene.Count,
                "真实场景包含页面拥有元素及Symbol引用展开实例");
            CxSceneItem instance = scene.Single(x => x.Name.StartsWith("Symbol Instance / ",
                StringComparison.Ordinal));
            context.AssertTrue(instance.Address != null &&
                instance.Address.InstancePath.Count == 1 &&
                instance.Address.InstancePath[0] == content.References[0].Id &&
                instance.Address.ObjectId == symbol.RootGroup.Children[0].Id,
                "Symbol实例选择身份必须含使用路径", true,
                instance.Address == null ? "Missing" : instance.Address.ToString());
            Guid detector = scene.First(x => x.Name == "Detector").Id;
            Guid? hit = CxSceneProjection.HitTest(scene, new CxPoint2D(60, 75));
            context.AssertEqual((Guid?)detector, hit, "原位置命中真实元素");

            CxVisualEditor editor = new CxVisualEditor(document);
            MTCEditResult<bool> moved = editor.Move(detector, 500, 500);
            context.AssertTrue(moved.Success, "工程Move已提交", true, moved.Success);
            IList<CxSceneItem> updated = editor.Scene;
            context.AssertEqual((Guid?)null,
                CxSceneProjection.HitTest(updated, new CxPoint2D(60, 75)), "旧位置不再命中");
            context.AssertEqual((Guid?)detector,
                CxSceneProjection.HitTest(updated, new CxPoint2D(510, 510)), "新位置命中");
            context.Observe("Geometry", "真实Visual投影",
                "DocumentRevision=" + document.Revision + "; Detector moved (50,65) => (500,500)");
            context.Observe("ObjectTree", "场景对象归属",
                "Ownership: Project → Page → Content → Group → Element; " +
                "Reference: Content ↪ Symbol → RootGroup → Element; " +
                "InstancePath=" + instance.Address);
        }

        [TCTTest("TCT.MVIS.002", "Visual编辑撤销重做与工程Revision保持一致", TCTTestAspect.Mechanism,
            "Visual / Editing", ObjectId = "CxVisualEditor")]
        public static void UndoRedoAndRevision(TCTTestContext context)
        {
            MEMEngineeringSession document = TCTRealCases.CreateOrganizationSample();
            CxVisualEditor editor = new CxVisualEditor(document);
            int initial = editor.Scene.Count;
            int initialRevision = document.Revision;
            MTCEditResult<bool> add = editor.AddRectangle("TCT-Rectangle", 450, 20, 30, 40);
            context.AssertTrue(add.Success, "增加真正图元成功", true, add.Success);
            context.AssertEqual(initial + 1, editor.Scene.Count, "添加后场景数量");
            context.AssertTrue(editor.CanUndo, "可撤销", true, editor.CanUndo);
            context.AssertTrue(editor.Undo().Success, "Undo事务", true, true);
            context.AssertEqual(initial, editor.Scene.Count, "Undo恢复数量");
            context.AssertTrue(editor.CanRedo, "可重做", true, editor.CanRedo);
            context.AssertTrue(editor.Redo().Success, "Redo事务", true, true);
            context.AssertEqual(initial + 1, editor.Scene.Count, "Redo还原数量");
            context.AssertEqual(initialRevision + 3, document.Revision, "三次事务推进Revision");
            context.Observe("ObjectTree", "Visual编辑生命周期",
                "Add → Undo → Redo; Revision=" + document.Revision + "; SceneItems=" + editor.Scene.Count);
        }

        [TCTTest("TCT.MDATA.001", "Mechanism.Data配置保存与任务生效分离", TCTTestAspect.Mechanism,
            "Data / Configuration", ObjectId = "MHSConfigSection")]
        public static void ConfigSnapshotAndBoundary(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                MHSConfigService service = new MHSConfigService(sandbox.PathFor("config"));
                MHSConfigSection<TCTMachineConfiguration> section =
                    service.ForMachine("machine-a").Open<TCTMachineConfiguration>();
                context.AssertEqual(80, section.Get().ExposureMs, "有效默认值");
                using (MHSConfigSnapshot<TCTMachineConfiguration> initial = section.CaptureForTask("job-before"))
                {
                    MHSConfigCommitResult commit = section.SetValueAsync(
                        x => x.ExposureMs, 120, "TCT-test", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    context.AssertTrue(commit.Success, "配置持久提交", true, commit.Success);
                    context.AssertTrue(section.HasPendingApply, "需要任务边界生效", true, section.HasPendingApply);
                    context.AssertEqual(80, section.Get().ExposureMs, "有效值仍为旧配置");
                    context.AssertEqual(120, section.GetLatestSaved().ExposureMs, "保存值已经改变");
                    context.AssertTrue(section.ApplyPendingAtTaskBoundary(), "任务边界生效", true, true);
                    context.AssertEqual(120, section.Get().ExposureMs, "边界后生效");
                    context.AssertEqual(80, initial.Get().ExposureMs, "旧任务快照保持不变");
                }
                using (MHSConfigSnapshot<TCTMachineConfiguration> next = section.CaptureForTask("job-after"))
                {
                    context.AssertEqual(120, next.Get().ExposureMs, "新任务获得新版本");
                }
                context.Observe("Runtime", "配置任务版本语义",
                    "SavedVersion=" + section.SavedVersion + "; EffectiveVersion=" +
                    section.EffectiveVersion + "; ExposureMs=120");
            }
        }

        [TCTTest("TCT.MDATA.002", "非法配置不得破坏保存与有效版本", TCTTestAspect.Mechanism,
            "Data / Configuration", ObjectId = "MHSConfigEdit")]
        public static void InvalidConfigDoesNotCommit(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                MHSConfigService service = new MHSConfigService(sandbox.PathFor("config"));
                MHSConfigSection<TCTMachineConfiguration> section =
                    service.ForMachine("machine-b").Open<TCTMachineConfiguration>();
                string original = section.SavedVersion;
                using (MHSConfigEdit<TCTMachineConfiguration> edit = section.BeginEdit("invalid-range"))
                {
                    edit.WorkingCopy.ExposureMs = 0;
                    MHSConfigPreview preview = edit.Preview();
                    context.AssertTrue(!preview.Validation.IsValid, "Preview阻止非法范围", false,
                        preview.Validation.IsValid);
                    MHSConfigCommitResult commit = edit.CommitAsync(CancellationToken.None)
                        .GetAwaiter().GetResult();
                    context.AssertTrue(!commit.Success, "Commit拒绝非法范围", false, commit.Success);
                }
                context.AssertEqual(original, section.SavedVersion, "保存版本不改变");
                context.AssertEqual(80, section.Get().ExposureMs, "有效版本不改变");
                context.AssertTrue(!section.HasPendingApply, "不产生虚假待生效状态", false,
                    section.HasPendingApply);
                context.Observe("ObjectTree", "配置校验回滚", "Invalid Range => ValidationError; no revision increase");
            }
        }

        [TCTTest("TCT.MDATA.003", "配置Machine作用域不能串用", TCTTestAspect.Mechanism,
            "Data / Configuration", ObjectId = "MHSConfigService")]
        public static void MachineScopesAreIsolated(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                MHSConfigService service = new MHSConfigService(sandbox.PathFor("config"));
                MHSConfigSection<TCTMachineConfiguration> left =
                    service.ForMachine("line-1").Open<TCTMachineConfiguration>();
                MHSConfigSection<TCTMachineConfiguration> right =
                    service.ForMachine("line-2").Open<TCTMachineConfiguration>();
                MHSConfigCommitResult result = left.SetValueAsync(
                    x => x.ExposureMs, 250, "left-only", CancellationToken.None)
                    .GetAwaiter().GetResult();
                context.AssertTrue(result.Success, "左机保存成功", true, result.Success);
                context.AssertEqual(80, right.Get().ExposureMs, "右机有效配置未改变");
                context.AssertEqual(80, right.GetLatestSaved().ExposureMs, "右机保存配置未改变");
                context.AssertTrue(left.ApplyPendingAtTaskBoundary(), "左机单独生效", true, true);
                context.AssertEqual(250, left.Get().ExposureMs, "左机独立生效");
                context.AssertEqual(80, right.Get().ExposureMs, "右机不受影响");
            }
        }
    }
}