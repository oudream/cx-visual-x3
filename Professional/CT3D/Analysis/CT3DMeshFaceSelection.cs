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
}
