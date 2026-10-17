using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    /// <summary>Synchronous scoped transaction. Retaining it after Edit returns never grants write access.</summary>
    public sealed class MTCEditSession
    {
        private readonly MEMEngineeringSession _session;
        private bool _active = true;
        private readonly int _threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal readonly List<MTCMutation> Changes = new List<MTCMutation>();
        internal MTCEditSession(MEMEngineeringSession session) { _session = session; }
        internal void Complete() { _active = false; }
        private void Demand() { if (!_active || _threadId != System.Threading.Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("org.edit.expired"); }
        private void Change(Action forward, Action inverse)
        { Demand(); forward(); Changes.Add(new MTCMutation(forward, inverse)); }
        public MEMNode Add(MEMNode parent, MEMNode node)
        {
            Demand(); _session.DemandNode(parent); _session.CheckNew(node);
            int index = parent.Children.Count;
            Change(() => { parent.InsertOwned(index, node); _session.AddIndex(node); }, () => { parent.DetachOwned(node); _session.RemoveIndex(node); });
            return node;
        }
        public MEMPage AddPage(string name) { return (MEMPage)Add(_session.Project, new MEMPage(Guid.NewGuid(), name)); }
        public MEMContent AddContent(MEMNode parent, string name) { return (MEMContent)Add(parent, new MEMContent(Guid.NewGuid(), name)); }
        public MEMSymbol AddSymbol(string name) { return (MEMSymbol)Add(_session.Project, new MEMSymbol(Guid.NewGuid(), name)); }
        public MRCGroup AddGroup(MEMNode parent, string name) { return (MRCGroup)Add(parent, new MRCGroup(Guid.NewGuid(), name)); }
        public MRCRectangle AddRectangle(MEMNode parent, string name, double x, double y, double width = 120, double height = 80)
        {
            var page = parent as MEMPage; if (page != null) parent = page.RootContent;
            return (MRCRectangle)Add(parent, new MRCRectangle(Guid.NewGuid(), name, x, y, width, height));
        }
        public void Rename(MEMNode node, string name)
        { Demand(); _session.DemandNode(node); name = MEMNode.NormalizeName(name); string before = node.Name; if (before != name) Change(() => node.SetName(name), () => node.SetName(before)); }
        public void Reparent(MEMNode node, MEMNode parent, int index = -1)
        {
            Demand(); _session.DemandNode(node); _session.DemandNode(parent);
            var old = node.Parent;
            if (old == null || old is MEMPage || old is MEMSymbol) throw new InvalidOperationException("org.root.protected");
            if (node == parent || _session.Reaches(node, parent)) throw new InvalidOperationException("org.ownership_or_reference.cycle");
            if (!parent.Accepts(node) && parent != old) throw new InvalidOperationException("Illegal destination");
            if (node is IMEMResource && old is MEMProject && !(parent is MEMProject) && _session.HasUsages(node.Id)) throw new InvalidOperationException("org.reference.used");
            int oldIndex = old.IndexOf(node);
            int max = parent.Children.Count - (old == parent ? 1 : 0);
            if (index < 0) index = max;
            if (index > max) throw new ArgumentOutOfRangeException("index");
            int newIndex = index;
            if (old == parent && oldIndex == newIndex) return;
            Change(() => { old.DetachOwned(node); parent.InsertOwned(newIndex, node); }, () => { parent.DetachOwned(node); old.InsertOwned(oldIndex, node); });
        }
        public void Remove(MEMNode node)
        {
            Demand(); _session.DemandNode(node); _session.CheckRemove(node);
            MEMNode parent = node.Parent; int index = parent.IndexOf(node);
            Change(() => { parent.DetachOwned(node); _session.RemoveIndex(node); }, () => { parent.InsertOwned(index, node); _session.RestoreIndex(node); });
        }
        public MRIResourceReference Reference(MEMContent owner, MEMNode definition, string name)
        {
            Demand(); _session.CheckReference(owner, definition);
            var reference = new MRIResourceReference(Guid.NewGuid(), name, definition.Id, definition is MEMSymbol ? MRIReferenceKind.Symbol : MRIReferenceKind.Content, owner);
            AddReference(reference); return reference;
        }
        internal void AddReference(MRIResourceReference reference)
        {
            var owner = reference.Owner;
            Change(() => { _session.IndexReference(reference); if (owner.ReferenceItems == null) owner.ReferenceItems = new List<MRIResourceReference>(); owner.ReferenceItems.Add(reference); },
                () => { owner.ReferenceItems.Remove(reference); _session.UnindexReference(reference); });
        }
        public void Remove(MRIResourceReference reference)
        {
            Demand();
            if (reference == null || _session.FindReference(reference.Id) != reference) throw new InvalidOperationException("Unknown reference");
            var owner = reference.Owner; int position = owner.ReferenceItems.IndexOf(reference);
            Change(() => { owner.ReferenceItems.RemoveAt(position); _session.UnindexReference(reference); }, () => { _session.IndexReference(reference); owner.ReferenceItems.Insert(position, reference); });
        }
        private void Geometry(MRCVisualItem item, double x, double y, double width, double height, double angle, double sx, double sy)
        {
            Demand(); _session.DemandNode(item);
            double ox = item.X, oy = item.Y, ow = item.Width, oh = item.Height, oa = item.Rotation, osx = item.ScaleX, osy = item.ScaleY;
            if (x == ox && y == oy && width == ow && height == oh && angle == oa && sx == osx && sy == osy) return;
            Change(() => item.SetGeometry(x, y, width, height, angle, sx, sy), () => item.SetGeometry(ox, oy, ow, oh, oa, osx, osy));
        }
        public void Move(MRCVisualItem item, double x, double y) { Geometry(item, x, y, item.Width, item.Height, item.Rotation, item.ScaleX, item.ScaleY); }
        public void Rotate(MRCVisualItem item, double angle) { Geometry(item, item.X, item.Y, item.Width, item.Height, angle, item.ScaleX, item.ScaleY); }
        public void Scale(MRCVisualItem item, double sx, double sy) { Geometry(item, item.X, item.Y, item.Width, item.Height, item.Rotation, sx, sy); }
        public void Resize(MRCVisualItem item, double width, double height)
        {
            // Group coordinates remain local: resizing scales its transform, never rewrites its descendants.
            if (item is MRCGroup)
            {
                MRCVisualItem.Finite(width); MRCVisualItem.Finite(height);
                if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("size");
                Scale(item, width / item.Width, height / item.Height);
            }
            else Geometry(item, item.X, item.Y, width, height, item.Rotation, item.ScaleX, item.ScaleY);
        }
        public void SetBinding(MRCVisualItem item, string property, MPMBinding binding)
        {
            Demand(); _session.DemandNode(item); property = MEMNode.NormalizeName(property); var old = item.GetBinding(property);
            Change(() => AssignBinding(item, property, binding), () => AssignBinding(item, property, old));
        }
        private static void AssignBinding(MRCVisualItem item, string property, MPMBinding value)
        {
            if (value == null) { if (item.BindingItems != null) item.BindingItems.Remove(property); return; }
            if (item.BindingItems == null) item.BindingItems = new Dictionary<string, MPMBinding>(StringComparer.Ordinal);
            item.BindingItems[property] = value;
        }
        public void SetVisibility(MRCVisualItem item, bool visible, bool selectable)
        {
            Demand(); _session.DemandNode(item); bool ov = item.IsVisible, os = item.IsSelectable;
            Change(() => { item.IsVisible = visible; item.IsSelectable = selectable; }, () => { item.IsVisible = ov; item.IsSelectable = os; });
        }
    }
}
