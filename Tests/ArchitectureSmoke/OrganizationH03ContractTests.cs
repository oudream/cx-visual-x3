using System;
using System.IO;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationH03ContractTests
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("H03 contract: "+message);}
        private static void Reject(Action action){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,"expected rejection");}
        public static void Run()
        {
            string path=Path.Combine(Path.GetTempPath(),"cx-h03-contract-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var s=MEMEngineering.Create("contracts");var p=s.AddPage("P");var motor=s.AddContent(s.Project,"Motor");
                s.DefineProperty(motor,new MPMPropertyDescriptor("speed",MPMValueKind.Number,true,true,MPMBinding.Literal(MPMValue.Number(100)),0,1000));
                var a=s.PlaceContent(p.RootContent,motor,"A");var b=s.PlaceContent(p.RootContent,motor,"B");s.SetOverride(a,"speed",MPMBinding.Literal(MPMValue.Number(200)));
                Check(s.ResolveParameter(a,"speed").Value.AsNumber()==200&&s.ResolveParameter(b,"speed").Value.AsNumber()==100,"instance override changed definition/other instance");
                Reject(()=>s.SetOverride(a,"speed",MPMBinding.Literal(MPMValue.String("wrong"))));Reject(()=>s.SetOverride(a,"speed",MPMBinding.Literal(MPMValue.Number(1001))));
                s.ClearOverride(a,"speed");Check(s.ResolveParameter(a,"speed").Value.AsNumber()==100,"clear override did not restore default");
                var source=s.AddContent(motor,"source");var sink=s.AddContent(motor,"sink");
                var output=s.DefinePort(source,"out",MPCPortDirection.Output,"core.number",MPCPortKind.Data,MPCPortMultiplicity.Many,new MTUContractReference("mm","world","utc"));
                var input=s.DefinePort(sink,"in",MPCPortDirection.Input,"core.number",MPCPortKind.Data,MPCPortMultiplicity.One,new MTUContractReference("mm","world","utc"));
                var wire=s.Connect(motor,s.Endpoint(output),s.Endpoint(input));
                Reject(()=>s.Connect(motor,s.Endpoint(output),s.Endpoint(input)));Reject(()=>s.Connect(motor,s.Endpoint(input),s.Endpoint(output)));Reject(()=>s.RemovePort(input));
                Reject(()=>s.ExposePort(motor,"occupied input",s.Endpoint(input)));
                var boundary=s.DefinePort(sink,"command",MPCPortDirection.Input,"core.number",MPCPortKind.Data,MPCPortMultiplicity.One,new MTUContractReference("mm","world","utc"));
                var publicInput=s.ExposePort(motor,"input",s.Endpoint(boundary));var publicOutput=s.ExposePort(motor,"output",s.Endpoint(output));
                var cross=s.Connect(p.RootContent,s.Endpoint(a,"output"),s.Endpoint(b,"input"));
                Reject(()=>s.Remove(a));Reject(()=>s.Unexpose(motor,"input",MRCPublicMappingKind.Port));
                var meter=s.AddContent(motor,"wrong unit");var incompatible=s.DefinePort(meter,"in",MPCPortDirection.Input,"core.number",MPCPortKind.Data,MPCPortMultiplicity.One,new MTUContractReference("m","world","utc"));
                Reject(()=>s.Connect(motor,s.Endpoint(output),s.Endpoint(incompatible)));
                s.SetPlacement(a,new MRCTransform(1,0,0,1,25,35));s.SetOverride(a,"speed",MPMBinding.Literal(MPMValue.Number(250)));
                s.SaveAs(path);s.Close();var opened=MEMEngineering.Open(path);var again=opened.FindReference(a.Id);
                Check(opened.ResolveParameter(again,"speed").Value.AsNumber()==250&&again.Placement.X==25,"instance fields did not survive file roundtrip");
                Check(opened.FindConnection(cross.Id)!=null&&opened.FindPort(publicInput.Id)!=null&&opened.FindPort(publicOutput.Id)!=null&&opened.Validate().IsValid,"public ports/mappings/connections/index roundtrip");
                opened.Disconnect(opened.FindConnection(cross.Id));opened.Remove(again);opened.Undo().RequireValue();Check(opened.FindReference(a.Id)!=null,"reference restore lost contracts");opened.Close(true);
                Advanced();
                Console.WriteLine("PASS H03 real double-instance parameter isolation, public ports/mappings, directional/unit/cardinality protection, placement and file roundtrip");
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        private static void Advanced()
        {
            string path=Path.Combine(Path.GetTempPath(),"cx-h03-values-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var s=MEMEngineering.Create("declarations");var page=s.AddPage("P");var symbol=s.AddSymbol("panel");var rectangle=s.AddRectangle(symbol.RootGroup,"R",0,0);
                s.DefineProperty(symbol,new MPMPropertyDescriptor("width",MPMValueKind.Number,true,true,MPMBinding.Literal(MPMValue.Number(120))));
                s.DefineProperty(symbol,new MPMPropertyDescriptor("text",MPMValueKind.String,false,true,MPMBinding.Literal(MPMValue.String("A\0B\n中文"))));
                s.DefineProperty(symbol,new MPMPropertyDescriptor("secret",MPMValueKind.Integer,false,false,MPMBinding.Literal(MPMValue.Integer(long.MaxValue))));
                s.ExposeProperty(symbol,"width",s.GetAddress(rectangle),"width");var reference=s.PlaceSymbol(page.RootContent,symbol,"one");s.SetOverride(reference,"width",MPMBinding.Literal(MPMValue.Number(250)));
                Check(s.ResolveMappedBinding(reference,rectangle.Id,"width").Value.AsNumber()==250&&rectangle.Width==120,"public mapping mutated shared definition");
                Reject(()=>s.SetOverride(reference,"secret",MPMBinding.Literal(MPMValue.Integer(1))));
                s.DefineProperty(page.RootContent,new MPMPropertyDescriptor("input",MPMValueKind.Number));
                Reject(()=>s.SetProperty(page.RootContent,"input",MPMBinding.Member(s.GetAddress(symbol,new[]{reference.Id}),"secret")));
                Reject(()=>s.SetProperty(page.RootContent,"input",MPMBinding.Member(s.GetAddress(rectangle),"width")));
                s.SetBinding(rectangle,"width",MPMBinding.Member(s.GetAddress(symbol),"width"));Reject(()=>s.SetProperty(symbol,"width",MPMBinding.Member(s.GetAddress(rectangle),"width")));
                var payload=new byte[]{0,1,255,40};s.SetAspect(symbol,new MPMAspect(MPMAspectKind.Logic,"demo.capability",1,payload));payload[0]=99;
                Check(symbol.Aspects[0].Payload[0]==0&&!s.ValidateForRun(page).IsValid&&s.ValidateForRun(page,new[]{"demo.capability"}).IsValid,"aspect byte ownership/run requirement");
                var snapshot=s.GetSnapshot();s.SetOverride(reference,"width",MPMBinding.Literal(MPMValue.Number(300)));
                Check(snapshot.Find(page.RootContent.Id).References[0].Overrides["width"].Value.AsNumber()==250,"reference snapshot shared override dictionary");
                s.SaveAs(path);s.Close();var opened=MEMEngineering.Open(path);var definition=(MEMSymbol)opened.Find(symbol.Id);
                Check(opened.FindProperty(definition,"text").Binding.Value.AsString()=="A\0B\n中文"&&opened.FindProperty(definition,"secret").Binding.Value.AsInteger()==long.MaxValue,"literal precision/text roundtrip");
                Check(definition.Aspects[0].Payload[2]==255&&definition.PublicMappings.Count==1&&opened.Validate().IsValid,"aspect/property mapping roundtrip");opened.Close();
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
