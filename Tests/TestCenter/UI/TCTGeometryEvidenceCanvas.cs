using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.UI
{
    /// <summary>Geometry observation is immutable; interaction affects only this view's viewport.</summary>
    public sealed class TCTGeometryEvidenceCanvas : UserControl
    {
        private readonly Canvas _canvas = new Canvas { Width = 700, Height = 410,
            Background = Brushes.WhiteSmoke, ClipToBounds = true };
        private readonly TextBlock _state = new TextBlock { Margin = new Thickness(6) };
        private readonly ScaleTransform _zoomTransform = new ScaleTransform(1, 1);
        private readonly TranslateTransform _panTransform = new TranslateTransform();
        private readonly TransformGroup _viewTransform = new TransformGroup();
        private readonly Canvas _layer = new Canvas { Width = 700, Height = 410 };
        private double _zoom = 1;
        private Point _dragOrigin;
        private bool _panning;
        private readonly Dictionary<string, CheckBox> _roles =
            new Dictionary<string, CheckBox>(StringComparer.Ordinal);
        private IList<TCTGeometryFigure> _figures = new List<TCTGeometryFigure>();
        public event Action<TCTGeometryFigure> FigureSelected;
        public int RenderedFigureCount { get; private set; }
        public int VisibleFigureCount { get; private set; }
        public double Zoom { get { return _zoom; } }
        public TCTGeometryEvidenceCanvas()
        {
            _viewTransform.Children.Add(_zoomTransform);
            _viewTransform.Children.Add(_panTransform);
            _layer.RenderTransform = _viewTransform;
            _canvas.Children.Add(_layer);
            Button plus = new Button { Content = "+ 放大", Margin = new Thickness(4), Padding = new Thickness(7, 3, 7, 3) };
            Button minus = new Button { Content = "− 缩小", Margin = new Thickness(4), Padding = new Thickness(7, 3, 7, 3) };
            Button fit = new Button { Content = "适合窗口", Margin = new Thickness(4), Padding = new Thickness(7, 3, 7, 3) };
            plus.Click += (s, e) => SetZoom(_zoom * 1.25);
            minus.Click += (s, e) => SetZoom(_zoom / 1.25);
            fit.Click += (s, e) => { SetZoom(1); _panTransform.X = 0; _panTransform.Y = 0; };
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(plus); buttons.Children.Add(minus); buttons.Children.Add(fit);
            buttons.Children.Add(_state);
            StackPanel layers = new StackPanel { Orientation = Orientation.Horizontal,
                Margin = new Thickness(6) };
            foreach (string role in new[] { "Input", "Expected", "Actual", "Diff" })
            {
                string capturedRole = role;
                CheckBox check = new CheckBox { Content = role, IsChecked = true,
                    Margin = new Thickness(8, 2, 8, 2) };
                _roles.Add(role, check);
                check.Checked += (sender, args) => RenderFigures();
                check.Unchecked += (sender, args) => RenderFigures();
                layers.Children.Add(check);
                System.Windows.Automation.AutomationProperties.SetAutomationId(check,
                    "TCT.Geometry.Layer." + capturedRole);
            }
            StackPanel panel = new StackPanel();
            panel.Children.Add(buttons); panel.Children.Add(layers); panel.Children.Add(_canvas);
            Content = panel;
            _canvas.MouseWheel += (s, e) => { SetZoom(_zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15)); e.Handled = true; };
            _canvas.MouseRightButtonDown += (s, e) =>
            { _dragOrigin = e.GetPosition(_canvas); _panning = true; _canvas.CaptureMouse(); e.Handled = true; };
            _canvas.MouseMove += (s, e) =>
            {
                if (!_panning) return;
                Point now = e.GetPosition(_canvas);
                _panTransform.X += now.X - _dragOrigin.X;
                _panTransform.Y += now.Y - _dragOrigin.Y;
                _dragOrigin = now;
            };
            _canvas.MouseRightButtonUp += (s, e) =>
            { _panning = false; _canvas.ReleaseMouseCapture(); e.Handled = true; };
            _canvas.LostMouseCapture += (s, e) => _panning = false;
            SetZoom(1);
        }

        private void SetZoom(double target)
        {
            _zoom = Math.Max(0.1, Math.Min(12, target));
            _zoomTransform.ScaleX = _zoom;
            _zoomTransform.ScaleY = _zoom;
            _state.Text = "Zoom " + _zoom.ToString("F2") + "×  ·  滚轮缩放 / 右键平移 / 点击图元查看Owner";
        }
        public void SetLayerVisible(string role, bool visible)
        {
            CheckBox check;
            if (!_roles.TryGetValue(role ?? "", out check))
                throw new ArgumentException("Unknown Geometry role", "role");
            check.IsChecked = visible;
        }
        public void ShowFigures(IList<TCTGeometryFigure> figures)
        {
            if (figures == null) throw new ArgumentNullException("figures");
            _figures = figures.ToArray();
            RenderFigures();
        }
        private void RenderFigures()
        {
            _layer.Children.Clear();
            RenderedFigureCount = _figures.Count;
            VisibleFigureCount = 0;
            if (_figures.Count == 0) return;
            IList<TCTGeometryFigure> figures = _figures;
            double left = figures.Min(f => Math.Min(f.X, f.X + f.Width));
            double top = figures.Min(f => Math.Min(f.Y, f.Y + f.Height));
            double right = figures.Max(f => Math.Max(f.X, f.X + f.Width));
            double bottom = figures.Max(f => Math.Max(f.Y, f.Y + f.Height));
            double scale = Math.Min(610 / Math.Max(1, right - left), 300 / Math.Max(1, bottom - top));
            if (double.IsNaN(scale) || double.IsInfinity(scale)) throw new InvalidOperationException("Geometry viewport cannot fit");
            scale = Math.Min(25, Math.Max(0.000001, scale));
            Func<double, double> sx = x => 44 + (x - left) * scale;
            Func<double, double> sy = y => 35 + (y - top) * scale;
            foreach (TCTGeometryFigure f in figures)
            {
                if (_roles[f.Role].IsChecked != true) continue;
                Brush stroke = f.Role == "Expected" ? Brushes.ForestGreen :
                    f.Role == "Actual" ? Brushes.IndianRed :
                    f.Role == "Diff" ? Brushes.DarkOrange : Brushes.SteelBlue;
                Shape shape;
                if (f.Kind == "Segment")
                {
                    shape = new Line { X1 = sx(f.X), Y1 = sy(f.Y),
                        X2 = sx(f.X + f.Width), Y2 = sy(f.Y + f.Height),
                        Stroke = stroke, StrokeThickness = 2.5 };
                }
                else
                {
                    shape = f.Kind == "Rectangle" ? (Shape)new Rectangle() : new Ellipse();
                    shape.Width = Math.Max(3, f.Kind == "Point" ? 8 : Math.Abs(f.Width) * scale);
                    shape.Height = Math.Max(3, f.Kind == "Point" ? 8 : Math.Abs(f.Height) * scale);
                    shape.Stroke = stroke;
                    shape.StrokeThickness = 2;
                    shape.Fill = f.Kind == "Point" ? stroke : Brushes.Transparent;
                    Canvas.SetLeft(shape, sx(f.X) - (f.Kind == "Point" ? 4 : 0));
                    Canvas.SetTop(shape, sy(f.Y) - (f.Kind == "Point" ? 4 : 0));
                }
                shape.ToolTip = f.Role + " · " + f.Label + " / " + f.SourceObjectId;
                TCTGeometryFigure selected = f;
                shape.MouseLeftButtonDown += (s, e) => {
                    if (FigureSelected != null) FigureSelected(selected);
                    e.Handled = true;
                };
                _layer.Children.Add(shape);
                VisibleFigureCount++;
                TextBlock label = new TextBlock { Text = f.Role + ": " + f.Label,
                    Foreground = stroke, FontSize = 11 };
                Canvas.SetLeft(label, sx(f.X) + 7);
                Canvas.SetTop(label, sy(f.Y) + 8);
                _layer.Children.Add(label);
            }
        }
    }
}
