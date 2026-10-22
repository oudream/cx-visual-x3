// PORTING REVIEW: ReferTo source copied and type-renamed; NOT INCLUDED IN PROJECT.
// Original: ReferTo/Protocols/ModbusTcpModule/ModbusTcpProtocol.cs; requires dependency cleanup, no compatibility Adapter will be retained.
using System;
using System.Collections.Generic;
using CxVisual.Platform.Protocols;
using CxVisual.Platform.Channels;
using CxVisual.Core;

namespace CxVisual.Mechanism.Connectivity.Porting
{
    public sealed class MPAModbusTcpProtocol : IProtocol
    {
        public MPAModbusTcpProtocol()
        {
            Codec = new MPAModbusTcpFrameCodec();
            FrameParser = new MPAModbusTcpFrameParser();
        }

        public string ProtocolId => "modbus.tcp";
        public string DisplayName => "Modbus TCP";
        public Version Version => new Version(1, 0);
        public IProtocolCodec Codec { get; }
        public IFrameParser FrameParser { get; }
        public CxProtocolDescriptor Descriptor => new CxProtocolDescriptor { Id = ProtocolId, Name = DisplayName, Version = "1.0.0", Capabilities = new List<string> { "Read", "Write", "RequestResponse" },
            MessageSchemas = new Dictionary<string, string> { ["frame"] = "modbus.tcp.mbap.v1" }, SessionModel = "RequestResponse" };
        public CxValidationResult ValidateBinding(CxProtocolBinding binding, IChannel channel)
        {
            var result = binding == null ? new CxValidationResult() : binding.Validate();
            if (binding == null || channel == null || binding.ProtocolId != ProtocolId || binding.ChannelRef != channel.Id || channel.Descriptor.Kind.ToUpperInvariant() != "TCP")
                CxChannelValidation.Error(result, "MODBUS_BINDING_CHANNEL_INVALID", "Modbus TCP requires the declared protocol and TCP channel.");
            if (binding?.Addressing != null && binding.Addressing.TryGetValue("unitId", out string unit) && !byte.TryParse(unit, out _))
                CxChannelValidation.Error(result, "MODBUS_BINDING_UNIT_INVALID", "Unit identifier must fit one byte.");
            return result;
        }
        public CxProtocolSession CreateSession(IChannel channel, bool ownsChannel = false)
        {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            ValidateBinding(new CxProtocolBinding { ProtocolId = ProtocolId, ChannelRef = channel.Id }, channel).ThrowIfInvalid();
            return new CxProtocolSession(this, channel, ownsChannel, validateResponse: (request, response) =>
            {
                var sent = (MPAModbusTcpFrame)request; var received = (MPAModbusTcpFrame)response;
                if (sent.TransactionId != received.TransactionId || sent.UnitId != received.UnitId || (received.FunctionCode != sent.FunctionCode && received.FunctionCode != (sent.FunctionCode | 0x80)))
                    throw new FormatException("MODBUS_RESPONSE_IDENTITY_MISMATCH");
            });
        }
    }

    public sealed class MPAModbusTcpFrameCodec : IProtocolCodec
    {
        public byte[] Encode(object message)
        {
            MPAModbusTcpFrame frame = message as MPAModbusTcpFrame;
            if (frame == null) throw new ArgumentException("Modbus TCP编码器只接受MPAModbusTcpFrame。", nameof(message));
            return frame.ToBytes();
        }

        public object Decode(byte[] frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            MPAModbusTcpFrame parsed;
            int consumed;
            if (!MPAModbusTcpFrame.TryParse(frame, out parsed, out consumed) || consumed != frame.Length)
                throw new FormatException("Modbus TCP帧不完整或长度非法。");
            return parsed;
        }
    }

    public sealed class MPAModbusTcpFrameParser : CxFrameParser
    {
        public MPAModbusTcpFrameParser(int capacity = 65536) : base(capacity) { }
        public override bool TryParse(IList<byte> buffer, out object frame, out int consumed)
        {
            MPAModbusTcpFrame parsed;
            bool success = MPAModbusTcpFrame.TryParse(buffer, out parsed, out consumed);
            frame = parsed;
            return success;
        }
    }

    public sealed class MPAModbusTcpFrame
    {
        public ushort TransactionId { get; set; }

        public ushort ProtocolId { get; set; } = 0;

        public ushort Length { get; set; }

        public byte UnitId { get; set; }

        public byte FunctionCode { get; set; }

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public bool IsExceptionResponse => FunctionCode >= 0x80;

        public byte ExceptionCode => Data != null && Data.Length > 0 ? Data[0] : (byte)0;

        public byte[] ToBytes()
        {
            var payload = Data ?? Array.Empty<byte>();
            if (ProtocolId != 0 || payload.Length > 252 || (FunctionCode & 0x7f) == 0 || (IsExceptionResponse && payload.Length != 1)) throw new FormatException("MODBUS_TCP_FRAME_INVALID");
            Length = (ushort)(2 + payload.Length);

            var buffer = new byte[6 + Length];
            buffer[0] = (byte)(TransactionId >> 8);
            buffer[1] = (byte)(TransactionId & 0xFF);
            buffer[2] = (byte)(ProtocolId >> 8);
            buffer[3] = (byte)(ProtocolId & 0xFF);
            buffer[4] = (byte)(Length >> 8);
            buffer[5] = (byte)(Length & 0xFF);
            buffer[6] = UnitId;
            buffer[7] = FunctionCode;

            if (payload.Length > 0)
            {
                Array.Copy(payload, 0, buffer, 8, payload.Length);
            }

            return buffer;
        }

