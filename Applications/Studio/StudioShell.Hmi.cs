using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AvalonDock.Layout;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.HMI;

namespace CxVisual.Studio
{
    /// <summary>HMI Viewer是已有Studio中央文档，MEM/MDS/MPW分别保有工程、数据和绘制Owner。</summary>
    public partial class MainWindow
    {
        private const string HmiViewerId = "hmi.scada.live";

        private void RegisterStudioHmiWorkbench()
        {
            _viewModel.RuntimeVariableChoices = () =>
            {
                if(_studioVariableScope!=null)
                    return _studioVariableScope.Definitions()
                        .Select(x=>x.Name).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                try
                {
                    return _studioRemoteHmi==null?new string[0]:
                        _studioRemoteHmi.Points().Select(x=>x.StablePointId)
                            .OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                }
                catch(InvalidOperationException){return new string[0];}
            };
            RegisterStudioResourceViewer(HmiViewerId, "HMI实时运行观察",
                CreateStudioHmiRuntimeView, false, "core.page");
            // Window关闭时Dock由原Studio释放，HMI只停止后台Pipe会话，
            // 避免在WPF Closed回调中再次操作已经拆卸的布局容器。
            Closed+=(sender,args)=>
            {
                PHMScadaRemoteReadSession running=_studioRemoteHmi;
                _studioRemoteHmi=null;
                if(running!=null)running.Dispose();
            };
        }

        /// <summary>在现有Page下打开真正的MVI/SceneView运行投影；未连接时明确提示。</summary>
        public string OpenStudioHmiPage(Guid pageId)
        {
            return OpenStudioResourceEditor(pageId, HmiViewerId);
        }

        private FrameworkElement CreateStudioHmiRuntimeView(
            StudioResourceDocumentIdentity identity)
        {
            MEMEngineeringSession engineering = _viewModel.ProjectSession;
            if (engineering.Project.Id != identity.ProjectId ||
                !(engineering.Find(identity.ResourceId) is MEMPage))
                throw new InvalidOperationException("HMI页面工程身份已经失效");
            if (_studioRemoteHmi != null)
                return CreateStudioRemoteHmiView(engineering,identity.ResourceId);
            MDSVariableTable table = _studioVariableScope;
            if (table == null)
                return new TextBlock {
                    Text = "HMI运行数据未连接。请先附加受信Host的现有MDS Scope；" +
                        "Studio不会生成演示数据或自动连接PLC。",
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18)
                };
            // 尚无A提供的跨进程认证命令服务：只读，不提供在线控制回调。
            PHMHmiRuntimeSession runtime = new PHMHmiRuntimeSession(
                identity.ProjectId, engineering.Revision, table,
                PHMHmiMode.DesignPreview, TimeSpan.FromSeconds(5));
            try
            {
                return new MPWLiveSceneControl(
                    engineering, identity.ResourceId, runtime,
                    () => runtime.Points().Select(point =>
                    {
                        PHMHmiPointObservation value = runtime.Read(point.Name);
                        return new MPWLivePoint {
                            Name = value.PointId,
                            Value = value.Text + (value.Unit.Length > 0 ? " " + value.Unit : ""),
                            Unit = value.Unit, Quality = value.Quality.ToString(),
                            Source = value.Source,
                            Updated = value.RefreshUtc == default(DateTime) ? "—" :
                                value.RefreshUtc.ToString("O", CultureInfo.InvariantCulture)
                        };
                    }).ToArray(),
                    refresh => runtime.Subscribe(changes => refresh(),
                        runtime.Points().Select(p => p.Name)),
                    runtime);
            }
            catch { runtime.Dispose(); throw; }
        }

