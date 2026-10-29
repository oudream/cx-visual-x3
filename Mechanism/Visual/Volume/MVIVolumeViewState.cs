using CxVisual.Foundation.Volume;
using System;
using System.ComponentModel;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>记录单个二维切片当前可视范围，并同步为三维彩色切面的范围。</summary>
    public struct MVISliceViewport
    {
        public MVISliceViewport(float minA, float maxA, float minB, float maxB)
        {
            MinA = minA;
            MaxA = maxA;
            MinB = minB;
            MaxB = maxB;
        }

        public float MinA { get; private set; }
        public float MaxA { get; private set; }
        public float MinB { get; private set; }
        public float MaxB { get; private set; }

        public static MVISliceViewport Full
        {
            get { return new MVISliceViewport(0.0f, 1.0f, 0.0f, 1.0f); }
        }
    }

    /// <summary>集中管理 X/Y/Z 切片索引和视口状态，供二维、三维视图联动使用。</summary>
    public sealed class MVIVolumeViewState : INotifyPropertyChanged
    {
        /// <summary>该会话唯一的三维相机状态；WPF与GL不得分别持有权威姿态。</summary>
        public MVI3DCameraState Camera { get; private set; } = new MVI3DCameraState();

        /// <summary>仅管理可视引用和选择，不复制工程/Volume/CT3D专业真值。</summary>
        public MVI3DSceneState Scene3D { get; private set; } = new MVI3DSceneState();

        private int _sliceX;
        private int _sliceY;
        private int _sliceZ;
        private bool _showCrosshair = true;
        private MVISliceViewport _xyViewport = MVISliceViewport.Full;
        private MVISliceViewport _xzViewport = MVISliceViewport.Full;
        private MVISliceViewport _yzViewport = MVISliceViewport.Full;
        private FVOObliqueSliceDefinition _obliqueSlice = new FVOObliqueSliceDefinition();

        public event PropertyChangedEventHandler PropertyChanged;

        public int SliceX
        {
            get { return _sliceX; }
            set { SetField(ref _sliceX, value, "SliceX"); }
        }

        public int SliceY
        {
            get { return _sliceY; }
            set { SetField(ref _sliceY, value, "SliceY"); }
        }

        public int SliceZ
        {
            get { return _sliceZ; }
            set { SetField(ref _sliceZ, value, "SliceZ"); }
        }

        public bool ShowCrosshair
        {
            get { return _showCrosshair; }
            set { SetField(ref _showCrosshair, value, "ShowCrosshair"); }
        }

        public FVOObliqueSliceDefinition ObliqueSlice
        {
            get { return _obliqueSlice; }
        }

        public void SetObliqueSlice(FVOObliqueSliceDefinition definition)
        {
            _obliqueSlice = (definition ?? new FVOObliqueSliceDefinition()).Clone();
            OnPropertyChanged("ObliqueSlice");
        }

        public MVISliceViewport GetSliceViewport(FVOSlicePlane plane)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    return _xyViewport;
                case FVOSlicePlane.XZ:
                    return _xzViewport;
                case FVOSlicePlane.YZ:
                    return _yzViewport;
                default:
                    return MVISliceViewport.Full;
            }
        }

        public void SetSliceViewport(FVOSlicePlane plane, float minA, float maxA, float minB, float maxB)
        {
            if (!IsFinite(minA) ||
                !IsFinite(maxA) ||
                !IsFinite(minB) ||
                !IsFinite(maxB))
            {
                return;
            }

            var next = new MVISliceViewport(
                Math.Min(minA, maxA),
                Math.Max(minA, maxA),
                Math.Min(minB, maxB),
                Math.Max(minB, maxB));

            switch (plane)
            {
                case FVOSlicePlane.XY:
                    if (IsSameViewport(_xyViewport, next)) return;
                    _xyViewport = next;
                    break;
                case FVOSlicePlane.XZ:
                    if (IsSameViewport(_xzViewport, next)) return;
                    _xzViewport = next;
                    break;
                case FVOSlicePlane.YZ:
                    if (IsSameViewport(_yzViewport, next)) return;
                    _yzViewport = next;
                    break;
            }

            OnPropertyChanged("SliceViewports");
        }

        public static MVIVolumeViewState CreateCentered(FVOVolumeData volume)
        {
            return new MVIVolumeViewState
            {
                SliceX = volume.OriginalWidth / 2,
                SliceY = volume.OriginalHeight / 2,
                SliceZ = volume.OriginalDepth / 2,
            };
        }

        /// <summary>将当前三刀上的二维像素矩形映射到正式FVO物理Frame，不建立第二套ROI真值。</summary>
        public FVOPhysicalSliceRectangle MapRectangleRoi(FVOSlicePlane plane,
            int left,int top,int right,int bottom,FVOVolumeData volume)
        {
            if(volume==null)throw new ArgumentNullException("volume");
            return FVOSliceImageBuilder.RectangleToPhysical(volume,SliceX,SliceY,SliceZ,
                plane,left,top,right,bottom);
        }

        public void ResetCentered(FVOVolumeData volume)
        {
            SetSlices(volume.OriginalWidth / 2, volume.OriginalHeight / 2, volume.OriginalDepth / 2, volume);
        }

        public void SetSliceX(int value, FVOVolumeData volume)
        {
            SliceX = Clamp(value, 0, volume.OriginalWidth - 1);
        }

        public void SetSliceY(int value, FVOVolumeData volume)
        {
            SliceY = Clamp(value, 0, volume.OriginalHeight - 1);
        }

        public void SetSliceZ(int value, FVOVolumeData volume)
        {
            SliceZ = Clamp(value, 0, volume.OriginalDepth - 1);
        }

        /// <summary>三正交切面滚轮换层，统一使用MVI单一三刀状态并钳制体素边界。</summary>
        public void ScrollSlice(FVOSlicePlane plane, int steps, FVOVolumeData volume)
        {
            if (volume == null) throw new ArgumentNullException("volume");
            if (steps == 0) return;
            switch (plane)
            {
                case FVOSlicePlane.XY: SetSliceZ(checked(SliceZ + steps), volume); break;
                case FVOSlicePlane.XZ: SetSliceY(checked(SliceY + steps), volume); break;
                case FVOSlicePlane.YZ: SetSliceX(checked(SliceX + steps), volume); break;
                default: throw new ArgumentOutOfRangeException("plane");
            }
        }

        /// <summary>由FVO真实切片像素索引定位共享三刀；支持非等距体素和降采样。</summary>
        public void SetSlicesFromImage(FVOSlicePlane plane, int pixelX, int pixelY,
            FVOVolumeData volume)
        {
            if (volume == null) throw new ArgumentNullException("volume");
            int x, y, z;
            FVOSliceImageBuilder.ImageToVoxel(volume, SliceX, SliceY, SliceZ,
                plane, pixelX, pixelY, out x, out y, out z);
            SetSlices(x, y, z, volume);
        }

        public void SetSlices(int x, int y, int z, FVOVolumeData volume)
        {
            var nextX = Clamp(x, 0, volume.OriginalWidth - 1);
            var nextY = Clamp(y, 0, volume.OriginalHeight - 1);
            var nextZ = Clamp(z, 0, volume.OriginalDepth - 1);
            if (_sliceX == nextX && _sliceY == nextY && _sliceZ == nextZ)
            {
                return;
            }

            _sliceX = nextX;
            _sliceY = nextY;
            _sliceZ = nextZ;
            OnPropertyChanged("All");
        }

        private void SetField(ref int field, int value, string propertyName)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }

        private void SetField(ref bool field, bool value, string propertyName)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }

        private static bool IsSameViewport(MVISliceViewport left, MVISliceViewport right)
        {
            const float epsilon = 0.0001f;
            return Math.Abs(left.MinA - right.MinA) < epsilon &&
                   Math.Abs(left.MaxA - right.MaxA) < epsilon &&
                   Math.Abs(left.MinB - right.MinB) < epsilon &&
                   Math.Abs(left.MaxB - right.MaxB) < epsilon;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Min(max, Math.Max(min, value));
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
