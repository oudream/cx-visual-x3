using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MEMEngineeringSession
    {
        private readonly Guid _sessionIdentity=Guid.NewGuid();
        public MTCEditDraft BeginEdit(string intent) { lock(_gate){DemandOpen();return new MTCEditDraft(this,intent,Revision);} }
        public MTCEditDraft BeginEdit(string intent,int expectedRevision) { lock(_gate){DemandOpen();return new MTCEditDraft(this,intent,expectedRevision);} }
        internal MVEValidationReport PreviewDraft(int expectedRevision,Action<MTCEditSession> commands)
        {
            DemandAccess(MEMAccessOperation.Edit);
            lock(_gate)
            {
                DemandOpen();
                if(_editing||_callbackThread==Thread.CurrentThread.ManagedThreadId||_historyFaulted||Volatile.Read(ref _collectingTypes)!=0||Volatile.Read(ref _authorizing)!=0)throw new InvalidOperationException("org.preview.unavailable");
                if(expectedRevision!=Revision)return new MVEValidationReport(new List<string>{"org.version.conflict"});
                var edit=new MTCEditSession(this);var issues=new List<string>();_editing=true;
                try { commands(edit); }
                catch(ArgumentException ex){issues.Add(ex.Message);}
                catch(InvalidOperationException ex){issues.Add(ex.Message);}
                finally
                {
                    try{for(int i=edit.Changes.Count-1;i>=0;i--)edit.Changes[i].Inverse();}
                    catch{_historyFaulted=true;throw;}
                    finally{edit.Complete();_editing=false;}
                }
                return new MVEValidationReport(issues);
            }
        }
        public MEMProjectSnapshot GetSnapshot()
        {
            DemandAccess(MEMAccessOperation.Read);
            lock(_gate){DemandOpen();if(_editing)throw new InvalidOperationException("Snapshot requires a committed revision");return new MEMProjectSnapshot(Project,Revision);}
        }
        public MEMProjectSnapshot GetSnapshot(Guid rootId,bool includeReferences)
        {
            DemandAccess(MEMAccessOperation.Read);lock(_gate)
            {
                DemandOpen();if(_editing)throw new InvalidOperationException("Snapshot requires a committed revision");var root=Find(rootId);if(root==null)throw new InvalidOperationException("org.node.missing");
                var selected=new List<MEMNode>();var seen=new HashSet<Guid>();var pending=new Stack<MEMNode>();pending.Push(root);
                while(pending.Count!=0){var node=pending.Pop();if(!seen.Add(node.Id))continue;selected.Add(node);foreach(var child in node.Children)pending.Push(child);if(includeReferences)foreach(var reference in MRIReferenceList.View(node))pending.Push(Find(reference.TargetId));}
                return new MEMProjectSnapshot(Project,Revision,selected);
            }
        }
        public IReadOnlyList<MEMNode> FindByName(string name,MEMNode scope=null)
        {DemandAccess(MEMAccessOperation.Read);name=MEMNode.NormalizeName(name);lock(_gate){DemandOpen();scope=scope??Project;DemandNode(scope);return scope.DescendantsAndSelf().Where(n=>string.Equals(n.Name,name,StringComparison.Ordinal)).ToList().AsReadOnly();}}
        public IReadOnlyList<MEMNode> FindByType(string typeId,MEMNode scope=null)
        {DemandAccess(MEMAccessOperation.Read);if(string.IsNullOrWhiteSpace(typeId))throw new ArgumentException("TypeId required");lock(_gate){DemandOpen();scope=scope??Project;DemandNode(scope);return scope.DescendantsAndSelf().Where(n=>string.Equals(n.TypeId,typeId,StringComparison.Ordinal)).ToList().AsReadOnly();}}
        public MRIObjectHandle GetHandle(Guid id)
        {DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();if(Find(id)==null)throw new InvalidOperationException("org.node.missing");return new MRIObjectHandle(_sessionIdentity,Project.Id,id);}}
        public MEMNode Read(MRIObjectHandle handle)
        {DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();if(handle==null||handle.SessionId!=_sessionIdentity||handle.ProjectId!=Project.Id)throw new InvalidOperationException("org.handle.foreign");return Find(handle.ObjectId);}}
        public MRIObjectAddress GetAddress(MEMNode node,IEnumerable<Guid> instancePath=null)
        {
            DemandAccess(MEMAccessOperation.Read);
            lock(_gate){DemandOpen();DemandNode(node);var address=new MRIObjectAddress(Project.Id,node.ResourceRootId,node.Id,instancePath);if(Resolve(address)!=node)throw new InvalidOperationException("org.address.invalid");return address;}
        }
        public MEMNode Resolve(MRIObjectAddress address)
        {
            DemandAccess(MEMAccessOperation.Read);
            lock(_gate)
            {
                DemandOpen();if(address==null||address.ProjectId!=Project.Id)throw new InvalidOperationException("org.address.project_mismatch");
                var node=Find(address.ObjectId);if(node==null)return null;
                if(node.ResourceRootId!=address.ResourceId)throw new InvalidOperationException("org.address.resource_mismatch");
                MEMNode scope=null;
                foreach(var id in address.InstancePath)
                {
                    var reference=FindReference(id);
                    if(reference==null)return null;
                    if(scope!=null&&!Within(reference.Owner,scope))throw new InvalidOperationException("org.address.instance_mismatch");
                    scope=Find(reference.TargetId);
                }
                if(scope!=null&&!Within(node,scope))throw new InvalidOperationException("org.address.instance_mismatch");
                return node;
            }
        }
        private static bool Within(MEMNode node,MEMNode ancestor)
        {for(var n=node;n!=null;n=n.Parent)if(n==ancestor)return true;return false;}
    }
}
