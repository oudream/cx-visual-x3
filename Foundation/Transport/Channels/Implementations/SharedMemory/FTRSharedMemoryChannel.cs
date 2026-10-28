using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>跨进程命名MMF双向环；各端独立接收线程，同步发送有界背压。</summary>
    public sealed class FTRSharedMemoryChannel : FTRSingleChannelBase,IFTRSharedBufferIO
    {
        private readonly FTRSharedMemoryChannelConfiguration _config;
        private readonly object _write=new object();
        private FTRSharedMemoryRegion _region;
        private CancellationTokenSource _stop;
        private Thread _reader;
        public int SlotBytes {get{return _config.SlotBytes;}}
        public int SlotCount {get{return _config.Slots;}}
        public FTRSharedMemoryChannel(FTRChannelConfiguration config)
            :base((config??throw new ArgumentNullException("config")).Id)
        {
            config.Validate();_config=config as FTRSharedMemoryChannelConfiguration ??
                throw new ArgumentException("需要FTRSharedMemoryChannelConfiguration");
        }
        protected override async Task OnOpenAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            _region=new FTRSharedMemoryRegion(_config);
            _stop=new CancellationTokenSource();
            StartBufferedReceive(_config);
            FTRSharedMemoryRegion region=_region;
            var ready=new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            _reader=new Thread(()=>
            {
                bool owned=false;
                try
                {
                    region.AcquireSide();
                    owned=true;
                    ready.TrySetResult(null);
                    ReadLoop();
                }
                catch(Exception error)
                {
                    if(!owned)ready.TrySetResult(error);
                    else if(!_stop.IsCancellationRequested)Fault(error);
                }
                finally {if(owned)region.ReleaseSide();}
            }){IsBackground=true,Name="FTR.SharedMemory.Receive."+Id};
            _reader.Start();
            Task settled=await Task.WhenAny(ready.Task,Task.Delay(3000,cancellation)).ConfigureAwait(false);
            if(settled!=ready.Task)
            {
                cancellation.ThrowIfCancellationRequested();
                throw new TimeoutException("共享内存端点独占锁获取未完成");
            }
            Exception startup=await ready.Task.ConfigureAwait(false);
            if(startup!=null)throw new InvalidOperationException("共享内存端点启动被拒绝",startup);
        }
        private void ReadLoop()
        {
            CancellationToken ct=_stop.Token;EnterReceiveThread();
            var region=_region;
            WaitHandle[] wait={region.Incoming(_config.Side),ct.WaitHandle};
            try
            {
                while(!ct.IsCancellationRequested)
                {
                    FTRReceiveBuffer buffer=RentReceiveBuffer(ct);
                    try
                    {
                        int count=region.TryRead(_config.Side,buffer.WritableBuffer,ct);
                        if(count>0)
                        {
                            region.SignalSpace(_config.Side);
                            PublishReceiveBuffer(new FTRSubChannelAddress(Id),buffer,count,"shm:"+_config.MapName);
                            buffer=null;
                            continue;
                        }
                    }
                    finally{if(buffer!=null)buffer.Dispose();}
                    if(WaitHandle.WaitAny(wait,50)==1)break;
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){}
            catch(Exception error){if(!ct.IsCancellationRequested)Fault(error);}
        }
        public void SendBuffer(byte[] bytes,int offset,int count,CancellationToken cancellation)
        {Send(bytes,offset,count,new FTRSubChannelAddress(Id),cancellation);}
        protected override void OnSendSynchronous(byte[] bytes,int offset,int count,
            FTRSubChannelAddress destination,CancellationToken cancellation,int segmentBytes)
        {
            if(destination.HasSubChannel)throw new ArgumentException("共享内存通道无子通道");
            var stop=_stop;var region=_region;
            if(stop==null || region==null)throw new InvalidOperationException("共享内存未打开");
            Stopwatch timer=Stopwatch.StartNew();
            lock(_write)
            {
                for(int done=0;done<count;)
                {
                    int n=Math.Min(Math.Min(_config.SlotBytes,count-done),
                        segmentBytes>0?segmentBytes:_config.SlotBytes);
                    while(!region.TryWrite(_config.Side,bytes,offset+done,n,cancellation))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if(stop.IsCancellationRequested)throw new OperationCanceledException("共享内存通道正在停止");
                        int left=_config.SendTimeoutMs-(int)timer.ElapsedMilliseconds;
                        if(left<=0)throw new TimeoutException("共享内存环已满且远端未及时消费");
                        int result=WaitHandle.WaitAny(new WaitHandle[]{region.OutgoingSpace(_config.Side),
                            cancellation.WaitHandle,stop.Token.WaitHandle},Math.Min(50,left));
                        if(result==1)cancellation.ThrowIfCancellationRequested();
                        if(result==2)throw new OperationCanceledException("共享内存通道已停止");
                    }
                    region.SignalData(_config.Side);done+=n;
                }
            }
        }
        protected override Task OnSendAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken ct)
        {return Task.Run(()=>OnSendSynchronous(bytes,0,bytes.Length,address,ct,0),ct);}
        protected override Task OnCloseAsync(CancellationToken cancellation)
        {
            return Task.Run(()=>
            {
                var timer=Stopwatch.StartNew();var stop=_stop;var region=_region;
                if(stop!=null)stop.Cancel();
                if(region!=null)
                {
                    region.Incoming(_config.Side).Set();
                    region.OutgoingSpace(_config.Side).Set();
                }
                FTRTransportThread.Join(_reader,timer,_config.StopTimeoutMs,cancellation);
                WaitForPendingSends(FTRTransportThread.Remaining(timer,_config.StopTimeoutMs),cancellation);
                DrainBufferedReceive(FTRTransportThread.Remaining(timer,_config.StopTimeoutMs),cancellation);
                _reader=null;_region=null;_stop=null;
                if(region!=null)region.Dispose();if(stop!=null)stop.Dispose();
            });
        }
    }
}
