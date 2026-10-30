using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>
    /// RS-232/RS-485原始共享总线。独立阻塞读取线程+FTR有界交付；
    /// 同步有序写入与异步入口复用同一SerialPort.Write，不解析站号/Modbus。
    /// </summary>
    public sealed class FTRSerialChannel : FTRChannelBase
    {
        private readonly FTRSerialChannelConfiguration _config;
        private readonly object _writer=new object();
        private SerialPort _port;
        private CancellationTokenSource _stop;
        private Thread _reader;

        public FTRSerialChannel(FTRChannelConfiguration configuration)
            :base((configuration??throw new ArgumentNullException("configuration")).Id,
                FTRChannelTopology.SharedBus)
        {
            configuration.Validate();
            _config=configuration as FTRSerialChannelConfiguration ??
                throw new ArgumentException("要求FTRSerialChannelConfiguration");
        }

        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Parity parity=(Parity)Enum.Parse(typeof(Parity),_config.Parity);
            StopBits stopBits=(StopBits)Enum.Parse(typeof(StopBits),_config.StopBits);
            // 固定短ReadTimeout只负责可中断轮询，独立ReceiveIdleTimeoutMs
            // 使用单调时间判断；不会把一次串口Read超时当作线路故障。
            SerialPort port=new SerialPort(_config.PortName,_config.BaudRate,
                parity,_config.DataBits,stopBits) {
                ReadTimeout=250,WriteTimeout=_config.SendTimeoutMs
            };
            try
            {
                port.Open();
                _port=port;
                _stop=new CancellationTokenSource();
                StartBufferedReceive(_config);
                _reader=new Thread(ReadLoop) {
                    IsBackground=true,Name="FTR.Receive."+Id
                };
                _reader.Start();
                return Task.CompletedTask;
            }
            catch
            {
                port.Dispose();
                throw;
            }
        }

        private void ReadLoop()
        {
            CancellationToken ct=_stop.Token;
            Stopwatch idle=Stopwatch.StartNew();
            EnterReceiveThread();
            try
            {
                SerialPort port=_port;
                while(!ct.IsCancellationRequested)
                {
                    FTRReceiveBuffer buffer=null;
                    try
                    {
                        buffer=RentReceiveBuffer(ct);
                        int count;
                        try
                        {
                            count=port.Read(buffer.WritableBuffer,0,buffer.WritableBuffer.Length);
                        }
                        catch(TimeoutException)
                        {
                            if(_config.ReceiveIdleTimeoutMs>0 &&
                                idle.ElapsedMilliseconds>=_config.ReceiveIdleTimeoutMs)
                                throw new IOException("Serial receive idle deadline",
                                    new TimeoutException("Serial receive idle"));
                            continue;
                        }
                        if(count<1)continue;
                        idle.Restart();
                        // Buffer租约接纳后由FTR的唯一Deliver线程发布；
                        // 慢消费者使池阻塞而非积累无界数组/丢可靠字节。
                        PublishReceiveBuffer(new FTRSubChannelAddress(Id),buffer,count,
                            "serial:"+_config.PortName);
                        buffer=null;
                    }
                    finally{if(buffer!=null)buffer.Dispose();}
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){}
            catch(Exception error){if(!ct.IsCancellationRequested)Fault(error);}
        }

        protected override void OnSendSynchronous(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,CancellationToken ct,int segmentBytes)
        {
            if(address.HasSubChannel)
                throw new ArgumentException("RS-485站号属于Protocol，非物理SubChannel");
            FTRDeadline deadline=new FTRDeadline(_config.SendTimeoutMs);
            bool entered=false;
            try
            {
                while(!entered)
                {
                    ct.ThrowIfCancellationRequested();
                    entered=Monitor.TryEnter(_writer,Math.Min(20,deadline.RemainingMs()));
                }
                ct.ThrowIfCancellationRequested();
                SerialPort port=_port;
                if(port==null || !port.IsOpen)
                    throw new IOException("Serial通道未打开");
                // 写入中断意味着物理字节可能已部分到达，绝不自动重放。
                using(ct.Register(()=>
                {
                    try{port.Close();}
                    catch(Exception){}
                }))
                {
                    int chunk=segmentBytes>0?segmentBytes:Math.Min(8192,count);
                    for(int done=0;done<count;)
                    {
                        ct.ThrowIfCancellationRequested();
                        port.WriteTimeout=deadline.RemainingMs();
                        int n=Math.Min(chunk,count-done);
                        port.Write(bytes,offset+done,n);
                        done+=n;
                    }
                }
            }
            catch
            {
                ct.ThrowIfCancellationRequested();
                throw;
            }
            finally{if(entered)Monitor.Exit(_writer);}
        }

        protected override Task OnSendAsync(byte[] bytes,FTRSubChannelAddress address,
            CancellationToken ct)
        {
            // FTRChannelBase已在入场时冻结唯一快照并限制字节/数量配额。
            return Task.Run(()=>OnSendSynchronous(bytes,0,bytes.Length,address,ct,0),ct);
        }

        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(()=>
            {
                Stopwatch time=Stopwatch.StartNew();
                CancellationTokenSource stop=_stop;
                if(stop!=null)stop.Cancel();
                SerialPort port=_port;
                if(port!=null)port.Close();
                FTRTransportThread.Join(_reader,time,_config.StopTimeoutMs,ct);
                WaitForPendingSends(FTRTransportThread.Remaining(time,_config.StopTimeoutMs),ct);
                DrainBufferedReceive(FTRTransportThread.Remaining(time,_config.StopTimeoutMs),ct);
                if(stop!=null)stop.Dispose();
                if(port!=null)port.Dispose();
                _port=null;_stop=null;_reader=null;
            });
        }
    }
}
