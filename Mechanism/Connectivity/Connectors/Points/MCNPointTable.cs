using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Mechanism.Connectivity
{
    // Point 属于外部地址语义，不保存实时变量值；PointTable 可选，不是全部规约必经节点。
    public sealed class MCNPointTable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MCNPointDefinition> _items =
            new Dictionary<string, MCNPointDefinition>(StringComparer.Ordinal);

        public void Add(MCNPointDefinition point)
        {
            if (point == null) throw new ArgumentNullException("point");
            if (string.IsNullOrWhiteSpace(point.VariableName)) throw new ArgumentException("点变量名必填");
            lock (_gate)
            {
                if (_items.ContainsKey(point.VariableName)) throw new ArgumentException("点重复: " + point.VariableName);
                _items.Add(point.VariableName, point);
            }
        }
        public MCNPointDefinition Find(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName)) return null;
            lock (_gate)
            {
                MCNPointDefinition item;
                return _items.TryGetValue(variableName, out item) ? item : null;
            }
        }
        public bool Remove(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName)) return false;
            lock (_gate) return _items.Remove(variableName);
        }
        public IReadOnlyList<MCNPointDefinition> Snapshot()
        {
            lock (_gate) return new ReadOnlyCollection<MCNPointDefinition>(_items.Values.ToList());
        }
    }

    public enum MCNPointTargetKind { Variable, FlowHeap, FlowSlot, Message, Command, ProfessionalData }

    public sealed class MCNPointBinding
    {
        public MCNPointBinding(string pointName, MCNPointTargetKind targetKind, string targetRef)
        {
            if (string.IsNullOrWhiteSpace(pointName) || string.IsNullOrWhiteSpace(targetRef))
                throw new ArgumentException("Point/DataTarget 引用不能为空");
            if (!Enum.IsDefined(typeof(MCNPointTargetKind), targetKind))
                throw new ArgumentOutOfRangeException("targetKind");
            PointName = pointName.Trim(); TargetKind = targetKind; TargetRef = targetRef.Trim();
        }
        public string PointName { get; private set; }
        public MCNPointTargetKind TargetKind { get; private set; }
        public string TargetRef { get; private set; }
    }
}
