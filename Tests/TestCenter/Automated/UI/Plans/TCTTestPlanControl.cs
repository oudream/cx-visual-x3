using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using CxVisual.TestCenter.Core;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Test plan stores Case IDs only; Catalog/Runner/Artifact remain TestCenter's unique truth.
    /// This is the first usable Plan UI, not a second suite execution engine.
    /// </summary>
    public sealed class TCTTestPlanControl : UserControl, ITCTWorkspaceFrame
    {
        private readonly TextBox _name = new TextBox { MinWidth = 210, Margin = new Thickness(4),
            Text = "工程回归计划" };
        private readonly ListBox _cases = new ListBox { Margin = new Thickness(5) };
        private readonly TreeView _available = new TreeView { Margin = new Thickness(6) };
        private StackPanel _commandBar;
        private readonly Dictionary<string, CheckBox> _checkboxes =
            new Dictionary<string, CheckBox>(StringComparer.Ordinal);
        private bool _changing;
        public ITCTTestCase SelectedTest { get; private set; }
        public event Action<ITCTTestCase> TestSelected;
        public event Action<string[]> CaseIdsChanged;
        public event Action StateChanged;
        public string WorkspaceId { get { return "auto.catalog"; } }
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get {
                return new[] {
                    new TCTWorkspaceAction("plan.run", "运行所选真实测试", Run, () => _selected.Count > 0),
                    new TCTWorkspaceAction("plan.clear", "清空选择", ClearCases,
                        () => _selected.Count > 0)
                };
            }
        }
        public void OnWorkspaceClosed() { }
        public void SetEmbeddedWorkspace(bool embedded)
        { if (_commandBar != null) _commandBar.Visibility =
            embedded ? Visibility.Collapsed : Visibility.Visible; }
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(5) };
        private readonly List<string> _selected = new List<string>();
        private TCTTestCatalog _catalog;
        private Func<string[]> _selectedProvider;
        private string _planId = Guid.NewGuid().ToString("N");
        public event Action<string[]> RunRequested;
        public int CaseCount { get { return _selected.Count; } }
        public string[] CaseIds { get { return _selected.ToArray(); } }
        public TCTTestPlanControl()
        {
            StackPanel commands = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            _commandBar = commands;
            Button capture = new Button { Content = "从Explorer勾选建立计划", Padding = new Thickness(8, 4, 8, 4) };
            Button open = new Button { Content = "打开计划", Padding = new Thickness(8, 4, 8, 4) };
            Button save = new Button { Content = "保存计划", Padding = new Thickness(8, 4, 8, 4) };
            Button run = new Button { Content = "执行计划", Padding = new Thickness(8, 4, 8, 4) };
            foreach (Button button in new[] { capture, open, save, run })
            { button.Margin = new Thickness(3); commands.Children.Add(button); }
            capture.Click += (s, e) => CaptureFromExplorer();
            run.Click += (s, e) => Run();
            save.Click += (s, e) =>
            {
                SaveFileDialog dialog = new SaveFileDialog {
                    Filter = "TestCenter Plan XML|*.tctplan", DefaultExt = ".tctplan",
                    FileName = "CxVisual-TestPlan.tctplan" };
                if (dialog.ShowDialog() != true) return;
                try { SaveTo(dialog.FileName); }
                catch (Exception error) { _status.Text = "保存失败：" + error.Message; }
            };
            open.Click += (s, e) =>
            {
                OpenFileDialog dialog = new OpenFileDialog { Filter = "TestCenter Plan XML|*.tctplan" };
                if (dialog.ShowDialog() != true) return;
                try { LoadFrom(dialog.FileName); }
                catch (Exception error) { _status.Text = "打开失败：" + error.Message; }
            };
            DockPanel panel = new DockPanel();
            StackPanel top = new StackPanel();
            top.Children.Add(commands);
            StackPanel details = new StackPanel { Orientation = Orientation.Horizontal };
            details.Children.Add(new TextBlock { Text = "计划名称", Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center }); details.Children.Add(_name);
            top.Children.Add(details);
            DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(_status, Dock.Bottom);
            panel.Children.Add(top); panel.Children.Add(_status);
            Grid selection = new Grid();
            selection.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(3, GridUnitType.Star), MinWidth = 230 });
            selection.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(2, GridUnitType.Star), MinWidth = 160 });
            Border availableBorder = new Border {
                BorderThickness = new Thickness(1), BorderBrush = System.Windows.Media.Brushes.LightSteelBlue,
                Margin = new Thickness(5), Padding = new Thickness(6) };
            DockPanel left = new DockPanel();
            TextBlock availableTitle = new TextBlock {
                Text = "测试目录（勾选加入当前计划）",
                FontSize = 17, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(3, 0, 0, 7) };
            DockPanel.SetDock(availableTitle, Dock.Top);
            left.Children.Add(availableTitle); left.Children.Add(_available);
            availableBorder.Child = left; Grid.SetColumn(availableBorder, 0);
            Border selectedBorder = new Border { BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.LightSteelBlue,
                Margin = new Thickness(5), Padding = new Thickness(6) };
            DockPanel right = new DockPanel();
            TextBlock selectedTitle = new TextBlock {
                Text = "当前计划包含的用例", FontSize = 17, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(3, 0, 0, 7) };
            DockPanel.SetDock(selectedTitle, Dock.Top);
            right.Children.Add(selectedTitle); right.Children.Add(_cases);
            selectedBorder.Child = right; Grid.SetColumn(selectedBorder, 1);
            selection.Children.Add(availableBorder); selection.Children.Add(selectedBorder);
            panel.Children.Add(selection);
            Content = panel;
            _status.Text = "选择左侧用例复选框，建立只存CaseId的测试计划。";
        }
        public void Bind(TCTTestCatalog catalog, Func<string[]> selectedProvider)
        {
            _catalog = catalog ?? throw new ArgumentNullException("catalog");
            _selectedProvider = selectedProvider ?? throw new ArgumentNullException("selectedProvider");
            BuildCatalogTree();
        }
        private void BuildCatalogTree()
        {
            _available.Items.Clear();
            _checkboxes.Clear();
            if (_catalog == null) return;
            foreach (TCTTestAspect aspect in new[] {
                TCTTestAspect.Foundation, TCTTestAspect.Mechanism, TCTTestAspect.Integration })
            {
                TreeViewItem groupRoot = new TreeViewItem {
                    Header = aspect.ToString(), IsExpanded = false, FontSize = 16 };
                foreach (IGrouping<string, ITCTTestCase> group in _catalog.Cases
                    .Where(x => x.Descriptor.Aspect == aspect)
                    .GroupBy(x => x.Descriptor.Group))
                {
                    TreeViewItem groupNode = new TreeViewItem {
                        Header = group.Key, IsExpanded = false };
                    foreach (ITCTTestCase test in group)
                    {
                        string id = test.Descriptor.Id;
                        CheckBox checkbox = new CheckBox {
                            Content = test.Descriptor.Name, IsChecked = _selected.Contains(id),
                            Margin = new Thickness(0, 2, 0, 2),
                            ToolTip = id + " · " + test.Descriptor.CapabilityId };
                        _checkboxes[id] = checkbox;
                        checkbox.Checked += (sender, args) => ChangeCaseChoice(id, true);
                        checkbox.Unchecked += (sender, args) => ChangeCaseChoice(id, false);
                        TreeViewItem item = new TreeViewItem {
                            Header = checkbox, Tag = test, ToolTip = id };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(item,
                            "TCT.Case." + id);
                        item.Selected += (sender, args) => {
                            if (!ReferenceEquals(args.OriginalSource, item)) return;
                            SelectedTest = test;
                            if (TestSelected != null) TestSelected(test);
                        };
                        groupNode.Items.Add(item);
                    }
                    groupRoot.Items.Add(groupNode);
                }
                _available.Items.Add(groupRoot);
            }
        }
        private void ChangeCaseChoice(string id, bool selected)
        {
            if (_changing) return;
            if (selected && !_selected.Contains(id)) _selected.Add(id);
            if (!selected) _selected.Remove(id);
            RefreshCaseList();
            if (CaseIdsChanged != null) CaseIdsChanged(CaseIds);
            if (StateChanged != null) StateChanged();
        }
        private void RefreshCaseList()
        {
            if (_catalog == null) return;
            _cases.ItemsSource = _selected.Select(id => _catalog.Cases.First(x =>
                x.Descriptor.Id == id).Descriptor.Name + "  [" + id + "]").ToArray();
            _status.Text = "当前计划：" + _selected.Count + " 个真实TestId（勾选不等于执行通过）";
        }
        private void ClearCases()
        {
            _changing = true;
            try {
                _selected.Clear();
                foreach (CheckBox box in _checkboxes.Values) box.IsChecked = false;
            }
            finally { _changing = false; }
            RefreshCaseList();
            if (CaseIdsChanged != null) CaseIdsChanged(CaseIds);
            if (StateChanged != null) StateChanged();
        }
        public void AssignCases(IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentNullException("ids");
            string[] values = ids.Distinct(StringComparer.Ordinal).ToArray();
            if (values.Length == 0) { ClearCases(); return; }
            SetIds(values);
            if (CaseIdsChanged != null) CaseIdsChanged(CaseIds);
            if (StateChanged != null) StateChanged();
        }
        public void CaptureFromExplorer()
        {
            if (_catalog == null || _selectedProvider == null)
                throw new InvalidOperationException("Test catalog not initialized");
            SetIds(_selectedProvider());
            _planId = Guid.NewGuid().ToString("N");
            _status.Text = "已选择 " + CaseCount + " 项；计划尚未保存或执行。";
        }
        private void SetIds(IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentNullException("ids");
            string[] values = ids.Distinct(StringComparer.Ordinal).ToArray();
            if (values.Length == 0 || values.Length > 5000)
                throw new InvalidDataException("Plan must contain 1..5000 distinct cases");
            HashSet<string> known = new HashSet<string>(
                _catalog.Cases.Select(x => x.Descriptor.Id), StringComparer.Ordinal);
            foreach (string id in values)
                if (string.IsNullOrWhiteSpace(id) || !known.Contains(id))
                    throw new InvalidDataException("Plan has unknown or missing TestId: " + id);
            _selected.Clear();
            _selected.AddRange(values);
            _changing = true;
            try {
                foreach (KeyValuePair<string, CheckBox> pair in _checkboxes)
                    pair.Value.IsChecked = _selected.Contains(pair.Key);
            }
            finally { _changing = false; }
            RefreshCaseList();
        }
        public void SaveTo(string file)
        {
            if (_selected.Count == 0) throw new InvalidOperationException("Plan is empty");
            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("Missing file", "file");
            string name = _name.Text.Trim();
            if (name.Length == 0 || name.Length > 120)
                throw new InvalidDataException("Plan name must contain 1..120 characters");
            XElement root = new XElement("TCTTestPlan", new XAttribute("schema", 1),
                new XAttribute("id", _planId), new XAttribute("name", name));
            foreach (string id in _selected)
                root.Add(new XElement("Case", new XAttribute("id", id)));
            string path = Path.GetFullPath(file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                File.WriteAllText(temporary, root.ToString(SaveOptions.DisableFormatting),
                    new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            _status.Text = "计划保存成功：" + path;
        }
        public void LoadFrom(string file)
        {
            if (_catalog == null) throw new InvalidOperationException("Catalog not bound");
            FileInfo info = new FileInfo(file);
            if (!info.Exists || info.Length > 1024 * 1024)
                throw new InvalidDataException("Plan missing or too large");
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 1024 * 1024
            };
            XDocument document;
            using (XmlReader reader = XmlReader.Create(file, settings))
                document = XDocument.Load(reader);
            XElement root = document.Root;
            if (root == null || root.Name != "TCTTestPlan" ||
                (int?)root.Attribute("schema") != 1)
                throw new InvalidDataException("Unsupported test plan schema");
            Guid parsed;
            if (!Guid.TryParseExact((string)root.Attribute("id"), "N", out parsed))
                throw new InvalidDataException("Invalid plan id");
            string name = (string)root.Attribute("name");
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
                throw new InvalidDataException("Invalid plan name");
            XElement[] entries = root.Elements().ToArray();
            if (entries.Length == 0 || entries.Length > 5000 ||
                entries.Any(x => x.Name != "Case") ||
                entries.Select(x => (string)x.Attribute("id")).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidDataException("Invalid plan case records");
            // Validate everything first; invalid plans must not partially change current selection.
            SetIds(entries.Select(x => (string)x.Attribute("id")));
            _planId = parsed.ToString("N");
            _name.Text = name;
            _status.Text = "已打开并验证计划：" + name + " / " + CaseCount + " 项";
        }
        public void Run()
        {
            if (_selected.Count == 0)
            { _status.Text = "空计划不能执行；先从Explorer勾选用例。"; return; }
            if (RunRequested != null) RunRequested(_selected.ToArray());
        }
    }
}
