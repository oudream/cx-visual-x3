using System;
using System.Windows.Threading;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering
{
    /// <summary>可视化资源失效分类，不依赖CT3D专业程序集。</summary>
    [Flags]
    public enum MPWVolumeRenderDirty
    {
        None=0, TransferLut=1, Uniforms=2, Scene=4, Slice=8, Camera=16, Volume=32
    }

    /// <summary>
    /// UI线程上合并刷新，区分已提交/已应用修订，Context代次废弃迟到回调。
    /// 不保存Volume、专业Settings或GL对象；由正式RenderHost提供提交动作。
    /// </summary>
    public sealed class MPWVolumeRenderScheduler : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<MPWVolumeRenderDirty> _apply;
        private MPWVolumeRenderDirty _pending;
        private long _epoch, _revision, _appliedRevision, _postTicket;
        private bool _queued, _disposed;
        public MPWVolumeRenderDirty Pending { get { return _pending; } }
        public long Epoch { get { return _epoch; } }
        public long Revision { get { return _revision; } }
        public long AppliedRevision { get { return _appliedRevision; } }
        public bool IsQueued { get { return _queued; } }

        public MPWVolumeRenderScheduler(Dispatcher dispatcher,Action<MPWVolumeRenderDirty> apply)
        {
            if(dispatcher==null)throw new ArgumentNullException("dispatcher");
            if(apply==null)throw new ArgumentNullException("apply");
            _dispatcher=dispatcher;_apply=apply;
        }

        public void Invalidate(MPWVolumeRenderDirty flags)
        {
            VerifyAccess();
            if(_disposed)throw new ObjectDisposedException("MPWVolumeRenderScheduler");
            const MPWVolumeRenderDirty known=MPWVolumeRenderDirty.TransferLut |
                MPWVolumeRenderDirty.Uniforms | MPWVolumeRenderDirty.Scene |
                MPWVolumeRenderDirty.Slice | MPWVolumeRenderDirty.Camera | MPWVolumeRenderDirty.Volume;
            if((flags & ~known)!=0)throw new ArgumentOutOfRangeException("flags");
            if(flags==MPWVolumeRenderDirty.None)return;
            _pending |= flags;
            _revision=checked(_revision+1);
            if(_queued)return;
            _queued=true;
            long queuedEpoch=_epoch,ticket=++_postTicket;
            _dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(delegate
            {
                if(_disposed || queuedEpoch!=_epoch || ticket!=_postTicket)return;
                _queued=false;
                FlushNow();
            }));
        }

        /// <summary>LUT必须同步可见时即时应用；其他变更在下一UI Render优先级合并。</summary>
        public void FlushNow()
        {
            VerifyAccess();
            if(_disposed)throw new ObjectDisposedException("MPWVolumeRenderScheduler");
            if(_pending==MPWVolumeRenderDirty.None)return;
            MPWVolumeRenderDirty flags=_pending;
            long epoch=_epoch,revision=_revision;
            _pending=MPWVolumeRenderDirty.None;
            _queued=false;
            ++_postTicket;
            try
            {
                _apply(flags);
                if(epoch==_epoch)_appliedRevision=revision;
            }
            catch
            {
                if(epoch==_epoch)_pending |= flags;
                throw;
            }
        }

        /// <summary>显卡上下文销毁与重建后拒绝全部旧更新。</summary>
        public void ResetContext(long epoch)
        {
            VerifyAccess();
            if(_disposed)throw new ObjectDisposedException("MPWVolumeRenderScheduler");
            if(epoch<_epoch)throw new ArgumentOutOfRangeException("epoch");
            _epoch=epoch;
            _pending=MPWVolumeRenderDirty.None;
            _queued=false;
            ++_postTicket;
        }

        public void Dispose()
        {
            VerifyAccess();
            if(_disposed)return;
            _disposed=true;
            _pending=MPWVolumeRenderDirty.None;
            _queued=false;
            ++_postTicket;
        }

        private void VerifyAccess()
        {
            if(!_dispatcher.CheckAccess())
                throw new InvalidOperationException("GPU刷新只能在WPF Dispatcher线程提交。");
        }
    }
}
