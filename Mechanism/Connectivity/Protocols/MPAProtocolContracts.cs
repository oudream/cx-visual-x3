namespace CxVisual.Mechanism.Connectivity
{
    // Protocol 是完整的数据解释/读写/交互能力集合，不限于基本 Codec。
    // RequiredIO/子通道配置归 Descriptor + Binding；不再要求单一 RequiredChannelKind 字符串。
    public interface IMPAProtocolDefinition
    {
        string Id { get; }
        MPAProtocolDescriptor Descriptor { get; }
    }

    // 编解码是可选规约能力：数据/命令规约未必经过字节 Frame。
    public interface IMPAEncodedProtocolDefinition : IMPAProtocolDefinition
    {
        IMPAProtocolCodec Codec { get; }
    }
}
