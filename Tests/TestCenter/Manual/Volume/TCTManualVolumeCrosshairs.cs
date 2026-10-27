using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Presentation.Wpf.Volume;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 三维工作台的真实十字线与操作：投影位置直接使用MPW图像的WPF变换，
    /// 旧HALCON行为：左键拖动定位三刀、滚轮换层、Ctrl+滚轮缩放；Alt+左键保留MPW平移。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private readonly Dictionary<FVOSlicePlane, MPWVolumeSliceOverlayControl> _crosshairs =
            new Dictionary<FVOSlicePlane, MPWVolumeSliceOverlayControl>();
        private readonly Dictionary<FVOSlicePlane, MPWVolumeSliceViewportControl> _sliceViewports =
            new Dictionary<FVOSlicePlane, MPWVolumeSliceViewportControl>();

        public int SliceLegendCount { get { return _crosshairs.Count; } }

        public int VisibleCrosshairCount
        {
            get
            {
                int count = 0;
                foreach (MPWVolumeSliceOverlayControl layer in _crosshairs.Values)
                    count += layer.VisibleCrosshairCount;
                return count;
            }
        }

        private void AddSliceViewport(Grid host, MPWImageViewerControl viewer,
            FVOSlicePlane plane, string title, int row, int column)
        {
            Grid section = AddViewport(host, viewer, title, row, column);
            section.Children.Remove(viewer);
            MPWVolumeSliceViewportControl content =
                new MPWVolumeSliceViewportControl(plane,viewer);
            Grid.SetRow(content,1);
            section.Children.Add(content);
            _sliceViewports.Add(plane,content);
            _crosshairs.Add(plane,content.Overlay);
            content.VoxelInspected+=(s,e)=>_status.Text=String.Format(
                "体素 ({0},{1},{2})  原始值 {3:0.###}  物理位置 ({4:0.###},{5:0.###},{6:0.###})",
                e.X,e.Y,e.Z,e.Value,e.PhysicalX,e.PhysicalY,e.PhysicalZ);
        }

        /// <summary>仅调用MPW公开的视口坐标同步能力；真正的WPF计算不在TestCenter。</summary>
        private void UpdateSliceViewportFromViewer(MPWImageViewerControl viewer,FVOSlicePlane plane)
        {
            if(_volume==null || _state==null || _updating)return;
            MPWVolumeSliceViewportControl control;
            if(_sliceViewports.TryGetValue(plane,out control))
                control.SyncVisibleViewport();
        }

        public void UpdateAllSliceViewportRanges()
        {
            UpdateSliceViewportFromViewer(_xy,FVOSlicePlane.XY);
            UpdateSliceViewportFromViewer(_xz,FVOSlicePlane.XZ);
            UpdateSliceViewportFromViewer(_yz,FVOSlicePlane.YZ);
        }

        /// <summary>复用唯一MVI切片索引，普通滚轮换层、Ctrl+滚轮由MPW缩放。</summary>
        public void ScrollSlice(FVOSlicePlane plane,int steps)
        {
            if(_volume==null || _state==null)return;
            _state.ScrollSlice(plane,steps,_volume);
        }

        /// <summary>图例由正式MPW公共叠加控件绘制；测试宿主只传入FVO与显示参数。</summary>
        private void UpdateSliceLegends()
        {
            if(_volume==null)return;
            foreach(MPWVolumeSliceOverlayControl layer in _crosshairs.Values)
                layer.UpdateLegend(_volume,_windowMin.Value,_windowMax.Value,
                    _pseudo.IsChecked==true);
        }

        public void SetCrosshairFromImage(FVOSlicePlane plane, int pixelX, int pixelY)
        {
            if (_volume == null || _state == null) return;
            _state.SetSlicesFromImage(plane,pixelX,pixelY,_volume);
        }

        public void UpdateCrosshairs()
        {
            if (_volume == null || _state == null)return;
            if (_crosshairEnabled.IsChecked!=true || !_state.ShowCrosshair)
            {
                ClearCrosshairs();
                return;
            }
            foreach (MPWVolumeSliceViewportControl viewport in _sliceViewports.Values)
                viewport.RefreshOverlay();
        }

        private void ClearCrosshairs()
        {
            foreach (MPWVolumeSliceOverlayControl layer in _crosshairs.Values)
                layer.ClearCrosshairs();
        }
    }
}
