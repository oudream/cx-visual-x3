using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Professional.Vision
{
    /// <summary>MEM正式二维ROI配方，数据与可视几何同Revision；图像字节不嵌入Aspect。</summary>
    public sealed class CxVisionRoiRecipe
    {
        public Guid RoiId { get; internal set; }
        public string SourceId { get; internal set; }
        public string SourceHash { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public FIMPixelFormat PixelFormat { get; internal set; }
        public long ConfigurationRevision { get; internal set; }
        public CxVisionInspectionOptions Options { get; internal set; }
        public MVIImageOverlayItem Roi { get; internal set; }
    }

    /// <summary>
    /// 用户显式确认后才从MVI临时ROI升级为MEM专业定义。
    /// 唯一MEM Undo/Redo/Save/Open真源；没有第二个ROI Store或私有历史栈。
    /// </summary>
    public static class CxVisionRoiEngineering
    {
        public const string ProviderId="vision.roi.inspection.r1";
        private const int Magic=0x31524F56;
        private const int Schema=1;

        public static MRIObjectAddress Commit(MEMEngineeringSession engineering,
            MEMContent owner,int expectedRevision,FIMImageBuffer source,
            string sourceId,MVIImageOverlayItem roi,CxVisionInspectionOptions options,
            long configRevision,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(owner==null||engineering.Find(owner.Id)!=owner)
                throw new ArgumentException("MEM Content不属于当前工程","owner");
            if(source==null)throw new ArgumentNullException("source");
            if(roi==null)throw new ArgumentNullException("roi");
            if(options==null)throw new ArgumentNullException("options");
            if(string.IsNullOrWhiteSpace(sourceId)||sourceId.Length>1024)
                throw new ArgumentException("无效原图SourceId","sourceId");
            if(configRevision<0)throw new ArgumentOutOfRangeException("configRevision");
            if(!roi.IsAreaRoi||!roi.IsVisible || roi.Points.Count>1024 ||
               roi.Text.Length>256 || roi.Bounds.Width<=0 || roi.Bounds.Height<=0 ||
               roi.Bounds.Left<0 || roi.Bounds.Top<0 ||
               roi.Bounds.Right>source.Width || roi.Bounds.Bottom>source.Height)
                throw new ArgumentException("仅允许有效原图空间内的可见面积ROI","roi");
            if(source.PixelFormat!=FIMPixelFormat.Gray8 &&
               source.PixelFormat!=FIMPixelFormat.Gray16)
                throw new ArgumentException("R1目前只支持Gray8/Gray16原图","source");
            if(CxVisionImageInterop.GetPixelDepth(source)!=options.PixelDepth)
                throw new ArgumentException("ROI位深与图像不一致","options");
            if(!string.IsNullOrWhiteSpace(CxVisionInspectionOptions.Validate(options)) ||
               options.UseAi)
                throw new ArgumentException("ROI专业检测参数非法或AI不受支持","options");
            cancellation.ThrowIfCancellationRequested();
            if(engineering.Revision!=expectedRevision)
                throw new InvalidOperationException("org.version.conflict");
            string hash=HashImage(source,cancellation);
            byte[] data,visual;
            using(var ms=new MemoryStream())
            {
                using(var writer=new BinaryWriter(ms,Encoding.UTF8,true))
                {
                    writer.Write(Magic);writer.Write(Schema);
                    writer.Write(roi.Id.ToByteArray());
                    writer.Write(sourceId.Trim());writer.Write(hash);
                    writer.Write(source.Width);writer.Write(source.Height);
                    writer.Write((int)source.PixelFormat);writer.Write(configRevision);
                    writer.Write((int)options.PixelDepth);writer.Write(options.GrayThreshold);
                    writer.Write(options.MinDefectArea);writer.Write(options.MaxDefectArea);
                }
                data=ms.ToArray();
            }
            using(var ms=new MemoryStream())
            {
                using(var writer=new BinaryWriter(ms,Encoding.UTF8,true))
                {
                    writer.Write(Magic);writer.Write(Schema);writer.Write(roi.Id.ToByteArray());
                    writer.Write((int)roi.Kind);writer.Write((int)roi.Layer);
                    writer.Write(roi.Bounds.X);writer.Write(roi.Bounds.Y);
                    writer.Write(roi.Bounds.Width);writer.Write(roi.Bounds.Height);
                    writer.Write(roi.Points.Count);
                    foreach(FGEPoint2D point in roi.Points)
                    {writer.Write(point.X);writer.Write(point.Y);}
                    writer.Write(roi.Text);writer.Write(roi.ExternalId);
                    writer.Write(roi.Argb);writer.Write(roi.LineWidth);
                    writer.Write(roi.IsVisible);writer.Write(roi.IsLocked);
                }
                visual=ms.ToArray();
            }
            cancellation.ThrowIfCancellationRequested();
            engineering.Edit("正式保存二维Vision ROI",expectedRevision,edit=>
            {
                edit.SetAspect(owner,new MPMAspect(MPMAspectKind.Data,ProviderId,Schema,data));
                edit.SetAspect(owner,new MPMAspect(MPMAspectKind.Visual,ProviderId,Schema,visual));
                return true;
            }).RequireValue();
            return engineering.GetAddress(owner);
        }

        public static CxVisionRoiRecipe Open(MEMEngineeringSession engineering,
            MRIObjectAddress address)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            MEMContent content=engineering.Resolve(address) as MEMContent;
            if(content==null)throw new InvalidDataException("二维ROI工程Content已丢失");
            var data=content.Aspects.SingleOrDefault(a=>
                a.Kind==MPMAspectKind.Data&&a.ProviderId==ProviderId);
            var visual=content.Aspects.SingleOrDefault(a=>
                a.Kind==MPMAspectKind.Visual&&a.ProviderId==ProviderId);
            if(data==null||visual==null ||
               data.SchemaVersion!=Schema||visual.SchemaVersion!=Schema)
                throw new InvalidDataException("二维ROI数据或可视Aspect缺失/版本不支持");
            var result=new CxVisionRoiRecipe();
            using(var ms=new MemoryStream(data.Payload,false))
            using(var reader=new BinaryReader(ms,Encoding.UTF8,true))
            {
                Header(reader);
                result.RoiId=new Guid(ReadExactly(reader,16));
                result.SourceId=reader.ReadString();
                result.SourceHash=reader.ReadString();
                result.Width=reader.ReadInt32();result.Height=reader.ReadInt32();
                result.PixelFormat=(FIMPixelFormat)reader.ReadInt32();
                result.ConfigurationRevision=reader.ReadInt64();
                result.Options=new CxVisionInspectionOptions {
                    PixelDepth=(CxVisionPixelDepth)reader.ReadInt32(),
                    GrayThreshold=reader.ReadInt32(),MinDefectArea=reader.ReadDouble(),
                    MaxDefectArea=reader.ReadDouble(),UseAi=false
                };
                if(ms.Position!=ms.Length)throw new InvalidDataException("ROI数据包含未识别字段");
            }
            using(var ms=new MemoryStream(visual.Payload,false))
            using(var reader=new BinaryReader(ms,Encoding.UTF8,true))
            {
                Header(reader);
                Guid id=new Guid(ReadExactly(reader,16));
                var kind=(MVIImageOverlayKind)reader.ReadInt32();
                var layer=(MVIImageOverlayLayer)reader.ReadInt32();
                var bounds=new FGERect2D(reader.ReadDouble(),reader.ReadDouble(),
                    reader.ReadDouble(),reader.ReadDouble());
                int count=reader.ReadInt32();
                if(count<0||count>1024)throw new InvalidDataException("ROI点数超预算");
                var points=new FGEPoint2D[count];
                for(int i=0;i<count;i++)
                    points[i]=new FGEPoint2D(reader.ReadDouble(),reader.ReadDouble());
                string label=reader.ReadString(),external=reader.ReadString();
                int argb=reader.ReadInt32();double line=reader.ReadDouble();
                bool visible=reader.ReadBoolean(),locked=reader.ReadBoolean();
                if(ms.Position!=ms.Length||id!=result.RoiId || !visible ||
                   label.Length>256 || external.Length>1024)
                    throw new InvalidDataException("ROI可视身份或约束被破坏");
                result.Roi=new MVIImageOverlayItem(kind,layer,bounds,points,label,
                    external,argb,line,id:id,isVisible:visible,isLocked:locked);
            }
            if(result.Roi==null||!result.Roi.IsAreaRoi ||
               result.Width<=0||result.Height<=0||result.ConfigurationRevision<0 ||
               result.Roi.Bounds.Left<0||result.Roi.Bounds.Top<0 ||
               result.Roi.Bounds.Right>result.Width||result.Roi.Bounds.Bottom>result.Height||
               result.SourceId.Length==0||result.SourceId.Length>1024||
               result.SourceHash.Length!=64||
               !result.SourceHash.All(x=>(x>='0'&&x<='9')||(x>='a'&&x<='f')) ||
               CxVisionInspectionOptions.Validate(result.Options)!=null||
               (result.PixelFormat==FIMPixelFormat.Gray8 &&
                result.Options.PixelDepth!=CxVisionPixelDepth.Bits8) ||
               (result.PixelFormat==FIMPixelFormat.Gray16 &&
                result.Options.PixelDepth!=CxVisionPixelDepth.Bits16) ||
               (result.PixelFormat!=FIMPixelFormat.Gray8 &&
                result.PixelFormat!=FIMPixelFormat.Gray16))
                throw new InvalidDataException("二维ROI工程定义不合法");
            return result;
        }

        public static async Task<CxVisionRunSummary> RunSavedAsync(
            MEMEngineeringSession engineering,MRIObjectAddress address,
            FIMImageBuffer source,CancellationToken cancellation)
        {
            if(source==null)throw new ArgumentNullException("source");
            cancellation.ThrowIfCancellationRequested();
            CxVisionRoiRecipe recipe=Open(engineering,address);
            if(source.Width!=recipe.Width||source.Height!=recipe.Height ||
               source.PixelFormat!=recipe.PixelFormat ||
               !string.Equals(HashImage(source,cancellation),recipe.SourceHash,
                   StringComparison.Ordinal))
                throw new InvalidOperationException("vision.roi.source_hash_mismatch");
            cancellation.ThrowIfCancellationRequested();
            MVIImageOverlayItem roi=recipe.Roi;
            int left=Math.Max(0,(int)Math.Floor(roi.Bounds.Left));
            int top=Math.Max(0,(int)Math.Floor(roi.Bounds.Top));
            int right=Math.Min(source.Width-1,(int)Math.Ceiling(roi.Bounds.Right)-1);
            int bottom=Math.Min(source.Height-1,(int)Math.Ceiling(roi.Bounds.Bottom)-1);
            if(left>right||top>bottom)
                throw new InvalidDataException("ROI没有有效像素");
            return await CxVisionDataFlow.RunRoiInspectionPreviewAsync(
                source,recipe.Options,recipe.SourceId,
                new FIMPixelRegion(left,top,right,bottom),
                (x,y)=>roi.ContainsRoiPixelCenter(x,y),cancellation).ConfigureAwait(false);
        }

        private static string HashImage(FIMImageBuffer image,CancellationToken cancellation)
        {
            using(var sha=SHA256.Create())
            using(var sink=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
            {
                FIMRawImageWriter.Write(sink,image,cancellationToken:cancellation);
                sink.FlushFinalBlock();
                return BitConverter.ToString(sha.Hash).Replace("-","")
                    .ToLowerInvariant();
            }
        }
        private static void Header(BinaryReader reader)
        {
            if(reader.ReadInt32()!=Magic||reader.ReadInt32()!=Schema)
                throw new InvalidDataException("Vision ROI Aspect头不合法");
        }
        private static byte[] ReadExactly(BinaryReader reader,int length)
        {
            byte[] bytes=reader.ReadBytes(length);
            if(bytes.Length!=length)throw new EndOfStreamException();
            return bytes;
        }
    }
}
