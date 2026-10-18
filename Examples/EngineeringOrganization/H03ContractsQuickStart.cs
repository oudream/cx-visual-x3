using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Mechanism.Core;

internal static class H03ContractsQuickStart
{
    private sealed class MotorTypes : IMPMTypeProvider
    {
        public IEnumerable<MPMTypeDescriptor> DescribeTypes()
        {
            yield return new MPMTypeDescriptor("demo.motor",1,MEMNodeKind.Content,
                new[]{new MPMPropertyDescriptor("Speed",MPMValueKind.Number,true,true,MPMBinding.Literal(MPMValue.Number(100)),0,1000)},
                new[]{new MPCPortDescriptor("Result",MPCPortDirection.Output,"core.number",isPublic:true),
                    new MPCPortDescriptor("Command",MPCPortDirection.Input,"core.number",multiplicity:MPCPortMultiplicity.One,isPublic:true)});
        }
    }
    public static int Main()
    {
        string file=Path.Combine(Path.GetTempPath(),"cx-h03-example-"+Guid.NewGuid().ToString("N")+".cxproj");
        try
        {
            var project=MEMEngineering.Create("Two stations");project.RegisterTypes(new MotorTypes());
            var page=project.AddPage("Monitor");var motor=project.AddTypedContent(project.Project,"Motor","demo.motor");
            var a=project.PlaceContent(page.RootContent,motor,"Station A");var b=project.PlaceContent(page.RootContent,motor,"Station B");
            project.SetOverride(a,"Speed",MPMBinding.Literal(MPMValue.Number(200)));
            project.Connect(page.RootContent,project.Endpoint(a,"Result"),project.Endpoint(b,"Command"));
            using(var draft=project.BeginEdit("Adjust station"))
            {
                draft.SetPlacement(a,new MRCTransform(1,0,0,1,20,30));draft.Rename(a,"Station 1");
                if(!draft.Preview().IsValid)throw new InvalidOperationException("Invalid adjustment");draft.Commit().RequireValue();
            }
            project.SaveAs(file);project.Close();
            using(var opened=MEMEngineering.Open(file))
            {
                if(opened.ResolveParameter(opened.FindReference(a.Id),"Speed").Value.AsNumber()!=200||opened.ResolveParameter(opened.FindReference(b.Id),"Speed").Value.AsNumber()!=100||!opened.Validate().IsValid)throw new InvalidOperationException("Instance isolation/roundtrip failed");
                Console.WriteLine("PASS H03ContractsQuickStart: typed Motor, two instances, public connection, draft and real file roundtrip");
            }
            return 0;
        }
        finally{if(File.Exists(file))File.Delete(file);}
    }
}
