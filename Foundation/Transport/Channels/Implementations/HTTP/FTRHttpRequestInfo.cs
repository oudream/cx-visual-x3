namespace CxVisual.Foundation.Transport
{
    /// <summary>HTTP传输级请求元数据，不保存大请求正文，不属于业务路由。</summary>
    public sealed class FTRHttpRequestInfo
    {
        public FTRSubChannelAddress Address {get;private set;}
        public string Method {get;private set;}
        public string Path {get;private set;}
        public string ContentType {get;private set;}
        public int ContentBytes {get;private set;}
        public FTRHttpRequestInfo(FTRSubChannelAddress address,string method,string path,string contentType,int contentBytes)
        {Address=address;Method=method;Path=path;ContentType=contentType;ContentBytes=contentBytes;}
    }
}
