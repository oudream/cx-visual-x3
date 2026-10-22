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
}
