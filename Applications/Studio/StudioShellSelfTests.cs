using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Runtime.Execution;
using AvalonDock.Layout;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Visual;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Professional.Vision;
using CxVisual.Professional.CT3D;
using CxVisual.Professional.Automation;

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
                CheckStep("B5 调试暂停继续单步取消的真实Studio入口与空会话安全", () =>
                {
                    window.OpenStudioDocument("doc.flow");
                    TextBlock feedback=window.FindName("LogicInfo") as TextBlock;
                    Assert(feedback!=null,"Studio调试缺少可见反馈区");
                    var flags=System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic;
                    var debugField=typeof(MainWindow).GetField("_logicDebug",flags);
                    var flowField=typeof(MainWindow).GetField("_logicFlow",flags);
                    Assert(debugField!=null&&flowField!=null&&
                        debugField.GetValue(window)==null&&flowField.GetValue(window)==null,
                        "B5空会话安全回归必须从未执行的Studio开始");
                    foreach(var item in new[]{
                        new[]{"OnFlowDebugPause","当前没有活跃MRK调试会话"},
                        new[]{"OnFlowDebugResume","当前没有活跃MRK调试会话"},
                        new[]{"OnFlowDebugStep","当前无可单步的等待节点"},
                        new[]{"OnFlowDebugCancel","没有可取消的运行任务"}})
                    {
                        var handler=typeof(MainWindow).GetMethod(item[0],flags);
                        Assert(handler!=null,"调试命令入口不存在: "+item[0]);
                        handler.Invoke(window,new object[]{window,
                            new RoutedEventArgs(Button.ClickEvent)});
                        Assert(feedback.Text.Contains(item[1]),
                            "调试无会话状态反馈不正确: "+item[0]);
                    }
                });
                CheckStep("B5 活跃MRK断点单步继续取消真实WPF交互", () =>
                {
                    window.OpenStudioDocument("doc.flow");
                    var flags=System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic;
                    var controllerField=typeof(MainWindow).GetField("_logicDebug",flags);
                    var flowField=typeof(MainWindow).GetField("_logicFlow",flags);
                    var ownerField=typeof(MainWindow).GetField("_activeFlowProjectId",flags);
                    var revisionField=typeof(MainWindow).GetField("_activeFlowEngineeringRevision",flags);
                    TextBlock status=window.FindName("LogicInfo") as TextBlock;
                    Assert(controllerField!=null&&flowField!=null&&
                        ownerField!=null&&revisionField!=null&&status!=null,
                        "B5调试控制与可见状态组件缺失");
                    Action<string> command=name=>{
                        var handler=typeof(MainWindow).GetMethod(name,flags);
                        Assert(handler!=null,"B5真实WPF命令未注册: "+name);
                        handler.Invoke(window,new object[]{window,
                            new RoutedEventArgs(Button.ClickEvent)});
                    };
                    int executions=0;
                    var plan=new MRKExecutionPlan("studio.b5.live",new[]{
                        new MRKExecutionStep("first",new string[0],(ctx,token)=>{
                            System.Threading.Interlocked.Increment(ref executions);
                            return Task.FromResult((object)1);
                        }),
                        new MRKExecutionStep("second",new[]{"first"},(ctx,token)=>{
                            System.Threading.Interlocked.Increment(ref executions);
                            return Task.FromResult((object)2);
                        })
                    });
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(1,4));
                    var debug=new MRKExecutionDebugController();
                    debug.AddBreakpoint("first");
                    try
                    {
                        var flow=engine.StartFlowWithControl(plan,debug);
                        controllerField.SetValue(window,debug);
                        flowField.SetValue(window,flow);
                        var activeProject=((StudioViewModel)window.DataContext).ProjectSession;
                        ownerField.SetValue(window,activeProject.Project.Id);
                        revisionField.SetValue(window,activeProject.Revision);
                        Assert(System.Threading.SpinWait.SpinUntil(
                            ()=>debug.WaitingCount==1,3000)&&executions==0,
                            "B5真实断点没有在首节点执行前暂停");
                        command("OnFlowDebugStep");
                        Assert(status.Text.Contains("已单步释放")&&
                            System.Threading.SpinWait.SpinUntil(
                                ()=>executions==1&&debug.WaitingCount==1,3000),
                            "B5单步未推进恰好一个节点");
                        TextBox preview=window.FindName("FlowResultPreview") as TextBox;
                        Assert(preview!=null,"B5缺少真实Flow数据查看控件");
                        command("OnFlowInspectData");
                        Assert(preview.Text.Contains("first = Int32 value=1")&&
                            !preview.Text.Contains("second ="),
                            "B5单步后结果展示了未执行的第二节点或遗失已提交结果");
                        command("OnFlowDebugResume");
                        Assert(status.Text.Contains("已恢复MRK执行")&&
                            flow.Completion.Wait(TimeSpan.FromSeconds(6))&&
                            flow.Completion.Result.State==MRKFlowState.Completed&&
                            executions==2,
                            "B5继续执行未到达真实完成状态");
                        command("OnFlowInspectData");
                        Assert(preview.Text.Contains("second = Int32 value=2")&&
                            preview.Text.Contains("Completed"),
                            "B5最终结果查看没有绑定真实MRK Flow提交结果");
                        command("OnFlowDebugCancel");
                        Assert(status.Text.Contains("没有可取消的运行任务"),
                            "B5已完成Flow错误接受取消");
                        // The real Studio run path releases its MRK handle after
                        // completion. The bounded preview must remain readable
                        // for the same MEM project/revision, and reject stale data.
                        var capture=typeof(MainWindow).GetMethod(
                            "CaptureCompletedFlowPreview",flags);
                        Assert(capture!=null,"B5缺少Flow完成后的安全摘要捕获");
                        var project=((StudioViewModel)window.DataContext).ProjectSession;
                        capture.Invoke(window,new object[]{flow,project.Project.Id,project.Revision});
                        flowField.SetValue(window,null);
                        command("OnFlowInspectData");
                        Assert(preview.Text.Contains("second = Int32 value=2"),
                            "B5运行完成释放句柄后结果不可查看");
                        capture.Invoke(window,new object[]{flow,Guid.NewGuid(),project.Revision});
                        command("OnFlowInspectData");
                        Assert(preview.Text.Contains("无可查看的运行结果"),
                            "B5错误允许查看其他工程的历史Flow数据");
                        var cancelDebug=new MRKExecutionDebugController();
                        cancelDebug.AddBreakpoint("first");
                        var cancelled=engine.StartFlowWithControl(plan,cancelDebug);
                        controllerField.SetValue(window,cancelDebug);
                        flowField.SetValue(window,cancelled);
                        Assert(System.Threading.SpinWait.SpinUntil(
                            ()=>cancelDebug.WaitingCount==1,3000),
                            "B5取消前的真实运行断点未命中");
                        command("OnFlowDebugCancel");
                        Assert(status.Text.Contains("已向MRK发送取消请求")&&
                            cancelled.Completion.Wait(TimeSpan.FromSeconds(6))&&
                            cancelled.Completion.Result.State==MRKFlowState.Cancelled&&
                            executions==2,
                            "B5暂停Flow取消未释放等待者或错误执行后续算法");
                    }
                    finally
                    {
                        controllerField.SetValue(window,null);
                        flowField.SetValue(window,null);
                        ownerField.SetValue(window,Guid.Empty);
                        revisionField.SetValue(window,-1);
                        debug.Resume();
                        engine.StopAsync(true).GetAwaiter().GetResult();
                    }
                });
                CheckStep("B5 活跃运行结果跨工程版本与多Flow隔离真实WPF反例", () =>
                {
                    var flags=System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic;
                    var flowField=typeof(MainWindow).GetField("_logicFlow",flags);
                    var ownerField=typeof(MainWindow).GetField("_activeFlowProjectId",flags);
                    var revisionField=typeof(MainWindow).GetField("_activeFlowEngineeringRevision",flags);
                    var capture=typeof(MainWindow).GetMethod("CaptureCompletedFlowPreview",flags);
                    var inspect=typeof(MainWindow).GetMethod("OnFlowInspectData",flags);
                    var preview=window.FindName("FlowResultPreview") as TextBox;
                    var session=((StudioViewModel)window.DataContext).ProjectSession;
                    Assert(flowField!=null&&ownerField!=null&&revisionField!=null&&
                        capture!=null&&inspect!=null&&preview!=null,
                        "B5真实Studio的Flow身份及结果入口缺失");
                    Action inspectClick=()=>inspect.Invoke(window,new object[]{
                        window,new RoutedEventArgs(Button.ClickEvent)});
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,8));
                    try
                    {
                        var first=engine.StartFlow(new MRKExecutionPlan("studio.b5.scope.a",
                            new[]{new MRKExecutionStep("productA",new string[0],
                                (ctx,ct)=>Task.FromResult((object)101))}));
                        var second=engine.StartFlow(new MRKExecutionPlan("studio.b5.scope.b",
                            new[]{new MRKExecutionStep("productB",new string[0],
                                (ctx,ct)=>Task.FromResult((object)202))}));
                        Assert(first.Completion.Wait(TimeSpan.FromSeconds(6))&&
                            second.Completion.Wait(TimeSpan.FromSeconds(6))&&
                            first.FlowId!=second.FlowId,"B5两条真实MRK Flow未完成或身份重复");
                        flowField.SetValue(window,first);
                        ownerField.SetValue(window,session.Project.Id);
                        revisionField.SetValue(window,session.Revision);
                        inspectClick();
                        Assert(preview.Text.Contains(first.FlowId.ToString("N"))&&
                            preview.Text.Contains("productA = Int32 value=101")&&
                            !preview.Text.Contains("productB"),
                            "B5 Flow A查看混入其它Flow结果");
                        flowField.SetValue(window,second);
                        inspectClick();
                        Assert(preview.Text.Contains(second.FlowId.ToString("N"))&&
                            preview.Text.Contains("productB = Int32 value=202")&&
                            !preview.Text.Contains("productA"),
                            "B5 Flow B查看混入上一Flow结果");
                        ownerField.SetValue(window,Guid.NewGuid());
                        inspectClick();
                        Assert(preview.Text.Contains("无可查看的运行结果")&&
                            !preview.Text.Contains("productB"),
                            "B5活跃Flow跨工程仍泄露运行结果");
                        ownerField.SetValue(window,session.Project.Id);
                        revisionField.SetValue(window,session.Revision+1);
                        inspectClick();
                        Assert(preview.Text.Contains("无可查看的运行结果")&&
                            !preview.Text.Contains("productB"),
                            "B5活跃Flow跨Revision仍泄露运行结果");
                        flowField.SetValue(window,null);
                        capture.Invoke(window,new object[]{second,session.Project.Id,session.Revision+1});
                        Assert(preview.Text.Contains("无可查看的运行结果"),
                            "B5异步Flow在工程被修改后错误发布完成摘要");
                        capture.Invoke(window,new object[]{second,session.Project.Id,session.Revision});
                        inspectClick();
                        Assert(preview.Text.Contains("productB = Int32 value=202"),
                            "B5相同工程修订的安全完成摘要没有保留");
                    }
                    finally
                    {
                        flowField.SetValue(window,null);
                        ownerField.SetValue(window,Guid.Empty);
                        revisionField.SetValue(window,-1);
                        engine.StopAsync(true).GetAwaiter().GetResult();
                    }
                });
                CheckStep("S1-03B 变量Studio工作台复用单一MDS Owner", () =>
                {
                    window.OpenStudioDocument("doc.variables");
                    LayoutDocument active = window.StudioDock.Layout.ActiveContent as LayoutDocument;
                    var variableView = active == null ? null :
                        active.Content as CxVisual.Mechanism.Presentation.Wpf.Variables.MPWVariableWorkspaceControl;
                    Assert(active != null && active.ContentId == "doc.variables" &&
                        variableView != null && variableView.BoundScopeId == null,
                        "Studio变量页不应自动生成第二套运行Scope");
                    Assert(window.GetStudioContentIds().Count(id => id == "doc.variables") == 1,
                        "同一变量工作区不得重复开Tab");
                    Assert(window.TryCloseStudioDocument("doc.variables") &&
                        !window.GetStudioContentIds().Contains("doc.variables"),
                        "变量工作台关闭后应释放文档");
                });
                CheckStep("S1-03C Studio模块中心与TestCenter共享MPW组件", () =>
                {
                    window.OpenStudioDocument("doc.modules");
                    LayoutDocument opened=window.StudioDock.Layout.ActiveContent as LayoutDocument;
                    MPWModuleCenterControl center=opened==null?null:opened.Content as MPWModuleCenterControl;
                    Assert(center!=null&&center.VisibleCapabilityCount>0,
                        "Studio共享中心未加载真正MOC演示目录");
                    int originalCount=center.VisibleCapabilityCount;
                    Assert(center.VisibleCapabilities.Any(x=>
                        x.Id==CxVisionThresholdProvider.CapabilityId &&
                        x.Kind==CxVisual.Mechanism.Core.Modules.MFMModuleKind.Compute &&
                        x.ModuleId=="studio.development.cpu.vision" &&
                        x.Resolved && !x.Demonstration),
                        "实际CPU专业算法未由唯一MOC/MFM关联为Compute");
                    var cpuPorts=center.VisibleCapabilities.Single(x=>
                        x.Id==CxVisionThresholdProvider.CapabilityId);
                    Assert(cpuPorts.InputPorts.Count==3 &&
                        cpuPorts.InputPorts.Any(x=>x.StartsWith("RawPixels : byte[]"))&&
                        cpuPorts.InputPorts.Any(x=>x.StartsWith("Width : int"))&&
                        cpuPorts.InputPorts.Any(x=>x.StartsWith("Height : int"))&&
                        cpuPorts.InputPorts.All(x=>x.Contains("(必填)"))&&
                        cpuPorts.OutputPorts.Count==1 &&
                        cpuPorts.OutputPorts.Any(x=>x.StartsWith("Inspection : "+
                            CxVisionAcceptedRegionMeasureProvider.InspectionTypeId)),
                        "Studio未从真正MOC能力目录展示强类型算法端口");

                    Assert(center.VisibleCapabilities.Any(x=>!x.Kind.HasValue&&x.Demonstration),
                        "演示CPU流程不能被混入真实专业模块身份");
                    center.SetSelectedKind(CxVisual.Mechanism.Core.Modules.MFMModuleKind.Compute);
                    string[] requiredVision={
                        CxVisionThresholdProvider.CapabilityId,
                        CxVisionBinaryThresholdProvider.CapabilityId,
                        CxVisionMaskRegionsProvider.CapabilityId,
                        CxVisionAcceptedRegionMeasureProvider.CapabilityId
                    };
                    Assert(center.VisibleCapabilityCount==4 &&
                        requiredVision.All(id=>center.VisibleCapabilities.Count(x=>
                            x.Id==id && x.Resolved && x.Described &&
                            x.ModuleId=="studio.development.cpu.vision" &&
                            !x.Demonstration)==1),
                        "Compute应通过唯一MOC/MFM目录发现四个真实专业节点");
                    var segmentPorts=center.VisibleCapabilities.Single(x=>
                        x.Id==CxVisionBinaryThresholdProvider.CapabilityId);
                    var regionPorts=center.VisibleCapabilities.Single(x=>
                        x.Id==CxVisionMaskRegionsProvider.CapabilityId);
                    var measurePorts=center.VisibleCapabilities.Single(x=>
                        x.Id==CxVisionAcceptedRegionMeasureProvider.CapabilityId);
                    Assert(segmentPorts.InputPorts.Any(x=>x.StartsWith("Threshold : int")) &&
                        segmentPorts.OutputPorts.Any(x=>
                            x.StartsWith("ThresholdMask : "+CxVisionBinaryThresholdProvider.MaskTypeId)) &&
                        regionPorts.InputPorts.Any(x=>
                            x.StartsWith("ThresholdMask : "+CxVisionBinaryThresholdProvider.MaskTypeId)) &&
                        regionPorts.InputPorts.Any(x=>x.StartsWith("RuleVersion : string")) &&
                        regionPorts.OutputPorts.Any(x=>x.StartsWith("Inspection : "+
                            CxVisionAcceptedRegionMeasureProvider.InspectionTypeId)) &&
                        measurePorts.InputPorts.Any(x=>x.StartsWith("Inspection : "+
                            CxVisionAcceptedRegionMeasureProvider.InspectionTypeId)) &&
                        measurePorts.OutputPorts.Any(x=>x.StartsWith("Area : long")),
                        "独立Mask→Region→Area正式端口与MOC能力目录不一致");
                    center.SetSelectedKind(null);
                    center.SetSelectedKind(CxVisual.Mechanism.Core.Modules.MFMModuleKind.Driver);
                    Assert(center.VisibleCapabilityCount==0,
                        "CPU演示目录不能伪装为已经注册的Driver Provider");
                    center.SetSelectedKind(CxVisual.Mechanism.Core.Modules.MFMModuleKind.Simulation);
                    Assert(center.VisibleCapabilityCount==0,
                        "未绑定模块的MOC演示能力不能错误归类为Simulation");
                    center.SetSelectedKind(null);
                    Assert(center.VisibleCapabilityCount==originalCount,
                        "返回全部类别未恢复真实目录");
                    center.SearchText="__missing_capability__";
                    Assert(center.VisibleCapabilityCount==0,"空搜索结果不能产生伪能力");
                    center.SearchText="";
                    Assert(center.VisibleCapabilityCount==originalCount,
                        "清除搜索未恢复目录");
                    // Reuse actual routed WPF Click handlers: center shortcuts open the
                    // existing Studio configuration/MOL documents, not shadow workspaces.
                    StackPanel moduleHeader=center.HeaderContent as StackPanel;
                    Assert(moduleHeader!=null,"模块中心缺少专业工作入口");
                    Button[] actions=moduleHeader.Children.OfType<WrapPanel>()
                        .SelectMany(x=>x.Children.OfType<Button>()).ToArray();
                    Button configure=actions.FirstOrDefault(x=>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                        "Studio.ModuleCenter.OpenConfiguration");
                    Button flow=actions.FirstOrDefault(x=>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                        "Studio.ModuleCenter.OpenFlow");
                    var rawPanel=moduleHeader.Children.OfType<WrapPanel>()
                        .FirstOrDefault(x=>x.Children.OfType<Button>().Any(y=>
                            System.Windows.Automation.AutomationProperties.GetAutomationId(y)==
                            "Studio.ModuleCenter.RunSeparatedRaw"));
                    Assert(rawPanel!=null && rawPanel.Children.OfType<ComboBox>().Any(x=>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                        "Studio.ModuleCenter.MedianRadius") &&
                        rawPanel.Children.OfType<Button>().Any(x=>
                            System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                            "Studio.ModuleCenter.CancelRaw") &&
                        rawPanel.Children.OfType<Button>().Any(x=>
                            System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                            "Studio.ModuleCenter.BindSavedRoiRaw") &&
                        rawPanel.Children.OfType<TextBox>().Any(x=>
                            System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                            "Studio.ModuleCenter.BindSavedRoiStride"),
                        "Studio模块中心缺少RAW检测、取消或正式工程ROI原图重绑入口");
                    var resultPanel=moduleHeader.Children.OfType<Expander>()
                        .SingleOrDefault(x=>System.Windows.Automation.AutomationProperties
                            .GetAutomationId(x)=="Studio.ModuleCenter.ResultPanel");
                    var resultStack=resultPanel==null?null:resultPanel.Content as StackPanel;
                    Assert(resultPanel!=null && resultStack!=null &&
                        resultStack.Children.OfType<MPWImageViewerControl>().Any(x=>
                            System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                            "Studio.ModuleCenter.ResultViewer") &&
                        resultStack.Children.OfType<ListBox>().Any(x=>
                            System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                            "Studio.ModuleCenter.ResultItems") &&
                        resultStack.Children.OfType<WrapPanel>()
                            .SelectMany(x=>x.Children.OfType<Button>())
                            .Count(x=>new[]{"Studio.ModuleCenter.ShowRunOriginal",
                                "Studio.ModuleCenter.GenerateRunContours",
                                "Studio.ModuleCenter.ExportRunCsv",
                                "Studio.ModuleCenter.ExportRunRaw",
                                "Studio.ModuleCenter.CommitSelectedRoi",
                                "Studio.ModuleCenter.RunSavedRoi"}.Contains(
                                System.Windows.Automation.AutomationProperties.GetAutomationId(x)))==6,
                        "Studio模块中心缺少MPW图窗、Contour、CSV或正式ROI保存/重开运行操作");
                    Button createVisionFlow=actions.FirstOrDefault(x=>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                        "Studio.ModuleCenter.CreateVisionFlow");
                    Assert(createVisionFlow!=null,
                        "Studio模块中心缺少已连线专业Vision流程创建入口");
                    Assert(configure!=null&&flow!=null,"模块中心缺少工程配置/流程快捷入口");
                    configure.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,configure));
                    Assert(window.GetStudioContentIds().Contains("doc.config"),
                        "模块中心配置入口没有打开原FCM/MHS Studio配置文档");
                    Assert(window.TryCloseStudioDocument("doc.config"),
                        "模块中心新打开的空配置文档不能正常关闭");
                    Button load=actions.FirstOrDefault(x=>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                        "Studio.ModuleCenter.LoadEffective");
                    Assert(load!=null,"模块中心缺少显式MHS Effective授权读取入口");
                    TextBlock effectiveStatus=moduleHeader.Children.OfType<TextBlock>()
                        .FirstOrDefault(x=>
                            System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                                "Studio.ModuleCenter.EffectiveStatus");
                    Assert(effectiveStatus!=null&&
                        effectiveStatus.Text.Contains("需要显式操作"),
                        "模块中心首次发现不应该自动打开MHS并Apply");
                    load.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,load));
                    Assert(effectiveStatus.Text.Contains("已从正式MHS读取 Effective")&&
                        effectiveStatus.Text.Contains("vision.cpu.threshold.v1/config:"),
                        "未通过真实MHS Effective快照刷新软件Provider版本："+
                        effectiveStatus.Text);
                    Assert(new[]{
                        CxVisionThresholdProvider.CapabilityId,
                        CxVisionBinaryThresholdProvider.CapabilityId,
                        CxVisionMaskRegionsProvider.CapabilityId,
                        CxVisionAcceptedRegionMeasureProvider.CapabilityId
                    }.All(id=>center.VisibleCapabilities.Any(x=>x.Id==id&&x.Resolved&&
                        x.Kind==CxVisual.Mechanism.Core.Modules.MFMModuleKind.Compute)),
                        "加载MHS有效配置后丢失独立分割区域或面积Provider");
                    Button addFlow=actions.FirstOrDefault(x=>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(x)==
                        "Studio.ModuleCenter.AddToFlow");
                    Assert(addFlow!=null,"模块中心缺少正式MOL能力加入入口");
                    int beforeRejectedAdd=window.StudioEngineeringRevision;
                    addFlow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,addFlow));
                    Assert(window.StudioEngineeringRevision==beforeRejectedAdd&&
                        effectiveStatus.Text.Contains("请先选中"),
                        "未选择真实Compute能力时不应新增MEM节点");
                    flow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,flow));
                    Assert(window.GetStudioContentIds().Contains("doc.flow"),
                        "模块中心流程入口没有打开原MOL Studio主流程");
                    window.OpenStudioDocument("doc.modules");
                    Assert(window.GetStudioContentIds().Count(id=>id=="doc.modules")==1,
                        "同一中心重复打开产生两个文档");
                    Assert(window.TryCloseStudioDocument("doc.modules"),"模块中心关闭失败");
                    Assert(window.StudioEngineeringRevision==revision,"只读中心不应修改MEM");
                });
                CheckStep("S1-03D RAW真实文件→MEM/MOC/MRK→缺陷结果及Trace闭环",
                    VerifyCpuVisionFileClosure);
                CheckStep("R1-01 保存工程→带Stride RAW指纹重绑→MRK复算/显示/导出",
                    VerifyStudioSavedRoiRebind);
                CheckStep("S1-04 共用MPW主题及Dock视觉不修改工程", () =>
                {
                    Assert(window.StudioDock.AnchorableHeaderTemplate != null &&
                        window.StudioDock.DocumentHeaderTemplate != null,
                        "Studio未接通公共MPW文档与工具页签模板");
                    Assert(ReferenceEquals(window.StudioDock.DocumentHeaderTemplate,
                        Application.Current.FindResource("MPWDockHeaderTemplate")),
                        "Studio文档Tab没有使用同一公共Owner");
                    Assert(ReferenceEquals(window.StudioDock.AnchorableHeaderTemplate,
                        Application.Current.FindResource("MPWToolDockHeaderTemplate")),
                        "Studio工具Tab未使用公共MPW工具模板");
                    // 公共视觉合同也必须能在真实WPF STA加载，而非仅拥有同名资源键。
                    Grid documentHeader = ((DataTemplate)Application.Current.FindResource(
                        "MPWDockHeaderTemplate")).LoadContent() as Grid;
                    Grid toolHeader = ((DataTemplate)Application.Current.FindResource(
                        "MPWToolDockHeaderTemplate")).LoadContent() as Grid;
                    Assert(documentHeader != null && documentHeader.Children.OfType<Border>()
                            .Any(b => b.Name == "DocumentAccent") &&
                        toolHeader != null && toolHeader.Children.OfType<Border>()
                            .Any(b => b.Name == "ToolAccent"),
                        "公共Dock Header没有固定尺寸的独立选中强调线");
                    Assert(Math.Abs(toolHeader.Width - 184) < 0.5 &&
                        Math.Abs(toolHeader.Height - 30) < 0.5,
                        "公共工具页签宽高未采用MPW固定紧凑规格");
                    Style gridStyle = Application.Current.FindResource("MPWDataGridStyle") as Style;
                    Assert(gridStyle != null && gridStyle.Setters.OfType<Setter>().Any(
                            setter => setter.Property == DataGrid.ColumnHeaderStyleProperty) &&
                        gridStyle.Setters.OfType<Setter>().Any(
                            setter => setter.Property == DataGrid.RowStyleProperty),
                        "DataGrid没有使用统一列头与行视觉样式");
                    Style treeStyle = Application.Current.FindResource("MPWTreeItemStyle") as Style;
                    Assert(treeStyle != null && treeStyle.Triggers.OfType<Trigger>().Any(
                        trigger => trigger.Property == TreeViewItem.IsSelectedProperty),
                        "公共资源树缺失正式选择态视觉");
                    Assert(window.Icon != null, "Studio主窗口缺少公共MPW图标");
                    window.ApplyStudioTheme(true);
                    Assert(window.StudioDarkTheme, "深色未切换");
                    SolidColorBrush darkAccent =
                        Application.Current.Resources["MPWAccentBrush"] as SolidColorBrush;
                    Assert(darkAccent != null &&
                        darkAccent.Color == (Color)ColorConverter.ConvertFromString("#85BFFF"),
                        "Studio深色主题没有更新公共MPW主题令牌");
                    window.ApplyStudioTheme(false);
                    Assert(!window.StudioDarkTheme, "浅色未恢复");
                    SolidColorBrush lightAccent =
                        Application.Current.Resources["MPWAccentBrush"] as SolidColorBrush;
                    Assert(lightAccent != null &&
                        lightAccent.Color == (Color)ColorConverter.ConvertFromString("#2477CB"),
                        "Studio浅色主题令牌未恢复");
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
                    // Header升级为公共MPW矢量UI，资源类别使用原Tag身份验收而非ToString()。
                    Assert(root.Items.OfType<TreeViewItem>().Any(item=>
                        String.Equals(item.Tag as string, "pages", StringComparison.Ordinal)),
                        "缺少实际MEM页面分组");
                    Assert(root.Items.OfType<TreeViewItem>().Any(item=>
                        String.Equals(item.Tag as string, "symbols", StringComparison.Ordinal)),
                        "缺少Symbol分组");
                    Assert(!root.Items.OfType<TreeViewItem>().Any(item=>
                        Convert.ToString(item.Tag).Contains("Element")),
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
                CheckStep("P0-4 跨页面MRK来源恢复与双Flow轨迹身份隔离", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage original=session.Project.Pages.Single(p=>
                        p.Name=="P0-4 真实执行XRay");
                    MEMPage other=session.AddPage("P0-4 来源切页反证");
                    MEMProjectSnapshot snapshot=session.GetSnapshot();
                    MEMNodeSnapshot root=snapshot.Find(original.RootContent.Id);
                    MRIReferenceSnapshot ai=root.References.Single(r=>
                        session.Find(r.TargetId).Name.Contains("AI"));
                    MOLWiringDefinition rootWiring=MOLWiringCodec.Get(root);
                    Guid aiBlock=MOLWiringCodec.Get(snapshot.Find(ai.TargetId))
                        .Blocks.First(x=>x.CapabilityId=="AI").Id;
                    Guid aiCall=rootWiring.Blocks.Single(x=>
                        x.CapabilityId=="moc.invoke:"+ai.TargetId.ToString("N")).Id;
                    var provider=typeof(MainWindow).GetField("_simulation",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    IMRKCapabilityResolver resolver=(IMRKCapabilityResolver)provider.GetValue(window);
                    MOCBuildResult build=new MOCBuildService().Build(snapshot,
                        original.RootContent.Id,resolver);
                    Assert(build.Succeeded,"跨页面定位基于真正可执行MOC Plan");
                    MainWindow.FlowTraceRecord first=null,second=null;
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(4,128));
                    try
                    {
                        MRKFlowHandle flow1=engine.StartFlow(build.Plan.Plan);
                        MRKFlowHandle flow2=engine.StartFlow(build.Plan.Plan);
                        MRKFlowResult[] results=Task.WhenAll(flow1.Completion,
                            flow2.Completion).GetAwaiter().GetResult();
                        Assert(flow1.FlowId!=flow2.FlowId&&
                            results.All(x=>x.State==MRKFlowState.Completed),
                            "相同Plan不同Flow实例执行失败或身份重复");
                        MOCObservedStep a=build.Plan.ReadTrace(flow1,0).Events.First(x=>
                            x.Origin.ContentAddress.ObjectId==ai.TargetId&&
                            x.Origin.BlockId==aiBlock&&
                            x.Transition.State==MRKStepState.Completed);
                        MOCObservedStep b=build.Plan.ReadTrace(flow2,0).Events.First(x=>
                            x.Origin.ContentAddress.ObjectId==ai.TargetId&&
                            x.Origin.BlockId==aiBlock&&
                            x.Transition.State==MRKStepState.Completed);
                        first=MainWindow.ProjectFlowTrace(build.Plan,a,
                            original.RootContent.Id,flow1.FlowId);
                        second=MainWindow.ProjectFlowTrace(build.Plan,b,
                            original.RootContent.Id,flow2.FlowId);
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                    Assert(first.FlowId!=Guid.Empty&&first.FlowId!=second.FlowId&&
                        first.RootContentId==original.RootContent.Id&&
                        second.RootContentId==first.RootContentId&&
                        first.BlockId==second.BlockId,
                        "Studio丢弃MRK实例身份或主流程来源");
                    ComboBox list=window.FindName("FlowTraceList") as ComboBox;
                    list.ItemsSource=new[]{first,second};list.SelectedIndex=1;
                    var parent=typeof(MainWindow).GetMethod("OnFlowLocateTrace",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    var visual=typeof(MainWindow).GetMethod("OnFlowLocateVisual",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    vm.ActivePage=other;
                    window.OpenStudioDocument("doc.flow");
                    int traceRevision=session.Revision;
                    parent.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(vm.ActivePage.Id==original.Id&&window.ActiveStudioDocumentId=="doc.flow"&&
                        ((MOLFlowCanvas)window.FindName("LogicHost")).SelectedBlockIds
                            .Contains(aiCall),
                        "切到其他页面后运行轨迹未恢复真实主流程调用点");
                    vm.ActivePage=other;
                    window.OpenStudioDocument("doc.flow");
                    visual.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(vm.ActivePage.Id==original.Id&&
                        window.ActiveStudioDocumentId=="doc.visual"&&
                        vm.SelectedId.HasValue&&session.Revision==traceRevision,
                        "切到其他页面后HMI来源被错误解析或写入工程");
                    // 故意破坏根来源，必须拒绝切换，不靠当前打开的同名子流程猜测。
                    first.RootContentId=Guid.NewGuid();
                    list.ItemsSource=new[]{first};list.SelectedIndex=0;
                    vm.ActivePage=other;
                    window.OpenStudioDocument("doc.flow");
                    string before=window.ActiveStudioDocumentId;
                    parent.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    visual.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(vm.ActivePage.Id==other.Id&&
                        window.ActiveStudioDocumentId==before&&session.Revision==traceRevision,
                        "不存在的根流程ContentId造成跨页面错跳或工程修改");
                });
                CheckStep("P0 动态绑定与流程校验命令必须命中活动Content而非旧Page", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent vision=session.AddContent(session.Project,"P0绑定校验Content");
                    MEMPage unrelatedPage=session.AddPage("P0绑定隔离Page");
                    window.OpenStudioResourceDocument(vision.Id,false);
                    Button rect=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="矩形");
                    rect.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MRCShapeElement visual=vision.DescendantsAndSelf().OfType<MRCShapeElement>()
                        .Single();
                    window.GetStudioResourceScene(vision.Id)
                        .SetSelectedVisualIds(new[]{visual.Id});
                    vm.ActivePage=unrelatedPage;
                    window.VisualBindingPropertyInput.Text="visible";
                    window.VisualBindingSourceInput.Text="Inspection.Ready";
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="绑定")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button binding=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="绑定变量");
                    int beforeBinding=session.Revision;
                    binding.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    MPMBinding value=(session.Find(visual.Id) as MRCVisualItem).GetBinding("visible");
                    Assert(session.Revision==beforeBinding+1&&value!=null&&
                        value.Kind==MPMBindingKind.Variable&&value.SourceId=="Inspection.Ready",
                        "Content动态绑定Ribbon仍被旧Page Guard拦截或绑定错对象");
                    window.OpenStudioResourceDocument(vision.Id,true);
                    MOCFlowBuilder.Edit(session,vision,b=>b.Step("test.missing.provider.P0"));
                    vm.ActivePage=unrelatedPage;
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="编排")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button validate=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="校验编排");
                    int beforeValidate=session.Revision;
                    validate.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==beforeValidate,
                        "编排校验错误地修改了MEM工程Revision");
                    System.Collections.IEnumerable source=window.FlowDiagnosticList.ItemsSource
                        as System.Collections.IEnumerable;
                    Assert(source!=null&&source.Cast<object>().Any(x=>
                        (Guid)x.GetType().GetProperty("ContentId").GetValue(x,null)==vision.Id),
                        "独立子流程校验使用了旧主Page的MOC诊断来源");
                });
                CheckStep("P0 Flow Port属性必须落活动AI子流程且单MEM事务可撤销", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage wrong=session.AddPage("P0端口旧页面");
                    MEMContent ai=session.AddContent(session.Project,"P0端口真实AI Content");
                    string tab=window.OpenStudioResourceDocument(ai.Id,true);
                    MOCFlowBuilder.Edit(session,ai,b=>b.Step("AI"));
                    MOLFlowCanvas canvas=window.GetStudioResourceFlow(ai.Id);
                    Guid node=canvas.WiringDefinition.Blocks.Single().Id;
                    canvas.SetSelection(new[]{node});
                    vm.ActivePage=wrong;
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="端口")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    window.FlowInspectorPortName.Text="InspectionScore";
                    window.FlowInspectorType.Text="core.number";
                    window.FlowInspectorDirection.SelectedIndex=1;
                    window.FlowInspectorKind.SelectedIndex=0;
                    window.FlowInspectorRequired.IsChecked=false;
                    int before=session.Revision;
                    window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="添加Port")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==before+1&&
                        canvas.WiringDefinition.Ports[node].Any(x=>x.MemberId=="InspectionScore")&&
                        !wrong.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().Any(),
                        "Port属性误命中旧主页面或没有提交当前Flow");
                    session.Undo().RequireValue();
                    Assert(!canvas.WiringDefinition.Ports[node].Any(x=>x.MemberId=="InspectionScore"),
                        "Port属性编辑未通过可撤销的单MEM事务");
                    session.Redo().RequireValue();
                    Assert(canvas.WiringDefinition.Ports[node].Any(x=>x.MemberId=="InspectionScore"),
                        "Port属性重做丢失定义");
                    window.OpenStudioResourceDocument(ai.Id,false);
                    int unchanged=session.Revision;
                    window.StudioRibbonActions.Children.OfType<Button>()
                        .Any(x=>Convert.ToString(x.Content)=="添加Port");
                    // 独立Visual Tab触发面板旧Port编辑必须明确拒绝，而非修改wrong页面
                    var method=typeof(MainWindow).GetMethod("OnFlowAddPort",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    method.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    Assert(session.Revision==unchanged,
                        "独立Visual Tab的旧Port操作误写其他Flow");
                    window.OpenStudioResourceDocument(ai.Id,true);
                    Assert(window.ActiveStudioDocumentId==tab,"重新打开Flow丢失文档身份");
                });
                CheckStep("P0-4 X-Ray CPU软件发布候选与MRK授权版本交接", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage page=session.Project.Pages.Single(p=>p.Name=="P0-4 真实执行XRay");
                    vm.ActivePage=page;
                    window.OpenStudioDocument("doc.flow");
                    Button button=window.FindName("FlowTraceList")==null?null:
                        window.GetType().GetMethod("OnPrepareFlowSoftwareRelease",
                            System.Reflection.BindingFlags.NonPublic|
                            System.Reflection.BindingFlags.Instance)==null?null:
                        new Button();
                    Assert(button!=null,"Studio CPU软件发布命令未接入");
                    var prepare=typeof(MainWindow).GetMethod("OnPrepareFlowSoftwareRelease",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    prepare.Invoke(window,new object[]{window,new System.Windows.RoutedEventArgs()});
                    var field=typeof(MainWindow).GetField("_preparedCpuRelease",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    MOCProjectReleaseCandidate candidate=(MOCProjectReleaseCandidate)field.GetValue(window);
                    Assert(candidate!=null&&candidate.ProjectId==session.Project.Id&&
                        candidate.EntryContentId==page.RootContent.Id&&
                        candidate.CapabilityVersions.ContainsKey("AI")&&
                        candidate.CapabilityVersions.ContainsKey("CV"),
                        "Studio没有锁定真实AI/CV及主流程发布依赖");
                    var resolverField=typeof(MainWindow).GetField("_simulation",
                        System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance);
                    IMRKCapabilityResolver resolver=(IMRKCapabilityResolver)resolverField.GetValue(window);
                    var getVersions=resolver.GetType().GetMethod("DescribeReleaseVersions");
                    var versions=(IReadOnlyDictionary<string,string>)getVersions.Invoke(resolver,null);
                    var policy=new P04CpuReleaseTestPolicy{Safe=true};
                    bool denied=false;
                    try{MainWindow.StageCpuSoftwareRelease(candidate,session.GetSnapshot(),
                        resolver,versions,new P04CpuReleaseTestPolicy());}
                    catch(UnauthorizedAccessException){denied=true;}
                    Assert(denied,"CPU软件发布候选未执行MRK授权拒绝");
                    denied=false;
                    try{MainWindow.StageCpuSoftwareRelease(candidate,session.GetSnapshot(),
                        resolver,versions,new P04CpuReleaseTestPolicy{Authorized=true,Safe=false});}
                    catch(InvalidOperationException){denied=true;}
                    Assert(denied,"CPU模拟Host安全点拒绝未生效");
                    policy.Authorized=true;
                    MOCCompiledPlan staged=MainWindow.StageCpuSoftwareRelease(
                        candidate,session.GetSnapshot(),resolver,versions,policy);
                    Assert(object.ReferenceEquals(staged,candidate.Plan),
                        "发布过的MOC Plan被Studio替换为另外一次编译结果");
                    var updated=versions.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
                    updated["AI"]="studio-cpu-v2";
                    denied=false;
                    try{MainWindow.StageCpuSoftwareRelease(candidate,session.GetSnapshot(),
                        resolver,updated,policy);}
                    catch(InvalidOperationException){denied=true;}
                    Assert(denied,"模拟AI Provider版本变化未拒绝旧Candidate");
                    int savedRevision=session.Revision;
                    session.AddContent(session.Project,"P0-4 发布Revision冲突反证");
                    denied=false;
                    try{MainWindow.StageCpuSoftwareRelease(candidate,session.GetSnapshot(),
                        resolver,versions,policy);}
                    catch(InvalidOperationException){denied=true;}
                    Assert(denied&&session.Revision>savedRevision,
                        "旧工程Revision的MOC发布Candidate跨修改仍被激活");
                    var again=MOCProjectRelease.Prepare(session.GetSnapshot(),
                        page.RootContent.Id,resolver,versions);
                    Assert(again.EngineeringRevision==session.Revision&&
                        MainWindow.StageCpuSoftwareRelease(again,session.GetSnapshot(),
                            resolver,versions,policy)!=null,
                        "Studio工程变更后未能重新准备并激活有效CPU候选");
                    field.SetValue(window,null); // 避免此STA命令候选影响下一测试的仿真入口
                });
                CheckStep("S2/P1 工程配置中心真实Vision与CT双Schema编辑并隔离Project Scope", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    int p1Revision=session.Revision;
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    var root=doc.Content as System.Windows.DependencyObject;
                    Assert(root!=null,"中央配置工作区未创建真实WPF控件");
                    Func<System.Windows.DependencyObject,Type,bool> exists=null;
                    exists=(element,type)=>type.IsInstanceOfType(element)||
                        System.Windows.LogicalTreeHelper.GetChildren(element)
                            .OfType<System.Windows.DependencyObject>()
                            .Any(child=>exists(child,type));
                    Assert(exists(root,typeof(MPWConfigEditorControl<CxVisionInspectionOptions>))&&
                        exists(root,typeof(MPWConfigEditorControl<CxCtReconstructionOptions>)),
                        "中央配置仍为模拟占位，没有装载两种专业Schema Editor");
                    Func<System.Windows.DependencyObject,MPWConfigEditorControl<CxVisionInspectionOptions>>
                        visionEditor=null;
                    visionEditor=element=>
                        element as MPWConfigEditorControl<CxVisionInspectionOptions> ??
                        System.Windows.LogicalTreeHelper.GetChildren(element)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>visionEditor(child)).FirstOrDefault(v=>v!=null);
                    MPWConfigEditorControl<CxVisionInspectionOptions> live=visionEditor(root);
                    Assert(live!=null&&live.Draft!=null,
                        "实际中央Vision Schema Editor草稿未初始化");
                    window.OpenStudioDocument("doc.visual");
                    window.OpenStudioDocument("doc.config");
                    Assert(live.Draft!=null,
                        "在不同Studio Tab之间切换后原配置草稿被非预期释放");
                    string folder=System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CxVisual","Configs","V24","Projects");
                    MHSConfigService service=new MHSConfigService(folder);
                    string projectId=session.Project.Id.ToString("N");
                    var scope=service.ForProject(projectId);
                    MHSConfigSection<CxVisionInspectionOptions> vision=scope.Open<CxVisionInspectionOptions>();
                    MHSConfigSection<CxCtReconstructionOptions> ct=scope.Open<CxCtReconstructionOptions>();
                    Assert(vision.Identity.ScopeId==projectId&&ct.Identity.ScopeId==projectId&&
                        vision.Identity.SectionId!=ct.Identity.SectionId&&session.Revision==p1Revision,
                        "Vision/CT配置不属于真实Project Scope或错误修改MEM真源");
                    // 实际MHS草稿→保存→待生效；软件配置不能偷改MEM Revision或Effective。
                    int oldEffective=vision.Get().GrayThreshold;
                    int targetThreshold=oldEffective==2200?2400:2200;
                    using(MHSConfigEditorModel<CxVisionInspectionOptions> editor=
                        new MHSConfigEditorModel<CxVisionInspectionOptions>(
                            vision,"P1 Vision专业配置验收"))
                    {
                        editor.SetValue(x=>x.GrayThreshold,targetThreshold);
                        MHSConfigCommitResult result=editor.CommitAsync(
                            System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                        Assert(result.Success&&vision.GetLatestSaved().GrayThreshold==targetThreshold&&
                            vision.Get().GrayThreshold==oldEffective&&
                            vision.Revision.IsPending&&session.Revision==p1Revision,
                            "专业配置保存被错误当作生效或错误触发MEM工程事务");
                    }
                    using(MHSConfigEditorModel<CxVisionInspectionOptions> invalid=
                        new MHSConfigEditorModel<CxVisionInspectionOptions>(
                            vision,"P1专业配置非法反证"))
                    {
                        invalid.SetValue(x=>x.PixelDepth,CxVisionPixelDepth.Bits8);
                        invalid.SetValue(x=>x.GrayThreshold,1000);
                        MHSConfigCommitResult refused=invalid.CommitAsync(
                            System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                        Assert(!refused.Success&&
                            vision.GetLatestSaved().GrayThreshold==targetThreshold&&
                            session.Revision==p1Revision,
                            "非法Vision配置污染了最近成功Saved或工程Revision");
                    }
                    window.CloseStudioDocument("doc.config");
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument reopened=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    Assert(!Object.ReferenceEquals(root,reopened.Content)&&
                        session.Revision==p1Revision,
                        "关闭后复用已Dispose的MHS编辑器或配置操作污染MEM历史");
                });
                CheckStep("P0-4 Studio真实CPU候选按钮到MRK运行及跨根页面/过期拒绝", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage original=session.Project.Pages.Single(p=>
                        p.Name=="P0-4 真实执行XRay");
                    MEMPage secondary=session.AddPage("P0-4 CPU候选跨根页面反证");
                    MOCFlowBuilder.Edit(session,secondary.RootContent,
                        flow=>flow.Step("Acquire"));
                    var prepare=typeof(MainWindow).GetMethod("OnPrepareFlowSoftwareRelease",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    var run=typeof(MainWindow).GetMethod("OnRunLogicDemo",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    var prepared=typeof(MainWindow).GetField("_preparedCpuRelease",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    var running=typeof(MainWindow).GetField("_logicRunning",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    Assert(prepare!=null&&run!=null&&prepared!=null&&running!=null,
                        "CPU软件发布与仿真运行实际WPF入口不存在");
                    vm.ActivePage=original;
                    window.OpenStudioDocument("doc.flow");
                    prepare.Invoke(window,new object[]{window,
                        new System.Windows.RoutedEventArgs()});
                    MOCProjectReleaseCandidate candidate=(MOCProjectReleaseCandidate)
                        prepared.GetValue(window);
                    Assert(candidate!=null&&candidate.EntryContentId==original.RootContent.Id,
                        "Studio真实发布入口没有准备原主流程MOC候选");
                    vm.ActivePage=secondary;
                    window.OpenStudioDocument("doc.flow");
                    run.Invoke(window,new object[]{window,
                        new System.Windows.RoutedEventArgs()});
                    Assert(!(bool)running.GetValue(window)&&
                        ((TextBlock)window.FindName("LogicInfo")).Text.Contains("entry_mismatch")&&
                        Object.ReferenceEquals(prepared.GetValue(window),candidate),
                        "切换其他可执行Root后悄悄执行已锁定的旧CPU候选");
                    vm.ActivePage=original;
                    window.OpenStudioDocument("doc.flow");
                    run.Invoke(window,new object[]{window,
                        new System.Windows.RoutedEventArgs()});
                    var frame=new System.Windows.Threading.DispatcherFrame();
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    var tick=new System.Windows.Threading.DispatcherTimer{
                        Interval=TimeSpan.FromMilliseconds(25)};
                    tick.Tick+=(sender,args)=>{
                        if(!(bool)running.GetValue(window)||clock.Elapsed>TimeSpan.FromSeconds(12))
                            frame.Continue=false;
                    };
                    tick.Start();
                    try{System.Windows.Threading.Dispatcher.PushFrame(frame);}
                    finally{tick.Stop();}
                    Assert(!(bool)running.GetValue(window)&&
                        prepared.GetValue(window)==null,
                        "CPU授权候选没有完成一次真实Studio MRK执行并释放候选");
                    var traces=(window.FindName("FlowTraceList") as ComboBox)
                        .ItemsSource as IEnumerable<MainWindow.FlowTraceRecord>;
                    Assert(traces!=null&&traces.Any(x=>
                        x.SoftwareReleaseFingerprint==candidate.Fingerprint&&
                        x.PublicationRevision==1&&x.FlowId!=Guid.Empty&&
                        x.RootContentId==original.RootContent.Id)&&
                        ((TextBlock)window.FindName("LogicInfo")).Text.Contains("Completed"),
                        "真实Studio CPU仿真运行没有携带MOC已授权版本与MRK轨迹");
                    prepare.Invoke(window,new object[]{window,
                        new System.Windows.RoutedEventArgs()});
                    candidate=(MOCProjectReleaseCandidate)prepared.GetValue(window);
                    Assert(candidate!=null,"新CPU候选准备失败");
                    session.AddContent(session.Project,"P0-4 CPU授权候选过期反证");
                    run.Invoke(window,new object[]{window,
                        new System.Windows.RoutedEventArgs()});
                    Assert(!(bool)running.GetValue(window)&&
                        Object.ReferenceEquals(prepared.GetValue(window),candidate)&&
                        ((TextBlock)window.FindName("LogicInfo")).Text.Contains(
                            "stale_engineering_revision"),
                        "过期候选被偷偷降级为普通CPU仿真或错误消费");
                    prepared.SetValue(window,null);
                });
                CheckStep("S2/P1 第三专业Schema注册发现与组合编辑去重复用", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    int before=session.Revision;
                    window.OpenStudioDocument("doc.config");
                    AssertThrows(()=>window.RegisterStudioConfiguration<CxAutomationDeviceOptions>(
                        "Automation设备配置"));
                    window.CloseStudioDocument("doc.config");
                    window.RegisterStudioConfiguration<CxAutomationDeviceOptions>(
                        "Automation设备配置","core.content");
                    AssertThrows(()=>window.RegisterStudioConfiguration<CxAutomationDeviceOptions>(
                        "重复Automation"));
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    System.Windows.DependencyObject root=doc.Content as
                        System.Windows.DependencyObject;
                    Func<System.Windows.DependencyObject,Type,bool> exists=null;
                    exists=(parent,type)=>type.IsInstanceOfType(parent)||
                        System.Windows.LogicalTreeHelper.GetChildren(parent)
                            .OfType<System.Windows.DependencyObject>()
                            .Any(child=>exists(child,type));
                    Assert(root!=null&&
                        exists(root,typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>))&&
                        exists(root,typeof(MPWConfigEditorControl<CxVisionInspectionOptions>))&&
                        exists(root,typeof(MPWConfigEditorControl<CxCtReconstructionOptions>))&&
                        exists(root,typeof(MPWConfigEditorControl<
                            CxVisual.Mechanism.Data.RuntimeData.MDSDataLifecycleEngineeringOptions>)),
                        "第三个专业并未通过注册接入同一个真实配置页面");
                    // WPF AvalonDock document content can be hosted through a
                    // ControlTemplate. LogicalTreeHelper alone misses visual-only
                    // panels after template expansion, even when they are rendered.
                    var panels=new List<System.Windows.Controls.Primitives.UniformGrid>();
                    var visited=new HashSet<System.Windows.DependencyObject>();
                    Action<System.Windows.DependencyObject> visit=null;
                    visit=node=>{
                        if(node==null||!visited.Add(node))return;
                        var grid=node as System.Windows.Controls.Primitives.UniformGrid;
                        if(grid!=null)panels.Add(grid);
                        var visual=node as System.Windows.Media.Visual;
                        if(visual!=null)
                            for(int index=0;index<System.Windows.Media.VisualTreeHelper.GetChildrenCount(visual);index++)
                                visit(System.Windows.Media.VisualTreeHelper.GetChild(visual,index));
                        foreach(var child in System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>())visit(child);
                    };
                    visit(root);
                    // 默认已有Vision、CT3D和MDS生命周期三个Section；
                    // 再注册Automation后应为四张卡，列数仍按可用宽度限制在1~3。
                    Assert(panels.Any(g=>g.Children.Count==4&&g.Columns>=1&&g.Columns<=3),
                        "配置组合未使用可响应布局的统一1/2/3列容器");
                    string rootDir=System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CxVisual","Configs","V24","Projects");
                    MHSConfigService config=new MHSConfigService(rootDir);
                    var scope=config.ForProject(session.Project.Id.ToString("N"));
                    Assert(scope.Open<CxAutomationDeviceOptions>().Identity.ScopeId==
                        session.Project.Id.ToString("N")&&session.Revision==before,
                        "第三专业注册后丢失Project隔离或错误修改MEM工程");
                });
                CheckStep("S2/P1 资源及可信机台Scope选择与未保存草稿阻断", () =>
                {
                    StudioViewModel model=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=model.ProjectSession;
                    window.CloseStudioDocument("doc.config");
                    window.RegisterStudioConfigurationScope(
                        "machine","P1-MACHINE-001","验收机台");
                    AssertThrows(()=>window.RegisterStudioConfigurationScope(
                        "machine","P1-MACHINE-001","重复机台"));
                    AssertThrows(()=>window.RegisterStudioConfigurationScope(
                        "resource","fake","伪造资源"));
                    MEMContent content=session.AddContent(session.Project,"P1配置资源Content");
                    int before=session.Revision;
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument document=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    System.Windows.DependencyObject root=(System.Windows.DependencyObject)document.Content;
                    Func<System.Windows.DependencyObject,System.Windows.Controls.ComboBox> findBox=null;
                    findBox=node=>{
                        System.Windows.Controls.ComboBox combo=node as System.Windows.Controls.ComboBox;
                        if(combo!=null&&combo.Name=="StudioConfigScopeSelector")return combo;
                        return System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>findBox(child)).FirstOrDefault(x=>x!=null);
                    };
                    var selector=findBox(root);
                    Assert(selector!=null,"Studio没有真实的配置Scope选择控件");
                    object project=selector.Items.Cast<object>().First(x=>
                        x.ToString().StartsWith("工程配置"));
                    object resource=selector.Items.Cast<object>().First(x=>
                        x.ToString().Contains(content.Name));
                    object machine=selector.Items.Cast<object>().First(x=>
                        x.ToString().Contains("验收机台"));
                    selector.SelectedItem=resource;
                    Func<System.Windows.DependencyObject,Type,bool> exists=null;
                    exists=(node,type)=>type.IsInstanceOfType(node)||
                        System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Any(child=>exists(child,type));
                    Assert(exists(root,typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>))&&
                        !exists(root,typeof(MPWConfigEditorControl<CxVisionInspectionOptions>))&&
                        !exists(root,typeof(MPWConfigEditorControl<CxCtReconstructionOptions>)),
                        "资源Scope没有根据声明的core.content类型过滤专业");
                    Func<System.Windows.DependencyObject,MPWConfigEditorControl<CxAutomationDeviceOptions>>
                        findAutomation=null;
                    findAutomation=node=>node as MPWConfigEditorControl<CxAutomationDeviceOptions> ??
                        System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>findAutomation(child)).FirstOrDefault(x=>x!=null);
                    MPWConfigEditorControl<CxAutomationDeviceOptions> active=findAutomation(root);
                    Assert(active!=null,"资源Scope没有创建真实Automation Editor");
                    var field=typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>)
                        .GetField("_editor",System.Reflection.BindingFlags.Instance|
                            System.Reflection.BindingFlags.NonPublic);
                    MHSConfigEditorModel<CxAutomationDeviceOptions> editor=
                        field.GetValue(active) as MHSConfigEditorModel<CxAutomationDeviceOptions>;
                    int port=editor.Draft.ControllerPort==1502?1503:1502;
                    editor.SetValue(x=>x.ControllerPort,port);
                    Assert(active.HasUnsavedDraft,"配置草稿编辑没有正确标记未保存状态");
                    selector.SelectedItem=project;
                    Assert(Object.ReferenceEquals(selector.SelectedItem,resource)&&
                        active.HasUnsavedDraft,"更换Scope丢弃了未保存的专业编辑草稿");
                    editor.Undo();
                    Assert(!active.HasUnsavedDraft,"草稿撤销后仍错误阻断Scope切换");
                    selector.SelectedItem=project;
                    Assert(Object.ReferenceEquals(selector.SelectedItem,project)&&
                        exists(root,typeof(MPWConfigEditorControl<CxVisionInspectionOptions>)),
                        "切回Project未恢复原配置视图");
                    selector.SelectedItem=machine;
                    Assert(Object.ReferenceEquals(selector.SelectedItem,machine)&&
                        exists(root,typeof(MPWConfigEditorControl<CxCtReconstructionOptions>)),
                        "可信宿主注册的机台Scope无法加载原公共Schema");
                    string folder=System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CxVisual","Configs","V24","Projects");
                    MHSConfigService service=new MHSConfigService(folder);
                    Assert(service.ForScope("resource",content.Id.ToString("N"))
                        .Open<CxAutomationDeviceOptions>().Identity.ScopeKind=="resource"&&
                        service.ForScope("machine","P1-MACHINE-001")
                        .Open<CxAutomationDeviceOptions>().Identity.ScopeId=="P1-MACHINE-001"&&
                        session.Revision==before,
                        "Scope选择更改MEM工程Revision或伪造配置实例身份");
                });
                CheckStep("S2/P1 专业ICommand统一CommandId与Ribbon右键快捷键门禁", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    int before=session.Revision,executed=0;
                    bool permitted=false;
                    ICommand owner=new RelayCommand(()=>executed++,()=>permitted);
                    window.OpenStudioDocument("doc.config");
                    window.RegisterStudioCommand("p1.verify.scope","验证当前专业配置",
                        "配置","doc.config",owner,
                        System.Windows.Input.Key.K,
                        System.Windows.Input.ModifierKeys.Control|
                        System.Windows.Input.ModifierKeys.Shift);
                    AssertThrows(()=>window.RegisterStudioCommand("p1.verify.scope","重复",
                        "配置","doc.config",owner));
                    AssertThrows(()=>window.RegisterStudioCommand("p1.conflict","冲突",
                        "配置","doc.config",owner,
                        System.Windows.Input.Key.K,
                        System.Windows.Input.ModifierKeys.Control|
                        System.Windows.Input.ModifierKeys.Shift));
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(b=>Convert.ToString(b.Content)=="配置")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button ribbon=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(b=>Convert.ToString(b.Tag)=="p1.verify.scope");
                    LayoutDocument doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    FrameworkElement view=doc.Content as FrameworkElement;
                    var menu=view.ContextMenu.Items.OfType<MenuItem>()
                        .Single(m=>Convert.ToString(m.Tag)=="p1.verify.scope");
                    Assert(Object.ReferenceEquals(ribbon.Command,menu.Command)&&
                        !ribbon.Command.CanExecute(null)&&
                        !window.HandleStudioRegisteredShortcut(
                            System.Windows.Input.Key.K,
                            System.Windows.Input.ModifierKeys.None),
                        "命令上下文、快捷键或授权状态不一致");
                    Assert(window.HandleStudioRegisteredShortcut(
                        System.Windows.Input.Key.K,
                        System.Windows.Input.ModifierKeys.Control|
                        System.Windows.Input.ModifierKeys.Shift)&&executed==0,
                        "不授权专业Command仍被快捷键执行");
                    permitted=true;
                    Assert(ribbon.Command.CanExecute(null),"授权后命令无法启用");
                    ribbon.Command.Execute(null);
                    menu.Command.Execute(null);
                    window.HandleStudioRegisteredShortcut(
                        System.Windows.Input.Key.K,
                        System.Windows.Input.ModifierKeys.Control|
                        System.Windows.Input.ModifierKeys.Shift);
                    Assert(executed==3&&session.Revision==before,
                        "Ribbon、右键、快捷键没有共享真实ICommand Owner");
                    window.OpenStudioDocument("doc.visual");
                    Assert(!ribbon.Command.CanExecute(null)&&
                        !window.HandleStudioRegisteredShortcut(
                            System.Windows.Input.Key.K,
                            System.Windows.Input.ModifierKeys.Control|
                            System.Windows.Input.ModifierKeys.Shift),
                        "命令脱离活动配置文档仍可执行");
                    ribbon.Command.Execute(null);
                    Assert(executed==3,"失效文档命令仍改变专业状态");
                });
                CheckStep("S2/P1 专业Command实时工程Revision及配置Scope上下文", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    window.OpenStudioDocument("doc.config");
                    MainWindow.StudioCommandContext observed=null;
                    window.RegisterStudioCommand("p1.context.verify","查询配置作用域",
                        "配置","doc.config",new RelayCommand(value=>
                        {
                            observed=value as MainWindow.StudioCommandContext;
                        }));
                    window.StudioDynamicMenus.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Content)=="配置")
                        .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    Button item=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Tag)=="p1.context.verify");
                    item.Command.Execute(null);
                    Assert(observed!=null&&observed.ProjectId==session.Project.Id&&
                        observed.EngineeringRevision==session.Revision&&
                        observed.DocumentId=="doc.config"&&
                        observed.ConfigScopeKind=="machine"&&
                        observed.ConfigScopeId=="P1-MACHINE-001"&&
                        !observed.ResourceId.HasValue,
                        "专业命令没有收到真实工程/Revision/配置Scope上下文");
                    window.OpenStudioDocument("doc.visual");
                    Assert(!item.Command.CanExecute(null),
                        "离开配置Editor之后仍允许沿旧Scope执行专业Command");
                });
                CheckStep("S2/P1 删除MEM资源使MHS配置页失效且Undo恢复后可发现", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent resource=session.Project.Contents.Single(x=>
                        x.Name=="P1配置资源Content");
                    window.CloseStudioDocument("doc.config");
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    Func<System.Windows.DependencyObject,ComboBox> find=null;
                    find=node=>{
                        ComboBox box=node as ComboBox;
                        if(box!=null&&box.Name=="StudioConfigScopeSelector")return box;
                        return System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(c=>find(c)).FirstOrDefault(c=>c!=null);
                    };
                    ComboBox selector=find((System.Windows.DependencyObject)doc.Content);
                    Assert(selector!=null,"配置界面没有真实Scope选择器");
                    object selected=selector.Items.Cast<object>().Single(x=>
                        x.ToString().Contains(resource.Name));
                    selector.SelectedItem=selected;
                    Assert(Object.ReferenceEquals(selector.SelectedItem,selected),
                        "测试未真正选中待删除MEM资源Scope");
                    // 资源被工程事务删除时，即使旧Scope有草稿也绝不能允许继续提交。
                    Func<System.Windows.DependencyObject,MPWConfigEditorControl<CxAutomationDeviceOptions>>
                        findEditor=null;
                    findEditor=node=>node as MPWConfigEditorControl<CxAutomationDeviceOptions> ??
                        System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>findEditor(child)).FirstOrDefault(x=>x!=null);
                    var active=findEditor((System.Windows.DependencyObject)doc.Content);
                    var editorField=typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>)
                        .GetField("_editor",System.Reflection.BindingFlags.Instance|
                            System.Reflection.BindingFlags.NonPublic);
                    var draft=editorField.GetValue(active) as
                        MHSConfigEditorModel<CxAutomationDeviceOptions>;
                    draft.SetValue(x=>x.StationId,draft.Draft.StationId==7?8:7);
                    Assert(window.HasStudioUnsavedConfiguration(),
                        "删除资源反证未建立真实未保存配置草稿");
                    int removalRevision=session.Revision;
                    session.Remove(resource);
                    Assert(session.Find(resource.Id)==null&&session.Revision>removalRevision&&
                        !window.GetStudioContentIds().Contains("doc.config"),
                        "资源已删除但仍持有旧MHS资源配置编辑器");
                    Assert(session.Undo().Success&&session.Find(resource.Id)!=null,
                        "MEM Undo没能恢复原资源稳定ID");
                    window.OpenStudioDocument("doc.config");
                    doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    selector=find((System.Windows.DependencyObject)doc.Content);
                    Assert(selector!=null&&selector.Items.Cast<object>().Any(x=>
                        x.ToString().Contains(resource.Name)),
                        "Undo恢复后没有从MEM资源重新发现MHS Scope");
                    window.CloseStudioDocument("doc.config");
                });
                CheckStep("S2/P1 MHS授权Session在Scope切换与生效操作中拒绝越权", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent resource=session.Project.Contents.First(x=>
                        x.Name=="P1配置资源Content");
                    int authRevision=session.Revision;
                    window.CloseStudioDocument("doc.config");
                    FCMConfigPrincipal principal=new FCMConfigPrincipal(
                        "p1-config-operator",new[]{"p1-editor"});
                    FCMConfigRolePolicy policy=new FCMConfigRolePolicy();
                    foreach(FCMConfigAccessAction action in new[]{
                        FCMConfigAccessAction.Read,FCMConfigAccessAction.ReadSensitive,
                        FCMConfigAccessAction.Edit,FCMConfigAccessAction.Commit,
                        FCMConfigAccessAction.Audit,FCMConfigAccessAction.Restore})
                        policy.Grant("p1-editor",action,"Automation.Device",
                            "resource",resource.Id.ToString("N"));
                    window.SetStudioConfigurationAuthorization(principal,policy);
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument document=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    System.Windows.DependencyObject root=
                        (System.Windows.DependencyObject)document.Content;
                    Func<System.Windows.DependencyObject,System.Windows.Controls.ComboBox> find=null;
                    find=node=>{
                        var current=node as System.Windows.Controls.ComboBox;
                        if(current!=null&&current.Name=="StudioConfigScopeSelector")
                            return current;
                        return System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>find(child)).FirstOrDefault(x=>x!=null);
                    };
                    var selector=find(root);
                    object resourceItem=selector.Items.Cast<object>().First(x=>
                        x.ToString().Contains(resource.Name));
                    selector.SelectedItem=resourceItem;
                    Func<System.Windows.DependencyObject,Type,bool> exists=null;
                    exists=(node,t)=>t.IsInstanceOfType(node)||
                        System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Any(child=>exists(child,t));
                    Assert(exists(root,typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>)),
                        "已授权资源Scope的实际MHS WPF Editor无法打开");
                    object project=selector.Items.Cast<object>().First(x=>
                        x.ToString().StartsWith("工程配置"));
                    selector.SelectedItem=project;
                    Assert(!exists(root,typeof(MPWConfigEditorControl<CxVisionInspectionOptions>))&&
                        !exists(root,typeof(MPWConfigEditorControl<CxCtReconstructionOptions>)),
                        "无权限Project Scope仍泄露了专业编辑器或配置字段");
                    string folder=System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CxVisual","Configs","V24","Projects");
                    var authorized=new MHSConfigService(folder).Authorize(principal,policy)
                        .Open<CxAutomationDeviceOptions>(
                            new FCMConfigIdentity("Automation.Device","resource",
                                resource.Id.ToString("N")));
                    AssertThrows(()=>authorized.TryApplyPendingAtBoundary(
                        FCMConfigApplyPolicy.NextTask));
                    Assert(session.Revision==authRevision,
                        "配置权限拒绝错误地写入工程Revision");
                });
                CheckStep("S2/P1 配置Dock关闭阻断未保存草稿并可显式放弃", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    int before=session.Revision;
                    MEMContent resource=session.Project.Contents.First(x=>
                        x.Name=="P1配置资源Content");
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    Func<System.Windows.DependencyObject,System.Windows.Controls.ComboBox> find=null;
                    find=node=>{
                        var combo=node as System.Windows.Controls.ComboBox;
                        if(combo!=null&&combo.Name=="StudioConfigScopeSelector")return combo;
                        return System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>find(child)).FirstOrDefault(x=>x!=null);
                    };
                    var selector=find((System.Windows.DependencyObject)doc.Content);
                    selector.SelectedItem=selector.Items.Cast<object>().First(x=>
                        x.ToString().Contains(resource.Name));
                    Func<System.Windows.DependencyObject,MPWConfigEditorControl<CxAutomationDeviceOptions>>
                        findEditor=null;
                    findEditor=node=>node as MPWConfigEditorControl<CxAutomationDeviceOptions> ??
                        System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>findEditor(child)).FirstOrDefault(x=>x!=null);
                    var editorControl=findEditor((System.Windows.DependencyObject)doc.Content);
                    Assert(editorControl!=null,"授权资源Scope的配置编辑器不存在");
                    var field=typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>)
                        .GetField("_editor",System.Reflection.BindingFlags.Instance|
                            System.Reflection.BindingFlags.NonPublic);
                    var model=field.GetValue(editorControl) as
                        MHSConfigEditorModel<CxAutomationDeviceOptions>;
                    model.SetValue(x=>x.StationId,model.Draft.StationId==3?4:3);
                    Assert(window.HasStudioUnsavedConfiguration()&&
                        !window.TryCloseStudioDocument("doc.config")&&
                        window.GetStudioContentIds().Contains("doc.config")&&
                        editorControl.HasUnsavedDraft,
                        "关闭配置页绕过未保存草稿保护");
                    Assert(window.TryCloseStudioDocument("doc.config",true)&&
                        !window.GetStudioContentIds().Contains("doc.config")&&
                        session.Revision==before,
                        "明确放弃未保存草稿未释放Dock配置Editor或污染MEM");
                    window.OpenStudioDocument("doc.config");
                    doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    Assert(!Object.ReferenceEquals(editorControl,
                        findEditor((System.Windows.DependencyObject)doc.Content))&&
                        session.Revision==before,
                        "重新打开复用了已放弃草稿或产生工程事务");
                });
                CheckStep("S2/P1 工程新建与强制打开均遵守配置草稿前置门禁", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession original=vm.ProjectSession;
                    int originalRevision=original.Revision;
                    Func<bool> saved=vm.BeforeProjectReplacement;
                    int attempts=0;
                    try
                    {
                        vm.BeforeProjectReplacement=()=>{
                            attempts++;
                            return false; // 模拟用户拒绝放弃未保存专业配置草稿
                        };
                        vm.NewCommand.Execute(null);
                        Assert(Object.ReferenceEquals(original,vm.ProjectSession)&&
                            original.Revision==originalRevision,
                            "新建工程绕过配置草稿确认并替换了MEM真源");
                        vm.OpenProject(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                            "missing-p1-should-not-open.cxproj"),true);
                        Assert(attempts==2&&
                            Object.ReferenceEquals(original,vm.ProjectSession),
                            "discardCurrent=true绕过了专业配置草稿保护门禁");
                    }
                    finally {vm.BeforeProjectReplacement=saved;}
                });
                CheckStep("S2/P1 页面嵌入多层Content Scope发现与删除Undo稳定身份", () =>
                {
                    StudioViewModel embeddedVm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession embeddedSession=embeddedVm.ProjectSession;
                    window.TryCloseStudioDocument("doc.config",true);
                    MEMPage embeddedScopePage=embeddedSession.AddPage("P1嵌入式配置页面");
                    MEMContent embeddedScopeChild=embeddedSession.AddContent(
                        embeddedScopePage.RootContent,"P1相机嵌入配置");
                    Guid embeddedStableId=embeddedScopeChild.Id;
                    int embeddedScopeRevision=embeddedSession.Revision;
                    // P1权限反证：真实新资源不能继承上一资源的已授权身份。
                    FCMConfigPrincipal embeddedPrincipal=new FCMConfigPrincipal(
                        "p1-embedded-operator",new[]{"p1-embedded-editor"});
                    FCMConfigRolePolicy embeddedPolicy=new FCMConfigRolePolicy();
                    window.SetStudioConfigurationAuthorization(embeddedPrincipal,embeddedPolicy);
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument embeddedScopeDocument=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    Func<System.Windows.DependencyObject,ComboBox> embeddedFindBox=null;
                    embeddedFindBox=node=>{
                        ComboBox box=node as ComboBox;
                        if(box!=null&&box.Name=="StudioConfigScopeSelector")return box;
                        return System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Select(child=>embeddedFindBox(child)).FirstOrDefault(x=>x!=null);
                    };
                    ComboBox embeddedSelector=embeddedFindBox(
                        (System.Windows.DependencyObject)embeddedScopeDocument.Content);
                    Assert(embeddedSelector!=null,"多层资源配置没有真实Scope选择器");
                    object embeddedChoice=embeddedSelector.Items.Cast<object>().Single(x=>
                        x.ToString().Contains("P1嵌入式配置页面/RootContent/P1相机嵌入配置"));
                    Assert(embeddedSelector.Items.Cast<object>().Any(x=>
                        x.ToString().Contains("P1嵌入式配置页面/RootContent (core.content)")),
                        "Page根内嵌Content未被识别为可配置的MEM资源");
                    embeddedSelector.SelectedItem=embeddedChoice;
                    Func<System.Windows.DependencyObject,Type,bool> embeddedHasEditor=null;
                    embeddedHasEditor=(node,type)=>type.IsInstanceOfType(node)||
                        System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<System.Windows.DependencyObject>()
                            .Any(child=>embeddedHasEditor(child,type));
                    Assert(!embeddedHasEditor(
                        (System.Windows.DependencyObject)embeddedScopeDocument.Content,
                        typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>)),
                        "未经授权的嵌入Content错误开放了专业配置编辑器");
                    window.TryCloseStudioDocument("doc.config",true);
                    foreach(FCMConfigAccessAction embeddedAction in new[]{
                        FCMConfigAccessAction.Read,FCMConfigAccessAction.ReadSensitive,
                        FCMConfigAccessAction.Edit,FCMConfigAccessAction.Commit,
                        FCMConfigAccessAction.Audit,FCMConfigAccessAction.Restore})
                        embeddedPolicy.Grant("p1-embedded-editor",embeddedAction,
                            "Automation.Device","resource",embeddedStableId.ToString("N"));
                    window.SetStudioConfigurationAuthorization(embeddedPrincipal,embeddedPolicy);
                    window.OpenStudioDocument("doc.config");
                    embeddedScopeDocument=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    embeddedSelector=embeddedFindBox(
                        (System.Windows.DependencyObject)embeddedScopeDocument.Content);
                    embeddedChoice=embeddedSelector.Items.Cast<object>().Single(x=>
                        x.ToString().Contains("P1嵌入式配置页面/RootContent/P1相机嵌入配置"));
                    embeddedSelector.SelectedItem=embeddedChoice;
                    Assert(embeddedHasEditor(
                        (System.Windows.DependencyObject)embeddedScopeDocument.Content,
                        typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>))&&
                        !embeddedHasEditor(
                            (System.Windows.DependencyObject)embeddedScopeDocument.Content,
                            typeof(MPWConfigEditorControl<CxVisionInspectionOptions>)),
                        "显式授权的多层资源Scope未通过TypeId映射到Automation专业");
                    var embeddedService=new MHSConfigService(System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CxVisual","Configs","V24","Projects"));
                    Assert(embeddedService.ForScope("resource",embeddedStableId.ToString("N"))
                        .Open<CxAutomationDeviceOptions>().Identity.ScopeId==
                        embeddedStableId.ToString("N")&&
                        embeddedSession.Revision==embeddedScopeRevision,
                        "内嵌资源配置Scope没有使用稳定Id或错误写了MEM Revision");
                    // Undo刚新增的嵌套资源：旧配置编辑器必须立即失效，禁止悬空提交。
                    embeddedSession.Undo().RequireValue();
                    Assert(embeddedSession.Find(embeddedStableId)==null&&
                        !window.GetStudioContentIds().Contains("doc.config"),
                        "撤销删除内嵌资源后旧MHS编辑器仍然存活");
                    embeddedSession.Redo().RequireValue();
                    Assert(embeddedSession.Find(embeddedStableId)!=null,
                        "Redo没有恢复MEM内嵌资源稳定身份");
                    window.OpenStudioDocument("doc.config");
                    embeddedScopeDocument=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    embeddedSelector=embeddedFindBox(
                        (System.Windows.DependencyObject)embeddedScopeDocument.Content);
                    Assert(embeddedSelector.Items.Cast<object>().Any(x=>x.ToString()
                        .Contains("P1嵌入式配置页面/RootContent/P1相机嵌入配置")),
                        "嵌入Content恢复后仍不能从唯一MEM真树重新发现");
                    window.TryCloseStudioDocument("doc.config",true);
                });
                CheckStep("S2/P1 统一Editor候选、专业Viewer稳定DocumentKey及删除失效", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P1-Editor相机");
                    MEMPage page=session.AddPage("P1-Editor页面");
                    Guid projectId=session.Project.Id;
                    int created=0;
                    window.RegisterStudioResourceViewer("vision.camera.viewer","Vision相机观察",
                        identity=>{
                            created++;
                            Assert(identity.ProjectId==projectId&&session.Find(identity.ResourceId)!=null&&
                                identity.ResourceTypeId=="core.content","Viewer收到错误的工程资源身份");
                            return new TextBlock {Text="Vision实际专业Editor宿主："+identity.ResourceId};
                        },true,"core.content");
                    AssertThrows(()=>window.RegisterStudioResourceViewer(
                        "vision.camera.viewer","重复",(identity)=>new TextBlock(),false,"core.content"));
                    AssertThrows(()=>window.RegisterStudioResourceViewer(
                        "mol.flow","冒充内置流程",(identity)=>new TextBlock(),false,"core.content"));
                    Assert(window.GetStudioResourceEditors(page.Id).Any(e=>e.EditorId=="mem.visual")&&
                        !window.GetStudioResourceEditors(page.Id).Any(e=>e.EditorId=="vision.camera.viewer"),
                        "专业Viewer错误匹配到其他资源类型");
                    var candidates=window.GetStudioResourceEditors(content.Id);
                    Assert(candidates.Any(e=>e.EditorId=="mem.visual")&&
                        candidates.Any(e=>e.EditorId=="mol.flow")&&
                        candidates.Any(e=>e.EditorId=="vision.camera.viewer")&&
                        candidates.First().EditorId=="vision.camera.viewer",
                        "统一Editor注册未返回真实图元/流程/专业默认候选");
                    AssertThrows(()=>window.OpenStudioResourceEditor(page.Id,"vision.camera.viewer"));
                    AssertThrows(()=>window.OpenStudioResourceEditor(content.Id,"unknown.viewer"));
                    string scene=window.OpenStudioResourceEditor(content.Id,"mem.visual");
                    string flow=window.OpenStudioResourceEditor(content.Id,"mol.flow");
                    string viewer=window.OpenStudioResourceEditor(content.Id);
                    Assert(scene!=flow&&flow!=viewer&&
                        window.ActiveStudioDocumentId==viewer&&created==1,
                        "同一资源不同Editor串页或专业Viewer工厂未实例化");
                    StudioResourceDocumentIdentity identityNow=
                        window.GetStudioResourceDocumentIdentity(viewer);
                    Assert(identityNow!=null&&identityNow.ProjectId==projectId&&
                        identityNow.ResourceId==content.Id&&
                        identityNow.EditorId=="vision.camera.viewer"&&
                        identityNow.InstanceId==null&&
                        window.GetStudioCommandContext().ResourceId==content.Id,
                        "专业Viewer活动命令未绑定真实MEM资源/Editor身份");
                    Assert(window.OpenStudioResourceEditor(content.Id)==viewer&&created==1,
                        "相同DocumentKey不应重复构造Viewer");
                    window.SaveStudioLayout(temporary);
                    window.RestoreStudioLayout(temporary);
                    Assert(window.GetStudioResourceDocumentIdentity(viewer)!=null&&
                        window.GetStudioContentIds().Contains(viewer)&&created==1,
                        "布局保存重开未恢复同一专业Editor资源身份与视图");
                    Guid instance=Guid.NewGuid();
                    string instanceTab=window.OpenStudioResourceEditor(content.Id,
                        "vision.camera.viewer",instance);
                    Assert(instanceTab!=viewer&&created==2&&
                        window.GetStudioResourceDocumentIdentity(instanceTab).InstanceId==instance,
                        "不同实例路径共用了专业Editor身份");
                    AssertThrows(()=>window.OpenStudioResourceEditor(content.Id,"mem.visual",instance));
                    Assert(window.TryCloseStudioDocument(instanceTab)&&
                        window.GetStudioResourceDocumentIdentity(instanceTab)==null,
                        "关闭专业Editor未释放实例DocumentKey");
                    Assert(window.OpenStudioResourceEditor(content.Id,
                        "vision.camera.viewer",instance)==instanceTab&&created==3,
                        "关闭后重新打开未创建新的专业Editor实例");
                    MEMContent transient=session.AddContent(session.Project,"P1-删除失效资源");
                    string transientTab=window.OpenStudioResourceEditor(
                        transient.Id,"vision.camera.viewer");
                    Assert(window.GetStudioContentIds().Contains(transientTab),
                        "资源刚创建时专业Viewer无法打开");
                    session.Undo().RequireValue();
                    Assert(session.Find(transient.Id)==null&&
                        window.GetStudioResourceDocumentIdentity(transientTab)==null&&
                        !window.GetStudioContentIds().Contains(transientTab),
                        "撤销删除MEM资源后旧Viewer仍可提交或占用Dock");
                    Assert(window.GetStudioResourceEditors(transient.Id).Count==0&&
                        window.GetStudioResourceEditors(Guid.NewGuid()).Count==0,
                        "已删除/非当前工程资源仍被发现");
                    window.TryCloseStudioDocument(viewer);
                    window.TryCloseStudioDocument(instanceTab);
                });
                CheckStep("S2/P1 Editor级Owner ICommand跨资源Tab与快捷键状态隔离", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent a=session.AddContent(session.Project,"P1相机A");
                    MEMContent b=session.AddContent(session.Project,"P1相机B");
                    window.RegisterStudioResourceViewer("vision.camera.commands","相机控制台",
                        identity=>new TextBlock {Text="相机："+identity.ResourceId},
                        false,"core.content");
                    int executed=0;
                    Guid executedResource=Guid.Empty;
                    bool permitted=false;
                    ICommand owner=new RelayCommand(()=>{
                        executed++;
                        MainWindow.StudioCommandContext ctx=window.GetStudioCommandContext();
                        executedResource=ctx.ResourceId.HasValue?ctx.ResourceId.Value:Guid.Empty;
                    },()=>permitted);
                    window.RegisterStudioResourceEditorCommand("vision.camera.commands",
                        "vision.camera.check","校验相机参数","资源",owner,
                        System.Windows.Input.Key.L,
                        System.Windows.Input.ModifierKeys.Control|
                        System.Windows.Input.ModifierKeys.Shift);
                    AssertThrows(()=>window.RegisterStudioResourceEditorCommand("missing.viewer",
                        "vision.camera.bad","无效","资源",owner));
                    string first=window.OpenStudioResourceEditor(a.Id,"vision.camera.commands");
                    string second=window.OpenStudioResourceEditor(b.Id,"vision.camera.commands");
                    Assert(first!=second&&window.ActiveStudioDocumentId==second&&
                        window.GetStudioCommandContext().ResourceId==b.Id,
                        "同类专业Viewer不同资源共用了DocumentKey或命令身份");
                    Button ribbon=window.StudioRibbonActions.Children.OfType<Button>()
                        .Single(x=>Convert.ToString(x.Tag)=="vision.camera.check");
                    LayoutDocument doc=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId==second);
                    MenuItem menu=((FrameworkElement)doc.Content).ContextMenu.Items.OfType<MenuItem>()
                        .Single(x=>Convert.ToString(x.Tag)=="vision.camera.check");
                    Assert(Object.ReferenceEquals(ribbon.Command,menu.Command)&&
                        !ribbon.Command.CanExecute(null),
                        "跨Tab专业命令没有复用唯一Owner适配器或未严格禁用");
                    Assert(window.HandleStudioRegisteredShortcut(System.Windows.Input.Key.L,
                        System.Windows.Input.ModifierKeys.Control|
                        System.Windows.Input.ModifierKeys.Shift)&&executed==0,
                        "禁用的专业快捷键仍执行了Owner");
                    permitted=true;
                    ribbon.Command.Execute(null);
                    Assert(executed==1&&executedResource==b.Id,
                        "专业命令误用了上一个资源Tab的身份");
                    window.OpenStudioResourceEditor(a.Id,"vision.camera.commands");
                    menu.Command.Execute(menu.CommandParameter);
                    Assert(executed==1,"旧Tab右键菜单可在非活动资源上执行命令");
                    window.HandleStudioRegisteredShortcut(System.Windows.Input.Key.L,
                        System.Windows.Input.ModifierKeys.Control|
                        System.Windows.Input.ModifierKeys.Shift);
                    Assert(executed==2&&executedResource==a.Id,
                        "快捷键未重新绑定当前活动MEM资源");
                    window.OpenStudioDocument("doc.home");
                    Assert(!ribbon.Command.CanExecute(null)&&
                        !window.HandleStudioRegisteredShortcut(System.Windows.Input.Key.L,
                            System.Windows.Input.ModifierKeys.Control|
                            System.Windows.Input.ModifierKeys.Shift),
                        "切换到主页后专业命令仍可在错误上下文执行");
                    window.TryCloseStudioDocument(first);
                    window.TryCloseStudioDocument(second);
                });
                CheckStep("S2/P1 真实Schema组合搜索折叠不改变草稿与工程Revision", () =>
                {
                    window.TryCloseStudioDocument("doc.config",true);
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    int schemaRevision=vm.ProjectSession.Revision;
                    window.OpenStudioDocument("doc.config");
                    LayoutDocument config=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    Func<DependencyObject,IEnumerable<DependencyObject>> walk=null;
                    walk=root=>new[]{root}.Concat(
                        System.Windows.LogicalTreeHelper.GetChildren(root)
                            .OfType<DependencyObject>().SelectMany(child=>walk(child)));
                    DependencyObject[] all=walk((DependencyObject)config.Content).ToArray();
                    TextBox search=all.OfType<TextBox>().Single(x=>
                        x.Name=="StudioConfigSectionSearch");
                    Border vision=all.OfType<Border>().Single(x=>
                        (Convert.ToString(x.Tag)??"").Contains("Vision.Inspection"));
                    Border ct=all.OfType<Border>().Single(x=>
                        (Convert.ToString(x.Tag)??"").Contains("CT3D.Reconstruction"));
                    Expander ctSection=all.OfType<Expander>().Single(x=>
                        Convert.ToString(x.Tag)=="CT3D.Reconstruction");
                    Assert(vision.Visibility==Visibility.Visible&&
                        ct.Visibility==Visibility.Visible&&ctSection.IsExpanded,
                        "默认组合页未展开真实Schema配置卡");
                    int count=window.GetStudioContentIds().Length;
                    search.Text="ct3d";
                    Assert(vision.Visibility==Visibility.Collapsed&&
                        ct.Visibility==Visibility.Visible,
                        "配置搜索未按SectionId及大小写过滤");
                    ctSection.IsExpanded=false;
                    search.Text="";
                    Assert(ct.Visibility==Visibility.Visible&&
                        vision.Visibility==Visibility.Visible&&
                        !ctSection.IsExpanded&&
                        window.GetStudioContentIds().Length==count&&
                        vm.ProjectSession.Revision==schemaRevision,
                        "折叠与搜索修改了配置Editor身份或MEM工程Revision");
                    window.TryCloseStudioDocument("doc.config",true);
                });
                CheckStep("S2/P1 工程资源树右键动态Editor候选与过期资源拒绝", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent content=session.AddContent(session.Project,"P1资源树动态Editor");
                    int editorMenuRevision=session.Revision;
                    ContextMenu menu=window.CreateStudioResourceEditorMenu(content);
                    MenuItem[] candidates=menu.Items.OfType<MenuItem>().ToArray();
                    Assert(candidates.Any(x=>Convert.ToString(x.Tag)=="mem.visual")&&
                        candidates.Any(x=>Convert.ToString(x.Tag)=="mol.flow")&&
                        candidates.Any(x=>Convert.ToString(x.Tag)=="vision.camera.viewer"),
                        "Content右键未发现图元/流程/已注册专业Viewer三个真实Editor");
                    MenuItem flow=candidates.Single(x=>Convert.ToString(x.Tag)=="mol.flow");
                    flow.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert(window.GetStudioResourceFlow(content.Id)!=null&&
                        window.GetStudioResourceDocumentIdentity(window.ActiveStudioDocumentId)
                            .EditorId=="mol.flow",
                        "资源右键流程入口没有打开真实MOL Content画布");
                    MenuItem visual=candidates.Single(x=>Convert.ToString(x.Tag)=="mem.visual");
                    visual.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert(window.GetStudioResourceScene(content.Id)!=null&&
                        window.GetStudioResourceDocumentIdentity(window.ActiveStudioDocumentId)
                            .EditorId=="mem.visual"&&session.Revision==editorMenuRevision,
                        "资源右键图元入口未打开真实MEM Editor或修改了工程Revision");
                    // 同一右键菜单晚注册后必须能重新发现专业工厂。
                    window.RegisterStudioResourceViewer("automation.plc.viewer",
                        "PLC专业观察",identity=>new TextBlock {Text="PLC "+identity.ResourceId},
                        false,"core.content");
                    menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                    MenuItem plc=menu.Items.OfType<MenuItem>()
                        .Single(x=>Convert.ToString(x.Tag)=="automation.plc.viewer");
                    plc.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    string plcKey=window.ActiveStudioDocumentId;
                    Assert(window.GetStudioResourceDocumentIdentity(plcKey).ResourceId==content.Id&&
                        window.GetStudioResourceDocumentIdentity(plcKey).EditorId=="automation.plc.viewer",
                        "晚注册PLC专业Viewer未通过真实资源菜单打开");
                    // 删除工程资源后，过期菜单不允许再次进入旧资源和旧Viewer。
                    session.Undo().RequireValue();
                    Assert(session.Find(content.Id)==null,"Undo没有删除最后创建的资源");
                    menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                    Assert(menu.Items.OfType<MenuItem>().All(x=>!x.IsEnabled)&&
                        window.GetStudioResourceDocumentIdentity(plcKey)==null,
                        "删除资源的旧右键菜单仍可复用过期的DocumentKey");
                });
                CheckStep("S2/P1 四工业MPM类型与Studio属性编辑批量Undo保存重开", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    session.RegisterTypes(new StudioP1IndustrialTypes());
                    MEMContent camera=session.AddTypedContent(session.Project,
                        "相机A","studio.p1.camera");
                    MEMContent plc=session.AddTypedContent(session.Project,
                        "PLC-A","studio.p1.plc");
                    MEMContent recipe=session.AddTypedContent(session.Project,
                        "产品配方A","studio.p1.recipe");
                    MEMSymbol motor=session.AddTypedSymbol("电机Symbol","studio.p1.motor");
                    MEMPage page=session.AddPage("P1工业站点");
                    MRIResourceReference first=session.PlaceSymbol(page.RootContent,motor,"电机一");
                    MRIResourceReference second=session.PlaceSymbol(page.RootContent,motor,"电机二");
                    window.RegisterStudioResourceViewer("vision.p1.camera","工业相机",
                        identity=>new TextBlock {Text="相机工作台 · "+identity.ResourceId},
                        true,"studio.p1.camera");
                    Assert(window.GetStudioResourceEditors(camera.Id).First().EditorId==
                        "vision.p1.camera"&&
                        !window.GetStudioResourceEditors(plc.Id).Any(x=>
                            x.EditorId=="vision.p1.camera"),
                        "专业Editor未依据自定义MPM资源TypeId正确匹配");
                    string cameraTab=window.OpenStudioResourceEditor(camera.Id);
                    Assert(window.GetStudioResourceDocumentIdentity(cameraTab).ResourceId==
                        camera.Id,"工业相机资源未通过Studio专业工厂打开");
                    vm.SelectedResource=camera;
                    StudioPropertyItem gain=vm.Properties.Single(x=>x.Member=="Gain");
                    Assert(gain.Schema.Contains("0")&&gain.Schema.Contains("10")&&
                        gain.Mode=="常量"&&!gain.IsReadOnly,
                        "相机数值范围和MPM来源未投影至Studio");
                    int beforeGain=session.Revision;
                    gain.Text="5.5";vm.ApplyPropertyCommand.Execute(gain);
                    Assert(session.Revision==beforeGain+1&&
                        ((IMPMDefinition)camera).Properties.Single(x=>x.MemberId=="Gain")
                            .Binding.Value.AsNumber()==5.5,
                        "工业相机真实MPM值未通过Studio应用");
                    gain=vm.Properties.Single(x=>x.Member=="Gain");
                    gain.Text="99";
                    int rejectedGain=session.Revision;
                    vm.ApplyPropertyCommand.Execute(gain);
                    Assert(session.Revision==rejectedGain&&
                        ((IMPMDefinition)camera).Properties.Single(x=>x.MemberId=="Gain")
                            .Binding.Value.AsNumber()==5.5,
                        "非法相机增益导致部分工程提交");
                    vm.SelectedResource=plc;
                    StudioPropertyItem port=vm.Properties.Single(x=>x.Member=="ControllerPort");
                    port.Text="1502";int beforePlc=session.Revision;
                    vm.ApplyPropertyCommand.Execute(port);
                    Assert(session.Revision==beforePlc+1&&
                        ((IMPMDefinition)plc).Properties.Single(x=>
                            x.MemberId=="ControllerPort").Binding.Value.AsInteger()==1502,
                        "PLC配置未由MPM Schema落地或错用了MHS配置仓库");
                    StudioPropertyItem serial=vm.Properties.Single(x=>x.Member=="Serial");
                    int beforeSerial=session.Revision;
                    serial.Text="禁止修改";vm.ApplyPropertyCommand.Execute(serial);
                    Assert(session.Revision==beforeSerial&&
                        ((IMPMDefinition)plc).Properties.Single(x=>x.MemberId=="Serial")
                            .Binding.Value.AsString()=="PLC-A",
                        "MPM只读属性被Studio非法修改");
                    vm.SelectedResource=recipe;
                    StudioPropertyItem product=vm.Properties.Single(x=>x.Member=="ProductCode");
                    product.Text="XRay-2026";vm.ApplyPropertyCommand.Execute(product);
                    Assert(((IMPMDefinition)recipe).Properties.Single(x=>
                        x.MemberId=="ProductCode").Binding.Value.AsString()=="XRay-2026",
                        "产品配方未真实写入工程定义");
                    // 两电机实例不同覆盖；Studio Mixed不可回退成最后实例。
                    session.SetOverride(first,"Speed",
                        MPMBinding.Literal(MPMValue.Number(900)));
                    vm.SetSelection(new[]{first.Id,second.Id});
                    StudioPropertyItem mixed=vm.Properties.Single(x=>x.Member=="Speed");
                    Assert(mixed.SelectionCount==2&&mixed.IsMixedValue&&
                        mixed.BatchOwnerIds.Length==2,
                        "电机实例选区未正确显示Mixed/类型一致性");
                    mixed.Text="1600";
                    int beforeBatch=session.Revision;
                    vm.ApplyPropertyCommand.Execute(mixed);
                    Assert(session.Revision==beforeBatch+1&&
                        session.ResolveParameter(first,"Speed").Value.AsNumber()==1600&&
                        session.ResolveParameter(second,"Speed").Value.AsNumber()==1600,
                        "电机多实例没有以单一MEM事务同步覆盖");
                    session.Undo().RequireValue();
                    Assert(session.ResolveParameter(first,"Speed").Value.AsNumber()==900&&
                        session.ResolveParameter(second,"Speed").Value.AsNumber()==1200,
                        "电机批量提交需要超过一次Undo或破坏继承默认");
                    session.Redo().RequireValue();
                    vm.SetSelection(new[]{first.Id,second.Id});
                    StudioPropertyItem bad=vm.Properties.Single(x=>x.Member=="Speed");
                    bad.Text="5000";
                    int beforeInvalidBatch=session.Revision;
                    vm.ApplyPropertyCommand.Execute(bad);
                    Assert(session.Revision==beforeInvalidBatch&&
                        session.ResolveParameter(first,"Speed").Value.AsNumber()==1600&&
                        session.ResolveParameter(second,"Speed").Value.AsNumber()==1600,
                        "非法电机批量输入造成部分覆盖/错误Revision");
                    string file=Path.Combine(Path.GetTempPath(),
                        "cxstudio-p1-industrial-"+Guid.NewGuid().ToString("N")+".cxproj");
                    try
                    {
                        session.SaveAs(file);
                        using(MEMEngineeringSession reopened=MEMEngineering.Open(file))
                        {
                            Assert(reopened.FindType("studio.p1.camera")!=null&&
                                reopened.FindType("studio.p1.plc")!=null&&
                                reopened.FindType("studio.p1.recipe")!=null&&
                                reopened.FindType("studio.p1.motor")!=null&&
                                reopened.ResolveParameter(reopened.FindReference(first.Id),"Speed")
                                    .Value.AsNumber()==1600&&
                                reopened.ResolveParameter(reopened.FindReference(second.Id),"Speed")
                                    .Value.AsNumber()==1600,
                                "真实工程保存重开丢失工业类型Schema或实例覆盖");
                        }
                    }
                    finally {if(File.Exists(file))File.Delete(file);}
                    window.TryCloseStudioDocument(cameraTab);
                });
                CheckStep("S2/P1 资源Editor候选直接进入MHS Scope及未保存草稿保护", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent first=session.AddContent(session.Project,"P1相机参数资源一");
                    MEMContent second=session.AddContent(session.Project,"P1相机参数资源二");
                    window.TryCloseStudioDocument("doc.config",true);
                    var principal=new FCMConfigPrincipal("p1-editor-scope",
                        new[]{"p1-editor-scope-role"});
                    var policy=new FCMConfigRolePolicy();
                    foreach(MEMContent owner in new[]{first,second})
                        foreach(FCMConfigAccessAction action in new[]{
                            FCMConfigAccessAction.Read,FCMConfigAccessAction.ReadSensitive,
                            FCMConfigAccessAction.Edit,FCMConfigAccessAction.Commit,
                            FCMConfigAccessAction.Audit,FCMConfigAccessAction.Restore})
                            policy.Grant("p1-editor-scope-role",action,"Automation.Device",
                                "resource",owner.Id.ToString("N"));
                    window.SetStudioConfigurationAuthorization(principal,policy);
                    Assert(window.GetStudioResourceEditors(first.Id).Any(x=>
                        x.EditorId=="mhs.config"),
                        "已注册Automation配置未成为真实core.content的Editor候选");
                    int before=session.Revision;
                    string config=window.OpenStudioResourceEditor(first.Id,"mhs.config");
                    MainWindow.StudioCommandContext context=window.GetStudioCommandContext();
                    Assert(config=="doc.config"&&context.ConfigScopeKind=="resource"&&
                        context.ConfigScopeId==first.Id.ToString("N")&&
                        context.ResourceId==first.Id&&context.ResourceTypeId==first.TypeId&&
                        session.Revision==before,
                        "资源Editor打开错误MHS Scope或修改MEM工程Revision");
                    AssertThrows(()=>window.OpenStudioResourceEditor(first.Id,
                        "mhs.config",Guid.NewGuid()));
                    LayoutDocument document=window.StudioDock.Layout.Descendents()
                        .OfType<LayoutDocument>().Single(x=>x.ContentId=="doc.config");
                    Func<DependencyObject,MPWConfigEditorControl<CxAutomationDeviceOptions>>
                        findEditor=null;
                    findEditor=node=>node as MPWConfigEditorControl<CxAutomationDeviceOptions> ??
                        System.Windows.LogicalTreeHelper.GetChildren(node)
                            .OfType<DependencyObject>()
                            .Select(child=>findEditor(child)).FirstOrDefault(x=>x!=null);
                    MPWConfigEditorControl<CxAutomationDeviceOptions> editor=
                        findEditor((DependencyObject)document.Content);
                    Assert(editor!=null,"未能加载已授权的真实Automation Schema Editor");
                    var field=typeof(MPWConfigEditorControl<CxAutomationDeviceOptions>)
                        .GetField("_editor",System.Reflection.BindingFlags.NonPublic|
                            System.Reflection.BindingFlags.Instance);
                    var model=field.GetValue(editor) as MHSConfigEditorModel<CxAutomationDeviceOptions>;
                    int port=model.Draft.ControllerPort==1502?1503:1502;
                    model.SetValue(x=>x.ControllerPort,port);
                    Assert(editor.HasUnsavedDraft,"配置字段修改后未形成MHS草稿");
                    AssertThrows(()=>window.OpenStudioResourceEditor(second.Id,"mhs.config"));
                    Assert(window.GetStudioCommandContext().ConfigScopeId==first.Id.ToString("N")&&
                        editor.HasUnsavedDraft&&session.Revision==before,
                        "未保存MHS草稿被跨资源Editor切换悄悄丢弃");
                    model.Undo();
                    Assert(!editor.HasUnsavedDraft,"配置草稿Undo后仍被错误标记Dirty");
                    window.OpenStudioResourceEditor(second.Id,"mhs.config");
                    Assert(window.GetStudioCommandContext().ConfigScopeId==second.Id.ToString("N")&&
                        window.GetStudioCommandContext().ResourceId==second.Id&&
                        window.GetStudioCommandContext().ResourceTypeId==second.TypeId&&
                        session.Revision==before,
                        "切换资源配置Scope未重新选择正确的MHS身份");
                    window.TryCloseStudioDocument("doc.config",true);
                });
                CheckStep("S2/P1 专业Viewer跨Tab同步MPM检视及旧属性行拒绝", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMContent activeA=session.AddContent(session.Project,"P1 Viewer选区A");
                    MEMContent activeB=session.AddContent(session.Project,"P1 Viewer选区B");
                    var descriptor=new MPMPropertyDescriptor("Gain",MPMValueKind.Number,
                        true,true,MPMBinding.Literal(MPMValue.Number(1)),0,10);
                    session.DefineProperty(activeA,descriptor);
                    session.DefineProperty(activeB,descriptor);
                    string tabA=window.OpenStudioResourceEditor(activeA.Id,
                        "vision.camera.commands");
                    Assert(vm.SelectedResource!=null&&vm.SelectedResource.Id==activeA.Id&&
                        vm.Properties.Any(x=>x.Member=="Gain"),
                        "打开专业Viewer未将MPM Inspector投影至真实MEM资源A");
                    StudioPropertyItem stale=vm.Properties.Single(x=>x.Member=="Gain");
                    string tabB=window.OpenStudioResourceEditor(activeB.Id,
                        "vision.camera.commands");
                    Assert(vm.SelectedResource!=null&&vm.SelectedResource.Id==activeB.Id&&
                        vm.Properties.Single(x=>x.Member=="Gain").OwnerId==activeB.Id,
                        "切换专业Viewer未同步资源B或沿用A的Schema行");
                    int before=session.Revision;
                    stale.Text="7";
                    vm.ApplyPropertyCommand.Execute(stale);
                    Assert(session.Revision==before&&
                        ((IMPMDefinition)activeA).Properties.Single(x=>x.MemberId=="Gain")
                            .Binding.Value.AsNumber()==1&&
                        ((IMPMDefinition)activeB).Properties.Single(x=>x.MemberId=="Gain")
                            .Binding.Value.AsNumber()==1,
                        "旧Viewer属性行可在B的活动Tab跨资源写入A");
                    StudioPropertyItem current=vm.Properties.Single(x=>x.Member=="Gain");
                    current.Text="6";vm.ApplyPropertyCommand.Execute(current);
                    Assert(session.Revision==before+1&&
                        ((IMPMDefinition)activeB).Properties.Single(x=>x.MemberId=="Gain")
                            .Binding.Value.AsNumber()==6&&
                        ((IMPMDefinition)activeA).Properties.Single(x=>x.MemberId=="Gain")
                            .Binding.Value.AsNumber()==1,
                        "新活动Viewer属性编辑未提交B或误写共享资源A");
                    session.Undo().RequireValue();
                    Assert(((IMPMDefinition)activeB).Properties.Single(x=>x.MemberId=="Gain")
                        .Binding.Value.AsNumber()==1,
                        "Viewer属性操作没有落在统一MEM Undo历史");
                    window.TryCloseStudioDocument(tabA);
                    window.TryCloseStudioDocument(tabB);
                });
                CheckStep("S1界面设计00/01主页工程概览与真实工作区入口", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    window.OpenStudioDocument("doc.home");
                    int before=vm.ProjectSession.Revision;
                    Assert(window.FindName("StudioHomeWorkArea") is StackPanel&&
                        window.FindName("StudioHomeWorkflows") is WrapPanel&&
                        window.FindName("StudioHeaderProjectName") is TextBlock&&
                        ((TextBlock)window.FindName("StudioHeaderProjectName")).Text==
                            vm.ProjectSession.Project.Name,
                        "主页工作区卡片/动态工程标题缺失或仍显示演示名称");
                    var homeButtons=new[]{
                        Tuple.Create("StudioHomeHmiOpen","doc.visual"),
                        Tuple.Create("StudioHomeFlowOpen","doc.flow"),
                        Tuple.Create("StudioHomeConfigOpen","doc.config")};
                    foreach(var entry in homeButtons)
                    {
                        window.OpenStudioDocument("doc.home");
                        Button button=window.FindName(entry.Item1) as Button;
                        Assert(button!=null&&Convert.ToString(button.Tag)==entry.Item2,
                            "主页入口无法定位真实Studio文档："+entry.Item1);
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert(window.ActiveStudioDocumentId==entry.Item2&&
                            vm.ProjectSession.Revision==before,
                            "主页打开真实工作区时没有激活目标Tab或修改了MEM工程");
                    }
                    window.OpenStudioDocument("doc.home");
                    TextBox globalSearch=window.FindName("StudioGlobalResourceSearch") as TextBox;
                    Assert(globalSearch!=null,"顶部缺少真实资源搜索控件");
                    globalSearch.Text="P0";
                    Assert(window.StudioResourceSearch.Text=="P0"&&
                        vm.ProjectSession.Revision==before,
                        "顶部搜索未路由到原资源树筛选或错误修改MEM工程");
                    globalSearch.Text="";
                    Assert(window.StudioResourceSearch.Text=="",
                        "清除全局搜索未恢复左侧资源树");
                    vm.ProjectSession.Rename(vm.ProjectSession.Project,"P1 Studio新工程标题");
                    Assert(vm.StudioProjectTitle=="P1 Studio新工程标题"&&
                        ((TextBlock)window.FindName("StudioHeaderProjectName")).Text==
                            "P1 Studio新工程标题",
                        "实际MEM工程重命名后顶部标题未实时更新");
                });
                CheckStep("S1界面设计真实WPF浅深主题和中央工作区截图", () =>
                {
                    int sourceRevision=window.StudioEngineeringRevision;
                    string dir=Path.GetDirectoryName(report);
                    string[] ids={"doc.home","doc.visual","doc.flow"};
                    string[] names={"Studio-01-Home-Light.png",
                        "Studio-02-HMI-Light.png","Studio-03-Flow-Light.png"};
                    window.ApplyStudioTheme(false);
                    for(int i=0;i<ids.Length;i++)
                    {
                        window.OpenStudioDocument(ids[i]);
                        CaptureStudioWpf(window,Path.Combine(dir,names[i]));
                    }
                    window.OpenStudioDocument("doc.home");
                    window.ApplyStudioTheme(true);
                    CaptureStudioWpf(window,Path.Combine(dir,"Studio-04-Home-Dark.png"));
                    window.ApplyStudioTheme(false);
                    Assert(window.StudioEngineeringRevision==sourceRevision,
                        "WPF界面截图或主题切换错误地修改了工程Revision");
                });
                CheckStep("R08 Studio执行组参数编辑通过活动Content而非旧Page", () =>
                {
                    StudioViewModel vm=window.DataContext as StudioViewModel;
                    MEMEngineeringSession session=vm.ProjectSession;
                    MEMPage oldPage=session.AddPage("R08执行组旧页面");
                    MEMContent content=session.AddContent(session.Project,"R08独立Stage流程");
                    string tab=window.OpenStudioResourceDocument(content.Id,true);
                    Guid node=Guid.Empty;
                    MOCFlowBuilder.Edit(session,content,b=>node=b.Step("AI"));
                    MOLFlowCanvas canvas=window.GetStudioResourceFlow(content.Id);
                    canvas.SetSelection(new[]{node});
                    vm.ActivePage=oldPage;
                    Expander expander=window.FindName("StudioFlowAdvancedTools") as Expander;
                    Assert(expander!=null&&window.FlowApplyExecutionGroup!=null,
                        "Studio没有真实Stage组输入和点击按钮");
                    expander.IsExpanded=true;
                    window.FlowInspectorExecutionGroup.Text="vision-stage";
                    int before=session.Revision;
                    window.FlowApplyExecutionGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    MOLWiringDefinition result=MOLWiringCodec.Get(
                        session.GetSnapshot().Find(content.Id));
                    Assert(session.Revision==before+1&&
                        result.Policies.ContainsKey(node)&&
                        result.Policies[node].ExecutionGroup=="vision-stage"&&
                        window.ActiveStudioDocumentId==tab,
                        "执行组输入未写入当前Flow原工程事务");
                    session.Undo().RequireValue();
                    Assert(!MOLWiringCodec.Get(session.GetSnapshot().Find(content.Id))
                        .Policies.ContainsKey(node),"Stage组撤销没有恢复工程定义");
                    session.Redo().RequireValue();
                    Assert(MOLWiringCodec.Get(session.GetSnapshot().Find(content.Id))
                        .Policies[node].ExecutionGroup=="vision-stage",
                        "Stage组重做未恢复身份");
                    before=session.Revision;
                    window.FlowInspectorExecutionGroup.Text=new string('X',129);
                    window.FlowApplyExecutionGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(session.Revision==before&&
                        MOLWiringCodec.Get(session.GetSnapshot().Find(content.Id))
                            .Policies[node].ExecutionGroup=="vision-stage",
                        "非法Stage组ID污染原工程Revision");
                    window.FlowInspectorExecutionGroup.Text="";
                    window.FlowApplyExecutionGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(!MOLWiringCodec.Get(session.GetSnapshot().Find(content.Id))
                        .Policies.ContainsKey(node),"清空Stage组ID未移除缺省策略");
                });
                CheckStep("S1圆角Ribbon真实矢量及HMI/MOL工具折叠", () =>
                {
                    int rev=window.StudioEngineeringRevision;
                    window.OpenStudioDocument("doc.visual");
                    Expander hmi=window.FindName("StudioVisualAdvancedTools") as Expander;
                    Assert(hmi!=null,"HMI高级工具Expander未创建");
                    hmi.IsExpanded=false;
                    Button ribbon=window.StudioRibbonActions.Children.OfType<Button>()
                        .FirstOrDefault(b=>Convert.ToString(b.Content)=="矩形");
                    Assert(ribbon!=null,"HMI矩形Ribbon真实按钮缺失");
                    ribbon.ApplyTemplate();
                    Border tile=ribbon.Template.FindName("RibbonTile",ribbon) as Border;
                    Assert(tile!=null&&tile.CornerRadius.TopLeft>=6,
                        "图标Ribbon仍是方形控件模板");
                    Func<DependencyObject,bool> hasVector=null;
                    hasVector=node=>{
                        if(node is System.Windows.Shapes.Path)return true;
                        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++)
                            if(hasVector(System.Windows.Media.VisualTreeHelper.GetChild(node,i)))
                                return true;
                        return false;
                    };
                    window.UpdateLayout();
                    Assert(hasVector(ribbon),
                        "缺少与系统图标字体无关的WPF Path矢量图标");
                    hmi.IsExpanded=true;
                    Assert(window.FindName("PathCommandIndex") is TextBox,
                        "折叠后无法打开原有路径编辑能力");
                    hmi.IsExpanded=false;
                    window.OpenStudioDocument("doc.flow");
                    Expander flow=window.FindName("StudioFlowAdvancedTools") as Expander;
                    Assert(flow!=null,"MOL高级工具Expander未创建");
                    flow.IsExpanded=true;
                    Assert(window.FindName("FlowExistingLinks") is ComboBox&&
                        window.FindName("FlowInspectorValue") is TextBox,
                        "MOL折叠布局破坏Port与参数原有编辑能力");
                    flow.IsExpanded=false;
                    Assert(window.StudioEngineeringRevision==rev,
                        "Ribbon模板/高级工具展开折叠修改了工程Revision");
                });
                CheckStep("S1视觉一体化圆角输入与Ribbon分组及活动菜单", () =>
                {
                    int before=window.StudioEngineeringRevision;
                    TextBox search=window.StudioGlobalResourceSearch;
                    search.ApplyTemplate();
                    Border inputBorder=search.Template.FindName("MPWInputSurface",search) as Border;
                    ScrollViewer contentHost=search.Template.FindName("PART_ContentHost",search)
                        as ScrollViewer;
                    Assert(inputBorder!=null&&inputBorder.CornerRadius.TopLeft>=6&&
                        contentHost!=null,"圆角输入框未保留真实TextBox编辑部件");
                    search.Text="中文ABC";
                    search.Select(2,3);
                    Assert(search.SelectedText=="ABC"&&
                        window.StudioResourceSearch.Text=="中文ABC",
                        "圆角输入模板丢失文本选择或旧MEM树过滤");
                    search.Text="";
                    window.OpenStudioDocument("doc.visual");
                    Button selectedMenu=window.StudioDynamicMenus.Children.OfType<Button>()
                        .FirstOrDefault(b=>Convert.ToString(b.Content)=="绘图");
                    Assert(selectedMenu!=null&&selectedMenu.BorderThickness.Bottom>=2,
                        "绘图动态菜单缺选中底线");
                    Button action=window.StudioRibbonActions.Children.OfType<Button>()
                        .FirstOrDefault(b=>Convert.ToString(b.Content)=="矩形");
                    Assert(action!=null,"圆角Ribbon更改后原矩形Owner命令不可发现");
                    action.ApplyTemplate();
                    Border iconBox=action.Template.FindName("StudioRibbonIconBox",action) as Border;
                    Assert(iconBox!=null&&iconBox.CornerRadius.TopLeft>=6&&
                        window.StudioRibbonActions.Children.OfType<Border>().Count()>=2&&
                        window.StudioRibbonGroupCaptions!=null,
                        "Ribbon图标框或基础/路径/文本分组竖线缺失");
                    window.OpenStudioDocument("doc.flow");
                    Assert(window.StudioDynamicMenus.Children.OfType<Button>()
                        .Any(b=>Convert.ToString(b.Content)=="节点"),
                        "流程工作台菜单切换后无法发现节点命令");
                    window.ApplyStudioTheme(true);
                    Assert(window.StudioDarkTheme&&
                        ((SolidColorBrush)Application.Current.Resources["StudioTextBrush"])
                            .Color.R>150,
                        "深色主题文字没有足够的基础亮度");
                    window.ApplyStudioTheme(false);
                    Assert(window.StudioEngineeringRevision==before,
                        "纯视觉/搜索/主题操作修改了MEM工程Revision");
                });
                CheckStep("B-CT3D Studio真实MEM Case四视图、ROI编辑与保存重开", () =>
                {
                    string folder=Path.Combine(Path.GetTempPath(),
                        "CxVisual-Studio-CT3D-"+Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    int originalRevision=window.StudioEngineeringRevision;
                    try
                    {
                        const int n=12;
                        byte[] bytes=new byte[n*n*n*2];
                        for(int z=5;z<=6;z++)for(int y=5;y<=6;y++)for(int x=5;x<=6;x++)
                        {
                            int index=(x+y*n+z*n*n)*2;
                            bytes[index]=0xa4;bytes[index+1]=0x06;
                        }
                        string raw=Path.Combine(folder,"source.raw");
                        File.WriteAllBytes(raw,bytes);
                        string project=Path.Combine(folder,"analysis.cxproj");
                        var info=new FVORawVolumeInfo(n,n,n,1,1,1,
                            FVORawVoxelFormat.UInt16,"source.raw");
                        var frame=new FVOVolumeSpatialFrame(1,1,1,20,30,40,
                            new double[]{1,0,0,0,1,0,0,0,1},true);
                        var source=CT3DAssetReference.Capture(folder,"source.raw",
                            info,frame,1,CancellationToken.None);
                        var baseline=CT3DAnalysisCase.Analyze(folder,source,1000,100,
                            CT3DAnalysisCase.SelectTopVoxelFace,CancellationToken.None);
                        using(var engineering=MEMEngineering.Create("StudioCT3D"))
                        {
                            MEMContent owner=engineering.AddPage("真实三维分析").RootContent;
                            CT3DAnalysisPersistence.Commit(engineering,owner,
                                engineering.Revision,folder,baseline,CancellationToken.None);
                            engineering.SaveAs(project);
                        }
                        window.OpenStudioDocument("doc.ct3d");
                        var tab=window.StudioDock.Layout.Descendents().OfType<LayoutDocument>()
                            .Single(x=>x.ContentId=="doc.ct3d");
                        var workspace=tab.Content as StudioCT3DDocument;
                        Assert(workspace!=null&&workspace.CaseCount==0,
                            "Studio CT3D文档必须初始不加载任何演示工程");
                        workspace.OpenProject(project);
                        Assert(workspace.CaseCount==1&&workspace.CurrentAnalysis!=null&&
                            workspace.CurrentAnalysis.Kind==CT3DAnalysisKind.MeshPlane,
                            "真实CT3D MEM工程和原始RAW未进入Studio工作台");
                        var viewport=workspace.Workbench.GetViewport(FVOSlicePlane.XY);
                        var physical=viewport.SelectRectangleRoi(5,5,6,6);
                        Assert(physical.Unit=="mm"&&
                            Math.Abs(physical.TopLeft.Z-46)<1e-9,
                            "Studio四视图未使用同一标定Frame的二维物理ROI");
                        Guid hiddenMeasurement=workspace.Scene3D.Snapshot().Single(x=>
                            x.Kind==MVI3DSceneKind.Measurement).Id;
                        workspace.Scene3D.SetVisibility(hiddenMeasurement,false);
                        Assert(workspace.Scene3D.SelectedId==null,
                            "隐藏专业测量后必须清除旧场景选择，禁止继续选中不可见对象");
                        using(var cancelled=new CancellationTokenSource())
                        {
                            cancelled.Cancel();
                            bool rejected=false;
                            try
                            {
                                AwaitStudioTask(workspace.RecomputeSelectedSliceRoiAsync(
                                    .49,.51,cancelled.Token));
                            }
                            catch(OperationCanceledException){rejected=true;}
                            Assert(rejected&&!workspace.IsAnalyzing&&
                                !workspace.HasPendingAnalysis,
                                "Studio取消后台分析仍错误发布了过期Feature");
                        }
                        CT3DAnalysisCase preview=AwaitStudioTask(
                            workspace.RecomputeSelectedSliceRoiAsync(
                                .49,.51,CancellationToken.None));
                        Assert(preview.Plane.SampleCount==9&&workspace.HasPendingAnalysis&&
                            !workspace.IsAnalyzing,
                            "Studio后台原值拟合没有安全回到WPF并形成待保存专业结果");
                        workspace.DiscardPendingAnalysis();
                        Assert(!workspace.HasPendingAnalysis&&
                            workspace.CurrentAnalysis.Plane.ResultId==baseline.Plane.ResultId,
                            "放弃未提交的Studio分析没有恢复原Feature和Scene");
                        var hiddenAfterDiscard=workspace.Scene3D.Snapshot().Single(x=>
                            x.Kind==MVI3DSceneKind.Measurement);
                        Assert(!hiddenAfterDiscard.Visible&&workspace.Scene3D.SelectedId==null,
                            "Studio后台ROI复算/放弃不能重新选择被隐藏的Measurement");
                        workspace.Scene3D.SetVisibility(hiddenAfterDiscard.Id,true);
                        workspace.Scene3D.Select(hiddenAfterDiscard.Id);
                        CT3DAnalysisCase edited=workspace.RecomputeSelectedSliceRoi(.49,.51);
                        Assert(workspace.HasPendingAnalysis&&edited.Plane.SampleCount==9&&
                            edited.Plane.ResultId!=baseline.Plane.ResultId,
                            "Studio选区没有形成可持久化的新专业Feature");
                        Assert(!window.TryCloseStudioDocument("doc.ct3d")&&
                            window.GetStudioContentIds().Contains("doc.ct3d"),
                            "Studio有未保存专业结果时不能直接关闭并丢失分析");
                        workspace.SavePendingResult();
                        Assert(!workspace.HasPendingAnalysis,
                            "专业结果提交成功后仍错误保留未保存状态");
                        Guid editedId=edited.Plane.ResultId;
                        workspace.OpenProject(project);
                        Assert(workspace.CurrentAnalysis.Plane.ResultId==editedId&&
                            workspace.CurrentAnalysis.GetFeatureMeasurements().Count==2,
                            "Studio重新打开不能恢复已保存的Feature及Measurement");
                        using(var engineering=MEMEngineering.Open(project))
                        {
                            var owner=engineering.Project.DescendantsAndSelf().OfType<MEMContent>()
                                .Single(x=>x.Aspects.Any(a=>
                                    a.ProviderId==CT3DAnalysisPersistence.ProviderId));
                            var current=CT3DAnalysisPersistence.Open(engineering,
                                engineering.GetAddress(owner),folder,CancellationToken.None);
                            Assert(current.Plane.ResultId==editedId&&
                                owner.Aspects.Any(a=>a.ProviderId==
                                    MVI3DSceneLayoutPersistence.ProviderId),
                                "Studio保存必须同时提交CT3D Data及Scene Visual Aspect");
                        }
                        Assert(window.StudioEngineeringRevision==originalRevision,
                            "打开外部三维工程文档不应写入Studio当前其它工程的Revision");
                        window.CloseStudioDocument("doc.ct3d");
                        Assert(!window.GetStudioContentIds().Contains("doc.ct3d"),
                            "Studio关闭三维工程文档没有释放Dock会话");
                    }
                    finally
                    {
                        window.TryCloseStudioDocument("doc.ct3d",true);
                        try{Directory.Delete(folder,true);}catch(IOException){}
                    }
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

        /// <summary>使用显示中的真实WPF Visual导出讨论稿，非设计图替代或人工DPI验收。</summary>
        private static void CaptureStudioWpf(Window window,string path)
        {
            window.UpdateLayout();
            double w=window.ActualWidth,h=window.ActualHeight;
            if(w<800||h<600||double.IsNaN(w)||double.IsNaN(h))
                throw new InvalidOperationException("Studio实际窗口尺寸不适合截图");
            RenderTargetBitmap bitmap=new RenderTargetBitmap(
                (int)Math.Ceiling(w),(int)Math.Ceiling(h),96,96,PixelFormats.Pbgra32);
            bitmap.Render(window);
            PngBitmapEncoder encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(FileStream stream=File.Create(path))encoder.Save(stream);
            if(new FileInfo(path).Length<4096)
                throw new InvalidOperationException("Studio WPF截图为空或不完整："+path);
        }

        /// <summary>专业SDK示例：四类工业资源只声明MPM语义类型，仍由MEM保存。</summary>
        private sealed class StudioP1IndustrialTypes : IMPMTypeProvider
        {
            public IEnumerable<MPMTypeDescriptor> DescribeTypes()
            {
                yield return new MPMTypeDescriptor("studio.p1.camera",1,MEMNodeKind.Content,
                    new[]{
                        new MPMPropertyDescriptor("Gain",MPMValueKind.Number,true,true,
                            MPMBinding.Literal(MPMValue.Number(2)),0,10),
                        new MPMPropertyDescriptor("Triggered",MPMValueKind.Boolean,false,true,
                            MPMBinding.Literal(MPMValue.Boolean(true)))
                    });
                yield return new MPMTypeDescriptor("studio.p1.plc",1,MEMNodeKind.Content,
                    new[]{
                        new MPMPropertyDescriptor("ControllerPort",MPMValueKind.Integer,true,true,
                            MPMBinding.Literal(MPMValue.Integer(502)),1,65535),
                        new MPMPropertyDescriptor("Serial",MPMValueKind.String,true,true,
                            MPMBinding.Literal(MPMValue.String("PLC-A")),
                            isReadOnly:true)
                    });
                yield return new MPMTypeDescriptor("studio.p1.recipe",1,MEMNodeKind.Content,
                    new[]{
                        new MPMPropertyDescriptor("ProductCode",MPMValueKind.String,true,true,
                            MPMBinding.Literal(MPMValue.String("DefaultRecipe"))),
                        new MPMPropertyDescriptor("Confidence",MPMValueKind.Number,true,true,
                            MPMBinding.Literal(MPMValue.Number(0.75)),0,1)
                    });
                yield return new MPMTypeDescriptor("studio.p1.motor",1,MEMNodeKind.Symbol,
                    new[]{
                        new MPMPropertyDescriptor("Speed",MPMValueKind.Number,true,true,
                            MPMBinding.Literal(MPMValue.Number(1200)),0,3000),
                        new MPMPropertyDescriptor("Enabled",MPMValueKind.Boolean,false,true,
                            MPMBinding.Literal(MPMValue.Boolean(false)))
                    });
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

        private sealed class P04CpuReleaseTestPolicy : IMRKPlanPublicationPolicy
        {
            public bool Authorized;
            public bool Safe;
            public bool Authorize(string principal,MRKPlanPublishAction action,
                MRKExecutionPlan plan,long revision)
            {return Authorized&&principal=="studio-cpu-simulation";}
            public bool IsSafeToActivate(MRKExecutionPlan plan,long revision)
            {return Safe;}
        }
        // Shared by the full S1 shell run and the independent ModuleCenter
        // CPU acceptance job. This is identical real MHS/MEM/MOC/MRK test work.
        internal static bool RunCpuVisionOnly(string report)
        {
            Results.Clear();
            bool ok=true;
            try { CheckStep("S1-03D RAW真实文件→MEM/MOC/MRK→缺陷结果及Trace闭环",
                VerifyCpuVisionFileClosure);
                CheckStep("R1-01 保存工程→带Stride RAW指纹重绑→MRK复算/显示/导出",
                    VerifyStudioSavedRoiRebind); }
            catch(Exception ex) { Results.Add("FAIL: "+ex);ok=false; }
            File.WriteAllLines(report,Results);
            return ok;
        }
        /// <summary>
        /// R1-01 Studio复用同一MEM配方和真实FIM RAW读入器；不运行另一个检测
        /// 工程来冒充“重绑”。测试保留行填充、配置版本、RunId及可视结果。
        /// </summary>
        private static void VerifyStudioSavedRoiRebind()
        {
            string folder=Path.Combine(Path.GetTempPath(),
                "cx-studio-roi-rebind-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string rawPath=Path.Combine(folder,"input-4x4-stride10.raw");
            string wrongPath=Path.Combine(folder,"wrong-source.raw");
            string projectPath=Path.Combine(folder,"rebind.cxproj");
            string csvPath=Path.Combine(folder,"evidence.csv");
            string exportRaw=Path.Combine(folder,"export.raw");
            try
            {
                // Gray16 4×4，Stride=10；每行8字节像素+2字节填充。
                byte[] bytes=new byte[40];
                for(int y=0;y<4;y++)
                {
                    for(int x=0;x<4;x++)bytes[y*10+x*2]=210;
                    bytes[y*10+8]=77;
                    bytes[y*10+9]=88;
                }
                var image=new FIMImageBuffer(4,4,FIMPixelFormat.Gray16,bytes,10);
                FIMRawImageWriter.SaveFile(rawPath,image,preserveStride:true);
                var roi=new MVIImageOverlayItem(
                    MVIImageOverlayKind.PolygonRoi,MVIImageOverlayLayer.Roi,
                    new FGERect2D(0,0,3.6,3.6),
                    new[]{new FGEPoint2D(0,0),new FGEPoint2D(3.6,0),
                        new FGEPoint2D(0,3.6)},"Studio R1三角形");
                var options=new CxVisionInspectionOptions{
                    PixelDepth=CxVisionPixelDepth.Bits16,
                    GrayThreshold=200,MinDefectArea=1,MaxDefectArea=16,UseAi=false
                };
                MRIObjectAddress address;
                using(var original=MEMEngineering.Create("Studio R1真实ROI重绑"))
                {
                    MEMContent content=original.AddPage("R1工程页").RootContent;
                    int before=original.Revision;
                    address=CxVisionRoiEngineering.Commit(original,content,before,
                        image,"r1/studio/gray16",roi,options,43,CancellationToken.None);
                    Assert(original.Revision==before+1,
                        "ROI必须只使用一次MEM事务提交Data/Visual");
                    original.SaveAs(projectPath);
                    original.Close(true);
                }
                using(var reopened=MEMEngineering.Open(projectPath))
                {
                    try
                    {
                        int revision=reopened.Revision;
                        // SHA-256只计有效Gray16原像素，不混入Stride Padding；
                        // 这是4x4×Gray16(210)的固定数值回归向量。
                        Assert(CxVisionRoiEngineering.Open(reopened,address).SourceHash==
                            "d89466b0fdf25b41a4f4a59603c59bc4dee524fd4912d19e3fab793a3a3a1e73",
                            "保存ROI原像素SHA-256与固定真值不一致");
                        var loaded=MainWindow.LoadStudioSavedRoiRaw(reopened,address,
                            rawPath,10,CancellationToken.None);
                        Assert(loaded.Width==4 && loaded.Height==4 &&
                            loaded.Stride==10 &&
                            loaded.PixelFormat==FIMPixelFormat.Gray16 &&
                            loaded.GetGrayValue(0,0)==210 &&
                            loaded.GetGrayValue(3,3)==210 &&
                            reopened.Revision==revision && !reopened.IsDirty,
                            "同Studio正式工程重开后无法按原像素/Stride重绑，或意外修改工程");

                        bool badStride=false;
                        try {MainWindow.LoadStudioSavedRoiRaw(reopened,address,
                            rawPath,0,CancellationToken.None);}
                        catch(InvalidDataException){badStride=true;}
                        Assert(badStride,"未显式提供原Stride时不应猜测/截断RAW");

                        // Padding不属于有效像素；改变它不应伪造图像内容差异。
                        byte[] changedPadding=(byte[])bytes.Clone();
                        changedPadding[39]=89;
                        File.WriteAllBytes(wrongPath,changedPadding);
                        var samePixels=MainWindow.LoadStudioSavedRoiRaw(reopened,
                            address,wrongPath,10,CancellationToken.None);
                        Assert(samePixels.GetGrayValue(3,3)==210,
                            "仅修改RAW Padding时不应误报原像素Hash不同");
                        byte[] changed=(byte[])bytes.Clone();
                        changed[30]=209; // 更改第四行首像素低字节，必须拒绝错源。
                        File.WriteAllBytes(wrongPath,changed);
                        bool wrongSource=false;
                        try{MainWindow.LoadStudioSavedRoiRaw(reopened,address,
                            wrongPath,10,CancellationToken.None);}
                        catch(InvalidOperationException){wrongSource=true;}
                        Assert(wrongSource,"同尺寸、同位深但像素/填充变化的旧来源不得绑定");

                        using(var stop=new CancellationTokenSource())
                        {
                            stop.Cancel();
                            bool cancelled=false;
                            try{MainWindow.LoadStudioSavedRoiRaw(reopened,address,
                                rawPath,10,stop.Token);}
                            catch(OperationCanceledException){cancelled=true;}
                            Assert(cancelled,"取消的RAW重绑不应创建可显示的原图");
                        }
                        Assert(reopened.Revision==revision && !reopened.IsDirty,
                            "错误来源/取消修改了正式MEM工程");
                        CxVisionRunSummary run=CxVisionRoiEngineering.RunSavedAsync(
                            reopened,address,loaded,CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Assert(run.RunId!=Guid.Empty && run.SourceId=="r1/studio/gray16" &&
                            run.ConfigRevision==43 &&
                            run.DefinitionRevision==revision &&
                            run.DefinitionRoiId==roi.Id &&
                            run.ValidatedSourceHash==
                                CxVisionRoiEngineering.Open(reopened,address).SourceHash &&
                            run.Items.Single(x=>x.Kind==CxVisionResultKind.Measurement)
                                .IntegerValue==6 &&
                            run.Items.Any(x=>x.Kind==CxVisionResultKind.Mask) &&
                            run.Items.Any(x=>x.Kind==CxVisionResultKind.Region) &&
                            !reopened.IsDirty && reopened.Revision==revision,
                            "Studio重绑后MRK结果/版本/区域面积/工程Dirty不一致");

                        var viewer=new MPWImageViewerControl();
                        var items=new ListBox();
                        var report=new MainWindow.StudioCpuVisionReport {
                            FlowId=run.RunId,Evidence=run,SourceImage=loaded
                        };
                        MainWindow.PopulateStudioVisionResults(report,viewer,items);
                        var mask=run.Items.Single(x=>x.Kind==CxVisionResultKind.Mask);
                        MainWindow.SelectStudioVisionResult(run,loaded,viewer,mask);
                        Assert(items.Items.Count==run.Items.Count &&
                            Object.ReferenceEquals(viewer.Session.Original,loaded) &&
                            Object.ReferenceEquals(viewer.Session.Result,mask.Image) &&
                            mask.Image.GetGrayValue(3,3)==0,
                            "Studio同一MPW图窗未正确显示MRK Mask或投影了错误来源");

                        CxVisionRunCsvExporter.SaveFile(csvPath,run);
                        FIMRawImageWriter.SaveFile(exportRaw,loaded,preserveStride:true);
                        Assert(File.ReadAllText(csvPath).Contains(run.RunId.ToString("N")) &&
                            File.ReadAllText(csvPath).Contains("AcceptedRegionPixelArea") &&
                            File.ReadAllBytes(exportRaw).SequenceEqual(bytes),
                            "重开后CSV/RAW导出未关联原Run或原始Stride字节发生改变");
                    }
                    finally{reopened.Close(true);}
                }
            }
            finally
            {
                try{Directory.Delete(folder,true);}catch(IOException){}
            }
        }

        private static void VerifyCpuVisionFileClosure()
        {
                    string folder=Path.Combine(Path.GetTempPath(),
                        "cx-studio-cpu-closure-"+Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    string filename=Path.Combine(folder,"real-5x3.raw");
                    try
                    {
                        File.WriteAllBytes(filename,new byte[]{
                            210,210,0,0,0,210,0,0,0,0,0,0,0,0,250});
                        var session=MEMEngineering.Create("CPU RAW软件闭环专用工程");
                        var config=new MHSConfigService(Path.Combine(folder,"Configs"))
                            .ForProject(session.Project.Id.ToString("N"))
                            .Open<CxVisionInspectionOptions>();
                        var committed=config.UpdateAsync(c=>{
                            c.PixelDepth=CxVisionPixelDepth.Bits8;
                            c.GrayThreshold=200;
                            c.MinDefectArea=2;
                            c.MaxDefectArea=100;
                            c.UseAi=false;
                        },"Studio真实RAW软件验证",System.Threading.CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Assert(committed.Success,"测试Effective配置保存失败");
                        if(config.HasPendingApply)
                            Assert(config.ApplyPendingAtTaskBoundary(),"Effective应用未成功");
                        var provider=CxVisionThresholdProvider.FromEffectiveConfiguration(config);
                        int before=session.Revision;
                        bool badLengthDenied=false;
                        try
                        {
                            MainWindow.ExecuteStudioCpuVisionFileAsync(session,provider,
                                filename,6,3,System.Threading.CancellationToken.None)
                                .GetAwaiter().GetResult();
                        }
                        catch(InvalidDataException){badLengthDenied=true;}
                        Assert(badLengthDenied&&session.Revision==before,
                            "不匹配RAW长度意外修改了MEM或未被拒绝");
                        var cpuReport=MainWindow.ExecuteStudioCpuVisionFileAsync(session,provider,
                            filename,5,3,System.Threading.CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Assert(cpuReport.Inspection!=null &&
                            cpuReport.Inspection.ThresholdPixelCount==4 &&
                            cpuReport.Inspection.Regions.Count==1 &&
                            cpuReport.Inspection.Regions[0].PixelArea==3 &&
                            !cpuReport.Inspection.Passed &&
                            cpuReport.Inspection.ConfigRevision==provider.ConfigurationRevision &&
                            cpuReport.CompletedTraceEvents>0 && cpuReport.FlowId!=Guid.Empty &&
                            cpuReport.PlanFingerprint.Length==64 &&
                            session.Find(cpuReport.PageId) is MEMPage &&
                            session.Revision>before,
                            "真实RAW→MEM→MOC授权发布→MRK计算/Trace闭环未获得可复核输出");
                        var captured=provider.CaptureInspectionOptions();
                        Assert(captured.PixelDepth==CxVisionPixelDepth.Bits8 &&
                            captured.GrayThreshold==200 && captured.MinDefectArea==2,
                            "Studio拆分能力未读取当前真实MHS Effective参数");
                        // 修改返回副本不得影响已固定的Provider或下一次参数捕获。
                        captured.GrayThreshold=5;
                        Assert(provider.CaptureInspectionOptions().GrayThreshold==200,
                            "CPU Provider泄露了可变配置别名");
                        var split=MainWindow.ExecuteStudioSeparatedCpuVisionFileAsync(
                            session,provider,filename,5,3,0,System.Threading.CancellationToken.None)
                            .GetAwaiter().GetResult();
                        var mask=split.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Mask);
                        Assert(split.FlowId!=cpuReport.FlowId &&
                            split.SegmentId!=Guid.Empty && split.EnhanceId==Guid.Empty &&
                            split.MeasureId!=Guid.Empty && split.InspectId!=Guid.Empty &&
                            mask.RunId==split.FlowId &&
                            mask.Image.GetGrayValue(0,0)==255 &&
                            mask.Image.GetGrayValue(4,2)==255 &&
                            split.Inspection.ThresholdPixelCount==4 &&
                            split.Inspection.Regions.Count==1 &&
                            split.AcceptedRegionPixelArea==3 &&
                            split.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Measurement)
                                .IntegerValue==3 &&
                            split.TraceRecords.Any(x=>x.BlockId==split.SegmentId &&
                                x.State=="Completed") &&
                            session.Find(split.PageId) is MEMPage,
                            "Studio四节点真实Mask/Region/面积及工程Trace执行不正确");
                        // 使用真实Studio Report和MPW WPF控件验证结果显示，而非只测算法。
                        var previewViewer=new MPWImageViewerControl();
                        var previewItems=new ListBox();
                        Assert(split.SourceImage!=null &&
                            split.SourceImage.PixelFormat==CxVisual.Foundation.Imaging.FIMPixelFormat.Gray8 &&
                            split.SourceImage.GetGrayValue(0,0)==210 &&
                            split.SourceImage.GetGrayValue(4,2)==250,
                            "Studio真实Runner没有保留与MRK同字节的源图快照");
                        MainWindow.PopulateStudioVisionResults(split,previewViewer,previewItems);
                        Assert(previewItems.Items.Count==split.Evidence.Items.Count &&
                            Object.ReferenceEquals(previewViewer.Session.Original,split.SourceImage),
                            "Studio结果面板未共用原MPW Viewer及完整Run产物");
                        // 兼容旧一体化Run：没有原图快照时，统计/测量仍可点选，
                        // 但不能因此伪造Image或Mask，也不能显示其他Run的图。
                        var legacyViewer=new MPWImageViewerControl();
                        var legacyItems=new ListBox();
                        MainWindow.PopulateStudioVisionResults(cpuReport,legacyViewer,legacyItems);
                        var legacyMeasure=cpuReport.Evidence.Items.Single(x=>
                            x.Kind==CxVisionResultKind.Measurement);
                        string measureText=MainWindow.SelectStudioVisionResult(
                            cpuReport.Evidence,null,legacyViewer,legacyMeasure);
                        Assert(cpuReport.SourceImage==null &&
                            legacyViewer.Session.Original==null &&
                            measureText.Contains("3") &&
                            legacyItems.Items.Count==cpuReport.Evidence.Items.Count,
                            "无原图旧Run测量列表不应被新预览强制禁用");
                        bool mixedSourceDenied=false;
                        try
                        {
                            MainWindow.SelectStudioVisionResult(split.Evidence,null,
                                legacyViewer,split.Evidence.Items.Single(x=>
                                    x.Kind==CxVisionResultKind.Mask));
                        }
                        catch(InvalidOperationException){mixedSourceDenied=true;}
                        Assert(mixedSourceDenied,"没有同源原图时不得显示旧Run的Mask");

                        var imageResult=split.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Mask);
                        MainWindow.SelectStudioVisionResult(split.Evidence,split.SourceImage,
                            previewViewer,imageResult);
                        Assert(Object.ReferenceEquals(previewViewer.Session.Result,imageResult.Image) &&
                            previewViewer.Session.ActiveSlot==MVIImageSlot.Result &&
                            previewViewer.ActiveImage.GetGrayValue(4,2)==255,
                            "Studio点击Mask不能显示真实MRK二值产物");
                        var projectedRun=split.Evidence.WithThresholdMaskContours();
                        var outer=projectedRun.Items.Where(x=>x.Kind==CxVisionResultKind.Contour)
                            .First(x=>!x.Contour.IsHole);
                        var projection=new MainWindow.StudioCpuVisionReport {
                            FlowId=split.FlowId,Evidence=projectedRun,
                            SourceImage=split.SourceImage
                        };
                        MainWindow.PopulateStudioVisionResults(projection,previewViewer,previewItems);
                        MainWindow.SelectStudioVisionResult(projectedRun,split.SourceImage,
                            previewViewer,outer);
                        MainWindow.SelectStudioVisionResult(projectedRun,split.SourceImage,
                            previewViewer,outer);
                        Assert(previewViewer.Overlays.Items.Count(x=>x.ExternalId==outer.ItemId)==1 &&
                            previewViewer.Overlays.Items.Single(x=>x.ExternalId==outer.ItemId).IsLocked &&
                            previewViewer.Session.ActiveSlot==MVIImageSlot.Original &&
                            previewItems.Items.Count==projectedRun.Items.Count,
                            "Studio Contour结果项在同一个MVI Store重复创建或未显示原图");
                        bool staleResultDenied=false;
                        try
                        {
                            MainWindow.SelectStudioVisionResult(cpuReport.Evidence,split.SourceImage,
                                previewViewer,outer);
                        }
                        catch(InvalidOperationException){staleResultDenied=true;}
                        Assert(staleResultDenied,"Studio旧Run不得展示新Run的轮廓");
                        previewViewer.Clear();
                        bool clearedDenied=false;
                        try
                        {
                            MainWindow.SelectStudioVisionResult(projectedRun,split.SourceImage,
                                previewViewer,outer);
                        }
                        catch(InvalidOperationException){clearedDenied=true;}
                        Assert(clearedDenied && previewViewer.Overlays.Items.Count==0,
                            "Studio视口Clear之后旧Contour不可再进入Viewer");
                        var snapshot=session.GetSnapshot();
                        var splitContent=snapshot.Nodes.Single(x=>x.Kind==MEMNodeKind.Content &&
                            x.ParentId==split.PageId);
                        var wiring=MOLWiringCodec.Get(splitContent);
                        Assert(wiring!=null && wiring.Blocks.Count==4 &&
                            wiring.Links.Count(x=>x.Kind==MPCPortKind.Data)==3 &&
                            wiring.FlowOutputs.ContainsKey("ThresholdMask") &&
                            wiring.FlowOutputs.ContainsKey("AcceptedRegionPixelArea"),
                            "Studio拆分式算法没有进入唯一MEM/MOL工程模型");
                        // 以隔离测试会话验证五节点，真实中值处理不能修改RAW原件。
                        byte[] larger=new byte[49];
                        for(int y=2;y<=4;y++)for(int x=2;x<=4;x++)larger[y*7+x]=210;
                        larger[0]=250;
                        File.WriteAllBytes(filename,larger);
                        Assert(split.SourceImage.GetGrayValue(0,0)==210 &&
                            split.SourceImage.GetGrayValue(4,2)==250 &&
                            split.SourceImage.Width==5 && split.SourceImage.Height==3,
                            "本次执行RAW被替换后，Studio原图快照不得随磁盘文件漂移");
                        var five=MainWindow.ExecuteStudioSeparatedCpuVisionFileAsync(
                            session,provider,filename,7,7,1,System.Threading.CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Assert(five.EnhanceId!=Guid.Empty && five.SegmentId!=Guid.Empty &&
                            five.Inspection.ThresholdPixelCount==5 &&
                            five.AcceptedRegionPixelArea==5 &&
                            five.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Mask)
                                .Image.GetGrayValue(0,0)==0 &&
                            five.Evidence.Items.Any(x=>x.Kind==CxVisionResultKind.Image) &&
                            File.ReadAllBytes(filename).SequenceEqual(larger),
                            "Studio五节点FIM中值/分割/面积执行或原RAW保护失败");
                        int unchanged=session.Revision;
                        bool badRadius=false;
                        try
                        {
                            MainWindow.ExecuteStudioSeparatedCpuVisionFileAsync(
                                session,provider,filename,7,7,4,
                                System.Threading.CancellationToken.None)
                                .GetAwaiter().GetResult();
                        }
                        catch(ArgumentOutOfRangeException){badRadius=true;}
                        Assert(badRadius && session.Revision==unchanged,
                            "超出半径预算的Studio执行修改了正式工程");
                        // 与Studio按钮共用唯一的静态合同/版本/编辑方法；
                        // 独立插入三个算子而不建立第二套持久Graph。
                        var compositionPage=session.AddPage("Studio Vision独立节点参数合同");
                        var thresholdBlock=MainWindow.AddStudioVisionCapabilityToFlow(
                            session,compositionPage.RootContent,provider,
                            CxVisionBinaryThresholdProvider.CapabilityId);
                        var currentPage=session.Find(compositionPage.Id) as MEMPage;
                        Assert(currentPage!=null,"拆分式Studio Content未入工程");
                        var regionBlock=MainWindow.AddStudioVisionCapabilityToFlow(
                            session,currentPage.RootContent,provider,
                            CxVisionMaskRegionsProvider.CapabilityId);
                        currentPage=session.Find(compositionPage.Id) as MEMPage;
                        var areaBlock=MainWindow.AddStudioVisionCapabilityToFlow(
                            session,currentPage.RootContent,provider,
                            CxVisionAcceptedRegionMeasureProvider.CapabilityId);
                        currentPage=session.Find(compositionPage.Id) as MEMPage;
                        var composed=MOLWiringCodec.Get(session.GetSnapshot().Find(currentPage.RootContent.Id));
                        var controls=new CxVisionMaskRegionsProvider(
                            provider.CaptureInspectionOptions(),provider.ConfigurationRevision);
                        Assert(composed.Blocks.Count==3 &&
                            composed.Blocks.Select(x=>x.Id).Distinct().Count()==3 &&
                            composed.Blocks.Any(x=>x.Id==thresholdBlock &&
                                x.CapabilityId==CxVisionBinaryThresholdProvider.CapabilityId) &&
                            composed.Blocks.Any(x=>x.Id==regionBlock &&
                                x.CapabilityId==CxVisionMaskRegionsProvider.CapabilityId) &&
                            composed.Blocks.Any(x=>x.Id==areaBlock &&
                                x.CapabilityId==CxVisionAcceptedRegionMeasureProvider.CapabilityId) &&
                            composed.Parameters[thresholdBlock]["Threshold"]=="200" &&
                            composed.Parameters[regionBlock]["RuleVersion"]==controls.ReleaseVersion &&
                            composed.Links.Count==0,
                            "Studio独立算子未固定MHS参数、MOL身份或错误自动连接尚缺的图像输入");
                        unchanged=session.Revision;
                        bool unknownDenied=false;
                        try
                        {
                            MainWindow.AddStudioVisionCapabilityToFlow(
                                session,currentPage.RootContent,provider,
                                "vision.missing.unverified");
                        }
                        catch(InvalidOperationException){unknownDenied=true;}
                        Assert(unknownDenied && session.Revision==unchanged,
                            "未知Capability未拒绝或非法添加修改了MEM Revision");
                        // 与模块中心按钮共用生产入口：MHS Effective创建一次性的
                        // 可复用Mask→Region→Measure完整V2连线（非新Graph）。
                        var authored=MainWindow.CreateStudioSeparatedVisionFlow(
                            session,provider);
                        var authoredPage=session.Find(authored.PageId) as MEMPage;
                        Assert(authoredPage!=null &&
                            authored.MaskBlockId!=authored.RegionBlockId &&
                            authored.RegionBlockId!=authored.MeasureBlockId,
                            "真实专业模板未创建MEM Page或三个独立块");
                        var authoredWiring=MOLWiringCodec.Get(
                            session.GetSnapshot().Find(authoredPage.RootContent.Id));
                        authoredWiring.ValidateExecutable();
                        Assert(authoredWiring.Blocks.Count==3 &&
                            authoredWiring.Links.Count==4 &&
                            authoredWiring.Links.Count(x=>x.Kind==MPCPortKind.Control)==2 &&
                            authoredWiring.Links.Count(x=>x.Kind==MPCPortKind.Data)==2 &&
                            authoredWiring.Parameters[authored.MaskBlockId]["Threshold"]=="200" &&
                            authoredWiring.Parameters[authored.RegionBlockId]["RuleVersion"]==
                                controls.ReleaseVersion &&
                            authoredWiring.FlowInputs.Count==3 &&
                            authoredWiring.FlowInputs["RawPixels"].BlockId==
                                authored.MaskBlockId &&
                            authoredWiring.FlowInputs["Width"].PortMemberId=="Width" &&
                            authoredWiring.FlowInputs["Height"].PortMemberId=="Height" &&
                            authoredWiring.FlowOutputs.Count==3 &&
                            authoredWiring.FlowOutputs["ThresholdMask"].BlockId==
                                authored.MaskBlockId &&
                            authoredWiring.FlowOutputs["Inspection"].BlockId==
                                authored.RegionBlockId &&
                            authoredWiring.FlowOutputs["AcceptedRegionPixelArea"].BlockId==
                                authored.MeasureBlockId,
                            "Studio专业模板未生成可执行Mask/Region/Measure完整拓扑与公开端口");
                        var binary=MOLWiringCodec.Serialize(authoredWiring);
                        var restored=MOLWiringCodec.Deserialize(binary);
                        restored.ValidateExecutable();
                        Assert(restored.EntryBlockId==authored.MaskBlockId &&
                            restored.Blocks.Count==3 &&
                            restored.Links.Count==4 &&
                            restored.FlowInputs.Count==3 &&
                            restored.FlowOutputs.Count==3 &&
                            restored.Parameters[authored.RegionBlockId]["RuleVersion"]==
                                controls.ReleaseVersion,
                            "工程持久V2 Codec无法无损保存/还原真实视觉流程参数与连线");
                        // 不靠静态Codec作假：用已有MOC发布候选编译唯一MEM真源，
                        // 再由MRK/MDS实际运行外部RAW三输入，读取真实Mask/面积。
                        var measuredProvider=new CxVisionAcceptedRegionMeasureProvider(provider);
                        var regionsProvider=new CxVisionMaskRegionsProvider(
                            provider.CaptureInspectionOptions(),provider.ConfigurationRevision,
                            measuredProvider);
                        var maskProvider=new CxVisionBinaryThresholdProvider(
                            provider.PixelDepth,provider.CaptureInspectionOptions().GrayThreshold,
                            regionsProvider);
                        var versions=new Dictionary<string,string>(StringComparer.Ordinal) {
                            {CxVisionBinaryThresholdProvider.CapabilityId,maskProvider.ReleaseVersion},
                            {CxVisionMaskRegionsProvider.CapabilityId,regionsProvider.ReleaseVersion},
                            {CxVisionAcceptedRegionMeasureProvider.CapabilityId,
                                CxVisionAcceptedRegionMeasureProvider.Version}
                        };
                        var candidate=MOCProjectRelease.Prepare(
                            session.GetSnapshot(),authoredPage.RootContent.Id,
                            maskProvider,versions);
                        Assert(candidate.Plan!=null && candidate.Fingerprint.Length==64,
                            "完整三节点专业模板未通过真实MOC编译和来源指纹");
                        var external=candidate.Plan.InputDefaults.ToDictionary(
                            x=>x.Key,x=>x.Value,StringComparer.Ordinal);
                        external.Add("RawPixels",new byte[]{
                            210,210,0,0,0,210,0,0,0,0,0,0,0,0,250});
                        external.Add("Width",5);external.Add("Height",3);
                        var actualEngine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
                        try
                        {
                            var executed=actualEngine.StartFlow(candidate.Plan.Plan,external)
                                .Completion.GetAwaiter().GetResult();
                            Assert(executed.State==MRKFlowState.Completed &&
                                executed.GetCommittedOutput<
                                    CxVisual.Foundation.Imaging.FIMImageBuffer>("ThresholdMask")
                                    .GetGrayValue(4,2)==255 &&
                                executed.GetCommittedOutput<long>(
                                    "AcceptedRegionPixelArea")==3,
                                "独立MOL模板不能通过实际MRK/MDS算出4像素Mask和3px²面积");
                        }
                        finally { actualEngine.StopAsync(true).GetAwaiter().GetResult(); }
                        // 真正SaveAs → Close → Open，验证V2连线、输入出口和锁定参数。
                        string templateFile=Path.Combine(folder,"VisionTemplate.cxproj");
                        Guid persistedPage=Guid.Empty;
                        Guid persistedMask=Guid.Empty;
                        var localOptions=provider.CaptureInspectionOptions();
                        using(var authoredProject=MEMEngineering.Create("Vision可复用节点工程"))
                        {
                            try
                            {
                                var localProvider=new CxVisionThresholdProvider(
                                    localOptions,provider.ConfigurationRevision);
                                var template=MainWindow.CreateStudioSeparatedVisionFlow(
                                    authoredProject,localProvider);
                                persistedPage=template.PageId;
                                persistedMask=template.MaskBlockId;
                                authoredProject.SaveAs(templateFile);
                            }
                            finally { authoredProject.Close(true); }
                        }
                        using(var reopenedProject=MEMEngineering.Open(templateFile))
                        {
                            try
                            {
                                var pageFromDisk=reopenedProject.GetSnapshot().Find(persistedPage);
                                Assert(pageFromDisk!=null && pageFromDisk.Kind==MEMNodeKind.Page,
                                    "视觉专业流程SaveAs/Close/Open后Page身份丢失");
                                var pageContent=reopenedProject.GetSnapshot().Nodes.Single(n=>
                                    n.Kind==MEMNodeKind.Content && n.ParentId==persistedPage);
                                var persisted=MOLWiringCodec.Get(pageContent);
                                persisted.ValidateExecutable();
                                Assert(persisted.Blocks.Count==3 && persisted.Links.Count==4 &&
                                    persisted.FlowInputs.Count==3 && persisted.FlowOutputs.Count==3 &&
                                    persisted.Parameters[persistedMask]["Threshold"]=="200" &&
                                    !reopenedProject.IsDirty,
                                    "正式MEM工程磁盘重开丢失视觉专业拓扑/阈值或误标脏工程");
                            }
                            finally { reopenedProject.Close(true); }
                        }
                        // 该定义可以由既有MOL编辑器继续编辑，不能是按钮里的临时Graph。
                        MOCFlowBuilder.Edit(session,authoredPage.RootContent,b=>
                            b.Move(authored.MaskBlockId,144,88));
                        var moved=MOLWiringCodec.Get(
                            session.GetSnapshot().Find(authoredPage.RootContent.Id));
                        Assert(moved.Positions[authored.MaskBlockId].X==144 &&
                            moved.Positions[authored.MaskBlockId].Y==88 &&
                            moved.Links.Count==4,
                            "新建视觉流程不能继续使用原MOL画布编辑位置和连接");
                        unchanged=session.Revision;
                        var update=config.UpdateAsync(c=>c.GrayThreshold=201,
                            "Studio Effective变化栅栏",System.Threading.CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Assert(update.Success && config.ApplyPendingAtTaskBoundary(),
                            "测试MHS生效配置变更失败");
                        bool staleDenied=false;
                        try { provider.CaptureInspectionOptions(); }
                        catch(InvalidOperationException){staleDenied=true;}
                        Assert(staleDenied,"旧MHS Effective Provider在配置已更新后仍可启动拆分工程");
                        unchanged=session.Revision;
                        bool staleRunDenied=false;
                        try
                        {
                            MainWindow.ExecuteStudioSeparatedCpuVisionFileAsync(
                                session,provider,filename,7,7,0,
                                System.Threading.CancellationToken.None)
                                .GetAwaiter().GetResult();
                        }
                        catch(InvalidOperationException){staleRunDenied=true;}
                        Assert(staleRunDenied && session.Revision==unchanged,
                            "已失效MHS Provider仍可新增工程或污染Revision");
                        bool staleInsertDenied=false;
                        try
                        {
                            MainWindow.AddStudioVisionCapabilityToFlow(
                                session,currentPage.RootContent,provider,
                                CxVisionBinaryThresholdProvider.CapabilityId);
                        }
                        catch(InvalidOperationException){staleInsertDenied=true;}
                        Assert(staleInsertDenied && session.Revision==unchanged,
                            "配置变更后旧MHS Provider仍允许向Studio主流程添加节点");
                        bool staleTemplateDenied=false;
                        try { MainWindow.CreateStudioSeparatedVisionFlow(session,provider); }
                        catch(InvalidOperationException){staleTemplateDenied=true;}
                        Assert(staleTemplateDenied && session.Revision==unchanged,
                            "MHS失效后专业模板仍建新工程页或污染Revision");
                    }
                    finally
                    {
                        try{Directory.Delete(folder,true);}catch(IOException){}
                    }
        }

        private static T AwaitStudioTask<T>(Task<T> task)
        {
            if(task==null)throw new ArgumentNullException("task");
            if(!task.IsCompleted)
            {
                var frame=new System.Windows.Threading.DispatcherFrame();
                var clock=System.Diagnostics.Stopwatch.StartNew();
                var timer=new System.Windows.Threading.DispatcherTimer {
                    Interval=TimeSpan.FromMilliseconds(25)};
                timer.Tick+=(sender,args)=>{
                    if(task.IsCompleted||clock.Elapsed>TimeSpan.FromSeconds(20))
                        frame.Continue=false;
                };
                timer.Start();
                try{System.Windows.Threading.Dispatcher.PushFrame(frame);}
                finally{timer.Stop();}
                if(!task.IsCompleted)
                    throw new TimeoutException("Studio三维异步分析在Windows STA超时。");
            }
            return task.GetAwaiter().GetResult();
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
