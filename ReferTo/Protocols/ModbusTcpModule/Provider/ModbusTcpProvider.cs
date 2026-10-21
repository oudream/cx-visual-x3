using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Abstractions;
using CxVisual.Platform.Channels;
using CxVisual.Platform.Protocols;

namespace CxVisual.Platform.Protocols.ModbusTcp
{
    internal sealed class ModbusTcpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public byte UnitId { get; set; }
        public TimeSpan OperationTimeout { get; set; }
        public TimeSpan ReconnectMinimum { get; set; }
        public TimeSpan ReconnectMaximum { get; set; }
        public int PollIntervalMilliseconds { get; set; }
        public int MaximumConsecutiveFailures { get; set; }
        public int MaximumRegistersPerRead { get; set; }
        public int MaximumBitsPerRead { get; set; }
        public int MaximumRegisterGap { get; set; }
        public int MaximumBitGap { get; set; }
        public ModbusByteOrder ByteOrder { get; set; }
        public bool VerifyWrites { get; set; }

        public static ModbusTcpSettings From(IcsConnectionProfile connection, IcsDeviceDefinition definition)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string host;
            int port;
            ParseEndpoint(connection.Endpoint, out host, out port);
            Dictionary<string, string> merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in connection.Settings ?? new Dictionary<string, string>()) merged[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, string> pair in definition.Settings ?? new Dictionary<string, string>()) merged[pair.Key] = pair.Value;
            return new ModbusTcpSettings
            {
                Host = host,
                Port = port,
                UnitId = checked((byte)GetInt(merged, "UnitId", 1, 0, 247)),
                OperationTimeout = connection.OperationTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : connection.OperationTimeout,
                ReconnectMinimum = connection.ReconnectMinimum < TimeSpan.Zero ? TimeSpan.Zero : connection.ReconnectMinimum,
                ReconnectMaximum = connection.ReconnectMaximum < connection.ReconnectMinimum ? connection.ReconnectMinimum : connection.ReconnectMaximum,
                PollIntervalMilliseconds = GetInt(merged, "PollIntervalMs", 100, 10, 60000),
                MaximumConsecutiveFailures = GetInt(merged, "MaxConsecutiveFailures", 3, 1, 1000),
                MaximumRegistersPerRead = GetInt(merged, "MaxRegistersPerRead", 120, 1, 125),
                MaximumBitsPerRead = GetInt(merged, "MaxBitsPerRead", 1968, 1, 2000),
                MaximumRegisterGap = GetInt(merged, "MaxRegisterGap", 0, 0, 120),
                MaximumBitGap = GetInt(merged, "MaxBitGap", 0, 0, 1968),
                ByteOrder = GetEnum(merged, "ByteOrder", ModbusByteOrder.ABCD),
                VerifyWrites = GetBool(merged, "VerifyWrites", true)
            };
        }

        private static void ParseEndpoint(string endpoint, out string host, out int port)
        {
            string raw = string.IsNullOrWhiteSpace(endpoint) ? "127.0.0.1:502" : endpoint.Trim();
            Uri uri;
            if (Uri.TryCreate(raw, UriKind.Absolute, out uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                host = uri.Host;
                port = uri.IsDefaultPort ? 502 : uri.Port;
                return;
            }
            int separator = raw.LastIndexOf(':');
            if (separator <= 0 || separator >= raw.Length - 1) { host = raw; port = 502; return; }
            int parsed;
            if (!int.TryParse(raw.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed <= 0 || parsed > 65535)
                throw new FormatException("Modbus TCP Endpoint端口非法：" + endpoint);
            host = raw.Substring(0, separator);
            port = parsed;
        }

        private static int GetInt(IDictionary<string, string> settings, string key, int fallback, int minimum, int maximum)
        {
            string raw;
            int value;
            if (settings != null && settings.TryGetValue(key, out raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return Math.Max(minimum, Math.Min(maximum, value));
            return fallback;
        }
        private static bool GetBool(IDictionary<string, string> settings, string key, bool fallback)
        {
            string raw; bool value;
            return settings != null && settings.TryGetValue(key, out raw) && bool.TryParse(raw, out value) ? value : fallback;
        }
        private static T GetEnum<T>(IDictionary<string, string> settings, string key, T fallback) where T : struct
        {
            string raw; T value;
            return settings != null && settings.TryGetValue(key, out raw) && Enum.TryParse<T>(raw, true, out value) ? value : fallback;
        }
    }

    internal sealed class ModbusTcpClient : ProtocolSession
    {
        private readonly string _host;
        private readonly int _port;
        private readonly TimeSpan _timeout;
        private readonly SemaphoreSlim _ioGate = new SemaphoreSlim(1, 1);
        private bool _disposed;
        private int _transaction;

        public ModbusTcpClient(string host, int port, TimeSpan timeout)
            : this(host, port, timeout, CreateChannel(host, port, timeout))
        {
        }

        internal ModbusTcpClient(string host, int port, TimeSpan timeout, IChannel channel)
            : base(new ModbusTcpProtocolDefinition(), channel)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host不能为空。", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            _host = host; _port = port; _timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : timeout;
        }

        public bool IsConnected => !_disposed && Channel.Health.IsOnline;

        public async Task ConnectAsync(CancellationToken token)
        {
            await _ioGate.WaitAsync(token).ConfigureAwait(false);
            try { await EnsureConnectedCoreAsync(token).ConfigureAwait(false); }
            finally { _ioGate.Release(); }
        }

        public void Invalidate()
        {
            Channel.Close();
        }

        public async Task<bool[]> ReadBitsAsync(byte unitId, byte function, ushort start, ushort count, CancellationToken token)
        {
            if (function != 1 && function != 2) throw new ArgumentOutOfRangeException(nameof(function));
            if (count == 0 || count > 2000) throw new ArgumentOutOfRangeException(nameof(count));
            byte[] pdu = new[] { function, (byte)(start >> 8), (byte)start, (byte)(count >> 8), (byte)count };
            byte[] response = await ExecuteAsync(unitId, pdu, token).ConfigureAwait(false);
            if (response.Length < 2 || response[0] != function) throw new InvalidOperationException("Modbus位读取响应非法。");
            int byteCount = response[1];
            if (response.Length != 2 + byteCount || byteCount < (count + 7) / 8) throw new InvalidOperationException("Modbus位读取长度非法。");
            bool[] values = new bool[count];
            for (int i = 0; i < count; i++) values[i] = (response[2 + i / 8] & (1 << (i % 8))) != 0;
            return values;
        }

        public async Task<ushort[]> ReadRegistersAsync(byte unitId, byte function, ushort start, ushort count, CancellationToken token)
        {
            if (function != 3 && function != 4) throw new ArgumentOutOfRangeException(nameof(function));
            if (count == 0 || count > 125) throw new ArgumentOutOfRangeException(nameof(count));
            byte[] pdu = new[] { function, (byte)(start >> 8), (byte)start, (byte)(count >> 8), (byte)count };
            byte[] response = await ExecuteAsync(unitId, pdu, token).ConfigureAwait(false);
            int expectedBytes = count * 2;
            if (response.Length != 2 + expectedBytes || response[0] != function || response[1] != expectedBytes) throw new InvalidOperationException("Modbus寄存器读取响应非法。");
            ushort[] values = new ushort[count];
            for (int i = 0; i < count; i++) values[i] = (ushort)((response[2 + i * 2] << 8) | response[3 + i * 2]);
            return values;
        }

        public async Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken token)
        {
            ushort raw = value ? (ushort)0xFF00 : (ushort)0x0000;
            byte[] pdu = new[] { (byte)5, (byte)(address >> 8), (byte)address, (byte)(raw >> 8), (byte)raw };
            byte[] response = await ExecuteAsync(unitId, pdu, token).ConfigureAwait(false);
            if (response.Length != 5 || response[0] != 5 || response[1] != pdu[1] || response[2] != pdu[2] || response[3] != pdu[3] || response[4] != pdu[4])
                throw new InvalidOperationException("Modbus FC05写入确认非法。");
        }

        public async Task WriteSingleRegisterAsync(byte unitId, ushort address, ushort value, CancellationToken token)
        {
            byte[] pdu = new[] { (byte)6, (byte)(address >> 8), (byte)address, (byte)(value >> 8), (byte)value };
            byte[] response = await ExecuteAsync(unitId, pdu, token).ConfigureAwait(false);
            if (response.Length != 5 || response[0] != 6 || response[1] != pdu[1] || response[2] != pdu[2] || response[3] != pdu[3] || response[4] != pdu[4])
                throw new InvalidOperationException("Modbus FC06写入确认非法。");
        }

        public async Task WriteMultipleCoilsAsync(byte unitId, ushort address, bool[] values, CancellationToken token)
        {
            if (values == null || values.Length == 0 || values.Length > 1968) throw new ArgumentOutOfRangeException(nameof(values));
            int byteCount = (values.Length + 7) / 8;
            byte[] pdu = new byte[6 + byteCount];
            pdu[0] = 15; pdu[1] = (byte)(address >> 8); pdu[2] = (byte)address; pdu[3] = (byte)(values.Length >> 8); pdu[4] = (byte)values.Length; pdu[5] = (byte)byteCount;
            for (int i = 0; i < values.Length; i++) if (values[i]) pdu[6 + i / 8] |= (byte)(1 << (i % 8));
            byte[] response = await ExecuteAsync(unitId, pdu, token).ConfigureAwait(false);
            ValidateWriteMultipleResponse(response, 15, address, checked((ushort)values.Length));
        }

        public async Task WriteMultipleRegistersAsync(byte unitId, ushort address, ushort[] values, CancellationToken token)
        {
            if (values == null || values.Length == 0 || values.Length > 123) throw new ArgumentOutOfRangeException(nameof(values));
            byte[] pdu = new byte[6 + values.Length * 2];
            pdu[0] = 16; pdu[1] = (byte)(address >> 8); pdu[2] = (byte)address; pdu[3] = (byte)(values.Length >> 8); pdu[4] = (byte)values.Length; pdu[5] = checked((byte)(values.Length * 2));
            for (int i = 0; i < values.Length; i++) { pdu[6 + i * 2] = (byte)(values[i] >> 8); pdu[7 + i * 2] = (byte)values[i]; }
            byte[] response = await ExecuteAsync(unitId, pdu, token).ConfigureAwait(false);
            ValidateWriteMultipleResponse(response, 16, address, checked((ushort)values.Length));
        }

        private async Task<byte[]> ExecuteAsync(byte unitId, byte[] pdu, CancellationToken token)
        {
            await _ioGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                await EnsureConnectedCoreAsync(token).ConfigureAwait(false);
                ushort transaction = unchecked((ushort)Interlocked.Increment(ref _transaction));
                byte[] payload = new byte[pdu.Length - 1];
                if (payload.Length > 0) Buffer.BlockCopy(pdu, 1, payload, 0, payload.Length);
                ModbusTcpFrame request = new ModbusTcpFrame
                {
                    TransactionId = transaction,
                    UnitId = unitId,
                    FunctionCode = pdu[0],
                    Data = payload
                };
                byte[] frame = Protocol.Codec.Encode(request);

                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(_timeout);
                    try
                    {
                        await Channel.SendAsync(frame, 0, frame.Length, timeout.Token).ConfigureAwait(false);
                        Diagnostics.RequestCount++;
                        byte[] header = await ReadExactAsync(Channel, 7, timeout.Token).ConfigureAwait(false);
                        ushort receivedLength = (ushort)((header[4] << 8) | header[5]);
                        if (receivedLength < 2 || receivedLength > 260) throw new InvalidOperationException("Modbus TCP响应长度非法。");
                        byte[] tail = await ReadExactAsync(Channel, receivedLength - 1, timeout.Token).ConfigureAwait(false);
                        byte[] receivedFrame = new byte[header.Length + tail.Length];
                        Buffer.BlockCopy(header, 0, receivedFrame, 0, header.Length);
                        Buffer.BlockCopy(tail, 0, receivedFrame, header.Length, tail.Length);

                        object parsed;
                        int consumed;
                        if (!Protocol.FrameParser.TryParse(receivedFrame, out parsed, out consumed) || consumed != receivedFrame.Length)
                            throw new InvalidOperationException("Modbus TCP响应帧不完整。");
                        ModbusTcpFrame response = (ModbusTcpFrame)parsed;
                        if (response.TransactionId != transaction) throw new InvalidOperationException("Modbus TCP TransactionId不匹配。");
                        if (response.ProtocolId != 0) throw new InvalidOperationException("Modbus TCP ProtocolId非法。");
                        if (response.UnitId != unitId) throw new InvalidOperationException("Modbus TCP UnitId不匹配。");
                        if (response.IsExceptionResponse)
                        {
                            throw new InvalidOperationException("Modbus异常响应 FC=" + (response.FunctionCode & 0x7F).ToString(CultureInfo.InvariantCulture) + " Code=" + response.ExceptionCode.ToString(CultureInfo.InvariantCulture));
                        }
                        Diagnostics.ResponseCount++;
                        byte[] responsePdu = new byte[1 + response.Data.Length];
                        responsePdu[0] = response.FunctionCode;
                        if (response.Data.Length > 0) Buffer.BlockCopy(response.Data, 0, responsePdu, 1, response.Data.Length);
                        return responsePdu;
                    }
                    catch (OperationCanceledException)
                    {
                        Invalidate();
                        if (token.IsCancellationRequested) throw;
                        TimeoutException error = new TimeoutException("Modbus TCP操作超时：" + _host + ":" + _port.ToString(CultureInfo.InvariantCulture));
                        RecordError("modbus.timeout", error, true);
                        throw error;
                    }
                    catch (Exception ex)
                    {
                        Diagnostics.DecodeFailureCount++;
                        RecordError("modbus.exchange", ex, true);
                        Invalidate();
                        throw;
                    }
                }
            }
            finally { _ioGate.Release(); }
        }

        private async Task EnsureConnectedCoreAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            if (Channel.Health.IsOnline) return;
            await Channel.ConnectAsync(token).ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadExactAsync(IChannel channel, int count, CancellationToken token)
        {
            byte[] data = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = await channel.ReceiveAsync(data, offset, count - offset, token).ConfigureAwait(false);
                offset += read;
            }
            return data;
        }

        private static void ValidateWriteMultipleResponse(byte[] response, byte function, ushort address, ushort count)
        {
            if (response.Length != 5 || response[0] != function) throw new InvalidOperationException("Modbus批量写入确认非法。");
            ushort returnedAddress = (ushort)((response[1] << 8) | response[2]);
            ushort returnedCount = (ushort)((response[3] << 8) | response[4]);
            if (returnedAddress != address || returnedCount != count) throw new InvalidOperationException("Modbus批量写入确认内容不匹配。");
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Channel.Dispose();
            _ioGate.Dispose();
        }

        private static IChannel CreateChannel(string host, int port, TimeSpan timeout)
        {
            return new TcpClientTransportChannel(
                "modbus-tcp-" + Guid.NewGuid().ToString("N"),
                new ChannelEndpoint("tcp", host, port),
                timeout);
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ModbusTcpClient)); }
    }

    internal sealed class ModbusTcpConnectionSession : IDisposable
    {
        private readonly ModbusTcpClient _client;
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private readonly HashSet<string> _activeOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public ModbusTcpConnectionSession(ModbusTcpSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = new ModbusTcpClient(settings.Host, settings.Port, settings.OperationTimeout);
        }
        public ModbusTcpSettings Settings { get; private set; }
        public bool IsConnected { get { return _client.IsConnected; } }

        public async Task ActivateAsync(string ownerId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("ownerId不能为空。", nameof(ownerId));
            await _lifecycle.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ModbusTcpConnectionSession));
                if (_activeOwners.Contains(ownerId)) return;
                if (_activeOwners.Count == 0) await _client.ConnectAsync(token).ConfigureAwait(false);
                _activeOwners.Add(ownerId);
            }
            finally { _lifecycle.Release(); }
        }

        public async Task DeactivateAsync(string ownerId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return;
            await _lifecycle.WaitAsync(token).ConfigureAwait(false);
            try
            {
                _activeOwners.Remove(ownerId);
                if (_activeOwners.Count == 0) _client.Invalidate();
            }
            finally { _lifecycle.Release(); }
        }

        public void Invalidate() { _client.Invalidate(); }
        public Task<bool[]> ReadBitsAsync(byte unitId, ModbusDataArea area, ushort start, ushort count, CancellationToken token)
        {
            byte function = area == ModbusDataArea.Coil ? (byte)1 : area == ModbusDataArea.DiscreteInput ? (byte)2 : throw new ArgumentOutOfRangeException(nameof(area));
            return _client.ReadBitsAsync(unitId, function, start, count, token);
        }
        public Task<ushort[]> ReadRegistersAsync(byte unitId, ModbusDataArea area, ushort start, ushort count, CancellationToken token)
        {
            byte function = area == ModbusDataArea.HoldingRegister ? (byte)3 : area == ModbusDataArea.InputRegister ? (byte)4 : throw new ArgumentOutOfRangeException(nameof(area));
            return _client.ReadRegistersAsync(unitId, function, start, count, token);
        }
        public Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken token) { return _client.WriteSingleCoilAsync(unitId, address, value, token); }
        public Task WriteSingleRegisterAsync(byte unitId, ushort address, ushort value, CancellationToken token) { return _client.WriteSingleRegisterAsync(unitId, address, value, token); }
        public Task WriteMultipleCoilsAsync(byte unitId, ushort address, bool[] values, CancellationToken token) { return _client.WriteMultipleCoilsAsync(unitId, address, values, token); }
        public Task WriteMultipleRegistersAsync(byte unitId, ushort address, ushort[] values, CancellationToken token) { return _client.WriteMultipleRegistersAsync(unitId, address, values, token); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client.Dispose();
            _lifecycle.Dispose();
        }
    }

    public sealed class ModbusTcpDeviceDiagnostics
    {
        public string DeviceId { get; set; }
        public DeviceLifecycleState LifecycleState { get; set; }
        public IcsConnectionState ConnectionState { get; set; }
        public long SuccessfulPollCount { get; set; }
        public long FailedPollCount { get; set; }
        public long ReconnectCount { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? LastPollUtc { get; set; }
        public string LastError { get; set; }
    }

    /// <summary>无需第三方库的 Modbus TCP ICS Provider；Vendor/协议细节完全停留在 Drivers 边界。</summary>
    public sealed class ModbusTcpDeviceProvider : IIcsDeviceProvider, IDisposable
    {
        private sealed class SharedSession
        {
            public ModbusTcpConnectionSession Session;
            public int References;
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, SharedSession> _sessions = new Dictionary<string, SharedSession>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;
        public string ProviderId { get { return "cxv.modbus.tcp"; } }
        public string ProviderVersion { get { return "1.0.0"; } }
        public bool CanCreate(string deviceType) { return string.Equals(deviceType, "modbus.tcp.plc", StringComparison.OrdinalIgnoreCase) || string.Equals(deviceType, "modbus.tcp.device", StringComparison.OrdinalIgnoreCase); }

        public IIcsDevice Create(IcsDeviceDefinition definition, IcsConnectionProfile connection)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (!CanCreate(definition.DeviceType)) throw new NotSupportedException(definition.DeviceType);
            ModbusTcpSettings settings = ModbusTcpSettings.From(connection, definition);
            ModbusTcpConnectionSession session;
            lock (_gate)
            {
                ThrowIfDisposed();
                SharedSession shared;
                if (!_sessions.TryGetValue(connection.ConnectionId, out shared))
                {
                    shared = new SharedSession { Session = new ModbusTcpConnectionSession(settings), References = 0 };
                    _sessions.Add(connection.ConnectionId, shared);
                }
                else
                {
                    if (!string.Equals(shared.Session.Settings.Host, settings.Host, StringComparison.OrdinalIgnoreCase) || shared.Session.Settings.Port != settings.Port)
                        throw new InvalidOperationException("同一ConnectionId不能指向不同Modbus TCP Endpoint：" + connection.ConnectionId);
                }
                shared.References++;
                session = shared.Session;
            }
            return new ModbusTcpDevice(definition, connection, settings, session, () => Release(connection.ConnectionId));
        }

        public IReadOnlyCollection<CapabilityDescriptor> GetCapabilities()
        {
            return new[]
            {
                new CapabilityDescriptor(IcsCapabilityIds.DeviceLifecycle), new CapabilityDescriptor(IcsCapabilityIds.Variables),
                new CapabilityDescriptor(IcsCapabilityIds.MaintenanceRawAccess), new CapabilityDescriptor(IcsCapabilityIds.Diagnostics),
                new CapabilityDescriptor(ScadaCapabilityIds.RealtimeRead), new CapabilityDescriptor(ScadaCapabilityIds.ControlledWrite)
            };
        }

        private void Release(string connectionId)
        {
            ModbusTcpConnectionSession dispose = null;
            lock (_gate)
            {
                SharedSession shared;
                if (!_sessions.TryGetValue(connectionId, out shared)) return;
                shared.References--;
                if (shared.References <= 0) { _sessions.Remove(connectionId); dispose = shared.Session; }
            }
            if (dispose != null) dispose.Dispose();
        }

        public void Dispose()
        {
            List<ModbusTcpConnectionSession> sessions;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                sessions = _sessions.Values.Select(x => x.Session).Distinct().ToList();
                _sessions.Clear();
            }
            foreach (ModbusTcpConnectionSession session in sessions) session.Dispose();
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ModbusTcpDeviceProvider)); }
    }

    public sealed class ModbusTcpDevice : IIcsDevice, IIcsMaintenanceAccess
    {
        private readonly object _gate = new object();
        private readonly IcsDeviceDefinition _definition;
        private readonly IcsConnectionProfile _connection;
        private readonly ModbusTcpSettings _settings;
        private readonly ModbusTcpConnectionSession _session;
        private readonly ModbusVariableProvider _variables;
        private readonly Action _releaseSession;
        private DeviceLifecycleState _lifecycle = DeviceLifecycleState.Unconfigured;
        private IcsConnectionState _connectionState = IcsConnectionState.Offline;
        private CancellationTokenSource _pollCancellation;
        private Task _pollTask;
        private bool _disposed;
        private string _lastError;
        private int _consecutiveFailures;
        private DateTime? _lastPollUtc;
        private long _successfulPollCount;
        private long _failedPollCount;
        private long _reconnectCount;
        private bool _hasCommunicationFailure;

        internal ModbusTcpDevice(IcsDeviceDefinition definition, IcsConnectionProfile connection, ModbusTcpSettings settings, ModbusTcpConnectionSession session, Action releaseSession)
        {
            _definition = definition; _connection = connection; _settings = settings; _session = session; _releaseSession = releaseSession;
            _variables = new ModbusVariableProvider("device." + definition.DeviceId, definition.Tags, settings);
            _variables.AttachOwner(this);
        }

        public string DeviceId { get { return _definition.DeviceId; } }
        public string DisplayName { get { return _definition.DisplayName; } }
        public string ConnectionId { get { return _definition.ConnectionId; } }
        public DeviceLifecycleState LifecycleState { get { lock (_gate) return _lifecycle; } }
        public IcsConnectionState ConnectionState { get { lock (_gate) return _connectionState; } }
        public IVariableProvider VariableProvider { get { return _variables; } }
        public IIcsMaintenanceAccess MaintenanceAccess { get { return this; } }

        public Task ConfigureAsync(IndustrialDeviceProfile profile, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (profile == null || !string.Equals(profile.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Modbus ICS Profile与DeviceId不一致。");
                _lifecycle = DeviceLifecycleState.Inactive; _connectionState = IcsConnectionState.Offline; _lastError = null; _consecutiveFailures = 0;
            }
            _variables.SetConnection(false, "Configured");
            return Task.CompletedTask;
        }

        public async Task ActivateAsync(CancellationToken token)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_lifecycle == DeviceLifecycleState.Unconfigured) throw new InvalidOperationException("Modbus ICS设备未Configure：" + DeviceId);
                if (_lifecycle == DeviceLifecycleState.Faulted) throw new InvalidOperationException("Modbus ICS设备Faulted，需先ResetFault：" + DeviceId);
                if (_lifecycle == DeviceLifecycleState.Active) return;
                _connectionState = IcsConnectionState.Connecting;
            }
            try
            {
                await _session.ActivateAsync(DeviceId, token).ConfigureAwait(false);
                _variables.SetConnection(true, null);
                await PollOnceAsync(token).ConfigureAwait(false);
                lock (_gate) { _lifecycle = DeviceLifecycleState.Active; _connectionState = IcsConnectionState.Online; _lastError = null; _consecutiveFailures = 0; _successfulPollCount++; _lastPollUtc = DateTime.UtcNow; }
                _variables.SetConnection(true, null);
                StartPolling();
            }
            catch
            {
                try { await _session.DeactivateAsync(DeviceId, CancellationToken.None).ConfigureAwait(false); } catch { }
                lock (_gate) { _lifecycle = DeviceLifecycleState.Faulted; _connectionState = IcsConnectionState.Faulted; }
                _variables.SetConnection(false, "Activate failed");
                throw;
            }
        }

        public async Task DeactivateAsync(CancellationToken token)
        {
            Task poll = null;
            CancellationTokenSource cancellation = null;
            lock (_gate)
            {
                if (_disposed) return;
                cancellation = _pollCancellation; _pollCancellation = null; poll = _pollTask; _pollTask = null;
                _connectionState = IcsConnectionState.Offline;
                if (_lifecycle != DeviceLifecycleState.Finalized) _lifecycle = DeviceLifecycleState.Inactive;
            }
            if (cancellation != null) cancellation.Cancel();
            if (poll != null) { try { await WaitTaskAsync(poll, _settings.OperationTimeout, token).ConfigureAwait(false); } catch { } }
            if (cancellation != null) cancellation.Dispose();
            await _session.DeactivateAsync(DeviceId, token).ConfigureAwait(false);
            _variables.SetConnection(false, "Deactivated");
        }

        public Task ResetFaultAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_lifecycle == DeviceLifecycleState.Faulted) _lifecycle = DeviceLifecycleState.Inactive;
                _connectionState = IcsConnectionState.Offline; _lastError = null; _consecutiveFailures = 0; _hasCommunicationFailure = false;
            }
            _session.Invalidate();
            _variables.SetConnection(false, "ResetFault");
            return Task.CompletedTask;
        }

        public IndustrialDeviceStatus GetDeviceStatus()
        {
            lock (_gate)
            {
                return new IndustrialDeviceStatus
                {
                    DeviceId = DeviceId,
                    LifecycleState = _lifecycle,
                    IsReadable = _lifecycle == DeviceLifecycleState.Active && _connectionState == IcsConnectionState.Online,
                    IsWritable = _lifecycle == DeviceLifecycleState.Active && _connectionState == IcsConnectionState.Online,
                    Quality = _connectionState == IcsConnectionState.Online ? VariableQuality.Good : _connectionState == IcsConnectionState.Degraded ? VariableQuality.Uncertain : VariableQuality.Disconnected,
                    ErrorCode = _lifecycle == DeviceLifecycleState.Faulted ? "CXV-MODBUS-TCP-FAULT" : null,
                    Message = _lastError,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }

        public ModbusTcpDeviceDiagnostics GetDiagnostics()
        {
            lock (_gate)
            {
                return new ModbusTcpDeviceDiagnostics
                {
                    DeviceId = DeviceId, LifecycleState = _lifecycle, ConnectionState = _connectionState,
                    SuccessfulPollCount = _successfulPollCount, FailedPollCount = _failedPollCount, ReconnectCount = _reconnectCount,
                    ConsecutiveFailures = _consecutiveFailures, LastPollUtc = _lastPollUtc, LastError = _lastError
                };
            }
        }

        public IReadOnlyCollection<CapabilityDescriptor> GetCapabilities()
        {
            return new[]
            {
                new CapabilityDescriptor(IcsCapabilityIds.DeviceLifecycle), new CapabilityDescriptor(IcsCapabilityIds.Variables),
                new CapabilityDescriptor(IcsCapabilityIds.MaintenanceRawAccess), new CapabilityDescriptor(IcsCapabilityIds.Diagnostics),
                new CapabilityDescriptor(ScadaCapabilityIds.RealtimeRead), new CapabilityDescriptor(ScadaCapabilityIds.ControlledWrite)
            };
        }

        public async Task<object> ReadRawAsync(string address, CancellationToken token)
        {
            DemandActive();
            ModbusAddress parsed = ModbusAddress.Parse(address);
            if (parsed.Area == ModbusDataArea.Coil || parsed.Area == ModbusDataArea.DiscreteInput) return (await _session.ReadBitsAsync(_settings.UnitId, parsed.Area, parsed.Offset, 1, token).ConfigureAwait(false))[0];
            return (await _session.ReadRegistersAsync(_settings.UnitId, parsed.Area, parsed.Offset, 1, token).ConfigureAwait(false))[0];
        }

        public async Task WriteRawAsync(string address, object value, CancellationToken token)
        {
            DemandActive();
            ModbusAddress parsed = ModbusAddress.Parse(address);
            if (parsed.Area == ModbusDataArea.Coil) { await _session.WriteSingleCoilAsync(_settings.UnitId, parsed.Offset, Convert.ToBoolean(value, CultureInfo.InvariantCulture), token).ConfigureAwait(false); return; }
            if (parsed.Area != ModbusDataArea.HoldingRegister) throw new InvalidOperationException("Modbus Raw写只允许Coil/HoldingRegister。");
            await _session.WriteSingleRegisterAsync(_settings.UnitId, parsed.Offset, Convert.ToUInt16(value, CultureInfo.InvariantCulture), token).ConfigureAwait(false);
        }

        internal async Task<OperationResult> WriteTagAsync(string tagId, object value, CancellationToken token)
        {
            DemandActive();
            ModbusTagBinding binding = _variables.GetBinding(tagId);
            IcsTagDefinition tag = _variables.GetDefinition(tagId);
            if (tag.Access != VariableAccess.ReadWrite) return OperationResult.Fail("CXV-MODBUS-READONLY", "ICS", "Tag只读：" + tagId);
            try
            {
                if (binding.Address.Area == ModbusDataArea.Coil)
                {
                    bool target = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                    await _session.WriteSingleCoilAsync(_settings.UnitId, binding.Address.Offset, target, token).ConfigureAwait(false);
                    if (_settings.VerifyWrites)
                    {
                        bool actual = (await _session.ReadBitsAsync(_settings.UnitId, ModbusDataArea.Coil, binding.Address.Offset, 1, token).ConfigureAwait(false))[0];
                        if (actual != target) return OperationResult.Fail("CXV-MODBUS-READBACK", "ICS", "Coil写入回读不一致：" + tagId);
                    }
                    _variables.Publish(tagId, target, VariableQuality.Good);
                }
                else if (binding.Address.Area == ModbusDataArea.HoldingRegister)
                {
                    ushort[] registers = ModbusValueCodec.EncodeRegisters(value, tag.DataType, _settings.ByteOrder);
                    if (registers.Length == 1) await _session.WriteSingleRegisterAsync(_settings.UnitId, binding.Address.Offset, registers[0], token).ConfigureAwait(false);
                    else await _session.WriteMultipleRegistersAsync(_settings.UnitId, binding.Address.Offset, registers, token).ConfigureAwait(false);
                    object converted = ModbusValueCodec.DecodeRegisters(registers, 0, tag.DataType, _settings.ByteOrder);
                    if (_settings.VerifyWrites)
                    {
                        ushort[] actualRegisters = await _session.ReadRegistersAsync(_settings.UnitId, ModbusDataArea.HoldingRegister, binding.Address.Offset, checked((ushort)registers.Length), token).ConfigureAwait(false);
                        object actual = ModbusValueCodec.DecodeRegisters(actualRegisters, 0, tag.DataType, _settings.ByteOrder);
                        if (!ModbusValueCodec.Equivalent(converted, actual, tag.Deadband)) return OperationResult.Fail("CXV-MODBUS-READBACK", "ICS", "Holding Register写入回读不一致：" + tagId);
                        converted = actual;
                    }
                    _variables.Publish(tagId, converted, VariableQuality.Good);
                }
                else return OperationResult.Fail("CXV-MODBUS-READONLY", "ICS", "Input/Discrete区不允许写入：" + tagId);
                return OperationResult.Ok();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                OnCommunicationFailure(ex);
                return OperationResult.Fail("CXV-MODBUS-WRITE", "ICS", ex.GetBaseException().Message);
            }
        }

        private void StartPolling()
        {
            lock (_gate)
            {
                if (_pollTask != null && !_pollTask.IsCompleted) return;
                _pollCancellation = new CancellationTokenSource();
                CancellationToken token = _pollCancellation.Token;
                _pollTask = PollLoopAsync(token);
            }
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (_disposed) return;
                        if (_hasCommunicationFailure) { _reconnectCount++; _hasCommunicationFailure = false; }
                        _consecutiveFailures = 0; _connectionState = IcsConnectionState.Online; _lastError = null;
                        if (_lifecycle != DeviceLifecycleState.Finalized) _lifecycle = DeviceLifecycleState.Active;
                        _lastPollUtc = DateTime.UtcNow; _successfulPollCount++;
                    }
                    _variables.SetConnection(true, null);
                    await Task.Delay(_settings.PollIntervalMilliseconds, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { if (token.IsCancellationRequested) return; throw; }
                catch (Exception ex)
                {
                    bool faulted = OnCommunicationFailure(ex);
                    if (faulted)
                    {
                        try { await _session.DeactivateAsync(DeviceId, CancellationToken.None).ConfigureAwait(false); } catch { }
                        return;
                    }
                    int attempt;
                    lock (_gate) attempt = _consecutiveFailures;
                    double baseDelay = Math.Max(1.0, _settings.ReconnectMinimum.TotalMilliseconds);
                    double maxDelay = Math.Max(baseDelay, _settings.ReconnectMaximum.TotalMilliseconds);
                    int delay = (int)Math.Min(maxDelay, baseDelay * Math.Pow(2, Math.Max(0, attempt - 1)));
                    try { await Task.Delay(delay, token).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
                }
            }
        }

        private async Task PollOnceAsync(CancellationToken token)
        {
            foreach (ModbusPollGroup group in _variables.PollGroups)
            {
                token.ThrowIfCancellationRequested();
                if (group.Area == ModbusDataArea.Coil || group.Area == ModbusDataArea.DiscreteInput)
                {
                    bool[] values = await _session.ReadBitsAsync(_settings.UnitId, group.Area, group.Start, group.Count, token).ConfigureAwait(false);
                    foreach (ModbusTagBinding binding in group.Bindings) _variables.Publish(binding.TagId, values[binding.Address.Offset - group.Start], VariableQuality.Good);
                }
                else
                {
                    ushort[] registers = await _session.ReadRegistersAsync(_settings.UnitId, group.Area, group.Start, group.Count, token).ConfigureAwait(false);
                    foreach (ModbusTagBinding binding in group.Bindings)
                    {
                        int offset = binding.Address.Offset - group.Start;
                        object value = ModbusValueCodec.DecodeRegisters(registers, offset, binding.DataType, _settings.ByteOrder);
                        _variables.Publish(binding.TagId, value, VariableQuality.Good);
                    }
                }
            }
        }

        private bool OnCommunicationFailure(Exception error)
        {
            _session.Invalidate();
            _variables.SetConnection(false, error.GetBaseException().Message);
            lock (_gate)
            {
                _lastError = error.GetBaseException().Message;
                _consecutiveFailures++; _failedPollCount++; _hasCommunicationFailure = true;
                if (_consecutiveFailures >= _settings.MaximumConsecutiveFailures)
                {
                    _connectionState = IcsConnectionState.Faulted; _lifecycle = DeviceLifecycleState.Faulted; return true;
                }
                _connectionState = IcsConnectionState.Degraded;
                return false;
            }
        }

        private void DemandActive()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_lifecycle != DeviceLifecycleState.Active || (_connectionState != IcsConnectionState.Online && _connectionState != IcsConnectionState.Degraded))
                    throw new InvalidOperationException("Modbus ICS设备未Active：" + DeviceId);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { DeactivateAsync(CancellationToken.None).GetAwaiter().GetResult(); } catch { }
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _lifecycle = DeviceLifecycleState.Finalized; _connectionState = IcsConnectionState.Offline;
            }
            _variables.Dispose();
            if (_releaseSession != null) _releaseSession();
        }

        private static async Task WaitTaskAsync(Task task, TimeSpan timeout, CancellationToken token)
        {
            Task delay = Task.Delay(timeout, token);
            Task completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (completed != task) throw new TimeoutException("等待Modbus Poll Worker停止超时。");
            await task.ConfigureAwait(false);
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ModbusTcpDevice)); }
    }

    internal sealed class ModbusVariableProvider : IVariableProvider, IVariableProviderCapabilities
    {
        private sealed class Subscription : IVariableSubscription
        {
            private readonly ModbusVariableProvider _owner; private readonly Guid _id; private bool _disposed;
            public Subscription(ModbusVariableProvider owner, Guid id, IReadOnlyCollection<string> ids) { _owner = owner; _id = id; VariableIds = ids; }
            public IReadOnlyCollection<string> VariableIds { get; private set; }
            public void Dispose() { if (_disposed) return; _disposed = true; _owner.Remove(_id); }
        }
        private sealed class Sink { public HashSet<string> Ids; public Action<IReadOnlyList<VariableChange>> Callback; }

        private readonly object _gate = new object();
        private readonly Dictionary<string, VariableMetadata> _metadata = new Dictionary<string, VariableMetadata>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IcsTagDefinition> _definitions = new Dictionary<string, IcsTagDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModbusTagBinding> _bindings = new Dictionary<string, ModbusTagBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, VariableValue> _values = new Dictionary<string, VariableValue>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, Sink> _subscriptions = new Dictionary<Guid, Sink>();
        private ModbusTcpDevice _owner;
        private bool _connected;
        private bool _disposed;
        private long _sequence;
        private DateTime? _lastUpdateUtc;
        private string _lastError;

        public ModbusVariableProvider(string providerId, IEnumerable<IcsTagDefinition> tags, ModbusTcpSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ProviderId = providerId;
            List<ModbusTagBinding> all = new List<ModbusTagBinding>();
            foreach (IcsTagDefinition tag in tags ?? Enumerable.Empty<IcsTagDefinition>())
            {
                tag.Validate();
                ModbusAddress address = ModbusAddress.Parse(tag.Address);
                Type valueType = address.Area == ModbusDataArea.Coil || address.Area == ModbusDataArea.DiscreteInput ? typeof(bool) : ModbusValueCodec.GetClrType(tag.DataType);
                if ((address.Area == ModbusDataArea.Coil || address.Area == ModbusDataArea.DiscreteInput) && valueType != typeof(bool)) throw new InvalidOperationException("Modbus Coil/Discrete Tag必须为Boolean：" + tag.TagId);
                if ((address.Area == ModbusDataArea.InputRegister || address.Area == ModbusDataArea.DiscreteInput) && tag.Access == VariableAccess.ReadWrite) throw new InvalidOperationException("Modbus Input/Discrete区必须只读：" + tag.TagId);
                ModbusTagBinding binding = new ModbusTagBinding { TagId = tag.TagId, Address = address, DataType = tag.DataType, RegisterCount = address.Area == ModbusDataArea.Coil || address.Area == ModbusDataArea.DiscreteInput ? 0 : ModbusValueCodec.GetRegisterCount(tag.DataType), Deadband = tag.Deadband };
                _bindings.Add(tag.TagId, binding); all.Add(binding); _definitions.Add(tag.TagId, tag);
                _metadata.Add(tag.TagId, new VariableMetadata { Id = tag.TagId, ProviderId = providerId, ProviderIdentityKey = providerId, Name = tag.Name, Path = tag.TagId, Address = address.ToString(), ValueType = valueType, Unit = tag.Unit, Description = tag.Description, Access = tag.Access, Minimum = tag.Minimum, Maximum = tag.Maximum });
                _values.Add(tag.TagId, new VariableValue(DefaultValue(valueType), VariableQuality.Disconnected, DateTime.UtcNow, 0, providerId));
            }
            PollGroups = ModbusPollPlanner.Build(all, settings.MaximumRegistersPerRead, settings.MaximumBitsPerRead, settings.MaximumRegisterGap, settings.MaximumBitGap);
        }

        public string ProviderId { get; private set; }
        public IReadOnlyList<ModbusPollGroup> PollGroups { get; private set; }
        public Task<VariableValue> ReadAsync(string variableId, CancellationToken token) => CxVariableProviderObservations.ReadCacheAsync(() => Read(variableId), token);
        public CxVariableProviderState GetState() { lock (_gate) return _disposed ? CxVariableProviderState.Stopped : CxVariableProviderObservations.GetState(GetDiagnostics()); }
        public Task<ProviderDiagnostics> GetHealthAsync(CancellationToken token) { lock (_gate) return CxVariableProviderObservations.GetHealthAsync(GetDiagnostics, token, GetState()); }
        internal void AttachOwner(ModbusTcpDevice owner) { _owner = owner; }
        internal ModbusTagBinding GetBinding(string id) { lock (_gate) { ModbusTagBinding value; if (!_bindings.TryGetValue(id ?? string.Empty, out value)) throw new KeyNotFoundException("Modbus Tag不存在：" + id); return value; } }
        internal IcsTagDefinition GetDefinition(string id) { lock (_gate) { IcsTagDefinition value; if (!_definitions.TryGetValue(id ?? string.Empty, out value)) throw new KeyNotFoundException("Modbus Tag不存在：" + id); return value; } }

        public IReadOnlyList<VariableMetadata> GetMetadata() { lock (_gate) { ThrowIfDisposed(); return new ReadOnlyCollection<VariableMetadata>(_metadata.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList()); } }
        public VariableValue Read(string variableId) { lock (_gate) { ThrowIfDisposed(); VariableValue value; if (!_values.TryGetValue(variableId ?? string.Empty, out value)) throw new KeyNotFoundException("Modbus Tag不存在：" + variableId); return value; } }
        public Task<OperationResult> WriteAsync(string variableId, object value, CancellationToken cancellationToken)
        {
            ModbusTcpDevice owner = _owner;
            if (owner == null) return Task.FromResult(OperationResult.Fail("CXV-MODBUS-NO-OWNER", "ICS", "Modbus VariableProvider尚未绑定设备。"));
            return owner.WriteTagAsync(variableId, value, cancellationToken);
        }
        public IVariableSubscription Subscribe(IEnumerable<string> variableIds, Action<IReadOnlyList<VariableChange>> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            HashSet<string> ids = new HashSet<string>((variableIds ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            lock (_gate)
            {
                ThrowIfDisposed(); foreach (string id in ids) if (!_metadata.ContainsKey(id)) throw new KeyNotFoundException("Modbus Tag不存在：" + id);
                Guid key = Guid.NewGuid(); _subscriptions.Add(key, new Sink { Ids = ids, Callback = callback });
                return new Subscription(this, key, new ReadOnlyCollection<string>(ids.ToList()));
            }
        }
        public ProviderDiagnostics GetDiagnostics()
        {
            lock (_gate) return new ProviderDiagnostics { ProviderId = ProviderId, IsConnected = _connected, VariableCount = _metadata.Count, SubscriptionCount = _subscriptions.Count, LastUpdateUtc = _lastUpdateUtc, LastError = _lastError };
        }
        public IReadOnlyCollection<CapabilityDescriptor> GetCapabilities() { return new[] { new CapabilityDescriptor(ScadaCapabilityIds.RealtimeRead), new CapabilityDescriptor(ScadaCapabilityIds.ControlledWrite) }; }

        internal void Publish(string id, object value, VariableQuality quality)
        {
            List<VariableChange> changes = null;
            lock (_gate)
            {
                if (_disposed) return;
                VariableMetadata metadata; VariableValue old;
                if (!_metadata.TryGetValue(id, out metadata) || !_values.TryGetValue(id, out old)) return;
                object converted = ConvertTo(value, metadata.ValueType);
                IcsTagDefinition definition = _definitions[id];
                bool changed = old.Quality != quality || !EquivalentWithDeadband(old.Value, converted, definition.Deadband);
                VariableValue next = new VariableValue(converted, quality, DateTime.UtcNow, ++_sequence, ProviderId);
                _values[id] = next; _lastUpdateUtc = next.TimestampUtc;
                if (changed) changes = new List<VariableChange> { new VariableChange { VariableId = id, Value = next } };
            }
            if (changes != null) Notify(changes);
        }

        internal void SetConnection(bool connected, string error)
        {
            List<VariableChange> changes = new List<VariableChange>();
            lock (_gate)
            {
                if (_disposed) return;
                _connected = connected; _lastError = connected ? null : error;
                foreach (string id in _values.Keys.ToList())
                {
                    VariableValue old = _values[id]; VariableQuality quality = connected ? (old.Quality == VariableQuality.Disconnected ? VariableQuality.Uncertain : old.Quality) : VariableQuality.Disconnected;
                    if (old.Quality == quality) continue;
                    VariableValue next = new VariableValue(old.Value, quality, DateTime.UtcNow, ++_sequence, ProviderId);
                    _values[id] = next; changes.Add(new VariableChange { VariableId = id, Value = next });
                }
            }
            if (changes.Count > 0) Notify(changes);
        }

        private void Notify(IReadOnlyList<VariableChange> changes)
        {
            List<Sink> sinks; lock (_gate) sinks = _subscriptions.Values.ToList();
            foreach (Sink sink in sinks)
            {
                List<VariableChange> filtered = changes.Where(x => sink.Ids.Contains(x.VariableId)).ToList();
                if (filtered.Count == 0) continue;
                try { sink.Callback(new ReadOnlyCollection<VariableChange>(filtered)); } catch { }
            }
        }
        private void Remove(Guid id) { lock (_gate) if (!_disposed) _subscriptions.Remove(id); }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _subscriptions.Clear(); _metadata.Clear(); _definitions.Clear(); _bindings.Clear(); _values.Clear(); } }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ModbusVariableProvider)); }
        private static object DefaultValue(Type type) { if (type == typeof(string)) return string.Empty; return type.IsValueType ? Activator.CreateInstance(type) : null; }
        private static object ConvertTo(object value, Type type) { if (value == null || type.IsInstanceOfType(value)) return value; return Convert.ChangeType(value, type, CultureInfo.InvariantCulture); }
        private static bool EquivalentWithDeadband(object left, object right, double? deadband)
        {
            if (!deadband.HasValue || deadband.Value <= 0) return ModbusValueCodec.Equivalent(left, right);
            try { return Math.Abs(Convert.ToDouble(left, CultureInfo.InvariantCulture) - Convert.ToDouble(right, CultureInfo.InvariantCulture)) < deadband.Value; }
            catch { return ModbusValueCodec.Equivalent(left, right); }
        }
    }
}
