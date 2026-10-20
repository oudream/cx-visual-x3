using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Studio
{
    /// <summary>Only Studio editing state projected from an existing MPM descriptor; never an engineering source or a second Schema.</summary>
    public sealed class StudioPropertyItem : INotifyPropertyChanged
    {
        internal Guid OwnerId;
        internal int Revision;
        internal bool Reference,Visual;
        internal MPMValueKind Kind;
        internal MPMPropertyDescriptor Descriptor;
        internal Guid[] BatchOwnerIds;
        public int SelectionCount {get;internal set;}
        public bool IsReadOnly {get;internal set;}
        private bool _isMixedValue;
        public bool IsMixedValue
        {
            get{return _isMixedValue;}
            internal set
            {
                if(_isMixedValue==value)return;
                _isMixedValue=value;
                Notify("IsMixedValue");Notify("MixedValueDescription");Notify("NullableBooleanValue");Notify("ValidationMessage");
            }
        }
        public string MixedValueDescription {get{return IsMixedValue?"多个对象的值不一致，请指定统一新值":"";}}
        private string _text, _mode;
        private IMEMResource _selectedBindingResource;
        public IReadOnlyList<IMEMResource> ResourceChoices {get;internal set;}
        public IMEMResource SelectedBindingResource
        {
            get{return _selectedBindingResource;}
            set
            {
                if(object.ReferenceEquals(_selectedBindingResource,value))return;
                _selectedBindingResource=value;
                Notify("SelectedBindingResource");Notify("ValidationMessage");
            }
        }
        public bool IsResourceSource {get{return Mode=="资源";}}
        public string DefaultDescription {get;internal set;}
        public bool CanRestoreDefault {get;internal set;}
        public string[] BindingModes
        {
            get{return Kind==MPMValueKind.Resource?new[]{"资源","变量"}:new[]{"常量","变量"};}
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string member)
        {
            var handler=PropertyChanged;
            if(handler!=null)handler(this,new PropertyChangedEventArgs(member));
        }
        public string Member {get;internal set;}
        public string Schema {get;internal set;}
        public string Source {get;internal set;}
        public string Text
        {
            get{return _text;}
            set
            {
                if(_text==value)return;
                _text=value;
                if(_isMixedValue)IsMixedValue=false;
                Notify("Text");Notify("BooleanValue");Notify("NullableBooleanValue");Notify("ValidationMessage");
            }
        }
        public string Mode
        {
            get{return _mode;}
            set
            {
                if(_mode==value)return;
                _mode=value;
                // 从变量/资源切回布尔常量时让可见CheckBox与实际提交值保持一致。
                bool parsed;
                if(Kind==MPMValueKind.Boolean&&value=="常量"&&!bool.TryParse(Text,out parsed))Text="false";
                Notify("Mode");Notify("IsBooleanConstant");Notify("IsNumericConstant");Notify("IsResourceSource");Notify("ValidationMessage");
            }
        }
        public bool IsBooleanConstant {get{return Kind==MPMValueKind.Boolean&&Mode=="常量";}}
        public bool IsNumericConstant
        {get{return Mode=="常量"&&(Kind==MPMValueKind.Number||Kind==MPMValueKind.Integer||Kind==MPMValueKind.Decimal);}}
        public bool BooleanValue
        {
            get{return string.Equals(Text,"true",StringComparison.OrdinalIgnoreCase);}
            set{Text=value?"true":"false";}
        }
        public bool? NullableBooleanValue
        {
            get{return IsMixedValue?(bool?)null:BooleanValue;}
            set{if(value.HasValue)BooleanValue=value.Value;}
        }
        /// <summary>Advisory UI feedback. MEM/MPM remain the authoritative validator during transaction commit.</summary>
        public string ValidationMessage
        {
            get
            {
                if(IsReadOnly)return "所选属性只读，不允许编辑";
                if(IsMixedValue)return "多个对象的值不一致，请指定统一新值";
                if(IsResourceSource)
                {
                    if(Kind!=MPMValueKind.Resource)return "只有Resource类型属性可以引用工程资源";
                    if(SelectedBindingResource==null)return "请选择要引用的工程资源";
                    if(ResourceChoices==null||!ResourceChoices.Any(x=>x.Id==SelectedBindingResource.Id))
                        return "资源不属于当前工程或已失效";
                    return "";
                }
                if(Mode=="常量"&&Kind==MPMValueKind.Resource)return "Resource属性必须选择资源来源";
                if(!IsNumericConstant)return "";
                double number;
                if(Kind==MPMValueKind.Integer)
                {
                    long integerValue;
                    if(!long.TryParse(Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out integerValue))
                        return "请输入有效整数";
                    number=integerValue;
                }
                else if(Kind==MPMValueKind.Decimal)
                {
                    decimal decimalValue;
                    if(!decimal.TryParse(Text,NumberStyles.Number,CultureInfo.InvariantCulture,out decimalValue))
                        return "请输入有效十进制数值";
                    number=(double)decimalValue;
                }
                else if(!double.TryParse(Text,NumberStyles.Float|NumberStyles.AllowThousands,CultureInfo.InvariantCulture,out number)||
                    double.IsNaN(number)||double.IsInfinity(number))
                    return "请输入有效有限数值";
                if(Descriptor!=null&&
                    ((Descriptor.Minimum.HasValue&&number<Descriptor.Minimum.Value)||
                     (Descriptor.Maximum.HasValue&&number>Descriptor.Maximum.Value)))
                    return "数值超出工程Schema规定范围";
                return "";
            }
        }
    }
    public sealed class StudioPortItem
    {internal MPCEndpoint Endpoint;public string Name {get;internal set;}}
    public sealed class StudioConnectionItem
    {internal Guid Id;public string Name {get;internal set;}}
    public sealed partial class StudioViewModel
    {
        private readonly Dispatcher _dispatcher=Dispatcher.CurrentDispatcher;
        private int _refreshQueued;
        private Guid? _resourceId;
        private bool _refreshing;
        private bool _notifying;
        public bool IsRefreshing {get{return _refreshing||_notifying;}}
        private Guid[] _selection=new Guid[0];
        public string PropertySelectionHint {get;private set;}
        public Func<MEMEngineeringSession,bool> ConfirmReplace {get;set;}
        public MEMEngineeringSession ProjectSession {get{return _editor.Document;}}
        public ObservableCollection<MEMPage> Pages {get;private set;}
        public ObservableCollection<IMEMResource> Resources {get;private set;}
        public ObservableCollection<StudioPropertyItem> Properties {get;private set;}
        public ObservableCollection<StudioPortItem> Ports {get;private set;}
        public StudioPortItem SourcePort {get;set;}public StudioPortItem TargetPort {get;set;}
        public ObservableCollection<StudioConnectionItem> Connections {get;private set;}public StudioConnectionItem SelectedConnection {get;set;}
        public string RenameText {get;set;}
        public string[] BindingModes {get{return new[]{"常量","变量","资源"};}}
        public IMEMResource BindingResource {get;set;}
        public MEMPage ActivePage
        {get{return _editor==null||!_editor.CurrentPageId.HasValue?null:_editor.Document.Find(_editor.CurrentPageId.Value) as MEMPage;}set{if(value==null||_editor==null||IsRefreshing)return;_selected=null;_editor.SetPage(value.Id);NotifyScene();}}
        public IMEMResource SelectedResource
        {get{return !_resourceId.HasValue||_editor==null?null:_editor.Document.Project.FindResource(_resourceId.Value);}set{if(IsRefreshing)return;_resourceId=value==null?(Guid?)null:value.Id;_selected=null;_selection=new Guid[0];_editor.Select(null);Changed("SelectedResource");}}
        public string ContextName {get{return _editor.CurrentResourceId.HasValue?(_editor.Document.Find(_editor.CurrentResourceId.Value)?.Name??"已删除上下文"):"请选择页面或资源";}}
        public bool IsDirty {get{return _editor.Document.IsDirty;}}
        public ICommand AddPageCommand {get;private set;}public ICommand AddContentCommand {get;private set;}public ICommand AddSymbolCommand {get;private set;}
        public ICommand PlaceResourceCommand {get;private set;}public ICommand OpenDefinitionCommand {get;private set;}public ICommand BackCommand {get;private set;}
        public ICommand ApplyPropertyCommand {get;private set;}public ICommand RestorePropertyDefaultCommand {get;private set;}public ICommand DeleteCommand {get;private set;}public ICommand ImportCommand {get;private set;}public ICommand ExportCommand {get;private set;}
        public ICommand GroupCommand {get;private set;}public ICommand UngroupCommand {get;private set;}public ICommand ConnectPortsCommand {get;private set;}
        public ICommand AddInputPortCommand {get;private set;}public ICommand AddOutputPortCommand {get;private set;}
        public ICommand DisconnectCommand {get;private set;}public ICommand AddAssetCommand {get;private set;}public ICommand AddParameterCommand {get;private set;}public ICommand RenameCommand {get;private set;}
        private void InitializeEngineering()
        {
            Pages=new ObservableCollection<MEMPage>();Resources=new ObservableCollection<IMEMResource>();Properties=new ObservableCollection<StudioPropertyItem>();Ports=new ObservableCollection<StudioPortItem>();Connections=new ObservableCollection<StudioConnectionItem>();
            AddPageCommand=new RelayCommand(()=>RunEngineering(()=>{var p=ProjectSession.AddPage("页面 "+(Pages.Count+1));_editor.SetPage(p.Id);}));
            AddContentCommand=new RelayCommand(()=>RunEngineering(()=>{var content=ProjectSession.AddContent(ProjectSession.Project,"内容 "+(ProjectSession.Project.Contents.Count+1));_resourceId=content.Id;}));
            AddSymbolCommand=new RelayCommand(()=>RunEngineering(CreateSymbol));
            PlaceResourceCommand=new RelayCommand(()=>RunEngineering(PlaceSelectedResource));
            OpenDefinitionCommand=new RelayCommand(()=>RunEngineering(()=>{Guid? id=_selected??_resourceId;if(id.HasValue){_selected=null;_editor.OpenDefinition(id.Value);}}));
            BackCommand=new RelayCommand(()=>RunEngineering(()=>{_selected=null;_editor.ReturnToPrevious();}));
            DeleteCommand=new RelayCommand(()=>{if(_selected.HasValue)Execute(()=>_editor.Remove(_selected.Value));});
            ApplyPropertyCommand=new RelayCommand((object row)=>RunEngineering(()=>ApplyProperty((StudioPropertyItem)row)));
            RestorePropertyDefaultCommand=new RelayCommand((object row)=>RunEngineering(()=>RestorePropertyDefault((StudioPropertyItem)row)));
            ImportCommand=new RelayCommand(()=>RunEngineering(ImportPackage));ExportCommand=new RelayCommand(()=>RunEngineering(ExportSelected));
            GroupCommand=new RelayCommand(()=>RunEngineering(()=>{var items=_selection.Select(id=>ProjectSession.Find(id) as MRCVisualItem).ToArray();if(items.Any(i=>i==null))throw new InvalidOperationException("请选择同父级图元；资源实例可通过提取内容复用");var group=ProjectSession.Group(items,"分组");Select(group.Id);}));
            UngroupCommand=new RelayCommand(()=>RunEngineering(()=>{var node=_selected.HasValue?ProjectSession.Find(_selected.Value):null;var group=node as MRCGroup??node?.Parent as MRCGroup;if(group==null)throw new InvalidOperationException("请选择分组或组内图元");ProjectSession.Ungroup(group);Select(null);}));
            ConnectPortsCommand=new RelayCommand(()=>RunEngineering(()=>{if(SourcePort==null||TargetPort==null)throw new InvalidOperationException("请选择来源和目标端口");var root=CurrentOwner();ProjectSession.Connect(root,SourcePort.Endpoint,TargetPort.Endpoint);}));
            AddInputPortCommand=new RelayCommand(()=>RunEngineering(()=>AddPort(MPCPortDirection.Input)));AddOutputPortCommand=new RelayCommand(()=>RunEngineering(()=>AddPort(MPCPortDirection.Output)));
            DisconnectCommand=new RelayCommand(()=>RunEngineering(()=>{if(SelectedConnection==null)throw new InvalidOperationException("请选择一条连接");ProjectSession.Disconnect(ProjectSession.FindConnection(SelectedConnection.Id));}));
            AddAssetCommand=new RelayCommand(()=>RunEngineering(()=>{var dialog=new Microsoft.Win32.OpenFileDialog();if(dialog.ShowDialog()==true){var asset=ProjectSession.AddAsset(System.IO.Path.GetFileName(dialog.FileName),dialog.FileName);_resourceId=asset.Id;}}));
            AddParameterCommand=new RelayCommand(()=>RunEngineering(()=>{var owner=SelectedDefinition();var definition=owner as IMPMDefinition;if(definition==null)throw new InvalidOperationException("请选择内容、组件或图元定义");ProjectSession.DefineProperty(owner,new MPMPropertyDescriptor("参数 "+(definition.Properties.Count+1),MPMValueKind.Number,false,true,MPMBinding.Literal(MPMValue.Number(0))));}));
            RenameCommand=new RelayCommand(()=>RunEngineering(()=>{var reference=_selected.HasValue?ProjectSession.FindReference(_selected.Value):null;if(reference!=null)ProjectSession.Rename(reference,RenameText);else ProjectSession.Rename(SelectedDefinition(),RenameText);}));
        }
        private bool CanReplace(){if(_editor==null||!_editor.Document.IsDirty)return true;if(ConfirmReplace!=null&&ConfirmReplace(_editor.Document))return true;Status="当前工程尚未保存，已保留。";return false;}
        private void AttachSession(MEMEngineeringSession session)
        {
            if(_editor!=null){_editor.Changed-=EditorChanged;_editor.Dispose();_editor.Document.Close(true);}
            _editor=new CxVisualEditor(session);_editor.Changed+=EditorChanged;_selected=null;_resourceId=null;
        }
        public void OpenProject(string path,bool discardCurrent=false)
        {
            if(!discardCurrent&&!CanReplace())return;var project=MEMEngineering.Open(path);AttachSession(project);Status="已加载: "+path;NotifyScene();
        }
        public void SaveProject(string path){ProjectSession.SaveAs(path);Status="已保存: "+path;NotifyScene();}
        private void EditorChanged(object sender,EventArgs args)
        {
            if(_dispatcher.CheckAccess()){NotifyScene();return;}
            if(Interlocked.Exchange(ref _refreshQueued,1)!=0)return;
            _dispatcher.BeginInvoke(new Action(()=>{Interlocked.Exchange(ref _refreshQueued,0);if(_editor!=null&&!_editor.Document.IsClosed)NotifyScene();}));
        }
        private void RefreshEngineering()
        {
            if(_editor==null||_refreshing)return;_refreshing=true;
            try
            {
                Pages.Clear();foreach(var page in ProjectSession.Project.Pages)Pages.Add(page);Resources.Clear();foreach(var resource in ProjectSession.Project.Resources)Resources.Add(resource);
                Properties.Clear();
                PropertySelectionHint="";
                if(_selection.Length>1)
                {
                    // 本阶段仅对同一资源定义的多个实例批量编辑。其他混选只读提示，绝不降级为编辑最后一个对象。
                    MRIResourceReference[] references=_selection.Select(id=>ProjectSession.FindReference(id)).ToArray();
                    if(references.All(x=>x!=null)&&references.Select(x=>x.TargetId).Distinct().Count()==1)
                    {
                        var target=ProjectSession.Find(references[0].TargetId) as IMPMDefinition;
                        if(target!=null)
                            foreach(var property in target.Properties.Where(p=>p.Descriptor.IsPublic))
                            {
                                MPMBinding[] values=references.Select(x=>ProjectSession.ResolveParameter(x,property.MemberId)).ToArray();
                                bool mixed=values.Skip(1).Any(v=>!object.Equals(v,values[0]));
                                var row=Row(references[references.Length-1].Id,property.MemberId,
                                    property.Descriptor.ValueKind,values[0],true,false,property.Descriptor);
                                row.BatchOwnerIds=(Guid[])_selection.Clone();
                                row.SelectionCount=references.Length;
                                row.DefaultDescription="共同继承定义："+BindingSummary(property.Binding);
                                row.CanRestoreDefault=references.Any(x=>x.Overrides.ContainsKey(property.MemberId));
                                row.ResourceChoices=Resources.ToList().AsReadOnly();
                                if(!mixed&&values[0]!=null&&values[0].Kind==MPMBindingKind.Resource)
                                    row.SelectedBindingResource=row.ResourceChoices.FirstOrDefault(x=>x.Id==values[0].ResourceId.Value);
                                if(mixed){row.Text="";row.IsMixedValue=true;row.Source="混合值（"+references.Length+"个实例）";}
                                Properties.Add(row);
                            }
                    }
                    else PropertySelectionHint="当前选区只读：批量属性编辑仅支持同一Content/Symbol定义的多个实例；不同定义或图元混选不能误改。";
                }
                else if(_selected.HasValue)
                {
                    var reference=ProjectSession.FindReference(_selected.Value);var node=reference==null?ProjectSession.Find(_selected.Value):ProjectSession.Find(reference.TargetId);var definition=node as IMPMDefinition;
                    if(definition!=null)foreach(var property in definition.Properties.Where(p=>reference==null||p.Descriptor.IsPublic))
                    {
                        var binding=reference==null?property.Binding:ProjectSession.ResolveParameter(reference,property.MemberId);
                        var item=Row(_selected.Value,property.MemberId,property.Descriptor.ValueKind,binding,reference!=null,false,property.Descriptor);
                        var inherited=reference==null?property.Descriptor.DefaultBinding:property.Binding;
                        item.DefaultDescription=(reference==null?"Schema默认：":"继承定义：")+BindingSummary(inherited);
                        item.CanRestoreDefault=reference==null?property.HasOverride:reference.Overrides.ContainsKey(property.MemberId);
                        item.ResourceChoices=Resources.ToList().AsReadOnly();
                        if(binding!=null&&binding.Kind==MPMBindingKind.Resource)
                            item.SelectedBindingResource=item.ResourceChoices.FirstOrDefault(x=>x.Id==binding.ResourceId.Value);
                        Properties.Add(item);
                    }
                    var visual=reference==null?node as MRCVisualItem:null;
                    if(visual!=null){foreach(var pair in new[]{Tuple.Create("x",visual.X),Tuple.Create("y",visual.Y),Tuple.Create("width",visual.Width),Tuple.Create("height",visual.Height),Tuple.Create("rotation",visual.Rotation)})Properties.Add(Row(visual.Id,pair.Item1,MPMValueKind.Number,MPMBinding.Literal(MPMValue.Number(pair.Item2)),false,true));}
                }
                Ports.Clear();Connections.Clear();if(_editor.CurrentResourceId.HasValue)
                {
                    var root=CurrentOwner();foreach(var node in root.DescendantsAndSelf())
                    {
                        var declared=node as IMPMDefinition;if(declared!=null)foreach(var port in declared.Ports)Ports.Add(new StudioPortItem{Name=node.Name+" / "+port.MemberId+" ("+port.Direction+")",Endpoint=ProjectSession.Endpoint(port)});
                        var refs=node is MEMContent?((MEMContent)node).References:node is MEMSymbol?((MEMSymbol)node).References:null;
                        if(refs!=null)foreach(var reference in refs){var target=ProjectSession.Find(reference.TargetId) as IMPMDefinition;foreach(var port in target.Ports.Where(p=>p.IsPublic))Ports.Add(new StudioPortItem{Name=reference.Name+" / "+port.MemberId+" ("+port.Direction+")",Endpoint=ProjectSession.Endpoint(reference,port.MemberId)});}
                        if(declared!=null)foreach(var connection in declared.Connections)Connections.Add(new StudioConnectionItem{Id=connection.Id,Name=EndpointLabel(connection.Source)+" → "+EndpointLabel(connection.Target)});
                    }
                }
                SourcePort=null;TargetPort=null;
                var selectedReference=_selected.HasValue?ProjectSession.FindReference(_selected.Value):null;var selectedNode=SelectedDefinition();RenameText=selectedReference==null?selectedNode?.Name:selectedReference.Name;
                Changed("Pages");Changed("Resources");Changed("ActivePage");Changed("SelectedResource");Changed("ContextName");Changed("Properties");Changed("IsDirty");Changed("Ports");Changed("Connections");Changed("RenameText");Changed("PropertySelectionHint");
            }
            finally{_refreshing=false;}
        }
        private string BindingSummary(MPMBinding binding)
        {
            if(binding==null)return "(无默认)";
            if(binding.Kind==MPMBindingKind.Literal)return binding.Value.Kind==MPMValueKind.Null?"(空)":binding.Value.Canonical;
            if(binding.Kind==MPMBindingKind.Resource)
                return "资源："+(ProjectSession.Project.FindResource(binding.ResourceId.Value,true)?.Name??"资源缺失");
            if(binding.Kind==MPMBindingKind.Variable)return "变量："+binding.SourceId;
            return "来源："+binding.Kind;
        }
        private StudioPropertyItem Row(Guid owner,string member,MPMValueKind kind,MPMBinding binding,bool reference,bool visual,MPMPropertyDescriptor descriptor=null)
        {
            string schemaLabel="类型："+kind.ToString();
            if(descriptor!=null)
            {
                schemaLabel+=descriptor.Required?" · 必填":" · 可选";
                if(descriptor.Minimum.HasValue||descriptor.Maximum.HasValue)
                    schemaLabel+=" · 范围："+(descriptor.Minimum.HasValue?descriptor.Minimum.Value.ToString(CultureInfo.InvariantCulture):"无下限")
                        +" ～ "+(descriptor.Maximum.HasValue?descriptor.Maximum.Value.ToString(CultureInfo.InvariantCulture):"无上限");
                schemaLabel+=reference?" · 实例覆盖":" · 定义属性";
            }
            else if(visual) schemaLabel+=" · 几何属性";
            return new StudioPropertyItem{OwnerId=owner,Revision=ProjectSession.Revision,Member=member,Kind=kind,Reference=reference,Visual=visual,Schema=schemaLabel,Descriptor=descriptor,Text=binding==null?"":binding.Kind==MPMBindingKind.Literal?binding.Value.Canonical:binding.Kind==MPMBindingKind.Resource?(ProjectSession.Project.FindResource(binding.ResourceId.Value,true)?.Name??"资源缺失"):binding.SourceId,Mode=binding==null?(kind==MPMValueKind.Resource?"资源":"常量"):binding.Kind==MPMBindingKind.Literal?"常量":binding.Kind==MPMBindingKind.Resource?"资源":binding.Kind==MPMBindingKind.Variable?"变量":"保留来源",Source=binding==null?"未绑定":binding.Kind==MPMBindingKind.Configuration?"配置来源":binding.Kind==MPMBindingKind.Member?"对象成员来源":binding.Kind==MPMBindingKind.Expression?"表达式来源":"当前来源："+(binding.Kind==MPMBindingKind.Literal?"常量":binding.Kind==MPMBindingKind.Resource?"资源":"变量")};}
        public void SetSelection(System.Collections.Generic.IEnumerable<Guid> ids)
        {
            if(IsRefreshing)return;
            Guid[] selected=ids==null?new Guid[0]:ids.Distinct().ToArray();
            Select(selected.Length==0?(Guid?)null:selected[selected.Length-1]);
            _selection=selected;
            RefreshEngineering();
        }
        private MEMNode CurrentOwner(){if(!_editor.CurrentResourceId.HasValue)throw new InvalidOperationException("请选择页面或资源");var root=ProjectSession.Find(_editor.CurrentResourceId.Value);return root is MEMPage?((MEMPage)root).RootContent:root;}
        private MEMNode SelectedDefinition(){return _selected.HasValue?ProjectSession.Find(_selected.Value)??SelectedResource as MEMNode:SelectedResource as MEMNode;}
        private string EndpointLabel(MPCEndpoint endpoint){var port=ProjectSession.FindPort(endpoint.PortId);var name=endpoint.Address.InstancePath.Count==0?ProjectSession.Find(endpoint.Address.ObjectId).Name:ProjectSession.FindReference(endpoint.Address.InstancePath[0]).Name;return name+" / "+port.MemberId;}
        private void AddPort(MPCPortDirection direction)
        {var node=_selected.HasValue?ProjectSession.Find(_selected.Value):SelectedResource as MEMNode;if(!(node is IMPMDefinition))throw new InvalidOperationException("请选择内容、组件或图元定义");var definition=(IMPMDefinition)node;ProjectSession.DefinePort(node,(direction==MPCPortDirection.Input?"输入 ":"输出 ")+(definition.Ports.Count+1),direction,"core.number",isPublic:node.Parent is MEMProject);}
        private MRIResourceReference[] BatchReferences(StudioPropertyItem row)
        {
            if(row==null||row.BatchOwnerIds==null||row.BatchOwnerIds.Length<2||
                !row.Reference||row.IsReadOnly||!_selection.SequenceEqual(row.BatchOwnerIds))
                throw new InvalidOperationException("多选上下文已改变，禁止执行过期属性修改");
            MRIResourceReference[] refs=row.BatchOwnerIds.Select(id=>ProjectSession.FindReference(id)).ToArray();
            if(refs.Any(x=>x==null)||refs.Select(x=>x.TargetId).Distinct().Count()!=1)
                throw new InvalidOperationException("批量修改只支持同一定义的实例");
            var target=ProjectSession.Find(refs[0].TargetId) as IMPMDefinition;
            if(target==null||!target.Properties.Any(p=>p.MemberId==row.Member&&
                p.Descriptor.IsPublic&&p.Descriptor.ValueKind==row.Kind))
                throw new InvalidOperationException("批量属性非公开或Schema已改变");
            return refs;
        }
        private void RestorePropertyDefault(StudioPropertyItem row)
        {
            if(row==null||row.Visual||row.Descriptor==null||row.IsReadOnly)
                throw new InvalidOperationException("请选择可编辑的工程定义属性或公开实例参数");
            if(row.BatchOwnerIds!=null&&row.BatchOwnerIds.Length>1)
            {
                MRIResourceReference[] refs=BatchReferences(row);
                ProjectSession.Edit("批量清除实例属性覆盖",row.Revision,e=>
                {
                    foreach(MRIResourceReference reference in refs)e.SetOverride(reference,row.Member,null);
                    return true;
                }).RequireValue();
                return;
            }
            ProjectSession.Edit("恢复工程属性默认值",row.Revision,e=>
            {
                if(row.Reference)e.SetOverride(ProjectSession.FindReference(row.OwnerId),row.Member,null);
                else e.SetProperty(ProjectSession.Find(row.OwnerId),row.Member,null);
                return true;
            }).RequireValue();
        }
        private void ApplyProperty(StudioPropertyItem row)
        {
            if(row==null)return;
            if(row.IsReadOnly)throw new InvalidOperationException("当前属性只读");
            // 早期提示拒绝非法数字，最终范围/类型仍由原MEM编辑事务验证；失败绝不写工程历史。
            if(!string.IsNullOrEmpty(row.ValidationMessage))throw new InvalidOperationException(row.ValidationMessage);
            MPMBinding binding;
            if(row.Mode=="资源")
            {
                if(row.Kind!=MPMValueKind.Resource)throw new InvalidOperationException("当前属性不是Resource类型");
                IMEMResource resource=row.SelectedBindingResource;
                if(resource==null||ProjectSession.Project.FindResource(resource.Id,true)==null)
                    throw new InvalidOperationException("资源不存在于当前工程或已失效");
                binding=MPMBinding.Resource(resource.Id);
            }
            else if(row.Mode=="变量")binding=MPMBinding.Variable(row.Text);
            else if(row.Mode=="常量")
            {MPMValue value;switch(row.Kind){case MPMValueKind.Number:value=MPMValue.Number(double.Parse(row.Text,CultureInfo.InvariantCulture));break;case MPMValueKind.Integer:value=MPMValue.Integer(long.Parse(row.Text,CultureInfo.InvariantCulture));break;case MPMValueKind.Decimal:value=MPMValue.Decimal(decimal.Parse(row.Text,CultureInfo.InvariantCulture));break;case MPMValueKind.Boolean:value=MPMValue.Boolean(bool.Parse(row.Text));break;case MPMValueKind.String:value=MPMValue.String(row.Text);break;default:throw new InvalidOperationException("请选择资源来源");}binding=MPMBinding.Literal(value);}
            else throw new InvalidOperationException("原来源保持不变；请选择一种明确的新来源再应用");
            if(row.BatchOwnerIds!=null&&row.BatchOwnerIds.Length>1)
            {
                MRIResourceReference[] references=BatchReferences(row);
                ProjectSession.Edit("批量编辑实例属性",row.Revision,e=>
                {
                    foreach(MRIResourceReference reference in references)
                        e.SetOverride(reference,row.Member,binding);
                    return true;
                }).RequireValue();
                return;
            }
            ProjectSession.Edit("编辑对象属性",row.Revision,e=>
            {
                if(row.Reference)e.SetOverride(ProjectSession.FindReference(row.OwnerId),row.Member,binding);
                else if(row.Visual)
                {
                    var item=(MRCVisualItem)ProjectSession.Find(row.OwnerId);if(binding.Kind!=MPMBindingKind.Literal)e.SetBinding(item,row.Member,binding);
                    else{double value=binding.Value.AsNumber();e.SetBinding(item,row.Member,null);switch(row.Member){case "x":e.Move(item,value,item.Y);break;case "y":e.Move(item,item.X,value);break;case "width":e.Resize(item,value,item.Height);break;case "height":e.Resize(item,item.Width,value);break;case "rotation":e.Rotate(item,value);break;}}
                }
                else e.SetProperty(ProjectSession.Find(row.OwnerId),row.Member,binding);return true;
            }).RequireValue();
        }
        private void CreateSymbol()
        {
            var symbol=ProjectSession.Edit("新建可复用组件",e=>{var value=e.AddSymbol("组件 "+(ProjectSession.Project.Symbols.Count+1));var element=e.AddRectangle(value.RootGroup,"矩形",0,0);e.DefineProperty(value,new MPMPropertyDescriptor("宽度",MPMValueKind.Number,false,true,MPMBinding.Literal(MPMValue.Number(120)),1,10000));e.ExposeProperty(value,"宽度",ProjectSession.GetAddress(element),"width");return value;}).RequireValue();_resourceId=symbol.Id;
        }
        private void PlaceSelectedResource()
        {
            if(SelectedResource==null||!_editor.CurrentResourceId.HasValue)throw new InvalidOperationException("请选择内容或符号以及目标页面");var root=ProjectSession.Find(_editor.CurrentResourceId.Value);var owner=root is MEMPage?((MEMPage)root).RootContent:root;
            var resource=SelectedResource as MEMNode;if(!(resource is MEMSymbol||resource is MEMContent))throw new InvalidOperationException("页面/静态资产不作为组件实例放置");
            var reference=ProjectSession.Edit("放置组件实例",e=>{var use=e.Reference(owner,resource,resource.Name);e.SetPlacement(use,new MRCTransform(1,0,0,1,60,60));return use;}).RequireValue();Select(reference.Id);
        }
        private void ImportPackage()
        {
            var dialog=new Microsoft.Win32.OpenFileDialog{Filter="CxVisual 资源包/工程|*.cxproj"};if(dialog.ShowDialog()!=true)return;var source=MEMEngineering.Open(dialog.FileName);
            try{var plan=ProjectSession.PreviewImport(source,null,ProjectSession.Project);if(!plan.IsValid)throw new InvalidOperationException(string.Join(";",plan.Issues));if(System.Windows.MessageBox.Show("将导入 "+plan.IdMap.Count+" 个身份，依赖随包带入。\n"+string.Join("\n",plan.Warnings),"导入预览",System.Windows.MessageBoxButton.OKCancel)==System.Windows.MessageBoxResult.OK)ProjectSession.Import(plan);}finally{source.Close(true);}
        }
        private void ExportSelected()
        {if(!_resourceId.HasValue)throw new InvalidOperationException("请选择库资源");var dialog=new Microsoft.Win32.SaveFileDialog{Filter="CxVisual 资源包|*.cxproj"};if(dialog.ShowDialog()==true)ProjectSession.ExportPackage(new[]{_resourceId.Value},dialog.FileName);}
        private void RunEngineering(Action action){try{action();NotifyScene();Status="操作完成 | Revision "+ProjectSession.Revision;}catch(Exception error){Status="操作失败，工程已保留: "+error.Message;}}
    }
}
