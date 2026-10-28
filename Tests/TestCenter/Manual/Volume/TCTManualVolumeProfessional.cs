using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using CxVisual.Foundation.Volume;
using CxVisual.Foundation.Geometry;
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
        private Guid? _meshSceneItemId;
        private Guid? _planeSceneItemId;
        public CT3DPlaneFitResult LastMeshPlaneFit {get;private set;}
        private MPWVolumeDisplayMesh _meshPreview;
        public CT3DVoxelBoundaryMeshResult LastVoxelMesh {get;private set;}
        public int MeshDisplayedTriangleCount {get{return _scene.SurfaceMeshTriangleCount;}}
        private MPWVolumeDisplayMesh MeshForPresentation
        {
            get
            {
                if(_state==null||!_meshSceneItemId.HasValue||_meshPreview==null)return null;
                MVI3DSceneItem item;
                return _state.Scene3D.TryGet(_meshSceneItemId.Value,out item)&&item.Visible
                    ? _meshPreview:null;
            }
        }
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
            UpdateMeshPresentation();
        }

        private void UpdateMeshPresentation()
        {
            MPWVolumeDisplayMesh mesh=MeshForPresentation;
            if(mesh!=null)
            {
                _scene.SetSurfaceMesh(mesh);
                if(_gpu!=null)_gpu.SetSurfaceMesh(mesh);
            }
            else
            {
                _scene.ClearSurfaceMesh();
                if(_gpu!=null)_gpu.ClearSurfaceMesh();
            }
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
            _meshSceneItemId=null;
            _planeSceneItemId=null;
            LastMeshPlaneFit=null;
            _meshPreview=null;
            LastSphereFit=null;
            LastVoxelMesh=null;
            _scene.ClearSurfacePoints();
            _scene.ClearSurfaceMesh();
            if(_gpu!=null){_gpu.ClearSurfacePoints();_gpu.ClearSurfaceMesh();}
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

        /// <summary>同步小数据网格入口；所有结果来自真实RAW，不能对R8预览建Mesh。</summary>
        public CT3DVoxelBoundaryMeshResult AnalyzeCurrentVolumeMesh(
            float threshold,int maxTriangles=50000)
        {
            if(_volume==null||_state==null)throw new InvalidOperationException("未载入原始体。");
            if(!_volume.SpatialFrame.IsCalibrated)
                throw new InvalidOperationException("物理Frame未标定，拒绝伪报工业Mesh坐标。");
            FVOVolumeData volume=_volume;
            long epoch=_sphereEpoch;
            using(FVOOriginalScalarLease lease=volume.AcquireOriginalScalarLease())
            {
                CT3DVoxelBoundaryMeshResult mesh=CT3DVoxelBoundaryMeshExtractor.Extract(
                    lease,SurfaceSourceRef(volume),epoch,threshold,maxTriangles,
                    CancellationToken.None);
                PublishVoxelMesh(volume,epoch,mesh);
                return mesh;
            }
        }

        private void PublishVoxelMesh(FVOVolumeData volume,long epoch,
            CT3DVoxelBoundaryMeshResult result)
        {
            if(!Object.ReferenceEquals(volume,_volume)||_state==null||
                epoch!=_sphereEpoch||!Object.ReferenceEquals(result.SpatialFrame,volume.SpatialFrame))
                throw new InvalidOperationException("原值数据或空间版本已变，拒绝显示旧Mesh。");
            MPWVolumeDisplayMesh preview=MPWVolumeSurfaceProjection.ProjectMesh(
                result.Mesh,volume.SpatialFrame,volume.OriginalWidth,volume.OriginalHeight,
                volume.OriginalDepth,50000);
            // 重生成Mesh时必须作废依赖旧Mesh的平面特征和MVI测量引用。
            if(_planeSceneItemId.HasValue)_state.Scene3D.Remove(_planeSceneItemId.Value);
            _planeSceneItemId=null;
            LastMeshPlaneFit=null;
            if(_meshSceneItemId.HasValue)_state.Scene3D.Remove(_meshSceneItemId.Value);
            _meshPreview=preview;
            LastVoxelMesh=result;
            _meshSceneItemId=_state.Scene3D.Add(MVI3DSceneKind.Mesh,
                "ct3d:mesh:"+result.ResultId.ToString("N"),result.SourceRevision,
                "volume:physical-frame");
            _state.Scene3D.Select(_meshSceneItemId);
            UpdateMeshPresentation();
            _status.Text=String.Format(CultureInfo.InvariantCulture,
                "RAW体素面Mesh · {0}三角面 · 面积{1:0.###}mm² · {2}个面壳 · {3} · 非亚体素/未认证",
                result.Mesh.TriangleCount,result.Mesh.SurfaceArea,
                result.Mesh.ConnectedFaceComponentCount,
                result.HasCalibratedClosedGeometry?"单体闭合几何":"多体/边界/拓扑需复核");
            RaiseStateChanged();
        }

        /// <summary>
        /// 用户/算法已选局部Mesh顶点ROI的真正平面拟合入口。
        /// MVI仅注册Measurement引用；专业毫米结果由CT3D保存，绝不重新拟合R8预览。
        /// </summary>
        public CT3DPlaneFitResult AnalyzeCurrentMeshPlane(IReadOnlyList<int> selectedVertexIndices,
            double maxPlanarityRatio=0.08)
        {
            if(_state==null || _volume==null || LastVoxelMesh==null)
                throw new InvalidOperationException("请先从原始RAW建立三角Mesh并选择局部面片。");
            if(LastVoxelMesh.SourceRevision!=_sphereEpoch ||
               !Object.ReferenceEquals(LastVoxelMesh.SpatialFrame,_volume.SpatialFrame))
                throw new InvalidOperationException("原始Volume或空间Frame已变化，旧Mesh不能用于拟合。");
            CT3DPlaneFitResult result=CT3DMeshPlaneAnalysis.FitSelectedVertices(
                LastVoxelMesh,selectedVertexIndices,maxPlanarityRatio);
            if(_planeSceneItemId.HasValue)_state.Scene3D.Remove(_planeSceneItemId.Value);
            LastMeshPlaneFit=result;
            _planeSceneItemId=_state.Scene3D.Add(MVI3DSceneKind.Measurement,
                "ct3d:plane:"+result.ResultId.ToString("N"),result.SourceRevision,
                "volume:physical-frame");
            _state.Scene3D.Select(_planeSceneItemId);
            _status.Text=String.Format(CultureInfo.InvariantCulture,
                "局部平面拟合 · RMS {0:0.######} mm · 最大偏差 {1:0.######} mm · ROI {2}点（未作GD&T判定）",
                result.RmsResidualMm,result.MaxAbsoluteResidualMm,result.SampleCount);
            RaiseStateChanged();
            return result;
        }

        /// <summary>
        /// 便于开发者和测试验证的体素+Z端面快速选区示例，不是通用ROI交互或工业基准面。
        /// 通过SourceFrame逆变换寻找原始网格局部Z最大层，支持倾斜/镜像来源。
        /// </summary>
        public CT3DPlaneFitResult AnalyzeCurrentMeshPlaneTopZ(double maxPlanarityRatio=0.08)
        {
            if(LastVoxelMesh==null)throw new InvalidOperationException("请先生成网格。");
            var mesh=LastVoxelMesh.Mesh;
            FVOVolumeSpatialFrame frame=LastVoxelMesh.SpatialFrame;
            var z=new double[mesh.VertexCount];
            double largest=double.NegativeInfinity;
            for(int i=0;i<mesh.VertexCount;i++)
            {
                FGEPoint3D p=mesh.Vertices[i];
                z[i]=frame.PhysicalToVoxel(p.X,p.Y,p.Z).Z;
                largest=Math.Max(largest,z[i]);
            }
            var top=new List<int>();
            for(int i=0;i<z.Length;i++)
                if(Math.Abs(z[i]-largest)<1e-6)top.Add(i);
            return AnalyzeCurrentMeshPlane(top,maxPlanarityRatio);
        }

        private void StartMeshPlaneTopZFromUi()
        {
            try{AnalyzeCurrentMeshPlaneTopZ();}
            catch(Exception ex){_status.Text="局部端面拟合失败："+ex.Message;}
        }

        /// <summary>后台执行工业RAW网格提取，跟随Volume切换取消并拒绝旧结果发布。</summary>
        private async void StartVoxelMeshFromUi()
        {
            if(_volume==null)return;
            if(_sphereCancellation!=null)
            {
                _status.Text="已有专业Surface/Mesh分析进行中。";
                return;
            }
            float threshold;
            if(!float.TryParse(_sphereThresholdText.Text,NumberStyles.Float,
                CultureInfo.InvariantCulture,out threshold))
            {
                _status.Text="Mesh阈值输入无效。";
                return;
            }
            FVOVolumeData volume=_volume;
            if(!volume.SpatialFrame.IsCalibrated)
            {
                _status.Text="Mesh拒绝：尚无可信源空间标定。";
                return;
            }
            FVOOriginalScalarLease lease;
            try{lease=volume.AcquireOriginalScalarLease();}
            catch(Exception ex){_status.Text=ex.Message;return;}
            CancellationTokenSource current=new CancellationTokenSource();
            _sphereCancellation=current;
            long epoch=_sphereEpoch;
            _status.Text="正在后台从原始RAW构建共享顶点三角网格...";
            try
            {
                CT3DVoxelBoundaryMeshResult result=await Task.Run(delegate
                {
                    using(lease)
                        return CT3DVoxelBoundaryMeshExtractor.Extract(
                            lease,SurfaceSourceRef(volume),epoch,threshold,50000,current.Token);
                });
                if(epoch==_sphereEpoch && Object.ReferenceEquals(_volume,volume) &&
                    !current.IsCancellationRequested)
                    PublishVoxelMesh(volume,epoch,result);
            }
            catch(OperationCanceledException)
            {
                if(epoch==_sphereEpoch)_status.Text="Mesh构建已取消。";
            }
            catch(Exception ex)
            {
                if(epoch==_sphereEpoch)_status.Text="Mesh构建失败："+ex.Message;
            }
            finally
            {
                if(Object.ReferenceEquals(_sphereCancellation,current))
                    _sphereCancellation=null;
                current.Dispose();
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
