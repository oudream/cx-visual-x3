using System;
using CxVisual.Foundation.Imaging;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 继承旧ImageDocument/SourceVersion能力的UI无关图像会话。
    /// 仅拥有临时浏览状态；不拥有MEM工程文档、MRC图元或另一个Undo系统。
    /// </summary>
    public sealed class MVIImageSession
    {
        private readonly object _gate = new object();
        private FIMImageBuffer _original;
        private FIMImageBuffer _result;
        private MVIImageSlot _active = MVIImageSlot.Original;
        private long _sourceVersion;

        /// <summary>GoF Observer：会话提交状态变化时通知Viewer；事件始终在锁外触发。</summary>
        public event EventHandler Changed;

        public long SourceVersion
        {
            get { lock (_gate) return _sourceVersion; }
        }

        public MVIImageSlot ActiveSlot
        {
            get { lock (_gate) return _active; }
        }

        public FIMImageBuffer Original
        {
            get { lock (_gate) return _original; }
        }

        public FIMImageBuffer Result
        {
            get { lock (_gate) return _result; }
        }

        public FIMImageBuffer ActiveImage
        {
            get { lock (_gate) return _active == MVIImageSlot.Original ? _original : _result; }
        }

        public bool HasResult
        {
            get { lock (_gate) return _result != null; }
        }

        /// <summary>开始新的原图异步加载；只有最后一次加载令牌可以提交。</summary>
        public long BeginOriginalLoad()
        {
            lock (_gate) return checked(++_sourceVersion);
        }

        public bool TryCompleteOriginalLoad(long expectedVersion, FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            lock (_gate)
            {
                if (expectedVersion != _sourceVersion) return false;
                _original = image;
                _result = null;
                _active = MVIImageSlot.Original;
                checked { ++_sourceVersion; }
            }
            RaiseChanged();
            return true;
        }

        public long SetOriginal(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            long version;
            lock (_gate)
            {
                _original = image;
                _result = null;
                _active = MVIImageSlot.Original;
                version = checked(++_sourceVersion);
            }
            RaiseChanged();
            return version;
        }

        public long SetResult(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            long version;
            lock (_gate)
            {
                _result = image;
                version = checked(++_sourceVersion);
            }
            RaiseChanged();
            return version;
        }

        public void Select(MVIImageSlot slot)
        {
            bool changed;
            lock (_gate)
            {
                if (slot == MVIImageSlot.Original && _original == null)
                    throw new InvalidOperationException("原图不存在。");
                if (slot == MVIImageSlot.Result && _result == null)
                    throw new InvalidOperationException("结果图不存在。");
                if (slot != MVIImageSlot.Original && slot != MVIImageSlot.Result)
                    throw new ArgumentOutOfRangeException("slot");
                changed = _active != slot;
                _active = slot;
            }
            if (changed) RaiseChanged();
        }

        public long Clear()
        {
            long version;
            lock (_gate)
            {
                _original = null;
                _result = null;
                _active = MVIImageSlot.Original;
                version = checked(++_sourceVersion);
            }
            RaiseChanged();
            return version;
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
