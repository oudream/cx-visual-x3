using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace CxVisual.Foundation.Transport
{
    /// <summary>双向单生产者环，每端对应唯一读者；跨进程元数据由命名Mutex同步。</summary>
    internal sealed class FTRSharedMemoryRegion : IDisposable
    {
        private const long Magic = 0x46545253484d3031L; // FTRSHM01
        private const int Version=1;
        private const int HeaderSize=128;
        private readonly MemoryMappedFile _map;
        private readonly MemoryMappedViewAccessor _view;
        private readonly Mutex _guard;
        internal readonly EventWaitHandle DataA,DataB,SpaceA,SpaceB;
        private readonly int _slots,_stride;
        internal FTRSharedMemoryRegion(FTRSharedMemoryChannelConfiguration config)
        {
            _slots=config.Slots;_stride=checked(config.SlotBytes+4);
            string name=@"Local\CxVisual.FTR."+config.MapName;
            long size=checked(HeaderSize+2L*_slots*_stride);
            _guard=new Mutex(false,name+".lock");
            DataA=new EventWaitHandle(false,EventResetMode.AutoReset,name+".data.a");
            DataB=new EventWaitHandle(false,EventResetMode.AutoReset,name+".data.b");
            SpaceA=new EventWaitHandle(false,EventResetMode.AutoReset,name+".space.a");
            SpaceB=new EventWaitHandle(false,EventResetMode.AutoReset,name+".space.b");
            _map=MemoryMappedFile.CreateOrOpen(name+".map",size,MemoryMappedFileAccess.ReadWrite);
            _view=_map.CreateViewAccessor(0,size,MemoryMappedFileAccess.ReadWrite);
            bool acquired=false;
            try
            {
                acquired=Lock(CancellationToken.None,1000);
                if(!acquired)throw new TimeoutException("共享内存创建互斥锁超时");
                long magic=_view.ReadInt64(0);
                if(magic==0)
                {
                    _view.Write(8,Version);_view.Write(12,config.SlotBytes);
                    _view.Write(16,config.Slots);
                    _view.Write(32,0L);_view.Write(40,0L);_view.Write(48,0L);_view.Write(56,0L);
                    _view.Write(0,Magic);
                }
                else if(magic!=Magic || _view.ReadInt32(8)!=Version ||
                    _view.ReadInt32(12)!=config.SlotBytes || _view.ReadInt32(16)!=config.Slots)
                    throw new InvalidDataException("共享内存版本或槽位配置不匹配");
            }
            finally{if(acquired)_guard.ReleaseMutex();}
        }
        private bool Lock(CancellationToken ct,int timeout)
        {
            try
            {
                int result=WaitHandle.WaitAny(new WaitHandle[]{_guard,ct.WaitHandle},timeout);
                if(result==1)throw new OperationCanceledException(ct);
                return result==0;
            }
            catch(AbandonedMutexException){return true;} // 已取得被异常退出进程遗弃的互斥锁
        }
        private long Offset(FTRSharedMemorySide sender,long sequence)
        {
            return HeaderSize+(sender==FTRSharedMemorySide.B?(long)_slots*_stride:0)
                +(sequence%_slots)*_stride;
        }
        private static long WritePointer(FTRSharedMemorySide side)
        {return side==FTRSharedMemorySide.A?32L:48L;}
        private static long ReadPointer(FTRSharedMemorySide side)
        {return side==FTRSharedMemorySide.A?40L:56L;}
        internal bool TryWrite(FTRSharedMemorySide sender,byte[] bytes,int offset,int count,CancellationToken ct)
        {
            bool locked=Lock(ct,1000);
            if(!locked)return false;
            try
            {
                long write=_view.ReadInt64(WritePointer(sender)),read=_view.ReadInt64(ReadPointer(sender));
                if(write<read || write-read>_slots)throw new InvalidDataException("共享内存写入序号损坏");
                if(write-read==_slots)return false;
                long slot=Offset(sender,write);
                _view.Write(slot,count);
                _view.WriteArray(slot+4,bytes,offset,count);
                _view.Write(WritePointer(sender),checked(write+1));
                return true;
            }
            finally{_guard.ReleaseMutex();}
        }
        internal int TryRead(FTRSharedMemorySide receiver,byte[] target,CancellationToken ct)
        {
            FTRSharedMemorySide sender=receiver==FTRSharedMemorySide.A?FTRSharedMemorySide.B:FTRSharedMemorySide.A;
            bool locked=Lock(ct,1000);
            if(!locked)return 0;
            try
            {
                long write=_view.ReadInt64(WritePointer(sender)),read=_view.ReadInt64(ReadPointer(sender));
                if(write<read || write-read>_slots)throw new InvalidDataException("共享内存读取序号损坏");
                if(write==read)return 0;
                long slot=Offset(sender,read);
                int size=_view.ReadInt32(slot);
                if(size<1 || size>target.Length || size>_stride-4)throw new InvalidDataException("共享槽位长度损坏");
                if(_view.ReadArray(slot+4,target,0,size)!=size)throw new EndOfStreamException("共享槽位读取未完成");
                _view.Write(ReadPointer(sender),checked(read+1));
                return size;
            }
            finally{_guard.ReleaseMutex();}
        }
        internal void SignalData(FTRSharedMemorySide sender)
        {(sender==FTRSharedMemorySide.A?DataB:DataA).Set();}
        internal void SignalSpace(FTRSharedMemorySide receiver)
        {(receiver==FTRSharedMemorySide.A?SpaceB:SpaceA).Set();}
        internal EventWaitHandle Incoming(FTRSharedMemorySide receiver)
        {return receiver==FTRSharedMemorySide.A?DataA:DataB;}
        internal EventWaitHandle OutgoingSpace(FTRSharedMemorySide sender)
        {return sender==FTRSharedMemorySide.A?SpaceA:SpaceB;}
        public void Dispose()
        {
            _view.Dispose();_map.Dispose();DataA.Dispose();DataB.Dispose();
            SpaceA.Dispose();SpaceB.Dispose();_guard.Dispose();
        }
    }
}
