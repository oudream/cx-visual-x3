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

    /// <summary>
    /// 自描述长度帧的真实规约定义：复用MPALengthFrameCodec，不另造收发或调度引擎。
    /// 仅声明LengthFrame协议能力，不冒充Modbus/TCP设备驱动。
    /// </summary>
    public sealed class MPALengthFrameDefinition : IMPAEncodedProtocolDefinition
    {
        public MPALengthFrameDefinition()
        {
            Descriptor = new MPAProtocolDescriptor("length.frame", "Length Frame + CRC16", 1,
                MPAProtocolCapabilities.Interpret | MPAProtocolCapabilities.Send |
                MPAProtocolCapabilities.Stream, MCCIOCapabilities.ByteStream);
            Codec = new MPALengthFrameCodec();
        }

        public string Id { get { return Descriptor.Id; } }
        public MPAProtocolDescriptor Descriptor { get; private set; }
        public IMPAProtocolCodec Codec { get; private set; }
    }
}
