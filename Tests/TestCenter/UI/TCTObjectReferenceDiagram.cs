using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// A bounded, read-only ownership/reference projection of a verified MEMProjectSnapshot.
    /// Solid line = owned child; orange dashed line = independent definition reference.
    /// The WPF diagram never owns or mutates MEM engineering objects.
    /// </summary>
    public sealed class TCTObjectReferenceDiagram : UserControl
    {
        private readonly Canvas _canvas = new Canvas { Background = Brushes.WhiteSmoke };
        private readonly ScaleTransform _scale = new ScaleTransform(1, 1);
        private readonly TextBlock _hint = new TextBlock { Margin = new Thickness(7),
            TextWrapping = TextWrapping.Wrap };
        private readonly Dictionary<Guid, Border> _boxes = new Dictionary<Guid, Border>();
        private readonly Dictionary<Guid, Point> _positions = new Dictionary<Guid, Point>();
        private double _zoom = 1;
        public event Action<Guid, string> ObjectSelected;
        public int RenderedNodeCount { get; private set; }
        public int VerifiedOwnershipLines { get; private set; }
        public int VerifiedReferenceLines { get; private set; }
        public TCTObjectReferenceDiagram()
        {
            _canvas.LayoutTransform = _scale;
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal,
                Margin = new Thickness(4) };
            Button plus = new Button { Content = "+ 放大", Margin = new Thickness(3) };
            Button minus = new Button { Content = "− 缩小", Margin = new Thickness(3) };
            Button reset = new Button { Content = "100%", Margin = new Thickness(3) };
            plus.Click += (s, e) => SetZoom(_zoom * 1.2);
            minus.Click += (s, e) => SetZoom(_zoom / 1.2);
            reset.Click += (s, e) => SetZoom(1);
            actions.Children.Add(plus); actions.Children.Add(minus); actions.Children.Add(reset);
            actions.Children.Add(_hint);
            DockPanel dock = new DockPanel();
            DockPanel.SetDock(actions, Dock.Top);
            dock.Children.Add(actions);
            ScrollViewer scroll = new ScrollViewer { Content = _canvas,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 350 };
            dock.Children.Add(scroll);
            Content = dock;
        }
        private void SetZoom(double value)
        {
            _zoom = Math.Max(0.25, Math.Min(3.5, value));
            _scale.ScaleX = _scale.ScaleY = _zoom;
        }
        private static int Depth(XElement node, IDictionary<Guid, XElement> index)
        {
            int depth = 0;
            Guid parent;
            while (Guid.TryParse((string)node.Attribute("parentId"), out parent) &&
                index.ContainsKey(parent))
            {
                depth++;
                if (depth > 64) throw new InvalidOperationException("Snapshot exceeds diagram depth");
                node = index[parent];
            }
            return depth;
        }
        public void BindVerifiedSnapshot(XDocument snapshot)
        {
            if (snapshot == null || snapshot.Root == null) throw new ArgumentNullException("snapshot");
            // Caller must use TCTEngineeringSnapshotCodec.Deserialize on a verified artifact.
            XElement[] nodes = snapshot.Root.Elements("Node").ToArray();
            if (nodes.Length == 0 || nodes.Length > 500)
                throw new InvalidOperationException("Object diagram supports 1..500 verified nodes");
            var index = nodes.ToDictionary(n => Guid.Parse((string)n.Attribute("id")));
            _canvas.Children.Clear(); _boxes.Clear(); _positions.Clear();
            RenderedNodeCount = VerifiedOwnershipLines = VerifiedReferenceLines = 0;
            int maxDepth = 0;
            const double nodeWidth = 184, nodeHeight = 40, rowGap = 63, indent = 213;
            for (int i = 0; i < nodes.Length; i++)
            {
                Guid id = Guid.Parse((string)nodes[i].Attribute("id"));
                int depth = Depth(nodes[i], index);
                maxDepth = Math.Max(maxDepth, depth);
                _positions[id] = new Point(15 + depth * indent, 22 + i * rowGap);
            }
            _canvas.Width = Math.Max(660, 35 + maxDepth * indent + nodeWidth);
            _canvas.Height = Math.Max(230, 40 + nodes.Length * rowGap);
            foreach (XElement node in nodes)
            {
                Guid id = Guid.Parse((string)node.Attribute("id"));
                Point current = _positions[id];
                Guid parent;
                if (Guid.TryParse((string)node.Attribute("parentId"), out parent) &&
                    _positions.ContainsKey(parent))
                {
                    Point from = _positions[parent];
                    _canvas.Children.Add(new Line {
                        X1 = from.X + nodeWidth, Y1 = from.Y + nodeHeight / 2,
                        X2 = current.X, Y2 = current.Y + nodeHeight / 2,
                        Stroke = Brushes.SteelBlue, StrokeThickness = 1.5,
                        IsHitTestVisible = false });
                    VerifiedOwnershipLines++;
                }
                foreach (XElement reference in node.Elements("Reference"))
                {
                    Guid target = Guid.Parse((string)reference.Attribute("targetId"));
                    Point to;
                    if (!_positions.TryGetValue(target, out to))
                        throw new InvalidOperationException("Snapshot reference target not present");
                    _canvas.Children.Add(new Line {
                        X1 = current.X + nodeWidth / 2, Y1 = current.Y + nodeHeight,
                        X2 = to.X + nodeWidth / 2, Y2 = to.Y,
                        Stroke = Brushes.DarkOrange, StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 4, 3 },
                        IsHitTestVisible = false,
                        ToolTip = "Reference: " + (string)reference.Attribute("name")
                    });
                    VerifiedReferenceLines++;
                }
            }
            foreach (XElement node in nodes)
            {
                Guid id = Guid.Parse((string)node.Attribute("id"));
                string name = (string)node.Attribute("name") ?? "";
                string kind = (string)node.Attribute("kind") ?? "";
                Point point = _positions[id];
                Border box = new Border { Width = nodeWidth, Height = nodeHeight,
                    Background = Brushes.White, BorderBrush = Brushes.SteelBlue,
                    BorderThickness = new Thickness(1.4), CornerRadius = new CornerRadius(5),
                    ToolTip = kind + " · " + name + "\n" + id };
                TextBlock title = new TextBlock { Text = kind + "  " + name, Padding = new Thickness(7),
                    TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                box.Child = title;
                Guid captured = id;
                box.MouseLeftButtonDown += (s, e) =>
                { SelectNode(captured); e.Handled = true; };
                System.Windows.Automation.AutomationProperties.SetAutomationId(box,
                    "TCT.Object.Node." + captured.ToString("N"));
                Canvas.SetLeft(box, point.X);
                Canvas.SetTop(box, point.Y);
                _canvas.Children.Add(box);
                _boxes.Add(id, box);
                RenderedNodeCount++;
            }
            _hint.Text = RenderedNodeCount + " Node · " + VerifiedOwnershipLines +
                " Ownership（蓝色实线）· " + VerifiedReferenceLines +
                " Reference（橙色虚线）";
        }
        public void SelectNode(Guid id)
        {
            Border target;
            if (!_boxes.TryGetValue(id, out target))
                throw new ArgumentException("Unknown verified node", "id");
            foreach (Border box in _boxes.Values)
            { box.BorderBrush = Brushes.SteelBlue; box.BorderThickness = new Thickness(1.4); }
            target.BorderBrush = Brushes.DarkOrange;
            target.BorderThickness = new Thickness(3);
            if (ObjectSelected != null)
                ObjectSelected(id, "已验证工程对象：" + id +
                    "\n从Snapshot绘制的只读Ownership/Reference关系。");
        }
    }
}
