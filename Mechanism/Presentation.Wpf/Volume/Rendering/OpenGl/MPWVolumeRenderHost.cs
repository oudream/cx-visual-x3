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
        }
        public void Enable(MVI3DCameraState camera,Action<MPWVolumeOpenGlControl> configure)
        {
            VerifyAccess();
            if(_state==GpuState.Disposed)throw new ObjectDisposedException("MPWVolumeRenderHost");
            if(configure==null)throw new ArgumentNullException("configure");
            if(_gpu!=null)return;
            FailureReason=null;_epoch=checked(_epoch+1);
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
                ReleaseGpu();
                _state=GpuState.Fallback;_fallback.Visibility=Visibility.Visible;Notify();
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
            ReleaseGpu();
            _state=GpuState.Fallback;_fallback.Visibility=Visibility.Visible;
            Notify();
        }
        public void Disable()
        {
            VerifyAccess();
            if(_state==GpuState.Disposed || (_state==GpuState.Fallback && _gpu==null))return;
            ReleaseGpu();
            _state=GpuState.Fallback;_fallback.Visibility=Visibility.Visible;
            Notify();
        }
        public void Retry(MVI3DCameraState camera,Action<MPWVolumeOpenGlControl> configure)
        {
            VerifyAccess();
            Disable();
            Enable(camera,configure);
        }
        private void ReleaseGpu()
        {
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
            ReleaseGpu();
            _fallback.Visibility=Visibility.Visible;
            _state=GpuState.Disposed;Notify();
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