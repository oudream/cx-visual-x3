using System;

namespace CxVisual.Platform.Protocols.ModbusRtu
{
    internal sealed class ModbusRtuFrame
    {
        public byte SlaveAddress { get; set; }

        public byte FunctionCode { get; set; }

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public bool IsExceptionResponse => FunctionCode >= 0x80;

        public byte ExceptionCode => Data != null && Data.Length > 0 ? Data[0] : (byte)0;

        public byte[] ToBytes()
        {
            var payload = Data ?? Array.Empty<byte>();
            var buffer = new byte[2 + payload.Length + 2];
            buffer[0] = SlaveAddress;
            buffer[1] = FunctionCode;

            if (payload.Length > 0)
            {
                Array.Copy(payload, 0, buffer, 2, payload.Length);
            }

            ushort crc = ModbusRtuProtocol.ComputeCrc(buffer, buffer.Length - 2);
            buffer[buffer.Length - 2] = (byte)(crc & 0xFF);
            buffer[buffer.Length - 1] = (byte)(crc >> 8);
            return buffer;
        }

        public static bool TryParse(byte[] buffer, out ModbusRtuFrame frame, out string errorMessage)
        {
            frame = null;
            errorMessage = string.Empty;

            if (buffer == null || buffer.Length < 5)
            {
                errorMessage = "RTU frame is too short.";
                return false;
            }

            ushort expectedCrc = ModbusRtuProtocol.ComputeCrc(buffer, buffer.Length - 2);
            ushort actualCrc = (ushort)(buffer[buffer.Length - 2] | (buffer[buffer.Length - 1] << 8));
            if (expectedCrc != actualCrc)
            {
                errorMessage = $"CRC mismatch. Expected [{expectedCrc:X4}], actual [{actualCrc:X4}].";
                return false;
            }

            var dataLength = buffer.Length - 4;
            var data = new byte[dataLength];
            if (dataLength > 0)
            {
                Array.Copy(buffer, 2, data, 0, dataLength);
            }

            frame = new ModbusRtuFrame
            {
                SlaveAddress = buffer[0],
                FunctionCode = buffer[1],
                Data = data
            };
            return true;
        }
    }

    internal static class ModbusRtuProtocol
    {
        public static byte GetReadFunctionCode(ModbusRegisterType registerType)
        {
            switch (registerType)
            {
                case ModbusRegisterType.Coil:
                    return 0x01;
                case ModbusRegisterType.DiscreteInput:
                    return 0x02;
                case ModbusRegisterType.HoldingRegister:
                    return 0x03;
                case ModbusRegisterType.InputRegister:
                    return 0x04;
                default:
                    throw new ArgumentOutOfRangeException(nameof(registerType), registerType, "Unknown register type.");
            }
        }

        public static ModbusRtuFrame BuildReadRequest(byte slaveId, ModbusRegisterType registerType, ushort startAddress, ushort quantity)
        {
            return new ModbusRtuFrame
            {
                SlaveAddress = slaveId,
                FunctionCode = GetReadFunctionCode(registerType),
                Data = new[]
                {
                    (byte)(startAddress >> 8),
                    (byte)(startAddress & 0xFF),
                    (byte)(quantity >> 8),
                    (byte)(quantity & 0xFF)
                }
            };
        }

        public static ModbusRtuFrame BuildWriteSingleCoilRequest(byte slaveId, ushort address, bool value)
        {
            ushort coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;
            return new ModbusRtuFrame
            {
                SlaveAddress = slaveId,
                FunctionCode = 0x05,
                Data = new[]
                {
                    (byte)(address >> 8),
                    (byte)(address & 0xFF),
                    (byte)(coilValue >> 8),
                    (byte)(coilValue & 0xFF)
                }
            };
        }

        public static ModbusRtuFrame BuildWriteSingleRegisterRequest(byte slaveId, ushort address, ushort value)
        {
            return new ModbusRtuFrame
            {
                SlaveAddress = slaveId,
                FunctionCode = 0x06,
                Data = new[]
                {
                    (byte)(address >> 8),
                    (byte)(address & 0xFF),
                    (byte)(value >> 8),
                    (byte)(value & 0xFF)
                }
            };
        }

        public static ModbusRtuFrame BuildWriteMultipleRegistersRequest(byte slaveId, ushort startAddress, ushort[] values)
        {
            var payload = new byte[5 + values.Length * 2];
            payload[0] = (byte)(startAddress >> 8);
            payload[1] = (byte)(startAddress & 0xFF);
            payload[2] = (byte)(values.Length >> 8);
            payload[3] = (byte)(values.Length & 0xFF);
            payload[4] = (byte)(values.Length * 2);

            for (int i = 0; i < values.Length; i++)
            {
                payload[5 + i * 2] = (byte)(values[i] >> 8);
                payload[5 + i * 2 + 1] = (byte)(values[i] & 0xFF);
            }

            return new ModbusRtuFrame
            {
                SlaveAddress = slaveId,
                FunctionCode = 0x10,
                Data = payload
            };
        }

        public static ushort[] ParseRegistersFromReadResponse(ModbusRtuFrame frame)
        {
            if (frame == null || frame.Data == null || frame.Data.Length < 1)
            {
                return Array.Empty<ushort>();
            }

            int registerCount = frame.Data[0] / 2;
            var registers = new ushort[registerCount];
            for (int i = 0; i < registerCount; i++)
            {
                int index = 1 + i * 2;
                if (index + 1 >= frame.Data.Length)
                {
                    break;
                }

                registers[i] = (ushort)((frame.Data[index] << 8) | frame.Data[index + 1]);
            }

            return registers;
        }

        public static bool[] ParseBitsFromReadResponse(ModbusRtuFrame frame)
        {
            if (frame == null || frame.Data == null || frame.Data.Length < 1)
            {
                return Array.Empty<bool>();
            }

            int byteCount = frame.Data[0];
            var values = new bool[byteCount * 8];
            for (int i = 0; i < byteCount; i++)
            {
                int dataIndex = i + 1;
                if (dataIndex >= frame.Data.Length)
                {
                    break;
                }

                byte current = frame.Data[dataIndex];
                for (int bit = 0; bit < 8; bit++)
                {
                    values[i * 8 + bit] = (current & (1 << bit)) != 0;
                }
            }

            return values;
        }

        public static ushort ComputeCrc(byte[] data, int length)
        {
            if (data == null)
            {
                return 0;
            }

            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    bool lsbSet = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (lsbSet)
                    {
                        crc ^= 0xA001;
                    }
                }
            }

            return crc;
        }
    }
}
