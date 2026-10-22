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
}
