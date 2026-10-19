using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class VisualRuntimeProjectionTests
    {
        private static void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("VisualRuntime: "+message);}
        private sealed class LiveSource : IMDSExternalBindingResolver
        {
            public bool Ready=true,Fault,Running=true,InvalidValue;
            public double Position=56;
            public object Resolve(MDSBindingSource source,string name,Type requested)
            {
                if(source!=MDSBindingSource.Variable)throw new InvalidOperationException("wrong binding Owner");
                if(name=="Motor.Ready")return Ready;
                if(name=="Motor.Position")return InvalidValue?(object)"bad-number":Position;
                if(name=="Fault")return Fault;
                if(name=="Running")return Running;
                return null;
            }
        }
        public static void Run()
        {
            using(var doc=MEMEngineering.Create("Runtime-only visual"))
            {
                var page=doc.AddPage("Page");
                var shape=doc.AddShape(page.RootContent,"Indicator",
                    MRCShapeDefinition.Circle(new FGEPoint2D(30,30),30),10,10);
                doc.SetVisualStyle(shape,MRCVisualStyle.Solid(MRCColor.Parse("#FF0077CC")));
                doc.SetVisualBehavior(shape,new MRCVisualBehaviorDefinition(
                    new[]{new MRCVisualStateDefinition("Fault",
                        MRCVisualStyle.Solid(MRCColor.Parse("#FFE02020")))},
                    new[]{new MRCAnimationDefinition("opacity",0.2,0.8,1000,
                        "Running",MRCAnimationEasing.Linear,true)}));
                doc.SetBinding(shape,"x",MPMBinding.Variable("Motor.Position"));
                doc.SetBinding(shape,"visible",MPMBinding.Variable("Motor.Ready"));
                var source=new LiveSource();
                int revision=doc.Revision;
                var defaultScene=CxSceneProjection.Project(doc,page.Id);
                Check(defaultScene.Count==1&&Math.Abs(defaultScene[0].Bounds.X-10)<1e-9,
                    "design mode fallback");
                Check(Math.Abs(defaultScene[0].Style.Opacity-1)<1e-9,
                    "static design must not run animation at t=0");
                var scene=CxSceneProjection.Project(doc,page.Id,source,500);
                Check(scene.Count==1&&Math.Abs(scene[0].Bounds.X-56)<1e-9,
                    "runtime variable position");
                Check(Math.Abs(scene[0].Style.Opacity-0.5)<1e-9,"runtime easing at midpoint");
                var movingBehavior=new MRCVisualBehaviorDefinition(
                    animations:new[]{new MRCAnimationDefinition("x",80,180,1000,
                        "Running",MRCAnimationEasing.Linear,true)});
                Check(Math.Abs(MVIVisualRuntimeEvaluator.EvaluateNumber(
                    "x",56,movingBehavior,500,source)-130)<1e-8,
                    "runtime X animation did not produce the correct timed value");
                Check(doc.Revision==revision,"runtime animation calculation committed engineering data");
                source.Fault=true;
                scene=CxSceneProjection.Project(doc,page.Id,source,250);
                Check(scene[0].Style.Fill.Color.Equals(MRCColor.Parse("#FFE02020")),
                    "dynamic fault state style");
                source.Ready=false;
                Check(CxSceneProjection.Project(doc,page.Id,source,500).Count==0,
                    "live visibility did not remove shape");
                Check(doc.Revision==revision,"runtime updates changed engineering Revision");
                source.Ready=true;source.InvalidValue=true;
                bool rejected=false;
                try{CxSceneProjection.Project(doc,page.Id,source,0);}
                catch(InvalidCastException){rejected=true;}
                Check(rejected,"binding wrong type silently accepted");
                source.InvalidValue=false;
                using(var editor=new CxVisualEditor(doc))
                {
                    editor.SetRuntimeFrame(source,750);
                    Check(editor.Scene.Count==1&&Math.Abs(editor.Scene[0].Bounds.X-56)<1e-9,
                        "editor runtime frame not reflected");
                    editor.ClearRuntimeFrame();
                    Check(Math.Abs(editor.Scene[0].Bounds.X-10)<1e-9,"runtime frame was not cleared");
                    Check(Math.Abs(editor.Scene[0].Style.Opacity-1)<1e-9,
                        "editor clear must restore declared static appearance");
                }
                Check(doc.Revision==revision,"editor runtime update modified persisted definitions");
                // 事件定义与设备命令执行隔离：无Owner授权必须拒绝，授权才可转发。
                doc.SetVisualBehavior(shape,new MRCVisualBehaviorDefinition(
                    eventActions:new[]{new MRCVisualEventAction("Click","hmi.open")}));
                int invoked=0;
                Check(MVIVisualEventRouter.Dispatch(doc,shape.Id,"Click","ok",null)==0&&
                    invoked==0,"visual command must fail closed without an authorized Owner");
                Check(MVIVisualEventRouter.Dispatch(doc,shape.Id,"Click","ok",request=>
                {
                    invoked++;
                    return request.ActionId=="hmi.open"&&request.EventValue=="ok";
                })==1&&invoked==1,"saved event did not dispatch to approved host");
                Check(MVIVisualEventRouter.Dispatch(doc,shape.Id,"Changed","x",request=>
                {
                    invoked++;return true;
                })==0&&invoked==1,"unmatched event must not invoke host");
                doc.Close(true); // 显式放弃测试定义；正常用户工程仍保持未保存保护
            }
            Console.WriteLine("PASS Visual runtime: MDS Owner variable/source, state style, animation time, visibility, invalid type, editor frame separation");
        }
    }
}
