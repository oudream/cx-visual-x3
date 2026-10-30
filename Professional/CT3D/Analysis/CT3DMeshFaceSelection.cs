using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Professional.CT3D
{
    /// <summary>真实原Mesh三角面子元素引用；GPU显示LOD不参与计量或子元素身份。</summary>
    public sealed class CT3DMeshFaceReference
    {
        internal CT3DMeshFaceReference(CT3DAssetReference source,CT3DVoxelBoundaryMeshResult mesh,int index)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(mesh==null)throw new ArgumentNullException("mesh");
            if(mesh.SourceRef!=source.SourceRef||mesh.SourceRevision!=source.Revision||
                !source.MatchesSpatialFrame(mesh.SpatialFrame))
                throw new InvalidOperationException("Mesh与原始资产的Frame或Revision不同源。");
            if(index<0||index>=mesh.Mesh.TriangleCount)throw new ArgumentOutOfRangeException("index");
            SourceSignature=source.Signature;Method=mesh.Method;Threshold=mesh.Threshold;
            TriangleIndex=index;
            int offset=index*3;
            VertexA=mesh.Mesh.TriangleIndices[offset];
            VertexB=mesh.Mesh.TriangleIndices[offset+1];
            VertexC=mesh.Mesh.TriangleIndices[offset+2];
            PointA=mesh.Mesh.Vertices[VertexA];
            PointB=mesh.Mesh.Vertices[VertexB];
            PointC=mesh.Mesh.Vertices[VertexC];
            using(var memory=new MemoryStream())
            using(var w=new BinaryWriter(memory,Encoding.UTF8,true))
            {
                w.Write(SourceSignature);w.Write(Method);w.Write(Threshold);w.Write(index);
                w.Write(VertexA);w.Write(VertexB);w.Write(VertexC);
                WritePoint(w,PointA);WritePoint(w,PointB);WritePoint(w,PointC);
                w.Flush();StableId=CT3DArtifactFiles.HashBytes(memory.ToArray());
            }
        }
        public string SourceSignature {get;private set;}
        public string Method {get;private set;}
        public float Threshold {get;private set;}
        public int TriangleIndex {get;private set;}
        public int VertexA {get;private set;}
        public int VertexB {get;private set;}
        public int VertexC {get;private set;}
        public FGEPoint3D PointA {get;private set;}
        public FGEPoint3D PointB {get;private set;}
        public FGEPoint3D PointC {get;private set;}
        public string StableId {get;private set;}
        private static void WritePoint(BinaryWriter w,FGEPoint3D p)
        {w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        private static bool Equal(FGEPoint3D a,FGEPoint3D b)
        {return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;}
        internal void RequireCurrent(CT3DAssetReference source,CT3DVoxelBoundaryMeshResult mesh)
        {
            if(source==null||mesh==null||SourceSignature!=source.Signature||
                mesh.SourceRef!=source.SourceRef||mesh.SourceRevision!=source.Revision||
                !source.MatchesSpatialFrame(mesh.SpatialFrame)||Method!=mesh.Method||
                Threshold!=mesh.Threshold||TriangleIndex<0||
                TriangleIndex>=mesh.Mesh.TriangleCount)
                throw new InvalidOperationException("Mesh子元素的资产、Frame、提取配方或拓扑版本不一致。");
            int offset=TriangleIndex*3;
            if(mesh.Mesh.TriangleIndices[offset]!=VertexA||
                mesh.Mesh.TriangleIndices[offset+1]!=VertexB||
                mesh.Mesh.TriangleIndices[offset+2]!=VertexC||
                !Equal(mesh.Mesh.Vertices[VertexA],PointA)||
                !Equal(mesh.Mesh.Vertices[VertexB],PointB)||
                !Equal(mesh.Mesh.Vertices[VertexC],PointC))
                throw new InvalidOperationException("Mesh三角面位置或顶点已变化，禁止错用旧面片索引。");
        }
    }


    /// <summary>原始双精度Mesh面片ROI的拓扑及几何诊断，不代表工业精度认证。
    /// 面积为曲面三角面总面积，边界长度为原始Mesh边线物理长度。</summary>
    public sealed class CT3DMeshRegionQuality
    {
        internal CT3DMeshRegionQuality(int faceCount,int vertexCount,int components,
            int boundaryEdges,int boundaryLoops,int irregularBoundaries,int nonManifoldEdges,
            double areaMm2,double perimeterMm)
        {
            FaceCount=faceCount;VertexCount=vertexCount;
            ConnectedComponentCount=components;BoundaryEdgeCount=boundaryEdges;
            ClosedBoundaryLoopCount=boundaryLoops;
            IrregularBoundaryComponentCount=irregularBoundaries;
            NonManifoldEdgeCount=nonManifoldEdges;
            SurfaceAreaMm2=areaMm2;BoundaryLengthMm=perimeterMm;
        }
        public int FaceCount {get;private set;}
        public int VertexCount {get;private set;}
        public int ConnectedComponentCount {get;private set;}
        public int BoundaryEdgeCount {get;private set;}
        public int ClosedBoundaryLoopCount {get;private set;}
        public int IrregularBoundaryComponentCount {get;private set;}
        public int NonManifoldEdgeCount {get;private set;}
        public double SurfaceAreaMm2 {get;private set;}
        public double BoundaryLengthMm {get;private set;}
        public bool HasSingleSimpleBoundary
        {get{return ConnectedComponentCount==1&&ClosedBoundaryLoopCount==1&&
            IrregularBoundaryComponentCount==0&&NonManifoldEdgeCount==0;}}
        public bool IsIndustrialAccuracyCertified {get{return false;}}
    }

    /// <summary>复用FGE真实射线求交与CT3D原值分析，不拥有Scene/GPU/MEM。</summary>
    public static class CT3DMeshFaceSelection
    {
        public static CT3DMeshFaceReference Reference(CT3DAnalysisCase analysis,int index)
        {
            if(analysis==null)throw new ArgumentNullException("analysis");
            if(analysis.Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("当前案例不包含可选择的原始三角Mesh。");
            return new CT3DMeshFaceReference(analysis.Source,analysis.Mesh,index);
        }
        public static bool TryPick(CT3DAnalysisCase analysis,FGEPoint3D origin,
            FGEVector3D direction,out CT3DMeshFaceReference reference,out FGEMeshRayHit hit,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(analysis==null)throw new ArgumentNullException("analysis");
            if(analysis.Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("非MeshPlane案例不能选择三角面。");
            reference=null;
            cancellation.ThrowIfCancellationRequested();
            if(!FGEMeshRayIntersector.TryIntersect(analysis.Mesh.Mesh,origin,direction,
                out hit,cancellation))return false;
            reference=Reference(analysis,hit.TriangleIndex);
            return true;
        }
        /// <summary>从一个真实原Mesh面片沿共享边扩展连续近似共面区域。
        /// 所有判定在FGE双精度毫米顶点上进行，绝不使用WPF显示LOD或屏幕坐标。
        /// 仅返回选择引用；不修改专业Case、MVI会话或MEM工程。</summary>
        public static IReadOnlyList<CT3DMeshFaceReference> ExpandConnectedCoplanar(
            CT3DAnalysisCase analysis,CT3DMeshFaceReference seed,
            double maximumAngleDegrees,double maximumDeviationMm,
            int maximumFaces,CancellationToken cancellation=default(CancellationToken))
        {
            if(analysis==null)throw new ArgumentNullException("analysis");
            if(seed==null)throw new ArgumentNullException("seed");
            if(analysis.Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("共面扩展只能用于具有原值Mesh的案例。");
            if(double.IsNaN(maximumAngleDegrees)||double.IsInfinity(maximumAngleDegrees)||
                maximumAngleDegrees<0||maximumAngleDegrees>=90)
                throw new ArgumentOutOfRangeException("maximumAngleDegrees");
            if(double.IsNaN(maximumDeviationMm)||double.IsInfinity(maximumDeviationMm)||
                maximumDeviationMm<0)
                throw new ArgumentOutOfRangeException("maximumDeviationMm");
            if(maximumFaces<1||maximumFaces>30000)
                throw new ArgumentOutOfRangeException("maximumFaces");
            cancellation.ThrowIfCancellationRequested();
            var mesh=analysis.Mesh.Mesh;
            seed.RequireCurrent(analysis.Source,analysis.Mesh);
            FGEPoint3D origin=seed.PointA;
            double sx=seed.PointB.X-origin.X,sy=seed.PointB.Y-origin.Y,sz=seed.PointB.Z-origin.Z;
            double tx=seed.PointC.X-origin.X,ty=seed.PointC.Y-origin.Y,tz=seed.PointC.Z-origin.Z;
            double nx=sy*tz-sz*ty,ny=sz*tx-sx*tz,nz=sx*ty-sy*tx;
            double seedLength=Math.Sqrt(nx*nx+ny*ny+nz*nz);
            if(seedLength<=0||double.IsNaN(seedLength)||double.IsInfinity(seedLength))
                throw new InvalidOperationException("原Mesh种子面片退化，不能扩展ROI。");
            nx/=seedLength;ny/=seedLength;nz/=seedLength;
            double minimumDot=Math.Cos(maximumAngleDegrees*Math.PI/180.0);
            int triangleCount=mesh.TriangleCount;
            // 只为候选共面三角形创建边索引；防止过宽角度/毫米参数形成无界中间缓存。
            int candidateBudget=Math.Min(triangleCount,Math.Max(4096,maximumFaces*8));
            var edgeOwners=new Dictionary<ulong,List<int>>();
            var eligible=new bool[triangleCount];
            int accepted=0;
            for(int i=0;i<triangleCount;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                int offset=i*3;
                int a=mesh.TriangleIndices[offset],b=mesh.TriangleIndices[offset+1],
                    c=mesh.TriangleIndices[offset+2];
                FGEPoint3D pa=mesh.Vertices[a],pb=mesh.Vertices[b],pc=mesh.Vertices[c];
                double abx=pb.X-pa.X,aby=pb.Y-pa.Y,abz=pb.Z-pa.Z;
                double acx=pc.X-pa.X,acy=pc.Y-pa.Y,acz=pc.Z-pa.Z;
                double fx=aby*acz-abz*acy,fy=abz*acx-abx*acz,
                    fz=abx*acy-aby*acx;
                double length=Math.Sqrt(fx*fx+fy*fy+fz*fz);
                if(!(length>0)||double.IsNaN(length)||double.IsInfinity(length)||
                    (fx*nx+fy*ny+fz*nz)/length<minimumDot)continue;
                if(Math.Abs((pa.X-origin.X)*nx+(pa.Y-origin.Y)*ny+
                    (pa.Z-origin.Z)*nz)>maximumDeviationMm||
                    Math.Abs((pb.X-origin.X)*nx+(pb.Y-origin.Y)*ny+
                    (pb.Z-origin.Z)*nz)>maximumDeviationMm||
                    Math.Abs((pc.X-origin.X)*nx+(pc.Y-origin.Y)*ny+
                    (pc.Z-origin.Z)*nz)>maximumDeviationMm)continue;
                if(++accepted>candidateBudget)
                    throw new InvalidOperationException(
                        "候选共面网格超过区域扩展内存预算，请缩小角度/毫米范围或分区处理。");
                eligible[i]=true;
                AddEdgeOwner(edgeOwners,a,b,i);
                AddEdgeOwner(edgeOwners,b,c,i);
                AddEdgeOwner(edgeOwners,c,a,i);
            }
            if(!eligible[seed.TriangleIndex])
                throw new InvalidOperationException("种子面片没有满足当前原值质量条件。");
            var visited=new HashSet<int>();
            var queue=new Queue<int>();
            var result=new List<int>();
            queue.Enqueue(seed.TriangleIndex);
            visited.Add(seed.TriangleIndex);
            while(queue.Count>0)
            {
                cancellation.ThrowIfCancellationRequested();
                int face=queue.Dequeue();
                if(result.Count>=maximumFaces)
                    throw new InvalidOperationException("连通面片ROI超过选择预算，不允许截断。");
                result.Add(face);
                int offset=face*3;
                int a=mesh.TriangleIndices[offset],b=mesh.TriangleIndices[offset+1],
                    c=mesh.TriangleIndices[offset+2];
                QueueEdgeNeighbors(edgeOwners,EdgeKey(a,b),visited,queue);
                QueueEdgeNeighbors(edgeOwners,EdgeKey(b,c),visited,queue);
                QueueEdgeNeighbors(edgeOwners,EdgeKey(c,a),visited,queue);
            }
            cancellation.ThrowIfCancellationRequested();
            result.Sort();
            var references=new List<CT3DMeshFaceReference>(result.Count);
            foreach(int index in result)
            {
                cancellation.ThrowIfCancellationRequested();
                references.Add(new CT3DMeshFaceReference(analysis.Source,analysis.Mesh,index));
            }
            // 同样执行已有顶点上限，禁止产生不能进行专业原值分析的选区。
            ResolveVertices(analysis.Source,analysis.Mesh,references,cancellation);
            return references.AsReadOnly();
        }
        /// <summary>检查真实原Mesh面片区域的连通、边界与毫米面积/周长。
        /// 不把开放选区误称为整个实体的水密性，也不出具计量认证。</summary>
        public static CT3DMeshRegionQuality InspectRegion(
            CT3DAnalysisCase analysis,IReadOnlyList<CT3DMeshFaceReference> faces,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(analysis==null)throw new ArgumentNullException("analysis");
            if(analysis.Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("只有MeshPlane案例包含原值Mesh面片。");
            IReadOnlyList<int> vertices=ResolveVertices(analysis.Source,analysis.Mesh,
                faces,cancellation);
            var owners=BuildRegionEdges(faces,cancellation);
            List<List<int>> components=GetFaceComponents(faces,owners,analysis.Mesh.Mesh,
                cancellation);
            double area=0,perimeter=0;
            for(int i=0;i<faces.Count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                CT3DMeshFaceReference face=faces[i];
                FGEPoint3D a=face.PointA,b=face.PointB,c=face.PointC;
                double x=b.X-a.X,y=b.Y-a.Y,z=b.Z-a.Z;
                double u=c.X-a.X,v=c.Y-a.Y,w=c.Z-a.Z;
                double crossX=y*w-z*v,crossY=z*u-x*w,crossZ=x*v-y*u;
                double triangleArea=0.5*Math.Sqrt(crossX*crossX+
                    crossY*crossY+crossZ*crossZ);
                if(!(triangleArea>0)||double.IsInfinity(triangleArea)||
                    double.IsNaN(triangleArea))
                    throw new InvalidOperationException("原Mesh包含退化或非有限三角面，拒绝质量评价。");
                area+=triangleArea;
            }
            var boundary=new Dictionary<int,List<int>>();
            int edgeCount=0,nonManifold=0;
            foreach(KeyValuePair<ulong,List<int>> item in owners)
            {
                cancellation.ThrowIfCancellationRequested();
                if(item.Value.Count>2)nonManifold++;
                if(item.Value.Count!=1)continue;
                edgeCount++;
                int a=(int)(item.Key>>32),b=(int)(item.Key&0xffffffffUL);
                FGEPoint3D pa=analysis.Mesh.Mesh.Vertices[a],
                    pb=analysis.Mesh.Mesh.Vertices[b];
                double dx=pa.X-pb.X,dy=pa.Y-pb.Y,dz=pa.Z-pb.Z;
                perimeter+=Math.Sqrt(dx*dx+dy*dy+dz*dz);
                AddBoundaryNeighbor(boundary,a,b);
                AddBoundaryNeighbor(boundary,b,a);
            }
            int loops=0,irregular=0;
            var seen=new HashSet<int>();
            foreach(int start in boundary.Keys)
            {
                if(!seen.Add(start))continue;
                bool closed=true;
                var queue=new Queue<int>();queue.Enqueue(start);
                while(queue.Count>0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int current=queue.Dequeue();
                    List<int> adjacent=boundary[current];
                    if(adjacent.Count!=2)closed=false;
                    foreach(int other in adjacent)
                        if(seen.Add(other))queue.Enqueue(other);
                }
                if(closed)loops++;else irregular++;
            }
            if(double.IsNaN(area)||double.IsInfinity(area)||
                double.IsNaN(perimeter)||double.IsInfinity(perimeter))
                throw new OverflowException("ROI几何面积或边界长度超出数值范围。");
            return new CT3DMeshRegionQuality(faces.Count,vertices.Count,
                components.Count,edgeCount,loops,irregular,nonManifold,area,perimeter);
        }

        /// <summary>清理离散误选的孤岛；只保留共享边连通的最大原值三角面分量。
        /// 面片数相同时选择最小TriangleIndex的分量，结果顺序稳定且不触碰MEM。</summary>
        public static IReadOnlyList<CT3DMeshFaceReference> KeepLargestConnectedRegion(
            CT3DAnalysisCase analysis,IReadOnlyList<CT3DMeshFaceReference> faces,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(analysis==null)throw new ArgumentNullException("analysis");
            if(analysis.Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("仅MeshPlane案例支持真实面片连通清理。");
            ResolveVertices(analysis.Source,analysis.Mesh,faces,cancellation);
            var edges=BuildRegionEdges(faces,cancellation);
            List<List<int>> components=GetFaceComponents(faces,edges,analysis.Mesh.Mesh,
                cancellation);
            List<int> largest=null;
            foreach(List<int> component in components)
            {
                component.Sort();
                if(largest==null||component.Count>largest.Count||
                    (component.Count==largest.Count&&component[0]<largest[0]))
                    largest=component;
            }
            var lookup=new Dictionary<int,CT3DMeshFaceReference>();
            foreach(CT3DMeshFaceReference face in faces)lookup.Add(face.TriangleIndex,face);
            var result=new List<CT3DMeshFaceReference>(largest.Count);
            foreach(int index in largest)
            {
                cancellation.ThrowIfCancellationRequested();
                result.Add(lookup[index]);
            }
            return result.AsReadOnly();
        }

        private static Dictionary<ulong,List<int>> BuildRegionEdges(
            IReadOnlyList<CT3DMeshFaceReference> faces,CancellationToken cancellation)
        {
            var result=new Dictionary<ulong,List<int>>();
            for(int i=0;i<faces.Count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                CT3DMeshFaceReference face=faces[i];
                AddEdgeOwner(result,face.VertexA,face.VertexB,face.TriangleIndex);
                AddEdgeOwner(result,face.VertexB,face.VertexC,face.TriangleIndex);
                AddEdgeOwner(result,face.VertexC,face.VertexA,face.TriangleIndex);
            }
            return result;
        }

        private static List<List<int>> GetFaceComponents(
            IReadOnlyList<CT3DMeshFaceReference> faces,
            Dictionary<ulong,List<int>> owners,FGETriangleMesh3D mesh,
            CancellationToken cancellation)
        {
            var visited=new HashSet<int>();
            var components=new List<List<int>>();
            foreach(CT3DMeshFaceReference face in faces)
            {
                cancellation.ThrowIfCancellationRequested();
                if(!visited.Add(face.TriangleIndex))continue;
                var group=new List<int>();
                var queue=new Queue<int>();
                queue.Enqueue(face.TriangleIndex);
                while(queue.Count>0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int triangle=queue.Dequeue();
                    group.Add(triangle);
                    int offset=triangle*3;
                    int a=mesh.TriangleIndices[offset],b=mesh.TriangleIndices[offset+1],
                        c=mesh.TriangleIndices[offset+2];
                    QueueEdgeNeighbors(owners,EdgeKey(a,b),visited,queue);
                    QueueEdgeNeighbors(owners,EdgeKey(b,c),visited,queue);
                    QueueEdgeNeighbors(owners,EdgeKey(c,a),visited,queue);
                }
                components.Add(group);
            }
            return components;
        }

        private static void AddBoundaryNeighbor(Dictionary<int,List<int>> boundary,int a,int b)
        {
            List<int> adjacent;
            if(!boundary.TryGetValue(a,out adjacent))
            {
                adjacent=new List<int>(2);boundary.Add(a,adjacent);
            }
            adjacent.Add(b);
        }

        private static ulong EdgeKey(int a,int b)
        {
            uint first=(uint)Math.Min(a,b),second=(uint)Math.Max(a,b);
            return ((ulong)first<<32)|second;
        }
        private static void AddEdgeOwner(Dictionary<ulong,List<int>> owners,
            int a,int b,int triangle)
        {
            ulong key=EdgeKey(a,b);
            List<int> faces;
            if(!owners.TryGetValue(key,out faces))
            {
                faces=new List<int>(2);
                owners.Add(key,faces);
            }
            faces.Add(triangle);
        }
        private static void QueueEdgeNeighbors(Dictionary<ulong,List<int>> owners,
            ulong edge,HashSet<int> visited,Queue<int> queue)
        {
            List<int> faces;
            if(!owners.TryGetValue(edge,out faces))return;
            foreach(int face in faces)
                if(visited.Add(face))queue.Enqueue(face);
        }
        public static IReadOnlyList<int> ResolveVertices(CT3DAssetReference source,
            CT3DVoxelBoundaryMeshResult mesh,IReadOnlyList<CT3DMeshFaceReference> faces,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(source==null)throw new ArgumentNullException("source");
            if(mesh==null)throw new ArgumentNullException("mesh");
            if(faces==null)throw new ArgumentNullException("faces");
            if(faces.Count<1||faces.Count>30000)
                throw new ArgumentOutOfRangeException("faces");
            cancellation.ThrowIfCancellationRequested();
            var seen=new HashSet<int>();
            var vertices=new SortedSet<int>();
            for(int i=0;i<faces.Count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                CT3DMeshFaceReference face=faces[i];
                if(face==null)throw new ArgumentException("ROI包含null面片。","faces");
                face.RequireCurrent(source,mesh);
                if(!seen.Add(face.TriangleIndex))
                    throw new ArgumentException("ROI重复选择同一三角面。","faces");
                vertices.Add(face.VertexA);vertices.Add(face.VertexB);vertices.Add(face.VertexC);
                if(vertices.Count>30000)
                    throw new InvalidOperationException("ROI超过30000顶点预算。");
            }
            cancellation.ThrowIfCancellationRequested();
            return new List<int>(vertices).AsReadOnly();
        }
    }

    /// <summary>专业原Mesh子元素ROI配方；以资产/Frame/算法和稳定面片身份定义，
    /// 不保存WPF显示LOD、面片拾取像素或第二套MEM工程节点。</summary>
    public sealed class CT3DMeshFaceRoiRecipe
    {
        private readonly ReadOnlyCollection<CT3DMeshFaceReference> _faces;
        private CT3DMeshFaceRoiRecipe(CT3DAssetReference source,
            CT3DVoxelBoundaryMeshResult mesh,IReadOnlyList<CT3DMeshFaceReference> faces,
            bool preserveSerializedOrder)
        {
            CT3DMeshFaceSelection.ResolveVertices(source,mesh,faces);
            SourceSignature=source.Signature;
            MeshMethod=mesh.Method;
            MeshThreshold=mesh.Threshold;
            // 新选区以三角面身份规范排序，点击顺序不应改变专业参数签名。
            // 读取已有V5制品必须保留原序列，避免使历史参数签名失效。
            var ordered=new List<CT3DMeshFaceReference>(faces);
            if(!preserveSerializedOrder)
                ordered.Sort((left,right)=>left.TriangleIndex.CompareTo(right.TriangleIndex));
            _faces=ordered.AsReadOnly();
            using(var stream=new MemoryStream())
            using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                Write(w);w.Flush();Signature=CT3DArtifactFiles.HashBytes(stream.ToArray());
            }
        }
        public string SourceSignature {get;private set;}
        public string MeshMethod {get;private set;}
        public float MeshThreshold {get;private set;}
        public string Signature {get;private set;}
        public ReadOnlyCollection<CT3DMeshFaceReference> Faces {get{return _faces;}}

        public static CT3DMeshFaceRoiRecipe Capture(CT3DAssetReference source,
            CT3DVoxelBoundaryMeshResult mesh,IReadOnlyList<CT3DMeshFaceReference> faces)
        {
            if(faces==null)throw new ArgumentNullException("faces");
            return new CT3DMeshFaceRoiRecipe(source,mesh,faces,false);
        }
        /// <summary>验证当前资产、Frame、原Mesh方法与面片StableId，
        /// 返回与配方绑定的原值面片引用；允许Headless/SDK调用。</summary>
        public IReadOnlyList<CT3DMeshFaceReference> Resolve(
            CT3DAssetReference source,CT3DVoxelBoundaryMeshResult mesh,
            CancellationToken cancellation)
        {
            if(source==null||mesh==null||source.Signature!=SourceSignature||
                mesh.Method!=MeshMethod||mesh.Threshold!=MeshThreshold)
                throw new InvalidOperationException("已存面片配方来源或Mesh提取算法改变。");
            CT3DMeshFaceSelection.ResolveVertices(source,mesh,_faces,cancellation);
            return _faces;
        }
        internal void Write(BinaryWriter w)
        {
            w.Write(SourceSignature);w.Write(MeshMethod);w.Write(MeshThreshold);
            w.Write(_faces.Count);
            foreach(CT3DMeshFaceReference f in _faces)
            {
                w.Write(f.TriangleIndex);w.Write(f.StableId);
            }
        }
        internal static CT3DMeshFaceRoiRecipe Read(BinaryReader r,
            CT3DAssetReference source,CT3DVoxelBoundaryMeshResult mesh,
            CancellationToken cancellation)
        {
            string signature=CT3DArtifactFiles.ReadText(r,64);
            string method=CT3DArtifactFiles.ReadText(r,128);
            float threshold=r.ReadSingle();
            if(signature!=source.Signature||method!=mesh.Method||
                threshold!=mesh.Threshold)
                throw new InvalidDataException("存档面片ROI来源或算法版本不一致。");
            int count=r.ReadInt32();
            if(count<1||count>30000||count>mesh.Mesh.TriangleCount)
                throw new InvalidDataException("存档面片ROI数量无效。");
            var faces=new List<CT3DMeshFaceReference>(count);
            var unique=new HashSet<int>();
            for(int i=0;i<count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                int triangleIndex=r.ReadInt32();
                string expected=CT3DArtifactFiles.ReadText(r,64);
                if(!unique.Add(triangleIndex)||triangleIndex<0||
                    triangleIndex>=mesh.Mesh.TriangleCount)
                    throw new InvalidDataException("存档面片ROI重复或索引越界。");
                var actual=new CT3DMeshFaceReference(source,mesh,triangleIndex);
                if(actual.StableId!=expected)
                    throw new InvalidDataException("存档面片指纹与真实原Mesh拓扑不一致。");
                faces.Add(actual);
            }
            return new CT3DMeshFaceRoiRecipe(source,mesh,faces,true);
        }
    }
}
