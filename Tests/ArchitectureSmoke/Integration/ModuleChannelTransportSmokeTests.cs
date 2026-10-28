using CxVisual.Foundation.Transport;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Tests.ArchitectureSmoke
{
    // OS loopback 真网络 I/O：与 Virtual 区分，不能冒称外部 PLC/HIL 现场验收。
    internal static class ModuleChannelTransportSmokeTests
    {
        private static void Check(bool condition, string why)
        {
            if (!condition) throw new InvalidOperationException("CHANNEL.TRANSPORT " + why);
        }
        private sealed class StartupReceiveChannel : FTRSingleChannelBase
        {
            internal bool ClosedResources;
            internal StartupReceiveChannel(string id) : base(id) { }
            internal void InjectReceived(byte[] data) { Receive(data); }
            protected override Task OnOpenAsync(CancellationToken ct)
            {
                // 用确定性时序覆盖 Opening 状态中的第一次收包。
                Receive(new byte[] { 88 });
                return Task.CompletedTask;
            }
            protected override Task OnCloseAsync(CancellationToken ct)
            {
                ClosedResources = true;
                return Task.CompletedTask;
            }
            protected override Task OnSendAsync(byte[] payload, FTRSubChannelAddress address, CancellationToken ct)
            { return Task.CompletedTask; }
        }
        private sealed class FailingOpenChannel : FTRSingleChannelBase
        {
            internal bool CleanupCalled;
            internal FailingOpenChannel() : base("failure") { }
            protected override Task OnOpenAsync(CancellationToken ct)
            { throw new IOException("打开设备失败"); }
            protected override Task OnCloseAsync(CancellationToken ct)
            { CleanupCalled = true; return Task.CompletedTask; }
            protected override Task OnSendAsync(byte[] data, FTRSubChannelAddress target, CancellationToken ct)
            { return Task.CompletedTask; }
        }
        private static void LifecycleStateMachine()
        {
            StartupReceiveChannel early = new StartupReceiveChannel("early");
            byte[] first = null;
            early.Received += data => first = data.Data;
            early.Open();
            Check(early.State == FTRChannelState.Open && first != null &&
                first.SequenceEqual(new byte[] { 88 }),
                "OpenAsync 中的第一包应接纳，不能因后台接收快于状态切换而失败");
            early.Close();
            Check(early.ClosedResources && early.State == FTRChannelState.Closed,
                "关闭必须释放资源");
            early.Close();
            FailingOpenChannel fail = new FailingOpenChannel();
            bool rejected = false;
            try { fail.Open(); } catch (IOException) { rejected = true; }
            Check(rejected && fail.CleanupCalled && fail.State == FTRChannelState.Faulted,
                "Open 失败要清理已创建资源并报告 Faulted");
            fail.Close();
            Check(fail.State == FTRChannelState.Closed,
                "Faulted 关闭后可回到 Closed");
        }
        // 模拟物理重启：旧LengthFrame半包不能污染同一Connector的新会话。
        private static void ChannelSessionIsolation()
        {
            FTRVirtualChannel[] pair=FTRVirtualChannel.CreatePair("session-left","session-right");
            using(var left=pair[0]) using(var right=pair[1])
            {
                var config=new MCNConnectorConfiguration {
                    DisplayName="Protocol跨运行会话隔离",
                    Channel=new FTRVirtualChannelConfiguration {Id="session-left"},
                    Protocol=new MPAProtocolConfiguration("session-protocol","length.frame")
                };
                var connector=new MCNConnectorModule(
                    config.Describe("session-module",MFMModuleKind.Connector),
                    config,left,new MPALengthFrameProtocol());
                byte[] received=null;int count=0;
                connector.PayloadReceived+=payload=>{received=payload;Interlocked.Increment(ref count);};
                right.Open();
                connector.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                var encoded=new MPALengthFrameCodec().Encode(new byte[]{9,10});
                right.Send(encoded.Take(3).ToArray());
                Check(count==0,"原会话未完整长度帧不得提前解析");
                Guid before=left.SessionId;
                left.Close();
                left.Open();
                Check(left.SessionId!=before,"重新打开同一Channel必须生成新的物理SessionId");
                right.Send(new MPALengthFrameCodec().Encode(new byte[]{77,88}));
                Check(count==1 && received.SequenceEqual(new byte[]{77,88}),
                    "旧半包不得混入新会话的完整长度帧");
                connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                right.Close();
            }
            Console.WriteLine("F17.SESSION_ISOLATION_PASS: new SessionId and Protocol parser reset after reconnect");
        }

        private static int FreePort(bool udp)
        {
            if (udp)
            {
                using (UdpClient candidate = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                    return ((IPEndPoint)candidate.Client.LocalEndPoint).Port;
            }
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
            finally { listener.Stop(); }
        }

        public static void Run()
        {
            Check(typeof(FTRChannelBase).Assembly.GetName().Name == "CxVisual.Foundation.Transport" &&
                typeof(FTRChannelConfiguration).Assembly == typeof(FTRChannelBase).Assembly &&
                typeof(FTRTcpClientChannel).Assembly == typeof(FTRChannelBase).Assembly,
                "所有正式Channel契约、配置与实现必须由Foundation.Transport唯一拥有");
            ConnectorAllChannelKinds();
            LifecycleStateMachine();
            ChannelSessionIsolation();
            BoundedEvidence();
            TcpLoopback();
            ByteStreamAdapterLoopback();
            ConnectorModbusTcpLoopback();
            UdpLoopback();
            DatagramAdapterLoopback();
            HttpConfigurationSmoke();
            NamedPipeLoopback();
            WebSocketConfigSmoke();
            WebSocketLoopback();
            WebSocketServerLoopback();
            SharedMemoryLoopback();
            SharedMemoryCrossProcess();
            ReplayFileLoopback();
            MulticastConfigurationSmoke();
            HttpLoopback();
            HttpServerLoopback();
            FTRTcpBulkTests.Run();
            FTRChannelFactoryTests.Run();
            FTRUdpThreadTests.Run();
            FTRNamedPipeAdmissionTests.Run();
            FTRNamedPipeBulkTests.Run();
            FTRReceiveNotificationTests.Run();
            FTRReconnectControllerTests.Run();
            Console.WriteLine("CHANNEL.TRANSPORT: TCP/UDP loopback MultiPeer and stale generation PASS");
        }

        // UML核心三个新增传输类的独立可执行门禁，避免旧NamedPipe等测试阻断归因。
        public static void RunUmlCore()
        {
            HttpServerLoopback();
            WebSocketServerLoopback();
            SharedMemoryLoopback();
            SharedMemoryCrossProcess();
            Console.WriteLine("F17.UML_CORE_PASS HTTP/WebSocket/SharedMemory real OS, process and lifecycle");
        }

        // 覆盖所有已实现的 ChannelKind：装配能力不是打开真实设备的证明。
        // 始终使用真实 FTR 类型与单个 Empty Protocol，不新建替代通道工厂。
        private static void ConnectorAllChannelKinds()
        {
            FTRVirtualChannel[] virtualPair = FTRVirtualChannel.CreatePair("matrix-virtual", "matrix-peer");
            FTRChannelConfiguration[] configurations = {
                new FTRVirtualChannelConfiguration { Id = "matrix-virtual" },
                new FTRTcpClientChannelConfiguration { Id = "matrix-tcp-client" },
                new FTRTcpServerChannelConfiguration { Id = "matrix-tcp-server" },
                new FTRUdpChannelConfiguration { Id = "matrix-udp" },
                new FTRUdpServerChannelConfiguration { Id = "matrix-udp-server" },
                new FTRSerialChannelConfiguration { Id = "matrix-serial" },
                new FTRHttpClientChannelConfiguration { Id = "matrix-http" },
                new FTRNamedPipeClientChannelConfiguration { Id = "matrix-pipe-client" },
                new FTRNamedPipeServerChannelConfiguration { Id = "matrix-pipe-server" },
                new FTRWebSocketClientChannelConfiguration { Id = "matrix-websocket" },
                new FTRReplayChannelConfiguration { Id = "matrix-replay" },
                new FTRUdpMulticastChannelConfiguration { Id = "matrix-multicast" },
                new FTRHttpServerChannelConfiguration { Id = "matrix-http-server" },
                new FTRWebSocketServerChannelConfiguration { Id = "matrix-ws-server" },
                new FTRSharedMemoryChannelConfiguration { Id = "matrix-shm" }
            };
            IFTRChannel[] channels = {
                virtualPair[0],
                new FTRTcpClientChannel(configurations[1]),
                new FTRTcpServerChannel(configurations[2]),
                new FTRUdpChannel(configurations[3]),
                new FTRUdpServerChannel(configurations[4]),
                new FTRSerialChannel(configurations[5]),
                new FTRHttpClientChannel(configurations[6]),
                new FTRNamedPipeClientChannel(configurations[7]),
                new FTRNamedPipeServerChannel(configurations[8]),
                new FTRWebSocketClientChannel(configurations[9]),
                new FTRReplayChannel(configurations[10]),
                new FTRUdpMulticastChannel(configurations[11]),
                new FTRHttpServerChannel(configurations[12]),
                new FTRWebSocketServerChannel(configurations[13]),
                new FTRSharedMemoryChannel(configurations[14])
            };
            Check(configurations.Length == Enum.GetValues(typeof(FTRChannelKind)).Length - 1,
                "所有正式FTR ChannelKind必须都有装配测试（Custom没有内置Provider）");
            for (int i = 0; i < channels.Length; ++i)
            {
                MCNConnectorConfiguration model = new MCNConnectorConfiguration {
                    DisplayName = "通道装配矩阵 " + i,
                    Channel = configurations[i],
                    Protocol = new MPAProtocolConfiguration("matrix-protocol-" + i, "empty")
                };
                MCNConnectorModule module = new MCNConnectorModule(
                    model.Describe("matrix-connector-" + i, MFMModuleKind.Connector),
                    model, channels[i], new MPAEmptyProtocol());
                Check(ReferenceEquals(module.Channel, channels[i]) && !module.IsStarted &&
                    module.Channel.State == FTRChannelState.Closed,
                    "正式Channel应只装配而不隐式连接、创建后台IO或假装已经启动：" + configurations[i].Kind);
            }
            // 防止仅凭相同Id、兼容IO能力就接受错误的CLR Channel实现。
            MCNConnectorConfiguration mismatch = new MCNConnectorConfiguration {
                DisplayName = "错误通道拒绝",
                Channel = new FTRVirtualChannelConfiguration { Id = "matrix-tcp-client" },
                Protocol = new MPAProtocolConfiguration("matrix-wrong-type", "empty")
            };
            bool wrongTypeRejected = false;
            try {
                new MCNConnectorModule(mismatch.Describe("matrix-wrong-type", MFMModuleKind.Connector),
                    mismatch, channels[1], new MPAEmptyProtocol());
            }
            catch (NotSupportedException) { wrongTypeRejected = true; }
            Check(wrongTypeRejected, "配置与实际Channel类型不匹配必须拒绝");
            // LengthFrame仅适用于ByteStream；HTTP/WebSocket/Replay Message不得绕过协议能力门禁。
            foreach (int i in new[] { 6, 9, 10, 12, 13, 14 })
            {
                MCNConnectorConfiguration invalid = new MCNConnectorConfiguration {
                    DisplayName = "错误协议能力拒绝",
                    Channel = configurations[i],
                    Protocol = new MPAProtocolConfiguration("matrix-length-" + i, "length.frame")
                };
                bool rejected = false;
                try {
                    new MCNConnectorModule(invalid.Describe("matrix-invalid-" + i, MFMModuleKind.Connector),
                        invalid, channels[i], new MPALengthFrameProtocol());
                }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "消息类Channel不可假装支持ByteStream长度规约：" + configurations[i].Kind);
            }
            // Custom只有配置身份，没有已经注册的真实Provider，仍必须失败关闭。
            FTRCustomChannelConfiguration custom = new FTRCustomChannelConfiguration {
                Id = "matrix-virtual", CustomTypeId = "vendor.unregistered"
            };
            MCNConnectorConfiguration unknown = new MCNConnectorConfiguration {
                DisplayName = "未注册厂商通道", Channel = custom,
                Protocol = new MPAProtocolConfiguration("matrix-custom-protocol", "empty")
            };
            bool noProvider = false;
            try {
                new MCNConnectorModule(unknown.Describe("matrix-custom", MFMModuleKind.Connector),
                    unknown, virtualPair[0], new MPAEmptyProtocol());
            }
            catch (NotSupportedException) { noProvider = true; }
            Check(noProvider, "Custom未注册Provider不能装配成功");
            Console.WriteLine("CHANNEL.CONNECTOR: 15 built-in FTR kinds + mismatch/protocol/Custom rejection PASS");
        }

        // 大数据仍应完整传给上层；证据只能有界留存，并明确记录省略原因。
        private static void NamedPipeLoopback()
        {
            string pipeName = "CxVisualSmoke-" + Guid.NewGuid().ToString("N");
            FTRNamedPipeServerChannel server = new FTRNamedPipeServerChannel(
                new FTRNamedPipeServerChannelConfiguration { Id = "pipeserver", PipeName = pipeName });
            FTRNamedPipeClientChannel client = new FTRNamedPipeClientChannel(
                new FTRNamedPipeClientChannelConfiguration { Id = "pipeclient", PipeName = pipeName });
            byte[] received = null, reply = null;
            FTRSubChannelAddress source = null;
            using (ManualResetEventSlim first = new ManualResetEventSlim(false))
            using (ManualResetEventSlim second = new ManualResetEventSlim(false))
            {
                server.Received += packet => {
                    received = packet.Data; source = packet.Address; first.Set();
                };
                client.Received += packet => { reply = packet.Data; second.Set(); };
                try
                {
                    server.Open();
                    client.Open();
                    client.SendAsync(new byte[] { 9, 8, 7 },
                        new FTRSubChannelAddress(client.Id), CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(first.Wait(TimeSpan.FromSeconds(5)) &&
                        received.SequenceEqual(new byte[] { 9, 8, 7 }) &&
                        source.HasSubChannel, "NamedPipe client -> server");
                    server.SendAsync(new byte[] { 1, 2 }, source, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(second.Wait(TimeSpan.FromSeconds(5)) &&
                        reply.SequenceEqual(new byte[] { 1, 2 }),
                        "NamedPipe server -> client");
                }
                finally
                {
                    client.Close();
                    server.Close();
                }
            }
        }

        // 不使用 HttpListener ACL/URL Reservation，直接走真实 OS TCP Socket 的 HTTP/1.1 回环。
        private static void HttpLoopback()
        {
            int port = FreePort(false);
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Task serverTask = Task.Run(async () => {
                using (TcpClient socket = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
                using (NetworkStream stream = socket.GetStream())
                using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.ASCII,
                    false, 1024, true))
                {
                    string requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (requestLine == null || !requestLine.StartsWith("POST /", StringComparison.Ordinal))
                        throw new IOException("HTTP 未接收到 POST 请求");
                    string line;
                    do
                    {
                        line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) throw new IOException("HTTP 请求头未完整接收");
                    } while (line.Length != 0);
                    byte[] response = System.Text.Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\nContent-Length: 3\r\nConnection: close\r\n\r\nACK");
                    await stream.WriteAsync(response, 0, response.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
            });
            FTRHttpClientChannel client = new FTRHttpClientChannel(
                new FTRHttpClientChannelConfiguration { Id = "http-loopback",
                    Url = "http://127.0.0.1:" + port + "/" });
            byte[] reply = null;
            client.Received += received => reply = received.Data;
            try
            {
                client.Open();
                client.SendAsync(new byte[] { 1, 2, 3 },
                    new FTRSubChannelAddress(client.Id), CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(reply != null && reply.SequenceEqual(System.Text.Encoding.ASCII.GetBytes("ACK")),
                    "HTTP Client 真正收到 OS Socket 上的字节响应");
                Check(serverTask.Wait(TimeSpan.FromSeconds(5)), "HTTP server 处理请求");
            }
            finally { client.Close(); listener.Stop(); }
        }


        // 本机OS HTTP请求/响应闭环；只验证Transport字节收发和多对端地址，不引入REST业务协议。
        private static void HttpServerLoopback()
        {
            int port=FreePort(false);
            var config=new FTRHttpServerChannelConfiguration {
                Id="http-server-loopback",Prefix="http://localhost:"+port+"/",
                MaxClients=2,MaxRequestBytes=1024,MaxResponseBytes=1024,ResponseTimeoutMs=3000
            };
            var channel=new FTRHttpServerChannel(config);
            int methodCount=0;FTRSubChannelAddress first=null;int closeRejected=0;
            channel.RequestReceived += info => {
                Check(info.Method=="POST" && info.Path=="/" && info.ContentBytes==3,
                    "HTTP请求方法、路径及正文应正确传递");
                Interlocked.Increment(ref methodCount);
            };
            channel.Received += received => {
                try{channel.Close();}catch(InvalidOperationException){Interlocked.Exchange(ref closeRejected,1);}
                first=received.Address;
                channel.Send(new byte[]{9,8,7},0,3,received.Address,CancellationToken.None);
            };
            using(channel)
            using(var client=new System.Net.Http.HttpClient())
            {
                channel.Open();
                using(var body=new System.Net.Http.ByteArrayContent(new byte[]{1,2,3}))
                using(var response=client.PostAsync(config.Prefix,body).GetAwaiter().GetResult())
                {
                    byte[] reply=response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    Check(response.IsSuccessStatusCode && reply.SequenceEqual(new byte[]{9,8,7}),
                        "HTTP Server真实HttpListener应返回原始响应字节");
                }
                Check(methodCount==1 && first!=null && first.HasSubChannel && closeRejected==1,
                    "HTTP请求必须有独立SubChannel且回调内禁止自等待关闭");
                bool oldRejected=false;
                try{channel.Send(new byte[]{1},0,1,first,CancellationToken.None);}
                catch(InvalidOperationException){oldRejected=true;}
                Check(oldRejected,"HTTP已完成请求地址必须拒绝再次发送");
                channel.Close();
                Check(channel.SnapshotSubChannels().Length==0,"HTTP Server应回收请求SubChannel");
            }
            Console.WriteLine("F17.HTTP_SERVER PASS: real OS HTTP request/response, stale address and stop");
        }


        private static void WebSocketServerLoopback()
        {
            int port=FreePort(false);
            var config=new FTRWebSocketServerChannelConfiguration {
                Id="ws-server-loopback",Prefix="http://localhost:"+port+"/ws/",
                MaxClients=2,MaxMessageBytes=1024
            };
            using(var server=new FTRWebSocketServerChannel(config))
            using(var client=new System.Net.WebSockets.ClientWebSocket())
            {
                FTRSubChannelAddress address=null;int closeRejected=0;
                server.Received += data => {
                    try{server.Close();}catch(InvalidOperationException){Interlocked.Exchange(ref closeRejected,1);}
                    address=data.Address;
                    server.Send(new byte[]{7,8,9},0,3,data.Address,CancellationToken.None);
                };
                server.Open();
                using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                {
                client.ConnectAsync(new Uri("ws://localhost:"+port+"/ws/"),deadline.Token).GetAwaiter().GetResult();
                byte[] bytes={1,2,3};byte[] buffer=new byte[32];
                client.SendAsync(new ArraySegment<byte>(bytes),System.Net.WebSockets.WebSocketMessageType.Binary,true,
                    deadline.Token).GetAwaiter().GetResult();
                var reply=client.ReceiveAsync(new ArraySegment<byte>(buffer),deadline.Token).GetAwaiter().GetResult();
                Check(reply.MessageType==System.Net.WebSockets.WebSocketMessageType.Binary &&
                    reply.EndOfMessage && reply.Count==3 && buffer[0]==7 && buffer[1]==8 && buffer[2]==9,
                    "WebSocket Server必须真正完成RFC6455握手与双向二进制收发");
                Check(address!=null && address.HasSubChannel && closeRejected==1,
                    "WebSocket Server请求地址必须有Generation并禁止回调自身Close");
                server.Close();
                }
                Check(server.SnapshotSubChannels().Length==0,"WebSocket Server停止后必须移除所有真实对端");
            }
            Console.WriteLine("F17.WEBSOCKET_SERVER PASS: RFC6455 OS loopback, multi-peer address and close");
        }


        private static void SharedMemoryLoopback()
        {
            string shared="f17_"+Guid.NewGuid().ToString("N");
            var configA=new FTRSharedMemoryChannelConfiguration {
                Id="shared-a",MapName=shared,Side=FTRSharedMemorySide.A,
                Slots=4,SlotBytes=4096,ReceiveBufferBytes=4096,ReceiveByteBudget=16384,QueueCapacity=4
            };
            var configB=new FTRSharedMemoryChannelConfiguration {
                Id="shared-b",MapName=shared,Side=FTRSharedMemorySide.B,
                Slots=4,SlotBytes=4096,ReceiveBufferBytes=4096,ReceiveByteBudget=16384,QueueCapacity=4
            };
            using(var a=new FTRSharedMemoryChannel(configA))
            using(var b=new FTRSharedMemoryChannel(configB))
            using(var arrivedA=new ManualResetEventSlim())
            using(var arrivedB=new ManualResetEventSlim())
            {
                byte[] message=new byte[3*4096+17];for(int i=0;i<message.Length;i++)message[i]=(byte)(i%251);
                var received=new List<byte>();object gate=new object();
                b.Received += block=>{lock(gate){received.AddRange(block.Data);if(received.Count>=message.Length)arrivedB.Set();}};
                a.Received += block=>{if(block.Data.SequenceEqual(new byte[]{23,42}))arrivedA.Set();};
                a.Open();b.Open();
                using(var duplicate=new FTRSharedMemoryChannel(new FTRSharedMemoryChannelConfiguration {
                    Id="shared-duplicate",MapName=shared,Side=FTRSharedMemorySide.A,
                    Slots=4,SlotBytes=4096,ReceiveBufferBytes=4096,ReceiveByteBudget=16384,QueueCapacity=4
                }))
                {
                    bool rejected=false;
                    try{duplicate.Open();}catch(InvalidOperationException){rejected=true;}
                    Check(rejected && duplicate.State==FTRChannelState.Faulted,
                        "SharedMemory同一Map与Side必须拒绝第二生产者/消费者，而不是并发争用同一环");
                    duplicate.Close();
                }
                a.SendBuffer(message,0,message.Length,CancellationToken.None);
                Check(arrivedB.Wait(5000),"SharedMemory必须按有界槽位传输超过单槽的完整字节序列");
                lock(gate)Check(received.SequenceEqual(message),"共享环必须保持顺序、数据完整性及无额外字节");
                b.Send(new byte[]{23,42});Check(arrivedA.Wait(5000),"SharedMemory必须支持独立反向传输");
                Check(a.Diagnostics.ReceiveQueue!=null && b.Diagnostics.ReceiveQueue!=null,
                    "共享内存接收必须真正接入FTR池预算与诊断");
                a.Close();b.Close();
            }
            Console.WriteLine("F17.SHARED_MEMORY PASS: real named MMF two-ended bounded ring, integrity and close");
        }


        public static int RunSharedMemoryChild(string map)
        {
            var conf=new FTRSharedMemoryChannelConfiguration {
                Id="shm-cross-child",MapName=map,Side=FTRSharedMemorySide.B,
                Slots=4,SlotBytes=4096,ReceiveBufferBytes=4096,ReceiveByteBudget=16384,QueueCapacity=4
            };
            using(var channel=new FTRSharedMemoryChannel(conf))
            using(var reply=new ManualResetEventSlim())
            {
                channel.Received += data => {
                    if(data.Data.SequenceEqual(new byte[]{55,66,77}))reply.Set();
                };
                channel.Open();
                byte[] sent=new byte[8193];
                for(int i=0;i<sent.Length;i++)sent[i]=(byte)(i%239);
                channel.Send(sent);
                Check(reply.Wait(8000),"跨进程共享内存子进程必须真实接收父进程响应");
                channel.Close();
            }
            Console.WriteLine("F17.SHM_CHILD_PASS");
            return 0;
        }
        private static void SharedMemoryCrossProcess()
        {
            string name="f17cp_"+Guid.NewGuid().ToString("N");
            var conf=new FTRSharedMemoryChannelConfiguration {
                Id="shm-cross-parent",MapName=name,Side=FTRSharedMemorySide.A,
                Slots=4,SlotBytes=4096,ReceiveBufferBytes=4096,ReceiveByteBudget=16384,QueueCapacity=4
            };
            using(var channel=new FTRSharedMemoryChannel(conf))
            using(var dataReady=new ManualResetEventSlim())
            {
                var observed=new List<byte>();object gate=new object();
                channel.Received += block => {
                    bool complete=false;
                    lock(gate)
                    {
                        observed.AddRange(block.Data);
                        complete=observed.Count==8193;
                    }
                    if(complete)
                    {
                        dataReady.Set();
                        channel.Send(new byte[]{55,66,77});
                    }
                };
                channel.Open();
                var info=new System.Diagnostics.ProcessStartInfo(
                    System.Reflection.Assembly.GetExecutingAssembly().Location,
                    "--channel-shm-child "+name) {
                    UseShellExecute=false,CreateNoWindow=true,
                    RedirectStandardOutput=true,RedirectStandardError=true
                };
                using(var child=System.Diagnostics.Process.Start(info))
                {
                    Check(child!=null,"共享内存跨进程测试必须创建独立子进程");
                    Task<string> output=child.StandardOutput.ReadToEndAsync();
                    Task<string> error=child.StandardError.ReadToEndAsync();
                    bool exited=child.WaitForExit(10000);
                    if(!exited){child.Kill();child.WaitForExit(3000);}
                    Check(exited && child.ExitCode==0,
                        "共享内存跨进程子进程失败: "+(exited?error.GetAwaiter().GetResult():"timeout"));
                    Check(output.GetAwaiter().GetResult().Contains("F17.SHM_CHILD_PASS"),
                        "共享内存子进程成功结果必须真实输出");
                }
                Check(dataReady.IsSet,"真实子进程输入字节没有到达父进程");
                byte[] expected=new byte[8193];
                for(int i=0;i<expected.Length;i++)expected[i]=(byte)(i%239);
                lock(gate)Check(observed.SequenceEqual(expected),"跨进程共享环顺序、长度或内容不一致");
                channel.Close();
            }
            Console.WriteLine("F17.SHARED_MEMORY_CROSS_PROCESS PASS: real child process, 8193 bytes and response");
        }

        private static byte[] ReadExactly(NetworkStream stream, int count)
        {
            byte[] bytes = new byte[count];
            for (int offset = 0; offset < count;)
            {
                int n = stream.Read(bytes, offset, count - offset);
                if (n < 1) throw new IOException("WebSocket 测试对端提前关闭");
                offset += n;
            }
            return bytes;
        }

        // 本机真实 TCP 连接上的 RFC6455 Upgrade 与掩码二进制消息，避免HttpListener ACL依赖。
        private static void WebSocketLoopback()
        {
            int port = FreePort(false);
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Task echo = Task.Run(() => {
                using (TcpClient tcp = listener.AcceptTcpClient())
                using (NetworkStream stream = tcp.GetStream())
                using (StreamReader reader = new StreamReader(
                    stream, System.Text.Encoding.ASCII, false, 1024, true))
                {
                    tcp.ReceiveTimeout = 5000;
                    tcp.SendTimeout = 5000;
                    string first = reader.ReadLine();
                    if (first == null || !first.StartsWith("GET /ws", StringComparison.Ordinal))
                        throw new IOException("WebSocket 客户端 Upgrade 请求错误");
                    string key = null;
                    string line;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                        if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                            key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                    if (string.IsNullOrEmpty(key)) throw new IOException("WebSocket 握手密钥不存在");
                    string source = key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    string accept;
                    using (System.Security.Cryptography.SHA1 sha1 = System.Security.Cryptography.SHA1.Create())
                        accept = Convert.ToBase64String(sha1.ComputeHash(
                            System.Text.Encoding.ASCII.GetBytes(source)));
                    byte[] handshake = System.Text.Encoding.ASCII.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "
                        + accept + "\r\n\r\n");
                    stream.Write(handshake, 0, handshake.Length);
                    stream.Flush();
                    byte[] header = ReadExactly(stream, 2);
                    if (header[0] != 0x82 || (header[1] & 0x80) == 0 ||
                        (header[1] & 0x7f) != 3)
                        throw new IOException("WebSocket 客户端未发送带掩码的二进制单消息");
                    byte[] mask = ReadExactly(stream, 4);
                    byte[] data = ReadExactly(stream, 3);
                    for (int i = 0; i < data.Length; i++) data[i] ^= mask[i % 4];
                    if (!data.SequenceEqual(new byte[] { 7, 8, 9 }))
                        throw new IOException("WebSocket 二进制请求正文错误");
                    byte[] reply = new byte[] { 0x82, 0x03, 0x21, 0x22, 0x23 };
                    stream.Write(reply, 0, reply.Length);
                    stream.Flush();
                    // 确保客户端实际有机会接收响应后再断开本机测试Socket。
                    Thread.Sleep(150);
                }
            });
            FTRWebSocketClientChannel client = new FTRWebSocketClientChannel(
                new FTRWebSocketClientChannelConfiguration {
                    Id = "ws-real-loopback",
                    Url = "ws://127.0.0.1:" + port + "/ws" });
            byte[] observed = null;
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                client.Received += incoming => { observed = incoming.Data; received.Set(); };
                try
                {
                    client.Open();
                    client.SendAsync(new byte[] { 7, 8, 9 },
                        new FTRSubChannelAddress(client.Id), CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(received.Wait(TimeSpan.FromSeconds(5)) &&
                        observed.SequenceEqual(new byte[] { 0x21, 0x22, 0x23 }),
                        "WebSocket 二进制消息真正完成握手、掩码与接收");
                    Check(echo.Wait(TimeSpan.FromSeconds(5)), "WebSocket 服务端验证");
                }
                finally { client.Close(); listener.Stop(); }
            }
        }

        private static void MulticastConfigurationSmoke()
        {
            FTRUdpMulticastChannelConfiguration config = new FTRUdpMulticastChannelConfiguration {
                Id = "udp-multicast-test", GroupAddress = "239.3.7.11", Port = 30001,
                Loopback = false, TimeToLive = 2
            };
            Check(config.DescribeType().Topology == FTRChannelTopology.SharedBus &&
                config.DescribeType().IO == FTRIOCapabilities.Datagram,
                "组播是共享总线数据报，非TCP多Peer");
            Check(new FTRUdpMulticastChannel(config).State == FTRChannelState.Closed,
                "组播通道构建时不能提前绑定网络Socket");
            bool rejected = false;
            try { new FTRUdpMulticastChannelConfiguration {
                    GroupAddress = "192.168.1.1" }.Validate(); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "非组播地址必须拒绝");
        }

        private static void ReplayFileLoopback()
        {
            string file = Path.Combine(Path.GetTempPath(),
                "cx-replay-" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using (FileStream stream = File.Create(file))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("MCCRPL01"));
                    writer.Write(3);
                    writer.Write(new byte[] { 3, 5, 7 });
                    writer.Write(2);
                    writer.Write(new byte[] { 9, 11 });
                }
                FTRReplayChannel channel = new FTRReplayChannel(
                    new FTRReplayChannelConfiguration { Id = "replay-test", FilePath = file });
                List<byte[]> messages = new List<byte[]>();
                using (ManualResetEventSlim delivered = new ManualResetEventSlim(false))
                {
                    channel.Received += item => {
                        lock (messages)
                        {
                            messages.Add(item.Data);
                            if (messages.Count == 2) delivered.Set();
                        }
                    };
                    try
                    {
                        channel.Open();
                        Check(delivered.Wait(TimeSpan.FromSeconds(5)), "Replay 文件逐记录实际接收");
                        lock (messages)
                            Check(messages[0].SequenceEqual(new byte[] { 3, 5, 7 }) &&
                                messages[1].SequenceEqual(new byte[] { 9, 11 }),
                                "Replay 保留两个报文的原始内容和边界");
                        bool sendRejected = false;
                        try
                        {
                            channel.SendAsync(new byte[] { 1 }, new FTRSubChannelAddress(channel.Id),
                                CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch (NotSupportedException) { sendRejected = true; }
                        Check(sendRejected, "Replay 明确拒绝伪造设备发送成功");
                    }
                    finally { channel.Close(); }
                }
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        private static void WebSocketConfigSmoke()
        {
            FTRWebSocketClientChannelConfiguration config =
                new FTRWebSocketClientChannelConfiguration { Id = "ws-client",
                    Url = "ws://127.0.0.1:8080/ws" };
            Check(config.DescribeType().IO == FTRIOCapabilities.Message,
                "WebSocket 保持完整 Message 边界");
            Check(new FTRWebSocketClientChannel(config).State == FTRChannelState.Closed,
                "WebSocket 构造不得发生联网副作用");
            bool rejected = false;
            try { new FTRWebSocketClientChannelConfiguration { Url = "http://localhost/ws" }.Validate(); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "WebSocket 不接受 HTTP URL");
        }

        private static void HttpConfigurationSmoke()
        {
            FTRHttpClientChannelConfiguration config = new FTRHttpClientChannelConfiguration {
                Id = "http-client-test", Url = "http://127.0.0.1:8080/"
            };
            Check(config.DescribeType().IO == (FTRIOCapabilities.Message | FTRIOCapabilities.RequestResponse),
                "HTTP Client capabilities");
            FTRHttpClientChannel client = new FTRHttpClientChannel(config);
            Check(client.State == FTRChannelState.Closed, "HTTP Client constructed without network side effects");
            bool rejected = false;
            try { new FTRHttpClientChannelConfiguration { Url = "ftp://localhost/a" }.Validate(); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Reject non-http channel URL");
        }

        private static void BoundedEvidence()
        {
            StartupReceiveChannel channel = new StartupReceiveChannel("evidence-budget");
            int lastReceivedLength = 0;
            channel.Received += data => lastReceivedLength = data.Data.Length;
            try
            {
                channel.Open();
                byte[] oversized = new byte[5 * 1024 * 1024];
                oversized[0] = 0x5a;
                channel.InjectReceived(oversized);
                FTRChannelFrame metadata = channel.ReadFrames().Last();
                Check(lastReceivedLength == oversized.Length,
                    "超大接收数据不得被诊断证据预算截断");
                Check(metadata.Direction == FTRFrameDirection.Rx && metadata.Bytes.Length == 0 &&
                    metadata.Error.Contains("原始字节数=" + oversized.Length),
                    "超大报文诊断副本必须省略并保留长度与原因，不可静默丢字节");

                byte[] chunk = new byte[128 * 1024];
                for (int i = 0; i < 40; i++) channel.InjectReceived(chunk);
                System.Collections.Generic.IList<FTRChannelFrame> frames = channel.ReadFrames();
                long total = frames.Sum(frame => (long)frame.Bytes.Length);
                Check(total <= 4L * 1024 * 1024 && frames.Count < 42 &&
                    frames.Last().Bytes.Length == chunk.Length,
                    "诊断证据累计字节数必须有界且保留最新收包");
            }
            finally { channel.Close(); }
        }

        private static void TcpLoopback()
        {
            int port = FreePort(false);
            FTRTcpServerChannel server = new FTRTcpServerChannel(
                new FTRTcpServerChannelConfiguration { Id = "tcp-server-test", Kind = FTRChannelKind.TcpServer,
                    Host = "127.0.0.1", Port = port });
            FTRTcpClientChannel client = new FTRTcpClientChannel(
                new FTRTcpClientChannelConfiguration { Id = "tcp-client-test", Kind = FTRChannelKind.TcpClient,
                    Host = "127.0.0.1", Port = port });
            byte[] inbound = null, outbound = null;
            FTRSubChannelAddress sub = null;
            using (ManualResetEventSlim arrived = new ManualResetEventSlim(false))
            using (ManualResetEventSlim replied = new ManualResetEventSlim(false))
            {
                server.Received += message => {
                    inbound = message.Data; sub = message.Address; arrived.Set();
                };
                client.Received += message => { outbound = message.Data; replied.Set(); };
                try
                {
                    server.Open(); client.Open();
                    client.Send(new byte[] { 1, 2, 3 });
                    Check(arrived.Wait(3000) && inbound.SequenceEqual(new byte[] { 1, 2, 3 }) &&
                        sub != null && sub.HasSubChannel && sub.Generation > 0 &&
                        server.SnapshotSubChannels().Length == 1,
                        "TCP Server must receive from one SubChannel without creating another parent Channel");
                    server.SendAsync(new byte[] { 4, 5 }, sub, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(replied.Wait(3000) && outbound.SequenceEqual(new byte[] { 4, 5 }),
                        "TCP Server reply must use selected SubChannel");
                    FTRSubChannelAddress originalPeer = sub;
                    FTRTcpClientChannel second = new FTRTcpClientChannel(
                        new FTRTcpClientChannelConfiguration { Id = "tcp-client-test2",
                            Kind = FTRChannelKind.TcpClient, Host = "127.0.0.1", Port = port });
                    using (ManualResetEventSlim secondArrived = new ManualResetEventSlim(false))
                    using (ManualResetEventSlim secondReplied = new ManualResetEventSlim(false))
                    {
                        FTRSubChannelAddress secondAddress = null;
                        byte[] secondReply = null;
                        server.Received += msg => {
                            if (msg.Data.Length == 1 && msg.Data[0] == 6)
                            {
                                secondAddress = msg.Address;
                                secondArrived.Set();
                            }
                        };
                        second.Received += msg => { secondReply = msg.Data; secondReplied.Set(); };
                        try
                        {
                            second.Open(); second.Send(new byte[] { 6 });
                            Check(secondArrived.Wait(3000) && secondAddress != null &&
                                !secondAddress.Equals(originalPeer) &&
                                server.SnapshotSubChannels().Length == 2,
                                "TCP Server 一个父通道应同时接受两个独立 SubChannel");
                            server.SendAsync(new byte[] { 7 }, secondAddress, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            Check(secondReplied.Wait(3000) &&
                                secondReply.SequenceEqual(new byte[] { 7 }),
                                "TCP Server 第二对端定向响应");
                        }
                        finally { second.Close(); }
                    }
                    Check(server.RemovePeer(originalPeer), "TCP peer remove");
                    bool rejected = false;
                    try { server.SendAsync(new byte[] { 9 }, originalPeer, CancellationToken.None).GetAwaiter().GetResult(); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "TCP stale SubChannel must be rejected");
                }
                finally { client.Close(); server.Close(); }
            }
        }


        private static void ByteStreamAdapterLoopback()
        {
            int port=FreePort(false);
            var config=new FTRTcpClientChannelConfiguration {Id="adapter-client",Host="127.0.0.1",Port=port};
            using(var server=new FTRTcpServerChannel(new FTRTcpServerChannelConfiguration {
                Id="adapter-server",Host="127.0.0.1",Port=port}))
            using(var client=new FTRTcpClientChannel(config))
            using(var arrived=new ManualResetEventSlim())
            {
                FTRSubChannelAddress destination=null;
                byte[] sent=null;
                server.Received += packet => {
                    sent=packet.Data;
                    destination=packet.Address;
                    arrived.Set();
                };
                server.Open();client.Open();
                using(var view=new FTRByteStreamAdapter(client,config.DescribeType(),queueCapacity:1))
                {
                    client.Send(new byte[]{88});
                    Check(arrived.Wait(3000) && sent.SequenceEqual(new byte[]{88}),
                        "TCP byte-stream view不得创建第二个物理读取者或影响原接收");
                    server.Send(new byte[]{1,2,3,4},0,4,destination,CancellationToken.None);
                    var bytes=new byte[7];
                    using(var limit=new CancellationTokenSource(5000))
                    {
                        int n=view.ReadAsync(bytes,1,2,limit.Token).GetAwaiter().GetResult();
                        int m=view.ReadAsync(bytes,3,3,limit.Token).GetAwaiter().GetResult();
                        Check(n==2 && m==2 && bytes.Skip(1).Take(4).SequenceEqual(new byte[]{1,2,3,4}),
                            "ByteStream视图必须保留TCP接收块中的未消费后缀并支持切片");
                    }
                    view.WriteAsync(new byte[]{99,6,7,98},1,2,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(SpinWait.SpinUntil(()=>sent!=null && sent.Length==2,3000) &&
                        sent.SequenceEqual(new byte[]{6,7}),
                        "ByteStream写入应通过原FTR Channel唯一发送路径及字节切片");
                }
                bool invalid=false;
                try{new FTRByteStreamAdapter(client,new FTRChannelTypeDescriptor(
                    "udp",FTRChannelTopology.SinglePeer,FTRIOCapabilities.Datagram));}
                catch(ArgumentException){invalid=true;}
                Check(invalid,"不得将Datagram数据报无边界地冒充ByteStream");
                client.Close();server.Close();
            }
            Console.WriteLine("F17.BYTE_STREAM_ADAPTER PASS: bounded logical read, TCP fragments and single OS receive reader");
        }


        // 适配器只消费现有UDP接收租约，不另开ReceiveFrom；保留来源Endpoint、报文边界和代次。
        private static void DatagramAdapterLoopback()
        {
            int port=FreePort(true);
            var serverConfig=new FTRUdpServerChannelConfiguration {
                Id="datagram-server",Host="127.0.0.1",Port=port,MaxClients=2
            };
            var clientConfig=new FTRUdpChannelConfiguration {
                Id="datagram-client",Host="127.0.0.1",Port=port
            };
            using(var server=new FTRUdpServerChannel(serverConfig))
            using(var client=new FTRUdpChannel(clientConfig))
            {
                server.Open();client.Open();
                using(var incoming=new FTRDatagramAdapter(server,serverConfig.DescribeType(),queueCapacity:1))
                using(var response=new ManualResetEventSlim())
                {
                    byte[] echoed=null;
                    client.Received += packet=>{echoed=packet.Data;response.Set();};
                    client.Send(new byte[]{11,22,33});
                    FTRDatagram message;
                    using(var timeout=new CancellationTokenSource(5000))
                        message=incoming.ReceiveFromAsync(timeout.Token).GetAwaiter().GetResult();
                    Check(message!=null && message.Data.SequenceEqual(new byte[]{11,22,33}) &&
                        message.RemoteEndpoint.Contains(":"),"Datagram适配器必须保留真实UDP报文边界与来源");
                    incoming.SendToAsync(new FTRDatagram(message.RemoteEndpoint,new byte[]{44,55}),
                        CancellationToken.None).GetAwaiter().GetResult();
                    Check(response.Wait(5000) && echoed.SequenceEqual(new byte[]{44,55}),
                        "Datagram适配器回复必须按原SubChannel/Generation真实定向发送");
                    bool invalid=false;
                    try{new FTRDatagramAdapter(client,new FTRChannelTypeDescriptor("tcp",
                        FTRChannelTopology.SinglePeer,FTRIOCapabilities.ByteStream));}
                    catch(ArgumentException){invalid=true;}
                    Check(invalid,"ByteStream无报文边界，不得冒充Datagram");
                }
                client.Close();server.Close();
            }
            Console.WriteLine("F17.DATAGRAM_ADAPTER PASS: bounded original UDP pipe, true endpoint reply and boundary");
        }

        // 与纯 Virtual 请求测试互补：Connector + 正式 TCP Client / Server + Modbus Protocol。
        // 服务端为本机可控的模拟端点，不能当成 PLC 硬件 HIL。
        private static void ConnectorModbusTcpLoopback()
        {
            int port = FreePort(false);
            FTRTcpServerChannel server = new FTRTcpServerChannel(new FTRTcpServerChannelConfiguration {
                Id = "mb-loopback-server", Kind = FTRChannelKind.TcpServer,
                Host = "127.0.0.1", Port = port
            });
            MCNConnectorConfiguration config = new MCNConnectorConfiguration {
                DisplayName = "Modbus TCP Loopback",
                Channel = new FTRTcpClientChannelConfiguration {
                    Id = "mb-loopback-client", Kind = FTRChannelKind.TcpClient,
                    Host = "127.0.0.1", Port = port
                },
                Protocol = new MPAProtocolConfiguration {
                    Id = "mb-loopback-protocol", Kind = MPAProtocolKind.ModbusTcpDraft, TimeoutMs = 3000,
                    Points = new List<MPAPointDefinition> {
                        new MPAPointDefinition("temperature", "holding:100", typeof(ushort)) {
                            StationAddress = "3", CanRead = true, CanWrite = true
                        }
                    },
                    Mappings = new List<MPADataMapping> {
                        new MPADataMapping { PointRef = "temperature",
                            TargetKind = MPADataTargetKind.Variable, TargetRef = "plc/temperature",
                            Direction = MPADataDirection.Inbound, Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "temperature",
                            TargetKind = MPADataTargetKind.Variable, TargetRef = "plc/mirror",
                            Direction = MPADataDirection.Inbound, Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "temperature",
                            TargetKind = MPADataTargetKind.Variable, TargetRef = "plc/temperature",
                            Direction = MPADataDirection.Outbound, Operation = MPADataOperation.ReadValue }
                    }
                }
            };
            MCNConnectorModule connector = new MCNConnectorModule(
                config.Describe("mb-loopback", MFMModuleKind.Connector), config,
                new FTRTcpClientChannel(config.Channel), new MPAModbusTcpProtocol());
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable variables = new MDSVariableTable("plc");
            registry.Register(variables);
            variables.Define(new MDSVariableDefinition("temperature", typeof(ushort)));
            variables.Define(new MDSVariableDefinition("mirror", typeof(ushort)));
            MDSProtocolDataTargetAccess mds = new MDSProtocolDataTargetAccess(registry);
            ushort lastWritten = 0;
            List<byte> requestBuffer = new List<byte>();
            server.Received += received =>
            {
                lock (requestBuffer)
                {
                    requestBuffer.AddRange(received.Data);
                    if (requestBuffer.Count < 7) return;
                    int size = 6 + (requestBuffer[4] << 8 | requestBuffer[5]);
                    if (requestBuffer.Count < size) return;
                    // 回送同一 MBAP TransactionId/Unit；03 返回42，06 回显请求值。
                    byte[] response;
                    if (requestBuffer[7] == 6)
                    {
                        lastWritten = (ushort)((requestBuffer[10] << 8) | requestBuffer[11]);
                        response = requestBuffer.Take(size).ToArray();
                    }
                    else
                        response = new byte[] { requestBuffer[0], requestBuffer[1], 0, 0, 0, 5,
                            requestBuffer[6], 3, 2, 0, 42 };
                    requestBuffer.RemoveRange(0, size);
                    server.SendAsync(response, received.Address, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
            };
            try
            {
                server.Open();
                connector.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                MPAProtocolOperationRequest request = new MPAProtocolOperationRequest(
                    MPAProtocolOperationKind.Request, new MPAProtocolAddress("3"),
                    MPAModbusRegisterOperations.ReadHolding(100, 1));
                MPAProtocolOperationResult result = connector.RequestAsync(
                    request, new FTRSubChannelAddress(config.Channel.Id), CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(result.IsCompleted && ((byte[])result.Value).SequenceEqual(new byte[] { 3, 2, 0, 42 }),
                    "Connector RequestAsync 必须通过真实 TCP 收发并完成 03 事务确认");
                FTRSubChannelAddress destination = new FTRSubChannelAddress(config.Channel.Id);
                object pointValue = connector.ReadDevicePointAsync("temperature", mds, destination,
                    CancellationToken.None).GetAwaiter().GetResult();
                Check((ushort)pointValue == 42 && variables.Get<ushort>("temperature") == 42 &&
                    variables.Get<ushort>("mirror") == 42,
                    "PointTable holding:100 读值必须一对多写入真实 MDS Variable");
                variables.Update<ushort>("temperature", 55);
                MPAProtocolOperationResult confirmed = connector.WriteDevicePointAsync(
                    "temperature", mds, destination, CancellationToken.None).GetAwaiter().GetResult();
                Check(confirmed.IsCompleted && lastWritten == 55,
                    "MDS Outbound 变量必须经 PointTable 发送06并等设备确认");
                config.Protocol.Points[0].CanWrite = false;
                bool denied = false;
                try { connector.WriteDevicePointAsync("temperature", mds, destination,
                    CancellationToken.None).GetAwaiter().GetResult(); }
                catch (InvalidOperationException) { denied = true; }
                Check(denied, "PointTable 禁止写入的点不得发出设备命令");
            }
            finally
            {
                if (connector.IsStarted)
                    connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                server.Close();
            }
        }

        private static void UdpLoopback()
        {
            int port = FreePort(true);
            FTRUdpServerChannel server = new FTRUdpServerChannel(
                new FTRUdpServerChannelConfiguration { Id = "udp-server-test", Kind = FTRChannelKind.UdpServer,
                    Host = "127.0.0.1", Port = port });
            FTRUdpChannel client = new FTRUdpChannel(
                new FTRUdpChannelConfiguration { Id = "udp-client-test", Kind = FTRChannelKind.Udp,
                    Host = "127.0.0.1", Port = port });
            FTRSubChannelAddress sub = null;
            byte[] outbound = null;
            using (ManualResetEventSlim arrived = new ManualResetEventSlim(false))
            using (ManualResetEventSlim replied = new ManualResetEventSlim(false))
            {
                server.Received += msg => { sub = msg.Address; arrived.Set(); };
                client.Received += msg => { outbound = msg.Data; replied.Set(); };
                try
                {
                    server.Open(); client.Open();
                    client.Send(new byte[] { 33 });
                    Check(arrived.Wait(3000) && sub != null && sub.HasSubChannel &&
                        server.SnapshotSubChannels().Length == 1,
                        "UDP Server endpoint must become a logical SubChannel, not TCP Accept");
                    server.SendAsync(new byte[] { 44 }, sub, CancellationToken.None).GetAwaiter().GetResult();
                    Check(replied.Wait(3000) && outbound.SequenceEqual(new byte[] { 44 }),
                        "UDP Server reply to exact endpoint");
                    FTRSubChannelAddress originalPeer = sub;
                    FTRUdpChannel second = new FTRUdpChannel(
                        new FTRUdpChannelConfiguration { Id = "udp-client-test2",
                            Kind = FTRChannelKind.Udp, Host = "127.0.0.1", Port = port });
                    using (ManualResetEventSlim secondArrived = new ManualResetEventSlim(false))
                    using (ManualResetEventSlim secondReplied = new ManualResetEventSlim(false))
                    {
                        FTRSubChannelAddress secondAddress = null;
                        byte[] secondReply = null;
                        server.Received += msg => {
                            if (msg.Data.Length == 1 && msg.Data[0] == 66)
                            { secondAddress = msg.Address; secondArrived.Set(); }
                        };
                        second.Received += msg => { secondReply = msg.Data; secondReplied.Set(); };
                        try
                        {
                            second.Open(); second.Send(new byte[] { 66 });
                            Check(secondArrived.Wait(3000) && secondAddress != null &&
                                !secondAddress.Equals(originalPeer) &&
                                server.SnapshotSubChannels().Length == 2,
                                "UDP Server 一个父通道应区分两个来源 Endpoint");
                            server.SendAsync(new byte[] { 77 }, secondAddress, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            Check(secondReplied.Wait(3000) &&
                                secondReply.SequenceEqual(new byte[] { 77 }),
                                "UDP Server 第二逻辑对端定向响应");
                        }
                        finally { second.Close(); }
                    }
                    Check(server.RemovePeer(originalPeer), "UDP remove endpoint");
                    bool rejected = false;
                    try { server.SendAsync(new byte[] { 55 }, originalPeer, CancellationToken.None)
                        .GetAwaiter().GetResult(); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "UDP stale generation cannot route");
                }
                finally { client.Close(); server.Close(); }
            }
        }
    }
}
