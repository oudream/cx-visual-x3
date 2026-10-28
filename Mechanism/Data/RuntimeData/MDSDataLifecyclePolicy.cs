using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>独立所有权策略：物理数据仍由原Heap/专业Owner持有，不创建第二仓库。</summary>
    public sealed class MDSDataOwnershipPolicy
    {
        public static MDSDataOwnershipPolicy Default { get { return new MDSDataOwnershipPolicy(true, true); } }
        public bool AllowMove { get; private set; }
        public bool AllowCopy { get; private set; }
        public MDSDataOwnershipPolicy(bool allowMove, bool allowCopy)
        { AllowMove = allowMove; AllowCopy = allowCopy; }
        public MDSDataOwnershipPolicy WithMove(bool allowed)
        { return new MDSDataOwnershipPolicy(allowed, AllowCopy); }
        public MDSDataOwnershipPolicy WithCopy(bool allowed)
        { return new MDSDataOwnershipPolicy(AllowMove, allowed); }
        public void ApplyTo(MDSDataHeap heap, string name)
        { if (heap == null) throw new ArgumentNullException("heap"); heap.Lifecycle.ConfigureOwnership(name, this); }
    }

    /// <summary>可用期：默认随Flow；ConsumerBound需运行者明确关闭新消费者入口。</summary>
    public sealed class MDSDataLifetimePolicy
    {
        public static MDSDataLifetimePolicy Flow { get { return new MDSDataLifetimePolicy(false); } }
        public static MDSDataLifetimePolicy UntilLastConsumer()
        { return new MDSDataLifetimePolicy(true); }
        public bool ConsumerBound { get; private set; }
        private MDSDataLifetimePolicy(bool consumerBound) { ConsumerBound = consumerBound; }
        public void ApplyTo(MDSDataHeap heap, string name)
        { if (heap == null) throw new ArgumentNullException("heap"); heap.Lifecycle.ConfigureLifetime(name, this); }
    }

    /// <summary>保留原因独立于访问许可。缓存TTL是从策略生效时刻计算。</summary>
    public sealed class MDSDataRetentionPolicy
    {
        public static MDSDataRetentionPolicy Flow { get { return new MDSDataRetentionPolicy(null, false); } }
        public static MDSDataRetentionPolicy CacheFor(TimeSpan ttl)
        {
            if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("ttl");
            return new MDSDataRetentionPolicy(ttl, false);
        }
        public static MDSDataRetentionPolicy Pinned()
        { return new MDSDataRetentionPolicy(null, true); }
        public TimeSpan? TimeToLive { get; private set; }
        public bool IsPinned { get; private set; }
        private MDSDataRetentionPolicy(TimeSpan? ttl, bool pinned)
        { TimeToLive = ttl; IsPinned = pinned; }
        public void ApplyTo(MDSDataHeap heap, string name)
        { if (heap == null) throw new ArgumentNullException("heap"); heap.Lifecycle.ConfigureRetention(name, this); }
    }

    /// <summary>现有Heap.MoveTo/CopyTo的显式允许规则，不暗中实现零拷贝或外部借用。</summary>
    public sealed class MDSDataTransferPolicy
    {
        public static MDSDataTransferPolicy CopyAndMove
        { get { return new MDSDataTransferPolicy(true, true); } }
        public static MDSDataTransferPolicy CopyOnly
        { get { return new MDSDataTransferPolicy(true, false); } }
        public static MDSDataTransferPolicy MoveOnly
        { get { return new MDSDataTransferPolicy(false, true); } }
        public bool AllowCopy { get; private set; }
        public bool AllowMove { get; private set; }
        public MDSDataTransferPolicy(bool allowCopy, bool allowMove)
        { AllowCopy = allowCopy; AllowMove = allowMove; }
        public void ApplyTo(MDSDataHeap heap, string name)
        { if (heap == null) throw new ArgumentNullException("heap"); heap.Lifecycle.ConfigureTransfer(name, this); }
    }

    /// <summary>实际自动回收策略；未进入安全判定的普通数据仍按旧API显式移除。</summary>
    public sealed class MDSDataReclamationPolicy
    {
        public static MDSDataReclamationPolicy Explicit
        { get { return new MDSDataReclamationPolicy(false, false); } }
        public static MDSDataReclamationPolicy AfterLastConsumer
        { get { return new MDSDataReclamationPolicy(true, false); } }
        public static MDSDataReclamationPolicy OnExpiry
        { get { return new MDSDataReclamationPolicy(false, true); } }
        public static MDSDataReclamationPolicy OnPressure
        { get { return new MDSDataReclamationPolicy(false, false, true); } }
        public static MDSDataReclamationPolicy OnExpiryOrPressure
        { get { return new MDSDataReclamationPolicy(false, true, true); } }
        public bool ReleaseAfterConsumers { get; private set; }
        public bool ReleaseOnExpiry { get; private set; }
        public bool ReleaseOnPressure { get; private set; }
        public MDSDataReclamationPolicy(bool afterConsumers, bool onExpiry, bool onPressure = false)
        { ReleaseAfterConsumers = afterConsumers; ReleaseOnExpiry = onExpiry; ReleaseOnPressure = onPressure; }
        public void ApplyTo(MDSDataHeap heap, string name)
        { if (heap == null) throw new ArgumentNullException("heap"); heap.Lifecycle.ConfigureReclamation(name, this); }
    }

    /// <summary>只持有策略定义；可单独设置五维且不持有数据实例。</summary>
    public sealed class MDSDataLifecyclePolicy
    {
        public static MDSDataLifecyclePolicy Default
        {
            get { return new MDSDataLifecyclePolicy(
                MDSDataOwnershipPolicy.Default, MDSDataLifetimePolicy.Flow,
                MDSDataRetentionPolicy.Flow, MDSDataTransferPolicy.CopyAndMove,
                MDSDataReclamationPolicy.Explicit); }
        }
        public MDSDataOwnershipPolicy Ownership { get; private set; }
        public MDSDataLifetimePolicy Lifetime { get; private set; }
        public MDSDataRetentionPolicy Retention { get; private set; }
        public MDSDataTransferPolicy Transfer { get; private set; }
        public MDSDataReclamationPolicy Reclamation { get; private set; }

        public MDSDataLifecyclePolicy(MDSDataOwnershipPolicy ownership,
            MDSDataLifetimePolicy lifetime, MDSDataRetentionPolicy retention,
            MDSDataTransferPolicy transfer, MDSDataReclamationPolicy reclamation)
        {
            Ownership = ownership ?? throw new ArgumentNullException("ownership");
            Lifetime = lifetime ?? throw new ArgumentNullException("lifetime");
            Retention = retention ?? throw new ArgumentNullException("retention");
            Transfer = transfer ?? throw new ArgumentNullException("transfer");
            Reclamation = reclamation ?? throw new ArgumentNullException("reclamation");
            Validate();
        }
        public void Validate()
        {
            if (Reclamation.ReleaseAfterConsumers && !Lifetime.ConsumerBound)
                throw new ArgumentException("末消费者自动回收需要ConsumerBound生命周期");
            if (Reclamation.ReleaseOnExpiry && !Retention.TimeToLive.HasValue)
                throw new ArgumentException("到期回收需要明确的TTL缓存保留策略");
            if (Reclamation.ReleaseOnPressure && !Retention.TimeToLive.HasValue)
                throw new ArgumentException("容量压力回收只允许显式声明的可驱逐缓存");
            if (Retention.IsPinned && (Reclamation.ReleaseAfterConsumers ||
                Reclamation.ReleaseOnExpiry || Reclamation.ReleaseOnPressure))
                throw new ArgumentException("永久Pinned保留不能同时配置自动回收");
        }
        public MDSDataLifecyclePolicy WithOwnership(MDSDataOwnershipPolicy x)
        { return new MDSDataLifecyclePolicy(x, Lifetime, Retention, Transfer, Reclamation); }
        public MDSDataLifecyclePolicy WithLifetime(MDSDataLifetimePolicy x)
        { return new MDSDataLifecyclePolicy(Ownership, x, Retention, Transfer, Reclamation); }
        public MDSDataLifecyclePolicy WithRetention(MDSDataRetentionPolicy x)
        { return new MDSDataLifecyclePolicy(Ownership, Lifetime, x, Transfer, Reclamation); }
        public MDSDataLifecyclePolicy WithTransfer(MDSDataTransferPolicy x)
        { return new MDSDataLifecyclePolicy(Ownership, Lifetime, Retention, x, Reclamation); }
        public MDSDataLifecyclePolicy WithReclamation(MDSDataReclamationPolicy x)
        { return new MDSDataLifecyclePolicy(Ownership, Lifetime, Retention, Transfer, x); }
        public void ApplyTo(MDSDataHeap heap, string name)
        { if (heap == null) throw new ArgumentNullException("heap"); heap.Lifecycle.Configure(name, this); }
    }

    public sealed class MDSDataLifecycleSnapshot
    {
        internal MDSDataLifecycleSnapshot(string name, string owner, int active, int holds,
            bool closed, DateTime? expires, IEnumerable<string> blockers,
            IEnumerable<string> retentionReasons, MDSDataLifecyclePolicy policy)
        {
            Name = name; OwnerId = owner; ActiveConsumers = active; RetentionHolds = holds;
            ConsumerAdmissionClosed = closed; ExpiresUtc = expires; Policy = policy;
            Blockers = new ReadOnlyCollection<string>(new List<string>(blockers));
            RetentionReasons = new ReadOnlyCollection<string>(new List<string>(retentionReasons));
        }
        public string Name { get; private set; }
        public string OwnerId { get; private set; }
        public int ActiveConsumers { get; private set; }
        public int RetentionHolds { get; private set; }
        public bool ConsumerAdmissionClosed { get; private set; }
        public DateTime? ExpiresUtc { get; private set; }
        public IList<string> Blockers { get; private set; }
        public IList<string> RetentionReasons { get; private set; }
        public MDSDataLifecyclePolicy Policy { get; private set; }
    }
}