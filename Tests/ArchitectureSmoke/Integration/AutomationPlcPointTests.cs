using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Professional.Automation.DeviceVariables;
using CxVisual.Professional.Automation.Plc;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实Modbus MBAP/FC03/FC06通过本机TCP回环、F17 TCP、MPA、MCN、MDS与Automation授权路径，
    /// 不使用伪造的Connector、SDK或“假通过”。G1；G2/G3需现场实机。</summary>
    internal static class AutomationPlcPointTests
    {
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("AUT-PLC-G1: " + message);
        }
        private static void Reject<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException("AUT-PLC-G1: expected " + typeof(TException).Name);
        }

        public static void Run()
        {
            // 使用真实OS TCP Socket，而非绕过MCN Connector的Modbus TCP Channel类型约束。
            // 模拟器只提供现场寄存器报文行为，不实现/替换正式F17、MCN或MPA。
            using (TcpPlcLoopback plc = new TcpPlcLoopback())
            {
            MPAProtocolConfiguration protocolConfig = new MPAProtocolConfiguration("aut-modbus", "modbus.tcp");
            protocolConfig.TimeoutMs = 500;
            protocolConfig.Points.Add(new MPAPointDefinition("word100", "holding:100", typeof(ushort))
            {
                StationAddress = "1", CanRead = true, CanWrite = true
            });
            protocolConfig.Mappings.Add(new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Variable,
                TargetRef = "plc/Station.Word", Direction = MPADataDirection.Inbound,
                Operation = MPADataOperation.WriteValue, Required = true
            });
            protocolConfig.Mappings.Add(new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Variable,
                TargetRef = "plc/Station.Word", Direction = MPADataDirection.Outbound,
                Operation = MPADataOperation.ReadValue, Required = true
            });
            MCNConnectorConfiguration connectorConfig = new MCNConnectorConfiguration {
                DisplayName = "AUT-PLC Connector",
                Channel = new FTRTcpClientChannelConfiguration {
                    Id = "aut-plc-channel", Kind = FTRChannelKind.TcpClient,
                    Host = "127.0.0.1", Port = plc.Port, ConnectTimeoutMs = 2000
                },
                Protocol = protocolConfig
            };
            Reject<ArgumentException>(() => new MCNConnectorConfiguration {
                DisplayName = "禁用虚拟Modbus TCP Connector",
                Channel = new FTRVirtualChannelConfiguration { Id = "invalid-virtual", Kind = FTRChannelKind.Virtual },
                Protocol = protocolConfig
            }.Validate());
            MCNConnectorModule connector = new MCNConnectorModule(
                connectorConfig.Describe("aut-plc-connector", MFMModuleKind.Connector),
                connectorConfig, new FTRTcpClientChannel(connectorConfig.Channel), new MPAModbusTcpProtocol());

            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable table = new MDSVariableTable("plc");
            PADDeviceVariableCatalog catalog = new PADDeviceVariableCatalog("aut-plc");
            catalog.Register(new PADDeviceVariableDefinition("word", "Station", "Word",
                typeof(ushort), null, true));
            catalog.InstallDefinitions(table);
            registry.Register(table);
            PADPlcPointBridge bridge = new PADPlcPointBridge(
                connector, catalog, new MDSProtocolDataTargetAccess(registry),
                new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "word100") });
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new MDSProtocolDataTargetAccess(registry),
                new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "missing-point") }));
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new MDSProtocolDataTargetAccess(registry),
                new FTRSubChannelAddress("aut-plc-channel"), "wrong-scope",
                new [] { new PADPlcPointBinding("word", "word100") }));
            // 不能将一个PLC测点额外映射到未授权MDS目标或命令中心；
            // 这类隐藏副作用可能使普通读点引起跨站写操作。
            MPADataMapping illicitVariable = new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Variable,
                TargetRef = "plc/Unexpected", Direction = MPADataDirection.Inbound,
                Operation = MPADataOperation.WriteValue, Required = true
            };
            protocolConfig.Mappings.Add(illicitVariable);
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new MDSProtocolDataTargetAccess(registry),
                new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "word100") }));
            protocolConfig.Mappings.Remove(illicitVariable);
            MPADataMapping illicitCommand = new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Command,
                TargetRef = "unsafe-command", Direction = MPADataDirection.Inbound,
                Operation = MPADataOperation.InvokeCommand, Required = true
            };
            protocolConfig.Mappings.Add(illicitCommand);
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new MDSProtocolDataTargetAccess(registry),
                new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "word100") }));
            protocolConfig.Mappings.Remove(illicitCommand);
            PADDeviceVariableProvider provider = bridge.CreateWriteProvider(
                (principal, variable) => principal == "operator" && variable.StableId == "word");
            using (MDSVariableProviderHost host = new MDSVariableProviderHost(registry))
            {
                host.Register(provider);
                catalog.Bind(host, "plc");
                connector.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                host.Start();
                try
                {
                    object first = bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(first is ushort && (ushort)first == 42 &&
                        table.Get<ushort>("Station.Word") == 42 &&
                        plc.Requests == 1 && plc.Writes == 0,
                        "F17/MPA/MCN真实03帧读取→MDS唯一变量");

                    // 运行态点表发生跨目标或地址漂移时，必须在发送报文前拒绝。
                    protocolConfig.Mappings.Add(illicitCommand);
                    Reject<InvalidOperationException>(() => bridge.ReadAndMapAsync("word",
                        CancellationToken.None).GetAwaiter().GetResult());
                    protocolConfig.Mappings.Remove(illicitCommand);
                    MPAPointDefinition livePoint = protocolConfig.Points[0];
                    string originalAddress = livePoint.Address;
                    livePoint.Address = "holding:101";
                    Reject<InvalidOperationException>(() => bridge.ReadAndMapAsync("word",
                        CancellationToken.None).GetAwaiter().GetResult());
                    livePoint.Address = originalAddress;
                    Check(plc.Requests == 1 && plc.Writes == 0 &&
                        table.Get<ushort>("Station.Word") == 42,
                        "运行态点表漂移应先拒绝，不能触发未授权硬件报文");

                    Reject<UnauthorizedAccessException>(() => provider.WriteAsync("word", (ushort)60,
                        "guest", CancellationToken.None).GetAwaiter().GetResult());
                    Reject<InvalidCastException>(() => provider.WriteAsync("word", 60,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(plc.Writes == 0 && table.Get<ushort>("Station.Word") == 42,
                        "拒绝未授权/错误类型写入，禁止污染MDS");

                    Check(provider.WriteAsync("word", (ushort)55, "operator",
                        CancellationToken.None).GetAwaiter().GetResult() &&
                        plc.Writes == 1 && plc.ActualRegister == 55 &&
                        table.Get<ushort>("Station.Word") == 55,
                        "授权06写命令+协议回显+真实03设备回读一致才报告成功");
                    plc.IgnoreWrite = true;
                    Reject<PADPlcOutcomeUnknownException>(() => provider.WriteAsync("word", (ushort)77,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(plc.ActualRegister == 55 && table.Get<ushort>("Station.Word") == 55 &&
                        plc.Writes == 2, "ACK但硬件寄存器未变必须返回Unknown，不自动重发");

                    plc.IgnoreWrite = false; plc.IgnoreRead = true;
                    Reject<PADPlcOutcomeUnknownException>(() => provider.WriteAsync("word", (ushort)88,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(plc.Writes == 3 && plc.ActualRegister == 88 &&
                        table.Get<ushort>("Station.Word") == 55,
                        "写ACK后读回超时属于Unknown，不能伪造最新MDS成功样本");
                    plc.IgnoreRead = false;
                    object recovered = bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check((ushort)recovered == 88 && table.Get<ushort>("Station.Word") == 88,
                        "控制链恢复后须重新从现场读取事实");
                    Console.WriteLine("PASS Automation PLC real TCP loopback Modbus 03/06 -> MDS / authorized readback / Unknown");
                }
                finally
                {
                    host.Stop();
                    connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            Reject<InvalidOperationException>(() => bridge.ReadAndMapAsync("word", CancellationToken.None)
                .GetAwaiter().GetResult());
            }
        }

        private sealed class TcpPlcLoopback : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly Task _server;
            private int _register = 42;
            private int _requests;
            private int _writes;
            private volatile bool _ignoreWrite;
            private volatile bool _ignoreRead;
            private Exception _failure;
            public int Port { get; private set; }
            public ushort ActualRegister { get { return (ushort)Volatile.Read(ref _register); } }
            public int Requests { get { return Volatile.Read(ref _requests); } }
            public int Writes { get { return Volatile.Read(ref _writes); } }
            public bool IgnoreWrite { get { return _ignoreWrite; } set { _ignoreWrite = value; } }
            public bool IgnoreRead { get { return _ignoreRead; } set { _ignoreRead = value; } }

            public TcpPlcLoopback()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _server = Task.Run((Action)Serve);
            }

            private static bool ReadExactly(NetworkStream stream, byte[] bytes)
            {
                int offset = 0;
                while (offset != bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0)
                    {
                        if (offset == 0) return false;
                        throw new EndOfStreamException("Modbus TCP MBAP半帧截断");
                    }
                    offset += read;
                }
                return true;
            }

            private void Serve()
            {
                try
                {
                    using (TcpClient peer = _listener.AcceptTcpClient())
                    using (NetworkStream stream = peer.GetStream())
                    {
                        peer.NoDelay = true;
                        while (true)
                        {
                            byte[] mbap = new byte[7];
                            if (!ReadExactly(stream, mbap)) break;
                            int length = (mbap[4] << 8) | mbap[5];
                            if (length < 2 || length > 254)
                                throw new InvalidDataException("Modbus TCP请求MBAP长度不合法");
                            byte[] pdu = new byte[length - 1];
                            if (!ReadExactly(stream, pdu))
                                throw new EndOfStreamException("Modbus请求PDU截断");
                            byte function = pdu[0];
                            if (function == 3 && IgnoreRead) continue;
                            byte[] reply;
                            if (function == 3)
                            {
                                ushort value = ActualRegister;
                                reply = new byte[] { 3, 2, (byte)(value >> 8), (byte)value };
                            }
                            else if (function == 6 && pdu.Length == 5)
                            {
                                Interlocked.Increment(ref _writes);
                                ushort value = (ushort)((pdu[3] << 8) | pdu[4]);
                                if (!IgnoreWrite) Volatile.Write(ref _register, value);
                                reply = (byte[])pdu.Clone();
                            }
                            else throw new InvalidDataException("不支持的Modbus测试功能码");
                            Interlocked.Increment(ref _requests);
                            byte[] frame = new byte[7 + reply.Length];
                            Buffer.BlockCopy(mbap, 0, frame, 0, 7);
                            frame[4] = 0;
                            frame[5] = (byte)(reply.Length + 1);
                            Buffer.BlockCopy(reply, 0, frame, 7, reply.Length);
                            stream.Write(frame, 0, frame.Length);
                        }
                    }
                }
                catch (SocketException error) { _failure = error; }
                catch (IOException error) { _failure = error; }
                catch (ObjectDisposedException) { }
            }
            public void Dispose()
            {
                _listener.Stop();
                if (!_server.Wait(3000))
                    throw new TimeoutException("PLC TCP回环服务器未退出");
                if (_failure != null && !( _failure is IOException || _failure is SocketException ))
                    throw new InvalidOperationException("PLC TCP模拟端意外失败", _failure);
            }
        }
    }
}
