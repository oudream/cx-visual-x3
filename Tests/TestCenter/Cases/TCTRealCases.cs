using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using CxVisual.TestCenter.Models;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
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
                "Geometry", "CA-VIS-01", "CAP-GEOMETRY-CONTAINS", "FGERect2D", Contains);
            Add(catalog, "F-GEO-002", "Rectangle Intersects 接触边界", TCTTestAspect.Foundation,
                "Geometry", "CA-VIS-01", "CAP-GEOMETRY-INTERSECTS", "FGERect2D", Intersects);
            Add(catalog, "F-GEO-003", "Affine2D 平移变换", TCTTestAspect.Foundation,
                "Geometry", "CA-VIS-01", "CAP-GEOMETRY-TRANSFORM", "FGETransform2D", Transform);
            Add(catalog, "M-OBJ-001", "六对象 Ownership 组织", TCTTestAspect.Mechanism,
                "Object / Ownership", "CA-ORG-01", "CAP-ORG-OWNERSHIP", "MEMEngineeringSession", Ownership);
            Add(catalog, "M-OBJ-002", "原子提交失败回滚", TCTTestAspect.Mechanism,
                "Object / Transaction", "CA-ORG-01", "CAP-ORG-ATOMIC", "MEMEngineeringSession", AtomicRollback);
            Add(catalog, "M-OBJ-003", "工程 XML 保存加载", TCTTestAspect.Mechanism,
                "Object / Persistence", "CA-ORG-01", "CAP-ORG-ROUNDTRIP", "MEMFileProjectStore", RoundTrip);
            Add(catalog, "M-OBJ-004", "Revision 冲突拒绝", TCTTestAspect.Mechanism,
                "Object / Transaction", "CA-ORG-01", "CAP-ORG-REVISION", "MEMEngineeringSession", RevisionConflict);
            Add(catalog, "M-OBJ-005", "MEM固定快照和引用证据安全性", TCTTestAspect.Mechanism,
                "Object / Snapshot", "CA-ORG-01", "CAP-ORG-SNAPSHOT", "MEMProjectSnapshot", SnapshotEvidence);
            Add(catalog, "M-OBJ-006", "MEM已提交Revision对象差异", TCTTestAspect.Mechanism,
                "Object / Diff", "CA-ORG-01", "CAP-ORG-DIFF", "MEMProjectSnapshot", RevisionDiff);
            Add(catalog, "M-OBJ-007", "MEM共享Symbol双实例的真实Visual投影和证据",
                TCTTestAspect.Mechanism, "Visual / Projection", "CA-ORG-01",
                "CAP-ORG-VISUAL", "CxSceneProjection", SymbolInstancesEvidence);
        }

        private static void Add(TCTTestCatalog catalog, string id, string name, TCTTestAspect aspect,
            string group, string capabilityArea, string capability, string objectId, Action<TCTTestContext> execute)
        {
            catalog.Register(new TCTDelegateTestCase(new TCTTestCaseDescriptor(id, name, aspect,
                group, capabilityArea, capability, objectId), execute));
        }

        private static void Contains(TCTTestContext context)
        {
            FGERect2D rect = new FGERect2D(10, 20, 20, 30);
            context.AssertTrue(rect.Contains(new FGEPoint2D(10, 20)), "左上边界包含", true, rect.Contains(new FGEPoint2D(10, 20)));
            context.AssertTrue(rect.Contains(new FGEPoint2D(30, 50)), "右下边界包含", true, rect.Contains(new FGEPoint2D(30, 50)));
            context.AssertTrue(!rect.Contains(new FGEPoint2D(31, 50)), "界外点排除", false, rect.Contains(new FGEPoint2D(31, 50)));
            context.Observe("Geometry", "Rectangle", "X=10 Y=20 Width=20 Height=30; 边界与外部点命中已由生产FGERect2D计算");
        }

        private static void Intersects(TCTTestContext context)
        {
            FGERect2D rect = new FGERect2D(10, 20, 20, 30);
            bool overlaps = rect.Intersects(new FGERect2D(20, 30, 10, 10));
            bool touchesOnly = rect.Intersects(new FGERect2D(30, 30, 5, 10));
            context.AssertTrue(overlaps, "面积交叠", true, overlaps);
            context.AssertTrue(!touchesOnly, "仅接触边界不是面积交叠", false, touchesOnly);
            context.Observe("Geometry", "Intersects", "重叠矩形(20,30,10,10)；贴边矩形(30,30,5,10)");
        }

        private static void Transform(TCTTestContext context)
        {
            FGEPoint2D point = new FGETransform2D(1, 0, 0, 1, 30, 40).Transform(new FGEPoint2D(2, 4));
            context.AssertTrue(point.X == 32, "变换X", 32, point.X);
            context.AssertTrue(point.Y == 44, "变换Y", 44, point.Y);
            context.Observe("Geometry", "Affine2D", "(2,4) 经平移(30,40) → (" + point.X + "," + point.Y + ")");
        }

        internal static MEMEngineeringSession CreateOrganizationSample()
        {
            var session = MEMEngineering.Create("TCT organization");
            var page = session.AddPage("Main Page");
            var group = session.AddGroup(page.RootContent, "Drawing Group");
            session.AddRectangle(group, "Detector", 50, 65, 170, 100);
            session.AddRectangle(group, "Source", 300, 180, 125, 75);
            var symbol = session.AddSymbol("Shared Symbol");
            session.AddRectangle(symbol.RootGroup, "Template Item", 0, 0, 50, 50);
            session.PlaceSymbol(page.RootContent, symbol, "Symbol Instance");
            session.ClearHistory();
            return session;
        }
        internal static MRCGroup FirstGroup(MEMEngineeringSession session)
        {
            foreach (var page in session.Project.Pages)
                foreach (var item in page.RootContent.VisualItems)
                    if (item is MRCGroup) return (MRCGroup)item;
            throw new InvalidOperationException("Missing group");
        }
        private static void Ownership(TCTTestContext context)
        {
            var session = CreateOrganizationSample();
            var project = session.Project;
            var page = project.Pages[0];
            var content = page.RootContent;
            var symbol = project.Symbols[0];
            var group = FirstGroup(session);
            context.AssertTrue(page.Parent == project && page.Kind == MEMNodeKind.Page,
                "Page父子关系", "Page / Project", page.Kind + " / " + page.Parent.Kind);
            context.AssertTrue(content.Parent == page && content.Kind == MEMNodeKind.Content,
                "Content父子关系", "Content / Page", content.Kind + " / " + content.Parent.Kind);
            context.AssertTrue(symbol.RootGroup.Parent == symbol && symbol.Kind == MEMNodeKind.Symbol,
                "Symbol根关系", "Symbol / Group", symbol.Kind + " / " + symbol.RootGroup.Kind);
            context.AssertTrue(group.Children.Count == 2 && group.Children[0].Kind == MEMNodeKind.Element,
                "Element子节点数量", 2, group.Children.Count);
            context.AssertTrue(content.References.Count == 1 && content.References[0].TargetId == symbol.Id,
                "独立定义有使用记录", symbol.Id, content.References[0].TargetId);
            MEMProjectSnapshot snapshot = session.GetSnapshot();
            context.AssertEqual(project.DescendantsAndSelf().Count(), snapshot.Nodes.Count,
                "已提交工程快照覆盖全部拥有节点");
            context.ObserveStructured("ObjectTree", "Ownership",
                "MEMProjectSnapshot Revision=" + snapshot.Revision +
                "; Ownership 和 Reference 在只读证据中分开，内容来自真实工程会话",
                project.Id.ToString("D"), session.GetAddress(project).ToString(), null,
                TCTEngineeringSnapshotCodec.Serialize(snapshot), TCTEngineeringSnapshotCodec.Mime);
        }
        private static void AtomicRollback(TCTTestContext context)
        {
            var session = CreateOrganizationSample();
            var group = FirstGroup(session);
            int before = group.Children.Count, revision = session.Revision;
            var temporary = new MRCRectangle(Guid.NewGuid(), "Temp", 5, 6, 20, 20);
            var unknown = new MRCRectangle(Guid.NewGuid(), "Unknown", 1, 2, 20, 20);
            var result = session.Edit("Atomic rollback", e =>
            {
                e.Add(group, temporary);
                e.Move(unknown, 1, 2);
                return true;
            });
            context.AssertTrue(!result.Success, "无效批次拒绝", false, result.Success);
            context.AssertTrue(session.Revision == revision, "Revision未增加", revision, session.Revision);
            context.AssertTrue(group.Children.Count == before, "恢复子节点数量", before, group.Children.Count);
            context.AssertTrue(temporary.Parent == null, "临时节点脱离父节点", true, temporary.Parent == null);
            context.Observe("ObjectTree", "Atomic rollback", "第一条Add后第二条Move失败，同一Session原子逆恢复");
        }
        private static void RoundTrip(TCTTestContext context)
        {
            var session = CreateOrganizationSample();
            string file = Path.Combine(Path.GetTempPath(), "CxVisual-TCT-" + Guid.NewGuid().ToString("N") + ".cxproj");
            try
            {
                session.SaveAs(file);
                var restored = MEMEngineering.Open(file);
                context.AssertTrue(restored.Project.Id == session.Project.Id,
                    "Project ID保持", session.Project.Id, restored.Project.Id);
                context.AssertTrue(restored.Revision == session.Revision,
                    "Revision保持", session.Revision, restored.Revision);
                int expected = FirstGroup(session).Children.Count;
                int actual = FirstGroup(restored).Children.Count;
                context.AssertTrue(actual == expected, "子元素数量保持", expected, actual);
                context.AssertTrue(restored.Project.Symbols.Count == 1
                    && restored.Project.Pages[0].RootContent.References.Count == 1,
                    "共享资源引用往返", true, true);
                context.Observe("ObjectTree", "RoundTrip", "真实工程包保存/加载保持唯一对象及资源引用");
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }
        private static void SnapshotEvidence(TCTTestContext context)
        {
            MEMEngineeringSession session = CreateOrganizationSample();
            try
            {
                MEMProjectSnapshot original = session.GetSnapshot();
                byte[] payload = TCTEngineeringSnapshotCodec.Serialize(original);
                XDocument decoded = TCTEngineeringSnapshotCodec.Deserialize(payload);
                context.AssertEqual(original.Nodes.Count, decoded.Root.Elements("Node").Count(),
                    "快照节点数与真实Owner一致");
                XElement content = decoded.Root.Elements("Node")
                    .Single(x => (string)x.Attribute("kind") == "Content");
                XElement reference = content.Element("Reference");
                context.AssertTrue(reference != null &&
                    (string)reference.Attribute("targetId") == session.Project.Symbols[0].Id.ToString("D"),
                    "Reference指向真实独立Symbol", true, reference != null);
                Guid rectangleId = FirstGroup(session).Children[0].Id;
                XElement rectangle = decoded.Root.Elements("Node")
                    .Single(x => (string)x.Attribute("id") == rectangleId.ToString("D"));
                context.AssertTrue(rectangle.Element("Visual") != null,
                    "实际图元视觉参数已进入版本化证据", true,
                    rectangle.Element("Visual") != null);

                // Snapshot is an immutable revision projection, never a live shadow engineering tree.
                string before = (string)rectangle.Attribute("name");
                session.Rename(session.Find(rectangleId), "Renamed after snapshot");
                context.AssertEqual(before, (string)rectangle.Attribute("name"),
                    "提交新Revision不反向篡改旧证据");
                context.AssertEqual(original.Revision, (int)decoded.Root.Attribute("revision"),
                    "证据记录创建时的Revision");

                XDocument invalid = TCTEngineeringSnapshotCodec.Deserialize(payload);
                XElement broken = invalid.Root.Elements("Node")
                    .Single(x => (string)x.Attribute("kind") == "Symbol");
                broken.SetAttributeValue("parentId", Guid.NewGuid().ToString("D"));
                context.AssertThrows<InvalidDataException>(() =>
                    TCTEngineeringSnapshotCodec.Deserialize(Encoding.UTF8.GetBytes(
                        invalid.ToString(SaveOptions.DisableFormatting))),
                    "伪造Ownership必须拒绝");
                context.AssertThrows<System.Xml.XmlException>(() =>
                    TCTEngineeringSnapshotCodec.Deserialize(Encoding.UTF8.GetBytes(
                        "<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///tmp/secret'>]>" +
                        "<EngineeringSnapshot schema='1'/>")),
                    "工程证据不得包含DTD");
                XDocument duplicate = TCTEngineeringSnapshotCodec.Deserialize(payload);
                XElement parent = duplicate.Root.Elements("Node")
                    .First(x => x.Elements("Child").Any());
                parent.Add(new XElement(parent.Elements("Child").First()));
                context.AssertThrows<InvalidDataException>(() =>
                    TCTEngineeringSnapshotCodec.Deserialize(Encoding.UTF8.GetBytes(
                        duplicate.ToString(SaveOptions.DisableFormatting))),
                    "重复Child不能在Viewer中被渲染成两份对象");
                context.ObserveStructured("ObjectTree", "Immutable Snapshot",
                    "真实MEM快照、引用、视觉字段和非法关系拒绝",
                    session.Project.Id.ToString("D"), session.GetAddress(session.Project).ToString(), null,
                    payload, TCTEngineeringSnapshotCodec.Mime);
            }
            finally { session.Close(true); }
        }
        private static void RevisionDiff(TCTTestContext context)
        {
            MEMEngineeringSession session = CreateOrganizationSample();
            try
            {
                MEMProjectSnapshot before = session.GetSnapshot();
                byte[] left = TCTEngineeringSnapshotCodec.Serialize(before);
                MRCVisualItem visual = FirstGroup(session).Children[0];
                string originalName = visual.Name;
                session.Rename(visual, originalName + " Changed");
                MEMProjectSnapshot after = session.GetSnapshot();
                byte[] right = TCTEngineeringSnapshotCodec.Serialize(after);
                IList<string> diff = TCTEngineeringSnapshotCodec.Compare(left, right);
                context.AssertEqual(before.Revision + 1, after.Revision, "提交后Revision推进");
                context.AssertEqual(1, diff.Count, "按稳定节点Id发现唯一重命名变化");
                context.AssertTrue(diff[0].Contains(originalName) &&
                    diff[0].Contains("Changed"), "差异来自真实名称变化", true, diff[0]);
                context.AssertEqual(1, before.Nodes.SelectMany(n => n.References).Count(),
                    "旧Revision仍保留资源引用");
                context.AssertEqual(1, after.Nodes.SelectMany(n => n.References).Count(),
                    "新Revision仍保留资源引用");
                context.AssertThrows<InvalidDataException>(() =>
                    TCTEngineeringSnapshotCodec.Compare(right,
                        TCTEngineeringSnapshotCodec.Serialize(
                            MEMEngineering.Create("Other project").GetSnapshot())),
                    "不能比较不同工程的对象版本");
                string address = session.GetAddress(session.Project).ToString();
                context.ObserveStructured("ObjectTree", "Before Revision", "Revision=" + before.Revision,
                    session.Project.Id.ToString("D"), address, null, left,
                    TCTEngineeringSnapshotCodec.Mime, "Before");
                context.ObserveStructured("ObjectTree", "After Revision", "Revision=" + after.Revision,
                    session.Project.Id.ToString("D"), address, null, right,
                    TCTEngineeringSnapshotCodec.Mime, "After");
            }
            finally { session.Close(true); }
        }

        private static void SymbolInstancesEvidence(TCTTestContext context)
        {
            MEMEngineeringSession session = CreateOrganizationSample();
            try
            {
                MEMContent content = session.Project.Pages[0].RootContent;
                MEMSymbol symbol = session.Project.Symbols[0];
                session.PlaceSymbol(content, symbol, "Symbol Instance Two");
                MEMProjectSnapshot snapshot = session.GetSnapshot();
                IList<CxSceneItem> scene = CxSceneProjection.Project(session);
                CxSceneItem[] instances = scene.Where(x => x.Address != null &&
                    x.Address.InstancePath.Count != 0).ToArray();
                context.AssertEqual(2, instances.Length, "真实Symbol复用有两个不同使用位置");
                context.AssertEqual(2, instances.Select(x => x.Address.InstancePath[0])
                    .Distinct().Count(), "两次放置有不同实例路径");
                context.AssertEqual(4, scene.Count, "两个直属矩形与两个引用矩形均已投影");
                context.AssertTrue(scene.All(x => x.ElementTypeId == "core.rectangle"),
                    "当前验收样本全部真实矩形，不代画其它Shape", true,
                    scene.All(x => x.ElementTypeId == "core.rectangle"));
                List<TCTGeometryFigure> geometry = new List<TCTGeometryFigure>();
                foreach (CxSceneItem item in scene)
                {
                    geometry.Add(new TCTGeometryFigure("Rectangle", "Actual",
                        item.Name, item.Id.ToString("D"), item.Bounds.X, item.Bounds.Y,
                        item.Bounds.Width, item.Bounds.Height,
                        item.Address == null ? null : item.Address.ToString()));
                }
                byte[] geometryEvidence = TCTGeometryEvidence.Serialize(geometry);
                TCTGeometryFigure[] decodedFigures =
                    TCTGeometryEvidence.Read(geometryEvidence).ToArray();
                context.AssertEqual(2, decodedFigures
                    .Where(x => !string.IsNullOrEmpty(x.DefinitionAddress))
                    .Select(x => x.DefinitionAddress).Distinct(StringComparer.Ordinal).Count(),
                    "两处Symbol使用必须在几何证据中保留不同InstancePath");
                context.ObserveStructured("Geometry", "真实工程画布与Symbol实例",
                    "CxSceneProjection.Project(MEMEngineeringSession)，矩形与2个Symbol使用位置",
                    session.Project.Id.ToString("D"),
                    session.GetAddress(session.Project).ToString(), null,
                    geometryEvidence, TCTGeometryEvidence.Mime);
                context.ObserveStructured("ObjectTree", "所属与两处引用定义",
                    "独立Symbol是Reference，不是Content的Owned Child",
                    session.Project.Id.ToString("D"),
                    session.GetAddress(session.Project).ToString(), null,
                    TCTEngineeringSnapshotCodec.Serialize(snapshot),
                    TCTEngineeringSnapshotCodec.Mime);
            }
            finally { session.Close(true); }
        }

        private static void RevisionConflict(TCTTestContext context)
        {
            var session = CreateOrganizationSample();
            var group = FirstGroup(session);
            int revision = session.Revision;
            var temporary = new MRCRectangle(Guid.NewGuid(), "Conflict", 1, 2, 3, 4);
            var rejected = session.Edit("Stale revision", revision + 42, e =>
            {
                e.Add(group, temporary);
                return true;
            });
            context.AssertTrue(!rejected.Success, "冲突拒绝", false, rejected.Success);
            context.AssertTrue(rejected.ErrorCode == "org.version.conflict", "错误码",
                "org.version.conflict", rejected.ErrorCode);
            context.AssertTrue(session.Revision == revision && temporary.Parent == null,
                "工程未变更", true, session.Revision == revision && temporary.Parent == null);
        }
    }
}
