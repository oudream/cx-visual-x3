using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Professional.Automation.DeviceVariables
{
    // x2 DeviceVariableCatalog的强类型精简迁移：只管理静态源映射，不存运行Current。
    public sealed class PADDeviceVariableDefinition
    {
        public PADDeviceVariableDefinition(string stableId, string deviceName, string variableName,
            Type valueType, string unit = null, bool writable = false)
        {
            if (string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(deviceName) ||
                string.IsNullOrWhiteSpace(variableName) || valueType == null || valueType == typeof(void))
                throw new ArgumentException("工业变量定义缺少稳定ID、设备、名称或类型");
            if (deviceName.IndexOf('.') >= 0 || variableName.IndexOf('.') >= 0)
                throw new ArgumentException("设备名与变量名不得包含点分隔符");
            StableId = stableId.Trim(); DeviceName = deviceName.Trim(); VariableName = variableName.Trim();
            ValueType = valueType; Unit = unit; Writable = writable;
        }
        public string StableId { get; private set; }
        public string DeviceName { get; private set; }
        public string VariableName { get; private set; }
        public Type ValueType { get; private set; }
        public string Unit { get; private set; }
        public bool Writable { get; private set; }
        public string Address { get { return DeviceName + "." + VariableName; } }
    }

    public sealed class PADDeviceVariableCatalog
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, PADDeviceVariableDefinition> _ids =
            new Dictionary<string, PADDeviceVariableDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, PADDeviceVariableDefinition> _addresses =
            new Dictionary<string, PADDeviceVariableDefinition>(StringComparer.Ordinal);

        public PADDeviceVariableCatalog(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("ProviderId不能为空");
            ProviderId = providerId.Trim();
        }
        public string ProviderId { get; private set; }

        public void Register(PADDeviceVariableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            lock (_gate)
            {
                if (_ids.ContainsKey(definition.StableId))
                    throw new InvalidOperationException("工业变量稳定ID重复: " + definition.StableId);
                if (_addresses.ContainsKey(definition.Address))
                    throw new InvalidOperationException("工业变量地址重复: " + definition.Address);
                _ids.Add(definition.StableId, definition);
                _addresses.Add(definition.Address, definition);
            }
        }
        public PADDeviceVariableDefinition FindById(string id)
        {
            if (id == null) return null;
            lock (_gate)
            {
                PADDeviceVariableDefinition result;
                return _ids.TryGetValue(id, out result) ? result : null;
            }
        }
        public PADDeviceVariableDefinition Find(string deviceName, string variableName)
        {
            if (deviceName == null || variableName == null) return null;
            lock (_gate)
            {
                PADDeviceVariableDefinition result;
                return _addresses.TryGetValue(deviceName + "." + variableName, out result) ? result : null;
            }
        }
        public PADDeviceVariableDefinition[] Snapshot()
        {
            lock (_gate) return _ids.Values.OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray();
        }

        // 工程定义仍由既有MDS Table管理；此方法只负责单向装配，不建立第二套Value仓。
        public void InstallDefinitions(MDSVariableTable table)
        {
            if (table == null) throw new ArgumentNullException("table");
            foreach (PADDeviceVariableDefinition item in Snapshot())
            {
                if (table.Contains(item.Address))
                {
                    MDSVariableDefinition existing = table.Definitions().First(x => x.Name == item.Address);
                    if (existing.VariableId != item.StableId || existing.ValueType != item.ValueType ||
                        existing.Writable != item.Writable)
                        throw new InvalidOperationException("工业变量与既有工程定义冲突: " + item.Address);
                    continue;
                }
                table.Define(new MDSVariableDefinition(item.Address, item.ValueType,
                    item.Unit, item.Writable, null, item.StableId));
            }
        }
        public void Bind(MDSVariableProviderHost host, string scopeId)
        {
            if (host == null) throw new ArgumentNullException("host");
            foreach (PADDeviceVariableDefinition item in Snapshot())
                host.Bind(ProviderId, item.StableId, scopeId, item.Address);
        }
    }

    public static class PADDeviceVariableMetadataAdapter
    {
        public static MDSVariableDefinition ToRuntimeDefinition(PADDeviceVariableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            return new MDSVariableDefinition(definition.Address, definition.ValueType,
                definition.Unit, definition.Writable, null, definition.StableId);
        }
    }
}
