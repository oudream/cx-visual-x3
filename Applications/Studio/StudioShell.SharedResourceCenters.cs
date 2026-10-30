using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CxVisual.Mechanism.Core;

namespace CxVisual.Studio
{
    /// <summary>
    /// Content/Symbol 两个独立管理中心：当前工程定义、外部包、已注册类型三路投影。
    /// 唯一工程数据源仍为 MEM；导入/导出/创建/引用均走 MEM/MPM/MTI 正式契约。
    /// </summary>
    public partial class MainWindow
    {
        private StudioSharedResourceCenter _contentManagementCenter;
        private StudioSharedResourceCenter _symbolManagementCenter;

        public void OpenStudioSharedResourceCenter(bool symbol)
        {
            OpenStudioDocument(symbol ? "doc.symbol-center" : "doc.content-center");
        }

        private FrameworkElement CreateStudioSharedResourceCenterDocument(bool symbol)
        {
            StudioSharedResourceCenter old = symbol ? _symbolManagementCenter : _contentManagementCenter;
            if (old != null) old.Dispose();
            StudioSharedResourceCenter center = new StudioSharedResourceCenter(this, symbol);
            if (symbol) _symbolManagementCenter = center;
            else _contentManagementCenter = center;
            return center;
        }

        private void ReleaseStudioSharedResourceCenter(string key)
        {
            bool symbol = key == "doc.symbol-center";
            StudioSharedResourceCenter center = symbol ? _symbolManagementCenter : _contentManagementCenter;
            if (center != null) center.Dispose();
            if (symbol) _symbolManagementCenter = null;
            else _contentManagementCenter = null;
            _studioViews.Remove(key);
        }

