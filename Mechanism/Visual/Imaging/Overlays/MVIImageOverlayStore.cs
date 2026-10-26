using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 直接承接旧OverlayStore的唯一列表+四图层可见性，增加快照、去重及Observable变更通知。
    /// 操作应由所有者UI线程发起；不隐含另一个Undo、持久工程或异步队列。
    /// </summary>
    public sealed class MVIImageOverlayStore
    {
        private readonly List<MVIImageOverlayItem> _items = new List<MVIImageOverlayItem>();
        private readonly Dictionary<MVIImageOverlayLayer, bool> _visibility =
            new Dictionary<MVIImageOverlayLayer, bool>();
        public event EventHandler Changed;
        public MVIImageOverlayStore()
        {
            foreach (MVIImageOverlayLayer layer in Enum.GetValues(typeof(MVIImageOverlayLayer)))
                _visibility.Add(layer, true);
        }

        public IReadOnlyList<MVIImageOverlayItem> Items
        { get { return _items.ToArray(); } }

        public MVIImageOverlayItem Add(MVIImageOverlayItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            if (_items.Any(x => x.Id == item.Id)) throw new InvalidOperationException("相同ID的叠加项已经存在。");
            _items.Add(item);
            Notify();
            return item;
        }

        public MVIImageOverlayItem Find(Guid id)
        { return _items.FirstOrDefault(x => x.Id == id); }

        public bool Replace(MVIImageOverlayItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            int index = _items.FindIndex(x => x.Id == item.Id);
            if (index < 0) return false;
            if (ReferenceEquals(_items[index], item)) return true;
            if (_items[index].IsLocked)
                throw new InvalidOperationException("对象已锁定；请先通过Store正式解锁。");
            _items[index] = item;
            Notify();
            return true;
        }

        /// <summary>锁定状态由当前唯一OverlayStore管理；模型快照保留同一Guid。</summary>
        public bool SetLocked(Guid id,bool locked)
        {
            int index=_items.FindIndex(x=>x.Id==id);
            if (index<0) return false;
            if (_items[index].IsLocked==locked) return true;
            _items[index]=_items[index].WithLocked(locked);
            Notify();
            return true;
        }

        /// <summary>当前图层栈中的单步置前/置后；UI与几何拾取共用顺序。</summary>
        public bool MoveZOrder(Guid id,int direction)
        {
            if (direction!=-1 && direction!=1)
                throw new ArgumentOutOfRangeException("direction");
            int index=_items.FindIndex(x=>x.Id==id);
            if (index<0) return false;
            int target=index+direction;
            if (target<0 || target>=_items.Count) return false;
            if (_items[index].IsLocked || _items[target].IsLocked)
                throw new InvalidOperationException("锁定对象不可改变层叠位置。");
            MVIImageOverlayItem current=_items[index];
            _items[index]=_items[target];
            _items[target]=current;
            Notify();
            return true;
        }

        public bool Remove(Guid id)
        {
            int index = _items.FindIndex(x => x.Id == id);
            if (index < 0) return false;
            if (_items[index].IsLocked)
                throw new InvalidOperationException("对象已锁定；普通删除必须先解锁。");
            _items.RemoveAt(index);
            Notify();
            return true;
        }

        public void Clear(MVIImageOverlayLayer? layer = null)
        {
            int removed = layer.HasValue ?
                _items.RemoveAll(item => item.Layer == layer.Value) : _items.Count;
            if (!layer.HasValue) _items.Clear();
            if (removed > 0) Notify();
        }

        public bool IsLayerVisible(MVIImageOverlayLayer layer)
        {
            if (!_visibility.ContainsKey(layer)) throw new ArgumentOutOfRangeException("layer");
            return _visibility[layer];
        }

        public void SetLayerVisible(MVIImageOverlayLayer layer, bool visible)
        {
            if (!_visibility.ContainsKey(layer)) throw new ArgumentOutOfRangeException("layer");
            if (_visibility[layer] == visible) return;
            _visibility[layer] = visible;
            Notify();
        }

        public IReadOnlyList<MVIImageOverlayItem> GetVisibleItems()
        { return _items.Where(x => x.IsVisible && _visibility[x.Layer]).ToArray(); }

        private void Notify()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