        /// <summary>切换MDS Scope/Host代次后关闭旧View，避免旧数据污染新设备。</summary>
        private void CloseStudioHmiRuntimeDocuments()
        {
            string[] keys = _studioViewerDocuments.Where(x =>
                x.Value.Identity.EditorId == HmiViewerId)
                .Select(x => x.Key).ToArray();
            foreach (string key in keys)
            {
                LayoutDocument document = FindStudioContent(key) as LayoutDocument;
                if (document != null) { document.Content = null; document.Close(); }
                ReleaseStudioViewerDocument(key);
            }
        }
        /// <summary>原资源树Page右键菜单的专业贡献；不注册第二棵HMI工程树。</summary>
        private void AppendStudioHmiAuthoringMenu(ContextMenu menu,
            MEMEngineeringSession origin, Guid pageId)
        {
            MEMPage page=origin.Find(pageId) as MEMPage;
            if(page==null)return;
            MenuItem hmi=new MenuItem { Header="HMI：添加工业组件" };
            MDSVariableTable scope=_studioVariableScope;
            if(scope==null)
            {
                if(_studioRemoteHmi!=null)
                    AppendStudioHmiRemotePointItems(hmi,origin,pageId);
                else hmi.Items.Add(new MenuItem {
                    Header="未连接MDS/Runtime.Host：请选择受信运行来源",IsEnabled=false});
            }
            else
            {
                MDSVariableDefinition[] points=scope.Definitions()
                    .OrderBy(x=>x.Name,StringComparer.Ordinal).Take(100).ToArray();
                foreach(MDSVariableDefinition point in points)
                {
                    string pointName=point.Name;
                    bool boolean=point.ValueType==typeof(bool);
                    bool numeric=point.ValueType==typeof(short)||point.ValueType==typeof(ushort)||
                        point.ValueType==typeof(byte)||point.ValueType==typeof(sbyte)||
                        point.ValueType==typeof(int)||point.ValueType==typeof(uint)||
                        point.ValueType==typeof(long)||point.ValueType==typeof(ulong)||
                        point.ValueType==typeof(float)||point.ValueType==typeof(double)||
                        point.ValueType==typeof(decimal);
                    if(!boolean&&!numeric)continue;
                    MenuItem binding=new MenuItem {
                        Header=pointName+" · "+point.ValueType.Name+
                            (string.IsNullOrEmpty(point.Unit)?"":" · "+point.Unit) };
                    if(numeric)
                    {
                        binding.Items.Add(CreateStudioHmiInsertItem(
                            "添加数值显示",origin,pageId,scope,pointName,
                            (x,y)=>PHMHmiScreenAuthoring.AddNumeric(origin,pageId,pointName,
                                pointName,x,y)));
                        binding.Items.Add(CreateStudioHmiInsertItem(
                            "添加进度条（0～100）",origin,pageId,scope,pointName,
                            (x,y)=>PHMHmiScreenAuthoring.AddProgress(origin,pageId,pointName,
                                pointName,x,y,0,100)));
                    }
                    if(boolean)
                    {
                        binding.Items.Add(CreateStudioHmiInsertItem(
                            "添加状态灯",origin,pageId,scope,pointName,
                            (x,y)=>PHMHmiScreenAuthoring.AddStatusLamp(origin,pageId,pointName,
                                pointName,x,y)));
                        binding.Items.Add(CreateStudioHmiInsertItem(
                            "放置电机Faceplate（复用Symbol）",origin,pageId,scope,pointName,
                            (x,y)=>PHMFaceplateAuthoring.Place(origin,pageId,
                                PHMFaceplateKind.Motor,pointName+"电机",pointName,x,y)));
                        binding.Items.Add(CreateStudioHmiInsertItem(
                            "放置阀门Faceplate（复用Symbol）",origin,pageId,scope,pointName,
                            (x,y)=>PHMFaceplateAuthoring.Place(origin,pageId,
                                PHMFaceplateKind.Valve,pointName+"阀门",pointName,x,y)));
                    }
                    hmi.Items.Add(binding);
                }
                if(scope.Definitions().Length>100)
                    hmi.Items.Add(new MenuItem {
                        Header="仅显示前100项，请按需缩小工程点位范围",IsEnabled=false});
            }
            hmi.Items.Add(new Separator());
            var remoteConnect=new MenuItem {
                Header="连接独立Runtime.Host（同账户Pipe，只读）" };
            remoteConnect.Click+=(sender,args)=>SafeStudioAction(()=>
            {
                RequireStudioHmiTarget(origin,pageId,null,null);
                OpenStudioScadaReadOnlyDialog(origin,pageId);
            });
            hmi.Items.Add(remoteConnect);
            var button=new MenuItem {
                Header="添加控制按钮（未分配命令，在线禁止操作）" };
            button.Click+=(sender,args)=>SafeStudioAction(()=>
            {
                RequireStudioHmiTarget(origin,pageId,null,null);
                MEMPage target=origin.Find(pageId) as MEMPage;
                double x,y;NextStudioHmiPosition(target,out x,out y);
                OpenStudioResourceEditor(pageId,"mem.visual");
                Guid id=PHMHmiScreenAuthoring.AddButton(origin,pageId,"待配置操作",x,y);
                _viewModel.Select(id);
            });
            hmi.Items.Add(button);
            menu.Items.Add(new Separator());
            menu.Items.Add(hmi);
        }

