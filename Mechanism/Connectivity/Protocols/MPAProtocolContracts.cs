namespace CxVisual.Mechanism.Connectivity
{
    // 与现有 IMPAProtocolCodec 协作；正式规约直接实现本接口，不使用旧接口 Adapter。
    public interface IMPAProtocolDefinition
    {
        string Id { get; }
        string RequiredChannelKind { get; }
        IMPAProtocolCodec Codec { get; }
    }
}
