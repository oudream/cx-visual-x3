using System;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering
{
    /// <summary>正式GPU启停/回退/重建Owner，保留既有OpenGL控件和WPF切面。</summary>
    public sealed class MPWVolumeRenderHost : IDisposable
    {
        public enum GpuState { Uninitialized, Initializing, Ready, Failed, Unavailable, Fallback, Disposing, Disposed }
        private readonly Grid _slot;
        private readonly UIElement _fallback;
        private MPWVolumeOpenGlControl _gpu;
        private GpuState _state;
        private long _epoch;
        private readonly MPWVolumeRenderScheduler _scheduler;
        private Action<MPWVolumeOpenGlControl,MPWVolumeRenderDirty> _applyUpdate;
        public long PendingRevision {get{return _scheduler.Revision;}}
        public long AppliedRevision {get{return _scheduler.AppliedRevision;}}
        public MPWVolumeRenderDirty PendingUpdates {get{return _scheduler.Pending;}}
        public event EventHandler StateChanged;
        public MPWVolumeOpenGlControl CurrentGpu {get{return _gpu;}}
        public GpuState State {get{return _state;}}
        public long GpuEpoch {get{return _epoch;}}
        public string FailureReason {get;private set;}
        public bool IsReady {get{return _state==GpuState.Ready && _gpu!=null && _gpu.IsAvailable;}}
        public MPWVolumeRenderHost(Grid slot,UIElement fallback)
        {
            if(slot==null)throw new ArgumentNullException("slot");
            if(fallback==null || !slot.Children.Contains(fallback))
                throw new ArgumentException("WPF回退视图必须在宿主Grid中。","fallback");
            _slot=slot;_fallback=fallback;
            _scheduler=new MPWVolumeRenderScheduler(slot.Dispatcher,ApplyPendingUpdates);
        }

        /// <summary>业务宿主将CT3D变更映射为MPW资源失效位，不产生MPW到CT3D的项目依赖。</summary>
        public void QueueUpdate(MPWVolumeRenderDirty dirty,
            Action<MPWVolumeOpenGlControl,MPWVolumeRenderDirty> apply)
        {
            VerifyAccess();
            if(_state==GpuState.Disposed)throw new ObjectDisposedException("MPWVolumeRenderHost");
            if(dirty==MPWVolumeRenderDirty.None)return;
            if(apply==null)throw new ArgumentNullException("apply");
            _applyUpdate=apply;
            if(IsReady)_scheduler.Invalidate(dirty);
        }

        public void FlushUpdates()
        {
            VerifyAccess();
            if(IsReady)_scheduler.FlushNow();
        }

        private void ApplyPendingUpdates(MPWVolumeRenderDirty dirty)
        {
            MPWVolumeOpenGlControl gpu=_gpu;
            if(!IsReady || gpu==null || _applyUpdate==null)return;
            try {_applyUpdate(gpu,dirty);}
            catch(Exception error){Fail("GPU调度刷新失败："+error.Message);}
        }

        public void Enable(MVI3DCameraState camera,Action<MPWVolumeOpenGlControl> configure)
        {
            VerifyAccess();
            if(_state==GpuState.Disposed)throw new ObjectDisposedException("MPWVolumeRenderHost");
            if(configure==null)throw new ArgumentNullException("configure");
            if(_gpu!=null)return;
            FailureReason=null;_epoch=checked(_epoch+1);
            _scheduler.ResetContext(_epoch);
            _state=GpuState.Initializing;Notify();
            try
            {
                MPWVolumeOpenGlControl next=new MPWVolumeOpenGlControl();
                _gpu=next;
                next.BindCamera(camera);
                next.AvailabilityChanged+=OnAvailabilityChanged;
                configure(next);
                _slot.Children.Add(next);
            }
            catch(Exception error){Fail("GPU启用失败："+error.Message);}
        }
        private void OnAvailabilityChanged(object sender,EventArgs args)
        {
            if(!Object.ReferenceEquals(sender,_gpu) || _state!=GpuState.Initializing)return;
            if(!_gpu.IsAvailable)
            {
                FailureReason=_gpu.FailureReason;
                _state=FailureReason!=null && FailureReason.IndexOf("OpenGL 3.3",
                    StringComparison.OrdinalIgnoreCase)>=0?GpuState.Unavailable:GpuState.Failed;
                try { ReleaseGpu(); }
                catch(Exception cleanup) { FailureReason+="；GPU释放异常："+cleanup.Message; }
                finally
                {
                    _state=GpuState.Fallback;
                    _fallback.Visibility=Visibility.Visible;
                    Notify();
                }
                return;
            }
            _state=GpuState.Ready;
            _fallback.Visibility=Visibility.Collapsed;
            Notify();
        }
        public void Fail(string reason)
        {
            VerifyAccess();
            if(_state==GpuState.Disposed)return;
            FailureReason=string.IsNullOrWhiteSpace(reason)?"GPU故障":reason;
            _state=GpuState.Failed;
            try { ReleaseGpu(); }
            catch(Exception cleanup) { FailureReason+="；GPU释放异常："+cleanup.Message; }
            finally
            {
                _state=GpuState.Fallback;
                _fallback.Visibility=Visibility.Visible;
                Notify();
            }
        }
        public void Disable()
        {
            VerifyAccess();
            if(_state==GpuState.Disposed || (_state==GpuState.Fallback && _gpu==null))return;
            try { ReleaseGpu(); }
            catch(Exception cleanup) { FailureReason="GPU关闭异常："+cleanup.Message; }
            finally
            {
                _state=GpuState.Fallback;
                _fallback.Visibility=Visibility.Visible;
                Notify();
            }
        }
        public void Retry(MVI3DCameraState camera,Action<MPWVolumeOpenGlControl> configure)
        {
            VerifyAccess();
            Disable();
            Enable(camera,configure);
        }
        private void ReleaseGpu()
        {
            // 即使WGL释放抛错，仍先废弃队列，禁止旧帧触碰销毁的Context。
            _scheduler.ResetContext(checked(++_epoch));
            _applyUpdate=null;
            MPWVolumeOpenGlControl previous=_gpu;
            _gpu=null;
            if(previous==null)return;
            previous.AvailabilityChanged-=OnAvailabilityChanged;
            previous.BindCamera(null);
            try
            {
                if(_slot.Children.Contains(previous))_slot.Children.Remove(previous);
            }
            finally{previous.Dispose();}
        }
        public void Dispose()
        {
            VerifyAccess();
            if(_state==GpuState.Disposed)return;
            _state=GpuState.Disposing;
            try { ReleaseGpu(); }
            finally
            {
                _fallback.Visibility=Visibility.Visible;
                _state=GpuState.Disposed;
                _scheduler.Dispose();
                Notify();
            }
        }
        private void VerifyAccess()
        {
            if(!_slot.Dispatcher.CheckAccess())
                throw new InvalidOperationException("GPU上下文必须由所属WPF Dispatcher线程管理。");
        }
        private void Notify()
        {
            EventHandler callback=StateChanged;
            if(callback!=null)callback(this,EventArgs.Empty);
        }
    }
}