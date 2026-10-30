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
            CT3DVoxelBoundaryMeshResult mesh,IReadOnlyList<CT3DMeshFaceReference> faces)
        {
            CT3DMeshFaceSelection.ResolveVertices(source,mesh,faces);
            SourceSignature=source.Signature;
            MeshMethod=mesh.Method;
            MeshThreshold=mesh.Threshold;
            _faces=new List<CT3DMeshFaceReference>(faces).AsReadOnly();
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
            return new CT3DMeshFaceRoiRecipe(source,mesh,faces);
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
            return new CT3DMeshFaceRoiRecipe(source,mesh,faces);
        }
    }
}
