using CommonInterfaces;
using CxVisual.Platform.VariableObjects;
using S7.Net;
using S7.Net.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace CxVisual.Platform.Protocols.S7
{
    public class S7PointItem : PointItem
    {
        // PLC的DB区，如"DB1"
        // 合成整个地址："DB1.DBW10"等
        [DisplayName("DB区号")]
        public int DBArea { get; set; }      

        // 数据类型，如"Byte", "Word", "DWord"等
        [DisplayName("变量类型")]
        public VariableType VariableType { get; set; }    

        // Address是"0.0"，解释后要点前部分
        private string address;
        [DisplayName("变量地址")]
        public string Address
        {
            get => address;
            set
            {
                address = value;
                SetByteOffset();
                SetBitOffset();
            }
        }

        [DisplayName("字符长度")]
        public int StringLength { get; set; }

        [DisplayName("读取周期间隔")]
        public int ReadInterval { get; set; }

        [Browsable(false)]
        public int ByteOffset { get; private set; }

        [Browsable(false)] 
        public byte BitOffset { get; private set; }

        public S7PointItem(string deviceName, string variableName) : base(deviceName, variableName)
        {
        }

        public DataItem ToDataItem()
        {
            int count = 1;
            if (this.VariableType == VariableType.S7String)
            {
                if (StringLength > 0 && StringLength < 254)
                {
                    count = StringLength;
                }
                else
                {
                    count = 254;
                }
            }

            return new DataItem
            {
                Count = count,
                DataType = DataType.DataBlock,
                DB = DBArea,
                StartByteAdr = ByteOffset,
                BitAdr = BitOffset,
                VarType = GetVarType()
            };
        }

        private void SetByteOffset()
        {
            // Address是"0.0"，解释后要点前部分
            // Assuming the Address format could be "DB1.DBW10" or "I0.0"
            var parts = Address.Split(new char[] { '.', 'W' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
            {
                // Try to parse the part that represents the byte offset
                if (int.TryParse(parts[0], out int byteOffset))
                {
                    ByteOffset = byteOffset;
                    return;
                }
            }
            ByteOffset = 0; // Indicate failure to parse
        }

        private void SetBitOffset()
        {
            // Address是"0.0"，解释后要点后部分
            var parts = Address.Split('.');
            if (parts.Length >= 2)
            {
                // The bit offset is after the period
                if (byte.TryParse(parts[1], out byte bitOffset))
                {
                    BitOffset = bitOffset;
                    return;
                }
            }
            BitOffset = 0; // Indicate failure to parse
        }

        public int GetLength()
        {
            // if string return Length
            // else switch VarType
            switch (VariableType)
            {
                case VariableType.Bit: return 1;
                case VariableType.Byte: return 1;
                case VariableType.Int16: return 2;
                case VariableType.Int32: return 4;
                case VariableType.Float32: return 4;
                case VariableType.Float64: return 8;
                case VariableType.DateTimeLong: return 12;
            }
            return StringLength;
        }

        public VarType GetVarType()
        {
            switch (VariableType)
            {
                case VariableType.Bit: return VarType.Bit;
                case VariableType.Byte: return VarType.Byte;
                case VariableType.Int16: return VarType.Int;
                case VariableType.Int32: return VarType.DInt;
                case VariableType.Float32: return VarType.Real;
                case VariableType.Float64: return VarType.LReal;
                case VariableType.DateTimeLong: return VarType.DateTimeLong;
            }
            return VarType.S7String;
        }

        public static VariableObject CreateAndRegisterDevice(string deviceName, VariableType variableType)
        {
            // 根据varType找出设备模板
            var deviceTemplateName = GetTemplateName(variableType);
            if (string.IsNullOrEmpty(deviceTemplateName))
            {
                LogHelper.Error("内部错误，{}找不到设备模板");
                return null;
            }
            return VariableObjectManager.CreateAndRegisterObject(deviceName, deviceTemplateName);
        }

        // 根据varType找出设备模板，些设备模板类型就是变量类型
        public static string GetTemplateName(VariableType variableType)
        {
            switch (variableType)
            {
                case VariableType.Bit:
                case VariableType.Byte:
                case VariableType.Int16:
                case VariableType.Int32:
                    return BuiltInVariableObjectTemplates.MeasureInteger.Name;
                case VariableType.Float32:
                case VariableType.Float64:
                    return BuiltInVariableObjectTemplates.MeasureDouble.Name;
                case VariableType.S7String:
                    return BuiltInVariableObjectTemplates.MeasureString.Name;
                case VariableType.DateTimeLong:
                    return BuiltInVariableObjectTemplates.MeasureLong.Name;
            }
            return "";
        }

        public static (string, long) GetVariableValueString(string deviceName, VariableType variableType)
        {
            var table = VariableManager.FindVariableTable(deviceName);
            if (table != null)
            {
                switch (variableType)
                {
                    case VariableType.Bit:
                    case VariableType.Byte:
                    case VariableType.Int16:
                    case VariableType.Int32:
                        var (valueInt, dtInt) = table.GetVariable2<int>(ConstHelper.VariableNameValue);
                        return (valueInt.ToString(), dtInt);
                    case VariableType.Float32:
                    case VariableType.Float64:
                        var (valueDouble, dtDouble) = table.GetVariable2<double>(ConstHelper.VariableNameValue);
                        return (valueDouble.ToString("F3"), dtDouble);
                    case VariableType.S7String:
                        var (valueString, dtString) = table.GetVariable2<string>(ConstHelper.VariableNameValue);
                        return (valueString, dtString);
                    case VariableType.DateTimeLong:
                        var (valueLong, dtLong) = table.GetVariable2<long>(ConstHelper.VariableNameValue);
                        return (TimeHelper.GetDateTimeString(valueLong), dtLong);
                }
            }
            return ("", 0);
        }

        public static void SetVariableValue(S7PointItem pt, object value, VarType varType)
        {
            switch (varType)
            {
                case VarType.Bit:
                    SetPointValue(pt, Convert.ToInt32(value));
                    break;
                case VarType.Byte:
                    SetPointValue(pt, Convert.ToInt32(value));
                    break;
                case VarType.Int:
                    SetPointValue(pt, Convert.ToInt32(value));
                    break;
                case VarType.DInt:
                    SetPointValue(pt, Convert.ToInt32(value));
                    break;
                case VarType.Real:
                    SetPointValue(pt, Convert.ToDouble(value));
                    break;
                case VarType.LReal:
                    SetPointValue(pt, Convert.ToDouble(value));
                    break;
                case VarType.S7String:
                    if (value is string str)
                    {
                        SetPointValue(pt, str);
                    }
                    break;
                case VarType.DateTimeLong:
                    if (value is System.DateTime dt)
                    {
                        SetPointValue(pt, TimeHelper.GetMs(dt));
                    }
                    break;
            }
        }

        public static void SetPointValue<TValue>(S7PointItem pt, TValue value)
        {
            var table = VariableManager.FindVariableTable(pt.DeviceName);
            if (table != null)
            {
                table.UpdateVariable(pt.VariableName, value);
            }
        }

        public static string CheckS7Point(S7PointItem pt)
        {
            if (pt.DBArea < 0 || pt.DBArea > 65535)
            {
                return "DB区号[{pt.DBArea}]不合法";
            }
            if (pt.ByteOffset < 0 || pt.ByteOffset > 65535)
            {
                return "字节偏移[{pt.ByteOffset}]不合法";
            }
            if (pt.BitOffset < 0 || pt.BitOffset > 7)
            {
                return "位偏移[{pt.BitOffset}]不合法";
            }
            if (pt.StringLength < 0 || pt.StringLength > 254)
            {
                return "字符串长度[{pt.StringLength}]不合法";
            }
            return null;
        }

    }

    public enum VariableType
    {
        [Description("Bit (bool)")]
        Bit,

        [Description("Byte (1 byte)")]
        Byte,

        [Description("Int16 (2 bytes)")]
        Int16,

        [Description("Int32 (4 bytes)")]
        Int32,

        [Description("Float32 (4 bytes)")]
        Float32,

        [Description("Float64 (8 bytes)")]
        Float64,

        [Description("String (N bytes < 254)")]
        S7String,

        [Description("DateTimeLong (12 bytes)")]
        DateTimeLong,
    }

}
