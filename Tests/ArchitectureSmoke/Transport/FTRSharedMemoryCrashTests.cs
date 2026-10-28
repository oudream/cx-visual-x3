using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实Windows进程异常退出：禁止同名旧MMF未知结果重放，新会话正常双向通信。</summary>
    internal static class FTRSharedMemoryCrashTests
    {
        private static void Check(bool value,string detail)
        {if(!value)throw new InvalidOperationException("F17.SHM_CRASH "+detail);}

        private static FTRSharedMemoryChannelConfiguration Configuration(string id,string map,FTRSharedMemorySide side)
        {
            return new FTRSharedMemoryChannelConfiguration {
                Id=id,MapName=map,Side=side,Slots=4,SlotBytes=4096,
                ReceiveBufferBytes=4096,ReceiveByteBudget=16384,QueueCapacity=4
            };
        }

        // 独立子进程使用，绝不能在父测试进程直接调用。
        internal static int RunCrashChild(string map)
        {
            var channel=new FTRSharedMemoryChannel(Configuration("shm-crash-child",map,FTRSharedMemorySide.B));
            channel.Open();
            Console.WriteLine("F17.SHM_CRASH_CHILD_READY");
            Console.Out.Flush();
            // 不执行Close/Dispose，OS遗弃该Side的命名Mutex；仅子进程提前退出。
            Environment.Exit(37);
            return 37;
        }

        internal static void Run()
        {
            string map="f17crash_"+Guid.NewGuid().ToString("N");
            using(var parent=new FTRSharedMemoryChannel(
                Configuration("shm-crash-parent",map,FTRSharedMemorySide.A)))
            {
                parent.Open();
                var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                    "--channel-shm-crash-child "+map) {
                    UseShellExecute=false,CreateNoWindow=true,
                    RedirectStandardOutput=true,RedirectStandardError=true
                };
                using(var child=Process.Start(start))
                {
                    Check(child!=null,"未创建真实共享内存异常退出子进程");
                    var stdout=child.StandardOutput.ReadToEndAsync();
                    var stderr=child.StandardError.ReadToEndAsync();
                    bool ended=child.WaitForExit(10000);
                    if(!ended){child.Kill();child.WaitForExit(3000);}
                    Check(ended && child.ExitCode==37 &&
                        stdout.GetAwaiter().GetResult().Contains("F17.SHM_CRASH_CHILD_READY"),
                        "子进程未在获得命名侧锁后故意退出: "+
                        (ended?stderr.GetAwaiter().GetResult():"timeout"));
                }
                using(var unsafeRestart=new FTRSharedMemoryChannel(
                    Configuration("shm-crash-restart",map,FTRSharedMemorySide.B)))
                {
                    bool rejected=false;
                    try{unsafeRestart.Open();}
                    catch(InvalidOperationException e)
                    {
                        rejected=e.ToString().Contains("异常退出");
                    }
                    Check(rejected && unsafeRestart.State==FTRChannelState.Faulted,
                        "旧MMF遗弃侧锁必须拒绝不安全重启，不能重放未知业务数据");
                    unsafeRestart.Close();
                }
                parent.Close();
            }

            // 恢复必须改用新会话/共享区名称；不对未知结果做任何自动重放。
            string fresh="f17fresh_"+Guid.NewGuid().ToString("N");
            using(var left=new FTRSharedMemoryChannel(
                Configuration("shm-fresh-a",fresh,FTRSharedMemorySide.A)))
            using(var right=new FTRSharedMemoryChannel(
                Configuration("shm-fresh-b",fresh,FTRSharedMemorySide.B)))
            using(var arrivedLeft=new ManualResetEventSlim())
            using(var arrivedRight=new ManualResetEventSlim())
            {
                byte[] receivedLeft=null,receivedRight=null;
                left.Received+=message=>{receivedLeft=message.Data;arrivedLeft.Set();};
                right.Received+=message=>{receivedRight=message.Data;arrivedRight.Set();};
                left.Open();right.Open();
                left.Send(new byte[]{1,3,5});
                right.Send(new byte[]{2,4,6,8});
                Check(arrivedLeft.Wait(6000) && arrivedRight.Wait(6000) &&
                    receivedLeft.SequenceEqual(new byte[]{2,4,6,8}) &&
                    receivedRight.SequenceEqual(new byte[]{1,3,5}),
                    "独立新共享区未恢复双向完整通信");
                left.Close();right.Close();
            }
            Console.WriteLine("F17.SHM_CRASH_PASS abandoned side rejects old region; fresh session duplex restored");
        }
    }
}
