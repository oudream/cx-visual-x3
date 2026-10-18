using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    public static class CxSceneProjection
    {
        public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            List<CxSceneItem> result = new List<CxSceneItem>();
            // Library definitions are not placed page instances. Preserve the existing all-page preview.
            foreach (var page in session.Project.Pages) Collect(page, result);
            return result.AsReadOnly();
        }
        public static Guid? HitTest(IList<CxSceneItem> items, CxPoint2D point)
        {
            if (items == null) return null;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].Contains(point.X, point.Y)) return items[i].Id;
            return null;
        }
        private static void Collect(MEMNode node, List<CxSceneItem> items)
        {
            var visual = node as MRCVisualItem;
            if (visual != null && !visual.IsVisible) return;
            MRCElement element = node as MRCElement;
            if (element != null && element.IsVisible) items.Add(new CxSceneItem(element));
            foreach (MEMNode child in node.Children) Collect(child, items);
        }
    }
}
