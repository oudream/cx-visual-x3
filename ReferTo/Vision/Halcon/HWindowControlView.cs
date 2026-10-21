using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using static HalconDriverModule.HalconWindowTools;

namespace HalconDriverModule
{
    public class HWindowControlView
    {
        public delegate void ShowImageInfoDelegate(int x, int y, int grayscaleValue);
        public ShowImageInfoDelegate ShowImageInfo;

        public delegate void ShowFullDelegate();
        public ShowFullDelegate ShowFull;

        // 矩形绘制委托
        public delegate double AddUserRectangleDelegate(Rectangle rectangle);
        public AddUserRectangleDelegate AddUserRectangle;

        // 圆形绘制委托
        public delegate double AddUserCircleDelegate(System.Drawing.Point center, double radius);
        public AddUserCircleDelegate AddUserCircle;

        // 多边形绘制委托
        public delegate double AddUserPolygonDelegate(System.Drawing.Point[] points);
        public AddUserPolygonDelegate AddUserPolygon;

        
        private HWindowControl _hWindowControl;
        private CheckBox _wlwwCheckBox = null;
        private TrackBar _wlTrackBar = null;
        private Label _wlLabel = null;
        private TrackBar _wwTrackBar = null;
        private Label _wwLabel = null;
        private RadioButton _noneRadioButton = null;
        private RadioButton _autoWLWWRadioButton = null;

        public int MaxRectWdith { get; set; } = 0;
        public int MaxRectHeight { get; set; } = 0;

        private HalconWindowTools _hWindowTools;

        // 鼠标左键按下，拖动图像
        private bool _mouseLeftButDown;
        private int _mouseLeftDownRow;
        private int _mouseLeftDownCol;

        // 按Shift键时绘画矩形框
        private Point _boxAutoWLWWStartPoint;
        private Rectangle _boxAutoWLWWRectangle;
        private bool _isBoxAutoWLWW;

        // 按Ctrl键时绘画线
        private bool _isMeasuring;
        private Point _measureStartPoint;
        private Point _measureEndPoint;
        private double _measureLength;

        private string _currentImageFilePath;
        private string _LabelText;

        private bool _isFirstDown = true;
        public event Action<int, int> WWWLChanged;

        // 為閉合多邊形定義一個像素半徑閾值
        private const int CLOSE_POLYGON_PIXEL_RADIUS = 10;

        // 新增绘图模式枚举
        public enum UserDrawMode
        {
            None,
            Point,
            Rectangle,
            Circle,
            Polygon,
            Ls2Ls,
            Arrow,
            Text,
            ThresholdSeg,
            RegionGrowing
        }

        private UserDrawMode _currentUserDrawMode = UserDrawMode.None;
        private bool _enableLeftButtonImagePan = true;

        /// <summary>
        /// 当前工具绘制过程的颜色。由宿主按 ROI、测量或标注图层设置；过程线均使用虚线。
        /// </summary>
        public string InteractionColor { get; set; } = "cyan";

        /// <summary>
        /// 是否允许普通左键拖动图像。默认保持历史行为；使用互斥工具模式的宿主可在绘制、
        /// 测量或标注期间关闭该开关，避免同一次鼠标移动同时触发绘制与平移。
        /// </summary>
        public bool EnableLeftButtonImagePan
        {
            get { return _enableLeftButtonImagePan; }
            set
            {
                _enableLeftButtonImagePan = value;
                if (!value)
                {
                    // 切换工具时立即丢弃上一个左键拖图状态，防止切回导航后发生“幽灵平移”。
                    _mouseLeftButDown = false;
                }
            }
        }

        public void SetCurrentUserDrawMode(UserDrawMode mode)
        {
            _currentUserDrawMode = mode;
        }

        private bool _isDrawingBox;
        private bool _isDrawingCircle;
        private Point _drawBoxStartPoint;
        private Rectangle _drawBoxRectangle;
        private Point _drawCircleStartPoint;
        private double _drawCircleRadius;

        // 存储多边形绘制状态
        private bool _isDrawingPolygon = false;
        private readonly List<System.Drawing.Point> _polygonPoints = new List<System.Drawing.Point>();

        // 两条线段距离绘制状态
        private bool _isDrawingLs2Ls = false;
        private int _ls2lsStage = 0;
        // 依次记录 L1Start、L1End、L2Start、L2End
        private Point _ls2lsL1Start, _ls2lsL1End, _ls2lsL2Start, _ls2lsL2End;

        // 箭头绘制
        private bool _isDrawingArrow = false;
        private System.Drawing.Point _arrowHead; // 第一下：箭头尖
        private System.Drawing.Point _arrowTail; // 第二下：箭尾

        // 多边形编辑相关字段
        private DrawPolygonInfo _selectedPolygon = null;  // 当前选中的多边形
        private int _draggingPointIndex = -1;              // 正在拖动的点索引
        private bool _isDraggingPoint = false;             // 是否正在拖动点

        // 自动区域面积使用
        public HalconWindowTools HWindowTools => _hWindowTools;

        // 定义事件参数类
        public class AutoAreaRectDrawnEventArgs : EventArgs
        {
            public HObject OriginalImage { get; set; }   // 原始图像（调用方负责释放）
            public Rectangle ROI { get; set; }
            public uint LineNumber { get; set; }
            public string UserLabel { get; set; }
            public HWindowControlView View { get; set; } // 传递自身引用
            public int Mode { get; set; }                 // 0:阈值模式, 1:区域生长模式
        }

        // 声明事件
        public event EventHandler<AutoAreaRectDrawnEventArgs> AutoAreaRectDrawn;

        public event Action<Rectangle> RectangleDrawn;
        public event Action<System.Drawing.Point, double> CircleDrawn;
        public event Action<System.Drawing.Point[]> PolygonDrawn;
        public event Action<System.Drawing.Point, System.Drawing.Point> ArrowDrawn;
        public event Action<System.Drawing.Point, string> TextDrawn;
        public bool IsRectangleDrawingHandledExternally { get; set; } = false;

