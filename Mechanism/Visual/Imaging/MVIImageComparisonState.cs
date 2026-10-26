using System;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>承接旧ImageComparisonState的浏览对比策略；不另持有图像数据或显示控件。</summary>
    public enum MVIImageComparisonMode { Single, Toggle, Blend, SideBySide, Swipe }

    public sealed class MVIImageComparisonState
    {
        private MVIImageComparisonMode _mode = MVIImageComparisonMode.Single;
        private bool _syncView;
        private double _blendRatio = 0.5;
        public event EventHandler Changed;
        public MVIImageComparisonMode Mode { get { return _mode; } }
        public bool IsSyncViewEnabled { get { return _syncView; } }
        public double BlendRatio { get { return _blendRatio; } }
        public void SetMode(MVIImageComparisonMode mode)
        {
            if (!Enum.IsDefined(typeof(MVIImageComparisonMode),mode))
                throw new ArgumentOutOfRangeException("mode");
            if (_mode == mode) return;
            _mode = mode; Notify();
        }
        public void SetSyncView(bool enabled)
        {
            if (_syncView == enabled) return;
            _syncView = enabled; Notify();
        }
        public void SetBlendRatio(double ratio)
        {
            if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio < 0 || ratio > 1)
                throw new ArgumentOutOfRangeException("ratio");
            if (_blendRatio == ratio) return;
            _blendRatio = ratio; Notify();
        }
        public void Reset()
        {
            if (_mode == MVIImageComparisonMode.Single && !_syncView && _blendRatio == .5) return;
            _mode = MVIImageComparisonMode.Single; _syncView = false; _blendRatio = 0.5; Notify();
        }
        private void Notify()
        { EventHandler handler = Changed; if(handler!=null) handler(this,EventArgs.Empty); }
    }
}
