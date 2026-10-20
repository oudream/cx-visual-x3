using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using AvalonDock.Themes;
using CxVisual.DockingPoC.Views;

namespace CxVisual.DockingPoC
{
    /// <summary>
    /// Studio 壳 PoC：AvalonDock 只管理面板布局。业务 Revision 和诊断数据独立保存在 DemoEngineeringState。
    /// 真正接入 CxVisual 时，保留原 MEM/MOC/MRK/MDS Owner，不把它们注入 LayoutRoot。
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DemoEngineeringState _engineering;
        private readonly bool _useSavedWorkspace;
        // 映射只缓存 UI 内容；浮窗/自动隐藏时模型会离开 RootPanel，但仍由同一 LayoutRoot 拥有。
        private readonly Dictionary<string, LayoutAnchorable> _knownTools =
            new Dictionary<string, LayoutAnchorable>(StringComparer.Ordinal);
        private readonly Dictionary<string, LayoutDocument> _knownDocuments =
            new Dictionary<string, LayoutDocument>(StringComparer.Ordinal);
        private string _initialLayoutXml;
        private bool _isDark;
        private bool _isReady;

        public DemoEngineeringState Engineering { get { return _engineering; } }
        public bool IsDark { get { return _isDark; } }

        public MainWindow() : this(true) { }

