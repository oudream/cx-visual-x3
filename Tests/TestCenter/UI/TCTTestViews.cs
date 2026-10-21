using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Fixtures;
using CxVisual.Mechanism.Core;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.UI
{
    public interface ITCTTestViewer
    {
        string Kind { get; }
        bool CanDisplay(TCTTestCaseResult result);
        void Bind(TCTTestCaseResult result);
        void Clear();
    }

    // Viewer从Result/Observation只读呈现，不参与Pass/Fail计算。
    public abstract class TCTTestViewBase : UserControl, ITCTTestViewer
    {
        protected readonly StackPanel ContentPanel = new StackPanel { Margin = new Thickness(15) };
        protected TCTTestViewBase()
        {
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = ContentPanel };
            Content = scroll;
        }
        // 仅传递真实Observation的选择，不参与测试状态和断言计算。
        public event Action<TCTTestObservation> ObservationSelected;
        protected void SelectObservation(TCTTestObservation observation)
        {
            if (observation != null && ObservationSelected != null)
                ObservationSelected(observation);
        }
        public abstract string Kind { get; }
        public virtual bool CanDisplay(TCTTestCaseResult result)
        {
            return result != null && result.Observations.Any(o =>
                string.Equals(o.Kind, Kind, StringComparison.OrdinalIgnoreCase));
        }
        public virtual void Clear() { ContentPanel.Children.Clear(); }
        public abstract void Bind(TCTTestCaseResult result);

        protected void Header(string title, string subtitle)
        {
            ContentPanel.Children.Add(new TextBlock { Text = title, FontSize = 18,
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue,
                Margin = new Thickness(0, 0, 0, 8) });
            ContentPanel.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14), Foreground = Brushes.DimGray });
        }
        protected void Observations(TCTTestCaseResult result, string kind)
        {
            foreach (TCTTestObservation observation in result.Observations.Where(o =>
                string.Equals(o.Kind, kind, StringComparison.OrdinalIgnoreCase)))
            {
                Button select = new Button {
                    Content = "[" + observation.Origin + "] " + observation.Title + "  ·  查看证据",
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 5),
                    Padding = new Thickness(6, 4, 6, 4)
                };
                select.Click += (sender, args) => SelectObservation(observation);
                ContentPanel.Children.Add(select);
                ContentPanel.Children.Add(new TextBlock { Text = observation.Details,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 9) });
            }
        }
        protected void Assertions(TCTTestCaseResult result)
        {
            foreach (TCTAssertionResult assertion in result.Assertions)
            {
                ContentPanel.Children.Add(new TextBlock {
                    Text = (assertion.Passed ? "✓ " : "✗ ") + assertion.Name +
                        "   expected=" + assertion.Expected + "   actual=" + assertion.Actual,
                    Foreground = assertion.Passed ? Brushes.DarkGreen : Brushes.DarkRed,
                    Margin = new Thickness(0, 3, 0, 3), TextWrapping = TextWrapping.Wrap });
            }
        }
    }

    public sealed class TCTGeometryTestView : TCTTestViewBase
    {
        public event Action<string, string> GeometrySelected;
        public int VerifiedFigureCount { get; private set; }
        public string EvidenceProblem { get; private set; }
        public string ArchivedEvidenceRoot { get; set; }
        public TCTGeometryEvidenceCanvas EvidenceCanvas { get; private set; }
        public override void Clear()
        {
            base.Clear(); VerifiedFigureCount = 0; EvidenceProblem = null; EvidenceCanvas = null;
        }
        public override string Kind { get { return "Geometry"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear();
            Header("Geometry / Expected vs Actual", "以下几何与数值均来自实际断言或真实观察结果。");
            Canvas canvas = new Canvas { Width = 520, Height = 235,
                Background = Brushes.AliceBlue, ClipToBounds = true };
            Line x = new Line { X1 = 20, Y1 = 195, X2 = 490, Y2 = 195,
                Stroke = Brushes.Gray, StrokeThickness = 1.2 };
            Line y = new Line { X1 = 35, Y1 = 20, X2 = 35, Y2 = 215,
                Stroke = Brushes.Gray, StrokeThickness = 1.2 };
            canvas.Children.Add(x); canvas.Children.Add(y);
            // 只有存在真实数值断言时才能画点；其它Geometry样例保留文本和断言。
            TCTAssertionResult ax = result.Assertions.FirstOrDefault(a => a.Name.Contains("变换X"));
            TCTAssertionResult ay = result.Assertions.FirstOrDefault(a => a.Name.Contains("变换Y"));
            double ex, ey, actualX, actualY;
            if (ax != null && ay != null &&
                double.TryParse(ax.Expected, NumberStyles.Float, CultureInfo.InvariantCulture, out ex) &&
                double.TryParse(ay.Expected, NumberStyles.Float, CultureInfo.InvariantCulture, out ey) &&
                double.TryParse(ax.Actual, NumberStyles.Float, CultureInfo.InvariantCulture, out actualX) &&
                double.TryParse(ay.Actual, NumberStyles.Float, CultureInfo.InvariantCulture, out actualY))
            {
                double maxX = Math.Max(Math.Abs(ex), Math.Abs(actualX)) + 5;
                double maxY = Math.Max(Math.Abs(ey), Math.Abs(actualY)) + 5;
                DrawPoint(canvas, 35 + ex / maxX * 430, 195 - ey / maxY * 150,
                    Brushes.ForestGreen, "Expected");
                DrawPoint(canvas, 35 + actualX / maxX * 430, 195 - actualY / maxY * 150,
                    Brushes.Crimson, "Actual");
            }
            else
            {
                TextBlock hint = new TextBlock { Text = "该用例未提供二维数值坐标：不生成模拟图形",
                    Foreground = Brushes.DimGray };
                Canvas.SetLeft(hint, 75); Canvas.SetTop(hint, 100); canvas.Children.Add(hint);
            }
            ContentPanel.Children.Add(canvas);
            foreach (TCTTestObservation observation in result.Observations.Where(o =>
                string.Equals(o.Kind, Kind, StringComparison.OrdinalIgnoreCase) &&
                o.Envelope != null && o.Envelope.PayloadRef != null &&
                o.Envelope.PayloadRef.Mime == TCTGeometryEvidence.Mime))
            {
                try
                {
                    TCTTestArtifactStore store = new TCTTestArtifactStore(
                        ArchivedEvidenceRoot ?? TCTTestArtifactStore.RunRoot(result.RunId));
                    TCTObservationReplay.Read(result, store);
                    IList<TCTGeometryFigure> figures = TCTGeometryEvidence.Read(
                        store.Read(observation.Envelope.PayloadRef));
                    TCTGeometryEvidenceCanvas proof = new TCTGeometryEvidenceCanvas();
                    proof.ShowFigures(figures);
                    proof.FigureSelected += figure =>
                    {
                        SelectObservation(observation);
                        if (GeometrySelected != null)
                            GeometrySelected(figure.SourceObjectId,
                                "角色: " + figure.Role + "\n类型: " + figure.Kind +
                                "\n名称: " + figure.Label + "\nOwner: " + figure.SourceObjectId +
                                "\nDefinition/InstanceAddress: " +
                                    (string.IsNullOrWhiteSpace(figure.DefinitionAddress) ?
                                        "未提供" : figure.DefinitionAddress) +
                                "\n坐标系: " + figure.CoordinateSpace +
                                "\n单位: " + figure.Unit +
                                "\n坐标: " + figure.X + ", " + figure.Y +
                                "\n尺寸/向量: " + figure.Width + ", " + figure.Height);
                    };
                    EvidenceCanvas = proof;
                    VerifiedFigureCount += proof.RenderedFigureCount;
                    ContentPanel.Children.Add(new TextBlock { Text = "SHA256已验证 · " +
                        observation.Title + " · " + proof.RenderedFigureCount + " 项真实几何",
                        FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 5) });
                    ContentPanel.Children.Add(proof);
                }
                catch (Exception geometryError) when (geometryError is InvalidDataException ||
                    geometryError is IOException || geometryError is UnauthorizedAccessException ||
                    geometryError is System.Xml.XmlException || geometryError is NotSupportedException)
                {
                    EvidenceProblem = geometryError.Message;
                    ContentPanel.Children.Add(new TextBlock { Text = "Geometry证据不可验证：" +
                        geometryError.Message, Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap });
                }
            }
            Observations(result, Kind); Assertions(result);
        }
        private static void DrawPoint(Canvas canvas, double x, double y, Brush color, string label)
        {
            Ellipse dot = new Ellipse { Width = 10, Height = 10, Fill = color };
            Canvas.SetLeft(dot, x - 5); Canvas.SetTop(dot, y - 5); canvas.Children.Add(dot);
            TextBlock text = new TextBlock { Text = label, Foreground = color, FontSize = 11 };
            Canvas.SetLeft(text, x + 8); Canvas.SetTop(text, y - 8); canvas.Children.Add(text);
        }
    }

    // Renders only SHA256-verified MEMProjectSnapshot evidence. No editable or guessed tree.
    public sealed class TCTObjectTestView : TCTTestViewBase
    {
        public override string Kind { get { return "ObjectTree"; } }
        public event Action<string, string> ObjectSelected;
        public int VerifiedNodeCount { get; private set; }
        public int VerifiedReferenceCount { get; private set; }
        public int VerifiedDiffCount { get; private set; }
        public string EvidenceProblem { get; private set; }
        public string ArchivedEvidenceRoot { get; set; }
        public TreeView OwnershipTree { get; private set; }
        public TCTObjectReferenceDiagram RelationDiagram { get; private set; }

        public override void Clear()
        {
            base.Clear();
            VerifiedNodeCount = 0;
            VerifiedReferenceCount = 0;
            VerifiedDiffCount = 0;
            EvidenceProblem = null;
            OwnershipTree = null;
            RelationDiagram = null;
        }

        public override void Bind(TCTTestCaseResult result)
        {
            Clear();
            Header("Engineering · Ownership / Reference / Definition",
                "真实 MEMProjectSnapshot 只读快照 · Ownership 实线层级，Reference 独立引用，不复制 Symbol 定义。");
            TCTTestObservation[] observations = result.Observations.Where(o =>
                string.Equals(o.Kind, Kind, StringComparison.OrdinalIgnoreCase)).ToArray();
            byte[] beforeBytes = null, afterBytes = null;
            foreach (TCTTestObservation observation in observations)
            {
                if (observation.Envelope == null || observation.Envelope.PayloadRef == null ||
                    observation.Envelope.PayloadRef.Mime != TCTEngineeringSnapshotCodec.Mime)
                {
                    ContentPanel.Children.Add(new TextBlock
                    {
                        Text = "旧文本观察（没有经过验证的工程快照，不能生成对象树）：" +
                            observation.Title + " · " + observation.Details,
                        Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 5, 0, 9)
                    });
                    continue;
                }
                try
                {
                    // Validates Evidence envelope, Attempt/Run, Payload inventory and SHA256 first.
                    TCTTestArtifactStore store = new TCTTestArtifactStore(
                        ArchivedEvidenceRoot ?? TCTTestArtifactStore.RunRoot(result.RunId));
                    TCTObservationReplay.Read(result, store);
                    byte[] bytes = store.Read(observation.Envelope.PayloadRef);
                    XDocument snapshot = TCTEngineeringSnapshotCodec.Deserialize(bytes);
                    BindSnapshot(snapshot, observation);
                    if (observation.Origin == "Before") beforeBytes = bytes;
                    else if (observation.Origin == "After") afterBytes = bytes;
                }
                catch (Exception ex) when (ex is InvalidDataException ||
                    ex is IOException || ex is UnauthorizedAccessException ||
                    ex is NotSupportedException || ex is System.Xml.XmlException)
                {
                    EvidenceProblem = ex.GetType().Name + ": " + ex.Message;
                    ContentPanel.Children.Add(new TextBlock
                    {
                        Text = "证据不可用 / 不可信： " + EvidenceProblem +
                            "。对象树不显示；不会使用文字合成对象或冒充通过。",
                        Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 8, 0, 8)
                    });
                }
            }
            if (beforeBytes != null && afterBytes != null)
            {
                try
                {
                IList<string> changes = TCTEngineeringSnapshotCodec.Compare(beforeBytes, afterBytes);
                VerifiedDiffCount = changes.Count;
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = "Revision Before / After · 差异 " + changes.Count + " 项",
                    FontWeight = FontWeights.Bold, Foreground = Brushes.DarkSlateBlue,
                    Margin = new Thickness(0, 6, 0, 8)
                });
                foreach (string change in changes)
                    ContentPanel.Children.Add(new TextBlock
                    {
                        Text = change, TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.DarkOrange,
                        Margin = new Thickness(4, 2, 0, 2)
                    });
                if (changes.Count == 0)
                    ContentPanel.Children.Add(new TextBlock { Text = "两个已验证快照没有对象差异。",
                        Foreground = Brushes.DarkGreen });
                }
                catch (InvalidDataException ex)
                {
                    EvidenceProblem = "工程版本不可比较：" + ex.Message;
                    ContentPanel.Children.Add(new TextBlock { Text = EvidenceProblem,
                        Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap });
                }
            }
            Observations(result, Kind);
            Assertions(result);
        }

        private void BindSnapshot(XDocument snapshot, TCTTestObservation observation)
        {
            XElement root = snapshot.Root;
            Guid projectId = Guid.Parse((string)root.Attribute("projectId"));
            int revision = (int)root.Attribute("revision");
            XElement[] nodes = root.Elements("Node").ToArray();
            Dictionary<Guid, XElement> index = nodes.ToDictionary(x =>
                Guid.Parse((string)x.Attribute("id")));
            VerifiedNodeCount = nodes.Length;
            VerifiedReferenceCount = nodes.Sum(x => x.Elements("Reference").Count());
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "已验证工程 " + projectId + "   Revision " + revision +
                    "   节点 " + VerifiedNodeCount + "   引用 " + VerifiedReferenceCount,
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue,
                Margin = new Thickness(0, 0, 0, 8)
            });

            Grid split = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            TreeView tree = new TreeView
            {
                MinHeight = 310, MaxHeight = 520,
                Background = Brushes.AliceBlue,
                BorderBrush = Brushes.LightSteelBlue
            };
            OwnershipTree = tree;
            tree.Items.Add(BuildItem(index[projectId], index));
            tree.SelectedItemChanged += (sender, args) =>
            {
                TreeViewItem selected = args.NewValue as TreeViewItem;
                if (selected == null) return;
                XElement node = selected.Tag as XElement;
                if (node == null) return;
                Guid id;
                if (!Guid.TryParse((string)node.Attribute("id"), out id)) return;
                string kind = node.Name.LocalName;
                Guid targetId;
                string address = kind == "Node" ?
                    new MRIObjectAddress(projectId,
                        Guid.Parse((string)node.Attribute("storageRootId")), id).ToString() :
                    (Guid.TryParse((string)node.Attribute("targetId"), out targetId) &&
                     index.ContainsKey(targetId) ?
                        new MRIObjectAddress(projectId,
                            Guid.Parse((string)index[targetId].Attribute("storageRootId")),
                            targetId, new[] { id }).ToString() :
                        "ReferenceId=" + id + " / TargetId unavailable");
                string detail = kind + " / " + (string)node.Attribute("name") +
                    "\nId: " + id +
                    "\nAddress: " + address +
                    (kind == "Node" ? "\nKind: " + (string)node.Attribute("kind") +
                        "\nParentId: " + (string)node.Attribute("parentId") +
                        "\nScope: " + (string)node.Attribute("scope") +
                        "\nVisual: " + (node.Element("Visual") == null ? "未提供" :
                            string.Join(", ", node.Element("Visual").Attributes().Select(a =>
                                a.Name.LocalName + "=" + a.Value))) :
                        "\nReference Kind: " + (string)node.Attribute("kind"));
                SelectObservation(observation);
                if (ObjectSelected != null) ObjectSelected(id.ToString("D"), detail);
            };
            Grid.SetColumn(tree, 0); split.Children.Add(tree);

            StackPanel related = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            related.Children.Add(new TextBlock { Text = "关联 / 声明总览", FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkSlateBlue, Margin = new Thickness(0, 0, 0, 7) });
            int ports = nodes.Sum(n => n.Elements("Port").Count());
            int connections = nodes.Sum(n => n.Elements("Connection").Count());
            int properties = nodes.Sum(n => n.Elements("Property").Count());
            int aspects = nodes.Sum(n => n.Elements("Aspect").Count());
            related.Children.Add(new TextBlock
            {
                Text = "Port: " + ports + "  Connection: " + connections +
                    "\nProperty: " + properties + "  Aspect: " + aspects,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
            });
            foreach (XElement node in nodes.Where(n => n.Elements("Reference").Any()))
                foreach (XElement reference in node.Elements("Reference"))
                {
                    Guid targetId = Guid.Parse((string)reference.Attribute("targetId"));
                    XElement target = index[targetId];
                    related.Children.Add(new TextBlock
                    {
                        Text = "↪ " + (string)node.Attribute("name") +
                            " / " + (string)reference.Attribute("name") +
                            " → " + (string)target.Attribute("name") +
                            " [" + (string)reference.Attribute("kind") + "]",
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3)
                    });
                }
            if (VerifiedReferenceCount == 0)
                related.Children.Add(new TextBlock { Text = "此快照没有资源引用。",
                    Foreground = Brushes.Gray });
            foreach (XElement node in nodes)
            {
                foreach (XElement port in node.Elements("Port"))
                    related.Children.Add(new TextBlock
                    {
                        Text = "Port · " + (string)node.Attribute("name") + "." +
                            (string)port.Attribute("member") + " / " +
                            (string)port.Attribute("direction") + " / " +
                            (string)port.Attribute("typeId"),
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 2)
                    });
                foreach (XElement connection in node.Elements("Connection"))
                    related.Children.Add(new TextBlock
                    {
                        Text = "Connection · " + (string)connection.Attribute("source") +
                            " → " + (string)connection.Attribute("target"),
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 2)
                    });
            }
            ScrollViewer relatedScroll = new ScrollViewer { Content = related,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 500 };
            Grid.SetColumn(relatedScroll, 1); split.Children.Add(relatedScroll);
            ContentPanel.Children.Add(split);
            if (nodes.Length <= 500)
            {
                TCTObjectReferenceDiagram diagram = new TCTObjectReferenceDiagram();
                diagram.BindVerifiedSnapshot(snapshot);
                diagram.ObjectSelected += (id, details) =>
                {
                    SelectObservation(observation);
                    if (ObjectSelected != null) ObjectSelected(id.ToString("D"), details);
                };
                RelationDiagram = diagram;
                ContentPanel.Children.Add(new TextBlock { Text = "Ownership / Reference · 真实只读对象关系图",
                    FontWeight = FontWeights.Bold, Margin = new Thickness(6, 5, 6, 5) });
                ContentPanel.Children.Add(diagram);
            }
            else
                ContentPanel.Children.Add(new TextBlock {
                    Text = "对象图节点超过500，暂仅使用已验证树显示，未虚构关系图。",
                    Foreground = Brushes.DarkGoldenrod, Margin = new Thickness(7) });
        }

        private static TreeViewItem BuildItem(XElement node, Dictionary<Guid, XElement> index)
        {
            TreeViewItem item = new TreeViewItem
            {
                Header = (string)node.Attribute("kind") + " · " + (string)node.Attribute("name"),
                Tag = node, IsExpanded = (string)node.Attribute("kind") != "Group"
            };
            foreach (XElement child in node.Elements("Child"))
                item.Items.Add(BuildItem(index[Guid.Parse((string)child.Attribute("id"))], index));
            // Reference is an independent leaf, NOT a child-owned Symbol/Content definition.
            foreach (XElement reference in node.Elements("Reference"))
                item.Items.Add(new TreeViewItem
                {
                    Header = "↪ Reference · " + (string)reference.Attribute("name"),
                    Tag = reference
                });
            return item;
        }
    }

    // Graph/Runtime视图只消费Run/Attempt校验后的生产Owner证据。
    public abstract class TCTGraphRuntimeViewBase : TCTTestViewBase
    {
        public string ArchivedEvidenceRoot { get; set; }
        public string EvidenceProblem { get; private set; }
        public int VerifiedItems { get; private set; }
        public event Action<string, string> SourceSelected;

        public override void Clear()
        {
            base.Clear();
            EvidenceProblem = null; VerifiedItems = 0;
        }
        protected void Selected(TCTTestObservation observation, string id, string details)
        {
            SelectObservation(observation);
            if (SourceSelected != null) SourceSelected(id, details);
        }
        protected void Count() { VerifiedItems++; }
        protected abstract void Render(XDocument data, TCTTestObservation observation);
        protected void RenderEvidence(TCTTestCaseResult result)
        {
            foreach (TCTTestObservation observation in result.Observations.Where(o =>
                string.Equals(o.Kind, Kind, StringComparison.OrdinalIgnoreCase)))
            {
                if (observation.Envelope == null || observation.Envelope.PayloadRef == null ||
                    observation.Envelope.PayloadRef.Mime != TCTGraphRuntimeEvidence.Mime)
                {
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "NoEvidence：没有受支持的GraphRuntime结构化Owner数据 · " +
                            observation.Title,
                        Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
                    continue;
                }
                try
                {
                    TCTTestArtifactStore store = new TCTTestArtifactStore(
                        ArchivedEvidenceRoot ?? TCTTestArtifactStore.RunRoot(result.RunId));
                    TCTObservationReplay.Read(result, store);
                    XDocument data = TCTGraphRuntimeEvidence.Read(store.Read(observation.Envelope.PayloadRef));
                    XElement root = data.Root;
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "SHA256/Schema已验证 · Project " + (string)root.Attribute("projectId") +
                            " · Revision " + (string)root.Attribute("revision") +
                            " · Flow " + (string)root.Attribute("flowId"),
                        FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 9),
                        TextWrapping = TextWrapping.Wrap });
                    Render(data, observation);
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IOException ||
                    ex is System.Xml.XmlException || ex is UnauthorizedAccessException ||
                    ex is NotSupportedException)
                {
                    EvidenceProblem = ex.Message;
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "GraphRuntime证据被拒绝：" + ex.Message,
                        Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap });
                }
            }
            Observations(result, Kind);
            Assertions(result);
        }
    }

    public sealed class TCTGraphTestView : TCTGraphRuntimeViewBase
    {
        public override string Kind { get { return "Graph"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear(); Header("MOL Definition / MOC Plan / SourceMap",
                "真实原始Block、已编译Step及依赖，点击定义或编译Step可以定位同一工程地址。");
            RenderEvidence(result);
        }
        protected override void Render(XDocument data, TCTTestObservation observation)
        {
            XElement root = data.Root;
            ContentPanel.Children.Add(new TextBlock { Text = "MOL Definition Tree",
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 5) });
            TreeView definition = new TreeView { MaxHeight = 280 };
            foreach (XElement block in root.Element("Definition").Elements("Block"))
                definition.Items.Add(BuildBlock(block, observation));
            ContentPanel.Children.Add(definition);
            ContentPanel.Children.Add(new TextBlock { Text = "MOC Compiled Step / Dependency → MOL Block",
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
            Dictionary<string, XElement> map = root.Element("SourceMap").Elements("Map")
                .ToDictionary(x => (string)x.Attribute("step"), StringComparer.Ordinal);
            foreach (XElement step in root.Element("Plan").Elements("Step"))
            {
                string id = (string)step.Attribute("id");
                string deps = string.Join(", ", step.Elements("Dependency")
                    .Select(x => (string)x.Attribute("id")));
                XElement origin;
                map.TryGetValue(id, out origin);
                string block = origin == null ? "Unmapped" : (string)origin.Attribute("block");
                string address = origin == null ? "" : (string)origin.Attribute("address");
                Button link = new Button {
                    Content = id + "  →  " + block + (deps.Length == 0 ? "" : " · depends: " + deps),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(5, 4, 5, 4), Margin = new Thickness(0, 2, 0, 2)
                };
                System.Windows.Automation.AutomationProperties.SetAutomationId(link,
                    "TCT.Graph.Step." + id);
                link.Click += (sender, args) => Selected(observation, block,
                    "MOC → MOL SourceMap\nCompiledStep: " + id + "\nMOL Block: " + block +
                    "\nDefinitionAddress: " + address + "\nDependencies: " + deps);
                ContentPanel.Children.Add(link);
                Count();
            }
        }
        private TreeViewItem BuildBlock(XElement block, TCTTestObservation observation)
        {
            string id = (string)block.Attribute("id");
            TreeViewItem item = new TreeViewItem {
                Header = (string)block.Attribute("kind") + " · " +
                    (string)block.Attribute("capability") + " · " + id, IsExpanded = true };
            item.Selected += (sender, args) => {
                if (!object.ReferenceEquals(args.OriginalSource, item)) return;
                Selected(observation, id, "MOL Definition\nBlockId: " + id +
                    "\nKind: " + (string)block.Attribute("kind") +
                    "\nCapability: " + (string)block.Attribute("capability"));
            };
            foreach (XElement child in block.Elements("Block"))
                item.Items.Add(BuildBlock(child, observation));
            return item;
        }
    }

    public sealed class TCTChannelTestView : TCTTestViewBase
    {
        public override string Kind { get { return "Channel"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear(); Header("Channel / Protocol / TX-RX",
                "尚未取得真实生产Channel/Codec/Session及Frame证据时不绘制虚构报文。");
            Observations(result, Kind); Assertions(result);
        }
    }

    public sealed class TCTRuntimeTestView : TCTGraphRuntimeViewBase
    {
        public override string Kind { get { return "Runtime"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear(); Header("MRK Flow / StepState / Trace Timeline",
                "原生MRK运行实例与事件；Unknown、失败、取消只显示实际状态，不以节点颜色猜测。");
            RenderEvidence(result);
        }
        protected override void Render(XDocument data, TCTTestObservation observation)
        {
            XElement root = data.Root;
            string flow = (string)root.Attribute("flowId");
            ContentPanel.Children.Add(new TextBlock {
                Text = "Flow " + flow + " · " + (string)root.Attribute("flowState") +
                    " · Plan " + (string)root.Attribute("planId"),
                FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            Dictionary<string, XElement> map = root.Element("SourceMap").Elements("Map")
                .ToDictionary(x => (string)x.Attribute("step"), StringComparer.Ordinal);
            ContentPanel.Children.Add(new TextBlock {
                Text = "ExecutionInstance / Step States", FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5) });
            foreach (XElement state in root.Element("Execution").Elements("State"))
            {
                string id = (string)state.Attribute("step");
                XElement origin;
                map.TryGetValue(id, out origin);
                string block = origin == null ? "Unmapped" : (string)origin.Attribute("block");
                Button button = new Button {
                    Content = (string)state.Attribute("value") + " · " + id + " → " + block,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(5, 4, 5, 4), Margin = new Thickness(0, 2, 0, 2) };
                System.Windows.Automation.AutomationProperties.SetAutomationId(button,
                    "TCT.Runtime.Step." + id);
                button.Click += (sender, args) => Selected(observation, block,
                    "MRK Step → MOL Block\nFlow: " + flow + "\nStep: " + id +
                    "\nBlock: " + block + "\nState: " + (string)state.Attribute("value"));
                ContentPanel.Children.Add(button);
                Count();
            }
            ContentPanel.Children.Add(new TextBlock {
                Text = "MRK Trace / Timeline · Lost=" + (string)root.Element("Timeline").Attribute("lost"),
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
            foreach (XElement evt in root.Element("Timeline").Elements("Event"))
            {
                string block = (string)evt.Attribute("block");
                string step = (string)evt.Attribute("step");
                Button button = new Button {
                    Content = "#" + (string)evt.Attribute("sequence") + " · " +
                        (string)evt.Attribute("state") + " · " + step,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(5, 4, 5, 4), Margin = new Thickness(0, 2, 0, 2) };
                button.Click += (sender, args) => Selected(observation, block,
                    "MRK Trace → MOL Block\nFlow: " + flow + "\nStep: " + step +
                    "\nBlock: " + block + "\nSequence: " + (string)evt.Attribute("sequence") +
                    "\nUTC: " + (string)evt.Attribute("utc"));
                ContentPanel.Children.Add(button);
                Count();
            }
        }
    }

    public sealed class TCTTestDocumentHostControl : UserControl
    {
        private readonly TabControl _tabs = new TabControl();
        private readonly TextBlock _empty = new TextBlock { Text = "选择用例运行后显示真实观察数据。",
            Margin = new Thickness(15), Foreground = Brushes.DimGray };
        private readonly List<ITCTTestViewer> _viewers = new List<ITCTTestViewer> {
            new TCTGeometryTestView(), new TCTObjectTestView(), new TCTGraphTestView(),
            new TCTChannelTestView(), new TCTRuntimeTestView()
        };
        // Shell订阅同一条选择事件，Inspector可显示结构化Owner/Attempt/Artifact证据。
        public event Action<TCTTestObservation> ObservationSelected;
        public event Action<string, string> ObjectSelected;
        public int ViewerCount { get { return _tabs.Items.OfType<TabItem>().Count(tab => tab.Content is ITCTTestViewer); } }
        public TCTTestDocumentHostControl()
        {
            foreach (TCTTestViewBase viewer in _viewers.OfType<TCTTestViewBase>())
                viewer.ObservationSelected += observation => {
                    if (ObservationSelected != null) ObservationSelected(observation);
                };
            TCTObjectTestView objectViewer = _viewers.OfType<TCTObjectTestView>().Single();
            objectViewer.ObjectSelected += (id, details) =>
            {
                if (ObjectSelected != null) ObjectSelected(id, details);
            };
            foreach (TCTGraphRuntimeViewBase sourceView in _viewers.OfType<TCTGraphRuntimeViewBase>())
                sourceView.SourceSelected += (id, details) => {
                    if (ObjectSelected != null) ObjectSelected(id, details);
                };
            TCTGeometryTestView geometryViewer = _viewers.OfType<TCTGeometryTestView>().Single();
            geometryViewer.GeometrySelected += (id, details) =>
            {
                if (ObjectSelected != null) ObjectSelected(id, details);
            };
            Content = _tabs;
            Clear();
        }
        public void Clear()
        {
            _tabs.Items.Clear();
            foreach (ITCTTestViewer viewer in _viewers) viewer.Clear();
            _tabs.Items.Add(new TabItem { Header = "概览", Content = _empty });
        }
        public string ActiveMode
        {
            get { return _tabs.SelectedItem is TabItem ? ((TabItem)_tabs.SelectedItem).Header.ToString() : ""; }
        }

        public void ShowMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode) || mode == "概览")
            {
                _tabs.SelectedIndex = 0;
                return;
            }
            foreach (TabItem existing in _tabs.Items.OfType<TabItem>())
                if (string.Equals(existing.Header as string, mode, StringComparison.OrdinalIgnoreCase))
                {
                    _tabs.SelectedItem = existing;
                    return;
                }
            if (!_viewers.Any(v => string.Equals(v.Kind, mode, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Unknown viewer mode", "mode");
            TabItem missing = new TabItem
            {
                Header = mode,
                Content = new TextBlock
                {
                    Text = mode + " · NoEvidence / Unsupported\n\n" +
                        "尚未从真实生产 Owner 的本次测试 Attempt 中获取此模式的结构化观察。" +
                        "\n请先在左侧运行相应真实测试。不会绘制演示状态或虚假成功结果。",
                    Margin = new Thickness(22), Foreground = Brushes.DarkGoldenrod,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            _tabs.Items.Add(missing);
            _tabs.SelectedItem = missing;
        }

        public void ShowResult(TCTTestCaseResult result)
        {
            ShowResult(result, null);
        }
        public void ShowResult(TCTTestCaseResult result, string verifiedArchiveDirectory)
        {
            Clear();
            _viewers.OfType<TCTObjectTestView>().Single().ArchivedEvidenceRoot =
                verifiedArchiveDirectory;
            _viewers.OfType<TCTGeometryTestView>().Single().ArchivedEvidenceRoot =
                verifiedArchiveDirectory;
            foreach (TCTGraphRuntimeViewBase ownerView in _viewers.OfType<TCTGraphRuntimeViewBase>())
                ownerView.ArchivedEvidenceRoot = verifiedArchiveDirectory;
            if (result == null) return;
            _empty.Text = result.Status + " · " + result.Descriptor.Name +
                "\n观察数：" + result.Observations.Count +
                "\n断言数：" + result.Assertions.Count +
                "\n诊断数：" + result.Diagnostics.Count +
                "\n产物数：" + result.Artifacts.Count +
                "\n\n没有专业观察时不生成演示数据。";
            foreach (ITCTTestViewer viewer in _viewers)
            {
                if (!viewer.CanDisplay(result)) continue;
                viewer.Bind(result);
                TabItem tab = new TabItem { Header = viewer.Kind, Content = (UserControl)viewer };
                System.Windows.Automation.AutomationProperties.SetAutomationId(tab,
                    "TCT.Viewer." + viewer.Kind);
                _tabs.Items.Add(tab);
            }
            if (_tabs.Items.Count > 1) _tabs.SelectedIndex = 1;
        }
    }
}
