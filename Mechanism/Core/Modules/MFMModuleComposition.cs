using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Mechanism.Core.Modules
{
    // 只是可校验的模块组件引用视图，不创建第二份 MEM 工程树或持久化真源。
    public enum MFMComponentRole { Channel, Protocol, PointTable, VariableScope, Capability, Resource }

    public sealed class MFMComponentReference
    {
        public MFMComponentReference(MFMComponentRole role, string referenceId, bool owned = false)
        {
            if (!Enum.IsDefined(typeof(MFMComponentRole), role)) throw new ArgumentOutOfRangeException("role");
            if (string.IsNullOrWhiteSpace(referenceId)) throw new ArgumentException("组件引用不能为空", "referenceId");
            Role = role; ReferenceId = referenceId.Trim(); Owned = owned;
        }
        public MFMComponentRole Role { get; private set; }
        public string ReferenceId { get; private set; }
        public bool Owned { get; private set; }
    }

    public sealed class MFMModuleComposition
    {
        private readonly ReadOnlyCollection<MFMComponentReference> _references;
        public MFMModuleComposition(string moduleId, IEnumerable<MFMComponentReference> references)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("模块标识不能为空", "moduleId");
            List<MFMComponentReference> items = new List<MFMComponentReference>(references ?? Enumerable.Empty<MFMComponentReference>());
            if (items.Any(item => item == null)) throw new ArgumentException("组件引用不能为空");
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (MFMComponentReference item in items)
                if (!keys.Add(item.Role + ":" + item.ReferenceId)) throw new ArgumentException("模块组件引用重复: " + item.ReferenceId);
            ModuleId = moduleId.Trim();
            _references = new ReadOnlyCollection<MFMComponentReference>(items);
        }
        public string ModuleId { get; private set; }
        public IReadOnlyList<MFMComponentReference> References { get { return _references; } }
        public IReadOnlyList<MFMComponentReference> Find(MFMComponentRole role)
        {
            return _references.Where(item => item.Role == role).ToArray();
        }
    }

    // 无状态计算保持普通 C# 方法即可；需要公开为单个类型化操作时可选择本合同。
    public interface IMFMComputeOperation<TInput, TOutput>
    {
        TOutput Execute(TInput input);
    }
}
