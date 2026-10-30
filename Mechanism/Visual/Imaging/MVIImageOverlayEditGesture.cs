using System;
using System.Linq;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>编辑操作类别仅表示短命手势；正式工程提交/Undo仍归MEM/MTC。</summary>
    public enum MVIImageOverlayEditKind { Move, Resize, Rotate, PolygonVertex }

    /// <summary>
    /// UI无关、单Owner的GoF State短事务：Begin -> Update* -> Complete/Cancel。
    /// 一次只能编辑唯一MVI OverlayStore中的一个对象；保存编辑前不可变快照。
    /// 取消只回滚本手势最后一次写入的实例，外部删除/锁定/替换时不得覆盖外部修改。
    /// 调用遵循OverlayStore现有UI线程串行约束；不建立第二个Overlay列表或Undo。
    /// </summary>
    public sealed class MVIImageOverlayEditGesture
    {
        private readonly MVIImageOverlayStore _store;
        private MVIImageOverlayItem _original;
        private MVIImageOverlayItem _lastWritten;
        private MVIImageOverlayEditKind? _kind;

        public MVIImageOverlayEditGesture(MVIImageOverlayStore store)
        { _store=store ?? throw new ArgumentNullException("store"); }

        public bool IsActive { get { return _original!=null; } }
        public MVIImageOverlayEditKind? Kind { get { return _kind; } }

        public bool Begin(MVIImageOverlayItem item,MVIImageOverlayEditKind kind)
        {
            if(item==null)throw new ArgumentNullException("item");
            if(!Enum.IsDefined(typeof(MVIImageOverlayEditKind),kind))
                throw new ArgumentOutOfRangeException("kind");
            if(IsActive)throw new InvalidOperationException("上一编辑手势未完成或取消。");
            if(item.IsLocked || !ReferenceEquals(_store.Find(item.Id),item) ||
               !_store.GetVisibleItems().Any(x=>x.Id==item.Id))
                return false;
            _original=item;
            _lastWritten=item;
            _kind=kind;
            return true;
        }

        /// <summary>仅当前手势拥有的不可变快照可推进；外部变更不允许被旧手势覆盖。</summary>
        public bool TryUpdate(MVIImageOverlayItem updated)
        {
            if(updated==null)throw new ArgumentNullException("updated");
            if(!IsActive)return false;
            if(updated.Id!=_original.Id || updated.Kind!=_original.Kind ||
               updated.Layer!=_original.Layer || updated.IsLocked)
                throw new ArgumentException("编辑只能保持原图元身份/类型/图层及解锁状态。","updated");
            if(!ReferenceEquals(_store.Find(_original.Id),_lastWritten) ||
               !_store.GetVisibleItems().Any(x=>x.Id==_original.Id))
            {
                Abandon(); // 外部写入已经胜出，不应回滚它。
                return false;
            }
            if(!_store.Replace(updated)){Abandon();return false;}
            _lastWritten=updated;
            return true;
        }

        public bool Complete()
        {
            if(!IsActive)return false;
            bool owned=ReferenceEquals(_store.Find(_original.Id),_lastWritten);
            if(owned && (!_store.GetVisibleItems().Any(x=>x.Id==_original.Id) ||
                         _lastWritten.IsLocked))
            {
                // 隐藏图层不更换Item实例；MouseUp不能因此提交不可见的草稿。
                // 恢复自己的最后写入，保留外部图层可见性/锁定设置。
                Cancel();
                return false;
            }
            Abandon();
            return owned;
        }

        /// <summary>取消只撤销自己的草稿更新，保持当前Store其他对象及外部修改。</summary>
        public bool Cancel()
        {
            if(!IsActive)return false;
            MVIImageOverlayItem original=_original,last=_lastWritten;
            Abandon();
            if(ReferenceEquals(original,last))return false;
            if(!ReferenceEquals(_store.Find(original.Id),last))return false;
            return _store.Replace(original);
        }

        private void Abandon()
        {
            _original=null;
            _lastWritten=null;
            _kind=null;
        }
    }
}
