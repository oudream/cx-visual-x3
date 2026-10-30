using System;
using System.Collections.Generic;
using System.Numerics;
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
using CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 原生WPF三维空间切片场景：真实RGB切片贴在XY/XZ/YZ空间平面。
    /// 这是可运行的三维切片浏览，不是GLSL体绘；不能以它替代旧Raymarch。
    /// 不持有体数据或工程状态，调用方直接提供图像与三刀位置。
    /// </summary>
    /// <summary>显示网格鼠标命中射线，非工业计量值和非原Mesh面片身份。</summary>
    public sealed class MPWVolumeMeshRayEventArgs : EventArgs
    {
        public MPWVolumeMeshRayEventArgs(Point3D origin,Point3D hit)
        {DisplayOrigin=origin;DisplayHit=hit;}
        public Point3D DisplayOrigin {get;private set;}
        public Point3D DisplayHit {get;private set;}
    }

    public sealed class MPWVolumeSliceSceneControl : Grid
    {
        private readonly Viewport3D _viewport = new Viewport3D();
        private readonly Model3DGroup _models = new Model3DGroup();
        // 独立于三刀/任意切Mesh，防止换层时清除专业可视点。
        private readonly ModelVisual3D _surfaceOverlay = new ModelVisual3D();
        private readonly ModelVisual3D _meshOverlay = new ModelVisual3D();
        // 显示级高亮不承接原Mesh顶点索引、原值计量或MEM。
        private readonly ModelVisual3D _selectedFaceOverlay=new ModelVisual3D();
        private MPWVolumeDisplayMesh _selectedFacePreview;
        /// <summary>真实WPF MeshGeometry3D当前生成的高亮三角面数，而不是输入请求计数。</summary>
        public int SelectedFaceHighlightTriangleCount
        {
            get
            {
                var model=_selectedFaceOverlay.Content as GeometryModel3D;
                var mesh=model==null?null:model.Geometry as MeshGeometry3D;
                return mesh==null?0:mesh.TriangleIndices.Count/3;
            }
        }
        private readonly Dictionary<Guid,ModelVisual3D> _additionalMeshes=
            new Dictionary<Guid,ModelVisual3D>();
        private readonly Dictionary<Guid,int> _additionalMeshTriangles=
            new Dictionary<Guid,int>();
        private readonly Dictionary<Guid,ModelVisual3D> _additionalPointClouds=
            new Dictionary<Guid,ModelVisual3D>();
        private readonly Dictionary<Guid,int> _additionalPointCloudSizes=
            new Dictionary<Guid,int>();
        private MVI3DSceneState _pointCloudScene;
        private Func<MVI3DSceneItem,IReadOnlyList<Vector3>> _pointCloudResolver;
        private readonly Dictionary<Guid,string> _pointCloudKeys=new Dictionary<Guid,string>();

        /// <summary>MVI是点云实例可视状态的唯一Owner；WPF不持有专业点云资产。</summary>
        public void BindAdditionalPointClouds(MVI3DSceneState scene,
            Func<MVI3DSceneItem,IReadOnlyList<Vector3>> resolver)
        {
            Dispatcher.VerifyAccess();
            if(scene!=null&&resolver==null)throw new ArgumentNullException("resolver");
            if(_pointCloudScene!=null)_pointCloudScene.Changed-=PointCloudSceneChanged;
            foreach(Guid id in new List<Guid>(_pointCloudKeys.Keys))
                RemoveAdditionalPointCloud(id);
            _pointCloudKeys.Clear();
            _pointCloudScene=scene;_pointCloudResolver=resolver;
            if(scene!=null)
            {
                scene.Changed+=PointCloudSceneChanged;
                SynchronizePointClouds();
            }
        }
        private void PointCloudSceneChanged(object sender,EventArgs e)
        {
            if(Dispatcher.CheckAccess())SynchronizePointClouds();
            else Dispatcher.BeginInvoke(new Action(SynchronizePointClouds));
        }
        private void SynchronizePointClouds()
        {
            Dispatcher.VerifyAccess();
            if(_pointCloudScene==null)return;
            var wanted=new Dictionary<Guid,Tuple<string,IReadOnlyList<Vector3>,double>>();
            int count=0;
            foreach(MVI3DSceneItem item in _pointCloudScene.Snapshot())
            {
                if(item.Kind!=MVI3DSceneKind.PointCloud||!item.Visible)continue;
                string key=item.SourceRef+":"+item.SourceRevision+":"+item.SpatialFrameRef;
                string oldKey;
                bool unchanged=_pointCloudKeys.TryGetValue(item.Id,out oldKey)&&oldKey==key;
                IReadOnlyList<Vector3> points=unchanged?null:_pointCloudResolver(item);
                if(!unchanged&&points==null)continue;
                int size=0;
                if(unchanged)_additionalPointCloudSizes.TryGetValue(item.Id,out size);
                else size=points.Count;
                count=checked(count+size);
                if(count>12000)
                    throw new InvalidOperationException("三维PointCloud场景合计超过12000显示点预算。");
                wanted.Add(item.Id,Tuple.Create(key,points,item.Opacity));
            }
            foreach(Guid id in new List<Guid>(_pointCloudKeys.Keys))
            {
                if(wanted.ContainsKey(id))continue;
                RemoveAdditionalPointCloud(id);
                _pointCloudKeys.Remove(id);
            }
            foreach(var pair in wanted)
            {
                string oldKey;
                if(_pointCloudKeys.TryGetValue(pair.Key,out oldKey)&&
                    oldKey==pair.Value.Item1)
                    SetAdditionalPointCloudOpacity(pair.Key,pair.Value.Item3);
                else
                {
                    SetAdditionalPointCloud(pair.Key,pair.Value.Item2,pair.Value.Item3);
                    _pointCloudKeys[pair.Key]=pair.Value.Item1;
                }
            }
        }
        private void SetAdditionalPointCloudOpacity(Guid id,double opacity)
        {
            ModelVisual3D visual;
            if(!_additionalPointClouds.TryGetValue(id,out visual))return;
            var model=visual.Content as GeometryModel3D;
            var material=model==null?null:model.Material as DiffuseMaterial;
            var brush=material==null?null:material.Brush as SolidColorBrush;
            if(brush!=null&&brush.Opacity!=opacity)brush.Opacity=opacity;
        }

        public int AdditionalPointCloudCount {get{return _additionalPointClouds.Count;}}
        public int AdditionalPointCount
        {
            get
            {
                int count=0;
                foreach(int size in _additionalPointCloudSizes.Values)count=checked(count+size);
                return count;
            }
        }
        private int _rawWidth,_rawHeight,_rawDepth;
        /// <summary>Ctrl+Shift点击真实表面Mesh后发出显示射线；交由CT3D在原值网格再次拾取。</summary>
        public event EventHandler<MPWVolumeMeshRayEventArgs> MeshFaceRayRequested;

        /// <summary>按当前真实Volume尺寸及各向异性显示比例还原体素坐标。</summary>
        public Point3D DisplayPointToVoxel(Point3D display)
        {
            Dispatcher.VerifyAccess();
            if(_rawWidth<=0||_rawHeight<=0||_rawDepth<=0||
                _displaySx<=0||_displaySy<=0||_displaySz<=0)
                throw new InvalidOperationException("尚未绑定有效原Volume显示空间。");
            return new Point3D(
                (display.X/_displaySx+.5)*_rawWidth-.5,
                (-display.Y/_displaySy+.5)*_rawHeight-.5,
                (display.Z/_displaySz+.5)*_rawDepth-.5);
        }

        /// <summary>只接受最前面的可见主Mesh命中，防止透过切片或其他对象拾取。</summary>
        public bool TryGetSurfaceMeshDisplayRay(Point point,out Point3D origin,out Point3D hit)
        {
            Dispatcher.VerifyAccess();
            origin=new Point3D();hit=new Point3D();
            if(_meshOverlay.Content==null||_rawWidth<=0||
                double.IsNaN(point.X)||double.IsInfinity(point.X)||
                double.IsNaN(point.Y)||double.IsInfinity(point.Y)||
                _viewport.ActualWidth<=0||_viewport.ActualHeight<=0||
                point.X<0||point.Y<0||point.X>=_viewport.ActualWidth||
                point.Y>=_viewport.ActualHeight)return false;
            bool matched=false;
            Point3D contact=new Point3D();
            VisualTreeHelper.HitTest(_viewport,null,result=>
            {
                var mesh=result as RayMeshGeometry3DHitTestResult;
                if(mesh==null)return HitTestResultBehavior.Continue;
                if(Object.ReferenceEquals(mesh.ModelHit,_meshOverlay.Content)||
                    Object.ReferenceEquals(mesh.ModelHit,_selectedFaceOverlay.Content))
                {matched=true;contact=mesh.PointHit;}
                return HitTestResultBehavior.Stop;
            },new PointHitTestParameters(point));
            if(!matched)return false;
            origin=_camera.Position;hit=contact;
            return true;
        }

        private MVI3DSceneState _selectionScene;

        /// <summary>Ctrl+点击真实3D几何选择唯一MVI实例；WPF只消费场景引用。</summary>
        public void BindSceneSelection(MVI3DSceneState scene)
        {
            Dispatcher.VerifyAccess();
            _selectionScene=scene;
        }
        public bool TrySelectSceneAtPoint(Point viewportPoint)
        {
            Dispatcher.VerifyAccess();
            if(_selectionScene==null||(double.IsNaN(viewportPoint.X)||double.IsInfinity(viewportPoint.X))||
                (double.IsNaN(viewportPoint.Y)||double.IsInfinity(viewportPoint.Y))||_viewport.ActualWidth<=0||
                _viewport.ActualHeight<=0||viewportPoint.X<0||viewportPoint.Y<0||
                viewportPoint.X>=_viewport.ActualWidth||
                viewportPoint.Y>=_viewport.ActualHeight)return false;
            Guid selected=Guid.Empty;
            VisualTreeHelper.HitTest(_viewport,null,result=>
            {
                var mesh=result as RayMeshGeometry3DHitTestResult;
                if(mesh==null)return HitTestResultBehavior.Continue;
                foreach(var pair in _additionalMeshes)
                {
                    if(!Object.ReferenceEquals(pair.Value.Content,mesh.ModelHit))continue;
                    MVI3DSceneItem item;
                    if(_selectionScene.TryGet(pair.Key,out item)&&item.Visible)
                    {
                        selected=pair.Key;return HitTestResultBehavior.Stop;
                    }
                }
                foreach(var pair in _additionalPointClouds)
                {
                    if(!Object.ReferenceEquals(pair.Value.Content,mesh.ModelHit))continue;
                    MVI3DSceneItem cloudItem;
                    if(_selectionScene.TryGet(pair.Key,out cloudItem)&&cloudItem.Visible&&
                        cloudItem.Kind==MVI3DSceneKind.PointCloud)
                    {
                        selected=pair.Key;return HitTestResultBehavior.Stop;
                    }
                }
                MVI3DSceneKind kind;
                if(Object.ReferenceEquals(_meshOverlay.Content,mesh.ModelHit))
                    kind=MVI3DSceneKind.Mesh;
                else if(Object.ReferenceEquals(_surfaceOverlay.Content,mesh.ModelHit))
                    kind=MVI3DSceneKind.Surface;
                else return HitTestResultBehavior.Continue;
                Guid match=Guid.Empty;
                foreach(MVI3DSceneItem item in _selectionScene.Snapshot())
                {
                    if(!item.Visible||item.Kind!=kind)continue;
                    if(match!=Guid.Empty)return HitTestResultBehavior.Stop;
                    match=item.Id;
                }
                if(match!=Guid.Empty)selected=match;
                return HitTestResultBehavior.Stop;
            },new PointHitTestParameters(viewportPoint));
            if(selected==Guid.Empty)return false;
            _selectionScene.Select(selected);
            return true;
        }

        private MVI3DSceneState _additionalScene;
        private Func<MVI3DSceneItem,MPWVolumeDisplayMesh> _additionalMeshResolver;
        private readonly HashSet<Guid> _sceneMeshIds=new HashSet<Guid>();
        private readonly Dictionary<Guid,string> _sceneMeshKeys=
            new Dictionary<Guid,string>();

        /// <summary>正式MVI场景是唯一显隐/透明度Owner；WPF仅消费其专业几何显示投影。</summary>
        public void BindAdditionalMeshes(MVI3DSceneState scene,
            Func<MVI3DSceneItem,MPWVolumeDisplayMesh> resolver)
        {
            Dispatcher.VerifyAccess();
            if(scene!=null&&resolver==null)throw new ArgumentNullException("resolver");
            if(_additionalScene!=null)_additionalScene.Changed-=AdditionalSceneChanged;
            foreach(Guid id in new List<Guid>(_sceneMeshIds))RemoveAdditionalMesh(id);
            _sceneMeshIds.Clear();
            _sceneMeshKeys.Clear();
            _additionalScene=scene;_additionalMeshResolver=resolver;
            if(scene!=null)
            {
                scene.Changed+=AdditionalSceneChanged;
                SynchronizeAdditionalMeshes();
            }
        }
        private void AdditionalSceneChanged(object sender,EventArgs e)
        {
            if(Dispatcher.CheckAccess())SynchronizeAdditionalMeshes();
            else Dispatcher.BeginInvoke(new Action(SynchronizeAdditionalMeshes));
        }
        private void SynchronizeAdditionalMeshes()
        {
            Dispatcher.VerifyAccess();
            if(_additionalScene==null)return;
            var wanted=new Dictionary<Guid,Tuple<string,MPWVolumeDisplayMesh,double>>();
            int triangles=SurfaceMeshTriangleCount;
            foreach(MVI3DSceneItem item in _additionalScene.Snapshot())
            {
                if(!item.Visible||(item.Kind!=MVI3DSceneKind.Mesh&&
                    item.Kind!=MVI3DSceneKind.Cad&&
                    item.Kind!=MVI3DSceneKind.Roi&&
                    item.Kind!=MVI3DSceneKind.Mask))continue;
                string key=item.Kind+":"+item.SourceRef+":"+item.SourceRevision+":"+
                    item.SpatialFrameRef;
                string old;
                bool unchanged=_sceneMeshKeys.TryGetValue(item.Id,out old)&&old==key;
                MPWVolumeDisplayMesh geometry=unchanged?null:_additionalMeshResolver(item);
                if(!unchanged&&geometry==null)continue;
                int count=0;
                if(unchanged)_additionalMeshTriangles.TryGetValue(item.Id,out count);
                else count=geometry.TriangleCount;
                triangles=checked(triangles+count);
                if(triangles>50000)
                    throw new InvalidOperationException("MVI多对象WPF Mesh总预算超过50000三角面。");
                wanted.Add(item.Id,Tuple.Create(key,geometry,item.Opacity));
            }
            // 先移除已失效或来源改变的旧显示资源，避免合法批次在中间状态超预算。
            foreach(Guid id in new List<Guid>(_sceneMeshIds))
            {
                Tuple<string,MPWVolumeDisplayMesh,double> planned;
                string old;
                if(wanted.TryGetValue(id,out planned)&&
                    _sceneMeshKeys.TryGetValue(id,out old)&&old==planned.Item1)continue;
                RemoveAdditionalMesh(id);
                _sceneMeshIds.Remove(id);
                _sceneMeshKeys.Remove(id);
            }
            foreach(var pair in wanted)
            {
                string old;
                if(_sceneMeshKeys.TryGetValue(pair.Key,out old)&&old==pair.Value.Item1)
                    SetAdditionalMeshOpacity(pair.Key,pair.Value.Item3);
                else
                {
                    SetAdditionalMesh(pair.Key,pair.Value.Item2,pair.Value.Item3);
                    _sceneMeshIds.Add(pair.Key);
                    _sceneMeshKeys[pair.Key]=pair.Value.Item1;
                }
            }
        }
        private void SetAdditionalMeshOpacity(Guid id,double opacity)
        {
            ModelVisual3D item;
            if(!_additionalMeshes.TryGetValue(id,out item))return;
            var model=item.Content as GeometryModel3D;
            var diffuse=model==null?null:model.Material as DiffuseMaterial;
            var brush=diffuse==null?null:diffuse.Brush as SolidColorBrush;
            if(brush!=null&&brush.Opacity!=opacity)brush.Opacity=opacity;
        }

        public int AdditionalMeshCount {get{return _additionalMeshes.Count;}}
        public int AdditionalMeshTriangleCount
        {
            get
            {
                int count=0;
                foreach(int value in _additionalMeshTriangles.Values)count=checked(count+value);
                return count;
            }
        }
        public int SurfaceMeshTriangleCount {get;private set;}
        private double _displaySx=1,_displaySy=1,_displaySz=1;
        public int SurfacePreviewPointCount {get;private set;}
        private readonly PerspectiveCamera _camera = new PerspectiveCamera();
        private readonly Canvas _orientation = new Canvas { Width=108,Height=108,
            HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,
            Margin=new Thickness(4),Background=new SolidColorBrush(Color.FromArgb(165,4,10,18)) };
        private readonly Line[] _axisLines=new Line[3];
        private readonly TextBlock[] _axisLabels=new TextBlock[3];
        private Point _dragOrigin;
        private bool _dragging;
        private bool _panning;
        private Vector3D _cameraTarget=new Vector3D();
        private double _yaw = 37.0;
        private double _pitch = 23.0;
        private double _distance = 3.5;
        private MVI3DCameraState _cameraState;

        /// <summary>显式绑定MVI会话相机；WPF只缓存用于渲染的矩阵，解绑时解除订阅。</summary>
        public void BindCamera(MVI3DCameraState camera)
        {
            if(Object.ReferenceEquals(_cameraState,camera))return;
            if(_cameraState!=null)_cameraState.Changed-=OnCameraChanged;
            _cameraState=camera;
            if(_cameraState!=null)
            {
                _cameraState.Changed+=OnCameraChanged;
                OnCameraChanged(_cameraState,EventArgs.Empty);
            }
        }
        private void OnCameraChanged(object sender,EventArgs args)
        {
            if(_cameraState==null)return;
            _yaw=_cameraState.YawRadians*180/Math.PI;
            _pitch=_cameraState.PitchRadians*180/Math.PI;
            _distance=_cameraState.Distance;
            _cameraTarget=new Vector3D(_cameraState.TargetX,_cameraState.TargetY,_cameraState.TargetZ);
            UpdateCamera();
        }

        public MPWVolumeSliceSceneControl()
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(8, 14, 22));
            ClipToBounds = true;
            AutomationProperties.SetAutomationId(this, "TCT.Volume.Scene3D");
            _models.Children.Add(new AmbientLight(Colors.White));
            ModelVisual3D scene = new ModelVisual3D { Content = _models };
            _viewport.Children.Add(scene);
            _viewport.Children.Add(_surfaceOverlay);
            _viewport.Children.Add(_meshOverlay);
            _viewport.Children.Add(_selectedFaceOverlay);
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
            MouseRightButtonDown += OnRightDown;
            MouseRightButtonUp += OnUp;
            MouseMove += OnMove;
            MouseWheel += OnWheel;
            LostMouseCapture += delegate { _dragging = false; };
            ResetCamera();
        }

        public int SlicePlaneCount { get { return Math.Max(0, _models.Children.Count - 1); } }
        public double CameraDistance { get { return _distance; } }
        public Vector3D CameraTarget { get { return _cameraTarget; } }
        public void Orbit(double dx,double dy)
        {
            if(_cameraState!=null)
            {
                _cameraState.Orbit(dx*.007,-dy*.007);
                return;
            }
            _yaw+=dx*.5;_pitch=Math.Max(-85,Math.Min(85,_pitch-dy*.5));
            UpdateCamera();
        }
        public void Zoom(double wheelSteps)
        {
            if(_cameraState!=null)
            {
                _cameraState.Zoom(wheelSteps);
                return;
            }
            _distance=Math.Max(.55,Math.Min(16,_distance*Math.Pow(.89,wheelSteps)));
            UpdateCamera();
        }
        public void Pan(double dx,double dy)
        {
            if(_cameraState!=null)
            {
                _cameraState.Pan(dx,dy,Math.Max(1,ActualHeight));
                return;
            }
            double yaw=_yaw*Math.PI/180,pitch=_pitch*Math.PI/180;
            Vector3D right=new Vector3D(Math.Cos(yaw),0,-Math.Sin(yaw));
            Vector3D up=new Vector3D(-Math.Sin(yaw)*Math.Sin(pitch),Math.Cos(pitch),
                -Math.Cos(yaw)*Math.Sin(pitch));
            double scale=2*_distance*Math.Tan(42*Math.PI/360)/Math.Max(1,ActualHeight);
            _cameraTarget+=(-right*dx+up*dy)*scale;
            UpdateCamera();
        }
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
            ClearSelectedMeshFaces();
            _rawWidth=0;_rawHeight=0;_rawDepth=0;
            while (_models.Children.Count > 1) _models.Children.RemoveAt(1);
            ClearSurfacePoints();
            BindAdditionalMeshes(null,null);
            BindAdditionalPointClouds(null,null);
            BindSceneSelection(null);
            ClearSurfaceMesh();
            ClearAdditionalMeshes();
            ClearAdditionalPointClouds();
        }

        /// <summary>索引三角Mesh真正进入WPF Viewport3D，不退化为点云伪显示。</summary>
        public void SetSurfaceMesh(MPWVolumeDisplayMesh preview)
        {
            Dispatcher.VerifyAccess();
            if(preview==null)throw new ArgumentNullException("preview");
            if(preview.TriangleCount+AdditionalMeshTriangleCount>50000)
                throw new ArgumentOutOfRangeException("preview","全部WPF三角面总量超过50000预算。");
            _meshOverlay.Content=BuildMeshModel(preview,1);
            SurfaceMeshTriangleCount=preview.TriangleCount;
            RefreshSelectedFaceHighlight();
        }

        /// <summary>显示级专业面片高亮；最多512三角面，其余已选面仍保留专业计算身份。</summary>
        public void SetSelectedMeshFaces(MPWVolumeDisplayMesh preview)
        {
            Dispatcher.VerifyAccess();
            if(preview==null)throw new ArgumentNullException("preview");
            if(preview.TriangleCount<1||preview.TriangleCount>512)
                throw new ArgumentOutOfRangeException("preview");
            _selectedFacePreview=preview;
            RefreshSelectedFaceHighlight();
        }

        public void ClearSelectedMeshFaces()
        {
            Dispatcher.VerifyAccess();
            _selectedFacePreview=null;
            _selectedFaceOverlay.Content=null;
        }

        /// <summary>可视面相对原Mesh向相机方向微偏移避免共面闪烁，不改变专业几何。</summary>
        private void RefreshSelectedFaceHighlight()
        {
            if(_selectedFacePreview==null||_meshOverlay.Content==null)
            {_selectedFaceOverlay.Content=null;return;}
            var positions=new Point3DCollection();
            var triangles=new Int32Collection();
            for(int i=0;i<_selectedFacePreview.TriangleIndices.Count;i+=3)
            {
                Vector3 a=_selectedFacePreview.Vertices[_selectedFacePreview.TriangleIndices[i]];
                Vector3 b=_selectedFacePreview.Vertices[_selectedFacePreview.TriangleIndices[i+1]];
                Vector3 c=_selectedFacePreview.Vertices[_selectedFacePreview.TriangleIndices[i+2]];
                Point3D p=new Point3D(a.X*_displaySx,-a.Y*_displaySy,a.Z*_displaySz);
                Point3D q=new Point3D(b.X*_displaySx,-b.Y*_displaySy,b.Z*_displaySz);
                Point3D r=new Point3D(c.X*_displaySx,-c.Y*_displaySy,c.Z*_displaySz);
                Vector3D normal=Vector3D.CrossProduct(q-p,r-p);
                if(normal.LengthSquared<1e-20)continue;
                normal.Normalize();
                Point3D middle=new Point3D((p.X+q.X+r.X)/3,
                    (p.Y+q.Y+r.Y)/3,(p.Z+q.Z+r.Z)/3);
                if(Vector3D.DotProduct(normal,_camera.Position-middle)<0)normal=-normal;
                Vector3D offset=normal*0.001;
                int start=positions.Count;
                positions.Add(p+offset);positions.Add(q+offset);positions.Add(r+offset);
                triangles.Add(start);triangles.Add(start+2);triangles.Add(start+1);
            }
            if(triangles.Count==0){_selectedFaceOverlay.Content=null;return;}
            var geometry=new MeshGeometry3D {Positions=positions,TriangleIndices=triangles};
            var brush=new SolidColorBrush(Color.FromRgb(255,209,56)) {Opacity=0.87};
            var material=new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(brush));
            material.Children.Add(new EmissiveMaterial(brush));
            _selectedFaceOverlay.Content=new GeometryModel3D(geometry,material)
                {BackMaterial=material};
        }

        private GeometryModel3D BuildMeshModel(MPWVolumeDisplayMesh preview,double opacity)
        {
            if(preview==null)throw new ArgumentNullException("preview");
            if(double.IsNaN(opacity)||double.IsInfinity(opacity)||opacity<0||opacity>1)
                throw new ArgumentOutOfRangeException("opacity");
            var positions=new Point3DCollection();
            foreach(Vector3 v in preview.Vertices)
            {
                if(float.IsNaN(v.X)||float.IsNaN(v.Y)||float.IsNaN(v.Z)||
                    float.IsInfinity(v.X)||float.IsInfinity(v.Y)||float.IsInfinity(v.Z)||
                    Math.Abs(v.X)>.50001||Math.Abs(v.Y)>.50001||Math.Abs(v.Z)>.50001)
                    throw new ArgumentOutOfRangeException("preview");
                positions.Add(new Point3D(v.X*_displaySx,-v.Y*_displaySy,v.Z*_displaySz));
            }
            var triangles=new Int32Collection();
            for(int i=0;i<preview.TriangleIndices.Count;i+=3)
            {
                triangles.Add(preview.TriangleIndices[i]);
                triangles.Add(preview.TriangleIndices[i+2]);
                triangles.Add(preview.TriangleIndices[i+1]);
            }
            var geometry=new MeshGeometry3D {Positions=positions,TriangleIndices=triangles};
            var brush=new SolidColorBrush(Color.FromRgb(77,216,241)){Opacity=opacity};
            var material=new DiffuseMaterial(brush);
            return new GeometryModel3D(geometry,material){BackMaterial=material};
        }

        /// <summary>多对象专用：可复用的Volume、Mesh、CAD三角面独立挂载在同一WPF 3D场景。
        /// id由MVI或MEM资源实例提供，渲染层只持有显示几何，不管理专业资源。</summary>
        public void SetAdditionalMesh(Guid id,MPWVolumeDisplayMesh preview,double opacity=1)
        {
            Dispatcher.VerifyAccess();
            if(id==Guid.Empty)throw new ArgumentException("必须提供稳定场景实例Id。","id");
            if(preview==null)throw new ArgumentNullException("preview");
            int old=0;
            _additionalMeshTriangles.TryGetValue(id,out old);
            if(preview.TriangleCount>50000||
                SurfaceMeshTriangleCount+AdditionalMeshTriangleCount-old+preview.TriangleCount>50000)
                throw new ArgumentOutOfRangeException("preview","多对象Mesh总三角面预算超限。");
            GeometryModel3D model=BuildMeshModel(preview,opacity);
            ModelVisual3D item;
            if(!_additionalMeshes.TryGetValue(id,out item))
            {
                item=new ModelVisual3D();
                _additionalMeshes.Add(id,item);
                _viewport.Children.Add(item);
            }
            item.Content=model;
            _additionalMeshTriangles[id]=preview.TriangleCount;
        }
        public void RemoveAdditionalMesh(Guid id)
        {
            Dispatcher.VerifyAccess();
            ModelVisual3D item;
            if(!_additionalMeshes.TryGetValue(id,out item))return;
            _viewport.Children.Remove(item);
            _additionalMeshes.Remove(id);_additionalMeshTriangles.Remove(id);
        }
        public void ClearAdditionalMeshes()
        {
            Dispatcher.VerifyAccess();
            foreach(ModelVisual3D item in _additionalMeshes.Values)
                _viewport.Children.Remove(item);
            _additionalMeshes.Clear();_additionalMeshTriangles.Clear();
        }

        public void ClearSurfaceMesh()
        {
            Dispatcher.VerifyAccess();
            ClearSelectedMeshFaces();
            _meshOverlay.Content=null;
            SurfaceMeshTriangleCount=0;
        }

        /// <summary>专业数值输出转为仅供显示的标准化三维顶点，保留现有三刀和相机。</summary>
        public void SetSurfacePoints(IReadOnlyList<Vector3> normalizedPoints)
        {
            Dispatcher.VerifyAccess();
            if(normalizedPoints==null)throw new ArgumentNullException("normalizedPoints");
            if(normalizedPoints.Count>12000)
                throw new ArgumentOutOfRangeException("normalizedPoints","请先使用MPW显示抽样，不能无限生成WPF Mesh。");
            // 每个点采用小四面体实心标记，使用一个批量Mesh而不是上万个Visual3D实例。
            var positions=new Point3DCollection();
            var triangles=new Int32Collection();
            const double radius=.0024;
            for(int i=0;i<normalizedPoints.Count;i++)
            {
                Vector3 p=normalizedPoints[i];
                if(float.IsNaN(p.X)||float.IsNaN(p.Y)||float.IsNaN(p.Z)||
                   float.IsInfinity(p.X)||float.IsInfinity(p.Y)||float.IsInfinity(p.Z)||
                   Math.Abs(p.X)>.50001||Math.Abs(p.Y)>.50001||Math.Abs(p.Z)>.50001)
                    throw new ArgumentOutOfRangeException("normalizedPoints","显示点超出当前标准化Volume。");
                int start=positions.Count;
                double x=p.X*_displaySx,y=-p.Y*_displaySy,z=p.Z*_displaySz;
                positions.Add(new Point3D(x,y+radius,z));
                positions.Add(new Point3D(x-radius,y-radius,z-radius));
                positions.Add(new Point3D(x+radius,y-radius,z-radius));
                positions.Add(new Point3D(x,y-radius,z+radius));
                int[] faces={0,1,2,0,2,3,0,3,1,1,3,2};
                foreach(int face in faces)triangles.Add(start+face);
            }
            if(positions.Count==0)
            {
                ClearSurfacePoints();
                return;
            }
            var mesh=new MeshGeometry3D{Positions=positions,TriangleIndices=triangles};
            var material=new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255,198,56)));
            _surfaceOverlay.Content=new GeometryModel3D(mesh,material){BackMaterial=material};
            SurfacePreviewPointCount=normalizedPoints.Count;
        }

        /// <summary>点云显示独立实例：原始点由专业Owner持有，WPF只接收显示投影。</summary>
        public void SetAdditionalPointCloud(Guid id,IReadOnlyList<Vector3> points,double opacity=1)
        {
            Dispatcher.VerifyAccess();
            if(id==Guid.Empty)throw new ArgumentException("点云Scene实例Id无效。","id");
            if(points==null)throw new ArgumentNullException("points");
            if(double.IsNaN(opacity)||double.IsInfinity(opacity)||opacity<0||opacity>1)
                throw new ArgumentOutOfRangeException("opacity");
            int previous=0;
            _additionalPointCloudSizes.TryGetValue(id,out previous);
            if(points.Count>12000||AdditionalPointCount-previous+points.Count>12000)
                throw new ArgumentOutOfRangeException("points","WPF三维点云显示超出12000点合计预算。");
            GeometryModel3D model=BuildPointCloudModel(points,opacity);
            ModelVisual3D visual;
            if(!_additionalPointClouds.TryGetValue(id,out visual))
            {
                visual=new ModelVisual3D();
                _additionalPointClouds.Add(id,visual);
                _viewport.Children.Add(visual);
            }
            visual.Content=model;
            _additionalPointCloudSizes[id]=points.Count;
        }
        private GeometryModel3D BuildPointCloudModel(IReadOnlyList<Vector3> points,double opacity)
        {
            var positions=new Point3DCollection();
            var triangles=new Int32Collection();
            const double radius=.0024;
            for(int i=0;i<points.Count;i++)
            {
                Vector3 p=points[i];
                if(float.IsNaN(p.X)||float.IsNaN(p.Y)||float.IsNaN(p.Z)||
                    float.IsInfinity(p.X)||float.IsInfinity(p.Y)||float.IsInfinity(p.Z)||
                    Math.Abs(p.X)>.50001||Math.Abs(p.Y)>.50001||Math.Abs(p.Z)>.50001)
                    throw new ArgumentOutOfRangeException("points","点云显示坐标超出标准化体空间。");
                int start=positions.Count;
                double x=p.X*_displaySx,y=-p.Y*_displaySy,z=p.Z*_displaySz;
                positions.Add(new Point3D(x,y+radius,z));
                positions.Add(new Point3D(x-radius,y-radius,z-radius));
                positions.Add(new Point3D(x+radius,y-radius,z-radius));
                positions.Add(new Point3D(x,y-radius,z+radius));
                int[] faces={0,1,2,0,2,3,0,3,1,1,3,2};
                foreach(int face in faces)triangles.Add(start+face);
            }
            var mesh=new MeshGeometry3D {Positions=positions,TriangleIndices=triangles};
            var brush=new SolidColorBrush(Color.FromRgb(82,214,242)){Opacity=opacity};
            var material=new DiffuseMaterial(brush);
            return new GeometryModel3D(mesh,material){BackMaterial=material};
        }
        public void RemoveAdditionalPointCloud(Guid id)
        {
            Dispatcher.VerifyAccess();
            ModelVisual3D visual;
            if(!_additionalPointClouds.TryGetValue(id,out visual))return;
            _viewport.Children.Remove(visual);
            _additionalPointClouds.Remove(id);
            _additionalPointCloudSizes.Remove(id);
        }
        public void ClearAdditionalPointClouds()
        {
            Dispatcher.VerifyAccess();
            foreach(ModelVisual3D visual in _additionalPointClouds.Values)
                _viewport.Children.Remove(visual);
            _additionalPointClouds.Clear();
            _additionalPointCloudSizes.Clear();
        }

        public void ClearSurfacePoints()
        {
            Dispatcher.VerifyAccess();
            _surfaceOverlay.Content=null;
            SurfacePreviewPointCount=0;
        }

        public void ResetCamera()
        {
            if(_cameraState!=null)
            {
                _cameraState.Reset();
                return;
            }
            _yaw = 37.0; _pitch = 23.0; _distance = 3.5; _cameraTarget=new Vector3D();
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
            _displaySx=sx;_displaySy=sy;_displaySz=sz;
            _rawWidth=width;_rawHeight=height;_rawDepth=depth;
            RefreshSelectedFaceHighlight();
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
            _camera.Position = pos+_cameraTarget;
            _camera.LookDirection = new Vector3D(-pos.X, -pos.Y, -pos.Z);
            _camera.UpDirection = new Vector3D(0, 1, 0);
            _camera.FieldOfView = _cameraState==null ? 42 : 45;
            _camera.NearPlaneDistance = 0.01;
            _camera.FarPlaneDistance = 30;
            RefreshSelectedFaceHighlight();
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
            if((Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))==
                (ModifierKeys.Control|ModifierKeys.Shift))
            {
                Point3D origin,hit;
                if(TryGetSurfaceMeshDisplayRay(e.GetPosition(_viewport),out origin,out hit))
                {
                    var handler=MeshFaceRayRequested;
                    if(handler!=null)handler(this,new MPWVolumeMeshRayEventArgs(origin,hit));
                }
                e.Handled=true;return;
            }
            if((Keyboard.Modifiers&ModifierKeys.Control)!=0&&
                TrySelectSceneAtPoint(e.GetPosition(_viewport)))
            {
                e.Handled=true;
                return;
            }
            _dragOrigin = e.GetPosition(this);
            _panning=(Keyboard.Modifiers & ModifierKeys.Shift)!=0;
            _dragging = true;
            CaptureMouse();
            e.Handled = true;
        }
        private void OnRightDown(object sender,MouseButtonEventArgs e)
        {
            _dragOrigin=e.GetPosition(this);
            _dragging=true;_panning=true;
            CaptureMouse();e.Handled=true;
        }
        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;_panning=false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
        }
        private void OnMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point next = e.GetPosition(this);
            double dx=next.X-_dragOrigin.X,dy=next.Y-_dragOrigin.Y;
            _dragOrigin = next;
            if(_panning)Pan(dx,dy);
            else Orbit(dx,dy);
            e.Handled = true;
        }
        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            Zoom(e.Delta/120.0);
            e.Handled = true;
        }
    }
}
