using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationH04Tests
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("H04: "+message);}
        private static void Reject(Action action){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(InvalidDataException){rejected=true;}catch(IOException){rejected=true;}Check(rejected,"expected rejection");}
        private sealed class PausedStore : IMEMProjectStore
        {
            internal readonly ManualResetEventSlim Captured=new ManualResetEventSlim(),Resume=new ManualResetEventSlim();
            internal bool Pause;
            public MEMEngineeringSession Load(string path){return MEMFileProjectStore.Default.Load(path);}
            public MEMSaveResult Save(MEMSaveSnapshot snapshot,string path,string expectedToken,CancellationToken cancellation)
            {
                if(Pause){Captured.Set();if(!Resume.Wait(10000))throw new TimeoutException("save barrier");}
                return MEMFileProjectStore.Default.Save(snapshot,path,expectedToken,cancellation);
            }
        }
        public static void Run()
        {
            string directory=Path.Combine(Path.GetTempPath(),"cx-h04-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string path=Path.Combine(directory,"project.cxproj");
            try
            {
                var s=MEMEngineering.Create("CAS");var page=s.AddPage("P");s.SaveAs(path);string first=s.StoreToken;
                var b=MEMEngineering.Open(path);s.Rename(page,"A saved");s.Save();b.Rename(b.Find(page.Id),"B unsaved");int revision=b.Revision;
                Reject(()=>b.Save());Check(b.IsDirty&&b.Revision==revision&&b.Location==path&&b.StoreToken==first,"conflict changed editing facts");
                using(var read=MEMEngineering.Open(path)){Check(read.Find(page.Id).Name=="A saved","stale writer overwrote file");}
                b.Close(true);s.Close();
                var store=new PausedStore();var concurrent=MEMEngineering.Open(path,store);store.Pause=true;
                concurrent.Rename(concurrent.Find(page.Id),"captured");int capturedRevision=concurrent.Revision;
                var saving=concurrent.SaveAsync(CancellationToken.None);Check(store.Captured.Wait(5000),"snapshot save did not start");
                concurrent.Rename(concurrent.Find(page.Id),"later edit");Reject(()=>concurrent.Close(true));Reject(()=>concurrent.Save());
                store.Resume.Set();saving.GetAwaiter().GetResult();Check(concurrent.IsDirty&&concurrent.LastSaveResult.SavedRevision==capturedRevision,"saving old snapshot marked new edit clean");
                using(var read=MEMEngineering.Open(path)){Check(read.Find(page.Id).Name=="captured","IO serialized mutable live state");}
                store.Pause=false;concurrent.Save();Check(!concurrent.IsDirty,"subsequent save failed");
                var before=File.ReadAllBytes(path);using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{concurrent.SaveAsync(cancel.Token).GetAwaiter().GetResult();throw new InvalidOperationException("cancel ignored");}catch(OperationCanceledException){}}
                Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(path)),"cancel changed existing file");
                using(var occupied=new FileStream(path+".cx-lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){concurrent.Rename(concurrent.Find(page.Id),"lock rejected");Reject(()=>concurrent.Save());}
                Check(concurrent.IsDirty,"locked save cleared dirty");concurrent.Save();concurrent.Close();
                AssetsAndUnknown(directory);
                AccessAndStreams(directory);
                Console.WriteLine("PASS H04 real two-session store CAS, fixed-revision concurrent save, cancellation, writer lock and dirty-state lifecycle");
            }
            finally{Directory.Delete(directory,true);}
        }
        private sealed class Policy : IMEMAccessPolicy
        {
            internal MEMAccessOperation? Denied;internal Action OnCheck;
            public bool IsAllowed(MEMAccessRequest request){if(OnCheck!=null)OnCheck();return request.Operation!=Denied;}
        }
        private static void AccessAndStreams(string directory)
        {
            var policy=new Policy();var s=MEMEngineering.Create("guarded",policy:policy);var page=s.AddPage("P");int revision=s.Revision;
            policy.Denied=MEMAccessOperation.Edit;try{s.Rename(page,"denied");throw new Exception("unauthorized edit accepted");}catch(UnauthorizedAccessException){}
            Check(s.Revision==revision&&page.Name=="P","denied edit changed state");
            policy.Denied=MEMAccessOperation.Save;string path=Path.Combine(directory,"denied.cxproj");try{s.SaveAs(path);throw new Exception("unauthorized save accepted");}catch(UnauthorizedAccessException){}
            Check(!File.Exists(path)&&s.Location==null,"denied save leaked bytes");
            policy.Denied=MEMAccessOperation.Export;try{s.CaptureSaveSnapshot();throw new Exception("unauthorized export accepted");}catch(UnauthorizedAccessException){}
            policy.Denied=MEMAccessOperation.Read;try{s.GetSnapshot();throw new Exception("unauthorized read accepted");}catch(UnauthorizedAccessException){}
            policy.Denied=null;policy.OnCheck=()=>s.Close(true);Reject(()=>s.GetSnapshot());policy.OnCheck=null;
            Check(!s.IsClosed,"policy callback closed live session");
            using(var stream=new MemoryStream())
            {
                MEMFileProjectStore.Default.WritePackage(stream,s.CaptureSaveSnapshot(),CancellationToken.None);Check(stream.CanWrite,"writer closed caller stream");stream.Position=0;
                var loaded=MEMFileProjectStore.Default.ReadPackage(stream);Check(stream.CanRead&&loaded.Find(page.Id)!=null&&loaded.Location==null&&loaded.IsDirty,"stream read ownership/identity");loaded.Close(true);
            }
            s.SaveAs(path);s.Close(MEMCloseMode.RequireSaved);
            policy.Denied=MEMAccessOperation.Open;try{MEMEngineering.Open(path,policy:policy);throw new Exception("unauthorized open accepted");}catch(UnauthorizedAccessException){}
        }
        private static void AssetsAndUnknown(string directory)
        {
            string file=Path.Combine(directory,"asset-project.cxproj"),input=Path.Combine(directory,"calibration.bin");File.WriteAllBytes(input,new byte[]{0,2,255,12});
            var s=MEMEngineering.Create("assets");var content=s.AddContent(s.Project,"background");var asset=s.AddAsset("calibration",input,"application/octet-stream");
            s.DefineProperty(content,new MPMPropertyDescriptor("Calibration",MPMValueKind.Resource,true,true,MPMBinding.Resource(asset.Id)));
            s.SetAspect(content,new MPMAspect(MPMAspectKind.Logic,"missing.plugin",37,new byte[]{0,255,0,99}));
            Reject(()=>s.RemoveAsset(asset.Id));s.SaveAs(file);s.Close();File.Delete(input);
            string moved=Path.Combine(directory,"moved");Directory.CreateDirectory(moved);string relocated=Path.Combine(moved,"relocated.cxproj");File.Move(file,relocated);
            var opened=MEMEngineering.Open(relocated);var metadata=opened.Project.FindResource(asset.Id);
            Check(metadata!=null&&metadata.Id==asset.Id&&opened.Assets.Count==1,"asset index lost after package relocation");
            using(var bytes=opened.OpenAsset(asset.Id)){Check(bytes.ReadByte()==0&&bytes.ReadByte()==2&&bytes.ReadByte()==255,"asset bytes changed");}
            var definition=(MEMContent)opened.Find(content.Id);Check(definition.Aspects[0].SchemaVersion==37&&definition.Aspects[0].Payload[3]==99&&!opened.ValidateForRun(definition).IsValid,"unknown provider payload was lost or allowed to run");
            opened.Save();opened.Close();
            using(var again=MEMEngineering.Open(relocated)){Check(again.Assets[0].ContentHash==asset.ContentHash,"asset hash roundtrip");}
            using(var bad=new MemoryStream())
            {
                byte[] original=File.ReadAllBytes(relocated);bad.Write(original,0,original.Length);bad.Position=0;
                using(var zip=new ZipArchive(bad,ZipArchiveMode.Update,true))
                {var entry=zip.GetEntry("assets/"+asset.Id.ToString("N"));entry.Delete();using(var output=zip.CreateEntry("assets/"+asset.Id.ToString("N")).Open())output.Write(new byte[]{0,2,254,12},0,4);}
                bad.Position=0;Reject(()=>MEMFileProjectStore.Default.ReadPackage(bad));Check(bad.CanRead,"rejected package closed caller stream");
            }
        }
    }
}
