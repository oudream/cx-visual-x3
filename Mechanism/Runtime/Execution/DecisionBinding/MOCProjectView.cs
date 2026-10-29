using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>Minimal read-only Content identity shared by old MEM and the
    /// self-contained Decision Plan loader. Never owns or edits MEM nodes.</summary>
    internal sealed class MOCContentView
    {
        internal MOCContentView(MEMNodeSnapshot node)
        {
            Node=node??throw new ArgumentNullException("node");
            Id=node.Id;Kind=node.Kind;StorageRootId=node.StorageRootId;
            References=new ReadOnlyCollection<MOCReferenceView>(
                node.References.Select(x=>new MOCReferenceView(x.Id,x.TargetId,x.Kind)).ToArray());
        }
        internal MOCContentView(MDCPlanContent content)
        {
            if(content==null)throw new ArgumentNullException("content");
            Node=null;Id=content.ContentId;Kind=MEMNodeKind.Content;
            StorageRootId=content.StorageRootId;
            References=new ReadOnlyCollection<MOCReferenceView>(
                content.References.Select(x=>new MOCReferenceView(x.Id,x.TargetId,x.Kind)).ToArray());
        }
        internal MEMNodeSnapshot Node {get;private set;}
        internal Guid Id {get;private set;}
        internal Guid StorageRootId {get;private set;}
        internal MEMNodeKind Kind {get;private set;}
        internal IReadOnlyList<MOCReferenceView> References {get;private set;}
    }

    internal sealed class MOCReferenceView
    {
        internal MOCReferenceView(Guid id,Guid target,MRIReferenceKind kind)
        {Id=id;TargetId=target;Kind=kind;}
        internal Guid Id {get;private set;}
        internal Guid TargetId {get;private set;}
        internal MRIReferenceKind Kind {get;private set;}
    }

    /// <summary>Local projection of identities/references, not a second Graph.
    /// Only the legacy route holds a MEMProjectSnapshot; the Plan route does not.</summary>
    internal sealed class MOCProjectView
    {
        private readonly Dictionary<Guid,MOCContentView> _index;
        internal MOCProjectView(MEMProjectSnapshot project)
        {
            if(project==null)throw new ArgumentNullException("project");
            ProjectId=project.ProjectId;Revision=project.Revision;
            Nodes=new ReadOnlyCollection<MOCContentView>(
                project.Nodes.Select(x=>new MOCContentView(x)).ToArray());
            _index=Nodes.ToDictionary(x=>x.Id);
        }
        internal MOCProjectView(MDCDeclarativePlan plan)
        {
            if(plan==null)throw new ArgumentNullException("plan");
            plan.RequireIntegrity();
            ProjectId=plan.ProjectId;Revision=plan.EngineeringRevision;
            Nodes=new ReadOnlyCollection<MOCContentView>(
                plan.Contents.Select(x=>new MOCContentView(x)).ToArray());
            _index=Nodes.ToDictionary(x=>x.Id);
        }
        internal Guid ProjectId {get;private set;}
        internal int Revision {get;private set;}
        internal IReadOnlyList<MOCContentView> Nodes {get;private set;}
        internal MOCContentView Find(Guid id)
        {MOCContentView result;return _index.TryGetValue(id,out result)?result:null;}
    }
}
