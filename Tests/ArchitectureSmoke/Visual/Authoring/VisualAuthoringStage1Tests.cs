using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实MEM工程与几何/样式/Widget定义闭环，不依赖WPF或Mock。</summary>
    internal static class VisualAuthoringStage1Tests
    {
        private static void Check(bool yes,string description)
        {if(!yes)throw new InvalidOperationException("visual.stage1: "+description);}
        private sealed class WidgetTypes : IMPMTypeProvider
        {
            public IEnumerable<MPMTypeDescriptor> DescribeTypes()
            {
                yield return new MPMTypeDescriptor("demo.ui.button",1,MEMNodeKind.Element,
                    new[]{new MPMPropertyDescriptor("caption",MPMValueKind.String,false,true,MPMBinding.Literal(MPMValue.String("Run")))});
            }
        }
        public static void Run()
        {
            // 无WPF的二维几何计算与矩阵约定。
            var p=new FGEPoint2D(5,9);
            var transform=FGETransform2D.Translation(20,30).Compose(FGETransform2D.Rotation(Math.PI/2));
            FGEPoint2D transformed=transform.TransformPoint(p);
            Check(transform.Inverse().TransformPoint(transformed).DistanceTo(p)<1e-8,"transform inversion");
            Check(new FGESegment2D(new FGEPoint2D(0,0),new FGEPoint2D(10,0)).Bounds.Height==0,"zero-width geometry");
            Check(Math.Abs(FGEGeometry2D.DistanceToSegment(new FGEPoint2D(4,3),new FGESegment2D(new FGEPoint2D(0,0),new FGEPoint2D(10,0)))-3)<1e-9,"distance");
            var polygon=new FGEPolygon2D(new[]{new FGEPoint2D(0,0),new FGEPoint2D(20,0),new FGEPoint2D(0,20)});
            Check(polygon.Contains(new FGEPoint2D(2,2))&&!polygon.Contains(new FGEPoint2D(30,30)),"polygon containment");

            var path=new FGEPath2D(new[]{FGEPathCommand.Move(new FGEPoint2D(0,0)),FGEPathCommand.Quadratic(new FGEPoint2D(30,40),new FGEPoint2D(60,0)),FGEPathCommand.Line(new FGEPoint2D(70,30))});
            var sample=new MRCShapeDefinition[]
            {
                MRCShapeDefinition.Line(new FGEPoint2D(0,0),new FGEPoint2D(100,0)),
                MRCShapeDefinition.Rect(80,60),
                MRCShapeDefinition.Circle(new FGEPoint2D(40,40),40),
                MRCShapeDefinition.Ellipse(new FGEPoint2D(60,40),60,40),
                MRCShapeDefinition.Polyline(new[]{new FGEPoint2D(0,0),new FGEPoint2D(30,60),new FGEPoint2D(70,0)}),
                MRCShapeDefinition.Polygon(new[]{new FGEPoint2D(0,0),new FGEPoint2D(80,0),new FGEPoint2D(40,70)}),
                MRCShapeDefinition.Arc(new FGEArc2D(new FGEPoint2D(40,40),40,40,0,Math.PI)),
                MRCShapeDefinition.Bezier(new FGEBezier2D(new FGEPoint2D(0,0),new FGEPoint2D(30,50),new FGEPoint2D(60,50),new FGEPoint2D(90,0))),
                MRCShapeDefinition.Path(path)
            };
            string file=Path.Combine(Path.GetTempPath(),"cxvisual-visual-stage1-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var session=MEMEngineering.Create("visual-stage1");
                var page=session.AddPage("Main");
                var group=session.AddGroup(page.RootContent,"Drawing");
                var ids=new Guid[sample.Length];
                for(int i=0;i<sample.Length;i++)
                {
                    var element=session.AddShape(group,"Shape-"+(i+1),sample[i],i*10,0);
                    ids[i]=element.Id;
                    Check(element.Shape.Kind==(MRCShapeKind)(i+1),"ShapeKind "+i);
                }
                var circle=(MRCShapeElement)session.Find(ids[2]);
                Check(circle.HitTest(40+20,40),"circle fill hit");
                Check(!circle.HitTest(20,0),"circle corner no false hit");
                var zeroLine=(MRCShapeElement)session.Find(ids[0]);
                Check(zeroLine.Shape.Bounds.Height==0,"line exact zero height");
                // 真实复用工业图元：一份定义、两个独立实例、各自的参数覆盖。
                var motor=session.AddSymbol("Motor");
                var motorBody=session.AddShape(motor.RootGroup,"Body",
                    MRCShapeDefinition.Circle(new FGEPoint2D(20,20),20),0,0);
                session.DefineProperty(motor,new MPMPropertyDescriptor("DeviceName",
                    MPMValueKind.String,false,true,
                    MPMBinding.Literal(MPMValue.String("DefaultMotor"))));
                var motorA=session.PlaceSymbol(page.RootContent,motor,"Motor-A");
                var motorB=session.PlaceSymbol(page.RootContent,motor,"Motor-B");
                session.SetOverride(motorA,"DeviceName",
                    MPMBinding.Literal(MPMValue.String("Axis-A")));
                session.SetOverride(motorB,"DeviceName",
                    MPMBinding.Literal(MPMValue.String("Axis-B")));
                Check(session.ResolveParameter(motorA,"DeviceName").Value.AsString()=="Axis-A",
                    "independent symbol A parameters");
                Check(session.ResolveParameter(motorB,"DeviceName").Value.AsString()=="Axis-B",
                    "independent symbol B parameters");
                Check(motorA.TargetId==motorB.TargetId&&motorA.Id!=motorB.Id,
                    "shared symbol definition with distinct instances");
                var text=session.AddText(group,"Title","Original",0,100);
                session.SetText(text,"工程标题");
                session.AddImage(group,"Image",0,200);
                session.AddSvg(group,"Svg",150,200);
                session.RegisterTypes(new WidgetTypes());
                var widget=session.AddTypedElement(group,"Start","demo.ui.button");
                Check(widget.WidgetTypeId=="demo.ui.button","Element type metadata");

                var style=new MRCVisualStyle(MRCBrushDefinition.Solid(MRCColor.Parse("#FF4488AA")),
                    MRCBrushDefinition.Solid(MRCColor.Parse("#112233")),2,0.9,"Segoe UI",12,"theme.primary");
                var behavior=new MRCVisualBehaviorDefinition(
                    new[]{new MRCVisualStateDefinition("Fault",MRCVisualStyle.Solid(MRCColor.Parse("#FF0000")))},
                    new[]{new MRCAnimationDefinition("opacity",0,1,350,"Running",MRCAnimationEasing.EaseInOut,true)},
                    new[]{new MRCVisualEventAction("Click","hmi.open.dialog","dialogId")});
                session.Edit("批量图形定义",e=>{e.SetVisualStyle(session.Find(ids[0]) as MRCVisualItem,style);e.SetVisualBehavior(session.Find(ids[0]) as MRCVisualItem,behavior);return true;}).RequireValue();
                Check(MRCVisualAppearance.StyleOf((MRCVisualItem)session.Find(ids[0])).StrokeWidth==2,"style write");
                Check(MRCVisualAppearance.BehaviorOf((MRCVisualItem)session.Find(ids[0])).EventActions.Count==1,"event write");
                // 动态绑定是工程声明，不因实时值变化而提交工程Revision。
                session.SetBinding((MRCVisualItem)session.Find(ids[0]),"visible",
                    MPMBinding.Variable("Motor.Ready"));
                Check(((MRCVisualItem)session.Find(ids[0])).GetBinding("visible").Kind==
                    MPMBindingKind.Variable,"binding API write");
                int rev=session.Revision;session.SetVisualStyle((MRCVisualItem)session.Find(ids[0]),MRCVisualStyle.Default);
                Check(session.Undo().Success&&MRCVisualAppearance.StyleOf((MRCVisualItem)session.Find(ids[0])).StrokeWidth==2,"undo style");
                Check(session.Revision>=rev,"revision consistent");

                session.SaveAs(file);session.Close();
                var loaded=MEMEngineering.Open(file);
                Check(loaded.Validate().IsValid,"roundtrip validation");
                for(int i=0;i<ids.Length;i++)
                {
                    var element=loaded.Find(ids[i]) as MRCShapeElement;
                    Check(element!=null&&element.Shape.Kind==(MRCShapeKind)(i+1),"shape roundtrip "+i);
                    Check(element.Shape.Geometry.Commands.Count==sample[i].Geometry.Commands.Count,"exact commands "+i);
                }
                Check(((MRCTextElement)loaded.Find(text.Id)).Text=="工程标题","text roundtrip");
                Check(loaded.Find(motorBody.Id) is MRCShapeElement,
                    "symbol member shape roundtrip");
                var restoredMotorA=loaded.FindReference(motorA.Id);
                var restoredMotorB=loaded.FindReference(motorB.Id);
                Check(restoredMotorA!=null&&restoredMotorB!=null&&
                    restoredMotorA.TargetId==restoredMotorB.TargetId,
                    "shared symbol references saved");
                Check(loaded.ResolveParameter(restoredMotorA,"DeviceName").Value.AsString()=="Axis-A"&&
                    loaded.ResolveParameter(restoredMotorB,"DeviceName").Value.AsString()=="Axis-B",
                    "symbol instance binding roundtrip");
                Check(loaded.Find(widget.Id) is MRCWidgetElement,"widget runtime type roundtrip");
                Check(loaded.Find(widget.Id).TypeId=="demo.ui.button","widget type identity");
                var brush=MRCVisualAppearance.StyleOf((MRCVisualItem)loaded.Find(ids[0]));
                Check(brush.Fill.Color.Equals(MRCColor.Parse("#FF4488AA")),"ARGB style roundtrip");
                Check(((MRCVisualItem)loaded.Find(ids[0])).GetBinding("visible").Kind==
                    MPMBindingKind.Variable,"binding source roundtrip");
                var data=MRCVisualAppearance.BehaviorOf((MRCVisualItem)loaded.Find(ids[0]));
                Check(data.States.Count==1&&data.Animations.Count==1&&data.EventActions.Count==1,"behavior roundtrip");
                loaded.Close();
                // 开发者简洁API示例必须真实执行，不只通过编译。
                string quickPath=Path.Combine(Path.GetTempPath(),
                    "cxvisual-quickstart-"+Guid.NewGuid().ToString("N")+".cxproj");
                try
                {
                    CxVisual.Examples.VisualAuthoring.EngineeringVisualQuickStart.Create(quickPath);
                    using(var demo=MEMEngineering.Open(quickPath))
                    {
                        Check(demo.Validate().IsValid,"developer quickstart roundtrip");
                        Check(demo.Project.Pages.Count==1&&demo.Project.Symbols.Count==1,
                            "developer quickstart model");
                        Check(demo.Project.Symbols[0].RootGroup.Children.Count==1,
                            "developer quickstart compound element");
                    }
                }
                finally{if(File.Exists(quickPath))File.Delete(quickPath);}
                Console.WriteLine("PASS Visual M0: nine shapes, exact geometry, text, widget schema, style, state, animation, actions and save/open");
            }
            finally{if(File.Exists(file))File.Delete(file);}
        }
    }
}
