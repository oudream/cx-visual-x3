using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.TestCenter.Models
{
    // 生产 MEM/MOL/MOC/MRK 的只读证据投影，不建立第二套图或执行状态。
    public static class TCTGraphRuntimeEvidence
    {
        public const string Mime = "application/vnd.cxvisual.graph-runtime+xml";
        private const int MaxBytes = 2 * 1024 * 1024;

        public static byte[] Serialize(MEMProjectSnapshot snapshot, MOLLogicDefinition logic,
            MOCCompiledPlan compiled, MRKFlowResult result, MOCTraceView trace)
        {
            if (snapshot == null || logic == null || compiled == null || result == null || trace == null)
                throw new ArgumentNullException("GraphRuntime Owner objects");
            if (compiled.ProjectId != snapshot.ProjectId ||
                compiled.EngineeringRevision != snapshot.Revision || result.FlowId == Guid.Empty)
                throw new InvalidDataException("MOC/MEM revision or MRK Flow identity mismatch");
            XElement root = new XElement("GraphRuntime", new XAttribute("schema", 1),
                new XAttribute("projectId", snapshot.ProjectId),
                new XAttribute("revision", snapshot.Revision),
                new XAttribute("planId", compiled.Plan.Id),
                new XAttribute("flowId", result.FlowId),
                new XAttribute("flowState", result.State));
            XElement definition = new XElement("Definition");
            WriteBlock(definition, logic.Root, 0);
            root.Add(definition);
            XElement plan = new XElement("Plan");
            foreach (MRKExecutionStep step in compiled.Plan.Steps)
            {
                XElement row = new XElement("Step", new XAttribute("id", step.Id));
                foreach (string dependency in step.Dependencies)
                    row.Add(new XElement("Dependency", new XAttribute("id", dependency)));
                plan.Add(row);
            }
            root.Add(plan);
            XElement sources = new XElement("SourceMap");
            foreach (MOCStepOrigin origin in compiled.SourceMap.Steps.Values.OrderBy(x => x.StepId, StringComparer.Ordinal))
                sources.Add(new XElement("Map", new XAttribute("step", origin.StepId),
                    new XAttribute("block", origin.BlockId),
                    new XAttribute("address", origin.ContentAddress == null ? "" : origin.ContentAddress.ToString()),
                    new XAttribute("role", origin.Role ?? "")));
            root.Add(sources);
            XElement execution = new XElement("Execution");
            foreach (KeyValuePair<string, MRKStepState> step in result.StepStates.OrderBy(x => x.Key, StringComparer.Ordinal))
                execution.Add(new XElement("State", new XAttribute("step", step.Key),
                    new XAttribute("value", step.Value)));
            root.Add(execution);
            XElement timeline = new XElement("Timeline", new XAttribute("lost", trace.LostCount));
            foreach (MOCObservedStep step in trace.Events.OrderBy(x => x.Transition.Sequence))
                timeline.Add(new XElement("Event", new XAttribute("sequence", step.Transition.Sequence),
                    new XAttribute("step", step.Transition.StepId),
                    new XAttribute("block", step.Origin.BlockId),
                    new XAttribute("state", step.Transition.State),
                    new XAttribute("utc", step.Transition.TimestampUtc.ToString("o"))));
            root.Add(timeline);
            byte[] bytes = Encoding.UTF8.GetBytes(new XDocument(root).ToString(SaveOptions.DisableFormatting));
            Read(bytes);
            return bytes;
        }

        private static void WriteBlock(XElement parent, MOLControlBlock block, int depth)
        {
            if (depth > 32) throw new InvalidDataException("MOL definition depth exceeded");
            XElement x = new XElement("Block", new XAttribute("id", block.Id),
                new XAttribute("kind", block.Kind),
                new XAttribute("capability", block.CapabilityId ?? ""));
            parent.Add(x);
            foreach (MOLControlBlock child in block.Children) WriteBlock(x, child, depth + 1);
            foreach (MOLBranchCase branch in block.Cases) WriteBlock(x, branch.Body, depth + 1);
            if (block.Loop != null) WriteBlock(x, block.Loop.Body, depth + 1);
        }

        public static XDocument Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes)
                throw new InvalidDataException("Missing or excessive GraphRuntime bytes");
            XDocument data;
            using (MemoryStream input = new MemoryStream(bytes))
            using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaxBytes, IgnoreWhitespace = true }))
                data = XDocument.Load(reader);
            XElement root = data.Root;
            if (root == null || root.Name != "GraphRuntime" ||
                (string)root.Attribute("schema") != "1")
                throw new InvalidDataException("Unsupported GraphRuntime schema");
            Guid project, flow;
            int revision;
            if (!Guid.TryParse((string)root.Attribute("projectId"), out project) || project == Guid.Empty ||
                !Guid.TryParse((string)root.Attribute("flowId"), out flow) || flow == Guid.Empty ||
                !int.TryParse((string)root.Attribute("revision"), out revision) || revision < 0 ||
                string.IsNullOrWhiteSpace((string)root.Attribute("planId")) ||
                !new[] { "Completed", "Failed", "Cancelled" }.Contains((string)root.Attribute("flowState")))
                throw new InvalidDataException("Invalid GraphRuntime identity/state");
            XElement definition = root.Element("Definition"), plan = root.Element("Plan"),
                sources = root.Element("SourceMap"), execution = root.Element("Execution"),
                timeline = root.Element("Timeline");
            if (definition == null || plan == null || sources == null ||
                execution == null || timeline == null)
                throw new InvalidDataException("Incomplete GraphRuntime evidence");
            var blocks = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement block in definition.Descendants("Block"))
            {
                Guid id;
                string key = (string)block.Attribute("id");
                if (!Guid.TryParse(key, out id) || id == Guid.Empty || !blocks.Add(key))
                    throw new InvalidDataException("Invalid/duplicate MOL Block");
            }
            var steps = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement step in plan.Elements("Step"))
                if (string.IsNullOrWhiteSpace((string)step.Attribute("id")) ||
                    !steps.Add((string)step.Attribute("id")))
                    throw new InvalidDataException("Invalid/duplicate MOC Step");
            if (blocks.Count == 0 || steps.Count == 0 || blocks.Count > 4096 || steps.Count > 8192)
                throw new InvalidDataException("Empty/oversized graph");
            foreach (XElement dep in plan.Descendants("Dependency"))
                if (!steps.Contains((string)dep.Attribute("id")))
                    throw new InvalidDataException("Unknown compiled dependency");
            var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement row in sources.Elements("Map"))
            {
                string step = (string)row.Attribute("step"), block = (string)row.Attribute("block");
                if (!steps.Contains(step) || !blocks.Contains(block) || mapping.ContainsKey(step))
                    throw new InvalidDataException("Invalid MOC SourceMap link");
                mapping.Add(step, block);
            }
            foreach (XElement state in execution.Elements("State"))
                if (!steps.Contains((string)state.Attribute("step")) ||
                    !Enum.IsDefined(typeof(MRKStepState), (string)state.Attribute("value")))
                    throw new InvalidDataException("Invalid MRK StepState");
            long previous = 0;
            foreach (XElement evt in timeline.Elements("Event"))
            {
                long current;
                string step = (string)evt.Attribute("step");
                string block;
                if (!long.TryParse((string)evt.Attribute("sequence"), out current) ||
                    current <= previous || !mapping.TryGetValue(step, out block) ||
                    block != (string)evt.Attribute("block") ||
                    !Enum.IsDefined(typeof(MRKStepState), (string)evt.Attribute("state")))
                    throw new InvalidDataException("Unmapped/reordered MRK Trace");
                previous = current;
            }
            return data;
        }
    }
}