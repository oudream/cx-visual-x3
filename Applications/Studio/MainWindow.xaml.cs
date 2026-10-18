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
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new StudioViewModel();
            _viewModel.ConfirmReplace = ConfirmProjectReplacement;
            DataContext = _viewModel;
            LogicHost.BlockSelected += (sender, args) => LogicInfo.Text = "已选逻辑块: " + args.BlockId.ToString("N");
            SceneHost.NodeActivated += OnActivated;
            SceneHost.MoveRequested += OnMove;
            _viewModel.SceneChanged += (sender, args) => RefreshScene();
            RefreshScene();
        }
        private void OnActivated(object sender, CxNodeEventArgs args)
        { _viewModel.Select(args.Id); }
        private void OnMove(object sender, CxMoveEventArgs args)
        { _viewModel.Drag(args.Id, args.X, args.Y); }
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
            _viewModel.Dispose();
            base.OnClosed(e);
        }
        private void RefreshScene()
        {
            SceneHost.SetScene(_viewModel.Items, _viewModel.SelectedId);
            if(_logicRunning)return;
            MEMPage page=_viewModel.ActivePage;
            LogicHost.Definition=page==null?null:
                MOLLogicCodec.Get(_viewModel.ProjectSession.GetSnapshot().Find(page.RootContent.Id));
            LogicHost.RuntimeStates=null;
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
                LogicHost.Definition=model;
                LogicHost.RuntimeStates=null;
                LogicInfo.Text="示例逻辑已通过工程Session提交到Content；可随工程保存、撤销和重开。";
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"创建逻辑失败");}
        }
        private sealed class SimulationResolver : IMRKCapabilityResolver
        {
            public bool TryResolve(string id,out MRKResolvedCapability capability)
            {
                capability=null;
                switch(id)
                {
                    case "Acquire": case "AI": case "CV": case "OutputOK": case "OutputNG":
                        capability=new MRKResolvedCapability(async (ctx,token) =>
                        {
                            await Task.Delay(id=="AI"?80:20,token).ConfigureAwait(false);
                            return (object)id;
                        });
                        return true;
                    default:return false;
                }
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
                var compiled=new MOCEngineeringCompiler().Compile(
                    _viewModel.ProjectSession.GetSnapshot(),page.RootContent.Id,new SimulationResolver());
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
