using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Studio
{
    /// <summary>
    /// S1的工程资源导航：只投影已有MEM工程和资源身份，不保存第二棵业务树。
    /// Group/Element属于Visual图形结构，绝不递归混入工程资源树。
    /// </summary>
    public partial class MainWindow
    {
        private bool _studioTreeRefreshing;
        private string _studioTreeSnapshot;
        private readonly Dictionary<string, bool> _studioTreeExpanded =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        private void OnStudioSceneChangedForTree(object sender, EventArgs args)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshStudioResourceTree));
                return;
            }
            RefreshStudioResourceTree();
        }

        private void OnStudioResourceSearchChanged(object sender, TextChangedEventArgs args)
        {
            if (_viewModel != null) RefreshStudioResourceTree();
        }

        // 顶部搜索与左侧资源树只共享过滤字串，不创建第二份工程索引。
        private void OnStudioGlobalResourceSearchChanged(object sender, TextChangedEventArgs args)
        {
            if(StudioResourceSearch==null)return;
            TextBox global=sender as TextBox;
            if(global!=null&&StudioResourceSearch.Text!=global.Text)
                StudioResourceSearch.Text=global.Text;
        }

        private bool ResourceMatches(string value)
        {
            string term = StudioResourceSearch == null ? "" : StudioResourceSearch.Text.Trim();
            return term.Length == 0 ||
                   (value ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private TreeViewItem CreateResourceCategory(string key, string title, bool expanded)
        {
            bool prior;
            return new TreeViewItem
            {
                Header = MPWWorkspaceVisuals.TreeHeader(title, "group", false, false), Tag = key,
                IsExpanded = _studioTreeExpanded.TryGetValue(key, out prior) ? prior : expanded
            };
        }

        /// <summary>
        /// S2.01：Editor候选由真实MEM身份及现有专业Viewer注册动态生成；
        /// 菜单是一次性WPF投影，不保存第二份资源或Editor定义。
        /// 资源删除、工程切换及晚注册专业Editor时均在菜单打开瞬间重新检查。
        /// </summary>
        internal ContextMenu CreateStudioResourceEditorMenu(MEMNode resource)
        {
            if(resource==null)throw new ArgumentNullException("resource");
            MEMEngineeringSession origin=_viewModel.ProjectSession;
            Guid projectId=origin.Project.Id;
            Guid resourceId=resource.Id;
            ContextMenu menu=new ContextMenu();
            Action refresh=()=>
            {
                menu.Items.Clear();
                if(!ReferenceEquals(_viewModel.ProjectSession,origin)||
                    origin.Project.Id!=projectId||origin.Find(resourceId)==null)
                {
                    menu.Items.Add(new MenuItem {Header="资源已失效，无法打开",IsEnabled=false});
                    return;
                }
                StudioResourceEditorInfo[] candidates=
                    GetStudioResourceEditors(resourceId).ToArray();
                if(candidates.Length==0)
                {
                    menu.Items.Add(new MenuItem {Header="没有可用的编辑器",IsEnabled=false});
                    return;
                }
                foreach(StudioResourceEditorInfo candidate in candidates)
                {
                    string editorId=candidate.EditorId;
                    MenuItem item=new MenuItem {
                        Header="以"+candidate.Title+"打开"+(candidate.IsDefault?"（默认）":""),
                        Tag=editorId };
                    item.Click+=(sender,args)=>
                    {
                        // 菜单可能在工程切换或Undo后才执行；再次检查Owner身份。
                        if(!ReferenceEquals(_viewModel.ProjectSession,origin)||
                            origin.Project.Id!=projectId||origin.Find(resourceId)==null||
                            !GetStudioResourceEditors(resourceId).Any(x=>x.EditorId==editorId))
                        {
                            _viewModel.ReportStudioShellWarning("资源或Editor已失效，拒绝旧菜单操作");
                            return;
                        }
                        SafeStudioAction(()=>{OpenStudioResourceEditor(resourceId,editorId);});
                    };
                    menu.Items.Add(item);
                }
                if(origin.Find(resourceId) is MEMPage)
                    AppendStudioHmiAuthoringMenu(menu,origin,resourceId);
            };
            menu.Opened+=(sender,args)=>refresh();
            refresh();
            return menu;
        }

        private TreeViewItem CreateResourceLeaf(string title,object definition)
        {
            string role = definition is MEMPage ? "viewer" :
                definition is MEMSymbol ? "3d" :
                definition is MEMContent ? "resource" : "module";
            TreeViewItem leaf=new TreeViewItem {
                Header=MPWWorkspaceVisuals.TreeHeader(title,role,true,false),Tag=definition};
            MEMNode resource=definition as MEMNode;
            if(resource!=null)
                leaf.ContextMenu=CreateStudioResourceEditorMenu(resource);
            return leaf;
        }

        public void RefreshStudioResourceTree()
        {
            if (_viewModel == null || StudioResourceTree == null || _studioTreeRefreshing)
                return;
            // 资源树是MEM页面和资源的只读投影：纯图元位置/运行帧变化不重建导航。
            string search = StudioResourceSearch == null ? "" : StudioResourceSearch.Text.Trim();
            string snapshot = _viewModel.ProjectSession.Project.Id.ToString("N") + "|" + search + "|" +
                (_viewModel.ActivePage == null ? "" : _viewModel.ActivePage.Id.ToString("N")) + "|" +
                String.Join(";", _viewModel.Pages.Select(item => item.Id.ToString("N") + ":" + item.Name)) + "|" +
                String.Join(";", _viewModel.Resources.Select(item => item.Id.ToString("N") + ":" + item.Name));
            if (String.Equals(_studioTreeSnapshot, snapshot, StringComparison.Ordinal)) return;
            _studioTreeRefreshing = true;
            try
            {
                // 先记录用户折叠状态，不覆盖MEM或进行工程编辑。
                TreeViewItem previousRoot = StudioResourceTree.Items.OfType<TreeViewItem>().FirstOrDefault();
                if (previousRoot != null)
                    foreach (TreeViewItem category in previousRoot.Items.OfType<TreeViewItem>())
                    {
                        string key = category.Tag as string;
                        if (key != null) _studioTreeExpanded[key] = category.IsExpanded;
                    }
                StudioResourceTree.Items.Clear();
                string projectName = _viewModel.ProjectSession.Project.Name;
                TreeViewItem projectRoot = new TreeViewItem
                {
                    Header = MPWWorkspaceVisuals.TreeHeader(
                        String.IsNullOrWhiteSpace(projectName) ? "当前工程" : projectName,
                        "project", false, true),
                    IsExpanded = true
                };
                TreeViewItem pages = CreateResourceCategory("pages", "页面", true);
                foreach (MEMPage page in _viewModel.Pages)
                    if (ResourceMatches(page.Name))
                    {
                        TreeViewItem leaf = CreateResourceLeaf(page.Name, page);
                        pages.Items.Add(leaf);
                        if (_viewModel.ActivePage != null && _viewModel.ActivePage.Id == page.Id)
                            leaf.IsSelected = true;
                    }
                projectRoot.Items.Add(pages);

                TreeViewItem contents = CreateResourceCategory("contents", "共享内容（Content）", true);
                TreeViewItem symbols = CreateResourceCategory("symbols", "共享组件（Symbol）", true);
                TreeViewItem assets = CreateResourceCategory("assets", "其他资源", true);
                foreach (IMEMResource resource in _viewModel.Resources)
                {
                    if (!ResourceMatches(resource.Name)) continue;
                    TreeViewItem leaf = CreateResourceLeaf(resource.Name, resource);
                    if (resource is MEMSymbol) symbols.Items.Add(leaf);
                    else if (resource is MEMContent) contents.Items.Add(leaf);
                    else assets.Items.Add(leaf);
                }
                projectRoot.Items.Add(contents);
                projectRoot.Items.Add(symbols);
                projectRoot.Items.Add(assets);
                StudioResourceTree.Items.Add(projectRoot);
                _studioTreeSnapshot = snapshot;
            }
            finally { _studioTreeRefreshing = false; }
        }

        private void OnStudioResourceTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            if (_studioTreeRefreshing || _viewModel == null) return;
            TreeViewItem selected = args.NewValue as TreeViewItem;
            if (selected == null) return;
            MEMPage page = selected.Tag as MEMPage;
            if (page != null)
            {
                MEMPage current = _viewModel.ActivePage;
                if (current == null || current.Id != page.Id) _viewModel.ActivePage = page;
                return;
            }
            IMEMResource resource = selected.Tag as IMEMResource;
            if (resource != null)
            {
                IMEMResource current = _viewModel.SelectedResource;
                if (current == null || current.Id != resource.Id) _viewModel.SelectedResource = resource;
            }
        }

        private void OnStudioResourceTreeDoubleClick(object sender, MouseButtonEventArgs args)
        {
            if (_studioTreeRefreshing || _viewModel == null) return;
            TreeViewItem selected = StudioResourceTree.SelectedItem as TreeViewItem;
            if (selected == null) return;
            MEMPage page = selected.Tag as MEMPage;
            if (page != null)
            {
                OpenStudioResourceEditor(page.Id);
                args.Handled = true;
                return;
            }
            IMEMResource resource = selected.Tag as IMEMResource;
            if (resource == null) return;
            _viewModel.SelectedResource = resource;
            MEMNode definition=resource as MEMNode;
            if(definition!=null)
            {
                OpenStudioResourceEditor(definition.Id);
                args.Handled=true;
            }
        }
    }
}
