using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Cases
{
    internal sealed class TCTDelegateTestCase : ITCTTestCase
    {
        private readonly Action<TCTTestContext> _execute;
        public TCTDelegateTestCase(TCTTestCaseDescriptor descriptor, Action<TCTTestContext> execute)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException("descriptor");
            _execute = execute ?? throw new ArgumentNullException("execute");
        }
        public TCTTestCaseDescriptor Descriptor { get; private set; }
        public void Execute(TCTTestContext context) { _execute(context); }
    }

    // 仅调用仓库已经存在的生产类型，不生成演示成功状态。
    public static class TCTRealCases
    {
        public static void Register(TCTTestCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            Add(catalog, "F-GEO-001", "Rectangle Contains 边界", TCTTestAspect.Foundation,
                "Geometry", "CA-VIS-01", "CAP-GEOMETRY-CONTAINS", "CxRect2D", Contains);
            Add(catalog, "F-GEO-002", "Rectangle Intersects 接触边界", TCTTestAspect.Foundation,
                "Geometry", "CA-VIS-01", "CAP-GEOMETRY-INTERSECTS", "CxRect2D", Intersects);
            Add(catalog, "F-GEO-003", "Affine2D 平移变换", TCTTestAspect.Foundation,
                "Geometry", "CA-VIS-01", "CAP-GEOMETRY-TRANSFORM", "CxAffine2D", Transform);
            Add(catalog, "M-OBJ-001", "六对象 Ownership 组织", TCTTestAspect.Mechanism,
                "Object / Ownership", "CA-ORG-01", "CAP-ORG-OWNERSHIP", "CxProjectDocument", Ownership);
            Add(catalog, "M-OBJ-002", "原子提交失败回滚", TCTTestAspect.Mechanism,
                "Object / Transaction", "CA-ORG-01", "CAP-ORG-ATOMIC", "CxProjectDocument", AtomicRollback);
            Add(catalog, "M-OBJ-003", "工程 XML 保存加载", TCTTestAspect.Mechanism,
                "Object / Persistence", "CA-ORG-01", "CAP-ORG-ROUNDTRIP", "CxDocumentSerializer", RoundTrip);
            Add(catalog, "M-OBJ-004", "Revision 冲突拒绝", TCTTestAspect.Mechanism,
                "Object / Transaction", "CA-ORG-01", "CAP-ORG-REVISION", "CxProjectDocument", RevisionConflict);
        }

        private static void Add(TCTTestCatalog catalog, string id, string name, TCTTestAspect aspect,
            string group, string capabilityArea, string capability, string objectId, Action<TCTTestContext> execute)
        {
            catalog.Register(new TCTDelegateTestCase(new TCTTestCaseDescriptor(id, name, aspect,
                group, capabilityArea, capability, objectId), execute));
        }

        private static void Contains(TCTTestContext context)
        {
            CxRect2D rect = new CxRect2D(10, 20, 20, 30);
            context.AssertTrue(rect.Contains(new CxPoint2D(10, 20)), "左上边界包含", true, rect.Contains(new CxPoint2D(10, 20)));
            context.AssertTrue(rect.Contains(new CxPoint2D(30, 50)), "右下边界包含", true, rect.Contains(new CxPoint2D(30, 50)));
            context.AssertTrue(!rect.Contains(new CxPoint2D(31, 50)), "界外点排除", false, rect.Contains(new CxPoint2D(31, 50)));
            context.Observe("Geometry", "Rectangle", "X=10 Y=20 Width=20 Height=30; 边界与外部点命中已由生产CxRect2D计算");
        }

        private static void Intersects(TCTTestContext context)
        {
            CxRect2D rect = new CxRect2D(10, 20, 20, 30);
            bool overlaps = rect.Intersects(new CxRect2D(20, 30, 10, 10));
            bool touchesOnly = rect.Intersects(new CxRect2D(30, 30, 5, 10));
            context.AssertTrue(overlaps, "面积交叠", true, overlaps);
            context.AssertTrue(!touchesOnly, "仅接触边界不是面积交叠", false, touchesOnly);
            context.Observe("Geometry", "Intersects", "重叠矩形(20,30,10,10)；贴边矩形(30,30,5,10)");
        }

        private static void Transform(TCTTestContext context)
        {
            CxPoint2D point = new CxAffine2D(1, 0, 0, 1, 30, 40).Transform(new CxPoint2D(2, 4));
            context.AssertTrue(point.X == 32, "变换X", 32, point.X);
            context.AssertTrue(point.Y == 44, "变换Y", 44, point.Y);
            context.Observe("Geometry", "Affine2D", "(2,4) 经平移(30,40) → (" + point.X + "," + point.Y + ")");
        }

        private static void Ownership(TCTTestContext context)
        {
            CxProjectDocument document = CxDocumentFactory.CreateSample();
            CxNode project = document.Project;
            CxNode page = project.Children[0];
            CxNode content = page.Children[0];
            CxNode symbol = content.Children[0];
            CxNode group = symbol.Children[0];
            context.AssertTrue(page.Kind == CxNodeKind.Page && page.Parent == project,
                "Page父子关系", "Page / Project", page.Kind + " / " + page.Parent.Kind);
            context.AssertTrue(content.Kind == CxNodeKind.Content && content.Parent == page,
                "Content父子关系", "Content / Page", content.Kind + " / " + content.Parent.Kind);
            context.AssertTrue(symbol.Kind == CxNodeKind.Symbol && group.Kind == CxNodeKind.Group,
                "Symbol-Group关系", "Symbol / Group", symbol.Kind + " / " + group.Kind);
            context.AssertTrue(group.Children.Count == 2 && group.Children[0].Kind == CxNodeKind.Element,
                "Element子节点数量", 2, group.Children.Count);
            context.Observe("ObjectTree", "Ownership", "Project → Page → Content → Symbol → Group → 2 Elements（按当前仓库样例验证）");
        }

        private static void AtomicRollback(TCTTestContext context)
        {
            CxProjectDocument document = CxDocumentFactory.CreateSample();
            CxGroup group = CxDocumentFactory.FirstGroup(document);
            int before = group.Children.Count;
            CxElement temporary = new CxElement(Guid.NewGuid(), "Temp", 5, 6, 20, 20);
            CxChangeOperation[] batch = {
                CxChangeOperation.Add(group.Id, temporary),
                CxChangeOperation.Move(Guid.NewGuid(), 1, 2)
            };
            CxCommitResult result = document.Commit(batch, document.Revision);
            context.AssertTrue(!result.Success, "无效批次拒绝", false, result.Success);
            context.AssertTrue(document.Revision == 0, "Revision未增加", 0, document.Revision);
            context.AssertTrue(group.Children.Count == before, "恢复子节点数量", before, group.Children.Count);
            context.AssertTrue(temporary.Parent == null, "临时节点脱离父节点", true, temporary.Parent == null);
            context.Observe("ObjectTree", "Atomic rollback", "首条Add成功，第二条Move失败，整体恢复至提交前");
        }

        private static void RoundTrip(TCTTestContext context)
        {
            CxProjectDocument document = CxDocumentFactory.CreateSample();
            string file = Path.Combine(Path.GetTempPath(), "CxVisual-TCT-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                CxDocumentSerializer.Save(document, file);
                CxProjectDocument restored = CxDocumentSerializer.Load(file);
                context.AssertTrue(restored.Project.Id == document.Project.Id,
                    "Project ID保持", document.Project.Id, restored.Project.Id);
                context.AssertTrue(restored.Revision == document.Revision,
                    "Revision保持", document.Revision, restored.Revision);
                int expected = CxDocumentFactory.FirstGroup(document).Children.Count;
                int actual = CxDocumentFactory.FirstGroup(restored).Children.Count;
                context.AssertTrue(actual == expected, "子元素数量保持", expected, actual);
                context.Observe("ObjectTree", "RoundTrip", "使用临时文件完成真实CxDocumentSerializer.Save/Load；退出后删除");
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        private static void RevisionConflict(TCTTestContext context)
        {
            CxProjectDocument document = CxDocumentFactory.CreateSample();
            CxGroup group = CxDocumentFactory.FirstGroup(document);
            CxElement temporary = new CxElement(Guid.NewGuid(), "Conflict", 1, 2, 3, 4);
            CxCommitResult rejected = document.Commit(new [] { CxChangeOperation.Add(group.Id, temporary) }, 42);
            context.AssertTrue(!rejected.Success, "冲突拒绝", false, rejected.Success);
            context.AssertTrue(rejected.ErrorCode == "REVISION_CONFLICT", "错误码",
                "REVISION_CONFLICT", rejected.ErrorCode);
            context.AssertTrue(document.Revision == 0 && temporary.Parent == null, "工程未变更", true,
                document.Revision == 0 && temporary.Parent == null);
        }
    }
}
