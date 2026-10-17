using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Xml;

namespace CxVisual.Mechanism.Core
{
    /// <summary>H01 format 2: one ZIP/project.xml. Schema 1 XML remains an explicit compatibility path.</summary>
    internal static class MEMFileProjectStore
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        internal static void Save(CxProjectDocument document, string path, CancellationToken token, bool legacy = false)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path required", "path");
            document.ValidateTree(); token.ThrowIfCancellationRequested();
            string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full);
            if (!legacy && File.Exists(full) && !string.Equals(full, document.Session.Location, StringComparison.OrdinalIgnoreCase)) throw new IOException("SaveAs target already exists");
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, ".cx-save-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (legacy) Write(file, document, 1, token);
                    else using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
                        using (var content = zip.CreateEntry("project.xml", CompressionLevel.Fastest).Open()) Write(content, document, 2, token);
                    file.Flush(true);
                }
                // Verify staged bytes before publication. No second live engineering document is built.
                using (var file = File.OpenRead(temp))
                {
                    if (legacy) ValidateXmlBytes(file, token);
                    else using (var zip = new ZipArchive(file, ZipArchiveMode.Read, true)) using (var content = zip.GetEntry("project.xml").Open()) ValidateXmlBytes(content, token);
                }
                token.ThrowIfCancellationRequested();
                if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
                // Cancellation after this publication point cannot claim that the saved file was not written.
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private static XmlReader Reader(Stream stream)
        { return XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 512L * 1024 * 1024, CloseInput = false }); }
        private static void ValidateXmlBytes(Stream stream, CancellationToken token)
        { using (var reader = Reader(stream)) while (reader.Read()) token.ThrowIfCancellationRequested(); }
        private struct Frame
        {
            internal MEMNode Node; internal int Child;
            internal Frame(MEMNode node, int child) { Node = node; Child = child; }
        }
        private static void Write(Stream stream, CxProjectDocument document, int schema, CancellationToken token)
        {
            using (var w = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), CloseOutput = false }))
            {
                w.WriteStartElement("CxVisualProject"); w.WriteAttributeString("schema", schema.ToString(Invariant)); w.WriteAttributeString("revision", document.Revision.ToString(Invariant));
                var stack = new Stack<Frame>(); stack.Push(new Frame(document.Project, -1));
                while (stack.Count != 0)
                {
                    token.ThrowIfCancellationRequested(); var f = stack.Pop();
                    if (f.Child == -1) { WriteNodeStart(w, f.Node); f.Child = 0; }
                    if (f.Child < f.Node.Children.Count)
                    { var child = f.Node.Children[f.Child++]; stack.Push(f); stack.Push(new Frame(child, -1)); }
                    else w.WriteEndElement();
                }
                w.WriteEndElement();
            }
        }
        private static void WriteNodeStart(XmlWriter w, MEMNode node)
        {
            w.WriteStartElement("Node"); w.WriteAttributeString("id", node.Id.ToString("D")); w.WriteAttributeString("kind", node.Kind.ToString());
            w.WriteAttributeString("name", node.Name); w.WriteAttributeString("typeId", node.TypeId);
            if (node is CxPage || node is CxContent || node is CxSymbol || node is CxGroup || node is CxElement) w.WriteAttributeString("legacy", "true");
            var visual = node as MRCVisualItem;
            if (visual != null)
            {
                Number(w, "x", visual.X); Number(w, "y", visual.Y); Number(w, "width", visual.Width); Number(w, "height", visual.Height);
                Number(w, "rotation", visual.Rotation); Number(w, "scaleX", visual.ScaleX); Number(w, "scaleY", visual.ScaleY);
                w.WriteAttributeString("visible", XmlConvert.ToString(visual.IsVisible)); w.WriteAttributeString("selectable", XmlConvert.ToString(visual.IsSelectable));
                if (visual.BindingItems != null) foreach (var pair in visual.BindingItems)
                { w.WriteStartElement("Binding"); w.WriteAttributeString("property", pair.Key); w.WriteAttributeString("source", pair.Value.SourceId); w.WriteAttributeString("member", pair.Value.MemberId); w.WriteEndElement(); }
            }
            var content = node as MEMContent;
            if (content != null) foreach (var reference in content.References)
            {
                w.WriteStartElement("Reference"); w.WriteAttributeString("id", reference.Id.ToString("D")); w.WriteAttributeString("name", reference.Name);
                w.WriteAttributeString("kind", reference.Kind.ToString()); w.WriteAttributeString("target", reference.TargetId.ToString("D")); w.WriteEndElement();
            }
        }
        private static void Number(XmlWriter w, string name, double value) { w.WriteAttributeString(name, value.ToString("R", Invariant)); }
        internal static CxProjectDocument Load(string path)
        {
            using (var file = File.OpenRead(path))
            {
                int first = file.ReadByte(), second = file.ReadByte(); file.Position = 0;
                CxProjectDocument document;
                if (first == 'P' && second == 'K')
                {
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read, true))
                    {
                        if (zip.Entries.Count != 1 || zip.Entries[0].FullName != "project.xml" || zip.Entries[0].Length > 512L * 1024 * 1024) throw new InvalidDataException("Unsupported/oversized H01 package");
                        using (var content = zip.Entries[0].Open()) document = Read(content);
                    }
                }
                else document = Read(file);
                document.Session.MarkLoaded(path); return document;
            }
        }
        private static string Required(XmlReader r, string name) { var value = r.GetAttribute(name); if (value == null) throw new InvalidDataException("Missing " + name); return value; }
        private static double Number(XmlReader r, string name, double fallback) { var value = r.GetAttribute(name); return value == null ? fallback : double.Parse(value, Invariant); }
        private static CxProjectDocument Read(Stream stream)
        {
            using (var r = Reader(stream))
            {
                r.MoveToContent(); if (r.Name != "CxVisualProject") throw new InvalidDataException("Missing project");
                string schema = Required(r, "schema"); if (schema != "1" && schema != "2") throw new InvalidDataException("Unsupported schema");
                int revision = int.Parse(Required(r, "revision"), Invariant); var stack = new Stack<MEMNode>();
                CxProject project = null; var references = new List<MRIResourceReference>(); int count = 0;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.EndElement && r.Name == "Node") { if (stack.Count == 0) throw new InvalidDataException("Invalid node end"); stack.Pop(); continue; }
                    if (r.NodeType != XmlNodeType.Element) continue;
                    if (r.Name == "Node")
                    {
                        if (++count > 2000000 || stack.Count > 256) throw new InvalidDataException("Engineering structure budget exceeded");
                        bool legacy = schema == "1" || r.GetAttribute("legacy") == "true";
                        Guid id = Guid.Parse(Required(r, "id")); string name = Required(r, "name"), kind = Required(r, "kind"); MEMNode node;
                        switch (kind)
                        {
                            case "Project": node = new CxProject(id, name); break;
                            case "Page": node = legacy ? (MEMNode)new CxPage(id, name) : new MEMPage(id, name, false); break;
                            case "Content": node = legacy ? (MEMNode)new CxContent(id, name) : new MEMContent(id, name); break;
                            case "Symbol": node = legacy ? (MEMNode)new CxSymbol(id, name) : new MEMSymbol(id, name, false); break;
                            case "Group": node = legacy ? (MEMNode)new CxGroup(id, name) : new MRCGroup(id, name); break;
                            case "Element": node = legacy ? (MEMNode)new CxElement(id, name, Number(r,"x",0),Number(r,"y",0),Number(r,"width",120),Number(r,"height",80)) : new MRCRectangle(id,name,Number(r,"x",0),Number(r,"y",0),Number(r,"width",120),Number(r,"height",80)); break;
                            default: throw new InvalidDataException("Unknown kind " + kind);
                        }
                        var declaredType = r.GetAttribute("typeId"); if (declaredType != null && declaredType != node.TypeId) throw new InvalidDataException("Unsupported TypeId");
                        var visual = node as MRCVisualItem;
                        if (visual != null)
                        {
                            visual.SetGeometry(Number(r,"x",0), Number(r,"y",0), Number(r,"width",120), Number(r,"height",80), Number(r,"rotation",0), Number(r,"scaleX",1), Number(r,"scaleY",1));
                            visual.IsVisible = r.GetAttribute("visible") == null || XmlConvert.ToBoolean(r.GetAttribute("visible"));
                            visual.IsSelectable = r.GetAttribute("selectable") == null || XmlConvert.ToBoolean(r.GetAttribute("selectable"));
                        }
                        if (stack.Count == 0) { if (project != null || !(node is CxProject)) throw new InvalidDataException("Exactly one project required"); project = (CxProject)node; }
                        else stack.Peek().InsertOwned(stack.Peek().Children.Count, node);
                        if (!r.IsEmptyElement) stack.Push(node);
                    }
                    else if (r.Name == "Reference")
                    {
                        var owner = stack.Count == 0 ? null : stack.Peek() as MEMContent;
                        if (owner == null || !r.IsEmptyElement) throw new InvalidDataException("Invalid reference");
                        MRIReferenceKind kind;
                        if (!Enum.TryParse(Required(r,"kind"), out kind) || (kind != MRIReferenceKind.Content && kind != MRIReferenceKind.Symbol)) throw new InvalidDataException("Unknown reference kind");
                        var id = Guid.Parse(Required(r,"id")); if (id == Guid.Empty) throw new InvalidDataException("Empty reference identity");
                        references.Add(new MRIResourceReference(id,Required(r,"name"),Guid.Parse(Required(r,"target")),kind,owner));
                    }
                    else if (r.Name == "Binding")
                    {
                        var visual = stack.Count == 0 ? null : stack.Peek() as MRCVisualItem;
                        if (visual == null || !r.IsEmptyElement) throw new InvalidDataException("Invalid binding");
                        if (visual.BindingItems == null) visual.BindingItems = new Dictionary<string,MPMBinding>(StringComparer.Ordinal);
                        visual.BindingItems.Add(Required(r,"property"),new MPMBinding(Required(r,"source"),Required(r,"member")));
                    }
                    else throw new InvalidDataException("Unknown engineering field " + r.Name);
                }
                if (project == null || stack.Count != 0) throw new InvalidDataException("Incomplete project");
                var document = new CxProjectDocument(project, revision);
                foreach (var reference in references)
                {
                    var target = document.Session.Find(reference.TargetId);
                    if ((reference.Kind == MRIReferenceKind.Symbol) != (target is MEMSymbol)) throw new InvalidDataException("Reference target kind mismatch");
                    document.Session.CheckReference(reference.Owner, target); document.Session.IndexReference(reference);
                    if (reference.Owner.ReferenceItems == null) reference.Owner.ReferenceItems = new List<MRIResourceReference>();
                    reference.Owner.ReferenceItems.Add(reference);
                }
                document.ValidateTree(); return document;
            }
        }
    }
}
