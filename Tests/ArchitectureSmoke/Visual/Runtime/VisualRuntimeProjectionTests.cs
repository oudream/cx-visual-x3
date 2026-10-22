using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
        private sealed class CustomWidgets : IMPMTypeProvider
        {
            public IEnumerable<MPMTypeDescriptor> DescribeTypes()
            {
                return new[]{new MPMTypeDescriptor("demo.widget.slider",1,
                    MEMNodeKind.Element)};
            }
        }
        private sealed class LiveSource : IMDSExternalBindingResolver
        {
            public bool Ready=true,Fault,Running=true,InvalidValue;
            public bool MotorAReady=true,MotorBReady=false;
            public double Position=56;
            public object Resolve(MDSBindingSource source,string name,Type requested)
            {
                if(source!=MDSBindingSource.Variable)throw new InvalidOperationException("wrong binding Owner");
                if(name=="Motor.Ready")return Ready;
                if(name=="Motor.A.Ready")return MotorAReady;
                if(name=="Motor.B.Ready")return MotorBReady;
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
                var defaultScene=MVISceneProjection.Project(doc,page.Id);
                Check(defaultScene.Count==1&&Math.Abs(defaultScene[0].Bounds.X-10)<1e-9,
                    "design mode fallback");
                Check(Math.Abs(defaultScene[0].Style.Opacity-1)<1e-9,
                    "static design must not run animation at t=0");
                var scene=MVISceneProjection.Project(doc,page.Id,source,500);
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
                scene=MVISceneProjection.Project(doc,page.Id,source,250);
                Check(scene[0].Style.Fill.Color.Equals(MRCColor.Parse("#FFE02020")),
                    "dynamic fault state style");
                source.Ready=false;
                Check(MVISceneProjection.Project(doc,page.Id,source,500).Count==0,
                    "live visibility did not remove shape");
                Check(doc.Revision==revision,"runtime updates changed engineering Revision");
                source.Ready=true;source.InvalidValue=true;
                bool rejected=false;
                try{MVISceneProjection.Project(doc,page.Id,source,0);}
                catch(InvalidCastException){rejected=true;}
                Check(rejected,"binding wrong type silently accepted");
                source.InvalidValue=false;
                using(var editor=new MVIVisualEditor(doc))
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
                bool oversizeRejected=false;
                try
                {
                    MVIVisualEventRouter.Dispatch(doc,shape.Id,"Click",
                        new string('x',65537),request=>true);
                }
                catch(ArgumentException){oversizeRejected=true;}
                Check(oversizeRejected,"unbounded Widget input must not enter authorized host");
                doc.SetVisibility(shape,false,false);
                Check(MVIVisualEventRouter.Dispatch(doc,shape.Id,"Click","x",
                    request=>{invoked++;return true;})==0&&invoked==1,
                    "hidden shape must never dispatch visual actions");
                doc.RegisterTypes(new CustomWidgets());
                using(var customEditor=new MVIVisualEditor(doc))
                {
                    MTCEditResult<bool> invalid=customEditor.AddWidget("Unregistered",
                        "core.widget.unregistered",15,20);
                    Check(!invalid.Success,"unregistered core widget must be rejected before saving");
                    MTCEditResult<bool> permitted=customEditor.AddWidget("CustomSlider",
                        "demo.widget.slider",15,20);
                    Check(permitted.Success&&
                        customEditor.Scene.Count(x=>x.ElementTypeId=="demo.widget.slider")==1,
                        "registered custom Element Widget failed to project");
                }
                doc.Close(true); // 显式放弃测试定义；正常用户工程仍保持未保存保护
            }
            // 真实电机Symbol双实例：共享工程定义但各自覆盖不同MDS来源。
            // 本案例不借助第二个数据仓库；每帧投影不能产生工程Revision。
            string motorFile=Path.Combine(Path.GetTempPath(),
                "cxvisual-motors-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                using(var motors=MEMEngineering.Create("Motor symbol runtime"))
                {
                    var page=motors.AddPage("Motors");
                    var definition=motors.AddSymbol("Motor");
                    var indicator=motors.AddShape(definition.RootGroup,"Indicator",
                        MRCShapeDefinition.Circle(new FGEPoint2D(20,20),20),0,0);
                    motors.DefineProperty(definition,new MPMPropertyDescriptor("Online",
                        MPMValueKind.Boolean,false,true,
                        MPMBinding.Literal(MPMValue.Boolean(true))));
                    motors.ExposeProperty(definition,"Online",
                        motors.GetAddress(indicator),"visible");
                    var first=motors.PlaceSymbol(page.RootContent,definition,"Motor-A");
                    var second=motors.PlaceSymbol(page.RootContent,definition,"Motor-B");
                    motors.SetPlacement(first,new MRCTransform(1,0,0,1,100,20));
                    motors.SetPlacement(second,new MRCTransform(1,0,0,1,200,20));
                    motors.SetOverride(first,"Online",MPMBinding.Variable("Motor.A.Ready"));
                    motors.SetOverride(second,"Online",MPMBinding.Variable("Motor.B.Ready"));
                    var source=new LiveSource();
                    int revision=motors.Revision;
                    var visible=MVISceneProjection.Project(motors,page.Id,source,0);
                    Check(visible.Count==1&&visible[0].Id==first.Id&&
                        visible[0].Address.InstancePath.Single()==first.Id,
                        "Motor-A did not resolve its own MDS instance override");
                    source.MotorAReady=false;source.MotorBReady=true;
                    visible=MVISceneProjection.Project(motors,page.Id,source,40);
                    Check(visible.Count==1&&visible[0].Id==second.Id&&
                        visible[0].Address.InstancePath.Single()==second.Id,
                        "Motor-B runtime value leaked from another symbol instance");
                    source.MotorAReady=true;
                    visible=MVISceneProjection.Project(motors,page.Id,source,80);
                    Check(visible.Count==2&&visible.Select(x=>x.Id).Distinct().Count()==2&&
                        motors.Revision==revision,
                        "runtime source refresh mutated MEM or merged separate instances");
                    motors.SaveAs(motorFile);
                    using(var restored=MEMEngineering.Open(motorFile))
                    {
                        var a=restored.FindReference(first.Id);
                        var b=restored.FindReference(second.Id);
                        Check(a!=null&&b!=null&&a.TargetId==b.TargetId,
                            "shared definition was not preserved by project Save/Open");
                        source.MotorBReady=false;
                        visible=MVISceneProjection.Project(restored,page.Id,source,120);
                        Check(visible.Count==1&&visible[0].Id==first.Id&&
                            restored.ResolveParameter(a,"Online").Kind==MPMBindingKind.Variable&&
                            restored.ResolveParameter(b,"Online").Kind==MPMBindingKind.Variable,
                            "symbol instance MDS bindings lost after project reopen");
                    }
                }
            }
            finally
            {
                if(File.Exists(motorFile))File.Delete(motorFile);
            }
            // 同一Widget定义放进两个Symbol实例：场景选择Id可能是同一类引用Id，
            // 只有完整MRIObjectAddress才能把已授权动作归属到正确实例。
            using(var instanceEvents=MEMEngineering.Create("Symbol widget events"))
            {
                var page=instanceEvents.AddPage("Control");
                var symbol=instanceEvents.AddSymbol("Reusable motor panel");
                var button=new MRCWidgetElement(Guid.NewGuid(),"Start","core.widget",0,0,90,35);
                instanceEvents.Add(symbol.RootGroup,button);
                instanceEvents.SetVisualBehavior(button,new MRCVisualBehaviorDefinition(
                    eventActions:new[]{new MRCVisualEventAction("Click","motor.start")}));
                var first=instanceEvents.PlaceSymbol(page.RootContent,symbol,"Motor-A");
                var second=instanceEvents.PlaceSymbol(page.RootContent,symbol,"Motor-B");
                instanceEvents.SetPlacement(first,new MRCTransform(1,0,0,1,30,30));
                instanceEvents.SetPlacement(second,new MRCTransform(1,0,0,1,180,30));
                var scene=MVISceneProjection.Project(instanceEvents,page.Id);
                Check(scene.Count==2&&scene.All(x=>x.Address.ObjectId==button.Id)&&
                    scene.Select(x=>x.Address.InstancePath[0]).Distinct().Count()==2,
                    "Widget instance address missing in Scene projection");
                int called=0;
                foreach(var row in scene)
                {
                    int result=MVIVisualEventRouter.Dispatch(instanceEvents,row.Address,
                        "Click","requested",request=>
                        {
                            Check(request.ItemId==button.Id&&
                                request.Address.Equals(row.Address)&&
                                request.ActionId=="motor.start",
                                "authorized action lost its instance identity");
                            called++;return true;
                        });
                    Check(result==1,"Symbol Widget action not routed through authorized host");
                    Check(MVIVisualEventRouter.Dispatch(instanceEvents,row.Id,
                        "Click","requested",request=>true)==0,
                        "ambiguous reference Guid must not execute a definition event");
                }
                Check(called==2,"two distinct Widget instances were not dispatched separately");
                instanceEvents.Close(true); // 此处仅做无磁盘事件验证，明确丢弃临时工程
            }
            Console.WriteLine("PASS Visual runtime: MDS Owner, state/animation, Widget events, Motor Symbol dual instances and disk roundtrip");
        }
    }
}
