using System;
using System.IO;
using System.Windows;
using CxVisual.Mechanism.Core;
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
                // 演示配置存放用户可写目录；不会写入版本化的App-Vx.y程序目录。
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CxVisual", "Configs");
                CxConfigManager manager = new CxConfigManager(root);
                manager.Register<StudioDemoConfig>("Studio.Demo");
                manager.Initialize();
                CxConfigEditorDialog.Edit<StudioDemoConfig>(this, manager, "Studio.Demo");
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
