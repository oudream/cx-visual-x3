using CommonInterfaces;
using HalconDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HalconDriverModule
{
    public class HalconWindowTools : IDisposable
    {
        // 图像增强回调：输入当前显示图，输出增强图
        public delegate HObject EnhanceImageHandler(HObject srcImage, int enhType = 0, float bright = 0.0f, int contrast = 0, int grayValue = 0, int isWhole = 1);
        // 放大比率回调：输入当前显示图，输出放大比率
        public delegate double RatioCountHandler(HObject ho_Image);

        // 外部注入的增强图算法
        public static EnhanceImageHandler EnhanceImageHandlerOne = null;
        // 外部注入的放大比率算法
        public static RatioCountHandler RatioCountHandlerOne = null;

        // 绘制度量小数位数
        public const string DrawMeasurePrecision = "F3";

        //绘制字体偏移步长
        public const int DarwTextSetp = 35;

        public delegate void CamMoveDelegate(int moveCamId, int mouseRow, int mouseCol);
        public CamMoveDelegate CamMove;

        public bool ShowSNR { get; set; } = false;

        public class DrawObjectInfo
        {
            public HObject HObject;
            public int LineWidth;
            public string Color;
            public string DrawMode;
            public int Font;
            public Action<HTuple> DrawAction;
            public DrawTextInfo TextInfo;
        }

        public class DrawMeasureLineInfo
        {
            public System.Drawing.Point Start { get; set; }
            public System.Drawing.Point End { get; set; }
            public double Length { get; set; }
            public string Label { get; set; }
            public string Color { get; set; }
            // 非空时优先作为画面文字；Label 仍可保留给旧业务作内部标识。
            public string DisplayText { get; set; }
        }

        public class DrawRectangleInfo
        {
            // 矩形
            public System.Drawing.Rectangle Rectangle { get; set; }
            // 平均灰度值
            public double GrayValue { get; set; }

            public string Label { get; set; }
            public string Color { get; set; }
            // null 保持旧版自动尺寸/灰度标签；空字符串表示只绘制几何、不显示文字。
            public string DisplayText { get; set; }

            public List<Point> ToPoints()
            {
                return new List<Point>
                {
                    new Point(Rectangle.Location.X,Rectangle.Location.Y),
                    new Point(Rectangle.Right, Rectangle.Top),
                    new Point(Rectangle.Right, Rectangle.Bottom),
                    new Point(Rectangle.Left, Rectangle.Bottom),
                };
            }
        }

        public class DrawCircleInfo
        {
            // 圆心
            public System.Drawing.Point Center { get; set; }
            // 半径
            public int Radius { get; set; }
            // 平均灰度值
            public double GrayValue { get; set; }

            public string Label { get; set; }
            public string Color { get; set; }
            public string DisplayText { get; set; }
        }

        public class DrawPolygonInfo
        {
            public List<System.Drawing.Point> Points { get; set; }
            // 平均灰度值
            public double GrayValue { get; set; }

            public double width { get; set; }
            public double height { get; set; }
            public double area { get; set; }

            // 新增：编辑状态相关字段
            [JsonIgnore]
            public bool IsSelected { get; set; } = false;      // 是否被选中

            [JsonIgnore]
            public int EditingPointIndex { get; set; } = -1;    // 当前正在编辑的点索引

            [JsonIgnore]
            public bool IsDragging { get; set; } = false;       // 是否正在拖动

            [JsonIgnore]
            public const int HIT_RADIUS = 10;                   // 点选命中半径

            public string Label { get; set; }
            public string Color { get; set; }

            // null 保持旧版自动面积/灰度标签；空字符串表示仅绘制多边形。
            public string DisplayText { get; set; }

            public void UpdateWHA(double unitArea)
            {
                if (Points != null)
                {
                    var rows = new HTuple(Points.Select(p => (HTuple)p.Y).ToArray());
                    var cols = new HTuple(Points.Select(p => (HTuple)p.X).ToArray());
                    HOperatorSet.GenRegionPolygonFilled(out HObject region, rows, cols);

                    // 在多边形中心显示面积
                    HOperatorSet.AreaCenter(region, out HTuple hv_Area, out HTuple rowC, out HTuple colC);
                    //HOperatorSet.SetTposition(_hWindowControl.HalconWindow, rowC.D, colC.D);

                    //var unitArea = CalcActualSizeMM(1, lineNumber);
                    area = unitArea * unitArea * hv_Area.D;
                    double minX = Points.Min(p => p.X);
                    double maxX = Points.Max(p => p.X);
                    double minY = Points.Min(p => p.Y);
                    double maxY = Points.Max(p => p.Y);

                    width = (maxX - minX);
                    height = (maxY - minY);

                    region.Dispose();
                }

            }

            // 新增：更新点位置
            public void UpdatePoint(int index, Point newPos)
            {
                if (index >= 0 && index < Points.Count)
                {
                    Points[index] = newPos;
                }
            }
        }

        public class DrawTextInfo
        {
            public double Row { get; set; }
            public double Col { get; set; }
            public string Color { get; set; }
            public string Text { get; set; }
            public int FontSize { get; set; } = 0;   // 0 表示使用全局字体
        }

        public class DrawLs2LsInfo
        {
            public System.Drawing.Point L1Start { get; set; }
            public System.Drawing.Point L1End { get; set; }
            public System.Drawing.Point L2Start { get; set; }
            public System.Drawing.Point L2End { get; set; }

            // 最短连线两端点（各在线段上的最近点）
            public System.Drawing.Point ClosestOnL1 { get; set; }
            public System.Drawing.Point ClosestOnL2 { get; set; }

            public double DistancePixel { get; set; }
            public double AngleDeg { get; set; }   // 两条线段的夹角(0~180)
            public string Label { get; set; }      // 比如 "θ.Ls2Ls"
        }

        public class DrawArrowInfo
        {
            public System.Drawing.Point Head { get; set; }   // 箭头尖（第一点）
            public System.Drawing.Point Tail { get; set; }   // 箭尾（第二点）
            public string Label { get; set; } = "θ.Arrow";
            public string Color { get; set; }
            public string DisplayText { get; set; }
        }

        private int _currentBeginRow, _currentBeginCol, _currentEndRow, _currentEndCol;
        private int _zoomBeginRow, _zoomBeginCol, _zoomEndRow, _zoomEndCol;
        private HObject _origImage;
        private OpenCvSharp.Mat _origMat;           // 原始16位数据（CV_16U）
        private HObject _showingImage;
        private HObject _showingImage2;

        // 要绘制的对象
        private readonly List<DrawObjectInfo> _drawObjectList;
        // 拟合的最小外接圆
        private HObject _circumCircle;
        public double CircumCircleCenterOffsetX { get { return _circumCircleCenterOffsetX; } }
        public double CircumCircleCenterOffsetY { get { return _circumCircleCenterOffsetY; } }
        private double _circumCircleCenterOffsetX;
        private double _circumCircleCenterOffsetY;
        private double _circumCircleRadius;
        // 添加布尔标志以指示当前是否在找圆并标记
        public bool IsFindCircle { get; set; } = false;
        private double _findCircleMaxRadius;

        //钢珠找圆找到的坐标和半径
        public double FindCircleCenterOffsetX { get { return _findCircleCenterOffsetX; } }
        public double FindCircleCenterOffsetY { get { return _findCircleCenterOffsetY; } }

        private double _findCircleCenterOffsetX;
        private double _findCircleCenterOffsetY;
        private double _findCircleRadius;
        public int FindCircleGray { get; set; } = 22000;
        // 用户绘画
        // 用户绘画的测量线，放大比
        public readonly List<DrawMeasureLineInfo> _drawUserMeasureLines;
        // 临时交互预览不进入用户绘制列表：鼠标移动时可反复刷新，完成后才由调用方保存结果。
        private readonly List<DrawMeasureLineInfo> _transientInteractionLines = new List<DrawMeasureLineInfo>();
        private readonly List<System.Drawing.Point> _transientInteractionMarkers = new List<System.Drawing.Point>();
        private string _transientInteractionColor = "cyan";
        // 用户绘画的矩形
        public readonly List<DrawRectangleInfo> _drawUserRectangles;
        // 用户绘画的圆
        public readonly List<DrawCircleInfo> _drawUserCircles;
        // 用户绘画多边形
        public readonly List<DrawPolygonInfo> _drawUserPolygons;
        // 新增：用户绘画 线段-线段 距离
        public readonly List<DrawLs2LsInfo> _drawUserLs2Lses;
        // 用户绘画箭头
        public readonly List<DrawArrowInfo> _drawUserArrows;

        // Ls2LsID
        private int _drawLs2LsId = 0;
        // 指示ID
        private int _drawArrowId = 0;

        //自动区域提前使用灰度值
        public int DefectThresholdMin { get; set; } = 0;
        public int DefectThresholdMax { get; set; } = 135;

        public enum DrawActionType
        {
            AddRectangle,
            AddCircle,
            AddMeasureLine,
            AddPolygon,
            AddLs2Ls,
            AddArrow
        }
        public readonly List<DrawActionType> _drawActionTypes;

        public enum DisplayStyle
        {
            One = 1,
            Two = 2,
        }
        private DisplayStyle _displayStyle = DisplayStyle.One;
        public void SetDisplayStyle(DisplayStyle displayStyle, uint lineNumber = 0)
        {
            _displayStyle = displayStyle;
            ReShowDrawALL(lineNumber);
        }
        public void SetDisplayStyleOnly(DisplayStyle displayStyle)
        {
            _displayStyle = displayStyle;
        }

        public class UserDrawState
        {
            // 使用 JsonIgnore 来忽略此属性的序列化
            [JsonIgnore]
            public List<DrawObjectInfo> DrawObjectList { get; private set; }
            public List<DrawMeasureLineInfo> MeasureLines { get; private set; }
            public List<DrawRectangleInfo> UserRectangles { get; private set; }
            public List<DrawCircleInfo> UserCircles { get; private set; }
            public List<DrawPolygonInfo> UserPolygons { get; private set; }
            public List<DrawLs2LsInfo> UserLs2Lses { get; private set; }
            public List<DrawArrowInfo> UserArrows { get; private set; }
            // 同样使用 JsonIgnore 来忽略此属性的序列化
            [JsonIgnore]
            public List<DrawActionType> ActionTypes { get; private set; }

            public UserDrawState(
                List<DrawObjectInfo> drawObjectList,
                List<DrawMeasureLineInfo> measureLines,
                List<DrawRectangleInfo> userRectangles,
                List<DrawCircleInfo> userCircles,
                List<DrawPolygonInfo> userPolygons,
                List<DrawLs2LsInfo> userLs2Ls,
                List<DrawArrowInfo> userArrows,
                List<DrawActionType> actionTypes)
            {
                DrawObjectList = new List<DrawObjectInfo>(drawObjectList);
                MeasureLines = new List<DrawMeasureLineInfo>(measureLines);
                UserRectangles = new List<DrawRectangleInfo>(userRectangles);
                UserCircles = new List<DrawCircleInfo>(userCircles);
                UserPolygons = new List<DrawPolygonInfo>(userPolygons);
                UserLs2Lses = new List<DrawLs2LsInfo>(userLs2Ls);
                UserArrows = new List<DrawArrowInfo>(userArrows);
                ActionTypes = new List<DrawActionType>(actionTypes);
            }

            public UserDrawState()
            {
                DrawObjectList = new List<DrawObjectInfo>();
                MeasureLines = new List<DrawMeasureLineInfo>();
                UserRectangles = new List<DrawRectangleInfo>();
                UserCircles = new List<DrawCircleInfo>();
                UserPolygons = new List<DrawPolygonInfo>();
                UserLs2Lses = new List<DrawLs2LsInfo>();
                UserArrows = new List<DrawArrowInfo>();
                ActionTypes = new List<DrawActionType>();
            }
        }

        /// <summary>
        /// 将 UserDrawState 对象序列化成字节数组（内部为 JSON）
        /// </summary>
        /// <param name="state">UserDrawState 对象</param>
        /// <returns>序列化后得到的字节数组</returns>
        public static byte[] SerializeToBytes(UserDrawState state)
        {
            try
            {
                // 序列化配置
                var settings = new JsonSerializerSettings
                {
                    // Formatting.Indented 让 JSON 更容易阅读（带缩进）
                    Formatting = Formatting.Indented,
                    // ReferenceLoopHandling.Ignore 用于在出现循环引用时避免错误
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                // 先序列化为 JSON 字符串
                string jsonString = JsonConvert.SerializeObject(state, settings);

                // 再转换为 UTF8 字节数组
                return Encoding.UTF8.GetBytes(jsonString);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"HalconWindowTools 序列化异常：{ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 从字节数组反序列化回 UserDrawState 对象
        /// </summary>
        /// <param name="data">字节数组（内容是 JSON）</param>
        /// <returns>反序列化得到的对象</returns>
        public static UserDrawState DeserializeFromBytes(byte[] data)
        {
            try
            {
                if (data == null || data.Length == 0)
                    return null; // 或者返回一个新的 UserDrawState()

                // 先从字节数组还原 UTF8 字符串
                string jsonString = Encoding.UTF8.GetString(data);

                // 反序列化配置
                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                // 反序列化回 UserDrawState 对象
                return JsonConvert.DeserializeObject<UserDrawState>(jsonString, settings);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"HalconWindowTools 反序列化异常：{ex.Message}");
            }

            return null;
        }

        private readonly double _displayStyleOneMinScale = 1.0f;

        private double[] _fodFddRatios = new double[] { 1.0, 1.0, 1.0, 1.0 };
        private double _unitPixelActualSize = 0.014f;
        private int _fontMeasureSize = 22;
        private int _drawLineWidth = 1;

        public uint _lineNumber = 0;

        public bool SetLineNumber(uint lineNumber)
        {
            if (lineNumber >= 0 || lineNumber < _fodFddRatios.Count())
            {
                _lineNumber = lineNumber;
                return true;
            }
            else
            {
                return false;
            }
        }


        public void UpdateDrawParams(double[] fodFddRatios, double unitPixelActualSize, int fontMeasureSize, int drawLineWidth)
        {
            {
                for (int i = 0; i < fodFddRatios.Length; i++)
                {
                    if (i < _fodFddRatios.Length)
                    {
                        _fodFddRatios[i] = fodFddRatios[i];
                    }
                }
            }
            _unitPixelActualSize = unitPixelActualSize;
            _fontMeasureSize = fontMeasureSize;
            _drawLineWidth = drawLineWidth;
        }

        public void UpdateFodFddRatio(double fodFddRatio, int lineIndex)
        {
            if (lineIndex >= 0 && lineIndex < _fodFddRatios.Length)
            {
                _fodFddRatios[lineIndex] = fodFddRatio;
            }
        }

        public double GetFodFddRatio(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= _fodFddRatios.Length)
            {
                LogHelper.Error($"GetFodFddRatio 索引越界：尝试获取线号 {lineIndex}，但数组长度为 {_fodFddRatios.Length}。返回 0 作为异常标识。");
                return 0.0; // 返回 0 明确表示获取失败
            }
            return _fodFddRatios[lineIndex];
        }

        // 绘画标准位置轮廓
        private HObject _showingShapeModelS = null;
        private HObject _showingShapeModelL = null;

        private readonly HWindowControl _hWindowControl;

        // 添加布尔标志以指示当前的绘制模式
        public bool IsFilled { get; set; } = false;

        // 添加布尔标志以指示是否绘制中心十字
        public bool DrawCenterCross { get; set; } = false;

        public HWindowControl GetHWindowControl() => _hWindowControl;

        public double GetCircumCircleRadius() => _circumCircleRadius;
        public double GetFindCircleRadius() => _findCircleRadius;

        public HObject GetOrigImageClone() { return _origImage == null ? null : _origImage.Clone(); }
        public HObject GetShowImageClone() { return _showingImage == null ? null : _showingImage.Clone(); }

        public delegate void ImageChangedDelegate(HObject orgImage, HObject showingImage);
        private ImageChangedDelegate _imageChanged;

        public delegate void ReShowDrawingALLDelegate(HWindowControl hWindowControl, HObject showingImage);
        public ReShowDrawingALLDelegate ReShowDrawingALL;

        // 计算图片放大倍数
        private double fitRow1 = 0, fitRow2 = 0;
        private double scaleFactor = 1.0;               // 图片缩放比例
        public double GetScaleFactor() => scaleFactor / originalScaleFactor;
        private double originalScaleFactor = 3.88;      // 图片原始缩放比例

        public HalconWindowTools(HWindowControl hWindowControl1
            , int fontMeasureSize
            , double imageUnitPixelActualSize
            , double[] fodFddRatios)
        {
            //if (HalconAPI.isWindows)
            //    HOperatorSet.SetSystem("use_window_thread", "true");

            _hWindowControl = hWindowControl1;

            _fontMeasureSize = fontMeasureSize;

            // 设置线条颜色和字体
            HOperatorSet.SetColor(_hWindowControl.HalconWindow, "green");

            // 检查 hv_Font 是否有元素
            HOperatorSet.QueryFont(_hWindowControl.HalconWindow, out var hv_Font);
            if (hv_Font.Length > 0)
            {
                // 获取第一个字体名
                var fontType = hv_Font.TupleSelect(0).S;

                // 检查字体名是否已经包含 "-" 后缀
                if (!fontType.EndsWith("-"))
                {
                    // 如果没有 "-" 后缀，则加上指定的大小
                    fontType += "-";
                }
            }

            HalconHelper.SetHalconFont(_hWindowControl.HalconWindow, 24);

            HOperatorSet.SetDraw(_hWindowControl.HalconWindow, "margin");
            // hw_ctrl.HalconWindow.SetDraw("margin");

            _drawObjectList = new List<DrawObjectInfo>();

            _drawActionTypes = new List<DrawActionType>();
            _drawUserMeasureLines = new List<DrawMeasureLineInfo>();
            _drawUserRectangles = new List<DrawRectangleInfo>();
            _drawUserCircles = new List<DrawCircleInfo>();
            _drawUserPolygons = new List<DrawPolygonInfo>();
            _drawUserLs2Lses = new List<DrawLs2LsInfo>();
            _drawUserArrows = new List<DrawArrowInfo>();

            _unitPixelActualSize = imageUnitPixelActualSize;

            // 防御性拷贝：避免外部数组被改导致你内部读到“半更新”
            var copyFs = new double[fodFddRatios.Length];
            Array.Copy(fodFddRatios, copyFs, fodFddRatios.Length);
            _fodFddRatios = copyFs;
        }

        public HWindow GetHWindow()
        {
            return _hWindowControl.HalconWindow;
        }

        // 打开图像
        public void OpenImage(string imgPath)
        {
            if (!File.Exists(imgPath))
                return;

            if (HalconHelper.SplitFileNameHandel != null)
            {
                if (HalconHelper.SplitFileNameHandel(
                    imgPath,
                    out var lineNumber,
                    out _, out _, out _, out _, out _))
                {
                    if (lineNumber < _fodFddRatios.Length)
                    {
                        _lineNumber = lineNumber;
                    }
                }
            }

            // 读取新图像
            HObject newImage;
            HOperatorSet.ReadImage(out newImage, imgPath);

            // 判断图像是否需要更新显示
            bool sizeChanged;
            if (_origImage != null)
            {
                // 获取原图像的宽高
                HTuple widthOrig, heightOrig;
                HOperatorSet.GetImageSize(_origImage, out widthOrig, out heightOrig);

                // 获取新图像的宽高
                HTuple widthNew, heightNew;
                HOperatorSet.GetImageSize(newImage, out widthNew, out heightNew);

                // 判断图像大小是否发生变化
                sizeChanged = (widthOrig.I != widthNew.I) || (heightOrig.I != heightNew.I);
            }
            else
            {
                // 如果原图像为空，说明是首次加载
                sizeChanged = true;
            }

            // 释放原图像资源
            _origImage?.Dispose();
            _origImage = newImage;

            // 释放并克隆新图像到显示图像
            _showingImage?.Dispose();
            _showingImage = _origImage.Clone();

            // 触发图像改变事件
            OnImageChanged(_origImage, _showingImage);

            // 如果图像大小发生变化，则调用 DispImageFit 方法
            if (sizeChanged)
            {
                DispImageFit();
            }
        }

        public void ShowRegion(HObject ho_Region)
        {
            HOperatorSet.DispObj(ho_Region, _hWindowControl.HalconWindow);
        }

        // image的生命周期交给 _originalImage
        public void OpenImage(HObject image, int wwwlMin, int wwwlMax, bool bClone = true)
        {
            if (image == null) return;

            // 打印图像通道数、位深度、数据类型、行列
            //Console.WriteLine($"通道数:{imageOrg.Channels()} 位深度:{imageOrg.Depth()} 数据类型:{imageOrg.Type()} 行列:{imageOrg.Rows} x {imageOrg.Cols}");
            Stopwatch stopwatch = Stopwatch.StartNew();

            HObject imageByWL = null;
            try
            {
                if (wwwlMin == 0 || wwwlMax == 0)
                {
                    if (bClone)
                        imageByWL = image.Clone();
                    else
                        imageByWL = image;
                }
                else
                {
                    UseWLForImage(image, out imageByWL, wwwlMin, wwwlMax);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"窗宽窗位设置失败，{ex.Message}");
            }

            if (imageByWL == null) return;

            _origImage?.Dispose();
            _origImage = image;
            _showingImage?.Dispose();
            _showingImage = imageByWL;
            _origMat?.Dispose();
            // 获取像素指针
            HOperatorSet.GetImagePointer1(_origImage, out var ptr, out var type, out var width, out var height);
            // 包装为 OpenCV Mat（只读，不复制数据）
            _origMat = new OpenCvSharp.Mat(height, width, OpenCvSharp.MatType.CV_16U, (IntPtr)ptr);

            OnImageChanged(image, imageByWL);

            CalculateScaleFactor();

            ReShowDrawALL();

            stopwatch.Stop();
            var costImageProcessing = stopwatch.ElapsedMilliseconds;
        }
        public HObject ApplyWindowLevel(int window, int level)
        {
            if(_origMat == null)
                return null;
            double min = level - window / 2.0;
            double max = level + window / 2.0;
            min = Math.Max(0, min);
            max = Math.Min(16383, max);   // 14位有效数据

            OpenCvSharp.Mat dst8 = new OpenCvSharp.Mat();
            double scale = 255.0 / (max - min);
            double offset = -min * scale;
            _origMat.ConvertTo(dst8, OpenCvSharp.MatType.CV_8UC1, scale, offset);

            // 3. 将 Mat 转换为 HObject (byte 类型)
            HObject hoDisplay;
            HOperatorSet.GenImage1(out hoDisplay, "byte", _origMat.Width, _origMat.Height, dst8.Data);

            dst8.Dispose();   // 释放临时 Mat
            return hoDisplay;
        }

        public void SetWindowLevel(int window, int level)
        {
            if(_origMat == null) return;
            var imageByWL = ApplyWindowLevel(window, level);
            if(imageByWL == null) return;
            _showingImage?.Dispose();
            _showingImage = imageByWL;
            ReShowDrawALL();
        }
        public void StartImageChanged(ImageChangedDelegate imageChanged)
        {
            _imageChanged = imageChanged;
        }

        public void StopImageChanged(ImageChangedDelegate imageChanged)
        {
            _imageChanged = null;
        }

        private void OnImageChanged(HObject orgImage, HObject showingImage)
        {
            _imageChanged?.Invoke(orgImage, showingImage);
            if (_circumCircle != null)
            {
                FitCircumCircle();
            }
        }

        // 获取图像基本信息
        public bool GetImageInfo(out int width, out int height, out int channels, out int bitDepth)
        {
            return HalconHelper.GetImageInfo(_origImage, out width, out height, out channels, out bitDepth, out _);
        }

        // 获取图像基本信息
        public (double, double) GetMinMaxGray()
        {
            return HalconHelper.GetMinMaxGray(_origImage);
        }

        // 通过调整窗宽窗位来更新图像
        public void UpdataImageByWLWW(int wwwlMin = 0, int wwwlMax = 0)
        {
            if (_origImage == null) return;

            if (wwwlMin == 0 || wwwlMax == 0)
            {
                _showingImage?.Dispose();
                _showingImage = _origImage.Clone();
            }
            else
            {
                UseWLForImage(_origImage, out HObject imageByWL, wwwlMin, wwwlMax);
                _showingImage?.Dispose();
                _showingImage = imageByWL;
            }

            // 原图没有变
            //OnImageChanged(_origImage, _showingImage);

            ReShowDrawALL();
        }

        // 设置窗宽窗位，返回新的图像 ho_ImageSetWL
        private void UseWLForImage(HObject ho_Image, out HObject ho_ImageSetWL, int wwwlMin, int wwwlMax)
        {
            HOperatorSet.CopyImage(ho_Image, out HObject imageCopy);
            HOperatorSet.GetImagePointer1(imageCopy, out var ptrImage, out HTuple type1, out var img_width, out var img_height);

            short[] img_data = new short[img_width * img_height];
            Marshal.Copy(ptrImage, img_data, 0, img_data.Length);
            SetImageWL(ptrImage, img_data, img_width, img_height, out ho_ImageSetWL, wwwlMin, wwwlMax);
            imageCopy.Dispose();
        }

        // 设置窗宽窗位
        private static void SetImageWL(IntPtr ptrImage, short[] img_data, int img_width, int img_height, out HObject ho_ImageSetWL, int wwwlMin, int wwwlMax)
        {
            //var min = wl - ww / 2.0;
            //var max = wl + ww / 2.0;
            var min = wwwlMin;
            var max = wwwlMax;
            double ww = max - min;
            var len = img_width * img_height;
            short[] data = new short[img_data.Length];

            Parallel.ForEach(Partitioner.Create(0, len), (range) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var val = (ushort)img_data[i];

                    if (val < min) val = 0;
                    else if (val > max) val = 255;
                    else val = (ushort)((val - min) / ww * 255);
                    data[i] = (short)val;
                }
            });
            Marshal.Copy(data, 0, ptrImage, img_data.Length);
            HOperatorSet.GenImage1(out ho_ImageSetWL, "uint2", img_width, img_height, ptrImage);
        }

        // 自适应显示图片
        public void DispImageFit()
        {
            if (_showingImage == null) return;
            try
            {
                GetImageSize(out int img_width, out int img_height);
                //int win_width = hw_ctrl.ImagePart.Width;
                //int win_height = hw_ctrl.ImagePart.Height;
                int win_width = _hWindowControl.Size.Width;
                int win_height = _hWindowControl.Size.Height;

                double w_ratio = 1.0 * img_width / win_width;
                double h_ratio = 1.0 * img_height / win_height;
                double ratio = (w_ratio > h_ratio ? w_ratio : h_ratio);
                double column1 = -(ratio * win_width - img_width) / 2.0;
                double row1 = -(ratio * win_height - img_height) / 2.0;
                double column2 = (ratio * win_width - img_width) / 2.0 + img_width;
                double row2 = (ratio * win_height - img_height) / 2.0 + img_height;

                HOperatorSet.SetPart(_hWindowControl.HalconWindow, row1 - 1, column1 - 1, row2 - 1, column2 - 1);

                CalculateScaleFactor();

                ReShowDrawALL();
            }
            catch (Exception)
            {
                //LogHelper.Error(ex.Message);
            }
        }

        // 放大或缩放图片
        public void DispImageZoom(HTuple mode)
        {
            if (_showingImage == null) return;
            try
            {
                _hWindowControl.HalconWindow.GetMpositionSubPix(out var Mouse_row, out var Mouse_col, out _);
                GetImageSize(out int hv_imageWidth, out int hv_imageHeight);
                _hWindowControl.HalconWindow.GetPart(out _currentBeginRow, out _currentBeginCol, out _currentEndRow, out _currentEndCol);
                if (mode > 0)//图像放大,当窗口过小时可能出现除数为0
                {
                    _zoomBeginRow = (int)(_currentBeginRow + (Mouse_row - _currentBeginRow) * 0.300d);
                    _zoomBeginCol = (int)(_currentBeginCol + (Mouse_col - _currentBeginCol) * 0.300d);
                    _zoomEndRow = (int)(_currentEndRow - (_currentEndRow - Mouse_row) * 0.300d);
                }
                else//图像缩小
                {
                    _zoomBeginRow = (int)(Mouse_row - (Mouse_row - _currentBeginRow) / 0.700d);
                    _zoomBeginCol = (int)(Mouse_col - (Mouse_col - _currentBeginCol) / 0.700d);
                    _zoomEndRow = (int)(Mouse_row + (_currentEndRow - Mouse_row) / 0.700d);
                }

                var hw_width = _hWindowControl.WindowSize.Width;
                var hw_height = _hWindowControl.WindowSize.Height;
                double windowPartRatio = 1.0 * hw_height / hw_width;
                _zoomEndCol = (int)((_zoomEndRow - _zoomBeginRow) / windowPartRatio + _zoomBeginCol);

                var _isOutOfArea = _zoomBeginRow >= hv_imageHeight || _zoomBeginCol >= hv_imageWidth || _zoomEndRow <= 0 || _zoomEndCol <= 0;
                var _isOutOfSize = (_zoomEndRow - _zoomBeginRow) > hv_imageHeight * 20 || (_zoomEndCol - _zoomBeginCol) > hv_imageWidth * 20;//避免像素过小
                var _isOutOfPixel = hw_height / (_zoomEndRow - _zoomBeginRow) > 400 || hw_width / (_zoomEndCol - _zoomBeginCol) > 400; //避免像素过大

                if (_isOutOfArea || _isOutOfSize || _isOutOfPixel) return;

                _hWindowControl.HalconWindow.SetPaint(new HTuple("default"));
                _hWindowControl.HalconWindow.SetPart(_zoomBeginRow, _zoomBeginCol, _zoomEndRow, _zoomEndCol);

                CalculateScaleFactor();

                ReShowDrawALL();
            }
            catch (Exception)
            {
                //LogHelper.Error(ex.Message);
            }
        }

        /// <summary>
        /// 计算图片放大倍数
        /// </summary>
        public void CalculateScaleFactor()
        {
            if (fitRow1 == 0 && fitRow2 == 0)
            {
                if (_showingImage == null) return;
                try
                {
                    GetImageSize(out int img_width, out int img_height);
                    //int win_width = hw_ctrl.ImagePart.Width;
                    //int win_height = hw_ctrl.ImagePart.Height;
                    int win_width = _hWindowControl.Size.Width;
                    int win_height = _hWindowControl.Size.Height;

                    double w_ratio = 1.0 * img_width / win_width;
                    double h_ratio = 1.0 * img_height / win_height;
                    double ratio = (w_ratio > h_ratio ? w_ratio : h_ratio);
                    fitRow1 = -(ratio * win_height - img_height) / 2.0;
                    fitRow2 = (ratio * win_height - img_height) / 2.0 + img_height;
                    scaleFactor = 1.0;
                    originalScaleFactor = ratio;
                }
                catch (Exception)
                {
                    //LogHelper.Error(ex.Message);
                }
            }
            if (fitRow1 != fitRow2)
            {
                HOperatorSet.GetPart(_hWindowControl.HalconWindow, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                scaleFactor = (fitRow2 - fitRow1) / (1.0 * (row2 - row1));
            }
        }

        // 移动图片
        public void DispImageMove(int btn_down_row, int btn_down_col)
        {
            if (_showingImage == null) return;
            try
            {
                _hWindowControl.HalconWindow.GetPart(out var current_beginRow, out var current_beginCol, out var current_endRow, out int current_endCol);
                _hWindowControl.HalconWindow.GetMposition(out var mouse_post_row, out var mouse_pose_col, out _);
                _hWindowControl.HalconWindow.SetPaint(new HTuple("default"));
                _hWindowControl.HalconWindow.SetPart(current_beginRow + btn_down_row - mouse_post_row, current_beginCol + btn_down_col - mouse_pose_col, current_endRow + btn_down_row - mouse_post_row, current_endCol + btn_down_col - mouse_pose_col);

                ReShowDrawALL();
            }
            catch (Exception)
            {
                //当移动鼠标超出窗口会报错
                //System.Windows.MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// 以指定点为中心缩放图像（内部通用方法）
        /// </summary>
        /// <param name="mode">正数放大，负数缩小</param>
        /// <param name="centerRow">缩放中心行坐标（图像坐标）</param>
        /// <param name="centerCol">缩放中心列坐标（图像坐标）</param>
        private void ZoomImageAt(HTuple mode, double centerRow, double centerCol)
        {
            if (_showingImage == null) return;
            try
            {
                GetImageSize(out int hv_imageWidth, out int hv_imageHeight);
                _hWindowControl.HalconWindow.GetPart(out _currentBeginRow, out _currentBeginCol, out _currentEndRow, out _currentEndCol);

                if (mode > 0) // 放大
                {
                    _zoomBeginRow = (int)(_currentBeginRow + (centerRow - _currentBeginRow) * 0.300d);
                    _zoomBeginCol = (int)(_currentBeginCol + (centerCol - _currentBeginCol) * 0.300d);
                    _zoomEndRow = (int)(_currentEndRow - (_currentEndRow - centerRow) * 0.300d);
                }
                else // 缩小
                {
                    _zoomBeginRow = (int)(centerRow - (centerRow - _currentBeginRow) / 0.700d);
                    _zoomBeginCol = (int)(centerCol - (centerCol - _currentBeginCol) / 0.700d);
                    _zoomEndRow = (int)(centerRow + (_currentEndRow - centerRow) / 0.700d);
                }

                var hw_width = _hWindowControl.WindowSize.Width;
                var hw_height = _hWindowControl.WindowSize.Height;
                double windowPartRatio = 1.0 * hw_height / hw_width;
                _zoomEndCol = (int)((_zoomEndRow - _zoomBeginRow) / windowPartRatio + _zoomBeginCol);

                // 边界保护
                var _isOutOfArea = _zoomBeginRow >= hv_imageHeight || _zoomBeginCol >= hv_imageWidth || _zoomEndRow <= 0 || _zoomEndCol <= 0;
                var _isOutOfSize = (_zoomEndRow - _zoomBeginRow) > hv_imageHeight * 20 || (_zoomEndCol - _zoomBeginCol) > hv_imageWidth * 20;
                var _isOutOfPixel = hw_height / (_zoomEndRow - _zoomBeginRow) > 400 || hw_width / (_zoomEndCol - _zoomBeginCol) > 400;

                if (_isOutOfArea || _isOutOfSize || _isOutOfPixel) return;

                _hWindowControl.HalconWindow.SetPaint(new HTuple("default"));
                _hWindowControl.HalconWindow.SetPart(_zoomBeginRow, _zoomBeginCol, _zoomEndRow, _zoomEndCol);

                CalculateScaleFactor();
                ReShowDrawALL();
            }
            catch (Exception)
            {
                // 可记录日志
            }
        }

        /// <summary>
        /// 以当前视口中心为基准缩放（供按钮使用）
        /// </summary>
        public void DispImageZoomAtCenter(HTuple mode)
        {
            if (_showingImage == null) return;
            try
            {
                _hWindowControl.HalconWindow.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                double centerRow = (row1.D + row2.D) / 2.0;
                double centerCol = (col1.D + col2.D) / 2.0;
                ZoomImageAt(mode, centerRow, centerCol);
            }
            catch (Exception)
            {
                // 忽略
            }
        }

        // 添加用户矩形
        public void AddUserRectangle(Rectangle rect, double grayValue, string Label = "", string color = null, string displayText = null)
        {
            _drawUserRectangles.Add(new DrawRectangleInfo { Rectangle = rect, GrayValue = grayValue, Label = Label, Color = color, DisplayText = displayText });

            _drawActionTypes.Add(DrawActionType.AddRectangle);

            ReShowDrawALL();
        }

        public void RemoveRectangleByLabelText(string label)
        {
            for(int i=0; i< _drawUserRectangles.Count; i++)
            {
                if (_drawUserRectangles[i].Label == label)
                {
                    _drawUserRectangles.RemoveAt(i);
                    _drawActionTypes.RemoveAt(i);
                }
            }
        }

        // 添加用户矩形
        public void AddUserCircle(DrawCircleInfo circleInfo)
        {
            _drawUserCircles.Add(circleInfo);

            _drawActionTypes.Add(DrawActionType.AddCircle);

            ReShowDrawALL();
        }

        // 添加用户多边形，并计算面积
        public void AddUserPolygon(DrawPolygonInfo polyInfo, bool recordAction = true)
        {
            _drawUserPolygons.Add(polyInfo);

            if (recordAction)
                _drawActionTypes.Add(DrawActionType.AddPolygon);

            ReShowDrawALL();
        }

        // 添加用户线段-线段距离
        public void AddUserLs2Ls(DrawLs2LsInfo info)
        {
            string ls2LsId = $"{_drawLs2LsId++}.Ls2Ls";
            if (string.IsNullOrWhiteSpace(info.Label))
            {
                info.Label = ls2LsId;
            }
            else
            {
                info.Label = $"{info.Label}-{ls2LsId}";
            }
            _drawUserLs2Lses.Add(info);

            _drawActionTypes.Add(DrawActionType.AddLs2Ls);

            ReShowDrawALL();
        }

        // 添加用户箭头
        public void AddUserArrow(System.Drawing.Point head, System.Drawing.Point tail, string color = null, string displayText = null)
        {
            _drawUserArrows.Add(new DrawArrowInfo { Head = head, Tail = tail, Label = $"{_drawArrowId++}.Arrow", Color = color, DisplayText = displayText });

            _drawActionTypes.Add(DrawActionType.AddArrow);

            ReShowDrawALL();
        }

        // 添加测量线
        public void AddUserMeasureLine(System.Drawing.Point start, System.Drawing.Point end, double length, string Label = "", string color = null, string displayText = null)
        {
            _drawUserMeasureLines.Add(new DrawMeasureLineInfo() { Start = start, End = end, Length = length, Label = Label, Color = color, DisplayText = displayText });

            _drawActionTypes.Add(DrawActionType.AddMeasureLine);

            ReShowDrawALL();
        }

        /// <summary>
        /// 设置图窗交互过程中的临时预览。该预览只用于反馈，不参与撤销、保存或持久化绘制。
        /// </summary>
        public void SetTransientInteractionPreview(IEnumerable<DrawMeasureLineInfo> lines, IEnumerable<System.Drawing.Point> markers, string color = null)
        {
            _transientInteractionLines.Clear();
            _transientInteractionMarkers.Clear();

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line == null)
                    {
                        continue;
                    }

                    _transientInteractionLines.Add(new DrawMeasureLineInfo
                    {
                        Start = line.Start,
                        End = line.End,
                        Length = line.Length,
                        Label = line.Label
                    });
                }
            }

            if (markers != null)
            {
                _transientInteractionMarkers.AddRange(markers);
            }

            _transientInteractionColor = string.IsNullOrWhiteSpace(color) ? "cyan" : color;

            ReShowDrawALL();
        }

        /// <summary>
        /// 清除正在绘制的临时反馈，不影响已经完成的 ROI、标注和测量结果。
        /// </summary>
        public void ClearTransientInteractionPreview()
        {
            if (_transientInteractionLines.Count == 0 && _transientInteractionMarkers.Count == 0)
            {
                return;
            }

            _transientInteractionLines.Clear();
            _transientInteractionMarkers.Clear();
            _transientInteractionColor = "cyan";
            ReShowDrawALL();
        }

        public void UndoLastAction()
        {
            if (_drawActionTypes.Count == 0) return;

            var lastAction = _drawActionTypes[_drawActionTypes.Count - 1];
            _drawActionTypes.RemoveAt(_drawActionTypes.Count - 1);

            switch (lastAction)
            {
                case DrawActionType.AddRectangle:
                    if (_drawUserRectangles.Count > 0)
                    {
                        _drawUserRectangles.RemoveAt(_drawUserRectangles.Count - 1);
                    }
                    break;
                case DrawActionType.AddCircle:
                    if (_drawUserCircles.Count > 0)
                    {
                        _drawUserCircles.RemoveAt(_drawUserCircles.Count - 1);
                    }
                    break;
                case DrawActionType.AddMeasureLine:
                    if (_drawUserMeasureLines.Count > 0)
                    {
                        _drawUserMeasureLines.RemoveAt(_drawUserMeasureLines.Count - 1);
                    }
                    break;
                case DrawActionType.AddPolygon:
                    if (_drawUserPolygons.Count > 0)
                    {
                        _drawUserPolygons.RemoveAt(_drawUserPolygons.Count - 1);
                    }
                    break;
                case DrawActionType.AddLs2Ls:
                    if (_drawUserLs2Lses.Count > 0)
                    {
                        _drawUserLs2Lses.RemoveAt(_drawUserLs2Lses.Count - 1);
                    }
                    break;
                case DrawActionType.AddArrow:
                    if (_drawUserArrows.Count > 0)
                    {
                        _drawUserArrows.RemoveAt(_drawUserArrows.Count - 1);
                    }
                    break;
            }

            ReShowDrawALL();
        }

        // 只能同线程使用
        // 获取当前绘制状态
        public UserDrawState SaveCurrentUserDrawState()
        {
            return new UserDrawState(_drawObjectList, _drawUserMeasureLines, _drawUserRectangles, _drawUserCircles, _drawUserPolygons, _drawUserLs2Lses, _drawUserArrows, _drawActionTypes);
        }

        // 还原绘制状态
        public void RestoreUserDrawState(UserDrawState savedState)
        {
            // 清空当前内容
            _drawObjectList.Clear();
            _drawUserMeasureLines.Clear();
            _drawUserRectangles.Clear();
            _drawUserCircles.Clear();
            _drawUserPolygons.Clear();
            _drawUserLs2Lses.Clear();
            _drawUserArrows.Clear();
            _drawActionTypes.Clear();

            // 重新添加保存的状态
            _drawObjectList.AddRange(savedState.DrawObjectList);
            _drawUserMeasureLines.AddRange(savedState.MeasureLines);
            _drawUserRectangles.AddRange(savedState.UserRectangles);
            _drawUserCircles.AddRange(savedState.UserCircles);
            _drawUserPolygons.AddRange(savedState.UserPolygons);
            _drawUserLs2Lses.AddRange(savedState.UserLs2Lses);
            _drawUserArrows.AddRange(savedState.UserArrows);
            _drawActionTypes.AddRange(savedState.ActionTypes);

            _drawLs2LsId = _drawUserLs2Lses.Count();
            _drawArrowId = _drawUserArrows.Count();

            ReShowDrawALL(); // 重新绘制所有对象
        }

        // 注意、注意、注意：hObject的生命周期需要调用者自己维护
        // 添加绘制对象
        public void AddNewDrawObject(HObject hObject, string color, string drawMode, int font, Action<HTuple> drawAction, DrawTextInfo textInfo)
        {
            var drawObject = new DrawObjectInfo
            {
                HObject = hObject,
                Color = color,
                DrawMode = drawMode,
                Font = font,
                DrawAction = drawAction,
                TextInfo = textInfo,
            };
            _drawObjectList.Add(drawObject);
        }
        public void CleareDrawObjectList()
        {
            _drawObjectList.Clear();
        }

        // 注意、注意、注意：hObject的生命周期需要调用者自己维护
        public void AddDispObject(HObject hObject, string color, string drawMode, int font)
        {
            AddNewDrawObject(hObject, color, drawMode, font, null, null);
        }

        public void AddNewDrawFont(string text, string color, int row, int col, int fontSize = 0)
        {
            var drawObject = new DrawObjectInfo();
            drawObject.TextInfo = new DrawTextInfo
            {
                Text = text,
                Color = color,
                Row = row,
                Col = col,
                FontSize = fontSize
            };
            _drawObjectList.Add(drawObject);
        }

        // 添加绘制对象 -> DispCaltabObject
        public void AddDispCaltabObject(HTuple hv_PlateDescription, HTuple hv_StartParameters, HTuple hv_StartPose)
        {
            AddNewDrawObject(null, null, null, 0, (hWindow) =>
            {
                HOperatorSet.DispCaltab(hWindow, hv_PlateDescription, hv_StartParameters, hv_StartPose, 1);
            }, null);
        }
        // 清除显示，重新显示图，画所有应该画的对象
        public void ReShowDrawALL(uint lineNumber = 0)
        {
            // 显示图像
            HOperatorSet.ClearWindow(_hWindowControl.HalconWindow);

            if (_showingImage == null) return;

            GetImageSize(out int imgWidth, out int imgHeight);

            if (_displayStyle == DisplayStyle.One)
            {
                //测试缩放显示
                HOperatorSet.GetPart(_hWindowControl.HalconWindow, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                double dWidth = col2 - col1;
                double dHeight = row2 - row1;
                HOperatorSet.GetWindowExtents(_hWindowControl.HalconWindow, out HTuple row3, out HTuple col3, out HTuple width2, out HTuple height2);
                double rh = height2 / dHeight;
                double rw = width2 / dWidth;
                if (rw > 2) rw = 2;
                if (rw < _displayStyleOneMinScale)
                {
                    // 显示底图
                    _showingImage2?.Dispose();
                    HOperatorSet.MeanImage(_showingImage, out _showingImage2, 3, 3); // 均值滤波
                    HOperatorSet.DispObj(_showingImage2, _hWindowControl.HalconWindow);
                }
                else
                {
                    HOperatorSet.DispObj(_showingImage, _hWindowControl.HalconWindow);
                }
            }
            else
            {
                HOperatorSet.SetSystem("flush_graphic", "false");

                //测试缩放显示
                HOperatorSet.GetPart(_hWindowControl.HalconWindow, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                double dWidth = col2 - col1;
                double dHeight = row2 - row1;
                HOperatorSet.GetWindowExtents(_hWindowControl.HalconWindow, out HTuple row3, out HTuple col3, out HTuple width2, out HTuple height2);
                if (width2 < 10 || height2 < 10) return;
                double rh = height2 / dHeight;
                double rw = width2 / dWidth;
                if (rw > 2) rw = 2;
                _showingImage2?.Dispose();
                HOperatorSet.ZoomImageFactor(_showingImage, out _showingImage2, rw, rw, "weighted");
                //if (rw < 0.8)
                //{
                //    _showingImage3?.Dispose();
                //    HOperatorSet.MeanImage(_showingImage2, out _showingImage3, 3, 3); // 均值滤波
                //    //HOperatorSet.MedianImage(_showingImage2, out _showingImage3, "userCircle", 2, "mirrored"); // 中值滤波

                //    HOperatorSet.SetPart(_hWindowControl.HalconWindow, row1 * rw, col1 * rw, row2 * rw, col2 * rw);

                //    _showingImage2?.Dispose();
                //    HOperatorSet.DispObj(_showingImage3, _hWindowControl.HalconWindow);

                //    HOperatorSet.SetPart(_hWindowControl.HalconWindow, row1, col1, row2, col2);

                //    HOperatorSet.SetSystem("flush_graphic", "true");
                //}
                //else
                {

                    HOperatorSet.SetPart(_hWindowControl.HalconWindow, row1 * rw, col1 * rw, row2 * rw, col2 * rw);

                    HOperatorSet.DispObj(_showingImage2, _hWindowControl.HalconWindow);

                    HOperatorSet.SetPart(_hWindowControl.HalconWindow, row1, col1, row2, col2);

                    HOperatorSet.SetSystem("flush_graphic", "true");
                }
            }

            // 默认显示方式、线条宽度、字体大小
            HOperatorSet.SetDraw(_hWindowControl.HalconWindow, "margin");
            HOperatorSet.SetLineWidth(_hWindowControl.HalconWindow, _drawLineWidth);
            int fontSize = (int)(_fontMeasureSize * GetScaleFactor());
            HalconHelper.SetHalconFont(_hWindowControl.HalconWindow, fontSize);

            // 绘制最小圆
            if (_circumCircle != null)
            {
                HOperatorSet.SetColor(_hWindowControl.HalconWindow, "red");
                HOperatorSet.DispObj(_circumCircle, _hWindowControl.HalconWindow);
                DrawCrossAtCircleCenter(_circumCircle, "red");
                DisplayCircleInfo();
            }

            // 绘制找圆
            if (IsFindCircle)
            {
                try
                {
                    double magnification = 1;
                    double pixelsize = 1;
                    HTuple centRow = new HTuple(), centCol = new HTuple();
                    HTuple centRow2 = new HTuple(), centCol2 = new HTuple(), radius = new HTuple();
                    HObject ho_Region;
                    (centRow, centCol, centRow2, centCol2, radius, ho_Region) = HalconHelper.GetCirclePoint(_showingImage, FindCircleGray);

                    HOperatorSet.DispObj(ho_Region, _hWindowControl.HalconWindow);
                    ho_Region.Dispose();

                    double radMax = (double)0.0;
                    HOperatorSet.TupleLength(centRow, out HTuple length);
                    for (int i = 0; i < length; i++)
                    {
                        HOperatorSet.TupleSelect(centRow, i, out HTuple row);
                        HOperatorSet.TupleSelect(centCol, i, out HTuple col);
                        HOperatorSet.TupleSelect(radius, i, out HTuple hRad);

                        //生成亚像素圆
                        HOperatorSet.GenCircleContourXld(out HObject ContCircle, row, col, 2.5, 0, 6.28318, "positive", 1);
                        HOperatorSet.GenCrossContourXld(out HObject Cross, row, col, 7, 0);
                        HOperatorSet.DispObj(ContCircle, _hWindowControl.HalconWindow);
                        HOperatorSet.DispObj(Cross, _hWindowControl.HalconWindow);

                        double rx = row - (imgHeight - 1) / 2f;
                        double cx = col - (imgWidth - 1) / 2f;
                        double rad = (hRad.D * pixelsize) / magnification;//计算实际半径
                        HOperatorSet.DispText(_hWindowControl.HalconWindow, cx.ToString("f2") + "，" + rx.ToString("f2") + "，" + rad.ToString("f3"), "image", row, col + 10, "red", "box", "false");

                        if (rad > radMax)
                        {
                            radMax = rad;
                        }
                        _findCircleCenterOffsetX = rx;
                        _findCircleCenterOffsetY = cx;
                        _findCircleRadius = rad;
                    }

                    _findCircleMaxRadius = radMax;
                }
                catch (Exception) { }
            }

            if (_showingShapeModelS != null && _showingShapeModelL != null)
            {
                HOperatorSet.SetLineWidth(_hWindowControl.HalconWindow, 3);
                HOperatorSet.SetDraw(_hWindowControl.HalconWindow, "margin");
                HOperatorSet.SetColor(_hWindowControl.HalconWindow, "blue");
                HOperatorSet.DispObj(_showingShapeModelS, _hWindowControl.HalconWindow);
                HOperatorSet.SetColor(_hWindowControl.HalconWindow, "red");
                HOperatorSet.DispObj(_showingShapeModelL, _hWindowControl.HalconWindow);
            }

            // 绘制中心十字虚线（如果开关标志为 true）
            if (DrawCenterCross)
            {
                GetImageSize(out int width, out int height);
                int centerX = width / 2;
                int centerY = height / 2;

                // 设置颜色和线型
                HOperatorSet.SetColor(_hWindowControl.HalconWindow, "green");
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple(5, 5)); // 5像素的虚线

                // 绘制水平线
                HOperatorSet.DispLine(_hWindowControl.HalconWindow, centerY, 0, centerY, width);

                // 绘制垂直线
                HOperatorSet.DispLine(_hWindowControl.HalconWindow, 0, centerX, height, centerX);

                // 恢复默认线型
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple());
            }

            // 绘用户画的外形、对象、测量，传入 HWindow
            DrawUser(_hWindowControl.HalconWindow, lineNumber, imgWidth, _drawLineWidth, fontSize);

            ReShowDrawingALL?.Invoke(_hWindowControl, _showingImage);
        }

        private bool _showLabel = true;
        public void ShowRectangleLabel(bool show = true)
        {
            _showLabel = show;
        }
        // 绘用户画的外形、对象、测量，传入 HWindow
        private void DrawUser(HTuple hWindow, uint lineNumber, int imageSize, int lineWidth, int fontSize)
        {
            // 选择显眼的颜色
            HOperatorSet.SetColor(hWindow, "magenta");
            HOperatorSet.SetDraw(hWindow, "margin");
            HalconHelper.SetHalconFont(hWindow, fontSize);
            HOperatorSet.SetLineWidth(hWindow, lineWidth);

            // 绘制用户矩形
            foreach (var rectangleInfo in _drawUserRectangles)
            {
                var overlayColor = string.IsNullOrWhiteSpace(rectangleInfo.Color) ? "magenta" : rectangleInfo.Color;
                HOperatorSet.SetColor(hWindow, overlayColor);
                // 绘制用户矩形
                var rect = rectangleInfo.Rectangle;

                var width = CalcActualSizeMM(rectangleInfo.Rectangle.Width, lineNumber);
                var height = CalcActualSizeMM(rectangleInfo.Rectangle.Height, lineNumber);
                var area = width * height;
                var centerX = rectangleInfo.Rectangle.Left;
                var centerY = rectangleInfo.Rectangle.Bottom;

                string snrText = "";
                if (ShowSNR && _origImage != null)
                {
                    try
                    {
                        HalconHelper.ConvertRectangleToRegion(rect, out HObject region);
                        HOperatorSet.Intensity(region, _origImage, out HTuple mean, out HTuple dev);
                        double snr = dev.D > 0 ? 20 * Math.Log10(mean.D / dev.D) : 0;
                        snrText = $"\nSNR={snr:F2} dB";
                        region.Dispose();
                    }
                    catch { }
                }

                // 在矩形中央显示面积
                // 同时绘制实际的像素尺寸(针对增强图和结果图)
                if (_showLabel)
                {
                    HOperatorSet.DispRectangle1(hWindow, rect.Top, rect.Left, rect.Bottom, rect.Right);
                    // 没有自定义显示文字时完全沿用旧的尺寸、灰度、SNR 显示，确保既有项目不变。
                    var displayText = rectangleInfo.DisplayText ?? $"{rectangleInfo.Label}{width:F3}*{height:F3}={area:F3} mm²\nGray={rectangleInfo.GrayValue:F3}{snrText}";
                    if (!string.IsNullOrWhiteSpace(displayText))
                    {
                        HOperatorSet.DispText(hWindow, displayText, "image", centerY + DarwTextSetp, centerX + DarwTextSetp, overlayColor, "box", "false");
                    }
                }
                else
                {
                    HOperatorSet.SetColor(hWindow, "cyan");
                    HOperatorSet.SetLineWidth(hWindow, 2);
                    HOperatorSet.DispRectangle1(hWindow, rect.Top, rect.Left, rect.Bottom, rect.Right);
                    HOperatorSet.DispText(hWindow, $"{rectangleInfo.Label}", "image", rect.Top + 5, rect.Left + 5, "black", new HTuple(), new HTuple());
                }
                //HOperatorSet.DispText(hWindow, $"{rectangleInfo.Rectangle.Width}*{rectangleInfo.Rectangle.Height}={rectangleInfo.Rectangle.Width * rectangleInfo.Rectangle.Height} pixel²", "image", centerY + 25, centerX, "green", "box", "false");
            }

            // 绘制用户圆
            foreach (var userCircle in _drawUserCircles)
            {
                var overlayColor = string.IsNullOrWhiteSpace(userCircle.Color) ? "magenta" : userCircle.Color;
                HOperatorSet.SetColor(hWindow, overlayColor);
                // 绘制用户圆
                HOperatorSet.DispCircle(hWindow, userCircle.Center.Y, userCircle.Center.X, userCircle.Radius);

                var radius = CalcActualSizeMM(userCircle.Radius, lineNumber);
                var area = Math.PI * radius * radius;
                var centerX = userCircle.Center.X - userCircle.Radius;
                var centerY = userCircle.Center.Y + userCircle.Radius;
                // 在圆中央显示面积；自定义空文字用于仅绘制点/圆几何。
                var displayText = userCircle.DisplayText ?? $"π*{radius:F3}²={area:F3}mm²\nGray={userCircle.GrayValue:F3}";
                if (!string.IsNullOrWhiteSpace(displayText))
                {
                    HOperatorSet.DispText(hWindow, displayText, "image", centerY + DarwTextSetp, centerX + DarwTextSetp, overlayColor, "box", "false");
                }
            }

            // 绘制用户多边形
            foreach (var polygon in _drawUserPolygons)
            {
                var overlayColor = string.IsNullOrWhiteSpace(polygon.Color) ? "magenta" : polygon.Color;
                var rows = new HTuple(polygon.Points.Select(p => (HTuple)p.Y).ToArray());
                var cols = new HTuple(polygon.Points.Select(p => (HTuple)p.X).ToArray());
                HOperatorSet.GenRegionPolygonFilled(out HObject region, rows, cols);

                // 根据选中状态设置颜色和线宽
                if (polygon.IsSelected)
                {
                    HOperatorSet.SetColor(hWindow, "yellow");  // 选中状态用黄色
                    HOperatorSet.SetLineWidth(hWindow, lineWidth * 2);

                    // 绘制所有顶点（放大显示，方便点击）- 保持填充模式只用于顶点
                    for (int i = 0; i < polygon.Points.Count; i++)
                    {
                        var point = polygon.Points[i];

                        if (polygon.IsDragging && i == polygon.EditingPointIndex)
                        {
                            // 正在拖动的点用红色大圆点（更大，方便看到）
                            HOperatorSet.SetColor(hWindow, "red");
                            HOperatorSet.DispCircle(hWindow, point.Y, point.X, 8);
                        }
                        else
                        {
                            // 普通顶点用青色大圆点
                            HOperatorSet.SetColor(hWindow, "cyan");
                            HOperatorSet.DispCircle(hWindow, point.Y, point.X, 8);

                            // 可选：在顶点旁边显示序号（方便知道是哪个点）
                            // HOperatorSet.DispText(hWindow, i.ToString(), "image", point.Y - 15, point.X - 15, "cyan", "box", "false");
                        }
                    }
                }
                else
                {
                    HOperatorSet.SetColor(hWindow, overlayColor);
                    HOperatorSet.SetLineWidth(hWindow, lineWidth);
                }

                // 绘制多边形
                HOperatorSet.DispRegion(region, hWindow);

                // 在多边形中心显示面积
                HOperatorSet.AreaCenter(region, out HTuple hv_Area, out HTuple rowC, out HTuple colC);
                //HOperatorSet.SetTposition(hWindow, rowC.D, colC.D);

                var unitArea = CalcActualSizeMM(1, lineNumber);
                var area = unitArea * unitArea * hv_Area.D;

                // 文本颜色根据选中状态变化
                string textColor = polygon.IsSelected ? "yellow" : overlayColor;
                var displayText = polygon.DisplayText ?? $"{polygon.Label}{area:F3} mm²\nGray={polygon.GrayValue:F3}";
                if (!string.IsNullOrWhiteSpace(displayText))
                {
                    HOperatorSet.DispText(hWindow, displayText,
                        "image", rowC + DarwTextSetp, colC + DarwTextSetp, textColor, "box", "false");
                }

                region.Dispose();
            }

            // 绘制“线段对线段距离”
            foreach (var userLs2Ls in _drawUserLs2Lses)
            {
                // 画两条实线段
                HOperatorSet.DispLine(hWindow, userLs2Ls.L1Start.Y, userLs2Ls.L1Start.X, userLs2Ls.L1End.Y, userLs2Ls.L1End.X);
                HOperatorSet.DispLine(hWindow, userLs2Ls.L2Start.Y, userLs2Ls.L2Start.X, userLs2Ls.L2End.Y, userLs2Ls.L2End.X);

                // 画最短连线：虚线
                HOperatorSet.SetLineStyle(hWindow, new HTuple(6, 4));
                HOperatorSet.DispLine(hWindow, userLs2Ls.ClosestOnL1.Y, userLs2Ls.ClosestOnL1.X, userLs2Ls.ClosestOnL2.Y, userLs2Ls.ClosestOnL2.X);
                HOperatorSet.SetLineStyle(hWindow, new HTuple()); // 还原

                // 文本：放在连线中点附近
                var midX = (userLs2Ls.ClosestOnL1.X + userLs2Ls.ClosestOnL2.X) / 2.0;
                var midY = (userLs2Ls.ClosestOnL1.Y + userLs2Ls.ClosestOnL2.Y) / 2.0;

                var distMM = CalcActualSizeMM(userLs2Ls.DistancePixel, lineNumber);
                string text = $"{userLs2Ls.Label}\n距离={distMM.ToString(DrawMeasurePrecision)} mm\n角度={userLs2Ls.AngleDeg.ToString(DrawMeasurePrecision)}°";
                HOperatorSet.DispText(hWindow, text, "image", midY + DarwTextSetp, midX + DarwTextSetp, "magenta", "box", "false");
            }

            // ---- 绘制用户箭头 ----
            foreach (var a in _drawUserArrows)
            {
                var overlayColor = string.IsNullOrWhiteSpace(a.Color) ? "magenta" : a.Color;
                HOperatorSet.SetColor(hWindow, overlayColor);
                // 主干
                HOperatorSet.DispLine(hWindow, a.Tail.Y, a.Tail.X, a.Head.Y, a.Head.X);

                // 箭翼参数（随缩放变化）
                double scale = GetScaleFactor(); // 已有函数
                double headLen = Math.Max(12, 22 * scale);    // 头长
                double headAng = 25.0 * Math.PI / 180.0;      // 翼角

                double dx = a.Tail.X - a.Head.X;
                double dy = a.Tail.Y - a.Head.Y;
                double L = Math.Sqrt(dx * dx + dy * dy);
                if (L < 1e-6) continue;

                double ux = dx / L, uy = dy / L; // 指向尾巴的单位向量（从头到尾）

                // 两个翼的方向（绕 head 旋转 ±headAng）
                double cosA = Math.Cos(headAng), sinA = Math.Sin(headAng);
                // 旋转(+)
                double vx1 = ux * cosA - uy * sinA;
                double vy1 = ux * sinA + uy * cosA;
                // 旋转(-)
                double vx2 = ux * cosA + uy * sinA;
                double vy2 = -ux * sinA + uy * cosA;

                // 翼端点
                var w1 = new System.Drawing.Point((int)Math.Round(a.Head.X + vx1 * headLen), (int)Math.Round(a.Head.Y + vy1 * headLen));
                var w2 = new System.Drawing.Point((int)Math.Round(a.Head.X + vx2 * headLen), (int)Math.Round(a.Head.Y + vy2 * headLen));

                // 画两条翼
                HOperatorSet.DispLine(hWindow, a.Head.Y, a.Head.X, w1.Y, w1.X);
                HOperatorSet.DispLine(hWindow, a.Head.Y, a.Head.X, w2.Y, w2.X);

                // 文本（箭头尖右侧）；为空时箭头只表达方向，避免显示内部编号。
                var displayText = a.DisplayText ?? a.Label;
                if (!string.IsNullOrWhiteSpace(displayText))
                {
                    HOperatorSet.DispText(hWindow, displayText, "image", a.Head.Y + DarwTextSetp, a.Head.X + DarwTextSetp, overlayColor, "box", "false");
                }
            }

            // 绘制所有测量线
            foreach (var line in _drawUserMeasureLines)
            {
                var overlayColor = string.IsNullOrWhiteSpace(line.Color) ? "magenta" : line.Color;
                HOperatorSet.SetColor(hWindow, overlayColor);
                HOperatorSet.SetLineWidth(hWindow, _drawLineWidth * 2);

                // 绘制测量线
                HOperatorSet.DispLine(hWindow, line.Start.Y, line.Start.X, line.End.Y, line.End.X);

                // 计算箭头方向和长度
                double arrowLength = imageSize / 200; // 箭头的长度
                double angle = Math.Atan2(line.End.Y - line.Start.Y, line.End.X - line.Start.X);

                // 绘制起点箭头
                double arrowX1 = line.Start.X + arrowLength * Math.Cos(angle - Math.PI / 6);
                double arrowY1 = line.Start.Y + arrowLength * Math.Sin(angle - Math.PI / 6);
                double arrowX2 = line.Start.X + arrowLength * Math.Cos(angle + Math.PI / 6);
                double arrowY2 = line.Start.Y + arrowLength * Math.Sin(angle + Math.PI / 6);
                HOperatorSet.DispLine(hWindow, line.Start.Y, line.Start.X, arrowY1, arrowX1);
                HOperatorSet.DispLine(hWindow, line.Start.Y, line.Start.X, arrowY2, arrowX2);

                // 绘制终点箭头
                arrowX1 = line.End.X - arrowLength * Math.Cos(angle - Math.PI / 6);
                arrowY1 = line.End.Y - arrowLength * Math.Sin(angle - Math.PI / 6);
                arrowX2 = line.End.X - arrowLength * Math.Cos(angle + Math.PI / 6);
                arrowY2 = line.End.Y - arrowLength * Math.Sin(angle + Math.PI / 6);
                HOperatorSet.DispLine(hWindow, line.End.Y, line.End.X, arrowY1, arrowX1);
                HOperatorSet.DispLine(hWindow, line.End.Y, line.End.X, arrowY2, arrowX2);

                // 显示长度在终点
                HOperatorSet.SetTposition(hWindow, line.End.Y + 5, line.End.X + 5);
                // 旧调用仍自动补充毫米长度；新调用可提供完整业务文字，避免重复或泄漏内部 ID。
                HOperatorSet.WriteString(hWindow, line.DisplayText ?? $"{line.Label}{CalcActualSizeMM(line.Length, lineNumber):F3} mm");
            }

            // 预览层使用高对比虚线和十字，不与最终结果混淆。
            if (_transientInteractionLines.Count > 0 || _transientInteractionMarkers.Count > 0)
            {
                HOperatorSet.SetColor(hWindow, _transientInteractionColor);
                HOperatorSet.SetLineWidth(hWindow, Math.Max(2, _drawLineWidth * 2));
                HOperatorSet.SetLineStyle(hWindow, new HTuple(8, 6));

                foreach (var marker in _transientInteractionMarkers)
                {
                    HOperatorSet.DispCross(hWindow, marker.Y, marker.X, 12, 0);
                }

                foreach (var line in _transientInteractionLines)
                {
                    HOperatorSet.DispLine(hWindow, line.Start.Y, line.Start.X, line.End.Y, line.End.X);
                    if (!string.IsNullOrWhiteSpace(line.Label))
                    {
                        HOperatorSet.DispText(hWindow, line.Label, "image", line.End.Y + DarwTextSetp, line.End.X + DarwTextSetp, _transientInteractionColor, "box", "false");
                    }
                }

                HOperatorSet.SetLineStyle(hWindow, new HTuple());
            }

            // 绘制对象列表中的对象
            for (int i = 0; i < _drawObjectList.Count; i++)
            {
                var drawObject = _drawObjectList[i];
                if (drawObject.LineWidth > 0)
                {
                    HOperatorSet.SetLineWidth(hWindow, _drawLineWidth);
                }

                if (!string.IsNullOrEmpty(drawObject.Color))
                {
                    HOperatorSet.SetColor(hWindow, drawObject.Color);
                }

                if (!string.IsNullOrEmpty(drawObject.DrawMode))
                {
                    HOperatorSet.SetDraw(hWindow, drawObject.DrawMode);
                }

                if (drawObject.Font > 0)
                {
                    int fontSize2 = (int)(drawObject.Font * GetScaleFactor());
                    HalconHelper.SetHalconFont(hWindow, fontSize2);
                }

                if (drawObject.DrawAction == null)
                {
                    if (drawObject.HObject != null)
                    {
                        HOperatorSet.DispObj(drawObject.HObject, hWindow);
                    }
                }
                else
                {
                    drawObject.DrawAction.Invoke(hWindow);
                }

                if (drawObject.TextInfo != null && string.IsNullOrEmpty(drawObject.TextInfo.Text) == false && string.IsNullOrEmpty(drawObject.TextInfo.Color) == false)
                {
                    int originalFontSize = fontSize; // 保存当前全局字体大小
                    if (drawObject.TextInfo.FontSize > 0)
                    {
                        HalconHelper.SetHalconFont(hWindow, drawObject.TextInfo.FontSize);
                    }
                    HOperatorSet.DispText(hWindow, drawObject.TextInfo.Text, "image",
                        drawObject.TextInfo.Row + DarwTextSetp, drawObject.TextInfo.Col + DarwTextSetp,
                        drawObject.TextInfo.Color, "box", "false");
                    if (drawObject.TextInfo.FontSize > 0)
                    {
                        HalconHelper.SetHalconFont(hWindow, originalFontSize); // 恢复全局字体
                    }
                }
            }
        }
        public bool UseNaviCamPixel2Mm = false;
        public bool UsePosiCamPixel2Mm = false;
        public double NaviCamPixel2Mm = 0.001;
        public double PosiCamPixel2Mm = 0.001;
        public double CalcActualSizeMM(double pixel, uint lineNumber = 0)
        {
            var line = (lineNumber != _lineNumber && lineNumber == 0) ? _lineNumber : lineNumber;
            var fodFddRatio = _fodFddRatios[line - MachineProfile.LineStart];
            var r = pixel * _unitPixelActualSize / fodFddRatio;

            if (UseNaviCamPixel2Mm) r = pixel * NaviCamPixel2Mm;
            if (UsePosiCamPixel2Mm) r = pixel * PosiCamPixel2Mm;

            return r;
        }

        public void GetImageSize(out int width, out int height)
        {
            HOperatorSet.GetImageSize(_showingImage, out HTuple hWidth, out HTuple hHeight);
            width = hWidth.I;
            height = hHeight.I;
        }

        // 取平均灰度值在圆
        public double GetGrayForCircle(Point center, double radius)
        {
            if (_origImage == null) return 0;
            try
            {
                HOperatorSet.GenCircle(out HObject ho_CircleRegion, center.Y, center.X, radius);

                // 计算圆形区域内的平均强度和标准差
                HOperatorSet.Intensity(ho_CircleRegion, _origImage, out HTuple hv_Mean, out HTuple hv_Deviation);
                ho_CircleRegion.Dispose();

                return hv_Mean.D;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // 取平均灰度值在矩形
        public double GetGrayForRectangle(Rectangle rectangle)
        {
            if (_origImage == null) return 0;
            try
            {
                // 创建感兴趣区域（例如：矩形）
                HalconHelper.ConvertRectangleToRegion(rectangle, out HObject ho_rectRegions);

                // 计算区域内的平均强度和标准差
                HOperatorSet.Intensity(ho_rectRegions, _origImage, out HTuple hv_Mean, out HTuple hv_Deviation);
                ho_rectRegions.Dispose();

                return hv_Mean.D;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // 取平均灰度值在多边形内
        public double GetGrayForPolygon(System.Drawing.Point[] points)
        {
            if (_origImage == null) return 0;
            // 如果有加载的图像，可以同矩形、圆形逻辑一样获取图像对象
            // 这里仅示例：用 Halcon 生成多边形区域并计算面积
            try
            {
                // 生成多边形区域
                var rows = new HTuple(points.Select(p => (HTuple)p.Y).ToArray());
                var cols = new HTuple(points.Select(p => (HTuple)p.X).ToArray());
                HOperatorSet.GenRegionPolygonFilled(out HObject polyRegion, rows, cols);

                // 计算面积
                HOperatorSet.Intensity(polyRegion, _origImage, out HTuple hv_Mean, out HTuple hv_Deviation);
                polyRegion.Dispose();

                return hv_Mean.D;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // 绘制圆心十字线
        public void DrawCrossAtCircleCenter(HObject circle, string color)
        {
            // 获取圆心坐标和半径
            HTuple area = 0; HTuple row = 0; HTuple column = 0; HTuple radius = 0;
            bool bIsXld = false;
            try
            {
                //HOperatorSet.AreaCenter(circle, out area, out row, out column);
                HOperatorSet.SmallestCircle(circle, out row, out column, out radius);
            }
            catch { bIsXld = true; }
            if (bIsXld)
            {
                HOperatorSet.SmallestCircleXld(circle, out var rowT, out var columnT, out var radiusT);
                if (radiusT.Length == 1)
                {
                    row = rowT;
                    column = columnT;
                    radius = radiusT;
                }
            }

            if (radius.Length > 0)
            {

                // 设置颜色和线型
                HOperatorSet.SetColor(_hWindowControl.HalconWindow, color);
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple(5, 5)); // 5像素的虚线

                // 计算十字线长度为半径的三分之一
                double crossLength = radius.D / 3.0;

                // 绘制水平线
                HOperatorSet.DispLine(_hWindowControl.HalconWindow, row, column - crossLength, row, column + crossLength);

                // 绘制垂直线
                HOperatorSet.DispLine(_hWindowControl.HalconWindow, row - crossLength, column, row + crossLength, column);

                // 恢复默认线型
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple());
            }
        }

        // 显示圆心偏移和半径信息
        private void DisplayCircleInfo()
        {
            // 获取圆心坐标

            HTuple area = 0; HTuple row = 0; HTuple column = 0; HTuple radius = 0;
            bool bIsXld = false;
            try
            {
                HOperatorSet.AreaCenter(_circumCircle, out area, out row, out column);
            }
            catch { bIsXld = true; }
            if (bIsXld)
            {
                HOperatorSet.AreaCenterXld(_circumCircle, out _, out var rowT, out var columnT, out _);
                if (rowT.Length == 1)
                {
                    row = rowT;
                    column = columnT;
                }
            }

            if (row.Length > 0)
            {
                string info = $"X: {_circumCircleCenterOffsetX:F2}, Y: {_circumCircleCenterOffsetY:F2}, R: {_circumCircleRadius:F2}";

                // 设置显示文字的位置为圆心的右下方
                HOperatorSet.SetTposition(_hWindowControl.HalconWindow, row + 20, column + 20);

                // 设置字体大小为14
                HalconHelper.SetHalconFont(_hWindowControl.HalconWindow, 14);

                // 显示信息
                HOperatorSet.WriteString(_hWindowControl.HalconWindow, info);
            }
        }

        // 绘制矩形
        public void DrawRectangle(Rectangle rect, string color = "magenta", bool dashed = false)
        {
            HOperatorSet.SetColor(_hWindowControl.HalconWindow, color);
            HOperatorSet.SetDraw(_hWindowControl.HalconWindow, IsFilled ? "fill" : "margin"); // 根据标志设置绘制模式
            if (dashed)
            {
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple(8, 6));
            }
            HOperatorSet.DispRectangle1(_hWindowControl.HalconWindow, rect.Top, rect.Left, rect.Bottom, rect.Right);
            if (dashed)
            {
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple());
            }
        }

        // 创建区域适应矩形
        public void CreateAreaFitRectangle(Rectangle rect, string tip)
        {
            HOperatorSet.DispRectangle1(_hWindowControl.HalconWindow, rect.Top, rect.Left, rect.Bottom, rect.Right);
            HOperatorSet.DispText(_hWindowControl.HalconWindow, tip, "image", rect.Bottom, rect.Left, "green", "box", "false");
            // HOperatorSet.WriteString(_hWindowControl.HalconWindow, tip);
        }

        // 绘制圆形
        public void DrawCircle(System.Drawing.Point start, int radius, string color = "magenta", bool dashed = false)
        {
            HOperatorSet.SetColor(_hWindowControl.HalconWindow, color);
            HOperatorSet.SetDraw(_hWindowControl.HalconWindow, IsFilled ? "fill" : "margin"); // 根据标志设置绘制模式
            if (dashed)
            {
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple(8, 6));
            }
            HOperatorSet.DispCircle(_hWindowControl.HalconWindow, start.Y, start.X, radius);
            if (dashed)
            {
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple());
            }
        }

        // 绘制测量线
        public void DrawLine(System.Drawing.Point start, System.Drawing.Point end, string color = "magenta", bool dashed = false)
        {
            HOperatorSet.SetColor(_hWindowControl.HalconWindow, color);
            HOperatorSet.SetLineWidth(_hWindowControl.HalconWindow, _drawLineWidth);
            if (dashed)
            {
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple(8, 6));
            }
            HOperatorSet.DispLine(_hWindowControl.HalconWindow, start.Y, start.X, end.Y, end.X);
            if (dashed)
            {
                HOperatorSet.SetLineStyle(_hWindowControl.HalconWindow, new HTuple());
            }
        }

        // 计算自动窗宽窗位
        public bool CalcAutoWLWW(Rectangle currentRectangle, out int wl, out int ww)
        {
            wl = 0;
            ww = 0;
            if (_origImage == null) return false;
            _hWindowControl.Focus();

            try
            {
                _hWindowControl.HalconWindow.SetDraw("margin");
                double column_1 = currentRectangle.X;
                double row_1 = currentRectangle.Y;
                double column_2 = currentRectangle.X + currentRectangle.Width;
                double row_2 = currentRectangle.Y + currentRectangle.Height;
                //hw_ctrl.HalconWindow.DrawRectangle1(out double row_1, out double column_1, out double row_2, out double column_2);
                HOperatorSet.GenRectangle1(out HObject RectOut, row_1, column_1, row_2, column_2);
                //HOperatorSet.DispObj(RectOut, _hWindowControl.HalconWindow);

                //灰度
                HOperatorSet.Intensity(RectOut, _origImage, out HTuple mean, out HTuple deviation);
                HOperatorSet.MinMaxGray(RectOut, _origImage, 1, out HTuple mingray, out HTuple maxgray, out HTuple rangegray);
                wl = (int)mean.D;
                int min1 = (int)(mean - mingray).D;
                int max1 = (int)(maxgray - mean).D;
                ww = Math.Max(min1, max1);

                RectOut.Dispose();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 一比一显示图像
        public void DisplayOriginalSizeImage()
        {
            if (_showingImage == null) return;

            try
            {
                // 获取图像尺寸
                GetImageSize(out int imgWidth, out int imgHeight);

                // 获取窗口的实际尺寸
                int winWidth = _hWindowControl.Size.Width;
                int winHeight = _hWindowControl.Size.Height;

                _currentScale = 1.0f;

                if (imgWidth > winWidth || imgHeight > winHeight)
                {
                    // 图像尺寸大于窗口尺寸，显示图像中心部分
                    double rowOffset = (imgHeight - winHeight) / 2.0;
                    double colOffset = (imgWidth - winWidth) / 2.0;
                    HOperatorSet.SetPart(_hWindowControl.HalconWindow, rowOffset - 1, colOffset - 1, rowOffset + winHeight - 1, colOffset + winWidth - 1);
                }
                else
                {
                    // 图像尺寸小于窗口尺寸，按比例缩放图像
                    double rowOffset = (winHeight - imgHeight) / 2.0;
                    double colOffset = (winWidth - imgWidth) / 2.0;
                    HOperatorSet.SetPart(_hWindowControl.HalconWindow, -rowOffset - 1, -colOffset - 1, rowOffset + imgHeight - 1, colOffset + imgWidth - 1);
                }

                CalculateScaleFactor();

                ReShowDrawALL();
            }
            catch (Exception)
            {
                // 捕捉并记录异常
                // LogHelper.Error(ex.Message);
            }
        }

        private double _currentScale = 1.0f;
        public double CurrentScale => _currentScale;

        public bool SetCurScale(double currentScale = 0.7)
        {
            if (currentScale <= 0)
            {
                return false;
            }
            _currentScale = currentScale;
            return true;
        }

        // 自适应窗体大小显示图像
        public void DisplayAdaptiveSizeImage()
        {
            if (_showingImage == null) return;

            try
            {
                // 获取图像尺寸
                GetImageSize(out int imgWidth, out int imgHeight);

                // 获取窗口尺寸
                int winWidth = _hWindowControl.Size.Width;
                int winHeight = _hWindowControl.Size.Height;

                // 计算宽高比例
                double wRatio = 1.0 * imgWidth / winWidth;
                double hRatio = 1.0 * imgHeight / winHeight;
                double ratio = Math.Max(wRatio, hRatio);

                // ⭐ 关键：记录当前比例
                _currentScale = 1.0 / ratio;

                // 计算适应窗口的显示区域
                double column1 = -(ratio * winWidth - imgWidth) / 2.0;
                double row1 = -(ratio * winHeight - imgHeight) / 2.0;
                double column2 = (ratio * winWidth - imgWidth) / 2.0 + imgWidth;
                double row2 = (ratio * winHeight - imgHeight) / 2.0 + imgHeight;

                // 设置窗口显示的图像区域
                HOperatorSet.SetPart(_hWindowControl.HalconWindow, row1 - 1, column1 - 1, row2 - 1, column2 - 1);

                CalculateScaleFactor();

                ReShowDrawALL();
            }
            catch (Exception)
            {
                // 捕捉并记录异常
                // LogHelper.Error(ex.Message);
            }
        }

        /// <summary>
        /// 按比例顯示（scale=1 一比一；scale>1 放大；scale<1 縮小）
        /// </summary>
        public void DisplayScaledImage(double scale)
        {
            if (_showingImage == null) return;

            // 防呆
            if (double.IsNaN(scale) || double.IsInfinity(scale)) return;
            if (scale < 0.05) scale = 0.05;
            if (scale > 20.0) scale = 20.0;

            _currentScale = scale;

            try
            {
                GetImageSize(out int imgWidth, out int imgHeight);

                int winWidth = _hWindowControl.Size.Width;
                int winHeight = _hWindowControl.Size.Height;
                if (winWidth <= 1 || winHeight <= 1) return;

                // 以图像中心为中心缩放
                double centerRow = (imgHeight - 1) / 2.0;
                double centerCol = (imgWidth - 1) / 2.0;

                // 映射关系：window 像素对应 image 的 1/scale 像素
                double partHeight = winHeight / scale;
                double partWidth = winWidth / scale;

                double row1 = centerRow - partHeight / 2.0;
                double row2 = centerRow + partHeight / 2.0;
                double col1 = centerCol - partWidth / 2.0;
                double col2 = centerCol + partWidth / 2.0;

                HOperatorSet.SetPart(_hWindowControl.HalconWindow, row1, col1, row2, col2);

                CalculateScaleFactor();
                ReShowDrawALL();
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// 弹窗输入比例后显示（默认 0.7）
        /// </summary>
        public void DisplayScaledImageByDialog(double defaultScale = 0.7)
        {
            if (_showingImage == null) return;

            try
            {
                double? scale = PromptDouble("輸入顯示比例（例如 0.7）", "按比例顯示", defaultScale);
                if (scale.HasValue)
                {
                    DisplayScaledImage(scale.Value);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static double? PromptDouble(string tip, string title, double defaultValue)
        {
            using (var form = new Form())
            using (var lbl = new Label())
            using (var tb = new TextBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.Width = 360;
                form.Height = 150;

                lbl.AutoSize = true;
                lbl.Left = 12;
                lbl.Top = 15;
                lbl.Text = tip;

                tb.Left = 12;
                tb.Top = 40;
                tb.Width = 320;
                tb.Text = defaultValue.ToString("0.###", CultureInfo.InvariantCulture);

                ok.Text = "OK";
                ok.Left = 172;
                ok.Top = 75;
                ok.Width = 75;
                ok.DialogResult = DialogResult.OK;

                cancel.Text = "Cancel";
                cancel.Left = 257;
                cancel.Top = 75;
                cancel.Width = 75;
                cancel.DialogResult = DialogResult.Cancel;

                form.Controls.Add(lbl);
                form.Controls.Add(tb);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);

                form.AcceptButton = ok;
                form.CancelButton = cancel;

                if (form.ShowDialog() != DialogResult.OK) return null;

                // 允许 0.7 / 1 / 2 等
                string s = (tb.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(s)) return null;

                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    // 兼容中文输入法逗号
                    s = s.Replace("，", ".").Replace(",", ".");
                    if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        return null;
                }

                return v;
            }
        }

        public bool IsNullImage()
        {
            return _origImage == null || _showingImage == null;
        }

        public (bool, int, int, int) GetCurrentMousePosAndGrayval()
        {
            try
            {
                if (_showingImage == null || _origImage == null)
                {
                    return (false, 0, 0, 0);
                }

                _hWindowControl.HalconWindow.GetMposition(out var mousePostRow, out var mousePostCol, out _);
                GetImageSize(out int imageWidth, out int imageHeight);
                HTuple grayValue = 0;
                if (mousePostRow > 0 && mousePostCol > 0 && mousePostRow < imageHeight && mousePostCol < imageWidth)
                {
                    HOperatorSet.GetGrayval(_origImage, mousePostRow, mousePostCol, out grayValue);
                    grayValue = grayValue[0];
                }

                return (true, mousePostRow, mousePostCol, grayValue.I);
            }
            catch { }
            return (false, 0, 0, 0);
        }

        // 清除拟合的最小圆
        public void ClearCircumCircle()
        {
            _circumCircle = null;

            ReShowDrawALL();
        }

        // 清除标准位置轮廓
        public void ClearShowingShapeModel()
        {
            _showingShapeModelS?.Dispose();
            _showingShapeModelL?.Dispose();
            _showingShapeModelS = null;
            _showingShapeModelL = null;

            ReShowDrawALL();
        }

        public void ClearAllDrawingOnly()
        {
            // 清除用户绘制的矩形
            _drawUserRectangles.Clear();

            // 清除用户绘制的圆
            _drawUserCircles.Clear();

            // 清除用户绘制的多边形
            _drawUserPolygons.Clear();

            // 清除用户绘制的多边形
            _drawUserLs2Lses.Clear();
            _drawLs2LsId = 0;

            // 清除用户绘制的箭头
            _drawUserArrows.Clear();
            _drawArrowId = 0;

            // 清除用户绘制的线
            _drawUserMeasureLines.Clear();

            // 交互预览不应在加载新图或清空时残留。
            _transientInteractionLines.Clear();
            _transientInteractionMarkers.Clear();

            // 清除要绘制的动作类型
            _drawActionTypes.Clear();

            // 清除要绘制的对象 
            // 不删除对象，由添加进来时的拥有者来管生命周期
            //for (int i = 0; i < _drawObjectList.Count; i++)
            //{
            //    _drawObjectList[0].HObject?.Dispose();
            //}
            _drawObjectList.Clear();

            // 清除拟合的最小圆
            _circumCircle?.Dispose();
            _circumCircle = null;

            IsFindCircle = false;

            DrawCenterCross = false;

            _showingShapeModelS?.Dispose();
            _showingShapeModelL?.Dispose();
            _showingShapeModelS = null;
            _showingShapeModelL = null;
        }

        // 清除全部框、要绘制的对象
        public void ClearAllDrawing()
        {
            ClearAllDrawingOnly();

            ReShowDrawALL();
        }

        public void ClearAll()
        {
            _showingImage?.Dispose();
            _showingImage = null;
            _origImage?.Dispose();
            _origImage = null;
            ClearAllDrawing();
        }

        // 拟合最小圆
        // 圆心坐标: X = " + hv_Column.D + ", Y = " + hv_Row.D
        public (bool, double, double) FitCircumCircle()
        {
            if (_showingImage == null)
            {
                return (false, 0, 0);
            }

            // Local iconic variables 
            //HObject ho_Region, ho_RegionOpening, ho_Circle;
            // Local control variables 
            HTuple hv_UsedThreshold = new HTuple(), hv_Row = new HTuple();
            HTuple hv_Column = new HTuple(), hv_Radius = new HTuple();
            // Initialize local and output iconic variables 
            //HOperatorSet.GenEmptyObj(out ho_Region);
            //HOperatorSet.GenEmptyObj(out ho_RegionOpening);
            //HOperatorSet.GenEmptyObj(out ho_Circle);

            var halconWindow = _hWindowControl.HalconWindow;
            halconWindow.SetDraw("margin");

            HOperatorSet.ScaleImageMax(_showingImage, out var ho_ImageScaleMax);
            HOperatorSet.BinaryThreshold(ho_ImageScaleMax, out var ho_Region, "max_separability", "dark", out hv_UsedThreshold);
            HOperatorSet.Threshold(ho_ImageScaleMax, out ho_Region, 1, hv_UsedThreshold * 0.5);
            HOperatorSet.Connection(ho_Region, out var ho_ConnectedRegions);
            HOperatorSet.OpeningCircle(ho_ConnectedRegions, out var ho_RegionOpening, 3.5);
            HOperatorSet.FillUp(ho_RegionOpening, out var ho_RegionFillUp);
            HOperatorSet.SelectShape(ho_RegionFillUp, out var ho_SelectedRegions, "circularity", "and", 0.8, 1.0);
            HOperatorSet.SelectShapeStd(ho_SelectedRegions, out var ho_SelectedRegions2, "max_area", 70);
            HOperatorSet.Intersection(ho_SelectedRegions2, ho_RegionOpening, out var ho_RegionIntersection);

            HOperatorSet.GenContourRegionXld(ho_RegionIntersection, out var ho_Contours, "border");
            HOperatorSet.SmallestCircleXld(ho_Contours, out hv_Row, out hv_Column, out hv_Radius);
            HOperatorSet.GenCircleContourXld(out var ho_ContCircle, hv_Row, hv_Column, hv_Radius, 0, 6.28318, "positive", 1);

            //HOperatorSet.SmallestCircle(ho_RegionIntersection, out hv_Row, out hv_Column, out hv_Radius);
            //HOperatorSet.GenCircle(out var ho_ContCircle, hv_Row, hv_Column, hv_Radius);

            // 显示圆
            //HOperatorSet.DispObj(ho_Circle, halconWindow);
            _circumCircle?.Dispose();
            _circumCircle = ho_ContCircle.Clone();

            var rX = hv_Column.Length == 1 ? hv_Column.D : 0;
            var rY = hv_Row.Length == 1 ? hv_Row.D : 0;
            //System.Console.WriteLine("圆心坐标: X = " + hv_Column.D + ", Y = " + hv_Row.D);

            // 计算偏移值
            HOperatorSet.GetImageSize(ho_ImageScaleMax, out HTuple width, out HTuple height);
            _circumCircleCenterOffsetX = rX - width.D / 2;
            _circumCircleCenterOffsetY = rY - height.D / 2;
            //if (RatioCountHandlerOne != null)
            //{
            //    _circumCircleRadius = RatioCountHandlerOne(_origImage);
            //}
            //else
            {
                //_circumCircleRadius = hv_Radius.D * 0.139;
                _circumCircleRadius = hv_Radius.Length == 1 ? hv_Radius.D : 0;
            }

            ho_ImageScaleMax.Dispose();
            ho_Region.Dispose(); ho_ConnectedRegions.Dispose();
            ho_RegionOpening.Dispose(); ho_RegionFillUp.Dispose();
            ho_SelectedRegions.Dispose(); ho_SelectedRegions2.Dispose();
            ho_RegionIntersection.Dispose(); ho_Contours.Dispose(); ho_ContCircle.Dispose();

            return (true, rX, rY);
        }

        public void SetShowingShapeModel(HObject ho_ChRegionS, HObject ho_ChRegionL)
        {
            _showingShapeModelS?.Dispose();
            _showingShapeModelL?.Dispose();
            _showingShapeModelS = ho_ChRegionS;
            _showingShapeModelL = ho_ChRegionL;
        }

        internal void SaveOrigImage(string fileName)
        {
            if (_origImage == null) return;

            var dirPath = FileSystemHelper.GetDirectoryName(fileName);
            if (Directory.Exists(dirPath))
            {
                try
                {
                    if (HalconHelper.GetImageInfo(_origImage, out int width, out int height, out int channels, out int bitDepth, out _))
                    {
                        if (channels == 1 && bitDepth == 16)
                        {
                            HalconHelper.SaveImage(_origImage, fileName, "tiff");
                            LogHelper.Debug($"保存图像到：{fileName + ".tif"}");
                        }
                        else
                        {
                            HalconHelper.SaveImage(_origImage, fileName, "png");
                            LogHelper.Debug($"保存图像到：{fileName + ConstHelper.ImageExtPng}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"图像存储 Failed to save image '{fileName}': {ex.Message}");
                }
            }
        }

        public void SaveShowingImage(string fileName)
        {
            if (_showingImage == null) return;

            var dirPath = FileSystemHelper.GetDirectoryName(fileName);
            if (Directory.Exists(dirPath))
            {
                try
                {
                    HalconHelper.SaveImage(_showingImage, fileName);
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"图像存储 Failed to save image '{fileName}': {ex.Message}");
                }
            }
        }

        public bool SaveImageByDump(string fileName)
        {
            if (_showingImage == null) return false;

            var dirPath = FileSystemHelper.GetDirectoryName(fileName);
            if (Directory.Exists(dirPath))
            {
                try
                {
                    var outputImage = DumpImage();
                    if (outputImage != null)
                    {
                        HalconHelper.SaveImage(outputImage, fileName, "png");
                        LogHelper.Debug($"保存图像到：{fileName}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"保存图像到失败：{ex.Message}");
                }

            }
            return false;
        }

        public HObject DumpImage(uint lineNumber = 0, HObject showingImage = null)
        {
            var baseImage = showingImage == null ? _showingImage : showingImage;
            if (baseImage == null) return null;

            try
            {
                // 初始化 HALCON 窗口
                // 获取图像宽度和高度
                HOperatorSet.GetImagePointer1(baseImage, out HTuple hPointer, out HTuple hType, out HTuple hWidth, out HTuple hHeight);

                // 创建虚拟 HALCON 窗口并设置为图像大小
                HOperatorSet.OpenWindow(0, 0, hWidth, hHeight, "root", "buffer", "", out HTuple hWindow);

                // 显示图像
                HOperatorSet.ClearWindow(hWindow);
                HOperatorSet.SetPart(hWindow, 0, 0, hHeight - 1, hWidth - 1);
                HOperatorSet.DispObj(baseImage, hWindow);

                // 绘用户画的外形、对象、测量，传入 HWindow
                DrawUser(hWindow, lineNumber, (int)hWidth.D, _drawLineWidth, _fontMeasureSize);

                // 从虚拟窗口获取绘制后的图像
                HOperatorSet.DumpWindowImage(out var outputImage, hWindow);

                HOperatorSet.CloseWindow(hWindow);

                return outputImage;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"视觉 Main处理 合成结果图失败 DumpImage：{ex.Message}");
            }
            return null;
        }

        public double GetCurrentZoomLevel()
        {
            _hWindowControl.HalconWindow.GetPart(out int currentBeginRow, out int currentBeginCol, out int currentEndRow, out int currentEndCol);
            GetImageSize(out int imageWidth, out int imageHeight);

            // 计算水平和垂直方向的缩放比例
            double zoomX = (double)(currentEndCol - currentBeginCol) / imageWidth;
            double zoomY = (double)(currentEndRow - currentBeginRow) / imageHeight;

            // 通常水平和垂直方向的缩放比例应该相同
            return Math.Min(zoomX, zoomY);
        }

        public void EnhanceImage()
        {
            if (EnhanceImageHandlerOne != null)
            {
                HObject enhanceImage = EnhanceImageHandlerOne(_showingImage);

                //HObject enhanceImage = HalconVccEngine.EnhanceImage(_showingImage);
                _showingImage?.Dispose();
                _showingImage = enhanceImage;
                ReShowDrawALL();
            }
        }

        public void Dispose()
        {
            ClearAllDrawingOnly();
            _origImage?.Dispose();
            _showingImage?.Dispose();
            _showingImage2?.Dispose();
            _origMat?.Dispose();
        }

        /// <summary>
        /// 在指定ROI内寻找最大缺陷区域（基于阈值分割），返回多边形轮廓点
        /// </summary>
        /// <param name="image">原始图像（需未经过窗宽窗位处理）</param>
        /// <param name="roi">矩形ROI（图像坐标）</param>
        /// <param name="areaPixel">返回区域的像素面积</param>
        /// <param name="meanGray">返回区域的平均灰度</param>
        /// <returns>多边形点列表（图像坐标）</returns>
        public List<Point> FindLargestDefect(HObject image, Rectangle roi, int minGray, int maxGray, out double areaPixel, out double meanGray)
        {
            areaPixel = 0;
            meanGray = 0;
            var points = new List<Point>();
            try
            {
                HOperatorSet.GenRectangle1(out HObject ho_Rect, roi.Top, roi.Left, roi.Bottom, roi.Right);
                HOperatorSet.ReduceDomain(image, ho_Rect, out HObject imageReduced);
                HOperatorSet.Threshold(imageReduced, out HObject regions, minGray, maxGray);
                HOperatorSet.Connection(regions, out HObject connected);
                HOperatorSet.SelectShapeStd(connected, out HObject maxRegion, "max_area", 0);
                if (maxRegion == null || !maxRegion.IsInitialized())
                    return points;
                HOperatorSet.AreaCenter(maxRegion, out HTuple area, out _, out _);
                areaPixel = area.D;
                HOperatorSet.Intensity(maxRegion, image, out HTuple mean, out _);
                meanGray = mean.D;
                HOperatorSet.GenContourRegionXld(maxRegion, out HObject contours, "border");
                HOperatorSet.GetContourXld(contours, out HTuple rows, out HTuple cols);
                for (int i = 0; i < rows.Length; i++)
                    points.Add(new Point((int)cols[i].D, (int)rows[i].D));

                // 简化轮廓点
                points = SimplifyContour(points, tolerance: 5.0); // 阈值可根据需要调整

                ho_Rect.Dispose(); imageReduced.Dispose(); regions.Dispose();
                connected.Dispose(); maxRegion.Dispose(); contours.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.Error($"FindLargestDefect error: {ex.Message}");
            }
            return points;
        }

        /// <summary>
        /// 执行固定 Halcon 检测流程（用于溢胶缺陷检测）
        /// </summary>
        public List<Point> PerformFixedDetection(HObject image, Rectangle roi, out double areaPixel, out double meanGray)
        {
            areaPixel = 0;
            meanGray = 0;
            var points = new List<Point>();
            try
            {
                // 生成 ROI 矩形
                HOperatorSet.GenRectangle1(out HObject rect, roi.Top, roi.Left, roi.Bottom, roi.Right);

                // 减少域
                HOperatorSet.ReduceDomain(image, rect, out HObject imageReduced);

                // 增强与滤波
                HOperatorSet.Emphasize(imageReduced, out HObject imageEmphasize, 11, 11, 1);
                HOperatorSet.GaussFilter(imageEmphasize, out HObject imageGauss, 11);

                // 阈值分割（固定 0-159，可根据实际调整）
                HOperatorSet.Threshold(imageGauss, out HObject region, 0, 159);

                // 连接区域
                HOperatorSet.Connection(region, out HObject connected);

                // 开闭运算优化
                HOperatorSet.OpeningCircle(connected, out HObject regionOpening, 6);
                HOperatorSet.ClosingCircle(regionOpening, out HObject regionClosing, 6);
                HOperatorSet.Connection(regionClosing, out HObject connected2);

                // 选择面积最大的区域
                HOperatorSet.AreaCenter(connected2, out HTuple areas, out _, out _);
                if (areas.Length == 0)
                {
                    // 无区域，释放资源后返回空列表
                    rect.Dispose(); imageReduced.Dispose(); imageEmphasize.Dispose(); imageGauss.Dispose();
                    region.Dispose(); connected.Dispose(); regionOpening.Dispose(); regionClosing.Dispose(); connected2.Dispose();
                    return points;
                }
                HOperatorSet.TupleMax(areas, out HTuple maxArea);
                HOperatorSet.SelectShape(connected2, out HObject selectedRegions, "area", "and", maxArea * 0.99, maxArea);

                // 填充区域
                HOperatorSet.FillUp(selectedRegions, out HObject regionFillUp);

                // 获取面积和灰度
                HOperatorSet.AreaCenter(regionFillUp, out HTuple area, out _, out _);
                areaPixel = area.D;
                HOperatorSet.Intensity(regionFillUp, image, out HTuple mean, out _);
                meanGray = mean.D;

                // 提取轮廓点
                HOperatorSet.GenContourRegionXld(regionFillUp, out HObject contours, "border");
                HOperatorSet.GetContourXld(contours, out HTuple rows, out HTuple cols);
                for (int i = 0; i < rows.Length; i++)
                    points.Add(new Point((int)cols[i].D, (int)rows[i].D));

                // 简化轮廓点
                points = SimplifyContour(points, 20);

                // 释放所有临时对象
                rect.Dispose(); imageReduced.Dispose(); imageEmphasize.Dispose(); imageGauss.Dispose();
                region.Dispose(); connected.Dispose(); regionOpening.Dispose(); regionClosing.Dispose();
                connected2.Dispose(); selectedRegions.Dispose(); regionFillUp.Dispose(); contours.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.Error($"PerformFixedDetection error: {ex.Message}");
            }
            return points;
        }

        /// <summary>
        /// 将多边形与矩形区域合并，更新多边形点集并重新计算面积
        /// </summary>
        public void MergePolygonWithRectangle(DrawPolygonInfo polygon, Rectangle rect)
        {
            if (polygon == null || polygon.Points == null || polygon.Points.Count < 3)
                return;

            try
            {
                // 生成多边形填充区域
                var rowsPoly = new HTuple(polygon.Points.Select(p => (HTuple)p.Y).ToArray());
                var colsPoly = new HTuple(polygon.Points.Select(p => (HTuple)p.X).ToArray());
                HOperatorSet.GenRegionPolygonFilled(out HObject polyRegion, rowsPoly, colsPoly);

                // 生成矩形区域
                HOperatorSet.GenRectangle1(out HObject rectRegion, rect.Top, rect.Left, rect.Bottom, rect.Right);

                // 合并
                HOperatorSet.Union2(polyRegion, rectRegion, out HObject unionRegion);

                // 提取合并后的轮廓点
                HOperatorSet.GenContourRegionXld(unionRegion, out HObject contours, "border");
                HOperatorSet.GetContourXld(contours, out HTuple rows, out HTuple cols);

                // 转换为点列表
                var newPoints = new List<Point>();
                for (int i = 0; i < rows.Length; i++)
                {
                    newPoints.Add(new Point((int)Math.Round(cols[i].D), (int)Math.Round(rows[i].D)));
                }

                // 简化轮廓点
                newPoints = SimplifyContour(newPoints, tolerance: 5.0);

                // 更新多边形
                polygon.Points = newPoints;
                polygon.UpdateWHA(CalcActualSizeMM(1, _lineNumber)); // 重新计算面积等

                // 释放资源
                polyRegion.Dispose();
                rectRegion.Dispose();
                unionRegion.Dispose();
                contours.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.Error($"MergePolygonWithRectangle error: {ex.Message}");
            }
        }

        /// <summary>
        /// 简化轮廓点集，移除距离过近的点
        /// </summary>
        /// <param name="contour">原始轮廓点</param>
        /// <param name="tolerance">距离阈值（像素）</param>
        /// <returns>简化后的点集</returns>
        public List<Point> SimplifyContour(List<Point> contour, double tolerance = 5.0)
        {
            if (contour.Count <= 3) return contour;

            List<Point> simplified = new List<Point>();
            simplified.Add(contour[0]); // 保留起点
            Point last = contour[0];

            for (int i = 1; i < contour.Count; i++)
            {
                double dist = Math.Sqrt(Math.Pow(contour[i].X - last.X, 2) + Math.Pow(contour[i].Y - last.Y, 2));
                if (dist >= tolerance)
                {
                    simplified.Add(contour[i]);
                    last = contour[i];
                }
            }

            // 如果最后一个点与起点重合，可能需要处理，但一般轮廓点最后一个点不重合
            return simplified;
        }

    }

}
