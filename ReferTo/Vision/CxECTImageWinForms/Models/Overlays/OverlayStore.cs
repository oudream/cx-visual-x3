using System;
using System.Collections.Generic;
using System.Linq;

namespace CxECTImageWinForms.Models.Overlays
{
    /// <summary>
    /// 叠加模型的唯一持有者；重绘时由它恢复图窗，不持有 HALCON 绘制对象。
    /// </summary>
    public sealed class OverlayStore
    {
        private readonly List<ImageOverlayItem> _items = new List<ImageOverlayItem>();
        private readonly Dictionary<ImageOverlayLayer, bool> _layerVisibility = new Dictionary<ImageOverlayLayer, bool>();

        public OverlayStore()
        {
            foreach (ImageOverlayLayer layer in Enum.GetValues(typeof(ImageOverlayLayer)))
            {
                _layerVisibility[layer] = true;
            }
        }

        public IEnumerable<ImageOverlayItem> Items
        {
            get { return _items; }
        }

        public void Add(ImageOverlayItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            _items.Add(item);
        }

        public bool Remove(Guid id)
        {
            var item = _items.FirstOrDefault(value => value.Id == id);
            if (item == null)
            {
                return false;
            }

            _items.Remove(item);
            return true;
        }

        public ImageOverlayItem Find(Guid id)
        {
            return _items.FirstOrDefault(value => value.Id == id);
        }

        public void Clear(ImageOverlayLayer? layer)
        {
            if (!layer.HasValue)
            {
                _items.Clear();
                return;
            }

            _items.RemoveAll(item => item.Layer == layer.Value);
        }

        public void SetLayerVisible(ImageOverlayLayer layer, bool visible)
        {
            _layerVisibility[layer] = visible;
        }

        public bool IsLayerVisible(ImageOverlayLayer layer)
        {
            return _layerVisibility[layer];
        }

        public IEnumerable<ImageOverlayItem> GetVisibleItems()
        {
            return _items.Where(item => item.IsVisible && IsLayerVisible(item.Layer));
        }
    }
}
