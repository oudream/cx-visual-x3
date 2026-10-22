using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace CxVisual.Mechanism.Core.Modules
{
    // 四种模块是功能分类，不是四套执行器；普通函数无需创建模块实例。
    public enum MFMModuleKind { Connector, Compute, Simulation, Service }
    public sealed class MFMModuleDescriptor
    {
        public MFMModuleDescriptor(string id, string name, MFMModuleKind kind, int version = 1, IEnumerable<string> capabilityIds = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("模块身份不能为空");
            if (!Enum.IsDefined(typeof(MFMModuleKind), kind) || version < 1)
                throw new ArgumentOutOfRangeException("kind/version");
            List<string> ids = new List<string>(capabilityIds ?? Enumerable.Empty<string>());
            if (ids.Any(string.IsNullOrWhiteSpace) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                throw new ArgumentException("能力 Id 不能为空或重复");
            Id = id.Trim(); Name = name.Trim(); Kind = kind; Version = version;
            CapabilityIds = new ReadOnlyCollection<string>(ids);
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public MFMModuleKind Kind { get; private set; }
        public int Version { get; private set; }
        public IReadOnlyList<string> CapabilityIds { get; private set; }
    }
    // 可选：只对有状态或多能力模块要求实例；普通 C# 函数直接作为 MOC Capability。
    public interface IMFMModule { MFMModuleDescriptor Descriptor { get; } }
}
