using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Core
{
    /// <summary>统一工程真源，Revision事务提交不可被UI旁路。</summary>
    public sealed class CxProjectDocument
    {
        public CxProjectDocument(CxProject project, int revision)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (revision < 0) throw new ArgumentOutOfRangeException("revision");
            Project = project;
            Revision = revision;
            ValidateTree();
        }
        public CxProject Project { get; private set; }
        public int Revision { get; private set; }

        public CxCommitResult Commit(IList<CxChangeOperation> operations, int expectedRevision)
        {
            if (expectedRevision != Revision)
                return Failure("REVISION_CONFLICT", "Expected revision differs from current document revision");
            if (operations == null || operations.Count == 0)
                return Failure("EMPTY_BATCH", "At least one operation is required");
            List<CxChangeOperation> inverses = new List<CxChangeOperation>();
            try
            {
                foreach (CxChangeOperation operation in operations)
                {
                    if (operation == null) throw new InvalidOperationException("Null operation");
                    CxChangeOperation inverse = Apply(operation);
                    inverses.Insert(0, inverse);
                }
                ValidateTree();
            }
            catch (Exception ex)
            {
                // 逆序补偿同一内存树；不提升Revision；失败必须显式暴露。
                foreach (CxChangeOperation inverse in inverses)
                {
                    try { Apply(inverse); }
                    catch (Exception rollbackEx)
                    {
                        throw new InvalidOperationException("ROLLBACK_FAILED: invariants no longer guaranteed", rollbackEx);
                    }
                }
                return Failure("INVALID_CHANGE", ex.Message);
            }
            Revision++;
            return new CxCommitResult(true, "OK", "Committed", Revision, inverses);
        }

        private CxChangeOperation Apply(CxChangeOperation operation)
        {
            if (operation.Kind == CxChangeKind.AddElement)
            {
                CxNode parent = Project.Find(operation.ParentId);
                if (parent == null || parent.Kind != CxNodeKind.Group)
                    throw new InvalidOperationException("Element parent must be an owned Group");
                if (Project.Find(operation.Id) != null) throw new InvalidOperationException("Duplicate element id");
                parent.Attach(operation.Element);
                return CxChangeOperation.Remove(operation.Id);
            }
            CxElement element = Project.Find(operation.Id) as CxElement;
            if (element == null) throw new InvalidOperationException("Target element was not found");
            if (operation.Kind == CxChangeKind.RemoveElement)
            {
                CxNode parent = element.Parent;
                parent.Detach(element);
                return CxChangeOperation.Add(parent.Id, element);
            }
            if (operation.Kind == CxChangeKind.MoveElement)
            {
                double oldX = element.X;
                double oldY = element.Y;
                element.SetPosition(operation.X, operation.Y);
                return CxChangeOperation.Move(element.Id, oldX, oldY);
            }
            throw new InvalidOperationException("Unsupported change operation");
        }

        private CxCommitResult Failure(string error, string message)
        {
            return new CxCommitResult(false, error, message, Revision, new CxChangeOperation[0]);
        }

        internal void ValidateTree()
        {
            HashSet<Guid> ids = new HashSet<Guid>();
            Visit(Project, null, ids);
        }
        private static void Visit(CxNode node, CxNode parent, HashSet<Guid> ids)
        {
            if (node.Parent != parent) throw new InvalidOperationException("Parent ownership invariant failed");
            if (!ids.Add(node.Id)) throw new InvalidOperationException("Repeated node id");
            foreach (CxNode child in node.Children)
            {
                if (!CxNode.Allows(node.Kind, child.Kind)) throw new InvalidOperationException("Illegal node kind");
                Visit(child, node, ids);
            }
        }
    }
}
