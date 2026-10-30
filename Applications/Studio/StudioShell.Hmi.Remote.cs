using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.HMI;

namespace CxVisual.Studio
{
    /// <summary>
    /// Studio HMI的独立Runtime.Host只读连接；消费A正式v1 Pipe。
    /// 无设备命令、匿名写入或并行采集器；UI只持受版本约束的快照会话。
    /// </summary>
    public partial class MainWindow
    {
        private PHMScadaRemoteReadSession _studioRemoteHmi;
        private Guid _studioRemoteProjectId;

        private void AttachStudioScadaReadOnly(string pipeName,
            PHMScadaDeviceIdentity approved)
        {
            if(!Dispatcher.CheckAccess())
                throw new InvalidOperationException("hmi.remote.requires_ui_thread");
            if(approved==null||!approved.Running)
                throw new InvalidOperationException("hmi.remote.device_not_running");
            MEMEngineeringSession project=_viewModel.ProjectSession;
            if(!(project.Find(approved.DeviceRef) is IMEMResource))
                throw new InvalidOperationException(
                    "Host DeviceRef不属于当前MEM工程，拒绝跨工程HMI绑定");
            var client=new PHMScadaReadOnlyPipeClient(pipeName);
            PHMScadaRemoteReadSession next=new PHMScadaRemoteReadSession(
                client,approved,TimeSpan.FromSeconds(5));
            try{next.Start(1000);}
            catch{next.Dispose();throw;}
            CloseStudioHmiRuntimeDocuments();
            PHMScadaRemoteReadSession previous=_studioRemoteHmi;
            _studioRemoteHmi=next;
            _studioRemoteProjectId=project.Project.Id;
            _studioVariableScope=null;
            _studioVariableProvider=null;
            _studioVariableWritesAllowed=false;
            // 之前的本地MDS工具页不能继续冒充当前Host的点位。
            TryCloseStudioDocument("doc.variables",true);
            if(previous!=null)previous.Dispose();
            _viewModel.RefreshRuntimeVariableChoices();
        }

        private void ReleaseStudioScadaReadOnly()
        {
            PHMScadaRemoteReadSession prior=_studioRemoteHmi;
            if(prior==null)return;
            _studioRemoteHmi=null;
            _studioRemoteProjectId=Guid.Empty;
            CloseStudioHmiRuntimeDocuments();
            prior.Dispose();
        }

        private FrameworkElement CreateStudioRemoteHmiView(
            MEMEngineeringSession engineering,Guid pageId)
        {
            PHMScadaRemoteReadSession remote=_studioRemoteHmi;
            if(remote==null||engineering.Project.Id!=_studioRemoteProjectId||
                !(engineering.Find(remote.Identity.DeviceRef) is IMEMResource))
                throw new InvalidOperationException(
                    "Host DeviceRef/当前工程身份不一致，禁止使用旧远程帧");
            return new MPWLiveSceneControl(engineering,pageId,remote,
                ()=>remote.Points().Select(point=>new MPWLivePoint {
                    Name=point.StablePointId,
                    Value=point.Text+(point.Unit.Length>0?" "+point.Unit:""),
                    Unit=point.Unit,
                    Quality=(DateTime.UtcNow-point.RefreshUtc>TimeSpan.FromSeconds(5)&&
                             point.Quality==CxVisual.Mechanism.Data.Variables.MDSVariableQuality.Good
                             ?"Stale":point.Quality.ToString()),
                    Source=point.Source,
                    Updated=point.RefreshUtc.ToString("O",CultureInfo.InvariantCulture)
                }).ToArray(),
                refresh=>remote.Subscribe(refresh),
                null);
        }

        private void OpenStudioScadaReadOnlyDialog(MEMEngineeringSession origin,Guid pageId)
        {
            if(!ReferenceEquals(_viewModel.ProjectSession,origin)||
                !(origin.Find(pageId) is MEMPage))
                throw new InvalidOperationException("HMI Page已切换");
            Window dialog=new Window {
                Title="连接独立Runtime.Host（同账户，只读）",
                Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,
                Width=670,Height=420,MinWidth=580,MinHeight=340
            };
            var layout=new DockPanel { Margin=new Thickness(16) };
            var header=new StackPanel();
            header.Children.Add(new TextBlock {
                Text="Pipe名称（由scada-group-serve --read-pipe指定）",
                FontWeight=FontWeights.SemiBold });
            var name=new TextBox { Text="cxvisual-scada-read",Margin=new Thickness(0,5,0,9) };
            header.Children.Add(name);
            var discover=new Button {
                Content="发现受信Host设备（只读）",MinHeight=32 };
            header.Children.Add(discover);
            var information=new TextBlock {
                Text="尚未发现设备。当前会话不提供远程控制和完整订阅。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,9,0,9)
            };
            header.Children.Add(information);
            DockPanel.SetDock(header,Dock.Top);
            layout.Children.Add(header);
            var bottom=new StackPanel {
                Orientation=Orientation.Horizontal,
                HorizontalAlignment=HorizontalAlignment.Right
            };
            var connect=new Button {
                Content="连接所选工站并打开HMI只读页面",
                MinHeight=32,IsEnabled=false,Margin=new Thickness(4)
            };
            var close=new Button { Content="取消",MinHeight=32,Margin=new Thickness(4) };
            bottom.Children.Add(connect);bottom.Children.Add(close);
            DockPanel.SetDock(bottom,Dock.Bottom);
            layout.Children.Add(bottom);
            var devices=new ListBox();
            layout.Children.Add(devices);
            dialog.Content=layout;
            discover.Click+=async (sender,args)=>
            {
                discover.IsEnabled=false;connect.IsEnabled=false;devices.Items.Clear();
                try
                {
                    string pipe=name.Text.Trim();
                    var all=await Task.Run(()=>new PHMScadaReadOnlyPipeClient(pipe).Discover());
                    if(!dialog.IsVisible)return;
                    foreach(PHMScadaDeviceIdentity identity in all)
                        devices.Items.Add(new ListBoxItem {
                            Tag=identity,
                            Content=identity.ScopeId+" · "+identity.DeviceRef.ToString("N")+
                                " · "+(identity.Running?"运行":"离线")+
                                " · Gen="+identity.Generation
                        });
                    information.Text="发现 "+all.Count+
                        " 个设备；必须选当前工程中的真实DeviceRef。";
                    connect.IsEnabled=devices.SelectedItem is ListBoxItem;
                }
                catch(Exception error)
                {
                    information.Text="只读发现失败："+error.Message;
                }
                finally{discover.IsEnabled=true;}
            };
            devices.SelectionChanged+=(sender,args)=>{
                var selected=devices.SelectedItem as ListBoxItem;
                var token=selected==null?null:selected.Tag as PHMScadaDeviceIdentity;
                connect.IsEnabled=token!=null&&token.Running;
            };
            connect.Click+=(sender,args)=>
            {
                try
                {
                    if(!ReferenceEquals(_viewModel.ProjectSession,origin)||
                        !(origin.Find(pageId) is MEMPage))
                        throw new InvalidOperationException("工程已切换，拒绝旧设备连接");
                    var selected=devices.SelectedItem as ListBoxItem;
                    var token=selected==null?null:selected.Tag as PHMScadaDeviceIdentity;
                    AttachStudioScadaReadOnly(name.Text.Trim(),token);
                    OpenStudioHmiPage(pageId);
                    dialog.DialogResult=true;
                }
                catch(Exception error){information.Text="连接失败："+error.Message;}
            };
            close.Click+=(sender,args)=>dialog.DialogResult=false;
            dialog.ShowDialog();
        }
    }
}
