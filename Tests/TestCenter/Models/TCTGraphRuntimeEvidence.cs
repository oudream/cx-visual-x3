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
            XElement definition = new XElement("Definition");
            WriteBlock(definition, logic.Root, 0);
            return SerializeOwner(snapshot, definition, compiled, result, trace);
        }

        // MOL V2自由连接Owner，直接读取保存过的MPC端口及MOL Link，不从编译依赖倒推连线。
        public static byte[] Serialize(MEMProjectSnapshot snapshot, MOLWiringDefinition wiring,
            MOCCompiledPlan compiled, MRKFlowResult result, MOCTraceView trace)
        {
            if (wiring == null) throw new ArgumentNullException("wiring");
            wiring.ValidateExecutable();
            XElement definition = new XElement("Definition", new XAttribute("variant", "Wiring"),
                new XAttribute("entry", wiring.EntryBlockId));
            foreach (MOLControlBlock block in wiring.Blocks)
            {
                XElement node = new XElement("Block", new XAttribute("id", block.Id),
                    new XAttribute("kind", block.Kind),
                    new XAttribute("capability", block.CapabilityId ?? ""));
                foreach (MPCPortDescriptor port in wiring.Ports[block.Id])
                    node.Add(new XElement("Port", new XAttribute("member", port.MemberId),
                        new XAttribute("direction", port.Direction),
                        new XAttribute("type", port.TypeId),
                        new XAttribute("kind", port.Kind),
                        new XAttribute("required", port.Required)));
                MOLBlockPosition position;
                if (wiring.Positions.TryGetValue(block.Id, out position))
                {
                    node.SetAttributeValue("x", position.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    node.SetAttributeValue("y", position.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                }
                definition.Add(node);
            }
            foreach (MOLLink link in wiring.Links)
                definition.Add(new XElement("Link", new XAttribute("id", link.Id),
                    new XAttribute("sourceBlock", link.Source.BlockId),
                    new XAttribute("sourcePort", link.Source.PortMemberId),
                    new XAttribute("targetBlock", link.Target.BlockId),
                    new XAttribute("targetPort", link.Target.PortMemberId),
                    new XAttribute("kind", link.Kind),
                    new XAttribute("condition", link.ConditionId ?? "")));
            return SerializeOwner(snapshot, definition, compiled, result, trace);
        }

        private static byte[] SerializeOwner(MEMProjectSnapshot snapshot, XElement definition,
            MOCCompiledPlan compiled, MRKFlowResult result, MOCTraceView trace)
        {
            if (snapshot == null || compiled == null || result == null || trace == null)
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
            if ((string)definition.Attribute("variant") == "Wiring")
            {
                var ports = new Dictionary<string, Dictionary<string, XElement>>(StringComparer.Ordinal);
                foreach (XElement block in definition.Elements("Block"))
                {
                    string id = (string)block.Attribute("id");
                    var members = new Dictionary<string, XElement>(StringComparer.Ordinal);
                    foreach (XElement port in block.Elements("Port"))
                    {
                        string member = (string)port.Attribute("member");
                        if (string.IsNullOrWhiteSpace(member) || members.ContainsKey(member) ||
                            string.IsNullOrWhiteSpace((string)port.Attribute("type")) ||
                            !Enum.IsDefined(typeof(MPCPortDirection), (string)port.Attribute("direction")) ||
                            !Enum.IsDefined(typeof(MPCPortKind), (string)port.Attribute("kind")))
                            throw new InvalidDataException("Invalid/duplicate MPC typed Port");
                        members.Add(member, port);
                    }
                    ports.Add(id, members);
                }
                var linkIds = new HashSet<Guid>();
                foreach (XElement link in definition.Elements("Link"))
                {
                    Guid id;
                    string source = (string)link.Attribute("sourceBlock"),
                        target = (string)link.Attribute("targetBlock"),
                        a = (string)link.Attribute("sourcePort"),
                        b = (string)link.Attribute("targetPort");
                    Dictionary<string, XElement> outputs, inputs;
                    XElement outPort, inPort;
                    if (!Guid.TryParse((string)link.Attribute("id"), out id) || id == Guid.Empty ||
                        !linkIds.Add(id) || !ports.TryGetValue(source, out outputs) ||
                        !ports.TryGetValue(target, out inputs) ||
                        !outputs.TryGetValue(a, out outPort) || !inputs.TryGetValue(b, out inPort) ||
                        (string)outPort.Attribute("direction") != "Output" ||
                        (string)inPort.Attribute("direction") != "Input" ||
                        (string)outPort.Attribute("kind") != (string)link.Attribute("kind") ||
                        (string)inPort.Attribute("kind") != (string)link.Attribute("kind"))
                        throw new InvalidDataException("MOL link has invalid typed endpoints");
                }
                Guid entry;
                if (!Guid.TryParse((string)definition.Attribute("entry"), out entry) ||
                    !blocks.Contains(entry.ToString("D")))
                    throw new InvalidDataException("Missing MOL Wiring entry");
            }
            else if (definition.Attribute("variant") != null)
                throw new InvalidDataException("Unknown MOL Definition variant");
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