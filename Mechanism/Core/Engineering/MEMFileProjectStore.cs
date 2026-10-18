using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Xml;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    /// <summary>H01 format 2: one ZIP/project.xml. Schema 1 XML remains an explicit compatibility path.</summary>
    public sealed partial class MEMFileProjectStore : IMEMProjectStore
    {
        public static readonly MEMFileProjectStore Default=new MEMFileProjectStore();
        public void WritePackage(Stream output,MEMSaveSnapshot snapshot,CancellationToken cancellation)
        {
            if(output==null||!output.CanWrite)throw new ArgumentException("Writable stream required");if(snapshot==null)throw new ArgumentNullException("snapshot");
            using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
            {
                using(var content=zip.CreateEntry("project.xml",CompressionLevel.Fastest).Open())using(var xml=snapshot.OpenRead())
                {var buffer=new byte[81920];int read;while((read=xml.Read(buffer,0,buffer.Length))!=0){cancellation.ThrowIfCancellationRequested();content.Write(buffer,0,read);}}
                WriteAssetBytes(zip,snapshot,cancellation);
            }
        }
        public MEMEngineeringSession ReadPackage(Stream input)
        {
            if(input==null||!input.CanRead)throw new ArgumentException("Readable stream required");
            using(var zip=new ZipArchive(input,ZipArchiveMode.Read,true))return ReadArchive(zip);
        }
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly HashSet<string> ProjectFields = new HashSet<string>(new[] { "schema", "revision" }, StringComparer.Ordinal);
        private static readonly HashSet<string> NodeFields = new HashSet<string>(new[] { "id", "kind", "name", "typeId", "legacy" }, StringComparer.Ordinal);
        private static readonly HashSet<string> VisualFields = new HashSet<string>(new[] { "id", "kind", "name", "typeId", "legacy", "x", "y", "width", "height", "rotation", "scaleX", "scaleY", "shear", "visible", "selectable", "locked", "layer" }, StringComparer.Ordinal);
        private static readonly HashSet<string> ReferenceFields = new HashSet<string>(new[] { "id", "name", "kind", "target" }, StringComparer.Ordinal);
        private static void CheckFields(XmlReader reader, HashSet<string> allowed)
        {
            if (reader.HasAttributes)
                while (reader.MoveToNextAttribute())
                    if (reader.NamespaceURI.Length != 0 || !allowed.Contains(reader.Name)) throw new InvalidDataException("Unsupported engineering attribute " + reader.Name);
            reader.MoveToElement();
        }
        internal static MEMSaveSnapshot Capture(MEMEngineeringSession document,CancellationToken token)
        {
            if(!document.Validate().IsValid)throw new InvalidDataException("Invalid engineering tree");
            using(var xml=new MemoryStream()){Write(xml,document,2,token);if(xml.Length>512L*1024*1024)throw new InvalidDataException("Engineering snapshot budget exceeded");return new MEMSaveSnapshot(document.Project.Id,document.Revision,xml.ToArray(),document.Assets);}
        }
        private static string Hash(Stream stream)
        {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static string FileToken(string path){using(var file=File.OpenRead(path))return Hash(file);}
        internal static string ResourceHash(MEMNode root)
        {
            using(var bytes=new MemoryStream())
            {
                using(var writer=XmlWriter.Create(bytes,new XmlWriterSettings{Encoding=new System.Text.UTF8Encoding(false),CloseOutput=false}))
                {
                    var pending=new Stack<Frame>();pending.Push(new Frame(root,-1));
                    while(pending.Count!=0){var frame=pending.Pop();if(frame.Child==-1){WriteNodeStart(writer,frame.Node);frame.Child=0;}if(frame.Child<frame.Node.Children.Count){var child=frame.Node.Children[frame.Child++];pending.Push(frame);pending.Push(new Frame(child,-1));}else writer.WriteEndElement();}
                }
                bytes.Position=0;return Hash(bytes);
            }
        }
        public MEMSaveResult Save(MEMSaveSnapshot snapshot, string path,string expectedToken,CancellationToken token)
        {
            if(snapshot==null)throw new ArgumentNullException("snapshot");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path required", "path");
            token.ThrowIfCancellationRequested();
            string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full);
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, ".cx-save-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    WritePackage(file,snapshot,token);
                    file.Flush(true);
                }
                // Verify staged bytes before publication. No second live engineering document is built.
                using (var file = File.OpenRead(temp))
                {
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read, true)) using (var content = zip.GetEntry("project.xml").Open()) ValidateXmlBytes(content, token);
                }
                token.ThrowIfCancellationRequested();
                // A first-save race must fail rather than replace a file another writer just created.
                string nextToken=FileToken(temp);
                // Cooperative cross-process writer lease. It is metadata, never an engineering object.
                using(var lease=new FileStream(full+".cx-lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                {
                    bool replacing=File.Exists(full);
                    if(expectedToken==null?replacing:(!replacing||FileToken(full)!=expectedToken))throw new MEMStoreConflictException(full);
                    token.ThrowIfCancellationRequested();
                    if (replacing) File.Replace(temp, full, null); else File.Move(temp, full);
                }
                return new MEMSaveResult(snapshot.ProjectId,snapshot.Revision,full,nextToken);
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
        private static void Write(Stream stream, MEMEngineeringSession document, int schema, CancellationToken token)
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
            var project=node as MEMProject;if(project!=null){WriteTypes(w,project);WriteAssetManifest(w,project);}
            
            var visual = node as MRCVisualItem;
            if (visual != null)
            {
                Number(w, "x", visual.X); Number(w, "y", visual.Y); Number(w, "width", visual.Width); Number(w, "height", visual.Height);
                Number(w, "rotation", visual.Rotation); Number(w, "scaleX", visual.ScaleX); Number(w, "scaleY", visual.ScaleY);
                Number(w,"shear",visual.Shear);w.WriteAttributeString("locked",XmlConvert.ToString(visual.IsLocked));w.WriteAttributeString("layer",visual.LayerId);
                w.WriteAttributeString("visible", XmlConvert.ToString(visual.IsVisible)); w.WriteAttributeString("selectable", XmlConvert.ToString(visual.IsSelectable));
                if (visual.BindingItems != null) foreach (var pair in visual.BindingItems)
                { w.WriteStartElement("Binding"); w.WriteAttributeString("property", pair.Key); WriteBinding(w,pair.Value); w.WriteEndElement(); }
            }
            WriteDeclarations(w,node);
            foreach (var reference in MRIReferenceList.View(node))
            {
                WriteReference(w,reference);
            }
        }
        private static void Number(XmlWriter w, string name, double value) { w.WriteAttributeString(name, value.ToString("R", Invariant)); }
        public MEMEngineeringSession Load(string path)
        {
            using (var file = File.OpenRead(path))
            {
                int first = file.ReadByte(), second = file.ReadByte(); file.Position = 0;
                MEMEngineeringSession document;
                if (first == 'P' && second == 'K')
                {
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read, true))
                    {
                        document=ReadArchive(zip);
                    }
                }
                else document = Read(file);
                file.Position=0;document.MarkLoaded(path,Hash(file));document.UseStore(this); return document;
            }
        }
        private static MEMEngineeringSession ReadArchive(ZipArchive zip)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);long expanded=0;
            foreach(var entry in zip.Entries)
            {
                if(!names.Add(entry.FullName)||(entry.FullName!="project.xml"&&(!entry.FullName.StartsWith("assets/",StringComparison.Ordinal)||entry.FullName.Length!=39||entry.FullName.Substring(7).Any(c=>!"0123456789abcdef".Contains(c))))||entry.Length>512L*1024*1024)throw new InvalidDataException("Invalid engineering package entry");
                expanded+=entry.Length;if(expanded>768L*1024*1024||names.Count>4097)throw new InvalidDataException("Engineering package budget exceeded");
            }
            var definition=zip.GetEntry("project.xml");if(definition==null)throw new InvalidDataException("Project definition missing");MEMEngineeringSession document;
            using(var content=definition.Open())document=Read(content,zip);
            if(zip.Entries.Count!=document.Assets.Count+1)throw new InvalidDataException("Unmanifested asset entries");return document;
        }
        private static string Required(XmlReader r, string name) { var value = r.GetAttribute(name); if (value == null) throw new InvalidDataException("Missing " + name); return value; }
        private static double Number(XmlReader r, string name, double fallback) { var value = r.GetAttribute(name); return value == null ? fallback : double.Parse(value, Invariant); }
        private static MEMEngineeringSession Read(Stream stream,ZipArchive package=null)
        {
            using (var r = Reader(stream))
            {
                r.MoveToContent(); if (r.Name != "CxVisualProject") throw new InvalidDataException("Missing project");
                CheckFields(r, ProjectFields);
                string schema = Required(r, "schema"); if (schema != "1" && schema != "2") throw new InvalidDataException("Unsupported schema");
                int revision = int.Parse(Required(r, "revision"), Invariant); var stack = new Stack<MEMNode>();
                MEMProject project = null; var references = new List<MRIResourceReference>(); var declarations=new Dictionary<Guid,MPMDefinitionState>(); int count = 0;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.EndElement && r.Name == "Node") { if (stack.Count == 0) throw new InvalidDataException("Invalid node end"); stack.Pop(); continue; }
                    if ((r.NodeType == XmlNodeType.Text || r.NodeType == XmlNodeType.CDATA) && !string.IsNullOrWhiteSpace(r.Value)) throw new InvalidDataException("Unexpected engineering text");
                    if (r.NodeType != XmlNodeType.Element) continue;
                    if (r.Name == "Node")
                    {
                        if (++count > MEMNode.MaximumNodeCount || stack.Count >= MEMNode.MaximumDepth) throw new InvalidDataException("Engineering structure budget exceeded");
                        bool legacy = schema == "1" || r.GetAttribute("legacy") == "true";
                        Guid id = Guid.Parse(Required(r, "id")); string name = Required(r, "name"), kind = Required(r, "kind"); MEMNode node;
                        switch (kind)
                        {
                            case "Project": node = new MEMProject(id, name); break;
                            case "Page": node = new MEMPage(id, name, false); break;
                            case "Content": node = new MEMContent(id, name); break;
                            case "Symbol": node = new MEMSymbol(id, name, false); break;
                            case "Group": node = new MRCGroup(id, name); break;
                            case "Element": node = new MRCRectangle(id,name,Number(r,"x",0),Number(r,"y",0),Number(r,"width",120),Number(r,"height",80)); break;
                            default: throw new InvalidDataException("Unknown kind " + kind);
                        }
                        var declaredType = r.GetAttribute("typeId"); if (declaredType != null && declaredType != node.TypeId)
                        {
                            MPMTypeDescriptor descriptor;
                            if(project==null||project.TypeItems==null||!project.TypeItems.TryGetValue(declaredType,out descriptor)||descriptor.Kind!=node.Kind)throw new InvalidDataException("Unregistered/mismatched TypeId");
                            node.SetTypeId(declaredType);
                        }
                        var visual = node as MRCVisualItem;
                        CheckFields(r, visual == null ? NodeFields : VisualFields);
                        if (visual != null)
                        {
                            visual.SetGeometry(Number(r,"x",0), Number(r,"y",0), Number(r,"width",120), Number(r,"height",80), Number(r,"rotation",0), Number(r,"scaleX",1), Number(r,"scaleY",1),Number(r,"shear",0));
                            visual.IsLocked=r.GetAttribute("locked")!=null&&XmlConvert.ToBoolean(r.GetAttribute("locked"));
                            string layer=r.GetAttribute("layer")??"";visual.LayerId=layer==""?"":MEMNode.NormalizeName(layer);
                            visual.IsVisible = r.GetAttribute("visible") == null || XmlConvert.ToBoolean(r.GetAttribute("visible"));
                            visual.IsSelectable = r.GetAttribute("selectable") == null || XmlConvert.ToBoolean(r.GetAttribute("selectable"));
                        }
                        if (stack.Count == 0) { if (project != null || !(node is MEMProject)) throw new InvalidDataException("Exactly one project required"); project = (MEMProject)node; }
                        else if (legacy && node is MEMSymbol && stack.Peek() is MEMContent)
                        {
                            // Legacy Content-owned symbols become a shared definition plus an explicit use record.
                            var owner = (MEMContent)stack.Peek();
                            if (project == null) throw new InvalidDataException("Legacy symbol without project");
                            project.InsertOwned(project.Children.Count, node);
                            references.Add(new MRIResourceReference(Guid.NewGuid(), name, id, MRIReferenceKind.Symbol, owner));
                        }
                        else stack.Peek().InsertOwned(stack.Peek().Children.Count, node);
                        if (!r.IsEmptyElement) stack.Push(node);
                    }
                    else if(r.Name=="Type")
                    {
                        if(stack.Count!=1||stack.Peek()!=project||project.Children.Count!=0)throw new InvalidDataException("Type schemas must precede project resources");
                        var type=ReadType(r);if(project.TypeItems==null)project.TypeItems=new Dictionary<string,MPMTypeDescriptor>(StringComparer.Ordinal);if(project.TypeItems.Count>=4096)throw new InvalidDataException("Type registry budget exceeded");project.TypeItems.Add(type.TypeId,type);
                    }
                    else if(r.Name=="Asset")
                    {
                        if(stack.Count!=1||stack.Peek()!=project||project.Children.Count!=0)throw new InvalidDataException("Asset manifest must precede project resources");ReadAsset(r,project,package);
                    }
                    else if (r.Name == "Reference")
                    {
                        var owner = stack.Count == 0 ? null : stack.Peek();
                        if (!(owner is MEMContent||owner is MEMSymbol)) throw new InvalidDataException("Invalid reference");
                        references.Add(ReadReference(r,owner));
                    }
                    else if (r.Name == "Binding")
                    {
                        var visual = stack.Count == 0 ? null : stack.Peek() as MRCVisualItem;
                        if (visual == null) throw new InvalidDataException("Invalid binding");
                        if (visual.BindingItems == null) visual.BindingItems = new Dictionary<string,MPMBinding>(StringComparer.Ordinal);
                        var binding=ReadSmallElement(r);visual.BindingItems.Add(MEMNode.NormalizeName(XRequired(binding,"property")),ReadBinding(binding,true));
                    }
                    else if(TryReadDeclaration(r,stack.Count==0?null:stack.Peek(),declarations)) { }
                    else throw new InvalidDataException("Unknown engineering field " + r.Name);
                }
                if (project == null || stack.Count != 0) throw new InvalidDataException("Incomplete project");
                var document = new MEMEngineeringSession(project, revision);
                foreach (var reference in references)
                {
                    var target = document.Find(reference.TargetId);
                    if ((reference.Kind == MRIReferenceKind.Symbol) != (target is MEMSymbol)) throw new InvalidDataException("Reference target kind mismatch");
                    var overrides=reference.OverrideItems;reference.OverrideItems=null;
                    document.CheckReference(reference.Owner, target); document.IndexReference(reference);reference.OverrideItems=overrides;
                    MRIReferenceList.Mutable(reference.Owner,true).Add(reference);
                }
                document.AttachLoadedDeclarations(declarations); if(!document.Validate().IsValid) throw new InvalidDataException("Invalid engineering tree"); return document;
            }
        }
    }
}
