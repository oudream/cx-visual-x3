using CxVisual.Platform.VariableObjects;
using System.ComponentModel;

namespace CxVisual.Platform.Protocols.ModbusTcp
{
    public enum ModbusMultiRegisterByteOrder
    {
        [Description("AB CD")]
        AbCd,

        [Description("CD AB")]
        CdAb,

        [Description("BA DC")]
        BaDc,

        [Description("DC BA")]
        DcBa
    }

    public class ModbusTcpDriverModuleConfig : DriverModuleConfig
    {
        [Category("Modbus TCP"), DisplayName("IP地址")]
        public string RemoteIp { get; set; } = string.Empty;

        [Category("Modbus TCP"), DisplayName("远程端口")]
        public ushort RemotePort { get; set; } = 502;

        [Category("Modbus TCP"), DisplayName("站号")]
        public byte SlaveId { get; set; } = 1;

        [Category("Modbus TCP"), DisplayName("默认读取间隔(ms)")]
        public int ReadInterval { get; set; } = 1000;

        [Category("Modbus TCP"), DisplayName("响应超时(ms)")]
        public int ResponseTimeout { get; set; } = 1000;

        [Category("Modbus TCP"), DisplayName("允许读空洞地址")]
        public bool AllowReadSparseAddress { get; set; } = true;

        [Category("Modbus TCP"), DisplayName("是否启动日志")]
        public bool LogEnabled { get; set; } = false;

        [Category("Modbus TCP"), DisplayName("多寄存器字节序")]
        public ModbusMultiRegisterByteOrder MultiRegisterByteOrder { get; set; } = ModbusMultiRegisterByteOrder.AbCd;

        public ModbusTcpDriverModuleConfig(long id, string name) : base(id, name)
        {
        }

        public ModbusTcpDriverModuleConfig() : base(0, "")
        {
        }
    }
}
