using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using AvalonDock.Layout;
using CxVisual.Mechanism.Presentation.Wpf;
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
                    Assert(window.StudioDock.Layout.ActiveContent != null &&
                        window.StudioDock.Layout.ActiveContent.ContentId == "doc.visual",
                        "图形结构显示不能抢占HMI中央文档焦点");
                    Assert(window.StudioDynamicMenus.Children.OfType<Button>().Any(
                        b => Convert.ToString(b.Content) == "绘图"), "图元上下文菜单未切换");
                    window.OpenStudioDocument("doc.flow");
                    Assert(window.StudioDock.Layout.ActiveContent != null &&
                        window.StudioDock.Layout.ActiveContent.ContentId == "doc.flow",
                        "从HMI切换流程必须保留流程主文档焦点");
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
                    var tools=window.StudioDock.Layout.Descendents().OfType<AvalonDock.Layout.LayoutAnchorable>().ToArray();
                    Assert(tools.Any(p=>p.ContentId=="tool.graphics"&&p.IsVisible)&&
                        tools.Any(p=>p.ContentId=="tool.properties"&&p.IsVisible),
                        "图形结构与属性必须可同时显示，不应共用一组分页遮挡属性操作");
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
                CheckStep("S1-11 图形树和画布共享MEM选中身份并保持对象节点", () =>
                {
                    window.OpenStudioDocument("doc.visual");
                    window.RefreshStudioGraphicsTree();
                    TreeViewItem root=window.StudioGraphicsTree.Items.OfType<TreeViewItem>().First();
                    TreeViewItem node=root.Items.OfType<TreeViewItem>()
                        .SelectMany(layer=>layer.Items.OfType<TreeViewItem>())
                        .FirstOrDefault(item=>item.Tag is MRCVisualItem);
                    Assert(node!=null,"图形结构缺少可选的真实MRCVisualItem");
                    int unchanged=window.StudioEngineeringRevision;
                    MRCVisualItem visual=(MRCVisualItem)node.Tag;
                    // 对象UI投影源于真实MEM Id；测试不允许创建替代对象或暗中提交工程事务。
                    window.DataContext.GetType().GetMethod("Select")
                        .Invoke(window.DataContext,new object[]{(Guid?)visual.Id});
                    window.RefreshStudioGraphicsTree();
                    TreeViewItem rootAfter=window.StudioGraphicsTree.Items.OfType<TreeViewItem>().First();
                    Assert(Object.ReferenceEquals(root,rootAfter),"单次选择引起图形结构树整棵重建");
                    Assert(window.StudioEngineeringRevision==unchanged,"对象选中修改了工程Revision");
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
                CheckStep("R05 定义与实例往返恢复及失效选择保护", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    Assert(vm!=null,"Studio缺少当前工程的统一ViewModel");
                    MEMPage page=vm.ActivePage;
                    Assert(page!=null,"当前工程缺少有效页面");
                    MEMSymbol motor=vm.ProjectSession.AddSymbol("R05共享电机");
                    MRIResourceReference instance=vm.ProjectSession.PlaceSymbol(
                        page.RootContent,motor,"R05电机实例");
                    vm.ActivePage=page;
                    vm.Select(instance.Id);
                    int version=vm.ProjectSession.Revision;
                    Assert(!vm.BackCommand.CanExecute(null),"未导航时返回应禁用");
                    vm.OpenDefinitionCommand.Execute(null);
                    Assert(vm.CurrentStudioDefinition!=null&&
                        vm.CurrentStudioDefinition.Id==motor.Id,"未进入真实共享定义");
                    Assert(vm.SelectedId==null,"定义编辑仍然选中页面实例");
                    Assert(window.StudioResourceBackButton.Command.CanExecute(null),
                        "左侧资源导航缺少可执行的返回入口");
                    window.StudioResourceBackButton.Command.Execute(null);
                    Assert(vm.CurrentStudioDefinition!=null&&
                        vm.CurrentStudioDefinition.Id==page.Id,"未回到原页面");
                    Assert(vm.SelectedId==instance.Id,"未恢复原实例的稳定MEM身份");
                    Assert(vm.ProjectSession.Revision==version,
                        "进入/返回共享定义不应修改工程Revision");
                    Assert(!vm.BackCommand.CanExecute(null),"已返回原页但导航栈仍未清空");
                    vm.OpenDefinitionCommand.Execute(null);
                    vm.ProjectSession.Remove(instance);
                    vm.BackCommand.Execute(null);
                    Assert(vm.CurrentStudioDefinition!=null&&
                        vm.CurrentStudioDefinition.Id==page.Id,"实例删除后无法返回页面");
                    Assert(vm.SelectedId==null,"返回时复活了已删除的实例选择");
                });
                CheckStep("R06 两页面独立编辑Tab、事务、布局和跨工程失效反证", () =>
                {
                    StudioViewModel model = window.DataContext as StudioViewModel;
                    MEMEngineeringSession session = model.ProjectSession;
                    MEMPage one = session.AddPage("R06 一号工位");
                    MEMPage two = session.AddPage("R06 二号工位");
                    int initialPageRevision = session.Revision;
                    string first = window.OpenStudioPageDocument(one.Id);
                    Assert(window.ActiveStudioDocumentId == first, "页面一未成为活动资源文档");
                    string second = window.OpenStudioPageDocument(two.Id);
                    Assert(window.ActiveStudioDocumentId == second, "页面二未成为活动资源文档");
                    Assert(first != second && window.GetStudioContentIds().Contains(first) &&
                        window.GetStudioContentIds().Contains(second), "页面没有不同的资源Tab");
                    SceneView firstView = window.GetStudioPageScene(one.Id);
                    SceneView secondView = window.GetStudioPageScene(two.Id);
                    Assert(firstView != null && secondView != null &&
                        !Object.ReferenceEquals(firstView, secondView), "两个Tab仍共用一张场景");
                    firstView.SetViewport(1.7, 0, 0);
                    Assert(Math.Abs(firstView.Zoom - 1.7) < 0.001 &&
                        Math.Abs(secondView.Zoom - 1) < 0.001,
                        "不同页面的独立视口丢失");
                    window.OpenStudioPageDocument(one.Id);
                    Assert(model.ActivePage.Id == one.Id, "Tab激活没有切换真实MEM页面");
                    Assert(session.Revision == initialPageRevision, "仅激活页面修改MEM Revision");
                    LayoutDocument firstDoc = window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().First(d => d.ContentId == first);
                    DockPanel panel = firstDoc.Content as DockPanel;
                    Assert(panel != null, "页面Tab未挂载真实画布");
                    StackPanel stack = panel.Children.OfType<StackPanel>().First();
                    Button add = stack.Children.OfType<WrapPanel>().First()
                        .Children.OfType<Button>().First(b =>
                            Object.Equals(b.Content, "添加矩形"));
                    int shapes = one.RootContent.DescendantsAndSelf().OfType<MRCRectangle>().Count();
                    add.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(one.RootContent.DescendantsAndSelf().OfType<MRCRectangle>().Count() == shapes + 1,
                        "页面Tab添加矩形没有经过MEM");
                    Assert(session.Revision > initialPageRevision, "真实画布操作未增加工程Revision");
                    int afterAdd = session.Revision;
                    window.OpenStudioPageDocument(two.Id);
                    Assert(two.RootContent.DescendantsAndSelf().OfType<MRCRectangle>().Count() == 0,
                        "页面一编辑串入页面二");
                    string xml = window.SerializeStudioLayout();
                    MainWindow.ValidateStudioLayoutXml(xml);
                    string path = Path.Combine(Path.GetTempPath(), "cxstudio-r06-" +
                        Guid.NewGuid().ToString("N") + ".xml");
                    try
                    {
                        window.SaveStudioLayout(path);
                        window.RestoreStudioLayout(path);
                        Assert(window.GetStudioContentIds().Contains(first) &&
                               window.GetStudioContentIds().Contains(second),
                            "工作区布局恢复丢失了两个页面Tab");
                        Assert(session.Revision == afterAdd, "恢复布局修改MEM Revision");
                        string malformed = xml.Replace(second,
                            "doc.page." + Guid.NewGuid().ToString("N"));
                        File.WriteAllText(path, malformed, new UTF8Encoding(false));
                        AssertThrows(() => window.RestoreStudioLayout(path));
                        Assert(session.Revision == afterAdd, "失效页面布局修改工程");
                        File.WriteAllText(path, xml, new UTF8Encoding(false));
                        window.CloseStudioDocument(first);
                        Assert(!window.GetStudioContentIds().Contains(first) &&
                            window.GetStudioPageScene(one.Id) == null, "关闭资源Tab未释放Editor");
                        window.OpenStudioPageDocument(one.Id);
                        Assert(window.GetStudioContentIds().Count(x => x == first) == 1,
                            "重开资源Tab重复注册");
                        // 资源树检视了页面B时，页面A仍是活动Tab；统一Ribbon不得误写B。
                        int firstCircles = one.RootContent.DescendantsAndSelf()
                            .OfType<MRCShapeElement>().Count(x => x.Shape.Kind == MRCShapeKind.Circle);
                        int secondCircles = two.RootContent.DescendantsAndSelf()
                            .OfType<MRCShapeElement>().Count(x => x.Shape.Kind == MRCShapeKind.Circle);
                        model.ActivePage = two;
                        Assert(window.ActiveStudioDocumentId == first,
                            "仅选中资源树节点不应暗中切走页面编辑Tab");
                        Button circleButton = window.StudioRibbonActions.Children.OfType<Button>()
                            .First(b => Convert.ToString(b.Content) == "圆");
                        circleButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                        Assert(model.ActivePage.Id == one.Id &&
                            one.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>()
                                .Count(x => x.Shape.Kind == MRCShapeKind.Circle) == firstCircles + 1 &&
                            two.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>()
                                .Count(x => x.Shape.Kind == MRCShapeKind.Circle) == secondCircles,
                            "动态Ribbon没有将真实图元编辑限定到活动页面Tab");
                        window.CloseStudioDocument(first);
                        LayoutDocument afterClose = window.StudioDock.Layout.ActiveContent as LayoutDocument;
                        Assert(afterClose != null && afterClose.ContentId != first &&
                            window.ActiveStudioDocumentId == afterClose.ContentId,
                            "关闭当前页面Tab后中央文档与Ribbon上下文失去一致性");
                        window.OpenStudioPageDocument(one.Id);
                        // 真实工程保存→重新打开：只能释放旧工程页面Tab，不得抢走固定流程文档。
                        string projectPath = Path.Combine(Path.GetTempPath(),
                            "cxstudio-r06-project-" + Guid.NewGuid().ToString("N") + ".cxproj");
                        try
                        {
                            session.SaveAs(projectPath);
                            window.OpenStudioDocument("doc.flow");
                            Assert(window.StudioDock.Layout.ActiveContent.ContentId == "doc.flow",
                                "切换工程前流程文档未激活");
                            model.OpenProject(projectPath, true);
                            Assert(window.StudioDock.Layout.ActiveContent.ContentId == "doc.flow" &&
                                window.ActiveStudioDocumentId == "doc.flow",
                                "更换MEM工程误把固定流程文档切回首页");
                            Assert(!window.GetStudioContentIds().Contains(first) &&
                                !window.GetStudioContentIds().Contains(second),
                                "切换工程后仍保留旧MEM会话页面Tab");
                            // 从活动的动态页面切换工程：需要恢复真实doc.visual和绘图Ribbon。
                            MEMPage reloadedPage = model.ProjectSession.Project.Pages.First(p => p.Name == one.Name);
                            string reloadedId = window.OpenStudioPageDocument(reloadedPage.Id);
                            Assert(window.StudioDock.Layout.ActiveContent.ContentId == reloadedId,
                                "保存重开后页面文档无法恢复编辑");
                            model.OpenProject(projectPath, true);
                            Assert(!window.GetStudioContentIds().Contains(reloadedId) &&
                                window.StudioDock.Layout.ActiveContent is LayoutDocument &&
                                window.StudioDock.Layout.ActiveContent.ContentId == "doc.visual" &&
                                window.ActiveStudioDocumentId == "doc.visual",
                                "从活动页面切换MEM会话未激活有效的固定HMI编辑器；实际中央=" +
                                Convert.ToString(window.StudioDock.Layout.ActiveContent == null
                                    ? null : window.StudioDock.Layout.ActiveContent.ContentId) +
                                "，Ribbon上下文=" + window.ActiveStudioDocumentId);
                        }
                        finally { if (File.Exists(projectPath)) File.Delete(projectPath); }
                    }
                    finally { if (File.Exists(path)) File.Delete(path); }
                });
                CheckStep("P0-2 Content与Symbol独立资源编辑Tab、实例及工程历史", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P0-2 AI共享内容");
                    MEMSymbol symbol=session.AddSymbol("P0-2 电机组件");
                    MEMPage page=session.AddPage("P0-2 HMI");
                    MRIResourceReference use=session.PlaceSymbol(page.RootContent,symbol,"电机A");
                    string contentTab=window.OpenStudioResourceDocument(content.Id);
                    string symbolTab=window.OpenStudioResourceDocument(symbol.Id);
                    string pageTab=window.OpenStudioResourceDocument(page.Id);
                    Assert(contentTab!=symbolTab&&symbolTab!=pageTab&&
                        window.GetStudioContentIds().Contains(contentTab)&&
                        window.GetStudioContentIds().Contains(symbolTab),"真实资源文档未区分身份");
                    SceneView c=window.GetStudioResourceScene(content.Id);
                    SceneView m=window.GetStudioResourceScene(symbol.Id);
                    Assert(c!=null&&m!=null&&!Object.ReferenceEquals(c,m),
                        "Content与Symbol仍共享一张画布");
                    c.SetViewport(1.4,0,0);
                    Assert(Math.Abs(c.Zoom-1.4)<.001&&Math.Abs(m.Zoom-1)<.001,
                        "跨资源Tab的视口串位");
                    window.OpenStudioResourceDocument(symbol.Id);
                    LayoutDocument symbolDoc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().First(x=>x.ContentId==symbolTab);
                    DockPanel panel=symbolDoc.Content as DockPanel;
                    Button add=panel.Children.OfType<StackPanel>().First()
                        .Children.OfType<WrapPanel>().First().Children.OfType<Button>()
                        .First(x=>Object.Equals(x.Content,"添加矩形"));
                    int before=session.Revision;
                    add.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(symbol.RootGroup.DescendantsAndSelf().OfType<MRCRectangle>().Any()&&
                        !content.DescendantsAndSelf().OfType<MRCRectangle>().Any()&&
                        session.Revision==before+1,"Symbol图元没有通过唯一MEM事务提交");
                    session.Undo().RequireValue();
                    Assert(!symbol.RootGroup.DescendantsAndSelf().OfType<MRCRectangle>().Any(),
                        "跨资源编辑未使用同一工程Undo");
                    window.OpenStudioResourceDocument(symbol.Id);
                    Assert(window.GetStudioContentIds().Count(x=>x==symbolTab)==1,
                        "重复打开同一个Symbol产生多个Tab");
                    string layout=Path.Combine(Path.GetTempPath(),"cxstudio-p02-"+
                        Guid.NewGuid().ToString("N")+".xml");
                    try
                    {
                        window.SaveStudioLayout(layout);
                        window.RestoreStudioLayout(layout);
                        Assert(window.GetStudioContentIds().Contains(symbolTab)&&
                            window.GetStudioContentIds().Contains(contentTab),
                            "恢复布局丢失共享Content/Symbol真实资源Tab");
                        string xml=File.ReadAllText(layout);
                        File.WriteAllText(layout,xml.Replace(symbolTab,
                            "doc.symbol."+Guid.NewGuid().ToString("N")));
                        AssertThrows(()=>window.RestoreStudioLayout(layout));
                    }
                    finally{if(File.Exists(layout))File.Delete(layout);}
                    Assert(session.FindReference(use.Id)!=null,"工程实例被UI操作改变");
                    window.CloseStudioDocument(contentTab);
                    Assert(window.GetStudioResourceScene(content.Id)==null,
                        "关闭Content仍持有可编辑视图");
                    window.OpenStudioResourceDocument(content.Id);
                });
                CheckStep("P0-2 主子流程独立MOL画布、真实事务及失效资源清理", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession s=model.ProjectSession;
                    MEMContent main=s.AddContent(s.Project,"P0-2 检测主流程");
                    MEMContent ai=s.AddContent(s.Project,"P0-2 AI子流程");
                    MEMContent cv=s.AddContent(s.Project,"P0-2 CV子流程");
                    MOLLogicDefinition definition=new MOLLogicDefinition(
                        MOLControlBlock.Sequence(MOLControlBlock.Action("Acquire"),
                            MOLControlBlock.Action("Check")));
                    MOLLogicCodec.Set(s,main,definition);
                    MOLLogicCodec.Set(s,ai,definition);
                    MOLLogicCodec.Set(s,cv,definition);
                    int flowRevision=s.Revision;
                    string m=window.OpenStudioResourceDocument(main.Id,true);
                    string a=window.OpenStudioResourceDocument(ai.Id,true);
                    string c=window.OpenStudioResourceDocument(cv.Id,true);
                    Assert(m!=a&&a!=c&&window.GetStudioContentIds().Contains(m)&&
                        window.GetStudioContentIds().Contains(a)&&
                        window.GetStudioContentIds().Contains(c),"主/AI/CV仍共用流程Tab");
                    MOLFlowCanvas first=window.GetStudioResourceFlow(main.Id);
                    MOLFlowCanvas second=window.GetStudioResourceFlow(ai.Id);
                    Assert(first!=null&&second!=null&&first.Definition!=null&&
                        second.Definition!=null&&!Object.ReferenceEquals(first,second),
                        "流程Tab未从各自MEM Content中投影真正MOL定义");
                    window.OpenStudioResourceDocument(main.Id,true);
                    Assert(window.ActiveStudioDocumentId==m,"打开主流程未激活实际Tab");
                    Assert(s.Revision==flowRevision,"切换流程资源修改了工程");
                    string visual=window.OpenStudioResourceDocument(main.Id,false);
                    Assert(visual!=m&&window.GetStudioResourceScene(main.Id)!=null,
                        "同一Content缺少独立图元/逻辑编辑模式");
                    window.OpenStudioResourceDocument(main.Id,true);
                    s.Remove(cv);
                    Assert(!window.GetStudioContentIds().Contains(c)&&
                        window.GetStudioResourceFlow(cv.Id)==null,
                        "删除子流程仍保留非法编辑文档");
                    Assert(window.GetStudioContentIds().Contains(m)&&
                        window.GetStudioContentIds().Contains(a),
                        "删除CV误关闭了其他合法子流程Tab");
                });
                CheckStep("P0-2 资源Dirty与引用影响独立标记和保存复位", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent changed=session.AddContent(session.Project,"P0-2 被共享的算法");
                    MEMContent unrelated=session.AddContent(session.Project,"P0-2 无关算法");
                    MEMPage dependent=session.AddPage("P0-2 使用方HMI");
                    session.PlaceContent(dependent.RootContent,changed,"使用算法");
                    string target=window.OpenStudioResourceDocument(changed.Id);
                    string other=window.OpenStudioResourceDocument(unrelated.Id);
                    string user=window.OpenStudioResourceDocument(dependent.Id);
                    Func<string,string> title=id=>window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(d=>d.ContentId==id).Title;
                    string projectFile=Path.Combine(Path.GetTempPath(),
                        "cxstudio-dirty-"+Guid.NewGuid().ToString("N")+".cxproj");
                    try
                    {
                        model.SaveProject(projectFile);
                        Assert(!session.IsDirty&&!title(target).Contains(" *")&&
                            !title(other).Contains(" *")&&!title(user).Contains(" ~"),
                            "保存后工作区仍显示旧Dirty状态");
                        int before=session.Revision;
                        session.AddRectangle(changed,"参数图元",8,9);
                        Assert(session.Revision==before+1&&
                            title(target).Contains(" *")&&title(user).Contains(" ~")&&
                            !title(other).Contains(" *")&&!title(other).Contains(" ~"),
                            "不同资源的直接修改和引用影响未独立显示");
                        // 图元和流程文档共享同一Content真源与Dirty来源。
                        string flow=window.OpenStudioResourceDocument(changed.Id,true);
                        Assert(title(flow).Contains(" *"),"同资源流程Tab缺少真实Dirty状态");
                        model.SaveProject(projectFile);
                        Assert(!session.IsDirty&&!title(target).Contains(" *")&&
                            !title(user).Contains(" ~")&&!title(flow).Contains(" *"),
                            "保存后各Tab没有一起回到干净状态");
                        session.Undo().RequireValue();
                        Assert(title(target).Contains(" *")&&title(user).Contains(" ~"),
                            "Undo产生的新Revision未更新引用影响与Dirty提示");
                        session.Redo().RequireValue();
                        Assert(title(target).Contains(" *")&&title(user).Contains(" ~")&&
                            !title(other).Contains(" *")&&!title(other).Contains(" ~"),
                            "Redo恢复MEM对象后误丢失受影响资源或污染无关Tab");
                    }
                    finally{if(File.Exists(projectFile))File.Delete(projectFile);}
                });
                CheckStep("P0-3 真实AI/CV及Symbol资源Schema编辑、失效行拒绝、Undo与持久化", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent ai=session.AddContent(session.Project,"P0-3 AI算法");
                    MEMContent cv=session.AddContent(session.Project,"P0-3 CV后处理");
                    MEMSymbol motor=session.AddSymbol("P0-3 电机");
                    // 专业对象的数据契约仍使用MEM/MPM原生资源定义；不引入Studio专属Schema。
                    session.DefineProperty(ai,new MPMPropertyDescriptor("confidence",
                        MPMValueKind.Number,true,true,
                        MPMBinding.Literal(MPMValue.Number(0.75)),0,1));
                    session.DefineProperty(ai,new MPMPropertyDescriptor("modelName",
                        MPMValueKind.String,false,true,
                        MPMBinding.Literal(MPMValue.String("AI-model-v1"))));
                    session.DefineProperty(cv,new MPMPropertyDescriptor("minArea",
                        MPMValueKind.Integer,true,true,
                        MPMBinding.Literal(MPMValue.Integer(12)),1,1000));
                    session.DefineProperty(motor,new MPMPropertyDescriptor("enabled",
                        MPMValueKind.Boolean,true,true,
                        MPMBinding.Literal(MPMValue.Boolean(true))));
                    var logic=new MOLLogicDefinition(MOLControlBlock.Sequence(
                        MOLControlBlock.Action("Acquire"),MOLControlBlock.Action("Infer")));
                    MOLLogicCodec.Set(session,ai,logic);
                    string aiTab=window.OpenStudioResourceDocument(ai.Id,false);
                    Assert(model.SelectedResource!=null&&model.SelectedResource.Id==ai.Id,
                        "资源Tab没有定位AI真实Content");
                    var confidence=model.Properties.Single(x=>x.Member=="confidence");
                    Assert(confidence.OwnerId==ai.Id&&!confidence.Reference&&
                        confidence.Kind==MPMValueKind.Number&&confidence.Schema.Contains("范围"),
                        "未显示资源定义自己的MPM Schema");
                    int before=session.Revision;
                    confidence.Text="0.89";
                    model.ApplyPropertyCommand.Execute(confidence);
                    Assert(session.Revision==before+1&&
                        session.FindProperty(ai,"confidence").Binding.Value.AsNumber()==0.89,
                        "资源Tab的编辑命令没有经过MEM原生属性事务");
                    session.Undo().RequireValue();
                    Assert(session.FindProperty(ai,"confidence").Binding.Value.AsNumber()==0.75,
                        "AI参数属性编辑没有使用统一Undo");
                    session.Redo().RequireValue();
                    Assert(session.FindProperty(ai,"confidence").Binding.Value.AsNumber()==0.89,
                        "AI参数属性编辑没有使用统一Redo");
                    confidence=model.Properties.Single(x=>x.Member=="confidence");
                    int rejectRevision=session.Revision;
                    confidence.Text="1.7";
                    model.ApplyPropertyCommand.Execute(confidence);
                    Assert(session.Revision==rejectRevision&&
                        session.FindProperty(ai,"confidence").Binding.Value.AsNumber()==0.89,
                        "越界Schema参数错误提交工程");
                    StudioPropertyItem previous=model.Properties.Single(x=>x.Member=="modelName");
                    string cvTab=window.OpenStudioResourceDocument(cv.Id,false);
                    Assert(model.Properties.Any(x=>x.Member=="minArea"&&x.OwnerId==cv.Id)&&
                        !model.Properties.Any(x=>x.Member=="confidence"),
                        "切换资源后属性面板仍在显示AI旧Schema");
                    previous.Text="stale";
                    model.ApplyPropertyCommand.Execute(previous);
                    Assert(session.Revision==rejectRevision&&
                        session.FindProperty(ai,"modelName").Binding.Value.AsString()=="AI-model-v1",
                        "切换资源Tab后旧属性行仍可误写AI定义");
                    window.OpenStudioResourceDocument(ai.Id,true);
                    Assert(model.SelectedResource.Id==ai.Id&&
                        model.Properties.Any(x=>x.Member=="confidence"),
                        "切换真实MOL流程Tab未显示关联Content参数");
                    string motorTab=window.OpenStudioResourceDocument(motor.Id,false);
                    StudioPropertyItem enabled=model.Properties.Single(x=>x.Member=="enabled");
                    enabled.Text="false";
                    model.ApplyPropertyCommand.Execute(enabled);
                    Assert(session.FindProperty(motor,"enabled").Binding.Value.AsBoolean()==false,
                        "Symbol Boolean属性没有进入原MEM命令");
                    string file=Path.Combine(Path.GetTempPath(),"cxstudio-p03-"+
                        Guid.NewGuid().ToString("N")+".cxproj");
                    try
                    {
                        model.SaveProject(file);
                        MEMEngineeringSession reopened=MEMEngineering.Open(file);
                        MEMContent savedAi=reopened.Project.Contents.First(x=>x.Name==ai.Name);
                        MEMSymbol savedMotor=reopened.Project.Symbols.First(x=>x.Name==motor.Name);
                        Assert(reopened.FindProperty(savedAi,"confidence").Binding.Value.AsNumber()==0.89&&
                            !reopened.FindProperty(savedMotor,"enabled").Binding.Value.AsBoolean(),
                            "资源Schema编辑保存重开后丢失");
                        reopened.Close();
                    }
                    finally{if(File.Exists(file))File.Delete(file);}
                    Assert(window.GetStudioContentIds().Contains(aiTab)&&
                        window.GetStudioContentIds().Contains(cvTab)&&
                        window.GetStudioContentIds().Contains(motorTab),
                        "Schema应用打断多个资源文档的布局身份");
                });
                CheckStep("P0-3 页面内嵌Content真实流程Tab属性与公开资源身份", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMPage page=session.AddPage("P0-3 主产线");
                    MEMContent root=page.RootContent;
                    session.DefineProperty(root,new MPMPropertyDescriptor("limit",
                        MPMValueKind.Integer,true,true,
                        MPMBinding.Literal(MPMValue.Integer(5)),1,20));
                    var modelLogic=new MOLLogicDefinition(MOLControlBlock.Sequence(
                        MOLControlBlock.Action("Start"),MOLControlBlock.Action("Stop")));
                    MOLLogicCodec.Set(session,root,modelLogic);
                    int before=session.Revision;
                    string flowTab=window.OpenStudioResourceDocument(root.Id,true);
                    Assert(model.SelectedResource!=null&&model.SelectedResource.Id==root.Id,
                        "Page.RootContent的真实流程Tab未找到当前工程内嵌定义");
                    StudioPropertyItem limit=model.Properties.Single(x=>x.Member=="limit");
                    Assert(limit.OwnerId==root.Id&&!limit.IsReadOnly,
                        "内嵌流程仍未投影MEM原生Schema");
                    limit.Text="17";
                    model.ApplyPropertyCommand.Execute(limit);
                    Assert(session.Revision==before+1&&
                        session.FindProperty(root,"limit").Binding.Value.AsInteger()==17,
                        "Page子Content编辑未通过原工程事务");
                    Assert(window.GetStudioResourceFlow(root.Id)!=null&&
                        window.GetStudioContentIds().Contains(flowTab),
                        "原Page下的真实MOL画布被属性编辑破坏");
                });
                CheckStep("P0-4 MRK来源身份跳转AI/CV子流程及失效拒绝", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage page=session.AddPage("P0-4 X-Ray主流程");
                    MEMContent ai=session.AddContent(session.Project,"P0-4 AI子流程");
                    MEMContent cv=session.AddContent(session.Project,"P0-4 CV子流程");
                    MOLControlBlock aiBlock=MOLControlBlock.Action("AI");
                    MOLControlBlock cvBlock=MOLControlBlock.Action("CV");
                    var aiPorts=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>{
                        {aiBlock.Id,new[]{new MPCPortDescriptor("Done",
                            MPCPortDirection.Output,"signal",MPCPortKind.Control)}}};
                    var cvPorts=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>{
                        {cvBlock.Id,new[]{new MPCPortDescriptor("Done",
                            MPCPortDirection.Output,"signal",MPCPortKind.Control)}}};
                    MOLWiringCodec.Set(session,ai,new MOLWiringDefinition(
                        aiBlock.Id,new[]{aiBlock},aiPorts,new MOLLink[0]));
                    MOLWiringCodec.Set(session,cv,new MOLWiringDefinition(
                        cvBlock.Id,new[]{cvBlock},cvPorts,new MOLLink[0]));
                    session.PlaceContent(page.RootContent,ai,"AI检测调用");
                    MRIResourceReference cvUse=session.PlaceContent(page.RootContent,cv,"CV检测调用");
                    window.OpenStudioPageDocument(page.Id);
                    int traceRevision=session.Revision;
                    Assert(window.LocateStudioFlowSource(ai.Id,aiBlock.Id),
                        "AI原始来源没有打开子流程Tab");
                    string aiTab=window.ActiveStudioDocumentId;
                    MOLFlowCanvas aiCanvas=window.GetStudioResourceFlow(ai.Id);
                    Assert(aiCanvas!=null&&aiCanvas.SelectedBlockIds.SequenceEqual(
                        new[]{aiBlock.Id})&&aiTab=="doc.resourceflow."+ai.Id.ToString("N"),
                        "AI轨迹错误定位到父流程或未选中原块");
                    Assert(window.LocateStudioFlowSource(cv.Id,cvBlock.Id),
                        "CV原始来源没有打开子流程Tab");
                    string cvTab=window.ActiveStudioDocumentId;
                    Assert(cvTab!=aiTab&&window.GetStudioResourceFlow(cv.Id)
                        .SelectedBlockIds.Single()==cvBlock.Id,
                        "AI/CV子流程错误共享节点选择");
                    Assert(!window.LocateStudioFlowSource(ai.Id,cvBlock.Id)&&
                        window.ActiveStudioDocumentId==cvTab,
                        "错误BlockId误切换编辑器");
                    Assert(!window.LocateStudioFlowSource(Guid.NewGuid(),aiBlock.Id)&&
                        window.ActiveStudioDocumentId==cvTab,
                        "跨工程无效来源误切换编辑器");
                    Assert(session.Revision==traceRevision,
                        "来源定位错误修改MEM Revision");
                    AssertThrows(()=>session.Remove(cv));
                    Assert(session.Revision==traceRevision,
                        "被引用的CV定义非法删除修改了工程");
                    session.Remove(cvUse);
                    session.Remove(cv);
                    Assert(!window.LocateStudioFlowSource(cv.Id,cvBlock.Id),
                        "已删除子流程仍能错误导航");
                });
                CheckStep("P0-4 运行计划工程身份与Revision失效拒绝", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent ai=session.Project.Contents.Single(x=>x.Name=="P0-4 AI子流程");
                    MOLWiringDefinition wiring=MOLWiringCodec.Get(
                        session.GetSnapshot().Find(ai.Id));
                    Guid originalBlock=wiring.Blocks.Single().Id;
                    int compiledRevision=session.Revision;
                    Guid compiledProject=session.Project.Id;
                    Assert(window.LocateStudioFlowSource(compiledProject,compiledRevision,
                        ai.Id,originalBlock),"有效MOC版本来源被错误拒绝");
                    string active=window.ActiveStudioDocumentId;
                    Assert(!window.LocateStudioFlowSource(Guid.NewGuid(),compiledRevision,
                        ai.Id,originalBlock)&&window.ActiveStudioDocumentId==active,
                        "其他工程Plan的来源错误进入当前工程画布");
                    Assert(!window.LocateStudioFlowSource(compiledProject,
                        compiledRevision+1,ai.Id,originalBlock)&&
                        window.ActiveStudioDocumentId==active,
                        "未来版本Plan的来源错误进入当前工程画布");
                    session.AddContent(session.Project,"P0-4 Plan版本变更验证");
                    Assert(session.Revision!=compiledRevision&&
                        !window.LocateStudioFlowSource(compiledProject,compiledRevision,
                            ai.Id,originalBlock)&&
                        window.ActiveStudioDocumentId==active,
                        "工程修改后旧Plan来源被错误解释为新工程节点");
                });
                CheckStep("P0-4 构建诊断到AI子流程原始MOL节点与版本隔离", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent ai=session.Project.Contents.Single(x=>x.Name=="P0-4 AI子流程");
                    Guid blockId=MOLWiringCodec.Get(session.GetSnapshot().Find(ai.Id))
                        .Blocks.Single().Id;
                    Type type=typeof(MainWindow).GetNestedType("FlowDiagnosis",
                        System.Reflection.BindingFlags.NonPublic);
                    Assert(type!=null,"构建诊断类型不存在");
                    object item=Activator.CreateInstance(type,true);
                    type.GetProperty("ProjectId").SetValue(item,session.Project.Id,null);
                    type.GetProperty("EngineeringRevision").SetValue(item,session.Revision,null);
                    type.GetProperty("ContentId").SetValue(item,ai.Id,null);
                    type.GetProperty("BlockId").SetValue(item,(Guid?)blockId,null);
                    type.GetProperty("Label").SetValue(item,"P0-4 AI来源验证",null);
                    ComboBox list=window.FindName("FlowDiagnosticList") as ComboBox;
                    Assert(list!=null,"构建诊断界面缺失");
                    list.ItemsSource=new[]{item};list.SelectedIndex=0;
                    var locate=typeof(MainWindow).GetMethod("OnFlowLocateDiagnostic",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    Assert(locate!=null,"构建诊断入口缺失");
                    int diagnosticRevision=session.Revision;
                    locate.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(window.ActiveStudioDocumentId==
                        "doc.resourceflow."+ai.Id.ToString("N")&&
                        window.GetStudioResourceFlow(ai.Id).SelectedBlockIds.Single()==blockId&&
                        session.Revision==diagnosticRevision,
                        "构建诊断未正确下钻原AI流程或修改了工程");
                    string active=window.ActiveStudioDocumentId;
                    session.AddContent(session.Project,"P0-4 诊断版本变更");
                    locate.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(window.ActiveStudioDocumentId==active&&
                        ((TextBlock)window.FindName("LogicInfo")).Text.Contains("Revision"),
                        "过期构建诊断错误下钻当前工程节点");
                });
                CheckStep("P0 Editor跨资源九Shape/Widget与统一MEM历史路由", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P0统一图元算法");
                    MEMSymbol symbol=session.AddSymbol("P0统一电机组件");
                    MEMPage page=session.AddPage("P0界面验证页");
                    string contentTab=window.OpenStudioResourceDocument(content.Id,false);
                    string[] names={"直线","矩形","圆","椭圆","折线","多边形","圆弧","贝塞尔","自由路径"};
                    foreach(string name in names)
                        window.StudioRibbonActions.Children.OfType<Button>()
                            .Single(x=>Convert.ToString(x.Content)==name)
                            .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    var shapes=content.DescendantsAndSelf().OfType<MRCShapeElement>().ToArray();
                    Assert(shapes.Length==9&&Enum.GetValues(typeof(MRCShapeKind))
                        .Cast<MRCShapeKind>().All(kind=>shapes.Any(x=>x.Shape.Kind==kind)),
                        "Content资源Tab未使用现有九Shape Catalog及MEM事务");
                    window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="按钮Widget")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(content.DescendantsAndSelf().OfType<MRCWidgetElement>().Count()==1,
                        "Content资源Tab未插入真正Widget");
                    window.OpenStudioResourceDocument(symbol.Id,false);
                    Button circle=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="圆");
                    int symbolRevisionBefore=session.Revision;
                    model.ActivePage=page; // 非活动检视不能取代活动资源Tab身份
                    circle.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==symbolRevisionBefore+1&&
                        symbol.RootGroup.DescendantsAndSelf().OfType<MRCShapeElement>()
                            .Count(x=>x.Shape.Kind==MRCShapeKind.Circle)==1&&
                        content.DescendantsAndSelf().OfType<MRCShapeElement>().Count()==9&&
                        !page.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "Ribbon绕过活动资源Tab写错MEM对象");
                    session.Undo().RequireValue();
                    Assert(!symbol.RootGroup.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "独立资源绘图未共用原MEM Undo");
                    session.Redo().RequireValue();
                    Assert(symbol.RootGroup.DescendantsAndSelf().OfType<MRCShapeElement>().Count()==1,
                        "资源绘图重做没有使用原MEM Redo");
                    window.OpenStudioResourceDocument(content.Id,true);
                    int flowRevision=session.Revision;
                    circle.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==flowRevision&&
                        content.DescendantsAndSelf().OfType<MRCShapeElement>().Count()==9,
                        "MOL流程Tab的旧Ribbon命令错误修改图元");
                    window.OpenStudioResourceDocument(content.Id,false);
                    Assert(window.ActiveStudioDocumentId==contentTab&&
                        window.GetStudioContentIds().Count(x=>x==contentTab)==1,
                        "复用图元Tab造成重复Editor或丢失活动身份");
                });
                CheckStep("P0-4 X-Ray真实MOC与MRK主子流程Trace到Studio全链定位", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage xrayPage=session.AddPage("P0-4 真实执行XRay");
                    vm.ActivePage=xrayPage;
                    window.OpenStudioDocument("doc.flow");
                    var create=typeof(MainWindow).GetMethod("OnCreateXRayDualSubflowDemo",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    Assert(create!=null,"X-Ray主子流程编辑入口不存在");
                    create.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    MEMNodeSnapshot rootSnapshot=session.GetSnapshot().Find(xrayPage.RootContent.Id);
                    MOLWiringDefinition rootWiring=MOLWiringCodec.Get(rootSnapshot);
                    Assert(rootWiring!=null&&rootSnapshot.References.Count==2,
                        "X-Ray入口没有真正创建两份子Content引用");
                    MRIReferenceSnapshot aiReference=rootSnapshot.References.Single(r=>
                        session.Find(r.TargetId).Name.Contains("AI"));
                    MRIReferenceSnapshot cvReference=rootSnapshot.References.Single(r=>
                        session.Find(r.TargetId).Name.Contains("传统"));
                    Guid aiContentId=aiReference.TargetId;
                    Guid cvContentId=cvReference.TargetId;
                    Guid aiBlockId=MOLWiringCodec.Get(session.GetSnapshot().Find(aiContentId))
                        .Blocks.First(b=>b.CapabilityId=="AI").Id;
                    Guid cvBlockId=MOLWiringCodec.Get(session.GetSnapshot().Find(cvContentId))
                        .Blocks.First(b=>b.CapabilityId=="CV").Id;
                    Guid aiCallId=rootWiring.Blocks.Single(b=>
                        b.CapabilityId=="moc.invoke:"+aiContentId.ToString("N")).Id;
                    var resolverField=typeof(MainWindow).GetField("_simulation",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    Assert(resolverField!=null,"缺少已注册的CPU仿真Provider");
                    IMRKCapabilityResolver resolver=(IMRKCapabilityResolver)resolverField.GetValue(window);
                    MEMProjectSnapshot fixedSnapshot=session.GetSnapshot();
                    MOCBuildResult checkedBuild=new MOCBuildService().Build(
                        fixedSnapshot,xrayPage.RootContent.Id,resolver);
                    Assert(checkedBuild.Succeeded,"真实X-Ray MOC编译未通过: "+
                        string.Join(";",checkedBuild.Diagnostics.Select(d=>d.Message)));
                    MOCCompiledPlan compiled=checkedBuild.Plan;
                    Assert(compiled.ProjectId==session.Project.Id&&
                        compiled.EngineeringRevision==session.Revision,
                        "MOC没有固定本次工程来源Revision");
                    MOCObservedStep observedAI=null;
                    MOCObservedStep observedCV=null;
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(3,64));
                    try
                    {
                        MRKFlowHandle execution=engine.StartFlow(compiled.Plan);
                        MRKFlowResult completed=execution.Completion.GetAwaiter().GetResult();
                        MOCTraceView trace=compiled.ReadTrace(execution,0);
                        observedAI=trace.Events.FirstOrDefault(e=>
                            e.Origin.ContentAddress.ObjectId==aiContentId&&
                            e.Origin.BlockId==aiBlockId&&
                            e.Transition.State==MRKStepState.Completed);
                        observedCV=trace.Events.FirstOrDefault(e=>
                            e.Origin.ContentAddress.ObjectId==cvContentId&&
                            e.Origin.BlockId==cvBlockId&&
                            e.Transition.State==MRKStepState.Completed);
                        Assert(completed.State==MRKFlowState.Completed&&
                            observedAI!=null&&observedCV!=null&&
                            observedAI.Origin.CallPath.Contains(aiCallId),
                            "实际MRK执行没有产生含原始Content与调用链的AI/CV Trace");
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                    MainWindow.FlowTraceRecord line=MainWindow.ProjectFlowTrace(compiled,observedAI);
                    Assert(line.ContentId==aiContentId&&line.BlockId==aiBlockId&&
                        line.ProjectId==session.Project.Id&&
                        line.EngineeringRevision==compiled.EngineeringRevision&&
                        line.CallPath.Contains(aiCallId),
                        "Studio轨迹投影丢失真实MOC来源身份");
                    ComboBox list=window.FindName("FlowTraceList") as ComboBox;
                    Assert(list!=null,"Studio运行轨迹列表不存在");
                    list.ItemsSource=new[]{line};list.SelectedIndex=0;
                    var parent=typeof(MainWindow).GetMethod("OnFlowLocateTrace",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    var source=typeof(MainWindow).GetMethod("OnFlowLocateSourceContent",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    var visual=typeof(MainWindow).GetMethod("OnFlowLocateVisual",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    Assert(parent!=null&&source!=null&&visual!=null,
                        "Studio运行来源与HMI导航入口未完整接入");
                    int stableRevision=session.Revision;
                    window.OpenStudioDocument("doc.flow");
                    parent.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(((MOLFlowCanvas)window.FindName("LogicHost")).SelectedBlockIds
                        .Contains(aiCallId),"真实Trace未定位主流程AI调用节点");
                    source.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(window.ActiveStudioDocumentId==
                        "doc.resourceflow."+aiContentId.ToString("N")&&
                        window.GetStudioResourceFlow(aiContentId).SelectedBlockIds
                            .Contains(aiBlockId),
                        "真实Trace未下钻AI原始Content中的执行块");
                    visual.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(window.ActiveStudioDocumentId=="doc.visual"&&
                        vm.SelectedId.HasValue&&session.Revision==stableRevision,
                        "真实Trace→HMI定位失败或导航更改了MEM工程");
                    string active=window.ActiveStudioDocumentId;
                    session.AddContent(session.Project,"P0-4 重新构建版本反证");
                    parent.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    source.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    visual.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(window.ActiveStudioDocumentId==active&&
                        ((TextBlock)window.FindName("LogicInfo")).Text.Contains("重新构建"),
                        "旧MOC Plan的父、子、HMI定位没有被工程新Revision拒绝");
                    MOCBuildResult rebuilt=new MOCBuildService().Build(
                        session.GetSnapshot(),xrayPage.RootContent.Id,resolver);
                    Assert(rebuilt.Succeeded&&
                        rebuilt.Plan.EngineeringRevision==session.Revision&&
                        rebuilt.Plan.EngineeringRevision!=compiled.EngineeringRevision,
                        "工程修改后没有产生对应新Revision的可执行Plan");
                });
                CheckStep("P0-4 AI真实MRK失败追溯与保存重开后重构建", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage xray=session.Project.Pages.Single(p=>
                        p.Name=="P0-4 真实执行XRay");
                    MEMNodeSnapshot root=session.GetSnapshot().Find(xray.RootContent.Id);
                    MRIReferenceSnapshot ai=root.References.Single(r=>
                        session.Find(r.TargetId).Name.Contains("AI"));
                    Guid aiId=ai.TargetId;
                    Guid blockId=MOLWiringCodec.Get(session.GetSnapshot().Find(aiId))
                        .Blocks.First(x=>x.CapabilityId=="AI").Id;
                    var providerField=typeof(MainWindow).GetField("_simulation",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    IMRKCapabilityResolver normal=(IMRKCapabilityResolver)providerField.GetValue(window);
                    MEMProjectSnapshot fixedVersion=session.GetSnapshot();
                    MOCBuildResult failedBuild=new MOCBuildService().Build(
                        fixedVersion,xray.RootContent.Id,new InjectedXRayFailureResolver(normal));
                    Assert(failedBuild.Succeeded,"AI异常注入不得破坏静态MOC构建");
                    int beforeRun=session.Revision;
                    MRKFlowResult failedResult=null;
                    MOCObservedStep failedEvent=null;
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(3,64));
                    try
                    {
                        MRKFlowHandle job=engine.StartFlow(failedBuild.Plan.Plan);
                        failedResult=job.Completion.GetAwaiter().GetResult();
                        MOCTraceView actual=failedBuild.Plan.ReadTrace(job,0);
                        failedEvent=actual.Events.FirstOrDefault(e=>
                            e.Origin.ContentAddress.ObjectId==aiId&&
                            e.Origin.BlockId==blockId&&
                            e.Transition.State==MRKStepState.Failed);
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                    Assert(failedResult.State==MRKFlowState.Failed&&failedResult.Error!=null&&
                        failedResult.Error.ToString().Contains("p04.ai.failure")&&
                        failedResult.CommittedOutputs.Count==0&&failedEvent!=null&&
                        session.Revision==beforeRun,
                        "真实AI执行故障丢失MRK失败来源、泄漏输出或修改MEM工程");
                    MainWindow.FlowTraceRecord line=MainWindow.ProjectFlowTrace(
                        failedBuild.Plan,failedEvent);
                    ComboBox list=window.FindName("FlowTraceList") as ComboBox;
                    list.ItemsSource=new[]{line};list.SelectedIndex=0;
                    var source=typeof(MainWindow).GetMethod("OnFlowLocateSourceContent",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    source.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(window.ActiveStudioDocumentId=="doc.resourceflow."+aiId.ToString("N")&&
                        window.GetStudioResourceFlow(aiId).SelectedBlockIds.Single()==blockId&&
                        session.Revision==beforeRun,
                        "AI故障诊断无法回到原Content和失败节点");
                    string file=Path.Combine(Path.GetTempPath(),"CxVisual-XRay-P04-"+
                        Guid.NewGuid().ToString("N")+".xml");
                    try
                    {
                        session.SaveAs(file);
                        MEMEngineeringSession reopened=MEMEngineering.Open(file);
                        try
                        {
                            MEMProjectSnapshot saved=reopened.GetSnapshot();
                            Assert(saved.Find(aiId)!=null&&saved.Find(xray.RootContent.Id)!=null&&
                                MOLWiringCodec.Get(saved.Find(aiId)).Blocks.Any(x=>x.Id==blockId),
                                "工程保存重开丢失AI子流程及故障SourceMap稳定身份");
                            MOCBuildResult restoreBuild=new MOCBuildService().Build(
                                saved,xray.RootContent.Id,normal);
                            Assert(restoreBuild.Succeeded&&
                                restoreBuild.Plan.SourceMap.Find(blockId).Any(x=>
                                    x.ContentAddress.ObjectId==aiId)&&
                                restoreBuild.Plan.ProjectId==saved.ProjectId,
                                "工程重开后不能通过同一MOC编译恢复AI原始来源");
                        }
                        finally{reopened.Close(true);}
                    }
                    finally
                    {
                        if(File.Exists(file))File.Delete(file);
                        if(File.Exists(file+".cx-lock"))File.Delete(file+".cx-lock");
                    }
                });
                CheckStep("P0 Editor活动多选对齐分布与图层顺序统一事务", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P0排列编辑Content");
                    MEMPage unrelated=session.AddPage("P0排列隔离Page");
                    string tab=window.OpenStudioResourceDocument(content.Id,false);
                    Button rect=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="矩形");
                    for(int i=0;i<3;i++)
                        rect.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MRCShapeElement[] originals=content.DescendantsAndSelf()
                        .OfType<MRCShapeElement>().Where(x=>x.Shape.Kind==MRCShapeKind.Rect).ToArray();
                    Assert(originals.Length==3&&originals.Select(x=>x.X).Distinct().Count()==3,
                        "排列测试未创建三个真实独立图元");
                    Guid[] selected=originals.Select(x=>x.Id).ToArray();
                    double[] beforeX=originals.Select(x=>x.X).ToArray();
                    SceneView scene=window.GetStudioResourceScene(content.Id);
                    scene.SetSelectedVisualIds(selected);
                    model.ActivePage=unrelated; // 活动Tab不变，旧检视目标不得被排列操作修改
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="排列")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button align=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="左对齐");
                    int beforeRevision=session.Revision;
                    align.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==beforeRevision+1&&
                        selected.Select(id=>(session.Find(id) as MRCShapeElement).X)
                            .Distinct().Count()==1&&
                        !unrelated.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "排列Ribbon未针对活动资源多选一次提交或误写其他页面");
                    session.Undo().RequireValue();
                    Assert(selected.Select((id,i)=>
                        Math.Abs((session.Find(id) as MRCShapeElement).X-beforeX[i])<1e-8)
                            .All(x=>x),"排列不是单步可撤销MEM事务");
                    session.Redo().RequireValue();
                    Assert(selected.Select(id=>(session.Find(id) as MRCShapeElement).X)
                        .Distinct().Count()==1,"Redo后未恢复对齐");
                    // 单选时Align必须拒绝：与旧页面选择/内容Tab选区无关。
                    scene.SetSelectedVisualIds(new[]{selected[0]});
                    int unchanged=session.Revision;
                    align.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==unchanged,"不足两个元素竟创建排列事务");
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="图层")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button front=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="置于顶层");
                    scene.SetSelectedVisualIds(new[]{selected[0]});
                    int zRevision=session.Revision;
                    front.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MRCShapeElement first=session.Find(selected[0]) as MRCShapeElement;
                    Assert(session.Revision==zRevision+1&&
                        first.Parent.Children.Last().Id==first.Id,
                        "活动资源图层命令未通过原MEM重排");
                    session.Undo().RequireValue();
                    Assert((session.Find(selected[0]) as MRCShapeElement).Parent.Children.Last().Id!=
                        selected[0],"撤销未恢复原Z顺序");
                    session.Redo().RequireValue();
                    Assert((session.Find(selected[0]) as MRCShapeElement).Parent.Children.Last().Id==
                        selected[0],"重做未恢复前置顺序");
                    window.OpenStudioResourceDocument(content.Id,true);
                    int flowRevision=session.Revision;
                    align.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    front.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==flowRevision,
                        "流程Tab借由旧排列/图层按钮错误修改可视对象");
                    window.OpenStudioResourceDocument(content.Id,false);
                    Assert(window.ActiveStudioDocumentId==tab,"返回图元Tab丢失稳定资源身份");
                });
                CheckStep("P0 独立资源几何属性路径及失效图元拒绝与撤销", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P0精细几何Content");
                    MEMPage other=session.AddPage("P0几何其他Page");
                    string key=window.OpenStudioResourceDocument(content.Id,false);
                    Button shapeButton=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(b=>Convert.ToString(b.Content)=="自由路径");
                    shapeButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MRCShapeElement path=content.DescendantsAndSelf().OfType<MRCShapeElement>()
                        .Single(x=>x.Shape.Kind==MRCShapeKind.Path);
                    SceneView canvas=window.GetStudioResourceScene(content.Id);
                    canvas.SetSelectedVisualIds(new[]{path.Id});
                    model.ActivePage=other;
                    var geo=typeof(MainWindow).GetMethod("RouteStudioResourceGeometry",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    var editPath=typeof(MainWindow).GetMethod("RouteStudioResourcePath",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    Assert(geo!=null&&editPath!=null,"Studio缺失统一几何/路径路由");
                    int start=session.Revision;
                    Assert((bool)geo.Invoke(window,new object[]{128d,92d,170d,106d,25d})&&
                        session.Revision==start+1&&
                        Math.Abs((session.Find(path.Id) as MRCShapeElement).X-128)<1e-8&&
                        !other.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "独立Content几何修改没有通过MEM事务或误改其他Page");
                    session.Undo().RequireValue();
                    Assert(Math.Abs((session.Find(path.Id) as MRCShapeElement).X-128)>1e-8,
                        "几何属性无法单步撤销");
                    session.Redo().RequireValue();
                    int pathRevision=session.Revision;
                    Assert((bool)editPath.Invoke(window,new object[]{1,"split"})&&
                        session.Revision==pathRevision+1,
                        "路径分段操作未通过当前资源原MEM事务");
                    session.Undo().RequireValue();
                    canvas.SetSelectedVisualIds(new[]{Guid.NewGuid()});
                    int reject=session.Revision;
                    geo.Invoke(window,new object[]{22d,30d,50d,50d,0d});
                    Assert(session.Revision==reject,"失效选中图元错误提交几何");
                    window.OpenStudioResourceDocument(content.Id,true);
                    geo.Invoke(window,new object[]{22d,30d,50d,50d,0d});
                    editPath.Invoke(window,new object[]{1,"delete"});
                    Assert(session.Revision==reject,
                        "流程Tab错误继承可视编辑命令");
                    window.OpenStudioResourceDocument(content.Id,false);
                    Assert(window.ActiveStudioDocumentId==key,"重入几何资源Tab丢失身份");
                });
                CheckStep("P0 独立图元快捷键遵守真实Tab焦点与MEM历史", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMSymbol symbol=session.AddSymbol("P0快捷键Symbol");
                    window.OpenStudioResourceDocument(symbol.Id,false);
                    Button rect=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(b=>Convert.ToString(b.Content)=="矩形");
                    rect.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MRCShapeElement shape=symbol.RootGroup.DescendantsAndSelf()
                        .OfType<MRCShapeElement>().Single();
                    SceneView canvas=window.GetStudioResourceScene(symbol.Id);
                    canvas.SetSelectedVisualIds(new[]{shape.Id});
                    int before=session.Revision;
                    Assert(window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.Delete,System.Windows.Input.ModifierKeys.None)&&
                        session.Revision==before+1&&session.Find(shape.Id)==null,
                        "Delete没有删除活动Symbol图元");
                    Assert(window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.Z,System.Windows.Input.ModifierKeys.Control)&&
                        session.Find(shape.Id)!=null,
                        "Ctrl+Z没用MEM历史撤销活动资源删除");
                    Assert(window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.Y,System.Windows.Input.ModifierKeys.Control)&&
                        session.Find(shape.Id)==null,
                        "Ctrl+Y没用MEM历史重做删除");
                    MEMContent flowContent=session.AddContent(session.Project,"P0快捷键Flow");
                    window.OpenStudioResourceDocument(flowContent.Id,true);
                    int flow=session.Revision;
                    window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.Delete,System.Windows.Input.ModifierKeys.None);
                    Assert(session.Revision==flow,
                        "Flow Tab错误继承视觉Delete快捷键");
                });
                CheckStep("P0 独立MOL子流程Ribbon能力Join布局与快捷键统一MEM历史", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent ai=session.AddContent(session.Project,"P0 Flow动态AI");
                    MEMPage parent=session.AddPage("P0 Flow其他活动页面");
                    window.OpenStudioResourceDocument(ai.Id,true);
                    window.FlowActionCapability.Text="AI";
                    int before=session.Revision;
                    Button action=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="添加流程能力");
                    action.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MOLFlowCanvas canvas=window.GetStudioResourceFlow(ai.Id);
                    Assert(session.Revision==before+1&&canvas.WiringDefinition!=null&&
                        canvas.WiringDefinition.Blocks.Count==1&&
                        canvas.WiringDefinition.Blocks[0].CapabilityId=="AI",
                        "子流程节点Ribbon没调用当前Content的MOC Builder");
                    vm.ActivePage=parent;
                    window.FlowActionCapability.Text="CV";
                    action.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(canvas.WiringDefinition.Blocks.Count==2&&
                        !parent.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "子流程能力错误落到旧活动Page");
                    canvas.SelectAll();
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="编排")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button arrange=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="自动布局");
                    int layoutBefore=session.Revision;
                    arrange.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==layoutBefore+1,
                        "当前MOL Tab自动布局未原子提交");
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="节点")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button join=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="插入分支Join");
                    canvas.SelectAll();
                    int joinBefore=session.Revision;
                    join.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==joinBefore+1&&canvas.WiringDefinition.Blocks.Count==3,
                        "Join没有复用当前子Content的类型化Port/MOC事务");
                    int deleteBefore=session.Revision;
                    Assert(window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.Delete,System.Windows.Input.ModifierKeys.None)&&
                        session.Revision==deleteBefore+1&&canvas.WiringDefinition.Blocks.Count==2,
                        "子流程Delete没有按当前MOL选区删除或Undo不是单一事务");
                    Assert(window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.Z,System.Windows.Input.ModifierKeys.Control)&&
                        canvas.WiringDefinition.Blocks.Count==3,
                        "子流程Ctrl+Z未恢复当前资源的删除");
                    Assert(window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.Y,System.Windows.Input.ModifierKeys.Control)&&
                        canvas.WiringDefinition.Blocks.Count==2,
                        "子流程Ctrl+Y未恢复相同工程修改");
                    window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.A,System.Windows.Input.ModifierKeys.Control);
                    Assert(canvas.SelectedBlockIds.Count==2,"子流程Ctrl+A未选择真实节点");
                });
                CheckStep("P0 资源Tab选择与浮动标志及关闭后编辑器释放重建", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P0文档生命周期Content");
                    string key=window.OpenStudioResourceDocument(content.Id,false);
                    LayoutDocument doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId==key);
                    Assert(doc.CanFloat&&doc.CanClose,
                        "独立资源文档被错误禁止浮动/关闭");
                    SceneView oldScene=window.GetStudioResourceScene(content.Id);
                    Button rect=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="添加矩形");
                    int before=session.Revision;
                    rect.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==before+1&&
                        content.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "旧矩形Ribbon未路由当前独立资源");
                    window.HandleStudioResourceShortcut(
                        System.Windows.Input.Key.A,System.Windows.Input.ModifierKeys.Control);
                    Assert(oldScene.SelectedVisualIds.Count>0,
                        "Ctrl+A未选择当前图元Tab的真实元素");
                    Button select=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="选择");
                    select.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(window.ActiveStudioDocumentId==key,
                        "选择命令错误跳转到固定图元编辑器");
                    int savedRevision=session.Revision;
                    window.CloseStudioDocument(key);
                    Assert(window.GetStudioResourceScene(content.Id)==null&&
                        session.Revision==savedRevision,
                        "关闭资源Tab泄漏Editor缓存或修改工程");
                    window.OpenStudioResourceDocument(content.Id,false);
                    SceneView rebuilt=window.GetStudioResourceScene(content.Id);
                    Assert(rebuilt!=null&&!Object.ReferenceEquals(oldScene,rebuilt)&&
                        session.Revision==savedRevision,
                        "重开Tab没有释放旧场景/复用原MEM真源");
                });
                CheckStep("P0 多资源Inspector样式与失效选区防串写", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P0属性面板Content");
                    MEMPage other=session.AddPage("P0属性面板隔离Page");
                    string tab=window.OpenStudioResourceDocument(content.Id,false);
                    Button rect=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="矩形");
                    rect.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MRCShapeElement shape=content.DescendantsAndSelf()
                        .OfType<MRCShapeElement>().Single();
                    SceneView scene=window.GetStudioResourceScene(content.Id);
                    scene.SetSelectedVisualIds(new[]{shape.Id});
                    model.ActivePage=other;
                    window.FillColorInput.Text="#FF0088CC";
                    window.StrokeWidthInput.Text="3";
                    var apply=typeof(MainWindow).GetMethod("OnApplyVisualStyle",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    Assert(apply!=null,"Studio样式属性按钮不存在");
                    int before=session.Revision;
                    apply.Invoke(window,new object[]{window,
                        new System.Windows.RoutedEventArgs()});
                    Assert(session.Revision==before+1&&
                        Math.Abs(MRCVisualAppearance.StyleOf(
                            session.Find(shape.Id) as MRCVisualItem).StrokeWidth-3)<1e-9&&
                        !other.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "属性面板没有根据活动Content真实图元提交或污染旧页面");
                    scene.SetSelectedVisualIds(new Guid[0]);
                    int rejected=session.Revision;
                    apply.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(session.Revision==rejected,
                        "没有活动可视定义的属性按钮错误修改上一个图元");
                    window.OpenStudioResourceDocument(content.Id,true);
                    apply.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(session.Revision==rejected,
                        "Flow Tab错把旧选区当成有效可视图元");
                    window.OpenStudioResourceDocument(content.Id,false);
                    Assert(window.ActiveStudioDocumentId==tab,"返回图元Tab身份失效");
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

        /// <summary>仅用于WPF STA故障反证，复用真实MRK能力契约，不连接设备。</summary>
        private sealed class InjectedXRayFailureResolver : IMRKCapabilityResolver
        {
            private readonly IMRKCapabilityResolver _normal;
            internal InjectedXRayFailureResolver(IMRKCapabilityResolver normal)
            { _normal=normal; }
            public bool TryResolve(string id,out MRKResolvedCapability capability)
            {
                if(id!="AI")return _normal.TryResolve(id,out capability);
                capability=new MRKResolvedCapability((context,token)=>
                    Task.FromException<object>(new InvalidOperationException("p04.ai.failure")),
                    new MRKExecutionOutput(typeof(string)),
                    inputs:new[]{new MRKCapabilityInput("Image",typeof(string),false)});
                return true;
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
