using System;
using System.Collections.Generic;
using System.IO;

namespace CxVisual.Mechanism.Connectivity
{
    public interface IMPAProtocolCodec
    {
        byte[] Encode(byte[] payload);
        byte[] Decode(byte[] frame);
    }

    /// <summary>机制层自描述长度+CRC16传输封套，不代表Modbus等工业专业协议。</summary>
    public sealed class MPALengthFrameCodec : IMPAProtocolCodec
    {
        private readonly int _maxPayload;
        public MPALengthFrameCodec(int maxPayload = 4096)
        {
            if (maxPayload < 1 || maxPayload > 60000)
                throw new ArgumentOutOfRangeException("maxPayload");
            _maxPayload = maxPayload;
        }
        public byte[] Encode(byte[] payload)
        {
            if (payload == null || payload.Length > _maxPayload)
                throw new ArgumentException("Payload exceeds codec budget", "payload");
            byte[] data = new byte[payload.Length + 6];
            data[0] = 0xC5; data[1] = 0x3A;
            data[2] = (byte)(payload.Length >> 8);
            data[3] = (byte)(payload.Length & 255);
            Buffer.BlockCopy(payload, 0, data, 4, payload.Length);
            ushort crc = Crc16(data, data.Length - 2);
            data[data.Length - 2] = (byte)(crc >> 8);
            data[data.Length - 1] = (byte)crc;
            return data;
        }
        public byte[] Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 6 || bytes[0] != 0xC5 || bytes[1] != 0x3A)
                throw new InvalidDataException("protocol.bad_header");
            int length = (bytes[2] << 8) | bytes[3];
            if (length > _maxPayload || length + 6 != bytes.Length)
                throw new InvalidDataException("protocol.invalid_length");
            ushort checksum = Crc16(bytes, bytes.Length - 2);
            if (bytes[bytes.Length - 2] != (byte)(checksum >> 8) ||
                bytes[bytes.Length - 1] != (byte)checksum)
                throw new InvalidDataException("protocol.crc_mismatch");
            byte[] payload = new byte[length];
            Buffer.BlockCopy(bytes, 4, payload, 0, length);
            return payload;
        }
        internal static ushort Crc16(byte[] source, int count)
        {
            ushort crc = 0xffff;
            for (int i = 0; i < count; i++)
            {
                crc ^= (ushort)(source[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }
            return crc;
        }
    }

    /// <summary>实际字节分包解析，半包等待、粘包可多次读取、错误后Reset。</summary>
    public sealed class MPALengthFrameParser
    {
        private readonly List<byte> _buffer = new List<byte>();
        private readonly MPALengthFrameCodec _codec;
        private readonly int _maximum;
        private bool _faulted;
        private readonly object _gate = new object();
        public MPALengthFrameParser(int maximumPayload = 4096)
        {
            _codec = new MPALengthFrameCodec(maximumPayload);
            _maximum = maximumPayload + 6;
        }
        public void PushBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            lock (_gate)
            {
                if (_faulted) throw new InvalidOperationException("protocol.requires_reset");
                if (bytes.Length > _maximum * 2 - _buffer.Count)
                    throw new InvalidDataException("protocol.buffer_capacity");
                _buffer.AddRange(bytes);
            }
        }
        public bool TryReadFrame(out byte[] payload)
        {
            byte[] rawFrame;
            return TryReadFrame(out payload, out rawFrame);
        }
        public bool TryReadFrame(out byte[] payload, out byte[] rawFrame)
        {
            lock (_gate)
            {
                payload = null; rawFrame = null;
                if (_faulted) throw new InvalidOperationException("protocol.requires_reset");
                if (_buffer.Count < 4) return false;
                try
                {
                    if (_buffer[0] != 0xc5 || _buffer[1] != 0x3a)
                        throw new InvalidDataException("protocol.bad_header");
                    int length = (_buffer[2] << 8) | _buffer[3];
                    if (length > _maximum - 6)
                        throw new InvalidDataException("protocol.invalid_length");
                    int total = length + 6;
                    if (_buffer.Count < total) return false;
                    byte[] candidate = _buffer.GetRange(0, total).ToArray();
                    payload = _codec.Decode(candidate);
                    rawFrame = candidate;
                    _buffer.RemoveRange(0, total);
                    return true;
                }
                catch (InvalidDataException)
                {
                    _faulted = true;
                    throw;
                }
            }
        }
        public void Reset()
        { lock (_gate) { _faulted = false; _buffer.Clear(); } }
    }
}