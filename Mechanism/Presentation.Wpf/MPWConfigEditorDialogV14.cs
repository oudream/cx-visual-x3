using System;
using System.Collections;
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
    /// Reusable schema-driven editor for trusted WPF hosts. All writes go through
    /// the existing MHSConfigEditorModel and its single saved/effective truth.
    /// Never hand a raw Section or this trusted control to untrusted plugins.
    /// </summary>
    /// <summary>通用配置视图的未保存草稿标记，供宿主安全切换上下文。</summary>
    public interface IMPWConfigDraftGuard { bool HasUnsavedDraft { get; } }

    public sealed class MPWConfigEditorControl<T> : UserControl, IDisposable, IMPWConfigDraftGuard where T : new()
    {
        private readonly MHSConfigSection<T> _section;
        private readonly MHSConfigAuthorizedSection<T> _authorized;
        private MHSConfigEditorModel<T> _editor;
        private readonly StackPanel _form = new StackPanel();
        private StackPanel _shellHeading;
        private WrapPanel _shellCommands;
        private TabControl _shellTabs;
        private bool _workspaceCompact;
        private HashSet<string> _visibleRootFields;
        private HashSet<string> _nestedCategories;
        /// <summary>
        /// Schema-only presentation projection for embedded workspaces.
        /// Values and edits still belong to this one MHSConfigEditorModel.
        /// </summary>
        public void ConfigureWorkspaceProjection(IEnumerable<string> rootFields,
            IEnumerable<string> nestedCategories = null, bool compact = true)
        {
            EnsureActive();
            _visibleRootFields = rootFields == null ? null :
                new HashSet<string>(rootFields, StringComparer.Ordinal);
            _nestedCategories = nestedCategories == null ? null :
                new HashSet<string>(nestedCategories, StringComparer.Ordinal);
            _workspaceCompact = compact;
            if (_shellHeading != null) _shellHeading.Visibility =
                compact ? Visibility.Collapsed : Visibility.Visible;
            if (_shellCommands != null) _shellCommands.Visibility =
                compact ? Visibility.Collapsed : Visibility.Visible;
            if (_issues != null) _issues.Visibility =
                compact ? Visibility.Collapsed : Visibility.Visible;
            if (_shellTabs != null)
            {
                _shellTabs.TabStripPlacement = compact ? Dock.Bottom : Dock.Top;
                if (_shellTabs.Items.Count > 1)
                    ((TabItem)_shellTabs.Items[1]).Visibility =
                        compact ? Visibility.Collapsed : Visibility.Visible;
                _shellTabs.SelectedIndex = 0;
            }
            RefreshAll();
        }
        public int VisibleWorkspaceEditorCount { get { return _count; } }
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _issues = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };
        private readonly ComboBox _boundaries = new ComboBox { Width = 120, Margin = new Thickness(5) };
        private readonly ComboBox _history = new ComboBox { Width = 110, Margin = new Thickness(5) };
        private readonly Dictionary<string, Func<string, Action<string>, FrameworkElement>> _custom =
            new Dictionary<string, Func<string, Action<string>, FrameworkElement>>(StringComparer.Ordinal);
        private bool _disposed;
        private bool _working;
        private bool _saved;
        private int _count;
        private IDictionary<string, MHSConfigEditorField> _fieldStates;
        private readonly Dictionary<string, int> _visibleCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private const int PageSize = 150;
        public bool WasSaved { get { return _saved; } }
        public bool HasUnsavedDraft { get { return !_disposed && _editor != null && _editor.CanUndo; } }
        public T Draft { get { EnsureActive(); return _editor.Draft; } }
        public int VisibleEditorCount { get { return _count; } }
        public MPWConfigEditorControl(MHSConfigSection<T> section)
        {
            _section = section ?? throw new ArgumentNullException("section");
            _editor = NewEditor();
            InitializeControls();
        }
        /// <summary>Authorized mode keeps raw Section private to the Mechanism assembly.
        /// Every edit, status, restore and apply action is mediated by its session.</summary>
        public MPWConfigEditorControl(MHSConfigAuthorizedSection<T> authorized)
        {
            _authorized = authorized ?? throw new ArgumentNullException("authorized");
            _editor = NewEditor();
            InitializeControls();
        }
        private MHSConfigEditorModel<T> NewEditor()
        {
            return _authorized == null ?
                new MHSConfigEditorModel<T>(_section, "通用配置编辑") :
                _authorized.BeginEditor("授权配置编辑");
        }
        private FCMConfigIdentity ModelIdentity
        { get { return _authorized == null ? _section.Identity : _authorized.Identity; } }
        private FCMConfigRevision ModelRevision
        { get { return _authorized == null ? _section.Revision : _authorized.Revision; } }
        private IList<string> Versions()
        {
            return _authorized == null ? _section.ListSavedVersions() : _authorized.ListSavedVersions();
        }
        private MHSConfigStatus Status()
        { return _authorized == null ? _section.GetStatus() : _authorized.GetStatus(); }
        private MHSConfigApplyResult TryApply(FCMConfigApplyPolicy boundary)
        {
            return _authorized == null ? _section.TryApplyPendingAtBoundary(boundary, null) :
                _authorized.TryApplyPendingAtBoundary(boundary);
        }
        private Task<MHSConfigCommitResult> Restore(string version, CancellationToken token)
        {
            return _authorized == null ?
                _section.RestoreSavedVersionAsync(version, "Studio 历史恢复", token) :
                _authorized.RestoreSavedVersionAsync(version, "授权历史恢复", token);
        }
        private void InitializeControls()
        {
            foreach (FCMConfigApplyPolicy policy in Enum.GetValues(typeof(FCMConfigApplyPolicy)))
                _boundaries.Items.Add(policy);
            _boundaries.SelectedItem = FCMConfigApplyPolicy.NextTask;
            BuildShell();
            RefreshAll();
            Unloaded += (s, e) => Dispose();
        }
        /// <summary>Professional editors return a WPF element and accept a JSON setter.
        /// The setter still performs typed validation and edits only the current draft.</summary>
        public void RegisterEditor(string path, Func<string, Action<string>, FrameworkElement> factory)
        {
            EnsureActive();
            if (string.IsNullOrWhiteSpace(path) || factory == null) throw new ArgumentException("Editor required");
            _custom[path] = factory;
            RefreshAll();
        }
        private static Button MakeButton(string title, Action action)
        {
            Button button = new Button { Content = title, Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(3), MinHeight = 27 };
            button.Click += (s, e) => action();
            return button;
        }
        private void BuildShell()
        {
            DockPanel root = new DockPanel { Margin = new Thickness(12) };
            StackPanel header = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            _shellHeading = header;
            header.Children.Add(new TextBlock { Text = FCMConfigSchemaBuilder.Describe<T>().SectionId,
                FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue });
            header.Children.Add(_status);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            WrapPanel commands = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
            _shellCommands = commands;
            commands.Children.Add(MakeButton("撤销", () => Execute(() => _editor.Undo())));
            commands.Children.Add(MakeButton("重做", () => Execute(() => _editor.Redo())));
            commands.Children.Add(MakeButton("预览与验证", () => PreviewAsync()));
            commands.Children.Add(MakeButton("校验并保存", () => SaveAsync()));
            commands.Children.Add(_boundaries);
            commands.Children.Add(MakeButton("申请生效", Apply));
            commands.Children.Add(new TextBlock { Text = "历史版本", VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0) });
            commands.Children.Add(_history);
            commands.Children.Add(MakeButton("恢复为新修订", () => RestoreAsync()));
            DockPanel.SetDock(commands, Dock.Top);
            root.Children.Add(commands);

            _issues.Margin = new Thickness(0, 3, 0, 8);
            DockPanel.SetDock(_issues, Dock.Bottom);
            root.Children.Add(_issues);

            TabControl tabs = new TabControl();
            _shellTabs = tabs;
            tabs.Items.Add(new TabItem { Header = "参数及动态集合", Content = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _form } });
            StackPanel detail = new StackPanel { Margin = new Thickness(12) };
            detail.Children.Add(new TextBlock { Text = "Saved / Effective / 审计诊断",
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            Button inspect = MakeButton("刷新诊断", () => ShowDiagnostics(detail));
            detail.Children.Add(inspect);
            tabs.Items.Add(new TabItem { Header = "版本、历史与诊断",
                Content = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            tabs.SelectionChanged += (s, e) => { if (tabs.SelectedIndex == 1) ShowDiagnostics(detail); };
            root.Children.Add(tabs);
            Content = root;
        }
        private void ShowDiagnostics(StackPanel host)
        {
            if (_disposed || host.Children.Count < 2) return;
            while (host.Children.Count > 2) host.Children.RemoveAt(2);
            try
            {
                MHSConfigStatus status = Status();
                FCMConfigRevision revision = ModelRevision;
                host.Children.Add(new TextBlock { Text = "Saved: " + revision.Saved +
                    "    Effective: " + revision.Effective + "    Pending: " + revision.IsPending +
                    "    Audit entries: " + status.AuditCount, TextWrapping = TextWrapping.Wrap });
                host.Children.Add(new TextBlock { Text = "完整性: " + (status.IsHealthy ? "通过" : "需要检查") +
                    "    诊断问题: " + status.IntegrityIssues.Count,
                    Margin = new Thickness(0, 8, 0, 2) });
                if (_authorized == null)
                    foreach (MHSConfigAuditEntry audit in _section.ListAudit().Reverse().Take(50))
                        host.Children.Add(new TextBlock { Text = audit.Revision + "    " +
                            audit.RecordedAtUtc.ToLocalTime().ToString("g") +
                            "    字段数: " + audit.ChangedPaths.Count,
                            Margin = new Thickness(0, 4, 0, 0) });
                else
                    foreach (MHSConfigCommittedRecord record in _authorized.ReadCommittedAfter(null, 50))
                        host.Children.Add(new TextBlock { Text = record.Revision + "    " +
                            record.RecordedAtUtc.ToLocalTime().ToString("g") +
                            "    修改字段数: " + record.ChangedPaths.Count,
                            Margin = new Thickness(0, 4, 0, 0) });
            }
            catch (UnauthorizedAccessException)
            {
                host.Children.Add(new TextBlock { Text = "当前授权不足以读取配置审计与诊断。",
                    Foreground = Brushes.Gray });
            }
        }
        private void EnsureActive()
        { if (_disposed) throw new ObjectDisposedException("MPWConfigEditorControl"); }
        private void Execute(Action action)
        {
            if (_disposed || _working) return;
            try { _issues.Text = ""; action(); RefreshAll(); }
            catch (Exception ex) { ShowError(ex); RefreshAll(); }
        }
        private void ShowError(Exception ex)
        {
            // Never display arbitrary plugin/validator exception messages; those may contain secrets.
            _issues.Text = "操作未完成（" + ex.GetType().Name + "）。请检查输入及授权日志。";
        }
        private void RefreshAll()
        {
            if (_disposed) return;
            _count = 0;
            _form.Children.Clear();
            try
            {
                FCMConfigRevision revision = ModelRevision;
                FCMConfigIdentity identity = ModelIdentity;
                _status.Text = "范围: " + identity.ScopeKind + "/" + identity.ScopeId +
                    "    Saved: " + revision.Saved + "    Effective: " + revision.Effective +
                    (revision.IsPending ? "    [待生效]" : "    [已同步]");
                object data = _editor.Draft;
                _fieldStates = _editor.DescribeFields().ToDictionary(x => x.Schema.Path,
                    x => x, StringComparer.Ordinal);
                RenderObject(_form, typeof(T), data, "", 0, false);
                string selected = _history.SelectedItem as string;
                _history.Items.Clear();
                try
                {
                    foreach (string version in Versions().Reverse()) _history.Items.Add(version);
                    _history.SelectedItem = selected != null && _history.Items.Contains(selected)
                        ? selected : revision.Saved;
                }
                catch (UnauthorizedAccessException)
                { _history.IsEnabled = false; }
            }
            catch (UnauthorizedAccessException)
            {
                _status.Text = "当前权限无法读取或继续编辑该配置。";
                _form.Children.Add(new TextBlock {
                    Text = "配置内容已隐藏；请通过可信宿主申请授权。",
                    Foreground = Brushes.Gray });
                _history.IsEnabled = false;
            }
        }
        private static bool IsScalar(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return FCMConfigSchemaBuilder.IsScalar(type);
        }
        private void RenderObject(Panel host, Type type, object model, string parent, int depth, bool hidden)
        {
            if (depth > 24) { host.Children.Add(new TextBlock { Text = "嵌套深度超过编辑限制" }); return; }
            FCMConfigMemberDescriptor[] members = FCMConfigSchemaBuilder.Members(type)
                .Where(member => (parent.Length != 0 || _visibleRootFields == null ||
                    _visibleRootFields.Contains(member.Name)) &&
                    (parent.Length == 0 || _nestedCategories == null ||
                    _nestedCategories.Contains((member.Property.GetCustomAttributes(
                        typeof(FCMConfigItemAttribute), true).FirstOrDefault()
                        as FCMConfigItemAttribute)?.Category ?? "")))
                .ToArray();
            // Presentation-only: the real Channel configuration schema supplies all fields,
            // annotation labels and validation. Group them into the three requested panels.
            // There is no second Channel editor, converter or mutable view-model.
            if (_workspaceCompact && type.Name == "MCCChannelConfiguration" &&
                parent.Length > 0 && members.Length > 0)
            {
                Grid group = new Grid { Margin = new Thickness(2, 6, 2, 8) };
                System.Windows.Automation.AutomationProperties.SetAutomationId(group, "MPW.Channel.SchemaCards");
                for (int column = 0; column < 3; column++)
                    group.ColumnDefinitions.Add(new ColumnDefinition {
                        Width = new GridLength(1, GridUnitType.Star),
                        MinWidth = 220 });
                string[] titles = { "通道类型 / 身份与子通道", "网络配置 / 串口", "数据配置 / 执行与重连" };
                for (int column = 0; column < 3; column++)
                {
                    StackPanel panel = new StackPanel();
                    Border card = new Border {
                        Background = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(204, 221, 239)),
                        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                        Margin = new Thickness(0, 0, 9, 5), Padding = new Thickness(10) };
                    StackPanel items = new StackPanel();
                    items.Children.Add(new TextBlock {
                        Text = titles[column], FontSize = 14,
                        FontWeight = FontWeights.SemiBold, Foreground = Brushes.MidnightBlue,
                        Background = Brushes.AliceBlue, Padding = new Thickness(7, 7, 7, 7),
                        Margin = new Thickness(0, 0, 0, 10) });
                    foreach (FCMConfigMemberDescriptor member in members)
                    {
                        FCMConfigItemAttribute attr = member.Property.GetCustomAttributes(
                            typeof(FCMConfigItemAttribute), true).FirstOrDefault() as FCMConfigItemAttribute;
                        string category = attr == null ? "" : attr.Category;
                        int assigned = category == "身份" || category == "子通道" ? 0 :
                            category == "网络" || category == "串口" ? 1 : 2;
                        if (assigned != column) continue;
                        string path = parent + "." + member.Name;
                        object value = model == null ? null : member.Property.GetValue(model, null);
                        RenderValue(panel, member.MemberType, value, path, member.Label,
                            depth, hidden || member.Property.IsDefined(
                                typeof(FCMConfigSensitiveAttribute), true) ||
                            FCMConfigSensitivity.ContainsSensitive(member.MemberType), member.Unit);
                    }
                    items.Children.Add(panel); card.Child = items;
                    Grid.SetColumn(card, column); group.Children.Add(card);
                }
                host.Children.Add(group);
                return;
            }
            foreach (FCMConfigMemberDescriptor member in members)
            {
                string path = parent.Length == 0 ? member.Name : parent + "." + member.Name;
                bool isSecret = hidden || member.Property.IsDefined(typeof(FCMConfigSensitiveAttribute), true) ||
                    FCMConfigSensitivity.ContainsSensitive(member.MemberType);
                object value = model == null ? null : member.Property.GetValue(model, null);
                RenderValue(host, member.MemberType, value, path, member.Label, depth, isSecret, member.Unit);
            }
        }
        private void RenderValue(Panel host, Type type, object value, string path,
            string label, int depth, bool hidden, string unit)
        {
            if (hidden)
            {
                host.Children.Add(new TextBlock { Text = label + " — [敏感配置已隐藏，需使用授权专用编辑器]",
                    Margin = new Thickness(4, 8, 0, 8), Foreground = Brushes.Gray });
                return;
            }
            Type actual = Nullable.GetUnderlyingType(type) ?? type;
            Func<string, Action<string>, FrameworkElement> complexEditor;
            if (!IsScalar(type) && _custom.TryGetValue(path, out complexEditor))
            {
                StackPanel customPanel = new StackPanel { Margin = new Thickness(5) };
                customPanel.Children.Add(new TextBlock { Text = label + " · 专业编辑器",
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(3, 4, 3, 5) });
                customPanel.Children.Add(complexEditor(FCMJsonValueCodec.Serialize(value),
                    json => Execute(() => _editor.SetPathJson(path, json))));
                host.Children.Add(customPanel);
                _count++;
                return;
            }
            if (IsScalar(type))
            {
                StackPanel line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 5, 4, 5) };
                TextBlock caption = new TextBlock { Text = label + (string.IsNullOrEmpty(unit) ? "" : " (" + unit + ")"),
                    Width = 190, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                line.Children.Add(caption);
                Func<string, Action<string>, FrameworkElement> custom;
                if (_custom.TryGetValue(path, out custom))
                {
                    FrameworkElement control = custom(_editor.ReadDraftJson(path), json =>
                        Execute(() => _editor.SetPathJson(path, json)));
                    line.Children.Add(control);
                }
                else if (actual == typeof(bool))
                {
                    CheckBox box = new CheckBox { IsChecked = value == null ? (bool?)null : (bool)value,
                        IsThreeState = Nullable.GetUnderlyingType(type) != null, VerticalAlignment = VerticalAlignment.Center };
                    box.Click += (s, e) => Execute(() => _editor.SetPathJson(path,
                        box.IsChecked.HasValue ? (box.IsChecked.Value ? "true" : "false") : "null"));
                    line.Children.Add(box);
                }
                else if (actual.IsEnum)
                {
                    ComboBox box = new ComboBox { Width = 210, ItemsSource = Enum.GetNames(actual),
                        SelectedItem = value == null ? null : value.ToString() };
                    box.SelectionChanged += (s, e) => {
                        if (box.SelectedItem != null) Execute(() => _editor.SetPathJson(path,
                            FCMJsonValueCodec.Serialize((string)box.SelectedItem)));
                    };
                    line.Children.Add(box);
                }
                else
                {
                    TextBox box = new TextBox { Width = 230, MinHeight = 26,
                        Text = Format(value), Padding = new Thickness(5, 3, 5, 3) };
                    box.LostFocus += (s, e) => Execute(() => _editor.SetPathJson(path,
                        ToConfigJson(box.Text, type)));
                    line.Children.Add(box);
                    if (Nullable.GetUnderlyingType(type) != null)
                        line.Children.Add(MakeButton("置空", () => Execute(() => _editor.SetPathJson(path, "null"))));
                }
                _count++;
                host.Children.Add(line);
                AddFieldDetails(host, path);
                return;
            }
            Expander expander = new Expander { Header = label + "  [" + path + "]", IsExpanded = depth < 2,
                Margin = new Thickness(4, 6, 4, 2), Padding = new Thickness(8, 5, 3, 5) };
            StackPanel inner = new StackPanel();
            expander.Content = inner;
            host.Children.Add(expander);
            if (value == null)
            {
                inner.Children.Add(new TextBlock { Text = "该节点目前为 null，可显式初始化。",
                    Foreground = Brushes.Gray });
                inner.Children.Add(MakeButton("初始化节点", () =>
                    Execute(() => _editor.SetPathJson(path, DefaultJson(actual)))));
                return;
            }
            if (actual.IsArray || (actual.IsGenericType &&
                (actual.GetGenericTypeDefinition() == typeof(List<>) ||
                 actual.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                 actual.GetGenericTypeDefinition() == typeof(HashSet<>))))
                RenderCollection(inner, actual, value, path, depth + 1);
            else RenderObject(inner, actual, value, path, depth + 1, false);
        }
        private void AddFieldDetails(Panel host, string path)
        {
            MHSConfigEditorField state;
            if (_fieldStates == null || !_fieldStates.TryGetValue(path, out state) ||
                state.Schema.IsSensitive) return;
            FCMConfigFieldInfo field = state.Schema;
            string summary = (field.Description ?? "") + "  " +
                (field.Range == null ? "" : "范围: " + field.Range + "  ") +
                (field.Regex == null ? "" : "正则: " + field.Regex + "  ") +
                "策略: " + field.ApplyPolicy + "  " +
                "默认: " + (field.DefaultJson ?? "(无)") + "  " +
                "Saved: " + (state.HasSavedValue ? state.SavedJson : "(缺失)") +
                "  Effective: " + (state.EffectiveJson ?? "(缺失)") +
                (state.IsModified ? "  [未保存草稿]" : "");
            host.Children.Add(new TextBlock {
                Text = summary, FontSize = 11, Foreground = Brushes.SlateGray,
                Margin = new Thickness(196, 0, 3, 3), TextWrapping = TextWrapping.Wrap
            });
            if (state.Issues.Any(x => x.Severity == FCMConfigIssueSeverity.Error))
                host.Children.Add(new TextBlock {
                    Text = "校验: " + string.Join(" / ",
                        state.Issues.Where(x => x.Severity == FCMConfigIssueSeverity.Error)
                            .Select(x => x.Code).Distinct()),
                    Foreground = Brushes.Firebrick, Margin = new Thickness(196, 0, 3, 5),
                    TextWrapping = TextWrapping.Wrap
                });
        }
        private int VisibleCount(string path)
        {
            int count;
            return _visibleCounts.TryGetValue(path, out count) ? count : PageSize;
        }
        private void More(Panel host, string path, int total, int displayed)
        {
            if (displayed >= total) return;
            host.Children.Add(new TextBlock {
                Text = "已显示 " + displayed + " / " + total + " 项（分批加载，避免大型字典阻塞窗口）",
                Foreground = Brushes.SlateGray, Margin = new Thickness(4, 4, 4, 2)
            });
            host.Children.Add(MakeButton("再显示 " + PageSize + " 项", () => {
                _visibleCounts[path] = Math.Min(total, displayed + PageSize);
                RefreshAll();
            }));
        }
        private void RenderCollection(Panel host, Type type, object value, string path, int depth)
        {
            Type[] args = type.IsArray ? new[] { type.GetElementType() } : type.GetGenericArguments();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (args[0] != typeof(string))
                { host.Children.Add(new TextBlock { Text = "仅支持 string 键" }); return; }
                StackPanel add = new StackPanel { Orientation = Orientation.Horizontal };
                TextBox newKey = new TextBox { Width = 160, Margin = new Thickness(3), ToolTip = "新字典键" };
                add.Children.Add(newKey);
                add.Children.Add(MakeButton("增加键", () => Execute(() =>
                    _editor.AddMapItem(path, newKey.Text, DefaultJson(args[1])))));
                host.Children.Add(add);
                IDictionary dict = (IDictionary)value;
                int shown = Math.Min(dict.Count, VisibleCount(path));
                IDictionaryEnumerator cursor = dict.GetEnumerator();
                int rendered = 0;
                while (rendered < shown && cursor.MoveNext())
                {
                    // IDictionary.GetEnumerator yields DictionaryEntry. Enumerable.Cast
                    // may instead enumerate generic KeyValuePair and throw InvalidCast.
                    DictionaryEntry entry = cursor.Entry;
                    rendered++;
                    string key = (string)entry.Key, child = path + "[" + FCMJsonValueCodec.Serialize(key) + "]";
                    StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
                    TextBox rename = new TextBox { Text = key, Width = 160, Margin = new Thickness(3) };
                    actions.Children.Add(rename);
                    actions.Children.Add(MakeButton("改名", () => Execute(() =>
                        _editor.RenameMapItem(path, key, rename.Text))));
                    actions.Children.Add(MakeButton("删除", () => Execute(() => _editor.RemoveMapItem(path, key))));
                    host.Children.Add(actions);
                    RenderValue(host, args[1], entry.Value, child, "键: " + key, depth, false, null);
                }
                More(host, path, dict.Count, shown);
                return;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                StackPanel add = new StackPanel { Orientation = Orientation.Horizontal };
                TextBox input = new TextBox { Width = 180, Margin = new Thickness(3),
                    ToolTip = "输入新集合元素" };
                add.Children.Add(input);
                add.Children.Add(MakeButton("增加元素", () => Execute(() =>
                    _editor.AddSetJson(path, ToConfigJson(input.Text, args[0])))));
                host.Children.Add(add);
                int total = ((IEnumerable)value).Cast<object>().Count();
                int shown = Math.Min(total, VisibleCount(path));
                foreach (object item in ((IEnumerable)value).Cast<object>().Take(shown))
                {
                    string json = FCMJsonValueCodec.Serialize(item);
                    StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(new TextBlock { Text = Format(item), Width = 200,
                        VerticalAlignment = VerticalAlignment.Center });
                    row.Children.Add(MakeButton("删除", () => Execute(() =>
                        _editor.RemoveSetJson(path, json))));
                    host.Children.Add(row);
                }
                More(host, path, total, shown);
                return;
            }
            IList list = (IList)value;
            bool mutable = !type.IsArray;
            if (mutable)
            {
                Button add = MakeButton(path == "Channels" ? "添加真实通道配置" :
                    path == "Protocols" ? "添加规约绑定" :
                    path == "Points" ? "添加点表项" : "追加一项",
                    () => Execute(() => _editor.InsertListItem(
                        path, list.Count, DefaultJson(args[0]))));
                System.Windows.Automation.AutomationProperties.SetAutomationId(add, "MPW.Collection.Add." + path);
                host.Children.Add(add);
                if (list.Count == 0 && path == "Channels")
                    host.Children.Add(new TextBlock {
                        Text = "当前没有通道。新增后显示正式Virtual/TCP/UDP/Serial、网络及执行字段。新增只是配置草稿，不代表已连接设备。",
                        Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(5, 6, 6, 12) });
            }
            int shownList = Math.Min(list.Count, VisibleCount(path));
            for (int i = 0; i < shownList; i++)
            {
                int index = i;
                string child = path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (mutable)
                {
                    StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
                    actions.Children.Add(new TextBlock { Text = "#" + i, Width = 55,
                        VerticalAlignment = VerticalAlignment.Center });
                    actions.Children.Add(MakeButton("↑", () => Execute(() =>
                        _editor.MoveListItem(path, index, Math.Max(0, index - 1)))));
                    actions.Children.Add(MakeButton("↓", () => Execute(() =>
                        _editor.MoveListItem(path, index, Math.Min(list.Count - 1, index + 1)))));
                    actions.Children.Add(MakeButton("删除", () => Execute(() =>
                        _editor.RemoveListItem(path, index))));
                    host.Children.Add(actions);
                }
                RenderValue(host, args[0], list[i], child, "第 " + i + " 项", depth, false, null);
            }
            More(host, path, list.Count, shownList);
        }
        private static string Format(object value)
        { return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture); }
        private static string DefaultJson(Type type)
        {
            if (type == typeof(string)) return FCMJsonValueCodec.Serialize("");
            if (Nullable.GetUnderlyingType(type) != null) return "null";
            object value = Activator.CreateInstance(type);
            return FCMJsonValueCodec.Serialize(value);
        }
        internal static string ToConfigJson(string value, Type type)
        {
            Type t = Nullable.GetUnderlyingType(type) ?? type;
            if (value.Length == 0 && Nullable.GetUnderlyingType(type) != null) return "null";
            if (t == typeof(string) || t == typeof(char) || t.IsEnum ||
                t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset) ||
                t == typeof(TimeSpan) || t == typeof(Uri) || t == typeof(Version))
                return FCMJsonValueCodec.Serialize(value);
            // Reject malformed numbers/booleans using the actual value codec.
            FCMJsonValueCodec.Deserialize(value, type);
            return value;
        }
        private void ShowIssues(IEnumerable<FCMConfigIssue> issues)
        {
            // No untrusted validator text and no sensitive field content in UI.
            _issues.Text = string.Join(Environment.NewLine, issues.Take(15)
                .Select(x => x.Path + "  " + x.Code + "（详细信息请使用可信诊断）"));
        }
        private async void PreviewAsync()
        {
            if (_disposed || _working) return;
            _working = true;
            try
            {
                MHSConfigPreview result = await _editor.PreviewAsync(CancellationToken.None);
                ShowIssues(result.Validation.Issues);
                if (result.Validation.IsValid) _issues.Text = "验证通过，可保存当前草稿。";
            }
            catch (Exception ex) { ShowError(ex); }
            finally { _working = false; }
        }
        /// <summary>Testable asynchronous save; the only write remains EditorModel.CommitAsync.</summary>
        public async Task<MHSConfigCommitResult> SaveDraftAsync(CancellationToken token)
        {
            EnsureActive();
            if (_working) throw new InvalidOperationException("CONFIG_UI_EDIT_BUSY");
            _working = true;
            try
            {
                MHSConfigCommitResult result = await _editor.CommitAsync(token);
                if (result.Success)
                {
                    _saved = true;
                    _editor = NewEditor();
                    RefreshAll();
                    _issues.Text = "已保存 " + result.Revision + "；实际生效取决于配置边界。";
                }
                else
                {
                    ShowIssues(result.Validation == null ? new FCMConfigIssue[0] : result.Validation.Issues);
                    _issues.Text = "保存失败：" + result.ErrorCode + " " + _issues.Text;
                }
                return result;
            }
            finally { _working = false; }
        }
        private async void SaveAsync()
        {
            if (_disposed || _working) return;
            try { await SaveDraftAsync(CancellationToken.None); }
            catch (Exception ex) { ShowError(ex); }
        }
        public MHSConfigApplyResult ApplySaved(FCMConfigApplyPolicy boundary)
        {
            EnsureActive();
            if (_working) throw new InvalidOperationException("CONFIG_UI_EDIT_BUSY");
            MHSConfigApplyResult result = TryApply(boundary);
            RefreshAll();
            _issues.Text = result.Applied ? "配置已生效: " + result.EffectiveVersion :
                "未生效: " + result.Status + " （边界或联锁尚未满足）";
            return result;
        }
        private void Apply()
        {
            if (_disposed || _working) return;
            try { ApplySaved((FCMConfigApplyPolicy)_boundaries.SelectedItem); }
            catch (Exception ex) { ShowError(ex); }
        }
        /// <summary>
        /// Restore is a NEW saved revision. UI button obtains an explicit Yes/No first;
        /// trusted programmatic callers must confirm with their own operator.
        /// </summary>
        public async Task<MHSConfigCommitResult> RestoreAsNewSavedAsync(string version, CancellationToken token)
        {
            EnsureActive();
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Revision required");
            if (_working) throw new InvalidOperationException("CONFIG_UI_EDIT_BUSY");
            _working = true;
            try
            {
                MHSConfigCommitResult result = await Restore(version, token);
                if (result.Success)
                {
                    _editor.Dispose();
                    _editor = NewEditor();
                    _saved = true;
                    RefreshAll();
                    _issues.Text = "已建立恢复修订 " + result.Revision + "，尚需检查生效边界。";
                }
                else _issues.Text = "历史恢复未成功：" + result.ErrorCode;
                return result;
            }
            finally { _working = false; }
        }
        private async void RestoreAsync()
        {
            if (_disposed || _working) return;
            string version = _history.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(version)) return;
            if (MessageBox.Show("历史版本将恢复为新的 Saved 修订，并不会直接改变正在运行的 Effective。继续？",
                "历史恢复", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { await RestoreAsNewSavedAsync(version, CancellationToken.None); }
            catch (Exception ex) { ShowError(ex); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _editor.Dispose();
        }
    }

    /// <summary>Existing Studio entry retained; the same UserControl can be embedded.</summary>
    public static class MPWConfigEditorDialog
    {
        public static bool Edit<T>(Window owner, MHSConfigAuthorizedSection<T> config) where T : new()
        {
            if (config == null) throw new ArgumentNullException("config");
            using (var control = new MPWConfigEditorControl<T>(config))
                return Open(owner, control);
        }
        public static bool Edit<T>(Window owner, MHSConfigSection<T> config) where T : new()
        {
            if (config == null) throw new ArgumentNullException("config");
            using (var control = new MPWConfigEditorControl<T>(config))
                return Open(owner, control);
        }
        private static bool Open<T>(Window owner, MPWConfigEditorControl<T> control) where T : new()
        {
            Window dialog = new Window {
                Title = "CxVisual · " + FCMConfigSchemaBuilder.Describe<T>().SectionId,
                Width = 960, Height = 770, MinWidth = 650, MinHeight = 470,
                WindowStartupLocation = owner == null ?
                    WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            };
            if (owner != null) dialog.Owner = owner;
            DockPanel shell = new DockPanel();
            Button close = new Button { Content = "关闭编辑器", Height = 34, Width = 120,
                Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right };
            close.Click += (s, e) => dialog.Close();
            DockPanel.SetDock(close, Dock.Bottom);
            shell.Children.Add(close);
            shell.Children.Add(control);
            dialog.Content = shell;
            dialog.ShowDialog();
            return control.WasSaved;
        }

    }
}
