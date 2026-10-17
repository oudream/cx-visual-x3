using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationAdversarialTests
    {
        private static void Check(bool value,string message) { if(!value)throw new InvalidOperationException("H01 adversarial: "+message); }
        private static void Reject(Action action) { bool fail=false;try{action();}catch(Exception ex){if(!(ex is InvalidOperationException||ex is ArgumentException||ex is System.Xml.XmlException||ex is InvalidDataException||ex is IOException))throw;fail=true;}Check(fail,"rejection expected"); }
        public static void Run()
        {
            IdentityAndTransactionLifetime(); ConcurrencyAndReferences(); VisualContracts(); Files();
            Console.WriteLine("PASS H01 adversarial identity, transaction lifetime, concurrency, reference cycles, malformed files and save failures");
        }
        private static void IdentityAndTransactionLifetime()
        {
            FOIStableId parsed;var identity=FOIStableId.NewId();
            Check(FOIStableId.TryParse(identity.ToString(),out parsed)&&parsed.Equals(identity),"identity roundtrip");
            Check(!FOIStableId.TryParse(Guid.Empty.ToString(),out parsed),"empty identity accepted");
            Reject(()=>default(FOIStableId).ToString());
            var s=MEMEngineering.Create("lifetime");var page=s.AddPage("P");MTCEditSession retained=null;
            s.Edit("capture",e=>{retained=e;});Reject(()=>retained.Rename(page,"late"));
            var scoped=s.Edit("foreign thread",e=>{
                Exception error=null;var thread=new Thread(()=>{try{e.Rename(page,"foreign");}catch(Exception ex){error=ex;}});thread.Start();Check(thread.Join(3000),"worker hung");
                Check(error is InvalidOperationException&&page.Name=="P","foreign thread modified draft");
            });Check(scoped.Success,"valid thread isolation");
            int before=s.Revision;
            try{s.Edit<bool>("throw",e=>{e.Rename(page,"partial");throw new ApplicationException("user failure");});throw new Exception("not thrown");}catch(ApplicationException){}
            Check(s.Revision==before&&page.Name=="P","callback exception rollback");
            var deep=s.Edit("depth",e=>{MEMNode parent=page.RootContent;for(int i=0;i<260;i++)parent=e.AddGroup(parent,"G");});
            Check(!deep.Success&&s.Count==3,"depth budget rollback");
            int revision=s.Revision;var conflict=s.Edit("stale",revision-1,e=>e.AddContent(s.Project,"must not run"));
            Check(!conflict.Success&&conflict.ErrorCode=="org.version.conflict"&&s.Count==3,"stale revision changed tree");
            Check(!typeof(MRCGroup).IsSubclassOf(typeof(MRCElement)),"group became a visual leaf");
            var fields=typeof(MEMNode).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);
            Check(fields.All(f=>!typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType)||f.FieldType==typeof(string)),"MEMNode stores optional containers");
            Reject(()=>s.Add(page.RootContent,new CustomRectangle()));
            s.Close(true);
        }
        private sealed class CustomRectangle : MRCRectangle
        { public CustomRectangle():base(Guid.NewGuid(),"custom",0,0){} public int UnknownPayload=42; }
        private static void VisualContracts()
        {
            var s=MEMEngineering.Create("visual");var page=s.AddPage("P");var g=s.AddGroup(page.RootContent,"G");var r=s.AddRectangle(g,"R",5,6,10,20);
            s.Rotate(g,90);s.Scale(g,-2,3);double x,y;r.TransformPoint(5,10,out x,out y);Check(r.HitTest(x,y),"negative scale inverse");
            s.Resize(g,240,160);Check(g.ScaleX==2&&g.ScaleY==2&&r.X==5&&r.Width==10,"group resize rewrote descendants");
            s.SetBinding(r,"visible",new MPMBinding("machine","running"));int revision=s.Revision;
            s.SetBinding(r,"visible",new MPMBinding("machine","running"));s.SetVisibility(r,true,true);s.Rotate(g,450);
            Check(s.Revision==revision,"equivalent visual edits advanced revision");
            var metadata=s.GetResourceEntry(page.Id);s.Rename(page,"P2");
            Check(metadata.Id==page.Id&&metadata.Name=="P"&&s.GetResourceEntry(page.Id).Name=="P2"&&s.Project.FindResource(page.Id)==page,"metadata projection owns resource");
            s.SetVisibility(g,false,true);Check(!r.HitTest(x,y),"hidden ancestor hit");s.Undo();s.SetVisibility(r,true,false);Check(!r.HitTest(x,y),"nonselectable hit");
            s.Close(true);
        }
        private static void ConcurrencyAndReferences()
        {
            var s=MEMEngineering.Create("concurrency");s.AddPage("P");int notices=0,last=1;
            using(s.Watch(c=>{Check(c.BeforeRevision==last,"notification order");last=c.AfterRevision;Interlocked.Increment(ref notices);}))
            {
                var tasks=Enumerable.Range(0,4).Select(worker=>Task.Run(()=>{
                    for(int n=0;n<40;n++)while(true){int rev=s.Revision;var result=s.Edit("parallel",rev,e=>e.AddContent(s.Project,"C"));if(result.Success)break;Check(result.ErrorCode=="org.version.conflict","unexpected conflict");}
                })).ToArray();Check(Task.WaitAll(tasks,15000),"concurrency timeout");
            }
            Check(notices==160&&s.Project.Contents.Count==160&&s.Validate().IsValid,"concurrent state/index/notification");
            var a=s.AddContent(s.Project,"A");var b=s.AddContent(s.Project,"B");s.PlaceContent(a,b,"B1");
            Reject(()=>s.PlaceContent(b,a,"cycle"));Reject(()=>s.Reparent(a,b));
            Check(s.Validate().IsValid,"cycle rejection damaged project");
            int rev2=s.Revision;s.Watch(c=>{throw new ApplicationException("observer");});s.Rename(a,"A2");
            Check(a.Name=="A2"&&s.Revision==rev2+1&&s.LastObserverError!=null,"observer undid commit");s.Close(true);
        }
        private static void Files()
        {
            string directory=Path.Combine(Path.GetTempPath(),"cx-h01-faults-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"p.cxproj"),bad=Path.Combine(directory,"bad.xml");
            try
            {
                var s=MEMEngineering.Create("files");var page=s.AddPage("P");var lamp=s.AddRectangle(page,"R",1,2);s.SaveAs(path);
                byte[] saved=File.ReadAllBytes(path);int revision=s.Revision;
                s.SetBinding(lamp,"visible",new MPMBinding("status","running"));s.Save();var copy=MEMEngineering.Open(path);
                Check(((MRCVisualItem)copy.Find(lamp.Id)).GetBinding("visible").MemberId=="running","binding roundtrip");copy.Close();
                saved=File.ReadAllBytes(path);revision=s.Revision;
                var cancelled=new CancellationTokenSource();cancelled.Cancel();bool cancelledSave=false;
                try{s.SaveAsync(cancelled.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelledSave=true;}
                Check(cancelledSave&&File.ReadAllBytes(path).SequenceEqual(saved),"cancelled save published");cancelled.Dispose();
                Task pending=null;var noIo=s.Edit("no IO",e=>{e.Rename(lamp,"partial");pending=s.SaveAsync(CancellationToken.None);});
                if(pending!=null)pending.GetAwaiter().GetResult();
                Check(!noIo.Success&&s.Revision==revision&&lamp.Name=="R"&&File.ReadAllBytes(path).SequenceEqual(saved),"async IO escaped edit");
                s.Rename(lamp,"dirty");Reject(()=>s.SaveAs(directory));Check(s.IsDirty&&s.Location==path&&File.ReadAllBytes(path).SequenceEqual(saved),"save failure lost disk/dirty state");
                Reject(()=>s.Close());Check(!s.IsClosed,"dirty close");
                var another=MEMEngineering.Create("other");another.AddPage("P");Reject(()=>another.SaveAs(path));
                Check(!Directory.GetFiles(directory,".cx-save-*.tmp").Any(),"temporary files leaked");
                string p=Guid.NewGuid().ToString(),c=Guid.NewGuid().ToString();
                string root="<CxVisualProject schema='2' revision='0'><Node id='"+p+"' kind='Project' name='P'>";
                foreach(var body in new[]{
                    "<Node id='"+c+"' kind='Content' name='C' future='lost'/>",
                    "<Node id='"+c+"' kind='Content' name='C'/><Node id='"+c+"' kind='Content' name='duplicate'/>",
                    "<Node id='"+c+"' kind='Group' name='wrong-root'/>",
                    "<Node id='"+c+"' kind='Content' name='C'><Node id='"+Guid.NewGuid()+"' kind='Element' name='R' x='NaN'/></Node>",
                    "<Node id='"+c+"' kind='Content' name='C'><Reference id='"+Guid.NewGuid()+"' kind='Content' name='missing' target='"+Guid.NewGuid()+"'/></Node>"})
                {File.WriteAllText(bad,root+body+"</Node></CxVisualProject>");Reject(()=>MEMEngineering.Open(bad));Check(s.Find(lamp.Id)==lamp,"bad load changed existing session");}
                File.WriteAllText(bad,"<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///not-read'>]><CxVisualProject schema='2' revision='0'>&x;</CxVisualProject>");Reject(()=>MEMEngineering.Open(bad));
                string zip=Path.Combine(directory,"bad.cxproj");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))archive.CreateEntry("../project.xml");Reject(()=>MEMEngineering.Open(zip));
                s.Close(true);another.Close(true);
            }
            finally
            {
                // Every file is from this exact freshly-created test directory; no recursive deletion or computed parent deletion.
                foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);
            }
        }
    }
}
