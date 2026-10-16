using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>
    /// Real Windows STA construction and interaction smoke; does not claim a human
    /// has checked pixel layout, keyboard navigation or hardware configuration.
    /// </summary>
    internal static class ConfigWpfTests
    {
        private static V AwaitOnSta<V>(Task<V> task)
        {
            if (task.IsCompleted) return task.GetAwaiter().GetResult();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            return task.GetAwaiter().GetResult();
        }
        public static int Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxConfig-Wpf-" + Guid.NewGuid().ToString("N"));
            Exception failure = null;
            Thread sta = new Thread(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    MHSConfigService service = new MHSConfigService(root);
                    MHSConfigSection<V13ConfigSample> section =
                        service.ForMachine("WPF").Open<V13ConfigSample>();
                    using (var control = new MPWConfigEditorControl<V13ConfigSample>(section))
                    {
                        if (control.VisibleEditorCount < 4 || control.Content == null)
                            throw new InvalidOperationException("CONFIG_WPF_SCHEMA_RENDER_FAILED");
                        Action<string> change = null;
                        control.RegisterEditor("ExposureMs", (value, update) =>
                        {
                            change = update;
                            return new TextBox { Text = value };
                        });
                        if (change == null) throw new InvalidOperationException("CONFIG_WPF_EDITOR_EXTENSION_MISSING");
                        change("145");
                        if (control.Draft.ExposureMs != 145 ||
                            section.GetLatestSaved().ExposureMs != 80)
                            throw new InvalidOperationException("CONFIG_WPF_EDITOR_TRANSACTION_BYPASS");
                    }
                    if (section.GetLatestSaved().ExposureMs != 80)
                        throw new InvalidOperationException("CONFIG_WPF_DISCARDED_DRAFT_WROTE_TO_DISK");
                    var savedConfig = service.ForMachine("WPF-EndToEnd").Open<V13ConfigSample>();
                    using (var endToEnd = new MPWConfigEditorControl<V13ConfigSample>(savedConfig))
                    {
                        Action<string> setExposure = null;
                        endToEnd.RegisterEditor("ExposureMs", (json, setter) =>
                        {
                            setExposure = setter;
                            return new TextBox { Text = json };
                        });
                        setExposure("128");
                        MHSConfigCommitResult written = AwaitOnSta(endToEnd.SaveDraftAsync(CancellationToken.None));
                        if (!written.Success || savedConfig.GetLatestSaved().ExposureMs != 128 ||
                            savedConfig.Value(x => x.ExposureMs) != 80)
                            throw new InvalidOperationException("CONFIG_WPF_SAVE_DID_NOT_SEPARATE_EFFECTIVE");
                        var applied = endToEnd.ApplySaved(FCMConfigApplyPolicy.NextTask);
                        if (!applied.Applied || savedConfig.Value(x => x.ExposureMs) != 128)
                            throw new InvalidOperationException("CONFIG_WPF_APPLY_FAILED");
                        string oldVersion = savedConfig.SavedVersion;
                        setExposure("148");
                        if (!AwaitOnSta(endToEnd.SaveDraftAsync(CancellationToken.None)).Success ||
                            savedConfig.GetLatestSaved().ExposureMs != 148)
                            throw new InvalidOperationException("CONFIG_WPF_SECOND_SAVED");
                        var restored = AwaitOnSta(endToEnd.RestoreAsNewSavedAsync(
                            oldVersion, CancellationToken.None));
                        if (!restored.Success || savedConfig.SavedVersion == oldVersion ||
                            savedConfig.GetLatestSaved().ExposureMs != 128 ||
                            savedConfig.Value(x => x.ExposureMs) != 128)
                            throw new InvalidOperationException("CONFIG_WPF_HISTORY_NOT_NEW_REVISION");
                    }
                    if (new MHSConfigService(root).ForMachine("WPF-EndToEnd")
                        .Open<V13ConfigSample>().Value(x => x.ExposureMs) != 128)
                        throw new InvalidOperationException("CONFIG_WPF_REOPEN_MISMATCH");
                    using (var complex = new MPWConfigEditorControl<V13ConfigSample>(section))
                    {
                        Action<string> editComplex = null;
                        complex.RegisterEditor("Acquisition.Group", (json, update) =>
                        {
                            editComplex = update;
                            return new TextBox { Text = "专业复杂控件" };
                        });
                        if (editComplex == null)
                            throw new InvalidOperationException("CONFIG_WPF_COMPLEX_CUSTOM_EDITOR_MISSING");
                        editComplex(FCMJsonValueCodec.Serialize(new V13ExposureGroup {
                            TimeoutMs = 630, CaptureKey = "CAP-232" }));
                        if (complex.Draft.Acquisition.Group.TimeoutMs != 630 ||
                            section.GetLatestSaved().Acquisition.Group.TimeoutMs == 630)
                            throw new InvalidOperationException("CONFIG_WPF_COMPLEX_EDITOR_BYPASSED_TRANSACTION");
                    }
                    var secured = service.ForMachine("WPF-Secure").Open<ConfigSecureSample>();
                    FCMConfigRolePolicy policy = new FCMConfigRolePolicy();
                    foreach (FCMConfigAccessAction action in new[] {
                        FCMConfigAccessAction.Read, FCMConfigAccessAction.ReadSensitive,
                        FCMConfigAccessAction.Edit, FCMConfigAccessAction.Commit,
                        FCMConfigAccessAction.Audit, FCMConfigAccessAction.Apply })
                        policy.Grant("config-editor", action, secured.Identity.SectionId,
                            secured.Identity.ScopeKind, secured.Identity.ScopeId);
                    MHSConfigAuthorizedSection<ConfigSecureSample> authorized = service.Authorize(
                        new FCMConfigPrincipal("WPF-User", new[] { "config-editor" }), policy)
                        .Open<ConfigSecureSample>(secured.Identity);
                    using (var secureControl = new MPWConfigEditorControl<ConfigSecureSample>(authorized))
                    {
                        if (secureControl.VisibleEditorCount != 1)
                            throw new InvalidOperationException("CONFIG_WPF_SENSITIVE_FIELD_VISIBLE");
                        secureControl.Draft.Level = 77; // detached clone, not a write
                        if (secured.GetLatestSaved().Level != 10)
                            throw new InvalidOperationException("CONFIG_WPF_UNAUTHORIZED_PERSISTENCE");
                        policy.Revoke("config-editor", FCMConfigAccessAction.ReadSensitive,
                            secured.Identity.SectionId, secured.Identity.ScopeKind, secured.Identity.ScopeId);
                        bool refused = false;
                        try { var value = secureControl.Draft; }
                        catch (UnauthorizedAccessException) { refused = true; }
                        if (!refused)
                            throw new InvalidOperationException("CONFIG_WPF_PERMISSION_REVOCATION_IGNORED");
                    }

                    var group = service.OpenAtomicGroup("WPF-Group");
                    group.Register<V13ConfigSample>("Scan");
                    group.Register<V13AsyncConfig>("Reconstruction");
                    group.Seal();
                    var groupUi = new MPWConfigAtomicGroupControl(group);
                    if (groupUi.MemberCount != 2 || groupUi.Content == null ||
                        groupUi.PendingFields != 0)
                        throw new InvalidOperationException("CONFIG_WPF_GROUP_EDITOR_MISSING");
                    groupUi.StageField("Scan", "ExposureMs", "137");
                    groupUi.StageField("Reconstruction", "Count", "4");
                    if (groupUi.PendingFields != 2)
                        throw new InvalidOperationException("CONFIG_WPF_GROUP_STAGE_FAILED");
                    MHSConfigCommitResult grouped = AwaitOnSta(
                        groupUi.SaveGroupDraftAsync(CancellationToken.None));
                    if (!grouped.Success || groupUi.PendingFields != 0)
                        throw new InvalidOperationException("CONFIG_WPF_GROUP_COMMIT_FAILED");
                    using (var before = group.CaptureForTask("WPF-Before-Apply"))
                    using (var saved = group.CaptureSaved())
                    {
                        if (saved.Get<V13ConfigSample>("Scan").ExposureMs != 137 ||
                            saved.Get<V13AsyncConfig>("Reconstruction").Count != 4 ||
                            before.Get<V13ConfigSample>("Scan").ExposureMs != 80)
                            throw new InvalidOperationException("CONFIG_WPF_GROUP_SAVED_NOT_ATOMIC");
                    }
                    if (!group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied)
                        throw new InvalidOperationException("CONFIG_WPF_GROUP_APPLY_FAILED");
                    using (var after = group.CaptureForTask("WPF-After-Apply"))
                        if (after.Get<V13ConfigSample>("Scan").ExposureMs != 137 ||
                            after.Get<V13AsyncConfig>("Reconstruction").Count != 4)
                            throw new InvalidOperationException("CONFIG_WPF_GROUP_EFFECTIVE_NOT_ATOMIC");
                    var rejected = service.ForMachine("WPF-Validation").Open<V13ConfigSample>();
                    using (var invalid = new MPWConfigEditorControl<V13ConfigSample>(rejected))
                    {
                        Action<string> update = null;
                        invalid.RegisterEditor("Acquisition.Group.TimeoutMs", (json, setter) =>
                        {
                            update = setter; return new TextBox { Text = json };
                        });
                        if (update == null) throw new InvalidOperationException("CONFIG_WPF_VALIDATOR_EDITOR");
                        update("3000");
                        var result = AwaitOnSta(invalid.SaveDraftAsync(CancellationToken.None));
                        if (result.Success || rejected.GetLatestSaved().Acquisition.Group.TimeoutMs != 300)
                            throw new InvalidOperationException("CONFIG_WPF_INVALID_FIELD_WROTE_DISK");
                    }
                    var guarded = service.ForMachine("WPF-Guard").Open<V13ConfigSample>();
                    guarded.ConfigureApplyGuard(change =>
                        MHSConfigApplyDecision.Deny("MACHINE_BUSY", "待设备安全确认"));
                    using (var guardView = new MPWConfigEditorControl<V13ConfigSample>(guarded))
                    {
                        Action<string> update = null;
                        guardView.RegisterEditor("ExposureMs", (json, setter) =>
                        {
                            update = setter; return new TextBox { Text = json };
                        });
                        update("138");
                        if (!AwaitOnSta(guardView.SaveDraftAsync(CancellationToken.None)).Success)
                            throw new InvalidOperationException("CONFIG_WPF_GUARD_PREPARE");
                        var result = guardView.ApplySaved(FCMConfigApplyPolicy.NextTask);
                        if (result.Applied || !guarded.HasPendingApply ||
                            guarded.Value(x => x.ExposureMs) != 80)
                            throw new InvalidOperationException("CONFIG_WPF_GUARD_FALSE_ACTIVATION");
                    }
                    var paged = service.ForMachine("WPF-Large").Open<ConfigDynamicCollections>();
                    var many = new Dictionary<string, ConfigDynamicNode>();
                    for (int i = 0; i < 250; ++i)
                        many.Add("D" + i.ToString("D4"), new ConfigDynamicNode { Gain = 2 });
                    if (!paged.UpdateAsync(x => x.Detectors = many, "WPF paging",
                        CancellationToken.None).GetAwaiter().GetResult().Success ||
                        !paged.ApplyPendingAtTaskBoundary())
                        throw new InvalidOperationException("CONFIG_WPF_PAGING_SETUP");
                    using (var largeUi = new MPWConfigEditorControl<ConfigDynamicCollections>(paged))
                    {
                        if (largeUi.VisibleEditorCount < 120 || largeUi.VisibleEditorCount > 170)
                            throw new InvalidOperationException("CONFIG_WPF_DYNAMIC_PAGING_LIMIT");
                        if (largeUi.Content == null)
                            throw new InvalidOperationException("CONFIG_WPF_DYNAMIC_PAGING_RENDER");
                    }
                    var layers = new[] {
                        new MVBConfigLayerSpec("Template", "template", "WPF-Base", 10),
                        new MVBConfigLayerSpec("Machine", "machine", "WPF", 20)
                    };
                    var atomic = new MVBConfigAtomicLayerProfile<V13ConfigSample>(
                        service, "WPF-Atomic", layers);
                    var atomicView = new MPWConfigLayerOverview<V13ConfigSample>(atomic);
                    if (atomicView.Content == null || atomicView.DisplayedSourceCount != 2)
                        throw new InvalidOperationException("CONFIG_WPF_ATOMIC_SOURCE_RENDER");
                    var independent = new MVBConfigLayeredProfile<V13ConfigSample>(
                        service, "WPF-Independent", layers);
                    var layerView = new MPWConfigLayerOverview<V13ConfigSample>(independent);
                    if (layerView.Content == null || layerView.DisplayedSourceCount != 2)
                        throw new InvalidOperationException("CONFIG_WPF_INDEPENDENT_SOURCE_RENDER");
                }
                catch (Exception ex) { failure = ex; }
            });
            sta.SetApartmentState(ApartmentState.STA);
            sta.Start();
            if (!sta.Join(TimeSpan.FromSeconds(45)))
            {
                Console.Error.WriteLine("FAIL CONFIG_WPF_STA_TIMEOUT");
                return 1;
            }
            try
            {
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
                Console.WriteLine("PASS Config WPF STA editor, custom field and two layer presentation modes");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL Config WPF STA: " + ex);
                return 1;
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
