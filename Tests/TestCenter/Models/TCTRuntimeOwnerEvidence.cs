using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.TestCenter.Models
{
    /// <summary>真实MRK会话、实例、状态观测及持久Journal的只读归档投影。绝不存业务Payload。</summary>
    public static class TCTRuntimeOwnerEvidence
    {
        public const string Mime = "application/vnd.cxvisual.runtime-owner-evidence+xml";
        private const int MaxBytes = 2 * 1024 * 1024;

        public static byte[] Serialize(MOCCompiledPlan compiled, MRKRuntimeSessionSnapshot active,
            MRKRuntimeSessionSnapshot settled, MRKFlowSnapshot flow,
            MRKFlowResult result, IDictionary<string, MRKStepObservation> observations,
            IEnumerable<MRKJournalRecord> journal, IEnumerable<MRKExecutionGroupSnapshot> groups)
        {
            if (compiled == null || active == null || settled == null || flow == null ||
                result == null || observations == null || journal == null || groups == null)
                throw new ArgumentNullException("Runtime production evidence");
            if (flow.FlowId != result.FlowId || flow.PlanId != compiled.Plan.Id ||
                active.Id != settled.Id || active.PlanRevision != compiled.EngineeringRevision ||
                settled.PlanRevision != compiled.EngineeringRevision || result.State != flow.State)
                throw new InvalidDataException("Foreign MRK Flow/Session/Plan identity");
            MRKJournalRecord[] records = journal.Where(x => x.FlowId == flow.FlowId).ToArray();
            MRKRecoveryDecision recovery = MRKJournalRecovery.Evaluate(flow.FlowId, records);
            XElement root = new XElement("RuntimeOwner", new XAttribute("schema", 1),
                new XAttribute("project", compiled.ProjectId),
                new XAttribute("revision", compiled.EngineeringRevision),
                new XAttribute("plan", compiled.Plan.Id), new XAttribute("flow", flow.FlowId),
                new XAttribute("state", result.State), new XAttribute("recovery", recovery.Disposition));
            root.Add(new XElement("Session", new XAttribute("id", active.Id),
                new XAttribute("activeState", active.State), new XAttribute("endState", settled.State),
                new XAttribute("active", active.ActiveFlows),
                new XAttribute("settled", settled.ActiveFlows),
                new XAttribute("rejected", settled.Rejected)));
            root.Add(new XElement("Flow", new XAttribute("createdUtc", flow.CreatedUtc.ToString("o")),
                new XAttribute("endedUtc", flow.EndedUtc.HasValue ? flow.EndedUtc.Value.ToString("o") : ""),
                new XAttribute("total", flow.TotalSteps),
                new XAttribute("completed", flow.CompletedSteps), new XAttribute("waiting", flow.WaitingSteps),
                new XAttribute("failed", flow.FailedSteps), new XAttribute("skipped", flow.SkippedSteps),
                new XAttribute("cancelRequested", flow.CancelRequested)));
            XElement steps = new XElement("Observations");
            foreach (MRKStepObservation obs in observations.Values.OrderBy(x => x.StepId, StringComparer.Ordinal))
            {
                MOCStepOrigin origin;
                if (!compiled.SourceMap.TryResolve(obs.StepId, out origin))
                    throw new InvalidDataException("MRK observation not in compiled SourceMap: " + obs.StepId);
                steps.Add(new XElement("Step", new XAttribute("id", obs.StepId),
                    new XAttribute("block", origin.BlockId), new XAttribute("state", obs.State),
                    new XAttribute("reason", obs.Reason ?? ""),
                    new XAttribute("invocation", obs.InvocationId ?? ""),
                    new XAttribute("address", origin.ContentAddress == null ? "" : origin.ContentAddress.ToString()),
                    new XAttribute("utc", obs.ChangedUtc.ToString("o"))));
            }
            root.Add(steps);
            XElement history = new XElement("Journal");
            foreach (MRKJournalRecord entry in records)
                history.Add(new XElement("Entry", new XAttribute("seq", entry.Sequence),
                    new XAttribute("kind", entry.Kind), new XAttribute("flow", entry.FlowId),
                    new XAttribute("plan", entry.PlanId), new XAttribute("revision", entry.Revision),
                    new XAttribute("step", entry.StepId), new XAttribute("state", entry.State),
                    new XAttribute("correlation", entry.CorrelationId),
                    new XAttribute("operation", entry.OperationId),
                    new XAttribute("utc", entry.TimestampUtc.ToString("o"))));
            root.Add(history);
            XElement groupNode = new XElement("Groups");
            foreach (MRKExecutionGroupSnapshot group in groups)
                groupNode.Add(new XElement("Group", new XAttribute("id", group.Id),
                    new XAttribute("active", group.Active), new XAttribute("waiting", group.Waiting),
                    new XAttribute("rejected", group.Rejected)));
            root.Add(groupNode);
            byte[] bytes = Encoding.UTF8.GetBytes(new XDocument(root).ToString(SaveOptions.DisableFormatting));
            Read(bytes);
            return bytes;
        }

        public static XDocument Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes)
                throw new InvalidDataException("Missing or oversized RuntimeOwner evidence");
            XDocument document;
            using (MemoryStream stream = new MemoryStream(bytes))
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaxBytes, IgnoreWhitespace = true }))
                document = XDocument.Load(reader);
            XElement root = document.Root;
            if (root == null || root.Name != "RuntimeOwner" ||
                (string)root.Attribute("schema") != "1") throw new InvalidDataException("Invalid Runtime schema");
            Guid project, flow;
            int revision;
            string plan = (string)root.Attribute("plan"), flowString = (string)root.Attribute("flow");
            if (!Guid.TryParse((string)root.Attribute("project"), out project) || project == Guid.Empty ||
                !Guid.TryParse(flowString, out flow) || flow == Guid.Empty ||
                !int.TryParse((string)root.Attribute("revision"), out revision) || revision < 0 ||
                string.IsNullOrWhiteSpace(plan) ||
                !Enum.IsDefined(typeof(MRKFlowState), (string)root.Attribute("state")) ||
                !Enum.IsDefined(typeof(MRKRecoveryDisposition), (string)root.Attribute("recovery")))
                throw new InvalidDataException("Invalid Runtime Flow/Plan/Project identity");
            XElement session = root.Element("Session"), snapshot = root.Element("Flow"),
                observations = root.Element("Observations"), journal = root.Element("Journal"),
                groups = root.Element("Groups");
            if (session == null || snapshot == null || observations == null || journal == null ||
                groups == null) throw new InvalidDataException("Incomplete RuntimeOwner evidence");
            if (string.IsNullOrWhiteSpace((string)session.Attribute("id")) ||
                !Enum.IsDefined(typeof(MRKRuntimeSessionState), (string)session.Attribute("activeState")) ||
                !Enum.IsDefined(typeof(MRKRuntimeSessionState), (string)session.Attribute("endState")))
                throw new InvalidDataException("Invalid MRK session state");
            foreach (XElement item in new[] { session, snapshot })
                foreach (XAttribute attr in item.Attributes())
                    if (new[] { "active", "settled", "rejected", "total", "completed", "waiting", "failed", "skipped" }
                        .Contains(attr.Name.LocalName))
                    {
                        long n;
                        if (!long.TryParse(attr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 0)
                            throw new InvalidDataException("Invalid MRK counter");
                    }
            if (observations.Elements("Step").Count() > 8192 || journal.Elements("Entry").Count() > 50000 ||
                groups.Elements("Group").Count() > 1024)
                throw new InvalidDataException("Runtime evidence exceeds observer budget");
            HashSet<string> stepIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement obs in observations.Elements("Step"))
            {
                Guid block;
                if (string.IsNullOrWhiteSpace((string)obs.Attribute("id")) ||
                    !stepIds.Add((string)obs.Attribute("id")) ||
                    !Guid.TryParse((string)obs.Attribute("block"), out block) || block == Guid.Empty ||
                    !Enum.IsDefined(typeof(MRKStepState), (string)obs.Attribute("state")))
                    throw new InvalidDataException("Invalid MRK step identity/state");
            }
            long previous = 0;
            foreach (XElement entry in journal.Elements("Entry"))
            {
                long seq, rev;
                Guid recordFlow;
                if (!long.TryParse((string)entry.Attribute("seq"), out seq) || seq <= previous ||
                    !long.TryParse((string)entry.Attribute("revision"), out rev) || rev != revision ||
                    !Guid.TryParse((string)entry.Attribute("flow"), out recordFlow) || recordFlow != flow ||
                    (string)entry.Attribute("plan") != plan ||
                    !Enum.IsDefined(typeof(MRKJournalKind), (string)entry.Attribute("kind")))
                    throw new InvalidDataException("Foreign or reordered MRK journal record");
                string step = (string)entry.Attribute("step");
                if (!string.IsNullOrWhiteSpace(step) && !stepIds.Contains(step))
                    throw new InvalidDataException("MRK journal has unmapped Step");
                previous = seq;
            }
            foreach (XElement group in groups.Elements("Group"))
            {
                int active, waiting;
                long rejected;
                if (string.IsNullOrWhiteSpace((string)group.Attribute("id")) ||
                    !int.TryParse((string)group.Attribute("active"), out active) || active < 0 ||
                    !int.TryParse((string)group.Attribute("waiting"), out waiting) || waiting < 0 ||
                    !long.TryParse((string)group.Attribute("rejected"), out rejected) || rejected < 0)
                    throw new InvalidDataException("Invalid MRK execution group");
            }
            return document;
        }
    }
}