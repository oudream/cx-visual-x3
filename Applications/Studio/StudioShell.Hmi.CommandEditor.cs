using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Professional.Automation.Plc;
using CxVisual.Professional.HMI;

namespace CxVisual.Studio
{
    /// <summary>
    /// MEM保存原HMI Click动作；命令候选仅由既有受信MRK Router贡献。
    /// 此处从不建立命令中心、提交MRK请求或连接PLC。
    /// </summary>
    public partial class MainWindow
    {
        private MRKCommandRouter _studioHmiCommandCatalog;
        private Func<MRKCommandDescriptor,string> _studioHmiActionIdResolver;

        /// <summary>
        /// 仅用于工程编辑：可信运行方注入现有MRK命令目录和其ActionId映射。
        /// 没有贡献时Studio不提供命令保存入口；不意味着获得运行控制授权。
        /// </summary>
        public void AttachStudioHmiCommandCatalog(MRKCommandRouter router,
            Func<MRKCommandDescriptor,string> actionIdResolver)
        {
            if(router==null||actionIdResolver==null)
                throw new ArgumentNullException(router==null?"router":"actionIdResolver");
            _studioHmiCommandCatalog=router;
            _studioHmiActionIdResolver=actionIdResolver;
        }

        public void DetachStudioHmiCommandCatalog()
        {
            _studioHmiCommandCatalog=null;
            _studioHmiActionIdResolver=null;
        }

        private static bool IsScadaWriteDescriptor(MRKCommandDescriptor item)
        {
            if(item==null||item.Kind!=MRKCommandKind.Device||
                item.Definition.SchemaVersion!=1||
                item.Definition.CommandCode!=PADPlcMrkCommandRegistration.WriteUInt16Code||
                item.Definition.PermissionKey!=PADPlcMrkCommandRegistration.WritePermission||
                !item.Target.StartsWith("Automation/PLC/",StringComparison.Ordinal))
                return false;
            var inputs=item.Definition.Inputs;
            return inputs.Count==4&&
                inputs.Any(x=>x.Code=="StableId"&&x.DataType==typeof(string)&&x.Required)&&
                inputs.Any(x=>x.Code=="Value"&&x.DataType==typeof(ushort)&&x.Required)&&
                inputs.Any(x=>x.Code=="EffectiveVersion"&&x.DataType==typeof(string)&&x.Required)&&
                inputs.Any(x=>x.Code=="SessionGeneration"&&x.DataType==typeof(string)&&x.Required);
        }

        private MRKCommandDescriptor[] StudioHmiCommands(MEMEngineeringSession document)
        {
            if(document==null||_studioHmiCommandCatalog==null)
                return new MRKCommandDescriptor[0];
            return _studioHmiCommandCatalog.Discover(kind:MRKCommandKind.Device)
                .Where(IsScadaWriteDescriptor)
                .Where(d=>
                {
                    Guid id;
                    return Guid.TryParse(d.Target.Substring("Automation/PLC/".Length),out id)&&
                        document.Find(id) is IMEMResource;
                })
                .Take(100).ToArray();
        }

