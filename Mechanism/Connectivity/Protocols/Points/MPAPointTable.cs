using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Connectivity
{
    // 点描述设备/规约寻址，不保存 MDS 实时值；同一地址可映射多个数据目标。
    public sealed class MPAPointDefinition
    {
        [FCMConfigItem("点名称", Order = 1)] [Required]
        public string Name { get; set; } = "point01";
        [FCMConfigItem("规约地址", Order = 2)] [Required]
        public string Address { get; set; } = "holding:100";
        [FCMConfigItem("规约站号", Order = 3)]
        public string StationAddress { get; set; } = "";
        [FCMConfigItem("值类型", Order = 4)] [Required]
        public string ValueType { get; set; } = "UInt16";
        [FCMConfigItem("可读取", Order = 5)]
        public bool CanRead { get; set; } = true;
        [FCMConfigItem("可写入", Order = 6)]
        public bool CanWrite { get; set; }

        public MPAPointDefinition() { }
        public MPAPointDefinition(string name, string address, Type type)
        {
            if (type == null || type == typeof(void)) throw new ArgumentException("点值类型无效");
            Name = name; Address = address; ValueType = type.Name;
            Validate();
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Address) ||
                string.IsNullOrWhiteSpace(ValueType)) throw new ArgumentException("点名称、地址和类型必须有效");
        }
    }

    // 点表运行索引，不是第二份配置真源；配置真源在 MPAProtocolConfiguration.Points。
    public sealed class MPAPointTable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MPAPointDefinition> _points =
            new Dictionary<string, MPAPointDefinition>(StringComparer.Ordinal);
        public MPAPointTable(IEnumerable<MPAPointDefinition> points = null)
        {
            if (points != null) foreach (MPAPointDefinition point in points) Add(point);
        }
        public void Add(MPAPointDefinition point)
        {
            if (point == null) throw new ArgumentNullException("point");
            point.Validate();
            lock (_gate)
            {
                if (_points.ContainsKey(point.Name)) throw new ArgumentException("点重复：" + point.Name);
                _points.Add(point.Name, point);
            }
        }
        public MPAPointDefinition Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            lock (_gate) { MPAPointDefinition p; return _points.TryGetValue(name, out p) ? p : null; }
        }
        public bool Remove(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_gate) return _points.Remove(name);
        }
        public IReadOnlyList<MPAPointDefinition> Snapshot()
        {
            lock (_gate) return new ReadOnlyCollection<MPAPointDefinition>(_points.Values.ToArray());
        }
    }
}
