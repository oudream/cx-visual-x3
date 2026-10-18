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
                    id == Guid.Empty || !index.TryAdd(id, node))
                    throw new InvalidDataException("Duplicate or empty engineering node identity");
                if (string.IsNullOrWhiteSpace((string)node.Attribute("kind")) ||
                    string.IsNullOrWhiteSpace((string)node.Attribute("name")))
                    throw new InvalidDataException("Engineering node missing kind or name");
            }
            int roots = 0;
            foreach (XElement node in nodes)
            {
                Guid id = Guid.Parse((string)node.Attribute("id"));
                Guid parentId;
                string parentText = (string)node.Attribute("parentId");
                if (string.IsNullOrEmpty(parentText))
                {
                    if (id != projectId || (string)node.Attribute("kind") != "Project") 
                        throw new InvalidDataException("Invalid engineering root");
                    roots++;
                }
                else if (!Guid.TryParse(parentText, out parentId) || !index.ContainsKey(parentId))
                    throw new InvalidDataException("Engineering parent missing");
                foreach (XElement child in node.Elements("Child"))
                {
                    if (!Guid.TryParse((string)child.Attribute("id"), out Guid childId) ||
                        !index.TryGetValue(childId, out XElement descendant) ||
                        (string)descendant.Attribute("parentId") != id.ToString())
                        throw new InvalidDataException("Engineering ownership link mismatch");
                }
                if (parentText != null)
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
            return document;
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