        private string[] StudioHmiCommandPoints(Guid deviceId)
        {
            if(_studioRemoteHmi!=null)
            {
                if(_studioRemoteHmi.Identity.DeviceRef!=deviceId)return new string[0];
                try
                {
                    return _studioRemoteHmi.Points().Where(p=>p.ValueTypeName=="UInt16")
                        .Select(p=>p.StablePointId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(x=>x,StringComparer.Ordinal).Take(100).ToArray();
                }
                catch(InvalidOperationException){return new string[0];}
            }
            if(_studioVariableScope==null)return new string[0];
            return _studioVariableScope.Definitions().Where(d=>d.ValueType==typeof(ushort))
                .Select(d=>d.Name).Distinct(StringComparer.Ordinal)
                .OrderBy(x=>x,StringComparer.Ordinal).Take(100).ToArray();
        }

        /// <summary>
        /// 真实已注册MRK Schema + MEM Widget + 当前点源三重检查后才允许保存；
        /// 参数键在原Visual事件ParameterBindingId，不做新持久化格式。
        /// </summary>
        internal void AssignStudioHmiRegisteredAction(MEMEngineeringSession expected,
            Guid pageId,Guid buttonId,MRKCommandDescriptor selected,string pointId)
        {
            RequireStudioHmiTarget(expected,pageId,null,null);
            MRCWidgetElement button=expected.Find(buttonId) as MRCWidgetElement;
            if(button==null||button.TypeId!="core.widget.button"||
                !button.Ancestors().Any(x=>x.Id==pageId))
                throw new InvalidOperationException("hmi.action.button_not_in_current_page");
            if(!IsScadaWriteDescriptor(selected)||
                _studioHmiCommandCatalog==null||_studioHmiActionIdResolver==null)
                throw new InvalidOperationException("hmi.action.trusted_catalog_required");
            MRKCommandDescriptor actual=_studioHmiCommandCatalog.Resolve(
                selected.Target,selected.Definition.CommandCode);
            if(!Object.ReferenceEquals(actual,selected))
                throw new InvalidOperationException("hmi.action.command_registration_stale");
            Guid deviceId;
            if(!Guid.TryParse(selected.Target.Substring("Automation/PLC/".Length),out deviceId)||
                !(expected.Find(deviceId) is IMEMResource)||
                String.IsNullOrWhiteSpace(pointId)||
                !StudioHmiCommandPoints(deviceId).Contains(pointId,StringComparer.Ordinal))
                throw new InvalidOperationException("hmi.action.device_or_point_stale");
            string savedId=_studioHmiActionIdResolver(actual);
            if(String.IsNullOrWhiteSpace(savedId)||savedId.Length>512)
                throw new InvalidOperationException("hmi.action.registered_id_missing");
            // 只保存意图和现有绑定；Value与双版本必须在受信Host的操作流程
            // 按原MRK Schema校验并提供，绝不把只读Pipe的Generation当授权。
            PHMHmiScreenAuthoring.AssignSavedAction(expected,buttonId,savedId,pointId);
        }

        private void OpenStudioHmiCommandEditor(MEMEngineeringSession origin,Guid pageId)
        {
            RequireStudioHmiTarget(origin,pageId,null,null);
            MEMPage page=origin.Find(pageId) as MEMPage;
            MRCWidgetElement[] buttons=page.RootContent.DescendantsAndSelf()
                .OfType<MRCWidgetElement>().Where(x=>x.TypeId=="core.widget.button")
                .ToArray();
            MRKCommandDescriptor[] commands=StudioHmiCommands(origin);
            if(buttons.Length==0||commands.Length==0)
                throw new InvalidOperationException(buttons.Length==0?
                    "请先在HMI Page创建控制按钮":"当前没有受信MRK注册的SCADA写命令，不能使用静态预检冒充命令注册");
            int expectedRevision=origin.Revision;
            Window dialog=new Window {
                Title="HMI按钮：从已注册MRK命令选择（仅工程编辑）",
                Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,
                Width=690,Height=440,MinWidth=560,MinHeight=360
            };
            StackPanel layout=new StackPanel {Margin=new Thickness(16)};
            layout.Children.Add(new TextBlock {
                Text="仅保存MEM按钮动作。普通设计预览及Runtime.Host只读Pipe均禁止PLC写入；运行时仍须可信用户、Effective/Generation、Value及现场联锁。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)
            });
            layout.Children.Add(new TextBlock {Text="当前Page的控制按钮"});
            ComboBox buttonBox=new ComboBox {Margin=new Thickness(0,2,0,10),
                DisplayMemberPath="Name",ItemsSource=buttons,SelectedIndex=0};
            layout.Children.Add(buttonBox);
            layout.Children.Add(new TextBlock {Text="MRK正式注册的PLC命令（当前工程DeviceRef）"});
            ComboBox commandsBox=new ComboBox {Margin=new Thickness(0,2,0,10),
                ItemsSource=commands,DisplayMemberPath="Definition.Name",SelectedIndex=0};
            layout.Children.Add(commandsBox);
            TextBlock commandDescription=new TextBlock {
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,9)
            };
            layout.Children.Add(commandDescription);
            layout.Children.Add(new TextBlock {Text="从当前运行数据源选择UInt16 PointId（不是手填内部Guid）"});
            ComboBox pointBox=new ComboBox {Margin=new Thickness(0,2,0,10)};
            layout.Children.Add(pointBox);
            Action refresh=()=>
            {
                MRKCommandDescriptor chosen=commandsBox.SelectedItem as MRKCommandDescriptor;
                pointBox.ItemsSource=null;
                if(chosen==null)return;
                Guid device;
                if(!Guid.TryParse(chosen.Target.Substring("Automation/PLC/".Length),out device))
                    return;
                pointBox.ItemsSource=StudioHmiCommandPoints(device);
                pointBox.SelectedIndex=pointBox.Items.Count>0?0:-1;
                commandDescription.Text=chosen.Target+" / "+
                    chosen.Definition.CommandCode+" / schema="+
                    chosen.Definition.SchemaVersion+" / "+chosen.Definition.PermissionKey+
                    "。参数：StableId:string、Value:UInt16、EffectiveVersion:string、SessionGeneration:string。"+
                    "当前仅保存PointId绑定；Value和双版本在运行时由可信Host补齐并最终授权。";
            };
            commandsBox.SelectionChanged+=(sender,args)=>refresh();
            refresh();
            TextBlock result=new TextBlock {TextWrapping=TextWrapping.Wrap,
                Margin=new Thickness(0,8,0,8)};
            layout.Children.Add(result);
            Button save=new Button {Content="保存到MEM Click事件（不执行）",
                Padding=new Thickness(12,6,12,6),
                HorizontalAlignment=HorizontalAlignment.Left};
            save.Click+=(sender,args)=>
            {
                try
                {
                    if(origin.Revision!=expectedRevision)
                        throw new InvalidOperationException("HMI工程版本已改变，请重新打开");
                    MRCWidgetElement button=buttonBox.SelectedItem as MRCWidgetElement;
                    MRKCommandDescriptor command=commandsBox.SelectedItem as MRKCommandDescriptor;
                    string point=pointBox.SelectedItem as string;
                    if(button==null)throw new InvalidOperationException("未选择有效按钮");
                    AssignStudioHmiRegisteredAction(origin,pageId,button.Id,command,point);
                    dialog.DialogResult=true;
                }
                catch(Exception error){result.Text="保存被拒绝："+error.Message;}
            };
            layout.Children.Add(save);
            ScrollViewer scroll=new ScrollViewer {Content=layout,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            dialog.Content=scroll;
            dialog.ShowDialog();
        }
    }
}
