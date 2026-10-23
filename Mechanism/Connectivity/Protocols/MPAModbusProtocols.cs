using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CxVisual.Mechanism.Connectivity
{
    // Modbus 公共功能码请求与显式检查；点地址/站号由 Protocol.Points 定义。
    public static class MPAModbusRegisterOperations
    {
        public static byte[] ReadHolding(ushort address, ushort count)
        {
            if (count < 1 || count > 125) throw new ArgumentOutOfRangeException("count");
            return new byte[] { 3, (byte)(address >> 8), (byte)address,
                (byte)(count >> 8), (byte)count };
        }
        public static byte[] WriteSingle(ushort address, ushort value)
        {
            return new byte[] { 6, (byte)(address >> 8), (byte)address,
                (byte)(value >> 8), (byte)value };
        }
        internal static byte GetUnit(MPAProtocolContext context)
        {
            string text = context == null || context.ProtocolAddress == null
                ? "1" : context.ProtocolAddress.Value;
            byte unit;
            if (!byte.TryParse(text, out unit) || unit < 1 || unit > 247)
                throw new ArgumentException("Modbus Unit/RTU Station 必须为 1～247");
            return unit;
        }
        internal static void CheckResponsePdu(byte[] pdu)
        {
            if (pdu == null || pdu.Length < 2) throw new InvalidDataException("Modbus PDU 太短");
            byte function = pdu[0];
            if ((function & 0x80) != 0)
                throw new InvalidDataException("Modbus 异常响应，Function=" +
                    (function & 0x7F) + " Error=" + pdu[1]);
            if (function == 3 && (pdu.Length != 2 + pdu[1] || pdu[1] % 2 != 0))
                throw new InvalidDataException("Modbus 读寄存器响应长度不合法");
            if (function == 6 && pdu.Length != 5)
                throw new InvalidDataException("Modbus 写单个寄存器响应长度不合法");
        }
    }

    // 已有 ReferTo 的 Modbus Frame/Station 原理直接收敛到 Protocol Owner，
    // 不重新引入 ReferTo IProtocol/CxChannel adapter。
    public sealed class MPAModbusTcpProtocol : MPAProtocolBase, IMPAProtocolSessionCleanup
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, List<byte>> _buffers =
            new Dictionary<string, List<byte>>(StringComparer.Ordinal);
        private int _nextTransaction;

        public MPAModbusTcpProtocol() : base(new MPAProtocolDescriptor("modbus.tcp",
            "Modbus TCP", 1, MPAProtocolCapabilities.Interpret |
            MPAProtocolCapabilities.Read | MPAProtocolCapabilities.Write |
            MPAProtocolCapabilities.Send | MPAProtocolCapabilities.RequestResponse,
            MCCIOCapabilities.ByteStream)) { }

        public override byte[] PrepareSend(byte[] pdu, MPAProtocolContext context)
        {
            if (pdu == null || pdu.Length < 2 || pdu.Length > 253)
                throw new ArgumentException("Modbus TCP PDU 长度 2～253");
            byte unit = MPAModbusRegisterOperations.GetUnit(context);
            int id = Interlocked.Increment(ref _nextTransaction) & 0xffff;
            int bytes = pdu.Length + 1;
            byte[] message = new byte[pdu.Length + 7];
            message[0] = (byte)(id >> 8); message[1] = (byte)id;
            message[4] = (byte)(bytes >> 8); message[5] = (byte)bytes;
            message[6] = unit;
            Buffer.BlockCopy(pdu, 0, message, 7, pdu.Length);
            return message;
        }

        public override IReadOnlyList<byte[]> ProcessReceived(
            MCCChannelReceiveContext received, MPAProtocolContext context)
        {
            if (received == null || received.Address == null) throw new ArgumentNullException("received");
            lock (_gate)
            {
                string peer = received.Address.ToString();
                List<byte> pending;
                if (!_buffers.TryGetValue(peer, out pending))
                { pending = new List<byte>(); _buffers.Add(peer, pending); }
                byte[] incoming = received.Data;
                if (incoming.Length + pending.Count > 131072)
                    throw new InvalidDataException("Modbus TCP 缓冲超预算");
                pending.AddRange(incoming);
                List<byte[]> frames = new List<byte[]>();
                while (pending.Count >= 7)
                {
                    int protocol = (pending[2] << 8) | pending[3];
                    int length = (pending[4] << 8) | pending[5];
                    if (protocol != 0 || length < 2 || length > 254)
                        throw new InvalidDataException("Modbus TCP MBAP 协议标识或长度错误");
                    if (pending.Count < 6 + length) break;
                    byte[] pdu = pending.GetRange(7, length - 1).ToArray();
                    MPAModbusRegisterOperations.CheckResponsePdu(pdu);
                    frames.Add(pdu);
                    pending.RemoveRange(0, 6 + length);
                }
                return frames;
            }
        }
        public void Reset(MCCSubChannelAddress address)
        { if (address == null) throw new ArgumentNullException("address");
          lock (_gate) _buffers.Remove(address.ToString()); }
        public void ResetAll() { lock (_gate) _buffers.Clear(); }
    }

    public sealed class MPAModbusRtuProtocol : MPAProtocolBase, IMPAProtocolSessionCleanup
    {
        private readonly object _gate = new object();
        private readonly List<byte> _buffer = new List<byte>();
        public MPAModbusRtuProtocol() : base(new MPAProtocolDescriptor("modbus.rtu",
            "Modbus RTU", 1, MPAProtocolCapabilities.Interpret |
            MPAProtocolCapabilities.Read | MPAProtocolCapabilities.Write |
            MPAProtocolCapabilities.Send | MPAProtocolCapabilities.RequestResponse,
            MCCIOCapabilities.ByteStream)) { }

        public override byte[] PrepareSend(byte[] pdu, MPAProtocolContext context)
        {
            if (pdu == null || pdu.Length < 2 || pdu.Length > 252)
                throw new ArgumentException("Modbus RTU PDU 长度 2～252");
            byte[] frame = new byte[pdu.Length + 3];
            frame[0] = MPAModbusRegisterOperations.GetUnit(context);
            Buffer.BlockCopy(pdu, 0, frame, 1, pdu.Length);
            ushort crc = Crc16(frame, frame.Length - 2);
            frame[frame.Length - 2] = (byte)crc;
            frame[frame.Length - 1] = (byte)(crc >> 8);
            return frame;
        }

        public override IReadOnlyList<byte[]> ProcessReceived(
            MCCChannelReceiveContext received, MPAProtocolContext context)
        {
            if (received == null) throw new ArgumentNullException("received");
            lock (_gate)
            {
                byte[] bytes = received.Data;
                if (bytes.Length + _buffer.Count > 2048)
                    throw new InvalidDataException("RTU 接收缓冲超预算");
                _buffer.AddRange(bytes);
                List<byte[]> frames = new List<byte[]>();
                while (_buffer.Count >= 5)
                {
                    int required;
                    byte function = _buffer[1];
                    if ((function & 128) != 0) required = 5;
                    else if (function == 3)
                    {
                        if (_buffer.Count < 3) break;
                        required = 5 + _buffer[2];
                    }
                    else if (function == 6) required = 8;
                    else throw new NotSupportedException("本批 RTU 支持 03/06/异常响应；其余功能码需专业扩展");
                    if (required > 256) throw new InvalidDataException("RTU 帧过长");
                    if (_buffer.Count < required) break;
                    byte[] frame = _buffer.GetRange(0, required).ToArray();
                    ushort actual = Crc16(frame, required - 2);
                    if (frame[required - 2] != (byte)actual ||
                        frame[required - 1] != (byte)(actual >> 8))
                        throw new InvalidDataException("Modbus RTU CRC16 校验失败");
                    if (frame[0] < 1 || frame[0] > 247)
                        throw new InvalidDataException("无效 RTU 站号");
                    byte[] pdu = new byte[required - 3];
                    Buffer.BlockCopy(frame, 1, pdu, 0, pdu.Length);
                    MPAModbusRegisterOperations.CheckResponsePdu(pdu);
                    frames.Add(pdu);
                    _buffer.RemoveRange(0, required);
                }
                return frames;
            }
        }
        public void Reset() { lock (_gate) _buffer.Clear(); }
        public void Reset(MCCSubChannelAddress address)
        {
            if (address == null) throw new ArgumentNullException("address");
            // RTU 为 SharedBus，不按子通道分离，清理当前总线事务解析缓存。
            Reset();
        }
        public void ResetAll() { Reset(); }
        private static ushort Crc16(byte[] buffer, int count)
        {
            ushort crc = 0xffff;
            for (int i = 0; i < count; i++)
            {
                crc ^= buffer[i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xa001 : crc >> 1);
            }
            return crc;
        }
    }
}
