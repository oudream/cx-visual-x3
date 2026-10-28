using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>Ephemeral MOC read-only view, not another CapabilityCatalog or execution owner.</summary>
    public sealed class MPWModuleCapabilityView
    {
        public MPWModuleCapabilityView(string id,string name,string category,
            MFMModuleKind? kind,int version,bool resolved,bool demonstration,string moduleId=null,
            bool described=true,bool resolverChecked=true,
            IEnumerable<string> inputPorts=null,IEnumerable<string> outputPorts=null)
        {
            if(String.IsNullOrWhiteSpace(id)||String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("module.capability.identity");
            Id=id;Name=name;Category=category??"";Kind=kind;
            Version=version;Resolved=resolved;Demonstration=demonstration;
            ModuleId=String.IsNullOrWhiteSpace(moduleId)?null:moduleId.Trim();
            Described=described;ResolverChecked=resolverChecked;
            InputPorts=FreezePorts(inputPorts);
            OutputPorts=FreezePorts(outputPorts);
        }
        private static IReadOnlyList<string> FreezePorts(IEnumerable<string> source)
        {
            var values=(source??Enumerable.Empty<string>()).ToArray();
            if(values.Length>256||values.Any(x=>String.IsNullOrWhiteSpace(x)||x.Length>200))
                throw new ArgumentException("module.center.invalid_ports");
            return new ReadOnlyCollection<string>(values);
        }
        public IReadOnlyList<string> InputPorts {get;private set;}
        public IReadOnlyList<string> OutputPorts {get;private set;}
        public string Id {get;private set;}
        public string Name {get;private set;}
        public string Category {get;private set;}
        public MFMModuleKind? Kind {get;private set;}
        public int Version {get;private set;}
        public bool Resolved {get;private set;}
        public bool Demonstration {get;private set;}
        /// <summary>Explicit MFM association, null for an unbound capability.</summary>
        public string ModuleId {get;private set;}
        /// <summary>True only if the existing MOC directory was queried and held the definition.</summary>
        public bool Described {get;private set;}
        /// <summary>False means no resolver was checked for this configuration-only scope.</summary>
        public bool ResolverChecked {get;private set;}
        public string Label {get{return Name+" · "+Id+(ModuleId==null?"":" · "+ModuleId);}}
        public string Status {get{
            if(Demonstration)return "未绑定正式模块的演示Resolver，非设备就绪";
            if(!ResolverChecked)return "仅声明；MOC描述/Runtime解析均未核实";
            if(!Described)return "MOC未发现能力描述，不可视为技术支持";
            return Resolved?"Runtime解析存在（非业务就绪或授权）":"MOC已描述，但未解析运行处理者";
        }}
    }

    /// <summary>Shared WPF host surface; FCM/MHS, MEM, MOC, MRK remain the only owners.</summary>
    public sealed class MPWModuleCenterControl : UserControl
    {
        private readonly TabControl _pages=new TabControl {MinHeight=44,
            Margin=new Thickness(0,4,0,3),BorderThickness=new Thickness(0)};
        private readonly ContentControl _editor=new ContentControl();
        private readonly ContentControl _header=new ContentControl();
        private readonly TextBox _search=new TextBox {MinWidth=130,Margin=new Thickness(2,4,2,4)};
        private readonly ListBox _list=new ListBox {DisplayMemberPath="Label"};
        private readonly TextBlock _source=new TextBlock {TextWrapping=TextWrapping.Wrap};
        private readonly TextBlock _detail=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6)};
        private readonly WrapPanel _kinds=new WrapPanel();
        private readonly Button _allKinds=new Button {Content="全部",
            Margin=new Thickness(2),Padding=new Thickness(9,5,9,5)};
        private readonly ColumnDefinition _catalogColumn=new ColumnDefinition {Width=new GridLength(220)};
        private readonly DockPanel _catalogPane=new DockPanel {Margin=new Thickness(0,2,9,0)};
        private MPWModuleCapabilityView[] _catalog=new MPWModuleCapabilityView[0];
        private MFMModuleKind? _kind;
        private string _sourceName="暂无能力目录";
        public event Action<MFMModuleKind> ModuleKindRequested;
        public TabControl Pages {get{return _pages;}}
        public ContentControl EditorHost {get{return _editor;}}
        public object HeaderContent {get{return _header.Content;}set{_header.Content=value;}}
        public int VisibleCapabilityCount {get{return _list.Items.Count;}}
        /// <summary>UI-only read-only snapshot; no mutable registration or execution handle.</summary>
        public IReadOnlyList<MPWModuleCapabilityView> VisibleCapabilities
        {get{return _list.Items.Cast<MPWModuleCapabilityView>().ToArray();}}
        public string SearchText {get{return _search.Text;}set{_search.Text=value??"";}}
        public MFMModuleKind? SelectedKind {get{return _kind;}}
        /// <summary>Restore full-width FCM editing in TestCenter when no live catalog is wired.</summary>
        public bool ConfigurationOnly {
            get {return _catalogPane.Visibility==Visibility.Collapsed;}
            set {
                _catalogPane.Visibility=value?Visibility.Collapsed:Visibility.Visible;
                _catalogColumn.Width=value?new GridLength(0):new GridLength(220);
            }
        }
        public MPWModuleCenterControl()
        {
            AutomationProperties.SetAutomationId(this,"MPW.ModuleCenter");
            AutomationProperties.SetAutomationId(_search,"MPW.ModuleCenter.Search");
            AutomationProperties.SetAutomationId(_list,"MPW.ModuleCenter.Capabilities");
            var root=new DockPanel {Margin=new Thickness(9,6,9,7)};
            root.SetResourceReference(Panel.BackgroundProperty,"MPWBackgroundBrush");
            var head=new StackPanel();
            var title=new TextBlock {Text="功能模块中心 · Module Center",FontSize=20,
                FontWeight=FontWeights.SemiBold};
            title.SetResourceReference(TextBlock.ForegroundProperty,"MPWForegroundBrush");
            head.Children.Add(title);
            _allKinds.Click+=(s,e)=>SetSelectedKind(null);
            _kinds.Children.Add(_allKinds);
            foreach(MFMModuleKind kind in new[] {MFMModuleKind.Connector,MFMModuleKind.Driver,
                MFMModuleKind.Compute,MFMModuleKind.Simulation,MFMModuleKind.Service})
            {
                var capture=kind;
                var button=new Button {Content=kind.ToString(),Padding=new Thickness(9,5,9,5),
                    Margin=new Thickness(2)};
                button.Click+=(s,e)=>{
                    var callback=ModuleKindRequested;
                    if(callback==null)SetSelectedKind(capture);else callback(capture);
                };
                _kinds.Children.Add(button);
            }
            head.Children.Add(_kinds);head.Children.Add(_header);
            DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
            var grid=new Grid();
            grid.ColumnDefinitions.Add(_catalogColumn);
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            var left=_catalogPane;
            var filter=new StackPanel();
            filter.Children.Add(new TextBlock {Text="能力发现 / 搜索",FontWeight=FontWeights.SemiBold});
            filter.Children.Add(_search);
            _source.SetResourceReference(TextBlock.ForegroundProperty,"MPWMutedBrush");
            filter.Children.Add(_source);
            DockPanel.SetDock(filter,Dock.Top);left.Children.Add(filter);
            left.Children.Add(_list);grid.Children.Add(left);
            var right=new DockPanel();Grid.SetColumn(right,1);
            _pages.SetResourceReference(Control.BackgroundProperty,"MPWSurfaceBrush");
            DockPanel.SetDock(_pages,Dock.Top);right.Children.Add(_pages);
            _detail.SetResourceReference(TextBlock.ForegroundProperty,"MPWMutedBrush");
            DockPanel.SetDock(_detail,Dock.Bottom);right.Children.Add(_detail);
            _editor.Content=new TextBlock {
                Text="选择能力或打开正式模块配置；未接入Provider时不执行。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(12,18,12,12)};
            right.Children.Add(_editor);grid.Children.Add(right);root.Children.Add(grid);
            Content=root;
            _search.TextChanged+=(s,e)=>Refresh();
            _list.SelectionChanged+=(s,e)=>{
                var item=_list.SelectedItem as MPWModuleCapabilityView;
                _detail.Text=item==null?"请选择能力查看声明与运行解析信息。":
                    item.Name+" / V"+item.Version+" / "+item.Category+" · "+
                    (item.Kind.HasValue?item.Kind.Value.ToString():"未归属五类正式模块")+" · "+
                    (item.ModuleId??"无正式模块身份")+" · "+item.Status+
                    "；Declared="+(item.ModuleId==null?"未绑定模块":"已声明")+
                    "，Described="+(!item.ResolverChecked?"未核实":item.Described?"是":"否")+
                    "，RuntimeResolved="+(!item.ResolverChecked?"未核实":item.Resolved?"是":"否")+
                    "；Supported、Available、Authorized仍需目标Owner独立核验。"+
                    "\n输入："+(item.InputPorts.Count==0?"未声明":String.Join("、",item.InputPorts))+
                    "\n输出："+(item.OutputPorts.Count==0?"未声明":String.Join("、",item.OutputPorts));
            };
            SetCapabilities(null,"未注入运行能力；仅配置操作可用");
        }
        public void SetSelectedKind(MFMModuleKind? kind)
        {
            if(kind.HasValue&&!Enum.IsDefined(typeof(MFMModuleKind),kind.Value))
                throw new ArgumentOutOfRangeException("kind");
            _kind=kind;
            _allKinds.FontWeight=kind.HasValue?FontWeights.Normal:FontWeights.Bold;
            foreach(var button in _kinds.Children.OfType<Button>())
                button.FontWeight=kind.HasValue&&(string)button.Content==kind.ToString()
                    ?FontWeights.Bold:FontWeights.Normal;
            Refresh();
        }
        public void SetCapabilities(IEnumerable<MPWModuleCapabilityView> items,string source)
        {
            var values=(items??Enumerable.Empty<MPWModuleCapabilityView>()).ToArray();
            if(values.Length>4096||values.Any(x=>x==null))
                throw new ArgumentException("module.center.invalid_catalog_projection");
            _catalog=values;_sourceName=source??"";Refresh();
        }
        private void Refresh()
        {
            string q=(_search.Text??"").Trim();
            var found=_catalog.Where(x=>(!_kind.HasValue||x.Kind==_kind.Value)&&
                (q.Length==0||x.Id.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||
                x.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||
                x.Category.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
            _list.ItemsSource=found;
            _source.Text=_sourceName+"；"+found.Length+"/"+_catalog.Length+" 项";
        }
    }
}