        public static bool TryParse(IList<byte> buffer, out MPAModbusTcpFrame frame, out int frameLength)
        {
            frame = null;
            frameLength = 0;

            if (buffer == null || buffer.Count < 6)
            {
                return false;
            }

            ushort length = (ushort)((buffer[4] << 8) | buffer[5]);
            if (buffer[2] != 0 || buffer[3] != 0 || length < 2 || length > 254) throw new FormatException("MODBUS_TCP_HEADER_INVALID");

            int total = 6 + length;
            if (buffer.Count < total)
            {
                return false;
            }

            if ((buffer[7] & 0x7f) == 0 || (buffer[7] >= 0x80 && length != 3)) throw new FormatException("MODBUS_TCP_FUNCTION_INVALID");
            frameLength = total;
            var dataLength = frameLength - 8;
            var data = new byte[Math.Max(dataLength, 0)];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = buffer[8 + i];
            }

            frame = new MPAModbusTcpFrame
            {
                TransactionId = (ushort)((buffer[0] << 8) | buffer[1]),
                ProtocolId = (ushort)((buffer[2] << 8) | buffer[3]),
                Length = length,
                UnitId = buffer[6],
                FunctionCode = buffer[7],
                Data = data
            };
            return true;
        }
    }

    internal static class ModbusTcpProtocol
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
                    throw new ArgumentOutOfRangeException(nameof(registerType), registerType, "未知的寄存器区");
            }
        }

        public static MPAModbusTcpFrame BuildReadRequest(byte slaveId, ModbusRegisterType registerType, ushort startAddress, ushort quantity)
        {
            return new MPAModbusTcpFrame
            {
                UnitId = slaveId,
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

        public static MPAModbusTcpFrame BuildWriteSingleCoilRequest(byte slaveId, ushort address, bool value)
        {
            ushort coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;
            return new MPAModbusTcpFrame
            {
                UnitId = slaveId,
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

        public static MPAModbusTcpFrame BuildWriteSingleRegisterRequest(byte slaveId, ushort address, ushort value)
        {
            return new MPAModbusTcpFrame
            {
                UnitId = slaveId,
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

        public static MPAModbusTcpFrame BuildWriteMultipleRegistersRequest(byte slaveId, ushort startAddress, ushort[] values)
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

            return new MPAModbusTcpFrame
            {
                UnitId = slaveId,
                FunctionCode = 0x10,
                Data = payload
            };
        }

        public static ushort[] ParseRegistersFromReadResponse(MPAModbusTcpFrame frame)
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

        public static bool[] ParseBitsFromReadResponse(MPAModbusTcpFrame frame)
        {
            if (frame == null || frame.Data == null || frame.Data.Length < 1)
            {
                return Array.Empty<bool>();
            }

            int byteCount = frame.Data[0];
            var values = new bool[byteCount * 8];
            for (int i = 0; i < byteCount; i++)
            {
                byte data = frame.Data[i + 1];
                for (int bit = 0; bit < 8; bit++)
                {
                    values[i * 8 + bit] = (data & (1 << bit)) != 0;
                }
            }

            return values;
        }

        public static bool TryParsePassiveWriteRegisters(MPAModbusTcpFrame frame, out ushort startAddress, out ushort[] values)
        {
            startAddress = 0;
            values = Array.Empty<ushort>();

            if (frame == null || frame.Data == null)
            {
                return false;
            }

            if (frame.FunctionCode == 0x06 && frame.Data.Length >= 4)
            {
                startAddress = (ushort)((frame.Data[0] << 8) | frame.Data[1]);
                values = new[] { (ushort)((frame.Data[2] << 8) | frame.Data[3]) };
                return true;
            }

            if (frame.FunctionCode == 0x10 && frame.Data.Length >= 5)
            {
                ushort quantity = (ushort)((frame.Data[2] << 8) | frame.Data[3]);
                int byteCount = frame.Data[4];
                if (frame.Data.Length < 5 + byteCount || byteCount != quantity * 2)
                {
                    return false;
                }

                startAddress = (ushort)((frame.Data[0] << 8) | frame.Data[1]);
                values = new ushort[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    int dataIndex = 5 + i * 2;
                    values[i] = (ushort)((frame.Data[dataIndex] << 8) | frame.Data[dataIndex + 1]);
                }
                return true;
            }

            return false;
        }

        public static bool TryParsePassiveWriteCoils(MPAModbusTcpFrame frame, out ushort startAddress, out bool[] values)
        {
            startAddress = 0;
            values = Array.Empty<bool>();

            if (frame == null || frame.Data == null)
            {
                return false;
            }

            if (frame.FunctionCode == 0x05 && frame.Data.Length >= 4)
            {
                startAddress = (ushort)((frame.Data[0] << 8) | frame.Data[1]);
                ushort rawValue = (ushort)((frame.Data[2] << 8) | frame.Data[3]);
                values = new[] { rawValue == 0xFF00 };
                return true;
            }

            if (frame.FunctionCode == 0x0F && frame.Data.Length >= 5)
            {
                ushort quantity = (ushort)((frame.Data[2] << 8) | frame.Data[3]);
                int byteCount = frame.Data[4];
                if (frame.Data.Length < 5 + byteCount)
                {
                    return false;
                }

                startAddress = (ushort)((frame.Data[0] << 8) | frame.Data[1]);
                values = new bool[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    int dataIndex = 5 + i / 8;
                    values[i] = (frame.Data[dataIndex] & (1 << (i % 8))) != 0;
                }
                return true;
            }

            return false;
        }
    }
}