        public HWindowControlView(HalconWindowTools halconWindowTools)
        {
            _hWindowTools = halconWindowTools;
            _hWindowControl = _hWindowTools.GetHWindowControl();
            _hWindowControl.HMouseDown += HWindowControl_HMouseDown;
            _hWindowControl.HMouseMove += HWindowControl_HMouseMove;
            _hWindowControl.HMouseUp += HWindowControl_HMouseUp;
            _hWindowControl.HMouseWheel += HWindowControl_HMouseWheel;

            _hWindowControl.KeyDown += HWindowControl_KeyDown;
        }

        public void SetHMouseDownRelease(bool swicth)
        {
            if (!swicth)
            {
                _hWindowControl.HMouseDown -= HWindowControl_HMouseDown;
            }
        }
        public void SetHMouseMoveRelease(bool swicth)
        {
            if (!swicth)
            {
                _hWindowControl.HMouseMove -= HWindowControl_HMouseMove;
            }
        }
        public void SetHMouseUpRelease(bool swicth)
        {
            _hWindowControl.HMouseUp -= HWindowControl_HMouseUp;
        }
        public void SetHMouseWheelRelease(bool swicth)
        {
            if (!swicth)
            {
                _hWindowControl.HMouseWheel -= HWindowControl_HMouseWheel;
            }
        }

        public void SetKeyDownRelease(bool swicth)
        {
            if (!swicth)
            {
                _hWindowControl.KeyDown -= HWindowControl_KeyDown;
            }
        }


        private void HWindowControl_KeyDown(object sender, KeyEventArgs e)
        {
            //Ctrl+Z
            if (e.Control && e.KeyCode == Keys.Z)
            {
                UndoLastAction();
            }
            //Ctrl + "+"
            if (e.Control && e.KeyCode == Keys.Oemplus)
            {
                _hWindowTools.DisplayOriginalSizeImage();
            }
            //Ctrl + "-"
            if (e.Control && e.KeyCode == Keys.OemMinus)
            {
                _hWindowTools.DisplayAdaptiveSizeImage();
            }
        }

        public HWindowControlView(HalconWindowTools halconWindowTools, CheckBox wlwwCheckBox, TrackBar wlBar, Label wlLabel, TrackBar wwBar, Label wwLabel, RadioButton noneRadioButton, RadioButton autoWLWWRadioButton)
        {
            _hWindowTools = halconWindowTools;

            _wlwwCheckBox = wlwwCheckBox;
            _wlTrackBar = wlBar;
            _wlLabel = wlLabel;
            _wwTrackBar = wwBar;
            _wwLabel = wwLabel;
            _noneRadioButton = noneRadioButton;
            _autoWLWWRadioButton = autoWLWWRadioButton;

            _hWindowControl = _hWindowTools.GetHWindowControl();
            _hWindowControl.HMouseDown += HWindowControl_HMouseDown;
            _hWindowControl.HMouseMove += HWindowControl_HMouseMove;
            _hWindowControl.HMouseUp += HWindowControl_HMouseUp;
            _hWindowControl.HMouseWheel += HWindowControl_HMouseWheel;
            _hWindowControl.KeyDown += HWindowControl_KeyDown;

            if (_wlTrackBar != null)
                _wlTrackBar.Scroll += wlTrackBarScroll;
            if (_wwTrackBar != null)
                _wwTrackBar.Scroll += wwTrackBarScroll;
            if (_wlwwCheckBox != null)
                _wlwwCheckBox.CheckedChanged += WLWWCheckBox_CheckedChanged;

            //LoadRectanglesFromConfig("0,1024,3071,1024");
        }

        public void UndoLastAction()
        {
            if (_isMeasuring || _isDrawingBox || _isDrawingCircle || _isDrawingPolygon || _isDrawingLs2Ls || _isDrawingArrow)
            {
                // 创建一个模拟鼠标按下的事件参数
                _drawBoxStartPoint = new Point(0, 0);
                _drawBoxRectangle = new Rectangle(0, 0, 0, 0);
                _drawCircleStartPoint = new Point(0, 0);
                _isFirstDown = true;
                _isMeasuring = false;
                _isDrawingBox = false;
                _isDrawingCircle = false;
                _polygonPoints.Clear();
                _isDrawingPolygon = false;
                _isDrawingLs2Ls = false;
                _isDrawingArrow = false;

                _hWindowTools.ReShowDrawALL();
            }
            else
            {
                _hWindowTools.UndoLastAction();
            }
        }
        // 窗宽窗位显示
        private void WLWWCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_wlwwCheckBox.Checked && _hWindowTools.IsNullImage())
            {
                _wlwwCheckBox.Checked = false;
                return;
            }