        public MainWindow(bool useSavedWorkspace)
        {
            _engineering = new DemoEngineeringState();
            _useSavedWorkspace = useSavedWorkspace;
            InitializeComponent();
            _engineering.RevisionChanged += OnRevisionChanged;
            ApplyTheme(false);
            AttachViewsToCurrentLayout();
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs args)
        {
            if (_isReady) return;
            _initialLayoutXml = SerializeLayout();
            _isReady = true;
            if (_useSavedWorkspace && File.Exists(LayoutPersistence.DefaultFilePath))
            {
                try
                {
                    RestoreLayoutFromFile(LayoutPersistence.DefaultFilePath);
                    Log("已从 AppData 恢复用户的布局。");
                }
                catch (Exception error)
                {
                    Log("用户布局无法恢复，继续使用默认布局：" + error.Message);
                    _engineering.AddDiagnostic("WARN | 用户布局恢复失败：" + error.Message);
                }
            }
            UpdateStatus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // PoC 自动保存 UI 布局；真实工程文件和 Revision 完全不参与。
            if (_useSavedWorkspace && _isReady)
            {
                try { SaveLayoutToFile(LayoutPersistence.DefaultFilePath); }
                catch (Exception error)
                {
                    MessageBox.Show(this, "退出时无法保存工作区布局：" + error.Message,
                        "布局保存警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            _engineering.RevisionChanged -= OnRevisionChanged;
            base.OnClosing(e);
        }

        private void OnRevisionChanged(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void Log(string message)
        {
            _engineering.AddLog(message);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (StatusText == null || DpiText == null) return;
            StatusText.Text = "工程 Rev=" + _engineering.Revision +
                " | 布局状态不修改工程 | 文档=" + FindDocuments().Count() +
                " | 面板=" + FindTools().Count() +
                " | 模式=Design / CPU 示意";
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            DpiText.Text = "DPI X=" + (dpi.DpiScaleX * 100.0).ToString("F0", CultureInfo.InvariantCulture) +
                "% / Y=" + (dpi.DpiScaleY * 100.0).ToString("F0", CultureInfo.InvariantCulture) + "%";
        }

        private IEnumerable<LayoutDocument> FindDocuments()
        {
            return DockManager.Layout.Descendents().OfType<LayoutDocument>()
                .Concat(_knownDocuments.Values.Where(item => ReferenceEquals(item.Root, DockManager.Layout)))
                .Distinct();
        }

        private IEnumerable<LayoutAnchorable> FindTools()
        {
            // 额外枚举自动隐藏的四侧和 Hidden，避免隐藏后误判“已关闭”并生成重复面板。
            List<LayoutAnchorable> all = DockManager.Layout.Descendents()
                .OfType<LayoutAnchorable>().ToList();
            all.AddRange(DockManager.Layout.Hidden);
            LayoutAnchorSide[] sides = { DockManager.Layout.LeftSide, DockManager.Layout.RightSide,
                DockManager.Layout.TopSide, DockManager.Layout.BottomSide };
            foreach (LayoutAnchorSide side in sides)
            {
                if (side == null) continue;
                foreach (LayoutAnchorGroup group in side.Children)
                    all.AddRange(group.Children);
            }
            all.AddRange(_knownTools.Values.Where(item => ReferenceEquals(item.Root, DockManager.Layout)));
            return all.Distinct();
        }

        private LayoutAnchorable FindTool(string id)
        {
            return FindTools().FirstOrDefault(item => item.ContentId == id);
        }

        private void AttachViewsToCurrentLayout()
        {
            // 初始布局由 XAML 声明；视图不是工程对象，可安全按 ContentId 实例化。
            foreach (LayoutContent item in DockManager.Layout.Descendents().OfType<LayoutContent>())
            {
                if (!WorkspaceIds.IsKnown(item.ContentId))
                    throw new InvalidOperationException("XAML 含未注册 ContentId: " + item.ContentId);
                item.Content = DemoViewFactory.Create(item.ContentId, _engineering);
                LayoutAnchorable tool = item as LayoutAnchorable;
                if (tool != null) _knownTools[item.ContentId] = tool;
                LayoutDocument doc = item as LayoutDocument;
                if (doc != null) _knownDocuments[item.ContentId] = doc;
            }
        }

        public string SerializeLayout()
        {
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(DockManager);
            using (Utf8StringWriter writer = new Utf8StringWriter())
            {
                serializer.Serialize(writer);
                return writer.ToString();
            }
        }

        public void SaveLayoutToFile(string path)
        {
            LayoutPersistence.SaveAtomic(path, SerializeLayout());
        }

        public void RestoreLayoutFromFile(string path)
        {
            string xml = LayoutPersistence.ReadChecked(path);
            string backup = SerializeLayout();
            try
            {
                DeserializeLayout(xml);
                EnsureUniqueIds();
                UpdateStatus();
            }
            catch
            {
                // 失败后恢复之前的可用布局；外部输入绝不更改 Engineering.Revision。
                try { DeserializeLayout(backup); } catch { /* 原始异常仍向调用方报告。 */ }
                throw;
            }
        }

        private void DeserializeLayout(string xml)
        {
            LayoutPersistence.ValidateXml(xml);
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(DockManager);
            serializer.LayoutSerializationCallback += (sender, args) =>
            {
                string id = args.Model.ContentId;
                if (!WorkspaceIds.IsKnown(id))
                {
                    args.Cancel = true;
                    return;
                }
                // 每个反序列化的项构建全新的 WPF 视图，防止一个控件出现两个父节点。
                args.Content = DemoViewFactory.Create(id, _engineering);
                LayoutAnchorable tool = args.Model as LayoutAnchorable;
                if (tool != null) _knownTools[id] = tool;
                LayoutDocument document = args.Model as LayoutDocument;
                if (document != null) _knownDocuments[id] = document;
            };
            using (StringReader reader = new StringReader(xml))
                serializer.Deserialize(reader);
        }

        public void EnsureUniqueIds()
        {
            string[] ids = FindDocuments().Select(item => item.ContentId)
                .Concat(FindTools().Select(item => item.ContentId)).ToArray();
            if (ids.Any(id => !WorkspaceIds.IsKnown(id)))
                throw new InvalidDataException("布局包含未注册的 ContentId。");
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidDataException("布局包含重复的 ContentId。");
        }

        public string[] SnapshotIds()
        {
            return FindDocuments().Select(item => item.ContentId)
                .Concat(FindTools().Select(item => item.ContentId))
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        public void ApplyTheme(bool dark)
        {
            _isDark = dark;
            DockManager.Theme = dark ? (Theme)new Vs2013DarkTheme() : new Vs2013LightTheme();
            Resources["ShellBrush"] = BrushFrom(dark ? "#25262B" : "#F5F7FA");
            Resources["PanelBrush"] = BrushFrom(dark ? "#2D2D30" : "#FFFFFF");
            Resources["TextBrush"] = BrushFrom(dark ? "#ECECEC" : "#202D40");
            Resources["LineBrush"] = BrushFrom(dark ? "#46464C" : "#D9E0EA");
            Resources["AccentBrush"] = BrushFrom(dark ? "#77B9FF" : "#2563AB");
            ThemeButton.Content = dark ? "切换浅色" : "切换深色";
        }

        private static Brush BrushFrom(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public void FloatTool(string id)
        {
            LayoutAnchorable tool = FindTool(id);
            if (tool == null) throw new InvalidOperationException("面板不存在：" + id);
            if (!tool.IsVisible) tool.Show();
            tool.Float();
            Log("浮动面板：" + WorkspaceIds.TitleOf(id));
        }

        public void DockTool(string id)
        {
            LayoutAnchorable tool = FindTool(id);
            if (tool == null) throw new InvalidOperationException("面板不存在：" + id);
            if (!tool.IsVisible) tool.Show();
            tool.Dock();
            Log("恢复停靠：" + WorkspaceIds.TitleOf(id));
        }

        public void ToggleAutoHideTool(string id)
        {
            LayoutAnchorable tool = FindTool(id);
            if (tool == null) throw new InvalidOperationException("面板不存在：" + id);
            if (!tool.IsVisible) tool.Show();
            tool.ToggleAutoHide();
            Log("切换自动隐藏：" + WorkspaceIds.TitleOf(id));
        }

        public void CloseTool(string id)
        {
            LayoutAnchorable tool = FindTool(id);
            if (tool != null) tool.Close();
            _knownTools.Remove(id);
            Log("关闭面板：" + WorkspaceIds.TitleOf(id));
        }

        public void ShowTool(string id)
        {
            if (!WorkspaceIds.IsTool(id)) throw new ArgumentException("不是工具面板：" + id);
            LayoutAnchorable tool = FindTool(id);
            if (tool != null)
            {
                if (!tool.IsVisible) tool.Show();
                tool.IsActive = true;
            }
            else
            {
                tool = new LayoutAnchorable
                {
                    Title = WorkspaceIds.TitleOf(id), ContentId = id,
                    CanClose = true, CanHide = true, CanFloat = true, CanAutoHide = true,
                    Content = DemoViewFactory.Create(id, _engineering)
                };
                AnchorableShowStrategy side = id == WorkspaceIds.Properties ?
                    AnchorableShowStrategy.Right :
                    (id == WorkspaceIds.Diagnostics || id == WorkspaceIds.Logs ?
                        AnchorableShowStrategy.Bottom : AnchorableShowStrategy.Left);
                tool.AddToLayout(DockManager, side);
                _knownTools[id] = tool;
                tool.IsActive = true;
            }
            Log("显示面板：" + WorkspaceIds.TitleOf(id));
        }

        public void CloseDocument(string id)
        {
            LayoutDocument document = FindDocuments().FirstOrDefault(item => item.ContentId == id);
            if (document != null) document.Close();
            _knownDocuments.Remove(id);
            Log("关闭文档：" + WorkspaceIds.TitleOf(id));
        }

        public void OpenDocument(string id)
        {
            if (Array.IndexOf(WorkspaceIds.Documents, id) < 0)
                throw new ArgumentException("不是文档：" + id);
            LayoutDocument doc = FindDocuments().FirstOrDefault(item => item.ContentId == id);
            if (doc == null)
            {
                LayoutDocumentPane pane = DockManager.Layout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault();
                if (pane == null) throw new InvalidOperationException("布局不存在文档承载区域。");
                doc = new LayoutDocument { Title = WorkspaceIds.TitleOf(id), ContentId = id,
                    CanClose = true, CanFloat = true, Content = DemoViewFactory.Create(id, _engineering) };
                pane.Children.Add(doc);
                _knownDocuments[id] = doc;
            }
            doc.IsActive = true;
            Log("打开文档：" + WorkspaceIds.TitleOf(id));
        }

        public void ResetLayout()
        {
            if (string.IsNullOrEmpty(_initialLayoutXml))
                throw new InvalidOperationException("初始布局尚未捕获。");
            DeserializeLayout(_initialLayoutXml);
            UpdateStatus();
            Log("已重置默认布局（不修改工程版本）。");
        }

        private void Safely(Action command)
        {
            try { command(); UpdateStatus(); }
            catch (Exception error)
            {
                _engineering.AddDiagnostic("ERROR | " + error.Message);
                MessageBox.Show(this, error.Message, "AvalonDock 操作失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AutoHideResources_Click(object sender, RoutedEventArgs e)
        { Safely(() => ToggleAutoHideTool(WorkspaceIds.Resources)); }
        private void FloatProperties_Click(object sender, RoutedEventArgs e)
        { Safely(() => FloatTool(WorkspaceIds.Properties)); }
        private void DockProperties_Click(object sender, RoutedEventArgs e)
        { Safely(() => DockTool(WorkspaceIds.Properties)); }
        private void CloseProperties_Click(object sender, RoutedEventArgs e)
        { Safely(() => CloseTool(WorkspaceIds.Properties)); }
        private void ReopenProperties_Click(object sender, RoutedEventArgs e)
        { Safely(() => ShowTool(WorkspaceIds.Properties)); }
        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        { Safely(() => OpenDocument(WorkspaceIds.Configuration)); }
        private void OpenVisual_Click(object sender, RoutedEventArgs e)
        { Safely(() => OpenDocument(WorkspaceIds.Visual)); }
        private void OpenFlow_Click(object sender, RoutedEventArgs e)
        { Safely(() => OpenDocument(WorkspaceIds.Flow)); }
        private void Save_Click(object sender, RoutedEventArgs e)
        { Safely(() => { SaveLayoutToFile(LayoutPersistence.DefaultFilePath); Log("布局已保存：" + LayoutPersistence.DefaultFilePath); }); }
        private void Restore_Click(object sender, RoutedEventArgs e)
        { Safely(() => { RestoreLayoutFromFile(LayoutPersistence.DefaultFilePath); Log("布局已恢复。"); }); }
        private void Reset_Click(object sender, RoutedEventArgs e)
        { Safely(() => ResetLayout()); }
        private void Theme_Click(object sender, RoutedEventArgs e)
        { Safely(() => { ApplyTheme(!_isDark); Log("主题：" + (_isDark ? "VS Dark" : "Light")); }); }
        private void ChangeRevision_Click(object sender, RoutedEventArgs e)
        { Safely(() => _engineering.SimulateEngineeringEdit()); }
        private void Dpi_Click(object sender, RoutedEventArgs e)
        { Safely(() => { UpdateStatus(); Log("DPI 已读取；跨显示器请人工复核。" ); }); }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }
        }
    }
}
