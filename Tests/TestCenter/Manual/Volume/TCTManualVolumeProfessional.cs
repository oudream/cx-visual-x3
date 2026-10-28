using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering;
using CxVisual.Professional.CT3D;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 正式CT3D算法的工作台装配者：算法不引用UI，MPW只接收显示投影。
    /// 本类不保存另一份Volume或专业结果仓库；只有会话级预览顶点。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private readonly TextBox _sphereThresholdText=new TextBox{Width=66,Text="1000",
            Margin=new System.Windows.Thickness(3)};
        private IReadOnlyList<Vector3> _surfacePreviewPoints;
        private Guid? _surfaceSceneItemId;
        private CancellationTokenSource _sphereCancellation;
        private long _sphereEpoch;
        public CT3DSphereFitResult LastSphereFit {get;private set;}
        public int SurfaceDisplayedPointCount {get{return _scene.SurfacePreviewPointCount;}}
        private IReadOnlyList<Vector3> SurfacePointsForPresentation
        {
            get
            {
                if(_state==null || !_surfaceSceneItemId.HasValue || _surfacePreviewPoints==null)
                    return null;
                MVI3DSceneItem item;
                return _state.Scene3D.TryGet(_surfaceSceneItemId.Value,out item) && item.Visible
                    ? _surfacePreviewPoints:null;
            }
        }

        private void CancelSurfaceAnalysis()
        {
            _sphereEpoch=checked(_sphereEpoch+1);
            if(_sphereCancellation!=null)_sphereCancellation.Cancel();
        }

        /// <summary>MVI Scene显隐是真源；WPF和GPU只消费当前可视投影。</summary>
        private void OnProfessionalSceneChanged(object sender,EventArgs e)
        {
            if(_state==null || !Object.ReferenceEquals(sender,_state.Scene3D))return;
            UpdateSurfacePresentation();
        }

        private void UpdateSurfacePresentation()
        {
            if(!_surfaceSceneItemId.HasValue || _state==null ||
               _surfacePreviewPoints==null)
                return;
            MVI3DSceneItem item;
            bool visible=_state.Scene3D.TryGet(_surfaceSceneItemId.Value,out item) &&
                item.Visible;
            if(visible)
            {
                _scene.SetSurfacePoints(_surfacePreviewPoints);
                if(_gpu!=null)_gpu.SetSurfacePoints(_surfacePreviewPoints);
            }
            else
            {
                _scene.ClearSurfacePoints();
                if(_gpu!=null)_gpu.ClearSurfacePoints();
            }
        }

        private void ClearSurfaceAnalysis()
        {
            CancelSurfaceAnalysis();
            _surfacePreviewPoints=null;
            _surfaceSceneItemId=null;
            LastSphereFit=null;
            _scene.ClearSurfacePoints();
            if(_gpu!=null)_gpu.ClearSurfacePoints();
        }

        /// <summary>仅供调用者已明确标定的真实RAW源；同步入口用于小型自动化测试。</summary>
        public CT3DSphereFitResult AnalyzeCurrentVolumeSphere(
            float threshold,int sampleStep=1,int maxPoints=8000)
        {
            if(_volume==null || _state==null)
                throw new InvalidOperationException("没有加载原始CT体数据。");
            if(!_volume.SpatialFrame.IsCalibrated)
                throw new InvalidOperationException("源空间未标定：拒绝将显示Spacing当作已验证工业毫米。");
            FVOVolumeData volume=_volume;
            long epoch=_sphereEpoch;
            using(FVOOriginalScalarLease original=volume.AcquireOriginalScalarLease())
            {
                CT3DSurfaceSamples surface=CT3DSurfaceSphereAnalysis.SampleBoundary(
                    original,SurfaceSourceRef(volume),epoch,threshold,sampleStep,maxPoints,
                    CancellationToken.None);
                CT3DSphereFitResult fit=CT3DSurfaceSphereAnalysis.FitSphere(surface);
                PublishSurfaceAnalysis(volume,epoch,surface,fit);
                return fit;
            }
        }

        private static string SurfaceSourceRef(FVOVolumeData volume)
        {
            return string.IsNullOrWhiteSpace(volume.SourceFilePath)
                ? "session:volume:"+volume.Name
                : volume.SourceFilePath;
        }

        private void PublishSurfaceAnalysis(FVOVolumeData volume,long epoch,
            CT3DSurfaceSamples surface,CT3DSphereFitResult fit)
        {
            if(!Object.ReferenceEquals(volume,_volume) || epoch!=_sphereEpoch ||
               _state==null || !Object.ReferenceEquals(surface.SpatialFrame,volume.SpatialFrame))
                throw new InvalidOperationException("体数据或空间Frame已改变，拒绝发布过期分析结果。");
            IReadOnlyList<Vector3> points=MPWVolumeSurfaceProjection.Project(
                surface.Points,volume.SpatialFrame,volume.OriginalWidth,
                volume.OriginalHeight,volume.OriginalDepth,8000);
            // 同一会话的最后一次分析替换旧Surface引用；旧结果不再伪装可见。
            if(_surfaceSceneItemId.HasValue)
                _state.Scene3D.Remove(_surfaceSceneItemId.Value);
            _surfacePreviewPoints=points;
            LastSphereFit=fit;
            Guid itemId=_state.Scene3D.Add(MVI3DSceneKind.Surface,
                "ct3d:result:"+fit.ResultId.ToString("N"),fit.SourceRevision,
                "volume:physical-frame");
            _surfaceSceneItemId=itemId;
            _state.Scene3D.Select(itemId);
            UpdateSurfacePresentation();
            _status.Text=String.Format(CultureInfo.InvariantCulture,
                "离散球分析 · 直径 {0:0.###} mm · RMS {1:0.###} mm · 显示 {2} 点（非工业验收）",
                fit.DiameterMm,fit.RmsResidualMm,points.Count);
            RaiseStateChanged();
        }

        /// <summary>工具栏走异步运算；切换Volume/关闭时取消，旧结果不得更新新场景。</summary>
        private async void StartSurfaceSphereFromUi()
        {
            if(_volume==null)return;
            if(_sphereCancellation!=null)
            {
                _status.Text="已有表面分析执行中，请先切换/取消本次操作。";
                return;
            }
            float threshold;
            if(!float.TryParse(_sphereThresholdText.Text,NumberStyles.Float,
                CultureInfo.InvariantCulture,out threshold))
            {
                _status.Text="请输入有效的原始标量阈值，例如1000。";
                return;
            }
            FVOVolumeData volume=_volume;
            if(!volume.SpatialFrame.IsCalibrated)
            {
                _status.Text="专业表面分析拒绝：尚无可信空间方向/原点标定。";
                return;
            }
            FVOOriginalScalarLease lease;
            try {lease=volume.AcquireOriginalScalarLease();}
            catch(Exception error) {_status.Text="专业表面分析拒绝："+error.Message;return;}
            CancellationTokenSource current=new CancellationTokenSource();
            _sphereCancellation=current;
            long epoch=_sphereEpoch;
            _status.Text="正在后台提取真实RAW边界并拟合球...";
            try
            {
                var result=await Task.Run(delegate
                {
                    using(lease)
                    {
                        CT3DSurfaceSamples sampled=CT3DSurfaceSphereAnalysis.SampleBoundary(
                            lease,SurfaceSourceRef(volume),epoch,threshold,1,8000,current.Token);
                        CT3DSphereFitResult fit=CT3DSurfaceSphereAnalysis.FitSphere(sampled);
                        return Tuple.Create(sampled,fit);
                    }
                });
                if(epoch==_sphereEpoch && Object.ReferenceEquals(_volume,volume) &&
                   !current.IsCancellationRequested)
                    PublishSurfaceAnalysis(volume,epoch,result.Item1,result.Item2);
            }
            catch(OperationCanceledException)
            {
                if(epoch==_sphereEpoch)_status.Text="专业表面分析已取消。";
            }
            catch(Exception error)
            {
                if(epoch==_sphereEpoch)_status.Text="专业表面分析失败："+error.Message;
            }
            finally
            {
                if(Object.ReferenceEquals(_sphereCancellation,current))
                    _sphereCancellation=null;
                current.Dispose();
            }
        }
    }
}
