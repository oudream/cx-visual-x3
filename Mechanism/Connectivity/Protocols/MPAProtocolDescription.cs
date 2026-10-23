using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Mechanism.Connectivity
{
    // Protocol 的能力全集；一个规约不必实现全部能力。
    [Flags]
    public enum MPAProtocolCapabilities
    {
        None = 0, Interpret = 1, Read = 2, Write = 4, Send = 8,
        RequestResponse = 16, Subscribe = 32, Event = 64, Message = 128,
        Command = 256, Stream = 512, Route = 1024
    }
    public enum MPAProtocolProcessingMode { Inline, Serial, KeyedSerial, Parallel, Dedicated, Runtime }
    public enum MPAProtocolSessionScope { Binding, SubChannel, ProtocolAddress }

    public sealed class MPAProtocolDescriptor
    {
        public MPAProtocolDescriptor(string id, string name, int version,
            MPAProtocolCapabilities capabilities, MCCIOCapabilities requiredIO = MCCIOCapabilities.None)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || version < 1)
                throw new ArgumentException("规约身份/版本无效");
            if (capabilities == MPAProtocolCapabilities.None) throw new ArgumentException("规约至少应描述一项能力");
            Id = id.Trim(); Name = name.Trim(); Version = version;
            Capabilities = capabilities; RequiredIO = requiredIO;
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int Version { get; private set; }
        public MPAProtocolCapabilities Capabilities { get; private set; }
        public MCCIOCapabilities RequiredIO { get; private set; }
        public bool Supports(MPAProtocolCapabilities flag) { return (Capabilities & flag) == flag; }
    }

}
