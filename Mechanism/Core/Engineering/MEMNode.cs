using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Core;

namespace CxVisual.Mechanism.Core
{
    public enum MEMNodeKind { Project = 1, Page = 2, Content = 3, Symbol = 4, Group = 5, Element = 6 }
    public enum MEMResourceScope { Standalone = 1, Embedded = 2 }
    public interface IMEMResource
    {
        Guid Id { get; }
        string Name { get; }
        string TypeId { get; }
        Guid StorageRootId { get; }
        MEMResourceScope Scope { get; }
    }

    /// <summary>Only identity/navigation storage. Optional abilities and child lists belong to derived owners.</summary>
    public abstract class MEMNode : CxNode
    {
        internal static readonly IReadOnlyList<MEMNode> Empty = new MEMNode[0];
        private string _name;
        private MEMNode _parent;
        protected MEMNode(Guid id, string name, string typeId, MEMNodeKind kind)
        {
            Id = new FOIStableId(id).Value; _name = NormalizeName(name); TypeId = typeId; Kind = kind;
        }
        public new Guid Id { get; private set; }
        public new string Name { get { return _name; } }
        public string TypeId { get; private set; }
        public new MEMNodeKind Kind { get; private set; }
        public new MEMNode Parent { get { return _parent; } }
        public new IReadOnlyList<MEMNode> Children { get { return Owned; } }
        internal virtual IReadOnlyList<MEMNode> Owned { get { return Empty; } }
        internal virtual bool Accepts(MEMNode child) { return false; }
        internal virtual void InsertCore(int index, MEMNode child) { throw new InvalidOperationException("Leaf has no children"); }
        internal virtual void RemoveCore(MEMNode child) { throw new InvalidOperationException("Leaf has no children"); }
        internal void SetName(string name) { _name = NormalizeName(name); }
        internal static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", "name");
            name = name.Trim().Normalize();
            if (name.Length > 128 || name.Any(char.IsControl)) throw new ArgumentException("Invalid name", "name");
            return name;
        }
        internal MEMEngineeringSession OwnerSession
        {
            get { var root = this; while (root.Parent != null) root = root.Parent; var p = root as MEMProject; return p == null ? null : p.Session; }
        }
        internal void InsertOwned(int index, MEMNode child)
        {
            if (child == null) throw new ArgumentNullException("child");
            if (child.Parent != null) throw new InvalidOperationException("Object already has an owner");
            if (!Accepts(child)) throw new InvalidOperationException("Illegal containment");
            for (MEMNode n = this; n != null; n = n.Parent) if (n == child) throw new InvalidOperationException("Ownership cycle");
            if (index < 0 || index > Owned.Count) throw new ArgumentOutOfRangeException("index");
            InsertCore(index, child); child._parent = this;
        }
        internal void DetachOwned(MEMNode child)
        {
            if (child == null || child.Parent != this) throw new InvalidOperationException("Wrong owner");
            RemoveCore(child); child._parent = null;
        }
        internal int IndexOf(MEMNode child) { for (int i = 0; i < Owned.Count; i++) if (Owned[i] == child) return i; return -1; }
        public IEnumerable<MEMNode> DescendantsAndSelf()
        {
            var stack = new Stack<MEMNode>(); stack.Push(this);
            while (stack.Count != 0) { var n = stack.Pop(); yield return n; for (int i = n.Owned.Count - 1; i >= 0; i--) stack.Push(n.Owned[i]); }
        }
        public IEnumerable<MEMNode> Ancestors() { for (var n = Parent; n != null; n = n.Parent) yield return n; }
        public new MEMNode Find(Guid id)
        {
            var session = OwnerSession;
            if (session != null)
            {
                var found = session.Find(id);
                for (var n = found; n != null; n = n.Parent) if (n == this) return found;
                return null;
            }
            return DescendantsAndSelf().FirstOrDefault(n => n.Id == id);
        }
        internal Guid ResourceRootId
        {
            get { var node = this; while (node.Parent != null && !(node.Parent is MEMProject)) node = node.Parent; return node.Id; }
        }
    }

    // Small typed storage helper; it owns no policy, identity or indexing semantics.
    internal sealed class MEMChildList<T> : IReadOnlyList<T> where T : MEMNode
    {
        private readonly List<T> _items = new List<T>();
        public T this[int index] { get { return _items[index]; } }
        public int Count { get { return _items.Count; } }
        internal void Insert(int index, T item) { _items.Insert(index, item); }
        internal void Remove(T item) { if (!_items.Remove(item)) throw new InvalidOperationException("Missing child"); }
        public IEnumerator<T> GetEnumerator() { return _items.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
    internal sealed class MEMTypedView<T> : IReadOnlyList<T> where T : class
    {
        private readonly Func<IEnumerable<T>> _read;
        internal MEMTypedView(Func<IEnumerable<T>> read) { _read = read; }
        public int Count { get { return _read().Count(); } }
        public T this[int index] { get { return _read().ElementAt(index); } }
        public IEnumerator<T> GetEnumerator() { return _read().GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public class MEMProject : MEMNode
    {
        private MEMChildList<MEMNode> _items;
        internal MEMEngineeringSession Session;
        public MEMProject(Guid id, string name) : base(id, name, "core.project", MEMNodeKind.Project) { }
        internal override IReadOnlyList<MEMNode> Owned { get { return _items == null ? Empty : _items; } }
        internal override bool Accepts(MEMNode child) { return child is MEMPage || child is MEMContent || child is MEMSymbol; }
        internal override void InsertCore(int index, MEMNode child) { if (_items == null) _items = new MEMChildList<MEMNode>(); _items.Insert(index, child); }
        internal override void RemoveCore(MEMNode child) { _items.Remove(child); }
        public IReadOnlyList<MEMPage> Pages { get { return new MEMTypedView<MEMPage>(() => Owned.OfType<MEMPage>()); } }
        public IReadOnlyList<MEMContent> Contents { get { return new MEMTypedView<MEMContent>(() => Owned.OfType<MEMContent>()); } }
        public IReadOnlyList<MEMSymbol> Symbols { get { return new MEMTypedView<MEMSymbol>(() => Owned.OfType<MEMSymbol>()); } }
        public IReadOnlyList<IMEMResource> Resources { get { return new MEMTypedView<IMEMResource>(() => Owned.OfType<IMEMResource>()); } }
        public IMEMResource FindResource(Guid id, bool includeEmbedded = false)
        {
            var n = Find(id) as IMEMResource;
            return n != null && (includeEmbedded || n.Scope == MEMResourceScope.Standalone) ? n : null;
        }
        public MEMPage FindPage(Guid id) { return Find(id) as MEMPage; }
        public MEMContent FindContent(Guid id) { return Find(id) as MEMContent; }
        public MEMSymbol FindSymbol(Guid id) { return Find(id) as MEMSymbol; }
    }
    public class MEMPage : MEMNode, IMEMResource
    {
        private MEMChildList<MEMContent> _items;
        public MEMPage(Guid id, string name) : this(id, name, true) { }
        internal MEMPage(Guid id, string name, bool root) : base(id, name, "core.page", MEMNodeKind.Page)
        { if (root) InsertOwned(0, new MEMContent(Guid.NewGuid(), "RootContent")); }
        public MEMContent RootContent { get { return Owned.OfType<MEMContent>().FirstOrDefault(); } }
        public Guid StorageRootId { get { return ResourceRootId; } }
        public MEMResourceScope Scope { get { return Parent is MEMProject ? MEMResourceScope.Standalone : MEMResourceScope.Embedded; } }
        internal override IReadOnlyList<MEMNode> Owned { get { return _items == null ? Empty : _items; } }
        internal override bool Accepts(MEMNode child) { return child is MEMContent && Owned.Count == 0; }
        internal override void InsertCore(int index, MEMNode child) { if (_items == null) _items = new MEMChildList<MEMContent>(); _items.Insert(index, (MEMContent)child); }
        internal override void RemoveCore(MEMNode child) { _items.Remove((MEMContent)child); }
    }
    public class MEMContent : MEMNode, IMEMResource
    {
        private MEMChildList<MEMNode> _items;
        internal List<MRIResourceReference> ReferenceItems;
        private static readonly MRIResourceReference[] NoReferences = new MRIResourceReference[0];
        public MEMContent(Guid id, string name) : base(id, name, "core.content", MEMNodeKind.Content) { }
        public Guid StorageRootId { get { return ResourceRootId; } }
        public MEMResourceScope Scope { get { return Parent is MEMProject ? MEMResourceScope.Standalone : MEMResourceScope.Embedded; } }
        public IReadOnlyList<MRIResourceReference> References { get { return ReferenceItems == null ? (IReadOnlyList<MRIResourceReference>)NoReferences : ReferenceItems.AsReadOnly(); } }
        public IReadOnlyList<MEMContent> Contents { get { return new MEMTypedView<MEMContent>(() => Owned.OfType<MEMContent>()); } }
        public IReadOnlyList<MRCVisualItem> VisualItems { get { return new MEMTypedView<MRCVisualItem>(() => Owned.OfType<MRCVisualItem>()); } }
        internal override IReadOnlyList<MEMNode> Owned { get { return _items == null ? Empty : _items; } }
        internal override bool Accepts(MEMNode child) { return child is MEMContent || child is MRCVisualItem || (this is CxContent && child is CxSymbol); }
        internal override void InsertCore(int index, MEMNode child) { if (_items == null) _items = new MEMChildList<MEMNode>(); _items.Insert(index, child); }
        internal override void RemoveCore(MEMNode child) { _items.Remove(child); }
    }
    public class MEMSymbol : MEMNode, IMEMResource
    {
        private MEMChildList<MRCGroup> _items;
        public MEMSymbol(Guid id, string name) : this(id, name, true) { }
        internal MEMSymbol(Guid id, string name, bool root) : base(id, name, "core.symbol", MEMNodeKind.Symbol)
        { if (root) InsertOwned(0, new MRCGroup(Guid.NewGuid(), "RootGroup")); }
        public MRCGroup RootGroup { get { return Owned.OfType<MRCGroup>().FirstOrDefault(); } }
        public Guid StorageRootId { get { return ResourceRootId; } }
        public MEMResourceScope Scope { get { return Parent is MEMProject ? MEMResourceScope.Standalone : MEMResourceScope.Embedded; } }
        internal override IReadOnlyList<MEMNode> Owned { get { return _items == null ? Empty : _items; } }
        internal override bool Accepts(MEMNode child) { return child is MRCGroup && Owned.Count == 0; }
        internal override void InsertCore(int index, MEMNode child) { if (_items == null) _items = new MEMChildList<MRCGroup>(); _items.Insert(index, (MRCGroup)child); }
        internal override void RemoveCore(MEMNode child) { _items.Remove((MRCGroup)child); }
    }
    public enum MRIReferenceKind { Content = 1, Symbol = 2 }
    public sealed class MRIResourceReference
    {
        internal MRIResourceReference(Guid id, string name, Guid targetId, MRIReferenceKind kind, MEMContent owner)
        { Id = id; Name = MEMNode.NormalizeName(name); TargetId = targetId; Kind = kind; Owner = owner; }
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public Guid TargetId { get; private set; }
        public MRIReferenceKind Kind { get; private set; }
        public MEMContent Owner { get; internal set; }
    }
}
