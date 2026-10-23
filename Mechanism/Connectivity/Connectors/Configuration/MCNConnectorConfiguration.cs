using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Mechanism.Connectivity
{
    // 工程资源引用的可校验配置视图；真实保存/发布归 MEM/MHS，运行装配归 Host。
    public sealed class MCNConnectorConfiguration
    {
        private readonly ReadOnlyCollection<MCCChannelConfiguration> _channels;
        private readonly ReadOnlyCollection<MPAProtocolBinding> _protocols;
        private readonly ReadOnlyCollection<MCNPointBinding> _bindings;
        public MCNConnectorConfiguration(string moduleId,
            IEnumerable<MCCChannelConfiguration> channels,
            IEnumerable<MPAProtocolBinding> protocols,
            MCNPointTable pointTable = null, IEnumerable<MCNPointBinding> bindings = null)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("模块 Id 必填");
            ModuleId = moduleId.Trim();
            _channels = new ReadOnlyCollection<MCCChannelConfiguration>(
                new List<MCCChannelConfiguration>(channels ?? Enumerable.Empty<MCCChannelConfiguration>()));
            _protocols = new ReadOnlyCollection<MPAProtocolBinding>(
                new List<MPAProtocolBinding>(protocols ?? Enumerable.Empty<MPAProtocolBinding>()));
            _bindings = new ReadOnlyCollection<MCNPointBinding>(
                new List<MCNPointBinding>(bindings ?? Enumerable.Empty<MCNPointBinding>()));
            PointTable = pointTable;
            Validate();
        }
        public string ModuleId { get; private set; }
        public IReadOnlyList<MCCChannelConfiguration> Channels { get { return _channels; } }
        public IReadOnlyList<MPAProtocolBinding> Protocols { get { return _protocols; } }
        public MCNPointTable PointTable { get; private set; }
        public IReadOnlyList<MCNPointBinding> PointBindings { get { return _bindings; } }

        public void Validate()
        {
            HashSet<string> channelIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MCCChannelConfiguration item in _channels)
            {
                if (item == null) throw new ArgumentException("Channel 配置不能为 null");
                item.Validate();
                if (!channelIds.Add(item.Identity.Id)) throw new ArgumentException("Channel Id 重复: " + item.Identity.Id);
            }
            HashSet<string> protocolIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MPAProtocolBinding protocol in _protocols)
            {
                if (protocol == null) throw new ArgumentException("Protocol Binding 不能为 null");
                protocol.Validate();
                if (!protocolIds.Add(protocol.Id)) throw new ArgumentException("Protocol Binding Id 重复: " + protocol.Id);
                if (protocol.RequiresChannel && !channelIds.Contains(protocol.ChannelRef))
                    throw new ArgumentException("规约引用的 Channel 不存在: " + protocol.ChannelRef);
            }
            HashSet<string> bindKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (MCNPointBinding binding in _bindings)
            {
                if (binding == null) throw new ArgumentException("Point Binding 不能为 null");
                if (PointTable == null || PointTable.Find(binding.PointName) == null)
                    throw new ArgumentException("点绑定引用了不存在的点: " + binding.PointName);
                string key = binding.PointName + ":" + binding.TargetKind + ":" + binding.TargetRef;
                if (!bindKeys.Add(key)) throw new ArgumentException("点绑定重复: " + key);
            }
        }

        // 只有取得当前规约类型能力描述后才能做完整兼容检查；不可把“类型未提供”当成验证成功。
        public void ValidateProtocolCapabilities(IReadOnlyDictionary<string, MPAProtocolDescriptor> descriptions)
        {
            if (descriptions == null) throw new ArgumentNullException("descriptions");
            Validate();
            Dictionary<string, MCCChannelTypeDescriptor> channels =
                _channels.ToDictionary(item => item.Identity.Id, item => item.Type, StringComparer.Ordinal);
            foreach (MPAProtocolBinding binding in _protocols)
            {
                MPAProtocolDescriptor description;
                if (!descriptions.TryGetValue(binding.ProtocolTypeId, out description) || description == null)
                    throw new InvalidOperationException("规约类型尚未注册/解析: " + binding.ProtocolTypeId);
                MCCChannelTypeDescriptor channel = null;
                if (binding.RequiresChannel) channels.TryGetValue(binding.ChannelRef, out channel);
                binding.ValidateCompatibility(description, channel);
            }
        }
    }
}
