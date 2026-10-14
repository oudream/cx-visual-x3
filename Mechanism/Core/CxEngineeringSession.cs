using System;
using System.Collections.Generic;
namespace CxVisual.Mechanism.Core
{
    /// <summary>提供无UI的工程变更门面；未来连接授权、事件和多文档事务。</summary>
    public sealed class CxEngineeringSession
    {
        public CxEngineeringSession(CxProjectDocument document)
        { Document = document ?? throw new ArgumentNullException("document"); }
        public CxProjectDocument Document { get; private set; }
        public CxCommitResult Apply(IList<CxChangeOperation> operations, int expectedRevision)
        { return Document.Commit(operations, expectedRevision); }
    }
}
