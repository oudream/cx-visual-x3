using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>F17 S1/S2：真实公开诊断契约、故障阶段/作用域和统一健康快照。</summary>
    internal static class FTRChannelHealthTests
    {
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("F17.HEALTH "+message);}

        private sealed class FaultProbe : FTRSingleChannelBase
        {
            internal bool FailOpen;
            internal int CleanupCount;
            internal FaultProbe(string id):base(id){}
            internal void Break(){Fault(new IOException("receive stream disconnected"));}
            protected override Task OnOpenAsync(CancellationToken ct)
            {
                if(FailOpen)throw new SocketException((int)SocketError.ConnectionRefused);
                return Task.CompletedTask;
            }
            protected override Task OnCloseAsync(CancellationToken ct)
            {Interlocked.Increment(ref CleanupCount);return Task.CompletedTask;}
            protected override Task OnSendAsync(byte[] data,FTRSubChannelAddress addr,CancellationToken ct)
            {throw new TimeoutException("send deadline exceeded; delivery unknown");}
        }

        public static void Run()
        {
            ClassifiedDiagnostics();
            ChannelLifecycleAndHealth();
            ObserverIsolation();
            Console.WriteLine("F17.HEALTH_PASS typed failure, stage/scope, observer isolation, live rates and channel state");
        }

        private static void ClassifiedDiagnostics()
        {
            var diagnostics=new FTRChannelDiagnostics();
            diagnostics.RecordSend(8192);
            diagnostics.RecordReceive(4096);
            Thread.Sleep(140); // 首次采样有真实单调时间跨度。
            FTRChannelDiagnosticsSnapshot first=diagnostics.Snapshot();
            Check(first.SentBytes==8192 && first.ReceivedBytes==4096 &&
                first.SendBytesPerSecond>0 && first.ReceiveBytesPerSecond>0 &&
                first.LastFailure==null,"字节计数/速率与空故障快照");

            diagnostics.RecordError(new OperationCanceledException("caller cancelled"),"Send",FTRFaultScope.Operation);
            var cancelled=diagnostics.Snapshot().LastFailure;
            Check(cancelled.Kind==FTRFailureKind.Cancelled && cancelled.Scope==FTRFaultScope.Operation &&
                cancelled.Stage=="Send","取消不能归类为可重连网络故障");

            diagnostics.RecordError(new IOException("wrapped socket",new SocketException((int)SocketError.ConnectionReset)),
                "Receive",FTRFaultScope.SubChannel);
            var remote=diagnostics.Snapshot().LastFailure;
            Check(remote.Kind==FTRFailureKind.RemoteClosed && remote.Scope==FTRFaultScope.SubChannel &&
                remote.Stage=="Receive","必须识别嵌套Socket故障和Peer范围");

            diagnostics.RecordError(new UnauthorizedAccessException("denied"),"Open",FTRFaultScope.Channel);
            Check(diagnostics.Snapshot().LastFailure.Kind==FTRFailureKind.PermissionDenied,
                "权限错误不可当作普通网络断线");

            diagnostics.RecordError(new TimeoutException("timed out"),"Close",FTRFaultScope.Channel);
            var end=diagnostics.Snapshot();
            Check(end.Errors==4 && end.LastFailure.Kind==FTRFailureKind.Timeout &&
                end.LastFailure.Stage=="Close" && end.LastError=="timed out",
                "错误计数及最后一次故障详情");
        }

        private static void ObserverIsolation()
        {
            using(var channel=new FaultProbe("f17-observer"))
            {
                channel.StateChanged+=state=>{throw new IOException("observer intentionally failed");};
                channel.Open();
                var health=channel.Health;
                Check(health.State==FTRChannelState.Open &&
                    health.Diagnostics.LastFailure.Stage=="Observer" &&
                    health.Diagnostics.LastFailure.Scope==FTRFaultScope.Operation,
                    "状态观察者异常不应改变传输状态或归为物理接收故障");
                channel.Close();
            }
        }

        private static void ChannelLifecycleAndHealth()
        {
            using(var channel=new FaultProbe("f17-health"))
            {
                channel.Open();
                var before=channel.Health;
                Check(before.ChannelId=="f17-health" && before.SessionId==channel.SessionId &&
                    before.State==FTRChannelState.Open && before.ActivePeers==1,
                    "健康快照不得替换当前会话身份");
                try{channel.Send(new byte[]{1,2,3});Check(false,"错误发送意外成功");}
                catch(TimeoutException){}
                var failure=channel.Health;
                Check(failure.Diagnostics.LastFailure.Kind==FTRFailureKind.Timeout &&
                    failure.Diagnostics.LastFailure.Stage=="Send" &&
                    failure.Diagnostics.LastFailure.Scope==FTRFaultScope.Operation,
                    "Send应记录操作作用域故障，而不是擅自重启物理连接");
                channel.Break();
                var fault=channel.Health;
                Check(fault.State==FTRChannelState.Faulted &&
                    fault.Diagnostics.LastFailure.Kind==FTRFailureKind.TransportIO &&
                    fault.Diagnostics.LastFailure.Scope==FTRFaultScope.Channel,
                    "Fault应留下通道级故障与可读的状态快照");
                channel.Close();
                Check(channel.Health.State==FTRChannelState.Closed && channel.Health.ActivePeers==0,
                    "Close结算后健康快照必须反映释放");
            }

            using(var offline=new FTRTcpServerChannel(new FTRTcpServerChannelConfiguration{
                Id="f17-health-offline",Host="127.0.0.1",Port=16161
            }))
            {
                var peer=offline.RegisterPeer("offline-peer","offline.local");
                Check(offline.SnapshotSubChannels().Length==1 &&
                    offline.Health.State==FTRChannelState.Closed &&
                    offline.Health.ActivePeers==0,
                    "离线登记的SubChannel不得在健康视图中冒充在线客户端");
                offline.RemovePeer(peer.Address);
            }

            using(var channel=new FaultProbe("f17-open-failure"){FailOpen=true})
            {
                try{channel.Open();Check(false,"错误连接意外打开");}
                catch(SocketException){}
                var error=channel.Health;
                Check(error.State==FTRChannelState.Faulted &&
                    error.Diagnostics.LastFailure.Kind==FTRFailureKind.NetworkUnavailable &&
                    error.Diagnostics.LastFailure.Stage=="Open" &&
                    channel.CleanupCount==1,
                    "Open失败要同时记录根因并释放部分资源");
                channel.Close();
            }
        }
    }
}
