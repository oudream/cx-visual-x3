using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.Mechanism.Connectivity;

namespace CxVisual.TestCenter.Models
{
    /// <summary>只读Virtual/Replay通信证据，生产Channel负责真实字节/Session/解析。</summary>
    public static class TCTChannelEvidence
    {
        public const string Mime = "application/vnd.cxvisual.channel-evidence+xml";
        private const int MaxBytes = 2 * 1024 * 1024;
        public static byte[] Serialize(IEnumerable<MCCChannelFrame> frames)
        {
            if (frames == null) throw new ArgumentNullException("frames");
            MCCChannelFrame[] items = frames.OrderBy(x => x.TimestampUtc)
                .ThenBy(x => x.ChannelId, StringComparer.Ordinal)
                .ThenBy(x => x.Sequence).ToArray();
            if (items.Length == 0 || items.Length > 5000)
                throw new InvalidDataException("Empty/oversized Channel evidence");
            // v2：Channel只保留原始IO和错误记录；Protocol独立解析。
            if (items.Any(x => x.DecodedPayload != null))
                throw new InvalidDataException("Channel原始证据不允许保存Protocol解码副本");
            XElement root = new XElement("ChannelEvidence",
                new XAttribute("schema", 2), new XAttribute("mode", "RawChannel"));
            foreach (MCCChannelFrame frame in items)
                root.Add(new XElement("Frame",
                    new XAttribute("session", frame.SessionId),
                    new XAttribute("channel", frame.ChannelId),
                    new XAttribute("sequence", frame.Sequence),
                    new XAttribute("utc", frame.TimestampUtc.ToString("o")),
                    new XAttribute("direction", frame.Direction),
                    new XAttribute("correlation", frame.CorrelationId),
                    new XAttribute("error", frame.Error),
                    new XAttribute("raw", Convert.ToBase64String(frame.Bytes))));
            byte[] bytes = Encoding.UTF8.GetBytes(new XDocument(root).ToString(SaveOptions.DisableFormatting));
            Read(bytes);
            return bytes;
        }

        public static XDocument Read(byte[] raw)
        {
            if (raw == null || raw.Length == 0 || raw.Length > MaxBytes)
                throw new InvalidDataException("Missing/oversized Channel evidence");
            XDocument doc;
            using (MemoryStream stream = new MemoryStream(raw))
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaxBytes, IgnoreWhitespace = true }))
                doc = XDocument.Load(reader);
            XElement root = doc.Root;
            bool rawV2 = root != null && root.Name == "ChannelEvidence" &&
                (string)root.Attribute("schema") == "2" &&
                (string)root.Attribute("mode") == "RawChannel";
            bool legacyV1 = root != null && root.Name == "ChannelEvidence" &&
                (string)root.Attribute("schema") == "1" &&
                (string)root.Attribute("mode") == "Virtual";
            if (!rawV2 && !legacyV1)
                throw new InvalidDataException("Unknown Channel evidence schema/mode");
            XElement[] frames = root.Elements("Frame").ToArray();
            if (frames.Length == 0 || frames.Length > 5000 || root.Elements().Count() != frames.Length)
                throw new InvalidDataException("Channel evidence frame budget/shape");
            var cursor = new Dictionary<string, long>(StringComparer.Ordinal);
            MPALengthFrameCodec codec = new MPALengthFrameCodec();
            foreach (XElement frame in frames)
            {
                Guid session;
                long sequence;
                DateTime timestamp;
                string channel = (string)frame.Attribute("channel");
                string direction = (string)frame.Attribute("direction");
                string encoded = (string)frame.Attribute("raw");
                if (!Guid.TryParse((string)frame.Attribute("session"), out session) ||
                    session == Guid.Empty || string.IsNullOrWhiteSpace(channel) ||
                    !long.TryParse((string)frame.Attribute("sequence"), out sequence) ||
                    sequence <= 0 || !DateTime.TryParse((string)frame.Attribute("utc"),
                        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp) ||
                    timestamp.Kind != DateTimeKind.Utc ||
                    !Enum.IsDefined(typeof(MCCFrameDirection), direction) || encoded == null)
                    throw new InvalidDataException("Invalid Channel frame identity");
                long previous;
                if (cursor.TryGetValue(channel, out previous) && sequence <= previous)
                    throw new InvalidDataException("Channel events reordered");
                cursor[channel] = sequence;
                byte[] data, payload = null;
                try
                {
                    data = Convert.FromBase64String(encoded);
                    if (frame.Attribute("decoded") != null)
                        payload = Convert.FromBase64String((string)frame.Attribute("decoded"));
                }
                catch (FormatException error) { throw new InvalidDataException("Invalid Channel byte encoding", error); }
                if ((data.Length == 0 && direction != "Error") || data.Length > 65536)
                    throw new InvalidDataException("Invalid Channel byte budget");
                if (rawV2 && payload != null)
                    throw new InvalidDataException("原始 Channel 证据不得包含协议解码数据");
                if (legacyV1 && direction == "Rx")
                {
                    if (payload == null || !codec.Decode(data).SequenceEqual(payload))
                        throw new InvalidDataException("Legacy RX 解码负载不匹配");
                }
                else if (payload != null)
                    throw new InvalidDataException("Only legacy verified RX may contain decoded payload");
                if (direction == "Error" && string.IsNullOrWhiteSpace((string)frame.Attribute("error")))
                    throw new InvalidDataException("Error frame cannot be missing error details");
            }
            return doc;
        }
    }
}