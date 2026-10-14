using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>UI无关的编辑命令与每文档Undo/Redo；Core仅掌握数据事务。</summary>
    public sealed class CxVisualEditor
    {
        private sealed class HistoryEntry
        {
            public List<CxChangeOperation> Forward;
            public List<CxChangeOperation> Inverse;
        }
        private readonly CxEngineeringSession _session;
        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();
        private int _cursor;

        public CxVisualEditor(CxEngineeringSession session)
        { _session = session ?? throw new ArgumentNullException("session"); }
        public CxProjectDocument Document { get { return _session.Document; } }
        public bool CanUndo { get { return _cursor > 0; } }
        public bool CanRedo { get { return _cursor < _history.Count; } }
        public ReadOnlyCollection<CxSceneItem> Scene { get { return CxSceneProjection.Project(Document); } }

        public CxCommitResult AddRectangle(string name, double x, double y, double width, double height)
        {
            CxGroup group = CxDocumentFactory.FirstGroup(Document);
            if (group == null) throw new InvalidOperationException("Document has no drawing group");
            CxElement element = new CxElement(Guid.NewGuid(), name, x, y, width, height);
            return Run(new[] { CxChangeOperation.Add(group.Id, element) });
        }
        public CxCommitResult Move(Guid id, double x, double y)
        { return Run(new[] { CxChangeOperation.Move(id, x, y) }); }
        public CxCommitResult Remove(Guid id)
        { return Run(new[] { CxChangeOperation.Remove(id) }); }
        private CxCommitResult Run(IList<CxChangeOperation> operations)
        {
            CxCommitResult result = _session.Apply(operations, Document.Revision);
            if (result.Success)
            {
                if (_cursor < _history.Count) _history.RemoveRange(_cursor, _history.Count - _cursor);
                _history.Add(new HistoryEntry { Forward = new List<CxChangeOperation>(operations),
                    Inverse = new List<CxChangeOperation>(result.InverseOperations) });
                _cursor++;
            }
            return result;
        }
        public CxCommitResult Undo()
        {
            if (!CanUndo) return Empty("NO_UNDO");
            HistoryEntry entry = _history[_cursor - 1];
            CxCommitResult result = _session.Apply(entry.Inverse, Document.Revision);
            if (result.Success) _cursor--;
            return result;
        }
        public CxCommitResult Redo()
        {
            if (!CanRedo) return Empty("NO_REDO");
            HistoryEntry entry = _history[_cursor];
            CxCommitResult result = _session.Apply(entry.Forward, Document.Revision);
            if (result.Success) _cursor++;
            return result;
        }
        private CxCommitResult Empty(string code)
        { return CxCommitResult.Reject(code, Document.Revision); }
    }
}
