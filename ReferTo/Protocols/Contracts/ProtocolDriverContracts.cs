using System.Collections.Generic;
using CxVisual.Abstractions;

namespace CxVisual.Platform.Protocols
{
    /// <summary>传统点表驱动与新 Provider 共用的最小平台边界。</summary>
    public interface IProtocolDriverModule : ICapabilityProvider
    {
        long Id { get; }
        string Name { get; }
        string TypeName { get; }
        void StartReadDynamicProperty();
        void StopReadDynamicProperty();
    }

    public static class ProtocolDriverCapabilityIds
    {
        public const string Lifecycle = "protocol.driver.lifecycle";
        public const string CyclicRead = "protocol.driver.cyclic-read";
        public const string PointRead = "protocol.driver.point-read";
        public const string PointWrite = "protocol.driver.point-write";
        public const string VariableBinding = "protocol.driver.variable-binding";
    }

    internal static class ProtocolDriverCapabilities
    {
        internal static IReadOnlyCollection<CapabilityDescriptor> Standard { get; } = new[]
        {
            new CapabilityDescriptor(ProtocolDriverCapabilityIds.Lifecycle),
            new CapabilityDescriptor(ProtocolDriverCapabilityIds.CyclicRead),
            new CapabilityDescriptor(ProtocolDriverCapabilityIds.PointRead),
            new CapabilityDescriptor(ProtocolDriverCapabilityIds.PointWrite),
            new CapabilityDescriptor(ProtocolDriverCapabilityIds.VariableBinding)
        };
    }
}
