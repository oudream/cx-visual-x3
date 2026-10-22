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
}
