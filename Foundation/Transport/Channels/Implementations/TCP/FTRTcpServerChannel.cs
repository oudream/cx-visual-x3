using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>一个Select接收线程服务多客户端，不按连接建立Thread/Task；解析留给后级。</summary>
    public sealed class FTRTcpServerChannel : FTRMultiChannelBase
    {
        private sealed class Peer
        {
            internal TcpClient Client;
            internal NetworkStream Stream;
            internal FTRSubChannelAddress Address;
            internal readonly object Write=new object();
            internal bool Ended;
            internal long LastReceive=Stopwatch.GetTimestamp();
        }
        private readonly FTRTcpServerChannelConfiguration _config;
        private readonly object _gate=new object();
        private readonly Dictionary<string,Peer> _clients=new Dictionary<string,Peer>(StringComparer.Ordinal);
        private TcpListener _listener;
        private CancellationTokenSource _shutdown;
        private Thread _receiver;
        public FTRTcpServerChannel(FTRChannelConfiguration configuration)
            :base((configuration??throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();_config=configuration as FTRTcpServerChannelConfiguration??
                throw new ArgumentException("要求FTRTcpServerChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();_shutdown=new CancellationTokenSource();
            _listener=new TcpListener(FTRNetworkAddress.Resolve(_config.Host),_config.Port);
            _listener.Start(_config.MaxClients);
            StartBufferedReceive(_config);
            _receiver=new Thread(ReceiveLoop){IsBackground=true,Name="FTR.Receive."+Id};
            _receiver.Start();return Task.CompletedTask;
        }
        private void ReceiveLoop()
        {
            CancellationToken ct=_shutdown.Token;int cursor=0;
            EnterReceiveThread();
            try
            {
                while(!ct.IsCancellationRequested)
                {
                    Peer[] all;lock(_gate)all=_clients.Values.Where(p=>!p.Ended).ToArray();
                    // Windows Select集合有规模限制；按批轮转，每批仍检查监听Socket。
                    Peer[] batch=all.Length<=1024?all:all.Skip(cursor).Take(1024).ToArray();
                    cursor=all.Length==0?0:(cursor+batch.Length)%all.Length;
                    Socket listenerSocket=_listener.Server;
                    var readable=new List<Socket>{listenerSocket};
                    foreach(Peer peer in batch)
                        if(peer.Client.Client!=null)readable.Add(peer.Client.Client);
                    try{Socket.Select(readable,null,null,all.Length>1024?1000:50000);}
                    catch(SocketException)when(!ct.IsCancellationRequested){continue;}
                    catch(ObjectDisposedException)when(!ct.IsCancellationRequested){continue;}
                    if(ct.IsCancellationRequested)break;
                    if(readable.Contains(listenerSocket))AcceptPeer();
                    foreach(Peer peer in batch)
                    {
                        if(ct.IsCancellationRequested)break;
                        Socket socket=peer.Client.Client;
                        if(socket==null){EndPeer(peer,new IOException("TCP peer socket closed"));continue;}
                        if(!readable.Contains(socket))
                        {
                            if(_config.ReceiveIdleTimeoutMs>0&&
                                (Stopwatch.GetTimestamp()-peer.LastReceive)*1000.0/Stopwatch.Frequency>
                                    _config.ReceiveIdleTimeoutMs)EndPeer(peer,new TimeoutException("TCP receive idle"));
                            continue;
                        }
                        FTRReceiveBuffer buffer=null;
                        try
                        {
                            buffer=RentReceiveBuffer(ct);
                            int count=socket.Receive(buffer.WritableBuffer,0,
                                buffer.WritableBuffer.Length,SocketFlags.None);
                            if(count==0){EndPeer(peer,null);continue;}
                            peer.LastReceive=Stopwatch.GetTimestamp();
                            PublishReceiveBuffer(peer.Address,buffer,count);buffer=null;
                        }
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
                        catch(Exception error){EndPeer(peer,error);}
                        finally{if(buffer!=null)buffer.Dispose();}
                    }
                }
            }
            catch(Exception error){if(!ct.IsCancellationRequested)Fault(error);}
        }
        private void AcceptPeer()
        {
            TcpClient client=_listener.AcceptTcpClient();
            try
            {
                client.NoDelay=true;client.SendTimeout=_config.SendTimeoutMs;
                NetworkStream stream=client.GetStream();
                string endpoint=client.Client.RemoteEndPoint.ToString();
                lock(_gate)
                {
                    if(_clients.Count>=_config.MaxClients)
                        throw new IOException("TCP MaxClients reached; peer rejected before admission");
                    var address=RegisterPeer(Guid.NewGuid().ToString("N"),endpoint).Address;
                    _clients.Add(address.ToString(),new Peer{Client=client,Stream=stream,Address=address});
                }
            }
            catch(Exception error){client.Close();RecordReceiveError(error);}
        }
        private void EndPeer(Peer peer,Exception error)
        {
            lock(_gate){if(peer.Ended)return;peer.Ended=true;}
            if(error!=null)RecordReceiveError(error,FTRFaultScope.SubChannel,peer.Address);
            // 先交付已接纳字节，再通知唯一Protocol清理；终止通知不会越过旧半包。
            AfterReceived(()=>RemovePeer(peer.Address),_shutdown.Token);
        }
        public override bool RemovePeer(FTRSubChannelAddress address)
        {
            if(address==null)return false;
            Peer peer;
            lock(_gate)
            {
                if(_clients.TryGetValue(address.ToString(),out peer))
                {peer.Ended=true;_clients.Remove(address.ToString());}
            }
            if(peer!=null)peer.Client.Close();
            return base.RemovePeer(address);
        }
        protected override void OnSendSynchronous(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,CancellationToken ct,int segmentBytes)
        {
            if(FindSubChannel(address)==null)throw new InvalidOperationException("Stale SubChannel");
            Peer peer;lock(_gate)
                if(!_clients.TryGetValue(address.ToString(),out peer))
                    throw new IOException("TCP peer disconnected");
            FTRTcpIO.Write(peer.Client,peer.Stream,peer.Write,bytes,offset,count,segmentBytes,_config.SendTimeoutMs,ct);
        }
        protected override Task SendToPeerAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken ct)
        {return Task.Run(()=>OnSendSynchronous(bytes,0,bytes.Length,address,ct,0),ct);}
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(()=>
            {
                Stopwatch timer=Stopwatch.StartNew();
                if(_shutdown!=null)_shutdown.Cancel();
                if(_listener!=null)_listener.Stop();
                Peer[] peers;lock(_gate)peers=_clients.Values.ToArray();
                foreach(Peer peer in peers)peer.Client.Close();
                FTRTcpIO.Join(_receiver,timer,_config.StopTimeoutMs,ct);
                WaitForPendingSends(FTRTcpIO.Remaining(timer,_config.StopTimeoutMs),ct);
                DrainBufferedReceive(FTRTcpIO.Remaining(timer,_config.StopTimeoutMs),ct);
                foreach(Peer peer in peers)RemovePeer(peer.Address);
                if(_shutdown!=null)_shutdown.Dispose();
                _receiver=null;_shutdown=null;_listener=null;
            });
        }
    }
}
