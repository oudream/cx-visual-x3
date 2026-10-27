namespace CxVisual.Foundation.Transport
{
    /// <summary>Host显式注册的扩展工厂；仅创建未打开的新实例，不负责Connector或业务协议。</summary>
    public interface IFTRChannelProvider
    {
        FTRChannelTypeDescriptor Descriptor { get; }
        IFTRChannelTransport Create(FTRCustomChannelConfiguration configuration);
    }
}
