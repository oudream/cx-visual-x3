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
            Grid geometryLayout = new Grid { Margin = new Thickness(0, 5, 0, 12) };
            geometryLayout.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(3, GridUnitType.Star) });
            geometryLayout.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(2, GridUnitType.Star) });
            Border coordinateCard = GeometryCard("坐标系 / Expected vs Actual · 真实断言", canvas);
            Grid.SetColumn(coordinateCard, 0);
            geometryLayout.Children.Add(coordinateCard);
            StackPanel comparison = new StackPanel();
            comparison.Children.Add(GeometryRow("Test ID", result.Descriptor.Id));
            comparison.Children.Add(GeometryRow("Expected X", ax == null ? "NoEvidence" : ax.Expected));
            comparison.Children.Add(GeometryRow("Actual X", ax == null ? "NoEvidence" : ax.Actual));
            comparison.Children.Add(GeometryRow("Expected Y", ay == null ? "NoEvidence" : ay.Expected));
            comparison.Children.Add(GeometryRow("Actual Y", ay == null ? "NoEvidence" : ay.Actual));
            comparison.Children.Add(GeometryRow("Assertions",
                result.Assertions.Count(assertion => assertion.Passed) + " / " + result.Assertions.Count + " Pass"));
            comparison.Children.Add(new TextBlock {
                Text = "详细几何仅使用SHA256核验后的Owner Figure；此卡只显示本次实际Assertion。",
                Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 10, 8, 0) });
            Border comparisonCard = GeometryCard("输入 / 预期 / 实际 / 误差依据", comparison);
            Grid.SetColumn(comparisonCard, 1); geometryLayout.Children.Add(comparisonCard);
            ContentPanel.Children.Add(geometryLayout);
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
                    ContentPanel.Children.Add(GeometryCard(
                        "SHA256 Verified Geometry · Input / Expected / Actual", proof));
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
        private static Border GeometryCard(string title, UIElement body)
        {
            DockPanel layout = new DockPanel();
            TextBlock header = new TextBlock {
                Text = title, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.MidnightBlue, Background = Brushes.AliceBlue,
                Padding = new Thickness(8, 7, 8, 7), Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
            layout.Children.Add(body);
            return new Border { Child = layout, Padding = new Thickness(8),
                Background = Brushes.White, Margin = new Thickness(0, 0, 10, 0),
                BorderBrush = Brushes.LightSteelBlue, BorderThickness = new Thickness(1) };
        }
        private static TextBlock GeometryRow(string name, string value)
        {
            return new TextBlock { Text = name + ":  " + value,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = name + ": " + value, Margin = new Thickness(8, 7, 8, 2),
                Foreground = Brushes.DarkSlateGray };
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
            Border ownershipPanel = ObjectPanel("Ownership / 父子对象树", tree);
            Grid.SetColumn(ownershipPanel, 0); split.Children.Add(ownershipPanel);

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
            Border attributesPanel = ObjectPanel("Object / Property / Port / Connection", relatedScroll);
            Grid.SetColumn(attributesPanel, 2); split.Children.Add(attributesPanel);
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
                Border referencePanel = ObjectPanel("Reference / Mapping · 已验证只读关系图", diagram);
                Grid.SetColumn(referencePanel, 1); split.Children.Add(referencePanel);
            }
            else
            {
                Border referenceFallback = ObjectPanel("Reference / Mapping",
                    new TextBlock {
                        Text = "对象图节点超过500，暂仅展示真实对象树。引用图不绘制虚拟节点。",
                        Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(referenceFallback, 1); split.Children.Add(referenceFallback);
            }
        }

        private static Border ObjectPanel(string title, UIElement content)
        {
            DockPanel body = new DockPanel();
            TextBlock heading = new TextBlock {
                Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.MidnightBlue, Background = Brushes.AliceBlue,
                Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(heading, Dock.Top);
            body.Children.Add(heading); body.Children.Add(content);
            return new Border { Child = body, Margin = new Thickness(0, 0, 8, 0),
                Background = Brushes.White, BorderBrush = Brushes.LightSteelBlue,
                BorderThickness = new Thickness(1), Padding = new Thickness(8) };
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
        public string EvidenceProblem { get; protected set; }
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
                if (Kind == "Runtime" && observation.Envelope != null &&
                    observation.Envelope.PayloadRef != null &&
                    observation.Envelope.PayloadRef.Mime == TCTRuntimeOwnerEvidence.Mime)
                    continue;
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
            RenderActualGraph(root, observation);
            Grid definitionsSplit = new Grid { Margin = new Thickness(0, 5, 0, 10) };
            definitionsSplit.ColumnDefinitions.Add(new ColumnDefinition { Width =
                new GridLength(1, GridUnitType.Star) });
            definitionsSplit.ColumnDefinitions.Add(new ColumnDefinition { Width =
                new GridLength(1, GridUnitType.Star) });
            StackPanel definitionSide = new StackPanel();
            StackPanel planSide = new StackPanel();
            Border left = GraphPanel("MOL Definition / MPC Ports", definitionSide);
            Border right = GraphPanel("MOC Plan / SourceMap", planSide);
            Grid.SetColumn(right, 1);
            definitionsSplit.Children.Add(left); definitionsSplit.Children.Add(right);
            ContentPanel.Children.Add(definitionsSplit);
            definitionSide.Children.Add(new TextBlock { Text = "MOL Definition Tree",
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 5) });
            TreeView definition = new TreeView { MaxHeight = 280 };
            foreach (XElement block in root.Element("Definition").Elements("Block"))
                definition.Items.Add(BuildBlock(block, observation));
            definitionSide.Children.Add(definition);
            XElement wiring = root.Element("Definition");
            if ((string)wiring.Attribute("variant") == "Wiring")
            {
                definitionSide.Children.Add(new TextBlock {
                    Text = "MOL V2 · MPC Typed Port / Connection（工程真源，非推导依赖）",
                    FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
                foreach (XElement link in wiring.Elements("Link"))
                {
                    string identity = (string)link.Attribute("id"),
                        source = (string)link.Attribute("sourceBlock"),
                        target = (string)link.Attribute("targetBlock");
                    Button connection = new Button {
                        Content = (string)link.Attribute("kind") + " · " +
                            source + "." + (string)link.Attribute("sourcePort") +
                            " → " + target + "." + (string)link.Attribute("targetPort"),
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(5, 4, 5, 4), Margin = new Thickness(0, 2, 0, 2)
                    };
                    System.Windows.Automation.AutomationProperties.SetAutomationId(connection,
                        "TCT.Graph.Link." + identity);
                    connection.Click += (sender, args) => Selected(observation, identity,
                        "MOL V2 Connection\nLinkId: " + identity +
                        "\nSource: " + source + "." + (string)link.Attribute("sourcePort") +
                        "\nTarget: " + target + "." + (string)link.Attribute("targetPort") +
                        "\nKind: " + (string)link.Attribute("kind"));
                    definitionSide.Children.Add(connection);
                    Count();
                }
            }
            planSide.Children.Add(new TextBlock { Text = "MOC Compiled Step / Dependency → MOL Block",
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
                planSide.Children.Add(link);
                Count();
            }
        }
        private void RenderActualGraph(XElement root, TCTTestObservation observation)
        {
            XElement def = root.Element("Definition");
            XElement[] blocks = def.Descendants("Block").ToArray();
            if (blocks.Length == 0)
            {
                ContentPanel.Children.Add(new TextBlock {
                    Text = "NoEvidence：本次真实MOL定义中没有图节点。",
                    Foreground = Brushes.DarkGoldenrod });
                return;
            }
            // 坐标仅为Viewer排版；节点和连线来自经过证据校验的MOL/MPC定义。
            const int limit = 80;
            XElement[] visible = blocks.Take(limit).ToArray();
            Dictionary<string, Point> positions = new Dictionary<string, Point>(StringComparer.Ordinal);
            Canvas canvas = new Canvas { Width = 840,
                Height = Math.Max(185, ((visible.Length + 3) / 4) * 110 + 25),
                Background = new SolidColorBrush(Color.FromRgb(249, 252, 255)),
                ClipToBounds = true };
            for (int i = 0; i < visible.Length; i++)
            {
                XElement block = visible[i];
                string id = (string)block.Attribute("id");
                positions[id] = new Point(15 + (i % 4) * 207, 25 + (i / 4) * 110);
            }
            if ((string)def.Attribute("variant") == "Wiring")
            {
                foreach (XElement link in def.Elements("Link"))
                {
                    Point from, to;
                    if (!positions.TryGetValue((string)link.Attribute("sourceBlock"), out from) ||
                        !positions.TryGetValue((string)link.Attribute("targetBlock"), out to))
                        continue;
                    canvas.Children.Add(new Line {
                        X1 = from.X + 175, Y1 = from.Y + 31,
                        X2 = to.X, Y2 = to.Y + 31,
                        Stroke = new SolidColorBrush(Color.FromRgb(37, 121, 206)),
                        StrokeThickness = 2 });
                }
            }
            foreach (XElement block in visible)
            {
                string id = (string)block.Attribute("id");
                string kind = (string)block.Attribute("kind");
                string capability = (string)block.Attribute("capability");
                Button button = new Button {
                    Content = new TextBlock {
                        Text = kind + " / " + capability + "\n" + id,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.Wrap, Foreground = Brushes.MidnightBlue,
                        MaxWidth = 170 },
                    Width = 175, Height = 62,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(35, 117, 211)),
                    BorderThickness = new Thickness(1.5),
                    Background = new SolidColorBrush(Color.FromRgb(235, 245, 255)),
                    Padding = new Thickness(7, 4, 7, 4) };
                System.Windows.Automation.AutomationProperties.SetAutomationId(button,
                    "TCT.Graph.Block." + id);
                button.Click += (sender, args) => Selected(observation, id,
                    "MOL Definition Block\nBlockId: " + id + "\nKind: " + kind +
                    "\nCapability: " + capability + "\n真实MPC连线由定义快照读取");
                Canvas.SetLeft(button, positions[id].X);
                Canvas.SetTop(button, positions[id].Y);
                canvas.Children.Add(button);
            }
            ContentPanel.Children.Add(GraphPanel(
                "Graph / Port · 真实MOL定义节点与MPC连接（" +
                visible.Length + " / " + blocks.Length + "）", canvas));
            if (blocks.Length > limit)
                ContentPanel.Children.Add(new TextBlock {
                    Text = "节点超过80个；这里只呈现前80个，完整定义及计划可在下方查看。",
                    Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
        }
        private static Border GraphPanel(string title, UIElement content)
        {
            DockPanel layout = new DockPanel();
            TextBlock header = new TextBlock {
                Text = title, FontWeight = FontWeights.SemiBold, FontSize = 13,
                Foreground = Brushes.MidnightBlue,
                Background = Brushes.AliceBlue, Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(header, Dock.Top);
            layout.Children.Add(header); layout.Children.Add(content);
            return new Border { Child = layout, Padding = new Thickness(9),
                BorderBrush = Brushes.LightSteelBlue, BorderThickness = new Thickness(1),
                Background = Brushes.White, Margin = new Thickness(0, 0, 9, 9) };
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
            foreach (XElement port in block.Elements("Port"))
                item.Items.Add(new TreeViewItem {
                    Header = (string)port.Attribute("direction") + " " +
                        (string)port.Attribute("member") + " : " + (string)port.Attribute("type") +
                        " · " + (string)port.Attribute("kind") });
            foreach (XElement child in block.Elements("Block"))
                item.Items.Add(BuildBlock(child, observation));
            return item;
        }
    }

    public sealed class TCTChannelTestView : TCTTestViewBase
    {
        public override string Kind { get { return "Channel"; } }
        public string ArchivedEvidenceRoot { get; set; }
        public int VerifiedFrameCount { get; private set; }
        public int VerifiedErrorCount { get; private set; }
        public string EvidenceProblem { get; private set; }
        public event Action<string, string> FrameSelected;
        public override void Clear()
        {
            base.Clear();
            VerifiedFrameCount = 0; VerifiedErrorCount = 0; EvidenceProblem = null;
        }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear();
            Header("Channel / Protocol / TX-RX / Virtual Session",
                "经Run/Attempt及SHA256核验的生产Connectivity原始帧、CRC解码、错误和重连记录。仅Virtual模式，非Real设备。");
            foreach (TCTTestObservation observation in result.Observations.Where(o =>
                string.Equals(o.Kind, Kind, StringComparison.OrdinalIgnoreCase)))
            {
                if (observation.Envelope == null || observation.Envelope.PayloadRef == null ||
                    observation.Envelope.PayloadRef.Mime != TCTChannelEvidence.Mime)
                {
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "NoEvidence：没有Channel生产Owner结构化收发证据 · " + observation.Title,
                        Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
                    continue;
                }
                try
                {
                    TCTTestArtifactStore store = new TCTTestArtifactStore(
                        ArchivedEvidenceRoot ?? TCTTestArtifactStore.RunRoot(result.RunId));
                    TCTObservationReplay.Read(result, store);
                    XDocument verified = TCTChannelEvidence.Read(store.Read(observation.Envelope.PayloadRef));
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "TX / RX Frame Timeline  ·  SHA256已验证  ·  " +
                            (string)verified.Root.Attribute("mode") + "  ·  " +
                            verified.Root.Elements("Frame").Count() + " 条真实Owner收发记录",
                        FontSize = 15, Foreground = Brushes.MidnightBlue,
                        FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 8) });
                    Border tableSurface = new Border {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(200, 218, 237)),
                        BorderThickness = new Thickness(1), Background = Brushes.White };
                    StackPanel timeline = new StackPanel();
                    timeline.Children.Add(ChannelRow("时间 (UTC)", "方向", "Channel",
                        "序号", "原始 HEX", "解码 / 异常", true));
                    tableSurface.Child = timeline;
                    ContentPanel.Children.Add(tableSurface);
                    Border detailSurface = new Border {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(200, 218, 237)),
                        BorderThickness = new Thickness(1), Background = Brushes.White,
                        Margin = new Thickness(0, 13, 0, 8), Padding = new Thickness(12, 9, 12, 9) };
                    TextBlock hexDetail = new TextBlock {
                        Text = "从上方选择真实TX/RX帧查看原始字节与解码内容。",
                        FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.SteelBlue };
                    detailSurface.Child = hexDetail;
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "选中帧解析 / Verified Frame", FontSize = 14,
                        FontWeight = FontWeights.SemiBold, Foreground = Brushes.MidnightBlue,
                        Margin = new Thickness(0, 10, 0, 2) });
                    ContentPanel.Children.Add(detailSurface);
                    int frameIndex = 0;
                    foreach (XElement frame in verified.Root.Elements("Frame"))
                    {
                        string channel = (string)frame.Attribute("channel");
                        string sequence = (string)frame.Attribute("sequence");
                        string direction = (string)frame.Attribute("direction");
                        string correlation = (string)frame.Attribute("correlation");
                        byte[] bytes = Convert.FromBase64String((string)frame.Attribute("raw"));
                        string hex = BitConverter.ToString(bytes).Replace("-", " ");
                        string payload = frame.Attribute("decoded") == null ? "" :
                            BitConverter.ToString(Convert.FromBase64String(
                                (string)frame.Attribute("decoded"))).Replace("-", " ");
                        string problem = (string)frame.Attribute("error");
                        string details = "Channel Frame · " + direction +
                            "\nSessionId: " + (string)frame.Attribute("session") +
                            "\nChannelId: " + channel + "\nSequence: " + sequence +
                            "\nTime UTC: " + (string)frame.Attribute("utc") +
                            "\nCorrelation: " + correlation +
                            "\nOriginal raw HEX: " + hex +
                            (payload.Length == 0 ? "" : "\nDecoded payload HEX: " + payload) +
                            (problem.Length == 0 ? "" : "\nError: " + problem);
                        string utc = (string)frame.Attribute("utc") ?? "";
                        string decoded = problem.Length > 0 ? "ERROR: " + problem :
                            payload.Length == 0 ? "等待解码" : payload;
                        Grid row = ChannelRow(utc, direction, channel, sequence, hex, decoded, false);
                        Button frameLink = new Button {
                            Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Background = frameIndex % 2 == 0 ? Brushes.White :
                                new SolidColorBrush(Color.FromRgb(242, 247, 253)),
                            BorderThickness = new Thickness(0), Padding = new Thickness(0),
                            ToolTip = correlation + " · " + details,
                            Cursor = System.Windows.Input.Cursors.Hand
                        };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(frameLink,
                            "TCT.Channel.Frame." + channel + "." + sequence);
                        frameLink.Click += (sender, args) => {
                            hexDetail.Text = details;
                            SelectObservation(observation);
                            if (FrameSelected != null)
                                FrameSelected(channel + ":" + sequence, details);
                        };
                        timeline.Children.Add(frameLink);
                        if (frameIndex == 0) hexDetail.Text = details;
                        frameIndex++;
                        if (direction == "Error") VerifiedErrorCount++;
                        VerifiedFrameCount++;
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IOException ||
                    ex is System.Xml.XmlException || ex is UnauthorizedAccessException ||
                    ex is NotSupportedException)
                {
                    EvidenceProblem = ex.Message;
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "Channel证据拒绝：" + ex.Message,
                        Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap });
                }
            }
            Observations(result, Kind); Assertions(result);
        }

        private static Grid ChannelRow(string utc, string direction, string channel,
            string sequence, string hex, string decoded, bool header)
        {
            Grid row = new Grid { Height = 33, Margin = new Thickness(0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (header) row.Background = new SolidColorBrush(Color.FromRgb(219, 235, 250));
            string[] values = { utc, direction, channel, sequence, hex, decoded };
            for (int i = 0; i < values.Length; i++)
            {
                TextBlock text = new TextBlock {
                    Text = values[i], TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(9, 0, 7, 0),
                    FontFamily = i == 4 && !header ? new FontFamily("Consolas") : new FontFamily("Segoe UI"),
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = !header && direction == "Error" ? Brushes.Firebrick :
                        header ? Brushes.MidnightBlue : Brushes.DarkSlateGray,
                    ToolTip = values[i] };
                Grid.SetColumn(text, i);
                row.Children.Add(text);
            }
            return row;
        }
    }

    public sealed class TCTRuntimeTestView : TCTGraphRuntimeViewBase
    {
        public override string Kind { get { return "Runtime"; } }
        public int VerifiedJournalCount { get; private set; }
        public override void Clear()
        {
            base.Clear();
            VerifiedJournalCount = 0;
        }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear(); Header("MRK Session / Flow / Journal / Step / Timeline",
                "真实MRK会话、运行状态、Journal恢复判定及Trace；若存在未知外部动作，不允许自动视为安全恢复。");
            RenderEvidence(result);
            RenderRuntimeOwner(result);
        }
        private void RenderRuntimeOwner(TCTTestCaseResult result)
        {
            foreach (TCTTestObservation observation in result.Observations.Where(o =>
                string.Equals(o.Kind, "Runtime", StringComparison.OrdinalIgnoreCase) &&
                o.Envelope != null && o.Envelope.PayloadRef != null &&
                o.Envelope.PayloadRef.Mime == TCTRuntimeOwnerEvidence.Mime))
            {
                try
                {
                    TCTTestArtifactStore store = new TCTTestArtifactStore(
                        ArchivedEvidenceRoot ?? TCTTestArtifactStore.RunRoot(result.RunId));
                    TCTObservationReplay.Read(result, store);
                    XDocument evidence = TCTRuntimeOwnerEvidence.Read(store.Read(observation.Envelope.PayloadRef));
                    XElement root = evidence.Root;
                    string flow = (string)root.Attribute("flow");
                    XElement session = root.Element("Session"), flowInfo = root.Element("Flow");
                    // 三分区对应Runtime设计稿：实例树 / 资源与状态 / 真实Journal时间线。
                    Grid runtimeDashboard = new Grid { Margin = new Thickness(0, 7, 0, 10) };
                    runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                    runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                    StackPanel instancePanel = new StackPanel();
                    StackPanel resourcePanel = new StackPanel();
                    TreeView instanceTree = new TreeView { MinHeight = 190, MaxHeight = 340,
                        Background = Brushes.White, BorderThickness = new Thickness(0) };
                    TreeViewItem flowNode = new TreeViewItem {
                        Header = "Flow " + flow + " [" + (string)root.Attribute("state") + "]",
                        IsExpanded = true, FontWeight = FontWeights.SemiBold };
                    instanceTree.Items.Add(flowNode);
                    instancePanel.Children.Add(instanceTree);
                    Border instancesCard = RuntimeCard("Execution Instance Tree / MRK Steps", instancePanel);
                    Border resourceCard = RuntimeCard("Selected Flow / Resource & Wait Queue", resourcePanel);
                    Grid.SetColumn(instancesCard, 0);
                    Grid.SetColumn(resourceCard, 1);
                    runtimeDashboard.Children.Add(instancesCard);
                    runtimeDashboard.Children.Add(resourceCard);
                    ContentPanel.Children.Add(runtimeDashboard);
                    StackPanel journalPanel = new StackPanel();
                    ContentPanel.Children.Add(RuntimeCard("MRK Journal / Timeline · 原生顺序事件", journalPanel));
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "SHA256已验证 · MRKRuntimeSession / MRKFileExecutionJournal",
                        FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 6) });
                    resourcePanel.Children.Add(new TextBlock {
                        Text = "Session: " + (string)session.Attribute("id") + " · State: " +
                            (string)session.Attribute("activeState") + " → " +
                            (string)session.Attribute("endState") + " · Active: " +
                            (string)session.Attribute("active") + " → " +
                            (string)session.Attribute("settled") + " · Rejected: " +
                            (string)session.Attribute("rejected") +
                            "\nFlow: " + flow + " · " + (string)root.Attribute("state") +
                            " · Revision: " + (string)root.Attribute("revision") +
                            " · Completed: " + (string)flowInfo.Attribute("completed") +
                            " / " + (string)flowInfo.Attribute("total") +
                            " · Waiting: " + (string)flowInfo.Attribute("waiting") +
                            " · Failed: " + (string)flowInfo.Attribute("failed"),
                        TextWrapping = TextWrapping.Wrap });
                    string recovery = (string)root.Attribute("recovery");
                    resourcePanel.Children.Add(new TextBlock {
                        Text = "Journal Recovery: " + recovery +
                            (recovery == "Closed" ? "（流程终态已闭合）" :
                             "（须由实际Owner核对，不能自动重播）"),
                        FontWeight = FontWeights.Bold,
                        Foreground = recovery == "Closed" ? Brushes.DarkGreen : Brushes.DarkRed,
                        Margin = new Thickness(0, 8, 0, 6) });
                    instancePanel.Children.Add(new TextBlock {
                        Text = "Step · MRKStepObservation / Definition Address",
                        FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 4) });
                    foreach (XElement step in root.Element("Observations").Elements("Step"))
                    {
                        string stepId = (string)step.Attribute("id");
                        string block = (string)step.Attribute("block");
                        Button select = new Button {
                            Content = (string)step.Attribute("state") + " · " + stepId +
                                " → " + block,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(5, 4, 5, 4) };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(select,
                            "TCT.Runtime.Owner.Step." + stepId);
                        select.Click += (sender, args) => Selected(observation, block,
                            "MRK StepObservation → MOL SourceMap\nFlow: " + flow +
                            "\nStep: " + stepId + "\nBlock: " + block +
                            "\nState: " + (string)step.Attribute("state") +
                            "\nWait/Failure Reason: " + (string)step.Attribute("reason") +
                            "\nInvocation: " + (string)step.Attribute("invocation") +
                            "\nDefinitionAddress: " + (string)step.Attribute("address"));
                        flowNode.Items.Add(new TreeViewItem { Header = select });
                        Count();
                    }
                    journalPanel.Children.Add(new TextBlock {
                        Text = "Journal · 原生顺序记录，不回放设备副作用",
                        FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8) });
                    journalPanel.Children.Add(RuntimeTimelineRow(
                        "Sequence", "UTC", "Event kind", "Step", "State", true));
                    foreach (XElement entry in root.Element("Journal").Elements("Entry"))
                    {
                        string sequence = (string)entry.Attribute("seq"), stepId = (string)entry.Attribute("step");
                        Button select = new Button {
                            Content = RuntimeTimelineRow(sequence,
                                (string)entry.Attribute("utc"),
                                (string)entry.Attribute("kind"), stepId,
                                (string)entry.Attribute("state"), false),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(5, 4, 5, 4) };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(select,
                            "TCT.Runtime.Journal." + sequence);
                        select.Click += (sender, args) => Selected(observation, flow,
                            "MRK Journal 原生事实\nFlow: " + flow + "\nSequence: " + sequence +
                            "\nKind: " + (string)entry.Attribute("kind") +
                            "\nStep: " + stepId + "\nState: " + (string)entry.Attribute("state") +
                            "\nCorrelation: " + (string)entry.Attribute("correlation") +
                            "\nOperationId: " + (string)entry.Attribute("operation") +
                            "\nUTC: " + (string)entry.Attribute("utc"));
                        journalPanel.Children.Add(select);
                        VerifiedJournalCount++; Count();
                    }
                    resourcePanel.Children.Add(new TextBlock {
                        Text = "MRK ExecutionGroup · 运行时实际配额快照",
                        FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4) });
                    XElement[] groups = root.Element("Groups").Elements("Group").ToArray();
                    if (groups.Length == 0)
                        resourcePanel.Children.Add(new TextBlock {
                            Text = "NoEvidence：本次MRK快照没有执行组或资源配额信息。",
                            Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
                    foreach (XElement group in groups)
                    {
                        resourcePanel.Children.Add(new TextBlock {
                            Text = (string)group.Attribute("id"),
                            FontWeight = FontWeights.SemiBold, Foreground = Brushes.MidnightBlue,
                            Margin = new Thickness(0, 9, 0, 3) });
                        resourcePanel.Children.Add(new TextBlock {
                            Text = "Active: " + (string)group.Attribute("active") +
                                "   Waiting: " + (string)group.Attribute("waiting") +
                                "   Rejected: " + (string)group.Attribute("rejected"),
                            Foreground = Brushes.DarkSlateGray });
                    }
                    resourcePanel.Children.Add(new TextBlock {
                        Text = "Lease Owner / 等待者身份：本次证据未包含时不推断；仅展示MRK实际Group Snapshot和Step等待原因。",
                        FontSize = 11, Foreground = Brushes.SlateGray,
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 0) });
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IOException ||
                    ex is System.Xml.XmlException || ex is UnauthorizedAccessException ||
                    ex is NotSupportedException)
                {
                    EvidenceProblem = ex.Message;
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "MRK Owner证据拒绝：" + ex.Message,
                        Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap });
                }
            }
        }
        private static Grid RuntimeTimelineRow(string sequence, string utc, string kind,
            string step, string state, bool header)
        {
            Grid row = new Grid { Height = 34 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (header) row.Background = new SolidColorBrush(Color.FromRgb(220, 234, 250));
            string[] labels = { sequence, utc, kind, step, state };
            for (int i = 0; i < labels.Length; i++)
            {
                TextBlock value = new TextBlock {
                    Text = labels[i] ?? "", TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12, Margin = new Thickness(8, 0, 4, 0),
                    Foreground = header ? Brushes.MidnightBlue :
                        (state != null && state.IndexOf("Fail", StringComparison.OrdinalIgnoreCase) >= 0
                            ? Brushes.Firebrick : Brushes.DarkSlateGray),
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                    ToolTip = labels[i] };
                Grid.SetColumn(value, i); row.Children.Add(value);
            }
            return row;
        }
        private static Border RuntimeCard(string title, UIElement content)
        {
            DockPanel panel = new DockPanel();
            TextBlock heading = new TextBlock {
                Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 67, 111)),
                Background = new SolidColorBrush(Color.FromRgb(235, 245, 255)),
                Padding = new Thickness(8, 7, 8, 7), Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(heading, Dock.Top);
            panel.Children.Add(heading); panel.Children.Add(content);
            return new Border {
                Child = panel, Padding = new Thickness(9, 8, 9, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 220, 238)),
                BorderThickness = new Thickness(1), Background = Brushes.White,
                Margin = new Thickness(0, 0, 9, 10), CornerRadius = new CornerRadius(3) };
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
        private readonly Dictionary<string, TabItem> _workspaceTabs =
            new Dictionary<string, TabItem>(StringComparer.Ordinal);
        public event Action<TCTWorkspaceDescriptor> WorkspaceActivated;
        private readonly TCTTestOverviewControl _overview = new TCTTestOverviewControl();
        public TCTTestOverviewControl Overview { get { return _overview; } }
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
            _viewers.OfType<TCTChannelTestView>().Single().FrameSelected += (id, details) => {
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
            _tabs.SelectionChanged += (sender, args) =>
            {
                if (!object.ReferenceEquals(args.OriginalSource, _tabs)) return;
                TabItem current = _tabs.SelectedItem as TabItem;
                if (WorkspaceActivated != null)
                    WorkspaceActivated(current == null ? null : current.Tag as TCTWorkspaceDescriptor);
            };
            Clear();
        }
        public UserControl OpenWorkspace(TCTWorkspaceDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Factory == null)
                throw new InvalidOperationException("Unsupported workspace factory");
            TabItem tab;
            if (!_workspaceTabs.TryGetValue(descriptor.WorkspaceId, out tab))
            {
                UserControl content = descriptor.Factory();
                if (content == null) throw new InvalidOperationException(
                    "Workspace factory returned null: " + descriptor.WorkspaceId);
                tab = new TabItem { Header = descriptor.Title, Tag = descriptor, Content = content };
                System.Windows.Automation.AutomationProperties.SetAutomationId(tab,
                    "TCT.Workspace.Tab." + descriptor.WorkspaceId);
                MenuItem close = new MenuItem { Header = "关闭工作区" };
                close.Click += (sender, args) => CloseWorkspace(descriptor.WorkspaceId);
                ContextMenu menu = new ContextMenu(); menu.Items.Add(close);
                tab.ContextMenu = menu;
                _workspaceTabs.Add(descriptor.WorkspaceId, tab);
                _tabs.Items.Add(tab);
            }
            _tabs.SelectedItem = tab;
            return (UserControl)tab.Content;
        }
        public bool CloseWorkspace(string id)
        {
            TabItem tab;
            if (!_workspaceTabs.TryGetValue(id, out tab)) return false;
            ITCTWorkspaceFrame frame = tab.Content as ITCTWorkspaceFrame;
            if (frame != null) frame.OnWorkspaceClosed();
            _tabs.Items.Remove(tab);
            _workspaceTabs.Remove(id);
            return true;
        }
        public void CloseAllWorkspaces()
        {
            foreach (string id in _workspaceTabs.Keys.ToArray())
                CloseWorkspace(id);
        }
        public void Clear()
        {
            TabItem[] saved = _workspaceTabs.Values.ToArray();
            _tabs.Items.Clear();
            foreach (ITCTTestViewer viewer in _viewers) viewer.Clear();
            _tabs.Items.Add(new TabItem { Header = "概览", Content = _overview });
            foreach (TabItem workspace in saved) _tabs.Items.Add(workspace);
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
            _viewers.OfType<TCTChannelTestView>().Single().ArchivedEvidenceRoot =
                verifiedArchiveDirectory;
            if (result == null) return;
            foreach (ITCTTestViewer viewer in _viewers)
            {
                if (!viewer.CanDisplay(result)) continue;
                viewer.Bind(result);
                TabItem tab = new TabItem { Header = viewer.Kind, Content = (UserControl)viewer };
                System.Windows.Automation.AutomationProperties.SetAutomationId(tab,
                    "TCT.Viewer." + viewer.Kind);
                _tabs.Items.Add(tab);
            }
            TabItem firstViewer = _tabs.Items.OfType<TabItem>()
                .FirstOrDefault(t => t.Content is ITCTTestViewer);
            if (firstViewer != null) _tabs.SelectedItem = firstViewer;
        }
    }
}
