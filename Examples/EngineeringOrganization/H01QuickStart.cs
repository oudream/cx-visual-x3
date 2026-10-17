using System;
using System.IO;
using CxVisual.Mechanism.Core;

/// <summary>Compile against the real Core/Foundation.Core DLLs; no mock or reference-only assembly.</summary>
public static class H01QuickStart
{
    public static void Main(string[] args)
    {
        string path = args.Length == 0 ? Path.Combine(Path.GetTempPath(), "cx-h01-example-" + Guid.NewGuid().ToString("N") + ".cxproj") : args[0];
        var engineering = MEMEngineering.Create("检测工程");
        var page = engineering.AddPage("操作页");
        var content = engineering.AddContent(page.RootContent, "工位一");
        var lamp = engineering.AddRectangle(content, "运行灯", 20, 40, 30, 30);
        var other = engineering.AddRectangle(content, "备用灯", 70, 40);
        var symbol = engineering.AddSymbol("电机面板");
        engineering.AddRectangle(symbol.RootGroup, "面板外观", 0, 0);
        engineering.PlaceSymbol(page.RootContent, symbol, "电机一");
        engineering.SetBinding(lamp, "visible", new MPMBinding("machine.status", "running"));
        Guid lampId = lamp.Id, otherId = other.Id;
        engineering.SaveAs(path); engineering.Close();

        var reopened = MEMEngineering.Open(path);
        var edit = reopened.Edit("修改工位", e =>
        {
            e.Rename(reopened.Find(lampId), "运行状态");
            e.Move((MRCVisualItem)reopened.Find(lampId), 25, 45);
            e.Remove(reopened.Find(otherId));
        });
        if (!edit.Success) throw new InvalidOperationException(edit.ErrorCode + ": " + edit.Message);
        reopened.Save(); reopened.Close();
        var verified = MEMEngineering.Open(path);
        if (verified.Find(otherId) != null || verified.Find(lampId).Name != "运行状态" || !verified.Validate().IsValid) throw new InvalidOperationException("Engineering roundtrip failed");
        Console.WriteLine("PASS H01QuickStart nodes=" + verified.Count + " revision=" + verified.Revision + " path=" + path);
        verified.Close();
    }
}
