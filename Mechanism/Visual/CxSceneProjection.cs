using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Core;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    public static class CxSceneProjection
    {
        public static ReadOnlyCollection<CxSceneItem> Project(CxProjectDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            List<CxSceneItem> result = new List<CxSceneItem>();
            Collect(document.Project, result);
            return result.AsReadOnly();
        }
        public static Guid? HitTest(IList<CxSceneItem> items, CxPoint2D point)
        {
            if (items == null) return null;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].Bounds.Contains(point)) return items[i].Id;
            return null;
        }
        private static void Collect(CxNode node, List<CxSceneItem> items)
        {
            CxElement element = node as CxElement;
            if (element != null) items.Add(new CxSceneItem(element));
            foreach (CxNode child in node.Children) Collect(child, items);
        }
    }
}
