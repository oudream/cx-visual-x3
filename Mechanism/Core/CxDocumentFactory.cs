using System;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    /// <summary>离线示例，不引入专业设备状态。</summary>
    public static class CxDocumentFactory
    {
        public static CxProjectDocument CreateSample()
        {
            CxProject project = new CxProject(Guid.NewGuid(), "Demo Project");
            CxPage page = new CxPage(Guid.NewGuid(), "Main Page");
            CxContent content = new CxContent(Guid.NewGuid(), "Main Content");
            CxSymbol symbol = new CxSymbol(Guid.NewGuid(), "Rectangle Symbol");
            CxGroup group = new CxGroup(Guid.NewGuid(), "Drawing Group");
            group.Attach(new CxElement(Guid.NewGuid(), "Detector", 50, 65, 170, 100));
            group.Attach(new CxElement(Guid.NewGuid(), "Source", 300, 180, 125, 75));
            symbol.Attach(group);
            content.Attach(symbol);
            page.Attach(content);
            project.Attach(page);
            return new CxProjectDocument(project, 0);
        }
        public static CxGroup FirstGroup(CxProjectDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            return FindGroup(document.Project);
        }
        private static CxGroup FindGroup(CxNode node)
        {
            CxGroup group = node as CxGroup;
            if (group != null) return group;
            foreach (CxNode child in node.Children)
            {
                group = FindGroup(child);
                if (group != null) return group;
            }
            return null;
        }
    }
}
