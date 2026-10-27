using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Volume;
using CxVisual.Professional.CT3D;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 原 WinForms RAW预览优先 + 后台完整精度缓存逻辑的WPF迁移。
    /// 后台只读取FVO，界面只在Dispatcher线程更新；加载替换/关闭需取消旧请求。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private CancellationTokenSource _activeRawLoad;
        private CancellationTokenSource _activeRawCache;
        private CancellationTokenSource _activeBmpLoad;
        private int _lastLoadGeneration;
        public bool IsRawLoading { get; private set; }
        public bool IsBmpLoading { get; private set; }
        public bool HasPendingVolumeLoads
        {
            get { return IsRawLoading || IsBmpLoading || _activeRawCache!=null; }
        }
        private void UpdateLoadAvailability()
        {
            _cancelLoadButton.IsEnabled=HasPendingVolumeLoads;
            RaiseStateChanged();
        }
        /// <summary>保留当前已显示的FVO，仅取消尚未完成的RAW/BMP/高清缓存任务。</summary>
        public void CancelPendingVolumeLoads()
        {
            if(!HasPendingVolumeLoads)return;
            CancelPendingRawLoads();
            _status.Text="已请求取消后台体数据读取；当前已显示的体数据保持不变。";
            UpdateLoadAvailability();
        }

        /// <summary>与RAW共享加载世代号，切换数据或关闭时拒绝过期BMP结果。</summary>
        public async Task LoadBmpDirectoryAsync(string directory,CancellationToken cancellation)
        {
            if(String.IsNullOrWhiteSpace(directory))throw new ArgumentNullException("directory");
            CancelPendingRawLoads();
            CancellationTokenSource current=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            _activeBmpLoad=current;
            int generation=++_lastLoadGeneration;
            FVOVolumeData loaded=null;
            IsBmpLoading=true;
            UpdateLoadAvailability();
            _status.Text="正在后台读取 BMP 序列："+Path.GetFileName(
                directory.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
            Stopwatch elapsed=Stopwatch.StartNew();
            try
            {
                loaded=await Task.Run(()=>LoadBmpStack(directory,current.Token),current.Token);
                current.Token.ThrowIfCancellationRequested();
                if(generation!=_lastLoadGeneration)throw new OperationCanceledException();
                LoadVolume(loaded);
                loaded=null; // 所有权转移给现有FVO工作台，不创建副本。
                _status.Text=String.Format("BMP序列后台加载完成 {0:0} ms。",elapsed.ElapsedMilliseconds);
            }
            finally
            {
                if(loaded!=null)loaded.Dispose();
                if(Object.ReferenceEquals(_activeBmpLoad,current))
                {
                    _activeBmpLoad=null;
                    IsBmpLoading=false;
                }
                current.Dispose();
                UpdateLoadAvailability();
            }
        }

        public async Task LoadRawFileAsync(string filename,CancellationToken cancellation)
        {
            if(String.IsNullOrWhiteSpace(filename))throw new ArgumentNullException("filename");
            if(!File.Exists(filename))throw new FileNotFoundException("RAW文件不存在。",filename);
            CancelPendingRawLoads();
            CancellationTokenSource current=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            _activeRawLoad=current;
            int generation=++_lastLoadGeneration;
            FVOVolumeData loaded=null;
            IsRawLoading=true;
            UpdateLoadAvailability();
            _status.Text="正在后台读取 RAW 交互预览："+Path.GetFileName(filename);
            Stopwatch elapsed=Stopwatch.StartNew();
            try
            {
                loaded=await CT3DVolumeLoadService.LoadRawPreviewAsync(filename,current.Token);
                current.Token.ThrowIfCancellationRequested();
                if(generation!=_lastLoadGeneration)throw new OperationCanceledException();
                LoadVolume(loaded);
                loaded=null; // FVO唯一所有权已经转移给工作台
                _status.Text=String.Format("RAW预览加载完成 {0:0} ms；显示可交互，后台缓存高分辨率原始体...",
                    elapsed.ElapsedMilliseconds);
                if(_volume!=null)StartOriginalVolumeCache(_volume,generation);
            }
            finally
            {
                if(loaded!=null)loaded.Dispose();
                if(Object.ReferenceEquals(_activeRawLoad,current))
                {
                    _activeRawLoad=null;
                    IsRawLoading=false;
                }
                current.Dispose();
                UpdateLoadAvailability();
            }
        }

        private async void StartOriginalVolumeCache(FVOVolumeData volume,int generation)
        {
            if(volume==null || !volume.HasRawSource)return;
            // 预览及原值缓存决策统一由正式CT3D专业服务负责。
            if(!CT3DVolumeLoadService.NeedsOriginalCache(volume))
            {
                _status.Text+="；已有完整原始体，无需再次加载。";
                return;
            }
            bool mapped=CT3DVolumeLoadService.UsesMappedOriginalCache(volume);
            CancellationTokenSource cacheSource=new CancellationTokenSource();
            _activeRawCache=cacheSource;
            UpdateLoadAvailability();
            try
            {
                // UInt16/Float32保留真实原始精度，绝不能通过归一化8bit图恢复。
                FVOUnmanagedRawVolumeBuffer cache=await
                    CT3DVolumeLoadService.LoadOriginalCacheAsync(volume,cacheSource.Token);
                if(cacheSource.IsCancellationRequested||generation!=_lastLoadGeneration||
                    !Object.ReferenceEquals(volume,_volume))
                {
                    cache.Dispose();return;
                }
                volume.SetRawBuffer(cache);
                RefreshSlices();
                _status.Text+=mapped?"；原始RAW文件映射已就绪（按需高清切片）。":"；完整RAW缓存已就绪。";
            }
            catch(OperationCanceledException)
            {
                // 清空、更换体数据或关闭工作区时，后台缓冲停止且非托管内存自动释放。
            }
            catch(Exception exception)
            {
                if(Object.ReferenceEquals(volume,_volume))
                    _status.Text+="；原始缓存不可用："+exception.Message;
            }
            finally
            {
                if(Object.ReferenceEquals(_activeRawCache,cacheSource))
                    _activeRawCache=null;
                cacheSource.Dispose();
                UpdateLoadAvailability();
            }
        }

        private void CancelPendingRawLoads()
        {
            _lastLoadGeneration++;
            if(_activeRawLoad!=null)_activeRawLoad.Cancel();
            if(_activeRawCache!=null)_activeRawCache.Cancel();
            if(_activeBmpLoad!=null)_activeBmpLoad.Cancel();
        }
    }
}
