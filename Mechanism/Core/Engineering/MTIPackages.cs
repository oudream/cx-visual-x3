using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed class MTIExtractionResult
    {
        internal MTIExtractionResult(Guid definition,Guid reference,MTITransferResult transfer){DefinitionId=definition;ReferenceId=reference;Transfer=transfer;}
        public Guid DefinitionId {get;private set;}public Guid ReferenceId {get;private set;}public MTITransferResult Transfer {get;private set;}
    }
    public static class MTIEngineeringTemplates
    {
        public static MEMEngineeringSession Create(string packagePath,string name,IMTIAspectRemapper remapper=null)
        {
            var template=MEMEngineering.Open(packagePath);var project=MEMEngineering.Create(name);
            try{project.Import(project.PreviewImport(template,null,project.Project,MTIExternalPolicy.IncludeClosure,remapper));return project;}
            catch{project.Close(true);throw;}finally{template.Close(true);}
        }
    }
    public sealed partial class MEMEngineeringSession
    {
        /// <summary>Exports a reusable copy with explicit identity map. SaveAs/CaptureSaveSnapshot preserve full-project identities.</summary>
        public MTITransferResult ExportPackage(IEnumerable<Guid> roots,string path,IMTIAspectRemapper remapper=null)
        {
            DemandAccess(MEMAccessOperation.Export);var package=MEMEngineering.Create("Resource package");
            try{var result=package.Import(package.PreviewImport(this,roots,package.Project,MTIExternalPolicy.IncludeClosure,remapper));package.SaveAs(path);return result;}
            finally{package.Close(true);}
        }
        public MTIExtractionResult ExtractReusable(IEnumerable<Guid> roots,MEMContent owner,string name,IMTIAspectRemapper remapper=null)
        {
            if(roots==null)throw new ArgumentNullException("roots");DemandAccess(MEMAccessOperation.Edit);var selected=roots.ToArray();var definition=new MEMContent(Guid.NewGuid(),name);
            MEMProjectSnapshot snapshot;Dictionary<Guid,MEMResourceEntry> resources;string hash;TransferFacts(out snapshot,out resources,out hash);MTITransferPlan plan;
            lock(_gate)
            {
                DemandOpen();DemandNode(owner);if(selected.Any(id=>Find(id)==null||Find(id).Parent!=owner))throw new InvalidOperationException("org.extract.requires_direct_content_children");
                if(_editing||_collectingTypes!=0)throw new InvalidOperationException("org.transfer.busy");plan=new MTITransferPlan(snapshot,this,resources,this,definition,selected,MTIExternalPolicy.Pin,hash);_collectingTypes=1;
            }
            try{plan.PrepareAspects(remapper);}finally{lock(_gate)_collectingTypes=0;}
            if(!plan.IsValid)throw new InvalidOperationException(string.Join(";",plan.Issues));Guid referenceId=Guid.Empty;
            Edit("提取可复用内容",plan.BaseRevision,e=>{referenceId=e.Extract(plan,owner,definition).Id;return true;}).RequireValue();
            return new MTIExtractionResult(definition.Id,referenceId,new MTITransferResult(plan,Revision));
        }
    }
    public sealed partial class MTCEditSession
    {
        private void AddTransferredConnection(MPCConnection connection)
        {
            var owner=_session.Find(connection.OwnerId);var state=Definition(owner);_session.CheckConnection(owner,connection.Source,connection.Target);
            Change(()=>{if(state.ConnectionItems==null)state.ConnectionItems=new List<MPCConnection>();_session.IndexConnection(connection);state.ConnectionItems.Add(connection);},()=>{state.ConnectionItems.Remove(connection);_session.UnindexConnection(connection);},MTCChangeKind.Connection,connection.Id);
        }
        internal MRIResourceReference Extract(MTITransferPlan plan,MEMContent owner,MEMContent definition)
        {
            Demand();DemandEditable(owner);
            // Boundary wires are explicit records owned outside the selected forest, never silently discarded.
            Func<MPCEndpoint,bool> inside=endpoint=>endpoint.Address.InstancePath.Count==0?plan.Included.Contains(endpoint.Address.ObjectId):plan.Included.Contains(_session.FindReference(endpoint.Address.InstancePath[0]).Owner.Id);
            var wires=plan.Source.Nodes.Where(n=>!plan.Included.Contains(n.Id)).SelectMany(n=>n.Connections).Where(c=>inside(c.Source)||inside(c.Target)).ToArray();
            foreach(var wire in wires)Disconnect(_session.FindConnection(wire.Id));
            Add(_session.Project,definition);Import(plan);var use=Reference(owner,definition,definition.Name);var publicPorts=new Dictionary<MPCEndpoint,string>();
            Func<MPCEndpoint,MPCEndpoint> endpointMap=endpoint=>
            {
                if(!inside(endpoint))return endpoint;string member;
                if(!publicPorts.TryGetValue(endpoint,out member)){member="p_"+publicPorts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);ExposePort(definition,member,plan.Map(endpoint));publicPorts.Add(endpoint,member);}return _session.Endpoint(use,member);
            };
            foreach(var wire in wires)AddTransferredConnection(new MPCConnection(wire.Id,wire.OwnerId,endpointMap(wire.Source),endpointMap(wire.Target)).CopyRouteFrom(wire));
            foreach(var id in plan.SelectedRoots)Remove(_session.Find(id));return use;
        }
    }
}
