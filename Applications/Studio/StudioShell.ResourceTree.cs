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

        private TreeViewItem CreateResourceCategory(string key, string title, string description, bool expanded)
        {
            bool prior;
            return new TreeViewItem
            {
                Header = MPWWorkspaceVisuals.TreeHeader(title, "group", false, false),
                Tag = key, ToolTip = description, ContextMenu = CreateResourceCategoryMenu(key),
                IsExpanded = _studioTreeExpanded.TryGetValue(key, out prior) ? prior : expanded
            };
        }

        // 分类只是MEM资源的导航投影；菜单调用已有Owner命令或既有Studio工作区。
        // 不为分类创建MEMNode、MDS实例或另一套可写资源库。
        private ContextMenu CreateResourceCategoryMenu(string key)
        {
            ContextMenu menu=new ContextMenu();
            switch(key)
            {
                case "pages":
                    menu.Items.Add(new MenuItem {Header="新建页面",Command=_viewModel.AddPageCommand});
                    break;
                case "contents":
                    menu.Items.Add(new MenuItem {Header="新建共享Content",Command=_viewModel.AddContentCommand});
                    menu.Items.Add(new MenuItem {Header="导入已有Content资源包",Command=_viewModel.ImportCommand});
                    break;
                case "symbols":
                    menu.Items.Add(new MenuItem {Header="新建共享Symbol",Command=_viewModel.AddSymbolCommand});
                    menu.Items.Add(new MenuItem {Header="导入已有Symbol资源包",Command=_viewModel.ImportCommand});
                    break;
                case "flows":
                    AddResourceNavigation(menu,"打开主流程编排","doc.flow");
                    break;
                case "modules":
                    AddResourceNavigation(menu,"打开功能模块中心","doc.modules");
                    break;
                case "data":
                    AddResourceNavigation(menu,"打开变量中心","doc.variables");
                    AddResourceNavigation(menu,"打开运行数据中心","doc.runtime-data");
                    break;
                case "configuration":
                    AddResourceNavigation(menu,"打开统一配置中心","doc.config");
                    break;
                case "assets":
                    menu.Items.Add(new MenuItem {Header="添加静态资源",Command=_viewModel.AddAssetCommand});
                    menu.Items.Add(new MenuItem {Header="导入资源包",Command=_viewModel.ImportCommand});
                    break;
                case "cases":
                    menu.Items.Add(new MenuItem {Header="正式用例由TestCenter管理；当前工程尚无独立Case资源类型",IsEnabled=false});
                    break;
            }
            return menu;
        }

        private void AddResourceNavigation(ContextMenu menu,string label,string documentId)
        {
            MenuItem item=new MenuItem {Header=label};
            item.Click+=(sender,args)=>SafeStudioAction(()=>OpenStudioDocument(documentId));
            menu.Items.Add(item);
        }

        private static bool ContainsLogicDefinition(MEMContent content)
        {
            return content!=null&&content.Aspects.Any(a=>a.Kind==MPMAspectKind.Logic);
        }

        // 逻辑资源项指向真实MEMContent，右键直接打开其原有MOL编辑器。
        private ContextMenu CreateStudioFlowResourceMenu(MEMContent content)
        {
            MEMEngineeringSession session=_viewModel.ProjectSession;
            Guid resourceId=content.Id;
            ContextMenu menu=new ContextMenu();
            MenuItem item=new MenuItem {Header="打开该Content的流程编辑器"};
            item.Click+=(sender,args)=>
            {
                if(!ReferenceEquals(_viewModel.ProjectSession,session)||
                    !(session.Find(resourceId) is MEMContent))
                {
                    _viewModel.ReportStudioShellWarning("工程已切换或流程已删除，拒绝旧资源菜单");
                    return;
                }
                SafeStudioAction(()=>OpenStudioResourceDocument(resourceId,true));
            };
            menu.Items.Add(item);
            return menu;
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

        private TreeViewItem CreateResourceLeaf(string title,object definition,bool flow=false)
        {
            string role = definition is MEMPage ? "viewer" :
                definition is MEMSymbol ? "3d" :
                definition is MEMContent ? "resource" : "module";
            TreeViewItem leaf=new TreeViewItem {
                Header=MPWWorkspaceVisuals.TreeHeader(title,role,true,false),Tag=definition};
            MEMNode resource=definition as MEMNode;
            if(flow&&resource is MEMContent)
                leaf.ContextMenu=CreateStudioFlowResourceMenu((MEMContent)resource);
            else if(resource!=null)
                leaf.ContextMenu=CreateStudioResourceEditorMenu(resource);
            return leaf;
        }

        public void RefreshStudioResourceTree()
        {
            if (_viewModel == null || StudioResourceTree == null || _studioTreeRefreshing)
                return;
            // 资源树是MEM页面和资源的只读投影：纯图元位置/运行帧变化不重建导航。
            string search = StudioResourceSearch == null ? "" : StudioResourceSearch.Text.Trim();
            string snapshot = _viewModel.ProjectSession.Project.Id.ToString("N") + "|" +
                _viewModel.ProjectSession.Project.Name + "|" + search + "|" +
                (_viewModel.ActivePage == null ? "" : _viewModel.ActivePage.Id.ToString("N")) + "|" +
                String.Join(";", _viewModel.Pages.Select(item => item.Id.ToString("N") +
                    ":" + item.Name + ":" + ContainsLogicDefinition(item.RootContent))) + "|" +
                String.Join(";", _viewModel.Resources.Select(item => item.Id.ToString("N") +
                    ":" + item.Name + ":" + (item is MEMContent &&
                    ContainsLogicDefinition((MEMContent)item))));
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
                        (String.IsNullOrWhiteSpace(projectName) ? "当前工程" : projectName) +
                        "  · MEMProject", "project", false, true),
                    Tag=_viewModel.ProjectSession.Project, IsExpanded=true
                };
                // 目录是工程定义的分类视图；不是新的工程节点类型。
                TreeViewItem pages=CreateResourceCategory("pages","页面 / Pages",
                    "当前MEMProject的真实MEMPage及独立页面编辑器",true);
                TreeViewItem contents=CreateResourceCategory("contents","共享内容 / Content",
                    "MEMContent可复用定义；可从内容中心组合或导入",true);
                TreeViewItem symbols=CreateResourceCategory("symbols","共享组件 / Symbol",
                    "MEMSymbol与唯一RootGroup；支持复用和实例配置",true);
                TreeViewItem flows=CreateResourceCategory("flows","流程 / 逻辑资源",
                    "仅列出具有Logic Aspect的真实MEMContent，包括页面RootContent",true);
                TreeViewItem modules=CreateResourceCategory("modules","设备与功能模块",
                    "模块定义与能力由已有功能模块中心管理；不伪造工程资源",false);
                TreeViewItem data=CreateResourceCategory("data","数据与交互定义",
                    "变量、数据、消息和命令的工程定义/绑定；运行值不入工程树",false);
                TreeViewItem configuration=CreateResourceCategory("configuration","工程配置与绑定",
                    "由MHS配置Owner管理版本、作用域及Saved/Effective，非第二配置仓",false);
                TreeViewItem assets=CreateResourceCategory("assets","资源库与外部资产",
                    "MEM真实静态资产；外部资源需按正式引用与依赖契约管理",true);
                TreeViewItem cases=CreateResourceCategory("cases","用例与验证资源",
                    "正式Case由TestCenter管理；不建立演示Case或额外资源树",false);

                foreach(MEMPage page in _viewModel.Pages)
                {
                    if(ResourceMatches(page.Name))
                    {
                        TreeViewItem leaf=CreateResourceLeaf(page.Name,page);
                        pages.Items.Add(leaf);
                        if(_viewModel.ActivePage!=null&&_viewModel.ActivePage.Id==page.Id)
                            leaf.IsSelected=true;
                    }
                    // Page的逻辑保存于唯一RootContent；这里仅增加导航入口，不复制定义。
                    MEMContent flow=page.RootContent;
                    if(ContainsLogicDefinition(flow)&&
                        (ResourceMatches(page.Name)||ResourceMatches(flow.Name)))
                        flows.Items.Add(CreateResourceLeaf(page.Name+" / "+flow.Name,flow,true));
                }

                foreach(IMEMResource resource in _viewModel.Resources)
                {
                    if(!ResourceMatches(resource.Name))continue;
                    if(resource is MEMSymbol)
                        symbols.Items.Add(CreateResourceLeaf(resource.Name,resource));
                    else if(resource is MEMContent)
                    {
                        MEMContent content=(MEMContent)resource;
                        contents.Items.Add(CreateResourceLeaf(content.Name,content));
                        if(ContainsLogicDefinition(content))
                            flows.Items.Add(CreateResourceLeaf(content.Name,content,true));
                    }
                    else
                        assets.Items.Add(CreateResourceLeaf(resource.Name,resource));
                }
                projectRoot.Items.Add(pages);
                projectRoot.Items.Add(contents);
                projectRoot.Items.Add(symbols);
                projectRoot.Items.Add(flows);
                projectRoot.Items.Add(modules);
                projectRoot.Items.Add(data);
                projectRoot.Items.Add(configuration);
                projectRoot.Items.Add(assets);
                projectRoot.Items.Add(cases);
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
            string category=selected.Tag as string;
            if(category!=null)
            {
                // 分组打开现有真实工作区，不会创建或写入任何工程对象。
                string document=category=="flows"?"doc.flow":
                    category=="modules"?"doc.modules":
                    category=="data"?"doc.variables":
                    category=="configuration"?"doc.config":null;
                if(document!=null)SafeStudioAction(()=>OpenStudioDocument(document));
                else _viewModel.ReportStudioShellWarning("此目录仅显示现有工程资源；右键可创建或导入");
                args.Handled=true;
                return;
            }
            TreeViewItem parent=selected.Parent as TreeViewItem;
            MEMContent flow=selected.Tag as MEMContent;
            if(parent!=null&&String.Equals(parent.Tag as string,"flows",StringComparison.Ordinal)&&
                flow!=null)
            {
                if(_viewModel.ProjectSession.Find(flow.Id) is MEMContent)
                    SafeStudioAction(()=>OpenStudioResourceDocument(flow.Id,true));
                args.Handled=true;
                return;
            }
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