            EnableWLWW(_wlwwCheckBox.Checked);
        }

        public void EnableWLWW(bool wlwwEnabled)
        {
            // _wlTrackBar 为 null 时返回，是因为在初始化时 _wlTrackBar 为 null，那么 _wlwwCheckBox、_autoWLWWRadioButton等 也为 null
            if (_wlTrackBar == null) return;
            _wlTrackBar.Visible = wlwwEnabled;
            _wwTrackBar.Visible = wlwwEnabled;
            if (wlwwEnabled)
            {
                //AdjustControlsInPanel(wwTrackBar, wlTrackBar, imagePanel);
                _hWindowTools.UpdataImageByWLWW((int)_wlTrackBar.Value, (int)_wwTrackBar.Value);
                _wlLabel.Visible = true;
                _wwLabel.Visible = true;
                _autoWLWWRadioButton.Enabled = true;
                _autoWLWWRadioButton.Checked = true;
            }
            else
            {
                // 重置窗宽窗
                _hWindowTools.UpdataImageByWLWW(0, 0);
                _wlLabel.Visible = false;
                _wwLabel.Visible = false;
                _noneRadioButton.Checked = true;
                _autoWLWWRadioButton.Enabled = false;
            }
        }

        // 打开图像
        public void OpenImage()
        {
            string filename = "";
            OpenFileDialog dlg = new OpenFileDialog();
            //dlg.Filter = "Tiff文件|*.tif|Bmp文件|*.bmp|Erdas img文件|*.img|EVNI文件|*.hdr|jpeg文件|*.jpg|raw文件|*.raw|vrt文件|*.vrt|所有文件|*.*";
            //dlg.Filter = "Tiff文件|*.tif|Png文件|*.png|Bmp文件|*.bmp|jpeg文件|*.jpg";
            //dlg.Filter = "图像文件(*.tif;*.png;*.bmp;*.jpg)|*.tif;*.png;*.bmp;*.jpg|所有文件(*.*)|*.*";
            dlg.Filter = "图像文件(*.tif;*.png;*.bmp;*.jpg)|*.tif;*.png;*.bmp;*.jpg";
            dlg.FilterIndex = 0;
            dlg.Title = "打开图像";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                filename = dlg.FileName;
            }
            if (filename == "")
            {
                return;
            }

            OpenImage(filename);
        }

        public void OpenImage(string fileName)
        {
            try
            {
                // 加载图像
                _hWindowTools.OpenImage(fileName);
                // 更新当前图像路径
                _currentImageFilePath = fileName;

                // 如果是16位图，启用窗宽窗位，否则禁用
                if (_hWindowTools.GetImageInfo(out int imgWidth, out int imgHeight, out int imgChannels, out int imgBigDepth))
                {
                    // _wlTrackBar 为 null 时返回，是因为在初始化时 _wlTrackBar 为 null，那么 _wlwwCheckBox、_autoWLWWRadioButton等 也为 null
                    if (_wlTrackBar == null) return;

                    if (imgChannels == 1 && imgBigDepth == 16)
                    {
                        _wlwwCheckBox.Enabled = true;
                        _wlwwCheckBox.Visible = true;
                        _wlwwCheckBox.Checked = true;

                        //var (minGray, maxGray) = _hWindowTools.GetMinMaxGray();
                        //_wlTrackBar.Maximum = (int)maxGray;
                        //_wlTrackBar.Minimum = (int)minGray;
                        //_wwTrackBar.Maximum = (int)maxGray;
                        //_wwTrackBar.Minimum = (int)minGray;
                    }
                    else
                    {
                        _wlwwCheckBox.Enabled = false;
                        _wlwwCheckBox.Visible = false;
                        _wlwwCheckBox.Checked = false;
                    }
                    EnableWLWW(_wlwwCheckBox.Checked);
                }
            }
            catch (HalconException ex)
            {
                MessageBox.Show("Error loading image: " + ex.Message);
            }
        }

        // 生命周期交给 HWindowTools
        public void OpenImage(HObject image, int wwwlMin, int wwwlMax)
        {
            _hWindowTools.OpenImage(image, wwwlMin, wwwlMax);
            // 更新当前图像路径
            _currentImageFilePath = "";
        }

        public void SetLineNumber(uint lineNumber)
        {
            _hWindowTools.SetLineNumber(lineNumber);
        }

        // 生命周期交给 HWindowTools
        public void OpenImage(HObject image, uint lineNumber)
        {
            if (HalconHelper.GetImageInfo(image, out int imgWidth, out int imgHeight, out int imgChannels, out int imgBigDepth, out _))
            {
                int wwwlMin = 0, wwwlMax = 0;
                // _wlTrackBar 为 null 时，那么 _wlwwCheckBox、_autoWLWWRadioButton等 也为 null
                if (_wlTrackBar != null)
                {
                    if (imgChannels == 1 && imgBigDepth == 16)
                    {
                        _wlwwCheckBox.Enabled = true;
                        _wlwwCheckBox.Visible = true;
                        _wlwwCheckBox.Checked = true;
                    }
                    else
                    {
                        _wlwwCheckBox.Enabled = false;
                        _wlwwCheckBox.Visible = false;
                        _wlwwCheckBox.Checked = false;
                    }
                    if (_wlwwCheckBox.Checked)
                    {
                        wwwlMin = (int)_wlTrackBar.Value;
                        wwwlMax = (int)_wwTrackBar.Value;
                    }
                }
                SetLineNumber(lineNumber);
                _hWindowTools.OpenImage(image, wwwlMin, wwwlMax);
                // 更新当前图像路径
                _currentImageFilePath = "";
                if (_wlwwCheckBox != null)
                {
                    EnableWLWW(_wlwwCheckBox.Checked);
                }
            }
        }

        public string GetCurrentImageFilePath() => _currentImageFilePath;
        // 鼠标 左键按下
        private void HWindowControl_HMouseDown(object sender, HMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (Control.ModifierKeys == Keys.Shift) // 按住 Shift 键时开始绘制矩形
                {
                    if (_noneRadioButton == null) return;
                    if (_noneRadioButton.Checked) return;
                    _isBoxAutoWLWW = true;
                    _boxAutoWLWWStartPoint = new Point((int)e.X, (int)e.Y);
                }
                else if (Control.ModifierKeys == Keys.Control) // 按住 Ctrl 键时开始测量
                {
                    if (_isFirstDown == true)
                    {
                        _isMeasuring = true;
                        _measureStartPoint = new Point((int)e.X, (int)e.Y);
                    }
                }
                else if (_isFirstDown == false)
                {
                    //进入此函数的条件为 测量线的第二次鼠标左键按下
                    //不做任何处理,只是为了不触发LeftButtonDown标志...
                }
                else if (_currentUserDrawMode == UserDrawMode.Rectangle)
                {
                    _isDrawingBox = true;
                    _drawBoxStartPoint = new Point((int)e.X, (int)e.Y);
                }
                else if (_currentUserDrawMode == UserDrawMode.Point)
                {
                    var point = new Point((int)e.X, (int)e.Y);
                    var gray = _hWindowTools.GetGrayForCircle(point, 3);
                    _hWindowTools.AddUserCircle(new HalconWindowTools.DrawCircleInfo
                    {
                        Center = point,
                        Radius = 3,
                        GrayValue = gray,
                        Label = _LabelText
                    });
                    CircleDrawn?.Invoke(point, 3);
                    return;
                }
                else if (_currentUserDrawMode == UserDrawMode.Circle)
                {
                    _isDrawingCircle = true;
                    _drawCircleStartPoint = new Point((int)e.X, (int)e.Y);
                }
                else if (_currentUserDrawMode == UserDrawMode.Polygon)
                {
                    var mousePos = new Point((int)e.X, (int)e.Y);

                    // 编辑模式：优先处理已存在多边形的选中和拖动
                    bool handled = false;

                    // 1. 先检查是否点击到已存在多边形的顶点（放大命中半径到15像素）
                    foreach (var polygon in _hWindowTools._drawUserPolygons)
                    {
                        // 如果多边形已被选中，检查它的顶点
                        if (polygon.IsSelected)
                        {
                            for (int i = 0; i < polygon.Points.Count; i++)
                            {
                                double distance = Math.Sqrt(Math.Pow(polygon.Points[i].X - mousePos.X, 2) +
                                                            Math.Pow(polygon.Points[i].Y - mousePos.Y, 2));
                                if (distance <= 15) // 放大命中半径，更容易点到
                                {
                                    // 命中顶点，开始拖动
                                    _selectedPolygon = polygon;
                                    _draggingPointIndex = i;
                                    _isDraggingPoint = true;

                                    // 设置多边形的编辑状态
                                    polygon.EditingPointIndex = i;
                                    polygon.IsDragging = true;

                                    _hWindowTools.ReShowDrawALL(); // 立即重绘，显示拖动状态
                                    handled = true;
                                    break;
                                }
                            }
                            if (handled) break;
                        }
                    }

                    if (handled) return;

                    // 2. 如果没有命中顶点，检查是否点击到多边形内部（选中/取消选中）
                    foreach (var polygon in _hWindowTools._drawUserPolygons)
                    {
                        if (IsPointInPolygon(mousePos, polygon.Points))
                        {
                            // 取消之前选中的多边形
                            foreach (var p in _hWindowTools._drawUserPolygons)
                            {
                                p.IsSelected = false;
                                p.EditingPointIndex = -1;
                                p.IsDragging = false;
                            }

                            // 选中新的多边形
                            polygon.IsSelected = true;
                            _selectedPolygon = polygon;
                            _hWindowTools.ReShowDrawALL();
                            handled = true;
                            break;
                        }
                    }

                    if (handled) return;

                    // 3. 如果点击空白区域，取消所有选中
                    if (_hWindowTools._drawUserPolygons.Count > 0 && !_isDrawingPolygon)
                    {
                        foreach (var polygon in _hWindowTools._drawUserPolygons)
                        {
                            polygon.IsSelected = false;
                            polygon.EditingPointIndex = -1;
                            polygon.IsDragging = false;
                        }
                        _selectedPolygon = null;
                        _hWindowTools.ReShowDrawALL();
                        // 注意：这里不设置handled为true，因为点击空白区域后应该可以开始新绘制
                    }

                    // 4. 新绘制逻辑：只有在没有选中任何多边形或者根本没有多边形时，才允许新绘制
                    // 检查是否所有多边形都没有被选中
                    bool anyPolygonSelected = false;
                    foreach (var p in _hWindowTools._drawUserPolygons)
                    {
                        if (p.IsSelected)
                        {
                            anyPolygonSelected = true;
                            break;
                        }
                    }

                    // 如果没有多边形被选中，才允许新绘制
                    if (!anyPolygonSelected)
                    {
                        if (e.Clicks > 1) // 双击完成绘制
                        {
                            if (_isDrawingPolygon && _polygonPoints.Count >= 3)
                            {
                                // 闭合多边形：自动在末尾加回起点
                                _polygonPoints.Add(_polygonPoints[0]);
                                var ptsArray = _polygonPoints.ToArray();
                                double gray = AddUserPolygon == null ? _hWindowTools.GetGrayForPolygon(ptsArray) : AddUserPolygon(ptsArray);
                                _hWindowTools.AddUserPolygon(new HalconWindowTools.DrawPolygonInfo
                                {
                                    Points = ptsArray.ToList(),
                                    GrayValue = gray,
                                    Label = _LabelText
                                });
                                PolygonDrawn?.Invoke(ptsArray);

                                // 重置绘制状态
                                _isDrawingPolygon = false;
                                _polygonPoints.Clear();
                            }
                        }
                        else
                        {
                            var pt = new System.Drawing.Point((int)e.X, (int)e.Y);
                            if (!_isDrawingPolygon)
                            {
                                // 开始新的多边形绘制
                                _isDrawingPolygon = true;
                                _polygonPoints.Clear();
                                _polygonPoints.Add(pt);
                            }
                            else if (_isDrawingPolygon)
                            {
                                // 继续当前绘制，添加新点
                                _polygonPoints.Add(pt);
                            }
                        }
                    }
                }
                else if (_currentUserDrawMode == UserDrawMode.Ls2Ls)
                {
                    if (!_isDrawingLs2Ls)
                    {
                        _isDrawingLs2Ls = true;
                        _ls2lsStage = 0;
                    }

                    if (_ls2lsStage == 0)
                    {
                        _ls2lsL1Start = new Point((int)e.X, (int)e.Y);
                        _ls2lsStage = 1;
                    }
                    else if (_ls2lsStage == 1)
                    {
                        _ls2lsL1End = new Point((int)e.X, (int)e.Y);
                        _ls2lsStage = 2;
                    }
                    else if (_ls2lsStage == 2)
                    {
                        _ls2lsL2Start = new Point((int)e.X, (int)e.Y);
                        _ls2lsStage = 3;
                    }
                    else if (_ls2lsStage == 3)
                    {
                        // 完成
                        _ls2lsL2End = new Point((int)e.X, (int)e.Y);

                        // 计算最近点与距离、角度
                        Point pA, pB;
                        double dist = HalconHelper.ClosestPointsOnSegments(_ls2lsL1Start, _ls2lsL1End, _ls2lsL2Start, _ls2lsL2End, out pA, out pB);
                        double angle = HalconHelper.AngleBetweenSegmentsDeg(_ls2lsL1Start, _ls2lsL1End, _ls2lsL2Start, _ls2lsL2End);

                        _hWindowTools.AddUserLs2Ls(new HalconWindowTools.DrawLs2LsInfo
                        {
                            L1Start = _ls2lsL1Start,
                            L1End = _ls2lsL1End,
                            L2Start = _ls2lsL2Start,
                            L2End = _ls2lsL2End,
                            ClosestOnL1 = pA,
                            ClosestOnL2 = pB,
                            DistancePixel = dist,
                            AngleDeg = angle,
                            Label = _LabelText,
                        });

                        // 重置状态
                        _isDrawingLs2Ls = false;
                        _ls2lsStage = 0;
                    }

                    return; // 已处理，不走后面的拖拽/缩放
                }
                else if (_currentUserDrawMode == UserDrawMode.Arrow)
                {
                    if (!_isDrawingArrow)
                    {
                        _isDrawingArrow = true;
                        _arrowHead = new Point((int)e.X, (int)e.Y); // 第一次：确定箭头尖
                    }
                    else
                    {
                        _arrowTail = new Point((int)e.X, (int)e.Y); // 第二次：确定箭尾
                        _hWindowTools.AddUserArrow(_arrowHead, _arrowTail);
                        ArrowDrawn?.Invoke(_arrowHead, _arrowTail);
                        _isDrawingArrow = false;
                    }
                    return;
                }
                else if (_currentUserDrawMode == UserDrawMode.Text)
                {
                    var point = new Point((int)e.X, (int)e.Y);
                    var text = string.IsNullOrWhiteSpace(_LabelText) ? "标注" : _LabelText;
                    _hWindowTools.AddNewDrawFont(text, "red", point.Y, point.X);
                    _hWindowTools.ReShowDrawALL();
                    TextDrawn?.Invoke(point, text);
                    return;
                }
                else if (_currentUserDrawMode == UserDrawMode.ThresholdSeg)
                {
                    _isDrawingBox = true;          // 复用矩形绘制状态
                    _drawBoxStartPoint = new Point((int)e.X, (int)e.Y);
                }
                else if (_currentUserDrawMode == UserDrawMode.RegionGrowing)
                {
                    _isDrawingBox = true;          // 复用矩形绘制状态
                    _drawBoxStartPoint = new Point((int)e.X, (int)e.Y);
                }
                else if (EnableLeftButtonImagePan)
                {
                    _mouseLeftButDown = true;
                    try
                    {
                        _hWindowControl.HalconWindow.GetMposition(out _mouseLeftDownRow, out _mouseLeftDownCol, out _);
                    }
                    catch (Exception)
                    {
                    }
                    if (e.Clicks == 2) _hWindowTools.DispImageFit();
                }
            }
            if (e.Button == MouseButtons.Right)
            {
                _hWindowControl.HalconWindow.GetMposition(out var mouseRow, out var mouseCol, out _);

                if (e.Clicks == 2 ) //&& _hWindowTools.UseNaviCamPixel2Mm)
                {
                    if (Control.ModifierKeys == Keys.Alt) // 按住 Alt 键时，右键双击导航图，移动定位相机
                    {
                        _hWindowTools.CamMove?.Invoke(3, mouseRow, mouseCol);
                    }
                    if (Control.ModifierKeys == Keys.Control) // 按住 Ctrl 键时，右键双击导航图，移动X光相机
                    {
                        _hWindowTools.CamMove?.Invoke(1, mouseRow, mouseCol);
                    }
                    return;
                }

                ShowFull?.Invoke();
            }
        }

        // 鼠标 移动
        private void HWindowControl_HMouseMove(object sender, HMouseEventArgs e)
        {
            // 鼠标按下时进行移动
            if (_isBoxAutoWLWW)
            {
                _boxAutoWLWWRectangle = new Rectangle(
                    (int)Math.Min(_boxAutoWLWWStartPoint.X, e.X),
                    (int)Math.Min(_boxAutoWLWWStartPoint.Y, e.Y),
                    (int)Math.Abs(_boxAutoWLWWStartPoint.X - e.X),
                    (int)Math.Abs(_boxAutoWLWWStartPoint.Y - e.Y)
                );
                _hWindowTools.ReShowDrawALL();
                _hWindowTools.DrawRectangle(_boxAutoWLWWRectangle);
            }
            else if (EnableLeftButtonImagePan && _mouseLeftButDown)
            {
                _hWindowTools.DispImageMove(_mouseLeftDownRow, _mouseLeftDownCol);
            }
            else if (_isMeasuring)
            {
                _hWindowTools.ReShowDrawALL();
                _measureEndPoint = new Point((int)e.X, (int)e.Y);
                _hWindowTools.DrawLine(_measureStartPoint, _measureEndPoint);
            }
            else if (_isDrawingBox)
            {
                _hWindowTools.ReShowDrawALL();
                _drawBoxRectangle = new Rectangle(
                    (int)Math.Min(_drawBoxStartPoint.X, e.X),
                    (int)Math.Min(_drawBoxStartPoint.Y, e.Y),
                    (int)Math.Abs(_drawBoxStartPoint.X - e.X),
                    (int)Math.Abs(_drawBoxStartPoint.Y - e.Y)
                );
                _hWindowTools.DrawRectangle(_drawBoxRectangle, InteractionColor, true);
            }
            else if (_isDrawingCircle)
            {
                _drawCircleRadius = Math.Sqrt(Math.Pow(e.X - _drawCircleStartPoint.X, 2) + Math.Pow(e.Y - _drawCircleStartPoint.Y, 2));
                _hWindowTools.ReShowDrawALL();
                _hWindowTools.DrawCircle(_drawCircleStartPoint, (int)_drawCircleRadius, InteractionColor, true);
            }
            else if (_isDrawingPolygon && _polygonPoints.Count > 0)
            {
                // 先重绘底图和已画对象
                _hWindowTools.ReShowDrawALL();
                try
                {
                    // 当前鼠标位置
                    _hWindowControl.HalconWindow.GetMpositionSubPix(out double row, out double col, out _);
                    HOperatorSet.SetColor(_hWindowControl.HalconWindow, InteractionColor);
                    HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple(8, 6));
                    // 画已记录的线段
                    for (int i = 0; i < _polygonPoints.Count - 1; i++)
                    {
                        var p1 = _polygonPoints[i];
                        var p2 = _polygonPoints[i + 1];
                        HOperatorSet.DispLine(_hWindowControl.HalconWindow, p1.Y, p1.X, p2.Y, p2.X);
                    }
                    // 画最后一点到当前鼠标的虚线
                    var last = _polygonPoints.Last();
                    _hWindowTools.DrawLine(last, new Point((int)col, (int)row), InteractionColor, true);
                    HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple());
                }
                catch { }
            }
            else if (_currentUserDrawMode == UserDrawMode.Ls2Ls && _isDrawingLs2Ls)
            {
                _hWindowTools.ReShowDrawALL();

                if (_ls2lsStage >= 1)
                {
                    // 预览第一条线段
                    var end = (_ls2lsStage == 1) ? new Point((int)e.X, (int)e.Y) : _ls2lsL1End;
                    _hWindowTools.DrawLine(_ls2lsL1Start, end);
                }
                if (_ls2lsStage >= 3)
                {
                    // 预览第二条线段
                    var end2 = (_ls2lsStage == 3) ? new Point((int)e.X, (int)e.Y) : _ls2lsL2End;
                    _hWindowTools.DrawLine(_ls2lsL2Start, end2);

                    // 如果两条线段都确定，则预览最短连线（虚线）
                    var a0 = _ls2lsL1Start; var a1 = (_ls2lsStage == 3) ? _ls2lsL2Start : _ls2lsL1End; // 忽略
                    Point pA, pB;
                    var tempL2End = (_ls2lsStage == 3) ? new Point((int)e.X, (int)e.Y) : _ls2lsL2End;
                    HalconHelper.ClosestPointsOnSegments(_ls2lsL1Start, _ls2lsL1End, _ls2lsL2Start, tempL2End, out pA, out pB);

                    // 画预览虚线
                    var win = _hWindowTools.GetHWindow();
                    HOperatorSet.SetLineStyle(win, new HTuple(6, 4));
                    HOperatorSet.DispLine(win, pA.Y, pA.X, pB.Y, pB.X);
                    HOperatorSet.SetLineStyle(win, new HTuple());
                }
            }
            else if (_currentUserDrawMode == UserDrawMode.Arrow && _isDrawingArrow)
            {
                _hWindowTools.ReShowDrawALL();

                var cur = new Point((int)e.X, (int)e.Y);
                // 画主干
                _hWindowTools.DrawLine(_arrowHead, cur, InteractionColor, true);

                // 画临时箭翼（与上面 ReShowDrawALL 中算法一致）
                var win = _hWindowTools.GetHWindow();
                double dx = _arrowHead.X - cur.X, dy = _arrowHead.Y - cur.Y;
                double L = Math.Sqrt(dx * dx + dy * dy); if (L < 1e-6) return;
                double scale = _hWindowTools.GetScaleFactor();
                double headLen = Math.Max(12, 22 * scale);
                double headAng = 25.0 * Math.PI / 180.0;
                double ux = (cur.X - _arrowHead.X) / -L; // 从头指向尾的反向单位向量
                double uy = (cur.Y - _arrowHead.Y) / -L;

                double cosA = Math.Cos(headAng), sinA = Math.Sin(headAng);
                double vx1 = ux * cosA - uy * sinA;
                double vy1 = ux * sinA + uy * cosA;
                double vx2 = ux * cosA + uy * sinA;
                double vy2 = -ux * sinA + uy * cosA;

                var w1 = new Point((int)Math.Round(_arrowHead.X + vx1 * headLen), (int)Math.Round(_arrowHead.Y + vy1 * headLen));
                var w2 = new Point((int)Math.Round(_arrowHead.X + vx2 * headLen), (int)Math.Round(_arrowHead.Y + vy2 * headLen));

                HOperatorSet.SetColor(win, InteractionColor);
                HOperatorSet.SetLineStyle(win, new HTuple(8, 6));
                HOperatorSet.DispLine(win, _arrowHead.Y, _arrowHead.X, w1.Y, w1.X);
                HOperatorSet.DispLine(win, _arrowHead.Y, _arrowHead.X, w2.Y, w2.X);
                HOperatorSet.SetLineStyle(win, new HTuple());
            }
            else if (_fitRectangleMaxX != 0 && _fitRectangleMaxY != 0 && _fitRectangleMaxArea != 0 && !string.IsNullOrEmpty(_fitRectangleTip))
            {
                var rec = new Rectangle()
                {
                    X = (int)e.X - _fitRectangleMaxX,
                    Y = (int)e.Y - _fitRectangleMaxY,
                    Width = _fitRectangleMaxX,
                    Height = _fitRectangleMaxY
                };
                _hWindowTools.ReShowDrawALL();
                _hWindowTools.CreateAreaFitRectangle(rec, _fitRectangleTip);
            }
            // 拖动多边形顶点的处理
            else if (_isDraggingPoint && _selectedPolygon != null)
            {
                var currentPos = new Point((int)e.X, (int)e.Y);

                // 更新点位置
                _selectedPolygon.UpdatePoint(_draggingPointIndex, currentPos);

                // 重新计算面积
                _selectedPolygon.UpdateWHA(_hWindowTools.CalcActualSizeMM(1, _hWindowTools._lineNumber));

                _hWindowTools.ReShowDrawALL();
            }
            else
            {
                var (r, mousePostRow, mousePostCol, grayValue) = _hWindowTools.GetCurrentMousePosAndGrayval();
                if (r)
                {
                    //ShowImageInfo?.Invoke(mousePostCol.ToString(CultureInfo.InvariantCulture), mousePostRow.ToString(CultureInfo.InvariantCulture), grayValue.ToString());
                    ShowImageInfo?.Invoke(mousePostCol, mousePostRow, grayValue);
                }
            }
        }

        public void SetUserLabelText(string Label)
        {
            if (Label != "")
            {
                Label += ": ";
            }
            _LabelText = Label;

        }

        /// <summary>
        /// 设置原样显示的文字内容。与 SetUserLabelText 不同，此入口不会追加“: ”，
        /// 用于图像文字标注而非测量对象标签。
        /// </summary>
        public void SetUserText(string text)
        {
            _LabelText = text ?? string.Empty;
        }

        // 鼠标 左键松开
        private void HWindowControl_HMouseUp(object sender, HMouseEventArgs e)
        {
            //鼠标左键松开
            if (e.Button == MouseButtons.Left)
            {
                if (_isBoxAutoWLWW)
                {
                    _isBoxAutoWLWW = false;
                    if (_autoWLWWRadioButton.Checked)
                    {
                        bool ret = _hWindowTools.CalcAutoWLWW(_boxAutoWLWWRectangle, out int wl, out int ww);
                        if (ret)
                        {
                            var wwwlMin = (int)(wl - ww / 2.0);
                            var wwwlMax = (int)(wl + ww / 2.0);
                            UpdataWLWW(wwwlMin, wwwlMax);
                            _hWindowTools.UpdataImageByWLWW(wwwlMin, wwwlMax);
                        }
                    }
                    else
                    {
                        //hWindowTool.AddUserRectangle(currentRectangle);
                    }
                }
                else if (_isMeasuring)
                {
                    if (!_isFirstDown)
                    {
                        _isMeasuring = false;
                        _measureEndPoint = new Point((int)e.X, (int)e.Y);
                        _isFirstDown = true;
                        _measureLength = CalculateDistance(_measureStartPoint, _measureEndPoint);
                        _hWindowTools.AddUserMeasureLine(_measureStartPoint, _measureEndPoint, _measureLength, _LabelText);
                    }
                    else
                    {
                        _isFirstDown = false;
                    }
                }
                else if (_isDrawingBox && _currentUserDrawMode == UserDrawMode.Rectangle)
                {
                    _isDrawingBox = false;

                    if (MaxRectWdith > 0 || MaxRectHeight > 0)
                    {
                        _drawBoxRectangle.Width = MaxRectWdith;
                        _drawBoxRectangle.Height = MaxRectHeight;
                    }
                    if (_drawBoxRectangle.Width > 1 && _drawBoxRectangle.Height > 1)
                    {

                        //if (MaxRectWdith <= 0 && MaxRectHeight <= 0 || (MaxRectWdith > 0 && MaxRectHeight > 0 && _drawBoxRectangle.Width < MaxRectWdith && _drawBoxRectangle.Height < MaxRectWdith))
                        {
                            // 触发外部事件
                            RectangleDrawn?.Invoke(_drawBoxRectangle);

                            if (!IsRectangleDrawingHandledExternally)
                            {
                                double gray = AddUserRectangle == null ? _hWindowTools.GetGrayForRectangle(_drawBoxRectangle)
                                    : AddUserRectangle(_drawBoxRectangle);
                                _hWindowTools.AddUserRectangle(_drawBoxRectangle, gray, _LabelText);
                            }
                            // 外部已处理，重置标志
                            IsRectangleDrawingHandledExternally = false;
                        }
                    }
                }
                else if (_isDrawingCircle && _currentUserDrawMode == UserDrawMode.Circle)
                {
                    _isDrawingCircle = false;
                    if (_drawCircleRadius > 10)
                    {
                        double gray = AddUserCircle == null ? _hWindowTools.GetGrayForCircle(_drawCircleStartPoint, _drawCircleRadius)
                            : AddUserCircle(_drawCircleStartPoint, _drawCircleRadius);
                        _hWindowTools.AddUserCircle(new HalconWindowTools.DrawCircleInfo
                        {
                            Center = _drawCircleStartPoint,
                            Radius = (int)_drawCircleRadius,
                            GrayValue = gray,
                            Label = _LabelText
                        });
                        CircleDrawn?.Invoke(_drawCircleStartPoint, _drawCircleRadius);
                    }
                }
                else if (_isDrawingPolygon && _currentUserDrawMode == UserDrawMode.Polygon)
                {

                }
                else if (_isDrawingLs2Ls && _currentUserDrawMode == UserDrawMode.Ls2Ls)
                {

                }
                else if (_isDrawingBox && _currentUserDrawMode == UserDrawMode.ThresholdSeg)
                {
                    _isDrawingBox = false;
                    if (_drawBoxRectangle.Width > 1 && _drawBoxRectangle.Height > 1)
                    {
                        HObject origImage = _hWindowTools.GetOrigImageClone();
                        if (origImage != null)
                        {
                            AutoAreaRectDrawn?.Invoke(this, new AutoAreaRectDrawnEventArgs
                            {
                                OriginalImage = origImage,
                                ROI = _drawBoxRectangle,
                                LineNumber = _hWindowTools._lineNumber,
                                UserLabel = _LabelText,
                                View = this
                            });
                            // 注意：origImage 不在此释放，由事件接收方负责
                        }
                    }
                }
                else if (_isDrawingBox && _currentUserDrawMode == UserDrawMode.RegionGrowing)
                {
                    _isDrawingBox = false;
                    if (_drawBoxRectangle.Width > 1 && _drawBoxRectangle.Height > 1)
                    {
                        HObject origImage = _hWindowTools.GetOrigImageClone();
                        if (origImage != null)
                        {
                            AutoAreaRectDrawn?.Invoke(this, new AutoAreaRectDrawnEventArgs
                            {
                                OriginalImage = origImage,
                                ROI = _drawBoxRectangle,
                                LineNumber = _hWindowTools._lineNumber,
                                UserLabel = _LabelText,
                                View = this,
                                Mode = 1   // 区域生长模式
                            });
                            // origImage 由事件接收方负责释放
                        }
                    }
                }
                else if (_isDrawingArrow && _currentUserDrawMode == UserDrawMode.Arrow)
                {

                }
                // 释放鼠标，结束拖动
                else if (_isDraggingPoint)
                {
                    _isDraggingPoint = false;
                    if (_selectedPolygon != null)
                    {
                        _selectedPolygon.IsDragging = false;
                        _selectedPolygon.EditingPointIndex = -1;
                    }
                    _draggingPointIndex = -1;
                    _hWindowTools.ReShowDrawALL();
                }
                else
                {
                    _mouseLeftButDown = false;
                    try
                    {
                        _hWindowControl.HalconWindow.GetMposition(out _, out _, out _);
                    }
                    catch { }
                }
            }
        }

        // 鼠标 滚轮
        private void HWindowControl_HMouseWheel(object sender, HMouseEventArgs e)
        {
            HTuple mode = e.Delta;
            _hWindowTools.DispImageZoom(mode);
        }

        private int _fitRectangleMaxX = 0, _fitRectangleMaxY = 0, _fitRectangleMaxArea = 0;
        private string _fitRectangleTip = string.Empty;
        public void SetAreaFitRectangle(int maxX, int maxY, int maxArea, string tip, int type)
        {
            _fitRectangleMaxX = maxX;
            _fitRectangleMaxY = maxY;
            _fitRectangleMaxArea = maxArea;
            _fitRectangleTip = tip;
            if (type == 1)       //绘制XY矩形框方式
            {

            }
            else
            {
                int len = (int)Math.Sqrt(_fitRectangleMaxArea);
                if (type == 2)       //绘制area正方形框
                {
                    _fitRectangleMaxX = len;
                    _fitRectangleMaxY = len;
                }
                else if (type == 0)      //根据最小面积绘制
                {
                    if (maxX * maxY > maxArea)   //XY面积大于area时，使用area正方形框
                    {
                        _fitRectangleMaxX = len;
                        _fitRectangleMaxY = len;
                    }
                }
            }
            HOperatorSet.SetColor(_hWindowControl.HalconWindow, "red");
            HalconHelper.SetHalconFont(_hWindowControl.HalconWindow, 12);
        }
        public void ClearAreaFitRectangle()
        {
            _fitRectangleMaxX = 0;
            _fitRectangleMaxY = 0;
            _fitRectangleMaxArea = 0;
            _fitRectangleTip = string.Empty;
        }

        // 计算两点间距离
        private double CalculateDistance(Point start, Point end)
        {
            return Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        }

        // 新增：判断点是否在多边形内部（增加容差）
        private bool IsPointInPolygon(Point p, List<Point> polygon)
        {
            // 先检查是否靠近多边形边界（增加5像素容差）
            for (int i = 0; i < polygon.Count; i++)
            {
                int j = (i + 1) % polygon.Count;
                double distance = DistanceToSegment(p, polygon[i], polygon[j]);
                if (distance <= 8) // 如果靠近边界，也算选中
                    return true;
            }

            // 标准的点在多边形内判断
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                    (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) /
                    (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // 新增：计算点到线段的最短距离
        private double DistanceToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lengthSq = dx * dx + dy * dy;

            if (lengthSq == 0) return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));

            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSq;
            t = Math.Max(0, Math.Min(1, t));

            double projX = a.X + t * dx;
            double projY = a.Y + t * dy;

            return Math.Sqrt(Math.Pow(p.X - projX, 2) + Math.Pow(p.Y - projY, 2));
        }

        // 窗宽窗位 滚动
        private void wlTrackBarScroll(object sender, EventArgs e)
        {
            _wwTrackBar.Minimum = _wlTrackBar.Value;
            int wwwlMin = _wlTrackBar.Value;
            int wwwlMax = _wwTrackBar.Value;
            _wlLabel.Text = wwwlMin.ToString();
            _wwLabel.Text = wwwlMax.ToString();
            _hWindowTools.UpdataImageByWLWW(wwwlMin, wwwlMax);

            // 发布窗宽窗位变化
            WWWLChanged?.Invoke(wwwlMin, wwwlMax);
        }

        // 窗宽窗位 滚动
        private void wwTrackBarScroll(object sender, EventArgs e)
        {
            _wlTrackBar.Maximum = _wwTrackBar.Value;
            int wwwlMin = _wlTrackBar.Value;
            int wwwlMax = _wwTrackBar.Value;
            _wlLabel.Text = wwwlMin.ToString();
            _wwLabel.Text = wwwlMax.ToString();
            _hWindowTools.UpdataImageByWLWW(wwwlMin, wwwlMax);

            // 发布窗宽窗位变化
            WWWLChanged?.Invoke(wwwlMin, wwwlMax);
        }

        // 切换填充模式
        private void button2_Click(object sender, EventArgs e)
        {
            _hWindowTools.IsFilled = !_hWindowTools.IsFilled; // 切换实心和空心模式
            //btnToggleFillMode.Text = hWindowTool.IsFilled ? "切换到空心模式" : "切换到实心模式"; // 更新按钮文本
        }


        // 更新窗宽窗位到界面，-1表示保持原数值
        private void UpdataWLWW(int wwwlMin, int wwwlMax)
        {
            if (_wlTrackBar == null) return;

            if (wwwlMin != -1)
            {
                if (wwwlMin < _wlTrackBar.Minimum)
                {
                    wwwlMin = _wlTrackBar.Minimum;
                }
                else if (wwwlMin > _wlTrackBar.Maximum)
                {
                    wwwlMin = _wlTrackBar.Maximum;
                }
                _wlLabel.Text = wwwlMin.ToString();
                _wlTrackBar.Value = wwwlMin;
            }

            if (wwwlMax != -1)
            {
                if (wwwlMax < _wwTrackBar.Minimum)
                {
                    wwwlMax = _wwTrackBar.Minimum;
                }
                else if (wwwlMax > _wwTrackBar.Maximum)
                {
                    wwwlMax = _wwTrackBar.Maximum;
                }
                _wwLabel.Text = wwwlMax.ToString();
                _wwTrackBar.Value = wwwlMax;
            }
        }

        public void EnhanceImage()
        {
            _hWindowTools.EnhanceImage();
        }

        public void SaveImage(string path)
        {
            _hWindowTools.SaveShowingImage(path);
        }

        private void ClearTempPolygon()
        {
            // 查找并移除标签以 "__TEMP__" 开头的多边形
            var toRemove = _hWindowTools._drawUserPolygons
                .Where(p => p.Label != null && p.Label.StartsWith("__TEMP__")).ToList();
            foreach (var p in toRemove)
            {
                _hWindowTools._drawUserPolygons.Remove(p);
                // 注意：不修改 _drawActionTypes，因为临时多边形从未加入
            }
        }

    }

}
