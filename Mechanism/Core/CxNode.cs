using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace CxVisual.Mechanism.Core
{
    /// <summary>Stateless compatibility surface over MEMNode. No second tree or identity storage.</summary>
    public abstract class CxNode
    {
        public Guid Id { get { return ((MEMNode)this).Id; } }
        public string Name { get { return ((MEMNode)this).Name; } }
        public CxNode Parent { get { return ((MEMNode)this).Parent; } }
        public CxNodeKind Kind { get { return (CxNodeKind)((int)((MEMNode)this).Kind - 1); } }
        public ReadOnlyCollection<CxNode> Children { get { return new ReadOnlyCollection<CxNode>(new LegacyChildren((MEMNode)this)); } }
        public CxNode Find(Guid id) { return ((MEMNode)this).Find(id); }
        internal void Attach(CxNode node)
        {
            var self = (MEMNode)this;
            if (self.OwnerSession != null) throw new InvalidOperationException("Use the engineering session");
            self.InsertOwned(self.Children.Count, (MEMNode)node);
        }
        internal void Detach(CxNode node)
        {
            var self = (MEMNode)this;
            if (self.OwnerSession != null) throw new InvalidOperationException("Use the engineering session");
            self.DetachOwned((MEMNode)node);
        }
        public static bool Allows(CxNodeKind parent, CxNodeKind child)
        {
            if (parent == CxNodeKind.Project) return child == CxNodeKind.Page || child == CxNodeKind.Content || child == CxNodeKind.Symbol;
            if (parent == CxNodeKind.Page) return child == CxNodeKind.Content;
            if (parent == CxNodeKind.Content) return child == CxNodeKind.Content || child == CxNodeKind.Symbol || child == CxNodeKind.Group || child == CxNodeKind.Element;
            if (parent == CxNodeKind.Symbol) return child == CxNodeKind.Group;
            if (parent == CxNodeKind.Group) return child == CxNodeKind.Group || child == CxNodeKind.Element;
            return false;
        }
        private sealed class LegacyChildren : IList<CxNode>
        {
            private readonly MEMNode _owner;
            internal LegacyChildren(MEMNode owner) { _owner = owner; }
            public int Count { get { return _owner.Children.Count; } }
            public bool IsReadOnly { get { return true; } }
            public CxNode this[int i] { get { return _owner.Children[i]; } set { throw new NotSupportedException(); } }
            public int IndexOf(CxNode item) { return _owner.IndexOf((MEMNode)item); }
            public bool Contains(CxNode item) { return IndexOf(item) >= 0; }
            public void CopyTo(CxNode[] array, int start) { foreach (var n in this) array[start++] = n; }
            public IEnumerator<CxNode> GetEnumerator() { foreach (var n in _owner.Children) yield return n; }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
            public void Add(CxNode item) { throw new NotSupportedException(); }
            public void Clear() { throw new NotSupportedException(); }
            public void Insert(int index, CxNode item) { throw new NotSupportedException(); }
            public bool Remove(CxNode item) { throw new NotSupportedException(); }
            public void RemoveAt(int index) { throw new NotSupportedException(); }
        }
    }
}
