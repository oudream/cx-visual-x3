using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Professional.Automation;
using CxVisual.Professional.Vision;
using CxVisual.Professional.CT3D;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Studio
{
    public partial class MainWindow : Window
    {
        private readonly StudioViewModel _viewModel;
        private readonly MOLRuntimeOverlay _logicOverlay=new MOLRuntimeOverlay();
        private MRKFlowHandle _logicFlow;
        private MRKExecutionDebugController _logicDebug;
        private readonly HashSet<Guid> _logicBreakpoints=new HashSet<Guid>();
        // 调试会话的限定执行路径，不参与MEM工程持久化。
        private readonly HashSet<string> _invocationBreakpoints=
            new HashSet<string>(StringComparer.Ordinal);
        private bool _logicRunning;
        private readonly System.Diagnostics.Stopwatch _visualClock=new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch _flowVisualClock=new System.Diagnostics.Stopwatch();
        private MEMEngineeringSession _renderAssetSession;
        private readonly DispatcherTimer _visualPreviewTimer=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(50)};

        private MOCWiringFragment _flowClipboard;
        private int _flowPasteCount;
        private bool _flowPanning;
        private Point _flowPanStart;
        private double _flowPanX,_flowPanY;
        private Guid? _logicSelected;
        private Guid? _pendingWireSource;
        private Guid? _pendingWireTarget;
        private readonly SimulationResolver _simulation=new SimulationResolver();
        // 仅保存当前Studio CPU仿真发布候选。MEM工程、MOC构建和MRK Slot仍是唯一Owner。
        private MOCProjectReleaseCandidate _preparedCpuRelease;
        private const string CpuReleasePrincipal="studio-cpu-simulation";

        private sealed class StudioCpuSoftwarePolicy : IMRKPlanPublicationPolicy
        {
            public bool Authorize(string principal,MRKPlanPublishAction action,
                MRKExecutionPlan plan,long revision)
            {return principal==CpuReleasePrincipal;}
            public bool IsSafeToActivate(MRKExecutionPlan plan,long revision)
            {return true;} // 仅内存CPU仿真；绝不可用作生产机台设备联锁策略。
        }
        /// <summary>
        /// 已有MOC源指纹、MRK授权Publisher和受保护Slot的CPU仿真交接。
        /// 不替代Runtime.Host生产授权，也不触发硬件设备。
        /// </summary>
        internal static MOCCompiledPlan StageCpuSoftwareRelease(
            MOCProjectReleaseCandidate candidate,MEMProjectSnapshot current,
            IMRKCapabilityResolver resolver,IReadOnlyDictionary<string,string> versions,
            IMRKPlanPublicationPolicy policy)
        {
            if(candidate==null)throw new ArgumentNullException("candidate");
            if(policy==null)throw new ArgumentNullException("policy");
            // 未授权的候选不可作为Slot的初始活动Plan，初始仅放置无外设惰性步骤。
            // 正式候选必须通过原MOC Stage和MRK授权Activate后才能成为活动版本。
            MRKExecutionPlan baseline=new MRKExecutionPlan("studio.cpu.inert.baseline",
                new[]{new MRKExecutionStep("inert",new string[0],
                    (context,token)=>Task.FromResult((object)0))});
            MRKExecutionPlanSlot slot=new MRKExecutionPlanSlot(baseline,0,true);
            MRKExecutionPlanPublisher publisher=new MRKExecutionPlanPublisher(slot,baseline,0,policy);
            MRKPlanPublicationReceipt receipt=MOCProjectRelease.Stage(candidate,
                current,resolver,versions,publisher,CpuReleasePrincipal,1);
            if(!receipt.Accepted||!publisher.TryActivate(CpuReleasePrincipal))
                throw new InvalidOperationException("studio.cpu_release.activation_denied");
            using(MRKExecutionPlanLease active=slot.Capture())
                if(!object.ReferenceEquals(active.Plan,candidate.Plan.Plan)||active.Revision!=1)
                    throw new InvalidOperationException("studio.cpu_release.plan_mismatch");
            return candidate.Plan;
        }
        private void OnPrepareFlowSoftwareRelease(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            _preparedCpuRelease=null;
            try
            {
                MEMPage page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择工程主流程页面");
                _preparedCpuRelease=MOCProjectRelease.Prepare(
                    _viewModel.ProjectSession.GetSnapshot(),page.RootContent.Id,
                    _simulation,_simulation.DescribeReleaseVersions());
                LogicInfo.Text="CPU仿真软件候选已锁定：Revision "+
                    _preparedCpuRelease.EngineeringRevision+"；能力版本 "+
                    _preparedCpuRelease.CapabilityVersions.Count+"；指纹 "+
                    _preparedCpuRelease.Fingerprint.Substring(0,12)+
                    "。下次仿真运行将走MOC→MRK授权Stage/Activate（非现场发布）。";
            }
            catch(Exception ex){LogicInfo.Text="CPU仿真软件候选拒绝："+ex.Message;}
        }
        private sealed class FlowDiagnosis
        {
            public Guid ContentId{get;set;}
            public Guid ProjectId{get;set;}
            public int EngineeringRevision{get;set;}
            public Guid? BlockId{get;set;}
            public Guid? LinkId{get;set;}
            public string Label{get;set;}
        }
        internal sealed class FlowTraceRecord
        {
            public string StepId{get;set;}
            public Guid BlockId{get;set;}
            public Guid ContentId{get;set;}
            public Guid ProjectId{get;set;}
            public int EngineeringRevision{get;set;}
            /// <summary>运行入口所在MEM主流程Content，定位不受后来页面切换影响。</summary>
            public Guid RootContentId{get;set;}
            /// <summary>原MRK Flow实例，不以相同StepId混同两次运行。</summary>
            public Guid FlowId{get;set;}
            public string SoftwareReleaseFingerprint{get;set;}
            public long PublicationRevision{get;set;}
            public IReadOnlyList<Guid> CallPath{get;set;}
            public string Label{get;set;}
        }
        /// <summary>使用真实MOC编译源与MRK观测记录创建同一个Studio轨迹投影。</summary>
        internal static FlowTraceRecord ProjectFlowTrace(MOCCompiledPlan compiled,
            MOCObservedStep observed,Guid rootContentId=default(Guid),Guid flowId=default(Guid),
            string releaseFingerprint=null,long publicationRevision=0)
        {
            if(compiled==null)throw new ArgumentNullException("compiled");
            if(observed==null||observed.Origin==null||observed.Transition==null)
                throw new ArgumentException("缺少已关联SourceMap的MRK轨迹","observed");
            return new FlowTraceRecord{
                StepId=observed.Transition.StepId,
                BlockId=observed.Origin.BlockId,
                ContentId=observed.Origin.ContentAddress.ObjectId,
                ProjectId=compiled.ProjectId,
                EngineeringRevision=compiled.EngineeringRevision,
                RootContentId=rootContentId,FlowId=flowId,
                SoftwareReleaseFingerprint=releaseFingerprint,
                PublicationRevision=publicationRevision,
                CallPath=observed.Origin.CallPath,
                Label=observed.Transition.State+" · "+observed.Origin.BlockId.ToString("N")
                    .Substring(0,8)+" · "+observed.Transition.StepId};
        }
        private sealed class FlowPortChoice
        {
            public string SourcePort{get;set;}
            public string TargetPort{get;set;}
            public MPCPortKind Kind{get;set;}
            public string Label{get;set;}
        }
        private sealed class FlowExistingLink
        {
            public Guid LinkId{get;set;}
            public string Label{get;set;}
        }
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new StudioViewModel();
            _viewModel.ConfirmReplace = ConfirmProjectReplacement;
            _viewModel.BeforeProjectReplacement = ConfirmStudioConfigReplacement;
            DataContext = _viewModel;
            LogicHost.BlockSelected += (sender, args) => { _logicSelected=args.BlockId; LogicInfo.Text = "已选逻辑块: " + args.BlockId.ToString("N"); };
            LogicHost.LinkRequested += OnWiringLinkRequested;
            LogicHost.PortLinkRequested += OnDirectPortLinkRequested;
            LogicHost.BlockMoveRequested += OnWiringBlockMoveRequested;
            LogicHost.BlocksMoveRequested += OnWiringBlocksMoved;
            LogicHost.SelectionChanged += (sender,args)=>{
                _logicSelected=args.BlockIds.Count==1?(Guid?)args.BlockIds[0]:null;
                LogicInfo.Text="选中 "+args.BlockIds.Count+" 个节点；Ctrl增减、空白框选、拖动批量移动。";
                RefreshFlowInspector();
            };
            PreviewKeyDown += OnFlowShortcuts;
            LogicHost.PortLinkRejected += (sender,args) =>
                LogicInfo.Text="拖线被拒绝："+args.Reason;
            SceneHost.NodeActivated += OnActivated;
            SceneHost.SelectionSetRequested += ids=>
            {
                _viewModel.SetSelection(ids);
                SceneHost.SetSelectedVisualIds(ids);
                RefreshVisualInspector();
                VisualInfo.Text="场景选中 "+ids.Count+" 个对象，可用原工程成组操作";
            };
            SceneHost.MoveRequested += OnMove;
            SceneHost.HandleEditRequested += (sender,edit)=>
            {
                try
                {
                    MTCEditResult<bool> result=_viewModel.EditVisualHandle(
                        edit.Handle,edit.WorldX,edit.WorldY);
                    VisualInfo.Text=result.Success?
                        "已修改"+edit.Handle.Kind+" | Revision "+result.Revision+
                            "（可撤销、重做并保存工程）":
                        "图元编辑被拒绝："+result.ErrorCode+" "+result.Message;
                }
                catch(Exception ex){VisualInfo.Text="图元编辑失败："+ex.Message;}
            };
            _visualPreviewTimer.Tick+=(sender,args)=>
            {
                if(SceneHost.IsDesignMode)return;
                try
                {
                    _viewModel.UpdateVisualRuntimeFrame(_visualClock.Elapsed.TotalMilliseconds);
                }
                catch(Exception ex)
                {
                    // 错误类型/外部数据失联不能使WPF UI线程崩溃；立即撤回预览。
                    _visualPreviewTimer.Stop();
                    _visualClock.Reset();
                    SceneHost.IsDesignMode=true;
                    VisualPreviewButton.Content="预览交互";
                    try{_viewModel.StopVisualRuntime();}
                    catch(Exception){/* 保留原始数据异常；不触发现场命令 */ }
                    VisualInfo.Text="动态可视预览已停止："+ex.Message+
                        "；请检查MDS绑定来源及数据类型。";
                }
            };

            SceneHost.WidgetEventRequested += (sender,widget) =>
            {
                try
                {
                    int approved=widget.Address!=null?
                        _viewModel.RouteVisualWidgetEvent(
                            widget.Address,widget.EventId,widget.Value):
                        _viewModel.RouteVisualWidgetEvent(
                            widget.ElementId,widget.EventId,widget.Value);
                    VisualInfo.Text="Widget事件："+widget.EventId+
                        " · 对象："+widget.ElementId.ToString("N").Substring(0,8)+
                        (approved>0?" · 已转交授权宿主":" · 无授权宿主，不执行设备命令");
                }
                catch(Exception ex){VisualInfo.Text="Widget事件被拒绝："+ex.Message;}
            };
            _viewModel.SceneChanged += (sender, args) => RefreshScene();
            RefreshScene();
            InitializeStudioShell();
        }
        private void OnActivated(object sender, CxNodeEventArgs args)
        { _viewModel.Select(args.Id); RefreshVisualInspector(); }
        private void OnMove(object sender,CxMoveEventArgs args)
        {
            IReadOnlyList<Guid> ids=SceneHost.SelectedVisualIds;
            if(ids.Count>1&&ids.Contains(args.Id))
            {
                MTCEditResult<bool> result=_viewModel.DragVisualSelection(ids,
                    args.WorldDeltaX,args.WorldDeltaY,SceneHost.SnapToGrid);
                VisualInfo.Text=result.Success?"整体拖动已提交一个可撤销事务":
                    result.ErrorCode+" "+result.Message;
            }
            else _viewModel.Drag(args.Id,args.X,args.Y);
        }
        private static double ReadVisualNumber(TextBox text)
        {
            return double.Parse(text.Text,System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        private void RefreshVisualInspector()
        {
            if(!_viewModel.SelectedId.HasValue)return;
            MRCVisualItem item=_viewModel.ProjectSession.Find(
                _viewModel.SelectedId.Value) as MRCVisualItem;
            if(item==null)return;
            VisualXInput.Text=item.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            VisualYInput.Text=item.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            VisualWidthInput.Text=item.Width.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            VisualHeightInput.Text=item.Height.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            VisualRotationInput.Text=item.Rotation.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            VisualLayerInput.Text=item.LayerId;
        }
        private void OnToggleVisualSnap(object sender,RoutedEventArgs args)
        {SceneHost.SnapToGrid=VisualGridSnap.IsChecked==true;}
        private void OnVisualZoom(object sender,RoutedEventArgs args)
        {
            string operation=((Button)sender).Tag.ToString();
            double zoom=operation=="reset"?1:
                Math.Max(0.05,Math.Min(16,SceneHost.Zoom*(operation=="in"?1.25:0.8)));
            SceneHost.SetViewport(zoom,0,0);
            VisualInfo.Text="画布缩放："+zoom.ToString("P0")+
                "；鼠标滚轮以指针为中心缩放，中键拖动画布";
        }
        private void OnAlignVisuals(object sender,RoutedEventArgs args)
        {
            try
            {
                string mode=((Button)sender).Tag.ToString();
                if(RouteStudioResourceVisualAlignment(mode))return;
                MTCEditResult<bool> result=_viewModel.AlignVisualSelection(mode);
                VisualInfo.Text=result.Success?"多选对齐/分布已提交（可撤销）":
                    "对齐被拒绝："+result.ErrorCode+" "+result.Message;
            }
            catch(Exception ex){VisualInfo.Text="对齐失败："+ex.Message;}
        }
        private void OnPathOperation(object sender,RoutedEventArgs args)
        {
            try
            {
                int index=int.Parse(PathCommandIndex.Text,
                    System.Globalization.CultureInfo.InvariantCulture);
                string op=((Button)sender).Tag.ToString();
                if(RouteStudioResourcePath(index,op))return;
                MTCEditResult<bool> result=_viewModel.EditVisualPath(index,op);
                VisualInfo.Text=result.Success?"路径操作已保存："+op:
                    result.ErrorCode+" "+result.Message;
            }
            catch(Exception ex){VisualInfo.Text="路径操作失败："+ex.Message;}
        }
        private void OnApplyVisualGeometry(object sender,RoutedEventArgs args)
        {
            try
            {
                double x=ReadVisualNumber(VisualXInput),y=ReadVisualNumber(VisualYInput);
                double width=ReadVisualNumber(VisualWidthInput);
                double height=ReadVisualNumber(VisualHeightInput);
                double angle=ReadVisualNumber(VisualRotationInput);
                if(RouteStudioResourceGeometry(x,y,width,height,angle))return;
                MTCEditResult<bool> result=_viewModel.EditVisualGeometry(
                    x,y,width,height,angle);
                VisualInfo.Text=result.Success?"已提交几何位置/尺寸/旋转":
                    result.ErrorCode+" "+result.Message;
            }
            catch(Exception ex){VisualInfo.Text="几何属性失败："+ex.Message;}
        }
        private void ApplyVisualLayer(bool toFront)
        {
            try
            {
                if(toFront&&RouteStudioResourceVisualZOrder(true))return;
                if(!toFront&&RouteStudioResourceLayer(VisualLayerInput.Text,false))return;
                MTCEditResult<bool> result=_viewModel.EditVisualLayer(VisualLayerInput.Text,toFront);
                VisualInfo.Text=result.Success?"图层修改已提交":
                    result.ErrorCode+" "+result.Message;
            }
            catch(Exception ex){VisualInfo.Text="图层编辑失败："+ex.Message;}
        }
        private void OnApplyVisualLayer(object sender,RoutedEventArgs args){ApplyVisualLayer(false);}

        private void OnSendVisualBack(object sender,RoutedEventArgs args)
        {
            try
            {
                if(RouteStudioResourceVisualZOrder(false))return;
                MTCEditResult<bool> result=_viewModel.SetSelectedZOrder(false);
                VisualInfo.Text=result.Success?"已置于同容器底层（可撤销）":
                    result.ErrorCode+" "+result.Message;
            }
            catch(Exception ex){VisualInfo.Text="置于底层失败："+ex.Message;}
        }        private void OnBringVisualFront(object sender,RoutedEventArgs args){ApplyVisualLayer(true);}
        private void OnInsertShape(object sender,RoutedEventArgs args)
        {
            try
            {
                var button=(System.Windows.Controls.Button)sender;
                string kind=button.Tag.ToString();
                if(RouteStudioResourceVisualInsertion(kind))return;
                if(kind=="Text")_viewModel.AddVisualText();
                else if(kind=="WidgetButton")_viewModel.AddVisualWidget();
                else if(kind.StartsWith("core.widget.",StringComparison.Ordinal))
                    _viewModel.AddVisualWidgetType(kind);
                else _viewModel.AddVisualShape((MRCShapeKind)Enum.Parse(typeof(MRCShapeKind),kind));
                VisualInfo.Text="已创建 "+button.Content+" | Revision "+_viewModel.Revision;
            }
            catch(Exception ex){VisualInfo.Text="创建失败: "+ex.Message;}
        }
        private void OnInsertVisualAsset(object sender,RoutedEventArgs args)
        {
            try
            {
                var dialog=new Microsoft.Win32.OpenFileDialog
                {
                    Filter="图片/SVG|*.png;*.jpg;*.jpeg;*.bmp;*.svg",
                    CheckFileExists=true
                };
                if(dialog.ShowDialog()!=true)return;
                if(RouteStudioResourceAsset(dialog.FileName))return;
                _viewModel.AddVisualAsset(dialog.FileName);
                VisualInfo.Text="工程资源已打包并绑定至当前图元，保存工程后无需原始外部文件";
            }
            catch(Exception ex){VisualInfo.Text="添加资源失败："+ex.Message;}
        }
        private void OnApplyWidgetSettings(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                MTCEditResult<bool> result=_viewModel.ConfigureVisualWidget(
                    WidgetContentInput.Text,WidgetOptionsInput.Text,
                    ReadVisualNumber(WidgetMinInput),ReadVisualNumber(WidgetMaxInput),
                    WidgetEnabledInput.IsChecked==true,WidgetTooltipInput.Text);
                VisualInfo.Text=result.Success?
                    "Widget控件属性已原子提交；撤销可一次恢复":
                    result.ErrorCode+": "+result.Message;
            }
            catch(Exception ex){VisualInfo.Text="Widget属性配置失败："+ex.Message;}
        }
        private void OnLinkVisualFlowState(object sender,RoutedEventArgs args)
        {
            if(_logicRunning){VisualInfo.Text="MRK运行中不可修改图元定义";return;}
            try
            {
                Guid selectedBlock=SelectedFlowBlock();
                _viewModel.LinkSelectedVisualToFlowBlock(selectedBlock);
                VisualInfo.Text="已建立同工程MOL节点状态关联：蓝色运行、绿色完成、红色失败。";
            }
            catch(Exception ex){VisualInfo.Text="关联流程状态失败："+ex.Message;}
        }
        private void OnApplyVisualBinding(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                _viewModel.BindVisualVariable(VisualBindingPropertyInput.Text,
                    VisualBindingSourceInput.Text);
                VisualInfo.Text="已保存MDS变量引用；附加运行数据源后预览会实际更新";
            }
            catch(Exception ex){VisualInfo.Text="绑定失败："+ex.Message;}
        }
        private void OnAddVisualState(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                _viewModel.AddVisualState(VisualStateInput.Text,VisualStateColorInput.Text);
                VisualInfo.Text="已保存状态样式，运行态由MDS布尔变量触发";
            }
            catch(Exception ex){VisualInfo.Text="添加状态失败："+ex.Message;}
        }
        private void OnApplyVisualAnimation(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                _viewModel.AddVisualAnimation(VisualAnimationProperty.Text,
                    ReadVisualNumber(VisualAnimationFrom),ReadVisualNumber(VisualAnimationTo),
                    int.Parse(VisualAnimationDuration.Text,
                        System.Globalization.CultureInfo.InvariantCulture));
                VisualInfo.Text="动画参数已保存，可点击预览交互查看";
            }
            catch(Exception ex){VisualInfo.Text="动画参数失败："+ex.Message;}
        }
        private void OnApplyFullStyle(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                _viewModel.SetFullVisualStyle(VisualStrokeColor.Text,
                    ReadVisualNumber(VisualOpacityInput),ReadVisualNumber(VisualFontSizeInput));
                VisualInfo.Text="字体、描边、不透明度已保存";
            }
            catch(Exception ex){VisualInfo.Text="完整样式失败："+ex.Message;}
        }
        private void OnAddBreathingAnimation(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                _viewModel.AddSelectedBreathingAnimation();
                VisualInfo.Text="透明度动画已保存：点击预览交互查看效果；撤销可恢复定义。";
            }
            catch(Exception ex){VisualInfo.Text="添加动画失败："+ex.Message;}
        }
        private void OnToggleVisualPreview(object sender,RoutedEventArgs args)
        {
            if(_logicRunning){VisualInfo.Text="正在MRK运行，由MRK轨迹统一刷新画面";return;}
            SceneHost.CancelInteraction();
            bool entering=SceneHost.IsDesignMode;
            SceneHost.IsDesignMode=!entering;
            var button=(System.Windows.Controls.Button)sender;
            if(entering)
            {
                _visualClock.Restart();
                _visualPreviewTimer.Start();
                button.Content="返回设计";
                VisualInfo.Text="运行预览：动画随帧采样；未连接的数据仍显示工程默认值。设备命令不会直接执行。";
            }
            else
            {
                _visualPreviewTimer.Stop();
                _visualClock.Reset();
                _viewModel.StopVisualRuntime();
                button.Content="预览交互";
                VisualInfo.Text="设计模式：工程属性与几何编辑已恢复，运行态值不写入工程。";
            }
        }
        private void OnApplyVisualStyle(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                _viewModel.SetSelectedFillColor(FillColorInput.Text,
                    double.Parse(StrokeWidthInput.Text,System.Globalization.CultureInfo.InvariantCulture));
                VisualInfo.Text="图元样式已提交，可使用撤销/重做和保存工程。";
            }
            catch(Exception ex){VisualInfo.Text="设置样式失败: "+ex.Message;}
        }
        private void OnBindVisualEvent(object sender,RoutedEventArgs args)
        {
            try
            {
                if(!EnsureStudioVisualInspectorTarget())return;
                _viewModel.AppendSelectedEventAction(EventIdInput.Text,ActionIdInput.Text);
                VisualInfo.Text="事件定义已保存；命令实际执行由运行体系和专业权限负责。";
            }
            catch(Exception ex){VisualInfo.Text="事件定义失败: "+ex.Message;}
        }
        private void OnVisualSelectionChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs args)
        {if(_viewModel==null||_viewModel.IsRefreshing)return;var list=(System.Windows.Controls.ListBox)sender;_viewModel.SetSelection(list.SelectedItems.OfType<CxVisual.Mechanism.Visual.CxSceneItem>().Select(i=>i.Id));}
        private bool ConfirmProjectReplacement(MEMEngineeringSession session)
        {
            var choice=MessageBox.Show(this,"当前工程有未保存修改。是否先保存？","工程修改",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
            if(choice==MessageBoxResult.Cancel)return false;if(choice==MessageBoxResult.No)return true;_viewModel.SaveCommand.Execute(null);return !session.IsDirty;
        }
        private bool ConfirmStudioConfigReplacement()
        {
            if(!HasStudioUnsavedConfiguration())return true;
            return MessageBox.Show(this,
                "当前专业配置尚有未保存的草稿。切换工程将失效并放弃这些修改，是否继续？",
                "CxVisual专业配置",MessageBoxButton.YesNo,MessageBoxImage.Warning)==
                MessageBoxResult.Yes;
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            bool automated=Environment.GetCommandLineArgs().Contains(
                "--shell-self-test",StringComparer.OrdinalIgnoreCase);
            if(!automated&&!ConfirmStudioConfigReplacement())e.Cancel=true;
            if(!e.Cancel&&!automated&&_viewModel.ProjectSession.IsDirty&&
                (_viewModel.ConfirmReplace==null||
                 !_viewModel.ConfirmReplace(_viewModel.ProjectSession)))e.Cancel=true;
            OnStudioShellClosing(e);
            base.OnClosing(e);
        }
        private const string ConfigSamplesFolder = "CxVisual";

        private static string GetDemoDataRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ConfigSamplesFolder);
        }

        private bool EditConfig<T>() where T : new()
        {
            string root = Path.Combine(GetDemoDataRoot(), "Configs", "V14");
            MHSConfigService service = new MHSConfigService(root);
            MHSConfigSection<T> section = service.ForMachine("StudioDemo").Open<T>();
            return MPWConfigEditorDialog.Edit(this, section);
        }
        private void OnConfigDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                bool saved;
                switch (ConfigDemoKind.SelectedIndex)
                {
                    case 1: saved = EditConfig<CxAutomationDeviceOptions>(); break;
                    case 2: saved = EditConfig<CxVisionInspectionOptions>(); break;
                    case 3: saved = EditConfig<CxCtReconstructionOptions>(); break;
                    case 4: saved = EditConfig<StudioRichConfig>(); break;
                    default: saved = EditConfig<StudioDemoConfig>(); break;
                }
                if (saved) MessageBox.Show(this,
                    "配置已保存。生效版本可在配置编辑器中查看并按适当边界生效。",
                    "配置 V2.4", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "配置编辑失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Legacy L4 release APIs have been deleted; do not pretend publishing is implemented by L1 storage.
        private void OnPublishConfigDemo(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "跨机台发布仍属于后续范围；本窗口只处理当前配置服务和生效边界。",
                "发布尚未接入", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void OnRollbackConfigDemo(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "请点击“编辑所选配置”，在“版本、历史与诊断”内选择旧版本并恢复为新的 Saved 修订。",
                "回滚尚未接入", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private static MVBConfigLayerSpec[] DemoLayers()
        {
            return new[] {
                new MVBConfigLayerSpec("Template", "template", "Standard", 10),
                new MVBConfigLayerSpec("Machine", "machine", "StudioDemo", 20),
                new MVBConfigLayerSpec("Product", "product", "XRaySample", 30),
                new MVBConfigLayerSpec("Override", "runtime", "DebugSession", 40)
            };
        }
        private void OnAtomicLayerDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new MHSConfigService(Path.Combine(GetDemoDataRoot(), "Configs", "V24", "Layered"));
                var profile = new MVBConfigAtomicLayerProfile<StudioRichConfig>(
                    service, "StudioAtomicRecipe", DemoLayers());
                MPWConfigLayerDialog.Show(this, profile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开原子层级配置：" + ex.GetType().Name,
                    "配置 V2.4", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void OnIndependentLayerDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new MHSConfigService(Path.Combine(GetDemoDataRoot(), "Configs", "V24", "Layered"));
                var profile = new MVBConfigLayeredProfile<StudioRichConfig>(
                    service, "StudioIndependentRecipe", DemoLayers());
                MPWConfigLayerDialog.Show(this, profile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开独立层级配置：" + ex.GetType().Name,
                    "配置 V2.4", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void OnMultiModelConfigDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new MHSConfigService(Path.Combine(GetDemoDataRoot(),
                    "Configs", "V24", "AtomicGroups"));
                MHSConfigAtomicGroup group = service.OpenAtomicGroup("Studio.UnifiedModels");
                group.Register<StudioDemoConfig>("Workspace");
                group.Register<StudioRichConfig>("Process");
                group.Seal();
                MPWConfigAtomicGroupDialog.Show(this, group);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "多模型原子配置编辑器未启动：" + ex.GetType().Name,
                    "配置 V2.4", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void OnEmbeddedConfigDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new MHSConfigService(Path.Combine(GetDemoDataRoot(), "Configs", "V24", "Embedded"));
                var section = service.ForMachine("StudioEmbedded").Open<StudioRichConfig>();
                var editor = new MPWConfigEditorControl<StudioRichConfig>(section);
                Window host = new Window { Owner = this, Title = "配置编辑器 · 可嵌入宿主",
                    Width = 1000, Height = 750, Content = editor,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner };
                host.Closed += (s, args) => editor.Dispose();
                host.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "配置嵌入演示未启动：" + ex.GetType().Name,
                    "配置 V2.4", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        protected override void OnClosed(System.EventArgs e)
        {
            if(_logicFlow!=null)_logicFlow.RequestCancel();
            _visualPreviewTimer.Stop();
            _visualClock.Stop();
            _viewModel.Dispose();
            base.OnClosed(e);
        }
        private void RefreshScene()
        {
            if(!object.ReferenceEquals(_renderAssetSession,_viewModel.ProjectSession))
            {
                _renderAssetSession=_viewModel.ProjectSession;
                SceneHost.SetAssetResolver(id=>_viewModel.ProjectSession.OpenAsset(id));
            }
            SceneHost.SetScene(_viewModel.Items, _viewModel.SelectedId);
            if(_logicRunning||_visualPreviewTimer.IsEnabled)return;
            MEMPage page=_viewModel.ActivePage;
            MEMNodeSnapshot node=page==null?null:
                _viewModel.ProjectSession.GetSnapshot().Find(page.RootContent.Id);
            LogicHost.WiringDefinition=node==null?null:MOLWiringCodec.Get(node);
            LogicHost.Definition=node==null||LogicHost.WiringDefinition!=null?null:
                MOLLogicCodec.Get(node);
            LogicHost.RuntimeStates=null;
            FlowExistingLinks.ItemsSource=LogicHost.WiringDefinition==null?null:
                LogicHost.WiringDefinition.Links.Select(l=>new FlowExistingLink{
                    LinkId=l.Id,
                    Label=l.Kind+": "+l.Source.BlockId.ToString("N").Substring(0,6)+"."+
                        l.Source.PortMemberId+" → "+l.Target.BlockId.ToString("N").Substring(0,6)+"."+
                        l.Target.PortMemberId}).ToList();
            FlowPortPairs.ItemsSource=null;
            _pendingWireSource=null;
            _pendingWireTarget=null;
            _logicSelected=null;
            LogicHost.ResetLinkSelection();
            LogicHost.RetainValidSelection();
            RefreshFlowInspector();
        }
        private void OnCreateLogicDemo(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                MEMPage page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先创建或选择页面");
                var model=new MOLLogicDefinition(MOLControlBlock.Sequence(
                    MOLControlBlock.Action("Acquire"),
                    MOLControlBlock.Parallel(MOLControlBlock.Action("AI"),MOLControlBlock.Action("CV")),
                    MOLControlBlock.If(MOLExpression.InputTrue("accepted"),
                        MOLControlBlock.Action("OutputOK"),MOLControlBlock.Action("OutputNG"))));
                MOLLogicCodec.Set(_viewModel.ProjectSession,page.RootContent,model);
                LogicHost.WiringDefinition=null;
                LogicHost.Definition=model;
                LogicHost.RuntimeStates=null;
                LogicInfo.Text="示例逻辑已通过工程Session提交到Content；可随工程保存、撤销和重开。";
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"创建逻辑失败");}
        }

        /// <summary>同一个MEM工程中创建实际X-Ray主流程、两段可执行Content、HMI状态灯。</summary>
        private void OnCreateXRayDualSubflowDemo(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=GetFlowPage();
                var session=_viewModel.ProjectSession;
                var current=session.GetSnapshot().Find(page.RootContent.Id);
                if(MOLWiringCodec.Get(current)!=null||MOLLogicCodec.Get(current)!=null)
                    throw new InvalidOperationException("当前页面已有编排，请先新建页面；不能静默覆盖现有流程");
                Guid acquire=Guid.Empty,ai=Guid.Empty,aiPost=Guid.Empty,cv=Guid.Empty,
                    cvPost=Guid.Empty,judge=Guid.Empty,result=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>
                {
                    acquire=b.Step("Acquire");
                    ai=b.Step("AI");
                    aiPost=b.Step("AIReview");
                    cv=b.Step("CV");
                    cvPost=b.Step("CVReview");
                    judge=b.Step("Judge");
                    result=b.Step("OutputOK");
                    Guid[] ids={acquire,ai,aiPost,cv,cvPost,judge,result};
                    for(int i=1;i<ids.Length;i++)
                    {
                        b.Connect(ids[i-1],"Done",ids[i],"Start");
                        b.Move(ids[i],30+i*170,40+(i%2)*50);
                    }
                });
                var aiContent=MOCFlowBuilder.ExtractLinearContent(
                    session,page.RootContent,new[]{ai,aiPost},
                    "XRay-AI检测子流程");
                var cvContent=MOCFlowBuilder.ExtractLinearContent(
                    session,page.RootContent,new[]{cv,cvPost},
                    "XRay-传统算法子流程");
                // 不创建第二份设备模型。HMI图元在同一Page的MEM节点树内。
                var aiLamp=session.AddRectangle(page.RootContent,"AI检测运行状态",35,255,125,58);
                var cvLamp=session.AddRectangle(page.RootContent,"传统算法运行状态",215,255,125,58);
                session.SetVisualStyle(aiLamp,MRCVisualStyle.Solid(MRCColor.Parse("#FF7A8394")));
                session.SetVisualStyle(cvLamp,MRCVisualStyle.Solid(MRCColor.Parse("#FF7A8394")));
                BindFlowExampleState(session,aiLamp,ai);
                BindFlowExampleState(session,cvLamp,cv);
                var build=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,_simulation);
                RefreshScene();
                LogicInfo.Text="同一工程已创建采图、AI/传统算法双可执行Content、判定及两个HMI状态灯；"+
                    "主流程5节点、子流程各2节点；"+
                    (build.Succeeded?"MOC构建通过，可点击仿真执行并追踪":
                        "MOC构建失败："+FormatBuildDiagnostics(build));
            }
            catch(Exception ex){LogicInfo.Text="创建完整X-Ray示例失败："+ex.Message;}
        }
        private static void BindFlowExampleState(MEMEngineeringSession session,
            MRCVisualItem item,Guid block)
        {
            string[] names={"Running","Completed","Failed"};
            string[] colors={"#FF2778D7","#FF16A34A","#FFDC2626"};
            var states=new List<MRCVisualStateDefinition>();
            for(int i=0;i<names.Length;i++)
                states.Add(new MRCVisualStateDefinition(
                    MOLVisualTraceBindingResolver.VariableFor(block,names[i]),
                    MRCVisualStyle.Solid(MRCColor.Parse(colors[i]))));
            session.SetVisualBehavior(item,new MRCVisualBehaviorDefinition(states:states));
        }

        private void OnCreateWiringDemo(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择页面");
                MEMNodeSnapshot node=_viewModel.ProjectSession.GetSnapshot().Find(page.RootContent.Id);
                if(MOLWiringCodec.Get(node)==null&&MOLLogicCodec.Get(node)!=null&&MessageBox.Show(this,
                    "将以V2自由连线替换本Content现有V1示例。继续？","替换编排",
                    MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
                var a=MOLControlBlock.Action("Acquire");
                var b=MOLControlBlock.Action("AI");
                var ports=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>{
                    {a.Id,new[]{
                        new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control),
                        new MPCPortDescriptor("Image",MPCPortDirection.Output,"string",MPCPortKind.Data)}},
                    {b.Id,new[]{
                        new MPCPortDescriptor("Start",MPCPortDirection.Input,"signal",MPCPortKind.Control),
                        new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control),
                        new MPCPortDescriptor("Image",MPCPortDirection.Input,"string",MPCPortKind.Data)}}
                };
                var wiring=new MOLWiringDefinition(a.Id,new[]{a,b},ports,new[]{
                    new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(a.Id,"Done"),
                        new MOLBlockEndpoint(b.Id,"Start"),MPCPortKind.Control)
                });
                MOLWiringCodec.Set(_viewModel.ProjectSession,page.RootContent,wiring);
                RefreshScene();
                LogicInfo.Text="V2自由连线已保存到同一Content，支持添加能力、连接与CPU仿真。";
            }
            catch(Exception ex){LogicInfo.Text="创建自由连线失败: "+ex.Message;}
        }
        private void OnAddWiringAction(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            if(RouteStudioResourceFlowCommand("action"))return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择页面");
                string capability=FlowActionCapability.Text==null?"":FlowActionCapability.Text.Trim();
                var found=CurrentCatalog().Find(capability);
                if(found==null)throw new InvalidOperationException("能力尚未注册，请先在工具箱搜索并选择。");
                if(!found.RuntimeAvailable)
                    throw new InvalidOperationException("当前Provider未提供此能力的执行实现。");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>b.Step(found.Definition));
                RefreshScene();
                LogicInfo.Text="已插入未连线的类型化能力节点；请选取两个节点并选择端口连接。";
            }
            catch(Exception ex){LogicInfo.Text="添加能力失败: "+ex.Message;}
        }
        /// <summary>
        /// R07：Studio与开发者走同一MOC事务。只接受两个以上已有分支，
        /// 所有连接在一次MEM编辑中完成；错误来源不会产生孤立Join。
        /// Join在已有V2 Control边上由MOC编译为多依赖MRK Step。
        /// </summary>
        internal static Guid InsertSelectedJoin(MEMEngineeringSession session,
            MEMContent content, MOCCapabilityDescriptor capability,
            IEnumerable<Guid> branchIds)
        {
            if(session==null||content==null||capability==null||branchIds==null)
                throw new ArgumentNullException("session/content/capability/branchIds");
            Guid[] branches=branchIds.ToArray();
            if(branches.Length<2||branches.Any(id=>id==Guid.Empty)||
                branches.Distinct().Count()!=branches.Length)
                throw new InvalidOperationException("studio.join.requires_distinct_branches");
            Guid joined=Guid.Empty;
            MOCFlowBuilder.Edit(session,content,b=>{
                foreach(Guid branch in branches)
                {
                    var outputs=b.GetPorts(branch);
                    if(!outputs.Any(p=>p.MemberId=="Done" &&
                        p.Kind==MPCPortKind.Control &&
                        p.Direction==MPCPortDirection.Output))
                        throw new InvalidOperationException("studio.join.branch_without_done:"+
                            branch.ToString("N"));
                }
                joined=b.Step(capability);
                foreach(Guid branch in branches)
                    b.Connect(branch,"Done",joined,"Start",MPCPortKind.Control);
            });
            return joined;
        }

        private void OnInsertSelectedJoin(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            if(RouteStudioResourceFlowCommand("join"))return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择页面");
                var found=CurrentCatalog().Find("Judge");
                if(found==null||!found.RuntimeAvailable)
                    throw new InvalidOperationException("Join后继判断能力不可用");
                Guid[] selected=LogicHost.SelectedBlockIds.ToArray();
                Guid join=InsertSelectedJoin(_viewModel.ProjectSession,
                    page.RootContent,found.Definition,selected);
                RefreshScene();
                LogicHost.SetSelection(new[]{join});
                LogicInfo.Text="已将 "+selected.Length+
                    " 条分支接入同一个Join（Control依赖）；按原Content保存，可撤销、重开、编译和仿真。";
            }
            catch(Exception ex){LogicInfo.Text="插入Join失败："+ex.Message;}
        }

        private MOCCapabilityCatalog CurrentCatalog()
        {
            return new MOCCapabilityCatalog(new IMOCCapabilityProvider[]{_simulation},_simulation);
        }
        private void OnSearchCapabilities(object sender,RoutedEventArgs args)
        {
            try
            {
                var entries=CurrentCatalog().Search(FlowCapabilitySearch.Text);
                FlowCapabilityResults.ItemsSource=entries;
                LogicInfo.Text="找到 "+entries.Count+" 项已注册能力（灰度及授权状态以实际Provider为准）。";
            }
            catch(Exception ex){LogicInfo.Text="搜索失败: "+ex.Message;}
        }
        private void OnCapabilitySelected(object sender,System.Windows.Controls.SelectionChangedEventArgs args)
        {
            var item=FlowCapabilityResults.SelectedItem as MOCCapabilityCatalogItem;
            if(item!=null)
            {
                FlowActionCapability.Text=item.Definition.Id;
                LogicInfo.Text=item.Definition.Name+" · "+item.Definition.Category+
                    " · 契约V"+item.Definition.Version+
                    (item.RuntimeAvailable?"（已提供运行实现）":"（暂无运行实现）");
            }
        }
        private void OnToggleWiring(object sender,RoutedEventArgs args)
        {
            if(RouteStudioResourceFlowCommand("wiring"))return;
            LogicHost.IsConnectMode=!LogicHost.IsConnectMode;
            LogicHost.ResetLinkSelection();
            FlowWireMode.Content=LogicHost.IsConnectMode?"结束连线":"开始连线";
            LogicInfo.Text=LogicHost.IsConnectMode?
                "依次点击来源和目标块，随后在端口列表选择合法端口对并确认。":"已退出连线模式。";
        }
        // P0收口：固定主流程与独立子流程共用同一属性面板，
        // 但Selection/Content必须来自当前活动Document，而非旧ActivePage。
        private MOLFlowCanvas ActiveFlowCanvas()
        {
            Guid resourceId;string kind;
            if(TryParseResourceDocumentId(_activeStudioDocumentId,out resourceId,out kind))
            {
                ResourceDocumentContext context=ActiveStudioResourceContext();
                if(kind!="flow"||context==null||!context.IsFlow||
                    !ResourceDocumentExists(_activeStudioDocumentId)||
                    !ReferenceEquals(context.Session,_viewModel.ProjectSession))
                    return null;
                return context.Flow;
            }
            return _activeStudioDocumentId=="doc.flow"?LogicHost:null;
        }
        private MEMContent ActiveFlowContent()
        {
            Guid resourceId;string kind;
            if(TryParseResourceDocumentId(_activeStudioDocumentId,out resourceId,out kind))
            {
                if(kind!="flow"||ActiveFlowCanvas()==null)
                    throw new InvalidOperationException("当前Tab不是有效流程编辑器");
                MEMContent content=_viewModel.ProjectSession.Find(resourceId) as MEMContent;
                if(content==null)throw new InvalidOperationException("源流程已删除");
                return content;
            }
            if(_activeStudioDocumentId!="doc.flow")
                throw new InvalidOperationException("请先激活流程编辑器");
            return GetFlowPage().RootContent;
        }
        private Guid SelectedFlowBlock()
        {
            MOLFlowCanvas canvas=ActiveFlowCanvas();
            if(canvas==null||canvas.WiringDefinition==null||
                canvas.SelectedBlockIds.Count!=1)
                throw new InvalidOperationException("请在当前流程Tab选择唯一V2逻辑节点");
            return canvas.SelectedBlockIds[0];
        }
        private void RefreshFlowInspector()
        {
            MOLFlowCanvas canvas=ActiveFlowCanvas();
            var wiring=canvas==null?null:canvas.WiringDefinition;
            FlowPublicPortSummary.Text=wiring==null?"未声明公开端口":
                "Input: "+string.Join(",",wiring.FlowInputs.Keys.OrderBy(x=>x))+
                " · Output: "+string.Join(",",wiring.FlowOutputs.Keys.OrderBy(x=>x));
            Guid id=canvas!=null&&canvas.SelectedBlockIds.Count==1?
                canvas.SelectedBlockIds[0]:Guid.Empty;
            var block=wiring==null?null:wiring.Blocks.FirstOrDefault(b=>b.Id==id);
            FlowInspectorCapability.Text=block==null?"":block.CapabilityId;
            FlowInspectorPorts.ItemsSource=block==null?null:wiring.Ports[id];
            if(block!=null&&wiring.Ports[id].Count>0)FlowInspectorPorts.SelectedIndex=0;
            else FlowInspectorValue.Text="";
        }
        private void OnFlowInspectorPortSelected(object sender,
            System.Windows.Controls.SelectionChangedEventArgs args)
        {
            var port=FlowInspectorPorts.SelectedItem as MPCPortDescriptor;
            if(port==null)return;
            FlowInspectorPortName.Text=port.MemberId;
            FlowInspectorType.Text=port.TypeId;
            FlowInspectorDirection.SelectedIndex=port.Direction==MPCPortDirection.Output?1:0;
            FlowInspectorKind.SelectedIndex=port.Kind==MPCPortKind.Control?1:0;
            FlowInspectorRequired.IsChecked=port.Required;
            MOLFlowCanvas canvas=ActiveFlowCanvas();
            var wiring=canvas==null?null:canvas.WiringDefinition;
            Guid id=canvas!=null&&canvas.SelectedBlockIds.Count==1?
                canvas.SelectedBlockIds[0]:Guid.Empty;
            IReadOnlyDictionary<string,string> values;
            string value;
            FlowInspectorValue.Text=wiring!=null&&wiring.Parameters.TryGetValue(id,out values)&&
                values.TryGetValue(port.MemberId,out value)?value:"";
        }
        private MPCPortDescriptor ReadFlowPort()
        {
            var direction=(MPCPortDirection)Enum.Parse(typeof(MPCPortDirection),
                ((System.Windows.Controls.ComboBoxItem)FlowInspectorDirection.SelectedItem).Content.ToString());
            var kind=(MPCPortKind)Enum.Parse(typeof(MPCPortKind),
                ((System.Windows.Controls.ComboBoxItem)FlowInspectorKind.SelectedItem).Content.ToString());
            return new MPCPortDescriptor(FlowInspectorPortName.Text.Trim(),direction,
                FlowInspectorType.Text.Trim(),kind,MPCPortMultiplicity.One,null,false,
                FlowInspectorRequired.IsChecked==true);
        }
        private void ApplyFlowProperty(Action<MOCFlowBuilder> edit,string message)
        {
            if(_logicRunning)return;
            try
            {
                MEMContent current=ActiveFlowContent();
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,current,edit);
                if(_activeStudioDocumentId=="doc.flow")RefreshScene();
                else
                {
                    MOLFlowCanvas flow=ActiveFlowCanvas();
                    if(flow!=null)
                    {
                        MEMNodeSnapshot snapshot=_viewModel.ProjectSession.GetSnapshot().Find(current.Id);
                        flow.WiringDefinition=MOLWiringCodec.Get(snapshot);
                        flow.RetainValidSelection();
                    }
                }
                RefreshFlowInspector();
                LogicInfo.Text=message;
            }
            catch(Exception ex){LogicInfo.Text="属性编辑拒绝："+ex.Message;}
        }
        private MEMPage GetFlowPage()
        {
            if(_viewModel.ActivePage==null)throw new InvalidOperationException("请先选择页面");
            return _viewModel.ActivePage;
        }
        private void OnFlowExtractContent(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=GetFlowPage();
                Guid[] selected=LogicHost.SelectedBlockIds.ToArray();
                if(selected.Length<2)
                    throw new InvalidOperationException("至少选择两个连续Control节点");
                var content=MOCFlowBuilder.ExtractLinearContent(_viewModel.ProjectSession,
                    page.RootContent,selected,FlowExtractName.Text.Trim());
                RefreshScene();
                var call=LogicHost.WiringDefinition.Blocks.Single(b=>
                    b.CapabilityId=="moc.invoke:"+content.Id.ToString("N"));
                LogicHost.SetSelection(new[]{call.Id});
                LogicInfo.Text="已在单次事务中提取可执行Content："+content.Name+
                    "；保留原Control连线，跨子流程Data映射尚未支持。";
            }
            catch(Exception ex){LogicInfo.Text="提取Content被拒绝："+ex.Message;}
        }
        private void OnFlowSetCapability(object sender,RoutedEventArgs args)
        {
            try
            {
                Guid id=SelectedFlowBlock();
                var entry=CurrentCatalog().Find(FlowInspectorCapability.Text.Trim());
                if(entry==null||!entry.RuntimeAvailable)
                    throw new InvalidOperationException("能力Provider尚未注册或不可执行");
                ApplyFlowProperty(b=>b.SetCapability(id,entry.Definition.Id),"节点能力ID已更新");
            }
            catch(Exception ex){LogicInfo.Text="能力修改拒绝："+ex.Message;}
        }
        private void OnFlowSetEntry(object sender,RoutedEventArgs args)
        {
            try{Guid id=SelectedFlowBlock();ApplyFlowProperty(b=>b.SetEntry(id),"流程入口已更新");}
            catch(Exception ex){LogicInfo.Text=ex.Message;}
        }
        private void OnFlowAddPort(object sender,RoutedEventArgs args)
        {
            try{Guid id=SelectedFlowBlock();var port=ReadFlowPort();
                ApplyFlowProperty(b=>b.AddPort(id,port),"已添加类型化Port");}
            catch(Exception ex){LogicInfo.Text="端口编辑拒绝："+ex.Message;}
        }
        private void OnFlowUpdatePort(object sender,RoutedEventArgs args)
        {
            try{Guid id=SelectedFlowBlock();var port=ReadFlowPort();
                ApplyFlowProperty(b=>b.ReplacePort(id,port),"Port契约已更新");}
            catch(Exception ex){LogicInfo.Text="端口编辑拒绝："+ex.Message;}
        }
        private void OnFlowRemovePort(object sender,RoutedEventArgs args)
        {
            try{Guid id=SelectedFlowBlock();string name=FlowInspectorPortName.Text.Trim();
                ApplyFlowProperty(b=>b.RemovePort(id,name),"已删除未连线Port");}
            catch(Exception ex){LogicInfo.Text="端口删除拒绝："+ex.Message;}
        }
        private void OnFlowExposeInput(object sender,RoutedEventArgs args)
        {
            try
            {
                Guid id=SelectedFlowBlock();
                var port=FlowInspectorPorts.SelectedItem as MPCPortDescriptor;
                if(port==null||port.Kind!=MPCPortKind.Data||
                   port.Direction!=MPCPortDirection.Input)
                    throw new InvalidOperationException("请选择一个已有Data Input端口");
                string alias=FlowPublicPortName.Text.Trim();
                ApplyFlowProperty(b=>b.ExposeInput(alias,id,port.MemberId),
                    "已公开子流程输入: "+alias);
            }
            catch(Exception ex){LogicInfo.Text="公开Input拒绝: "+ex.Message;}
        }
        private void OnFlowExposeOutput(object sender,RoutedEventArgs args)
        {
            try
            {
                Guid id=SelectedFlowBlock();
                var port=FlowInspectorPorts.SelectedItem as MPCPortDescriptor;
                if(port==null||port.Kind!=MPCPortKind.Data||
                   port.Direction!=MPCPortDirection.Output)
                    throw new InvalidOperationException("请选择一个已有Data Output端口");
                string alias=FlowPublicPortName.Text.Trim();
                ApplyFlowProperty(b=>b.ExposeOutput(alias,id,port.MemberId),
                    "已公开子流程输出: "+alias);
            }
            catch(Exception ex){LogicInfo.Text="公开Output拒绝: "+ex.Message;}
        }
        private void OnFlowUnexposeInput(object sender,RoutedEventArgs args)
        {
            string alias=FlowPublicPortName.Text.Trim();
            ApplyFlowProperty(b=>b.RemoveExposedInput(alias),"已取消公开输入: "+alias);
        }
        private void OnFlowUnexposeOutput(object sender,RoutedEventArgs args)
        {
            string alias=FlowPublicPortName.Text.Trim();
            ApplyFlowProperty(b=>b.RemoveExposedOutput(alias),"已取消公开输出: "+alias);
        }
        private void OnFlowSetParameter(object sender,RoutedEventArgs args)
        {
            try{Guid id=SelectedFlowBlock();string name=FlowInspectorPortName.Text.Trim();
                string value=FlowInspectorValue.Text;
                ApplyFlowProperty(b=>b.SetParameter(id,name,value),"端口输入参数已保存");}
            catch(Exception ex){LogicInfo.Text="参数编辑拒绝："+ex.Message;}
        }
        private void OnFlowClearParameter(object sender,RoutedEventArgs args)
        {
            try{Guid id=SelectedFlowBlock();string name=FlowInspectorPortName.Text.Trim();
                ApplyFlowProperty(b=>b.RemoveParameter(id,name),"已清除参数");}
            catch(Exception ex){LogicInfo.Text="参数删除拒绝："+ex.Message;}
        }

        private void OnWiringBlocksMoved(object sender,MOLBlocksMoveRequestedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("未选择Content");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>b.MoveMany(args.BlockIds,args.DeltaX,args.DeltaY));
                RefreshScene();
                LogicInfo.Text="批量移动 "+args.BlockIds.Count+" 个节点，已一次提交工程事务。";
            }
            catch(Exception ex){LogicInfo.Text="批量移动失败："+ex.Message;}
        }
        private void OnWiringBlockMoveRequested(object sender,MOLBlockMoveRequestedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("未选择Content");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>b.Move(args.BlockId,args.X,args.Y));
                RefreshScene();
                LogicInfo.Text="节点位置已写入同一Content Logic；支持撤销、重做与保存重开。";
            }
            catch(Exception ex){LogicInfo.Text="移动未提交："+ex.Message;}
        }

        // 鼠标直接由输出端口拖至输入端口：使用与C#开发者一致的Builder事务。
        private void OnDirectPortLinkRequested(object sender,MOLPortLinkRequestedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("未选择Content");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>b.Connect(args.SourceBlockId,args.SourcePort,
                        args.TargetBlockId,args.TargetPort,args.Kind));
                RefreshScene();
                LogicInfo.Text="Port拖线已提交工程事务；可撤销、构建及保存重开。";
            }
            catch(Exception ex){LogicInfo.Text="Port拖线未提交："+ex.Message;}
        }

        private void OnWiringLinkRequested(object sender,MOLLinkRequestedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var wiring=LogicHost.WiringDefinition;
                if(wiring==null)throw new InvalidOperationException("当前Content不是V2自由编排");
                var result=new List<FlowPortChoice>();
                foreach(var output in wiring.Ports[args.SourceBlockId])
                foreach(var input in wiring.Ports[args.TargetBlockId])
                {
                    if(output.Direction==MPCPortDirection.Input||
                        input.Direction==MPCPortDirection.Output||
                        output.Kind!=input.Kind||output.TypeId!=input.TypeId||
                        !object.Equals(output.Contract,input.Contract)||
                        (output.Kind!=MPCPortKind.Control&&output.Kind!=MPCPortKind.Data))
                        continue;
                    result.Add(new FlowPortChoice{
                        SourcePort=output.MemberId,TargetPort=input.MemberId,Kind=output.Kind,
                        Label=output.Kind+": "+output.MemberId+" → "+input.MemberId+
                            " ("+output.TypeId+")"});
                }
                _pendingWireSource=args.SourceBlockId;
                _pendingWireTarget=args.TargetBlockId;
                FlowPortPairs.ItemsSource=result;
                if(result.Count!=0)FlowPortPairs.SelectedIndex=0;
                LogicInfo.Text=result.Count==0?
                    "没有类型、方向和种类都兼容的端口对；请检查端口契约。":
                    "找到 "+result.Count+" 组兼容端口，请选择并点击确认端口连线。";
            }
            catch(Exception ex){LogicInfo.Text="端口匹配失败: "+ex.Message;}
        }
        private void OnConfirmWiringLink(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                var choice=FlowPortPairs.SelectedItem as FlowPortChoice;
                if(page==null||!_pendingWireSource.HasValue||!_pendingWireTarget.HasValue||
                    choice==null)throw new InvalidOperationException("请先在画布中选择起止块和合法端口对。");
                var from=_pendingWireSource.Value;var to=_pendingWireTarget.Value;
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,b=>
                    b.Connect(from,choice.SourcePort,to,choice.TargetPort,choice.Kind));
                RefreshScene();
                LogicInfo.Text="类型化连接已通过工程事务提交。";
            }
            catch(Exception ex){LogicInfo.Text="连接被拒绝: "+ex.Message;}
        }
        private void OnDeleteWiringLink(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var link=FlowExistingLinks.SelectedItem as FlowExistingLink;
                var page=_viewModel.ActivePage;
                if(link==null||page==null)throw new InvalidOperationException("请选择要删除的连接");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,b=>b.Disconnect(link.LinkId));
                RefreshScene();LogicInfo.Text="已删除连接；不完整流程仍可作为草稿保存。";
            }
            catch(Exception ex){LogicInfo.Text="删除连接失败: "+ex.Message;}
        }
        private void OnFlowSelectAll(object sender,RoutedEventArgs args)
        {LogicHost.SelectAll();LogicHost.Focus();}
        private void OnFlowCopy(object sender,RoutedEventArgs args)
        {
            try
            {
                var wiring=LogicHost.WiringDefinition;
                if(wiring==null)throw new InvalidOperationException("请先进入V2自由编排");
                _flowClipboard=MOCWiringFragment.Capture(wiring,LogicHost.SelectedBlockIds);
                _flowPasteCount=0;
                // Windows系统剪贴板可跨进程传输；不可用时仍保留当前Studio内存快照。
                try{Clipboard.SetData("CxVisual.MOLWiringFragment.V2",_flowClipboard.ToPayload());}
                catch(System.Runtime.InteropServices.ExternalException){/* 无剪贴板桌面会话 */ }
                LogicInfo.Text="已复制 "+_flowClipboard.Blocks.Count+
                    " 个节点及 "+_flowClipboard.Links.Count+" 条内部连接（内存选区快照）。";
            }
            catch(Exception ex){LogicInfo.Text="复制失败："+ex.Message;}
        }
        private void OnFlowPaste(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                try
                {
                    var payload=Clipboard.GetData("CxVisual.MOLWiringFragment.V2") as byte[];
                    if(payload!=null)_flowClipboard=MOCWiringFragment.FromPayload(payload);
                }
                catch(System.Runtime.InteropServices.ExternalException)
                { /* 无操作系统剪贴板时使用本会话内存快照 */ }
                if(_flowClipboard==null)throw new InvalidOperationException("请先复制节点选区");
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先打开目标Content");
                int pasteIndex=_flowPasteCount+1;
                IReadOnlyDictionary<Guid,Guid> mapping=null;
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,b=>
                    mapping=b.Paste(_flowClipboard,40*pasteIndex,40*pasteIndex));
                _flowPasteCount=pasteIndex;
                RefreshScene();
                LogicHost.SetSelection(mapping.Values);
                LogicInfo.Text="已粘贴 "+mapping.Count+" 个新节点及内部连接（原子工程事务）。";
            }
            catch(Exception ex){LogicInfo.Text="粘贴失败："+ex.Message;}
        }
        private void OnFlowDeleteSelection(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                Guid[] selected=LogicHost.SelectedBlockIds.ToArray();
                var page=_viewModel.ActivePage;
                if(page==null||selected.Length==0)
                    throw new InvalidOperationException("请先选择至少一个节点");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>b.RemoveMany(selected));
                RefreshScene();
                LogicHost.SetSelection(new Guid[0]);
                LogicInfo.Text="已删除 "+selected.Length+" 个节点及相关连线；可一次撤销。";
            }
            catch(Exception ex){LogicInfo.Text="批量删除失败："+ex.Message;}
        }
        private void OnFlowGroup(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                Guid[] ids=LogicHost.SelectedBlockIds.ToArray();
                if(page==null||ids.Length<2)
                    throw new InvalidOperationException("至少选择两个节点");
                Guid group=Guid.Empty;
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>group=b.Group(ids,"组合 "+DateTime.Now.ToString("HHmmss")));
                RefreshScene();
                LogicHost.SetSelection(ids);
                LogicInfo.Text="已创建画布编辑组合 "+group.ToString("N").Substring(0,8)+
                    "；移动成员将一起移动，不改变运行逻辑。";
            }
            catch(Exception ex){LogicInfo.Text="组合失败："+ex.Message;}
        }
        private void OnFlowUngroup(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                var wiring=LogicHost.WiringDefinition;
                var selected=new HashSet<Guid>(LogicHost.SelectedBlockIds);
                if(page==null||wiring==null||selected.Count==0)
                    throw new InvalidOperationException("请先选中组合成员");
                Guid[] groups=wiring.Groups.Where(g=>
                    g.BlockIds.Any(selected.Contains)).Select(g=>g.Id).ToArray();
                if(groups.Length==0)throw new InvalidOperationException("选区没有组合");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,b=>{
                    foreach(Guid id in groups)b.Ungroup(id);
                });
                RefreshScene();
                LogicInfo.Text="已解除 "+groups.Length+" 个画布编辑组合；节点和连接保留。";
            }
            catch(Exception ex){LogicInfo.Text="解除组合失败："+ex.Message;}
        }
        private void OnFlowAlign(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var button=sender as Button;
                var alignment=(MOCBlockAlignment)Enum.Parse(typeof(MOCBlockAlignment),
                    Convert.ToString(button.Tag));
                Guid[] selected=LogicHost.SelectedBlockIds.ToArray();
                var page=_viewModel.ActivePage;
                if(page==null||selected.Length<2)
                    throw new InvalidOperationException("请选择至少两个节点");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>b.Align(selected,alignment));
                RefreshScene();
                LogicInfo.Text="已对齐 "+selected.Length+" 个节点，Undo可原子撤销。";
            }
            catch(Exception ex){LogicInfo.Text="对齐失败："+ex.Message;}
        }
        private void OnFlowDistribute(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                Guid[] ids=LogicHost.SelectedBlockIds.ToArray();
                if(page==null||ids.Length<3)throw new InvalidOperationException("至少选中三个节点");
                bool horizontal=Convert.ToString(((Button)sender).Tag)=="horizontal";
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,b=>
                    b.Distribute(ids,horizontal));
                RefreshScene();
                LogicInfo.Text="节点均匀分布已提交；可撤销重做。";
            }
            catch(Exception ex){LogicInfo.Text="均匀分布失败："+ex.Message;}
        }
        private void OnFlowArrange(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            if(RouteStudioResourceFlowCommand("arrange"))return;
            try
            {
                var page=_viewModel.ActivePage;
                Guid[] ids=LogicHost.SelectedBlockIds.ToArray();
                if(page==null||ids.Length==0)throw new InvalidOperationException("请先选择节点");
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                    b=>b.ArrangeGrid(ids));
                RefreshScene();
                LogicInfo.Text="已按稳定次序自动布局 "+ids.Length+" 个节点。";
            }
            catch(Exception ex){LogicInfo.Text="布局失败："+ex.Message;}
        }
        private void OnFlowFit(object sender,RoutedEventArgs args)
        {
            if(LogicHost.WiringDefinition==null)return;
            var layout=MOLFlowLayout.Build(LogicHost.WiringDefinition);
            if(layout.Count==0)return;
            double width=layout.Max(x=>x.X+x.Width)+35;
            double height=layout.Max(x=>x.Y+x.Height)+35;
            double scaleX=FlowScroll.ViewportWidth>0?FlowScroll.ViewportWidth/width:1;
            double scaleY=FlowScroll.ViewportHeight>0?FlowScroll.ViewportHeight/height:1;
            LogicHost.ZoomFactor=Math.Max(0.25,Math.Min(3,Math.Min(scaleX,scaleY)));
            FlowScroll.ScrollToHorizontalOffset(0);
            FlowScroll.ScrollToVerticalOffset(0);
            LogicInfo.Text="画布已适配视口；仅更改显示倍率。";
        }
        private void OnFlowLocate(object sender,RoutedEventArgs args)
        {
            if(LogicHost.WiringDefinition==null||LogicHost.SelectedBlockIds.Count==0)return;
            var selected=new HashSet<Guid>(LogicHost.SelectedBlockIds);
            var bounds=MOLFlowLayout.Build(LogicHost.WiringDefinition)
                .Where(x=>selected.Contains(x.BlockId)).ToArray();
            if(bounds.Length==0)return;
            double scale=LogicHost.ZoomFactor;
            FlowScroll.ScrollToHorizontalOffset(Math.Max(0,
                bounds.Min(x=>x.X)*scale-FlowScroll.ViewportWidth/3));
            FlowScroll.ScrollToVerticalOffset(Math.Max(0,
                bounds.Min(x=>x.Y)*scale-FlowScroll.ViewportHeight/3));
            LogicInfo.Text="已将选中节点定位到可视区。";
        }
        private void OnFlowZoom(object sender,RoutedEventArgs args)
        {
            var button=sender as Button;
            string mode=Convert.ToString(button.Tag);
            double next=mode=="reset"?1:mode=="in"?
                Math.Min(3,LogicHost.ZoomFactor*1.2):
                Math.Max(0.25,LogicHost.ZoomFactor/1.2);
            LogicHost.ZoomFactor=next;
            LogicInfo.Text="画布视图 "+Math.Round(next*100)+"%（不改变工程定义）。";
        }
        private void OnFlowWheel(object sender,MouseWheelEventArgs args)
        {
            if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
            LogicHost.ZoomFactor=args.Delta>0?
                Math.Min(3,LogicHost.ZoomFactor*1.1):
                Math.Max(0.25,LogicHost.ZoomFactor/1.1);
            args.Handled=true;
        }
        private void OnFlowPanDown(object sender,MouseButtonEventArgs args)
        {
            if(args.ChangedButton!=MouseButton.Middle)return;
            _flowPanning=true;_flowPanStart=args.GetPosition(FlowScroll);
            _flowPanX=FlowScroll.HorizontalOffset;_flowPanY=FlowScroll.VerticalOffset;
            FlowScroll.CaptureMouse();args.Handled=true;
        }
        private void OnFlowPanMove(object sender,MouseEventArgs args)
        {
            if(!_flowPanning)return;
            Point pos=args.GetPosition(FlowScroll);
            FlowScroll.ScrollToHorizontalOffset(_flowPanX+_flowPanStart.X-pos.X);
            FlowScroll.ScrollToVerticalOffset(_flowPanY+_flowPanStart.Y-pos.Y);
            args.Handled=true;
        }
        private void OnFlowPanUp(object sender,MouseButtonEventArgs args)
        {
            if(!_flowPanning||args.ChangedButton!=MouseButton.Middle)return;
            _flowPanning=false;
            if(FlowScroll.IsMouseCaptured)FlowScroll.ReleaseMouseCapture();
            args.Handled=true;
        }
        private void OnFlowShortcuts(object sender,KeyEventArgs args)
        {
            // 画布快捷键不能抢走文本输入、配置表单或运行态焦点。
            if(args.OriginalSource is TextBoxBase||args.OriginalSource is ComboBox)return;
            if(HandleStudioResourceShortcut(args.Key,Keyboard.Modifiers))
            {args.Handled=true;return;}
            if(!IsStudioDocumentActive("doc.flow")||_logicRunning)return;
            bool control=(Keyboard.Modifiers&ModifierKeys.Control)!=0;
            if(control&&args.Key==Key.A)OnFlowSelectAll(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.C)OnFlowCopy(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.V)OnFlowPaste(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.Z)OnUndoWiring(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.Y)OnRedoWiring(sender,new RoutedEventArgs());
            else if(args.Key==Key.Delete)OnFlowDeleteSelection(sender,new RoutedEventArgs());
            else if(args.Key==Key.Escape)
            {
                // 不创建Revision：仅取消当前预览与旧式两点连线暂存。
                LogicHost.ResetLinkSelection();
                LogicHost.IsConnectMode=false;
                FlowWireMode.Content="开始连线";
                _pendingWireSource=null;
                _pendingWireTarget=null;
                FlowPortPairs.ItemsSource=null;
                _flowPanning=false;
                if(FlowScroll.IsMouseCaptured)FlowScroll.ReleaseMouseCapture();
                LogicInfo.Text="已取消当前画布操作，工程未修改。";
            }
            else return;
            args.Handled=true;
        }
        private void OnDeleteWiringBlock(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null||LogicHost.SelectedBlockIds.Count==0)
                    throw new InvalidOperationException("请先在画布中选中节点");
                Guid[] ids=LogicHost.SelectedBlockIds.ToArray();
                MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,b=>b.RemoveMany(ids));
                RefreshScene();LogicInfo.Text="已删除 "+ids.Length+" 个节点及相邻连接。";
            }
            catch(Exception ex){LogicInfo.Text="删除节点失败: "+ex.Message;}
        }
        private void OnUndoWiring(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try{_viewModel.ProjectSession.Undo().RequireValue();RefreshScene();
                LogicInfo.Text="已撤销最近一次工程修改。";}
            catch(Exception ex){LogicInfo.Text="撤销失败: "+ex.Message;}
        }
        private void OnRedoWiring(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try{_viewModel.ProjectSession.Redo().RequireValue();RefreshScene();
                LogicInfo.Text="已重做最近一次工程修改。";}
            catch(Exception ex){LogicInfo.Text="重做失败: "+ex.Message;}
        }
        private static string FormatBuildDiagnostics(MOCBuildResult build)
        {
            if(build.Succeeded)return "编译验证通过：Content与能力契约可生成MRK执行计划。";
            return string.Join(Environment.NewLine,build.Diagnostics.Take(8).Select(x=>
                x.Code+(x.BlockId.HasValue?" / Block "+x.BlockId.Value.ToString("N").Substring(0,8):"")+
                (x.LinkId.HasValue?" / Link "+x.LinkId.Value.ToString("N").Substring(0,8):"")+
                "： "+x.Message));
        }
        private void OnFlowToggleBreakpoint(object sender,RoutedEventArgs args)
        {
            try
            {
                Guid id=SelectedFlowBlock();
                if(!_logicBreakpoints.Add(id))_logicBreakpoints.Remove(id);
                LogicInfo.Text=(_logicBreakpoints.Contains(id)?"已增加":"已移除")+
                    "节点断点 "+id.ToString("N").Substring(0,8);
            }
            catch(Exception ex){LogicInfo.Text="设置断点失败："+ex.Message;}
        }
        private void OnFlowToggleInvocationBreakpoint(object sender,RoutedEventArgs args)
        {
            try
            {
                string qualified=FlowInvocationBreakpoint.Text.Trim();
                if(qualified.Length==0)
                {
                    var chosen=FlowTraceList.SelectedItem as FlowTraceRecord;
                    qualified=chosen==null?string.Empty:chosen.StepId;
                }
                if(string.IsNullOrWhiteSpace(qualified)||qualified.Length>1024||
                    qualified.Any(char.IsControl))
                    throw new InvalidOperationException("请输入完整StepId，或先选择一条运行轨迹");
                bool enabled=_invocationBreakpoints.Add(qualified);
                if(!enabled)_invocationBreakpoints.Remove(qualified);
                if(_logicDebug!=null)
                {
                    if(enabled)_logicDebug.AddBreakpoint(qualified);
                    else _logicDebug.RemoveBreakpoint(qualified);
                }
                LogicInfo.Text=(enabled?"已增加":"已移除")+"执行实例断点: "+qualified;
            }
            catch(Exception ex){LogicInfo.Text="设置执行实例断点失败: "+ex.Message;}
        }
        private void OnFlowDebugPause(object sender,RoutedEventArgs args)
        {
            if(_logicDebug==null){LogicInfo.Text="当前没有活跃MRK调试会话";return;}
            _logicDebug.Pause();
            LogicInfo.Text="已请求在下一个MRK节点边界暂停";
        }
        private void OnFlowDebugResume(object sender,RoutedEventArgs args)
        {
            if(_logicDebug==null){LogicInfo.Text="当前没有活跃MRK调试会话";return;}
            _logicDebug.Resume();
            LogicInfo.Text="已恢复MRK执行";
        }
        private void OnFlowDebugStep(object sender,RoutedEventArgs args)
        {
            LogicInfo.Text=_logicDebug!=null&&_logicDebug.StepOnce()?
                "已单步释放一个等待的MRK节点":"当前无可单步的等待节点";
        }
        private void OnFlowDebugCancel(object sender,RoutedEventArgs args)
        {
            if(_logicFlow!=null&&_logicFlow.RequestCancel())
                LogicInfo.Text="已向MRK发送取消请求";
            else LogicInfo.Text="没有可取消的运行任务";
        }
        /// <summary>从MOC/MRK SourceMap节点反查相同MEM工程中的图元绑定。</summary>
        private void OnFlowLocateVisual(object sender,RoutedEventArgs args)
        {
            try
            {
                var trace=FlowTraceList.SelectedItem as FlowTraceRecord;
                if(trace!=null&&(trace.ProjectId!=_viewModel.ProjectSession.Project.Id||
                    trace.EngineeringRevision!=_viewModel.ProjectSession.Revision))
                {
                    LogicInfo.Text="关联画面来源于过期或其他工程的MRK Plan，请重新构建并运行。";
                    return;
                }
                if(trace!=null&&trace.RootContentId!=Guid.Empty)
                {
                    MEMPage sourcePage=_viewModel.ProjectSession.Project.Pages
                        .FirstOrDefault(p=>p.RootContent.Id==trace.RootContentId);
                    if(sourcePage==null)
                    {
                        LogicInfo.Text="轨迹原主流程页面已删除或无效，拒绝关联画面误跳。";
                        return;
                    }
                    if(_viewModel.ActivePage==null||_viewModel.ActivePage.Id!=sourcePage.Id)
                    {
                        _viewModel.ActivePage=sourcePage;
                        RefreshScene();
                    }
                }
                Guid blockId=trace==null?SelectedFlowBlock():trace.BlockId;
                Guid? visualId=_viewModel.FindVisualForFlowBlock(blockId);
                if(!visualId.HasValue)
                {
                    LogicInfo.Text="当前画面未配置关联此逻辑节点的状态图元；请先在视觉页关联流程状态。";
                    return;
                }
                _viewModel.Select(visualId.Value);
                OpenStudioDocument("doc.visual");
                RefreshVisualInspector();
                VisualInfo.Text="已从流程节点 "+blockId.ToString("N").Substring(0,8)+
                    " 定位关联画面对象；与逻辑执行、诊断共用MEM身份和MPM绑定。";
            }
            catch(Exception ex){LogicInfo.Text="定位关联画面失败："+ex.Message;}
        }
        private void OnFlowLocateDiagnostic(object sender,RoutedEventArgs args)
        {
            FlowDiagnosis item=FlowDiagnosticList.SelectedItem as FlowDiagnosis;
            if(item==null){LogicInfo.Text="请先选择一条诊断";return;}
            MEMEngineeringSession current=_viewModel.ProjectSession;
            if(item.ProjectId!=current.Project.Id||
                item.EngineeringRevision!=current.Revision)
            {
                LogicInfo.Text="此构建诊断所属工程或Revision已变化，请重新构建后定位。";
                return;
            }
            if(!item.BlockId.HasValue)
            {
                LogicInfo.Text="当前诊断只定位到Content，缺少BlockId："+item.Label;
                return;
            }
            MEMPage page=_viewModel.ActivePage;
            MOLWiringDefinition wiring=LogicHost.WiringDefinition;
            if(page!=null&&page.RootContent.Id==item.ContentId&&wiring!=null&&
                wiring.Blocks.Any(block=>block.Id==item.BlockId.Value))
            {
                LogicHost.SetSelection(new[]{item.BlockId.Value});
                OnFlowLocate(sender,args);
                LogicInfo.Text="已定位主流程构建诊断："+item.Label;
            }
            else if(LocateStudioFlowSource(item.ProjectId,item.EngineeringRevision,
                item.ContentId,item.BlockId.Value))
                LogicInfo.Text="已定位原始Content流程构建诊断："+item.Label;
            else LogicInfo.Text="诊断来源已失效或无可定位MOL节点："+item.Label;
        }
        /// <summary>从MRK原始Content/Block来源定位子流程；与父流程调用定位并存。</summary>
        private void OnFlowLocateSourceContent(object sender,RoutedEventArgs args)
        {
            FlowTraceRecord trace=FlowTraceList.SelectedItem as FlowTraceRecord;
            if(trace==null){LogicInfo.Text="请先选择一条真实运行轨迹";return;}
            try
            {
                MEMEngineeringSession current=_viewModel.ProjectSession;
                if(trace.ProjectId!=current.Project.Id||
                    trace.EngineeringRevision!=current.Revision)
                {
                    LogicInfo.Text="运行轨迹基于其他工程或过期Revision；请重新构建并运行，未切换当前Tab。";
                    return;
                }
                LogicInfo.Text=LocateStudioFlowSource(trace.ProjectId,
                    trace.EngineeringRevision,trace.ContentId,trace.BlockId)?
                    "已定位真实子流程源节点："+trace.Label:
                    "源节点已失效或没有V2 MOL定义，未改变当前Tab："+trace.Label;
            }
            catch(Exception ex){LogicInfo.Text="定位源节点失败："+ex.Message;}
        }
        private void OnFlowLocateTrace(object sender,RoutedEventArgs args)
        {
            var trace=FlowTraceList.SelectedItem as FlowTraceRecord;
            if(trace==null){LogicInfo.Text="请先选择一条运行轨迹";return;}
            if(trace.ProjectId!=_viewModel.ProjectSession.Project.Id||
                trace.EngineeringRevision!=_viewModel.ProjectSession.Revision)
            {
                LogicInfo.Text="父流程定位拒绝过期或其他工程的MRK Plan，请重新构建并运行。";
                return;
            }
            MEMPage sourcePage=null;
            MOLWiringDefinition wiring=LogicHost.WiringDefinition;
            if(trace.RootContentId!=Guid.Empty)
            {
                sourcePage=_viewModel.ProjectSession.Project.Pages.FirstOrDefault(p=>
                    p.RootContent.Id==trace.RootContentId);
                if(sourcePage==null)
                {
                    LogicInfo.Text="轨迹原主流程页面已删除或无效，拒绝错误定位。";
                    return;
                }
                MEMNodeSnapshot source=_viewModel.ProjectSession.GetSnapshot()
                    .Find(sourcePage.RootContent.Id);
                wiring=source==null?null:MOLWiringCodec.Get(source);
            }
            Guid candidate=trace.BlockId;
            if(wiring!=null&&!wiring.Blocks.Any(b=>b.Id==candidate))
            {
                // 内嵌Content的真实MRK轨迹保留原始BlockId；
                // 在当前父流程画布定位其调用链，不能伪装成当前Content里的本地节点。
                candidate=trace.CallPath==null?Guid.Empty:
                    trace.CallPath.Reverse().FirstOrDefault(id=>
                        wiring.Blocks.Any(b=>b.Id==id));
            }
            if(candidate!=Guid.Empty&&wiring!=null&&
                wiring.Blocks.Any(b=>b.Id==candidate))
            {
                if(sourcePage!=null)
                {
                    if(_viewModel.ActivePage==null||_viewModel.ActivePage.Id!=sourcePage.Id)
                        _viewModel.ActivePage=sourcePage;
                    OpenStudioDocument("doc.flow");
                    RefreshScene();
                }
                LogicHost.SetSelection(new[]{candidate});
                OnFlowLocate(sender,args);
                LogicInfo.Text=candidate==trace.BlockId?
                    "已定位运行轨迹："+trace.Label:
                    "已定位子流程调用节点；实际执行节点属于Content "+
                    trace.ContentId.ToString("N").Substring(0,8)+"："+trace.Label;
            }
            else LogicInfo.Text="当前画布无法解析此子流程调用链："+trace.Label;
        }
        private void OnValidateWiring(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            if(RouteStudioResourceFlowCommand("validate"))return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择页面");
                MEMProjectSnapshot snapshot=_viewModel.ProjectSession.GetSnapshot();
                var result=new MOCBuildService().Build(snapshot,
                    page.RootContent.Id,_simulation);
                LogicInfo.Text=FormatBuildDiagnostics(result);
                FlowDiagnosticList.ItemsSource=result.Diagnostics.Select(x=>new FlowDiagnosis{
                    ContentId=x.ContentId,ProjectId=snapshot.ProjectId,
                    EngineeringRevision=snapshot.Revision,
                    BlockId=x.BlockId,LinkId=x.LinkId,
                    Label=x.Code+" · "+x.Message}).ToList();
                LogicHost.RuntimeStates=result.Diagnostics.Where(x=>x.BlockId.HasValue)
                    .GroupBy(x=>x.BlockId.Value).ToDictionary(x=>x.Key,x=>"Failed");
            }
            catch(Exception ex){LogicInfo.Text="校验失败: "+ex.Message;}
        }

        private sealed class SimulationResolver : IMRKVersionedCapabilityResolver,IMOCCapabilityProvider
        {
            private readonly MOCCapabilityDescriptor[] _definitions={
                new MOCCapabilityDescriptor("Acquire","采图","X-Ray",
                    new[]{new MPCPortDescriptor("Image",MPCPortDirection.Output,"string",MPCPortKind.Data)}),
                new MOCCapabilityDescriptor("AI","AI检测","Vision",
                    new[]{new MPCPortDescriptor("Image",MPCPortDirection.Input,"string",MPCPortKind.Data)}),
                new MOCCapabilityDescriptor("CV","传统检测","Vision",
                    new[]{new MPCPortDescriptor("Image",MPCPortDirection.Input,"string",MPCPortKind.Data)}),
                new MOCCapabilityDescriptor("OutputOK","合格放行","Inspection"),
                new MOCCapabilityDescriptor("OutputNG","不合格剔除","Inspection"),
                new MOCCapabilityDescriptor("AIReview","AI后处理","Vision"),
                new MOCCapabilityDescriptor("CVReview","传统算法后处理","Vision"),
                new MOCCapabilityDescriptor("Judge","综合结果判定","Inspection")
            };
            public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
            {return _definitions;}
            // 唯一CPU模拟Host声明的能力版本；不代表真实PLC、AI模型或CT Provider。
            public IReadOnlyDictionary<string,string> DescribeReleaseVersions()
            {
                return _definitions.ToDictionary(d=>d.Id,
                    d=>"studio-cpu-v1",StringComparer.Ordinal);
            }
            public bool TryGetCapabilityVersion(string id,out string version)
            {
                if(_definitions.Any(x=>x.Id==id))
                {version="studio-cpu-v1";return true;}
                version=null;return false;
            }
            public bool TryResolve(string id,out MRKResolvedCapability capability)
            {
                capability=null;
                if(!_definitions.Any(x=>x.Id==id))return false;
                var input=(id=="AI"||id=="CV")?
                    new[]{new MRKCapabilityInput("Image",typeof(string),false)}:null;
                capability=new MRKResolvedCapability(async (ctx,token) =>
                {
                    await Task.Delay(id=="AI"?80:20,token).ConfigureAwait(false);
                    return (object)id;
                },new MRKExecutionOutput(typeof(string)),inputs:input);
                return true;
            }
        }
        private async void OnRunLogicDemo(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            MRKExecutionEngine engine=null;
            DispatcherTimer timer=null;
            var previousVisualOwner=_viewModel.VisualRuntimeSource;
            bool flowVisualAttached=false;
            try
            {
                MEMPage page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择页面");
                MEMProjectSnapshot runSnapshot=_viewModel.ProjectSession.GetSnapshot();
                var checkedBuild=new MOCBuildService().Build(
                    runSnapshot,page.RootContent.Id,_simulation);
                if(!checkedBuild.Succeeded)
                {
                    LogicInfo.Text=FormatBuildDiagnostics(checkedBuild);
                    LogicHost.RuntimeStates=checkedBuild.Diagnostics.Where(x=>x.BlockId.HasValue)
                        .GroupBy(x=>x.BlockId.Value).ToDictionary(x=>x.Key,x=>"Failed");
                    return;
                }
                if(_visualPreviewTimer.IsEnabled)
                    throw new InvalidOperationException("请先退出独立视觉预览，再运行同工程MRK仿真");
                MOCCompiledPlan compiled=checkedBuild.Plan;
                string releaseFingerprint=null;
                long publicationRevision=0;
                if(_preparedCpuRelease!=null)
                {
                    // 同一工程可能同时打开多个Root Page；候选不能套用到另一个主流程。
                    if(_preparedCpuRelease.EntryContentId!=page.RootContent.Id)
                        throw new InvalidOperationException("studio.cpu_release.entry_mismatch");
                    // 候选锁定旧Revision、子Content和Host声明能力版本；
                    // 过期/未授权时绝不自动回退为普通CPU运行。
                    compiled=StageCpuSoftwareRelease(_preparedCpuRelease,runSnapshot,
                        _simulation,_simulation.DescribeReleaseVersions(),
                        new StudioCpuSoftwarePolicy());
                    releaseFingerprint=_preparedCpuRelease.Fingerprint;
                    publicationRevision=1;
                    _preparedCpuRelease=null;
                }
                _logicDebug=new MRKExecutionDebugController();
                foreach(Guid id in _logicBreakpoints)
                foreach(var origin in compiled.SourceMap.Find(id))
                    _logicDebug.AddBreakpoint(origin.StepId);
                foreach(string qualified in _invocationBreakpoints)
                    _logicDebug.AddBreakpoint(qualified);
                FlowTraceList.ItemsSource=new List<FlowTraceRecord>();
                _logicRunning=true;
                _logicOverlay.Clear();
                // 复用现有MRK轨迹和MDS外部绑定，不另建运行数据仓库。
                _viewModel.AttachVisualRuntimeSource(
                    new MOLVisualTraceBindingResolver(
                        id=>_logicOverlay.StateOf(id),previousVisualOwner));
                flowVisualAttached=true;
                _flowVisualClock.Restart();
                SceneHost.CancelInteraction();
                SceneHost.IsDesignMode=false;
                LogicHost.RuntimeStates=_logicOverlay.Snapshot();
                _viewModel.UpdateVisualRuntimeFrame(0);
                engine=new MRKExecutionEngine(new FCRWorkerOptions(3,64));
                var inputs=compiled.InputDefaults.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
                inputs["accepted"]=true;
                _logicFlow=engine.StartFlowWithControl(compiled.Plan,_logicDebug,inputs);
                long cursor=0;
                Action update=()=>
                {
                    var changes=compiled.ReadTrace(_logicFlow,cursor);
                    cursor=changes.NextCursor;
                    if(changes.LostCount!=0)
                    {
                        LogicInfo.Text="运行追踪有数据缺口，请用Flow完整快照校准";
                        return;
                    }
                    _logicOverlay.Apply(_logicFlow.FlowId,changes);
                    LogicHost.RuntimeStates=_logicOverlay.Snapshot();
                    _viewModel.UpdateVisualRuntimeFrame(_flowVisualClock.Elapsed.TotalMilliseconds);
                    var previous=(FlowTraceList.ItemsSource as IEnumerable<FlowTraceRecord>)
                        .ToList();
                    previous.AddRange(changes.Events.Select(e=>ProjectFlowTrace(compiled,e,
                        page.RootContent.Id,_logicFlow.FlowId,
                        releaseFingerprint,publicationRevision)));
                    if(previous.Count>500)previous=previous.Skip(previous.Count-500).ToList();
                    FlowTraceList.ItemsSource=previous;
                };
                timer=new DispatcherTimer(DispatcherPriority.Background)
                {Interval=TimeSpan.FromMilliseconds(150)};
                timer.Tick+=(o,e)=>update();
                timer.Start();
                LogicInfo.Text="CPU仿真运行中：Plan Revision "+compiled.EngineeringRevision;
                var result=await _logicFlow.Completion;
                update();
                LogicInfo.Text="仿真执行结束: "+result.State+
                    "；FlowId="+result.FlowId.ToString("N")+
                    (result.Error==null?"":"；错误: "+result.Error.Message);
            }
            catch(Exception ex){LogicInfo.Text="编排或运行失败: "+ex.Message;}
            finally
            {
                if(timer!=null)timer.Stop();
                if(engine!=null)await engine.StopAsync(true);
                _flowVisualClock.Reset();
                if(flowVisualAttached)
                {
                    SceneHost.IsDesignMode=true;
                    _viewModel.StopVisualRuntime();
                    _viewModel.AttachVisualRuntimeSource(previousVisualOwner);
                }
                _logicFlow=null;
                _logicDebug=null;
                _logicRunning=false;
            }
        }
    }
}
