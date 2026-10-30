using System;
using CxVisual.Foundation.Volume;
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
    /// <summary>MEM专业Aspect + 内容寻址专业结果制品；工程编辑/保存/Undo仍完全由MEM执行。</summary>
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
            return Commit(engineering,owner,expectedRevision,root,analysis,null,cancellation);
        }

        /// <summary>CT3D Data与调用方提供的MEM Visual Aspect整体提交并共享一次Undo。</summary>
        public static MRIObjectAddress Commit(MEMEngineeringSession engineering,MEMContent owner,
            int expectedRevision,string root,CT3DAnalysisCase analysis,MPMAspect visualAspect,
            CancellationToken cancellation)
        {
            if(visualAspect!=null&&visualAspect.Kind!=MPMAspectKind.Visual)
                throw new ArgumentException("附加Aspect必须为Visual。","visualAspect");
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
                engineering.Edit("登记三维专业分析结果",expectedRevision,edit=>
                {
                    edit.SetAspect(owner,new MPMAspect(MPMAspectKind.Data,ProviderId,Schema,payload,true));
                    if(visualAspect!=null)edit.SetAspect(owner,visualAspect);
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

        /// <summary>将多个独立CT3D结果在一次MEM事务中整体登记。
        /// 制品先发布并完成校验，MEM编辑失败则所有Content保持原状态；
        /// 完整未引用制品允许保留，不误删可供其它工程共享的内容寻址数据。</summary>
        public static IReadOnlyList<MRIObjectAddress> CommitAll(MEMEngineeringSession engineering,
            IReadOnlyList<MEMContent> owners,int expectedRevision,string root,
            IReadOnlyList<CT3DAnalysisCase> analyses,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(owners==null)throw new ArgumentNullException("owners");
            if(analyses==null)throw new ArgumentNullException("analyses");
            if(owners.Count==0||owners.Count>128||owners.Count!=analyses.Count)
                throw new ArgumentException("批量三维Case须有1~128个对应的MEMContent。","owners");
            if(engineering.Revision!=expectedRevision)
                throw new InvalidOperationException("org.version.conflict");
            var ids=new HashSet<Guid>();
            for(int i=0;i<owners.Count;i++)
            {
                if(owners[i]==null||engineering.Find(owners[i].Id)!=owners[i]||!ids.Add(owners[i].Id))
                    throw new ArgumentException("MEMContent归属无效或重复。","owners");
                if(analyses[i]==null)throw new ArgumentException("专业Case不能为空。","analyses");
            }
            cancellation.ThrowIfCancellationRequested();
            var locks=new List<FileStream>();
            try
            {
                var aspects=new List<MPMAspect>(analyses.Count);
                long totalArtifactBytes=0;
                for(int i=0;i<analyses.Count;i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var analysis=analyses[i];
                    locks.Add(analysis.Source.OpenVerified(root,cancellation));
                    string relative=CT3DArtifactFiles.Publish(root,".analysis",stream=>
                    {
                        using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                            WriteResult(writer,analysis,cancellation);
                    },cancellation);
                    string hash=Path.GetFileNameWithoutExtension(relative);
                    totalArtifactBytes=checked(totalArtifactBytes+new FileInfo(
                        CT3DArtifactFiles.Resolve(root,relative)).Length);
                    if(totalArtifactBytes>CT3DArtifactFiles.MaximumResultBytes)
                        throw new InvalidOperationException("批量专业结果制品合计超过内存/磁盘预算。");
                    byte[] payload;
                    using(var memory=new MemoryStream())
                    {
                        using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))
                        {
                            writer.Write(Magic);writer.Write(Schema);
                            analysis.Source.Write(writer);writer.Write(hash);
                        }
                        payload=memory.ToArray();
                    }
                    aspects.Add(new MPMAspect(MPMAspectKind.Data,ProviderId,Schema,payload,true));
                }
                cancellation.ThrowIfCancellationRequested();
                engineering.Edit("批量登记三维专业结果",expectedRevision,edit=>
                {
                    for(int i=0;i<owners.Count;i++)edit.SetAspect(owners[i],aspects[i]);
                    return true;
                }).RequireValue();
                var addresses=new List<MRIObjectAddress>(owners.Count);
                foreach(MEMContent owner in owners)addresses.Add(engineering.GetAddress(owner));
                return addresses.AsReadOnly();
            }
            finally
            {
                foreach(FileStream stream in locks)stream.Dispose();
            }
        }

        /// <summary>同一工程多个Feature由已保存配方按原值整体复算，
        /// 全部成功后一次MEM事务替换；任何一项异常/取消均不改变工程修订。</summary>
        public static IReadOnlyList<MRIObjectAddress> RecomputeAllAndCommit(
            MEMEngineeringSession engineering,IReadOnlyList<MEMContent> owners,
            int expectedRevision,string root,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(owners==null)throw new ArgumentNullException("owners");
            if(owners.Count==0||owners.Count>128)
                throw new ArgumentOutOfRangeException("owners");
            if(engineering.Revision!=expectedRevision)
                throw new InvalidOperationException("org.version.conflict");
            var analyses=new List<CT3DAnalysisCase>(owners.Count);
            var ids=new HashSet<Guid>();
            foreach(MEMContent owner in owners)
            {
                cancellation.ThrowIfCancellationRequested();
                if(owner==null||engineering.Find(owner.Id)!=owner||!ids.Add(owner.Id))
                    throw new ArgumentException("批量复算目标无效或重复。","owners");
                CT3DAnalysisCase old=Open(engineering,engineering.GetAddress(owner),root,cancellation);
                analyses.Add(old.Recompute(root,cancellation));
            }
            cancellation.ThrowIfCancellationRequested();
            return CommitAll(engineering,owners,expectedRevision,root,analyses,cancellation);
        }

        /// <summary>校验旧结果及工程修订后原值复算，使用既有MEM事务替换同一Content的结果。
        /// 同一工程提交前旧Revision冲突会拒绝，不创建平行Undo或制品仓库。</summary>
        public static MRIObjectAddress RecomputeAndCommit(MEMEngineeringSession engineering,
            MEMContent owner,int expectedRevision,string root,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(owner==null||engineering.Find(owner.Id)!=owner)
                throw new ArgumentException("待复算目标不是当前MEM工程内容。","owner");
            if(engineering.Revision!=expectedRevision)
                throw new InvalidOperationException("org.version.conflict");
            cancellation.ThrowIfCancellationRequested();
            var existing=Open(engineering,engineering.GetAddress(owner),root,cancellation);
            var recomputed=existing.Recompute(root,cancellation);
            cancellation.ThrowIfCancellationRequested();
            return Commit(engineering,owner,expectedRevision,root,recomputed,cancellation);
        }

        /// <summary>将重开案例的物理切片ROI修改作为同一MEM事务提交。
        /// 先验证既有专业结果、RAW及Frame，再重算新Feature，异常/取消不改变MEM Revision。</summary>
        public static MRIObjectAddress RecomputeSliceRoiAndCommit(
            MEMEngineeringSession engineering,MEMContent owner,int expectedRevision,
            string root,FVOPhysicalSliceRectangle rectangle,double minimumOffsetMm,
            double maximumOffsetMm,CancellationToken cancellation)
        {
            return RecomputeSliceRoiAndCommit(engineering,owner,expectedRevision,
                root,rectangle,minimumOffsetMm,maximumOffsetMm,null,cancellation);
        }

        /// <summary>ROI修改与已有Scene Visual引用同一MEM事务提交。
        /// 视觉Owner负责重绑定新ResultId，CT3D不依赖MVI；缺少重绑定器时拒绝产生悬空Scene引用。</summary>
        public static MRIObjectAddress RecomputeSliceRoiAndCommit(
            MEMEngineeringSession engineering,MEMContent owner,int expectedRevision,
            string root,FVOPhysicalSliceRectangle rectangle,double minimumOffsetMm,
            double maximumOffsetMm,
            Func<CT3DAnalysisCase,CT3DAnalysisCase,MPMAspect> rebindVisual,
            CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(owner==null||engineering.Find(owner.Id)!=owner)
                throw new ArgumentException("ROI编辑目标必须属于当前MEM工程。","owner");
            if(rectangle==null)throw new ArgumentNullException("rectangle");
            if(engineering.Revision!=expectedRevision)
                throw new InvalidOperationException("org.version.conflict");
            cancellation.ThrowIfCancellationRequested();
            const string sceneProvider="mvi.scene3d.layout";
            bool hasScene=owner.Aspects.Any(a=>a.Kind==MPMAspectKind.Visual&&
                a.ProviderId==sceneProvider);
            if(hasScene&&rebindVisual==null)
                throw new InvalidOperationException(
                    "专业ROI重算需要同时重绑定当前Scene的Mesh/Measurement ResultId。");
            var original=Open(engineering,engineering.GetAddress(owner),root,cancellation);
            var edited=original.RecomputeWithSliceRectangle(root,rectangle,
                minimumOffsetMm,maximumOffsetMm,cancellation);
            cancellation.ThrowIfCancellationRequested();
            MPMAspect visual=rebindVisual==null?null:rebindVisual(original,edited);
            if(hasScene&&(visual==null||visual.Kind!=MPMAspectKind.Visual||
                visual.ProviderId!=sceneProvider))
                throw new InvalidOperationException("Scene重绑定未返回同一MVI Visual Aspect。");
            cancellation.ThrowIfCancellationRequested();
            return Commit(engineering,owner,expectedRevision,root,edited,visual,cancellation);
        }

        /// <summary>从唯一MEM工程枚举所有CT3D专业Case；逐个复用Open的Hash/Frame/算法检验。
        /// 任何一个来源损坏即整体失败，不返回似乎完整的部分测量集合。</summary>
        public static IReadOnlyList<CT3DAnalysisCase> OpenAll(MEMEngineeringSession engineering,
            string root,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            cancellation.ThrowIfCancellationRequested();
            var result=new List<CT3DAnalysisCase>();
            foreach(MEMContent owner in engineering.Project.DescendantsAndSelf().OfType<MEMContent>())
            {
                cancellation.ThrowIfCancellationRequested();
                if(owner.Aspects.Any(a=>a.Kind==MPMAspectKind.Data&&a.ProviderId==ProviderId))
                {
                    if(result.Count>=128)throw new InvalidOperationException("三维工程一次打开超过128个Case预算。");
                    result.Add(Open(engineering,engineering.GetAddress(owner),root,cancellation));
                }
            }
            return result.AsReadOnly();
        }

        private static void WriteResult(BinaryWriter w,CT3DAnalysisCase a,CancellationToken cancellation)
        {
            // 元数据Aspect保持Schema1；结果制品V2增加明确种类，读取仍兼容已有V1 Mesh/平面。
            w.Write(Magic);w.Write(a.Kind==CT3DAnalysisKind.SurfaceCylinder?3:2);w.Write((int)a.Kind);w.Write(a.AlgorithmVersion);w.Write(a.Source.Signature);
            if(a.Kind==CT3DAnalysisKind.SurfaceSphere)
            {
                WriteSphereResult(w,a,cancellation);
                return;
            }
            if(a.Kind==CT3DAnalysisKind.SurfaceCylinder){WriteCylinderResult(w,a,cancellation);return;}
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
            if(r.ReadInt32()!=Magic)throw new InvalidDataException("三维结果头无效。");
            int version=r.ReadInt32();
            if(version!=1&&version!=2&&version!=3)throw new InvalidDataException("三维结果版本不受支持。");
            var kind=version==1?CT3DAnalysisKind.MeshPlane:(CT3DAnalysisKind)r.ReadInt32();
            if(!Enum.IsDefined(typeof(CT3DAnalysisKind),kind))throw new InvalidDataException("三维结果种类无效。");
            string method=kind==CT3DAnalysisKind.MeshPlane?"ct3d.voxel-mesh-plane/1":kind==CT3DAnalysisKind.SurfaceSphere?"ct3d.surface-sphere/1":"ct3d.surface-cylinder-axis/1";
            if(CT3DArtifactFiles.ReadText(r,64)!=method||
                CT3DArtifactFiles.ReadText(r,64)!=source.Signature)
                throw new InvalidDataException("三维结果算法版本或来源签名不一致。");
            if(kind==CT3DAnalysisKind.SurfaceSphere)return ReadSphereResult(r,source,cancellation);
            if(kind==CT3DAnalysisKind.SurfaceCylinder)return ReadCylinderResult(r,source,cancellation,version);
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


        private static void WriteCylinderResult(BinaryWriter w,CT3DAnalysisCase a,CancellationToken token)
        {
            var surface=a.Surface;var feature=a.Cylinder;
            w.Write(surface.ResultId.ToByteArray());w.Write(feature.ResultId.ToByteArray());
            w.Write(surface.Threshold);w.Write(surface.TouchesVolumeBoundary);
            w.Write(surface.SampleStep);w.Write(surface.MaximumPoints);w.Write(feature.MaximumConditionNumber);
            w.Write(feature.AxisDirection.X);w.Write(feature.AxisDirection.Y);w.Write(feature.AxisDirection.Z);
            w.Write(feature.SelectedPointIndices.Count);
            foreach(int index in feature.SelectedPointIndices)w.Write(index);
            w.Write(surface.Count);
            for(int i=0;i<surface.Count;i++)
            {
                if((i&255)==0)token.ThrowIfCancellationRequested();
                var p=surface.Points[i];w.Write(p.X);w.Write(p.Y);w.Write(p.Z);
            }
            w.Write(feature.SampleCount);
            foreach(double v in CylinderValues(feature))w.Write(v);
        }
        private static double[] CylinderValues(CT3DCylinderFitResult f)
        {
            return new[]{f.AxisPoint.X,f.AxisPoint.Y,f.AxisPoint.Z,
                f.AxisDirection.X,f.AxisDirection.Y,f.AxisDirection.Z,
                f.RadiusMm,f.RmsResidualMm,f.MaxAbsoluteResidualMm,f.ConditionNumber};
        }
        private static CT3DAnalysisCase ReadCylinderResult(BinaryReader r,CT3DAssetReference source,CancellationToken token,int version)
        {
            Guid sid=new Guid(r.ReadBytes(16)),fid=new Guid(r.ReadBytes(16));
            float threshold=r.ReadSingle();bool boundary=r.ReadBoolean();
            int step=r.ReadInt32(),maximum=r.ReadInt32();double condition=r.ReadDouble();
            var axis=new CxVisual.Foundation.Volume.FVOPhysicalPoint3D(r.ReadDouble(),r.ReadDouble(),r.ReadDouble());
            var selected=new List<int>();
            if(version>=3)
            {
                int selectedCount=r.ReadInt32();
                if(selectedCount<0||selectedCount>10000||(selectedCount>0&&selectedCount<6))
                    throw new InvalidDataException("圆柱侧壁ROI数量无效。");
                for(int i=0;i<selectedCount;i++)
                {
                    if((i&255)==0)token.ThrowIfCancellationRequested();
                    selected.Add(r.ReadInt32());
                }
            }
            if(sid==Guid.Empty||fid==Guid.Empty||float.IsNaN(threshold)||float.IsInfinity(threshold)||
                step<1||maximum<6||maximum>10000)throw new InvalidDataException("圆柱制品身份或参数无效。");
            int count=ReadCount(r,maximum,24);
            var points=new List<CxVisual.Foundation.Volume.FVOPhysicalPoint3D>(count);
            for(int i=0;i<count;i++)
            {
                if((i&255)==0)token.ThrowIfCancellationRequested();
                points.Add(new CxVisual.Foundation.Volume.FVOPhysicalPoint3D(r.ReadDouble(),r.ReadDouble(),r.ReadDouble()));
            }
            var surface=new CT3DSurfaceSamples(points,threshold,source.SpatialFrame,boundary,
                source.SourceRef,source.Revision,step,maximum,sid);
            var fit=CT3DSurfaceCylinderAnalysis.FitCylinder(surface,axis,
                selected.Count==0?null:selected,condition,token);
            if(r.ReadInt32()!=fit.SampleCount)throw new InvalidDataException("圆柱点数不一致。");
            foreach(double expected in CylinderValues(fit))
            {
                double actual=r.ReadDouble();
                if(double.IsNaN(actual)||double.IsInfinity(actual)||
                    Math.Abs(actual-expected)>Math.Max(1e-10,Math.Abs(expected)*1e-10))
                    throw new InvalidDataException("圆柱数值复核失败。");
            }
            if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("圆柱制品有多余数据。");
            var restored=new CT3DCylinderFitResult(fit.AxisPoint,fit.AxisDirection,fit.RadiusMm,
                fit.RmsResidualMm,fit.MaxAbsoluteResidualMm,fit.ConditionNumber,
                fit.SampleCount,surface,condition,selected.Count==0?null:selected,fid);
            return new CT3DAnalysisCase(source,surface,restored);
        }

        private static void WriteSphereResult(BinaryWriter w,CT3DAnalysisCase analysis,CancellationToken cancellation)
        {
            var surface=analysis.Surface;var sphere=analysis.Sphere;
            w.Write(surface.ResultId.ToByteArray());w.Write(sphere.ResultId.ToByteArray());
            w.Write(surface.Threshold);w.Write(surface.TouchesVolumeBoundary);
            w.Write(surface.SampleStep);w.Write(surface.MaximumPoints);w.Write(sphere.MaximumConditionNumber);
            w.Write(surface.Count);
            for(int i=0;i<surface.Count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                var point=surface.Points[i];w.Write(point.X);w.Write(point.Y);w.Write(point.Z);
            }
            w.Write(sphere.SampleCount);
            foreach(double value in SphereValues(sphere))w.Write(value);
        }

        private static CT3DAnalysisCase ReadSphereResult(BinaryReader r,CT3DAssetReference source,
            CancellationToken cancellation)
        {
            Guid surfaceId=new Guid(r.ReadBytes(16)),sphereId=new Guid(r.ReadBytes(16));
            float threshold=r.ReadSingle();bool boundary=r.ReadBoolean();
            int step=r.ReadInt32(),maximum=r.ReadInt32();double condition=r.ReadDouble();
            if(surfaceId==Guid.Empty||sphereId==Guid.Empty||float.IsNaN(threshold)||float.IsInfinity(threshold)||
                step<1||maximum<6||maximum>10000)
                throw new InvalidDataException("Surface/球结果身份或参数无效。");
            int count=ReadCount(r,maximum,24);
            var points=new List<CxVisual.Foundation.Volume.FVOPhysicalPoint3D>(count);
            for(int i=0;i<count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                points.Add(new CxVisual.Foundation.Volume.FVOPhysicalPoint3D(r.ReadDouble(),r.ReadDouble(),r.ReadDouble()));
            }
            var surface=new CT3DSurfaceSamples(points,threshold,source.SpatialFrame,boundary,
                source.SourceRef,source.Revision,step,maximum,surfaceId);
            var sphere=CT3DSurfaceSphereAnalysis.FitSphere(surface,condition,sphereId);
            if(r.ReadInt32()!=sphere.SampleCount)throw new InvalidDataException("球结果样本数不一致。");
            foreach(double expected in SphereValues(sphere))
            {
                double actual=r.ReadDouble();
                if(double.IsNaN(actual)||double.IsInfinity(actual)||Math.Abs(actual-expected)>Math.Max(1e-10,Math.Abs(expected)*1e-10))
                    throw new InvalidDataException("保存的毫米球参数或残差与复算不一致。");
            }
            if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("球制品含多余数据。");
            cancellation.ThrowIfCancellationRequested();
            return new CT3DAnalysisCase(source,surface,sphere);
        }

        private static double[] SphereValues(CT3DSphereFitResult sphere)
        {return new[]{sphere.Center.X,sphere.Center.Y,sphere.Center.Z,sphere.RadiusMm,
            sphere.RmsResidualMm,sphere.MaxAbsoluteResidualMm,sphere.ConditionNumber};}

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
