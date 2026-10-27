using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>独立阻塞接收线程、有界交付线程和同步有序发送；异步发送复用相同Write。</summary>
    public sealed class FTRTcpClientChannel : FTRSingleChannelBase
    {
        private readonly FTRTcpClientChannelConfiguration _config;
        private readonly object _write=new object();
        private TcpClient _tcp;
        private NetworkStream _stream;
        private CancellationTokenSource _shutdown;
        private Thread _receiver;
        public FTRTcpClientChannel(FTRChannelConfiguration configuration)
            :base((configuration??throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            _config=configuration as FTRTcpClientChannelConfiguration??
                throw new ArgumentException("要求FTRTcpClientChannelConfiguration");
        }
        protected override async Task OnOpenAsync(CancellationToken ct)
        {
            var client=new TcpClient();
            try
            {
                using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    deadline.CancelAfter(_config.ConnectTimeoutMs);
                    try
                    {
                        using(deadline.Token.Register(()=>client.Close()))
                            await client.ConnectAsync(_config.Host,_config.Port).ConfigureAwait(false);
                        deadline.Token.ThrowIfCancellationRequested();
                    }
                    catch(Exception error)
                    {
                        ct.ThrowIfCancellationRequested();
                        if(deadline.IsCancellationRequested)throw new TimeoutException("TCP connect deadline",error);
                        throw;
                    }
                }
                client.NoDelay=true;client.SendTimeout=_config.SendTimeoutMs;
                client.ReceiveTimeout=_config.ReceiveIdleTimeoutMs;
                _tcp=client;_stream=client.GetStream();_shutdown=new CancellationTokenSource();
                StartBufferedReceive(_config);
                _receiver=new Thread(ReceiveLoop){IsBackground=true,Name="FTR.Receive."+Id};
                _receiver.Start();
            }
            catch{client.Close();throw;}
        }
        private void ReceiveLoop()
        {
            CancellationToken ct=_shutdown.Token;
            try
            {
                NetworkStream stream=_stream;
                while(!ct.IsCancellationRequested)
                {
                    FTRReceiveBuffer buffer=RentReceiveBuffer(ct);
                    try
                    {
                        int count=stream.Read(buffer.WritableBuffer,0,buffer.WritableBuffer.Length);
                        if(count==0)
                        {AfterReceived(()=>{if(!ct.IsCancellationRequested)Fault(new IOException("TCP peer closed"));});return;}
                        PublishReceiveBuffer(new FTRSubChannelAddress(Id),buffer,count);buffer=null;
                    }
                    finally{if(buffer!=null)buffer.Dispose();}
                }
            }
            catch(Exception error){if(!ct.IsCancellationRequested)Fault(error);}
        }
        protected override void OnSendSynchronous(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,CancellationToken ct,int segmentBytes)
        {
            if(address.HasSubChannel)throw new ArgumentException("TCP Client是一对一");
            TcpClient client=_tcp;if(client==null)throw new IOException("TCP not connected");
            FTRTcpIO.Write(client,_stream,_write,bytes,offset,count,segmentBytes,_config.SendTimeoutMs,ct);
        }
        protected override Task OnSendAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken ct)
        {return Task.Run(()=>OnSendSynchronous(bytes,0,bytes.Length,address,ct,0),ct);}
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(()=>
            {
                Stopwatch timer=Stopwatch.StartNew();
                if(_shutdown!=null)_shutdown.Cancel();
                if(_tcp!=null)_tcp.Close();
                FTRTcpIO.Join(_receiver,timer,_config.StopTimeoutMs,ct);
                WaitForPendingSends(FTRTcpIO.Remaining(timer,_config.StopTimeoutMs),ct);
                DrainBufferedReceive(FTRTcpIO.Remaining(timer,_config.StopTimeoutMs),ct);
                if(_shutdown!=null)_shutdown.Dispose();
                _receiver=null;_shutdown=null;_tcp=null;_stream=null;
            });
        }
    }
}
