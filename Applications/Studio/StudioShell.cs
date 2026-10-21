using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Xml;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using AvalonDock.Themes;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.Vision;
using CxVisual.Professional.CT3D;

namespace CxVisual.Studio
{
    /// <summary>
    /// S1：统一Studio壳。AvalonDock只管理视图及用户布局；工程定义、事务和运行仍由原Owner持有。
    /// 本类是原MainWindow的增量实现，不引入演示版工程状态或第二套业务文档。
    /// </summary>
    public partial class MainWindow
    {
        private const int LayoutByteLimit = 1024 * 1024;
        private readonly Dictionary<string, object> _studioViews =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly List<UIElement> _legacyRibbonActions = new List<UIElement>();
        private string _defaultStudioLayout;
        private bool _studioDark;
        private bool _studioInitialized;
        private string _ribbonSelection = "主页";
        private string _activeStudioDocumentId = "doc.home";
        private static readonly string[] KnownDocuments =
            { "doc.home", "doc.visual", "doc.flow", "doc.config" };
        private static readonly string[] KnownTools =
            { "tool.resources", "tool.toolbox", "tool.graphics", "tool.properties", "tool.output" };

        public bool StudioDarkTheme { get { return _studioDark; } }
        public int StudioEngineeringRevision { get { return _viewModel.Revision; } }

