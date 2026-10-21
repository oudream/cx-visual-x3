using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CxVisual.Platform.Protocols.ModbusTcp
{
    public enum ModbusDataArea
    {
        Coil,
        DiscreteInput,
        InputRegister,
        HoldingRegister
    }

    public enum ModbusByteOrder
    {
        ABCD,
        BADC,
        CDAB,
        DCBA
    }

    public sealed class ModbusAddress
    {
        public ModbusDataArea Area { get; private set; }
        public ushort Offset { get; private set; }

        private ModbusAddress(ModbusDataArea area, ushort offset)
        {
            Area = area;
            Offset = offset;
        }

        public static ModbusAddress Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Modbus地址不能为空。");
            string raw = text.Trim();
            int separator = raw.IndexOf(':');
            if (separator > 0)
            {
                string areaText = raw.Substring(0, separator).Trim().ToUpperInvariant();
                string offsetText = raw.Substring(separator + 1).Trim();
                int offset;
                if (!int.TryParse(offsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset) || offset < 0 || offset > ushort.MaxValue)
                    throw new FormatException("Modbus地址偏移非法：" + text);
                if (areaText == "C" || areaText == "COIL" || areaText == "0X") return new ModbusAddress(ModbusDataArea.Coil, (ushort)offset);
                if (areaText == "DI" || areaText == "DISCRETE" || areaText == "1X") return new ModbusAddress(ModbusDataArea.DiscreteInput, (ushort)offset);
                if (areaText == "IR" || areaText == "INPUT" || areaText == "3X") return new ModbusAddress(ModbusDataArea.InputRegister, (ushort)offset);
                if (areaText == "HR" || areaText == "HOLDING" || areaText == "4X") return new ModbusAddress(ModbusDataArea.HoldingRegister, (ushort)offset);
                throw new FormatException("不支持的Modbus地址区：" + areaText);
            }

            int reference;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out reference))
                throw new FormatException("Modbus地址格式非法：" + text);
            if (reference >= 1 && reference <= 9999) return new ModbusAddress(ModbusDataArea.Coil, checked((ushort)(reference - 1)));
            if (reference >= 10001 && reference <= 19999) return new ModbusAddress(ModbusDataArea.DiscreteInput, checked((ushort)(reference - 10001)));
            if (reference >= 30001 && reference <= 39999) return new ModbusAddress(ModbusDataArea.InputRegister, checked((ushort)(reference - 30001)));
            if (reference >= 40001 && reference <= 49999) return new ModbusAddress(ModbusDataArea.HoldingRegister, checked((ushort)(reference - 40001)));
            throw new FormatException("Modbus传统引用地址超出支持范围：" + text);
        }

        public override string ToString()
        {
            string prefix = Area == ModbusDataArea.Coil ? "C" : Area == ModbusDataArea.DiscreteInput ? "DI" : Area == ModbusDataArea.InputRegister ? "IR" : "HR";
            return prefix + ":" + Offset.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ModbusTagBinding
    {
        public string TagId { get; set; }
        public ModbusAddress Address { get; set; }
        public string DataType { get; set; }
        public int RegisterCount { get; set; }
        public double? Deadband { get; set; }
    }

    internal sealed class ModbusPollGroup
    {
        public ModbusDataArea Area { get; set; }
        public ushort Start { get; set; }
        public ushort Count { get; set; }
        public List<ModbusTagBinding> Bindings { get; set; }
    }

    internal static class ModbusPollPlanner
    {
        public static IReadOnlyList<ModbusPollGroup> Build(IEnumerable<ModbusTagBinding> bindings, int maxRegisters, int maxBits, int maxRegisterGap, int maxBitGap)
        {
            List<ModbusPollGroup> groups = new List<ModbusPollGroup>();
            foreach (IGrouping<ModbusDataArea, ModbusTagBinding> areaGroup in (bindings ?? Enumerable.Empty<ModbusTagBinding>()).GroupBy(x => x.Address.Area))
            {
                bool bits = areaGroup.Key == ModbusDataArea.Coil || areaGroup.Key == ModbusDataArea.DiscreteInput;
                int maxCount = bits ? Math.Max(1, Math.Min(2000, maxBits)) : Math.Max(1, Math.Min(125, maxRegisters));
                int maxGap = bits ? Math.Max(0, maxBitGap) : Math.Max(0, maxRegisterGap);
                ModbusPollGroup current = null;
                foreach (ModbusTagBinding binding in areaGroup.OrderBy(x => x.Address.Offset))
                {
                    int width = bits ? 1 : Math.Max(1, binding.RegisterCount);
                    int start = binding.Address.Offset;
                    int end = start + width;
                    if (end > ushort.MaxValue + 1) throw new InvalidOperationException("Modbus Tag超出地址空间：" + binding.TagId);
                    if (current == null)
                    {
                        current = NewGroup(areaGroup.Key, start, end, binding);
                        groups.Add(current);
                        continue;
                    }
                    int currentEnd = current.Start + current.Count;
                    int mergedEnd = Math.Max(currentEnd, end);
                    int gap = Math.Max(0, start - currentEnd);
                    int mergedCount = mergedEnd - current.Start;
                    if (gap <= maxGap && mergedCount <= maxCount)
                    {
                        current.Count = checked((ushort)mergedCount);
                        current.Bindings.Add(binding);
                    }
                    else
                    {
                        current = NewGroup(areaGroup.Key, start, end, binding);
                        groups.Add(current);
                    }
                }
            }
            return groups.AsReadOnly();
        }

        private static ModbusPollGroup NewGroup(ModbusDataArea area, int start, int end, ModbusTagBinding binding)
        {
            return new ModbusPollGroup
            {
                Area = area,
                Start = checked((ushort)start),
                Count = checked((ushort)(end - start)),
                Bindings = new List<ModbusTagBinding> { binding }
            };
        }
    }

    internal static class ModbusValueCodec
    {
        public static int GetRegisterCount(string dataType)
        {
            string type = Normalize(dataType);
            if (type == "boolean" || type == "bool" || type == "int16" || type == "uint16" || type == "short" || type == "ushort") return 1;
            if (type == "int32" || type == "uint32" || type == "int" || type == "uint" || type == "single" || type == "float") return 2;
            if (type == "int64" || type == "uint64" || type == "long" || type == "ulong" || type == "double") return 4;
            int stringLength = ParseStringLength(type);
            if (stringLength > 0) return Math.Max(1, (stringLength + 1) / 2);
            throw new NotSupportedException("不支持的Modbus数据类型：" + dataType);
        }

        public static Type GetClrType(string dataType)
        {
            string type = Normalize(dataType);
            if (type == "boolean" || type == "bool") return typeof(bool);
            if (type == "int16" || type == "short") return typeof(short);
            if (type == "uint16" || type == "ushort") return typeof(ushort);
            if (type == "int32" || type == "int") return typeof(int);
            if (type == "uint32" || type == "uint") return typeof(uint);
            if (type == "int64" || type == "long") return typeof(long);
            if (type == "uint64" || type == "ulong") return typeof(ulong);
            if (type == "single" || type == "float") return typeof(float);
            if (type == "double") return typeof(double);
            if (ParseStringLength(type) > 0) return typeof(string);
            throw new NotSupportedException("不支持的Modbus数据类型：" + dataType);
        }

        public static object DecodeRegisters(ushort[] registers, int offset, string dataType, ModbusByteOrder order)
        {
            if (registers == null) throw new ArgumentNullException(nameof(registers));
            int count = GetRegisterCount(dataType);
            if (offset < 0 || offset + count > registers.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            byte[] bytes = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                ushort value = registers[offset + i];
                bytes[i * 2] = (byte)(value >> 8);
                bytes[i * 2 + 1] = (byte)(value & 0xFF);
            }
            string type = Normalize(dataType);
            if (ParseStringLength(type) > 0)
            {
                int length = ParseStringLength(type);
                string text = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(length, bytes.Length));
                int zero = text.IndexOf('\0');
                return zero >= 0 ? text.Substring(0, zero) : text.TrimEnd('\0', ' ');
            }
            ApplyByteOrder(bytes, order);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            if (type == "boolean" || type == "bool") return BitConverter.ToUInt16(bytes, 0) != 0;
            if (type == "int16" || type == "short") return BitConverter.ToInt16(bytes, 0);
            if (type == "uint16" || type == "ushort") return BitConverter.ToUInt16(bytes, 0);
            if (type == "int32" || type == "int") return BitConverter.ToInt32(bytes, 0);
            if (type == "uint32" || type == "uint") return BitConverter.ToUInt32(bytes, 0);
            if (type == "int64" || type == "long") return BitConverter.ToInt64(bytes, 0);
            if (type == "uint64" || type == "ulong") return BitConverter.ToUInt64(bytes, 0);
            if (type == "single" || type == "float") return BitConverter.ToSingle(bytes, 0);
            if (type == "double") return BitConverter.ToDouble(bytes, 0);
            throw new NotSupportedException("不支持的Modbus数据类型：" + dataType);
        }

        public static ushort[] EncodeRegisters(object value, string dataType, ModbusByteOrder order)
        {
            string type = Normalize(dataType);
            int stringLength = ParseStringLength(type);
            if (stringLength > 0)
            {
                string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                byte[] encoded = System.Text.Encoding.ASCII.GetBytes(text);
                byte[] bytes = new byte[GetRegisterCount(type) * 2];
                Array.Copy(encoded, bytes, Math.Min(encoded.Length, Math.Min(stringLength, bytes.Length)));
                return ToRegisters(bytes);
            }

            byte[] hostBytes;
            if (type == "boolean" || type == "bool") hostBytes = BitConverter.GetBytes(Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? (ushort)1 : (ushort)0);
            else if (type == "int16" || type == "short") hostBytes = BitConverter.GetBytes(Convert.ToInt16(value, CultureInfo.InvariantCulture));
            else if (type == "uint16" || type == "ushort") hostBytes = BitConverter.GetBytes(Convert.ToUInt16(value, CultureInfo.InvariantCulture));
            else if (type == "int32" || type == "int") hostBytes = BitConverter.GetBytes(Convert.ToInt32(value, CultureInfo.InvariantCulture));
            else if (type == "uint32" || type == "uint") hostBytes = BitConverter.GetBytes(Convert.ToUInt32(value, CultureInfo.InvariantCulture));
            else if (type == "int64" || type == "long") hostBytes = BitConverter.GetBytes(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            else if (type == "uint64" || type == "ulong") hostBytes = BitConverter.GetBytes(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
            else if (type == "single" || type == "float") hostBytes = BitConverter.GetBytes(Convert.ToSingle(value, CultureInfo.InvariantCulture));
            else if (type == "double") hostBytes = BitConverter.GetBytes(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            else throw new NotSupportedException("不支持的Modbus数据类型：" + dataType);

            if (BitConverter.IsLittleEndian) Array.Reverse(hostBytes);
            ApplyByteOrder(hostBytes, order);
            return ToRegisters(hostBytes);
        }

        public static bool Equivalent(object expected, object actual, double? tolerance = null)
        {
            if (expected == null || actual == null) return expected == actual;
            if (expected is string || actual is string) return string.Equals(Convert.ToString(expected, CultureInfo.InvariantCulture), Convert.ToString(actual, CultureInfo.InvariantCulture), StringComparison.Ordinal);
            if (expected is bool || actual is bool)
            {
                try { return Convert.ToBoolean(expected, CultureInfo.InvariantCulture) == Convert.ToBoolean(actual, CultureInfo.InvariantCulture); }
                catch { return false; }
            }
            try
            {
                double a = Convert.ToDouble(expected, CultureInfo.InvariantCulture);
                double b = Convert.ToDouble(actual, CultureInfo.InvariantCulture);
                double eps = tolerance ?? 1e-9;
                return Math.Abs(a - b) <= eps;
            }
            catch { return object.Equals(expected, actual); }
        }

        private static ushort[] ToRegisters(byte[] bytes)
        {
            if (bytes.Length % 2 != 0) throw new InvalidOperationException("Modbus寄存器字节数必须为偶数。");
            ushort[] registers = new ushort[bytes.Length / 2];
            for (int i = 0; i < registers.Length; i++) registers[i] = (ushort)((bytes[i * 2] << 8) | bytes[i * 2 + 1]);
            return registers;
        }

        private static void ApplyByteOrder(byte[] bytes, ModbusByteOrder order)
        {
            if (bytes == null || bytes.Length <= 1 || order == ModbusByteOrder.ABCD) return;
            if (order == ModbusByteOrder.BADC)
            {
                for (int i = 0; i + 1 < bytes.Length; i += 2)
                {
                    byte temp = bytes[i]; bytes[i] = bytes[i + 1]; bytes[i + 1] = temp;
                }
                return;
            }
            if (order == ModbusByteOrder.CDAB)
            {
                int words = bytes.Length / 2;
                for (int left = 0, right = words - 1; left < right; left++, right--)
                {
                    byte a0 = bytes[left * 2]; byte a1 = bytes[left * 2 + 1];
                    bytes[left * 2] = bytes[right * 2]; bytes[left * 2 + 1] = bytes[right * 2 + 1];
                    bytes[right * 2] = a0; bytes[right * 2 + 1] = a1;
                }
                return;
            }
            if (order == ModbusByteOrder.DCBA) Array.Reverse(bytes);
        }

        private static string Normalize(string dataType)
        {
            return (dataType ?? "UInt16").Trim().ToLowerInvariant();
        }

        private static int ParseStringLength(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith("string[", StringComparison.Ordinal) || !normalized.EndsWith("]", StringComparison.Ordinal)) return 0;
            string value = normalized.Substring(7, normalized.Length - 8);
            int length;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out length) && length > 0 ? length : 0;
        }
    }
}