        private void AppendStudioHmiRemotePointItems(MenuItem hmi,
            MEMEngineeringSession origin,Guid pageId)
        {
            IReadOnlyList<PHMScadaPointObservation> points;
            try{points=_studioRemoteHmi.Points();}
            catch(InvalidOperationException)
            {
                hmi.Items.Add(new MenuItem {
                    Header="等待Host首帧或需重新发现版本，暂不能选择点位",
                    IsEnabled=false});
                return;
            }
            foreach(PHMScadaPointObservation point in points.Take(100))
            {
                string id=point.StablePointId;
                string type=point.ValueTypeName;
                bool numeric=type=="UInt16"||type=="Int16"||type=="UInt32"||
                    type=="Int32"||type=="Byte"||type=="SByte"||
                    type=="Double"||type=="Single";
                bool boolean=type=="Boolean";
                if(!numeric&&!boolean)continue;
                MenuItem item=new MenuItem {
                    Header=id+" · "+type+" · "+point.Unit+"（远程只读）"};
                if(numeric)
                {
                    item.Items.Add(CreateStudioHmiInsertItem("添加数值显示",
                        origin,pageId,null,id,(x,y)=>
                            PHMHmiScreenAuthoring.AddNumeric(origin,pageId,id,id,x,y)));
                    item.Items.Add(CreateStudioHmiInsertItem("添加进度条（0～100）",
                        origin,pageId,null,id,(x,y)=>
                            PHMHmiScreenAuthoring.AddProgress(origin,pageId,id,id,x,y,0,100)));
                }
                if(boolean)
                {
                    item.Items.Add(CreateStudioHmiInsertItem("添加状态灯",
                        origin,pageId,null,id,(x,y)=>
                            PHMHmiScreenAuthoring.AddStatusLamp(origin,pageId,id,id,x,y)));
                    item.Items.Add(CreateStudioHmiInsertItem("放置电机Faceplate",
                        origin,pageId,null,id,(x,y)=>
                            PHMFaceplateAuthoring.Place(origin,pageId,
                                PHMFaceplateKind.Motor,id+"电机",id,x,y)));
                    item.Items.Add(CreateStudioHmiInsertItem("放置阀门Faceplate",
                        origin,pageId,null,id,(x,y)=>
                            PHMFaceplateAuthoring.Place(origin,pageId,
                                PHMFaceplateKind.Valve,id+"阀门",id,x,y)));
                }
                hmi.Items.Add(item);
            }
        }

        private MenuItem CreateStudioHmiInsertItem(string caption,
            MEMEngineeringSession origin,Guid pageId,MDSVariableTable expectedScope,
            string pointName,Func<double,double,Guid> creator)
        {
            var item=new MenuItem { Header=caption };
            PHMScadaRemoteReadSession remoteAtMenu=expectedScope==null?_studioRemoteHmi:null;
            item.Click+=(sender,args)=>SafeStudioAction(()=>
            {
                RequireStudioHmiTarget(origin,pageId,expectedScope,pointName);
                if(remoteAtMenu!=null &&
                    (!ReferenceEquals(remoteAtMenu,_studioRemoteHmi)||
                     !remoteAtMenu.Points().Any(x=>x.StablePointId==pointName)))
                    throw new InvalidOperationException("远程工站或点位已失效，重新选择");
                MEMPage page=origin.Find(pageId) as MEMPage;
                double x,y;NextStudioHmiPosition(page,out x,out y);
                OpenStudioResourceEditor(pageId,"mem.visual");
                Guid id=creator(x,y);
                _viewModel.Select(id);
            });
            return item;
        }

        private void RequireStudioHmiTarget(MEMEngineeringSession expected,
            Guid pageId,MDSVariableTable scope,string pointName)
        {
            if(_viewModel==null||!ReferenceEquals(_viewModel.ProjectSession,expected)||
                !(expected.Find(pageId) is MEMPage))
                throw new InvalidOperationException("HMI页面已切换或删除，拒绝旧菜单操作");
            if(scope!=null&&(!ReferenceEquals(scope,_studioVariableScope)||
                !scope.Contains(pointName)))
                throw new InvalidOperationException("HMI原MDS Scope/Point已改变，重新选择");
        }
        private static void NextStudioHmiPosition(MEMPage page,out double x,out double y)
        {
            // 唯一MEM已有图元数量决定初始放置点，实际最终位置仍通过原画布编辑。
            int count=page.RootContent.Children.Count;
            x=32+(count%3)*220;
            y=40+((count/3)%60)*65;
        }

    }
}
