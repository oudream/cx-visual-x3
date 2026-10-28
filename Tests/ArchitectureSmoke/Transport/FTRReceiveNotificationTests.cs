using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FTRReceiveNotificationTests
    {
        // 只验证正式交付FIFO的元数据预算，不模拟设备成功或另建生产Channel。
        private sealed class NotificationChannel : FTRSingleChannelBase
        {
            internal NotificationChannel() : base("notification-budget") { }
            internal void Queue(Action action, CancellationToken token) { AfterReceived(action, token); }
            protected override Task OnOpenAsync(CancellationToken token)
            { StartBufferedReceive(new FTRVirtualChannelConfiguration { QueueCapacity = 1 }); return Task.CompletedTask; }
            protected override Task OnCloseAsync(CancellationToken token)
            { DrainBufferedReceive(2000, token); return Task.CompletedTask; }
            protected override Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken token)
            { throw new NotSupportedException("Notification resource test does not implement transport"); }
        }
        internal static void Run()
        {
            using (var channel = new NotificationChannel()) using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim()) using (var attempting = new ManualResetEventSlim())
            using (var cancellation = new CancellationTokenSource())
            {
                int executed = 0; channel.Open();
                channel.Queue(() => { entered.Set(); release.Wait(); Interlocked.Increment(ref executed); }, CancellationToken.None);
                try
                {
                    Check(entered.Wait(3000), "control callback did not enter");
                    Task producer = Task.Run(() => {
                        attempting.Set();
                        try { channel.Queue(() => Interlocked.Increment(ref executed), cancellation.Token); }
                        catch (OperationCanceledException) { return; }
                        throw new InvalidOperationException("full notification queue incorrectly admitted producer");
                    });
                    Check(attempting.Wait(3000) && !producer.Wait(100), "metadata budget did not block before admission");
                    var queue = channel.Diagnostics.ReceiveQueue;
                    Check(queue.NotificationCapacity == 1 && queue.OutstandingNotifications == 1 && queue.PendingNotifications == 0,
                        "in-flight notification must retain its own credit");
                    cancellation.Cancel(); Check(producer.Wait(3000), "notification-budget wait did not observe stop token");
                }
                finally { release.Set(); }
                channel.Close();
                Check(executed == 1 && channel.Diagnostics.ReceiveQueue.OutstandingNotifications == 0,
                    "cancelled notification executed or budget was not returned");
            }
            Console.WriteLine("F17.NOTIFICATION PASS: terminal metadata bounded including callbacks; blocked admission cancellable and drained");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("F17.NOTIFICATION " + message); }
    }
}
