using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.Configuration;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Mechanism.Presentation.Wpf.RuntimeData
{
    /// <summary>
    /// 通用WPF运行策略投影，绝不拥有Heap或任何工程定义。
    /// Studio由可信Host显式授权才能修改；默认只观察真实MDS数据。
    /// </summary>
    public partial class MPWDataLifecycleWorkspaceControl : UserControl, IDisposable
    {
        private MDSDataHeap _heap;
        private bool _allowChanges;
        private MHSConfigSection<MDSDataLifecycleEngineeringOptions> _engineering;
        private bool _allowEngineeringChanges;
        private bool _disposed;
        public MPWDataLifecycleWorkspaceControl()
        {
            InitializeComponent();
            Populate(OwnershipMode, "允许Copy/Move", "禁止Move", "禁止Copy");
            Populate(LifetimeMode, "Flow结束", "最后消费者");
            Populate(RetentionMode, "Flow保留", "缓存TTL 2分钟", "Pinned保留");
            Populate(TransferMode, "允许Copy/Move", "仅Copy", "仅Move");
            Populate(ReclamationMode, "显式释放", "消费者结束", "TTL到期", "预算压力", "TTL或压力");
            OperationStatus.Text = "未绑定真实运行时Data Heap。";
            AutomationProperties.SetAutomationId(this, "MPW.RuntimeData.Lifecycle");
            AutomationProperties.SetAutomationId(ApplyAllButton, "MPW.RuntimeData.ApplyAll");
            AutomationProperties.SetAutomationId(LoadEngineeringButton, "MPW.RuntimeData.LoadEngineering");
            AutomationProperties.SetAutomationId(SaveEngineeringButton, "MPW.RuntimeData.SaveEngineering");
            SaveEngineeringButton.IsEnabled = false;
            LoadEngineeringButton.IsEnabled = false;
            EngineeringStatus.Text = "尚未绑定工程配置。";
        }
        private static void Populate(ComboBox control, params string[] names)
        {
            foreach (string name in names) control.Items.Add(name);
            control.SelectedIndex = 0;
        }
        public string SelectedDataName { get { return DataItems.SelectedItem as string; } }
        public bool IsBound { get { return _heap != null; } }
        public bool CanEdit { get { return _allowChanges; } }
        public string LastError { get; private set; }
        public bool HasEngineeringSection { get { return _engineering != null; } }

        /// <summary>仅绑定受信Host提供的MHS正式Section，不创建工程配置真源。</summary>
        public void BindEngineering(
            MHSConfigSection<MDSDataLifecycleEngineeringOptions> section,
            bool allowEngineeringChanges = false)
        {
            if (_disposed) throw new ObjectDisposedException("MPWDataLifecycleWorkspaceControl");
            _engineering = section ?? throw new ArgumentNullException("section");
            _allowEngineeringChanges = allowEngineeringChanges;
            LoadEngineeringButton.IsEnabled = true;
            SaveEngineeringButton.IsEnabled = allowEngineeringChanges;
            RefreshEngineeringStatus();
        }

        private void RefreshEngineeringStatus()
        {
            EngineeringStatus.Text = _engineering == null
                ? "尚未绑定工程配置。"
                : "Saved: " + _engineering.SavedVersion +
                  " / Effective: " + _engineering.EffectiveVersion +
                  (_engineering.HasPendingApply ? " · 待新Task边界审批生效" : " · 已生效") +
                  (_allowEngineeringChanges ? " · 可保存草稿" : " · 只读");
        }

        /// <summary>从MHS Saved版本加载可编辑草稿，不改变当前运行值或Effective。</summary>
        public void LoadEngineeringDraft()
        {
            if (_disposed) throw new ObjectDisposedException("MPWDataLifecycleWorkspaceControl");
            if (_engineering == null) throw new InvalidOperationException("未绑定MHS工程配置");
            MDSDataLifecycleEngineeringOptions x = _engineering.GetLatestSaved();
            x.ToPolicy();
            OwnershipMode.SelectedIndex = (int)x.Ownership;
            LifetimeMode.SelectedIndex = (int)x.Lifetime;
            RetentionMode.SelectedIndex = (int)x.Retention;
            CacheTtlSecondsBox.Text = x.CacheTtlSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            TransferMode.SelectedIndex = (int)x.Transfer;
            ReclamationMode.SelectedIndex = (int)x.Reclamation;
            RefreshEngineeringStatus();
        }

        public MDSDataLifecycleEngineeringOptions BuildEngineeringDraft()
        {
            int seconds;
            if (!int.TryParse(CacheTtlSecondsBox.Text,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out seconds) ||
                seconds < 1 || seconds > 86400)
                throw new ArgumentOutOfRangeException("CacheTtlSeconds",
                    "缓存TTL必须介于1至86400秒");
            var result = new MDSDataLifecycleEngineeringOptions {
                Ownership = (MDSOwnershipMode)OwnershipMode.SelectedIndex,
                Lifetime = (MDSLifetimeMode)LifetimeMode.SelectedIndex,
                Retention = (MDSRetentionMode)RetentionMode.SelectedIndex,
                CacheTtlSeconds = seconds,
                Transfer = (MDSTransferMode)TransferMode.SelectedIndex,
                Reclamation = (MDSReclamationMode)ReclamationMode.SelectedIndex
            };
            result.ToPolicy(); // 提前提示五维冲突；真正提交仍通过MHS的CAS和校验。
            return result;
        }

        /// <summary>保存Saved版本但绝不隐式执行ApplyPending或触碰运行Heap。</summary>
        public async Task<MHSConfigCommitResult> SaveEngineeringDraftAsync(
            string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_disposed) throw new ObjectDisposedException("MPWDataLifecycleWorkspaceControl");
            if (_engineering == null || !_allowEngineeringChanges)
                throw new InvalidOperationException("工程配置只读或未授权保存");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("提交原因不能为空", "reason");
            MDSDataLifecycleEngineeringOptions draft = BuildEngineeringDraft();
            var section = _engineering;
            return await section.UpdateAsync(saved => {
                saved.Ownership = draft.Ownership;
                saved.Lifetime = draft.Lifetime;
                saved.Retention = draft.Retention;
                saved.CacheTtlSeconds = draft.CacheTtlSeconds;
                saved.Transfer = draft.Transfer;
                saved.Reclamation = draft.Reclamation;
            }, reason, cancellationToken).ConfigureAwait(false);
        }

        private void LoadEngineeringClicked(object sender, RoutedEventArgs args)
        {
            try { LoadEngineeringDraft(); LastError = null; }
            catch (Exception error) { LastError = error.Message; EngineeringStatus.Text = LastError; }
        }

        private async void SaveEngineeringClicked(object sender, RoutedEventArgs args)
        {
            SaveEngineeringButton.IsEnabled = false;
            try
            {
                MHSConfigCommitResult result = await SaveEngineeringDraftAsync(
                    "Studio人工编辑运行数据五维生命周期", CancellationToken.None);
                LastError = result.Success ? null : (result.ErrorCode ?? "MHS草稿保存失败");
                RefreshEngineeringStatus();
                if (!result.Success) EngineeringStatus.Text += " · " + LastError;
            }
            catch (Exception error) { LastError = error.Message; EngineeringStatus.Text = LastError; }
            finally { SaveEngineeringButton.IsEnabled = _allowEngineeringChanges && !_disposed; }
        }

        public void Bind(MDSDataHeap heap, bool allowRuntimePolicyChanges = false)
        {
            if (_disposed) throw new ObjectDisposedException("MPWDataLifecycleWorkspaceControl");
            if (heap == null) throw new ArgumentNullException("heap");
            _heap = heap;
            _allowChanges = allowRuntimePolicyChanges;
            ApplyAllButton.IsEnabled = _allowChanges;
            RefreshItems();
        }
        public void RefreshItems()
        {
            if (_disposed) return;
            string previous = SelectedDataName;
            DataItems.Items.Clear();
            if (_heap != null)
                foreach (MDSDataSnapshot item in _heap.Query(new MDSDataQuery { Take = 500 }))
                    DataItems.Items.Add(item.Name);
            if (previous != null && DataItems.Items.Contains(previous))
                DataItems.SelectedItem = previous;
            else if (DataItems.Items.Count != 0)
                DataItems.SelectedIndex = 0;
            RefreshDiagnostics();
        }
        private MDSDataOwnershipPolicy Ownership()
        {
            switch (OwnershipMode.SelectedIndex)
            {
                case 1: return MDSDataOwnershipPolicy.Default.WithMove(false);
                case 2: return MDSDataOwnershipPolicy.Default.WithCopy(false);
                default: return MDSDataOwnershipPolicy.Default;
            }
        }
        private MDSDataLifetimePolicy Lifetime()
        { return LifetimeMode.SelectedIndex == 1
            ? MDSDataLifetimePolicy.UntilLastConsumer() : MDSDataLifetimePolicy.Flow; }
        private MDSDataRetentionPolicy Retention()
        {
            switch (RetentionMode.SelectedIndex)
            {
                case 1: return MDSDataRetentionPolicy.CacheFor(TimeSpan.FromMinutes(2));
                case 2: return MDSDataRetentionPolicy.Pinned();
                default: return MDSDataRetentionPolicy.Flow;
            }
        }
        private MDSDataTransferPolicy Transfer()
        {
            switch (TransferMode.SelectedIndex)
            {
                case 1: return MDSDataTransferPolicy.CopyOnly;
                case 2: return MDSDataTransferPolicy.MoveOnly;
                default: return MDSDataTransferPolicy.CopyAndMove;
            }
        }
        private MDSDataReclamationPolicy Reclamation()
        {
            switch (ReclamationMode.SelectedIndex)
            {
                case 1: return MDSDataReclamationPolicy.AfterLastConsumer;
                case 2: return MDSDataReclamationPolicy.OnExpiry;
                case 3: return MDSDataReclamationPolicy.OnPressure;
                case 4: return MDSDataReclamationPolicy.OnExpiryOrPressure;
                default: return MDSDataReclamationPolicy.Explicit;
            }
        }
        public MDSDataLifecyclePolicy BuildDraft()
        {
            return MDSDataLifecyclePolicy.Default
                .WithOwnership(Ownership())
                .WithLifetime(Lifetime())
                .WithRetention(Retention())
                .WithTransfer(Transfer())
                .WithReclamation(Reclamation());
        }
        /// <summary>共用UI及自动测试的便捷入口，调用真实MDS Policy，不做假保存。</summary>
        public bool TryApplyPolicy(MDSDataLifecyclePolicy policy)
        {
            if (_disposed) throw new ObjectDisposedException("MPWDataLifecycleWorkspaceControl");
            if (policy == null) throw new ArgumentNullException("policy");
            if (!_allowChanges || _heap == null || SelectedDataName == null)
            {
                LastError = "未绑定可信Heap、没有选择数据或运行策略写权限未授权";
                OperationStatus.Text = LastError;
                return false;
            }
            try
            {
                policy.ApplyTo(_heap, SelectedDataName);
                LastError = null;
                OperationStatus.Text = "已应用于当前真实MDS Heap；未保存为工程配置。";
                RefreshDiagnostics();
                return true;
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                OperationStatus.Text = LastError;
                RefreshDiagnostics();
                return false;
            }
        }
        private void ApplyAllClicked(object sender, RoutedEventArgs args)
        {
            try { TryApplyPolicy(BuildDraft()); }
            catch (Exception error) { OperationStatus.Text = error.Message; LastError=error.Message; }
        }
        private void ApplyOneClicked(object sender, RoutedEventArgs args)
        {
            if (!_allowChanges || _heap == null || SelectedDataName == null)
            {
                LastError = "未获得可信宿主授权或未选择数据";
                OperationStatus.Text = LastError;
                return;
            }
            try
            {
                string name = SelectedDataName;
                string dimension = (sender as Button)?.Tag as string;
                switch (dimension)
                {
                    case "Ownership": Ownership().ApplyTo(_heap, name); break;
                    case "Lifetime": Lifetime().ApplyTo(_heap, name); break;
                    case "Retention": Retention().ApplyTo(_heap, name); break;
                    case "Transfer": Transfer().ApplyTo(_heap, name); break;
                    case "Reclamation": Reclamation().ApplyTo(_heap, name); break;
                    default: throw new InvalidOperationException("不支持的策略维度");
                }
                LastError = null;
                OperationStatus.Text = dimension + " 已独立应用；未保存为工程配置。";
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                OperationStatus.Text = LastError;
            }
            RefreshDiagnostics();
        }
        private void SelectionChanged(object sender, SelectionChangedEventArgs args)
        { if (Diagnostics != null) RefreshDiagnostics(); }
        private void RefreshClicked(object sender, RoutedEventArgs args) { RefreshItems(); }
        public void RefreshDiagnostics()
        {
            if (_heap == null || SelectedDataName == null)
            { Diagnostics.Text = "当前没有绑定或可观察的数据"; return; }
            try
            {
                MDSDataLifecycleSnapshot x = _heap.Lifecycle.Inspect(SelectedDataName);
                Diagnostics.Text = "Data: " + x.Name + "\nOwner: " + x.OwnerId +
                    "\nActiveConsumers: " + x.ActiveConsumers +
                    "\nRetentionHolds: " + x.RetentionHolds +
                    "\nAdmissionClosed: " + x.ConsumerAdmissionClosed +
                    "\nTTL: " + (x.ExpiresUtc.HasValue ? x.ExpiresUtc.Value.ToString("O") : "—") +
                    "\nRetentionReasons: " + string.Join(",", x.RetentionReasons) +
                    "\nBlockers: " + string.Join(",", x.Blockers) +
                    "\nCopy/Move: " + x.Policy.Transfer.AllowCopy + "/" + x.Policy.Transfer.AllowMove +
                    "\nOnExpiry/Pressure: " + x.Policy.Reclamation.ReleaseOnExpiry +
                    "/" + x.Policy.Reclamation.ReleaseOnPressure;
            }
            catch (Exception error)
            { Diagnostics.Text = error.GetType().Name + ": " + error.Message; }
        }
        public void Dispose()
        {
            _disposed = true;
            _heap = null; _engineering = null;
            DataItems.Items.Clear();
        }
    }
}