using CxVisual.Platform.VariableObjects;
using System.ComponentModel;
using System.IO.Ports;

namespace CxVisual.Platform.Protocols.ModbusRtu
{
    public class ModbusRtuDriverModuleConfig : DriverModuleConfig
    {
        [Category("Modbus RTU"), DisplayName("Port Name")]
        public string PortName { get; set; } = string.Empty;

        [Category("Modbus RTU"), DisplayName("Baud Rate")]
        public int BaudRate { get; set; } = 9600;

        [Category("Modbus RTU"), DisplayName("Data Bits")]
        public int DataBits { get; set; } = 8;

        [Category("Modbus RTU"), DisplayName("Parity")]
        public Parity Parity { get; set; } = Parity.None;

        [Category("Modbus RTU"), DisplayName("Stop Bits")]
        public StopBits StopBits { get; set; } = StopBits.One;

        [Category("Modbus RTU"), DisplayName("Slave Id")]
        public byte SlaveId { get; set; } = 1;

        [Category("Modbus RTU"), DisplayName("Default Read Interval (ms)")]
        public int ReadInterval { get; set; } = 1000;

        [Category("Modbus RTU"), DisplayName("Response Timeout (ms)")]
        public int ResponseTimeout { get; set; } = 1000;

        [Category("Modbus RTU"), DisplayName("Inter Frame Delay (ms)")]
        public int InterFrameDelayMs { get; set; } = 10;

        [Category("Modbus RTU"), DisplayName("Allow Sparse Read")]
        public bool AllowReadSparseAddress { get; set; } = false;

        [Category("Modbus RTU"), DisplayName("Enable Driver Log")]
        public bool LogEnabled { get; set; } = false;

        public ModbusRtuDriverModuleConfig(long id, string name) : base(id, name)
        {
        }

        public ModbusRtuDriverModuleConfig() : base(0, "")
        {
        }
    }
}
