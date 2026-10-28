using System;

namespace CxVisual.Foundation.Volume
{
    /// <summary>
    /// FVO原始RAW只读租约：一次DangerousAddRef覆盖整个专业算法读取周期，不复制多GB体素。
    /// 可以在所属FVOVolumeData释放后继续读取；调用方必须在结束所有读取后Dispose租约。
    /// 同一租约的Dispose不得与正在执行的ReadScalar并发。
    /// </summary>
    public sealed class FVOOriginalScalarLease : IDisposable
    {
        private readonly FVOUnmanagedRawVolumeBuffer _owner;
        private readonly IntPtr _pointer;
        private readonly int _width,_height,_depth,_stride;
        private readonly FVORawVoxelFormat _format;
        private bool _disposed;
        public int Width {get{return _width;}}
        public int Height {get{return _height;}}
        public int Depth {get{return _depth;}}
        public FVORawVoxelFormat Format {get{return _format;}}
        /// <summary>获取租约时的不可变空间Frame，用于数值任务的原值+坐标一致性。</summary>
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}

        internal FVOOriginalScalarLease(FVOUnmanagedRawVolumeBuffer owner,
            FVORawVoxelFormat format,int width,int height,int depth,FVOVolumeSpatialFrame frame)
        {
            if(owner==null)throw new ArgumentNullException("owner");
            if(frame==null)throw new ArgumentNullException("frame");
            if(width<=0||height<=0||depth<=0)throw new ArgumentOutOfRangeException("width");
            int stride;
            switch(format)
            {
                case FVORawVoxelFormat.Byte:stride=1;break;
                case FVORawVoxelFormat.UInt16:stride=2;break;
                case FVORawVoxelFormat.Float32:stride=4;break;
                default:throw new ArgumentOutOfRangeException("format");
            }
            if(owner.ByteLength!=checked((long)width*height*depth*stride))
                throw new ArgumentException("租约原始缓冲大小与标量格式不匹配。","owner");
            bool acquired=false;
            try
            {
                owner.DangerousAddRef(ref acquired);
                IntPtr pointer=owner.DangerousGetHandle();
                if(pointer==IntPtr.Zero||pointer==new IntPtr(-1))
                    throw new ObjectDisposedException("owner","原值缓冲句柄无效。");
                _pointer=pointer;
                _owner=owner;
                _width=width;_height=height;_depth=depth;
                _format=format;_stride=stride;
                SpatialFrame=frame;
            }
            catch
            {
                if(acquired)owner.DangerousRelease();
                throw;
            }
        }

        public unsafe float ReadScalar(int x,int y,int z)
        {
            if(_disposed)throw new ObjectDisposedException("FVOOriginalScalarLease");
            if(x<0||x>=_width||y<0||y>=_height||z<0||z>=_depth)
                throw new ArgumentOutOfRangeException("x","原始体素索引越界，禁止钳制到边界。");
            long index=x+(long)y*_width+(long)z*_width*_height;
            byte* ptr=(byte*)_pointer.ToPointer()+checked(index*_stride);
            switch(_format)
            {
                case FVORawVoxelFormat.Byte:return *ptr;
                case FVORawVoxelFormat.UInt16:return *((ushort*)ptr);
                case FVORawVoxelFormat.Float32:return *((float*)ptr);
                default:throw new ArgumentOutOfRangeException("Format");
            }
        }

        public void Dispose()
        {
            if(_disposed)return;
            _disposed=true;
            _owner.DangerousRelease();
            GC.SuppressFinalize(this);
        }
    }
}
