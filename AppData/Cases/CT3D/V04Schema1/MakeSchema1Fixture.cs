using System;
using System.IO;
using System.Threading;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Core;
using CxVisual.Professional.CT3D;

internal static class MakeSchema1Fixture
{
    public static void Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]);
        if(Directory.Exists(root))throw new IOException("Fixture directory already exists");
        Directory.CreateDirectory(root);
        const int n=12;var raw=new byte[n*n*n*2];
        for(int z=5;z<=6;z++)for(int y=5;y<=6;y++)for(int x=5;x<=6;x++)
        {int i=(x+y*n+z*n*n)*2;raw[i]=0xa4;raw[i+1]=0x06;}
        File.WriteAllBytes(Path.Combine(root,"source.raw"),raw);
        var info=new FVORawVolumeInfo(n,n,n,1,1,1,FVORawVoxelFormat.UInt16,"source.raw");
        var frame=new FVOVolumeSpatialFrame(1,1,1,20,30,40,new double[]{1,0,0,0,1,0,0,0,1},true);
        var source=CT3DAssetReference.Capture(root,"source.raw",info,frame,7,CancellationToken.None);
        var analysis=CT3DAnalysisCase.Analyze(root,source,1000,100,
            CT3DAnalysisCase.SelectTopVoxelFace,CancellationToken.None,0.01,1000);
        using(var engineering=MEMEngineering.Create("V04 Schema1 compatibility fixture"))
        {
            var owner=engineering.AddPage("CT3D").RootContent;
            CT3DAnalysisPersistence.Commit(engineering,owner,engineering.Revision,root,analysis,CancellationToken.None);
            engineering.SaveAs(Path.Combine(root,"schema1.cxproj"));
        }
        Console.WriteLine("Created by unchanged CT3D schema1 DLL: "+root);
    }
}
