using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    /// <summary>专业RAW的固定身份和空间签名；MEM保存此描述，不复制原值到工程负载。</summary>
    public sealed class CT3DAssetReference
    {
        internal CT3DAssetReference(Guid id,long revision,string path,string hash,long length,
            int width,int height,int depth,FVORawVoxelFormat format,FVOVolumeSpatialFrame frame)
        {
            if(id==Guid.Empty || revision<1)throw new ArgumentException("资产身份或版本无效。");
            if(frame==null)throw new ArgumentNullException("frame");
            if(width<=0 || height<=0 || depth<=0 || !Enum.IsDefined(typeof(FVORawVoxelFormat),format))
                throw new ArgumentException("RAW尺寸或格式无效。");
            CT3DArtifactFiles.CheckRelativePath(path);
            CT3DArtifactFiles.CheckHash(hash);
            int bytes=format==FVORawVoxelFormat.Byte?1:format==FVORawVoxelFormat.UInt16?2:4;
            if(checked((long)width*height*depth*bytes)!=length)
                throw new InvalidDataException("RAW长度与元数据不一致。");
            Id=id;Revision=revision;RelativePath=path;ContentHash=hash;ByteLength=length;
            Width=width;Height=height;Depth=depth;Format=format;SpatialFrame=frame;
            using(var memory=new MemoryStream())
            {
                using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))Write(writer);
                Signature=CT3DArtifactFiles.HashBytes(memory.ToArray());
            }
        }
        public Guid Id {get;private set;}
        public long Revision {get;private set;}
        public string RelativePath {get;private set;}
        public string ContentHash {get;private set;}
        public long ByteLength {get;private set;}
        public int Width {get;private set;}
        public int Height {get;private set;}
        public int Depth {get;private set;}
        public FVORawVoxelFormat Format {get;private set;}
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}
        /// <summary>包括资产身份/数据版本/内容/空间映射的固定签名，不以对象引用判等。</summary>
        public string Signature {get;private set;}
        public string SourceRef {get{return "ct3d:asset:"+Id.ToString("N");}}

        /// <summary>按保存的显式元信息校验/加载原值及显示预览，返回值由调用方Dispose。</summary>
        public FVOVolumeData LoadVolume(string root,CancellationToken cancellation)
        {
            using(var locked=OpenVerified(root,cancellation))
            {
                var f=SpatialFrame;
                var info=new FVORawVolumeInfo(Width,Height,Depth,(float)f.SpacingX,
                    (float)f.SpacingY,(float)f.SpacingZ,Format,RelativePath);
                var volume=FVORawVolumeLoader.Load(locked.Name,info,64,cancellation);
                try
                {
                    volume.SetSpatialFrame(f);
                    volume.SetRawBuffer(FVORawVolumeLoader.MapRawBuffer(volume,cancellation));
                    return volume;
                }
                catch{volume.Dispose();throw;}
            }
        }

        public static CT3DAssetReference Capture(string root,string relativePath,
            FVORawVolumeInfo info,FVOVolumeSpatialFrame frame,long revision,
            CancellationToken cancellation,Guid? assetId=null)
        {
            if(info==null)throw new ArgumentNullException("info");
            if(frame==null)throw new ArgumentNullException("frame");
            if(Math.Abs(info.SpacingX-frame.SpacingX)>Math.Max(1e-6,frame.SpacingX*1e-6) ||
               Math.Abs(info.SpacingY-frame.SpacingY)>Math.Max(1e-6,frame.SpacingY*1e-6) ||
               Math.Abs(info.SpacingZ-frame.SpacingZ)>Math.Max(1e-6,frame.SpacingZ*1e-6))
                throw new ArgumentException("RAW元数据与空间Frame间距不一致。");
            string file=CT3DArtifactFiles.Resolve(root,relativePath);
            using(var stream=CT3DArtifactFiles.OpenRead(file))
                return new CT3DAssetReference(assetId??Guid.NewGuid(),revision,relativePath,
                    CT3DArtifactFiles.Hash(stream,cancellation),stream.Length,
                    info.Width,info.Height,info.Depth,info.Format,frame);
        }

        /// <summary>流式复制到案例目录的内容寻址RAW，保留输入原文件；不使用全量byte[]。</summary>
        public static CT3DAssetReference Import(string root,string sourceFile,
            FVORawVolumeInfo info,FVOVolumeSpatialFrame frame,long revision,
            CancellationToken cancellation,Guid? assetId=null)
        {
            string relative=CT3DArtifactFiles.Publish(root,".raw",output=>
            {
                using(var input=CT3DArtifactFiles.OpenRead(Path.GetFullPath(sourceFile)))
                    CT3DArtifactFiles.Copy(input,output,cancellation);
            },cancellation);
            return Capture(root,relative,info,frame,revision,cancellation,assetId);
        }

        internal FileStream OpenVerified(string root,CancellationToken cancellation)
        {
            var stream=CT3DArtifactFiles.OpenRead(CT3DArtifactFiles.Resolve(root,RelativePath));
            try
            {
                if(stream.Length!=ByteLength ||
                    !string.Equals(CT3DArtifactFiles.Hash(stream,cancellation),ContentHash,StringComparison.Ordinal))
                    throw new InvalidDataException("ct3d.source.changed：RAW来源已改变，旧结果失效。");
                stream.Position=0;
                return stream;
            }
            catch{stream.Dispose();throw;}
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(Id.ToByteArray());w.Write(Revision);w.Write(RelativePath);w.Write(ContentHash);
            w.Write(ByteLength);w.Write(Width);w.Write(Height);w.Write(Depth);w.Write((int)Format);
            var f=SpatialFrame;
            w.Write(f.SpacingX);w.Write(f.SpacingY);w.Write(f.SpacingZ);
            w.Write(f.OriginX);w.Write(f.OriginY);w.Write(f.OriginZ);
            foreach(double d in f.CopyDirectionRowMajor())w.Write(d);
            w.Write(f.IsCalibrated);
        }
        internal static CT3DAssetReference Read(BinaryReader r)
        {
            Guid id=new Guid(r.ReadBytes(16));long revision=r.ReadInt64();
            string path=CT3DArtifactFiles.ReadText(r,8192),hash=CT3DArtifactFiles.ReadText(r,64);long length=r.ReadInt64();
            int width=r.ReadInt32(),height=r.ReadInt32(),depth=r.ReadInt32();
            var format=(FVORawVoxelFormat)r.ReadInt32();
            double sx=r.ReadDouble(),sy=r.ReadDouble(),sz=r.ReadDouble();
            double ox=r.ReadDouble(),oy=r.ReadDouble(),oz=r.ReadDouble();
            var direction=new double[9];for(int i=0;i<9;i++)direction[i]=r.ReadDouble();
            var frame=new FVOVolumeSpatialFrame(sx,sy,sz,ox,oy,oz,direction,r.ReadBoolean());
            return new CT3DAssetReference(id,revision,path,hash,length,width,height,depth,format,frame);
        }
    }

    /// <summary>本专业Payload的文件IO，不登记工程、管理任务或另建资源目录服务。</summary>
    internal static class CT3DArtifactFiles
    {
        internal const long MaximumResultBytes=256L*1024*1024;
        internal static void CheckHash(string hash)
        {
            if(hash==null || hash.Length!=64)throw new InvalidDataException("SHA256无效。");
            foreach(char c in hash)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))
                throw new InvalidDataException("SHA256必须为小写十六进制。");
        }
        internal static void CheckRelativePath(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||path.Length>2048||Path.IsPathRooted(path)||path.IndexOf(':')>=0)
                throw new InvalidDataException("制品必须使用案例目录内的相对路径。");
            foreach(string part in path.Replace('\\','/').Split('/'))
                if(part=="."||part==".."||part.Length==0||part.EndsWith(".")||part.EndsWith(" "))
                    throw new InvalidDataException("制品相对路径无效。");
        }
        internal static string Resolve(string root,string relative)
        {
            CheckRelativePath(relative);
            string folder=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string full=Path.GetFullPath(Path.Combine(folder,relative));
            if(!full.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("制品路径越界。");
            for(string current=full;current!=null;current=Path.GetDirectoryName(current))
                if((File.Exists(current)||Directory.Exists(current))&&
                    (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                    throw new InvalidDataException("制品路径不得经过链接或重解析点。");
            return full;
        }
        internal static FileStream OpenRead(string path)
        {return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.SequentialScan);}
        internal static void Copy(Stream input,Stream output,CancellationToken cancellation)
        {
            byte[] buffer=new byte[1024*1024];int count;
            while((count=input.Read(buffer,0,buffer.Length))!=0)
            {cancellation.ThrowIfCancellationRequested();output.Write(buffer,0,count);}
            cancellation.ThrowIfCancellationRequested();
        }
        internal static string Hash(Stream stream,CancellationToken cancellation)
        {
            using(var sha=SHA256.Create())
            {
                byte[] buffer=new byte[1024*1024];int count;
                while((count=stream.Read(buffer,0,buffer.Length))!=0)
                {cancellation.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,buffer,0);}
                cancellation.ThrowIfCancellationRequested();sha.TransformFinalBlock(new byte[0],0,0);
                return Hex(sha.Hash);
            }
        }
        internal static string HashBytes(byte[] bytes)
        {using(var sha=SHA256.Create())return Hex(sha.ComputeHash(bytes));}
        internal static string ReadText(BinaryReader reader,int maximumBytes)
        {
            uint length=0;int shift=0;
            while(true)
            {
                byte part=reader.ReadByte();
                if(shift>=28)throw new InvalidDataException("字符串长度编码无效。");
                length|=(uint)(part&127)<<shift;
                if((part&128)==0)break;
                shift+=7;
            }
            if(length>maximumBytes)throw new InvalidDataException("字符串超出三维元数据预算。");
            byte[] bytes=reader.ReadBytes((int)length);
            if(bytes.Length!=length)throw new EndOfStreamException();
            return new UTF8Encoding(false,true).GetString(bytes);
        }
        private static string Hex(byte[] hash){return BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();}
        internal static string Publish(string root,string extension,Action<Stream> write,CancellationToken cancellation)
        {
            string directory=Resolve(root,"artifacts/ct3d");Directory.CreateDirectory(directory);
            string temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                {
                    write(output);cancellation.ThrowIfCancellationRequested();output.Flush(true);
                    output.Position=0;
                    string hash=Hash(output,cancellation);
                    string relative="artifacts/ct3d/"+hash+extension;
                    string final=Resolve(root,relative);
                    output.Close();
                    try{File.Move(temporary,final);}
                    catch(IOException)
                    {
                        if(!File.Exists(final))throw;
                        using(var existing=OpenRead(final))
                            if(Hash(existing,cancellation)!=hash)throw new InvalidDataException("已存在的内容寻址制品已损坏。");
                    }
                    return relative;
                }
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
