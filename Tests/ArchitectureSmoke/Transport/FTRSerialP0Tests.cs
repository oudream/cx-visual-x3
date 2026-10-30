using System;
using System.Threading;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>
    /// F17 Serial硬件缺席时的软件负面验收：原FCM、Owner、生命周期、
    /// 参数无效、取消与异常结算。真实收/发必须另以RS-232/485设备HIL验收。
    /// </summary>
    internal static class FTRSerialP0Tests
    {
        private static void Check(bool valid,string message)
        {if(!valid)throw new InvalidOperationException("F17.SERIAL_P0 "+message);}
        public static void Run()
        {
            FTRSerialChannelConfiguration config=new FTRSerialChannelConfiguration {
                Id="f17-serial-real-os-negative",
                PortName="COM65534",BaudRate=115200,DataBits=8,
                Parity="None",StopBits="One",Handshake="RequestToSend",
                ReceiveBufferBytes=4096,
                ReceiveByteBudget=8192,QueueCapacity=2,
                SendQueueCapacity=2,SendByteBudget=16384,
                SendTimeoutMs=800,StopTimeoutMs=2000,ReceiveIdleTimeoutMs=1000,
                CapturePayloadEvidence=false
            };
            var converter=new FTRChannelConfigurationConverter();
            var restored=(FTRSerialChannelConfiguration)converter.Read(converter.Write(config));
            Check(restored.Kind==FTRChannelKind.Serial&&restored.PortName==config.PortName&&
                restored.ReceiveByteBudget==8192&&restored.SendTimeoutMs==800&&
                restored.ReceiveIdleTimeoutMs==1000&&
                restored.Handshake=="RequestToSend"&&
                !restored.CapturePayloadEvidence&&
                restored.DescribeType().Topology==FTRChannelTopology.SharedBus,
                "Serial正式FCM参数/共享总线身份丢失");
            var invalid=new FTRSerialChannelConfiguration {DataBits=9};
            bool wrong=false;
            try{invalid.Validate();}catch(ArgumentException){wrong=true;}
            Check(wrong,"非法Serial数据位未拒绝");
            wrong=false;
            try{new FTRSerialChannelConfiguration{Handshake="UnknownFlowControl"}.Validate();}
            catch(ArgumentException){wrong=true;}
            Check(wrong,"非法Serial流控值未在接触物理设备前被FCM拒绝");
            Check(new FTRSerialChannelConfiguration().Handshake=="None",
                "新参数默认值不得改变旧工程串口流控");
            using(var channel=new FTRSerialChannel(restored))
            {
                Check(channel.State==FTRChannelState.Closed,"构造串口时不能自动访问硬件");
                bool rejected=false;
                try{channel.Send(new byte[]{1,2});}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Closed Serial禁止发送，不得伪称设备已收到");
                using(var cancel=new CancellationTokenSource())
                {
                    cancel.Cancel();
                    bool canceled=false;
                    try{channel.OpenAsync(cancel.Token).GetAwaiter().GetResult();}
                    catch(OperationCanceledException){canceled=true;}
                    Check(canceled&&channel.State==FTRChannelState.Closed,
                        "Open预取消时不应访问真实串口或进入Faulted");
                }
                bool failed=false;
                try{channel.Open();}
                catch(Exception){failed=true;}
                Check(failed&&channel.State==FTRChannelState.Faulted,
                    "不存在的真实COM口不能打开/伪造虚拟接收");
                channel.Close();
                Check(channel.State==FTRChannelState.Closed,
                    "Serial打开失败后必须能通过原生命周期清理");
                // 不创建Virtual替身，复测同一对象再次Open失败后的结算能力。
                failed=false;
                try{channel.Open();}
                catch(Exception){failed=true;}
                Check(failed&&channel.State==FTRChannelState.Faulted,
                    "复用Serial对象不得保留上一次失败会话");
                channel.Close();
                Check(channel.State==FTRChannelState.Closed,
                    "Serial复用失败后的Dispose前资源状态不正确");
            }
            Console.WriteLine("F17.SERIAL_P0_PASS configuration, cancel, absent real COM, "+
                "safe failure cleanup (hardware RX/TX not tested)");
        }
    }
}
