using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Core
{
    public sealed class MTCEditResult<T>
    {
        internal MTCEditResult(bool success, T value, int revision, string error, string message)
        { Success = success; Value = value; Revision = revision; ErrorCode = error; Message = message; }
        public bool Success { get; private set; }
        public T Value { get; private set; }
        public int Revision { get; private set; }
        public string ErrorCode { get; private set; }
        public string Message { get; private set; }
        public static MTCEditResult<T> Rejected(int revision,string code,string message){return new MTCEditResult<T>(false,default(T),revision,code,message);}
        public T RequireValue() { if (!Success) throw new InvalidOperationException(ErrorCode + ": " + Message); return Value; }
    }
    public sealed class MTCChangeSet
    {
        internal MTCChangeSet(string intent, int before, int after, IEnumerable<MTCMutation> changes, bool undo = false)
        { Intent = intent; BeforeRevision = before; AfterRevision = after; Operations = changes.Select(c => undo ? c.Info.Reversed() : c.Info).ToList().AsReadOnly(); }
        public string Intent { get; private set; }
        public int BeforeRevision { get; private set; }
        public int AfterRevision { get; private set; }
        public IReadOnlyList<MTCChangeOperation> Operations { get; private set; }
    }
    public sealed class MVEValidationReport
    {
        internal MVEValidationReport(IList<string> issues) { Issues = new System.Collections.ObjectModel.ReadOnlyCollection<string>(issues); }
        public IReadOnlyList<string> Issues { get; private set; }
        public bool IsValid { get { return Issues.Count == 0; } }
    }
    internal sealed class MTCMutation
    {
        internal Action Forward, Inverse;
        internal readonly MTCChangeOperation Info;
        internal MTCMutation(Action forward, Action inverse, MTCChangeOperation info) { Forward = forward; Inverse = inverse; Info = info; }
    }
    public static class MEMEngineering
    {
        public static MEMEngineeringSession Create(string name,IMEMProjectStore store=null,IMEMAccessPolicy policy=null) { var session=new MEMEngineeringSession(new MEMProject(Guid.NewGuid(), name), 0);session.UseStore(store);session.UseAccessPolicy(policy);session.DemandAccess(MEMAccessOperation.Edit);return session; }
        public static MEMEngineeringSession Open(string path,IMEMProjectStore store=null,IMEMAccessPolicy policy=null)
        {
            if(policy!=null&&!policy.IsAllowed(new MEMAccessRequest(MEMAccessOperation.Open,Guid.Empty,System.IO.Path.GetFullPath(path))))throw new UnauthorizedAccessException("Engineering open denied");
            store=store??MEMFileProjectStore.Default;var session=store.Load(path);session.UseStore(store);session.UseAccessPolicy(policy);try{session.DemandAccess(MEMAccessOperation.Read);return session;}catch{session.Close(true);throw;}
        }
        public static Task<MEMEngineeringSession> OpenAsync(string path, CancellationToken cancellation)
        { return Task.Run(() => { cancellation.ThrowIfCancellationRequested(); return Open(path); }, cancellation); }
    }
    /// <summary>One authoritative tree, index, write gate and history. Returned nodes are read-only live identities.</summary>
    public sealed partial class MEMEngineeringSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Guid, MEMNode> _index = new Dictionary<Guid, MEMNode>();
        private readonly Dictionary<Guid, MRIResourceReference> _references = new Dictionary<Guid, MRIResourceReference>();
        private readonly Dictionary<Guid, HashSet<MRIResourceReference>> _usages = new Dictionary<Guid, HashSet<MRIResourceReference>>();
        private readonly List<List<MTCMutation>> _history = new List<List<MTCMutation>>();
        private readonly List<Action<MTCChangeSet>> _observers = new List<Action<MTCChangeSet>>();
        private readonly Queue<MTCChangeSet> _notifications = new Queue<MTCChangeSet>();
        private int _cursor, _callbackThread;
        private bool _editing, _delivering, _closed, _historyFaulted;
        private int? _savedRevision;
        internal MEMEngineeringSession(MEMProject project, int revision)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (revision < 0) throw new ArgumentOutOfRangeException("revision");
            Project = project; Revision = revision;
            if (project.Session != null) throw new InvalidOperationException("Project already has a document");
            AddIndex(project); project.Session = this;
            var report = Validate();
            if (!report.IsValid) { project.Session = null; throw new InvalidOperationException(string.Join(";", report.Issues)); }
        }
        private MEMProject _project;
        public MEMProject Project { get { DemandAccess(MEMAccessOperation.Read);return _project; } private set{_project=value;} }
        public int Revision { get; private set; }
        public string Location { get; private set; }
        public bool IsDirty { get { lock (_gate) return _savedRevision != Revision; } }
        public bool IsClosed { get { lock (_gate) return _closed; } }
        public int Count { get { DemandAccess(MEMAccessOperation.Read);lock (_gate) { DemandOpen(); return _index.Count; } } }
        public bool CanUndo { get { lock (_gate) return !_closed && !_historyFaulted && _cursor > 0; } }
        public bool CanRedo { get { lock (_gate) return !_closed && !_historyFaulted && _cursor < _history.Count; } }
        public Exception LastObserverError { get; private set; }
        internal void DemandOpen() { if (_closed) throw new InvalidOperationException("org.session.closed"); }
        internal void DemandNode(MEMNode node)
        { MEMNode found; if (node == null || !_index.TryGetValue(node.Id, out found) || found != node) throw new InvalidOperationException("org.node.foreign_or_missing"); }
        public MEMNode Find(Guid id) { DemandAccess(MEMAccessOperation.Read);lock (_gate) { DemandOpen(); MEMNode node; return _index.TryGetValue(id, out node) ? node : null; } }
        public MRIResourceReference FindReference(Guid id) { DemandAccess(MEMAccessOperation.Read);lock (_gate) { DemandOpen(); MRIResourceReference value; return _references.TryGetValue(id, out value) ? value : null; } }
        public MEMResourceEntry GetResourceEntry(Guid id)
        {
            DemandAccess(MEMAccessOperation.Read);lock(_gate)
            {
                var resource=Project.FindResource(id,true);if(resource==null)return null;var asset=resource as MEMAssetReference;
                if(asset!=null)return new MEMResourceEntry(asset,asset.ContentHash,1,new Guid[0]);
                var node=(MEMNode)resource;var type=FindType(resource.TypeId);var dependencies=new HashSet<Guid>();
                foreach(var owned in node.DescendantsAndSelf())
                {
                    var references=MRIReferenceList.View(owned);foreach(var reference in references)dependencies.Add(reference.TargetId);
                    var records=new List<object>();records.AddRange(MPMDefinitionState.PropertiesOf(owned));records.AddRange(MPMDefinitionState.PortsOf(owned));records.AddRange(MPMDefinitionState.ConnectionsOf(owned));records.AddRange(MPMDefinitionState.MappingsOf(owned));
                    records.AddRange(references);
                    var visual=owned as MRCVisualItem;if(visual!=null&&visual.BindingItems!=null)foreach(var binding in visual.BindingItems)records.Add(Tuple.Create(owned.Id,binding.Key));
                    foreach(var record in records){Guid[] targets;if(!_declarationDependencies.TryGetValue(record,out targets))continue;foreach(var target in targets){var targetNode=Find(target);if(targetNode!=null&&targetNode.ResourceRootId!=node.ResourceRootId)dependencies.Add(targetNode.ResourceRootId);if(FindAsset(target)!=null)dependencies.Add(target);}}
                }
                return new MEMResourceEntry(resource,MEMFileProjectStore.ResourceHash(node),type==null?1:type.SchemaVersion,dependencies.Where(dep=>dep!=resource.Id));
            }
        }
        private static void CheckSupportedType(MEMNode node)
        {
            var t = node.GetType();
            if (t != typeof(MEMPage) && t != typeof(MEMContent) && t != typeof(MEMSymbol) && t != typeof(MRCGroup) && t != typeof(MRCRectangle)
)
                throw new InvalidOperationException("org.type.unsupported: H01 requires a registered built-in type; custom payload persistence is a later phase");
        }
        internal void CheckNew(MEMNode node)
        {
            if (node == null || node.Parent != null || node.OwnerSession != null) throw new InvalidOperationException("Object must be detached");
            if (node.Children.Count == 0)
            {
                CheckSupportedType(node);
                if (_index.Count >= MEMNode.MaximumNodeCount) throw new InvalidOperationException("org.node_budget.exceeded");
                if (IdentityExists(node.Id)) throw new InvalidOperationException("org.id.duplicate");
                if(MPMDefinitionState.For(node)!=null&&MPMDefinitionState.For(node).HasData)throw new InvalidOperationException("Attach empty definitions before adding declarations");
                if (MRIReferenceList.View(node).Count != 0) throw new InvalidOperationException("Attach definitions before references");
                return;
            }
            var seen = new HashSet<Guid>();
            foreach (var child in node.DescendantsAndSelf())
            {
                CheckSupportedType(child);
                if (!seen.Add(child.Id) || IdentityExists(child.Id)) throw new InvalidOperationException("org.id.duplicate");
                if(MPMDefinitionState.For(child)!=null&&MPMDefinitionState.For(child).HasData)throw new InvalidOperationException("Attach empty definitions before adding declarations");
                foreach (var owned in child.Children) if (owned.Parent != child) throw new InvalidOperationException("Wrong subtree owner");
                if (MRIReferenceList.View(child).Count != 0) throw new InvalidOperationException("Use explicit reference creation after attaching definitions");
            }
            if ((long)_index.Count + seen.Count > MEMNode.MaximumNodeCount) throw new InvalidOperationException("org.node_budget.exceeded");
        }
        internal void AddIndex(MEMNode root)
        { if (root.Children.Count == 0) { _index.Add(root.Id, root); return; } foreach (var node in root.DescendantsAndSelf()) _index.Add(node.Id, node); }
        internal void RemoveIndex(MEMNode root)
        {
            UnindexDeclarationTree(root);
            foreach (var node in root.DescendantsAndSelf())
            {
                foreach (var reference in MRIReferenceList.View(node)) UnindexReference(reference);
                _index.Remove(node.Id);
            }
        }
        internal void RestoreIndex(MEMNode root)
        { AddIndex(root); foreach (var owner in root.DescendantsAndSelf()) foreach (var reference in MRIReferenceList.View(owner)) IndexReference(reference); IndexDeclarationTree(root); }
        internal void CheckRemove(MEMNode root)
        {
            if (root.Parent == null || root.Parent is MEMPage || root.Parent is MEMSymbol) throw new InvalidOperationException("org.root.protected");
            var removed = new HashSet<MEMNode>(root.DescendantsAndSelf());
            CheckDeclarationRemove(removed);
            foreach (var node in removed)
            {
                HashSet<MRIResourceReference> uses;
                if (_usages.TryGetValue(node.Id, out uses) && uses.Any(r => !removed.Contains(r.Owner))) throw new InvalidOperationException("org.reference.used");
            }
        }
        internal void IndexReference(MRIResourceReference reference)
        {
            if (IdentityExists(reference.Id)) throw new InvalidOperationException("org.id.duplicate");
            _references.Add(reference.Id, reference);
            HashSet<MRIResourceReference> uses;
            if (!_usages.TryGetValue(reference.TargetId, out uses)) { uses = new HashSet<MRIResourceReference>(); _usages.Add(reference.TargetId, uses); }
            uses.Add(reference);
            IndexUsage(reference,reference.Overrides.Values.SelectMany(BindingTargets));
        }
        internal void UnindexReference(MRIResourceReference reference)
        { UnindexUsage(reference); _references.Remove(reference.Id); var uses = _usages[reference.TargetId]; uses.Remove(reference); if (uses.Count == 0) _usages.Remove(reference.TargetId); }
        internal void CheckReference(MEMNode owner, MEMNode definition)
        {
            DemandNode(owner); DemandNode(definition);
            if(!(owner is MEMContent||owner is MEMSymbol))throw new InvalidOperationException("org.reference.owner_requires_content_or_symbol");
            if (!(definition is MEMContent || definition is MEMSymbol) || !(definition.Parent is MEMProject)) throw new InvalidOperationException("Reference target must be a shared content/symbol definition");
            if (Reaches(definition, owner)) throw new InvalidOperationException("org.reference.cycle");
        }
        internal bool HasUsages(Guid id) { return _usages.ContainsKey(id); }
        internal bool Reaches(MEMNode source, MEMNode target)
        {
            if (_references.Count == 0)
            { for (var n=target;n!=null;n=n.Parent) if(n==source)return true; return false; }
            var todo = new Stack<MEMNode>(); var seen = new HashSet<Guid>(); todo.Push(source);
            while (todo.Count != 0)
            {
                var n = todo.Pop(); if (n == target) return true;
                if (!seen.Add(n.Id)) continue;
                foreach (var child in n.Children) todo.Push(child);
                foreach (var reference in MRIReferenceList.View(n)) todo.Push(_index[reference.TargetId]);
            }
            return false;
        }
        public MTCEditResult<T> Edit<T>(string intent, Func<MTCEditSession, T> build) { return Edit(intent, Revision, build); }
        public MTCEditResult<bool> Edit(string intent, Action<MTCEditSession> build)
        { return Edit(intent, e => { build(e); return true; }); }
        public MTCEditResult<T> Edit<T>(string intent, int expectedRevision, Func<MTCEditSession, T> build)
        {
            DemandAccess(MEMAccessOperation.Edit);
            if (build == null) throw new ArgumentNullException("build");
            MEMNode.NormalizeName(intent); T value; int before, committedRevision;
            lock (_gate)
            {
                DemandOpen();
                if (_editing || _callbackThread == Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("org.edit.reentrant");
                if (Volatile.Read(ref _collectingTypes)!=0||Volatile.Read(ref _authorizing)!=0) throw new InvalidOperationException("org.callback.cannot_edit");
                if (_historyFaulted) throw new InvalidOperationException("org.history.faulted");
                if (Revision != expectedRevision) return new MTCEditResult<T>(false, default(T), Revision, "org.version.conflict", "Stale revision");
                if (Revision == int.MaxValue) throw new InvalidOperationException("Revision exhausted");
                before = Revision; _editing = true;
                var draft = new MTCEditSession(this);
                try
                {
                    value = build(draft);
                    if (draft.Changes.Count != 0)
                    {
                        if (_cursor < _history.Count) _history.RemoveRange(_cursor, _history.Count - _cursor);
                        _history.Add(draft.Changes); _cursor++; Revision++;
                        if (_observers.Count != 0) _notifications.Enqueue(new MTCChangeSet(intent, before, Revision, draft.Changes));
                    }
                    committedRevision = Revision;
                }
                catch (Exception error)
                {
                    try { for (int i = draft.Changes.Count - 1; i >= 0; i--) draft.Changes[i].Inverse(); }
                    catch (Exception rollback) { _historyFaulted = true; throw new AggregateException("org.rollback.failed", error, rollback); }
                    if (error is ArgumentException || error is InvalidOperationException)
                        return new MTCEditResult<T>(false, default(T), Revision, "org.edit.rejected", error.Message);
                    throw;
                }
                finally { draft.Complete(); _editing = false; }
            }
            Deliver();
            return new MTCEditResult<T>(true, value, committedRevision, null, null);
        }
        private MTCEditResult<bool> Replay(bool undo)
        {
            DemandAccess(MEMAccessOperation.Edit);
            int committedRevision;
            lock (_gate)
            {
                DemandOpen();
                if (_editing || _callbackThread == Thread.CurrentThread.ManagedThreadId || _historyFaulted || Volatile.Read(ref _collectingTypes)!=0 || Volatile.Read(ref _authorizing)!=0) throw new InvalidOperationException("History unavailable");
                if (undo ? _cursor == 0 : _cursor == _history.Count) return new MTCEditResult<bool>(false, false, Revision, "org.history.empty", "No entry");
                if (Revision == int.MaxValue) throw new InvalidOperationException("Revision exhausted");
                var batch = _history[undo ? _cursor - 1 : _cursor]; var done = new List<MTCMutation>(); _editing = true;
                try
                {
                    foreach (var change in undo ? batch.AsEnumerable().Reverse() : batch)
                    { if (undo) change.Inverse(); else change.Forward(); done.Add(change); }
                    int before = Revision; _cursor += undo ? -1 : 1; Revision++;
                    if (_observers.Count != 0) _notifications.Enqueue(new MTCChangeSet(undo ? "撤销" : "重做", before, Revision, undo ? batch.AsEnumerable().Reverse() : batch, undo));
                    committedRevision = Revision;
                }
                catch (Exception failure)
                {
                    _historyFaulted = true;
                    try { foreach (var change in done.AsEnumerable().Reverse()) { if (undo) change.Forward(); else change.Inverse(); } }
                    catch (Exception rollback) { throw new AggregateException("History rollback failed", failure, rollback); }
                    throw;
                }
                finally { _editing = false; }
            }
            Deliver(); return new MTCEditResult<bool>(true, true, committedRevision, null, null);
        }
        public void ClearHistory()
        { DemandAccess(MEMAccessOperation.Edit);lock (_gate) { DemandOpen(); if (_editing || _delivering || _historyFaulted || Volatile.Read(ref _collectingTypes)!=0 || Volatile.Read(ref _authorizing)!=0) throw new InvalidOperationException("History busy/faulted"); _history.Clear(); _cursor=0; } }
        public MTCEditResult<bool> Undo() { return Replay(true); }
        public MTCEditResult<bool> Redo() { return Replay(false); }
        public IDisposable Watch(Action<MTCChangeSet> observer)
        { DemandAccess(MEMAccessOperation.Read);if (observer == null) throw new ArgumentNullException("observer"); lock (_gate) { DemandOpen(); _observers.Add(observer); } return new Subscription(this, observer); }
        private sealed class Subscription : IDisposable
        {
            private MEMEngineeringSession _owner; private readonly Action<MTCChangeSet> _observer;
            internal Subscription(MEMEngineeringSession owner, Action<MTCChangeSet> observer) { _owner = owner; _observer = observer; }
            public void Dispose() { var s = Interlocked.Exchange(ref _owner, null); if (s != null) lock (s._gate) s._observers.Remove(_observer); }
        }
        private void Deliver()
        {
            lock (_gate) { if (_delivering) return; _delivering = true; }
            bool ownsDelivery = true;
            try
            {
                while (true)
                {
                    MTCChangeSet change; Action<MTCChangeSet>[] observers;
                    lock (_gate) { if (_notifications.Count == 0) { _delivering = false; ownsDelivery = false; return; } change = _notifications.Dequeue(); observers = _observers.ToArray(); }
                    foreach (var observer in observers)
                    {
                        lock (_gate) { if (!_observers.Contains(observer)) continue; _callbackThread = Thread.CurrentThread.ManagedThreadId; }
                        try { observer(change); } catch (Exception error) { LastObserverError = error; }
                        finally { lock (_gate) _callbackThread = 0; }
                    }
                }
            }
            finally { if (ownsDelivery) lock (_gate) _delivering = false; }
        }
        public MVEValidationReport Validate()
        {
            DemandAccess(MEMAccessOperation.Read);
            lock (_gate)
            {
                DemandOpen(); var issues = new List<string>(); var seen = new HashSet<Guid>();
                foreach (var node in Project.DescendantsAndSelf())
                {
                    MEMNode indexed;
                    if (!seen.Add(node.Id) || !_index.TryGetValue(node.Id, out indexed) || indexed != node) issues.Add("org.index.inconsistent");
                    foreach (var child in node.Children) if (child.Parent != node) issues.Add("org.owner.inconsistent");
                    var page = node as MEMPage; var symbol = node as MEMSymbol;
                    if (page != null && (page.Children.Count != 1 || page.RootContent == null)) issues.Add("org.page.root");
                    if (symbol != null && (symbol.Children.Count != 1 || symbol.RootGroup == null)) issues.Add("org.symbol.root");
                }
                if (seen.Count != _index.Count) issues.Add("org.index.orphan");
                if(Project.AssetItems!=null)foreach(var asset in Project.AssetItems.Values)if(!seen.Add(asset.Id)||_ports.ContainsKey(asset.Id)||_connections.ContainsKey(asset.Id)||_references.ContainsKey(asset.Id))issues.Add("org.asset.identity_conflict");
                foreach (var reference in _references.Values)
                { try { CheckReference(reference.Owner, _index[reference.TargetId]); } catch (Exception e) { issues.Add(e.Message); } }
                ValidateDeclarations(issues);
                return new MVEValidationReport(issues);
            }
        }
        public MEMPage AddPage(string name) { return Edit("添加页面", e => e.AddPage(name)).RequireValue(); }
        public MEMContent AddContent(MEMNode parent, string name) { return Edit("添加内容", e => e.AddContent(parent, name)).RequireValue(); }
        public MEMSymbol AddSymbol(string name) { return Edit("添加符号", e => e.AddSymbol(name)).RequireValue(); }
        public MRCGroup AddGroup(MEMNode parent, string name) { return Edit("添加组", e => e.AddGroup(parent, name)).RequireValue(); }
        public MRCRectangle AddRectangle(MEMNode parent, string name, double x, double y, double width = 120, double height = 80)
        { return Edit("添加矩形", e => e.AddRectangle(parent, name, x, y, width, height)).RequireValue(); }
        public MEMNode Add(MEMNode parent, MEMNode node) { return Edit("添加对象", e => e.Add(parent, node)).RequireValue(); }
        public void Rename(MEMNode node, string name) { Edit("重命名", e => e.Rename(node, name)).RequireValue(); }
        public void Reparent(MEMNode node, MEMNode parent, int index = -1) { Edit("移动所有权", e => e.Reparent(node, parent, index)).RequireValue(); }
        public void Remove(MEMNode node) { Edit("删除对象", e => e.Remove(node)).RequireValue(); }
        public void Remove(MRIResourceReference reference) { Edit("删除引用", e => e.Remove(reference)).RequireValue(); }
        public MRIResourceReference PlaceSymbol(MEMNode owner, MEMSymbol definition, string name) { return Edit("放置符号", e => e.Reference(owner, definition, name)).RequireValue(); }
        public MRIResourceReference PlaceContent(MEMNode owner, MEMContent definition, string name) { return Edit("放置内容", e => e.Reference(owner, definition, name)).RequireValue(); }
        public void Move(MRCVisualItem item, double x, double y) { Edit("移动图元", e => e.Move(item, x, y)).RequireValue(); }
        public void Resize(MRCVisualItem item, double width, double height) { Edit("缩放图元", e => e.Resize(item, width, height)).RequireValue(); }
        public void Rotate(MRCVisualItem item, double angle) { Edit("旋转图元", e => e.Rotate(item, angle)).RequireValue(); }
        public void Scale(MRCVisualItem item, double x, double y) { Edit("变换图元", e => e.Scale(item, x, y)).RequireValue(); }
        public void SetBinding(MRCVisualItem item, string property, MPMBinding binding) { Edit("设置绑定", e => e.SetBinding(item, property, binding)).RequireValue(); }
        public void SetVisibility(MRCVisualItem item, bool visible, bool selectable) { Edit("设置可视属性", e => e.SetVisibility(item, visible, selectable)).RequireValue(); }
        public void SaveAs(string path) { SaveAs(path, CancellationToken.None); }
        public void SaveAs(string path, CancellationToken cancellation)
        { string full,expected;var snapshot=CaptureSave(path,cancellation,out full,out expected);SaveCaptured(snapshot,full,expected,cancellation); }
        public void Save() { if (Location == null) throw new InvalidOperationException("SaveAs first"); SaveAs(Location); }
        public Task SaveAsync(CancellationToken cancellation)
        {
            string path;
            lock (_gate)
            {
                DemandOpen();
                if (_editing || Volatile.Read(ref _collectingTypes)!=0) throw new InvalidOperationException("No IO in edit/provider callback");
                path = Location; if (path == null) throw new InvalidOperationException("SaveAs first");
            }
            return SaveAsAsync(path,cancellation);
        }
        internal void MarkLoaded(string path,string token) { Location = System.IO.Path.GetFullPath(path); _savedRevision = Revision;StoreToken=token; }
        public void Close(bool discardChanges = false)
        {
            lock (_gate)
            {
                if (_closed) return;
                if (_editing || _delivering || _saving || Volatile.Read(ref _collectingTypes)!=0 || Volatile.Read(ref _authorizing)!=0) throw new InvalidOperationException("Session busy");
                if (IsDirty && !discardChanges) throw new InvalidOperationException("Unsaved changes; save or explicitly discard");
                _closed = true; _observers.Clear(); _history.Clear(); _index.Clear(); _references.Clear(); _usages.Clear();
                _ports.Clear();_connections.Clear();_declarationUses.Clear();_declarationDependencies.Clear();_incoming.Clear();_outgoing.Clear();
                Project.Session = null;
            }
        }
        public void Dispose() { Close(); }
    }
}
