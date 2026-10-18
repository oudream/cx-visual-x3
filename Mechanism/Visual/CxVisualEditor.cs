using System;
using System.Collections.ObjectModel;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    /// <summary>Presentation commands share the document session history; there is no private replay stack.</summary>
    public sealed class CxVisualEditor
    {
        private readonly MEMEngineeringSession _session;
        public CxVisualEditor(MEMEngineeringSession session) { _session = session ?? throw new ArgumentNullException("session"); }
        public MEMEngineeringSession Document { get { return _session; } }
        public bool CanUndo { get { return Document.CanUndo; } }
        public bool CanRedo { get { return Document.CanRedo; } }
        public ReadOnlyCollection<CxSceneItem> Scene { get { return CxSceneProjection.Project(Document); } }
        public MTCEditResult<bool> AddRectangle(string name, double x, double y, double width, double height)
        {
            return Document.Edit("添加矩形", e =>
            {
                var pages = Document.Project.Pages;
                if (pages.Count == 0) throw new InvalidOperationException("NO_PAGE");
                MEMNode parent = pages[0].RootContent;
                e.AddRectangle(parent, name, x, y, width, height);
                return true;
            });
        }
        public MTCEditResult<bool> Move(Guid id, double x, double y)
        { return Document.Edit("移动图元", e => { e.Move(RequireVisual(id), x, y); return true; }); }
        public MTCEditResult<bool> Remove(Guid id)
        { return Document.Edit("删除图元", e => { e.Remove(RequireVisual(id)); return true; }); }
        private MRCVisualItem RequireVisual(Guid id) { var item = Document.Find(id) as MRCVisualItem; if (item == null) throw new InvalidOperationException("Missing visual item"); return item; }
        public MTCEditResult<bool> Undo() { return Document.Undo(); }
        public MTCEditResult<bool> Redo() { return Document.Redo(); }
    }
}
