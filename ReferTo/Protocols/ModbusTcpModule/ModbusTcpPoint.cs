using CommonInterfaces;
using CxVisual.Platform.VariableObjects;
using System;
using System.ComponentModel;
using VariableInterfaces;

namespace CxVisual.Platform.Protocols.ModbusTcp
{
    public class ModbusTcpPointItem : PointItem
    {
        [DisplayName("寄存器区")]
        public ModbusRegisterType RegisterType { get; set; }

        [DisplayName("值类型")]
        public ModbusValueType ValueType { get; set; }

        private string _address = "0";
        [DisplayName("地址")]
        public string Address
        {
            get => _address;
            set
            {
                _address = string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
                ParseAddress();
            }
        }

        [DisplayName("读取周期间隔")]
        public int ReadInterval { get; set; }

        [Browsable(false)]
        public ushort RegisterAddress { get; private set; }

        [Browsable(false)]
        public byte BitOffset { get; private set; }

        [Browsable(false)]
        public bool IsRegisterArea
            => RegisterType == ModbusRegisterType.HoldingRegister || RegisterType == ModbusRegisterType.InputRegister;

        public ModbusTcpPointItem(string deviceName, string variableName) : base(deviceName, variableName)
        {
            ParseAddress();
        }

        private void ParseAddress()
        {
            RegisterAddress = 0;
            BitOffset = 0;

            var text = string.IsNullOrWhiteSpace(_address) ? "0" : _address.Trim();
            var parts = text.Split('.');

            if (parts.Length >= 1 && ushort.TryParse(parts[0], out ushort registerAddress))
            {
                RegisterAddress = registerAddress;
            }

            if (parts.Length >= 2 && byte.TryParse(parts[1], out byte bitOffset))
            {
                BitOffset = bitOffset;
            }
        }

        public int GetRegisterCount()
        {
            switch (ValueType)
            {
                case ModbusValueType.Bit:
                case ModbusValueType.UInt16:
                case ModbusValueType.Int16:
                    return 1;
                case ModbusValueType.UInt32:
                case ModbusValueType.Int32:
                case ModbusValueType.Float32:
                    return 2;
                case ModbusValueType.Float64:
                    return 4;
                default:
                    return 1;
            }
        }

        public static VariableObject CreateAndRegisterDevice(string deviceName, ModbusValueType valueType)
        {
            var deviceTemplateName = GetTemplateName(valueType);
            if (string.IsNullOrEmpty(deviceTemplateName))
            {
                LogHelper.Error("内部错误，Modbus点位找不到设备模板");
                return null;
            }

            return VariableObjectManager.CreateAndRegisterObject(deviceName, deviceTemplateName);
        }

        public static string GetTemplateName(ModbusValueType valueType)
        {
            switch (valueType)
            {
                case ModbusValueType.Bit:
                case ModbusValueType.UInt16:
                case ModbusValueType.Int16:
                case ModbusValueType.Int32:
                    return BuiltInVariableObjectTemplates.MeasureInteger.Name;
                case ModbusValueType.UInt32:
                    return BuiltInVariableObjectTemplates.MeasureLong.Name;
                case ModbusValueType.Float32:
                case ModbusValueType.Float64:
                    return BuiltInVariableObjectTemplates.MeasureDouble.Name;
                default:
                    return string.Empty;
            }
        }

        public static Type GetType(ModbusValueType valueType)
        {
            switch (valueType)
            {
                case ModbusValueType.Bit:
                case ModbusValueType.UInt16:
                case ModbusValueType.Int16:
                case ModbusValueType.Int32:
                    return typeof(int);
                case ModbusValueType.UInt32:
                    return typeof(long);
                case ModbusValueType.Float32:
                case ModbusValueType.Float64:
                    return typeof(double);
                default:
                    return typeof(int);
            }
        }

        public static void SetPointValue<TValue>(ModbusTcpPointItem pointItem, TValue value)
        {
            var table = VariableManager.FindVariableTable(pointItem.DeviceName);
            if (table != null)
            {
                table.UpdateVariable(pointItem.VariableName, value);
            }
        }

        public static string CheckModbusPoint(ModbusTcpPointItem pointItem)
        {
            if (pointItem == null)
            {
                return "点位为空";
            }

            if (!pointItem.IsRegisterArea && pointItem.ValueType != ModbusValueType.Bit)
            {
                return $"寄存器区[{pointItem.RegisterType}]只支持 Bit 类型";
            }

            if (pointItem.IsRegisterArea && pointItem.ValueType != ModbusValueType.Bit && pointItem.BitOffset != 0)
            {
                return $"点位[{pointItem.DeviceName}]非 Bit 类型时地址不能带位偏移";
            }

            if (pointItem.IsRegisterArea && pointItem.ValueType == ModbusValueType.Bit && pointItem.BitOffset > 15)
            {
                return $"点位[{pointItem.DeviceName}]位偏移[{pointItem.BitOffset}]不合法";
            }

            if (!pointItem.IsRegisterArea && pointItem.BitOffset != 0)
            {
                return $"点位[{pointItem.DeviceName}]线圈/离散输入地址不支持位偏移";
            }

            if ((pointItem.RegisterType == ModbusRegisterType.DiscreteInput || pointItem.RegisterType == ModbusRegisterType.InputRegister) && pointItem.CanWrite)
            {
                return $"点位[{pointItem.DeviceName}]当前寄存器区不支持写入";
            }

            return null;
        }
    }

    public enum ModbusRegisterType
    {
        [Description("Coil (0x)")]
        Coil,

        [Description("DiscreteInput (1x)")]
        DiscreteInput,

        [Description("HoldingRegister (4x)")]
        HoldingRegister,

        [Description("InputRegister (3x)")]
        InputRegister,
    }

    public enum ModbusValueType
    {
        [Description("Bit (1 bit)")]
        Bit,

        [Description("UInt16 (1 register)")]
        UInt16,

        [Description("Int16 (1 register)")]
        Int16,

        [Description("UInt32 (2 registers)")]
        UInt32,

        [Description("Int32 (2 registers)")]
        Int32,

        [Description("Float32 (2 registers)")]
        Float32,

        [Description("Float64 (4 registers)")]
        Float64,
    }
}
