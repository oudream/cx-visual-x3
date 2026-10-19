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
        private bool _logicRunning;
        private readonly System.Diagnostics.Stopwatch _visualClock=new System.Diagnostics.Stopwatch();
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
            DataContext = _viewModel;
            LogicHost.BlockSelected += (sender, args) => { _logicSelected=args.BlockId; LogicInfo.Text = "已选逻辑块: " + args.BlockId.ToString("N"); };
            LogicHost.LinkRequested += OnWiringLinkRequested;
            LogicHost.PortLinkRequested += OnDirectPortLinkRequested;
            LogicHost.BlockMoveRequested += OnWiringBlockMoveRequested;
            LogicHost.BlocksMoveRequested += OnWiringBlocksMoved;
            LogicHost.SelectionChanged += (sender,args)=>{
                _logicSelected=args.BlockIds.Count==1?(Guid?)args.BlockIds[0]:null;
                LogicInfo.Text="选中 "+args.BlockIds.Count+" 个节点；Ctrl增减、空白框选、拖动批量移动。";
            };
            PreviewKeyDown += OnFlowShortcuts;
            LogicHost.PortLinkRejected += (sender,args) =>
                LogicInfo.Text="拖线被拒绝："+args.Reason;
            SceneHost.NodeActivated += OnActivated;
            SceneHost.MoveRequested += OnMove;
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
                VisualInfo.Text="Widget事件："+widget.EventId+
                    " · 对象："+widget.ElementId.ToString("N").Substring(0,8)+
                    "（仅事件通知，实际动作需运行时授权）";
            _viewModel.SceneChanged += (sender, args) => RefreshScene();
            RefreshScene();
        }
        private void OnActivated(object sender, CxNodeEventArgs args)
        { _viewModel.Select(args.Id); }
        private void OnMove(object sender, CxMoveEventArgs args)
        { _viewModel.Drag(args.Id, args.X, args.Y); }
        private void OnInsertShape(object sender,RoutedEventArgs args)
        {
            try
            {
                var button=(System.Windows.Controls.Button)sender;
                string kind=button.Tag.ToString();
                if(kind=="Text")_viewModel.AddVisualText();
                else if(kind=="WidgetButton")_viewModel.AddVisualWidget();
                else _viewModel.AddVisualShape((MRCShapeKind)Enum.Parse(typeof(MRCShapeKind),kind));
                VisualInfo.Text="已创建 "+button.Content+" | Revision "+_viewModel.Revision;
            }
            catch(Exception ex){VisualInfo.Text="创建失败: "+ex.Message;}
        }
        private void OnAddBreathingAnimation(object sender,RoutedEventArgs args)
        {
            try
            {
                _viewModel.AddSelectedBreathingAnimation();
                VisualInfo.Text="透明度动画已保存：点击预览交互查看效果；撤销可恢复定义。";
            }
            catch(Exception ex){VisualInfo.Text="添加动画失败："+ex.Message;}
        }
        private void OnToggleVisualPreview(object sender,RoutedEventArgs args)
        {
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
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {if(_viewModel.ProjectSession.IsDirty&&(_viewModel.ConfirmReplace==null||!_viewModel.ConfirmReplace(_viewModel.ProjectSession)))e.Cancel=true;base.OnClosing(e);}
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
            LogicHost.IsConnectMode=!LogicHost.IsConnectMode;
            LogicHost.ResetLinkSelection();
            FlowWireMode.Content=LogicHost.IsConnectMode?"结束连线":"开始连线";
            LogicInfo.Text=LogicHost.IsConnectMode?
                "依次点击来源和目标块，随后在端口列表选择合法端口对并确认。":"已退出连线模式。";
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
            if(!FlowTab.IsSelected||_logicRunning)return;
            if(args.OriginalSource is TextBoxBase||args.OriginalSource is ComboBox)return;
            bool control=(Keyboard.Modifiers&ModifierKeys.Control)!=0;
            if(control&&args.Key==Key.A)OnFlowSelectAll(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.C)OnFlowCopy(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.V)OnFlowPaste(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.Z)OnUndoWiring(sender,new RoutedEventArgs());
            else if(control&&args.Key==Key.Y)OnRedoWiring(sender,new RoutedEventArgs());
            else if(args.Key==Key.Delete)OnFlowDeleteSelection(sender,new RoutedEventArgs());
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
        private void OnValidateWiring(object sender,RoutedEventArgs args)
        {
            if(_logicRunning)return;
            try
            {
                var page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择页面");
                var result=new MOCBuildService().Build(_viewModel.ProjectSession.GetSnapshot(),
                    page.RootContent.Id,_simulation);
                LogicInfo.Text=FormatBuildDiagnostics(result);
                LogicHost.RuntimeStates=result.Diagnostics.Where(x=>x.BlockId.HasValue)
                    .GroupBy(x=>x.BlockId.Value).ToDictionary(x=>x.Key,x=>"Failed");
            }
            catch(Exception ex){LogicInfo.Text="校验失败: "+ex.Message;}
        }

        private sealed class SimulationResolver : IMRKCapabilityResolver,IMOCCapabilityProvider
        {
            private readonly MOCCapabilityDescriptor[] _definitions={
                new MOCCapabilityDescriptor("Acquire","采图","X-Ray",
                    new[]{new MPCPortDescriptor("Image",MPCPortDirection.Output,"string",MPCPortKind.Data)}),
                new MOCCapabilityDescriptor("AI","AI检测","Vision",
                    new[]{new MPCPortDescriptor("Image",MPCPortDirection.Input,"string",MPCPortKind.Data)}),
                new MOCCapabilityDescriptor("CV","传统检测","Vision",
                    new[]{new MPCPortDescriptor("Image",MPCPortDirection.Input,"string",MPCPortKind.Data)}),
                new MOCCapabilityDescriptor("OutputOK","合格放行","Inspection"),
                new MOCCapabilityDescriptor("OutputNG","不合格剔除","Inspection")
            };
            public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
            {return _definitions;}
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
            try
            {
                MEMPage page=_viewModel.ActivePage;
                if(page==null)throw new InvalidOperationException("请先选择页面");
                var checkedBuild=new MOCBuildService().Build(
                    _viewModel.ProjectSession.GetSnapshot(),page.RootContent.Id,_simulation);
                if(!checkedBuild.Succeeded)
                {
                    LogicInfo.Text=FormatBuildDiagnostics(checkedBuild);
                    LogicHost.RuntimeStates=checkedBuild.Diagnostics.Where(x=>x.BlockId.HasValue)
                        .GroupBy(x=>x.BlockId.Value).ToDictionary(x=>x.Key,x=>"Failed");
                    return;
                }
                var compiled=checkedBuild.Plan;
                _logicRunning=true;
                _logicOverlay.Clear();
                LogicHost.RuntimeStates=_logicOverlay.Snapshot();
                engine=new MRKExecutionEngine(new FCRWorkerOptions(3,64));
                _logicFlow=engine.StartFlow(compiled.Plan,
                    new Dictionary<string,object>{{"accepted",true}});
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
                    _logicOverlay.Apply(changes);
                    LogicHost.RuntimeStates=_logicOverlay.Snapshot();
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
                _logicFlow=null;
                _logicRunning=false;
            }
        }
    }
}
