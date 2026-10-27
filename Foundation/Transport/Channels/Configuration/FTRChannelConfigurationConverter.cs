using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelConfigurationConverter : IFCMConfigValueConverter<FTRChannelConfiguration>
    {
        public sealed class KindProbe
        {
            public KindProbe() { }
            public FTRChannelKind Kind { get; set; } = FTRChannelKind.Virtual;
        }
        public sealed class LegacyFlat
        {
            public string Id { get; set; } = "channel01";
            public FTRChannelKind Kind { get; set; } = FTRChannelKind.Virtual;
            public string CustomTypeId { get; set; } = "";
            public string SettingsJson { get; set; } = "{}";
            public string Host { get; set; } = "127.0.0.1";
            public int Port { get; set; } = 502;
            public int ConnectTimeoutMs { get; set; } = 5000;
            public int MaxClients { get; set; } = 128;
            public string PortName { get; set; } = "COM1";
            public int BaudRate { get; set; } = 9600;
            public int DataBits { get; set; } = 8;
            public string Parity { get; set; } = "None";
            public string StopBits { get; set; } = "One";
            public string SubChannelRule { get; set; } = "";
            public FTRChannelIOMode IoMode { get; set; } = FTRChannelIOMode.AsyncIO;
            public int QueueCapacity { get; set; } = 128;
            public int ReceiveByteBudget { get; set; } = 4 * 1024 * 1024;
            public int ReceiveBufferBytes {get;set;} = 64*1024;
            public bool CapturePayloadEvidence {get;set;} = true;
            public int SendTimeoutMs {get;set;} = 30000;
            public int StopTimeoutMs {get;set;} = 5000;
            public int ReceiveIdleTimeoutMs {get;set;}
            public int SendQueueCapacity {get;set;} = 4;
            public int SendByteBudget {get;set;} = 64*1024*1024;
            public int ReconnectMaxAttempts { get; set; } = 10;
            public int ReconnectInitialDelayMs { get; set; } = 250;
            public double ReconnectBackoff { get; set; } = 2.0;
            public int ReconnectMaxDelayMs { get; set; } = 30000;
        }
        public string Write(FTRChannelConfiguration value)
        {
            if (value == null) return "null";
            value.Validate();
            return FCMConfigValueCodec.Serialize(value);
        }
        public FTRChannelConfiguration Read(string json)
        {
            FTRChannelKind kind = FCMConfigValueCodec.Deserialize<KindProbe>(json).Kind;
            Type type = FTRChannelConfigurations.TypeOf(kind);
            var allowed = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(type))
                allowed.Add(member.Name);
            string[] keys = FCMConfigValueCodec.GetObjectPropertyNames(json);
            bool legacy = false;
            foreach (string key in keys) if (!allowed.Contains(key)) legacy = true;
            FTRChannelConfiguration result;
            if (!legacy)
                result = (FTRChannelConfiguration)FCMConfigValueCodec.Deserialize(json, type);
            else
            {
                // 既要恢复旧版参数，也不能静默删除无关类别的非默认字段。
                var known = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (FCMConfigMemberDescriptor item in FCMConfigSchemaBuilder.Members(typeof(LegacyFlat)))
                    known.Add(item.Name);
                foreach (string key in keys) if (!known.Contains(key))
                    throw new FormatException("CHANNEL_LEGACY_UNKNOWN_FIELD: " + key);
                result = Migrate(FCMConfigValueCodec.Deserialize<LegacyFlat>(json));
            }
            result.Validate();
            return result;
        }
        private static FTRChannelConfiguration Migrate(LegacyFlat source)
        {
            FTRChannelConfiguration target = FTRChannelConfigurations.Create(source.Kind);
            target.Id = source.Id;
            target.IoMode = source.IoMode;
            target.QueueCapacity = source.QueueCapacity;
            target.ReceiveByteBudget = source.ReceiveByteBudget;
            target.ReceiveBufferBytes=source.ReceiveBufferBytes;
            target.CapturePayloadEvidence=source.CapturePayloadEvidence;
            target.SendTimeoutMs=source.SendTimeoutMs;target.StopTimeoutMs=source.StopTimeoutMs;
            target.ReceiveIdleTimeoutMs=source.ReceiveIdleTimeoutMs;
            target.SendQueueCapacity=source.SendQueueCapacity;target.SendByteBudget=source.SendByteBudget;
            target.ReconnectMaxAttempts = source.ReconnectMaxAttempts;
            target.ReconnectInitialDelayMs = source.ReconnectInitialDelayMs;
            target.ReconnectBackoff = source.ReconnectBackoff;
            target.ReconnectMaxDelayMs = source.ReconnectMaxDelayMs;

            FTRTcpClientChannelConfiguration tcpClient = target as FTRTcpClientChannelConfiguration;
            FTRTcpServerChannelConfiguration tcpServer = target as FTRTcpServerChannelConfiguration;
            FTRUdpChannelConfiguration udp = target as FTRUdpChannelConfiguration;
            FTRUdpServerChannelConfiguration udpServer = target as FTRUdpServerChannelConfiguration;
            FTRSerialChannelConfiguration serial = target as FTRSerialChannelConfiguration;
            FTRCustomChannelConfiguration custom = target as FTRCustomChannelConfiguration;
            if (tcpClient != null)
            {
                tcpClient.Host = source.Host; tcpClient.Port = source.Port;
                tcpClient.ConnectTimeoutMs = source.ConnectTimeoutMs;
            }
            if (tcpServer != null)
            {
                tcpServer.Host = source.Host; tcpServer.Port = source.Port;
                tcpServer.MaxClients = source.MaxClients; tcpServer.SubChannelRule = source.SubChannelRule;
            }
            if (udp != null) { udp.Host = source.Host; udp.Port = source.Port; }
            if (udpServer != null)
            {
                udpServer.Host = source.Host; udpServer.Port = source.Port;
                udpServer.MaxClients = source.MaxClients; udpServer.SubChannelRule = source.SubChannelRule;
            }
            if (serial != null)
            {
                serial.PortName = source.PortName; serial.BaudRate = source.BaudRate;
                serial.DataBits = source.DataBits; serial.Parity = source.Parity;
                serial.StopBits = source.StopBits;
            }
            if (custom != null) { custom.CustomTypeId = source.CustomTypeId; custom.SettingsJson = source.SettingsJson; }
            bool network = tcpClient != null || tcpServer != null || udp != null || udpServer != null;
            bool server = tcpServer != null || udpServer != null;
            if ((!network && (source.Host != "127.0.0.1" || source.Port != 502)) ||
                (tcpClient == null && source.ConnectTimeoutMs != 5000) ||
                (!server && (source.MaxClients != 128 || source.SubChannelRule != "")) ||
                (serial == null && (source.PortName != "COM1" || source.BaudRate != 9600 ||
                    source.DataBits != 8 || source.Parity != "None" || source.StopBits != "One")) ||
                (custom == null && (source.CustomTypeId != "" || source.SettingsJson != "{}")))
                throw new FormatException("CHANNEL_LEGACY_CONFLICT: 旧配置含与当前通道类型无关的非默认设置，需人工裁决");
            return target;
        }
    }
}