        private void OnStudioSharedResourceCentersChanged(object sender, EventArgs args)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshStudioSharedResourceCenters));
                return;
            }
            RefreshStudioSharedResourceCenters();
        }

        private void RefreshStudioSharedResourceCenters()
        {
            if (_contentManagementCenter != null) _contentManagementCenter.Refresh();
            if (_symbolManagementCenter != null) _symbolManagementCenter.Refresh();
        }

        /// <summary>可信专业代码经现有IMPMTypeProvider注册冻结类型契约，
        /// 两中心只读取MEM注册结果，不做未知DLL反射扫描或私有CLR工厂持久化。</summary>
        public void RegisterStudioSharedResourceTypes(IMPMTypeProvider provider)
        {
            if(provider==null)throw new ArgumentNullException("provider");
            _viewModel.ProjectSession.RegisterTypes(provider);
            RefreshStudioSharedResourceCenters();
        }

        // These methods are also consumed by STA self-tests without native file dialogs.
        internal Guid CreateStudioSharedDefinition(bool symbol, string name, string typeId = null)
        {
            MEMEngineeringSession session = _viewModel.ProjectSession;
            MEMNode created;
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("资源名称不能为空");
            if (!String.IsNullOrWhiteSpace(typeId))
            {
                MPMTypeDescriptor descriptor = session.FindType(typeId);
                if (descriptor == null || descriptor.Kind != (symbol ? MEMNodeKind.Symbol : MEMNodeKind.Content))
                    throw new InvalidOperationException("未注册的Content/Symbol类型或类型不匹配");
                created = symbol ? (MEMNode)session.AddTypedSymbol(name, typeId) : session.AddTypedContent(session.Project, name, typeId);
            }
            else created = symbol ? (MEMNode)session.AddSymbol(name) :
                session.AddContent(session.Project, name);
            return created.Id;
        }

        internal Guid ImportStudioSharedDefinition(bool symbol, string path, Guid sourceId)
        {
            using (MEMEngineeringSession package = MEMEngineering.Open(path))
            {
                MEMNode definition = package.Find(sourceId);
                if ((symbol && !(definition is MEMSymbol)) ||
                    (!symbol && !(definition is MEMContent)) ||
                    definition.Parent != package.Project)
                    throw new InvalidOperationException("资源包不存在对应的独立Content/Symbol定义");
                MEMEngineeringSession target = _viewModel.ProjectSession;
                MTITransferPlan plan = target.PreviewImport(package,
                    new[] { sourceId }, target.Project, MTIExternalPolicy.IncludeClosure);
                if (!plan.IsValid)
                    throw new InvalidOperationException("资源包导入校验失败：" + String.Join(";", plan.Issues));
                MTITransferResult result = target.Import(plan);
                return result.IdMap[sourceId];
            }
        }

        internal Guid PlaceStudioSharedDefinition(bool symbol, Guid definitionId, Guid pageId)
        {
            MEMEngineeringSession session = _viewModel.ProjectSession;
            MEMPage page = session.Find(pageId) as MEMPage;
            MEMNode resource = session.Find(definitionId);
            if (page == null || resource == null || resource.Parent != session.Project)
                throw new InvalidOperationException("页面或共享定义已失效，禁止跨工程放置");
            MRIResourceReference reference;
            if (symbol && resource is MEMSymbol)
                reference = session.PlaceSymbol(page.RootContent, (MEMSymbol)resource, resource.Name);
            else if (!symbol && resource is MEMContent)
                reference = session.PlaceContent(page.RootContent, (MEMContent)resource, resource.Name);
            else throw new InvalidOperationException("共享定义类别不匹配");
            return reference.Id;
        }

        internal void SetStudioSharedInstanceOverride(Guid instanceId, string member, string literal)
        {
            MEMEngineeringSession session=_viewModel.ProjectSession;
            MRIResourceReference reference=session.FindReference(instanceId);
            if(reference==null)throw new InvalidOperationException("实例已失效");
            MEMNode definition=session.Find(reference.TargetId);
            IMPMDefinition contract=definition as IMPMDefinition;
            MPMProperty property=contract==null?null:contract.Properties.FirstOrDefault(p=>p.MemberId==member);
            if(property==null||!property.Descriptor.IsPublic||property.Descriptor.IsReadOnly)
                throw new InvalidOperationException("参数不允许实例覆盖");
            MPMBinding value;
            switch(property.Descriptor.ValueKind)
            {
                case MPMValueKind.String:value=MPMBinding.Literal(MPMValue.String(literal));break;
                case MPMValueKind.Number:value=MPMBinding.Literal(MPMValue.Number(
                    double.Parse(literal,CultureInfo.InvariantCulture)));break;
                case MPMValueKind.Integer:value=MPMBinding.Literal(MPMValue.Integer(
                    long.Parse(literal,CultureInfo.InvariantCulture)));break;
                case MPMValueKind.Decimal:value=MPMBinding.Literal(MPMValue.Decimal(
                    decimal.Parse(literal,CultureInfo.InvariantCulture)));break;
                case MPMValueKind.Boolean:value=MPMBinding.Literal(MPMValue.Boolean(
                    Boolean.Parse(literal)));break;
                case MPMValueKind.Resource:value=MPMBinding.Resource(Guid.Parse(literal));break;
                default:throw new InvalidOperationException("参数类型不支持直接文字覆盖");
            }
            session.SetOverride(reference,member,value);
        }

        internal void ClearStudioSharedInstanceOverride(Guid instanceId,string member)
        {
            MEMEngineeringSession session=_viewModel.ProjectSession;
            MRIResourceReference reference=session.FindReference(instanceId);
            if(reference==null)throw new InvalidOperationException("实例已失效");
            session.ClearOverride(reference,member);
        }

        private sealed class StudioSharedResourceCenter : DockPanel, IDisposable
        {
            private readonly MainWindow _host;
            private readonly bool _symbol;
            private MEMEngineeringSession _package;
            private MEMEngineeringSession _project;
            private readonly ListBox _local = new ListBox();
            private readonly ListBox _external = new ListBox();
            private readonly ListBox _types = new ListBox();
            private readonly ComboBox _pages = new ComboBox { MinWidth = 140, Margin = new Thickness(3) };
            private readonly ComboBox _instances = new ComboBox { MinWidth = 145, Margin = new Thickness(3) };
            private readonly ComboBox _members = new ComboBox { MinWidth = 120, Margin = new Thickness(3) };
            private readonly TextBox _overrideText = new TextBox { MinWidth = 120, Margin = new Thickness(3) };
            private readonly TextBox _search = new TextBox { MinWidth = 170, Margin = new Thickness(3) };
            private readonly TextBox _name = new TextBox { MinWidth = 140, Margin = new Thickness(3) };
            private readonly TextBlock _details = new TextBlock { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
            private readonly TextBlock _feedback = new TextBlock { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
            private readonly TabControl _catalog = new TabControl();
            private bool _disposed;
            private string _sourcePath;

            internal StudioSharedResourceCenter(MainWindow host, bool symbol)
            {
                _host = host; _symbol = symbol;
                LastProject = Guid.Empty;
                LastRevision = -1;
                Background = System.Windows.Media.Brushes.Transparent;
                var title = new TextBlock {
                    Text = symbol ? "Symbol 管理中心 · 系统类型 / 组件定义 / 工程共享" :
                        "Content 管理中心 · 系统类型 / 内容定义 / 工程共享",
                    FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8)
                };
                SetDock(title, Dock.Top); Children.Add(title);
                var description = new TextBlock {
                    Text = "三类来源分别管理：当前工程的可复用定义、外部资源包和已注册类型。选择后编辑/引用，所有变更提交到唯一MEM工程。",
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 2, 8, 7)
                };
                SetDock(description, Dock.Top); Children.Add(description);
                var searchBar = new WrapPanel { Margin = new Thickness(4) };
                searchBar.Children.Add(new TextBlock { Text = "筛选", VerticalAlignment = VerticalAlignment.Center });
                searchBar.Children.Add(_search);
                searchBar.Children.Add(new TextBlock { Text = "新资源名称", VerticalAlignment = VerticalAlignment.Center });
                searchBar.Children.Add(_name);
                SetDock(searchBar, Dock.Top); Children.Add(searchBar);
                var tools = new WrapPanel { Margin = new Thickness(4) };
                AddAction(tools, "新建空白", () => NewDefinition(false));
                AddAction(tools, "按注册类型创建", () => NewDefinition(true));
                AddAction(tools, "打开编辑器", OpenEditor);
                if (!_symbol) AddAction(tools, "编辑流程", OpenFlowEditor);
                AddAction(tools, "加载资源包", OpenPackagePicker);
                AddAction(tools, "导入选中", ImportSelected);
                AddAction(tools, "导出所选", ExportSelected);
                AddAction(tools, "依赖影响", ShowImpact);
                AddAction(tools, "刷新", () => Refresh());
                SetDock(tools, Dock.Top); Children.Add(tools);
                var placeBar = new WrapPanel { Margin = new Thickness(4) };
                placeBar.Children.Add(new TextBlock { Text = "将共享定义加入页面", VerticalAlignment = VerticalAlignment.Center });
                placeBar.Children.Add(_pages);
                AddAction(placeBar, "添加引用实例", PlaceSelected);
                SetDock(placeBar, Dock.Top); Children.Add(placeBar);
                var overrides = new WrapPanel { Margin = new Thickness(4) };
                overrides.Children.Add(new TextBlock { Text = "实例二次配置", VerticalAlignment = VerticalAlignment.Center });
                overrides.Children.Add(_instances);
                overrides.Children.Add(_members);
                overrides.Children.Add(_overrideText);
                AddAction(overrides, "覆盖公开参数", ApplyOverride);
                AddAction(overrides, "恢复继承", ClearOverride);
                SetDock(overrides, Dock.Top); Children.Add(overrides);
                _instances.SelectionChanged+=(s,e)=>UpdateMembers();
                _members.SelectionChanged+=(s,e)=>UpdateOverrideValue();
                _catalog.Items.Add(new TabItem { Header = "本工程共享定义", Content = _local });
                _catalog.Items.Add(new TabItem { Header = "外部资源包", Content = _external });
                _catalog.Items.Add(new TabItem { Header = "工程已注册类型", Content = _types });
                _catalog.SelectionChanged += (s,e) => { if (e.Source == _catalog) UpdateDetails(); };
                _local.SelectionChanged += (s,e) => { UpdateInstances();UpdateDetails(); };
                _external.SelectionChanged += (s,e) => UpdateDetails();
                _types.SelectionChanged += (s,e) => UpdateDetails();
                _local.MouseDoubleClick += (s,e) => TryAction(OpenEditor);
                _search.TextChanged += (s,e) => Refresh(true);
                SetDock(_feedback, Dock.Bottom); Children.Add(_feedback);
                SetDock(_details, Dock.Bottom); Children.Add(_details);
                Children.Add(_catalog);
                Refresh();
            }

            internal Guid LastProject { get; private set; }
            internal int LastRevision { get; private set; }
            internal int LocalCount { get { return _local.Items.Count; } }
            internal int PackageCount { get { return _external.Items.Count; } }
            internal int TypeCount { get { return _types.Items.Count; } }
            private static void AddAction(Panel panel, string label, Action action)
            {
                var button = new Button {
                    Content = label, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(3)
                };
                button.Click += (s,e) => {
                    try { action(); }
                    catch (Exception ex) {
                        MessageBox.Show(ex.Message, "资源操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
                panel.Children.Add(button);
            }
            private void TryAction(Action action)
            {
                try { action(); }
                catch(Exception ex) { _feedback.Text = "操作失败：" + ex.Message; }
            }
            private MEMEngineeringSession Session()
            {
                if (_disposed) throw new ObjectDisposedException("StudioSharedResourceCenter");
                MEMEngineeringSession session = _host._viewModel.ProjectSession;
                if (session.IsClosed) throw new InvalidOperationException("工程会话已关闭");
                return session;
            }
            private bool Matches(string value)
            {
                return String.IsNullOrWhiteSpace(_search.Text) ||
                    (value ?? "").IndexOf(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            }
            private static Guid? SelectedGuid(ListBox list)
            {
                ListBoxItem item = list.SelectedItem as ListBoxItem;
                return item == null ? (Guid?)null : (Guid)item.Tag;
            }
            private static string SelectedType(ListBox list)
            {
                ListBoxItem item = list.SelectedItem as ListBoxItem;
                return item == null ? null : item.Tag as string;
            }
            private void FillList(ListBox list, IEnumerable<IMEMResource> values, Guid? selected)
            {
                list.Items.Clear();
                foreach (IMEMResource resource in values)
                {
                    if (!Matches(resource.Name) && !Matches(resource.TypeId)) continue;
                    ListBoxItem item = new ListBoxItem {
                        Content = resource.Name + "    [" + resource.TypeId + "]", Tag = resource.Id };
                    list.Items.Add(item);
                    if (selected == resource.Id) item.IsSelected = true;
                }
            }
            internal void Refresh(bool force=false)
            {
                if (_disposed) return;
                MEMEngineeringSession session = Session();
                if (!force && ReferenceEquals(session,_project) &&
                    LastProject==session.Project.Id && LastRevision==session.Revision)
                    return;
                // 换工程后清除任何旧工程所选对象；包只是外部只读来源。
                if (!ReferenceEquals(_project,session))
                {
                    _local.SelectedItem = null; _types.SelectedItem = null;
                    _project = session;
                }
                Guid? selected = SelectedGuid(_local);
                Guid? external = SelectedGuid(_external);
                string type = SelectedType(_types);
                IEnumerable<IMEMResource> resources = _symbol
                    ? session.Project.Symbols.Cast<IMEMResource>() :
                      session.Project.Contents.Cast<IMEMResource>();
                FillList(_local, resources, selected);
                IEnumerable<IMEMResource> packages = _package == null
                    ? Enumerable.Empty<IMEMResource>()
                    : _symbol ? _package.Project.Symbols.Cast<IMEMResource>() :
                                _package.Project.Contents.Cast<IMEMResource>();
                FillList(_external, packages, external);
                _types.Items.Clear();
                foreach (MPMTypeDescriptor descriptor in session.RegisteredTypes
                    .Where(x=>x.Kind==(_symbol?MEMNodeKind.Symbol:MEMNodeKind.Content)))
                {
                    if (!Matches(descriptor.TypeId)) continue;
                    var item = new ListBoxItem { Content = descriptor.TypeId + " / Schema " +
                        descriptor.SchemaVersion, Tag = descriptor.TypeId };
                    _types.Items.Add(item);
                    if (type==descriptor.TypeId) item.IsSelected = true;
                }
                ComboBoxItem oldPage = _pages.SelectedItem as ComboBoxItem;
                Guid? page = oldPage==null ? (Guid?)null : (Guid)oldPage.Tag;
                _pages.Items.Clear();
                foreach (MEMPage item in session.Project.Pages)
                {
                    ComboBoxItem choice = new ComboBoxItem { Content = item.Name, Tag = item.Id };
                    _pages.Items.Add(choice);
                    if (page == item.Id) choice.IsSelected = true;
                }
                if (_pages.SelectedItem == null && _pages.Items.Count > 0) _pages.SelectedIndex = 0;
                LastProject = session.Project.Id; LastRevision = session.Revision;
                UpdateInstances();
                UpdateDetails();
            }
            private Guid? SelectedLocal()
            {
                Guid? id = SelectedGuid(_local);
                if (!id.HasValue) return null;
                MEMNode node = Session().Find(id.Value);
                return (_symbol && node is MEMSymbol || !_symbol && node is MEMContent)
                    && node.Parent==Session().Project ? id : null;
            }
            private Guid? SelectedExternal()
            {
                Guid? id = SelectedGuid(_external);
                if (!id.HasValue || _package == null) return null;
                MEMNode node = _package.Find(id.Value);
                return (_symbol && node is MEMSymbol || !_symbol && node is MEMContent)
                    && node.Parent==_package.Project ? id : null;
            }
            private static Guid? SelectedComboGuid(ComboBox control)
            {
                ComboBoxItem item=control.SelectedItem as ComboBoxItem;
                return item==null?(Guid?)null:(Guid)item.Tag;
            }
            private string SelectedMember()
            {
                ComboBoxItem item=_members.SelectedItem as ComboBoxItem;
                return item==null?null:item.Tag as string;
            }
            private void UpdateInstances()
            {
                Guid? definition=SelectedLocal();
                Guid? prior=SelectedComboGuid(_instances);
                _instances.Items.Clear();
                MEMEngineeringSession session=Session();
                if(!definition.HasValue){_members.Items.Clear();return;}
                var uses=session.Project.DescendantsAndSelf().OfType<MEMContent>()
                    .SelectMany(x=>x.References)
                    .Concat(session.Project.DescendantsAndSelf().OfType<MEMSymbol>()
                        .SelectMany(x=>x.References))
                    .Where(x=>x.TargetId==definition.Value);
                foreach(MRIResourceReference use in uses)
                {
                    var entry=new ComboBoxItem {Content=use.Owner.Name+" / "+use.Name,Tag=use.Id};
                    _instances.Items.Add(entry);
                    if(prior==use.Id)entry.IsSelected=true;
                }
                if(_instances.SelectedItem==null && _instances.Items.Count!=0)_instances.SelectedIndex=0;
                UpdateMembers();
            }
            private void UpdateMembers()
            {
                string old=SelectedMember();
                _members.Items.Clear();
                Guid? id=SelectedComboGuid(_instances);
                MRIResourceReference reference=id.HasValue?Session().FindReference(id.Value):null;
                IMPMDefinition definition=reference==null?null:Session().Find(reference.TargetId) as IMPMDefinition;
                if(definition==null){_overrideText.Text="";return;}
                foreach(MPMProperty p in definition.Properties.Where(p=>p.Descriptor.IsPublic&&!p.Descriptor.IsReadOnly))
                {
                    var option=new ComboBoxItem {Content=p.MemberId+" ["+p.Descriptor.ValueKind+"]",Tag=p.MemberId};
                    _members.Items.Add(option);
                    if(old==p.MemberId)option.IsSelected=true;
                }
                if(_members.SelectedItem==null && _members.Items.Count!=0)_members.SelectedIndex=0;
                UpdateOverrideValue();
            }
            private void UpdateOverrideValue()
            {
                Guid? id=SelectedComboGuid(_instances);
                string member=SelectedMember();
                MRIResourceReference reference=id.HasValue?Session().FindReference(id.Value):null;
                if(reference==null||member==null){_overrideText.Text="";return;}
                MPMBinding binding=Session().ResolveParameter(reference,member);
                _overrideText.Text=binding!=null&&binding.Kind==MPMBindingKind.Literal?
                    binding.Value.Canonical:binding!=null&&binding.Kind==MPMBindingKind.Resource?
                    binding.ResourceId.Value.ToString("D"):"";
            }
            private void ApplyOverride()
            {
                Guid? id=SelectedComboGuid(_instances);
                string member=SelectedMember();
                if(!id.HasValue||member==null)throw new InvalidOperationException("请选择实例和公开参数");
                _host.SetStudioSharedInstanceOverride(id.Value,member,_overrideText.Text);
                Refresh();
                _feedback.Text="已在MEM实例上覆盖 "+member+"，共享Content/Symbol定义未被修改";
            }
            private void ClearOverride()
            {
                Guid? id=SelectedComboGuid(_instances);
                string member=SelectedMember();
                if(!id.HasValue||member==null)throw new InvalidOperationException("请选择实例和公开参数");
                _host.ClearStudioSharedInstanceOverride(id.Value,member);
                Refresh();
                _feedback.Text="已清除实例覆盖，恢复继承共享定义的公开参数";
            }
            private void UpdateDetails()
            {
                if (_disposed) return;
                if (_catalog.SelectedIndex==2)
                {
                    string type=SelectedType(_types);
                    MPMTypeDescriptor descriptor=type==null?null:Session().FindType(type);
                    _details.Text=descriptor==null?"选择一个已注册的类型后创建定义。未注册的注解并不自动变成工程类型。":
                        "TypeId: "+descriptor.TypeId+" | Schema: "+descriptor.SchemaVersion+
                        " | 属性: "+descriptor.Properties.Count+" | 端口: "+descriptor.Ports.Count+
                        " | Aspect: "+descriptor.Aspects.Count;
                    return;
                }
                bool external = _catalog.SelectedIndex==1;
                Guid? id=external?SelectedExternal():SelectedLocal();
                MEMEngineeringSession source=external?_package:Session();
                MEMNode node=id.HasValue&&source!=null?source.Find(id.Value):null;
                if (node==null)
                {
                    _details.Text=external ? "选择加载的资源包定义，导入前先预览依赖和类型兼容性。" :
                        "选择工程中的共享定义。编辑器、实例和属性均使用当前MEM工程身份。";
                    return;
                }
                IMPMDefinition definition=node as IMPMDefinition;
                _details.Text=node.Name+" | TypeId: "+node.TypeId+
                    " | ID: "+node.Id.ToString("N")+" | 属性: "+definition.Properties.Count+
                    " | 端口: "+definition.Ports.Count+" | 引用: "+
                    (_symbol ? ((MEMSymbol)node).References.Count : ((MEMContent)node).References.Count)+
                    (external?" | 来源包: "+_sourcePath:" | 工程Revision: "+Session().Revision);
            }
            private void NewDefinition(bool typed)
            {
                string id=typed?SelectedType(_types):null;
                if(typed && id==null)throw new InvalidOperationException("请在‘工程已注册类型’页选择类型");
                string name=String.IsNullOrWhiteSpace(_name.Text)
                    ? (_symbol?"新组件 ":"新内容 ")+DateTime.Now.ToString("HHmmss")
                    :_name.Text.Trim();
                Guid created=_host.CreateStudioSharedDefinition(_symbol,name,id);
                Refresh();_catalog.SelectedIndex=0;Select(_local,created);
                _host.OpenStudioResourceDocument(created);
                _feedback.Text="已通过MEM创建独立共享定义："+name;
            }
            private static void Select(ListBox list,Guid id)
            {
                foreach(ListBoxItem item in list.Items.OfType<ListBoxItem>())
                    if((Guid)item.Tag==id){item.IsSelected=true;list.ScrollIntoView(item);return;}
            }
            private void OpenEditor()
            {
                Guid? id=SelectedLocal();
                if(!id.HasValue)throw new InvalidOperationException("请先选择当前工程中的共享定义，外部包需先导入");
                _host.OpenStudioResourceDocument(id.Value);
            }
            private void OpenFlowEditor()
            {
                Guid? id=SelectedLocal();
                if(!id.HasValue || _symbol)throw new InvalidOperationException("请先选择当前工程Content");
                _host.OpenStudioResourceDocument(id.Value,true);
            }
            private void OpenPackagePicker()
            {
                OpenFileDialog dialog=new OpenFileDialog {
                    Filter="CxVisual 工程/共享资源包|*.cxproj|全部文件|*.*",
                    CheckFileExists=true,Multiselect=false
                };
                if(dialog.ShowDialog()!=true)return;
                LoadPackage(dialog.FileName);
            }
            internal void LoadPackage(string path)
            {
                MEMEngineeringSession candidate=MEMEngineering.Open(path);
                if(candidate.Project.Contents.Count==0 && !_symbol ||
                    candidate.Project.Symbols.Count==0 && _symbol)
                {
                    candidate.Close(true);
                    throw new InvalidOperationException("该资源包没有可导入的对应类型共享定义");
                }
                MEMEngineeringSession previous=_package;
                _package=candidate; _sourcePath=path;
                if(previous!=null)previous.Close(true);
                Refresh(true);_catalog.SelectedIndex=1;
                _feedback.Text="资源包已加载为只读来源，尚未修改当前工程："+path;
            }
            private void ImportSelected()
            {
                Guid? id=SelectedExternal();
                if(!id.HasValue || _package==null)
                    throw new InvalidOperationException("请在外部资源包页选择Content/Symbol");
                MEMEngineeringSession target=Session();
                MTITransferPlan plan=target.PreviewImport(_package,new[]{id.Value},
                    target.Project,MTIExternalPolicy.IncludeClosure);
                if(!plan.IsValid)throw new InvalidOperationException(String.Join("；",plan.Issues));
                string message="将导入 "+plan.IdMap.Count+" 个身份及其依赖。\n"+
                    String.Join("\n",plan.Warnings ?? (IReadOnlyList<string>)new string[0])+"\n确定导入当前工程？";
                if(MessageBox.Show(message,"资源包导入预览",MessageBoxButton.OKCancel,
                    MessageBoxImage.Question)!=MessageBoxResult.OK)return;
                Guid created=target.Import(plan).IdMap[id.Value];
                Refresh();_catalog.SelectedIndex=0;Select(_local,created);
                _feedback.Text="已导入为新的工程定义，原包未修改。Revision: "+target.Revision;
            }
            private void ExportSelected()
            {
                Guid? id=SelectedLocal();
                if(!id.HasValue)throw new InvalidOperationException("选择本工程的共享定义");
                SaveFileDialog dialog=new SaveFileDialog {
                    Filter="CxVisual 资源包|*.cxproj", AddExtension=true,
                    FileName=(_symbol?"Symbol-":"Content-")+id.Value.ToString("N")+".cxproj"
                };
                if(dialog.ShowDialog()!=true)return;
                Session().ExportPackage(new[]{id.Value},dialog.FileName);
                _feedback.Text="已使用MEM/MTI导出带依赖的共享资源包："+dialog.FileName;
            }
            private void PlaceSelected()
            {
                Guid? id=SelectedLocal();
                ComboBoxItem page=_pages.SelectedItem as ComboBoxItem;
                if(!id.HasValue || page==null)throw new InvalidOperationException("请选择本工程共享定义和目标页面");
                Guid reference=_host.PlaceStudioSharedDefinition(_symbol,id.Value,(Guid)page.Tag);
                Refresh();
                foreach(ComboBoxItem item in _instances.Items.OfType<ComboBoxItem>())
                    if((Guid)item.Tag==reference){item.IsSelected=true;break;}
                _feedback.Text="已在目标页面新增独立引用实例："+reference.ToString("N")+
                    "。后续使用MEM实例覆盖进行二次配置。";
            }
            private void ShowImpact()
            {
                Guid? id=SelectedLocal();
                if(!id.HasValue)throw new InvalidOperationException("请选择工程定义");
                MRIResourceImpactReport report=Session().AnalyzeResourceImpact(id.Value);
                _details.Text="定义 "+id.Value.ToString("N")+" 影响分析 (Revision "+
                    report.Revision+"): "+report.Affected.Count+" 个受影响资源；"+
                    report.MissingDependencies.Count+" 个缺失依赖。\n"+
                    String.Join("\n",report.Affected.Take(20).Select(x=>
                        x.Resource.Name+" (深度"+x.Depth+")"));
            }
            public void Dispose()
            {
                if(_disposed)return;
                _disposed=true;
                if(_package!=null){_package.Close(true);_package=null;}
                _local.Items.Clear();_external.Items.Clear();_types.Items.Clear();
            }
        }
    }
}
