using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Numerics;
using CxVisual.Mechanism.Core;

namespace CxVisual.Professional.CT3D
{
    /// <summary>MEM专业Aspect + 内容寻址外部Mesh；工程编辑/保存/Undo仍完全由MEM执行。</summary>
    public static class CT3DAnalysisPersistence
    {
        public const string ProviderId="ct3d.analysis.case";
        private const int Magic=0x33444358;
        private const int Schema=1;

        /// <summary>
        /// 先生成完整制品再登记既有MEMContent。expectedRevision冲突/取消不修改工程；
        /// 外部制品可能保留为未引用文件，不伪称跨文件原子事务，也不擅自删除共享制品。
        /// </summary>
        public static MRIObjectAddress Commit(MEMEngineeringSession engineering,MEMContent owner,
            int expectedRevision,string root,CT3DAnalysisCase analysis,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(owner==null || engineering.Find(owner.Id)!=owner)
                throw new ArgumentException("结果必须登记到当前工程的MEMContent。","owner");
            if(analysis==null)throw new ArgumentNullException("analysis");
            if(engineering.Revision!=expectedRevision)throw new InvalidOperationException("org.version.conflict");
            using(var source=analysis.Source.OpenVerified(root,cancellation))
            {
                string relative=CT3DArtifactFiles.Publish(root,".analysis",stream=>
                {
                    using(var w=new BinaryWriter(stream,Encoding.UTF8,true))WriteResult(w,analysis,cancellation);
                },cancellation);
                string hash=Path.GetFileNameWithoutExtension(relative);
                byte[] payload;
                using(var memory=new MemoryStream())
                {
                    using(var w=new BinaryWriter(memory,Encoding.UTF8,true))
                    {w.Write(Magic);w.Write(Schema);analysis.Source.Write(w);w.Write(hash);}
                    payload=memory.ToArray();
                }
                cancellation.ThrowIfCancellationRequested();
                engineering.Edit("登记三维Mesh与平面分析结果",expectedRevision,edit=>
                {
                    edit.SetAspect(owner,new MPMAspect(MPMAspectKind.Data,ProviderId,Schema,payload,true));
                    return true;
                }).RequireValue();
                // 提交成功以后不再检查取消：避免工程已经提交而向调用者谎报回滚。
                return engineering.GetAddress(owner);
            }
        }

        public static CT3DAnalysisCase Open(MEMEngineeringSession engineering,
            MRIObjectAddress address,string root,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            var owner=engineering.Resolve(address) as MEMContent;
            if(owner==null)throw new InvalidDataException("三维案例工程定义不存在。");
            var aspect=owner.Aspects.SingleOrDefault(a=>a.Kind==MPMAspectKind.Data&&a.ProviderId==ProviderId);
            if(aspect==null||aspect.SchemaVersion!=Schema)throw new InvalidDataException("三维案例Schema不存在或不受支持。");
            CT3DAssetReference source;string hash;
            using(var memory=new MemoryStream(aspect.Payload,false))
            using(var r=new BinaryReader(memory,Encoding.UTF8,true))
            {
                if(r.ReadInt32()!=Magic||r.ReadInt32()!=Schema)throw new InvalidDataException("三维案例头无效。");
                source=CT3DAssetReference.Read(r);hash=CT3DArtifactFiles.ReadText(r,64);CT3DArtifactFiles.CheckHash(hash);
                if(memory.Position!=memory.Length)throw new InvalidDataException("三维案例负载有多余数据。");
            }
            using(var sourceLock=source.OpenVerified(root,cancellation))
            using(var stream=CT3DArtifactFiles.OpenRead(CT3DArtifactFiles.Resolve(root,"artifacts/ct3d/"+hash+".analysis")))
            {
                if(stream.Length>CT3DArtifactFiles.MaximumResultBytes)
                    throw new InvalidDataException("三维结果超过读取预算。");
                if(CT3DArtifactFiles.Hash(stream,cancellation)!=hash)
                    throw new InvalidDataException("ct3d.artifact.corrupt：三维结果Hash不匹配。");
                stream.Position=0;
                using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
                    return ReadResult(reader,source,cancellation);
            }
        }

        private static void WriteResult(BinaryWriter w,CT3DAnalysisCase a,CancellationToken cancellation)
        {
            w.Write(Magic);w.Write(Schema);w.Write(a.AlgorithmVersion);w.Write(a.Source.Signature);
            w.Write(a.Mesh.ResultId.ToByteArray());w.Write(a.Plane.ResultId.ToByteArray());
            w.Write(a.Mesh.Threshold);w.Write(a.Mesh.TouchesVolumeBoundary);
            w.Write(a.MaximumTriangles);w.Write(a.MaxPlanarityRatio);w.Write(a.MaxInPlaneCondition);
            var mesh=a.Mesh.Mesh;
            long bytes=checked(1024L+mesh.VertexCount*24L+mesh.TriangleCount*12L+a.SelectedVertexIndices.Count*4L);
            if(bytes>CT3DArtifactFiles.MaximumResultBytes)throw new InvalidOperationException("三维结果超过写入预算。");
            w.Write(mesh.VertexCount);
            for(int i=0;i<mesh.VertexCount;i++)
            {
                if((i&1023)==0)cancellation.ThrowIfCancellationRequested();
                var p=mesh.Vertices[i];w.Write(p.X);w.Write(p.Y);w.Write(p.Z);
            }
            w.Write(mesh.TriangleCount*3);
            for(int i=0;i<mesh.TriangleCount*3;i++)
            {if((i&4095)==0)cancellation.ThrowIfCancellationRequested();w.Write(mesh.TriangleIndices[i]);}
            w.Write(a.SelectedVertexIndices.Count);foreach(int index in a.SelectedVertexIndices)w.Write(index);
            WritePlane(w,a.Plane);
        }

