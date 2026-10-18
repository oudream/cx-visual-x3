using System;
using System.IO;
using CxVisual.Mechanism.Core;

public static class H02QuickStart
{
    public static void Main()
    {
        string path=Path.Combine(Path.GetTempPath(),"cx-h02-example-"+Guid.NewGuid().ToString("N")+".cxproj");
        var project=MEMEngineering.Create("事务与寻址");var page=project.AddPage("操作页");
        var left=project.AddGroup(page.RootContent,"工位一");var right=project.AddGroup(page.RootContent,"工位二");var lamp=project.AddRectangle(left,"灯",20,30);
        var snapshot=project.GetSnapshot();var address=project.GetAddress(lamp);
        using(var draft=project.BeginEdit("整理工位"))
        {
            draft.Rename(lamp,"运行状态");draft.Reparent(lamp,right);draft.Move(lamp,40,50);
            if(!draft.Preview().IsValid)throw new InvalidOperationException("Preview rejected");
            if(lamp.Name!="灯"||lamp.Parent!=left)throw new InvalidOperationException("Draft changed live definition");
            draft.Commit().RequireValue();
        }
        if(snapshot.Find(lamp.Id).Name!="灯"||project.Resolve(address)!=lamp)throw new InvalidOperationException("Snapshot/address identity failed");
        project.Undo().RequireValue();project.Redo().RequireValue();project.SaveAs(path);project.Close();
        var reopened=MEMEngineering.Open(path);var read=reopened.Find(lamp.Id);
        if(read.Name!="运行状态"||read.Parent.Id!=right.Id)throw new InvalidOperationException("Draft did not survive file roundtrip");
        Console.WriteLine("PASS H02QuickStart revision="+reopened.Revision+" object="+reopened.GetAddress(read));reopened.Close();File.Delete(path);
    }
}
