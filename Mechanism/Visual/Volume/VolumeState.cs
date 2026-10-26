using CxECTVolumeCore.Slicing;
using CxECTVolumeCore.Utils;
using System;
using System.ComponentModel;

namespace CxECTVolumeCore.Volume
{
    /// <summary>记录单个二维切片当前可视范围，并同步为三维彩色切面的范围。</summary>
    public struct SliceViewport
    {
        public SliceViewport(float minA, float maxA, float minB, float maxB)
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

        public static SliceViewport Full
        {
            get { return new SliceViewport(0.0f, 1.0f, 0.0f, 1.0f); }
        }
    }

    /// <summary>集中管理 X/Y/Z 切片索引和视口状态，供二维、三维视图联动使用。</summary>
    public sealed class VolumeState : INotifyPropertyChanged
    {
        private int _sliceX;
        private int _sliceY;
        private int _sliceZ;
        private bool _showCrosshair = true;
        private SliceViewport _xyViewport = SliceViewport.Full;
        private SliceViewport _xzViewport = SliceViewport.Full;
        private SliceViewport _yzViewport = SliceViewport.Full;
        private ObliqueSliceDefinition _obliqueSlice = new ObliqueSliceDefinition();

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

        public ObliqueSliceDefinition ObliqueSlice
        {
            get { return _obliqueSlice; }
        }

        public void SetObliqueSlice(ObliqueSliceDefinition definition)
        {
            _obliqueSlice = (definition ?? new ObliqueSliceDefinition()).Clone();
            OnPropertyChanged("ObliqueSlice");
        }

        public SliceViewport GetSliceViewport(SlicePlane plane)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                    return _xyViewport;
                case SlicePlane.XZ:
                    return _xzViewport;
                case SlicePlane.YZ:
                    return _yzViewport;
                default:
                    return SliceViewport.Full;
            }
        }

        public void SetSliceViewport(SlicePlane plane, float minA, float maxA, float minB, float maxB)
        {
            if (!FrameworkCompat.IsFinite(minA) ||
                !FrameworkCompat.IsFinite(maxA) ||
                !FrameworkCompat.IsFinite(minB) ||
                !FrameworkCompat.IsFinite(maxB))
            {
                return;
            }

            var next = new SliceViewport(
                Math.Min(minA, maxA),
                Math.Max(minA, maxA),
                Math.Min(minB, maxB),
                Math.Max(minB, maxB));

            switch (plane)
            {
                case SlicePlane.XY:
                    if (IsSameViewport(_xyViewport, next)) return;
                    _xyViewport = next;
                    break;
                case SlicePlane.XZ:
                    if (IsSameViewport(_xzViewport, next)) return;
                    _xzViewport = next;
                    break;
                case SlicePlane.YZ:
                    if (IsSameViewport(_yzViewport, next)) return;
                    _yzViewport = next;
                    break;
            }

            OnPropertyChanged("SliceViewports");
        }

        public static VolumeState CreateCentered(VolumeData volume)
        {
            return new VolumeState
            {
                SliceX = volume.OriginalWidth / 2,
                SliceY = volume.OriginalHeight / 2,
                SliceZ = volume.OriginalDepth / 2,
            };
        }

        public void ResetCentered(VolumeData volume)
        {
            SetSlices(volume.OriginalWidth / 2, volume.OriginalHeight / 2, volume.OriginalDepth / 2, volume);
        }

        public void SetSliceX(int value, VolumeData volume)
        {
            SliceX = FrameworkCompat.Clamp(value, 0, volume.OriginalWidth - 1);
        }

        public void SetSliceY(int value, VolumeData volume)
        {
            SliceY = FrameworkCompat.Clamp(value, 0, volume.OriginalHeight - 1);
        }

        public void SetSliceZ(int value, VolumeData volume)
        {
            SliceZ = FrameworkCompat.Clamp(value, 0, volume.OriginalDepth - 1);
        }

        public void SetSlices(int x, int y, int z, VolumeData volume)
        {
            var nextX = FrameworkCompat.Clamp(x, 0, volume.OriginalWidth - 1);
            var nextY = FrameworkCompat.Clamp(y, 0, volume.OriginalHeight - 1);
            var nextZ = FrameworkCompat.Clamp(z, 0, volume.OriginalDepth - 1);
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

        private static bool IsSameViewport(SliceViewport left, SliceViewport right)
        {
            const float epsilon = 0.0001f;
            return Math.Abs(left.MinA - right.MinA) < epsilon &&
                   Math.Abs(left.MaxA - right.MaxA) < epsilon &&
                   Math.Abs(left.MinB - right.MinB) < epsilon &&
                   Math.Abs(left.MaxB - right.MaxB) < epsilon;
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
