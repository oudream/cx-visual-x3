using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 原生WPF三维空间切片场景：真实RGB切片贴在XY/XZ/YZ空间平面。
    /// 这是可运行的三维切片浏览，不是GLSL体绘；不能以它替代旧Raymarch。
    /// 不持有体数据或工程状态，调用方直接提供图像与三刀位置。
    /// </summary>
    public sealed class MPWVolumeSliceSceneControl : Grid
    {
        private readonly Viewport3D _viewport = new Viewport3D();
        private readonly Model3DGroup _models = new Model3DGroup();
        private readonly PerspectiveCamera _camera = new PerspectiveCamera();
        private readonly Canvas _orientation = new Canvas { Width=108,Height=108,
            HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,
            Margin=new Thickness(4),Background=new SolidColorBrush(Color.FromArgb(165,4,10,18)) };
        private readonly Line[] _axisLines=new Line[3];
        private readonly TextBlock[] _axisLabels=new TextBlock[3];
        private Point _dragOrigin;
        private bool _dragging;
        private double _yaw = 37.0;
        private double _pitch = 23.0;
        private double _distance = 3.5;

        public MPWVolumeSliceSceneControl()
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(8, 14, 22));
            ClipToBounds = true;
            AutomationProperties.SetAutomationId(this, "TCT.Volume.Scene3D");
            _models.Children.Add(new AmbientLight(Colors.White));
            ModelVisual3D scene = new ModelVisual3D { Content = _models };
            _viewport.Children.Add(scene);
            _viewport.Camera = _camera;
            Children.Add(_viewport);
            Border label = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(130, 5, 9, 17)),
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = "3D 空间切面 · 拖动旋转 / 滚轮缩放",
                    Foreground = Brushes.LightSteelBlue, FontSize = 12
                }
            };
            Children.Add(label);
            Brush[] axisColors={Brushes.IndianRed,Brushes.LimeGreen,Brushes.DodgerBlue};
            string[] axisTitles={"X","Y","Z"};
            for(int i=0;i<3;i++)
            {
                Line line=new Line { Stroke=axisColors[i],StrokeThickness=2.5,
                    StrokeEndLineCap=PenLineCap.Triangle };
                _axisLines[i]=line;_orientation.Children.Add(line);
                TextBlock caption=new TextBlock { Text=axisTitles[i],Foreground=axisColors[i],
                    FontWeight=FontWeights.Bold,FontSize=12 };
                _axisLabels[i]=caption;_orientation.Children.Add(caption);
            }
            _orientation.MouseLeftButtonDown+=(s,e)=>{
                if(e.ClickCount>=2)ResetCamera();
                e.Handled=true;
            };
            Children.Add(_orientation);
            MouseLeftButtonDown += OnDown;
            MouseLeftButtonUp += OnUp;
            MouseMove += OnMove;
            MouseWheel += OnWheel;
            LostMouseCapture += delegate { _dragging = false; };
            ResetCamera();
        }

        public int SlicePlaneCount { get { return Math.Max(0, _models.Children.Count - 1); } }
        public double CameraDistance { get { return _distance; } }
        /// <summary>专用窗口验收：查看指定切面的实际世界宽度，而非判断截图是否存在。</summary>
        public double GetPlaneHorizontalSpan(int index)
        {
            if(index<0 || index>2 || _models.Children.Count<4)
                throw new ArgumentOutOfRangeException("index");
            GeometryModel3D model=_models.Children[index+1] as GeometryModel3D;
            MeshGeometry3D mesh=model==null ? null : model.Geometry as MeshGeometry3D;
            if(mesh==null || mesh.Positions.Count<2)throw new InvalidOperationException("切面未建立。");
            return (mesh.Positions[1]-mesh.Positions[0]).Length;
        }
        public int OrientationAxisCount { get { return _axisLines.Length; } }
        public void SetOrientationVisible(bool visible)
        {
            _orientation.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        }

        /// <summary>清空空间切片贴图；窗口关闭时释放GPU纹理的WPF引用。</summary>
        public void Clear()
        {
            while (_models.Children.Count > 1) _models.Children.RemoveAt(1);
        }

        public void ResetCamera()
        {
            _yaw = 37.0; _pitch = 23.0; _distance = 3.5;
            UpdateCamera();
        }

        public void SetSlices(FIMImageBuffer xy, FIMImageBuffer xz, FIMImageBuffer yz,
            int positionX, int positionY, int positionZ,
            int width, int height, int depth,
            double spacingX, double spacingY, double spacingZ)
        {
            if (xy == null || xz == null || yz == null)
                throw new ArgumentNullException("xy/xz/yz");
            if (width <= 0 || height <= 0 || depth <= 0 ||
                spacingX <= 0 || spacingY <= 0 || spacingZ <= 0)
                throw new ArgumentOutOfRangeException("width", "原始体数据尺寸与Spacing必须为正。");
            if (xy.PixelFormat != FIMPixelFormat.Bgr24 ||
                xz.PixelFormat != FIMPixelFormat.Bgr24 ||
                yz.PixelFormat != FIMPixelFormat.Bgr24)
                throw new ArgumentException("三维切面需要Bgr24图像。");
            double sx = width * spacingX, sy = height * spacingY, sz = depth * spacingZ;
            double divisor = Math.Max(sx, Math.Max(sy, sz));
            sx /= divisor; sy /= divisor; sz /= divisor;
            double x = sx * (Clamp(positionX, width) - 0.5);
            double y = sy * (0.5 - Clamp(positionY, height));
            double z = sz * (Clamp(positionZ, depth) - 0.5);
            while (_models.Children.Count > 1) _models.Children.RemoveAt(1);
            AddPlane(xy,
                new Point3D(-sx/2, sy/2, z), new Point3D(sx/2, sy/2, z),
                new Point3D(sx/2, -sy/2, z), new Point3D(-sx/2, -sy/2, z));
            AddPlane(xz,
                new Point3D(-sx/2, y, sz/2), new Point3D(sx/2, y, sz/2),
                new Point3D(sx/2, y, -sz/2), new Point3D(-sx/2, y, -sz/2));
            AddPlane(yz,
                new Point3D(x, sy/2, sz/2), new Point3D(x, -sy/2, sz/2),
                new Point3D(x, -sy/2, -sz/2), new Point3D(x, sy/2, -sz/2));
        }

        /// <summary>
        /// 旧FourViewLayout的关键能力：三张3D正交面跟随各自2D观察窗口，
        /// 而非固定整张体切片。纹理保持同一FIM图像，平移/缩放只重设Mesh与UV。
        /// 不裁剪到[0,1]，以保留二维视口超出原体积框的空白范围。
        /// </summary>
        public void SetSliceViewports(MVISliceViewport xy,MVISliceViewport xz,
            MVISliceViewport yz,int x,int y,int z,
            int width,int height,int depth,double spacingX,double spacingY,double spacingZ)
        {
            if(_models.Children.Count<4 || width<=0 || height<=0 || depth<=0)return;
            double wx=width*spacingX,wy=height*spacingY,wz=depth*spacingZ;
            double scale=Math.Max(wx,Math.Max(wy,wz));
            wx/=scale;wy/=scale;wz/=scale;
            double px=wx*(Clamp(x,width)-.5);
            double py=wy*(.5-Clamp(y,height));
            double pz=wz*(Clamp(z,depth)-.5);
            SetMeshViewport(1,xy,
                new Point3D(wx*(xy.MinA-.5),wy*(.5-xy.MinB),pz),
                new Point3D(wx*(xy.MaxA-.5),wy*(.5-xy.MinB),pz),
                new Point3D(wx*(xy.MaxA-.5),wy*(.5-xy.MaxB),pz),
                new Point3D(wx*(xy.MinA-.5),wy*(.5-xy.MaxB),pz));
            SetMeshViewport(2,xz,
                new Point3D(wx*(xz.MinA-.5),py,wz*(.5-xz.MinB)),
                new Point3D(wx*(xz.MaxA-.5),py,wz*(.5-xz.MinB)),
                new Point3D(wx*(xz.MaxA-.5),py,wz*(.5-xz.MaxB)),
                new Point3D(wx*(xz.MinA-.5),py,wz*(.5-xz.MaxB)));
            SetMeshViewport(3,yz,
                new Point3D(px,wy*(.5-yz.MinA),wz*(.5-yz.MinB)),
                new Point3D(px,wy*(.5-yz.MaxA),wz*(.5-yz.MinB)),
                new Point3D(px,wy*(.5-yz.MaxA),wz*(.5-yz.MaxB)),
                new Point3D(px,wy*(.5-yz.MinA),wz*(.5-yz.MaxB)));
        }

        private void SetMeshViewport(int index,MVISliceViewport bounds,
            Point3D p0,Point3D p1,Point3D p2,Point3D p3)
        {
            GeometryModel3D plane=_models.Children[index] as GeometryModel3D;
            MeshGeometry3D mesh=plane==null ? null : plane.Geometry as MeshGeometry3D;
            if(mesh==null)return;
            mesh.Positions=new Point3DCollection(new[]{p0,p1,p2,p3});
            mesh.TextureCoordinates=new PointCollection(new[]
            {
                new Point(bounds.MinA,bounds.MinB),
                new Point(bounds.MaxA,bounds.MinB),
                new Point(bounds.MaxA,bounds.MaxB),
                new Point(bounds.MinA,bounds.MaxB)
            });
        }

        /// <summary>额外任意切图像与三正交面共存在一个WPF Viewport3D中；传入世界坐标四角。</summary>
        public void SetObliqueSlice(FIMImageBuffer image, Point3D topLeft,
            Point3D topRight, Point3D bottomRight, Point3D bottomLeft)
        {
            while (_models.Children.Count > 4) _models.Children.RemoveAt(4);
            if (image == null) return;
            if (image.PixelFormat != FIMPixelFormat.Bgr24)
                throw new ArgumentException("任意切预览必须为Bgr24像素缓冲。");
            AddPlane(image,topLeft,topRight,bottomRight,bottomLeft);
        }

        private static double Clamp(int index, int count)
        {
            return Math.Max(0.0, Math.Min(1.0, index / (double)Math.Max(1, count - 1)));
        }

        private void AddPlane(FIMImageBuffer pixels, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            mesh.Positions = new Point3DCollection(new[] { p0, p1, p2, p3 });
            mesh.TextureCoordinates = new PointCollection(new[]
            {
                new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1)
            });
            mesh.TriangleIndices = new Int32Collection(new[] { 0, 1, 2, 0, 2, 3 });
            BitmapSource bitmap = BitmapSource.Create(
                pixels.Width, pixels.Height, 96, 96,
                PixelFormats.Bgr24, null, pixels.CopyPixels(), pixels.Stride);
            bitmap.Freeze();
            ImageBrush brush = new ImageBrush(bitmap) { Opacity = 0.83 };
            DiffuseMaterial material = new DiffuseMaterial(brush);
            GeometryModel3D plane = new GeometryModel3D(mesh, material) { BackMaterial = material };
            _models.Children.Add(plane);
        }

        private void UpdateCamera()
        {
            double yaw = _yaw * Math.PI / 180.0;
            double pitch = _pitch * Math.PI / 180.0;
            double c = Math.Cos(pitch);
            Point3D pos = new Point3D(
                _distance * c * Math.Sin(yaw),
                _distance * Math.Sin(pitch),
                _distance * c * Math.Cos(yaw));
            _camera.Position = pos;
            _camera.LookDirection = new Vector3D(-pos.X, -pos.Y, -pos.Z);
            _camera.UpDirection = new Vector3D(0, 1, 0);
            _camera.FieldOfView = 42;
            _camera.NearPlaneDistance = 0.01;
            _camera.FarPlaneDistance = 30;
            UpdateOrientation(pos);
        }

        /// <summary>沿用旧 WinForms 方向标记的三轴投影，双击复位相机。</summary>
        private void UpdateOrientation(Point3D camera)
        {
            Vector3D forward=new Vector3D(-camera.X,-camera.Y,-camera.Z);
            forward.Normalize();
            Vector3D right=Vector3D.CrossProduct(forward,new Vector3D(0,1,0));
            right.Normalize();
            Vector3D up=Vector3D.CrossProduct(right,forward);
            Vector3D[] axes={new Vector3D(1,0,0),new Vector3D(0,1,0),new Vector3D(0,0,1)};
            for(int i=0;i<3;i++)
            {
                double sx=Vector3D.DotProduct(axes[i],right);
                double sy=Vector3D.DotProduct(axes[i],up);
                double near=Vector3D.DotProduct(axes[i],-forward);
                double len=29+near*5;
                double x=54+sx*len,y=60-sy*len;
                Line line=_axisLines[i];
                line.X1=54;line.Y1=60;line.X2=x;line.Y2=y;
                Canvas.SetLeft(_axisLabels[i],x+2);
                Canvas.SetTop(_axisLabels[i],y-9);
            }
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            _dragOrigin = e.GetPosition(this);
            _dragging = true;
            CaptureMouse();
            e.Handled = true;
        }
        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
        }
        private void OnMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point next = e.GetPosition(this);
            _yaw += (next.X - _dragOrigin.X) * 0.5;
            _pitch = Math.Max(-85, Math.Min(85, _pitch - (next.Y - _dragOrigin.Y) * 0.5));
            _dragOrigin = next;
            UpdateCamera();
            e.Handled = true;
        }
        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            _distance = Math.Max(1.25, Math.Min(12, _distance * (e.Delta > 0 ? 0.89 : 1.12)));
            UpdateCamera();
            e.Handled = true;
        }
    }
}
