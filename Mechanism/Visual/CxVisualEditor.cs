using System;
using System.Collections.ObjectModel;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    /// <summary>Presentation commands share the document session history; there is no private replay stack.</summary>
    public sealed class CxVisualEditor
    {
        private readonly CxEngineeringSession _session;
        public CxVisualEditor(CxEngineeringSession session) { _session = session ?? throw new ArgumentNullException("session"); }
        public CxProjectDocument Document { get { return _session.Document; } }
        public bool CanUndo { get { return Document.Session.CanUndo; } }
        public bool CanRedo { get { return Document.Session.CanRedo; } }
        public ReadOnlyCollection<CxSceneItem> Scene { get { return CxSceneProjection.Project(Document); } }
        public CxCommitResult AddRectangle(string name, double x, double y, double width, double height)
        {
            var group = CxDocumentFactory.FirstGroup(Document);
            if (group != null) return _session.Apply(new[] { CxChangeOperation.Add(group.Id, new CxElement(Guid.NewGuid(), name, x, y, width, height)) }, Document.Revision);
            var pages = Document.Session.Project.Pages;
            if (pages.Count == 0) return CxCommitResult.Reject("NO_PAGE", Document.Revision);
            return Convert(Document.Session.Edit("添加矩形", e => e.AddRectangle(pages[0].RootContent, name, x, y, width, height) != null));
        }
        public CxCommitResult Move(Guid id, double x, double y)
        { return Convert(Document.Session.Edit("移动图元", e => e.Move(RequireVisual(id), x, y))); }
        public CxCommitResult Remove(Guid id)
        { return Convert(Document.Session.Edit("删除图元", e => e.Remove(RequireVisual(id)))); }
        private MRCVisualItem RequireVisual(Guid id) { var item = Document.Session.Find(id) as MRCVisualItem; if (item == null) throw new InvalidOperationException("Missing visual item"); return item; }
        public CxCommitResult Undo() { return Convert(Document.Session.Undo()); }
        public CxCommitResult Redo() { return Convert(Document.Session.Redo()); }
        private static CxCommitResult Convert(MTCEditResult<bool> result)
        { return CxCommitResult.FromEdit(result); }
    }
}
