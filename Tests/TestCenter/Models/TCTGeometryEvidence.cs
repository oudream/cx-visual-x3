using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CxVisual.TestCenter.Models
{
    /// <summary>Bounded read-only evidence schema for real geometry owner results.</summary>
    public sealed class TCTGeometryFigure
    {
        public string Kind { get; private set; }
        public string Role { get; private set; }
        public string Label { get; private set; }
        public string SourceObjectId { get; private set; }
        // Stable engineering address includes Symbol/Content reference instance path.
        public string DefinitionAddress { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public string DisplayName
        {
            get { return Role + " · " + Kind + " · " + Label +
                (string.IsNullOrWhiteSpace(DefinitionAddress) ? "" :
                    " · Instance: " + DefinitionAddress); }
        }
        public TCTGeometryFigure(string kind, string role, string label, string sourceObjectId,
            double x, double y, double width, double height, string definitionAddress = null)
        {
            Kind = kind; Role = role; Label = label ?? ""; SourceObjectId = sourceObjectId ?? "";
            DefinitionAddress = definitionAddress ?? "";
            X = x; Y = y; Width = width; Height = height;
            TCTGeometryEvidence.ValidateFigure(this);
        }
    }

    public static class TCTGeometryEvidence
    {
        public const string Mime = "application/vnd.cxvisual.geometry-evidence+xml";
        public const int Schema = 1;
        private const int MaxBytes = 1024 * 1024;
        private const int MaxFigures = 5000;

        internal static void ValidateFigure(TCTGeometryFigure f)
        {
            if (f == null ||
                !(f.Kind == "Rectangle" || f.Kind == "Point" || f.Kind == "Ellipse" || f.Kind == "Segment") ||
                !(f.Role == "Input" || f.Role == "Expected" || f.Role == "Actual" || f.Role == "Diff") ||
                !Finite(f.X) || !Finite(f.Y) || !Finite(f.Width) || !Finite(f.Height) ||
                (f.Kind != "Segment" && (f.Width < 0 || f.Height < 0)) ||
                f.Label.Length > 128 || f.SourceObjectId.Length > 128 ||
                f.DefinitionAddress.Length > 1024)
                throw new InvalidDataException("Invalid geometry figure, coordinate, role or kind");
        }
        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= 1e9;
        }
        public static byte[] Serialize(IEnumerable<TCTGeometryFigure> figures)
        {
            if (figures == null) throw new ArgumentNullException("figures");
            TCTGeometryFigure[] values = figures.Take(MaxFigures + 1).ToArray();
            if (values.Length == 0 || values.Length > MaxFigures)
                throw new InvalidDataException("Geometry evidence cardinality invalid");
            XElement root = new XElement("GeometryEvidence", new XAttribute("schema", Schema));
            foreach (TCTGeometryFigure f in values)
            {
                ValidateFigure(f);
                root.Add(new XElement("Figure", new XAttribute("kind", f.Kind),
                    new XAttribute("role", f.Role),
                    new XAttribute("label", f.Label), new XAttribute("owner", f.SourceObjectId),
                    new XAttribute("address", f.DefinitionAddress),
                    new XAttribute("x", N(f.X)), new XAttribute("y", N(f.Y)),
                    new XAttribute("w", N(f.Width)), new XAttribute("h", N(f.Height))));
            }
            byte[] bytes = Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            if (bytes.Length > MaxBytes) throw new InvalidDataException("Geometry evidence exceeds limit");
            return bytes;
        }
        public static IList<TCTGeometryFigure> Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes)
                throw new InvalidDataException("Geometry evidence is missing or oversized");
            XDocument doc;
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaxBytes
            };
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (XmlReader reader = XmlReader.Create(stream, settings))
                doc = XDocument.Load(reader);
            if (doc.Root == null || doc.Root.Name != "GeometryEvidence" ||
                (int?)doc.Root.Attribute("schema") != Schema)
                throw new InvalidDataException("Unsupported geometry evidence schema");
            XElement[] elements = doc.Root.Elements().ToArray();
            if (elements.Length == 0 || elements.Length > MaxFigures ||
                elements.Any(x => x.Name != "Figure"))
                throw new InvalidDataException("Geometry evidence figure count/element invalid");
            List<TCTGeometryFigure> list = new List<TCTGeometryFigure>();
            foreach (XElement element in elements)
            {
                double x, y, w, h;
                if (!double.TryParse((string)element.Attribute("x"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out x) ||
                    !double.TryParse((string)element.Attribute("y"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out y) ||
                    !double.TryParse((string)element.Attribute("w"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out w) ||
                    !double.TryParse((string)element.Attribute("h"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out h))
                    throw new InvalidDataException("Invalid geometry numeric field");
                list.Add(new TCTGeometryFigure((string)element.Attribute("kind"),
                    (string)element.Attribute("role"), (string)element.Attribute("label"),
                    (string)element.Attribute("owner"), x, y, w, h,
                    (string)element.Attribute("address")));
            }
            return list.AsReadOnly();
        }
        private static string N(double x)
        {
            return x.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
