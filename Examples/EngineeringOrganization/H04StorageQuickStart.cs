using System;
using System.IO;
using System.Threading;
using CxVisual.Mechanism.Core;

internal static class H04StorageQuickStart
{
    public static int Main()
    {
        string directory=Path.Combine(Path.GetTempPath(),"cx-h04-example-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string path=Path.Combine(directory,"inspection.cxproj"),assetFile=Path.Combine(directory,"calibration.bin");File.WriteAllBytes(assetFile,new byte[]{0,1,255});
            var project=MEMEngineering.Create("Inspection");var content=project.AddContent(project.Project,"Background service");
            var asset=project.AddAsset("Calibration",assetFile);project.DefineProperty(content,new MPMPropertyDescriptor("Calibration",MPMValueKind.Resource,true,true,MPMBinding.Resource(asset.Id)));
            project.SaveAsAsync(path,CancellationToken.None).GetAwaiter().GetResult();project.Close();
            var first=MEMEngineering.Open(path);var second=MEMEngineering.Open(path);
            first.Rename(first.Find(content.Id),"Version A");first.Save();second.Rename(second.Find(content.Id),"Unsaved B");
            try{second.Save();return 1;}catch(MEMStoreConflictException){if(!second.IsDirty)return 2;}
            second.Close(MEMCloseMode.Discard);first.Close(MEMCloseMode.RequireSaved);File.Delete(assetFile);
            using(var moved=MEMEngineering.Open(path))using(var bytes=moved.OpenAsset(asset.Id))
            {if(bytes.Length!=3)return 3;Console.WriteLine("PASS H04StorageQuickStart: packaged asset, async save, two-session conflict and close policies");}
            return 0;
        }
        finally{Directory.Delete(directory,true);}
    }
}
