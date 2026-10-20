using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using AvalonDock.Layout;

namespace CxVisual.DockingPoC
{
    /// <summary>真 WPF STA + 真 AvalonDock 模型测试；与主程序同程序集运行以免新建并行测试版引擎。</summary>
    internal static class DockingSelfTests
    {
        private static readonly List<string> Results = new List<string>();

        public static bool Run(MainWindow window, string reportPath)
        {
            Results.Clear();
            string temporary = Path.Combine(Path.GetTempPath(), "CxVisual-AvalonDock-Test-" +
                Guid.NewGuid().ToString("N") + ".xml");
            string invalidPath = temporary + ".invalid.xml";
            try
            {
                Assert("T01 初始九种 ContentId 和默认文档数量", () =>
                {
                    window.EnsureUniqueIds();
                    string[] ids = window.SnapshotIds();
                    Check(ids.Length == 9, "应为 3 文档 + 6 工具面板，实际 " + ids.Length);
                    Check(WorkspaceIds.Documents.All(id => ids.Contains(id)), "缺少中央文档");
                    Check(WorkspaceIds.Tools.All(id => ids.Contains(id)), "缺少工具面板");
                });

                int revision = window.Engineering.Revision;
                Assert("T02 浅色/深色切换不修改业务 Revision", () =>
                {
                    window.ApplyTheme(true);
                    Check(window.IsDark, "深色主题没有生效");
                    window.ApplyTheme(false);
                    Check(!window.IsDark, "浅色主题没有生效");
                    Check(window.Engineering.Revision == revision, "主题错误修改工程状态");
                });

                Assert("T03 XML 保存/恢复和 ContentId 回连", () =>
                {
                    string[] before = window.SnapshotIds();
                    window.SaveLayoutToFile(temporary);
                    Check(File.Exists(temporary), "没有生成布局文件");
                    string xml = LayoutPersistence.ReadChecked(temporary);
                    Check(xml.Contains("doc.flow") && xml.Contains("tool.properties"), "缺少持久 ContentId");
                    window.RestoreLayoutFromFile(temporary);
                    Check(before.SequenceEqual(window.SnapshotIds()), "恢复后面板/文档身份有变化");
                    Check(window.Engineering.Revision == revision, "读取布局修改了 Revision");
                });

                Assert("T04 工具面板真正 Close 后重新创建且无重复", () =>
                {
                    window.CloseTool(WorkspaceIds.Properties);
                    Check(!window.SnapshotIds().Contains(WorkspaceIds.Properties), "关闭没有移除属性面板");
                    window.ShowTool(WorkspaceIds.Properties);
                    Check(window.SnapshotIds().Count(id => id == WorkspaceIds.Properties) == 1,
                        "重开后 ContentId 不唯一");
                });

                Assert("T05 多文档关闭、重新打开且不污染工程", () =>
                {
                    window.CloseDocument(WorkspaceIds.Flow);
                    Check(!window.SnapshotIds().Contains(WorkspaceIds.Flow), "流程文档没有关闭");
                    window.OpenDocument(WorkspaceIds.Flow);
                    window.OpenDocument(WorkspaceIds.Visual);
                    Check(window.SnapshotIds().Count(id => id == WorkspaceIds.Flow) == 1,
                        "流程文档重复打开");
                    Check(window.Engineering.Revision == revision, "文档操作修改了工程");
                });

                Assert("T06 浮动/重新停靠 + 自动隐藏与恢复", () =>
                {
                    LayoutAnchorable property = window.DockManager.Layout.Descendents()
                        .OfType<LayoutAnchorable>().First(item => item.ContentId == WorkspaceIds.Properties);
                    window.FloatTool(WorkspaceIds.Properties);
                    Check(property.IsFloating, "属性面板没有转为浮动");
                    window.DockTool(WorkspaceIds.Properties);
                    Check(!property.IsFloating, "属性面板没有重新停靠");
                    LayoutAnchorable resource = window.DockManager.Layout.Descendents()
                        .OfType<LayoutAnchorable>().First(item => item.ContentId == WorkspaceIds.Resources);
                    window.ToggleAutoHideTool(WorkspaceIds.Resources);
                    Check(resource.IsAutoHidden, "没有进入自动隐藏状态");
                    window.ToggleAutoHideTool(WorkspaceIds.Resources);
                    Check(!resource.IsAutoHidden, "没有返回停靠状态");
                    Check(window.Engineering.Revision == revision, "自动隐藏修改工程");
                });

                Assert("T07 布局的异常 XML / DTD / 超限拒绝", () =>
                {
                    ExpectThrows(() => LayoutPersistence.ValidateXml("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///c:/x'>]><x>&e;</x>"));
                    ExpectThrows(() => LayoutPersistence.ValidateXml(new string('X', LayoutPersistence.MaxLayoutBytes + 1)));
                    File.WriteAllText(invalidPath, "not xml", Encoding.UTF8);
                    string[] before = window.SnapshotIds();
                    ExpectThrows(() => window.RestoreLayoutFromFile(invalidPath));
                    Check(before.SequenceEqual(window.SnapshotIds()), "无效布局破坏了现有窗口");
                    Check(window.Engineering.Revision == revision, "非法输入修改工程");
                });

                Assert("T08 默认布局重置且 Revision 保持", () =>
                {
                    window.ResetLayout();
                    Check(window.SnapshotIds().Length == 9, "重置没有恢复 9 项初始布局");
                    Check(window.Engineering.Revision == revision, "重置影响业务 Revision");
                });

                Assert("T09 只有主动工程编辑才增加 Revision", () =>
                {
                    window.Engineering.SimulateEngineeringEdit();
                    Check(window.Engineering.Revision == revision + 1, "模拟工程编辑未增加 Revision");
                    window.SaveLayoutToFile(temporary);
                    window.RestoreLayoutFromFile(temporary);
                    Check(window.Engineering.Revision == revision + 1, "布局读写再次增加 Revision");
                });

                Assert("T10 WPF DPI 值可读取", () =>
                {
                    DpiScale dpi = VisualTreeHelper.GetDpi(window);
                    Check(dpi.DpiScaleX > 0.0 && dpi.DpiScaleY > 0.0, "DPI 无效");
                });

                Results.Add("NOTE: 浮动鼠标拖动 / 跨屏 100%-150%-200% / 人工外观验收仍需 Windows 桌面手测。");
                return true;
            }
            catch (Exception error)
            {
                Results.Add("FAIL: " + error);
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                try { if (File.Exists(invalidPath)) File.Delete(invalidPath); } catch { }
                string folder = Path.GetDirectoryName(Path.GetFullPath(reportPath));
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllLines(reportPath, Results, Encoding.UTF8);
            }
        }

        private static void Assert(string name, Action action)
        {
            action();
            Results.Add("PASS: " + name);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void ExpectThrows(Action action)
        {
            bool failed = false;
            try { action(); }
            catch (Exception) { failed = true; }
            Check(failed, "应拒绝的错误输入被接受");
        }
    }
}
