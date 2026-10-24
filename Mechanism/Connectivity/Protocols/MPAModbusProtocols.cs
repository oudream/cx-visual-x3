using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
    public sealed class MPAModbusTcpProtocol : MPAProtocolBase, IMPAProtocolSessionCleanup, IMPAProtocolRequester, IMPAProtocolPointAccess
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, List<byte>> _buffers =
            new Dictionary<string, List<byte>>(StringComparer.Ordinal);
        private int _nextTransaction;
        // Session 是现有规约待响应 Owner；本表只保留 Modbus 事务的核对元数据。
        private sealed class PendingModbusRequest
        {
            public ushort TransactionId;
            public byte Unit;
            public byte Function;
            public string CorrelationId;
            public byte[] RequestPdu;
        }
        private readonly Dictionary<string, MPAProtocolSession> _sessions =
            new Dictionary<string, MPAProtocolSession>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<ushort, PendingModbusRequest>> _transactions =
            new Dictionary<string, Dictionary<ushort, PendingModbusRequest>>(StringComparer.Ordinal);

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

        // Request 为物理 Modbus TCP 事务：真正发送、关联响应、校验、超时和失败；
        // ReadAsync/WriteAsync 的数据映射语义保持原样，避免静默改变既有业务。
        public async Task<MPAProtocolOperationResult> RequestAsync(
            MPAProtocolOperationRequest request, MPAProtocolContext context, CancellationToken ct)
        {
            if (request == null || request.Kind != MPAProtocolOperationKind.Request ||
                !(request.Value is byte[]))
                throw new ArgumentException("Modbus TCP Request 需要原始功能码 PDU");
            if (context == null || context.Transport == null || context.ChannelAddress == null ||
                context.Configuration == null || context.Configuration.ProtocolTypeId != Id)
                throw new NotSupportedException("Modbus TCP 事务需要正式 Channel、配置及目标地址");
            if (context.Transport.State != MCCChannelState.Open ||
                context.Transport.Id != context.ChannelAddress.ChannelId)
                throw new InvalidOperationException("目标 Channel 未打开或身份不匹配");
            if (context.Transport.Topology == MCCChannelTopology.MultiPeer &&
                (!context.ChannelAddress.HasSubChannel ||
                 !(context.Transport is IMCCMultiPeerChannel) ||
                 ((IMCCMultiPeerChannel)context.Transport).FindSubChannel(context.ChannelAddress) == null))
                throw new InvalidOperationException("Modbus TCP MultiPeer 必须指定存活的子通道代次");
            byte[] pdu = (byte[])request.Value;
            if (pdu.Length != 5 || (pdu[0] != 3 && pdu[0] != 6))
                throw new NotSupportedException("本阶段 Modbus TCP 事务仅支持 03 读保持寄存器、06 写单寄存器");
            if (pdu[0] == 3)
            {
                int quantity = (pdu[3] << 8) | pdu[4];
                if (quantity < 1 || quantity > 125) throw new ArgumentOutOfRangeException("request", "03 读取数量必须 1～125");
            }
            string unitText = request.Target != null ? request.Target.Value :
                (context.ProtocolAddress != null ? context.ProtocolAddress.Value : context.Configuration.ProtocolAddress);
            if (string.IsNullOrWhiteSpace(unitText)) unitText = "1";
            byte unit;
            if (!byte.TryParse(unitText, out unit) || unit < 1 || unit > 247)
                throw new ArgumentException("Modbus TCP Unit 必须为 1～247");
            context.Configuration.Validate();
            ct.ThrowIfCancellationRequested();

            string route = context.ChannelAddress.ToString();
            ushort transactionId = 0;
            MPAProtocolSession session;
            Task<object> pendingResponse;
            lock (_gate)
            {
                if (!_sessions.TryGetValue(route, out session))
                {
                    session = new MPAProtocolSession(context.Configuration, context.ChannelAddress);
                    session.Start();
                    _sessions.Add(route, session);
                }
                Dictionary<ushort, PendingModbusRequest> requests;
                if (!_transactions.TryGetValue(route, out requests))
                {
                    requests = new Dictionary<ushort, PendingModbusRequest>();
                    _transactions.Add(route, requests);
                }
                if (requests.Count >= context.Configuration.MaxConcurrency ||
                    requests.Count >= context.Configuration.QueueCapacity)
                    throw new InvalidOperationException("Modbus TCP 当前对端事务数达到正式配置限制");
                bool available = false;
                for (int attempt = 0; attempt < 65536; attempt++)
                {
                    transactionId = unchecked((ushort)Interlocked.Increment(ref _nextTransaction));
                    if (!requests.ContainsKey(transactionId)) { available = true; break; }
                }
                if (!available) throw new InvalidOperationException("Modbus TCP TransactionId 用尽");
                pendingResponse = session.WaitResponseAsync(request.CorrelationId,
                    TimeSpan.FromMilliseconds(context.Configuration.TimeoutMs), ct);
                requests.Add(transactionId, new PendingModbusRequest {
                    TransactionId = transactionId, Unit = unit, Function = pdu[0],
                    CorrelationId = request.CorrelationId, RequestPdu = (byte[])pdu.Clone()
                });
            }
            byte[] packet = new byte[pdu.Length + 7];
            packet[0] = (byte)(transactionId >> 8); packet[1] = (byte)transactionId;
            packet[4] = (byte)((pdu.Length + 1) >> 8);
            packet[5] = (byte)(pdu.Length + 1);
            packet[6] = unit;
            Buffer.BlockCopy(pdu, 0, packet, 7, pdu.Length);
            try
            {
                try
                {
                    await context.Transport.SendAsync(packet, context.ChannelAddress, ct).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    session.FailResponse(request.CorrelationId, error);
                }
                byte[] response = (byte[])await pendingResponse.ConfigureAwait(false);
                return new MPAProtocolOperationResult(MPAProtocolOperationStage.Completed, response);
            }
            finally
            {
                lock (_gate)
                {
                    Dictionary<ushort, PendingModbusRequest> requests;
                    if (_transactions.TryGetValue(route, out requests))
                        requests.Remove(transactionId);
                }
            }
        }

        // PointTable 是外部寻址真源；只支持已明确实现的单个 16 位 Holding Register。
        private static MPAPointDefinition ResolveHoldingPoint(string pointName,
            MPAProtocolContext context, bool write, out ushort registerAddress)
        {
            if (context == null || context.Configuration == null)
                throw new NotSupportedException("点操作必须绑定正式规约配置");
            context.Configuration.Validate();
            MPAPointDefinition point = context.Configuration.Points.Find(
                p => string.Equals(p.Name, pointName, StringComparison.Ordinal));
            if (point == null || (write ? !point.CanWrite : !point.CanRead))
                throw new InvalidOperationException("点不存在或缺少读写权限：" + pointName);
            if (point.Address == null || !point.Address.StartsWith("holding:", StringComparison.OrdinalIgnoreCase) ||
                !ushort.TryParse(point.Address.Substring("holding:".Length), out registerAddress))
                throw new NotSupportedException("本阶段只支持 holding:0～65535 寄存器地址");
            if (point.ValueType != "UInt16" && point.ValueType != "Int16")
                throw new NotSupportedException("本阶段只支持 UInt16/Int16 单寄存器：" + point.ValueType);
            return point;
        }
        private static MPAProtocolAddress PointUnit(MPAPointDefinition point, MPAProtocolContext context)
        {
            string address = !string.IsNullOrWhiteSpace(point.StationAddress) ? point.StationAddress :
                (!string.IsNullOrWhiteSpace(context.Configuration.ProtocolAddress) ?
                    context.Configuration.ProtocolAddress : "1");
            return new MPAProtocolAddress(address);
        }

        public async Task<object> ReadDevicePointAsync(
            string pointName, MPAProtocolContext context, CancellationToken ct)
        {
            ushort address;
            MPAPointDefinition point = ResolveHoldingPoint(pointName, context, false, out address);
            if (context.DataAccess == null)
                throw new NotSupportedException("需要 MDS 数据 Owner 才能提交点映射");
            MPAProtocolOperationResult response = await RequestAsync(
                new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                    PointUnit(point, context), MPAModbusRegisterOperations.ReadHolding(address, 1)),
                context, ct).ConfigureAwait(false);
            byte[] pdu = (byte[])response.Value;
            if (pdu.Length != 4 || pdu[0] != 3 || pdu[1] != 2)
                throw new InvalidDataException("单寄存器响应 PDU 不合法");
            ushort raw = (ushort)((pdu[2] << 8) | pdu[3]);
            object value = point.ValueType == "Int16" ? (object)unchecked((short)raw) : raw;
            await new MPADataMappingExecutor(context.DataAccess).ApplyInboundAsync(
                context.Configuration, pointName, value, ct).ConfigureAwait(false);
            return value;
        }

        public async Task<MPAProtocolOperationResult> WriteDevicePointAsync(
            string pointName, MPAProtocolContext context, CancellationToken ct)
        {
            ushort address;
            MPAPointDefinition point = ResolveHoldingPoint(pointName, context, true, out address);
            if (context.DataAccess == null)
                throw new NotSupportedException("需要 MDS 数据 Owner 才能读取出站映射");
            object value = await new MPADataMappingExecutor(context.DataAccess).ReadOutboundAsync(
                context.Configuration, pointName, ct).ConfigureAwait(false);
            ushort registerValue = point.ValueType == "Int16"
                ? unchecked((ushort)Convert.ToInt16(value)) : Convert.ToUInt16(value);
            return await RequestAsync(new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                PointUnit(point, context), MPAModbusRegisterOperations.WriteSingle(address, registerValue)),
                context, ct).ConfigureAwait(false);
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
                    ushort transactionId = (ushort)((pending[0] << 8) | pending[1]);
                    byte unit = pending[6];
                    byte[] pdu = pending.GetRange(7, length - 1).ToArray();
                    Dictionary<ushort, PendingModbusRequest> transactions = null;
                    PendingModbusRequest transaction = null;
                    MPAProtocolSession session = null;
                    bool matched = _transactions.TryGetValue(peer, out transactions) &&
                        transactions.TryGetValue(transactionId, out transaction) &&
                        _sessions.TryGetValue(peer, out session);
                    if (matched)
                    {
                        try
                        {
                            ValidateResponse(transaction, unit, pdu);
                            // 仅把有真实存活请求且通过全部校验的响应交给上层观察者。
                            if (session.CompleteResponse(transaction.CorrelationId, pdu))
                                frames.Add(pdu);
                        }
                        catch (Exception error)
                        {
                            // 非法响应不能流入 Connector.PayloadReceived，再被误当作有效点值。
                            session.FailResponse(transaction.CorrelationId, error);
                        }
                    }
                    else
                    {
                        MPAModbusRegisterOperations.CheckResponsePdu(pdu);
                        frames.Add(pdu);
                    }
                    pending.RemoveRange(0, 6 + length);
                }
                return frames;
            }
        }

        private static void ValidateResponse(PendingModbusRequest request, byte unit, byte[] pdu)
        {
            if (unit != request.Unit)
                throw new InvalidDataException("Modbus 响应 Unit 与请求不一致");
            if (pdu == null || pdu.Length < 2)
                throw new InvalidDataException("Modbus 响应过短");
            if (pdu[0] == (request.Function | 0x80))
                throw new InvalidDataException("Modbus 异常响应，Function=" +
                    request.Function + " Error=" + pdu[1]);
            if (pdu[0] != request.Function)
                throw new InvalidDataException("Modbus 响应功能码与请求不一致");
            MPAModbusRegisterOperations.CheckResponsePdu(pdu);
            if (request.Function == 3)
            {
                int count = (request.RequestPdu[3] << 8) | request.RequestPdu[4];
                if (pdu[1] != count * 2)
                    throw new InvalidDataException("Modbus 响应寄存器数量与请求不一致");
            }
            else
            {
                for (int i = 1; i < 5; i++)
                    if (pdu[i] != request.RequestPdu[i])
                        throw new InvalidDataException("Modbus 写单寄存器响应回显不一致");
            }
        }

        public void Reset(MCCSubChannelAddress address)
        {
            if (address == null) throw new ArgumentNullException("address");
            lock (_gate)
            {
                string route = address.ToString();
                _buffers.Remove(route);
                _transactions.Remove(route);
                MPAProtocolSession session;
                if (_sessions.TryGetValue(route, out session))
                { _sessions.Remove(route); session.Dispose(); }
            }
        }
        public void ResetAll()
        {
            lock (_gate)
            {
                _buffers.Clear();
                _transactions.Clear();
                foreach (MPAProtocolSession session in _sessions.Values) session.Dispose();
                _sessions.Clear();
            }
        }
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
