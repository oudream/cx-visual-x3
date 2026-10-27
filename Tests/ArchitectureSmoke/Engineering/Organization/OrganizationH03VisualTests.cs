using System;
using System.IO;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationH03VisualTests
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("H03 visual: "+message);}
        private static double[] Corners(MRCVisualItem item)
        {var p=new double[8];item.TransformPoint(0,0,out p[0],out p[1]);item.TransformPoint(item.Width,0,out p[2],out p[3]);item.TransformPoint(item.Width,item.Height,out p[4],out p[5]);item.TransformPoint(0,item.Height,out p[6],out p[7]);return p;}
        private static void Equal(double[] a,double[] b,string message){Check(a.Zip(b,(x,y)=>Math.Abs(x-y)<1e-7).All(v=>v),message);}
        public static void Run()
        {
            CheckGroupSelectionLifetime();
            string path=Path.Combine(Path.GetTempPath(),"cx-h03-visual-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var s=MEMEngineering.Create("groups");var p=s.AddPage("P");var parent=s.AddGroup(p.RootContent,"parent");s.Move(parent,40,50);s.Rotate(parent,17);s.Scale(parent,1.3,.7);
                var a=s.AddRectangle(parent,"A",10,20,20,30);s.Rotate(a,25);var b=s.AddGroup(parent,"B");s.Move(b,60,70);var nested=s.AddRectangle(b,"nested",3,4,10,15);
                var beforeA=Corners(a);var beforeB=Corners(nested);MRCGroup grouped=s.Group(new MRCVisualItem[]{b,a},"combined");
                Equal(beforeA,Corners(a),"group changed A world shape");Equal(beforeB,Corners(nested),"group changed nested shape");Check(grouped.Children[0]==a&&grouped.Children[1]==b,"group ignored original sibling order");
                s.Rotate(grouped,37);s.Scale(grouped,2,.5);beforeA=Corners(a);beforeB=Corners(nested);
                var lifted=s.Ungroup(grouped);Equal(beforeA,Corners(a),"ungroup lost rotated nonuniform/shear transform");Equal(beforeB,Corners(nested),"nested ungroup shape");
                Check(lifted.Count==2&&s.Find(grouped.Id)==null&&Math.Abs(a.Shear)>1e-5,"full affine bake missing");
                Check(s.Undo().Success&&a.Parent==grouped&&s.Find(grouped.Id)==grouped,"ungroup undo index");Equal(beforeA,Corners(a),"ungroup undo geometry");s.Redo();
                s.SaveAs(path);s.Close();var open=MEMEngineering.Open(path);Equal(beforeA,Corners((MRCVisualItem)open.Find(a.Id)),"affine save/reopen");open.Close();
                var fail=MEMEngineering.Create("constraints");var page=fail.AddPage("P");var x=fail.AddRectangle(page,"X",0,0);var y=fail.AddRectangle(page,"Y",20,0);
                fail.SetLocked(x,true);int count=fail.Count,revision=fail.Revision;
                var result=fail.Edit("locked",e=>e.Group(new MRCVisualItem[]{x,y},"invalid"));Check(!result.Success&&fail.Count==count&&fail.Revision==revision,"locked selection partially grouped");
                fail.SetLocked(x,false);fail.SetLayer(x,"front");fail.SetLayer(y,"back");result=fail.Edit("layer",e=>e.Group(new MRCVisualItem[]{x,y},"invalid"));Check(!result.Success,"cross-layer grouping accepted");
                var invalid=fail.Edit("singular",e=>e.SetTransform(x,new MRCTransform(1,0,2,0,0,0)));Check(!invalid.Success&&x.X==0,"singular affine mutated node");
                fail.Close(true);
                Console.WriteLine("PASS H03 visual real mixed/nested grouping, affine/shear ungrouping, undo, lock/layer protection and file roundtrip");
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        private static void CheckGroupSelectionLifetime()
        {
            var session=MEMEngineering.Create("Group选区投影回归");
            try
            {
                var page=session.AddPage("Page");
                var a=session.AddRectangle(page.RootContent,"A",10,20,30,40);
                var b=session.AddRectangle(page.RootContent,"B",70,80,20,30);
                using(var editor=new MVIVisualEditor(session))
                {
                    editor.SetPage(page.Id);
                    var group=session.Group(new MRCVisualItem[]{a,b},"Group");
                    int revision=session.Revision;
                    Check(ReferenceEquals(session.Find(group.Id),group)&&
                        editor.Scene.Count==2&&editor.Scene.All(item=>item.Id!=group.Id),
                        "Group must remain an engineering container, not a renderable leaf");
                    Check(MVISceneProjection.Project(session,page.Id).Count==2,
                        "Group container leaked into ordinary render projection");
                    Check(editor.Select(group.Id)&&editor.SelectedId==group.Id&&
                        session.Revision==revision,"selecting Group changed engineering Revision");
                    Check(editor.Undo().Success&&session.Revision==revision+1&&
                        editor.SelectedId==null&&session.Find(group.Id)==null&&
                        editor.Scene.Count==2,"Undo Group retained stale selection or projection");
                    Check(editor.Redo().Success&&session.Revision==revision+2&&
                        ReferenceEquals(session.Find(group.Id),group)&&
                        editor.Scene.Count==2&&editor.SelectedId==null&&
                        ReferenceEquals(session.Find(a.Id),a)&&ReferenceEquals(session.Find(b.Id),b),
                        "Redo Group changed canonical object identity");
                    session.SetLocked(group,true);
                    Check(editor.Scene.All(item=>!item.IsSelectable),
                        "Group projection bypassed ancestor lock");
                    session.SetLocked(group,false);
                    session.SetVisibility(group,false,true);
                    Check(editor.Scene.Count==0,"hidden Group leaked editing descendants");
                }
            }
            finally{session.Close(true);}
        }
    }
}
