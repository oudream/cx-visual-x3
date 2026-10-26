using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Volume;

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
        private int _lastLoadGeneration;
        public bool IsRawLoading { get; private set; }

        public async Task LoadRawFileAsync(string filename,CancellationToken cancellation)
        {
            if(String.IsNullOrWhiteSpace(filename))throw new ArgumentNullException("filename");
            if(!File.Exists(filename))throw new FileNotFoundException("RAW文件不存在。",filename);
            if(_activeRawLoad!=null)_activeRawLoad.Cancel();
            if(_activeRawCache!=null)_activeRawCache.Cancel();
            CancellationTokenSource current=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            _activeRawLoad=current;
            int generation=++_lastLoadGeneration;
            FVOVolumeData loaded=null;
            IsRawLoading=true;
            _status.Text="正在后台读取 RAW 交互预览："+Path.GetFileName(filename);
            Stopwatch elapsed=Stopwatch.StartNew();
            try
            {
                long bytes=new FileInfo(filename).Length;
                int axis=bytes>=512L*1024*1024?128:512;
                loaded=await Task.Run(()=>FVORawVolumeLoader.Load(filename,axis,current.Token),current.Token);
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
            }
        }

        private async void StartOriginalVolumeCache(FVOVolumeData volume,int generation)
        {
            if(volume==null || !volume.HasRawSource)return;
            // 8bit且没有降采样：预览数据即原始体素，无需再次整文件读取。
            if(volume.SourceFormat==FVORawVoxelFormat.Byte &&
               volume.Width==volume.OriginalWidth &&
               volume.Height==volume.OriginalHeight &&
               volume.Depth==volume.OriginalDepth)
            {
                _status.Text+="；8bit完整体无需额外缓存。";
                return;
            }
            FileInfo info=new FileInfo(volume.SourceFilePath);
            // 大RAW不再降级成永久128³预览，改用OS分页的只读文件映射；
            // 小RAW保留旧版顺序读取的高速非托管缓存。
            bool mapped=info.Length>512L*1024*1024;
            CancellationTokenSource cacheSource=new CancellationTokenSource();
            _activeRawCache=cacheSource;
            try
            {
                // UInt16/Float32保留真实原始精度，绝不能通过归一化8bit图恢复。
                FVOUnmanagedRawVolumeBuffer cache=await Task.Run(()=>
                    mapped?FVORawVolumeLoader.MapRawBuffer(volume,cacheSource.Token):
                    FVORawVolumeLoader.LoadRawBuffer(volume,new FVORawVolumeLoadTiming(),
                        cacheSource.Token),cacheSource.Token);
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
            }
        }

        private void CancelPendingRawLoads()
        {
            _lastLoadGeneration++;
            if(_activeRawLoad!=null)_activeRawLoad.Cancel();
            if(_activeRawCache!=null)_activeRawCache.Cancel();
        }
    }
}
