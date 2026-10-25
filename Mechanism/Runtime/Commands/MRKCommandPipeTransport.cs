using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;

namespace CxVisual.Mechanism.Runtime.Commands
{
    /// <summary>
    /// Windows命名管道是真实OS IPC；同PC进程间可调用，同Windows域跨PC仅为平台能力，
    /// 未完成现场部署与SMB/Kerberos/ACL校验不得宣称跨PC已验收。
    /// 白名单值帧来自Foundation.Data，认证后仍由目标Host执行正式Command授权。
    /// </summary>
    internal static class MRKCommandPipeWire
    {
        internal static void ValidatePipeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128 ||
                name.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')))
                throw new ArgumentException("命名管道标识必须是1~128个字母数字或._-", "name");
        }
        private static string StringValue(IReadOnlyDictionary<string, object> values, string key)
        { return (string)values[key]; }
        private static int IntValue(IReadOnlyDictionary<string, object> values, string key)
        { return (int)values[key]; }
        private static long LongValue(IReadOnlyDictionary<string, object> values, string key)
        { return (long)values[key]; }
        private static Guid GuidValue(IReadOnlyDictionary<string, object> values, string key)
        { return (Guid)values[key]; }
        internal static byte[] RequestFrame(MRKCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            IReadOnlyDictionary<string, object> inputs =
                FDCCommandValueSnapshot.Capture(request.Inputs);
            Dictionary<string, object> nested = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in inputs) nested.Add(item.Key, item.Value);
            return FDCCommandValueCodec.Encode(new Dictionary<string, object> {
                { "kind", "request" }, { "target", request.Target },
                { "code", request.CommandCode }, { "commandId", request.CommandId },
                { "version", request.ExpectedSchemaVersion },
                { "requestId", request.RequestId }, { "operationId", request.OperationId },
                { "timeout", request.Options.TimeoutMilliseconds },
                { "priority", request.Options.Priority },
                { "remote", request.Options.Remote }, { "inputs", nested }
            });
        }
        internal static string ReadKind(byte[] frame)
        {
            IReadOnlyDictionary<string, object> envelope = FDCCommandValueCodec.Decode(frame);
            return StringValue(envelope, "kind");
        }
        private static bool IsOperationAction(string action)
        {
            return action == "query" || action == "cancel" ||
                action == "pause" || action == "resume";
        }
        private static byte[] OperationFrame(string action, Guid requestId, Guid operationId)
        {
            if (!IsOperationAction(action))
                throw new ArgumentException("未知命令操作动作", "action");
            if (requestId == Guid.Empty || operationId == Guid.Empty)
                throw new ArgumentException("命令操作关联身份不能为空");
            return FDCCommandValueCodec.Encode(new Dictionary<string, object> {
                { "kind", action }, { "requestId", requestId },
                { "operationId", operationId }
            });
        }
        internal static byte[] QueryFrame(Guid requestId, Guid operationId)
        { return OperationFrame("query", requestId, operationId); }
        internal static byte[] ControlFrame(string action, Guid requestId, Guid operationId)
        {
            if (action != "cancel" && action != "pause" && action != "resume")
                throw new ArgumentException("不支持的远程控制动作", "action");
            return OperationFrame(action, requestId, operationId);
        }
        internal static void ParseOperationFrame(byte[] frame, string action,
            out Guid requestId, out Guid operationId)
        {
            if (!IsOperationAction(action))
                throw new InvalidDataException("非法命令操作类型");
            IReadOnlyDictionary<string, object> envelope = FDCCommandValueCodec.Decode(frame);
            if (StringValue(envelope, "kind") != action)
                throw new InvalidDataException("命令操作帧动作不匹配");
            requestId = GuidValue(envelope, "requestId");
            operationId = GuidValue(envelope, "operationId");
            if (requestId == Guid.Empty || operationId == Guid.Empty)
                throw new InvalidDataException("命令操作关联身份不能为空");
        }
        internal static void ParseQuery(byte[] frame, out Guid requestId, out Guid operationId)
        { ParseOperationFrame(frame, "query", out requestId, out operationId); }
        internal static MRKCommandRequest ParseRequest(byte[] frame, string trustedCaller)
        {
            IReadOnlyDictionary<string, object> values = FDCCommandValueCodec.Decode(frame);
            if (StringValue(values, "kind") != "request" ||
                !((bool)values["remote"]))
                throw new InvalidDataException("IPC只接受明确的远程命令请求");
            IReadOnlyDictionary<string, object> inputs =
                values["inputs"] as IReadOnlyDictionary<string, object>;
            if (inputs == null) throw new InvalidDataException("命令输入不是命名对象");
            return new MRKCommandRequest(StringValue(values, "target"),
                StringValue(values, "code"), inputs, trustedCaller,
                GuidValue(values, "operationId"), GuidValue(values, "requestId"),
                new MRKCommandOptions(IntValue(values, "timeout"), false,
                    IntValue(values, "priority")), IntValue(values, "version"),
                LongValue(values, "commandId"));
        }
        internal static byte[] ResponseFrame(MRKCommandResponse response)
        {
            IReadOnlyDictionary<string, object> values = response.Outputs;
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in values) result.Add(item.Key, item.Value);
            return FDCCommandValueCodec.Encode(new Dictionary<string, object> {
                { "kind", "response" }, { "requestId", response.RequestId },
                { "operationId", response.OperationId },
                { "stage", (int)response.Stage }, { "state", (int)response.State },
                { "businessSuccess", response.BusinessSuccess.HasValue ?
                    (object)response.BusinessSuccess.Value : null },
                { "businessCode", response.BusinessCode }, { "errorCode", response.ErrorCode },
                { "error", response.Error }, { "duration", response.DurationMilliseconds },
                { "outputs", result }
            });
        }
        internal static MRKCommandResponse ParseResponse(byte[] frame,
            Guid expectedRequestId, Guid expectedOperationId)
        {
            IReadOnlyDictionary<string, object> values = FDCCommandValueCodec.Decode(frame);
            if (StringValue(values, "kind") != "response" ||
                GuidValue(values, "requestId") != expectedRequestId ||
                GuidValue(values, "operationId") != expectedOperationId)
                throw new InvalidDataException("远程应答关联身份不匹配");
            int stage = IntValue(values, "stage"), state = IntValue(values, "state");
            if (!Enum.IsDefined(typeof(MRKCommandStage), stage) ||
                !Enum.IsDefined(typeof(MRKCommandState), state))
                throw new InvalidDataException("远程应答状态不在正式枚举范围");
            IReadOnlyDictionary<string, object> outputs =
                values["outputs"] as IReadOnlyDictionary<string, object>;
            if (outputs == null) throw new InvalidDataException("远程输出不是命名对象");
            return new MRKCommandResponse(expectedRequestId, expectedOperationId,
                (MRKCommandStage)stage, (MRKCommandState)state, outputs,
                values["businessSuccess"] == null ? (bool?)null :
                    (bool)values["businessSuccess"],
                StringValue(values, "businessCode"),
                StringValue(values, "errorCode"), StringValue(values, "error"),
                (double)values["duration"]);
        }
        internal static async Task WriteAsync(Stream stream, byte[] frame,
            CancellationToken cancellation)
        {
            if (frame == null || frame.Length > FDCCommandValueCodec.MaxFrameBytes)
                throw new ArgumentException("IPC数据帧超限", "frame");
            byte[] length = BitConverter.GetBytes(frame.Length);
            await stream.WriteAsync(length, 0, length.Length, cancellation).ConfigureAwait(false);
            await stream.WriteAsync(frame, 0, frame.Length, cancellation).ConfigureAwait(false);
            await stream.FlushAsync(cancellation).ConfigureAwait(false);
        }
        private static async Task ReadFullyAsync(Stream stream, byte[] bytes,
            CancellationToken cancellation)
        {
            int pos = 0;
            while (pos < bytes.Length)
            {
                int got = await stream.ReadAsync(bytes, pos, bytes.Length - pos, cancellation)
                    .ConfigureAwait(false);
                if (got == 0) throw new EndOfStreamException("远程命令帧未完整交付");
                pos += got;
            }
        }
        internal static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellation)
        {
            byte[] prefix = new byte[4];
            await ReadFullyAsync(stream, prefix, cancellation).ConfigureAwait(false);
            int size = BitConverter.ToInt32(prefix, 0);
            if (size < 9 || size > FDCCommandValueCodec.MaxFrameBytes)
                throw new InvalidDataException("远程命令帧长度无效");
            byte[] frame = new byte[size];
            await ReadFullyAsync(stream, frame, cancellation).ConfigureAwait(false);
            return frame;
        }
    }

    /// <summary>
    /// 远程客户端：真实命名管道IPC，单次请求单次连接。失败后Operation状态Unknown，
    /// 不能因没有收到应答自动重发设备动作；同OperationId可由受控上层核对。
    /// </summary>
    public sealed class MRKCommandPipeClient
    {
        private readonly string _host;
        private readonly string _pipe;
        private readonly int _connectTimeout;
        private readonly int _exchangeTimeout;
        public MRKCommandPipeClient(string serverName, string pipeName,
            int connectTimeoutMilliseconds = 5000, int exchangeTimeoutMilliseconds = 30000)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("服务器名不能为空", "serverName");
            MRKCommandPipeWire.ValidatePipeName(pipeName);
            if (connectTimeoutMilliseconds < 1 || connectTimeoutMilliseconds > 60000)
                throw new ArgumentOutOfRangeException("connectTimeoutMilliseconds");
            if (exchangeTimeoutMilliseconds < 1 || exchangeTimeoutMilliseconds > 600000)
                throw new ArgumentOutOfRangeException("exchangeTimeoutMilliseconds");
            _host = serverName; _pipe = pipeName;
            _connectTimeout = connectTimeoutMilliseconds;
            _exchangeTimeout = exchangeTimeoutMilliseconds;
        }
        public Task<MRKCommandResponse> InvokeAsync(MRKCommandRequest request,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (request == null) throw new ArgumentNullException("request");
            if (!request.Options.Remote)
                throw new ArgumentException("IPC客户端仅接受Remote=true的命令请求", "request");
            return RoundTripAsync(MRKCommandPipeWire.RequestFrame(request),
                request.RequestId, request.OperationId, cancellation);
        }
        /// <summary>
        /// Query请求有新的RequestId但保持原OperationId，Host仅允许原Windows调用者查询。
        /// 可在调用超时/连接中断后核对原操作，不自动重新执行命令。
        /// </summary>
        public Task<MRKCommandResponse> QueryAsync(Guid operationId,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (operationId == Guid.Empty) throw new ArgumentException("操作ID不能为空", "operationId");
            Guid requestId = Guid.NewGuid();
            return RoundTripAsync(MRKCommandPipeWire.QueryFrame(requestId, operationId),
                requestId, operationId, cancellation);
        }
        /// <summary>远程取消仅请求Handler协作停止，不能替代工业设备安全联锁。</summary>
        public Task<MRKCommandResponse> CancelAsync(Guid operationId,
            CancellationToken cancellation = default(CancellationToken))
        { return SendControlAsync("cancel", operationId, cancellation); }
        public Task<MRKCommandResponse> PauseAsync(Guid operationId,
            CancellationToken cancellation = default(CancellationToken))
        { return SendControlAsync("pause", operationId, cancellation); }
        public Task<MRKCommandResponse> ResumeAsync(Guid operationId,
            CancellationToken cancellation = default(CancellationToken))
        { return SendControlAsync("resume", operationId, cancellation); }
        private Task<MRKCommandResponse> SendControlAsync(string action, Guid operationId,
            CancellationToken cancellation)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("操作ID不能为空", "operationId");
            Guid requestId = Guid.NewGuid();
            return RoundTripAsync(MRKCommandPipeWire.ControlFrame(action, requestId, operationId),
                requestId, operationId, cancellation);
        }
        private static bool IsBusyConnectionError(IOException error)
        {
            // Win32 ERROR_SEM_TIMEOUT(121) / ERROR_PIPE_BUSY(231)：尚未建立连接，
            // 因而尚未发送任何命令帧，可在连接预算内安全地再次尝试连接。
            int nativeCode = error.HResult & 0xFFFF;
            return nativeCode == 121 || nativeCode == 231;
        }

        private async Task<NamedPipeClientStream> ConnectWithBusyRetryAsync(
            CancellationToken cancellation)
        {
            Stopwatch budget = Stopwatch.StartNew();
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                int remaining = _connectTimeout - (int)budget.ElapsedMilliseconds;
                if (remaining <= 0)
                    throw new TimeoutException("IPC连接预算耗尽，尚未交付命令请求");
                NamedPipeClientStream client = new NamedPipeClientStream(
                    _host, _pipe, PipeDirection.InOut, PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Identification);
                try
                {
                    await client.ConnectAsync(remaining, cancellation).ConfigureAwait(false);
                    return client;
                }
                catch (IOException error) when (IsBusyConnectionError(error) &&
                    budget.ElapsedMilliseconds < _connectTimeout)
                {
                    client.Dispose();
                    // 仅OS连接阶段重试。连接成功后交付请求失败不能自动重新执行。
                    remaining = _connectTimeout - (int)budget.ElapsedMilliseconds;
                    if (remaining > 0)
                        await Task.Delay(Math.Min(50, remaining), cancellation)
                            .ConfigureAwait(false);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            }
        }

        private static async Task<MRKCommandResponse> ExchangeAsync(NamedPipeClientStream client,
            byte[] frame, Guid requestId, Guid operationId, CancellationToken cancellation)
        {
            await MRKCommandPipeWire.WriteAsync(client, frame, cancellation).ConfigureAwait(false);
            byte[] response = await MRKCommandPipeWire.ReadAsync(client, cancellation)
                .ConfigureAwait(false);
            return MRKCommandPipeWire.ParseResponse(response, requestId, operationId);
        }
        private async Task<MRKCommandResponse> RoundTripAsync(byte[] frame,
            Guid requestId, Guid operationId, CancellationToken cancellation)
        {
            try
            {
                using (CancellationTokenSource wait = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellation))
                using (NamedPipeClientStream client =
                    await ConnectWithBusyRetryAsync(wait.Token).ConfigureAwait(false))
                {
                    // .NET48部分驱动的PipeStream异步读取不一定及时响应Token取消。
                    // Task.WhenAny保证本端不会因对端已连接但不应答而无限挂起。
                    Task<MRKCommandResponse> exchange = ExchangeAsync(client, frame,
                        requestId, operationId, wait.Token);
                    Task timeout = Task.Delay(_exchangeTimeout, wait.Token);
                    Task winner = await Task.WhenAny(exchange, timeout).ConfigureAwait(false);
                    if (winner != exchange)
                    {
                        wait.Cancel();
                        client.Dispose(); // 主动释放受阻ReadAsync，不强制终止远端业务操作。
                        // 操作取消后未必马上退出；观测可能延迟抛出的底层IOException。
                        Task observe = exchange.ContinueWith(t =>
                        {
                            if (t.IsFaulted) { AggregateException ignored = t.Exception; }
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
                        return new MRKCommandResponse(requestId, operationId,
                            MRKCommandStage.Unknown, MRKCommandState.Unknown,
                            errorCode: cancellation.IsCancellationRequested
                                ? "CallerCancelled" : "RemoteExchangeTimeout",
                            error: "远程通道应答中断或超时，外部副作用尚不能确认；必须Query核对");
                    }
                    MRKCommandResponse response = await exchange.ConfigureAwait(false);
                    wait.Cancel();
                    return response;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return new MRKCommandResponse(requestId, operationId,
                    MRKCommandStage.Rejected, MRKCommandState.Failed,
                    errorCode: "RemoteUnauthorized", error: ex.Message);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is IOException ||
                ex is OperationCanceledException || ex is InvalidDataException ||
                ex is InvalidCastException)
            {
                return new MRKCommandResponse(requestId, operationId,
                    MRKCommandStage.Unknown, MRKCommandState.Unknown,
                    errorCode: "RemoteUnknown", error:
                        "远程是否已接收或执行无法确认；禁止自动重发：" + ex.Message);
            }
        }
    }

    /// <summary>
    /// 默认只允许当前Windows用户创建/连接管道；必须额外显式提供对等主体授权，
    /// 设备命令仍须由接收Host的MRK授权器二次校验。
    /// </summary>
    public sealed class MRKCommandPipeServer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MRKCommandCenter _center;
        private readonly string _name;
        private readonly Func<string, bool> _authorizePeer;
        private readonly int _requestReadTimeoutMilliseconds;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly List<Task> _sessions = new List<Task>();
        private readonly SemaphoreSlim _slots = new SemaphoreSlim(8, 8);
        private NamedPipeServerStream _listener;
        private Task _accept;
        private bool _started;
        public MRKCommandPipeServer(MRKCommandCenter center, string name,
            Func<string, bool> authorizePeer, int requestReadTimeoutMilliseconds = 5000)
        {
            _center = center ?? throw new ArgumentNullException("center");
            MRKCommandPipeWire.ValidatePipeName(name);
            _name = name;
            _authorizePeer = authorizePeer ?? throw new ArgumentNullException("authorizePeer");
            if (requestReadTimeoutMilliseconds < 1 || requestReadTimeoutMilliseconds > 600000)
                throw new ArgumentOutOfRangeException("requestReadTimeoutMilliseconds");
            _requestReadTimeoutMilliseconds = requestReadTimeoutMilliseconds;
        }

        private NamedPipeServerStream NewPipe()
        {
            WindowsIdentity current = WindowsIdentity.GetCurrent();
            if (current == null || current.User == null)
                throw new InvalidOperationException("无法确定命名管道Host身份");
            PipeSecurity acl = new PipeSecurity();
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new PipeAccessRule(current.User,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            return new NamedPipeServerStream(_name, PipeDirection.InOut, 8,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, acl);
        }
        public void Start()
        {
            lock (_gate)
            {
                if (_started) throw new InvalidOperationException("IPC Server已启动");
                if (_stop.IsCancellationRequested)
                    throw new ObjectDisposedException("MRKCommandPipeServer");
                _started = true;
                _accept = AcceptLoopAsync();
            }
        }
        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;
                bool transferred = false;
                bool admitted = false;
                try
                {
                    await _slots.WaitAsync(_stop.Token).ConfigureAwait(false);
                    admitted = true;
                    pipe = NewPipe();
                    lock (_gate) _listener = pipe;
                    await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                    if (_stop.IsCancellationRequested) break;
                    NamedPipeServerStream accepted = pipe;
                    Task task = ProcessSingleAsync(accepted);
                    transferred = true;
                    lock (_gate) _sessions.Add(task);
                    Task cleanup = task.ContinueWith(done =>
                    {
                        lock (_gate) _sessions.Remove(done);
                        _slots.Release();
                    }, CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (ObjectDisposedException) when (_stop.IsCancellationRequested) { break; }
                finally
                {
                    lock (_gate) if (object.ReferenceEquals(_listener, pipe)) _listener = null;
                    if (!transferred)
                    {
                        if (pipe != null) pipe.Dispose();
                        if (admitted) _slots.Release();
                    }
                }
            }
        }
        private async Task ProcessSingleAsync(NamedPipeServerStream pipe)
        {
            using (pipe)
            {
                try
                {
                    string peer = pipe.GetImpersonationUserName();
                    if (string.IsNullOrWhiteSpace(peer) || !_authorizePeer(peer))
                    {
                        // 不回送任何Handler数据；客户端只能得到拒绝或未知。
                        return;
                    }
                    // 完整请求帧必须在规定时间内收到；空连接/慢速半帧不能永久占用8个会话名额。
                    // .NET48底层PipeStream不总是及时响应CancellationToken，
                    // 因此以WhenAny独立限定等待，并在外层using中主动释放管道。
                    byte[] bytes;
                    using (CancellationTokenSource readDeadline =
                        CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                    {
                        Task<byte[]> read = MRKCommandPipeWire.ReadAsync(pipe, readDeadline.Token);
                        Task deadline = Task.Delay(_requestReadTimeoutMilliseconds, readDeadline.Token);
                        if (await Task.WhenAny(read, deadline).ConfigureAwait(false) != read)
                        {
                            readDeadline.Cancel();
                            // 连接即将Dispose；仍需观察可能延迟产生的IO异常。
                            Task observed = read.ContinueWith(t =>
                            {
                                if (t.IsFaulted) { AggregateException ignored = t.Exception; }
                            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                                TaskScheduler.Default);
                            return;
                        }
                        bytes = await read.ConfigureAwait(false);
                        readDeadline.Cancel();
                    }
                    MRKCommandResponse response;
                    string kind = MRKCommandPipeWire.ReadKind(bytes);
                    if (kind == "request")
                    {
                        MRKCommandRequest request = MRKCommandPipeWire.ParseRequest(bytes, peer);
                        response = await _center.InvokeAsync(request, _stop.Token)
                            .ConfigureAwait(false);
                    }
                    else if (kind == "query")
                    {
                        Guid requestId, operationId;
                        MRKCommandPipeWire.ParseQuery(bytes, out requestId, out operationId);
                        MRKCommandResponse observed = _center.QueryForCaller(operationId, peer);
                        response = observed == null
                            ? new MRKCommandResponse(requestId, operationId,
                                MRKCommandStage.Rejected, MRKCommandState.Failed,
                                errorCode: "OperationNotFoundOrUnauthorized",
                                error: "无权限查询或操作不存在")
                            : observed.Copy(null, requestId);
                    }
                    else if (kind == "cancel" || kind == "pause" || kind == "resume")
                    {
                        Guid requestId, operationId;
                        MRKCommandPipeWire.ParseOperationFrame(bytes, kind,
                            out requestId, out operationId);
                        // Host基于OS认证Caller裁决；Payload无权指定要控制的调用方。
                        response = _center.ControlForCaller(operationId, peer, kind, requestId);
                    }
                    else throw new InvalidDataException("未知远程命令请求类型");
                    await MRKCommandPipeWire.WriteAsync(pipe,
                        MRKCommandPipeWire.ResponseFrame(response), _stop.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // 故障不可被报告为命令执行失败；客户端收到EOF/断连将标为RemoteUnknown。
                }
            }
        }
        public async Task StopAsync()
        {
            Task loop;
            lock (_gate)
            {
                _stop.Cancel();
                if (_listener != null) _listener.Dispose();
                loop = _accept;
            }
            if (loop != null)
                try { await loop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
            Task[] sessions;
            lock (_gate) sessions = _sessions.ToArray();
            await Task.WhenAll(sessions).ConfigureAwait(false);
        }
        public void Dispose()
        {
            // 不阻塞WPF UI或强制终止正在执行的设备SDK；显式StopAsync用于关闭验收。
            Task stop = StopAsync();
        }
    }
}
