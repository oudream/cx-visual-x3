using System;
using System.IO;
using System.Linq;
using CxVisual.Mechanism.Core;

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
    }
}
