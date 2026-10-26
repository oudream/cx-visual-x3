using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>
    /// 与业务对象无关的WPF可视化能力。仅提供主题语义/矢量树标题；
    /// Studio与TestCenter各自保留MEM/测试实例、右键菜单和Tab生命周期。
    /// </summary>
    public static class MPWWorkspaceVisuals
    {
        public static void ApplyTheme(ResourceDictionary resources, bool dark)
        {
            if (resources == null) throw new ArgumentNullException("resources");
            SetBrush(resources, "MPWBackgroundBrush", dark ? "#25272D" : "#F2F6FB");
            SetBrush(resources, "MPWSurfaceBrush", dark ? "#303137" : "#FFFFFF");
            SetBrush(resources, "MPWSurfaceAltBrush", dark ? "#373B42" : "#EAF1F9");
            SetBrush(resources, "MPWForegroundBrush", dark ? "#E6E9EF" : "#243B58");
            SetBrush(resources, "MPWMutedBrush", dark ? "#B1BACA" : "#647D99");
            SetBrush(resources, "MPWAccentBrush", dark ? "#85BFFF" : "#2477CB");
            SetBrush(resources, "MPWBorderBrush", dark ? "#535B68" : "#C9D9E9");
            SetBrush(resources, "MPWHoverBrush", dark ? "#36495E" : "#DEEDFF");
            SetBrush(resources, "MPWSelectionBrush", dark ? "#35516F" : "#D8EAFE");
            SetBrush(resources, "MPWSuccessBrush", dark ? "#72CE9A" : "#14804A");
            SetBrush(resources, "MPWWarningBrush", dark ? "#F3BA58" : "#AF7619");
            SetBrush(resources, "MPWErrorBrush", dark ? "#FA8181" : "#C63838");
        }

        private static void SetBrush(ResourceDictionary resources, string key, string hex)
        {
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public static object TreeHeader(string label, string role, bool instance, bool root)
        {
            if (label == null) throw new ArgumentNullException("label");
            // 使用WPF Geometry而不是操作系统图标字体；避免系统语言/缺字形带来的方框。
            string shape;
            switch ((role ?? "").ToLowerInvariant())
            {
                case "project": case "module": case "resource": case "group":
                    shape = "M3,7 L9,7 11,10 21,10 21,20 3,20 Z M3,7 L3,5 10,5 12,7";
                    break;
                case "channel": case "connector": case "network":
                    shape = "M12,2 L12,8 M4,8 L20,8 M4,8 L4,14 M20,8 L20,14 M1,14 L8,14 8,21 1,21 Z M16,14 L23,14 23,21 16,21 Z M8,2 L16,2 16,7 8,7 Z";
                    break;
                case "compute": case "simulation": case "run":
                    shape = "M5,3 L19,12 5,21 Z";
                    break;
                case "report": case "test": case "catalog":
                    shape = "M5,2 L16,2 20,6 20,22 5,22 Z M9,9 L16,9 M9,13 L16,13 M9,17 L15,17";
                    break;
                case "3d": case "vision": case "viewer":
                    shape = "M12,2 L21,7 21,17 12,22 3,17 3,7 Z M3,7 L12,12 21,7 M12,12 L12,22";
                    break;
                case "service": case "command": case "settings":
                    shape = "M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M9,4 L9,8 M16,10 L16,14 M9,16 L9,20";
                    break;
                default:
                    shape = "M4,4 L20,4 20,20 4,20 Z M8,9 L16,9 M8,13 L16,13";
                    break;
            }
            Geometry geometry = Geometry.Parse(shape);
            StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
            Path icon = new Path {
                Data = geometry, Stretch = Stretch.Uniform, Height = 15, Width = 16,
                StrokeThickness = 1.6, Fill = Brushes.Transparent,
                StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(Shape.StrokeProperty,
                instance || root ? "MPWAccentBrush" : "MPWMutedBrush");
            TextBlock name = new TextBlock {
                Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = instance || root ? FontWeights.SemiBold : FontWeights.Normal
            };
            name.SetResourceReference(TextBlock.ForegroundProperty, "MPWForegroundBrush");
            stack.Children.Add(icon);
            stack.Children.Add(name);
            return stack;
        }
    }
}
