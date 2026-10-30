using System;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Professional.HMI
{
    /// <summary>工业Faceplate复用唯一MEM Symbol/Instance，不建立平行组件定义树。</summary>
    public enum PHMFaceplateKind { Motor=1, Valve=2 }

    public static class PHMFaceplateAuthoring
    {
        private const string Online="Online";
        public static Guid Place(MEMEngineeringSession project,Guid pageId,
            PHMFaceplateKind kind,string instanceName,string booleanPoint,double x,double y)
        {
            if(project==null)throw new ArgumentNullException("project");
            if(!Enum.IsDefined(typeof(PHMFaceplateKind),kind))
                throw new ArgumentOutOfRangeException("kind");
            if(String.IsNullOrWhiteSpace(instanceName)||instanceName.Length>256||
               String.IsNullOrWhiteSpace(booleanPoint)||booleanPoint.Length>512)
                throw new ArgumentException("hmi.faceplate.identity_invalid");
            if(Double.IsNaN(x)||Double.IsNaN(y)||Double.IsInfinity(x)||Double.IsInfinity(y)||
                Math.Abs(x)>10000000||Math.Abs(y)>10000000)
                throw new ArgumentOutOfRangeException("hmi.faceplate.position_invalid");
            MEMPage page=project.Find(pageId) as MEMPage;
            if(page==null)throw new InvalidOperationException("hmi.faceplate.page_missing");
            string definitionName=kind==PHMFaceplateKind.Motor?
                "HMI.Motor.Faceplate.v1":"HMI.Valve.Faceplate.v1";
            MEMSymbol old=project.Project.Children.OfType<MEMSymbol>()
                .FirstOrDefault(s=>s.Name==definitionName);
            if(old!=null)
            {
                if(!old.Properties.Any(p=>p.Descriptor.MemberId==Online))
                    throw new InvalidOperationException("hmi.faceplate.symbol_definition_conflict");
            }
            return project.Edit("HMI放置工业设备Faceplate实例",e=>
            {
                MEMSymbol symbol=old;
                if(symbol==null)
                {
                    symbol=e.AddSymbol(definitionName);
                    // 灰色作为失联、Bad、Uncertain及false的安全基态。
                    MRCRectangle chassis=e.AddRectangle(symbol.RootGroup,
                        "外框",0,0,176,72);
                    e.SetVisualStyle(chassis,new MRCVisualStyle(
                        MRCBrushDefinition.Solid(MRCColor.Parse("#FF243342")),
                        MRCBrushDefinition.Solid(MRCColor.Parse("#FF526678")),1.5));
                    MRCRectangle offline=e.AddRectangle(symbol.RootGroup,
                        "离线底灯",12,16,36,36);
                    e.SetVisualStyle(offline,MRCVisualStyle.Solid(
                        MRCColor.Parse("#FF7C8796")));
                    MRCRectangle running=e.AddRectangle(symbol.RootGroup,
                        "运转绿灯",12,16,36,36);
                    e.SetVisualStyle(running,MRCVisualStyle.Solid(
                        MRCColor.Parse("#FF25AC68")));
                    e.DefineProperty(symbol,new MPMPropertyDescriptor(Online,
                        MPMValueKind.Boolean,false,true,
                        MPMBinding.Literal(MPMValue.Boolean(false))));
                    e.ExposeProperty(symbol,Online,project.GetAddress(running),"visible");
                }
                // 同一共享定义通过MRI引用传入不同绑定；不存在多份独立图形树。
                MRIResourceReference instance=e.Reference(page.RootContent,
                    symbol,instanceName.Trim());
                e.SetPlacement(instance,new MRCTransform(1,0,0,1,x,y));
                e.SetOverride(instance,Online,MPMBinding.Variable(booleanPoint.Trim()));
                // 原MEM的公开成员映射暂不支持Text文本成员，不绕过Owner制造私有映射；
                // 实例标签作为同一Page上的原生Text，在相同MTC事务内保存。
                e.AddText(page.RootContent,instanceName.Trim()+"标签",
                    instanceName.Trim(),x+58,y+19,112,30);
                return instance.Id;
            }).RequireValue();
        }
    }
}
