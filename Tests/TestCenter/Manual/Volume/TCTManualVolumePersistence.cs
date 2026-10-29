using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Professional.CT3D;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    /// <summary>正式CT3D/MEM API的WPF消费入口；文件/算法/制品编码均不在测试页面实现。</summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private int[] _planeRoi;
        private double _planePlanarity=0.08;
        private long _caseRequestEpoch;
        private string _professionalProjectPath,_professionalStoreToken;
        private CT3DAssetReference _professionalSource;
        public CT3DAnalysisCase LastOpenedProfessionalCase {get;private set;}
        private bool CanSaveProfessionalCase {get{return _volume!=null&&_volume.HasRawSource&&
            ((LastVoxelMesh!=null&&LastMeshPlaneFit!=null&&_planeRoi!=null)||
             (LastSurfaceSamples!=null&&LastSphereFit!=null));}}

        public async Task SaveProfessionalCaseAsync(string projectFile,CancellationToken cancellation)
        {
            Dispatcher.VerifyAccess();
            if(!CanSaveProfessionalCase||!File.Exists(_volume.SourceFilePath))
                throw new InvalidOperationException("请先加载真实RAW并完成局部平面或Surface/球分析。");
            bool saveSphere=LastSurfaceSamples!=null&&LastSphereFit!=null&&
                (LastMeshPlaneFit==null||_state.Scene3D.SelectedId==_surfaceSceneItemId||
                 _state.Scene3D.SelectedId==_sphereSceneItemId);
            string full=Path.GetFullPath(projectFile),root=Path.GetDirectoryName(full);
            string expectedStore=string.Equals(full,_professionalProjectPath,StringComparison.OrdinalIgnoreCase)
                ? _professionalStoreToken:null;
            if(File.Exists(full)&&expectedStore==null)
                throw new IOException("目标已存在且不是当前打开的案例，请先重开或选择新文件名。");
            long capturedEpoch=_sphereEpoch;
            var capturedVolume=_volume;
            string raw=_volume.SourceFilePath;
            var frame=_volume.SpatialFrame;
            var info=new FVORawVolumeInfo(_volume.OriginalWidth,_volume.OriginalHeight,_volume.OriginalDepth,
                _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ,_volume.SourceFormat.Value,raw);
            var previous=LastVoxelMesh;
            var previousPlane=LastMeshPlaneFit;
            var previousSurface=LastSurfaceSamples;var previousSphere=LastSphereFit;
            if(!Object.ReferenceEquals(_volume.SpatialFrame,
                saveSphere?previousSurface.SpatialFrame:previous.SpatialFrame))
                throw new InvalidOperationException("来源Frame已变化，请重新分析后保存。");
            var capturedScene=_state.Scene3D;
            long capturedSceneRevision=capturedScene.Revision;
            var priorSource=_professionalSource;
            var roi=_planeRoi==null?null:(int[])_planeRoi.Clone();double ratio=_planePlanarity;
            string savedToken=null;
            var saved=await Task.Run(()=>
            {
                var source=CT3DAssetReference.Import(root,raw,info,frame,
                    priorSource==null?1:priorSource.Revision,cancellation,
                    priorSource==null?(Guid?)null:priorSource.Id);
                if(priorSource!=null&&source.Signature!=priorSource.Signature)
                    source=CT3DAssetReference.Capture(root,source.RelativePath,info,frame,
                        checked(priorSource.Revision+1),cancellation,priorSource.Id);
                var analysis=saveSphere?CT3DAnalysisCase.AnalyzeSphere(root,source,previousSurface.Threshold,
                    previousSurface.SampleStep,previousSurface.MaximumPoints,cancellation,previousSphere.MaximumConditionNumber):
                    CT3DAnalysisCase.Analyze(root,source,previous.Threshold,
                        Math.Max(12,previous.Mesh.TriangleCount),mesh=>roi,cancellation,ratio);
                // 保存的是当前视图所选几何，源文件变化不能让旧ROI悄悄指向另一片网格。
                if(saveSphere)
                {
                    if(!previousSurface.Points.SequenceEqual(analysis.Surface.Points))
                        throw new InvalidOperationException("RAW与当前Surface点集不一致，请重新分析。");
                }
                else if(previous.Mesh.VertexCount!=analysis.Mesh.Mesh.VertexCount ||
                    !previous.Mesh.TriangleIndices.SequenceEqual(analysis.Mesh.Mesh.TriangleIndices) ||
                    previous.Mesh.Vertices.Where((p,i)=>p.X!=analysis.Mesh.Mesh.Vertices[i].X ||
                        p.Y!=analysis.Mesh.Mesh.Vertices[i].Y || p.Z!=analysis.Mesh.Mesh.Vertices[i].Z).Any())
                    throw new InvalidOperationException("RAW与当前分析几何不一致，请重新加载并选取ROI。");
                using(var engineering=expectedStore==null?MEMEngineering.Create("CT3D专业分析"):MEMEngineering.Open(full))
                {
                    if(expectedStore!=null&&engineering.StoreToken!=expectedStore)
                        throw new MEMStoreConflictException(full);
                    var owner=expectedStore==null?engineering.AddPage("三维分析").RootContent:
                        engineering.Project.DescendantsAndSelf().OfType<MEMContent>()
                        .Single(c=>c.Aspects.Any(a=>a.ProviderId==CT3DAnalysisPersistence.ProviderId));
                    CT3DAnalysisPersistence.Commit(engineering,owner,engineering.Revision,root,analysis,cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    engineering.SaveAsAsync(full,cancellation).GetAwaiter().GetResult();
                    savedToken=engineering.StoreToken;
                }
                return analysis;
            },cancellation);
            bool sameVolume=capturedEpoch==_sphereEpoch&&Object.ReferenceEquals(_volume,capturedVolume);
            bool restore=sameVolume&&Object.ReferenceEquals(_volume.SpatialFrame,frame)&&
                Object.ReferenceEquals(LastVoxelMesh,previous)&&Object.ReferenceEquals(LastMeshPlaneFit,previousPlane)&&
                Object.ReferenceEquals(LastSurfaceSamples,previousSurface)&&Object.ReferenceEquals(LastSphereFit,previousSphere)&&
                _state!=null&&Object.ReferenceEquals(_state.Scene3D,capturedScene)&&
                capturedScene.Revision==capturedSceneRevision;
            if(sameVolume)
            {
                // 磁盘提交已完成，应接纳新的保存Token；Frame/ROI变化时只更新保存收据，
                // 不让旧快照替换当前专业结果。后续保存仍可沿用资产身份并递增来源修订。
                if(restore)RestoreProfessionalCase(saved);
                _professionalSource=saved.Source;
                _professionalProjectPath=full;_professionalStoreToken=savedToken;
            }
            _status.Text=(restore?"专业案例已保存（工业不确定度未评定）：":
                "专业快照已保存；当前视图已改变，未覆盖当前分析：")+full;
        }

        public async Task OpenProfessionalCaseAsync(string projectFile,CancellationToken cancellation)
        {
            Dispatcher.VerifyAccess();
            string full=Path.GetFullPath(projectFile),root=Path.GetDirectoryName(full);
            long request=++_caseRequestEpoch,volumeEpoch=_sphereEpoch;
            string openedToken=null;
            FVOVolumeData loaded=null;
            try
            {
                var analysis=await Task.Run(()=>
                {
                    using(var engineering=MEMEngineering.Open(full))
                    {
                        var owners=engineering.Project.DescendantsAndSelf().OfType<MEMContent>()
                            .Where(c=>c.Aspects.Any(a=>a.ProviderId==CT3DAnalysisPersistence.ProviderId)).ToArray();
                        if(owners.Length!=1)throw new InvalidDataException("该入口需要恰好一个三维分析案例。");
                        var value=CT3DAnalysisPersistence.Open(engineering,engineering.GetAddress(owners[0]),root,cancellation);
                        loaded=value.Source.LoadVolume(root,cancellation);
                        openedToken=engineering.StoreToken;
                        return value;
                    }
                },cancellation);
                cancellation.ThrowIfCancellationRequested();
                if(request!=_caseRequestEpoch||volumeEpoch!=_sphereEpoch)
                    throw new OperationCanceledException("视图已切换，丢弃旧案例读取结果。");
                LoadVolume(loaded);loaded=null;
                RestoreProfessionalCase(analysis);
                _professionalProjectPath=full;_professionalStoreToken=openedToken;
            }
            finally{if(loaded!=null)loaded.Dispose();}
        }

        private void RestoreProfessionalCase(CT3DAnalysisCase analysis)
        {
            if(analysis.Kind==CT3DAnalysisKind.SurfaceSphere)
            {
                RestoreSurfaceSphereCase(analysis);
                return;
            }
            _meshPreview=CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering.MPWVolumeSurfaceProjection.ProjectMesh(
                analysis.Mesh.Mesh,_volume.SpatialFrame,_volume.OriginalWidth,
                _volume.OriginalHeight,_volume.OriginalDepth,50000);
            if(_meshSceneItemId.HasValue)_state.Scene3D.Remove(_meshSceneItemId.Value);
            if(_planeSceneItemId.HasValue)_state.Scene3D.Remove(_planeSceneItemId.Value);
            LastVoxelMesh=analysis.Mesh;LastMeshPlaneFit=analysis.Plane;
            foreach(var item in _state.Scene3D.Snapshot().Where(i=>i.Kind==MVI3DSceneKind.Volume))
                _state.Scene3D.Remove(item.Id);
            _state.Scene3D.Add(MVI3DSceneKind.Volume,analysis.Source.SourceRef,
                analysis.Source.Revision,analysis.Source.Signature);
            _planeRoi=analysis.SelectedVertexIndices.ToArray();_planePlanarity=analysis.MaxPlanarityRatio;
            _meshSceneItemId=_state.Scene3D.Add(MVI3DSceneKind.Mesh,
                "ct3d:mesh:"+analysis.Mesh.ResultId.ToString("N"),analysis.Source.Revision,analysis.Source.Signature);
            _planeSceneItemId=_state.Scene3D.Add(MVI3DSceneKind.Measurement,
                "ct3d:plane:"+analysis.Plane.ResultId.ToString("N"),analysis.Source.Revision,analysis.Source.Signature);
            _state.Scene3D.Select(_planeSceneItemId);
            LastOpenedProfessionalCase=analysis;
            _professionalSource=analysis.Source;
            UpdateMeshPresentation();
            _status.Text="已重开专业案例：毫米/Frame/ROI/结果身份已校验；工业不确定度未评定。";
            RaiseStateChanged();
        }

        private void RestoreSurfaceSphereCase(CT3DAnalysisCase analysis)
        {
            var points=CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering.MPWVolumeSurfaceProjection.Project(
                analysis.Surface.Points,_volume.SpatialFrame,_volume.OriginalWidth,_volume.OriginalHeight,
                _volume.OriginalDepth,8000);
            if(_surfaceSceneItemId.HasValue)_state.Scene3D.Remove(_surfaceSceneItemId.Value);
            if(_sphereSceneItemId.HasValue)_state.Scene3D.Remove(_sphereSceneItemId.Value);
            _surfacePreviewPoints=points;LastSurfaceSamples=analysis.Surface;LastSphereFit=analysis.Sphere;
            foreach(var item in _state.Scene3D.Snapshot().Where(i=>i.Kind==MVI3DSceneKind.Volume))
                _state.Scene3D.Remove(item.Id);
            _state.Scene3D.Add(MVI3DSceneKind.Volume,analysis.Source.SourceRef,analysis.Source.Revision,analysis.Source.Signature);
            _surfaceSceneItemId=_state.Scene3D.Add(MVI3DSceneKind.Surface,
                "ct3d:surface:"+analysis.Surface.ResultId.ToString("N"),analysis.Source.Revision,analysis.Source.Signature);
            _sphereSceneItemId=_state.Scene3D.Add(MVI3DSceneKind.Measurement,
                "ct3d:sphere:"+analysis.Sphere.ResultId.ToString("N"),analysis.Source.Revision,analysis.Source.Signature);
            _state.Scene3D.Select(_surfaceSceneItemId);
            LastOpenedProfessionalCase=analysis;_professionalSource=analysis.Source;
            UpdateSurfacePresentation();
            _status.Text="已恢复Surface与毫米球结果/来源/残差；工业不确定度未评定。";
            RaiseStateChanged();
        }

        private async void SaveProfessionalCaseDialog()
        {
            var dialog=new SaveFileDialog{Filter="CxVisual工程 (*.cxproj)|*.cxproj",FileName="CT3D-Analysis.cxproj"};
            if(dialog.ShowDialog()!=true)return;
            try{await SaveProfessionalCaseAsync(dialog.FileName,CancellationToken.None);}
            catch(Exception ex){_status.Text="专业案例保存失败："+ex.Message;}
        }
        private async void OpenProfessionalCaseDialog()
        {
            var dialog=new OpenFileDialog{Filter="CxVisual工程 (*.cxproj)|*.cxproj"};
            if(dialog.ShowDialog()!=true)return;
            try{await OpenProfessionalCaseAsync(dialog.FileName,CancellationToken.None);}
            catch(OperationCanceledException){_status.Text="专业案例读取已取消。";}
            catch(Exception ex){_status.Text="专业案例重开失败："+ex.Message;}
        }
    }
}
