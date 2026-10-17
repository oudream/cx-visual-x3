using System;
using System.Collections.Generic;
namespace CxVisual.Mechanism.Core
{
    /// <summary>Compatibility document and the single session share exactly the same root.</summary>
    public sealed class CxProjectDocument
    {
        public CxProjectDocument(CxProject project, int revision)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (revision < 0) throw new ArgumentOutOfRangeException("revision");
            Project = project; Session = new MEMEngineeringSession(this, revision);
        }
        public CxProject Project { get; private set; }
        public MEMEngineeringSession Session { get; private set; }
        public int Revision { get { return Session.Revision; } }
        public CxCommitResult Commit(IList<CxChangeOperation> operations, int expectedRevision)
        {
            if (expectedRevision != Revision) return CxCommitResult.Reject("REVISION_CONFLICT", Revision);
            if (operations == null || operations.Count == 0) return CxCommitResult.Reject("EMPTY_BATCH", Revision);
            var inverses = new List<CxChangeOperation>();
            var result = Session.Edit("兼容矩形编辑", expectedRevision, e =>
            {
                foreach (var op in operations)
                {
                    if (op == null) throw new InvalidOperationException("Null operation");
                    if (op.Kind == CxChangeKind.AddElement)
                    {
                        var parent = Session.Find(op.ParentId);
                        if (!(parent is MRCGroup)) throw new InvalidOperationException("Element parent must be Group");
                        e.Add(parent, op.Element); inverses.Insert(0, CxChangeOperation.Remove(op.Id));
                    }
                    else
                    {
                        var element = Session.Find(op.Id) as CxElement;
                        if (element == null) throw new InvalidOperationException("Missing legacy element");
                        if (op.Kind == CxChangeKind.RemoveElement) { var parent = element.Parent; e.Remove(element); inverses.Insert(0, CxChangeOperation.Add(parent.Id, element)); }
                        else if (op.Kind == CxChangeKind.MoveElement) { double x=element.X,y=element.Y; e.Move(element, op.X, op.Y); inverses.Insert(0, CxChangeOperation.Move(element.Id, x, y)); }
                        else throw new InvalidOperationException("Unsupported operation");
                    }
                }
                return true;
            });
            return new CxCommitResult(result.Success, result.Success ? "OK" : result.ErrorCode == "org.version.conflict" ? "REVISION_CONFLICT" : "INVALID_CHANGE", result.Message, result.Revision, result.Success ? inverses : new List<CxChangeOperation>());
        }
        internal void ValidateTree() { var report = Session.Validate(); if (!report.IsValid) throw new InvalidOperationException(string.Join(";", report.Issues)); }
    }
}
