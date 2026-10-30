using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>真实Modbus MBAP/FC03/FC06通过F17 VirtualChannel、MPA、MCN、MDS与Automation授权路径，
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
            FTRVirtualChannel[] pair = FTRVirtualChannel.CreatePair("aut-plc-channel", "aut-plc-device");
            ushort actualRegister = 42;
            int requests = 0;
            int writes = 0;
            bool ignoreWrite = false;
            bool ignoreRead = false;
            pair[1].Received += received =>
            {
                byte[] request = received.Data;
                if (request.Length < 12) return;
                byte function = request[7];
                if (function == 3 && ignoreRead) return;
                byte[] pdu;
                if (function == 3)
                {
                    pdu = new byte[] { 3, 2, (byte)(actualRegister >> 8), (byte)actualRegister };
                }
                else if (function == 6)
                {
                    writes++;
                    ushort requested = (ushort)((request[10] << 8) | request[11]);
                    if (!ignoreWrite) actualRegister = requested;
                    pdu = request.Skip(7).Take(5).ToArray();
                }
                else return;
                requests++;
                byte[] response = new byte[7 + pdu.Length];
                response[0] = request[0]; response[1] = request[1];
                response[5] = (byte)(1 + pdu.Length);
                response[6] = request[6];
                Buffer.BlockCopy(pdu, 0, response, 7, pdu.Length);
                pair[1].SendAsync(response, new FTRSubChannelAddress("aut-plc-device"),
                    CancellationToken.None).GetAwaiter().GetResult();
            };

            MPAProtocolConfiguration protocolConfig = new MPAProtocolConfiguration("aut-modbus", "modbus.tcp");
            protocolConfig.TimeoutMs = 80;
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
                Channel = new FTRVirtualChannelConfiguration { Id = "aut-plc-channel", Kind = FTRChannelKind.Virtual },
                Protocol = protocolConfig
            };
            MCNConnectorModule connector = new MCNConnectorModule(
                connectorConfig.Describe("aut-plc-connector", MFMModuleKind.Connector),
                connectorConfig, pair[0], new MPAModbusTcpProtocol());

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
            PADDeviceVariableProvider provider = bridge.CreateWriteProvider(
                (principal, variable) => principal == "operator" && variable.StableId == "word");
            using (MDSVariableProviderHost host = new MDSVariableProviderHost(registry))
            {
                host.Register(provider);
                catalog.Bind(host, "plc");
                pair[1].Open();
                connector.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                host.Start();
                try
                {
                    object first = bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(first is ushort && (ushort)first == 42 &&
                        table.Get<ushort>("Station.Word") == 42 &&
                        requests == 1 && writes == 0,
                        "F17/MPA/MCN真实03帧读取→MDS唯一变量");

                    Reject<UnauthorizedAccessException>(() => provider.WriteAsync("word", (ushort)60,
                        "guest", CancellationToken.None).GetAwaiter().GetResult());
                    Reject<InvalidCastException>(() => provider.WriteAsync("word", 60,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(writes == 0 && table.Get<ushort>("Station.Word") == 42,
                        "拒绝未授权/错误类型写入，禁止污染MDS");

                    Check(provider.WriteAsync("word", (ushort)55, "operator",
                        CancellationToken.None).GetAwaiter().GetResult() &&
                        writes == 1 && actualRegister == 55 &&
                        table.Get<ushort>("Station.Word") == 55,
                        "授权06写命令+协议回显+真实03设备回读一致才报告成功");
                    ignoreWrite = true;
                    Reject<PADPlcOutcomeUnknownException>(() => provider.WriteAsync("word", (ushort)77,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(actualRegister == 55 && table.Get<ushort>("Station.Word") == 55 &&
                        writes == 2, "ACK但硬件寄存器未变必须返回Unknown，不自动重发");

                    ignoreWrite = false; ignoreRead = true;
                    Reject<PADPlcOutcomeUnknownException>(() => provider.WriteAsync("word", (ushort)88,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(writes == 3 && actualRegister == 88 &&
                        table.Get<ushort>("Station.Word") == 55,
                        "写ACK后读回超时属于Unknown，不能伪造最新MDS成功样本");
                    ignoreRead = false;
                    object recovered = bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check((ushort)recovered == 88 && table.Get<ushort>("Station.Word") == 88,
                        "控制链恢复后须重新从现场读取事实");
                    Console.WriteLine("PASS Automation PLC Modbus 03/06 -> MDS / authorized readback / Unknown");
                }
                finally
                {
                    host.Stop();
                    connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                    pair[1].Close();
                }
            }
            Reject<InvalidOperationException>(() => bridge.ReadAndMapAsync("word", CancellationToken.None)
                .GetAwaiter().GetResult());
        }
    }
}
