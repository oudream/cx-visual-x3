using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>
    /// Studio UI projection only: the one authoritative configuration backend lives in Mechanism.Data.
    /// Scalar values use the type codec; complex objects may be edited as JSON.
    /// </summary>
    public static class MPWConfigEditorDialog
    {
        public static bool Edit<T>(Window owner, MHSConfigSection<T> config) where T : new()
        {
            if (config == null) throw new ArgumentNullException("config");
            using (MHSConfigEdit<T> edit = config.BeginEdit("Studio 配置编辑"))
            {
                Window view = new Window
                {
                    Owner = owner, Width = 680, Height = 650, MinWidth = 480, MinHeight = 350,
                    Title = typeof(T).Name + " — 配置编辑", WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                DockPanel shell = new DockPanel { Margin = new Thickness(12) };
                StackPanel footer = new StackPanel
                { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                DockPanel.SetDock(footer, Dock.Bottom);
                Button save = new Button { Content = "校验并保存", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(5) };
                Button cancel = new Button { Content = "取消", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(5) };
                footer.Children.Add(save); footer.Children.Add(cancel);
                shell.Children.Add(footer);
                ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                StackPanel form = new StackPanel { Orientation = Orientation.Vertical };
                scroll.Content = form;
                shell.Children.Add(scroll);
                view.Content = shell;
                List<Tuple<FCMConfigMemberDescriptor, TextBox>> editors =
                    new List<Tuple<FCMConfigMemberDescriptor, TextBox>>();
                foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Describe<T>().Members)
                {
                    form.Children.Add(new TextBlock
                    {
                        Text = member.Label + (string.IsNullOrEmpty(member.Unit) ? "" : " (" + member.Unit + ")"),
                        Margin = new Thickness(0, 8, 0, 3), FontWeight = FontWeights.SemiBold
                    });
                    object old = member.Property.GetValue(edit.WorkingCopy, null);
                    TextBox field = new TextBox
                    {
                        Text = member.MemberType == typeof(string) ? (string)old ?? "" : FCMJsonValueCodec.Serialize(old),
                        MinHeight = 26, Padding = new Thickness(5),
                        ToolTip = "复杂对象使用 JSON；字符串直接输入文本。"
                    };
                    form.Children.Add(field);
                    editors.Add(Tuple.Create(member, field));
                }
                cancel.Click += (sender, e) => view.DialogResult = false;
                save.Click += (sender, e) =>
                {
                    try
                    {
                        foreach (Tuple<FCMConfigMemberDescriptor, TextBox> pair in editors)
                        {
                            string raw = pair.Item1.MemberType == typeof(string)
                                ? FCMJsonValueCodec.Serialize(pair.Item2.Text) : pair.Item2.Text;
                            object parsed = FCMJsonValueCodec.Deserialize(raw, pair.Item1.MemberType);
                            pair.Item1.Property.SetValue(edit.WorkingCopy, parsed, null);
                        }
                        MHSConfigPreview preview = edit.Preview();
                        if (!preview.Validation.IsValid)
                        {
                            List<string> errors = new List<string>();
                            foreach (FCMConfigIssue issue in preview.Validation.Issues)
                                errors.Add(issue.Path + ": " + issue.Message);
                            MessageBox.Show(view, string.Join(Environment.NewLine, errors),
                                "配置验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        MHSConfigCommitResult result = edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
                        if (!result.Success)
                            throw new InvalidOperationException("Commit failed: " + result.ErrorCode);
                        view.DialogResult = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(view, ex.Message, "无法保存配置", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                return view.ShowDialog() == true;
            }
        }
    }
}
