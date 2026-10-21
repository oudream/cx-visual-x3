using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationH02Tests
    {
        private static void Check(bool value,string message) { if(!value)throw new InvalidOperationException("H02: "+message); }
        private static void Reject(Action action) { bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,"expected rejection"); }
        public static void Run()
        {
            Drafts(); SnapshotsAndAddresses(); ReorderAndFile(); HistoryFault();
            Console.WriteLine("PASS H02 real draft preview/commit/cancel/conflict, immutable snapshot concurrency, handles/addresses and history fault isolation");
        }
        private static void Drafts()
        {
            var s=MEMEngineering.Create("drafts");var page=s.AddPage("P");var left=s.AddGroup(page.RootContent,"L");var right=s.AddGroup(page.RootContent,"R");var leaf=s.AddRectangle(left,"old",5,6);
            int revision=s.Revision;int notices=0;MTCChangeSet observed=null;using(s.Watch(c=>{notices++;observed=c;}))
            {
                using(var a=s.BeginEdit("organize",revision))using(var b=s.BeginEdit("competing",revision))
                {
                    a.Rename(leaf,"new");a.Reparent(leaf,right,0);a.Move(leaf,30,40);a.Resize(leaf,20,10);
                    Check(a.Preview().IsValid&&leaf.Name=="old"&&leaf.Parent==left&&s.Revision==revision&&notices==0,"preview mutated published state");
                    b.Rename(leaf,"must not win");var result=a.Commit();Check(result.Success&&leaf.Parent==right&&leaf.X==30&&leaf.Width==20&&s.Revision==revision+1,"draft commit not atomic");
                    Check(!b.Commit().Success&&leaf.Name=="new","stale draft overwrote current value");
                    Check(observed.Operations.Count==4&&observed.Operations.All(o=>o.TargetId==leaf.Id),"committed operation metadata missing");
                    Reject(()=>a.Rename(leaf,"expired"));
                }
                Check(notices==1&&s.Undo().Success&&leaf.Name=="old"&&leaf.Parent==left&&leaf.X==5,"draft undo");
                Check(s.Redo().Success&&leaf.Parent==right,"draft redo");
                int count=s.Count;revision=s.Revision;
                using(var cancelled=s.BeginEdit("cancel")){cancelled.AddRectangle(right,"discarded",0,0);Check(cancelled.Preview().IsValid&&s.Count==count,"preview add indexed draft object");}
                Check(s.Revision==revision&&s.Count==count,"Dispose committed draft");
                using(var cancelled=s.BeginEdit("explicit cancel")){cancelled.Rename(leaf,"cancelled");cancelled.Cancel();Reject(()=>cancelled.Commit());Check(leaf.Name=="new","explicit cancel mutated project");}
                using(var failed=s.BeginEdit("bad")){failed.Rename(leaf,"partial");failed.Reparent(right,leaf);Check(!failed.Preview().IsValid&&!failed.Commit().Success&&leaf.Name=="new","failed draft leaked partial state");}
                using(var create=s.BeginEdit("new page")){var p=create.AddPage("P2");create.AddRectangle(p,"draft R",1,2);Check(create.Preview().IsValid&&s.Project.Pages.Count==1,"draft roots leaked");Check(create.Commit().Success&&s.Project.Pages.Count==2,"draft created roots twice");}
            }
            int delivered=0;
            using(s.Watch(c=>{throw new ApplicationException("observer error");}))
            using(s.Watch(c=>{delivered++;Reject(()=>s.Rename(leaf,"reentrant"));}))s.Rename(leaf,"observed");
            Check(delivered==1&&leaf.Name=="observed"&&s.LastObserverError!=null,"observer isolation/reentry");
            s.Rename(leaf,"after notification");Check(delivered==1,"disposed observer was delivered");
            s.Close(true);
        }
        private static void SnapshotsAndAddresses()
        {
            var s=MEMEngineering.Create("snapshot");var p=s.AddPage("P");var r=s.AddRectangle(p,"R",1,2);var snapshot=s.GetSnapshot();var old=snapshot.Find(r.Id);
            var handle=s.GetHandle(r.Id);var address=s.GetAddress(r);Check(s.Read(handle)==r&&s.Resolve(MRIObjectAddress.Parse(address.ToString()))==r,"address/handle roundtrip");
            s.Rename(r,"renamed");Check(old.Name=="R"&&snapshot.Revision<s.Revision&&s.Resolve(address)==r,"snapshot changed with live node/name affected address");
            Check(s.FindByType("core.rectangle").Count==1&&s.FindByName("renamed",p.RootContent).Single()==r,"explicit search scope/type/name");
            bool immutable=false;try{((IList<Guid>)snapshot.Find(p.Id).ChildIds)[0]=Guid.NewGuid();}catch(NotSupportedException){immutable=true;}Check(immutable,"snapshot child array exposed mutation");
            var foreign=MEMEngineering.Create("foreign");Reject(()=>foreign.Read(handle));foreign.Close(true);
            var shared=s.AddContent(s.Project,"shared");var a=s.AddRectangle(shared,"inside",0,0);var reference=s.PlaceContent(p.RootContent,shared,"use");
            var instance=s.GetAddress(a,new[]{reference.Id});Check(s.Resolve(instance)==a&&instance.InstancePath.Count==1,"instance address resolution");
            Reject(()=>s.GetAddress(r,new[]{reference.Id}));
            var writer=Task.Run(()=>{for(int i=0;i<100;i++)s.Move(r,i,2);});
            for(int i=0;i<100;i++){var copy=s.GetSnapshot();foreach(var node in copy.Nodes)if(node.ParentId.HasValue)Check(copy.Find(node.ParentId.Value)!=null,"snapshot contains detached ownership");}
            writer.GetAwaiter().GetResult();Check(old.Visual.X==1&&old.Name=="R","parallel snapshots share mutable live payload");
            s.Close(true);Reject(()=>s.Read(handle));
        }
        private static void ReorderAndFile()
        {
            string path=Path.Combine(Path.GetTempPath(),"cx-h02-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var s=MEMEngineering.Create("reorder");var p=s.AddPage("P");var a=s.AddRectangle(p,"A",1,2);var b=s.AddRectangle(p,"B",3,4);
                Check(a.NextSibling==b&&b.PreviousSibling==a&&a.OwnerPage==p&&a.OwnerProject==s.Project,"read-only navigation");
                s.Reparent(b,p.RootContent,0);Check(p.RootContent.FirstChild==b&&p.RootContent.LastChild==a,"same-parent order");s.Undo();Check(p.RootContent.FirstChild==a,"order undo");
                s.SaveAs(path);Check(!s.IsDirty,"saved state");s.Redo();Check(s.IsDirty&&p.RootContent.FirstChild==b,"redo dirty");s.Save();s.Close();
                var open=MEMEngineering.Open(path);Check(open.Project.Pages[0].RootContent.FirstChild.Id==b.Id,"history state save/reopen");open.Close();
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        private static void HistoryFault()
        {
            var s=MEMEngineering.Create("fault");var p=s.AddPage("P");s.Rename(p,"P2");
            // Fault injection into a real inverse journal; no fake engine or production bypass API.
            var history=(IList)typeof(MEMEngineeringSession).GetField("_history",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
            var batch=(IList)history[history.Count-1];object mutation=batch[0];
            mutation.GetType().GetField("Inverse",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mutation,(Action)(()=>{throw new InvalidOperationException("injected inverse failure");}));
            Reject(()=>s.Undo());Check(!s.CanUndo&&!s.CanRedo&&s.Validate().IsValid,"history fault did not contain corruption");Reject(()=>s.Rename(p,"untrusted"));s.Close(true);
        }
    }
}
