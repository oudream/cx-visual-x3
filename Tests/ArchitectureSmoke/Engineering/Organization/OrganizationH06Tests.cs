using System;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationH06Tests
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("H06: "+message);}
        public static void Run()
        {
            var s=MEMEngineering.Create("views");var first=s.AddPage("P1");var second=s.AddPage("P2");var direct=s.AddRectangle(first,"direct",0,0,20,20);s.AddRectangle(second,"other page",500,0);
            var symbol=s.AddSymbol("panel");var rectangle=s.AddRectangle(symbol.RootGroup,"part",5,6,30,40);s.DefineProperty(symbol,new MPMPropertyDescriptor("W",MPMValueKind.Number,false,true,MPMBinding.Literal(MPMValue.Number(30))));s.ExposeProperty(symbol,"W",s.GetAddress(rectangle),"width");
            var a=s.PlaceSymbol(first.RootContent,symbol,"A");var b=s.PlaceSymbol(first.RootContent,symbol,"B");s.SetPlacement(a,new MRCTransform(1,0,0,1,100,0));s.SetPlacement(b,new MRCTransform(1,0,0,1,200,0));s.SetOverride(a,"W",MPMBinding.Literal(MPMValue.Number(80)));
            using(var editor=new MVIVisualEditor(s))
            {
                editor.SetPage(first.Id);var scene=editor.Scene;Check(scene.Count==3,"current page or placed resource projection missing");var av=scene.Single(i=>i.Id==a.Id);var bv=scene.Single(i=>i.Id==b.Id);
                Check(av.Address.ObjectId==rectangle.Id&&av.Address.InstancePath.Single()==a.Id&&av.Bounds.Width==80&&bv.Bounds.Width==30,"instance address/parameter geometry mixed definitions");
                Check(av.Contains(175,10)&&!bv.Contains(175,10),"instance geometry hit test");
                int revision=s.Revision;EventHandler<MVIInteractionEventArgs> cancel=(o,e)=>{if(e.Kind==MVIInteractionKind.Move&&e.Phase==MVIEventPhase.Capture)e.PreventDefault();};editor.Interaction+=cancel;
                Check(!editor.Move(a.Id,130,0).Success&&s.Revision==revision&&a.Placement.X==100,"canceled UI intent committed");editor.Interaction-=cancel;
                editor.Move(a.Id,130,0).RequireValue();Check(a.Placement.X==130&&rectangle.X==5&&b.Placement.X==200,"instance drag changed definition/other use");editor.Undo().RequireValue();Check(a.Placement.X==100,"UI did not share Core history");
                int refreshed=0;editor.Changed+=(o,e)=>refreshed++;s.Rename(direct,"from code");Check(refreshed==1&&editor.Scene.Any(i=>i.Name=="from code"),"code commit not consumed by view");
                editor.SetPage(second.Id);editor.AddRectangle("on second",10,10,10,10).RequireValue();Check(second.RootContent.VisualItems.Count==2&&first.RootContent.VisualItems.Count==1,"active page add used first page");
            }
            var outer=s.AddSymbol("composed");var nested=s.PlaceSymbol(outer,symbol,"inner");s.SetPlacement(nested,new MRCTransform(1,0,0,1,20,30));var outerUse=s.PlaceSymbol(first.RootContent,outer,"outer");
            var nestedScene=MVISceneProjection.Project(s,first.Id).Single(i=>i.Id==outerUse.Id);Check(nestedScene.Address.InstancePath.SequenceEqual(new[]{outerUse.Id,nested.Id})&&nestedScene.Bounds.X==25&&nestedScene.Bounds.Y==36,"nested Symbol address/placement");
            bool cycle=false;try{s.PlaceSymbol(symbol,outer,"cycle");}catch(InvalidOperationException){cycle=true;}Check(cycle,"nested Symbol reference cycle accepted");
            s.Close(true);Console.WriteLine("PASS H06 real page isolation, placed-instance projection/overrides/address/hit/drag, cancelable routing and shared code/UI history");
        }
    }
}
