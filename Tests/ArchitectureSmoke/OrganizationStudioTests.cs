using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationStudioTests
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("H06 Studio: "+message);}
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
        private static void Pump(){Dispatcher.CurrentDispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);}
        private static void Click(Window window,string label)
        {
            window.UpdateLayout();
            var buttons=Descendants(window).OfType<Button>().ToArray();
            var button=buttons.FirstOrDefault(b=>object.Equals(b.Content,label));
            if(button==null)throw new InvalidOperationException("Missing Studio button '"+label+
                "'; visible controls: "+string.Join(" | ",buttons.Select(b=>b.Content==null?"(none)":b.Content.ToString())));
            var peer=new ButtonAutomationPeer(button);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();Pump();
        }
        private static object Get(object instance,string name){return instance.GetType().GetProperty(name).GetValue(instance,null);}
        internal static void RunOnSta()
        {
            string path=Path.Combine(Path.GetTempPath(),"cx-h06-studio-"+Guid.NewGuid().ToString("N")+".cxproj");Window window=null;Application app=null;
            try
            {
                var assembly=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CxVisual.Studio.exe"));
                if(Application.Current==null){app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/CxVisual.Studio;component/Themes/Colors.xaml",UriKind.Relative)});app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/CxVisual.Studio;component/Styles/Controls.xaml",UriKind.Relative)});}
                window=(Window)Activator.CreateInstance(assembly.GetType("CxVisual.Studio.MainWindow"));window.Left=-10000;window.Top=-10000;window.ShowInTaskbar=false;window.ShowActivated=false;
                var vm=window.DataContext;vm.GetType().GetProperty("ConfirmReplace").SetValue(vm,new Func<MEMEngineeringSession,bool>(s=>true),null);window.Show();Pump();
                var session=(MEMEngineeringSession)Get(vm,"ProjectSession");var original=session.Project.Pages[0];int oldCount=original.RootContent.DescendantsAndSelf().Count();
                Click(window,"添加页面");var page=session.Project.Pages.Last();Click(window,"添加矩形");Check(page.RootContent.VisualItems.Count==1&&original.RootContent.DescendantsAndSelf().Count()==oldCount,"active page button added to wrong page");
                // 真正点击Studio九图元工具箱，并用属性入口提交工程定义。
                var nine=new[]{"直线","矩形Shape","圆形","椭圆","折线","多边形",
                    "圆弧","贝塞尔","自由路径"};
                foreach(string label in nine)Click(window,label);
                var gallery=page.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().ToArray();
                Check(gallery.Length==9&&gallery.Select(x=>x.Shape.Kind).Distinct().Count()==9,
                    "Studio palette did not create nine persisted shapes");
                // Studio真实按钮闭合/断开Path，同一工程历史负责Undo/Redo。
                var editablePath=gallery[8];
                vm.GetType().GetMethod("Select").Invoke(vm,new object[]{(Guid?)editablePath.Id});
                int pathRevision=session.Revision;
                Click(window,"路径断开");
                Check(session.Revision==pathRevision+1&&
                    editablePath.Shape.Geometry.Commands.Last().Verb!=FGEPathVerb.Close,
                    "Studio path open button didn't commit the definition");
                Click(window,"撤销");
                Check(editablePath.Shape.Geometry.Commands.Last().Verb==FGEPathVerb.Close,
                    "Studio path opening didn't use original Undo");
                Click(window,"重做");
                Check(editablePath.Shape.Geometry.Commands.Last().Verb!=FGEPathVerb.Close,
                    "Studio path opening didn't use original Redo");
                Click(window,"路径闭合");
                Check(editablePath.Shape.Geometry.Commands.Last().Verb==FGEPathVerb.Close,
                    "Studio path close button didn't restore closed path");
                // 实际Studio ViewModel从当前WPF场景世界坐标提交Bezier控制柄事务。
                Guid bezierId=gallery[7].Id;
                var visualRows=(IEnumerable<CxSceneItem>)Get(vm,"Items");
                CxSceneItem bezierScene=visualRows.First(x=>x.Id==bezierId);
                MVIVisualHandle studioHandle=MVIVisualEditing.Handles(bezierScene)
                    .First(x=>x.Kind==MVIVisualHandleKind.Control1);
                var oldControl=gallery[7].Shape.Geometry.Commands[1].Control1;
                int editBefore=session.Revision;
                vm.GetType().GetMethod("Select").Invoke(vm,new object[]{(Guid?)bezierId});
                vm.GetType().GetMethod("EditVisualHandle").Invoke(vm,new object[]{
                    studioHandle,studioHandle.WorldPoint.X+19,studioHandle.WorldPoint.Y-17});
                Check(session.Revision==editBefore+1&&
                    !gallery[7].Shape.Geometry.Commands[1].Control1.Equals(oldControl),
                    "Studio did not commit world Bezier control handle transaction");
                Click(window,"撤销");
                Check(gallery[7].Shape.Geometry.Commands[1].Control1.Equals(oldControl),
                    "Studio Bezier control handle Undo failed");
                Click(window,"重做");
                Check(!gallery[7].Shape.Geometry.Commands[1].Control1.Equals(oldControl),
                    "Studio Bezier control handle Redo failed");
                vm.GetType().GetMethod("Select").Invoke(vm,new object[]{(Guid?)gallery[8].Id});
                var fillBox=(TextBox)window.FindName("FillColorInput");
                fillBox.Text="#FF27B478";
                Click(window,"应用填充");
                Check(MRCVisualAppearance.StyleOf(gallery[8]).Fill.Color.Equals(
                    MRCColor.Parse("#FF27B478")),"Studio style was not committed");
                Click(window,"绑定事件");
                Check(MRCVisualAppearance.BehaviorOf(gallery[8]).EventActions.Count==1,
                    "Studio event/action was not persisted");
                Click(window,"撤销");
                Check(MRCVisualAppearance.BehaviorOf(gallery[8]).EventActions.Count==0,
                    "Studio event undo did not restore prior definition");
                Click(window,"重做");
                Check(MRCVisualAppearance.BehaviorOf(gallery[8]).EventActions.Count==1,
                    "Studio event redo failed");
                // 同一Shape的动画在预览帧真实求值，不修改工程Revision。
                Click(window,"添加呼吸动画");
                var animated=gallery[8];
                Check(MRCVisualAppearance.BehaviorOf(animated).Animations.Count==1,
                    "Studio animation command not persisted");
                int beforePreviewRevision=session.Revision;
                Click(window,"预览交互");
                var vmType=vm.GetType();
                vmType.GetMethod("UpdateVisualRuntimeFrame").Invoke(vm,new object[]{450.0});
                var runtimeItems=(IEnumerable<CxVisual.Mechanism.Visual.CxSceneItem>)Get(vm,"Items");
                var rendered=runtimeItems.First(x=>x.Id==animated.Id);
                Check(Math.Abs(rendered.Style.Opacity-0.575)<1e-6,
                    "Studio animation evaluation frame missing");
                Check(session.Revision==beforePreviewRevision,
                    "Studio animation preview dirtied engineering project");
                Click(window,"返回设计");
                Check(session.Revision==beforePreviewRevision,
                    "Leaving runtime preview committed an edit transaction");
                var editorItems=(IEnumerable<CxVisual.Mechanism.Visual.CxSceneItem>)Get(vm,"Items");
                Check(Math.Abs(editorItems.First(x=>x.Id==animated.Id).Style.Opacity-1)<1e-9,
                    "Studio returned to design but retained animation frame appearance");
                Click(window,"文字图元");
                Check(page.RootContent.DescendantsAndSelf().OfType<MRCTextElement>().Any(),
                    "Studio did not create text element");
                Click(window,"按钮控件");
                Check(page.RootContent.DescendantsAndSelf().OfType<MRCWidgetElement>().Count()==1,
                    "Studio widget tool did not create engineering element");
                var studioWidget=page.RootContent.DescendantsAndSelf()
                    .OfType<MRCWidgetElement>().Single();
                ((TextBox)window.FindName("WidgetContentInput")).Text="检测启动";
                ((TextBox)window.FindName("WidgetOptionsInput")).Text="运行|停止";
                ((TextBox)window.FindName("WidgetMinInput")).Text="10";
                ((TextBox)window.FindName("WidgetMaxInput")).Text="90";
                ((CheckBox)window.FindName("WidgetEnabledInput")).IsChecked=false;
                int revisionBeforeWidget=session.Revision;
                Click(window,"应用控件属性");
                Check(session.Revision==revisionBeforeWidget+1&&
                    studioWidget.GetBinding("content").Value.AsString()=="检测启动"&&
                    studioWidget.GetBinding("maximum").Value.AsNumber()==90&&
                    !studioWidget.GetBinding("enabled").Value.AsBoolean(),
                    "Studio Widget editor failed to atomically persist settings");
                Click(window,"撤销");
                Check(studioWidget.GetBinding("content")==null,
                    "Studio Widget configuration requires more than one Undo");
                Click(window,"重做");
                Check(studioWidget.GetBinding("content").Value.AsString()=="检测启动",
                    "Studio Widget settings not restored by Redo");
                var sceneHost=(CxVisual.Mechanism.Presentation.Wpf.SceneView)window.FindName("SceneHost");
                var widgetHost=VisualTreeHelper.GetChild(sceneHost,0) as Canvas;
                Check(widgetHost!=null&&widgetHost.Children.OfType<Button>().Any(),
                    "Studio widget is not a true WPF Button");
                Click(window,"预览交互");
                Check(!sceneHost.IsDesignMode,"Widget preview did not enter runtime interaction mode");
                Click(window,"返回设计");
                Check(sceneHost.IsDesignMode,"Widget preview did not return to design mode");
                Click(window,"新建组件");var symbol=session.Project.Symbols.Last();var resources=(ListBox)window.FindName("EngineeringResources");resources.SelectedItem=symbol;Pump();
                Click(window,"放置实例");var first=page.RootContent.References.Last();Click(window,"放置实例");var second=page.RootContent.References.Last();
                Check(first.Id!=second.Id&&page.RootContent.References.Count==2,"resource button did not create two real instances");
                var properties=(ItemsControl)window.FindName("EngineeringProperties");window.UpdateLayout();var text=Descendants(properties).OfType<TextBox>().First();text.Text="240";BindingOperations.GetBindingExpression(text,TextBox.TextProperty).UpdateSource();Click(window,"应用");
                Check(session.ResolveParameter(second,"宽度").Value.AsNumber()==240&&session.ResolveParameter(first,"宽度").Value.AsNumber()==120,"WPF property binding changed shared defaults");
                Click(window,"撤销");Check(session.ResolveParameter(second,"宽度").Value.AsNumber()==120,"property change required multiple undo");Click(window,"重做");
                Click(window,"编辑共享定义");Click(window,"添加矩形");Check(symbol.RootGroup.Children.Count==2,"shared definition editor context missing");Click(window,"返回");
                vm.GetType().GetMethod("SaveProject").Invoke(vm,new object[]{path});Click(window,"保存工程");vm.GetType().GetMethod("OpenProject").Invoke(vm,new object[]{path,true});Pump();
                var reopened=(MEMEngineeringSession)Get(vm,"ProjectSession");Check(reopened.FindReference(second.Id)!=null&&reopened.ResolveParameter(reopened.FindReference(second.Id),"宽度").Value.AsNumber()==240,"Studio file reopen lost instance parameter");
                Check(reopened.Project.Pages.Last().RootContent.DescendantsAndSelf()
                    .OfType<MRCShapeElement>().Count()==9,
                    "Studio nine shapes lost after real Save/Open");
                Check(reopened.Project.Pages.Last().RootContent.DescendantsAndSelf()
                    .OfType<MRCWidgetElement>().Count()==1,
                    "Studio Widget lost after Save/Open");
                Check(((ComboBox)window.FindName("EngineeringPages")).Items.Count==2&&((ListBox)window.FindName("EngineeringResources")).Items.Count==3,"real WPF collections failed to refresh after reopen");
                // 先激活逻辑编排Tab，使工具箱在可视树中。
                var tabControl=Descendants(window).OfType<TabControl>().First();
                var logicTab=Descendants(window).OfType<TabItem>()
                    .First(t=>object.Equals(t.Header,"逻辑编排 · 运行观察"));
                tabControl.SelectedItem=logicTab;
                tabControl.SelectedIndex=1;
                tabControl.ApplyTemplate();
                tabControl.UpdateLayout();
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(new Action(()=>{}),DispatcherPriority.Loaded);
                Pump();
                Check(tabControl.SelectedIndex==1,"logic tab did not become active");
                // G04 工程师界面：目录搜索、自由插入草稿、撤销重做、选择端口并编译。
                Click(window,"创建自由连接示例");
                var active=(MEMPage)Get(vm,"ActivePage");
                var beforeWiring=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(beforeWiring!=null&&beforeWiring.Blocks.Count==2,
                    "Studio V2 wiring demo not persisted in current Content");
                // G04深化：真实WPF逻辑画布的Port点拖线入口，复用工程事务、撤销重做。
                var flowCanvas=(CxVisual.Mechanism.Presentation.Wpf.MOLFlowCanvas)
                    window.FindName("LogicHost");
                var existingLinks=(ComboBox)window.FindName("FlowExistingLinks");
                existingLinks.SelectedIndex=0;
                Click(window,"删除连接");
                var detached=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(detached.Links.Count==0,"Studio failed to detach prior edge before Port drag");
                flowCanvas.UpdateLayout();
                var endpoints=flowCanvas.GetVisiblePorts();
                var fromPort=endpoints.Single(p=>p.BlockId==detached.Blocks[0].Id&&
                    p.MemberId=="Done"&&p.Direction==MPCPortDirection.Output);
                var toPort=endpoints.Single(p=>p.BlockId==detached.Blocks[1].Id&&
                    p.MemberId=="Start"&&p.Direction==MPCPortDirection.Input);
                Check(!flowCanvas.BeginPortDrag(toPort.Center),
                    "Input Port incorrectly allowed as mouse drag source");
                // Escape/ResetLinkSelection只能取消预览，不能残留提交边。
                Check(flowCanvas.BeginPortDrag(fromPort.Center),
                    "Port cancellation setup failed");
                flowCanvas.ResetLinkSelection();
                Check(!flowCanvas.CompletePortDrag(toPort.Center)&&
                    MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id))
                    .Links.Count==0,"cancelled port drag unexpectedly committed a link");
                Check(flowCanvas.BeginPortDrag(fromPort.Center),
                    "Output Port geometry was not hit-testable");
                Check(!flowCanvas.CompletePortDrag(fromPort.Center),
                    "Output-to-output connection was incorrectly accepted");
                Check(flowCanvas.BeginPortDrag(fromPort.Center)&&
                    flowCanvas.CompletePortDrag(toPort.Center),
                    "Port drag did not reach Studio engineering transaction");
                var dragCommitted=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(dragCommitted.Links.Count==1&&
                    dragCommitted.Links[0].Source.PortMemberId=="Done"&&
                    dragCommitted.Links[0].Target.PortMemberId=="Start",
                    "Mouse Port drag did not persist exact typed endpoints");
                Click(window,"撤销编排");
                Check(MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id))
                    .Links.Count==0,"Port drag was not one atomic Undo step");
                Click(window,"重做编排");
                Check(MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id))
                    .Links.Count==1,"Port drag Redo lost connection");
                // G04布局同源：块拖动写回V2 Logic可选位置，Undo/Redo继续由Core负责。
                var movedId=detached.Blocks[1].Id;
                var previewBlock=CxVisual.Mechanism.Visual.MOLFlowLayout.Build(
                    flowCanvas.WiringDefinition).Single(b=>b.BlockId==movedId);
                var origin=new Point(previewBlock.X+previewBlock.Width/2,
                    previewBlock.Y+12);
                var destination=new Point(origin.X+85,origin.Y+38);
                Check(flowCanvas.BeginBlockDrag(origin),"WPF node cancel setup failed");
                flowCanvas.UpdateBlockDrag(destination);
                flowCanvas.ResetLinkSelection();
                Check(!flowCanvas.CompleteBlockDrag(destination)&&
                    !MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id))
                        .Positions.ContainsKey(movedId),
                    "cancelled WPF block drag wrote engineering position");
                Check(flowCanvas.BeginBlockDrag(origin),"WPF node body could not begin drag");
                flowCanvas.UpdateBlockDrag(destination);
                Check(flowCanvas.CompleteBlockDrag(destination),
                    "WPF drag was not committed to engineering session");
                var withPosition=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                MOLBlockPosition position;
                Check(withPosition.Positions.TryGetValue(movedId,out position)&&
                    Math.Abs(position.X-previewBlock.X-85)<0.001&&
                    Math.Abs(position.Y-previewBlock.Y-38)<0.001,
                    "Studio mouse move did not persist node layout");
                Click(window,"撤销编排");
                Check(!MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id))
                    .Positions.ContainsKey(movedId),"Layout move Undo was not atomic");
                Click(window,"重做编排");
                Check(MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id))
                    .Positions.ContainsKey(movedId),"Layout move Redo failed");
                Click(window,"搜索能力");
                var searchResults=(ComboBox)window.FindName("FlowCapabilityResults");
                Check(searchResults.Items.Count>=2,"capability catalog search not populated");
                Click(window,"添加能力");
                var draft=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(draft.Blocks.Count==3&&draft.Links.Count==1,
                    "free Step insertion unexpectedly forced a control link");
                Click(window,"验证工程编排");
                Check(((TextBlock)window.FindName("LogicInfo")).Text.Contains("MOC_WIRING_INVALID"),
                    "Studio did not show incomplete draft error");
                Click(window,"撤销编排");
                Check(MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id)).Blocks.Count==2,
                    "Studio wiring Undo failed");
                Click(window,"重做编排");
                var afterRedo=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(afterRedo.Blocks.Count==3,"Studio wiring Redo failed");
                var selectedIds=afterRedo.Blocks.Select(x=>x.Id).ToArray();
                var evType=assembly.GetType("CxVisual.Studio.MainWindow");
                var onLink=evType.GetMethod("OnWiringLinkRequested",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                onLink.Invoke(window,new object[]{null,
                    new CxVisual.Mechanism.Presentation.Wpf.MOLLinkRequestedEventArgs(
                        selectedIds[1],selectedIds[2])});
                var choiceBox=(ComboBox)window.FindName("FlowPortPairs");
                Check(choiceBox.Items.Count>0,"compatible Port pairs not shown: "+((TextBlock)window.FindName("LogicInfo")).Text);
                Click(window,"确认端口连线");
                var complete=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(complete.Links.Count==2,"UI chosen typed Port link was not committed");
                // 类型化Data端口与Control端口共用一套Port匹配和Connect事务。
                onLink.Invoke(window,new object[]{null,
                    new CxVisual.Mechanism.Presentation.Wpf.MOLLinkRequestedEventArgs(
                        selectedIds[0],selectedIds[2])});
                var dataChoice=choiceBox.Items.Cast<object>().FirstOrDefault(x=>
                    (MPCPortKind)x.GetType().GetProperty("Kind").GetValue(x,null)==MPCPortKind.Data);
                Check(dataChoice!=null,"Studio compatible Data Port option was not shown");
                choiceBox.SelectedItem=dataChoice;Pump();
                Click(window,"确认端口连线");
                var linked=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(linked.Links.Count==3&&linked.Links.Count(x=>x.Kind==MPCPortKind.Data)==1,
                    "Studio Data Port connection was not persisted");
                Click(window,"验证工程编排");
                Check(((TextBlock)window.FindName("LogicInfo")).Text.Contains("编译验证通过"),
                    "Studio Control+Data flow did not validate");
                vm.GetType().GetMethod("SaveProject").Invoke(vm,new object[]{path});
                vm.GetType().GetMethod("OpenProject").Invoke(vm,new object[]{path,true});Pump();
                var again=(MEMEngineeringSession)Get(vm,"ProjectSession");
                var persisted=MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id));
                Check(persisted!=null&&persisted.Blocks.Count==3&&
                    persisted.Blocks[2].Id==selectedIds[2]&&persisted.Links.Count==3,
                    "Studio edited Logic did not survive real Save/Open");
                Check(persisted.Positions.ContainsKey(movedId)&&
                    Math.Abs(persisted.Positions[movedId].X-position.X)<0.001,
                    "Studio node layout was not retained across Save/Open");
                // G04-C/D：同一真实WPF窗口操作框选、组合、粘贴、对齐、批量拖动及原子回滚。
                Check(Math.Abs(flowCanvas.ZoomFactor-1)<0.001,"Studio canvas zoom baseline wrong");
                Click(window,"放大画布");
                Check(flowCanvas.ZoomFactor>1,"Studio zoom button did not change LayoutTransform");
                Click(window,"100%");
                Check(Math.Abs(flowCanvas.ZoomFactor-1)<0.001,"Studio zoom reset failed");
                flowCanvas.UpdateLayout();
                var firstBox=CxVisual.Mechanism.Visual.MOLFlowLayout.Build(
                    flowCanvas.WiringDefinition).Single(x=>x.BlockId==selectedIds[0]);
                flowCanvas.SelectRectangle(new Rect(firstBox.X+20,firstBox.Y+9,30,20));
                Check(flowCanvas.SelectedBlockIds.Count==1&&
                    flowCanvas.SelectedBlockIds[0]==selectedIds[0],"WPF marquee selection hit testing failed");
                flowCanvas.SelectBlock(selectedIds[1],true,true);
                Check(flowCanvas.SelectedBlockIds.Count==2,"Ctrl additive selection failed");
                flowCanvas.SelectBlock(selectedIds[1],true,true);
                Check(flowCanvas.SelectedBlockIds.Count==1&&
                    flowCanvas.SelectedBlockIds[0]==selectedIds[0],
                    "Ctrl deselect must not reselect through drag");
                flowCanvas.SelectRectangle(new Rect(firstBox.X+20,firstBox.Y+9,30,20),true);
                Check(flowCanvas.SelectedBlockIds.Count==1,"additive marquee unexpectedly removed selected node");
                bool badSelectionRejected=false;
                try{flowCanvas.SetSelection(new[]{Guid.NewGuid()});}
                catch(InvalidOperationException){badSelectionRejected=true;}
                Check(badSelectionRejected&&flowCanvas.SelectedBlockIds.Count==1,
                    "WPF selection accepted foreign BlockId");
                Click(window,"全选节点");
                Check(flowCanvas.SelectedBlockIds.Count==3,"Studio Select All failed");
                Click(window,"组合选中");
                var groupedUI=MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id));
                Check(groupedUI.Groups.Count==1&&groupedUI.Groups[0].BlockIds.Count==3,
                    "Studio grouped selection not stored in same Content Logic");
                Click(window,"复制选区");
                Click(window,"粘贴选区");
                var pastedUI=MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id));
                Check(pastedUI.Blocks.Count==6&&pastedUI.Links.Count==6&&
                    pastedUI.Groups.Count==2&&flowCanvas.SelectedBlockIds.Count==3,
                    "Studio paste did not copy group/internal links or select new IDs");
                var pastedIds=flowCanvas.SelectedBlockIds.ToArray();
                Check(pastedIds.All(id=>!selectedIds.Contains(id)),
                    "Studio paste reused original block identity");
                Click(window,"自动布局");
                var gridUI=MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id));
                Check(pastedIds.Select(id=>gridUI.Positions[id].X).Distinct().Count()==2,
                    "Studio two-column automatic grid did not persist positions");
                Click(window,"水平分布");
                var spacedUI=MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id));
                Check(spacedUI.Positions.Count>=3&&spacedUI.Groups.Count==2,
                    "Studio distribution changed group identity");
                Click(window,"定位选中");
                Click(window,"适配画布");
                Check(flowCanvas.ZoomFactor>=0.25&&flowCanvas.ZoomFactor<=3,
                    "Studio zoom-to-fit went out of range");
                Click(window,"100%");
                Click(window,"对齐左侧");
                var alignedUI=MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id));
                Check(pastedIds.Select(id=>alignedUI.Positions[id].X).Distinct().Count()==1,
                    "Studio selected nodes were not aligned as one transaction");
                flowCanvas.UpdateLayout();
                var sample=CxVisual.Mechanism.Visual.MOLFlowLayout.Build(
                    flowCanvas.WiringDefinition).Single(x=>x.BlockId==pastedIds[0]);
                var dragStart=new Point(sample.X+sample.Width/2,sample.Y+12);
                Check(flowCanvas.BeginBlockDrag(dragStart),"group member not draggable");
                Check(flowCanvas.CompleteBlockDrag(new Point(dragStart.X+22,dragStart.Y+15)),
                    "group drag did not commit");
                var shiftedUI=MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id));
                Check(pastedIds.All(id=>
                    Math.Abs(shiftedUI.Positions[id].X-alignedUI.Positions[id].X-22)<0.001),
                    "group drag did not move all selected nodes in one edit");
                Click(window,"撤销编排");
                Check(pastedIds.All(id=>
                    Math.Abs(MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id))
                        .Positions[id].X-alignedUI.Positions[id].X)<0.001),
                    "group move Undo not atomic");
                Click(window,"重做编排");
                Click(window,"解除组合");
                Check(MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id))
                    .Groups.Count==1,"Studio ungroup failed");
                Click(window,"撤销编排");
                Check(MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id))
                    .Groups.Count==2,"Studio ungroup Undo failed");
                Click(window,"删除逻辑选区");
                Check(MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id))
                    .Blocks.Count==3,"Studio batch delete failed: count="+
                    MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id)).Blocks.Count+
                    " / selected="+string.Join(",",flowCanvas.SelectedBlockIds.Select(id=>id.ToString("N").Substring(0,7)))+
                    " / ui="+((TextBlock)window.FindName("LogicInfo")).Text);
                Click(window,"撤销编排");
                Check(MOLWiringCodec.Get(again.GetSnapshot().Find(active.RootContent.Id))
                    .Blocks.Count==6,"Studio batch delete undo failed");
                vm.GetType().GetMethod("SaveProject").Invoke(vm,new object[]{path});
                vm.GetType().GetMethod("OpenProject").Invoke(vm,new object[]{path,true});Pump();
                var finalSession=(MEMEngineeringSession)Get(vm,"ProjectSession");
                var finalWiring=MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id));
                Check(finalWiring.Blocks.Count==6&&finalWiring.Groups.Count==2&&
                    finalWiring.Links.Count==6,"Studio grouped paste lost Save/Open identity");
                // 删除全部节点仍是可保存V2草稿，不允许伪造成功编译。
                Click(window,"全选节点");
                Click(window,"删除逻辑选区");
                var blank=MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id));
                Check(blank!=null&&blank.Blocks.Count==0&&blank.EntryBlockId==Guid.Empty,
                    "Studio all-node delete did not persist a clean empty draft");
                Click(window,"验证工程编排");
                Check(((TextBlock)window.FindName("LogicInfo")).Text.Contains("MOC_WIRING_INVALID"),
                    "Studio allowed empty draft into MOC/MRK executable path");
                Click(window,"撤销编排");
                var restored=MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id));
                Check(restored.Blocks.Count==6&&restored.Groups.Count==2&&restored.Links.Count==6,
                    "Studio delete-all undo lost group/link identity");

                // G04-E：真实Studio节点属性与输入参数，验证原子更新与非法值回滚。
                flowCanvas.SetSelection(new[]{selectedIds[0]});
                var portName=(TextBox)window.FindName("FlowInspectorPortName");
                var portType=(TextBox)window.FindName("FlowInspectorType");
                var portValue=(TextBox)window.FindName("FlowInspectorValue");
                var inspectorPorts=(ComboBox)window.FindName("FlowInspectorPorts");
                portName.Text="Threshold";
                portType.Text="double";
                ((ComboBox)window.FindName("FlowInspectorDirection")).SelectedIndex=0;
                ((ComboBox)window.FindName("FlowInspectorKind")).SelectedIndex=0;
                Click(window,"添加Port");
                var withPort=MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id));
                Check(withPort.Ports[selectedIds[0]].Any(p=>p.MemberId=="Threshold"&&
                    p.Kind==MPCPortKind.Data&&p.Direction==MPCPortDirection.Input),
                    "Studio typed Data Input Port add did not commit");
                inspectorPorts.SelectedItem=inspectorPorts.Items.Cast<MPCPortDescriptor>()
                    .Single(p=>p.MemberId=="Threshold");
                Pump();
                portValue.Text="1.75";
                Click(window,"保存节点参数");
                var withValue=MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id));
                IReadOnlyDictionary<string,string> inspectorValues;
                string inspectorThreshold;
                Check(withValue.Parameters.TryGetValue(selectedIds[0],out inspectorValues)&&
                    inspectorValues.TryGetValue("Threshold",out inspectorThreshold)&&
                    inspectorThreshold=="1.75",
                    "Studio parameter value did not persist: "+
                    ((TextBlock)window.FindName("LogicInfo")).Text+
                    " / id="+selectedIds[0].ToString("N")+
                    " / present="+string.Join(",",withValue.Parameters.Keys));
                int revisionBeforeBad=finalSession.Revision;
                portValue.Text="invalid-double";
                Click(window,"保存节点参数");
                var failedInput=MOLWiringCodec.Get(
                    finalSession.GetSnapshot().Find(active.RootContent.Id));
                Check(finalSession.Revision==revisionBeforeBad&&
                    failedInput.Parameters.ContainsKey(selectedIds[0])&&
                    failedInput.Parameters[selectedIds[0]].ContainsKey("Threshold")&&
                    failedInput.Parameters[selectedIds[0]]["Threshold"]=="1.75",
                    "Studio invalid typed input altered engineering revision");
                Click(window,"撤销编排");
                Check(!MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id))
                    .Parameters.ContainsKey(selectedIds[0]),"Studio parameter Undo failed");
                Click(window,"重做编排");
                Check(MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id))
                    .Parameters[selectedIds[0]]["Threshold"]=="1.75",
                    "Studio parameter Redo failed");
                // G04-G：断点可用，编译失败从结构化诊断对象定位，不吞异常。
                Click(window,"切换断点");
                Click(window,"验证工程编排");
                var errors=(ComboBox)window.FindName("FlowDiagnosticList");
                Check(errors.Items.Count>0,"Studio compile diagnostics list was not populated");
                errors.SelectedIndex=0;
                Click(window,"定位诊断");
                Click(window,"切换断点");
                Click(window,"调试暂停");
                Check(((TextBlock)window.FindName("LogicInfo")).Text.Contains("没有活跃MRK"),
                    "Studio debug pause falsely reported an active flow");
                // G04-F：选择简单Control链后提取真正Content，且一个Undo回到原V2工程。
                Click(window,"创建自由连接示例");
                var beforeExtract=MOLWiringCodec.Get(
                    finalSession.GetSnapshot().Find(active.RootContent.Id));
                Check(beforeExtract.Blocks.Count==2&&beforeExtract.Links.Count==1,
                    "G04-F Studio setup missing a linear V2 Control flow");
                flowCanvas.SelectAll();
                Click(window,"提取可执行Content");
                var extractedRoot=finalSession.GetSnapshot().Find(active.RootContent.Id);
                var extractWiring=MOLWiringCodec.Get(extractedRoot);
                Check(extractWiring.Blocks.Count==1&&
                    extractWiring.Blocks[0].CapabilityId.StartsWith("moc.invoke:")&&
                    extractedRoot.References.Count==1,
                    "Studio extract did not create a referenced executable Content");
                Click(window,"撤销编排");
                Check(MOLWiringCodec.Get(finalSession.GetSnapshot().Find(active.RootContent.Id))
                    .Blocks.Count==2,
                    "Studio extract Content Undo did not restore original flow");

                // C同一工程验证：真实WPF按钮自动关联选中MOL节点到图元状态，
                // 创建三个MPM运行态定义，一次MEM事务，Undo/Redo与逻辑共用。
                var flowLamp=finalSession.AddShape((MEMContent)finalSession.Find(active.RootContent.Id),
                    "Flow Status Lamp",
                    MRCShapeDefinition.Circle(new FGEPoint2D(20,20),20),24,24);
                Guid sourceBlock=MOLWiringCodec.Get(finalSession.GetSnapshot()
                    .Find(active.RootContent.Id)).Blocks[0].Id;
                vm.GetType().GetMethod("Select").Invoke(vm,new object[]{(Guid?)flowLamp.Id});
                flowCanvas.SetSelection(new[]{sourceBlock});
                int beforeLink=finalSession.Revision;
                Click(window,"关联流程状态");
                string runningKey=assembly.GetType("CxVisual.Studio.MOLVisualTraceBindingResolver")
                    .GetMethod("VariableFor").Invoke(null,new object[]{sourceBlock,"Running"}) as string;
                Check(finalSession.Revision==beforeLink+1&&
                    MRCVisualAppearance.BehaviorOf(flowLamp).States.Any(s=>s.Name==runningKey),
                    "C Studio Flow->Visual linkage must use a single MEM edit");
                Click(window,"撤销编排");
                Check(!MRCVisualAppearance.BehaviorOf(flowLamp).States.Any(s=>s.Name==runningKey),
                    "C visual linkage ignored shared Undo");
                Click(window,"重做编排");
                Check(MRCVisualAppearance.BehaviorOf(flowLamp).States.Any(s=>s.Name==runningKey),
                    "C visual linkage ignored shared Redo");
                // 同一份工程定义进行C运行态预览，帧只变颜色不改变Revision。
                int runtimeRevision=finalSession.Revision;
                var bridgeType=assembly.GetType("CxVisual.Studio.MOLVisualTraceBindingResolver");
                var resolver=(CxVisual.Mechanism.Data.RuntimeData.IMDSExternalBindingResolver)
                    Activator.CreateInstance(bridgeType,new object[]{
                        new Func<Guid,string>(id=>id==sourceBlock?"Running":null),null});
                CxSceneItem runningScene=CxSceneProjection.Project(finalSession,
                    active.Id,resolver,20).First(x=>x.Id==flowLamp.Id);
                Check(runningScene.Style.Fill.Color.Equals(MRCColor.Parse("#FF2778D7"))&&
                    finalSession.Revision==runtimeRevision,
                    "C MRK trace state did not repaint WPF model without MEM edit");
                bool malformed=false;
                try{resolver.Resolve(CxVisual.Mechanism.Data.RuntimeData.MDSBindingSource.Variable,
                    "Flow.Block.bad.Running",typeof(bool));}
                catch(ArgumentException){malformed=true;}
                Check(malformed,"C malformed MRK reference escaped the binding boundary");
                // 故障追溯闭环：从现有MOL块直接反查已绑定的HMI图元并跳转视觉画布。
                vm.GetType().GetMethod("Select").Invoke(vm,new object[]{(Guid?)null});
                flowCanvas.SetSelection(new[]{sourceBlock});
                Click(window,"定位关联画面");
                Check(((Guid?)Get(vm,"SelectedId"))==flowLamp.Id&&
                    ((TabItem)window.FindName("VisualTab")).IsSelected,
                    "C trace -> linked HMI element navigation lost MEM identity");
                Check(finalSession.Revision==runtimeRevision,
                    "C diagnostic navigation must never commit engineering changes");

                // C综合Studio用户路径：新建页后点一次真实WPF按钮，
                // 同一MEM工程创建主流程、两份可运行Content引用和状态图元。
                Click(window,"添加页面");
                var xraySession=(MEMEngineeringSession)Get(vm,"ProjectSession");
                var xrayPage=xraySession.Project.Pages.Last();
                ((TabItem)window.FindName("FlowTab")).IsSelected=true;Pump();
                Click(window,"创建完整X-Ray双子流程");
                var xrayRoot=xraySession.GetSnapshot().Find(xrayPage.RootContent.Id);
                var xrayFlow=MOLWiringCodec.Get(xrayRoot);
                Check(xrayFlow!=null&&xrayFlow.Blocks.Count==5&&
                    xrayRoot.References.Count==2&&
                    xraySession.Project.Contents.Count(c=>c.Name.StartsWith("XRay-",StringComparison.Ordinal))==2,
                    "C Studio XRay didn't persist both executable child Contents");
                Check(xrayRoot.References.All(r=>
                    MOLWiringCodec.Get(xraySession.GetSnapshot().Find(r.TargetId)).Blocks.Count==2),
                    "C Studio XRay children not real Content Logic definitions");
                var xrayLamps=xrayPage.RootContent.DescendantsAndSelf()
                    .OfType<MRCVisualItem>()
                    .Where(x=>x.Name.Contains("运行状态")).ToArray();
                Check(xrayLamps.Length==2&&xrayLamps.All(x=>
                    MRCVisualAppearance.BehaviorOf(x).States.Count==3),
                    "C Studio XRay HMI states not connected to executable nodes");
                Check(((TextBlock)window.FindName("LogicInfo")).Text.Contains("MOC构建通过"),
                    "C Studio XRay Flow did not pass real MOC validation");
                Console.WriteLine("PASS H06 Studio real WPF automation: add/switch page, resource two instances, bound parameter edit, shared Undo/Redo, definition navigation, save/reopen");
            }
            finally{if(window!=null)window.Close();if(app!=null)app.Shutdown();if(File.Exists(path))File.Delete(path);if(File.Exists(path+".cx-lock"))File.Delete(path+".cx-lock");}
        }
    }
}
