using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FTRNamedPipeAdmissionTests
    {
        internal static void Run()
        {
            string name = "f17-admission-" + Guid.NewGuid().ToString("N");
            using (var server = new FTRNamedPipeServerChannel(new FTRNamedPipeServerChannelConfiguration {
                Id = "pipe-admission", PipeName = name, MaxClients = 1 }))
            {
                int received = 0;
                server.Received += data => Interlocked.Add(ref received, data.Data.Length);
                server.Open();
                using (var first = new NamedPipeClientStream(".", name, PipeDirection.InOut))
                {
                    first.Connect(3000); first.WriteByte(1);
                    Check(SpinWait.SpinUntil(() => Volatile.Read(ref received) == 1, 3000), "首连接数据未到达");
                    using (var extra = new NamedPipeClientStream(".", name, PipeDirection.InOut))
                    {
                        bool rejected = false;
                        try { extra.Connect(100); } catch (TimeoutException) { rejected = true; } catch (IOException) { rejected = true; }
                        Check(rejected && server.State == FTRChannelState.Open && server.SnapshotSubChannels().Length == 1,
                            "正常满额不得创建第2实例并把Listener变成Faulted");
                    }
                }
                Check(SpinWait.SpinUntil(() => server.SnapshotSubChannels().Length == 0, 3000), "断开未归还逻辑对端");
                // 超过MaxClients很多轮的真实连接复用；断开清理之后必须继续接单。
                for (int i = 0; i < 32; i++)
                {
                    using (var next = new NamedPipeClientStream(".", name, PipeDirection.InOut))
                    {
                        next.Connect(3000); next.WriteByte(2);
                        int expected = i + 2;
                        Check(SpinWait.SpinUntil(() => Volatile.Read(ref received) == expected, 3000), "复用名额后真实数据缺失");
                    }
                    Check(SpinWait.SpinUntil(() => server.SnapshotSubChannels().Length == 0, 3000), "重复断开未清理");
                    Check(server.State == FTRChannelState.Open, "单Peer断开不应杀死监听");
                }
                server.Close();
                for (int i = 0; i < 8; i++)
                {
                    server.Open();
                    using (var racing = new NamedPipeClientStream(".", name, PipeDirection.InOut))
                    {
                        racing.Connect(3000); racing.WriteByte(3);
                        server.Close();
                        Check(server.State == FTRChannelState.Closed && server.SnapshotSubChannels().Length == 0,
                            "Close必须结算最后一次Accept与全部读者再释放入场信号量");
                    }
                }
            }
            Console.WriteLine("F17.PIPE_ADMISSION PASS: real MaxClients=1 saturation, 32 reconnects, release and 8 accept-close cycles");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("F17.PIPE_ADMISSION " + message); }
    }
}
