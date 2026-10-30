using System;
using System.IO;
using System.Text;
using System.Threading;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    /// <summary>可编辑切片ROI：原始Voxel四角、Frame/来源与毫米区间是唯一专业配方。
    /// 预览像素仅为交互投影，不作为工程保存格式。</summary>
    public sealed class CT3DSliceRoiRecipe
    {
        private readonly int[] _corners;
        private CT3DSliceRoiRecipe(CT3DAssetReference source,FVOSlicePlane plane,
            int[] corners,double minimumOffsetMm,double maximumOffsetMm)
        {
            if(source==null)throw new ArgumentNullException("source");
            Validate(source,plane,corners,minimumOffsetMm,maximumOffsetMm);
            SourceSignature=source.Signature;FrameSignature=source.FrameSignature;Plane=plane;
            _corners=(int[])corners.Clone();MinimumOffsetMm=minimumOffsetMm;MaximumOffsetMm=maximumOffsetMm;
            using(var memory=new MemoryStream())
            using(var w=new BinaryWriter(memory,Encoding.UTF8,true))
            {
                Write(w);w.Flush();Signature=CT3DArtifactFiles.HashBytes(memory.ToArray());
            }
        }
        public string SourceSignature {get;private set;}
        public string FrameSignature {get;private set;}
        public FVOSlicePlane Plane {get;private set;}
        public double MinimumOffsetMm {get;private set;}
        public double MaximumOffsetMm {get;private set;}
        public string Signature {get;private set;}

        /// <summary>原体素四角保持不变，仅更新ROI法向毫米范围。</summary>
        public CT3DSliceRoiRecipe WithNormalOffsets(CT3DAssetReference source,
            double minimumOffsetMm,double maximumOffsetMm)
        {
            RequireSource(source);
            return new CT3DSliceRoiRecipe(source,Plane,_corners,minimumOffsetMm,maximumOffsetMm);
        }

        internal static CT3DSliceRoiRecipe Capture(CT3DAssetReference source,
            FVOPhysicalSliceRectangle rectangle,double minMm,double maxMm)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(rectangle==null)throw new ArgumentNullException("rectangle");
            if(!source.MatchesSpatialFrame(rectangle.SpatialFrame))
                throw new InvalidOperationException("切片ROI与当前资产空间来源不一致。");
            if(rectangle.Left==rectangle.Right||rectangle.Top==rectangle.Bottom)
                throw new ArgumentException("切片ROI没有有效面积。","rectangle");
            var corners=new int[12];
            var physical=new [] {rectangle.TopLeft,rectangle.TopRight,rectangle.BottomRight,rectangle.BottomLeft};
            for(int i=0;i<4;i++)
            {
                var voxel=source.SpatialFrame.PhysicalToVoxel(physical[i].X,physical[i].Y,physical[i].Z);
                double[] q={voxel.X,voxel.Y,voxel.Z};
                for(int axis=0;axis<3;axis++)
                {
                    if(double.IsNaN(q[axis])||double.IsInfinity(q[axis])||
                        q[axis]<int.MinValue||q[axis]>int.MaxValue)
                        throw new ArgumentException("物理ROI无法映射为有效原体素角点。","rectangle");
                    int v=checked((int)Math.Round(q[axis]));
                    if(Math.Abs(q[axis]-v)>1e-6)
                        throw new ArgumentException("ROI角点不是原始Voxel中心。","rectangle");
                    corners[i*3+axis]=v;
                }
            }
            return new CT3DSliceRoiRecipe(source,rectangle.Plane,corners,minMm,maxMm);
        }

        private static void Validate(CT3DAssetReference source,FVOSlicePlane plane,int[] corners,
            double minMm,double maxMm)
        {
            if(corners==null||corners.Length!=12)throw new InvalidDataException("切片ROI必须有四个体素角点。");
            if(double.IsNaN(minMm)||double.IsInfinity(minMm)||double.IsNaN(maxMm)||
                double.IsInfinity(maxMm)||minMm>maxMm)
                throw new InvalidDataException("切片ROI毫米区间无效。");
            int u,v,w;
            switch(plane)
            {
                case FVOSlicePlane.XY:u=0;v=1;w=2;break;
                case FVOSlicePlane.XZ:u=0;v=2;w=1;break;
                case FVOSlicePlane.YZ:u=1;v=2;w=0;break;
                default:throw new InvalidDataException("切片ROI平面无效。");
            }
            int[] dimension={source.Width,source.Height,source.Depth};
            for(int i=0;i<4;i++)
                for(int axis=0;axis<3;axis++)
                    if(corners[i*3+axis]<0||corners[i*3+axis]>=dimension[axis])
                        throw new InvalidDataException("ROI角点超出原体素体积。");
            if(corners[u]>=corners[3+u]||corners[v]>=corners[9+v]||
                corners[u]!=corners[9+u]||corners[3+u]!=corners[6+u]||
                corners[v]!=corners[3+v]||corners[6+v]!=corners[9+v])
                throw new InvalidDataException("ROI四角不构成轴对齐、非退化矩形。");
            for(int i=1;i<4;i++)
                if(corners[i*3+w]!=corners[w])
                    throw new InvalidDataException("ROI角点不属于同一原体素切片。");
        }

        internal void RequireSource(CT3DAssetReference source)
        {
            if(source==null||SourceSignature!=source.Signature||FrameSignature!=source.FrameSignature)
                throw new InvalidOperationException("ROI配方的资产或标定已变化。");
        }

        public FVOPhysicalPoint3D GetPhysicalCorner(CT3DAssetReference source,int index)
        {
            RequireSource(source);
            if(index<0||index>=4)throw new ArgumentOutOfRangeException("index");
            int j=index*3;
            return source.SpatialFrame.VoxelToPhysical(_corners[j],_corners[j+1],_corners[j+2]);
        }

        internal System.Collections.Generic.IReadOnlyList<int> SelectVertices(
            CT3DVoxelBoundaryMeshResult mesh,CT3DAssetReference source,CancellationToken cancellation)
        {
            RequireSource(source);
            if(mesh==null)throw new ArgumentNullException("mesh");
            if(!Object.ReferenceEquals(mesh.SpatialFrame,source.SpatialFrame))
                throw new InvalidOperationException("选点Mesh与原值Frame不同源。");
            return CT3DMeshPlaneAnalysis.SelectPhysicalSlicePrismVertices(mesh,Plane,
                GetPhysicalCorner(source,0),GetPhysicalCorner(source,2),
                MinimumOffsetMm,MaximumOffsetMm,cancellation);
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(SourceSignature);w.Write(FrameSignature);w.Write((int)Plane);
            w.Write(MinimumOffsetMm);w.Write(MaximumOffsetMm);
            foreach(int coordinate in _corners)w.Write(coordinate);
        }

        internal static CT3DSliceRoiRecipe Read(BinaryReader r,CT3DAssetReference source)
        {
            string signature=CT3DArtifactFiles.ReadText(r,64),frame=CT3DArtifactFiles.ReadText(r,64);
            if(signature!=source.Signature||frame!=source.FrameSignature)
                throw new InvalidDataException("切片ROI来源与空间签名不一致。");
            int plane=r.ReadInt32();double min=r.ReadDouble(),max=r.ReadDouble();
            var corners=new int[12];
            for(int i=0;i<corners.Length;i++)corners[i]=r.ReadInt32();
            return new CT3DSliceRoiRecipe(source,(FVOSlicePlane)plane,corners,min,max);
        }
    }
}
