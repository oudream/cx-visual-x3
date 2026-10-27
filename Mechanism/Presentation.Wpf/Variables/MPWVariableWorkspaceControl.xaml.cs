using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Mechanism.Presentation.Wpf.Variables
{
    /// <summary>
    /// Studio与TestCenter通用运行变量视图。必须由宿主传入现有Table，
    /// 未绑定时仅呈现空视图，不创建第二个工程、运行Scope或变量仓库。
    /// </summary>
    public partial class MPWVariableWorkspaceControl : UserControl, IDisposable
    {
        private readonly MPWVariableWorkspaceViewModel _model = new MPWVariableWorkspaceViewModel();
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        private MDSVariableLeaf _selected;
        private bool _allowWrites;
        private bool _disposed;

        public MPWVariableWorkspaceControl()
        {
            InitializeComponent();
            WatchGrid.ItemsSource = _model.WatchRows;
            ChangesGrid.ItemsSource = _model.ChangeRows;
            ProvidersGrid.ItemsSource = _model.ProviderRows;
            StatusText.Text = "尚未绑定真实运行变量Scope。请由可信宿主Attach现有MDS变量表。";
            _timer.Tick += (sender, args) => RefreshWatch();
        }
        public string BoundScopeId { get { return _model.ScopeId; } }
        public int VisibleVariables { get { return _model.WatchRows.Count; } }
        public void Bind(MDSVariableTable table, MDSVariableProviderHost host = null, bool allowRuntimeWrites = false)
        {
            if (_disposed) throw new ObjectDisposedException("MPWVariableWorkspaceControl");
            if (table == null) throw new ArgumentNullException("table");
            _timer.Stop();
            _model.Bind(table, host);
            _allowWrites = allowRuntimeWrites;
            _selected = null;
            WriteButton.IsEnabled = false;
            RefreshTree();
            RefreshWatch();
            _timer.Start();
        }
        public void RefreshWatch()
        {
            if (_disposed) return;
            _model.Refresh(SearchBox.Text);
            StatusText.Text = _model.DispatchSummary();
        }
        private void RefreshTree()
        {
            VariableTree.Items.Clear();
            foreach (MDSVariableNode node in _model.Roots())
                VariableTree.Items.Add(CreateItem(node));
        }
        private static TreeViewItem CreateItem(MDSVariableNode node)
        {
            TreeViewItem item = new TreeViewItem { Header = node.Name, Tag = node };
            MDSVariableGroup group = node as MDSVariableGroup;
            if (group != null)
                foreach (MDSVariableNode child in group.Children)
                    item.Items.Add(CreateItem(child));
            return item;
        }
        private void RefreshClicked(object sender, RoutedEventArgs e)
        { RefreshTree(); RefreshWatch(); }
        private void SearchChanged(object sender, TextChangedEventArgs e)
        { if (!_disposed && _model != null) RefreshWatch(); }
        private void VariableSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem item = e.NewValue as TreeViewItem;
            _selected = item == null ? null : item.Tag as MDSVariableLeaf;
            SelectionLabel.Text = _selected == null ? "请选择叶变量" : _selected.VariablePath;
            WriteButton.IsEnabled = _selected != null && _allowWrites && _selected.Definition.Writable;
        }
        private void WriteClicked(object sender, RoutedEventArgs e)
        {
            if (_selected == null || !_allowWrites || _model.Table == null)
            { WriteStatus.Text = "未授权写入或未选择变量"; return; }
            try
            {
                string name = _selected.Definition.Name;
                string value = ValueInput.Text;
                Type type = _selected.Definition.ValueType;
                if (type == typeof(double))
                    _model.Table.Update<double>(name, double.Parse(value, CultureInfo.InvariantCulture));
                else if (type == typeof(int))
                    _model.Table.Update<int>(name, int.Parse(value, CultureInfo.InvariantCulture));
                else if (type == typeof(bool))
                    _model.Table.Update<bool>(name, bool.Parse(value));
                else if (type == typeof(string))
                    _model.Table.Update<string>(name, value);
                else throw new NotSupportedException("此UI尚不支持该数据类型的编辑，请使用强类型API");
                WriteStatus.Text = "运行值已更新；不代表现场设备动作成功。";
                RefreshWatch();
            }
            catch (Exception error)
            { WriteStatus.Text = error.GetType().Name + ": " + error.Message; }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _model.Dispose();
            _selected = null;
        }
    }
}
