using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    public enum MDSDataLoadState { NotFile, Unloaded, Loading, Loaded, Failed }

    /// <summary>可由专业方面提供CT Volume/图像ROI加载器，不让Data堆知道专业文件格式。</summary>
    public interface IMDSDataFileLoader
    {
        Type ValueType { get; }
        Task<object> LoadAsync(string absolutePath, CancellationToken cancellation);
    }

    /// <summary>受控加载器包装。用户实现必须独立返回一个可由基础安全适配器管理的数据对象。</summary>
    public sealed class MDSDataFileLoader<T> : IMDSDataFileLoader
    {
        private readonly Func<string, CancellationToken, Task<T>> _load;
        public MDSDataFileLoader(Func<string, CancellationToken, Task<T>> load)
        {
            _load = load ?? throw new ArgumentNullException("load");
        }
        public Type ValueType { get { return typeof(T); } }
        public async Task<object> LoadAsync(string absolutePath, CancellationToken cancellation)
        { return (object)await _load(absolutePath, cancellation).ConfigureAwait(false); }
    }

    /// <summary>默认文件加载器明确限制全量读取大小；大CT文件必须走专业的分块/ROI加载器。</summary>
    public sealed class MDSLimitedBinaryFileLoader : IMDSDataFileLoader
    {
        private readonly long _maxBytes;
        public MDSLimitedBinaryFileLoader(long maxBytes = 64L * 1024 * 1024)
        {
            if (maxBytes < 1 || maxBytes > int.MaxValue)
                throw new ArgumentOutOfRangeException("maxBytes");
            _maxBytes = maxBytes;
        }
        public Type ValueType { get { return typeof(byte[]); } }
        public async Task<object> LoadAsync(string absolutePath, CancellationToken cancellation)
        {
            using (FileStream stream = new FileStream(absolutePath, FileMode.Open,
                FileAccess.Read, FileShare.Read, 65536, true))
            {
                if (stream.Length > _maxBytes)
                    throw new InvalidOperationException("文件超过安全单次加载限制，必须使用专业ROI/分块Loader");
                byte[] result = new byte[(int)stream.Length];
                int position = 0;
                while (position < result.Length)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int read = await stream.ReadAsync(result, position,
                        result.Length - position, cancellation).ConfigureAwait(false);
                    if (read == 0) throw new EndOfStreamException("文件读取中断");
                    position += read;
                }
                if (stream.ReadByte() != -1)
                    throw new InvalidOperationException("读取期间文件大小发生变化");
                return result;
            }
        }
    }

    /// <summary>磁盘源文件不归Heap拥有；这里只拥有路径描述与已加载的安全缓存。</summary>
    internal sealed class MDSDataFileEntry
    {
        internal readonly string Path;
        internal readonly Type Type;
        internal readonly IMDSDataFileLoader Loader;
        internal readonly long Length;
        internal readonly DateTime LastWriteUtc;
        internal MDSDataLoadState State;
        internal object Cache;
        internal int Generation;
        internal MDSDataFileEntry(string path, Type type, IMDSDataFileLoader loader,
            long size, DateTime modified)
        {
            Path=path; Type=type; Loader=loader; Length=size; LastWriteUtc=modified;
            State=MDSDataLoadState.Unloaded;
        }
        internal MDSDataFileEntry CopyLocation()
        { return new MDSDataFileEntry(Path,Type,Loader,Length,LastWriteUtc); }
    }

    public sealed partial class MDSDataHeap
    {
        private readonly Dictionary<string, MDSDataFileEntry> _files =
            new Dictionary<string, MDSDataFileEntry>(StringComparer.Ordinal);

        private static void VerifyFile(MDSDataFileEntry entry)
        {
            FileInfo info = new FileInfo(entry.Path);
            info.Refresh();
            if (!info.Exists || info.Length != entry.Length ||
                info.LastWriteTimeUtc != entry.LastWriteUtc)
                throw new IOException("磁盘源文件已改变或丢失：" + entry.Path);
        }

        /// <summary>仅登记文件，不读取、复制或接管删除磁盘源文件。</summary>
        public void SetFile<T>(string name, string path, IMDSDataFileLoader loader)
        {
            CheckName(name);
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("文件路径为空", "path");
            if (loader == null) throw new ArgumentNullException("loader");
            if (loader.ValueType != typeof(T)) throw new ArgumentException("加载器类型不匹配", "loader");
            string absolute = Path.GetFullPath(path);
            FileInfo file = new FileInfo(absolute);
            file.Refresh();
            if (!file.Exists) throw new FileNotFoundException("文件不存在", absolute);
            MDSDataFileEntry entry = new MDSDataFileEntry(absolute, typeof(T), loader,
                file.Length, file.LastWriteTimeUtc);
            lock (_gate)
            {
                EnsureOpen();
                if (_values.ContainsKey(name) || _files.ContainsKey(name))
                    throw new MDSDataException(MDSDataErrorCode.NameConflict, "数据名已存在：" + name, name);
                _files.Add(name, entry);
            }
        }

        public void SetFile(string name, string path)
        { SetFile<byte[]>(name, path, new MDSLimitedBinaryFileLoader()); }

        public async Task<T> LoadAsync<T>(string name, CancellationToken cancellation = default(CancellationToken))
        {
            CheckName(name);
            MDSDataFileEntry entry;
            int generation;
            lock (_gate)
            {
                EnsureOpen();
                if (!_files.TryGetValue(name, out entry))
                    throw new KeyNotFoundException("未注册文件数据：" + name);
                if (entry.Type != typeof(T)) throw new InvalidCastException("加载目标类型不匹配：" + name);
                if (entry.State == MDSDataLoadState.Loading)
                    throw new MDSDataException(MDSDataErrorCode.Busy, "已有文件加载进行中：" + name, name);
                if (entry.State == MDSDataLoadState.Loaded)
                    return (T)CaptureValue(entry.Cache);
                entry.State = MDSDataLoadState.Loading;
                generation = ++entry.Generation;
            }
            object loaded = null;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                VerifyFile(entry);
                loaded = await entry.Loader.LoadAsync(entry.Path, cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (loaded == null || !entry.Type.IsInstanceOfType(loaded))
                    throw new InvalidCastException("文件加载器返回值类型不符合契约");
                object owned = CaptureValue(loaded);
                VerifyFile(entry);
                lock (_gate)
                {
                    EnsureOpen();
                    MDSDataFileEntry current;
                    if (!_files.TryGetValue(name, out current) ||
                        !object.ReferenceEquals(current, entry) || entry.Generation != generation)
                        throw new InvalidOperationException("文件加载结果过期，禁止发布");
                    entry.Cache = owned;
                    entry.State = MDSDataLoadState.Loaded;
                    return (T)CaptureValue(owned);
                }
            }
            catch
            {
                lock (_gate)
                {
                    MDSDataFileEntry current;
                    if (!_closed && _files.TryGetValue(name, out current) &&
                        object.ReferenceEquals(current, entry) && entry.Generation == generation)
                    { entry.State = MDSDataLoadState.Failed; entry.Cache = null; }
                }
                throw;
            }
        }

        public void Unload(string name)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureOpen();
                MDSDataFileEntry file;
                if (!_files.TryGetValue(name, out file))
                    throw new InvalidOperationException("Unload只适用于磁盘定位数据：" + name);
                if (file.State == MDSDataLoadState.Loading)
                    throw new MDSDataException(MDSDataErrorCode.Busy, "加载中不可卸载：" + name, name);
                file.Cache = null;
                file.State = MDSDataLoadState.Unloaded;
                file.Generation++;
            }
        }

        private bool HasFile(string name) { return _files.ContainsKey(name); }
    }
}
