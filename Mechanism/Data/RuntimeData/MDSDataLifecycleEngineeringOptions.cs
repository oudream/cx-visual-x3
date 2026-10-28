using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    public enum MDSOwnershipMode { Default, NoMove, NoCopy }
    public enum MDSLifetimeMode { Flow, LastConsumer }
    public enum MDSRetentionMode { Flow, CacheTtl, Pinned }
    public enum MDSTransferMode { CopyAndMove, CopyOnly, MoveOnly }
    public enum MDSReclamationMode { Explicit, LastConsumer, Ttl, Pressure, TtlOrPressure }

    /// <summary>
    /// MHS工程配置中的生命周期策略模板：仅保存定义，不持有运行时值或创建第二MDS Heap。
    /// Commit/Apply使用现有MHS版本及授权机制；运行Host应在安全Run边界读取Effective版本后显式应用。
    /// </summary>
    [FCMConfigSection("RuntimeData.Lifecycle")]
    [FCMConfigValidator(typeof(MDSDataLifecycleEngineeringOptionsRules))]
    public sealed class MDSDataLifecycleEngineeringOptions
    {
        [FCMConfigItem("所有权", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public MDSOwnershipMode Ownership { get; set; } = MDSOwnershipMode.Default;
        [FCMConfigItem("可用期", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public MDSLifetimeMode Lifetime { get; set; } = MDSLifetimeMode.Flow;
        [FCMConfigItem("保留策略", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public MDSRetentionMode Retention { get; set; } = MDSRetentionMode.Flow;
        [FCMConfigItem("缓存TTL", Unit = "s", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        [Range(1, 86400)]
        public int CacheTtlSeconds { get; set; } = 120;
        [FCMConfigItem("交接策略", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public MDSTransferMode Transfer { get; set; } = MDSTransferMode.CopyAndMove;
        [FCMConfigItem("回收触发", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public MDSReclamationMode Reclamation { get; set; } = MDSReclamationMode.Explicit;

        /// <summary>编译正式MDS五维组合，不缓存第二份可写运行状态。</summary>
        public MDSDataLifecyclePolicy ToPolicy()
        {
            if (!Enum.IsDefined(typeof(MDSOwnershipMode), Ownership) ||
                !Enum.IsDefined(typeof(MDSLifetimeMode), Lifetime) ||
                !Enum.IsDefined(typeof(MDSRetentionMode), Retention) ||
                !Enum.IsDefined(typeof(MDSTransferMode), Transfer) ||
                !Enum.IsDefined(typeof(MDSReclamationMode), Reclamation) ||
                CacheTtlSeconds < 1 || CacheTtlSeconds > 86400)
                throw new ArgumentException("工程生命周期枚举或TTL数值无效");
            MDSDataOwnershipPolicy ownership = Ownership == MDSOwnershipMode.NoMove
                ? MDSDataOwnershipPolicy.Default.WithMove(false)
                : Ownership == MDSOwnershipMode.NoCopy
                    ? MDSDataOwnershipPolicy.Default.WithCopy(false)
                    : MDSDataOwnershipPolicy.Default;
            MDSDataLifetimePolicy lifetime = Lifetime == MDSLifetimeMode.LastConsumer
                ? MDSDataLifetimePolicy.UntilLastConsumer() : MDSDataLifetimePolicy.Flow;
            MDSDataRetentionPolicy retention = Retention == MDSRetentionMode.CacheTtl
                ? MDSDataRetentionPolicy.CacheFor(TimeSpan.FromSeconds(CacheTtlSeconds))
                : Retention == MDSRetentionMode.Pinned
                    ? MDSDataRetentionPolicy.Pinned() : MDSDataRetentionPolicy.Flow;
            MDSDataTransferPolicy transfer = Transfer == MDSTransferMode.CopyOnly
                ? MDSDataTransferPolicy.CopyOnly
                : Transfer == MDSTransferMode.MoveOnly
                    ? MDSDataTransferPolicy.MoveOnly : MDSDataTransferPolicy.CopyAndMove;
            MDSDataReclamationPolicy reclamation;
            switch (Reclamation)
            {
                case MDSReclamationMode.LastConsumer: reclamation = MDSDataReclamationPolicy.AfterLastConsumer; break;
                case MDSReclamationMode.Ttl: reclamation = MDSDataReclamationPolicy.OnExpiry; break;
                case MDSReclamationMode.Pressure: reclamation = MDSDataReclamationPolicy.OnPressure; break;
                case MDSReclamationMode.TtlOrPressure: reclamation = MDSDataReclamationPolicy.OnExpiryOrPressure; break;
                default: reclamation = MDSDataReclamationPolicy.Explicit; break;
            }
            return new MDSDataLifecyclePolicy(ownership, lifetime, retention, transfer, reclamation);
        }

        /// <summary>仅由已确认Effective版本的受信Host在新Run安全边界显式调用。</summary>
        public void ApplyTo(MDSDataHeap heap, string dataName)
        {
            if (heap == null) throw new ArgumentNullException("heap");
            ToPolicy().ApplyTo(heap, dataName);
        }
    }

    public sealed class MDSDataLifecycleEngineeringOptionsRules :
        IFCMConfigValidator<MDSDataLifecycleEngineeringOptions>
    {
        public IEnumerable<FCMConfigIssue> Validate(MDSDataLifecycleEngineeringOptions settings,
            FCMConfigValidationContext context)
        {
            if (settings == null)
            {
                yield return FCMConfigIssue.Error(context.Path, "LIFECYCLE_POLICY",
                    "工程生命周期配置不能为空");
                yield break;
            }
            string problem = null;
            try { settings.ToPolicy(); }
            catch (ArgumentException error) { problem = error.Message; }
            if (problem != null)
                yield return FCMConfigIssue.Error(context.Path, "LIFECYCLE_POLICY", problem);
        }
    }
}