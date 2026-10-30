using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Visual;
using CxVisual.Professional.HMI;

namespace CxVisual.Studio
{
    /// <summary>
    /// HMI单次设定值/设备身份/点位Quality的人工预检工作区。
    /// 仅调用原MVI、MRK Schema及HMI专业校验，不会建立写连接或下发PLC。
    /// </summary>
    public partial class MainWindow
    {
        private void OpenStudioHmiWritePrecheckDialog(MEMEngineeringSession origin,
            Guid pageId)
        {
            RequireStudioHmiTarget(origin,pageId,null,null);
            PHMScadaRemoteReadSession remote=_studioRemoteHmi;
            if(remote==null||_studioHmiCommandCatalog==null||
                _studioHmiActionIdResolver==null)
                throw new InvalidOperationException(
                    "缺少只读在线快照或受信MRK命令目录，禁止准备写入");
            MEMPage page=origin.Find(pageId) as MEMPage;
            MRCWidgetElement[] buttons=page.RootContent.DescendantsAndSelf()
                .OfType<MRCWidgetElement>()
                .Where(x=>x.TypeId=="core.widget.button").ToArray();
            if(buttons.Length==0)
                throw new InvalidOperationException("HMI页面无待确认控制按钮");
            int revision=origin.Revision;
            PHMScadaDeviceIdentity selectedIdentity=remote.Identity;
            Window window=new Window {
                Title="HMI设定值与设备身份预检（不执行PLC操作）",
                Owner=this,Width=690,Height=500,MinWidth=550,MinHeight=400,
                WindowStartupLocation=WindowStartupLocation.CenterOwner
            };
            StackPanel panel=new StackPanel {Margin=new Thickness(14)};
            panel.Children.Add(new TextBlock {
                Text="仅验证已保存Click+MRK命令Schema+实时Good点+受信设备代次。"+
                    "本窗不申请控制权限、不提交MRK OperationId、不写PLC；"+
                    "正式控制必须另经认证Runtime.Host、点级授权、模式及联锁。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)
            });
            panel.Children.Add(new TextBlock {Text="当前工程HMI按钮"});
            ComboBox buttonBox=new ComboBox {
                Name="StudioHmiWriteButton",DisplayMemberPath="Name",
                ItemsSource=buttons,SelectedIndex=0,Margin=new Thickness(0,3,0,10)
            };
            panel.Children.Add(buttonBox);
            TextBlock identity=new TextBlock {
                Text="设备 "+selectedIdentity.DeviceRef.ToString("N")+
                    " / Scope "+selectedIdentity.ScopeId+
                    " / Deployment "+selectedIdentity.DeploymentVersion+
                    " / Effective "+selectedIdentity.ConfigVersion+
                    " / Generation "+selectedIdentity.Generation,
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)
            };
            panel.Children.Add(identity);
            panel.Children.Add(new TextBlock {
                Text="拟写入的 UInt16 设定值（十进制 0～65535）"
            });
            TextBox valueBox=new TextBox {
                Name="StudioHmiWriteValue",
                Margin=new Thickness(0,3,0,10),MaxLength=5
            };
            panel.Children.Add(valueBox);
            CheckBox confirmed=new CheckBox {
                Name="StudioHmiWriteConfirm",
                Content="我已检查当前设备、PointId、设定值及现场安全状态（仅预检确认）",
                Margin=new Thickness(0,0,0,10)
            };
            panel.Children.Add(confirmed);
            TextBlock result=new TextBlock {
                Name="StudioHmiWritePrecheckResult",
                Text="尚未验证；任何预检成功都不等于设备动作或授权。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)
            };
            panel.Children.Add(result);
            Button inspect=new Button {
                Name="StudioHmiWriteInspect",Content="校验强类型参数（不发送）",
                Padding=new Thickness(10,6,10,6),
                HorizontalAlignment=HorizontalAlignment.Left
            };
            panel.Children.Add(inspect);
            inspect.Click+=(sender,args)=>
            {
                try
                {
                    result.Text="验证中：未发送任何设备指令";
                    if(!ReferenceEquals(_viewModel.ProjectSession,origin)||
                        !ReferenceEquals(_studioRemoteHmi,remote)||
                        origin.Revision!=revision)
                        throw new InvalidOperationException(
                            "工程或在线运行会话已切换，拒绝旧操作上下文");
                    MRCWidgetElement button=buttonBox.SelectedItem as MRCWidgetElement;
                    if(button==null)
                        throw new InvalidOperationException("未选择有效按钮");
                    var actions=MVIVisualEventRouter.Resolve(origin,
                        origin.GetAddress(button),"Click",null);
                    if(actions.Count!=1)
                        throw new InvalidOperationException(
                            "按钮必须有且仅有一个已保存的正式Click动作");
                    MVIVisualActionInvocation action=actions[0];
                    MRKCommandDescriptor command=StudioHmiCommands(origin)
                        .SingleOrDefault(d=>String.Equals(
                            _studioHmiActionIdResolver(d),action.ActionId,
                            StringComparison.Ordinal));
                    if(command==null)
                        throw new InvalidOperationException(
                            "保存的ActionId不属于当前受信MRK注册目录");
                    if(!Object.ReferenceEquals(command,_studioHmiCommandCatalog.Resolve(
                        command.Target,command.Definition.CommandCode)))
                        throw new InvalidOperationException(
                            "预检期间正式MRK命令注册已变化");
                    Guid deviceId;
                    if(!Guid.TryParse(command.Target.Substring("Automation/PLC/".Length),
                        out deviceId)||deviceId!=remote.Identity.DeviceRef)
                        throw new InvalidOperationException(
                            "MRK命令目标不是当前在线设备");
                    PHMHmiPreparedWrite prepared=PHMHmiWritePreparation.Prepare(
                        origin,revision,action,action.ActionId,selectedIdentity,
                        remote.Identity,remote.Points(),valueBox.Text,
                        confirmed.IsChecked==true,TimeSpan.FromSeconds(5),DateTime.UtcNow);
                    // 复用MRK唯一强类型Schema：StableId、Value、Effective、
                    // Generation，禁止HMI创建新的参数类型/阈值校验体系。
                    var verified=command.Definition.BindInputs(prepared.Inputs);
                    if(verified.Count!=4)
                        throw new InvalidOperationException("hmi.write.mrk_schema_mismatch");
                    result.Text="软件预检通过（未下发）：PointId="+
                        prepared.StablePointId+"，Value="+prepared.Value+
                        "，Effective="+prepared.ConfigVersion+
                        "，Generation="+prepared.SessionGeneration+
                        "。实际提交仍需受信Host重新验证、授权及读取设备回执。";
                }
                catch(Exception error)
                {
                    result.Text="预检拒绝（无设备命令）："+error.Message;
                }
            };
            window.Content=new ScrollViewer {Content=panel,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            window.ShowDialog();
        }
    }
}
