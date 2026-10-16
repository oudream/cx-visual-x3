using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Tests.ArchitectureSmoke
{
    [FCMConfigSection("Tests.ConfigDynamicCollections")]
    public sealed class ConfigDynamicCollections
    {
        [FCMConfigItem("探测器动态键", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public Dictionary<string, V13DetectorSample> Detectors { get; set; } =
            new Dictionary<string, V13DetectorSample>();
        [FCMConfigItem("工艺段列表", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public List<V13DetectorSample> Segments { get; set; } = new List<V13DetectorSample>();
        [FCMConfigItem("工艺标签", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public HashSet<string> Tags { get; set; } = new HashSet<string>();
        public Dictionary<string, List<V13DetectorSample>> Groups { get; set; } =
            new Dictionary<string, List<V13DetectorSample>>();
    }

    /// <summary>
    /// 四个可运行开发者案例，专门验证 API 工作量/副作用/并发，而非只构造一个“看似可用”类。
    /// 示例均复用 V1.3 模型与真实文件存储。
    /// </summary>
    internal static class ConfigDeveloperExperienceTests
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxConfig-DX-" + Guid.NewGuid().ToString("N"));
            try
            {
                OrdinaryDeveloper(root);
                XRayMultipleDetectors(root);
                CtLongTask(root);
                AtomicProductRecipe(root);
                SecurityAndCatalog(root);
                PreparedReadAndConcurrency(root);
                DynamicCollectionEditing(root);
                Console.WriteLine("PASS config developer experience: basic/XRay/CT/recipe/security/performance");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void OrdinaryDeveloper(string root)
        {
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "ordinary"));
            MHSConfigSection<V13ConfigSample> cfg =
                service.ForMachine("Line-A").Open<V13ConfigSample>();
            Check(cfg.ExplainState(x => x.ExposureMs).Presence == FCMConfigValuePresence.Defaulted,
                "original factory field is defaulted");
            int changes = 0;
            using (IDisposable watch = cfg.Watch(x => x.ExposureMs, c =>
            {
                Check(c.NewValue == 120 && c.OldValue == 80 &&
                    c.DefaultValue == 80, "strongly typed before/after/default");
                Interlocked.Increment(ref changes);
            }))
            {
                // Edit an unrelated field; untouched ExposureMs must not stop being Defaulted.
                Check(cfg.UpdateAsync(x => x.Note = "Operator note", "变更备注",
                    CancellationToken.None).GetAwaiter().GetResult().Success,
                    "one expression updates config without custom transaction management");
                Check(cfg.ExplainState(x => x.ExposureMs).Presence == FCMConfigValuePresence.Defaulted,
                    "field source is not determined by whole-document revision");
                Check(cfg.UpdateAsync(x => x.ExposureMs = 120, "调整曝光",
                    CancellationToken.None).GetAwaiter().GetResult().Success &&
                    cfg.HasPendingApply && changes == 0, "saved changes are not effective");
                Check(cfg.ApplyPendingAtTaskBoundary(), "activate staged exposure");
                Check(cfg.ExplainState(x => x.ExposureMs).Presence == FCMConfigValuePresence.Present &&
                    cfg.Explain(x => x.ExposureMs).DefaultValue == 80 &&
                    changes == 1, "explicitly edited field becomes Present");
                Check(cfg.GetLatestSaved().ExposureMs == cfg.Value(x => x.ExposureMs),
                    "saved and effective agree after activation");
            }
            MHSConfigSection<V13ConfigSample> restarted =
                new MHSConfigService(Path.Combine(root, "ordinary"))
                    .ForMachine("Line-A").Open<V13ConfigSample>();
            Check(restarted.ExplainState(x => x.ExposureMs).Presence == FCMConfigValuePresence.Present,
                "field provenance remains correct after restart");
        }

        private static void XRayMultipleDetectors(string root)
        {
            MHSConfigSection<V13ConfigSample> cfg =
                new MHSConfigService(Path.Combine(root, "xray"))
                    .ForMachine("XRay-3FPD").Open<V13ConfigSample>();
            Check(cfg.UpdateAsync(x =>
            {
                x.Detectors["D-01"] = new V13DetectorSample { Gain = 2 };
                x.Detectors["D-02"] = new V13DetectorSample { Gain = 3 };
                x.Acquisition.Group.TimeoutMs = 650;
            }, "两个探测器和采图参数", CancellationToken.None).GetAwaiter().GetResult().Success,
                "multi detector edit in one strongly typed operation");
            Check(cfg.ApplyPendingAtTaskBoundary(), "multi detector next task activate");
            Func<int> gain = cfg.PrepareValue(x => x.Detectors["D-02"].Gain);
            Check(gain() == 3 && cfg.Section(x => x.Acquisition.Group).TimeoutMs == 650,
                "prepared accessor and nested section read");
            Dictionary<string, V13DetectorSample> detached = cfg.Value(x => x.Detectors);
            detached["D-02"].Gain = 900;
            Check(gain() == 3, "mutable values must be isolated from original configuration");
            Check(cfg.UpdateAsync(x =>
            {
                x.Detectors["D-02"].Gain = 4;
                x.ExposureMs = 95; // NextTask establishes a transaction-wide apply boundary.
            }, "采图参数联合更新", CancellationToken.None).GetAwaiter().GetResult().Success &&
                gain() == 3, "live accessor must not see saved-but-not-effective changes");
            Check(cfg.ApplyPendingAtTaskBoundary() && gain() == 4,
                "live accessor reads latest activated revision without recompiling expression");
        }

        private static void CtLongTask(string root)
        {
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "ct"));
            MHSConfigAtomicGroup group = service.OpenAtomicGroup("CT.ScanAndRebuild", members =>
            {
                members.Register<V13ConfigSample>("Scan");
                members.Register<V13AsyncConfig>("Reconstruction");
            });
            Check(group.UpdateAsync(edit =>
            {
                V13ConfigSample scan = edit.Get<V13ConfigSample>("Scan");
                scan.ExposureMs = 135;
                edit.Set("Scan", scan);
                V13AsyncConfig rebuild = edit.Get<V13AsyncConfig>("Reconstruction");
                rebuild.Count = 4;
                edit.Set("Reconstruction", rebuild);
            }, "扫描与重建联合配置", CancellationToken.None).GetAwaiter().GetResult().Success,
                "atomic group declaration + one transaction");
            Check(group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied,
                "CT group single effective version");
            using (MHSConfigGroupSnapshot fixedScan = group.CaptureForTask("CT-Scan-01"))
            {
                Check(fixedScan.Get<V13ConfigSample>("Scan").ExposureMs == 135 &&
                    fixedScan.Get<V13AsyncConfig>("Reconstruction").Count == 4,
                    "CT acquisition and reconstruction share exactly one version");
                Check(group.UpdateAsync(edit =>
                {
                    V13AsyncConfig rebuild = edit.Get<V13AsyncConfig>("Reconstruction");
                    rebuild.Count = 8;
                    edit.Set("Reconstruction", rebuild);
                }, "后续重建方案", CancellationToken.None).GetAwaiter().GetResult().Success,
                    "second transaction is isolated from running scan");
                Check(group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied &&
                    fixedScan.Get<V13AsyncConfig>("Reconstruction").Count == 4,
                    "long-running task keeps original revision despite later apply");
            }
        }

        private static void AtomicProductRecipe(string root)
        {
            MVBConfigLayerSpec[] specs = new[]
            {
                new MVBConfigLayerSpec("Template", "template", "Base", 10),
                new MVBConfigLayerSpec("Project", "project", "Job", 20),
                new MVBConfigLayerSpec("Machine", "machine", "Line-1", 30),
                new MVBConfigLayerSpec("Product", "product", "Cell-A", 40),
                new MVBConfigLayerSpec("Override", "runtime", "Session", 50)
            };
            MVBConfigAtomicLayerProfile<V13ConfigSample> profile =
                new MVBConfigAtomicLayerProfile<V13ConfigSample>(
                    new MHSConfigService(Path.Combine(root, "recipe")), "Recipe-A", specs);
            Check(profile.UpdateAsync(edit =>
            {
                edit.Set("Template", x => x.ExposureMs, 100);
                edit.Set("Machine", x => x.ExposureMs, 155);
                edit.Set("Product", x => x.Note, "Cell-A");
            }, "产品配方", CancellationToken.None).GetAwaiter().GetResult().Success,
                "all layer edits committed with one simple API");
            Check(profile.ResolveEffective().Value(x => x.ExposureMs) == 80 &&
                profile.ResolveSaved().Value(x => x.ExposureMs) == 155,
                "saved and effective composite profiles remain separate");
            Check(profile.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied &&
                profile.ResolveEffective().Explain(x => x.ExposureMs).EffectiveSource == "Machine",
                "layer priority and provenance");
            using (MVBConfigTaskSnapshot<V13ConfigSample> scan = profile.CaptureForTask("Recipe-task"))
            {
                Check(profile.UpdateAsync(edit => edit.Remove("Machine", x => x.ExposureMs),
                    "恢复模板覆盖", CancellationToken.None).GetAwaiter().GetResult().Success &&
                    profile.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied,
                    "layer removal uses same transactional update");
                Func<int> pinned = scan.PrepareValue(x => x.ExposureMs);
                Check(pinned() == 155 &&
                    profile.ResolveEffective().Value(x => x.ExposureMs) == 100,
                    "pinned task and future recipe do not mix layers");
            }
        }

        private static void SecurityAndCatalog(string root)
        {
            IList<FCMConfigFieldInfo> fields =
                FCMConfigCatalog.Describe<ConfigSensitiveCollectionSample>();
            FCMConfigFieldInfo protectedField = fields.Single(x => x.Path == "Credentials");
            Check(protectedField.IsSensitive && protectedField.DefaultJson == null,
                "catalog metadata must redact the parent of secret-bearing collections");
            string md = FCMConfigCatalog.ExportMarkdown<ConfigSensitiveCollectionSample>();
            Check(!md.Contains("secret-in-nested-list") && md.Contains("敏感值已隐藏"),
                "automatically generated public docs cannot contain secret values");
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "privacy"));
            FCMConfigIdentity id = new FCMConfigIdentity("Tests.SecretCollection", "machine", "SecureMachine");
            FCMConfigRolePolicy policy = new FCMConfigRolePolicy();
            foreach (FCMConfigAccessAction action in new[] {
                FCMConfigAccessAction.Read, FCMConfigAccessAction.Audit })
                policy.Grant("auditor", action, id.SectionId, id.ScopeKind, id.ScopeId);
            MHSConfigAuthorizedSection<ConfigSensitiveCollectionSample> auditor =
                service.Authorize(new FCMConfigPrincipal("auditor", new[] { "auditor" }), policy)
                    .Open<ConfigSensitiveCollectionSample>(id);
            Expect<UnauthorizedAccessException>(() =>
                auditor.PrepareValue(x => x.Credentials),
                "prepared sensitive reads cannot bypass field permission"); 
            Expect<UnauthorizedAccessException>(() => auditor.GetStatus(),
                "Audit alone cannot leak raw pending OldJson/NewJson");
            Expect<UnauthorizedAccessException>(() => auditor.ReadCommittedAfter(null, 5),
                "Audit without ReadSensitive cannot leak reasons or paths");
            policy.Grant("auditor", FCMConfigAccessAction.ReadSensitive,
                id.SectionId, id.ScopeKind, id.ScopeId);
            Func<Dictionary<string, ConfigSensitiveItem>> prepared =
                auditor.PrepareValue(x => x.Credentials);
            Check(prepared()["source"].Password == "secret-in-nested-list",
                "authorized prepared read uses detached sensitive values");
            policy.Revoke("auditor", FCMConfigAccessAction.ReadSensitive,
                id.SectionId, id.ScopeKind, id.ScopeId);
            Expect<UnauthorizedAccessException>(() => prepared(),
                "authorization is checked again on every prepared read");
        }

        private static void DynamicCollectionEditing(string root)
        {
            string folder = Path.Combine(root, "typed-editor");
            MHSConfigSection<ConfigDynamicCollections> section =
                new MHSConfigService(folder).ForMachine("Line-B").Open<ConfigDynamicCollections>();
            string key = "D.\"X]\\02";
            using (MHSConfigEditorModel<ConfigDynamicCollections> editor =
                new MHSConfigEditorModel<ConfigDynamicCollections>(section, "动态集合联合编辑"))
            {
                editor.AddKey(x => x.Detectors, key, new V13DetectorSample { Gain = 3 });
                editor.SetValue(x => x.Detectors[key].Gain, 9);
                editor.AddKey(x => x.Detectors, "temp", new V13DetectorSample());
                editor.RenameKey(x => x.Detectors, "temp", "renamed");
                editor.RemoveKey(x => x.Detectors, "renamed");
                editor.InsertItem(x => x.Segments, 0, new V13DetectorSample { Gain = 1 });
                editor.InsertItem(x => x.Segments, 1, new V13DetectorSample { Gain = 2 });
                editor.InsertItem(x => x.Segments, 2, new V13DetectorSample { Gain = 3 });
                editor.MoveItem(x => x.Segments, 0, 2);
                editor.RemoveItemAt(x => x.Segments, 1);
                editor.AddKey(x => x.Groups, "scan.01", new List<V13DetectorSample>());
                editor.InsertItem(x => x.Groups["scan.01"], 0,
                    new V13DetectorSample { Gain = 7 });
                editor.AddSetItem(x => x.Tags, "A");
                editor.AddSetItem(x => x.Tags, "A");
                editor.AddSetItem(x => x.Tags, "B");
                editor.RemoveSetItem(x => x.Tags, "B");
                Check(editor.Draft.Detectors[key].Gain == 9 &&
                    editor.Draft.Segments[0].Gain == 2 &&
                    editor.Draft.Segments[1].Gain == 1 &&
                    editor.Draft.Tags.SetEquals(new[] { "A" }),
                    "typed dictionary/list/set commands build a deep draft");
                IList<MHSConfigCollectionItem> dynamicItems = editor.DescribeCollectionItems();
                Check(dynamicItems.Any(x => x.CollectionKind == FCMConfigTypeKind.Map &&
                    x.CollectionPath == "Detectors" && x.KeyJson == FCMJsonValueCodec.Serialize(key) &&
                    x.ItemPath == "Detectors[" + FCMJsonValueCodec.Serialize(key) + "]" &&
                    x.HasDraft && !x.HasSaved && x.IsModified) &&
                    dynamicItems.Any(x => x.CollectionKind == FCMConfigTypeKind.List &&
                        x.CollectionPath == "Segments" && x.Index == 1 && x.CanReorder) &&
                    dynamicItems.Any(x => x.CollectionKind == FCMConfigTypeKind.Set &&
                        x.CollectionPath == "Tags" && x.ItemPath == null && !x.CanReorder &&
                        x.HasDraft && !x.HasSaved) &&
                    dynamicItems.Any(x => x.CollectionPath == "Groups[\"scan.01\"]" &&
                        x.Index == 0 && x.ItemType == typeof(V13DetectorSample)),
                    "dynamic field tree exposes maps, nested lists, sets and draft/saved provenance");
                string before = FCMJsonValueCodec.Serialize(editor.Draft);
                Expect<ArgumentException>(() => editor.AddKey(x => x.Detectors, key,
                    new V13DetectorSample()), "duplicate dictionary key refused");
                Expect<KeyNotFoundException>(() => editor.RenameKey(
                    x => x.Detectors, "missing", "other"), "missing dictionary key refused");
                Expect<ArgumentOutOfRangeException>(() =>
                    editor.MoveItem(x => x.Segments, 0, 5), "bad list index refused");
                Check(FCMJsonValueCodec.Serialize(editor.Draft) == before,
                    "failed edits never mutate draft or undo history");
                Check(editor.CanUndo && editor.Undo() && editor.Redo() &&
                    FCMJsonValueCodec.Serialize(editor.Draft) == before,
                    "complex collection edits share existing undo redo");
                Check(editor.Preview().Validation.IsValid &&
                    editor.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                    "deep collection commit uses existing validation and CAS");
            }
            Check(section.GetLatestSaved().Detectors[key].Gain == 9 &&
                section.Value(x => x.Detectors).Count == 0,
                "Saved and Effective remain separate");
            Check(section.ApplyPendingAtTaskBoundary() &&
                section.Value(x => x.Detectors)[key].Gain == 9,
                "deep collections become effective only after approval");
            ConfigDynamicCollections restarted = new MHSConfigService(folder)
                .ForMachine("Line-B").Open<ConfigDynamicCollections>().Get();
            Check(restarted.Segments.Select(x => x.Gain).SequenceEqual(new[] { 2, 1 }) &&
                restarted.Tags.SetEquals(new[] { "A" }) && restarted.Detectors.ContainsKey(key) &&
                restarted.Groups["scan.01"][0].Gain == 7,
                "restart preserves escaped key, list ordering and set");
        }

        private static void PreparedReadAndConcurrency(string root)
        {
            MHSConfigSection<V13ConfigSample> config = new MHSConfigService(
                Path.Combine(root, "concurrency")).ForMachine("Bench-Machine").Open<V13ConfigSample>();
            Func<int> live = config.PrepareValue(x => x.ExposureMs);
            const int warmup = 500;
            for (int i = 0; i < warmup; ++i)
                Check(live() == 80, "prepared accessor warmup");
            Stopwatch stopwatch = Stopwatch.StartNew();
            long sum = 0;
            const int runs = 2000000; // Reduce clock-resolution noise in performance reports.
            for (int i = 0; i < runs; ++i) sum += live();
            stopwatch.Stop();
            Check(sum == runs * 80L, "prepared reader must return correct stable effective value");
            Console.WriteLine("CONFIG_BENCHMARK preparedScalarReads=" + runs +
                " elapsedMs=" + stopwatch.ElapsedMilliseconds +
                " readsPerSecond=" + (long)(runs * 1000.0 / Math.Max(1, stopwatch.ElapsedMilliseconds)));

            using (MHSConfigSnapshot<V13ConfigSample> task = config.CaptureForTask("CT-Long-Scan"))
            {
                Func<int> fixedReader = task.PrepareValue(x => x.ExposureMs);
                int invalidReads = 0;
                Task readers = Task.Run(() => Parallel.For(0, 12000,
                    new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                {
                    int observed = live();
                    if (observed != 80 && observed != 120)
                        Interlocked.Increment(ref invalidReads);
                    if (fixedReader() != 80)
                        Interlocked.Increment(ref invalidReads);
                }));
                Task writer = Task.Run(() =>
                {
                    Check(config.UpdateAsync(x => x.ExposureMs = 120, "并行写入",
                        CancellationToken.None).GetAwaiter().GetResult().Success,
                        "write under concurrent prepared reads");
                    Check(config.ApplyPendingAtTaskBoundary(),
                        "activate new effective version while old task is running");
                });
                Task.WaitAll(readers, writer);
                Check(invalidReads == 0 && live() == 120 && fixedReader() == 80,
                    "parallel readers see either complete effective revision and task remains pinned");
            }
        }

        private static void Check(bool ok, string text)
        { if (!ok) throw new InvalidOperationException("DX_CHECK_FAILED: " + text); }
        private static void Expect<TException>(Action action, string text) where TException : Exception
        { try { action(); } catch (TException) { return; } throw new InvalidOperationException("DX_EXPECT_FAILED: " + text); }
    }
}
