using System;
using System.IO;
using CxVisual.Mechanism.Core;

internal static class H05ReuseQuickStart
{
    public static int Main()
    {
        string directory=Path.Combine(Path.GetTempPath(),"cx-h05-example-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var source=MEMEngineering.Create("Source");var motor=source.AddContent(source.Project,"Motor");source.DefineProperty(motor,new MPMPropertyDescriptor("Speed",MPMValueKind.Number,false,true,MPMBinding.Literal(MPMValue.Number(100))));
            var cabinet=source.AddContent(source.Project,"Cabinet");var first=source.PlaceContent(cabinet,motor,"Station A");source.PlaceContent(cabinet,motor,"Station B");source.SetOverride(first,"Speed",MPMBinding.Literal(MPMValue.Number(200)));
            var target=MEMEngineering.Create("Target");var plan=target.PreviewImport(source,new[]{cabinet.Id},target.Project);
            if(!plan.IsValid)throw new InvalidOperationException(string.Join(";",plan.Issues));var result=target.Import(plan);
            if(target.ResolveParameter(target.FindReference(result.IdMap[first.Id]),"Speed").Value.AsNumber()!=200)return 1;
            target.Undo().RequireValue();target.Redo().RequireValue();
            string library=Path.Combine(directory,"library.cxproj");target.ExportPackage(result.RootIds,library);
            var fromTemplate=MTIEngineeringTemplates.Create(library,"New inspection project");
            if(fromTemplate.Project.Id==target.Project.Id||!fromTemplate.Validate().IsValid)return 2;
            fromTemplate.SaveAs(Path.Combine(directory,"new.cxproj"));fromTemplate.Close();target.Close(true);source.Close(true);
            Console.WriteLine("PASS H05ReuseQuickStart: preview/import/undo/redo, dependency closure, library export and new template identity");return 0;
        }
        finally{Directory.Delete(directory,true);}
    }
}
