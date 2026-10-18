using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    /// <summary>Explicit immutable flat projection. It owns no live nodes and cannot become a second writable engineering tree.</summary>
    public sealed class MEMProjectSnapshot
    {
        private readonly Dictionary<Guid,MEMNodeSnapshot> _index;
        internal MEMProjectSnapshot(MEMProject project,int revision)
        {
            ProjectId=project.Id;Revision=revision;
            var nodes=project.DescendantsAndSelf().Select(n=>new MEMNodeSnapshot(n)).ToList();
            Nodes=nodes.AsReadOnly();_index=nodes.ToDictionary(n=>n.Id);
        }
        public Guid ProjectId { get; private set; }
        public int Revision { get; private set; }
        public IReadOnlyList<MEMNodeSnapshot> Nodes { get; private set; }
        public MEMNodeSnapshot Find(Guid id) { MEMNodeSnapshot node;return _index.TryGetValue(id,out node)?node:null; }
    }
    public sealed class MEMNodeSnapshot
    {
        private static readonly IReadOnlyList<MRIReferenceSnapshot> EmptyReferences=Array.AsReadOnly(new MRIReferenceSnapshot[0]);
        internal MEMNodeSnapshot(MEMNode node)
        {
            Id=node.Id;Name=node.Name;TypeId=node.TypeId;Kind=node.Kind;ParentId=node.Parent==null?(Guid?)null:node.Parent.Id;
            ChildIds=Array.AsReadOnly(node.Children.Select(c=>c.Id).ToArray());StorageRootId=node.ResourceRootId;
            var resource=node as IMEMResource;Scope=resource==null?(MEMResourceScope?)null:resource.Scope;
            var visual=node as MRCVisualItem;if(visual!=null)Visual=new MRCVisualSnapshot(visual);
            var content=node as MEMContent;
            References=content==null||content.References.Count==0?EmptyReferences:content.References.Select(r=>new MRIReferenceSnapshot(r)).ToList().AsReadOnly();
        }
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string TypeId { get; private set; }
        public MEMNodeKind Kind { get; private set; }
        public Guid? ParentId { get; private set; }
        public Guid StorageRootId { get; private set; }
        public MEMResourceScope? Scope { get; private set; }
        public IReadOnlyList<Guid> ChildIds { get; private set; }
        public MRCVisualSnapshot Visual { get; private set; }
        public IReadOnlyList<MRIReferenceSnapshot> References { get; private set; }
    }
    public sealed class MRCVisualSnapshot
    {
        private static readonly IReadOnlyDictionary<string,MPMBinding> EmptyBindings=new ReadOnlyDictionary<string,MPMBinding>(new Dictionary<string,MPMBinding>());
        internal MRCVisualSnapshot(MRCVisualItem item)
        {
            X=item.X;Y=item.Y;Width=item.Width;Height=item.Height;Rotation=item.Rotation;ScaleX=item.ScaleX;ScaleY=item.ScaleY;IsVisible=item.IsVisible;IsSelectable=item.IsSelectable;
            Bindings=item.BindingItems==null?EmptyBindings:new ReadOnlyDictionary<string,MPMBinding>(new Dictionary<string,MPMBinding>(item.BindingItems,StringComparer.Ordinal));
        }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Rotation { get; private set; }
        public double ScaleX { get; private set; }
        public double ScaleY { get; private set; }
        public bool IsVisible { get; private set; }
        public bool IsSelectable { get; private set; }
        public IReadOnlyDictionary<string,MPMBinding> Bindings { get; private set; }
    }
    public sealed class MRIReferenceSnapshot
    {
        internal MRIReferenceSnapshot(MRIResourceReference reference)
        {Id=reference.Id;Name=reference.Name;TargetId=reference.TargetId;Kind=reference.Kind;OwnerId=reference.Owner.Id;}
        public Guid Id { get; private set; }
        public Guid TargetId { get; private set; }
        public Guid OwnerId { get; private set; }
        public string Name { get; private set; }
        public MRIReferenceKind Kind { get; private set; }
    }
}
