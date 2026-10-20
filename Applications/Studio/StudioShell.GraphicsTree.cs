using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using AvalonDock.Layout;
using CxVisual.Mechanism.Core;

namespace CxVisual.Studio
{
    /// <summary>
    /// S1图形结构：从当前MEM的Page/Content/Symbol读取Visual定义并生成UI树。
    /// Layer、Group、Element的对象身份保持原MEM Id，不存储第二份工程结构。
    /// </summary>
    public partial class MainWindow
    {
        private bool _studioGraphicsRefreshing;
        private string _studioGraphicsSnapshot;

        private void UpdateStudioGraphicsVisibility()
        {
            if (StudioDock == null || _viewModel == null) return;
            LayoutAnchorable graphic = ExistingContents().OfType<LayoutAnchorable>()
                .FirstOrDefault(item => item.ContentId == "tool.graphics");
            if (graphic == null) return;
            if (_activeStudioDocumentId == "doc.visual")
            {
                if (!graphic.IsVisible) graphic.Show();
            }
            else if (graphic.IsVisible) graphic.Hide();

            // Anchorable.Show会夺取焦点，必须把中心LayoutDocument重新设为活动。
            // 否则原HMI SceneView仍存在于模型，却不会进入WPF可视树。
            LayoutDocument document = ExistingContents().OfType<LayoutDocument>()
                .FirstOrDefault(item => item.ContentId == _activeStudioDocumentId);
            if (document != null && !document.IsActive) document.IsActive = true;
        }

        private void OnStudioSceneChangedForGraphics(object sender, EventArgs args)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshStudioGraphicsTree));
                return;
            }
            RefreshStudioGraphicsTree();
        }

        private static TreeViewItem MakeGraphicsItem(MRCVisualItem item)
        {
            TreeViewItem node = new TreeViewItem
            {
                Header = (item is MRCGroup ? "[Group] " : "[Element] ") + item.Name,
                Tag = item,
                IsExpanded = item is MRCGroup
            };
            MRCGroup group = item as MRCGroup;
            if (group != null)
                foreach (MRCVisualItem child in group.Children)
                    node.Items.Add(MakeGraphicsItem(child));
            return node;
        }

        public void RefreshStudioGraphicsTree()
        {
            if (_studioGraphicsRefreshing || _viewModel == null || StudioGraphicsTree == null) return;
            MEMNode definition = _viewModel.CurrentStudioDefinition;
            MEMContent content = definition as MEMContent;
            MEMPage page = definition as MEMPage;
            if (content == null && page != null) content = page.RootContent;
            MEMSymbol symbol = definition as MEMSymbol;
            IList<MRCVisualItem> visuals = content != null
                ? (IList<MRCVisualItem>)content.VisualItems.ToList()
                : symbol != null && symbol.RootGroup != null
                    ? new List<MRCVisualItem> { symbol.RootGroup }
                    : new List<MRCVisualItem>();
            string snapshot = (definition == null ? "" : definition.Id.ToString("N")) + "|" +
                String.Join(";", CollectGraphicsFingerprint(visuals)) + "|" +
                (_viewModel.SelectedId.HasValue ? _viewModel.SelectedId.Value.ToString("N") : "");
            if (String.Equals(_studioGraphicsSnapshot, snapshot, StringComparison.Ordinal)) return;
            _studioGraphicsRefreshing = true;
            try
            {
                StudioGraphicsTree.Items.Clear();
                TreeViewItem root = new TreeViewItem
                {
                    Header = definition == null ? "当前无图形定义" : definition.Name,
                    IsExpanded = true
                };
                foreach (IGrouping<string, MRCVisualItem> layer in visuals
                    .GroupBy(item => String.IsNullOrWhiteSpace(item.LayerId) ? "默认图层" : item.LayerId))
                {
                    TreeViewItem layerTree = new TreeViewItem
                    { Header = "[图层] " + layer.Key, IsExpanded = true };
                    foreach (MRCVisualItem item in layer)
                        layerTree.Items.Add(MakeGraphicsItem(item));
                    root.Items.Add(layerTree);
                }
                StudioGraphicsTree.Items.Add(root);
                _studioGraphicsSnapshot = snapshot;
            }
            finally { _studioGraphicsRefreshing = false; }
        }

        private static IEnumerable<string> CollectGraphicsFingerprint(IEnumerable<MRCVisualItem> visuals)
        {
            foreach (MRCVisualItem item in visuals)
            {
                yield return item.Id.ToString("N") + ":" + item.Name + ":" + item.LayerId;
                MRCGroup group = item as MRCGroup;
                if (group != null)
                    foreach (string nested in CollectGraphicsFingerprint(group.Children))
                        yield return nested;
            }
        }

        private void OnStudioGraphicsTreeSelected(object sender,
            System.Windows.RoutedPropertyChangedEventArgs<object> args)
        {
            if (_studioGraphicsRefreshing || _viewModel == null) return;
            TreeViewItem node = args.NewValue as TreeViewItem;
            MRCVisualItem item = node == null ? null : node.Tag as MRCVisualItem;
            if (item == null || _viewModel.SelectedId == item.Id) return;
            _viewModel.Select(item.Id);
            SceneHost.SetSelectedVisualIds(new[] { item.Id });
            RefreshVisualInspector();
        }
    }
}
