using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace CxVisual.Mechanism.Core
{
    /// <summary>简单版本化XML工程格式；不保存当前UI交互状态。</summary>
    public static class CxDocumentSerializer
    {
        public static void Save(CxProjectDocument document, string path)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required", "path");
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            XDocument xml = new XDocument(new XElement("CxVisualProject",
                new XAttribute("schema", 1), new XAttribute("revision", document.Revision), WriteNode(document.Project)));
            string temp = full + ".tmp";
            xml.Save(temp);
            // 同目录原子替换：Windows现有文件使用Replace；跨卷问题由同目录规避。
            if (File.Exists(full))
            {
                try { File.Replace(temp, full, null); }
                catch (PlatformNotSupportedException) { File.Delete(full); File.Move(temp, full); }
            }
            else File.Move(temp, full);
        }

        public static CxProjectDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required", "path");
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                XDocument xml = XDocument.Load(reader);
                XElement root = xml.Root;
                if (root == null || root.Name.LocalName != "CxVisualProject" || (string)root.Attribute("schema") != "1")
                    throw new InvalidDataException("Unsupported project schema");
                int revision = int.Parse((string)root.Attribute("revision"), CultureInfo.InvariantCulture);
                XElement main = root.Element("Node");
                if (main == null) throw new InvalidDataException("Missing project node");
                CxProject project = ReadNode(main) as CxProject;
                if (project == null) throw new InvalidDataException("Project root type required");
                return new CxProjectDocument(project, revision);
            }
        }

        private static XElement WriteNode(CxNode node)
        {
            XElement result = new XElement("Node", new XAttribute("id", node.Id),
                new XAttribute("kind", node.Kind), new XAttribute("name", node.Name));
            CxElement element = node as CxElement;
            if (element != null)
            {
                result.SetAttributeValue("x", element.X.ToString("R", CultureInfo.InvariantCulture));
                result.SetAttributeValue("y", element.Y.ToString("R", CultureInfo.InvariantCulture));
                result.SetAttributeValue("width", element.Width.ToString("R", CultureInfo.InvariantCulture));
                result.SetAttributeValue("height", element.Height.ToString("R", CultureInfo.InvariantCulture));
            }
            foreach (CxNode child in node.Children) result.Add(WriteNode(child));
            return result;
        }
        private static CxNode ReadNode(XElement data)
        {
            Guid id = Guid.Parse(Required(data, "id"));
            string name = Required(data, "name");
            CxNodeKind kind = (CxNodeKind)Enum.Parse(typeof(CxNodeKind), Required(data, "kind"), false);
            CxNode node;
            switch (kind)
            {
                case CxNodeKind.Project: node = new CxProject(id, name); break;
                case CxNodeKind.Page: node = new CxPage(id, name); break;
                case CxNodeKind.Content: node = new CxContent(id, name); break;
                case CxNodeKind.Symbol: node = new CxSymbol(id, name); break;
                case CxNodeKind.Group: node = new CxGroup(id, name); break;
                case CxNodeKind.Element:
                    node = new CxElement(id, name, Number(data, "x"), Number(data, "y"),
                        Number(data, "width"), Number(data, "height"));
                    break;
                default: throw new InvalidDataException("Unknown node kind");
            }
            foreach (XElement child in data.Elements("Node")) node.Attach(ReadNode(child));
            return node;
        }
        private static double Number(XElement element, string name)
        {
            return double.Parse(Required(element, name), CultureInfo.InvariantCulture);
        }
        private static string Required(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            if (attribute == null) throw new InvalidDataException("Missing attribute: " + name);
            return attribute.Value;
        }
    }
}
