using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Data;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>
    /// L1公共WPF配置编辑器：从M04元数据自动创建布尔、枚举、数值和文本控件。
    /// 仅修改草案；按保存时预期Revision受控提交并持久化。复杂列表/自定义对象可由专业编辑器扩展。
    /// </summary>
    public static class MPWConfigEditorDialog
    {
        public static bool Edit<T>(Window owner, MTCConfigManager manager, string id) where T : class
        {
            return CreateWindow<T>(owner, manager, id).ShowDialog() == true;
        }

        /// <summary>生成可独立检查和自动化测试的配置编辑窗口，Edit仍为便利入口。</summary>
        public static Window CreateWindow<T>(Window owner, MTCConfigManager manager, string id) where T : class
        {
            if (manager == null) throw new ArgumentNullException("manager");
            T draft = manager.Get<T>(id);
            long revision = manager.GetRevision(id);
            MPMConfigProperty[] descriptors = manager.Describe<T>(id);
            Window window = new Window
            {
                Title = "配置编辑 · " + id,
                Width = 660, Height = 600,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                MinHeight = 350, MinWidth = 460,
                Background = Brushes.White
            };
            if (owner != null) window.Owner = owner;
            DockPanel layout = new DockPanel { Margin = new Thickness(16) };
            window.Content = layout;

            TextBlock summary = new TextBlock
            {
                Text = id + "   |   已加载版本 v" + revision,
                FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.DarkSlateBlue, Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(summary, Dock.Top);
            layout.Children.Add(summary);

            StackPanel actions = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 10, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);
            layout.Children.Add(actions);

            TextBlock error = new TextBlock
            {
                Name = "ConfigL1_Error",
                Foreground = Brushes.Firebrick,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            actions.Children.Add(error);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button save = new Button { Name = "ConfigL1_Save", Content = "验证并保存", Width = 112, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8) };
            Button cancel = new Button { Name = "ConfigL1_Cancel", Content = "取消", Width = 80, Padding = new Thickness(8) };
            buttons.Children.Add(save);
            buttons.Children.Add(cancel);
            actions.Children.Add(buttons);
            cancel.Click += (sender, e) => { window.DialogResult = false; window.Close(); };

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel fields = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            scroll.Content = fields;
            layout.Children.Add(scroll);

            Dictionary<MPMConfigProperty, FrameworkElement> inputs = new Dictionary<MPMConfigProperty, FrameworkElement>();
            string lastCategory = null;
            foreach (MPMConfigProperty property in descriptors)
            {
                if (property.Category != lastCategory)
                {
                    lastCategory = property.Category;
                    fields.Children.Add(new TextBlock
                    {
                        Text = lastCategory, FontWeight = FontWeights.Bold,
                        Foreground = Brushes.SteelBlue, Margin = new Thickness(0, 12, 0, 8)
                    });
                }
                Grid line = new Grid { Margin = new Thickness(0, 0, 0, 9) };
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TextBlock caption = new TextBlock
                {
                    Text = property.Title + (string.IsNullOrWhiteSpace(property.Unit) ? "" : " (" + property.Unit + ")"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    ToolTip = property.Description
                };
                line.Children.Add(caption);
                object old = property.Property.GetValue(draft, null);
                FrameworkElement control;
                Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (targetType == typeof(bool))
                {
                    control = new CheckBox { IsChecked = old != null && (bool)old, VerticalAlignment = VerticalAlignment.Center };
                }
                else if (targetType.IsEnum)
                {
                    control = new ComboBox
                    {
                        ItemsSource = Enum.GetValues(targetType),
                        SelectedItem = old, MinWidth = 150, Padding = new Thickness(4)
                    };
                }
                else if (targetType == typeof(string) || IsNumber(targetType))
                {
                    control = new TextBox
                    {
                        Text = old == null ? "" : Convert.ToString(old, CultureInfo.CurrentCulture),
                        Padding = new Thickness(5),
                        ToolTip = "范围：" + property.Minimum + "～" + property.Maximum
                    };
                }
                else
                {
                    // 对象/列表/字典统一生成可编辑JSON文本，供开发者快速调试；
                    // 仍由通用Manager进行类型转换、专业校验、乐观并发提交。
                    control = new TextBox
                    {
                        Text = old == null ? "null" : FCMConfigJsonFile.Serialize(old),
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        MinHeight = 80,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Padding = new Thickness(5),
                        ToolTip = "复杂配置JSON编辑；不符合类型或业务规则的内容不会保存"
                    };
                }
                control.Tag = property.Name;
                Grid.SetColumn(control, 1);
                line.Children.Add(control);
                fields.Children.Add(line);
                inputs.Add(property, control);
            }

            save.Click += (sender, e) =>
            {
                try
                {
                    foreach (KeyValuePair<MPMConfigProperty, FrameworkElement> item in inputs)
                    {
                        PropertyInfo property = item.Key.Property;
                        FrameworkElement control = item.Value;
                        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        object value;
                        CheckBox box = control as CheckBox;
                        ComboBox combo = control as ComboBox;
                        TextBox text = control as TextBox;
                        if (box != null) value = box.IsChecked == true;
                        else if (combo != null) value = combo.SelectedItem;
                        else if (text != null)
                        {
                            string input = text.Text;
                            if (type == typeof(string)) value = input;
                            else if (string.IsNullOrWhiteSpace(input) && Nullable.GetUnderlyingType(property.PropertyType) != null)
                                value = null;
                            else if (!IsNumber(type))
                                value = FCMConfigJsonFile.Deserialize(input, property.PropertyType);
                            else value = Convert.ChangeType(input, type, CultureInfo.CurrentCulture);
                        }
                        else continue; // 不支持的复杂属性原样保留
                        property.SetValue(draft, value, null);
                    }
                    manager.UpdateAndSave<T>(id, updated =>
                    {
                        foreach (MPMConfigProperty property in descriptors)
                            property.Property.SetValue(updated, property.Property.GetValue(draft, null), null);
                    }, revision);
                    window.DialogResult = true;
                    window.Close();
                }
                catch (Exception ex) { error.Text = "保存失败，已保留原有效版本：" + ex.Message; }
            };
            return window;
        }

        private static bool IsNumber(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.Int16:
                case TypeCode.UInt16: case TypeCode.Int32: case TypeCode.UInt32:
                case TypeCode.Int64: case TypeCode.UInt64: case TypeCode.Single:
                case TypeCode.Double: case TypeCode.Decimal: return true;
                default: return false;
            }
        }
    }
}
