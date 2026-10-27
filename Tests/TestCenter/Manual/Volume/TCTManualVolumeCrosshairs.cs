using System;
using System.Windows.Controls;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Volume;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// TestCenter只调用正式MPW四视图工作台；鼠标、切片叠加与可视区算法均属于MPW/MVI/FVO。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        public int SliceLegendCount { get { return _workbench==null?0:_workbench.SliceViewportCount; } }
        public int VisibleCrosshairCount
        {
            get { return _workbench==null?0:_workbench.VisibleCrosshairCount; }
        }

        public void UpdateAllSliceViewportRanges()
        {
            if(_volume!=null && _state!=null && !_updating)
                _workbench.SyncVisibleViewports();
        }
        public void ScrollSlice(FVOSlicePlane plane,int steps)
        {
            if(_volume!=null && _state!=null)_state.ScrollSlice(plane,steps,_volume);
        }
        private void UpdateSliceLegends()
        {
            if(_volume!=null)_workbench.UpdateLegends(
                _volume,_windowMin.Value,_windowMax.Value,_pseudo.IsChecked==true);
        }
        public void SetCrosshairFromImage(FVOSlicePlane plane,int pixelX,int pixelY)
        {
            if(_volume!=null && _state!=null)
                _state.SetSlicesFromImage(plane,pixelX,pixelY,_volume);
        }
        public void UpdateCrosshairs()
        {
            if(_volume==null || _state==null)return;
            if(_crosshairEnabled.IsChecked!=true || !_state.ShowCrosshair)
                ClearCrosshairs();
            else _workbench.RefreshCrosshairs();
        }
        private void ClearCrosshairs()
        {
            if(_workbench!=null)_workbench.ClearCrosshairs();
        }
    }
}
