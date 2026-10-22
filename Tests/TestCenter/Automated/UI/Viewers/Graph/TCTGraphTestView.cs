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
}
