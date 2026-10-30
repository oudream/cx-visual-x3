using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Runtime.Commands;

namespace CxVisual.Mechanism.Presentation.Wpf.Interaction
{
    /// <summary>
    /// MRK命令的共用目录、Schema编辑与Operation观察控件。
    /// 调用/取消采用显式Host授权开关，底层始终由真正MRK Owner校验。
    /// </summary>
    public sealed class MPWCommandCenterControl : UserControl, IDisposable
    {
        private sealed class OperationRow
        {
            public string Time {get;set;}
            public string Id {get;set;}
            public string Command {get;set;}
            public string Stage {get;set;}
            public string State {get;set;}
            public string Detail {get;set;}
        }
        private readonly ListBox _catalog=new ListBox {MinWidth=200};
        private readonly Grid _parameters=new Grid {Margin=new Thickness(5)};
        private readonly Dictionary<string,TextBox> _inputs=new Dictionary<string,TextBox>(StringComparer.Ordinal);
        private readonly ObservableCollection<OperationRow> _history=new ObservableCollection<OperationRow>();
        private readonly TextBlock _contract=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5)};
        private readonly TextBlock _status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5)};
        private readonly TextBox _operation=new TextBox {MinWidth=260,Margin=new Thickness(3)};
        private readonly Button _execute=new Button {Content="按Schema执行命令",Margin=new Thickness(3),Padding=new Thickness(9,5,9,5)};
        private readonly Button _cancel=new Button {Content="协作取消（非急停）",Margin=new Thickness(3),Padding=new Thickness(9,5,9,5)};
        private MRKCommandCenter _owner;
        private MRKCommandDescriptor _selected;
        private bool _allowed;
        private bool _disposed;
        private bool _busy;
        private int _generation;
        public bool Connected { get {return _owner!=null;} }
        public int VisibleCommandCount {get{return _catalog.Items.Count;}}
        public int OperationCount {get{return _history.Count;}}
        public string StatusSummary {get{return _status.Text;}}
        public MPWCommandCenterControl()
        {
            AutomationProperties.SetAutomationId(this,"MPW.CommandCenter");
            AutomationProperties.SetAutomationId(_catalog,"MPW.CommandCenter.Catalog");
            AutomationProperties.SetAutomationId(_execute,"MPW.CommandCenter.Execute");
            AutomationProperties.SetAutomationId(_operation,"MPW.CommandCenter.OperationId");
            DockPanel root=new DockPanel {Margin=new Thickness(8)};
            StackPanel top=new StackPanel();
            top.Children.Add(new TextBlock {Text="命令中心 / MRK",FontSize=18,FontWeight=FontWeights.SemiBold});
            top.Children.Add(new TextBlock {Text="真实MRK命令目录、FDC参数Schema与Operation追踪；Caller文本不替代Host授权，取消不等于硬件急停",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,6)});
            WrapPanel actions=new WrapPanel();
            actions.Children.Add(Button("刷新目录",RefreshCatalog));
            _execute.Click+=async(s,e)=>{try{await InvokeSelectedAsync();}catch(Exception ex){_status.Text="执行失败："+ex.Message;}};
            actions.Children.Add(_execute);
            actions.Children.Add(new TextBlock {Text="OperationId",VerticalAlignment=VerticalAlignment.Center});
            actions.Children.Add(_operation);
            actions.Children.Add(Button("查询",()=>QueryOperation()));
            _cancel.Click+=(s,e)=>{try{CancelOperation();}catch(Exception ex){_status.Text="取消失败："+ex.Message;}};
            actions.Children.Add(_cancel);
            top.Children.Add(actions);
            top.Children.Add(_status);
            DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
            DataGrid history=new DataGrid {ItemsSource=_history,IsReadOnly=true,
                AutoGenerateColumns=true,CanUserAddRows=false,Height=140,Margin=new Thickness(2)};
            AutomationProperties.SetAutomationId(history,"MPW.CommandCenter.History");
            DockPanel.SetDock(history,Dock.Bottom);root.Children.Add(history);
            Grid body=new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(230)});
            body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            body.Children.Add(_catalog);
            _catalog.DisplayMemberPath="Definition.Name";
            _catalog.SelectionChanged+=(s,e)=>SelectDescriptor(_catalog.SelectedItem as MRKCommandDescriptor);
            StackPanel editor=new StackPanel();
            editor.Children.Add(_contract);
            editor.Children.Add(new TextBlock {Text="输入值请用JSON表示；string需加双引号，数组/对象遵从已注册FDC Schema。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5)});
            editor.Children.Add(_parameters);
            ScrollViewer scroll=new ScrollViewer {Content=editor,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            Grid.SetColumn(scroll,1);body.Children.Add(scroll);
            root.Children.Add(body);
            Content=root;
            Bind(null);
        }
        private static Button Button(string label,Action act)
        {
            Button button=new Button {Content=label,Margin=new Thickness(3),Padding=new Thickness(9,5,9,5)};
            button.Click+=(s,e)=>{try{act();}catch(Exception ex){MessageBox.Show(ex.Message,label);}};
            return button;
        }
        /// <summary>传入的MRK中心归Host所有；本控件不负责创建、Dispose或注册Handler。</summary>
        public void Bind(MRKCommandCenter owner,bool allowInvoke=false)
        {
            if(_disposed)throw new ObjectDisposedException("MPWCommandCenterControl");
            _owner=owner;_allowed=owner!=null&&allowInvoke;_generation++;
            _operation.Text="";_history.Clear();_selected=null;
            RefreshCatalog();
            _status.Text=owner==null?"没有连接MRK Host，命令不可执行":
                "已连接MRK Host；"+(_allowed?"可提交命令，仍需Host侧授权":"只读监视；禁止提交/取消");
        }
        public void RefreshCatalog()
        {
            if(_disposed)return;
            _catalog.Items.Clear();_inputs.Clear();_parameters.Children.Clear();
            _parameters.RowDefinitions.Clear();_selected=null;_contract.Text="";
            if(_owner!=null)
                foreach(MRKCommandDescriptor item in _owner.Router.Discover())
                    _catalog.Items.Add(item);
            _execute.IsEnabled=_allowed&&!_busy&&_catalog.Items.Count>0;
            _cancel.IsEnabled=_allowed&&!_busy;
            if(_catalog.Items.Count>0)_catalog.SelectedIndex=0;
        }
        /// <summary>按正式Target/Code定位项目命令，供Studio绑定与自动化使用。</summary>
        public bool SelectCommand(string target,string code)
        {
            if(_disposed)throw new ObjectDisposedException("MPWCommandCenterControl");
            foreach(MRKCommandDescriptor descriptor in _catalog.Items.OfType<MRKCommandDescriptor>())
            {
                if(descriptor.Target==target&&descriptor.Definition.CommandCode==code)
                {_catalog.SelectedItem=descriptor;return true;}
            }
            return false;
        }
        /// <summary>按正式Schema成员设置JSON输入，不绕过Invoke时的FDC校验。</summary>
        public void SetInputJson(string member,string json)
        {
            if(_disposed)throw new ObjectDisposedException("MPWCommandCenterControl");
            TextBox field;
            if(!_inputs.TryGetValue(member,out field))
                throw new ArgumentException("未选择命令或Schema中没有该参数", "member");
            field.Text=json??"";
        }
        private void SelectDescriptor(MRKCommandDescriptor descriptor)
        {
            _selected=descriptor;
            _inputs.Clear();_parameters.Children.Clear();_parameters.RowDefinitions.Clear();
            _parameters.ColumnDefinitions.Clear();
            if(descriptor==null){_contract.Text="未选中正式命令定义";return;}
            FDCCommandDefinition contract=descriptor.Definition;
            _contract.Text=descriptor.Target+" / "+contract.CommandCode+
                " / ID="+contract.CommandId+" / SchemaV"+contract.SchemaVersion+
                " / "+descriptor.Kind+" / "+contract.Description;
            _parameters.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(170)});
            _parameters.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            int row=0;
            foreach(FDCCommandParameterDefinition parameter in contract.Inputs)
            {
                _parameters.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
                TextBlock label=new TextBlock {Text=parameter.Code+" : "+parameter.DataType.Name+
                    (parameter.Required?" *":""),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5)};
                Grid.SetRow(label,row);_parameters.Children.Add(label);
                TextBox field=new TextBox {Margin=new Thickness(5),MinWidth=135,
                    Text=parameter.HasDefaultValue?new JavaScriptSerializer().Serialize(parameter.DefaultValue):""};
                AutomationProperties.SetAutomationId(field,"MPW.CommandCenter.Input."+parameter.Code);
                Grid.SetColumn(field,1);Grid.SetRow(field,row);_parameters.Children.Add(field);
                _inputs.Add(parameter.Code,field);row++;
            }
        }
        private static object ConvertValue(object value,Type requested)
        {
            if(value==null)return null;
            Type type=Nullable.GetUnderlyingType(requested)??requested;
            if(type==typeof(object))return value;
            if(type==typeof(Dictionary<string,object>)||
                type==typeof(IDictionary<string,object>)||
                type==typeof(IReadOnlyDictionary<string,object>))
                return value as Dictionary<string,object> ??
                    throw new ArgumentException("对象参数需要JSON Map");
            if(type.IsArray)
            {
                object[] items=value as object[];
                if(items==null)throw new ArgumentException("数组参数需要JSON数组");
                Type element=type.GetElementType();
                Array output=Array.CreateInstance(element,items.Length);
                for(int i=0;i<items.Length;i++)output.SetValue(ConvertValue(items[i],element),i);
                return output;
            }
            if(type==typeof(Guid))return Guid.Parse(Convert.ToString(value,CultureInfo.InvariantCulture));
            if(type.IsEnum)return Enum.Parse(type,Convert.ToString(value,CultureInfo.InvariantCulture),true);
            return Convert.ChangeType(value,type,CultureInfo.InvariantCulture);
        }
        public async Task<MRKCommandResponse> InvokeSelectedAsync()
        {
            if(_disposed||_owner==null||!_allowed)
                throw new UnauthorizedAccessException("当前Studio/TestCenter没有命令执行授权");
            if(_busy||_selected==null)throw new InvalidOperationException("请选择可用命令且完成当前操作");
            MRKCommandCenter owner=_owner;
            MRKCommandDescriptor selected=_selected;
            int gen=_generation;
            Dictionary<string,object> inputs=new Dictionary<string,object>(StringComparer.Ordinal);
            JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=65536,RecursionLimit=16};
            foreach(FDCCommandParameterDefinition p in selected.Definition.Inputs)
            {
                string raw=_inputs[p.Code].Text;
                if(String.IsNullOrWhiteSpace(raw)&&!p.Required)continue;
                if(String.IsNullOrWhiteSpace(raw))throw new ArgumentException("缺少必填参数："+p.Code);
                inputs.Add(p.Code,ConvertValue(json.DeserializeObject(raw),p.DataType));
            }
            selected.Definition.BindInputs(inputs);
            MRKCommandRequest request=new MRKCommandRequest(selected.Target,
                selected.Definition.CommandCode,inputs,"MPW.CommandCenter",
                expectedSchemaVersion:selected.Definition.SchemaVersion,
                commandId:selected.Definition.CommandId);
            _busy=true;_execute.IsEnabled=false;_cancel.IsEnabled=false;
            try
            {
                MRKCommandResponse response=await owner.InvokeAsync(request);
                if(!_disposed&&gen==_generation&&ReferenceEquals(_owner,owner))
                {
                    _operation.Text=response.OperationId.ToString("D");
                    Append(response,selected.Definition.CommandCode);
                    _status.Text="Stage="+response.Stage+" / State="+response.State+
                        " / Business="+response.BusinessSuccess+" / "+response.ErrorCode+
                        "；Unknown绝不可直接重试副作用命令";
                }
                return response;
            }
            finally
            {
                _busy=false;
                if(!_disposed){_execute.IsEnabled=_allowed;_cancel.IsEnabled=_allowed;}
            }
        }
        private void Append(MRKCommandResponse response,string code)
        {
            _history.Add(new OperationRow {Time=DateTime.Now.ToString("HH:mm:ss.fff"),
                Id=response.OperationId.ToString("D"),Command=code,Stage=response.Stage.ToString(),
                State=response.State.ToString(),Detail=response.ErrorCode+" / "+response.ProgressMessage});
            while(_history.Count>250)_history.RemoveAt(0);
        }
        public MRKCommandResponse QueryOperation()
        {
            if(_owner==null)throw new InvalidOperationException("没有MRK Host");
            Guid id=Guid.Parse(_operation.Text);
            MRKCommandResponse response=_owner.Query(id);
            if(response==null){_status.Text="本Host中没有这个Operation；不推断远程状态";return null;}
            Append(response,_selected==null?"查询":_selected.Definition.CommandCode);
            _status.Text="查询："+response.Stage+" / "+response.State+
                " / Progress="+response.ProgressPercent+" / "+response.ErrorCode;
            return response;
        }
        public bool CancelOperation()
        {
            if(_owner==null||!_allowed)throw new UnauthorizedAccessException("没有协作取消权限");
            Guid id=Guid.Parse(_operation.Text);
            bool accepted=_owner.Cancel(id);
            _status.Text=accepted?"已发协作取消请求，需继续查询确认终态；这不是硬件急停":
                "取消未被受理；请查询Operation状态";
            return accepted;
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;_owner=null;_generation++;
            _catalog.Items.Clear();_history.Clear();_inputs.Clear();
        }
    }
}
