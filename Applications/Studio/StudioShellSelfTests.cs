using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;

namespace CxVisual.Studio
{
    /// <summary>真实Windows WPF STA自测：不调用外部设备，不伪造MEM Revision。</summary>
    internal static class StudioShellSelfTests
    {
        private static readonly List<string> Results = new List<string>();

        public static bool Run(MainWindow window, string report)
        {
            Results.Clear();
            string temporary = Path.Combine(Path.GetTempPath(), "CxVisual-Studio-S1-" +
                Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                int revision = window.StudioEngineeringRevision;
                CheckStep("S1-01 初始布局与唯一ContentId", () =>
                {
                    window.EnsureStudioContentIds();
                    string[] ids = window.GetStudioContentIds();
                    Assert(ids.Length == 8, "应为三个文档及五个工具面板，实际：" + ids.Length);
                    Assert(ids.Contains("doc.home") && ids.Contains("doc.visual") &&
                           ids.Contains("doc.flow") && ids.Contains("tool.properties"), "必需内容缺失");
                });
                CheckStep("S1-02 同一行9项固定菜单及动态上下文", () =>
                {
                    Assert(window.StudioStaticMenus.Children.OfType<Button>().Count() == 9, "固定菜单不是9项");
                    Assert(window.StudioDynamicMenus.Children.Count >= 1, "动态菜单为空");
                    window.OpenStudioDocument("doc.visual");
                    Assert(window.StudioDynamicMenus.Children.OfType<Button>().Any(
                        b => Convert.ToString(b.Content) == "绘图"), "图元上下文菜单未切换");
                    window.OpenStudioDocument("doc.flow");
                    Assert(window.StudioDynamicMenus.Children.OfType<Button>().Any(
                        b => Convert.ToString(b.Content) == "编排"), "流程上下文菜单未切换");
                });
                CheckStep("S1-03 文档打开与复用", () =>
                {
                    window.CloseStudioDocument("doc.flow");
                    Assert(!window.GetStudioContentIds().Contains("doc.flow"), "关闭无效");
                    window.OpenStudioDocument("doc.flow");
                    window.OpenStudioDocument("doc.flow");
                    Assert(window.GetStudioContentIds().Count(id => id == "doc.flow") == 1, "重复文档");
                });
                CheckStep("S1-04 两主题切换不修改工程", () =>
                {
                    window.ApplyStudioTheme(true);
                    Assert(window.StudioDarkTheme, "深色未切换");
                    window.ApplyStudioTheme(false);
                    Assert(!window.StudioDarkTheme, "浅色未恢复");
                    Assert(window.StudioEngineeringRevision == revision, "主题改变工程Revision");
                });
                CheckStep("S1-05 布局原子保存/恢复不修改工程", () =>
                {
                    window.SaveStudioLayout(temporary);
                    Assert(File.Exists(temporary), "布局文件未保存");
                    Assert(File.ReadAllText(temporary).Contains("doc.visual"), "未写入文档身份");
                    window.RestoreStudioLayout(temporary);
                    window.EnsureStudioContentIds();
                    Assert(window.StudioEngineeringRevision == revision, "布局修改工程Revision");
                });
                CheckStep("S1-06 关闭重开属性面板身份唯一", () =>
                {
                    window.CloseStudioTool("tool.properties");
                    window.ShowStudioTool("tool.properties");
                    Assert(window.GetStudioContentIds().Count(id => id == "tool.properties") == 1,
                        "重开属性面板重复");
                });
                CheckStep("S1-07 拒绝DTD/越界/未知ContentId", () =>
                {
                    AssertThrows(() => MainWindow.ValidateStudioLayoutXml(
                        "<!DOCTYPE x [<!ENTITY y SYSTEM 'file:///c:/unsafe'>]><x>&y;</x>"));
                    AssertThrows(() => MainWindow.ValidateStudioLayoutXml(new string('x', 1024 * 1024 + 1)));
                    AssertThrows(() => MainWindow.ValidateStudioLayoutXml(
                        "<LayoutRoot><LayoutDocument ContentId='unknown'/></LayoutRoot>"));
                    Assert(window.StudioEngineeringRevision == revision, "非法布局影响工程");
                });
                CheckStep("S1-08 默认布局重置", () =>
                {
                    window.ResetStudioLayout();
                    window.EnsureStudioContentIds();
                    Assert(window.GetStudioContentIds().Length == 8, "重置后数量异常");
                    Assert(window.StudioEngineeringRevision == revision, "重置改变工程Revision");
                });
                CheckStep("S1-09 层级资源树与MEM工程身份一致", () =>
                {
                    window.RefreshStudioResourceTree();
                    TreeViewItem root=window.StudioResourceTree.Items.OfType<TreeViewItem>().Single();
                    Assert(root.Items.OfType<TreeViewItem>().Any(item=>Convert.ToString(item.Header).StartsWith("页面")),
                        "缺少实际MEM页面分组");
                    Assert(root.Items.OfType<TreeViewItem>().Any(item=>Convert.ToString(item.Header).StartsWith("共享组件")),
                        "缺少Symbol分组");
                    Assert(!root.Items.OfType<TreeViewItem>().Any(item=>Convert.ToString(item.Header).Contains("Element")),
                        "普通资源树不应展开图形Element");
                    Assert(window.StudioEngineeringRevision==revision,"资源树刷新修改了MEM工程Revision");
                });
                CheckStep("S1-10 图形结构与资源树分离且指向真实MEM视觉对象", () =>
                {
                    window.OpenStudioDocument("doc.visual");
                    window.RefreshStudioGraphicsTree();
                    TreeViewItem root = window.StudioGraphicsTree.Items.OfType<TreeViewItem>().FirstOrDefault();
                    Assert(root != null && root.Items.Count > 0, "图形结构没有列出当前绘图定义");
                    Assert(root.Items.OfType<TreeViewItem>().Any(item => Convert.ToString(item.Header).Contains("图层")),
                        "图形结构缺少Layer分组");
                    TreeViewItem projectRoot = window.StudioResourceTree.Items.OfType<TreeViewItem>().Single();
                    Assert(projectRoot.Items.OfType<TreeViewItem>().All(item =>
                        !Convert.ToString(item.Header).Contains("Group/Element")),
                        "资源导航误混入图元内层");
                    Assert(window.StudioEngineeringRevision == revision, "图形树读取修改MEM Revision");
                });
                CheckStep("R07 同一工程插入Join、保存重开及非法输入回滚", () =>
                {
                    Assert(window.FlowInsertJoin != null &&
                        Convert.ToString(window.FlowInsertJoin.Content).Contains("Join"),
                        "Studio没有暴露真实Join操作");
                    string path=Path.Combine(Path.GetTempPath(),
                        "CxVisual-Studio-R07-"+Guid.NewGuid().ToString("N")+".json");
                    try
                    {
                        var session=MEMEngineering.Create("R07 Join Studio");
                        var page=session.AddPage("流程");
                        Guid left=Guid.Empty,right=Guid.Empty;
                        MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                            left=b.Step("Acquire");
                            right=b.Step("AI");
                        });
                        var judge=new MOCCapabilityDescriptor("Judge",
                            "综合结果判定","Inspection");
                        int before=session.Revision;
                        Guid joined=MainWindow.InsertSelectedJoin(session,
                            page.RootContent,judge,new[]{left,right});
                        Assert(session.Revision>before,"Join操作没有提交MEM工程事务");
                        var snapshot=session.GetSnapshot();
                        var wiring=MOLWiringCodec.Get(snapshot.Find(page.RootContent.Id));
                        Assert(wiring!=null && wiring.Blocks.Any(b=>b.Id==joined) &&
                            wiring.Links.Count(l=>l.Target.BlockId==joined &&
                                l.Target.PortMemberId=="Start" &&
                                l.Kind==MPCPortKind.Control)==2,
                            "Join没有写到原逻辑Aspect及两条真实Control连线");
                        before=session.Revision;
                        AssertThrows(()=>MainWindow.InsertSelectedJoin(session,
                            page.RootContent,judge,new[]{left,left}));
                        Assert(session.Revision==before,
                            "重复分支验证失败却修改MEM工程版本");
                        session.SaveAs(path);
                        var reopened=MEMEngineering.Open(path);
                        try
                        {
                            var saved=MOLWiringCodec.Get(
                                reopened.GetSnapshot().Find(page.RootContent.Id));
                            Assert(saved!=null&&saved.Blocks.Any(b=>b.Id==joined)&&
                                saved.Links.Count(l=>l.Target.BlockId==joined)==2,
                                "保存重开丢失Join原BlockId及Control连线");
                        }
                        finally{reopened.Close(true);}
                        session.Close(true);
                    }
                    finally{if(File.Exists(path))File.Delete(path);}
                    Assert(window.StudioEngineeringRevision==revision,
                        "独立Join自测误修改当前打开的Studio工程");
                });
                Results.Add("NOTE: 浮动、自动隐藏鼠标操作、多显示器、100/150/200%DPI、Viewer互操作尚需Windows桌面人工验收。");
                return true;
            }
            catch (Exception ex)
            {
                Results.Add("FAIL: " + ex);
                return false;
            }
            finally
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(report));
                    File.WriteAllLines(report, Results, new UTF8Encoding(false));
                }
                catch { /* 由CI检测缺失报告，并报告失败 */ }
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void CheckStep(string title, Action body)
        {
            body();
            Results.Add("PASS: " + title);
        }

        private static void Assert(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        private static void AssertThrows(Action action)
        {
            try { action(); }
            catch { return; }
            throw new InvalidOperationException("无效输入未被拒绝。");
        }
    }
}
