using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Models
{
    // Versioned, immutable TEST observation. Object IDs here only refer to real Owner objects.
    public sealed class TCTObservationEnvelope
    {
        public const int CurrentSchema = 1;
        public TCTObservationEnvelope(int schemaVersion, string runId, string attemptId,
            string kind, string title, string origin, string details, string sourceObjectId,
            string definitionAddress, string runtimeInstanceId, TCTArtifactRef payloadRef,
            DateTime? timestampUtc = null)
        {
            if (schemaVersion != CurrentSchema) throw new NotSupportedException("Unknown observation schema");
            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(attemptId))
                throw new ArgumentException("Observation requires RunId and AttemptId");
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Observation kind/title required");
            if (details != null && details.Length > 16000)
                throw new InvalidDataException("Observation details too large; use PayloadRef");
            if (payloadRef != null && payloadRef.AttemptId != attemptId)
                throw new InvalidDataException("Observation payload belongs to another Attempt");
            SchemaVersion = schemaVersion; RunId = runId; AttemptId = attemptId;
            Kind = kind; Title = title; Origin = origin ?? "Actual"; Details = details ?? "";
            SourceObjectId = sourceObjectId ?? ""; DefinitionAddress = definitionAddress ?? "";
            RuntimeInstanceId = runtimeInstanceId ?? ""; PayloadRef = payloadRef;
            TimestampUtc = timestampUtc ?? DateTime.UtcNow;
            if (TimestampUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Observation time must be UTC");
        }
        public int SchemaVersion { get; private set; }
        public string RunId { get; private set; }
        public string AttemptId { get; private set; }
        public string Kind { get; private set; }
        public string Title { get; private set; }
        public string Origin { get; private set; }
        public string Details { get; private set; }
        public string SourceObjectId { get; private set; }
        public string DefinitionAddress { get; private set; }
        public string RuntimeInstanceId { get; private set; }
        public TCTArtifactRef PayloadRef { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }

    public static class TCTObservationCodec
    {
        public const string Mime = "application/vnd.cxvisual.test-observation+xml";
        public static byte[] Serialize(TCTObservationEnvelope e)
        {
            if (e == null) throw new ArgumentNullException("e");
            XElement root = new XElement("Observation",
                new XAttribute("schema", e.SchemaVersion),
                new XAttribute("runId", e.RunId),
                new XAttribute("attemptId", e.AttemptId),
                new XAttribute("kind", e.Kind),
                new XAttribute("title", e.Title),
                new XAttribute("origin", e.Origin),
                new XAttribute("utcTicks", e.TimestampUtc.Ticks),
                new XAttribute("sourceObjectId", e.SourceObjectId),
                new XAttribute("definitionAddress", e.DefinitionAddress),
                new XAttribute("runtimeInstanceId", e.RuntimeInstanceId),
                new XElement("Details", e.Details));
            if (e.PayloadRef != null)
                root.Add(new XElement("Payload",
                    new XAttribute("path", e.PayloadRef.RelativePath),
                    new XAttribute("mime", e.PayloadRef.Mime),
                    new XAttribute("hash", e.PayloadRef.Sha256),
                    new XAttribute("bytes", e.PayloadRef.ByteSize),
                    new XAttribute("attemptId", e.PayloadRef.AttemptId)));
            byte[] data = Encoding.UTF8.GetBytes(new XDocument(root).ToString(SaveOptions.DisableFormatting));
            if (data.Length > 256 * 1024) throw new InvalidDataException("Observation envelope too large");
            return data;
        }
        public static TCTObservationEnvelope Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > 256 * 1024)
                throw new InvalidDataException("Invalid observation bytes");
            XDocument doc;
            XmlReaderSettings settings = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 256 * 1024 };
            using (MemoryStream ms = new MemoryStream(data, false))
            using (XmlReader reader = XmlReader.Create(ms, settings))
                doc = XDocument.Load(reader, LoadOptions.None);
            XElement root = doc.Root;
            if (root == null || root.Name != "Observation")
                throw new InvalidDataException("Not an observation envelope");
            if ((int?)root.Attribute("schema") != TCTObservationEnvelope.CurrentSchema)
                throw new NotSupportedException("Unsupported observation schema");
            XElement payload = root.Element("Payload");
            TCTArtifactRef reference = payload == null ? null :
                new TCTArtifactRef((string)payload.Attribute("path"),
                    (string)payload.Attribute("mime"), (string)payload.Attribute("hash"),
                    (long)payload.Attribute("bytes"), (string)payload.Attribute("attemptId"));
            return new TCTObservationEnvelope((int)root.Attribute("schema"),
                (string)root.Attribute("runId"), (string)root.Attribute("attemptId"),
                (string)root.Attribute("kind"), (string)root.Attribute("title"),
                (string)root.Attribute("origin"), (string)root.Element("Details"),
                (string)root.Attribute("sourceObjectId"), (string)root.Attribute("definitionAddress"),
                (string)root.Attribute("runtimeInstanceId"), reference,
                new DateTime((long)root.Attribute("utcTicks"), DateTimeKind.Utc));
        }
        // Actual artifacts are opened via the content-addressed store, including SHA256 verification.
        public static TCTObservationEnvelope Replay(TCTArtifactRef evidence,
            TCTTestArtifactStore store, string expectedAttemptId)
        {
            if (evidence == null || store == null) throw new ArgumentNullException("evidence/store");
            if (evidence.Mime != Mime || evidence.AttemptId != expectedAttemptId)
                throw new InvalidDataException("Observation evidence type/Attempt mismatch");
            TCTObservationEnvelope record = Deserialize(store.Read(evidence));
            if (record.AttemptId != expectedAttemptId)
                throw new InvalidDataException("Observation envelope cross-Attempt replay");
            if (record.PayloadRef != null) store.Read(record.PayloadRef);
            return record;
        }
    }

    // Replays only verified, real artifacts; never generates fake objects for a missing schema.
    public static class TCTObservationReplay
    {
        public static IList<TCTObservationEnvelope> Read(TCTTestCaseResult result,
            TCTTestArtifactStore store = null)
        {
            if (result == null) throw new ArgumentNullException("result");
            if (store == null)
            {
                if (result.RunId == "AdHoc")
                    throw new InvalidOperationException("AdHoc observations require explicit store");
                store = new TCTTestArtifactStore(TCTTestArtifactStore.RunRoot(result.RunId));
            }
            var output = new List<TCTObservationEnvelope>();
            foreach (TCTTestObservation observation in result.Observations)
            {
                if (observation.Envelope == null) continue;
                if (observation.Evidence == null ||
                    !result.Artifacts.Any(x => x.RelativePath == observation.Evidence.RelativePath &&
                        x.AttemptId == result.AttemptId && x.Sha256 == observation.Evidence.Sha256))
                    throw new InvalidDataException("Observation missing its registered evidence artifact");
                TCTObservationEnvelope record = TCTObservationCodec.Replay(observation.Evidence,
                    store, result.AttemptId);
                if (record.RunId != result.RunId ||
                    record.Kind != observation.Kind ||
                    record.Title != observation.Title ||
                    record.Origin != observation.Origin ||
                    record.Details != observation.Details ||
                    record.TimestampUtc != observation.Envelope.TimestampUtc ||
                    record.SourceObjectId != observation.Envelope.SourceObjectId ||
                    record.RuntimeInstanceId != observation.Envelope.RuntimeInstanceId ||
                    record.DefinitionAddress != observation.Envelope.DefinitionAddress ||
                    (record.PayloadRef == null) != (observation.Envelope.PayloadRef == null) ||
                    (record.PayloadRef != null && (record.PayloadRef.Sha256 !=
                        observation.Envelope.PayloadRef.Sha256 ||
                        record.PayloadRef.Mime != observation.Envelope.PayloadRef.Mime)))
                    throw new InvalidDataException("Observation replay payload/identity mismatch");
                output.Add(record);
            }
            return output.AsReadOnly();
        }
    }
}
