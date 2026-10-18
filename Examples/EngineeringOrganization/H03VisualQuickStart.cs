using System;
using System.IO;
using CxVisual.Mechanism.Core;

public static class H03VisualQuickStart
{
    public static void Main()
    {
        var project=MEMEngineering.Create("视觉组合");var page=project.AddPage("P");
        var a=project.AddRectangle(page,"A",20,30,40,50);var b=project.AddRectangle(page,"B",100,30,40,50);project.Rotate(a,25);
        var group=project.Group(new MRCVisualItem[]{a,b},"工位面板");project.Rotate(group,37);project.Scale(group,2,.5);
        double x,y;a.TransformPoint(0,0,out x,out y);project.Ungroup(group);double afterX,afterY;a.TransformPoint(0,0,out afterX,out afterY);
        if(Math.Abs(x-afterX)>1e-8||Math.Abs(y-afterY)>1e-8)throw new InvalidOperationException("Ungroup changed world geometry");
        string path=Path.Combine(Path.GetTempPath(),"cx-h03-visual-example-"+Guid.NewGuid().ToString("N")+".cxproj");project.SaveAs(path);project.Close();
        var loaded=MEMEngineering.Open(path);if(Math.Abs(((MRCVisualItem)loaded.Find(a.Id)).Shear)<1e-5)throw new InvalidOperationException("Affine shear lost");
        Console.WriteLine("PASS H03VisualQuickStart affine grouping/ungrouping and file roundtrip");loaded.Close();File.Delete(path);
    }
}
