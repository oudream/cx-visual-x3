using System;
using System.Collections.Generic;
using System.Threading;
using CxVisual.Foundation.Geometry;
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
                    Check(mesh.IsClosedOrientedManifold && mesh.BoundaryEdgeCount==0 &&
                          mesh.NonManifoldEdgeCount==0 && mesh.InconsistentWindingEdgeCount==0,
                        "规则六邻域表面必须形成法向一致的封闭流形");
                    Check(Math.Abs(mesh.SurfaceArea-24)<1e-9 &&
                          mesh.EnclosedVolume.HasValue && mesh.SignedEnclosedVolume.HasValue &&
                          mesh.SignedEnclosedVolume.Value>0 &&
                          Math.Abs(mesh.EnclosedVolume.Value-8)<1e-9,
                        "体素外露表面解析面积24平方毫米、体积8立方毫米");
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
            var open=new FGETriangleMesh3D(
                new[]{new FGEPoint3D(0,0,0),new FGEPoint3D(1,0,0),
                    new FGEPoint3D(0,1,0)},new[]{0,1,2});
            Check(open.BoundaryEdgeCount==3 && !open.EnclosedVolume.HasValue,
                "开放曲面不得伪报封闭体积");
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
            Console.WriteLine("PASS CT3D UInt16 physical voxel mesh, topology, mirrored normals and negative cases");
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
