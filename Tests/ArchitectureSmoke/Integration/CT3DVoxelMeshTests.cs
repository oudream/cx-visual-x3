using System;
using System.Collections.Generic;
using System.Threading;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Numerics;
using CxVisual.Foundation.Volume;
using CxVisual.Professional.CT3D;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>CT3D真实原值→物理Mesh：以解析真值验证面积、体积、法向、拓扑和失效边界。</summary>
    internal static class CT3DVoxelMeshTests
    {
        public static void Run()
        {
            const int size=12;
            byte[] raw=new byte[size*size*size*2];
            for(int z=5;z<=6;z++)
            for(int y=5;y<=6;y++)
            for(int x=5;x<=6;x++)
                SetUInt16(raw,size,x,y,z,1700);
            var info=new FVORawVolumeInfo(size,size,size,1,1,1,
                FVORawVoxelFormat.UInt16,"mesh-cube");
            using(FVOVolumeData volume=FVORawVolumeLoader.LoadFromMemory(
                raw,info,64,CancellationToken.None))
            {
                volume.SetSpatialFrame(new FVOVolumeSpatialFrame(1,1,1,20,30,40,
                    new double[]{1,0,0,0,1,0,0,0,1},true));
                using(FVOOriginalScalarLease lease=volume.AcquireOriginalScalarLease())
                {
                    CT3DVoxelBoundaryMeshResult result=CT3DVoxelBoundaryMeshExtractor.Extract(
                        lease,"fixture:mesh-cube",3,1000,100,CancellationToken.None);
                    FGETriangleMesh3D mesh=result.Mesh;
                    Check(mesh.TriangleCount==48 && mesh.VertexCount==26,
                        "2x2x2实体必须由24个外露四边形构成48个三角形，顶点跨面复用");
                    Check(mesh.ConnectedFaceComponentCount==1 &&
                          mesh.IsClosedOrientedManifold && mesh.BoundaryEdgeCount==0 &&
                          mesh.NonManifoldEdgeCount==0 && mesh.InconsistentWindingEdgeCount==0,
                        "规则六邻域表面必须形成法向一致的封闭流形");
                    Check(Math.Abs(mesh.SurfaceArea-24)<1e-9 &&
                          mesh.EnclosedVolume.HasValue && mesh.SignedEnclosedVolume.HasValue &&
                          mesh.SignedEnclosedVolume.Value>0 &&
                          Math.Abs(mesh.EnclosedVolume.Value-8)<1e-9,
                        "体素外露表面解析面积24平方毫米、体积8立方毫米");
                    // B包CPU真实三角拾取：必须命中原值毫米网格的最近面而非GPU预览图。
                    FGEMeshRayHit rayHit;
                    Check(FGEMeshRayIntersector.TryIntersect(mesh,
                        new FGEPoint3D(25,35,50),new FGEVector3D(0,0,-1),
                        out rayHit,CancellationToken.None)&&
                        rayHit.TriangleIndex>=0&&rayHit.TriangleIndex<mesh.TriangleCount&&
                        Math.Abs(rayHit.Point.Z-46.5)<1e-9&&
                        Math.Abs(rayHit.Distance-3.5)<1e-9&&
                        Math.Abs(rayHit.BarycentricU+rayHit.BarycentricV+rayHit.BarycentricW-1)<1e-9,
                        "毫米Mesh射线应命中离相机最近的真实三角面且重心坐标有效");
                    Check(!FGEMeshRayIntersector.TryIntersect(mesh,
                        new FGEPoint3D(25,35,50),new FGEVector3D(0,0,1),
                        out rayHit,CancellationToken.None)&&rayHit==null,
                        "背离网格的射线不可虚构命中");
                    bool invalidRay=false,cancelledRay=false;
                    try{FGEMeshRayIntersector.TryIntersect(mesh,
                        new FGEPoint3D(25,35,50),new FGEVector3D(0,0,0),
                        out rayHit,CancellationToken.None);}
                    catch(ArgumentOutOfRangeException){invalidRay=true;}
                    try{FGEMeshRayIntersector.TryIntersect(mesh,
                        new FGEPoint3D(25,35,50),new FGEVector3D(0,0,-1),
                        out rayHit,new CancellationToken(true));}
                    catch(OperationCanceledException){cancelledRay=true;}
                    Check(invalidRay&&cancelledRay,
                        "退化方向和取消不得返回伪造的专业Mesh拾取结果");
                    // 在FGE网格中选局部 +Z 面片ROI，不把整个封闭立方体拟合成平面。
                    var topFace=new List<int>();
                    var collinear=new List<int>();
                    for(int index=0;index<mesh.VertexCount;index++)
                    {
                        FGEPoint3D v=mesh.Vertices[index];
                        if(Math.Abs(v.Z-mesh.BoundsMax.Z)<1e-9)
                        {
                            topFace.Add(index);
                            if(Math.Abs(v.Y-mesh.BoundsMin.Y)<1e-9)
                                collinear.Add(index);
                        }
                    }
                    CT3DPlaneFitResult plane=CT3DMeshPlaneAnalysis.FitSelectedVertices(
                        result,topFace,0.01,1000,CancellationToken.None);
                    Check(topFace.Count==9 && plane.SampleCount==9 &&
                          Math.Abs(plane.PointMm.Z-46.5)<1e-9 &&
                          Math.Abs(plane.Normal.Z-1)<1e-9 &&
                          plane.RmsResidualMm<1e-9 &&
                          plane.MaxAbsoluteResidualMm<1e-9 &&
                          plane.SourceMeshResultId==result.ResultId &&
                          plane.SourceRevision==result.SourceRevision,
                        "真实UInt16外露+Z面片应由FNU PCA-SVD正确拟合出毫米平面及来源证据");
                    Check(Math.Abs(plane.SignedDistanceMm(
                            new FVOPhysicalPoint3D(25,35,47.5),result.SpatialFrame)-1)<1e-8 &&
                          Math.Abs(plane.SignedDistanceMm(
                            new FVOPhysicalPoint3D(25,35,45.5),result.SpatialFrame)+1)<1e-8 &&
                          Math.Abs(plane.AbsoluteDistanceMm(
                            new FVOPhysicalPoint3D(25,35,45.5),result.SpatialFrame)-1)<1e-8,
                        "同一来源毫米Frame中拟合平面应输出正负1mm偏差和1mm绝对距离");
                    bool frameRejected=false;
                    try
                    {
                        plane.SignedDistanceMm(new FVOPhysicalPoint3D(25,35,47.5),
                            FVOVolumeSpatialFrame.UncalibratedAxisAligned(1,1,1));
                    }
                    catch(InvalidOperationException){frameRejected=true;}
                    Check(frameRejected,
                        "不同空间Frame未经配准不得直接比较点到平面毫米偏差");
                    bool notPlanar=false;
                    var whole=new List<int>();
                    for(int index=0;index<mesh.VertexCount;index++)whole.Add(index);
                    try {CT3DMeshPlaneAnalysis.FitSelectedVertices(
                        result,whole,0.01,1000,CancellationToken.None);}
                    catch(InvalidOperationException){notPlanar=true;}
                    Check(notPlanar,"不能把全部立方体表面误当单一平面ROI");
                    bool lineRejected=false;
                    try {CT3DMeshPlaneAnalysis.FitSelectedVertices(
                        result,collinear,0.01,1000,CancellationToken.None);}
                    catch(InvalidOperationException){lineRejected=true;}
                    Check(lineRejected && collinear.Count==3,
                        "三点共线的几何ROI必须因秩亏被SVD拒绝");
                    bool repeatedRejected=false;
                    try {CT3DMeshPlaneAnalysis.FitSelectedVertices(
                        result,new[]{topFace[0],topFace[0],topFace[1]},
                        0.01,1000,CancellationToken.None);}
                    catch(ArgumentException){repeatedRejected=true;}
                    Check(repeatedRejected,"重复顶点不得冒充多个独立平面采样");
                    var arbitrary=new double[,]{
                        {0,0,4},{1,0,6},{2,0,8},
                        {0,1,7},{1,1,9},{2,1,11},
                        {0,2,10},{1,2,12},{2,2,14}
                    };
                    FNUPcaPlaneResult inclined=FNULinearAlgebra.FitPlaneBySvd(arbitrary);
                    double[] tiltNormal=inclined.Normal;
                    Check(inclined.RmsPerpendicularResidual<1e-9 &&
                          Math.Abs(tiltNormal[0]+2*tiltNormal[2])<1e-9 &&
                          Math.Abs(tiltNormal[1]+3*tiltNormal[2])<1e-9,
                        "FNU SVD必须支持任意倾斜平面，不依赖Z=ax+by固定方向");
                    Check(result.HasCalibratedClosedGeometry && !result.TouchesVolumeBoundary &&
                          result.SourceRef=="fixture:mesh-cube" && result.SourceRevision==3 &&
                          result.ResultId!=Guid.Empty,
                        "专业Mesh保留原值来源、版本、标定/边界状态");
                    Check(Math.Abs(mesh.BoundsMin.X-24.5)<1e-9 &&
                          Math.Abs(mesh.BoundsMax.Z-46.5)<1e-9,
                        "物理原点和体素中心±0.5的角点映射必须正确");
                    foreach(FGEVector3D normal in mesh.VertexNormals)
                        Check(Math.Abs(normal.Length-1)<1e-9,
                            "闭合规则网格全部顶点法向必须为单位向量");
                    bool budgetRejected=false;
                    try
                    {
                        CT3DVoxelBoundaryMeshExtractor.Extract(
                            lease,"fixture:mesh-cube",3,1000,12,CancellationToken.None);
                    }
                    catch(InvalidOperationException){budgetRejected=true;}
                    Check(budgetRejected,"预算不足不能静默裁掉网格面片");
                    bool noSurface=false;
                    try
                    {
                        CT3DVoxelBoundaryMeshExtractor.Extract(
                            lease,"fixture:mesh-cube",3,3000,100,CancellationToken.None);
                    }
                    catch(InvalidOperationException){noSurface=true;}
                    Check(noSurface,"无阈值实体必须拒绝伪造Mesh");
                    using(CancellationTokenSource stopped=new CancellationTokenSource())
                    {
                        stopped.Cancel();
                        bool canceled=false;
                        try
                        {
                            CT3DVoxelBoundaryMeshExtractor.Extract(
                                lease,"fixture:mesh-cube",3,1000,100,stopped.Token);
                        }
                        catch(OperationCanceledException){canceled=true;}
                        Check(canceled,"RAW三维网格计算必须传播取消信号");
                    }
                }
                volume.SetSpatialFrame(new FVOVolumeSpatialFrame(1,1,1,20,30,40,
                    new double[]{-1,0,0,0,1,0,0,0,1},true));
                using(FVOOriginalScalarLease mirrored=volume.AcquireOriginalScalarLease())
                {
                    FGETriangleMesh3D mesh=CT3DVoxelBoundaryMeshExtractor.Extract(
                        mirrored,"fixture:mirror",4,1000,100,CancellationToken.None).Mesh;
                    Check(mesh.IsClosedOrientedManifold && mesh.EnclosedVolume.HasValue && mesh.SignedEnclosedVolume.HasValue &&
                          mesh.SignedEnclosedVolume.Value>0 &&
                          Math.Abs(mesh.EnclosedVolume.Value-8)<1e-9,
                        "左手镜像来源必须翻转三角绕序，保持几何法向及体积证据正确");
                }
            }
            var anisotropicInfo=new FVORawVolumeInfo(size,size,size,2,3,4,
                FVORawVoxelFormat.UInt16,"mesh-anisotropic");
            using(FVOVolumeData anisotropic=FVORawVolumeLoader.LoadFromMemory(
                raw,anisotropicInfo,64,CancellationToken.None))
            {
                anisotropic.SetSpatialFrame(new FVOVolumeSpatialFrame(2,3,4,100,200,300,
                    new double[]{0,-1,0,1,0,0,0,0,1},true));
                using(FVOOriginalScalarLease lease=anisotropic.AcquireOriginalScalarLease())
                {
                    FGETriangleMesh3D mesh=CT3DVoxelBoundaryMeshExtractor.Extract(
                        lease,"fixture:anisotropic-rotated",5,1000,100,
                        CancellationToken.None).Mesh;
                    Check(Math.Abs(mesh.SurfaceArea-208)<1e-8 &&
                          mesh.EnclosedVolume.HasValue && mesh.SignedEnclosedVolume.HasValue &&
                          mesh.SignedEnclosedVolume.Value>0 &&
                          Math.Abs(mesh.EnclosedVolume.Value-192)<1e-8 &&
                          mesh.IsClosedOrientedManifold,
                        "各向异性2×3×4mm及90度方向旋转下必须保留解析面积208与体积192");
                }
            }

            // 两个分离的完整工件：每条边合法、各自闭合，但不能作为一个实体计量。
            byte[] twoObjects=new byte[size*size*size*2];
            SetUInt16(twoObjects,size,3,3,3,1700);
            SetUInt16(twoObjects,size,8,8,8,1700);
            using(FVOVolumeData source=FVORawVolumeLoader.LoadFromMemory(
                twoObjects,info,64,CancellationToken.None))
            {
                source.SetSpatialFrame(new FVOVolumeSpatialFrame(1,1,1,0,0,0,
                    new double[]{1,0,0,0,1,0,0,0,1},true));
                using(FVOOriginalScalarLease lease=source.AcquireOriginalScalarLease())
                {
                    CT3DVoxelBoundaryMeshResult result=CT3DVoxelBoundaryMeshExtractor.Extract(
                        lease,"fixture:two-islands",6,1000,50,CancellationToken.None);
                    Check(result.Mesh.ConnectedFaceComponentCount==2 &&
                          result.Mesh.IsClosedOrientedManifold &&
                          result.Mesh.TriangleCount==24 &&
                          result.Mesh.EnclosedVolume.HasValue &&
                          Math.Abs(result.Mesh.EnclosedVolume.Value-2)<1e-9 &&
                          !result.HasCalibratedClosedGeometry,
                        "两个完全闭合实体必须保留2个面壳身份，不能误称单工件计量结果");
                }
            }

            // 网格是独立的通用几何类型，错误索引/退化三角形必须被FGE拒绝。
            bool invalid=false;
            try
            {
                new FGETriangleMesh3D(
                    new[]{new FGEPoint3D(0,0,0),new FGEPoint3D(1,0,0),
                        new FGEPoint3D(2,0,0)},new[]{0,1,2});
            }
            catch(ArgumentException){invalid=true;}
            Check(invalid,"FGE必须拒绝零面积三角形");
            bool isolatedRejected=false;
            try
            {
                new FGETriangleMesh3D(
                    new[]{new FGEPoint3D(0,0,0),new FGEPoint3D(1,0,0),
                        new FGEPoint3D(0,1,0),new FGEPoint3D(999,999,999)},
                    new[]{0,1,2});
            }
            catch(ArgumentException){isolatedRejected=true;}
            Check(isolatedRejected,"FGE必须拒绝污染包围盒及ROI的孤立顶点");
            var open=new FGETriangleMesh3D(
                new[]{new FGEPoint3D(0,0,0),new FGEPoint3D(1,0,0),
                    new FGEPoint3D(0,1,0)},new[]{0,1,2});
            Check(open.NonManifoldVertexCount==0,
                "单连通的开放三角面不应误报为bow-tie顶点");
            Check(open.BoundaryEdgeCount==3 && !open.EnclosedVolume.HasValue,
                "开放曲面不得伪报封闭体积");
            // 两个闭合四面体只在一个顶点接触：每条边仍恰好被使用两次，
            // 但该顶点的邻接三角面分属两个不连通fan，禁止标记为闭合流形。
            var bowTie=new FGETriangleMesh3D(
                new[]{
                    new FGEPoint3D(0,0,0),
                    new FGEPoint3D(1,0,0),new FGEPoint3D(0,1,0),
                    new FGEPoint3D(0,0,1),new FGEPoint3D(-1,0,0),
                    new FGEPoint3D(0,-1,0),new FGEPoint3D(0,0,-1)
                },
                new[]{
                    0,2,1, 0,1,3, 0,3,2, 1,2,3,
                    0,4,5, 0,6,4, 0,5,6, 4,6,5
                });
            Check(bowTie.BoundaryEdgeCount==0 && bowTie.NonManifoldEdgeCount==0 &&
                  bowTie.InconsistentWindingEdgeCount==0 &&
                  bowTie.NonManifoldVertexCount==1 &&
                  bowTie.ConnectedFaceComponentCount==2 &&
                  !bowTie.IsClosedOrientedManifold && !bowTie.EnclosedVolume.HasValue,
                "仅顶点接触的两个封闭壳必须被判为bow-tie非流形而不是可计量闭合体");
            byte[] boundary=new byte[size*size*size*2];
            SetUInt16(boundary,size,0,0,0,1700);
            using(FVOVolumeData touch=FVORawVolumeLoader.LoadFromMemory(
                boundary,info,64,CancellationToken.None))
            {
                touch.SetSpatialFrame(new FVOVolumeSpatialFrame(1,1,1,0,0,0,
                    new double[]{1,0,0,0,1,0,0,0,1},true));
                using(FVOOriginalScalarLease lease=touch.AcquireOriginalScalarLease())
                {
                    var result=CT3DVoxelBoundaryMeshExtractor.Extract(
                        lease,"fixture:truncated",1,1000,50,CancellationToken.None);
                    Check(result.TouchesVolumeBoundary && !result.HasCalibratedClosedGeometry,
                        "触及原数据边界的离散闭面不可直接用于工业计量");
                }
            }
            // Float32非有限原始值：不允许NaN在阈值比较中被隐式解释为实体。
            byte[] floatRaw=new byte[size*size*size*4];
            int badIndex=4*(5+5*size+5*size*size);
            Buffer.BlockCopy(BitConverter.GetBytes(float.NaN),0,floatRaw,badIndex,4);
            var floatInfo=new FVORawVolumeInfo(size,size,size,1,1,1,
                FVORawVoxelFormat.Float32,"invalid-raw-f32");
            using(FVOVolumeData badFloat=FVORawVolumeLoader.LoadFromMemory(
                floatRaw,floatInfo,64,CancellationToken.None))
            {
                using(FVOOriginalScalarLease original=badFloat.AcquireOriginalScalarLease())
                {
                    bool meshRejected=false,surfaceRejected=false;
                    try
                    {
                        CT3DVoxelBoundaryMeshExtractor.Extract(
                            original,"fixture:nan",9,1f,100,CancellationToken.None);
                    }
                    catch(InvalidOperationException){meshRejected=true;}
                    try
                    {
                        CT3DSurfaceSphereAnalysis.SampleBoundary(
                            original,"fixture:nan",9,1f,1,100,CancellationToken.None);
                    }
                    catch(InvalidOperationException){surfaceRejected=true;}
                    Check(meshRejected && surfaceRejected,
                        "Float32非有限原始值必须被Mesh/离散球拟合输入共同拒绝");
                }
            }
            Console.WriteLine("PASS CT3D UInt16 physical voxel mesh, topology, mirrored normals and negative cases");
            VerifySlicePrism(raw);
        }

        private static void VerifySlicePrism(byte[] raw)
        {
            // 旋转+镜像+各向异性：框选与毫米法向范围不能按世界XYZ直接比较。
            var anisotropic=new FVORawVolumeInfo(12,12,12,.5f,2,3,FVORawVoxelFormat.UInt16,"roi-anisotropic");
            using(var volume=FVORawVolumeLoader.LoadFromMemory(raw,anisotropic,64,CancellationToken.None))
            {
                volume.SetSpatialFrame(new FVOVolumeSpatialFrame(.5,2,3,1000000,200,300,
                    new double[]{0,-1,0,1,0,0,0,0,-1},true));
                using(var lease=volume.AcquireOriginalScalarLease())
                {
                    var mesh=CT3DVoxelBoundaryMeshExtractor.Extract(lease,"fixture:roi",1,1000,100,CancellationToken.None);
                    foreach(var axis in new[]{FVOSlicePlane.XY,FVOSlicePlane.XZ,FVOSlicePlane.YZ})
                    {
                        var roi=FVOSliceImageBuilder.RectangleToPhysical(volume,6,6,6,axis,0,0,
                            FVOSliceImageBuilder.GetImageWidth(volume,axis)-1,FVOSliceImageBuilder.GetImageHeight(volume,axis)-1);
                        double spacing=axis==FVOSlicePlane.XY?3:axis==FVOSlicePlane.XZ?2:.5;
                        var indices=CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,roi,.49*spacing,.51*spacing);
                        var fitted=CT3DMeshPlaneAnalysis.FitSelectedVertices(mesh,indices,.01,1000);
                        var center=mesh.SpatialFrame.PhysicalToVoxel(fitted.PointMm.X,fitted.PointMm.Y,fitted.PointMm.Z);
                        double normal=axis==FVOSlicePlane.XY?center.Z:axis==FVOSlicePlane.XZ?center.Y:center.X;
                        Check(indices.Count==9&&fitted.SampleCount==9&&Math.Abs(normal-6.5)<1e-7&&fitted.RmsResidualMm<1e-7,
                            "三正交切片ROI须按原值Frame选中九个端面顶点并拟合毫米平面");
                        bool invalid=false,empty=false,cancelled=false;
                        try{CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,roi,2,1);}catch(ArgumentOutOfRangeException){invalid=true;}
                        try{CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,roi,100,101);}catch(InvalidOperationException){empty=true;}
                        try{CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,roi,0,1,new CancellationToken(true));}
                        catch(OperationCanceledException){cancelled=true;}
                        Check(invalid&&empty&&cancelled,"错误毫米范围、空ROI和取消不能制造拟合输入");
                    }
                    var degenerate=FVOSliceImageBuilder.RectangleToPhysical(volume,6,6,6,FVOSlicePlane.XY,0,0,0,2);
                    bool line=false;
                    try{CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,degenerate,0,1);}catch(ArgumentException){line=true;}
                    Check(line,"退化线选区不冒充有面积的矩形ROI");
                    var previous=FVOSliceImageBuilder.RectangleToPhysical(volume,6,6,6,FVOSlicePlane.XY,0,0,2,6);
                    volume.SetSpatialFrame(new FVOVolumeSpatialFrame(.5,2,3,0,0,0,new double[]{1,0,0,0,1,0,0,0,1},true));
                    var foreign=FVOSliceImageBuilder.RectangleToPhysical(volume,6,6,6,FVOSlicePlane.XY,0,0,2,6);
                    bool frameRejected=false;
                    try{CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,foreign,0,1);}catch(InvalidOperationException){frameRejected=true;}
                    Check(frameRejected&&Object.ReferenceEquals(previous.SpatialFrame,mesh.SpatialFrame),"标定改变后拒绝把新Frame选区用到旧Mesh");
                }
            }
            Console.WriteLine("PASS CT3D physical slice ROI prism to original mesh plane across rotated anisotropic frames");
            const int n=84;var large=new byte[n*n*n*2];
            for(int z=2;z<82;z++)for(int y=2;y<82;y++)for(int x=2;x<82;x++)SetUInt16(large,n,x,y,z,1700);
            using(var volume=FVORawVolumeLoader.LoadFromMemory(large,
                new FVORawVolumeInfo(n,n,n,1,1,1,FVORawVoxelFormat.UInt16,"roi-budget"),64,CancellationToken.None))
            {
                volume.SetSpatialFrame(new FVOVolumeSpatialFrame(1,1,1,0,0,0,new double[]{1,0,0,0,1,0,0,0,1},true));
                using(var lease=volume.AcquireOriginalScalarLease())
                {
                    var mesh=CT3DVoxelBoundaryMeshExtractor.Extract(lease,"fixture:roi-budget",1,1000,100000,CancellationToken.None);
                    var roi=FVOSliceImageBuilder.RectangleToPhysical(volume,42,42,42,FVOSlicePlane.XY,
                        0,0,FVOSliceImageBuilder.GetImageWidth(volume,FVOSlicePlane.XY)-1,
                        FVOSliceImageBuilder.GetImageHeight(volume,FVOSlicePlane.XY)-1);
                    bool budget=false;
                    try{CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,roi,-100,100);}catch(InvalidOperationException){budget=true;}
                    Check(mesh.Mesh.VertexCount>30000&&budget,"真实闭合大Mesh选区超过30000顶点时明确失败，不能静默截断");
                }
            }
        }

        private static void SetUInt16(byte[] data,int width,int x,int y,int z,ushort value)
        {
            int index=2*(x+width*y+width*width*z);
            data[index]=(byte)value;
            data[index+1]=(byte)(value>>8);
        }

        private static void Check(bool actual,string message)
        {
            if(!actual)throw new InvalidOperationException("CT3D网格验收失败："+message);
        }
    }
}
