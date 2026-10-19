using System;
using System.Collections.Generic;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;

namespace CxVisual.Examples.VisualAuthoring
{
    /// <summary>
    /// 工程可视化开发者入门：使用原工程会话和原配置/数据契约，无需构造第二套画布对象。
    /// 此示例创建可持久化定义，真实WPF渲染和Runtime绑定执行由后续UI阶段消费。
    /// </summary>
    public static class EngineeringVisualQuickStart
    {
        private sealed class ControlTypes : IMPMTypeProvider
        {
            public IEnumerable<MPMTypeDescriptor> DescribeTypes()
            {
                yield return new MPMTypeDescriptor("demo.hmi.button",1,MEMNodeKind.Element,
                    new[]{new MPMPropertyDescriptor("caption",MPMValueKind.String,false,true,
                        MPMBinding.Literal(MPMValue.String("启动")))});
            }
        }

        public static void Create(string filePath)
        {
            if(string.IsNullOrWhiteSpace(filePath))throw new ArgumentException("Save path required.","filePath");
            using(MEMEngineeringSession project=MEMEngineering.Create("生产线监控"))
            {
                MEMPage page=project.AddPage("总览");
                MEMContent canvas=page.RootContent;
                MRCGroup group=project.AddGroup(canvas,"设备区域");

                MRCShapeElement lamp=project.AddShape(group,"运行指示灯",
                    MRCShapeDefinition.Circle(new FGEPoint2D(20,20),20),50,60);
                project.SetVisualStyle(lamp,new MRCVisualStyle(
                    MRCBrushDefinition.Solid(MRCColor.Parse("#FF4A90E2")),
                    MRCBrushDefinition.Solid(MRCColor.Parse("#FF165080")),2));
                project.SetBinding(lamp,"visible",MPMBinding.Variable("Motor.Ready"));
                project.SetVisualBehavior(lamp,new MRCVisualBehaviorDefinition(
                    new[]{new MRCVisualStateDefinition("Fault",
                        MRCVisualStyle.Solid(MRCColor.Parse("#FFD93030")))},
                    new[]{new MRCAnimationDefinition("opacity",0.3,1,500,"Fault",
                        MRCAnimationEasing.EaseInOut,true)},
                    new[]{new MRCVisualEventAction("Click","hmi.show.motor-details")}));

                project.AddText(group,"标题","电机运行状态",20,12);
                MRCShapeElement pipe=project.AddShape(group,"连线",
                    MRCShapeDefinition.Polyline(new[]{
                        new FGEPoint2D(0,0),new FGEPoint2D(40,0),new FGEPoint2D(40,80)}),130,80);

                // 复杂工业图元：单份Symbol定义，多个独立引用及参数覆盖。
                MEMSymbol motor=project.AddSymbol("电机组件");
                project.AddShape(motor.RootGroup,"机身",
                    MRCShapeDefinition.Rect(100,60),0,0);
                project.DefineProperty(motor,new MPMPropertyDescriptor("设备名",MPMValueKind.String,
                    false,true,MPMBinding.Literal(MPMValue.String("未绑定"))));
                MRIResourceReference motorA=project.PlaceSymbol(canvas,motor,"电机A");
                MRIResourceReference motorB=project.PlaceSymbol(canvas,motor,"电机B");
                project.SetOverride(motorA,"设备名",MPMBinding.Literal(MPMValue.String("MotorA")));
                project.SetOverride(motorB,"设备名",MPMBinding.Literal(MPMValue.String("MotorB")));

                // UI控件是统一Element注册类型，不把WPF Button写进工程。
                project.RegisterTypes(new ControlTypes());
                MRCWidgetElement start=project.AddTypedElement(group,"启动按钮","demo.hmi.button");
                project.SetProperty(start,"caption",MPMBinding.Literal(MPMValue.String("启动")));
                if(!project.Validate().IsValid)throw new InvalidOperationException("Invalid visual project.");
                project.SaveAs(filePath);
            }
        }
    }
}
