using System;
using System.Linq;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Visual;
using CxVisual.Professional.Automation.Plc;
using CxVisual.Professional.HMI;

namespace CxVisual.Studio
{
    /// <summary>仅G1真实Studio工程编辑与MRK注册目录合同；绝不模拟PLC真实授权。</summary>
    internal static class StudioHmiCommandAuthoringTests
    {
        private static void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("HMI.Command.G1: "+message);}

        internal static Guid Run(MainWindow window,MEMEngineeringSession document,MEMPage page)
        {
            // 设备身份必须来自当前MEM工程；没有权限真实写PLC的测试MRK注册
            // 只用于核对HMI是否消费现有正式命令目录/Schema及保存动作。
            MEMContent device=document.AddContent(document.Project,"HMI G1 PLC设备");
            Guid widgetId=PHMHmiScreenAuthoring.AddButton(document,page.Id,
                "安全写入待授权",350,180);
            FDCCommandDefinition definition=new FDCCommandDefinition(
                801, PADPlcMrkCommandRegistration.WriteUInt16Code,
                "PLC寄存器写入注册合同",
                new[]{
                    new FDCCommandParameterDefinition("StableId",typeof(string),true),
                    new FDCCommandParameterDefinition("Value",typeof(ushort),true),
                    new FDCCommandParameterDefinition("EffectiveVersion",typeof(string),true),
                    new FDCCommandParameterDefinition("SessionGeneration",typeof(string),true)
                },
                new[]{new FDCCommandParameterDefinition("RegisterReadbackMatched",typeof(bool),true)},
                permissionKey:PADPlcMrkCommandRegistration.WritePermission);
            MRKCommandDescriptor descriptor=new MRKCommandDescriptor(
                PADPlcMrkCommandRegistration.TargetForDevice(device.Id.ToString("N")),
                definition,MRKCommandKind.Device);
            using(MRKCommandCenter center=new MRKCommandCenter(authorize:context=>false))
            using(IDisposable registered=center.Register(descriptor,
                new MRKDelegateCommandHandler((context,values,ct)=>
                {
                    throw new InvalidOperationException("G1绝不允许执行设备命令");
                })))
            {
                window.AttachStudioHmiCommandCatalog(center.Router,
                    d=>d.Target+"/"+d.Definition.CommandCode);
                int before=document.Revision;
                window.AssignStudioHmiRegisteredAction(document,page.Id,widgetId,
                    descriptor,"word100");
                Check(document.Revision==before+1,
                    "按钮未通过一次MEM事务保存MRK正式命令引用");
                var action=MRCVisualAppearance.BehaviorOf(
                    document.Find(widgetId) as MRCVisualItem)
                    .EventActions.Single(x=>x.EventId=="Click");
                Check(action.ActionId==descriptor.Target+"/"+definition.CommandCode&&
                    action.ParameterBindingId=="word100",
                    "未保存可信映射的ActionId或PointId参数源");
                var routed=MVIVisualEventRouter.Resolve(document,
                    document.GetAddress(document.Find(widgetId)),"Click",null);
                Check(routed.Count==1&&routed[0].ActionId==action.ActionId&&
                    routed[0].ParameterBindingId=="word100",
                    "MEM保存的Click没有经过既有MVI事件路由");
                before=document.Revision;
                bool denied=false;
                try{window.AssignStudioHmiRegisteredAction(document,page.Id,
                    widgetId,descriptor,"missing-point");}
                catch(InvalidOperationException){denied=true;}
                Check(denied&&document.Revision==before,
                    "未知点位不得修改工程、不得默默下发写入");
                // 原MEM Undo/Redo对HMI动作继续有效，不产生第二本操作历史。
                Check(document.CanUndo,"绑定后的MEM工程不支持Undo");
                document.Undo();
                Check(MRCVisualAppearance.BehaviorOf(
                    document.Find(widgetId) as MRCVisualItem).EventActions
                    .All(x=>x.EventId!="Click"),"HMI绑定Undo未撤销");
                document.Redo();
                Check(MRCVisualAppearance.BehaviorOf(
                    document.Find(widgetId) as MRCVisualItem).EventActions
                    .Any(x=>x.EventId=="Click"),"HMI绑定Redo未恢复");
            }
            window.DetachStudioHmiCommandCatalog();
            return widgetId;
        }
    }
}
