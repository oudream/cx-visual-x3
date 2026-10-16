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
    /// Multi-model editor: many declared POCO members, ONE atomic group Commit.
    /// The UI only stages scalar field changes; it cannot create a second Section.
    /// </summary>
    public sealed class MPWConfigAtomicGroupControl : UserControl
    {
        private readonly MHSConfigAtomicGroup _group;
        private readonly IList<MHSConfigGroupMemberInfo> _members;
        private readonly ComboBox _member = new ComboBox { DisplayMemberPath = "Key", Width = 180 };
        private readonly ComboBox _boundary = new ComboBox { Width = 130 };
        private readonly StackPanel _fields = new StackPanel();
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        private readonly Dictionary<string, string> _pending =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _working;
        public int MemberCount { get { return _members.Count; } }
        public int PendingFields { get { return _pending.Count; } }
        public MPWConfigAtomicGroupControl(MHSConfigAtomicGroup group)
        {
            _group = group ?? throw new ArgumentNullException("group");
            _members = group.DescribeMembers();
            foreach (MHSConfigGroupMemberInfo item in _members) _member.Items.Add(item);
            foreach (FCMConfigApplyPolicy value in Enum.GetValues(typeof(FCMConfigApplyPolicy)))
                _boundary.Items.Add(value);
            _boundary.SelectedItem = FCMConfigApplyPolicy.NextTask;
            _member.SelectionChanged += (s, e) => RefreshMember();
            StackPanel outer = new StackPanel { Margin = new Thickness(12) };
            outer.Children.Add(new TextBlock { Text = "多模型原子配置组  " + group.Identity,
                FontSize = 18, FontWeight = FontWeights.SemiBold });
            outer.Children.Add(new TextBlock { Text = "所有成员一次校验、一次 Saved 修订、一次 Effective 生效；不能单独保存某成员。",
                Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 10) });
            WrapPanel actions = new WrapPanel();
            actions.Children.Add(_member);
            actions.Children.Add(Button("放弃草稿", () => { _pending.Clear(); RefreshMember(); }));
            actions.Children.Add(Button("原子校验并保存", SaveAsync));
            actions.Children.Add(_boundary);
            actions.Children.Add(Button("申请整个组生效", Apply));
            actions.Children.Add(Button("刷新", RefreshMember));
            outer.Children.Add(actions);
            outer.Children.Add(_status);
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 600, Content = _fields };
            outer.Children.Add(scroll);
            Content = outer;
            if (_member.Items.Count > 0) _member.SelectedIndex = 0;
            RefreshMember();
        }
        private static Button Button(string caption, Action action)
        {
            Button button = new Button { Content = caption,
                Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(4) };
            button.Click += (s, e) => action();
            return button;
        }
        private static string Id(string key, string path)
        { return key.Length.ToString(CultureInfo.InvariantCulture) + ":" + key + path; }
        private static object Read(object model, string path)
        {
            foreach (string member in path.Split('.'))
            {
                if (model == null) return null;
                PropertyInfo p = model.GetType().GetProperty(member, BindingFlags.Instance | BindingFlags.Public);
                if (p == null) throw new InvalidOperationException("CONFIG_GROUP_UI_UNKNOWN_PATH");
                model = p.GetValue(model, null);
            }
            return model;
        }
        private static string Human(object value)
        { return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture); }
        private void RefreshMember()
        {
            _fields.Children.Clear();
            _status.Text = "Saved " + _group.SavedVersion + "   Effective " + _group.EffectiveVersion +
                (_group.HasPendingApply ? "   [待生效]" : "   [已同步]") +
                "   未保存字段 " + _pending.Count;
            MHSConfigGroupMemberInfo info = _member.SelectedItem as MHSConfigGroupMemberInfo;
            if (info == null) return;
            using (MHSConfigGroupSnapshot saved = _group.CaptureSaved())
            using (MHSConfigGroupSnapshot current = _group.CaptureForTask("WPF-group-preview"))
            {
                string savedJson = saved.GetJson(info.Key), effectiveJson = current.GetJson(info.Key);
                object staged = savedJson == null ? Activator.CreateInstance(info.ModelType) :
                    FCMJsonValueCodec.Deserialize(savedJson, info.ModelType);
                object active = effectiveJson == null ? Activator.CreateInstance(info.ModelType) :
                    FCMJsonValueCodec.Deserialize(effectiveJson, info.ModelType);
                foreach (FCMConfigFieldInfo field in FCMConfigCatalog.Describe(info.ModelType))
                {
                    if (!FCMConfigSchemaBuilder.IsScalar(field.DataType)) continue;
                    if (field.IsSensitive)
                    {
                        _fields.Children.Add(new TextBlock { Text = field.Title + " [敏感字段不可在通用组视图展示]",
                            Margin = new Thickness(5), Foreground = Brushes.Gray });
                        continue;
                    }
                    string key = Id(info.Key, field.Path), changed;
                    object savedValue = Read(staged, field.Path), effectiveValue = Read(active, field.Path);
                    StackPanel row = new StackPanel { Margin = new Thickness(5, 6, 5, 6) };
                    row.Children.Add(new TextBlock { Text = info.Key + "." + field.Path + "    [" +
                        field.DataType.Name + "]", FontWeight = FontWeights.SemiBold });
                    TextBox edit = new TextBox { Text = _pending.TryGetValue(key, out changed) ?
                        changed : Human(savedValue), MinWidth = 230, Margin = new Thickness(2, 4, 2, 2) };
                    edit.LostFocus += (s, e) =>
                    {
                        if (_working) return;
                        try
                        {
                            StageField(info.Key, field.Path, edit.Text);
                            _status.Text = "草稿字段已修改；请一次性原子校验并保存。";
                        }
                        catch (Exception ex)
                        { _status.Text = "字段格式错误：" + ex.GetType().Name; }
                    };
                    row.Children.Add(edit);
                    row.Children.Add(new TextBlock {
                        Text = "Saved: " + Human(savedValue) + "   Effective: " + Human(effectiveValue),
                        Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap });
                    _fields.Children.Add(row);
                }
            }
        }
        public void StageField(string memberKey, string path, string raw)
        {
            if (_working) throw new InvalidOperationException("CONFIG_GROUP_UI_BUSY");
            MHSConfigGroupMemberInfo member = _members.SingleOrDefault(x => x.Key == memberKey);
            if (member == null) throw new KeyNotFoundException("CONFIG_GROUP_UI_UNKNOWN_MEMBER");
            FCMConfigFieldInfo field = FCMConfigCatalog.Describe(member.ModelType)
                .SingleOrDefault(x => x.Path == path);
            if (field == null || field.IsSensitive ||
                !FCMConfigSchemaBuilder.IsScalar(field.DataType))
                throw new InvalidOperationException("CONFIG_GROUP_UI_FIELD_NOT_EDITABLE");
            string json = MPWConfigEditorControl<MHSConfigGroupDocument>.ToConfigJson(raw, field.DataType);
            FCMJsonValueCodec.Deserialize(json, field.DataType);
            _pending[Id(memberKey, path)] = raw;
        }
        public async Task<MHSConfigCommitResult> SaveGroupDraftAsync(CancellationToken token)
        {
            if (_working) throw new InvalidOperationException("CONFIG_GROUP_UI_BUSY");
            if (_pending.Count == 0) return null;
            _working = true;
            try
            {
                var pending = _pending.ToDictionary(x => x.Key, x => x.Value);
                var operations = new List<Tuple<string, string, string>>();
                foreach (MHSConfigGroupMemberInfo member in _members)
                    foreach (FCMConfigFieldInfo field in FCMConfigCatalog.Describe(member.ModelType))
                    {
                        string raw;
                        if (!pending.TryGetValue(Id(member.Key, field.Path), out raw)) continue;
                        string json = MPWConfigEditorControl<MHSConfigGroupDocument>
                            .ToConfigJson(raw, field.DataType);
                        operations.Add(Tuple.Create(member.Key, field.Path, json));
                    }
                MHSConfigCommitResult result = await _group.UpdateAsync(edit =>
                {
                    foreach (var op in operations)
                        edit.SetMemberPathJson(op.Item1, op.Item2, op.Item3);
                }, "WPF 多模型一次提交", token);
                if (result.Success) _pending.Clear();
                RefreshMember();
                _status.Text = result.Success ? "原子组已保存 " + result.Revision +
                    "；仍需按边界生效。" : "原子组校验或版本冲突：" + result.ErrorCode;
                return result;
            }
            finally { _working = false; }
        }
        private async void SaveAsync()
        {
            if (_working || _pending.Count == 0) return;
            try { await SaveGroupDraftAsync(CancellationToken.None); }
            catch (Exception ex) { _status.Text = "原子组未保存：" + ex.GetType().Name; }
        }
        private void Apply()
        {
            if (_working) return;
            try
            {
                MHSConfigApplyResult result = _group.TryApplyPendingAtBoundary(
                    (FCMConfigApplyPolicy)_boundary.SelectedItem, null);
                RefreshMember();
                _status.Text = result.Applied ? "整组已生效：" + result.EffectiveVersion :
                    "整组未生效：" + result.Status;
            }
            catch (Exception ex) { _status.Text = "生效失败：" + ex.GetType().Name; }
        }
    }

    public static class MPWConfigAtomicGroupDialog
    {
        public static void Show(Window owner, MHSConfigAtomicGroup group)
        {
            Window window = new Window {
                Width = 950, Height = 750, MinWidth = 650, MinHeight = 450,
                Title = "CxVisual · 多模型原子配置组",
                Content = new MPWConfigAtomicGroupControl(group),
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen :
                    WindowStartupLocation.CenterOwner
            };
            if (owner != null) window.Owner = owner;
            window.ShowDialog();
        }
    }
}
