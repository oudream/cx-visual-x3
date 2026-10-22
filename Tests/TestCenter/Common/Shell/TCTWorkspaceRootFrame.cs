using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Text;
using CxVisual.Mechanism.Connectivity;

namespace CxVisual.TestCenter.UI
{
    public sealed class TCTWorkspaceRootFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly TCTWorkspaceDescriptor _descriptor;
        public string WorkspaceId { get { return _descriptor.WorkspaceId; } }
        public IEnumerable<TCTWorkspaceAction> Commands { get { return new TCTWorkspaceAction[0]; } }
        public event Action StateChanged { add { } remove { } }
        public event Action<string> OpenRequested;
        public TCTWorkspaceRootFrame(TCTWorkspaceRegistry registry, string id, int catalogCount)
        {
            _descriptor = registry.Find(id);
            if (_descriptor == null) throw new ArgumentException("Unknown workspace");
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White };
            StackPanel body = new StackPanel { Margin = new Thickness(22) };
            body.Children.Add(new TextBlock { Text = _descriptor.Title, FontSize = 21,
                Foreground = Brushes.DarkSlateBlue, FontWeight = FontWeights.SemiBold });
            body.Children.Add(new TextBlock { Text = _descriptor.Description,
                Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 18) });
            if (id == "auto.root")
                body.Children.Add(new TextBlock { Text = "当前真实Catalog用例：" + catalogCount +
                    "（此数字不代表正式规格覆盖率）", Margin = new Thickness(0, 0, 0, 15) });
            TCTWorkspaceDescriptor[] children = registry.Children(id).ToArray();
            if (children.Length == 0)
                body.Children.Add(new TextBlock { Text = "尚无已登记的子工作区；不会生成虚假场景或结果。",
                    Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
            foreach (TCTWorkspaceDescriptor child in children)
            {
                bool supported = child.Factory != null;
                Button open = new Button { HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brushes.White, BorderBrush = Brushes.LightSteelBlue,
                    Padding = new Thickness(10, 9, 10, 9), Margin = new Thickness(0, 0, 0, 8),
                    IsEnabled = supported,
                    Content = child.Title + "   ·   " + (supported ? "打开工作区" : "待接入") +
                        "\n" + child.Description };
                System.Windows.Automation.AutomationProperties.SetAutomationId(open,
                    "TCT.Workspace.Entry." + child.WorkspaceId);
                open.Click += (sender, args) => {
                    if (OpenRequested != null) OpenRequested(child.WorkspaceId);
                };
                body.Children.Add(open);
            }
            scroll.Content = body; Content = scroll;
            System.Windows.Automation.AutomationProperties.SetAutomationId(this,
                "TCT.Workspace.Root." + id);
        }
        public void OnWorkspaceClosed() { }
    }

    /// <summary>人工实验台最小真实闭环。只调用生产Virtual Owner；绝不生成TCT Passed。</summary>
}
