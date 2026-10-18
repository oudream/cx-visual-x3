using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Core
{
    public enum MTCChangeKind { Add=1, Remove=2, Rename=3, Reparent=4, Geometry=5, Binding=6, Visibility=7, Reference=8, RemoveReference=9, Property=10, Port=11, Connection=12, Mapping=13, Aspect=14, ReferenceValue=15, TypeRegistration=16, Asset=17 }
    /// <summary>Immutable committed operation identity; inverse values stay in the private journal, not a replayable WAL.</summary>
    public struct MTCChangeOperation
    {
        internal MTCChangeOperation(MTCChangeKind kind,Guid target) { Kind=kind;TargetId=target; }
        public MTCChangeKind Kind { get; private set; }
        public Guid TargetId { get; private set; }
        internal MTCChangeOperation Reversed()
        { return new MTCChangeOperation(Kind==MTCChangeKind.Add?MTCChangeKind.Remove:Kind==MTCChangeKind.Remove?MTCChangeKind.Add:Kind==MTCChangeKind.Reference?MTCChangeKind.RemoveReference:Kind==MTCChangeKind.RemoveReference?MTCChangeKind.Reference:Kind,TargetId); }
    }
    public enum MTCEditState { Open=1, Committing=2, Committed=3, Cancelled=4, Rejected=5 }
    /// <summary>Queued intentions, not a second engineering tree. Preview uses the same reversible validator under the session gate.</summary>
    public sealed class MTCEditDraft : IDisposable
    {
        private readonly MEMEngineeringSession _session;
        private readonly object _gate=new object();
        private readonly List<Action<MTCEditSession>> _commands=new List<Action<MTCEditSession>>();
        internal MTCEditDraft(MEMEngineeringSession session,string intent,int revision) { _session=session;Intent=MEMNode.NormalizeName(intent);BaseRevision=revision;State=MTCEditState.Open; }
        public string Intent { get; private set; }
        public int BaseRevision { get; private set; }
        public MTCEditState State { get; private set; }
        private void Demand() { if(State!=MTCEditState.Open)throw new InvalidOperationException("org.draft.closed"); }
        private void Queue(Action<MTCEditSession> command) { lock(_gate){Demand();_commands.Add(command);} }
        public MEMNode Add(MEMNode parent,MEMNode node) { if(node==null)throw new ArgumentNullException("node");Queue(e=>e.Add(parent,node));return node; }
        public MEMPage AddPage(string name) { return (MEMPage)Add(_session.Project,new MEMPage(Guid.NewGuid(),name)); }
        public MEMContent AddContent(MEMNode parent,string name) { return (MEMContent)Add(parent,new MEMContent(Guid.NewGuid(),name)); }
        public MEMSymbol AddSymbol(string name) { return (MEMSymbol)Add(_session.Project,new MEMSymbol(Guid.NewGuid(),name)); }
        public MRCGroup AddGroup(MEMNode parent,string name) { return (MRCGroup)Add(parent,new MRCGroup(Guid.NewGuid(),name)); }
        public MRCRectangle AddRectangle(MEMNode parent,string name,double x,double y,double width=120,double height=80)
        { var page=parent as MEMPage;if(page!=null)parent=page.RootContent;return (MRCRectangle)Add(parent,new MRCRectangle(Guid.NewGuid(),name,x,y,width,height)); }
        public void Rename(MEMNode node,string name) { name=MEMNode.NormalizeName(name);Queue(e=>e.Rename(node,name)); }
        public void Rename(MRIResourceReference reference,string name){name=MEMNode.NormalizeName(name);Queue(e=>e.Rename(reference,name));}
        public void Retarget(MRIResourceReference reference,MEMNode definition){Queue(e=>e.Retarget(reference,definition));}
        public void Reparent(MEMNode node,MEMNode parent,int index=-1) { Queue(e=>e.Reparent(node,parent,index)); }
        public void Remove(MEMNode node) { Queue(e=>e.Remove(node)); }
        public void Remove(MRIResourceReference reference) { Queue(e=>e.Remove(reference)); }
        public void Move(MRCVisualItem item,double x,double y) { Queue(e=>e.Move(item,x,y)); }
        public void Resize(MRCVisualItem item,double w,double h) { Queue(e=>e.Resize(item,w,h)); }
        public void Rotate(MRCVisualItem item,double angle) { Queue(e=>e.Rotate(item,angle)); }
        public void Scale(MRCVisualItem item,double x,double y) { Queue(e=>e.Scale(item,x,y)); }
        public void SetTransform(MRCVisualItem item,MRCTransform transform) {Queue(e=>e.SetTransform(item,transform));}
        public void SetLocked(MRCVisualItem item,bool locked) {Queue(e=>e.SetLocked(item,locked));}
        public void SetLayer(MRCVisualItem item,string layerId) {Queue(e=>e.SetLayer(item,layerId));}
        public void SetVisibility(MRCVisualItem item,bool visible,bool selectable) { Queue(e=>e.SetVisibility(item,visible,selectable)); }
        public void SetBinding(MRCVisualItem item,string property,MPMBinding binding) { Queue(e=>e.SetBinding(item,property,binding)); }
        public void Reference(MEMContent owner,MEMNode definition,string name) { Queue(e=>e.Reference(owner,definition,name)); }
        public void DefineProperty(MEMNode owner,MPMPropertyDescriptor descriptor){Queue(e=>e.DefineProperty(owner,descriptor));}
        public void SetProperty(MEMNode owner,string member,MPMBinding binding){Queue(e=>e.SetProperty(owner,member,binding));}
        public void RemoveProperty(MEMNode owner,string member){Queue(e=>e.RemoveProperty(owner,member));}
        public void SetOverride(MRIResourceReference reference,string member,MPMBinding binding){Queue(e=>e.SetOverride(reference,member,binding));}
        public void ClearOverride(MRIResourceReference reference,string member){Queue(e=>e.SetOverride(reference,member,null));}
        public void SetPlacement(MRIResourceReference reference,MRCTransform placement){Queue(e=>e.SetPlacement(reference,placement));}
        public void SetAspect(MEMNode owner,MPMAspect aspect){Queue(e=>e.SetAspect(owner,aspect));}
        public void Connect(MEMNode owner,MPCEndpoint source,MPCEndpoint target){Queue(e=>e.Connect(owner,source,target));}
        public void Disconnect(MPCConnection connection){Queue(e=>e.Disconnect(connection));}
        public void ExposePort(MEMNode owner,string member,MPCEndpoint target){Queue(e=>e.ExposePort(owner,member,target));}
        public void ExposeProperty(MEMNode owner,string member,MRIObjectAddress target,string targetMember){Queue(e=>e.ExposeProperty(owner,member,target,targetMember));}
        public void Unexpose(MEMNode owner,string member,MRCPublicMappingKind kind){Queue(e=>e.Unexpose(owner,member,kind));}
        public void RemovePort(MPCPort port){Queue(e=>e.RemovePort(port));}
        public void DefinePort(MEMNode owner,string member,MPCPortDirection direction,string typeId,MPCPortKind kind=MPCPortKind.Data,MPCPortMultiplicity multiplicity=MPCPortMultiplicity.Many,MTUContractReference contract=null,bool isPublic=false,bool required=false)
        {Queue(e=>e.DefinePort(owner,member,direction,typeId,kind,multiplicity,contract,isPublic,required));}
        public MVEValidationReport Preview()
        { Action<MTCEditSession>[] copy;lock(_gate){Demand();copy=_commands.ToArray();}return _session.PreviewDraft(BaseRevision,e=>{foreach(var command in copy)command(e);}); }
        public MTCEditResult<bool> Commit()
        {
            Action<MTCEditSession>[] copy;lock(_gate){Demand();State=MTCEditState.Committing;copy=_commands.ToArray();}
            try
            {
                var result=_session.Edit(Intent,BaseRevision,e=>{foreach(var command in copy)command(e);return true;});
                lock(_gate){State=result.Success?MTCEditState.Committed:MTCEditState.Rejected;_commands.Clear();}return result;
            }
            catch { lock(_gate){State=MTCEditState.Rejected;_commands.Clear();}throw; }
        }
        public void Cancel() { lock(_gate){Demand();State=MTCEditState.Cancelled;_commands.Clear();} }
        public void Dispose() { lock(_gate)if(State==MTCEditState.Open){State=MTCEditState.Cancelled;_commands.Clear();} }
    }
}
