using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Data.Variables
{
    // x2组织树迁移：目录节点仅持有结构/身份；值只能由已有MDSVariable<T>持有。
    public abstract class MDSVariableNode
    {
        protected MDSVariableNode(string name, string id)
        {
            Validate(name);
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("变量ID不能为空", "id");
            Name = name.Trim(); VariableId = id.Trim();
        }
        public string VariableId { get; private set; }
        public string Name { get; internal set; }
        public string OwnerRef { get; internal set; }
        public MDSVariableGroup Parent { get; internal set; }
        public string ParentId { get { return Parent == null ? null : Parent.VariableId; } }
        public string VariablePath
        {
            get { return Parent == null || Parent.IsRoot ? "/" + Name : Parent.VariablePath + "/" + Name; }
        }
        internal static void Validate(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0)
                throw new ArgumentException("变量名称不得为空或包含路径分隔符", "name");
        }
    }
    public sealed class MDSVariableGroup : MDSVariableNode
    {
        internal readonly List<MDSVariableNode> Items = new List<MDSVariableNode>();
        internal bool IsRoot;
        internal MDSVariableGroup(string name, string id) : base(name, id) { }
        public MDSVariableNode[] Children { get { return Items.ToArray(); } }
    }
    public sealed class MDSVariableLeaf : MDSVariableNode
    {
        private readonly MDSVariableBase _target;
        internal MDSVariableLeaf(MDSVariableBase target)
            : base(target.EntryDefinition.Name, target.EntryDefinition.VariableId) { _target = target; }
        public MDSVariableDefinition Definition { get { return _target.EntryDefinition; } }
        public MDSVariable<T> Resolve<T>()
        {
            MDSVariable<T> result = _target as MDSVariable<T>;
            if (result == null) throw new InvalidCastException("变量类型不匹配: " + VariableId);
            return result;
        }
    }

    public sealed class MDSVariableTree
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MDSVariableNode> _ids = new Dictionary<string, MDSVariableNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, MDSVariableNode> _paths = new Dictionary<string, MDSVariableNode>(StringComparer.Ordinal);
        public MDSVariableTree()
        {
            Root = new MDSVariableGroup("Root", "variable-root") { IsRoot = true };
            _ids.Add(Root.VariableId, Root);
        }
        public MDSVariableGroup Root { get; private set; }
        public MDSVariableGroup AddGroup(string name, string parentId = null, string id = null)
        {
            lock (_gate)
            {
                MDSVariableGroup result = new MDSVariableGroup(name,
                    string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id);
                Attach(ParentOf(parentId), result);
                return result;
            }
        }
        internal void AddLeaf(MDSVariableBase target)
        {
            lock (_gate) Attach(Root, new MDSVariableLeaf(target));
        }
        internal void RemoveLeaf(string id)
        {
            lock (_gate)
            {
                MDSVariableNode node = Require(id);
                if (!(node is MDSVariableLeaf)) throw new InvalidOperationException("ID不是变量叶节点");
                RemoveNode(node);
            }
        }
        public bool RemoveEmptyGroup(string id)
        {
            lock (_gate)
            {
                MDSVariableNode node;
                if (id == null || !_ids.TryGetValue(id, out node)) return false;
                MDSVariableGroup group = node as MDSVariableGroup;
                if (group == null || group == Root || group.Items.Count != 0)
                    throw new InvalidOperationException("只能删除非根空分组");
                RemoveNode(group);
                return true;
            }
        }
        public void Move(string id, string parentId, string newName = null)
        {
            lock (_gate)
            {
                MDSVariableNode node = Require(id);
                if (node == Root) throw new InvalidOperationException("不能移动根节点");
                MDSVariableGroup parent = ParentOf(parentId);
                string name = newName == null ? node.Name : newName.Trim();
                MDSVariableNode.Validate(name);
                for (MDSVariableGroup g = parent; g != null; g = g.Parent)
                    if (g == node) throw new InvalidOperationException("不能将父节点移入自身子树");
                CheckSibling(parent, name, node);
                MDSVariableGroup old = node.Parent;
                old.Items.Remove(node);
                node.Parent = parent; node.Name = name; parent.Items.Add(node);
                Reindex();
            }
        }
        public void Rename(string id, string name)
        {
            lock (_gate) { MDSVariableNode node = Require(id); Move(id, node.ParentId, name); }
        }
        public void SetOwner(string id, string ownerRef)
        {
            lock (_gate) Require(id).OwnerRef = ownerRef;
        }
        public MDSVariableNode FindById(string id)
        {
            lock (_gate) { MDSVariableNode node; return id != null && _ids.TryGetValue(id, out node) ? node : null; }
        }
        public MDSVariableNode FindByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string normalized = "/" + string.Join("/", path.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            lock (_gate) { MDSVariableNode node; return _paths.TryGetValue(normalized, out node) ? node : null; }
        }
        public MDSVariableNode[] Snapshot()
        {
            lock (_gate) return _ids.Values.Where(x => x != Root).OrderBy(x => x.VariablePath, StringComparer.Ordinal).ToArray();
        }
        private void Attach(MDSVariableGroup parent, MDSVariableNode node)
        {
            if (_ids.ContainsKey(node.VariableId)) throw new InvalidOperationException("变量ID重复: " + node.VariableId);
            CheckSibling(parent, node.Name, null);
            parent.Items.Add(node); node.Parent = parent;
            _ids.Add(node.VariableId, node); _paths.Add(node.VariablePath, node);
        }
        private void RemoveNode(MDSVariableNode node)
        {
            _ids.Remove(node.VariableId); _paths.Remove(node.VariablePath);
            node.Parent.Items.Remove(node); node.Parent = null;
        }
        private void Reindex()
        {
            _paths.Clear();
            foreach (MDSVariableNode node in _ids.Values.Where(x => x != Root)) _paths.Add(node.VariablePath, node);
        }
        private MDSVariableNode Require(string id)
        {
            MDSVariableNode node;
            if (id == null || !_ids.TryGetValue(id, out node)) throw new KeyNotFoundException("找不到变量节点: " + id);
            return node;
        }
        private MDSVariableGroup ParentOf(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Root;
            MDSVariableGroup g = Require(id) as MDSVariableGroup;
            if (g == null) throw new InvalidOperationException("父节点不是分组");
            return g;
        }
        private static void CheckSibling(MDSVariableGroup parent, string name, MDSVariableNode except)
        {
            if (parent.Items.Any(x => x != except && string.Equals(x.Name, name, StringComparison.Ordinal)))
                throw new InvalidOperationException("分组内名称重复: " + name);
        }
    }
}
