using System;
using System.IO;
using System.Net.Sockets;
using System.Security;

namespace CxVisual.Foundation.Transport
{
    /// <summary>仅解释传输异常事实，不代替恢复策略或Protocol判断业务请求能否重试。</summary>
    internal static class FTRFailureClassifier
    {
        internal static FTRFailureInfo Classify(Exception error,string stage,FTRFaultScope scope)
        {
            if(error==null)throw new ArgumentNullException(nameof(error));
            Exception current=error;
            for(int depth=0;depth<6;depth++)
            {
                var aggregate=current as AggregateException;
                if(aggregate!=null && aggregate.InnerExceptions.Count==1)
                    {current=aggregate.InnerException;continue;}
                var io=current as IOException;
                if(io!=null && io.InnerException!=null)
                    {current=io.InnerException;continue;}
                break;
            }
            FTRFailureKind kind=FTRFailureKind.Unknown;
            if(current is OperationCanceledException)kind=FTRFailureKind.Cancelled;
            else if(current is TimeoutException)kind=FTRFailureKind.Timeout;
            else if(current is UnauthorizedAccessException || current is SecurityException)
                kind=FTRFailureKind.PermissionDenied;
            else if(current is ArgumentException || current is NotSupportedException)
                kind=FTRFailureKind.InvalidConfiguration;
            else if(current is OutOfMemoryException)
                kind=FTRFailureKind.ResourceExhausted;
            else if(current is SocketException)
            {
                switch(((SocketException)current).SocketErrorCode)
                {
                    case SocketError.ConnectionAborted:
                    case SocketError.ConnectionReset:
                    case SocketError.Disconnecting:
                    case SocketError.Shutdown:
                    case SocketError.NotConnected:
                        kind=FTRFailureKind.RemoteClosed;break;
                    case SocketError.TimedOut:
                        kind=FTRFailureKind.Timeout;break;
                    case SocketError.ConnectionRefused:
                    case SocketError.NetworkDown:
                    case SocketError.NetworkUnreachable:
                    case SocketError.HostUnreachable:
                    case SocketError.HostDown:
                        kind=FTRFailureKind.NetworkUnavailable;break;
                    default:kind=FTRFailureKind.TransportIO;break;
                }
            }
            else if(current is EndOfStreamException)
                kind=FTRFailureKind.RemoteClosed;
            else if(current is IOException)
                kind=FTRFailureKind.TransportIO;
            return new FTRFailureInfo(kind,scope,string.IsNullOrWhiteSpace(stage)?"Transport":stage,
                // 不持有Exception引用；消息只用于诊断，不作为业务协议的输入。
                error.Message.Length>1024 ? error.Message.Substring(0,1024) : error.Message);
        }
    }
}
