using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>
    /// Reusable read/explain/edit layer view. The physical commit semantics remain
    /// those of the supplied profile (independent layers versus one atomic group).
    /// </summary>
    public sealed class MPWConfigLayerOverview<T> : UserControl where T : new()
    {
        private readonly string _profileId;
        private readonly IList<MVBConfigLayerSpec> _layers;
        private readonly Func<MVBConfigResolved<T>> _effective;
        private readonly Func<MVBConfigResolved<T>> _saved;
        private readonly Func<string, string, string, CancellationToken, Task<MHSConfigCommitResult>> _write;
        private readonly Func<string, string, CancellationToken, Task<MHSConfigCommitResult>> _remove;
        private readonly Func<string, FCMConfigApplyPolicy, MHSConfigApplyResult> _apply;
        private readonly ComboBox _layer = new ComboBox { Width = 160, DisplayMemberPath = "Name" };
        private readonly ComboBox _field = new ComboBox { Width = 260, DisplayMemberPath = "Path" };
        private readonly ComboBox _boundary = new ComboBox { Width = 130 };
        private readonly TextBox _value = new TextBox { Width = 200, Margin = new Thickness(5) };
        private readonly StackPanel _rows = new StackPanel();
        private readonly TextBlock _notice = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateBlue };
        private bool _busy;
        private readonly bool _atomic;

        public string ProfileId { get { return _profileId; } }
        public int DisplayedSourceCount { get { return _layers.Count; } }

        public MPWConfigLayerOverview(MVBConfigAtomicLayerProfile<T> profile)
            : this(profile.ProfileId, profile.Layers, profile.ResolveEffective, profile.ResolveSaved,
                (layer, path, json, token) => profile.SetOverridePathJsonAsync(layer, path, json, token),
                (layer, path, token) => profile.RemoveOverridePathAsync(layer, path, token),
                (layer, boundary) => profile.TryApplyPendingAtBoundary(boundary, null), true) { }

        public MPWConfigLayerOverview(MVBConfigLayeredProfile<T> profile)
            : this(profile.ProfileId, profile.Layers, profile.ResolveEffective, null,
                (layer, path, json, token) => profile.SetOverridePathJsonAsync(layer, path, json, token),
                (layer, path, token) => profile.RemoveOverridePathAsync(layer, path, token),
                (layer, boundary) => profile.GetLayer(layer).TryApplyPendingAtBoundary(boundary, null), false) { }

        private MPWConfigLayerOverview(string id, IList<MVBConfigLayerSpec> layers,
            Func<MVBConfigResolved<T>> effective, Func<MVBConfigResolved<T>> saved,
            Func<string, string, string, CancellationToken, Task<MHSConfigCommitResult>> write,
            Func<string, string, CancellationToken, Task<MHSConfigCommitResult>> remove,
            Func<string, FCMConfigApplyPolicy, MHSConfigApplyResult> apply, bool atomic)
        {
            _profileId = id; _layers = layers; _effective = effective; _saved = saved;
            _write = write; _remove = remove; _apply = apply; _atomic = atomic;
            foreach (MVBConfigLayerSpec layer in layers) _layer.Items.Add(layer);
            _layer.SelectedIndex = 0;
            foreach (FCMConfigFieldInfo field in FCMConfigCatalog.Describe<T>())
                if (FCMConfigSchemaBuilder.IsScalar(field.DataType) && !field.IsSensitive)
                    _field.Items.Add(field);
            _field.SelectedIndex = _field.Items.Count == 0 ? -1 : 0;
            _field.SelectionChanged += (s, e) => RefreshSelectedValue();
            foreach (FCMConfigApplyPolicy policy in Enum.GetValues(typeof(FCMConfigApplyPolicy)))
                _boundary.Items.Add(policy);
            _boundary.SelectedItem = FCMConfigApplyPolicy.NextTask;
            Build();
            RefreshOverview();
        }
        private static Button Button(string text, Action action)
        {
            Button button = new Button { Content = text, Margin = new Thickness(3),
                Padding = new Thickness(9, 5, 9, 5), MinHeight = 28 };
            button.Click += (s, e) => action();
            return button;
        }
        private void Build()
        {
            DockPanel root = new DockPanel { Margin = new Thickness(14) };
            StackPanel top = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            top.Children.Add(new TextBlock { Text = "配置档案  " + _profileId,
                FontSize = 19, FontWeight = FontWeights.SemiBold });
            top.Children.Add(new TextBlock {
                Text = _atomic ? "原子层档案：全层共享一个 Saved/Effective 修订" :
                    "独立层档案：每层保存和生效彼此独立；不提供跨层原子承诺",
                Foreground = Brushes.SlateGray, Margin = new Thickness(0, 4, 0, 7) });
            WrapPanel commands = new WrapPanel { Orientation = Orientation.Horizontal };
            commands.Children.Add(new TextBlock { Text = "覆盖来源", VerticalAlignment = VerticalAlignment.Center });
            commands.Children.Add(_layer);
            commands.Children.Add(new TextBlock { Text = "字段", VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0) });
            commands.Children.Add(_field);
            commands.Children.Add(_value);
            commands.Children.Add(Button("设置覆盖", () => SetAsync()));
            commands.Children.Add(Button("移除覆盖", () => RemoveAsync()));
            commands.Children.Add(_boundary);
            commands.Children.Add(Button("申请生效", Apply));
            commands.Children.Add(Button("刷新", RefreshOverview));
            top.Children.Add(commands);
            _notice.Margin = new Thickness(3, 8, 3, 3);
            top.Children.Add(_notice);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = _rows;
            root.Children.Add(scroll);
            Content = root;
        }
        private static object ReadPath(object model, string path)
        {
            object current = model;
            foreach (string name in path.Split('.'))
            {
                if (current == null) return null;
                PropertyInfo property = current.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property == null) throw new InvalidOperationException("CONFIG_LAYER_UI_PATH_UNSUPPORTED");
                current = property.GetValue(current, null);
            }
            return current;
        }
        private static string ValueText(object obj)
        {
            if (obj == null) return "(null)";
            return Convert.ToString(obj, CultureInfo.InvariantCulture);
        }
        private void RefreshSelectedValue()
        {
            FCMConfigFieldInfo field = _field.SelectedItem as FCMConfigFieldInfo;
            if (field == null) return;
            try
            {
                MVBConfigResolved<T> source = _saved == null ? _effective() : _saved();
                object val = ReadPath(source.Get(), field.Path);
                _value.Text = val == null ? "" : ValueText(val);
            }
            catch (Exception)
            { _value.Text = ""; }
        }
        public void RefreshOverview()
        {
            _rows.Children.Clear();
            MVBConfigResolved<T> effective = _effective();
            MVBConfigResolved<T> saved = _saved == null ? null : _saved();
            foreach (MVBConfigLayerSpec layer in _layers)
            {
                string version;
                effective.Versions.TryGetValue(layer.Name, out version);
                _rows.Children.Add(new TextBlock {
                    Text = layer.Name + "  [" + layer.Kind + "/" + layer.Id + "]    优先级 " + layer.Priority +
                        "    生效版本 " + version,
                    FontSize = 14, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(2, 9, 2, 4), Foreground = Brushes.DarkSlateBlue });
            }
            _rows.Children.Add(new TextBlock { Text = "字段值与最终覆盖来源",
                FontWeight = FontWeights.Bold, Margin = new Thickness(3, 16, 3, 8) });
            foreach (FCMConfigFieldInfo field in FCMConfigCatalog.Describe<T>())
            {
                if (!FCMConfigSchemaBuilder.IsScalar(field.DataType)) continue;
                bool sensitive = field.IsSensitive;
                string active = sensitive ? "[敏感字段]" :
                    ValueText(ReadPath(effective.Get(), field.Path));
                string staged = saved == null ? "（独立层：分别保存）" :
                    (sensitive ? "[敏感字段]" : ValueText(ReadPath(saved.Get(), field.Path)));
                _rows.Children.Add(new TextBlock {
                    Text = field.Path + "  | Effective: " + active + "  | Saved: " + staged +
                        "  | 来源: " + effective.SourceOf(field.Path),
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 5, 4, 5) });
            }
            RefreshSelectedValue();
        }
        private async void SetAsync()
        {
            if (_busy || _layer.SelectedItem == null || _field.SelectedItem == null) return;
            _busy = true;
            try
            {
                string name = ((MVBConfigLayerSpec)_layer.SelectedItem).Name;
                FCMConfigFieldInfo field = (FCMConfigFieldInfo)_field.SelectedItem;
                string json = MPWConfigEditorControl<T>.ToConfigJson(_value.Text, field.DataType);
                MHSConfigCommitResult result = await _write(name, field.Path, json, CancellationToken.None);
                _notice.Text = result.Success ? "覆盖保存成功；请按实际边界生效。" : "覆盖保存被拒绝：" + result.ErrorCode;
                RefreshOverview();
            }
            catch (Exception ex) { _notice.Text = "覆盖未保存：" + ex.GetType().Name; }
            finally { _busy = false; }
        }
        private async void RemoveAsync()
        {
            if (_busy || _layer.SelectedItem == null || _field.SelectedItem == null) return;
            _busy = true;
            try
            {
                MHSConfigCommitResult result = await _remove(
                    ((MVBConfigLayerSpec)_layer.SelectedItem).Name,
                    ((FCMConfigFieldInfo)_field.SelectedItem).Path, CancellationToken.None);
                _notice.Text = result.Success ? "覆盖已移除（Saved）；需按边界生效。" :
                    "移除覆盖被拒绝：" + result.ErrorCode;
                RefreshOverview();
            }
            catch (Exception ex) { _notice.Text = "移除未完成：" + ex.GetType().Name; }
            finally { _busy = false; }
        }
        private void Apply()
        {
            if (_busy || _layer.SelectedItem == null) return;
            try
            {
                MHSConfigApplyResult result = _apply(((MVBConfigLayerSpec)_layer.SelectedItem).Name,
                    (FCMConfigApplyPolicy)_boundary.SelectedItem);
                _notice.Text = result.Applied ? "已生效 " + result.EffectiveVersion :
                    "尚未生效：" + result.Status;
                RefreshOverview();
            }
            catch (Exception ex) { _notice.Text = "生效未完成：" + ex.GetType().Name; }
        }
    }

    public static class MPWConfigLayerDialog
    {
        public static void Show<T>(Window owner, MVBConfigAtomicLayerProfile<T> profile) where T : new()
        { Show(owner, new MPWConfigLayerOverview<T>(profile)); }
        public static void Show<T>(Window owner, MVBConfigLayeredProfile<T> profile) where T : new()
        { Show(owner, new MPWConfigLayerOverview<T>(profile)); }
        private static void Show(Window owner, UserControl control)
        {
            Window dialog = new Window { Title = "CxVisual · 配置层级与来源", Width = 1100, Height = 760,
                MinWidth = 750, MinHeight = 480, Content = control,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen :
                    WindowStartupLocation.CenterOwner };
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }
    }
}