        private static string LayoutFile
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CxVisual", "Studio", "workspace.layout.xml");
            }
        }

        private void InitializeStudioShell()
        {
            foreach (LayoutContent item in StudioDock.Layout.Descendents().OfType<LayoutContent>())
            {
                if (!IsKnownContent(item.ContentId) || _studioViews.ContainsKey(item.ContentId))
                    throw new InvalidOperationException("无效或重复的工作区ContentId：" + item.ContentId);
                _studioViews[item.ContentId] = item.Content;
            }
            _legacyRibbonActions.AddRange(StudioRibbonActions.Children.Cast<UIElement>());
            PreviewKeyDown+=OnStudioRegisteredShortcut;
            ApplyStudioTheme(false);
            RefreshStudioMenus();
            _viewModel.SceneChanged += OnStudioSceneChangedForTree;
            RefreshStudioResourceTree();
            _viewModel.SceneChanged += OnStudioSceneChangedForGraphics;
            RefreshStudioGraphicsTree();
            UpdateStudioGraphicsVisibility();
            AttachStudioResourceChangeWatch(_viewModel.ProjectSession);
            _viewModel.SceneChanged += OnStudioPageSourceChanged;
            Loaded += (sender, args) =>
            {
                if (_studioInitialized) return;
                _defaultStudioLayout = SerializeStudioLayout();
                _studioInitialized = true;
                if (File.Exists(LayoutFile))
                {
                    try { RestoreStudioLayout(LayoutFile); }
                    catch (Exception ex)
                    {
                        _viewModel.ReportStudioShellWarning("用户布局恢复失败，保留默认布局：" + ex.Message);
                    }
                }
            };
        }

        private static bool IsKnownContent(string id)
        {
            Guid resourceId;string kind;
            return KnownDocuments.Contains(id, StringComparer.Ordinal) ||
                   KnownTools.Contains(id, StringComparer.Ordinal) ||
                   TryParseResourceDocumentId(id,out resourceId,out kind);
        }

        private IEnumerable<LayoutContent> ExistingContents()
        {
            List<LayoutContent> found = StudioDock.Layout.Descendents().OfType<LayoutContent>().ToList();
            found.AddRange(StudioDock.Layout.Hidden);
            LayoutAnchorSide[] sides = { StudioDock.Layout.LeftSide, StudioDock.Layout.RightSide,
                StudioDock.Layout.TopSide, StudioDock.Layout.BottomSide };
            foreach (LayoutAnchorSide side in sides)
                if (side != null)
                    foreach (LayoutAnchorGroup group in side.Children)
                        found.AddRange(group.Children);
            return found.Distinct();
        }

        public string[] GetStudioContentIds()
        {
            return ExistingContents().Select(x => x.ContentId)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        public void EnsureStudioContentIds()
        {
            string[] ids = GetStudioContentIds();
            if (ids.Any(id => !IsKnownContent(id) ||
                (IsResourceDocumentId(id) && !ResourceDocumentExists(id))) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidDataException("工作区ContentId未知或重复。");
        }

        private static bool IsResourceDocumentId(string id)
        {
            Guid resourceId;string kind;
            return TryParseResourceDocumentId(id,out resourceId,out kind);
        }
        private LayoutContent FindStudioContent(string id)
        {
            return ExistingContents().FirstOrDefault(x => x.ContentId == id);
        }

        private bool IsStudioDocumentActive(string id)
        {
            return String.Equals(_activeStudioDocumentId, id, StringComparison.Ordinal);
        }

        /// <summary>实际激活的编辑文档身份。工具面板获焦不改变最后一个活动编辑器。</summary>
        public string ActiveStudioDocumentId { get { return _activeStudioDocumentId; } }

        private static string DefaultContextFor(string id)
        {
            Guid resourceId;string kind;
            if(TryParseResourceDocumentId(id,out resourceId,out kind))
                return kind=="flow"?"节点":"绘图";
            if (id == "doc.visual") return "绘图";
            if (id == "doc.flow") return "节点";
            if (id == "doc.config") return "配置";
            return "主页";
        }

        private void SelectStudioContext(string id)
        {
            Guid pageId;string kind;
            if (!KnownDocuments.Contains(id, StringComparer.Ordinal) &&
                !TryParseResourceDocumentId(id,out pageId,out kind)) return;
            if (!String.Equals(_activeStudioDocumentId, id, StringComparison.Ordinal))
            {
                _activeStudioDocumentId = id;
                _ribbonSelection = DefaultContextFor(id);
                if (TryParseResourceDocumentId(id,out pageId,out kind) && !_switchingPageDocument)
                {
                    _switchingPageDocument = true;
                    try
                    {
                        MEMNode resource=_viewModel.ProjectSession.Find(pageId);
                        MEMPage page=resource as MEMPage;
                        if(page!=null&&(_viewModel.ActivePage==null||
                            _viewModel.ActivePage.Id!=page.Id))
                            _viewModel.ActivePage=page;
                        IMEMResource definition=resource as IMEMResource;
                        if(page==null&&definition!=null)
                            _viewModel.SelectedResource=definition;
                    }
                    finally { _switchingPageDocument = false; }
                }
                UpdateStudioGraphicsVisibility();
                RefreshStudioGraphicsTree();
                if(kind=="flow"||id=="doc.flow")RefreshFlowInspector();
            }
        }

        private void OnStudioOpenDocumentClick(object sender, RoutedEventArgs args)
        {
            Button button = sender as Button;
            if (button != null) OpenStudioDocument(button.Tag as string);
        }

        public void OpenStudioDocument(string id)
        {
            if (!KnownDocuments.Contains(id, StringComparer.Ordinal))
                throw new ArgumentException("未知Studio文档：" + id);
            LayoutDocument doc = FindStudioContent(id) as LayoutDocument;
            if (doc == null)
            {
                LayoutDocumentPane pane = StudioDock.Layout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault();
                if (pane == null) throw new InvalidOperationException("找不到中央文档面板。");
                object view;
                // MHS WPF控件在Unloaded时释放草稿/订阅，重新打开不能复用已释放的View。
                if (id=="doc.config" || !_studioViews.TryGetValue(id, out view))
                {
                    view = CreateConfigurationDocument();
                    _studioViews[id] = view;
                }
                doc = new LayoutDocument { ContentId = id, Title = StudioDocumentTitle(id),
                    CanClose = true, CanFloat = true, Content = view };
                pane.Children.Add(doc);
            }
            doc.IsActive = true;
            SelectStudioContext(id);
            RefreshStudioMenus();
        }

        public void CloseStudioDocument(string id)
        {
            LayoutDocument doc = FindStudioContent(id) as LayoutDocument;
            if (doc == null) return;
            doc.Content = null; // 下次打开复用同一个真实编辑视图和命令状态
            if(id=="doc.config")_studioViews.Remove(id); // 真正的MHS编辑控件需重建草稿
            doc.Close();
            if (_activeStudioDocumentId == id)
            {
                LayoutDocument next = StudioDock.Layout.ActiveContent as LayoutDocument;
                if (next == null || next.ContentId == id)
                    next = ExistingContents().OfType<LayoutDocument>()
                        .FirstOrDefault(item => item.ContentId != id);
                SelectStudioContext(next == null ? "doc.home" : next.ContentId);
            }
            RefreshStudioMenus();
        }

        private static string StudioDocumentTitle(string id)
        {
            switch (id)
            {
                case "doc.home": return "主页";
                case "doc.visual": return "HMI图元与Symbol [设计]";
                case "doc.flow": return "主流程编排 [设计]";
                case "doc.config": return "资源配置";
                default: return id;
            }
        }

        // P1 注册表只保存Editor类型与可信工厂，不持有工程数据或配置草稿。
        // 实际Schema/保存/审计/授权机制均在MHS和MPW中，保持Owner唯一。
        // 专业贡献现有ICommand；Studio只管理入口和活动文档，不重建业务Command Owner。
        private sealed class StudioCommandContribution
        {
            internal readonly string Id,Label,Group,DocumentId;
            internal readonly Key ShortcutKey;
            internal readonly ModifierKeys ShortcutModifiers;
            internal readonly ICommand Adapter;
            internal StudioCommandContribution(MainWindow host,string id,string label,string group,
                string documentId,ICommand command,Key key,ModifierKeys modifiers)
            {
                Id=id;Label=label;Group=group;DocumentId=documentId;
                ShortcutKey=key;ShortcutModifiers=modifiers;
                Adapter=new StudioCommandAdapter(host,documentId,command);
            }
        }
        private sealed class StudioCommandAdapter : ICommand
        {
            private readonly MainWindow _host;
            private readonly string _documentId;
            private readonly ICommand _owner;
            internal StudioCommandAdapter(MainWindow host,string doc,ICommand owner)
            {_host=host;_documentId=doc;_owner=owner;}
            public bool CanExecute(object parameter)
            {
                return _host._activeStudioDocumentId==_documentId&&
                    _host.FindStudioContent(_documentId)!=null&&
                    _owner.CanExecute(parameter);
            }
            public void Execute(object parameter)
            {if(CanExecute(parameter))_owner.Execute(parameter);}
            public event EventHandler CanExecuteChanged
            {
                add { _owner.CanExecuteChanged+=value;CommandManager.RequerySuggested+=value; }
                remove { _owner.CanExecuteChanged-=value;CommandManager.RequerySuggested-=value; }
            }
        }
        private readonly Dictionary<string,StudioCommandContribution> _studioCommands=
            new Dictionary<string,StudioCommandContribution>(StringComparer.Ordinal);
        /// <summary>复用Owner ICommand；Ribbon、右键与快捷键只路由同一Adapter。</summary>
        public void RegisterStudioCommand(string id,string label,string group,string documentId,
            ICommand command,Key shortcutKey=Key.None,
            ModifierKeys shortcutModifiers=ModifierKeys.None)
        {
            if(string.IsNullOrWhiteSpace(id)||id.Length>128||
                id.Any(c=>!char.IsLetterOrDigit(c)&&c!='.'&&c!='-'&&c!='_')||
                string.IsNullOrWhiteSpace(label)||string.IsNullOrWhiteSpace(group)||
                !IsKnownContent(documentId)||command==null)
                throw new ArgumentException("无效专业命令描述");
            if(_studioCommands.ContainsKey(id))
                throw new InvalidOperationException("CommandId重复："+id);
            if(shortcutKey!=Key.None&&_studioCommands.Values.Any(x=>
                x.ShortcutKey==shortcutKey&&x.ShortcutModifiers==shortcutModifiers&&
                x.DocumentId==documentId))
                throw new InvalidOperationException("专业快捷键冲突："+documentId);
            var item=new StudioCommandContribution(this,id,label,group,documentId,
                command,shortcutKey,shortcutModifiers);
            _studioCommands.Add(id,item);
            RefreshStudioMenus();
            FrameworkElement view=GetStudioCommandDocumentView(documentId);
            if(view!=null)RefreshStudioCommandContextMenu(view,documentId);
        }
        private FrameworkElement GetStudioCommandDocumentView(string id)
        {
            LayoutDocument doc=FindStudioContent(id) as LayoutDocument;
            return doc==null?null:doc.Content as FrameworkElement;
        }
        private void RefreshStudioCommandContextMenu(FrameworkElement host,string id)
        {
            ContextMenu menu=new ContextMenu();
            foreach(StudioCommandContribution item in _studioCommands.Values
                .Where(x=>x.DocumentId==id).OrderBy(x=>x.Id,StringComparer.Ordinal))
                menu.Items.Add(new MenuItem {Header=item.Label,Tag=item.Id,
                    Command=item.Adapter});
            host.ContextMenu=menu.Items.Count>0?menu:null;
        }
        internal bool HandleStudioRegisteredShortcut(Key key,ModifierKeys modifiers)
        {
            StudioCommandContribution command=_studioCommands.Values.FirstOrDefault(x=>
                x.DocumentId==_activeStudioDocumentId&&
                x.ShortcutKey!=Key.None&&x.ShortcutKey==key&&
                x.ShortcutModifiers==modifiers);
            if(command==null)return false;
            if(command.Adapter.CanExecute(null))command.Adapter.Execute(null);
            return true; // 被禁用的命令同样消费保留快捷键，禁止落入旧编辑器
        }
        private void OnStudioRegisteredShortcut(object sender,KeyEventArgs args)
        {
            if(args.Handled||args.OriginalSource is TextBoxBase||
                args.OriginalSource is ComboBox)return;
            if(HandleStudioRegisteredShortcut(args.Key,Keyboard.Modifiers))
                args.Handled=true;
        }

        private sealed class StudioConfigurationScopeChoice
        {
            internal readonly string Kind,Id,Label,ResourceTypeId;
            internal StudioConfigurationScopeChoice(string kind,string id,string label,string resourceTypeId)
            {Kind=kind;Id=id;Label=label;ResourceTypeId=resourceTypeId;}
            public override string ToString(){return Label;}
        }
        private readonly List<StudioConfigurationScopeChoice> _studioExtraScopes =
            new List<StudioConfigurationScopeChoice>();
        private string _studioConfigurationScopeKind="project";
        private string _studioConfigurationScopeId;
        private bool _studioScopeSwitching;
        // 仅可信宿主可注册真实机台/产品等外部身份；Studio不允许用户伪造Scope字符串。
        public void RegisterStudioConfigurationScope(string kind,string id,string label)
        {
            if(string.IsNullOrWhiteSpace(kind)||kind=="project"||kind=="resource"||
               kind.Any(c=>!char.IsLetterOrDigit(c)&&c!='-')||
               string.IsNullOrWhiteSpace(id)||id.Length>128||
               id.Any(char.IsControl)||string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("不合法的专业配置作用域身份");
            if(FindStudioContent("doc.config") is LayoutDocument)
                throw new InvalidOperationException("请先关闭配置页，避免丢失未保存草稿");
            if(_studioExtraScopes.Any(x=>x.Kind==kind&&x.Id==id))
                throw new InvalidOperationException("配置作用域重复："+kind+"/"+id);
            _studioExtraScopes.Add(new StudioConfigurationScopeChoice(
                kind,id,label+" · "+kind+"/"+id,null));
        }
        private sealed class StudioConfigurationContribution
        {
            internal readonly string SectionId;
            internal readonly string Title;
            internal readonly Func<MVBConfigScopeResolver,FrameworkElement> Create;
            internal readonly string[] ResourceTypeIds;
            internal StudioConfigurationContribution(string id,string title,
                Func<MVBConfigScopeResolver,FrameworkElement> create,string[] resourceTypeIds)
            {SectionId=id;Title=title;Create=create;ResourceTypeIds=resourceTypeIds;}
        }
        private readonly Dictionary<string,StudioConfigurationContribution> _studioConfigurations =
            new Dictionary<string,StudioConfigurationContribution>(StringComparer.Ordinal);
        private bool _studioConfigDefaultsRegistered;
        private Guid _studioConfigurationProjectId;

        private void EnsureStudioConfigurationDefaults()
        {
            if(_studioConfigDefaultsRegistered)return;
            _studioConfigDefaultsRegistered=true;
            RegisterStudioConfiguration<CxVisionInspectionOptions>("Vision · 检测参数");
            RegisterStudioConfiguration<CxCtReconstructionOptions>("CT3D · 重建参数");
        }

        /// <summary>
        /// 已受信任的专业宿主使用此入口向Studio提供真实配置Section。
        /// 不注册第二套Schema；每个SectionId只允许一个Editor贡献。
        /// 为保护草稿，不允许在配置文档已打开时热替换。
        /// </summary>
        public void RegisterStudioConfiguration<T>(string title) where T : new()
        {RegisterStudioConfiguration<T>(title,new string[0]);}
        // resourceTypeIds为真实MEM TypeId；空数组表示通用Project/可信外部Scope，
        // 禁止不声明资源类型就把专业配置自动关联到任意工程对象。
        public void RegisterStudioConfiguration<T>(string title,params string[] resourceTypeIds)
            where T : new()
        {
            if(string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("必须提供专业配置名称","title");
            // 专业可在首次打开配置页之前注册；先固定已有两个公共入口，
            // 避免稍后加载默认Schema时发生身份冲突。
            if(!_studioConfigDefaultsRegistered)EnsureStudioConfigurationDefaults();
            if(FindStudioContent("doc.config") is LayoutDocument)
                throw new InvalidOperationException("请先关闭配置文档，再注册新专业以保护当前草稿");
            string id=FCMConfigSchemaBuilder.Describe<T>().SectionId;
            if(_studioConfigurations.ContainsKey(id))
                throw new InvalidOperationException("专业配置SectionId重复："+id);
            if(resourceTypeIds==null)throw new ArgumentNullException("resourceTypeIds");
            if(resourceTypeIds.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>128))
                throw new ArgumentException("资源类型标识无效","resourceTypeIds");
            _studioConfigurations.Add(id,new StudioConfigurationContribution(id,title,
                scope=>new MPWConfigEditorControl<T>(scope.Open<T>()),
                resourceTypeIds.Distinct(StringComparer.Ordinal).ToArray()));
        }

        private StudioConfigurationScopeChoice[] StudioConfigurationScopeChoices()
        {
            MEMEngineeringSession session=_viewModel.ProjectSession;
            if(session==null||session.Project==null)return new StudioConfigurationScopeChoice[0];
            List<StudioConfigurationScopeChoice> choices=
                new List<StudioConfigurationScopeChoice>();
            string projectId=session.Project.Id.ToString("N");
            choices.Add(new StudioConfigurationScopeChoice("project",projectId,
                "工程配置 · "+session.Project.Name+" ("+projectId.Substring(0,8)+")",null));
            // 只从真实MEM资源获取身份；不凭界面名称创造机台、产品或虚构资源。
            foreach(IMEMResource resource in session.Project.Resources
                .Where(x=>x is MEMNode)
                .OrderBy(x=>x.Name,StringComparer.Ordinal))
            {
                if(!_studioConfigurations.Values.Any(c=>
                    c.ResourceTypeIds.Contains(resource.TypeId,StringComparer.Ordinal)))continue;
                choices.Add(new StudioConfigurationScopeChoice("resource",
                    resource.Id.ToString("N"),"资源配置 · "+resource.Name+
                    " ("+resource.TypeId+")",resource.TypeId));
            }
            choices.AddRange(_studioExtraScopes);
            return choices.ToArray();
        }
        private static bool ConfigurationMatchesScope(
            StudioConfigurationContribution contribution,StudioConfigurationScopeChoice scope)
        {
            if(scope.Kind!="resource")return true;
            return contribution.ResourceTypeIds.Contains(scope.ResourceTypeId,
                StringComparer.Ordinal);
        }
        private FrameworkElement CreateConfigurationDocument()
        {
            EnsureStudioConfigurationDefaults();
            MEMEngineeringSession session=_viewModel.ProjectSession;
            if(session==null||session.Project==null)
                throw new InvalidOperationException("必须先打开有效工程");
            Guid projectId=session.Project.Id;
            StudioConfigurationScopeChoice[] choices=StudioConfigurationScopeChoices();
            if(choices.Length==0)throw new InvalidOperationException("没有有效配置Scope");
            StudioConfigurationScopeChoice selected=choices.FirstOrDefault(x=>
                x.Kind==_studioConfigurationScopeKind&&
                x.Id==_studioConfigurationScopeId)??choices[0];
            string root=Path.Combine(GetDemoDataRoot(),"Configs","V24","Projects");
            MHSConfigService configs=new MHSConfigService(root);

            DockPanel host=new DockPanel {
                Background=(Brush)Application.Current.Resources["StudioBackgroundBrush"] };
            StackPanel header=new StackPanel {Margin=new Thickness(12,10,12,6)};
            header.Children.Add(new TextBlock {
                Text="专业配置 · Schema驱动组合编辑",
                FontSize=19,FontWeight=FontWeights.SemiBold });
            header.Children.Add(new TextBlock {
                Text="当前工程 "+projectId.ToString("N").Substring(0,10)+
                    "；从MEM选择合法资源Scope，可信宿主可贡献机台或产品Scope。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0) });
            DockPanel.SetDock(header,Dock.Top);host.Children.Add(header);
            ComboBox scopeBox=new ComboBox {
                Name="StudioConfigScopeSelector",MinWidth=270,MaxWidth=760,
                Margin=new Thickness(4,6,4,6),HorizontalAlignment=HorizontalAlignment.Left };
            foreach(StudioConfigurationScopeChoice item in choices)scopeBox.Items.Add(item);
            scopeBox.SelectedItem=selected;
            header.Children.Add(scopeBox);
            TextBlock origin=new TextBlock {
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,3,4,7) };
            header.Children.Add(origin);
            ScrollViewer scroller=new ScrollViewer {
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
            UniformGrid grid=new UniformGrid {Columns=2,Rows=0};
            List<FrameworkElement> editors=new List<FrameworkElement>();
            Action<StudioConfigurationScopeChoice> populate=choice=>
            {
                foreach(FrameworkElement editor in editors)
                {
                    IDisposable disposable=editor as IDisposable;
                    if(disposable!=null)disposable.Dispose();
                }
                editors.Clear();grid.Children.Clear();
                MVBConfigScopeResolver resolver=configs.ForScope(choice.Kind,choice.Id);
                foreach(StudioConfigurationContribution item in _studioConfigurations.Values
                    .Where(c=>ConfigurationMatchesScope(c,choice))
                    .OrderBy(c=>c.SectionId,StringComparer.Ordinal))
                {
                    FrameworkElement editor=item.Create(resolver);
                    editors.Add(editor);
                    Border card=new Border {
                        BorderThickness=new Thickness(1),
                        BorderBrush=(Brush)Application.Current.Resources["StudioBorderBrush"],
                        Margin=new Thickness(5),Padding=new Thickness(3) };
                    DockPanel panel=new DockPanel();
                    TextBlock title=new TextBlock {
                        Text=item.Title+" · "+item.SectionId,
                        FontWeight=FontWeights.SemiBold,
                        Margin=new Thickness(8,6,8,6) };
                    DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
                    panel.Children.Add(editor);card.Child=panel;grid.Children.Add(card);
                }
                origin.Text="配置来源："+choice.Label+
                    "；ScopeKind="+choice.Kind+"；ScopeId="+choice.Id+
                    "。Saved与Effective由MHS独立管理，选择Scope不触发设备生效。";
                _studioConfigurationScopeKind=choice.Kind;
                _studioConfigurationScopeId=choice.Id;
            };
            scopeBox.SelectionChanged+=(sender,args)=>
            {
                if(_studioScopeSwitching)return;
                StudioConfigurationScopeChoice choice=scopeBox.SelectedItem as
                    StudioConfigurationScopeChoice;
                if(choice==null)return;
                if(editors.OfType<IMPWConfigDraftGuard>().Any(e=>e.HasUnsavedDraft))
                {
                    _studioScopeSwitching=true;
                    scopeBox.SelectedItem=choices.First(x=>
                        x.Kind==_studioConfigurationScopeKind&&
                        x.Id==_studioConfigurationScopeId);
                    _studioScopeSwitching=false;
                    _viewModel.ReportStudioShellWarning(
                        "当前配置存在未保存草稿，请先保存、撤销或关闭配置编辑器。");
                    return;
                }
                populate(choice);
            };
            populate(selected);
            host.SizeChanged+=(sender,args)=>
            {
                int max=Math.Min(3,grid.Children.Count);
                int cols=args.NewSize.Width<900?1:
                    args.NewSize.Width<1500?2:3;
                grid.Columns=Math.Max(1,Math.Min(max,cols));
            };
            scroller.Content=grid;host.Children.Add(scroller);
            RefreshStudioCommandContextMenu(host,"doc.config");
            _studioConfigurationProjectId=projectId;
            return host;
        }

        public void ShowStudioTool(string id)
        {
            if (!KnownTools.Contains(id, StringComparer.Ordinal))
                throw new ArgumentException("未知工具面板：" + id);
            LayoutAnchorable tool = FindStudioContent(id) as LayoutAnchorable;
            if (tool == null)
            {
                object view;
                if (!_studioViews.TryGetValue(id, out view))
                    throw new InvalidOperationException("面板视图未注册：" + id);
                tool = new LayoutAnchorable { ContentId = id, Title = StudioToolTitle(id),
                    CanClose = true, CanHide = true, CanFloat = true, CanAutoHide = true,
                    Content = view };
                AnchorableShowStrategy side = id == "tool.properties" || id == "tool.graphics"
                    ? AnchorableShowStrategy.Right
                    : (id == "tool.output" ? AnchorableShowStrategy.Bottom : AnchorableShowStrategy.Left);
                tool.AddToLayout(StudioDock, side);
            }
            if (!tool.IsVisible) tool.Show();
            tool.IsActive = true;
        }

        private static string StudioToolTitle(string id)
        {
            switch (id)
            {
                case "tool.resources": return "工程资源管理器";
                case "tool.toolbox": return "工具箱";
                case "tool.properties": return "属性 / Schema / 绑定";
                case "tool.graphics": return "图形结构";
                case "tool.output": return "输出 / 诊断";
                default: return id;
            }
        }

        public void CloseStudioTool(string id)
        {
            LayoutAnchorable tool = FindStudioContent(id) as LayoutAnchorable;
            if (tool == null) return;
            tool.Content = null;
            tool.Close();
        }

        private void OnStudioActiveContentChanged(object sender, EventArgs args)
        {
            // DockingManager.ActiveContent是文档的实际View，不是LayoutContent。
            // 正确的文档身份来自LayoutRoot.ActiveContent，且右侧/底部工具获焦不能清空上下文。
            if (_viewModel == null) return;
            LayoutDocument document = StudioDock.Layout.ActiveContent as LayoutDocument;
            if (document != null) SelectStudioContext(document.ContentId);
            RefreshStudioMenus();
        }

        private void RefreshStudioMenus()
        {
            if (StudioDynamicMenus == null || StudioRibbonActions == null) return;
            string active = _activeStudioDocumentId;
            Guid resourceId;string resourceKind;
            bool resourceDocument=TryParseResourceDocumentId(active,out resourceId,out resourceKind);
            string[] domains = (active == "doc.visual" ||
                (resourceDocument&&resourceKind!="flow"))
                ? new[] { "绘图", "排列", "图层", "绑定" }
                : (active == "doc.flow" || (resourceDocument&&resourceKind=="flow"))
                    ? new[] { "节点", "连线", "编排", "端口" }
                    : active == "doc.config"
                        ? new[] { "配置" }
                        : new[] { "主页", "资源" };
            StudioDynamicMenus.Children.Clear();
            foreach (string name in domains)
            {
                Button tab = new Button { Content = name, Tag = name,
                    Padding = new Thickness(11, 5, 11, 5), Margin = new Thickness(1, 0, 0, 0) };
                tab.Click += OnStudioMenuClick;
                StudioDynamicMenus.Children.Add(tab);
            }
            if (!domains.Contains(_ribbonSelection, StringComparer.Ordinal) &&
                !new[] { "文件", "编辑", "视图", "工程", "调试", "运行", "发布", "工具", "帮助" }
                    .Contains(_ribbonSelection, StringComparer.Ordinal))
                _ribbonSelection = domains[0];
            UpdateStudioRibbon();
        }

        private void OnStudioMenuClick(object sender, RoutedEventArgs args)
        {
            Button button = sender as Button;
            if (button == null) return;
            _ribbonSelection = button.Tag as string ?? "主页";
            UpdateStudioRibbon();
        }

        private static string RibbonGroupOf(UIElement item)
        {
            if (item is ComboBox) return "工具";
            Button button = item as Button;
            string name = button == null ? "" : Convert.ToString(button.Content, CultureInfo.InvariantCulture);
            if (new[] { "新建工程", "保存工程", "打开工程", "导入资源包", "导出所选资源" }.Contains(name)) return "文件";
            if (new[] { "撤销", "重做", "删除选中", "重命名", "所选图元成组", "解散选中或父分组", "右移20" }.Contains(name)) return "编辑";
            if (new[] { "添加页面", "共享内容", "新建组件", "添加静态资源" }.Contains(name)) return "工程";
            if (new[] { "发布示例配置 (L4)", "回滚发布" }.Contains(name)) return "发布";
            return "工具";
        }

        // 动态Ribbon复用已有StudioViewModel命令；激活资源Tab时必须先恢复其MEM页面。
        // 左资源树单击只改变资源检视目标，不能让当前页面Tab的编辑命令误改另一页。
        private bool EnsureActiveStudioPageForRibbon()
        {
            Guid id;string kind;
            if(!TryParseResourceDocumentId(_activeStudioDocumentId,out id,out kind))return true;
            ResourceDocumentContext context;
            if(!_pageDocuments.TryGetValue(_activeStudioDocumentId,out context)||
                !ResourceDocumentExists(_activeStudioDocumentId)||
                !ReferenceEquals(context.Session,_viewModel.ProjectSession))
            {
                _viewModel.ReportStudioShellWarning("当前页面文档已失效，已拒绝编辑命令。");
                return false;
            }
            // 旧Ribbon仍与页面级ViewModel绑定；非页面资源请使用资源Tab工具栏，
            // 不允许悄悄向另一页面写入图元/流程。
            if(kind!="page")
            {
                _viewModel.ReportStudioShellWarning("当前资源Tab请使用资源编辑器工具栏，已拒绝页面级Ribbon写入。");
                return false;
            }
            MEMPage page=_viewModel.ProjectSession.Find(id) as MEMPage;
            if(_viewModel.ActivePage==null||_viewModel.ActivePage.Id!=id)
                _viewModel.ActivePage=page;
            return true;
        }

        private void AddRibbonButton(string label, string tag, RoutedEventHandler handler,
            bool resourceVisualCommand = false)
        {
            Button button = new Button { Content = label, Tag = tag, Margin = new Thickness(2),
                Padding = new Thickness(12, 8, 12, 8) };
            button.Click += (sender, args) =>
            {
                // 仅已接入的绘图/排列/图层能力路由到资源Editor，其它旧命令保持Guard。
                if (resourceVisualCommand && IsResourceDocumentId(_activeStudioDocumentId))
                    handler(sender, args);
                else if (EnsureActiveStudioPageForRibbon())
                    handler(sender, args);
            };
            StudioRibbonActions.Children.Add(button);
        }

        private void UpdateStudioRibbon()
        {
            if (StudioRibbonActions == null || _legacyRibbonActions.Count == 0) return;
            StudioRibbonActions.Children.Clear();
            string group = _ribbonSelection;
            foreach (UIElement item in _legacyRibbonActions)
            {
                if (RibbonGroupOf(item) == group ||
                    (group == "主页" && (RibbonGroupOf(item) == "文件" || RibbonGroupOf(item) == "工程")) ||
                    (group == "资源" && RibbonGroupOf(item) == "工程"))
                    StudioRibbonActions.Children.Add(item);
            }
            if (group == "绘图")
            {
                AddRibbonButton("选择", null, (s, e) =>
                {
                    ResourceDocumentContext active=ActiveStudioResourceContext();
                    if(active!=null&&!active.IsFlow&&active.Scene!=null)
                        active.Scene.Focus();
                    else OpenStudioDocument("doc.visual");
                }, true);
                AddRibbonButton("添加矩形", null, (s, e) =>
                {
                    if(RouteStudioResourceVisualInsertion("Rect"))return;
                    if (_viewModel.AddCommand.CanExecute(null)) _viewModel.AddCommand.Execute(null);
                }, true);
                AddRibbonButton("直线", "Line", OnInsertShape, true);
                AddRibbonButton("矩形", "Rect", OnInsertShape, true);
                AddRibbonButton("圆", "Circle", OnInsertShape, true);
                AddRibbonButton("椭圆", "Ellipse", OnInsertShape, true);
                AddRibbonButton("折线", "Polyline", OnInsertShape, true);
                AddRibbonButton("多边形", "Polygon", OnInsertShape, true);
                AddRibbonButton("圆弧", "Arc", OnInsertShape, true);
                AddRibbonButton("贝塞尔", "Bezier", OnInsertShape, true);
                AddRibbonButton("自由路径", "Path", OnInsertShape, true);
                AddRibbonButton("文字", "Text", OnInsertShape, true);
                AddRibbonButton("按钮Widget", "WidgetButton", OnInsertShape, true);
            }
            else if (group == "排列")
            {
                AddRibbonButton("左对齐", "left", OnAlignVisuals, true);
                AddRibbonButton("顶对齐", "top", OnAlignVisuals, true);
                AddRibbonButton("水平居中", "centerX", OnAlignVisuals, true);
                AddRibbonButton("垂直居中", "centerY", OnAlignVisuals, true);
                AddRibbonButton("水平分布", "distributeX", OnAlignVisuals, true);
            }
            else if (group == "图层")
            {
                AddRibbonButton("置于顶层", null, OnBringVisualFront, true);
                AddRibbonButton("置于底层", null, OnSendVisualBack, true);
            }
            else if (group == "绑定") AddRibbonButton("绑定变量", null, OnApplyVisualBinding, true);
            else if (group == "节点")
            {
                AddRibbonButton("添加流程能力", null, OnAddWiringAction, true);
                AddRibbonButton("插入分支Join", null, OnInsertSelectedJoin, true);
            }
            else if (group == "连线") AddRibbonButton("开始连线", null, OnToggleWiring, true);
            else if (group == "编排")
            {
                AddRibbonButton("校验编排", null, OnValidateWiring, true);
                AddRibbonButton("自动布局", null, OnFlowArrange, true);
                AddRibbonButton("仿真执行", null, OnRunLogicDemo);
            }
            else if (group == "端口") AddRibbonButton("添加Port", null, OnFlowAddPort, true);
            else if (group == "配置") AddRibbonButton("配置编辑", null, OnConfigDemo);
            else if (group == "视图")
            {
                AddRibbonButton("资源树", null, (s,e)=>ShowStudioTool("tool.resources"));
                AddRibbonButton("属性", null, (s,e)=>ShowStudioTool("tool.properties"));
                AddRibbonButton("图形结构", null, (s,e)=>ShowStudioTool("tool.graphics"));
                AddRibbonButton("输出", null, (s,e)=>ShowStudioTool("tool.output"));
                AddRibbonButton("保存布局", null, (s,e)=>SafeStudioAction(()=>SaveStudioLayout(LayoutFile)));
                AddRibbonButton("恢复布局", null, (s,e)=>SafeStudioAction(()=>RestoreStudioLayout(LayoutFile)));
                AddRibbonButton("重置布局", null, (s,e)=>SafeStudioAction(ResetStudioLayout));
            }
            else if (group == "运行") AddRibbonButton("CPU仿真执行", null, OnRunLogicDemo);
            else if (group == "调试")
            {
                AddRibbonButton("暂停", null, OnFlowDebugPause);
                AddRibbonButton("继续", null, OnFlowDebugResume);
                AddRibbonButton("单步", null, OnFlowDebugStep);
            }
            else if (group == "帮助") AddRibbonButton("文档入口", null, (s,e)=>
                MessageBox.Show(this, "开发入口：DOCS/00_CxVisual_协同开发入口与七仓复用基线.md\n设计规范：DOCS/界面设计。", "CxVisual帮助"));
                    foreach(StudioCommandContribution registered in _studioCommands.Values
                .Where(x=>x.DocumentId==_activeStudioDocumentId&&x.Group==group)
                .OrderBy(x=>x.Id,StringComparer.Ordinal))
                StudioRibbonActions.Children.Add(new Button {
                    Content=registered.Label,Tag=registered.Id,Command=registered.Adapter,
                    Margin=new Thickness(2),Padding=new Thickness(12,8,12,8) });
}

        private void SafeStudioAction(Action command)
        {
            try { command(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Studio工作区", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void OnStudioThemeClick(object sender, RoutedEventArgs args)
        {
            ApplyStudioTheme(!_studioDark);
        }

        public void ApplyStudioTheme(bool dark)
        {
            _studioDark = dark;
            StudioDock.Theme = dark ? (Theme)new Vs2013DarkTheme() : new Vs2013LightTheme();
            SetStudioColor("StudioBackgroundBrush", dark ? "#252526" : "#F3F6FA");
            SetStudioColor("StudioPrimaryBrush", dark ? "#333337" : "#182D4B");
            SetStudioColor("StudioPanelBrush", dark ? "#2D2D30" : "#FFFFFF");
            SetStudioColor("StudioTextBrush", dark ? "#E4E4E7" : "#243447");
            SetStudioColor("StudioSurfaceBrush", dark ? "#333337" : "#E9EFF7");
            SetStudioColor("StudioBorderBrush", dark ? "#49494F" : "#D2DDEA");
            SetStudioColor("StudioAccentBrush", dark ? "#80B9FF" : "#1969C5");
        }

        private static void SetStudioColor(string key, string hex)
        {
            Application.Current.Resources[key] =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        private sealed class Utf8Writer : StringWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }
        }

        public string SerializeStudioLayout()
        {
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(StudioDock);
            using (Utf8Writer writer = new Utf8Writer())
            {
                serializer.Serialize(writer);
                return writer.ToString();
            }
        }

        public static void ValidateStudioLayoutXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml) ||
                Encoding.UTF8.GetByteCount(xml) > LayoutByteLimit)
                throw new InvalidDataException("布局文件为空或超过1MB上限。");
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = LayoutByteLimit, MaxCharactersFromEntities = 0
            };
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            using (StringReader text = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(text, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    string id = reader.GetAttribute("ContentId");
                    if (id == null || id.Length == 0) continue;
                    if (!IsKnownContent(id) || !seen.Add(id))
                        throw new InvalidDataException("布局含未知或重复ContentId：" + id);
                }
            }
        }

        public void SaveStudioLayout(string path)
        {
            string xml = SerializeStudioLayout();
            ValidateStudioLayoutXml(xml);
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("布局路径无效。");
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, xml, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public void RestoreStudioLayout(string path)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists || file.Length > LayoutByteLimit) throw new InvalidDataException("布局不存在或超出限制。");
            string xml = File.ReadAllText(path, Encoding.UTF8);
            ValidateStudioLayoutXml(xml);
            ValidateResourceDocumentLayout(xml);
            string backup = SerializeStudioLayout();
            try { DeserializeStudioLayout(xml); EnsureStudioContentIds(); }
            catch
            {
                try { DeserializeStudioLayout(backup); }
                catch (Exception recovery)
                { _viewModel.ReportStudioShellWarning("恢复默认前的工作区失败：" + recovery.Message); }
                throw;
            }
        }

        private void ValidateResourceDocumentLayout(string xml)
        {
            // 用户布局仅能引用当前MEM工程真实页面；不接受失效/跨工程DocumentId。
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = LayoutByteLimit, MaxCharactersFromEntities = 0
            };
            using (StringReader text = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(text, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    string key = reader.GetAttribute("ContentId");
                    Guid id;string kind;
                    if (TryParseResourceDocumentId(key,out id,out kind) &&
                        !ResourceDocumentExists(key))
                        throw new InvalidDataException("工作区包含失效或跨工程的页面：" + id);
                }
            }
        }

        private void DeserializeStudioLayout(string xml)
        {
            ValidateStudioLayoutXml(xml);
            foreach (LayoutContent item in ExistingContents().ToArray()) item.Content = null;
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(StudioDock);
            serializer.LayoutSerializationCallback += (sender, args) =>
            {
                object view;
                if (!_studioViews.TryGetValue(args.Model.ContentId, out view))
                {
                    Guid pageId;string kind;
                    if (TryParseResourceDocumentId(args.Model.ContentId,out pageId,out kind))
                    {
                        ResourceDocumentContext context = CreateStudioPageContext(args.Model.ContentId);
                        view = context.Panel;
                    }
                    else
                        view = args.Model.ContentId == "doc.config" ?
                            CreateConfigurationDocument() : null;
                }
                if (view == null) { args.Cancel = true; return; }
                _studioViews[args.Model.ContentId] = view;
                args.Content = view;
                LayoutDocument pageModel = args.Model as LayoutDocument;
                if (pageModel != null && IsResourceDocumentId(pageModel.ContentId))
                    RegisterStudioPageClosing(pageModel);
            };
            using (StringReader text = new StringReader(xml)) serializer.Deserialize(text);
            foreach (string key in _pageDocuments.Keys.ToArray())
            {
                if (FindStudioContent(key) == null)
                {
                    ResourceDocumentContext orphan = _pageDocuments[key];
                    _pageDocuments.Remove(key);
                    _studioViews.Remove(key);
                    orphan.Dispose();
                }
            }
            LayoutDocument active = StudioDock.Layout.ActiveContent as LayoutDocument;
            if (active != null) SelectStudioContext(active.ContentId);
            UpdateStudioGraphicsVisibility();
            RefreshStudioGraphicsTree();
            RefreshStudioMenus();
        }

        public void ResetStudioLayout()
        {
            if (string.IsNullOrWhiteSpace(_defaultStudioLayout))
                throw new InvalidOperationException("尚未记录默认工作区布局。");
            DeserializeStudioLayout(_defaultStudioLayout);
        }

        private void OnStudioShellClosing(CancelEventArgs args)
        {
            if (args.Cancel || !_studioInitialized ||
                Environment.GetCommandLineArgs().Contains("--shell-self-test", StringComparer.OrdinalIgnoreCase)) return;
            try { SaveStudioLayout(LayoutFile); }
            catch (Exception ex) { _viewModel.ReportStudioShellWarning("保存用户布局失败：" + ex.Message); }
        }
    }
}
