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
            Stopwatch timer=Stopwatch.StartNew();bool taken=false;
            try
            {
                while(!taken)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int remaining=Remaining(timer,timeoutMilliseconds);
                    taken=Monitor.TryEnter(gate,Math.Min(20,remaining));
                }
                cancellation.ThrowIfCancellationRequested();
                using(cancellation.Register(()=>client.Close()))
                {
                    int chunk=segmentBytes==0?count:segmentBytes;
                    for(int done=0;done<count;)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        stream.WriteTimeout=Remaining(timer,timeoutMilliseconds);
                        int n=Math.Min(chunk,count-done);stream.Write(bytes,offset+done,n);done+=n;
                    }
                }
            }
            catch{cancellation.ThrowIfCancellationRequested();throw;}
            finally{if(taken)Monitor.Exit(gate);}
        }
        internal static int Remaining(Stopwatch timer,int timeout)
        {
            long remaining=timeout-timer.ElapsedMilliseconds;
            if(remaining<=0)throw new TimeoutException("TCP send deadline (including queue wait)");
            return (int)remaining;
        }
        internal static void Join(Thread thread,Stopwatch timer,int timeout,CancellationToken cancellation)
        {
            if(thread==null)return;
            while(!thread.Join(0))
            {
                cancellation.ThrowIfCancellationRequested();
                thread.Join(Math.Min(20,Remaining(timer,timeout)));
            }
        }
    }
}
