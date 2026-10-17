using System;

namespace CxVisual.Foundation.Data.Types
{
    /// <summary>
    /// 可跨节点安全共享的内存字节视图；创建时一次复制获取私有所有权。
    /// 不返回原始byte[]或可写Memory，ToArray显式创建独立副本。
    /// 不承诺磁盘映射、非托管内存和GPU零拷贝（由M3专业适配）。
    /// </summary>
    public sealed class FDTReadOnlyBuffer
    {
        private readonly byte[] _data;

        public FDTReadOnlyBuffer(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            _data = (byte[])data.Clone();
        }

        public int Length { get { return _data.Length; } }

        public byte this[int index]
        {
            get { return _data[index]; }
        }

        public byte[] ToArray()
        {
            return (byte[])_data.Clone();
        }

        public void CopyTo(byte[] destination, int destinationOffset = 0)
        {
            if (destination == null) throw new ArgumentNullException("destination");
            if (destinationOffset < 0 || destinationOffset > destination.Length ||
                _data.Length > destination.Length - destinationOffset)
                throw new ArgumentOutOfRangeException("destinationOffset");
            Buffer.BlockCopy(_data, 0, destination, destinationOffset, _data.Length);
        }
    }
}
