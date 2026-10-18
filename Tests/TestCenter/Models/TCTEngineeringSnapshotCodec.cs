using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.TestCenter.Models
{
    /// <summary>
    /// Read-only evidence projection of a committed MEMProjectSnapshot.
    /// It is NOT another engineering tree, serializer, compiler, or runtime.
    /// </summary>
    public static class TCTEngineeringSnapshotCodec
    {
        public const string Mime = "application/vnd.cxvisual.engineering-snapshot+xml";
        public const int SchemaVersion = 1;
        private const int MaximumBytes = 4 * 1024 * 1024;
        private const int MaximumNodes = 20000;

        public static byte[] Serialize(MEMProjectSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (snapshot.Nodes.Count > MaximumNodes)
                throw new InvalidDataException("Engineering evidence exceeds viewer node limit");
            XElement root = new XElement("EngineeringSnapshot",
                new XAttribute("schema", SchemaVersion),
                new XAttribute("projectId", snapshot.ProjectId),
                new XAttribute("revision", snapshot.Revision));
            foreach (MEMNodeSnapshot node in snapshot.Nodes)
            {
                XElement element = new XElement("Node",
                    new XAttribute("id", node.Id),
                    new XAttribute("name", node.Name),
                    new XAttribute("typeId", node.TypeId),
                    new XAttribute("kind", node.Kind),
                    new XAttribute("storageRootId", node.StorageRootId));
                if (node.ParentId.HasValue) element.Add(new XAttribute("parentId", node.ParentId.Value));
                if (node.Scope.HasValue) element.Add(new XAttribute("scope", node.Scope.Value));
                foreach (Guid child in node.ChildIds)
                    element.Add(new XElement("Child", new XAttribute("id", child)));
                if (node.Visual != null)
                {
                    MRCVisualSnapshot v = node.Visual;
                    element.Add(new XElement("Visual",
                        new XAttribute("x", Format(v.X)), new XAttribute("y", Format(v.Y)),
                        new XAttribute("width", Format(v.Width)), new XAttribute("height", Format(v.Height)),
                        new XAttribute("rotation", Format(v.Rotation)), new XAttribute("scaleX", Format(v.ScaleX)),
                        new XAttribute("scaleY", Format(v.ScaleY)), new XAttribute("shear", Format(v.Shear)),
                        new XAttribute("layer", v.LayerId ?? ""),
                        new XAttribute("visible", v.IsVisible),
                        new XAttribute("selectable", v.IsSelectable),
                        new XAttribute("locked", v.IsLocked)));
                }
                foreach (MRIReferenceSnapshot reference in node.References)
                {
                    XElement re = new XElement("Reference",
                        new XAttribute("id", reference.Id), new XAttribute("name", reference.Name),
                        new XAttribute("ownerId", reference.OwnerId),
                        new XAttribute("targetId", reference.TargetId),
                        new XAttribute("kind", reference.Kind));
                    element.Add(re);
                }
                foreach (MPMPropertySnapshot property in node.Properties)
                    element.Add(new XElement("Property",
                        new XAttribute("member", property.Descriptor.MemberId),
                        new XAttribute("kind", property.Descriptor.ValueKind),
                        new XAttribute("hasOverride", property.HasOverride)));
                foreach (MPCPort port in node.Ports)
                    element.Add(new XElement("Port", new XAttribute("id", port.Id),
                        new XAttribute("ownerId", port.OwnerId), new XAttribute("member", port.MemberId),
                        new XAttribute("direction", port.Direction), new XAttribute("kind", port.Kind),
                        new XAttribute("typeId", port.TypeId)));
                foreach (MPCConnection connection in node.Connections)
                    element.Add(new XElement("Connection", new XAttribute("id", connection.Id),
                        new XAttribute("source", connection.Source.Address.ToString()),
                        new XAttribute("sourcePort", connection.Source.PortId),
                        new XAttribute("target", connection.Target.Address.ToString()),
                        new XAttribute("targetPort", connection.Target.PortId)));
                foreach (MPMAspect aspect in node.Aspects)
                    element.Add(new XElement("Aspect", new XAttribute("kind", aspect.Kind),
                        new XAttribute("provider", aspect.ProviderId),
                        new XAttribute("schema", aspect.SchemaVersion),
                        new XAttribute("required", aspect.Required)));
                root.Add(element);
            }
            byte[] bytes = Encoding.UTF8.GetBytes(new XDocument(root).ToString(SaveOptions.DisableFormatting));
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Engineering evidence too large");
            return bytes;
        }

        public static XDocument Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes)
                throw new InvalidDataException("Invalid engineering snapshot evidence length");
            XDocument document;
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaximumBytes
            };
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (XmlReader reader = XmlReader.Create(stream, settings))
                document = XDocument.Load(reader, LoadOptions.None);
            XElement root = document.Root;
            if (root == null || root.Name != "EngineeringSnapshot" ||
                (int?)root.Attribute("schema") != SchemaVersion)
                throw new InvalidDataException("Unsupported engineering evidence schema");
            if (!Guid.TryParse((string)root.Attribute("projectId"), out Guid projectId) ||
                projectId == Guid.Empty || (int?)root.Attribute("revision") < 0)
                throw new InvalidDataException("Invalid engineering project identity or revision");
            XElement[] nodes = root.Elements("Node").ToArray();
            if (nodes.Length == 0 || nodes.Length > MaximumNodes ||
                root.Elements().Count() != nodes.Length)
                throw new InvalidDataException("Invalid engineering node collection");
            Dictionary<Guid, XElement> index = new Dictionary<Guid, XElement>();
            foreach (XElement node in nodes)
            {
                if (!Guid.TryParse((string)node.Attribute("id"), out Guid id) ||
                    id == Guid.Empty || index.ContainsKey(id))
                    throw new InvalidDataException("Duplicate or empty engineering node identity");
                index.Add(id, node);
                if (string.IsNullOrWhiteSpace((string)node.Attribute("kind")) ||
                    string.IsNullOrWhiteSpace((string)node.Attribute("name")))
                    throw new InvalidDataException("Engineering node missing kind or name");
            }
            int roots = 0;
            foreach (XElement node in nodes)
            {
                Guid id = Guid.Parse((string)node.Attribute("id"));
                Guid parentId = Guid.Empty;
                string parentText = (string)node.Attribute("parentId");
                if (string.IsNullOrEmpty(parentText))
                {
                    if (id != projectId || (string)node.Attribute("kind") != "Project") 
                        throw new InvalidDataException("Invalid engineering root");
                    roots++;
                }
                else if (!Guid.TryParse(parentText, out parentId) || !index.ContainsKey(parentId))
                    throw new InvalidDataException("Engineering parent missing");
                HashSet<Guid> children = new HashSet<Guid>();
                foreach (XElement child in node.Elements("Child"))
                {
                    if (!Guid.TryParse((string)child.Attribute("id"), out Guid childId) ||
                        !children.Add(childId) ||
                        !index.TryGetValue(childId, out XElement descendant) ||
                        (string)descendant.Attribute("parentId") != id.ToString())
                        throw new InvalidDataException("Engineering ownership link mismatch");
                }
                if (!string.IsNullOrEmpty(parentText))
                {
                    XElement parent = index[parentId];
                    if (!parent.Elements("Child").Any(x => (string)x.Attribute("id") == id.ToString()))
                        throw new InvalidDataException("Engineering child missing from parent list");
                }
                foreach (XElement reference in node.Elements("Reference"))
                {
                    if ((string)reference.Attribute("ownerId") != id.ToString() ||
                        !Guid.TryParse((string)reference.Attribute("targetId"), out Guid target) ||
                        !index.ContainsKey(target))
                        throw new InvalidDataException("Engineering reference owner/target mismatch");
                }
            }
            if (roots != 1) throw new InvalidDataException("Engineering root count invalid");
            // One root + local parent consistency is insufficient: an unrelated child cycle
            // could otherwise masquerade as a valid disconnected subtree.
            HashSet<Guid> reachable = new HashSet<Guid>();
            Stack<Guid> pending = new Stack<Guid>();
            pending.Push(projectId);
            while (pending.Count != 0)
            {
                Guid id = pending.Pop();
                if (!reachable.Add(id))
                    throw new InvalidDataException("Engineering ownership cycle or duplicate traversal");
                foreach (XElement child in index[id].Elements("Child"))
                    pending.Push(Guid.Parse((string)child.Attribute("id")));
            }
            if (reachable.Count != nodes.Length)
                throw new InvalidDataException("Engineering snapshot contains unreachable nodes");
            return document;
        }

        // Compares two verified, immutable snapshots of the SAME engineering project.
        // Revision comparison is not Golden approval or a substitute for Owner assertions.
        public static IList<string> Compare(byte[] before, byte[] after)
        {
            XDocument oldDocument = Deserialize(before);
            XDocument newDocument = Deserialize(after);
            string projectId = (string)oldDocument.Root.Attribute("projectId");
            if (projectId != (string)newDocument.Root.Attribute("projectId"))
                throw new InvalidDataException("Cannot compare snapshots of different projects");
            var oldNodes = oldDocument.Root.Elements("Node")
                .ToDictionary(x => (string)x.Attribute("id"), StringComparer.Ordinal);
            var newNodes = newDocument.Root.Elements("Node")
                .ToDictionary(x => (string)x.Attribute("id"), StringComparer.Ordinal);
            var changes = new List<string>();
            foreach (string id in oldNodes.Keys.Union(newNodes.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                XElement oldNode, newNode;
                bool hasOld = oldNodes.TryGetValue(id, out oldNode);
                bool hasNew = newNodes.TryGetValue(id, out newNode);
                if (!hasOld)
                    changes.Add("+ " + (string)newNode.Attribute("kind") + " · " +
                        (string)newNode.Attribute("name") + " (" + id + ")");
                else if (!hasNew)
                    changes.Add("- " + (string)oldNode.Attribute("kind") + " · " +
                        (string)oldNode.Attribute("name") + " (" + id + ")");
                else if (!XNode.DeepEquals(oldNode, newNode))
                    changes.Add("~ " + (string)oldNode.Attribute("kind") + " · " +
                        (string)oldNode.Attribute("name") + " → " +
                        (string)newNode.Attribute("name") + " (" + id + ")");
            }
            return changes.AsReadOnly();
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
