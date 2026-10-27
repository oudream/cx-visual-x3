using System;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Volume;

namespace CxVisual.TestCenter.UI
{
    /// <summary>TestCenter只注入正式FVO传递函数及CT3D专业刷新回调，不实现编辑器组合。</summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private MPWVolumeTransferFunctionPanels _transferPanels;
        public int ColorStopCount {get{return _gpuSettings.TransferFunctions.Color.Points.Count;}}
        public int ScalarStopCount {get{return _gpuSettings.TransferFunctions.ScalarOpacity.Points.Count;}}
        public int GradientStopCount {get{return _gpuSettings.TransferFunctions.GradientOpacity.Points.Count;}}

        private void BuildTransferPanels(MPWVolumeSettingsDockControl dock)
        {
            _transferPanels=new MPWVolumeTransferFunctionPanels(
                dock,_gpuSettings.TransferFunctions,
                _gpuUseThreshold,_gpuThreshold,_gpuColor,_gpuScalar,_gpuGradient,
                ()=>UpdateGpuSettings(true),()=>_upperTabs.SelectedIndex=3);
        }

        public float CurrentThresholdLower {get{return _gpuSettings.TransferFunctions.Threshold;}}
        public float CurrentThresholdUpper {get{return _gpuSettings.TransferFunctions.UpperThreshold;}}
        public void SetThresholdRangeForTest(float low,float high)
        {
            if(_transferPanels==null)throw new InvalidOperationException("尚未创建传递函数控件。");
            _transferPanels.ThresholdCurve.SetRange(low,high);
        }
        public void AddScalarStopForTest(float x,float y)
        {
            if(_transferPanels==null)throw new InvalidOperationException("尚未创建传递函数控件。");
            _transferPanels.ScalarCurve.AddControlPoint(x,y);
        }
        public void AddColorStopForTest(float x)
        {
            if(_transferPanels==null)throw new InvalidOperationException("尚未创建传递函数控件。");
            _transferPanels.ColorCurve.AddControlPoint(x,.5f);
        }
    }
}
