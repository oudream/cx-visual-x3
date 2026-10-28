using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeDataLifecyclePolicyTests
    {
        internal static void Run()
        {
            IndependentEntryPointsAndCombinedPolicies();
            ConsumerBoundAndPinnedRetention();
            ConcurrentBorrowProtectsOwnerAndBudget();
            InvalidCombinationsRejected();
        }
        private static void Check(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException("生命周期五维策略验收：" + reason);
        }
        private static void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("预期异常未抛出：" + typeof(T).Name);
        }
        private static void IndependentEntryPointsAndCombinedPolicies()
        {
            var budget = new MDSDataBudget(1024);
            using (var heap = new MDSDataHeap(null, budget))
            using (var target = new MDSDataHeap())
            {
                heap.Set("Image", new byte[] { 1, 2, 3, 4 });
                MDSDataLifecycleAccess control = heap.Lifecycle;
                control.ConfigureOwnership("Image", MDSDataOwnershipPolicy.Default.WithMove(false));
                control.ConfigureLifetime("Image", MDSDataLifetimePolicy.UntilLastConsumer());
                control.ConfigureRetention("Image", MDSDataRetentionPolicy.CacheFor(TimeSpan.FromMinutes(1)));
                control.ConfigureTransfer("Image", MDSDataTransferPolicy.CopyOnly);
                control.ConfigureReclamation("Image", MDSDataReclamationPolicy.OnExpiry);
                Check(control.Inspect("Image").Policy.Lifetime.ConsumerBound &&
                    !control.GetPolicy("Image").Ownership.AllowMove &&
                    control.GetPolicy("Image").Retention.TimeToLive.HasValue &&
                    !control.GetPolicy("Image").Transfer.AllowMove &&
                    control.GetPolicy("Image").Reclamation.ReleaseOnExpiry,
                    "五个独立的便捷设置均应对同一个数据生效");
                IDisposable use = control.AcquireUse("Image");
                IDisposable hold = control.Retain("Image");
                try
                {
                    Check(control.Inspect("Image").ActiveConsumers == 1 &&
                        control.Inspect("Image").RetentionHolds == 1, "活跃使用/保留理由可观察");
                    Check(!control.ReleaseEligible("Image"), "活跃对象不得被显式回收");
                    Reject<MDSDataException>(() => heap.Remove("Image"));
                    Reject<MDSDataException>(() => heap.Replace("Image", new byte[] { 9 }));
                    Reject<InvalidOperationException>(() => heap.MoveTo("Image", target, "Moved"));
                    heap.CopyTo("Image", target, "Copied");
                    byte[] copy = target.Get<byte[]>("Copied");
                    copy[0] = 55;
                    Check(heap.Get<byte[]>("Image")[0] == 1, "Copy必须具有独立可写底层缓冲");
                    Check(target.Lifecycle.Inspect("Copied").OwnerId == target.RunId,
                        "复制到新Heap后Owner身份属于目标Heap");
                    control.CloseConsumerAdmission("Image");
                    Reject<InvalidOperationException>(() => control.AcquireUse("Image"));
                    Check(control.Sweep(DateTime.UtcNow.AddHours(1)) == 0,
                        "即使TTL到期，活跃消费者与保留凭证也不许被驱逐");
                }
                finally { hold.Dispose(); use.Dispose(); }
                Check(control.Sweep(DateTime.UtcNow.AddHours(1)) == 1 &&
                    !heap.Contains("Image") && budget.RetainedBytes == 0,
                    "全部消费者/保留理由结束后，TTL回收应释放真实数据和预算");
                Check(target.Contains("Copied"), "回收源数据不应破坏独立Copy");
            }
        }
        private static void ConsumerBoundAndPinnedRetention()
        {
            using (var heap = new MDSDataHeap())
            {
                heap.Set("Temp", 11);
                var policy = MDSDataLifecyclePolicy.Default
                    .WithLifetime(MDSDataLifetimePolicy.UntilLastConsumer())
                    .WithReclamation(MDSDataReclamationPolicy.AfterLastConsumer);
                heap.Lifecycle.Configure("Temp", policy);
                IDisposable use = heap.Lifecycle.AcquireUse("Temp");
                heap.Lifecycle.CloseConsumerAdmission("Temp");
                Check(heap.Contains("Temp"), "关闭入场不等于强制释放已使用的对象");
                use.Dispose();
                Check(!heap.Contains("Temp"), "最后消费者退出时按策略自动释放");
                heap.Set("Pinned", new byte[] { 7 });
                heap.Lifecycle.ConfigureRetention("Pinned", MDSDataRetentionPolicy.Pinned());
                Check(!heap.Lifecycle.ReleaseEligible("Pinned") &&
                    heap.Lifecycle.GetBlockers("Pinned").Contains("Pinned"), "Pin必须阻止主动回收");
                Reject<MDSDataException>(() => heap.Remove("Pinned"));
                heap.Lifecycle.ConfigureRetention("Pinned", MDSDataRetentionPolicy.Flow);
                Check(heap.Lifecycle.ReleaseEligible("Pinned") && !heap.Contains("Pinned"),
                    "解除Pin后允许安全回收");
            }
        }
        private static void ConcurrentBorrowProtectsOwnerAndBudget()
        {
            var budget = new MDSDataBudget(1024);
            using (var heap = new MDSDataHeap(null, budget))
            {
                heap.Set("Shared", new byte[100]);
                var policy = MDSDataLifecyclePolicy.Default
                    .WithRetention(MDSDataRetentionPolicy.CacheFor(TimeSpan.FromSeconds(1)))
                    .WithReclamation(MDSDataReclamationPolicy.OnExpiry);
                heap.Lifecycle.Configure("Shared", policy);
                var started = new ManualResetEventSlim(false);
                var release = new ManualResetEventSlim(false);
                Task reader = Task.Run(() =>
                {
                    using (heap.Lifecycle.AcquireUse("Shared"))
                    {
                        started.Set();
                        if (!release.Wait(10000)) throw new TimeoutException("测试消费者超时");
                        Check(heap.Get<byte[]>("Shared").Length == 100,
                            "被借用的数据在合法消费者完成之前不得失效");
                    }
                });
                try
                {
                    Check(started.Wait(5000), "并发使用者成功入场");
                    Check(heap.Lifecycle.Sweep(DateTime.UtcNow.AddMinutes(5)) == 0,
                        "到期但有活跃消费者时不能物理回收");
                    Reject<MDSDataException>(() => heap.Remove("Shared"));
                    Reject<MDSDataException>(() => heap.Dispose());
                }
                finally { release.Set(); }
                Check(reader.Wait(5000), "消费者必须能够正常退出");
                Check(heap.Lifecycle.Sweep(DateTime.UtcNow.AddMinutes(5)) == 1 &&
                    budget.RetainedBytes == 0, "回收后账目和实际Heap一致");
                started.Dispose(); release.Dispose();
            }
        }
        private static void InvalidCombinationsRejected()
        {
            Reject<ArgumentException>(() => MDSDataLifecyclePolicy.Default
                .WithReclamation(MDSDataReclamationPolicy.AfterLastConsumer));
            Reject<ArgumentException>(() => MDSDataLifecyclePolicy.Default
                .WithReclamation(MDSDataReclamationPolicy.OnExpiry));
            Reject<ArgumentException>(() => MDSDataLifecyclePolicy.Default
                .WithRetention(MDSDataRetentionPolicy.Pinned())
                .WithLifetime(MDSDataLifetimePolicy.UntilLastConsumer())
                .WithReclamation(MDSDataReclamationPolicy.AfterLastConsumer));
            Reject<ArgumentOutOfRangeException>(() => MDSDataRetentionPolicy.CacheFor(TimeSpan.Zero));
        }
    }
}