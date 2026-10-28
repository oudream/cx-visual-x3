using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FTRReconnectControllerTests
    {
        private static void Check(bool valid,string message)
        {if(!valid)throw new InvalidOperationException("F17.RECONNECT "+message);}
        private sealed class FlakyChannel : FTRSingleChannelBase
        {
            internal int Failures,Opens,Closes;
            internal Exception InjectedFailure;
            internal FlakyChannel(int failures):base("reconnect-flaky"){Failures=failures;}
            internal void Break(){Fault(new IOException("simulated disconnected link"));}
            protected override Task OnOpenAsync(CancellationToken ct)
            {
                Interlocked.Increment(ref Opens);
                if(Opens<=Failures)throw InjectedFailure ?? new IOException("connect refused");
                return Task.CompletedTask;
            }
            protected override Task OnCloseAsync(CancellationToken ct)
            {Interlocked.Increment(ref Closes);return Task.CompletedTask;}
            protected override Task OnSendAsync(byte[] data,FTRSubChannelAddress address,CancellationToken ct)
            {return Task.CompletedTask;}
        }
        public static void Run()
        {
            BackoffAndLimit();
            FatalFailureStopsRetries();
            RealTcpRecovery();
            StopWithinRecoveryCallbacks();
            FTRChannelHealthTests.Run();
            FTRChannelConvenienceTests.Run();
            FTRPersistentChannelTests.Run();
            Console.WriteLine("F17.RECONNECT_PASS configured retries, optional fault recovery, OS TCP reconnect, callback-safe stop and no command replay");
        }
        private static void BackoffAndLimit()
        {
            var config=new FTRVirtualChannelConfiguration {
                Id="reconnect-flaky",ReconnectMaxAttempts=4,
                ReconnectInitialDelayMs=15,ReconnectMaxDelayMs=50,ReconnectBackoff=2
            };
            using(var c=new FlakyChannel(2))
            using(var control=new FTRReconnectController(c,config))
            {
                int failed=0,restored=0;
                control.AttemptFailed+=(n,e)=>Interlocked.Increment(ref failed);
                control.Reconnected+=()=>Interlocked.Increment(ref restored);
                control.Start();
                Check(SpinWait.SpinUntil(()=>c.State==FTRChannelState.Open && Volatile.Read(ref restored)==1,3000),
                    "按配置的初始延迟、退避和总次数恢复成功");
                Check(c.Opens==3&&failed==2&&restored==1&&!control.Snapshot().Exhausted,
                    "失败次数、成功通知及诊断必须正确");
                var recovery=control.Snapshot();
                Check(recovery.FailedAttemptsTotal==2 && recovery.SuccessfulRecoveries==1 &&
                    recovery.LastRecoveredUtc.HasValue && recovery.LastRecoveryDurationMs>=0 &&
                    recovery.LastFailure.Kind==FTRFailureKind.TransportIO,
                    "必须保留失败分类、实际恢复耗时与累计次数");
                control.StopAsync().GetAwaiter().GetResult();
                c.Break();
                Thread.Sleep(100);
                Check(c.Opens==3,"Stop后不能隐式恢复或重发命令");
                c.Close();
            }
            var limited=new FTRVirtualChannelConfiguration{
                Id="reconnect-flaky",ReconnectMaxAttempts=2,ReconnectInitialDelayMs=10,
                ReconnectMaxDelayMs=20,ReconnectBackoff=2
            };
            using(var c=new FlakyChannel(99))
            using(var control=new FTRReconnectController(c,limited))
            {
                // 构造后外部FCM变化不能偷偷改写当前运行恢复策略。
                limited.ReconnectMaxAttempts=100;
                limited.ReconnectInitialDelayMs=1000;
                control.Start();
                Check(SpinWait.SpinUntil(()=>control.Snapshot().Exhausted,3000),
                    "重试上限必须停止恢复循环");
                Check(c.Opens==2&&control.Snapshot().Attempts==2&&control.Snapshot().LastError!=null,
                    "重试达到上限应保留最后故障并拒绝继续尝试");
                control.StopAsync().GetAwaiter().GetResult();
                c.Close();
            }
        }
        private static void FatalFailureStopsRetries()
        {
            var config=new FTRVirtualChannelConfiguration{
                Id="reconnect-flaky",ReconnectMaxAttempts=10,
                ReconnectInitialDelayMs=5,ReconnectMaxDelayMs=20,ReconnectBackoff=2
            };
            using(var channel=new FlakyChannel(99){InjectedFailure=new UnauthorizedAccessException("port denied")})
            using(var control=new FTRReconnectController(channel,config))
            {
                int exhausted=0;
                control.Exhausted+=()=>Interlocked.Increment(ref exhausted);
                control.Start();
                Check(SpinWait.SpinUntil(()=>control.Snapshot().Exhausted,3000),
                    "不可恢复权限错误应停止重试");
                control.StopAsync().GetAwaiter().GetResult();
                var snap=control.Snapshot();
                Check(channel.Opens==1 && snap.Attempts==1 &&
                    snap.LastFailure.Kind==FTRFailureKind.PermissionDenied &&
                    snap.FailedAttemptsTotal==1 && snap.SuccessfulRecoveries==0 &&
                    Volatile.Read(ref exhausted)==1,
                    "不可恢复错误不应消耗全部重试额度且必须记录分类");
                channel.Close();
            }
        }

        /// <summary>恢复任务同步事件中关闭维护不能等待自身；外部停止仍须完成清理。</summary>
        private static void StopWithinRecoveryCallbacks()
        {
            var failedConfig=new FTRVirtualChannelConfiguration {
                Id="reconnect-flaky",ReconnectMaxAttempts=4,
                ReconnectInitialDelayMs=10,ReconnectMaxDelayMs=20,ReconnectBackoff=2
            };
            using(var channel=new FlakyChannel(1))
            using(var controller=new FTRReconnectController(channel,failedConfig))
            using(var callbackDone=new ManualResetEventSlim())
            {
                Exception callbackError=null;
                controller.AttemptFailed+=(attempt,error)=>
                {
                    try {controller.StopAsync().GetAwaiter().GetResult();}
                    catch(Exception ex) {callbackError=ex;}
                    finally {callbackDone.Set();}
                };
                controller.Start();
                Check(callbackDone.Wait(3000),"失败回调内StopAsync不得自等待死锁");
                Check(callbackError==null,"失败回调内停止不得抛出异常: "+callbackError);
                // 其它线程调用应等待工作任务及取消源的最终清理。
                controller.StopAsync().GetAwaiter().GetResult();
                Check(channel.Opens==1 && !controller.Snapshot().Active,
                    "失败回调请求停止后不得再打开下一次连接");
                channel.Close();
            }

            var successConfig=new FTRVirtualChannelConfiguration {
                Id="reconnect-flaky",ReconnectMaxAttempts=2,
                ReconnectInitialDelayMs=10,ReconnectMaxDelayMs=20,ReconnectBackoff=2
            };
            using(var channel=new FlakyChannel(0))
            using(var controller=new FTRReconnectController(channel,successConfig))
            using(var callbackDone=new ManualResetEventSlim())
            {
                Exception callbackError=null;
                controller.Reconnected+=()=>
                {
                    try {controller.Dispose();}
                    catch(Exception ex) {callbackError=ex;}
                    finally {callbackDone.Set();}
                };
                controller.Start();
                Check(callbackDone.Wait(3000),"成功回调内Dispose不得自等待死锁");
                Check(callbackError==null,"成功回调内Dispose不得抛出异常: "+callbackError);
                controller.StopAsync().GetAwaiter().GetResult();
                Check(channel.Opens==1 && !controller.Snapshot().Active,
                    "成功回调释放后不得重新调度恢复");
                channel.Close();
            }
        }
        private static void RealTcpRecovery()
        {
            var listener=new TcpListener(IPAddress.Loopback,0);
            listener.Start();
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            var cfg=new FTRTcpClientChannelConfiguration {
                Id="reconnect-os",Host="127.0.0.1",Port=port,ConnectTimeoutMs=1000,
                ReconnectMaxAttempts=10,ReconnectInitialDelayMs=30,
                ReconnectMaxDelayMs=200,ReconnectBackoff=2
            };
            try
            {
                using(var client=new FTRTcpClientChannel(cfg))
                {
                    Task<TcpClient> firstAccept=listener.AcceptTcpClientAsync();
                    client.Open();
                    using(var first=firstAccept.GetAwaiter().GetResult())
                    using(var control=new FTRReconnectController(client,cfg))
                    {
                        control.Start();
                        Task<TcpClient> nextAccept=listener.AcceptTcpClientAsync();
                        first.Close();
                        Check(nextAccept.Wait(TimeSpan.FromSeconds(8)),
                            "真实TCP对端断线后恢复器应建立第二条物理连接");
                        using(var replacement=nextAccept.GetAwaiter().GetResult())
                        {
                            replacement.ReceiveTimeout=3000;
                            Check(SpinWait.SpinUntil(()=>client.State==FTRChannelState.Open,3000),
                                "恢复后Channel必须回到Open");
                            client.Send(new byte[]{6,7,8});
                            byte[] buffer=new byte[3];int read=0;
                            while(read<buffer.Length)
                            {
                                int n=replacement.GetStream().Read(buffer,read,buffer.Length-read);
                                Check(n>0,"恢复TCP读取途中断开");read+=n;
                            }
                            Check(buffer[0]==6&&buffer[1]==7&&buffer[2]==8,
                                "真实新连接应该能够同步发送，不能自动重放历史命令");
                        }
                        control.StopAsync().GetAwaiter().GetResult();
                    }
                    client.Close();
                }
            }
            finally{listener.Stop();}
        }
    }
}
