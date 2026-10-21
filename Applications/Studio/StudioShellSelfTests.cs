using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
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
