using System;
using System.IO;
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
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new StudioViewModel();
            DataContext = _viewModel;
            SceneHost.NodeActivated += OnActivated;
            SceneHost.MoveRequested += OnMove;
            _viewModel.SceneChanged += (sender, args) => RefreshScene();
            RefreshScene();
        }
        private void OnActivated(object sender, CxNodeEventArgs args)
        { _viewModel.Select(args.Id); }
        private void OnMove(object sender, CxMoveEventArgs args)
        { _viewModel.Drag(args.Id, args.X, args.Y); }
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
            _viewModel.Dispose();
            base.OnClosed(e);
        }
        private void RefreshScene()
        { SceneHost.SetScene(_viewModel.Items, _viewModel.SelectedId); }
    }
}
