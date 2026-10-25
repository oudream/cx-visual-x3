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
            _items[index] = item;
            Notify();
            return true;
        }

        public bool Remove(Guid id)
        {
            int index = _items.FindIndex(x => x.Id == id);
            if (index < 0) return false;
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
