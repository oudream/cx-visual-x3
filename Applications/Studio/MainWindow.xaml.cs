using System;
using System.IO;
using System.Windows;
using CxVisual.Mechanism.Core;
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
        private void OnConfigDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                // 样例配置存放用户可写目录；不涉及真实设备连接、标定或安全联锁。
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CxVisual", "Configs", "Samples");
                CxConfigManager manager = new CxConfigManager(root);
                int selected = ConfigDemoKind.SelectedIndex;
                bool saved;
                if (selected == 1)
                {
                    manager.Register<CxAutomationDeviceOptions>("Samples.Automation.Device01",
                        validator: CxAutomationDeviceOptions.Validate);
                    manager.Initialize();
                    saved = CxConfigEditorDialog.Edit<CxAutomationDeviceOptions>(
                        this, manager, "Samples.Automation.Device01");
                }
                else if (selected == 2)
                {
                    manager.Register<CxVisionInspectionOptions>("Samples.Vision.Inspection",
                        validator: CxVisionInspectionOptions.Validate);
                    manager.Initialize();
                    saved = CxConfigEditorDialog.Edit<CxVisionInspectionOptions>(
                        this, manager, "Samples.Vision.Inspection");
                }
                else if (selected == 3)
                {
                    manager.Register<CxCtReconstructionOptions>("Samples.CT3D.Reconstruction",
                        validator: CxCtReconstructionOptions.Validate);
                    manager.Initialize();
                    saved = CxConfigEditorDialog.Edit<CxCtReconstructionOptions>(
                        this, manager, "Samples.CT3D.Reconstruction");
                }
                else
                {
                    manager.Register<StudioDemoConfig>("Studio.Demo");
                    manager.Initialize();
                    saved = CxConfigEditorDialog.Edit<StudioDemoConfig>(this, manager, "Studio.Demo");
                }
                if (saved)
                    MessageBox.Show(this, "已通过统一L1服务验证并保存示例配置。\n目录：" + root,
                        "配置已保存", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "配置初始化失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
