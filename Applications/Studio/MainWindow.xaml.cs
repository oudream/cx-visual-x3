using System;
using System.IO;
using System.Windows;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Lifecycle;
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

        /// <summary>所有专业只声明强类型配置及Validator；加载、保存与发布仍只使用统一管理器。</summary>
        private CxConfigManager CreateSelectedConfig(out string configId)
        {
            string root = Path.Combine(GetDemoDataRoot(), "Configs", "Samples");
            CxConfigManager manager = new CxConfigManager(root);
            switch (ConfigDemoKind.SelectedIndex)
            {
                case 1:
                    configId = "Samples.Automation.Device01";
                    manager.Register<CxAutomationDeviceOptions>(configId,
                        validator: CxAutomationDeviceOptions.Validate);
                    break;
                case 2:
                    configId = "Samples.Vision.Inspection";
                    manager.Register<CxVisionInspectionOptions>(configId,
                        validator: CxVisionInspectionOptions.Validate);
                    break;
                case 3:
                    configId = "Samples.CT3D.Reconstruction";
                    manager.Register<CxCtReconstructionOptions>(configId,
                        validator: CxCtReconstructionOptions.Validate);
                    break;
                default:
                    configId = "Studio.Demo";
                    manager.Register<StudioDemoConfig>(configId);
                    break;
            }
            manager.Initialize();
            return manager;
        }

        private void OnConfigDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                string id;
                CxConfigManager manager = CreateSelectedConfig(out id);
                bool saved;
                switch (ConfigDemoKind.SelectedIndex)
                {
                    case 1:
                        saved = CxConfigEditorDialog.Edit<CxAutomationDeviceOptions>(this, manager, id);
                        break;
                    case 2:
                        saved = CxConfigEditorDialog.Edit<CxVisionInspectionOptions>(this, manager, id);
                        break;
                    case 3:
                        saved = CxConfigEditorDialog.Edit<CxCtReconstructionOptions>(this, manager, id);
                        break;
                    default:
                        saved = CxConfigEditorDialog.Edit<StudioDemoConfig>(this, manager, id);
                        break;
                }
                if (saved)
                    MessageBox.Show(this, "已校验并保存配置。\n" + id,
                        "配置L1", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "配置编辑失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPublishConfigDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                string id;
                CxConfigManager manager = CreateSelectedConfig(out id);
                string root = GetDemoDataRoot();
                string releaseId = "demo-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") +
                    "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                CxConfigReleasePackage package = CxConfigReleaseBuilder.Build(manager, new[] { id },
                    Path.Combine(root, "ReleasePackages", "Samples"), releaseId);
                CxConfigReleaseReceiver receiver = new CxConfigReleaseReceiver(
                    Path.Combine(root, "Runtime", "Samples"));
                CxConfigPublishReport report = CxConfigPublisher.Publish(package.Path, receiver);
                if (!report.Success) throw new InvalidOperationException(report.Error);
                MessageBox.Show(this, "发布并激活本地运行快照成功。\n配置：" + id +
                    "\n版本：" + manager.GetRevision(id) + "\n发布：" + receiver.ActiveReleaseId +
                    "\n此示例不自动操作实际设备。", "配置L4", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "配置发布失败（旧运行版本未自动删除）",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRollbackConfigDemo(object sender, RoutedEventArgs e)
        {
            try
            {
                CxConfigReleaseReceiver receiver = new CxConfigReleaseReceiver(
                    Path.Combine(GetDemoDataRoot(), "Runtime", "Samples"));
                receiver.Rollback();
                MessageBox.Show(this, "当前本地运行发布版本：" +
                    (receiver.ActiveReleaseId ?? "(无)") + "\n此示例不操作实际设备。",
                    "回滚结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "配置回滚失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
