using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FTRChannelFactoryTests
    {
        private sealed class Provider : IFTRChannelProvider
        {
            internal Func<FTRCustomChannelConfiguration, IFTRChannelTransport> Build;
            public FTRChannelTypeDescriptor Descriptor { get; set; }
            public IFTRChannelTransport Create(FTRCustomChannelConfiguration configuration) { return Build(configuration); }
        }
        private static void Check(bool value, string why)
        { if (!value) throw new InvalidOperationException("F17.FACTORY " + why); }
        private static void Reject<T>(Action action, string why) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("F17.FACTORY expected " + typeof(T).Name + ": " + why);
        }
        private static FTRCustomChannelConfiguration Config()
        { return new FTRCustomChannelConfiguration { Id = "factory-custom", CustomTypeId = "vendor.tcp" }; }
        private static Provider TcpProvider(int port)
        {
            return new Provider { Descriptor = new FTRChannelTypeDescriptor("vendor.tcp", FTRChannelTopology.SinglePeer,
                FTRIOCapabilities.ByteStream), Build = config => new FTRTcpClientChannel(new FTRTcpClientChannelConfiguration {
                    Id = config.Id, Host = "127.0.0.1", Port = port, CapturePayloadEvidence = false }) };
        }
        internal static void Run()
        {
            BuiltInsAndPair();
            RealProviderConnector();
            RegistrationRaceAndInvalidResults();
            Console.WriteLine("F17.FACTORY PASS: built-ins, real custom TCP Connector, registration provenance, snapshot, revocation and races");
        }
        private static void BuiltInsAndPair()
        {
            var factory = new FTRChannelFactory();
            Check(factory.Discover().Length == 15 && factory.Discover().Select(x => x.TypeId).Distinct().Count() == 15,
                "内置发现必须保持原12类型");
            foreach (FTRChannelKind kind in Enum.GetValues(typeof(FTRChannelKind)))
            {
                if (kind == FTRChannelKind.Custom || kind == FTRChannelKind.Virtual) continue;
                var config = FTRChannelConfigurations.Create(kind);
                using (var channel = factory.Create(config))
                    Check(channel.State == FTRChannelState.Closed && factory.DescribeAttached(config, channel).TypeId ==
                        config.DescribeType().TypeId, "创建不得打开设备或丢失类型：" + kind);
            }
            Reject<NotSupportedException>(() => factory.Create(new FTRVirtualChannelConfiguration()), "Virtual不得暗造对端");
            var pair = factory.CreateVirtualPair(new FTRVirtualChannelConfiguration { Id = "factory-left" },
                new FTRVirtualChannelConfiguration { Id = "factory-right" });
            using (pair[0]) using (pair[1])
            {
                byte received = 0;
                pair[1].Received += data => received = data.Data[0];
                pair[0].Open(); pair[1].Open(); pair[0].Send(new byte[] { 47 });
                Check(received == 47, "Factory创建的显式Virtual对必须真实收发");
            }
            Reject<NotSupportedException>(() => factory.Create(Config()), "未注册不能创建");
            var extensionConfig = Config(); extensionConfig.SettingsJson = "{\"endpoint\":\"COM7\",\"mode\":2}";
            var codec = new FTRChannelConfigurationConverter();
            Check(((FTRCustomChannelConfiguration)codec.Read(codec.Write(extensionConfig))).SettingsJson == extensionConfig.SettingsJson,
                "Custom设置应走正式FCM往返");
            extensionConfig.SettingsJson = "[]";
            Reject<ArgumentException>(() => extensionConfig.Validate(), "Provider配置必须为对象");
            var reserved = TcpProvider(1);
            reserved.Descriptor = new FTRChannelTypeDescriptor("tcp.client", FTRChannelTopology.SinglePeer, FTRIOCapabilities.ByteStream);
            Reject<ArgumentException>(() => factory.Register(reserved), "内置TypeId不得覆盖");
        }
        private static void RealProviderConnector()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            try
            {
                var factory = new FTRChannelFactory();
                var provider = TcpProvider(((IPEndPoint)listener.LocalEndpoint).Port);
                var token = factory.Register(provider);
                Reject<ArgumentException>(() => factory.Register(provider), "重复注册");
                var config = Config();
                using (var channel = factory.Create(config))
                {
                    Check(factory.Discover().Length == 16, "扩展发现");
                    Reject<NotSupportedException>(() => new FTRChannelFactory().DescribeAttached(config, channel), "跨Factory伪造");
                    var changed = Config(); changed.SendByteBudget++;
                    Reject<NotSupportedException>(() => factory.DescribeAttached(changed, channel), "配置改变不能冒充原凭据");
                    var moduleConfig = new MCNConnectorConfiguration { DisplayName = "正式Provider实例", Channel = config,
                        Protocol = new MPAProtocolConfiguration("factory-empty", "empty") };
                    var connector = new MCNConnectorModule(moduleConfig.Describe("factory-module", MFMModuleKind.Connector),
                        moduleConfig, channel, new MPAEmptyProtocol(), factory);
                    Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
                    connector.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    using (var peer = accepted.GetAwaiter().GetResult())
                    using (var arrived = new ManualResetEventSlim())
                    {
                        peer.ReceiveTimeout = 3000;
                        int received = 0;
                        connector.PayloadReceived += bytes => { Interlocked.Add(ref received, bytes.Length); arrived.Set(); };
                        var stream = peer.GetStream(); stream.Write(new byte[] { 7, 8, 9 }, 0, 3);
                        Check(arrived.Wait(3000) && received == 3, "Custom经真实TCP与Empty规约接收");
                        connector.SendPayloadAsync(new byte[] { 61 }, CancellationToken.None).GetAwaiter().GetResult();
                        Check(stream.ReadByte() == 61, "Custom Connector真实反向发送");
                        provider.Build = _ => channel;
                        Reject<InvalidOperationException>(() => factory.Create(config), "不能复用已交付实例");
                        Check(channel.State == FTRChannelState.Open, "重复创建拒绝不能关闭原实例");
                        token.Dispose(); token.Dispose();
                        Check(factory.DescribeAttached(config, channel).TypeId == "vendor.tcp", "注销不夺走已创建实例所有权");
                        Reject<NotSupportedException>(() => factory.Create(config), "注销后不得创建");
                        connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                    }
                }
                // 旧令牌重复Dispose不得删除同名的新注册。
                var replacement = factory.Register(TcpProvider(1)); token.Dispose();
                Check(factory.Describe(Config()).TypeId == "vendor.tcp", "旧注销令牌不得越代"); replacement.Dispose();
                // Message Provider不得绕过LengthFrame的ByteStream能力门禁。
                var message = TcpProvider(1);
                message.Descriptor = new FTRChannelTypeDescriptor("vendor.tcp", FTRChannelTopology.SinglePeer, FTRIOCapabilities.Message);
                using (factory.Register(message)) using (var channel = factory.Create(config))
                {
                    var invalid = new MCNConnectorConfiguration { DisplayName = "能力反证", Channel = config,
                        Protocol = new MPAProtocolConfiguration("factory-length", "length.frame") };
                    Reject<ArgumentException>(() => new MCNConnectorModule(invalid.Describe("bad", MFMModuleKind.Connector),
                        invalid, channel, new MPALengthFrameProtocol(), factory), "必须检查真实声明的IO能力");
                }
            }
            finally { listener.Stop(); }
        }
        private static void RegistrationRaceAndInvalidResults()
        {
            var factory = new FTRChannelFactory(); var provider = TcpProvider(1);
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                IFTRChannelTransport candidate = null;
                provider.Build = config => {
                    candidate = new FTRTcpClientChannel(new FTRTcpClientChannelConfiguration { Id = config.Id });
                    entered.Set(); if (!release.Wait(3000)) throw new TimeoutException("factory test release"); return candidate;
                };
                var token = factory.Register(provider);
                var creating = Task.Run(() => Reject<InvalidOperationException>(() => factory.Create(Config()), "创建途中注销"));
                Check(entered.Wait(3000), "Provider创建未进入"); token.Dispose(); release.Set();
                Check(creating.Wait(3000), "注销不应在锁内等待Provider回调");
                Reject<ObjectDisposedException>(() => candidate.Open(), "撤销中的新实例必须释放");
            }
            provider = TcpProvider(1); provider.Build = _ => null;
            using (factory.Register(provider)) Reject<InvalidOperationException>(() => factory.Create(Config()), "空结果拒绝");
            provider = TcpProvider(1);
            provider.Build = config => new FTRTcpClientChannel(new FTRTcpClientChannelConfiguration { Id = "wrong" });
            using (factory.Register(provider)) Reject<InvalidOperationException>(() => factory.Create(Config()), "Id不匹配拒绝");
            provider = TcpProvider(1);
            provider.Build = config => new FTRTcpServerChannel(new FTRTcpServerChannelConfiguration { Id = config.Id });
            using (factory.Register(provider)) Reject<InvalidOperationException>(() => factory.Create(Config()), "拓扑不匹配拒绝");
            provider = TcpProvider(1);
            provider.Build = config => {
                string id = config.Id; config.Id = "provider-mutated";
                return new FTRTcpClientChannel(new FTRTcpClientChannelConfiguration { Id = id });
            };
            using (factory.Register(provider)) using (var channel = factory.Create(Config()))
                Check(factory.DescribeAttached(Config(), channel).TypeId == "vendor.tcp", "Provider只能修改其独立配置快照");
        }
    }
}
