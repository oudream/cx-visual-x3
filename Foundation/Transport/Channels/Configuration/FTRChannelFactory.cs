using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CxVisual.Foundation.Transport
{
    /// <summary>F17唯一通道创建入口。注册按Host实例隔离；不打开设备，不通过配置反射加载代码。</summary>
    public sealed class FTRChannelFactory
    {
        private sealed class Registration : IDisposable
        {
            internal readonly FTRChannelFactory Owner;
            internal readonly IFTRChannelProvider Provider;
            internal readonly FTRChannelTypeDescriptor Descriptor;
            internal bool Removed;
            internal Registration(FTRChannelFactory owner, IFTRChannelProvider provider, FTRChannelTypeDescriptor descriptor)
            { Owner = owner; Provider = provider; Descriptor = descriptor; }
            public void Dispose()
            {
                lock (Owner._gate)
                {
                    if (Removed) return;
                    Removed = true;
                    Registration current;
                    if (Owner._providers.TryGetValue(Descriptor.TypeId, out current) && ReferenceEquals(current, this))
                        Owner._providers.Remove(Descriptor.TypeId);
                }
            }
        }
        private sealed class Binding
        {
            internal string Configuration;
            internal FTRChannelTypeDescriptor Descriptor;
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, Registration> _providers = new Dictionary<string, Registration>(StringComparer.Ordinal);
        // 弱键不延长通道寿命；注销只影响未来创建，已交付实例仍由调用者拥有。
        private readonly ConditionalWeakTable<IFTRChannelTransport, Binding> _created =
            new ConditionalWeakTable<IFTRChannelTransport, Binding>();

        public IDisposable Register(IFTRChannelProvider provider)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            FTRChannelTypeDescriptor descriptor = provider.Descriptor;
            if (descriptor == null) throw new ArgumentException("Provider必须声明真实能力");
            if (BuiltInDescriptors().Any(item => item.TypeId == descriptor.TypeId))
                throw new ArgumentException("不可覆盖内置通道类型");
            lock (_gate)
            {
                if (_providers.ContainsKey(descriptor.TypeId)) throw new ArgumentException("Provider TypeId重复");
                var entry = new Registration(this, provider, descriptor);
                _providers.Add(descriptor.TypeId, entry);
                return entry;
            }
        }
        public FTRChannelTypeDescriptor[] Discover()
        {
            lock (_gate) return BuiltInDescriptors().Concat(_providers.Values.Select(item => item.Descriptor))
                .OrderBy(item => item.TypeId, StringComparer.Ordinal).ToArray();
        }
        private static IEnumerable<FTRChannelTypeDescriptor> BuiltInDescriptors()
        {
            foreach (FTRChannelKind kind in Enum.GetValues(typeof(FTRChannelKind)))
                if (kind != FTRChannelKind.Custom) yield return FTRChannelConfigurations.Create(kind).DescribeType();
        }
        public FTRChannelTypeDescriptor Describe(FTRChannelConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            configuration.Validate();
            if (configuration.Kind != FTRChannelKind.Custom) return configuration.DescribeType();
            lock (_gate) return Find((FTRCustomChannelConfiguration)configuration).Descriptor;
        }
        private Registration Find(FTRCustomChannelConfiguration configuration)
        {
            Registration registration;
            if (!_providers.TryGetValue(configuration.CustomTypeId, out registration))
                throw new NotSupportedException("自定义Channel未注册Provider：" + configuration.CustomTypeId);
            return registration;
        }
        public IFTRChannelTransport Create(FTRChannelConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            // 使用正式FCM白名单转换器取得快照，Provider或调用方不能修改彼此配置。
            var codec = new FTRChannelConfigurationConverter();
            string serialized = codec.Write(configuration);
            FTRChannelConfiguration snapshot = codec.Read(serialized);
            string expectedId = snapshot.Id;
            if (snapshot.Kind != FTRChannelKind.Custom) return CreateBuiltIn(snapshot);
            Registration registration;
            lock (_gate) registration = Find((FTRCustomChannelConfiguration)snapshot);
            IFTRChannelTransport channel = registration.Provider.Create((FTRCustomChannelConfiguration)snapshot);
            if (channel == null) throw new InvalidOperationException("Provider未返回Channel");
            bool valid = channel.State == FTRChannelState.Closed && channel.Id == expectedId &&
                channel.Topology == registration.Descriptor.Topology;
            bool alreadyOwned;
            lock (_gate)
            {
                Binding old;
                alreadyOwned = _created.TryGetValue(channel, out old);
                if (!alreadyOwned && !registration.Removed && valid)
                {
                    _created.Add(channel, new Binding { Configuration = serialized, Descriptor = registration.Descriptor });
                    return channel;
                }
            }
            // Provider不得复用已交付实例；拒绝时不能Dispose另一调用者的存活通道。
            if (!alreadyOwned) channel.Dispose();
            throw new InvalidOperationException("Provider已注销、复用实例或返回的身份/状态/拓扑无效");
        }
        private static IFTRChannelTransport CreateBuiltIn(FTRChannelConfiguration configuration)
        {
            switch (configuration.Kind)
            {
                case FTRChannelKind.TcpClient: return new FTRTcpClientChannel(configuration);
                case FTRChannelKind.TcpServer: return new FTRTcpServerChannel(configuration);
                case FTRChannelKind.Udp: return new FTRUdpChannel(configuration);
                case FTRChannelKind.UdpServer: return new FTRUdpServerChannel(configuration);
                case FTRChannelKind.Serial: return new FTRSerialChannel(configuration);
                case FTRChannelKind.HttpClient: return new FTRHttpClientChannel(configuration);
                case FTRChannelKind.HttpServer: return new FTRHttpServerChannel(configuration);
                case FTRChannelKind.NamedPipeClient: return new FTRNamedPipeClientChannel(configuration);
                case FTRChannelKind.NamedPipeServer: return new FTRNamedPipeServerChannel(configuration);
                case FTRChannelKind.WebSocketClient: return new FTRWebSocketClientChannel(configuration);
                case FTRChannelKind.WebSocketServer: return new FTRWebSocketServerChannel(configuration);
                case FTRChannelKind.Replay: return new FTRReplayChannel(configuration);
                case FTRChannelKind.UdpMulticast: return new FTRUdpMulticastChannel(configuration);
                case FTRChannelKind.Virtual: throw new NotSupportedException("Virtual需通过CreateVirtualPair显式装配两个端点");
                default: throw new NotSupportedException("通道类型未实现");
            }
        }
        public FTRVirtualChannel[] CreateVirtualPair(FTRVirtualChannelConfiguration left, FTRVirtualChannelConfiguration right)
        {
            if (left == null || right == null) throw new ArgumentNullException("configuration");
            left.Validate(); right.Validate();
            return FTRVirtualChannel.CreatePair(left.Id, right.Id);
        }
        /// <summary>正式装配校验；自定义通道必须由本Factory创建且配置一致，不能自报TypeId冒充。</summary>
        public FTRChannelTypeDescriptor DescribeAttached(FTRChannelConfiguration configuration, IFTRChannel channel)
        {
            if (configuration == null || channel == null) throw new ArgumentNullException("configuration/channel");
            configuration.Validate();
            if (channel.Id != configuration.Id) throw new ArgumentException("通道实例与配置Id不匹配");
            if (configuration.Kind == FTRChannelKind.Custom)
            {
                Binding binding;
                var transport = channel as IFTRChannelTransport;
                lock (_gate)
                    if (transport == null || !_created.TryGetValue(transport, out binding) ||
                        binding.Configuration != new FTRChannelConfigurationConverter().Write(configuration))
                        throw new NotSupportedException("无本Factory的真实Provider创建凭据或配置已改变");
                return binding.Descriptor;
            }
            bool matches = false;
            switch (configuration.Kind)
            {
                case FTRChannelKind.Virtual: matches = channel is FTRVirtualChannel; break;
                case FTRChannelKind.TcpClient: matches = channel is FTRTcpClientChannel; break;
                case FTRChannelKind.TcpServer: matches = channel is FTRTcpServerChannel; break;
                case FTRChannelKind.Udp: matches = channel is FTRUdpChannel; break;
                case FTRChannelKind.UdpServer: matches = channel is FTRUdpServerChannel; break;
                case FTRChannelKind.Serial: matches = channel is FTRSerialChannel; break;
                case FTRChannelKind.HttpClient: matches = channel is FTRHttpClientChannel; break;
                case FTRChannelKind.HttpServer: matches = channel is FTRHttpServerChannel; break;
                case FTRChannelKind.NamedPipeClient: matches = channel is FTRNamedPipeClientChannel; break;
                case FTRChannelKind.NamedPipeServer: matches = channel is FTRNamedPipeServerChannel; break;
                case FTRChannelKind.WebSocketClient: matches = channel is FTRWebSocketClientChannel; break;
                case FTRChannelKind.WebSocketServer: matches = channel is FTRWebSocketServerChannel; break;
                case FTRChannelKind.Replay: matches = channel is FTRReplayChannel; break;
                case FTRChannelKind.UdpMulticast: matches = channel is FTRUdpMulticastChannel; break;
            }
            if (!matches) throw new NotSupportedException("通道实例与正式类型不匹配");
            return configuration.DescribeType();
        }
    }
}
