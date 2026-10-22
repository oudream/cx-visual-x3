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
}
