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
}
