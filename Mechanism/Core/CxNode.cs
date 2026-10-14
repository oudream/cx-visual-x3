using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Core
{
    /// <summary>统一Definition节点身份；节点所有权由父子树唯一管理。</summary>
    public abstract class CxNode
    {
        private readonly List<CxNode> _children = new List<CxNode>();
        protected CxNode(Guid id, string name)
        {
            if (id == Guid.Empty) throw new ArgumentException("Node id cannot be empty", "id");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Node name is required", "name");
            Id = id;
            Name = name;
        }

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public CxNode Parent { get; private set; }
        public abstract CxNodeKind Kind { get; }
        public ReadOnlyCollection<CxNode> Children { get { return _children.AsReadOnly(); } }

        internal void Attach(CxNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            if (node.Parent != null) throw new InvalidOperationException("Node already has an owner");
            if (!Allows(Kind, node.Kind)) throw new InvalidOperationException("Illegal parent/child kind: " + Kind + "/" + node.Kind);
            if (node == this || node.Find(Id) != null) throw new InvalidOperationException("Node hierarchy cycle");
            if (Find(node.Id) != null) throw new InvalidOperationException("Duplicate id in subtree");
            _children.Add(node);
            node.Parent = this;
        }

        internal void Detach(CxNode node)
        {
            if (node == null || !_children.Remove(node)) throw new InvalidOperationException("Child does not belong to parent");
            node.Parent = null;
        }

        public CxNode Find(Guid id)
        {
            if (Id == id) return this;
            foreach (CxNode child in _children)
            {
                CxNode found = child.Find(id);
                if (found != null) return found;
            }
            return null;
        }

        public static bool Allows(CxNodeKind parent, CxNodeKind child)
        {
            if (parent == CxNodeKind.Project) return child == CxNodeKind.Page;
            if (parent == CxNodeKind.Page) return child == CxNodeKind.Content || child == CxNodeKind.Symbol;
            if (parent == CxNodeKind.Content) return child == CxNodeKind.Content || child == CxNodeKind.Symbol;
            if (parent == CxNodeKind.Symbol) return child == CxNodeKind.Group;
            if (parent == CxNodeKind.Group) return child == CxNodeKind.Group || child == CxNodeKind.Element;
            return false;
        }
    }
}
