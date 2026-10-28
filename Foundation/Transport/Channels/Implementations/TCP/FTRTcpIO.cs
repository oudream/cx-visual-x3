using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace CxVisual.Foundation.Transport
{
    internal static class FTRTcpIO
    {
        // 发送锁只保护单连接的字节顺序，接收从不持有它；超时覆盖排队和所有分块。
        internal static void Write(TcpClient client,NetworkStream stream,object gate,byte[] bytes,int offset,int count,
            int segmentBytes,int timeoutMilliseconds,CancellationToken cancellation)
        {
            var deadline=new FTRDeadline(timeoutMilliseconds);bool taken=false;
            try
            {
                while(!taken)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int remaining=deadline.RemainingMs();
                    taken=Monitor.TryEnter(gate,Math.Min(20,remaining));
                }
                cancellation.ThrowIfCancellationRequested();
                using(cancellation.Register(()=>client.Close()))
                {
                    int chunk=segmentBytes==0?count:segmentBytes;
                    for(int done=0;done<count;)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        stream.WriteTimeout=deadline.RemainingMs();
                        int n=Math.Min(chunk,count-done);stream.Write(bytes,offset+done,n);done+=n;
                    }
                }
            }
            catch{cancellation.ThrowIfCancellationRequested();throw;}
            finally{if(taken)Monitor.Exit(gate);}
        }
        internal static int Remaining(Stopwatch timer,int timeout)
        { return FTRTransportThread.Remaining(timer, timeout); }
        internal static void Join(Thread thread,Stopwatch timer,int timeout,CancellationToken cancellation)
        {
            FTRTransportThread.Join(thread, timer, timeout, cancellation);
        }
    }
}