        private static CT3DAnalysisCase ReadResult(BinaryReader r,CT3DAssetReference source,CancellationToken cancellation)
        {
            if(r.ReadInt32()!=Magic||r.ReadInt32()!=Schema||CT3DArtifactFiles.ReadText(r,64)!="ct3d.voxel-mesh-plane/1"||
                CT3DArtifactFiles.ReadText(r,64)!=source.Signature)
                throw new InvalidDataException("三维结果算法版本或来源签名不一致。");
            Guid meshId=new Guid(r.ReadBytes(16)),planeId=new Guid(r.ReadBytes(16));
            if(meshId==Guid.Empty||planeId==Guid.Empty)throw new InvalidDataException("三维结果身份无效。");
            float threshold=r.ReadSingle();bool boundary=r.ReadBoolean();int budget=r.ReadInt32();
            double planarity=r.ReadDouble(),condition=r.ReadDouble();
            if(float.IsNaN(threshold)||float.IsInfinity(threshold)||budget<12||budget>2000000)
                throw new InvalidDataException("三维结果参数无效。");
            int count=ReadCount(r,6000000,24);
            var vertices=new FGEPoint3D[count];
            for(int i=0;i<count;i++)
            {
                if((i&1023)==0)cancellation.ThrowIfCancellationRequested();
                vertices[i]=new FGEPoint3D(r.ReadDouble(),r.ReadDouble(),r.ReadDouble());
            }
            int indexCount=ReadCount(r,checked(budget*3),4);
            if(indexCount%3!=0)throw new InvalidDataException("三角索引数无效。");
            var indices=new int[indexCount];
            for(int i=0;i<indexCount;i++)
            {if((i&4095)==0)cancellation.ThrowIfCancellationRequested();indices[i]=r.ReadInt32();}
            var geometry=new FGETriangleMesh3D(vertices,indices);
            var mesh=new CT3DVoxelBoundaryMeshResult(geometry,threshold,source.SpatialFrame,
                source.SourceRef,source.Revision,boundary,meshId);
            int roiCount=ReadCount(r,30000,4);var roi=new int[roiCount];
            for(int i=0;i<roiCount;i++)roi[i]=r.ReadInt32();
            var fitted=CT3DMeshPlaneAnalysis.FitSelectedVertices(mesh,roi,planarity,condition,cancellation);
            ReadAndCheckPlane(r,fitted);
            if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("三维制品含多余数据。");
            // 用同一FNU求解器核验保存的量值，再恢复原始结果身份，不手工制造成功残差。
            var xyz=new double[roiCount,3];
            for(int i=0;i<roiCount;i++){var p=geometry.Vertices[roi[i]];xyz[i,0]=p.X;xyz[i,1]=p.Y;xyz[i,2]=p.Z;}
            var plane=new CT3DPlaneFitResult(mesh,FNULinearAlgebra.FitPlaneBySvd(xyz,condition),planeId);
            cancellation.ThrowIfCancellationRequested();
            return new CT3DAnalysisCase(source,mesh,plane,roi,planarity,condition,budget);
        }

        private static int ReadCount(BinaryReader r,int maximum,int stride)
        {
            int count=r.ReadInt32();
            if(count<1||count>maximum||count>(r.BaseStream.Length-r.BaseStream.Position)/stride)
                throw new InvalidDataException("三维制品计数或长度超出预算。");
            return count;
        }
        private static double[] PlaneValues(CT3DPlaneFitResult p)
        {return new[]{p.PointMm.X,p.PointMm.Y,p.PointMm.Z,p.Normal.X,p.Normal.Y,p.Normal.Z,
            p.RmsResidualMm,p.MaxAbsoluteResidualMm,p.PlanarityRatio,p.InPlaneConditionNumber};}
        private static void WritePlane(BinaryWriter w,CT3DPlaneFitResult p)
        {w.Write(p.SampleCount);foreach(double value in PlaneValues(p))w.Write(value);}
        private static void ReadAndCheckPlane(BinaryReader r,CT3DPlaneFitResult plane)
        {
            if(r.ReadInt32()!=plane.SampleCount)throw new InvalidDataException("ROI与保存的样本数不匹配。");
            foreach(double expected in PlaneValues(plane))
            {
                double actual=r.ReadDouble();
                if(double.IsNaN(actual)||double.IsInfinity(actual)||Math.Abs(actual-expected)>Math.Max(1e-10,Math.Abs(expected)*1e-10))
                    throw new InvalidDataException("保存的毫米量值/残差与几何复算不一致。");
            }
        }
    }
}
