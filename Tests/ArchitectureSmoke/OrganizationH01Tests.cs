using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationH01Tests
    {
        private static void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException("H01: " + text); }
        private static void Reject(Action action) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } catch (ArgumentException) { rejected = true; } Check(rejected, "expected rejection"); }
        public static void Run()
        {
            string path = Path.Combine(Path.GetTempPath(), "cx-h01-" + Guid.NewGuid().ToString("N") + ".cxproj");
            try
            {
                var s = MEMEngineering.Create("最小工程");
                Check(s.Project.Pages.Count == 0 && s.Revision == 0, "empty project");
                var made = s.Edit("建立工程", e =>
                {
                    var page = e.AddPage("操作页");
                    var area = e.AddContent(page.RootContent, "设备区");
                    var a = e.AddRectangle(area, "运行灯", 20, 30, 40, 50);
                    e.AddRectangle(area, "结果灯", 70, 30, 40, 50);
                    return a;
                });
                Check(made.Success && s.Revision == 1, "atomic create");
                var item = made.Value;
                Check(s.Find(item.Id) == item && item.Parent.Name == "设备区", "indexed ownership");
                var page1 = s.Project.Pages[0];
                Check(ReferenceEquals(page1, s.Project.FindResource(page1.Id)), "resource is same object");
                string expected = Snapshot(s); s.SaveAs(path); s.Close();
                Reject(() => s.Rename(item, "closed"));
                var loaded = MEMEngineering.Open(path);
                Check(Snapshot(loaded) == expected, "all persisted node fields/order/ownership differ");
                var again = (MRCElement)loaded.Find(item.Id);
                Check(again.Name == "运行灯" && again.X == 20 && again.Width == 40, "save/load fields");
                Check(loaded.Count == 6 && loaded.Find(item.Id).Parent.Parent.Parent == loaded.Project.Pages[0], "save/load hierarchy");
                loaded.Rename(again, "运行状态"); loaded.Move(again, 22, 35);
                var second = loaded.AddPage("第二页");
                var shared = loaded.AddContent(loaded.Project, "后台服务");
                var symbol = loaded.AddSymbol("电机");
                loaded.AddRectangle(symbol.RootGroup, "符号图元", 0, 0);
                var reference = loaded.PlaceSymbol(second.RootContent, symbol, "M1");
                Check(loaded.Project.Pages.Count == 2 && loaded.Project.Contents.Count == 1 && loaded.Project.Symbols.Count == 1, "typed resources");
                Check(loaded.Project.Resources.Count == 4 && loaded.Project.FindResource(shared.Id) == shared, "resource index");
                Check(loaded.FindReference(reference.Id).TargetId == symbol.Id, "reference");
                Reject(() => loaded.Remove(symbol));
                loaded.SetBinding(again,"visible",new MPMBinding("machine.status","running"));
                var group = loaded.AddGroup(second.RootContent,"旋转组"); loaded.Move(group,100,200); loaded.Rotate(group,35); loaded.Scale(group,-2,3);
                loaded.AddRectangle(group,"局部矩形",5,6,10,20); loaded.SetVisibility(group,false,false);
                expected=Snapshot(loaded); loaded.Save(); loaded.Close();
                var modified = MEMEngineering.Open(path);
                Check(Snapshot(modified) == expected,"complex resource/visual/reference/binding semantic roundtrip");
                Check(((MRCElement)modified.Find(item.Id)).X == 22 && modified.Find(item.Id).Name == "运行状态", "modify/reopen");
                Check(modified.FindReference(reference.Id).TargetId == symbol.Id, "reference roundtrip");
                modified.Remove(modified.FindReference(reference.Id)); modified.Remove(modified.Find(symbol.Id));
                modified.Save(); modified.Close();
                var final = MEMEngineering.Open(path); Check(final.Find(symbol.Id) == null && final.FindReference(reference.Id) == null, "delete/reopen"); final.Close();
                TransactionsAndTransforms();
                Console.WriteLine("PASS H01 real cases A/B/C, ownership, transactions, references, transforms, persistence");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static string Snapshot(MEMEngineeringSession session)
        {
            var rows = new List<string> { session.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            foreach (var n in session.Project.DescendantsAndSelf())
            {
                rows.Add(string.Join("|",new[]{n.Id.ToString(),n.Name,n.TypeId,n.Kind.ToString(),n.Parent == null ? "" : n.Parent.Id.ToString(),string.Join(",",n.Children.Select(c=>c.Id))}));
                var resource=n as IMEMResource; if(resource!=null)rows.Add(resource.Scope+"|"+resource.StorageRootId);
                var v=n as MRCVisualItem;
                if(v!=null)
                {
                    rows.Add(string.Join("|",new[]{v.X,v.Y,v.Width,v.Height,v.Rotation,v.ScaleX,v.ScaleY}.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+"|"+v.IsVisible+"|"+v.IsSelectable);
                    var binding=v.GetBinding("visible");if(binding!=null)rows.Add(binding.SourceId+"|"+binding.MemberId);
                }
                var content=n as MEMContent;
                if(content!=null)foreach(var r in content.References)rows.Add(r.Id+"|"+r.Name+"|"+r.Kind+"|"+r.TargetId+"|"+r.Owner.Id);
            }
            return string.Join("\n",rows);
        }

        private static void TransactionsAndTransforms()
        {
            var s = MEMEngineering.Create("事务");
            var page = s.AddPage("P"); var group = s.AddGroup(page.RootContent, "G");
            var nested = s.AddGroup(group, "G2"); var leaf = s.AddRectangle(nested, "矩形", 5, 6, 10, 20);
            s.Move(group, 100, 200); s.Move(nested, 10, 20);
            double x, y; leaf.TransformPoint(0, 0, out x, out y);
            Check(x == 115 && y == 226 && leaf.X == 5, "group local coordinates");
            Check(leaf.HitTest(116, 227) && !leaf.HitTest(0, 0), "world hit test");
            s.Rotate(group, 90); leaf.TransformPoint(0, 0, out x, out y);
            Check(Math.Abs(x - 74) < 1e-9 && Math.Abs(y - 215) < 1e-9, "group rotation composition");
            s.Rotate(group, 0);
            int version = s.Revision, count = s.Count;
            var failed = s.Edit("整批拒绝", e => { e.Rename(leaf, "temporary"); e.AddRectangle(nested, "new", 0, 0); e.Add(page.RootContent, new MRCRectangle(leaf.Id, "duplicate", 0, 0, 1, 1)); });
            Check(!failed.Success && s.Revision == version && s.Count == count && leaf.Name == "矩形", "failure rollback");
            Reject(() => s.Reparent(group, nested));
            Reject(() => s.Remove(page.RootContent));
            Reject(() => s.Add(leaf, new MEMContent(Guid.NewGuid(), "非法")));
            var foreign = MEMEngineering.Create("foreign"); Reject(() => s.Add(page.RootContent, foreign.AddPage("F")));
            s.Reparent(leaf, page.RootContent); Check(leaf.Parent == page.RootContent && s.Find(leaf.Id) == leaf, "reparent identity");
            Check(s.Undo().Success && leaf.Parent == nested, "undo parent");
            Check(s.Redo().Success && leaf.Parent == page.RootContent, "redo parent");
            s.Remove(group); Check(s.Find(group.Id) == null && s.Find(nested.Id) == null, "remove subtree index");
            Check(s.Undo().Success && s.Find(nested.Id) == nested, "restore subtree index");
            int notices = 0;
            using (s.Watch(c => { notices++; Check(s.Find(leaf.Id).Name == "changed", "postcommit observation"); })) s.Rename(leaf, "changed");
            Check(notices == 1 && s.Validate().IsValid, "one notification / validate");
            var definition = s.AddContent(s.Project, "复用内容");
            Reject(() => s.PlaceContent(definition, definition, "循环"));
        }
    }
}
