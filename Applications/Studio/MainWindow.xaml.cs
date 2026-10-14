using System.Windows;
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
        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosed(e);
        }
        private void RefreshScene()
        { SceneHost.SetScene(_viewModel.Items, _viewModel.SelectedId); }
    }
}
