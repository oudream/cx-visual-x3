using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using CxVisual.Core;
using CxVisual.Platform.Channels;
using Newtonsoft.Json.Linq;

namespace CxVisual.Platform.Protocols
{
    public sealed class CxProtocolDescriptor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; } = "1.0.0";
        public IList<string> Capabilities { get; set; } = new List<string>();
        public IDictionary<string, string> MessageSchemas { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public string SessionModel { get; set; } = "RequestResponse";
        public IDictionary<string, CxProtocolError> ErrorMap { get; set; } = new Dictionary<string, CxProtocolError>(StringComparer.Ordinal);
        public CxValidationResult Validate()
        {
            var result = new CxValidationResult(); System.Version version;
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) || !System.Version.TryParse(Version, out version) || string.IsNullOrWhiteSpace(SessionModel) ||
                Capabilities == null || MessageSchemas == null || ErrorMap == null)
                CxChannelValidation.Error(result, "PROTOCOL_DESCRIPTOR_INVALID", "Identity, version, session model and declaration collections are required.");
            if (Capabilities != null && (Capabilities.Any(string.IsNullOrWhiteSpace) || Capabilities.Distinct(StringComparer.Ordinal).Count() != Capabilities.Count))
                CxChannelValidation.Error(result, "PROTOCOL_CAPABILITIES_INVALID", "Capability names must be unique and nonempty.");
            if (MessageSchemas != null && MessageSchemas.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
                CxChannelValidation.Error(result, "PROTOCOL_MESSAGE_SCHEMA_INVALID", "Message schema identities are required.");
            if (ErrorMap != null && ErrorMap.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value == null || string.IsNullOrWhiteSpace(x.Value.Code) || string.IsNullOrWhiteSpace(x.Value.Category)))
                CxChannelValidation.Error(result, "PROTOCOL_ERROR_MAP_INVALID", "Error codes and categories are required.");
            return result;
        }
        public CxProtocolDescriptor Clone()
        {
            Validate().ThrowIfInvalid();
            return new CxProtocolDescriptor { Id = Id, Name = Name, Version = Version, SessionModel = SessionModel, Capabilities = Capabilities.ToList(),
                MessageSchemas = new Dictionary<string, string>(MessageSchemas, StringComparer.Ordinal), ErrorMap = ErrorMap.ToDictionary(x => x.Key, x => x.Value.Clone(), StringComparer.Ordinal) };
        }
    }
    public sealed class CxProtocolError
    {
        public string Code { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string RawCode { get; set; }
        public bool Retryable { get; set; }
        public CxProtocolError Clone() => (CxProtocolError)MemberwiseClone();
    }
    public sealed class CxProtocolFrame
    {
        public string FrameId { get; set; } = Guid.NewGuid().ToString("N");
        public byte[] RawBytes { get; set; } = new byte[0];
        public DateTime Timestamp { get; set; }
        public string Direction { get; set; }
        public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
        public string Checksum { get; set; }
        public string ChecksumAlgorithm { get; set; } = "SHA256";
        public bool ValidateChecksum()
        {
            if (string.IsNullOrWhiteSpace(FrameId) || RawBytes == null || Metadata == null || ChecksumAlgorithm != "SHA256" || Checksum == null || Checksum.Length != 64) return false;
            using (var hash = SHA256.Create())
            {
                var actual = string.Concat(hash.ComputeHash(RawBytes).Select(x => x.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
                return string.Equals(actual, Checksum, StringComparison.OrdinalIgnoreCase);
            }
        }
        public CxProtocolFrame Clone()
        {
            if (RawBytes == null || Metadata == null) throw new ArgumentException("PROTOCOL_FRAME_INVALID");
            var json = JObject.FromObject(Metadata, CxVariableValueRules.Serializer()); CxVariableValueRules.CheckJson(json);
            return new CxProtocolFrame { FrameId = FrameId, RawBytes = (byte[])RawBytes.Clone(), Timestamp = Timestamp, Direction = Direction,
                Checksum = Checksum, ChecksumAlgorithm = ChecksumAlgorithm, Metadata = json.ToObject<Dictionary<string, object>>(CxVariableValueRules.Serializer()) };
        }
    }
    public sealed class CxProtocolMessage
    {
        public string MessageType { get; set; }
        public IDictionary<string, object> Fields { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
        public string CorrelationId { get; set; }
        public DateTime Timestamp { get; set; }
        public object GetField(string name)
        { if (name == null) throw new ArgumentNullException(nameof(name)); object value; if (Fields == null || !Fields.TryGetValue(name, out value)) throw new KeyNotFoundException("PROTOCOL_FIELD_NOT_FOUND: " + name); return Copy(value); }
        public void SetField(string name, object value)
        { if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("PROTOCOL_FIELD_ID_REQUIRED", nameof(name)); if (Fields == null) throw new InvalidOperationException("PROTOCOL_FIELDS_REQUIRED"); Fields[name] = Copy(value); }
        private static object Copy(object value) { var json = CxVariableValueRules.Token(value); CxVariableValueRules.CheckJson(json); return json.DeepClone(); }
        public CxProtocolMessage Clone()
        {
            if (string.IsNullOrWhiteSpace(MessageType) || Fields == null) throw new ArgumentException("PROTOCOL_MESSAGE_INVALID");
            var copy = new CxProtocolMessage { MessageType = MessageType, CorrelationId = CorrelationId, Timestamp = Timestamp };
            foreach (var field in Fields) copy.SetField(field.Key, field.Value); return copy;
        }
    }
    public sealed class CxProtocolBinding
    {
        public string ProtocolId { get; set; }
        public string ChannelRef { get; set; }
        public IDictionary<string, string> Addressing { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
        public CxValidationResult Validate()
        {
            var result = new CxValidationResult();
            if (string.IsNullOrWhiteSpace(ProtocolId) || string.IsNullOrWhiteSpace(ChannelRef) || Addressing == null || Parameters == null)
                CxChannelValidation.Error(result, "PROTOCOL_BINDING_INVALID", "Protocol, channel and parameter declarations are required.");
            try { if (Parameters != null) CxVariableValueRules.CheckJson(JObject.FromObject(Parameters, CxVariableValueRules.Serializer())); }
            catch (Exception error) { CxChannelValidation.Error(result, "PROTOCOL_BINDING_RUNTIME_VALUE", error.Message); }
            return result;
        }
        public IChannel Resolve(IReadOnlyDictionary<string, IChannel> channels)
        {
            Validate().ThrowIfInvalid(); if (channels == null) throw new ArgumentNullException(nameof(channels)); IChannel channel;
            if (!channels.TryGetValue(ChannelRef, out channel) || channel == null || channel.ChannelId != ChannelRef) throw new InvalidOperationException("PROTOCOL_CHANNEL_NOT_FOUND: " + ChannelRef);
            return channel;
        }
    }
}
