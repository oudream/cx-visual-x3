using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Core.Modules;
namespace CxVisual.Mechanism.Connectivity
{
    // 外部地址与公开变量的对应关系；Point 不再套一个 VariableTable 外壳。
    public sealed class MCNPointDefinition
    {
        public MCNPointDefinition(string variableName, string address, Type valueType)
        {
            if (string.IsNullOrWhiteSpace(variableName) || valueType == null) throw new ArgumentException("点变量名和类型必填");
            VariableName = variableName; Address = address; ValueType = valueType;
        }
        public string VariableName { get; private set; }
        public string Address { get; private set; }
        public Type ValueType { get; private set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
    }
    // Channel、Protocol 可为 0..N；允许无通道业务连接器，不创建 EmptyChannel。
    public interface IMCNConnectorModule : IMFMModule
    {
        IReadOnlyList<IMCCChannel> Channels { get; }
        IReadOnlyList<IMPAProtocolDefinition> Protocols { get; }
        IReadOnlyList<MCNPointDefinition> Points { get; }
    }
}
