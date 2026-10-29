using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>仅是可视引用类别，源资产/几何/工程定义仍由FVO/CT3D/MEM持有。</summary>
    public enum MVI3DSceneKind
    {
        Volume, Surface, Mesh, Cad, Mask, Roi, Measurement, Defect
    }

    /// <summary>不可变可视引用，不包含原始体素、专业量值、GPU纹理或工程树。</summary>
    public sealed class MVI3DSceneItem
    {
        internal MVI3DSceneItem(Guid id,MVI3DSceneKind kind,string sourceRef,
            long sourceRevision,string frameRef,bool visible,double opacity,
            string layerKey="default",string groupKey=null)
        {
            Id=id;Kind=kind;SourceRef=sourceRef;SourceRevision=sourceRevision;
            SpatialFrameRef=frameRef;Visible=visible;Opacity=opacity;
            LayerKey=layerKey;GroupKey=groupKey;
        }
        public Guid Id {get;private set;}
        public MVI3DSceneKind Kind {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        public string SpatialFrameRef {get;private set;}
        public bool Visible {get;private set;}
        public double Opacity {get;private set;}
        public string LayerKey {get;private set;}
        public string GroupKey {get;private set;}

        internal MVI3DSceneItem WithStyle(bool visible,double opacity)
        {
            return new MVI3DSceneItem(Id,Kind,SourceRef,SourceRevision,
                SpatialFrameRef,visible,opacity,LayerKey,GroupKey);
        }
        internal MVI3DSceneItem WithOrganization(string layerKey,string groupKey)
        {
            return new MVI3DSceneItem(Id,Kind,SourceRef,SourceRevision,
                SpatialFrameRef,Visible,Opacity,layerKey,groupKey);
        }
    }

    /// <summary>
    /// 单三维会话的可视引用、选择与呈现状态；不负责MEM提交、实体所有权及专业算法。
    /// 操作在调用者的UI/会话线程串行执行；快照可无锁安全读取。
    /// </summary>
    public sealed class MVI3DSceneState
    {
        private readonly List<MVI3DSceneItem> _items=new List<MVI3DSceneItem>();
        private long _revision;
        private Guid? _selected;
        public event EventHandler Changed;
        public long Revision {get{return _revision;}}
        public Guid? SelectedId {get{return _selected;}}
        public int Count {get{return _items.Count;}}

        /// <summary>SourceRef由现有工程/资源Owner提供；无持久Ref时须显式使用session临时引用。</summary>
        public Guid Add(MVI3DSceneKind kind,string sourceRef,long sourceRevision,
            string spatialFrameRef=null,double opacity=1)
        {
            if(!Enum.IsDefined(typeof(MVI3DSceneKind),kind))
                throw new ArgumentOutOfRangeException("kind");
            if(string.IsNullOrWhiteSpace(sourceRef))
                throw new ArgumentException("场景项必须明确引用已有资源或临时会话资源。","sourceRef");
            if(sourceRevision<0)throw new ArgumentOutOfRangeException("sourceRevision");
            CheckOpacity(opacity);
            Guid id=Guid.NewGuid();
            _items.Add(new MVI3DSceneItem(id,kind,sourceRef,sourceRevision,
                spatialFrameRef,true,opacity));
            ChangedNow();
            return id;
        }

        public ReadOnlyCollection<MVI3DSceneItem> Snapshot()
        {
            return new List<MVI3DSceneItem>(_items).AsReadOnly();
        }

        public bool TryGet(Guid id,out MVI3DSceneItem item)
        {
            item=_items.Find(x=>x.Id==id);
            return item!=null;
        }

        public void SetVisibility(Guid id,bool visible)
        {
            int index=FindIndex(id);
            MVI3DSceneItem item=_items[index];
            if(item.Visible==visible)return;
            _items[index]=item.WithStyle(visible,item.Opacity);
            if(!visible && _selected==id)_selected=null;
            ChangedNow();
        }

        public void SetOpacity(Guid id,double opacity)
        {
            CheckOpacity(opacity);
            int index=FindIndex(id);
            MVI3DSceneItem item=_items[index];
            if(item.Opacity==opacity)return;
            _items[index]=item.WithStyle(item.Visible,opacity);
            ChangedNow();
        }

        /// <summary>可视分层/分组只组织已有引用，不复制专业资源或修改MEM。</summary>
        public void SetOrganization(Guid id,string layerKey,string groupKey=null)
        {
            CheckOrganization(layerKey,groupKey);
            int index=FindIndex(id);
            MVI3DSceneItem item=_items[index];
            if(string.Equals(item.LayerKey,layerKey,StringComparison.Ordinal)&&
               string.Equals(item.GroupKey,groupKey,StringComparison.Ordinal))return;
            _items[index]=item.WithOrganization(layerKey,groupKey);
            ChangedNow();
        }

        public ReadOnlyCollection<MVI3DSceneItem> SnapshotLayer(string layerKey)
        {
            if(string.IsNullOrWhiteSpace(layerKey))throw new ArgumentException("图层名称不能为空。","layerKey");
            return _items.FindAll(x=>string.Equals(x.LayerKey,layerKey,StringComparison.Ordinal))
                .AsReadOnly();
        }

        public void SetGroupVisibility(string groupKey,bool visible)
        {
            if(string.IsNullOrWhiteSpace(groupKey))throw new ArgumentException("组标识不能为空。","groupKey");
            bool changed=false;
            for(int i=0;i<_items.Count;i++)
            {
                MVI3DSceneItem item=_items[i];
                if(item.GroupKey!=groupKey||item.Visible==visible)continue;
                _items[i]=item.WithStyle(visible,item.Opacity);
                if(!visible&&_selected==item.Id)_selected=null;
                changed=true;
            }
            if(changed)ChangedNow();
        }

        /// <summary>整组修改透明度；资源依然由原Owner持有，变更只发布一次。</summary>
        public void SetGroupOpacity(string groupKey,double opacity)
        {
            if(string.IsNullOrWhiteSpace(groupKey))throw new ArgumentException("组标识不能为空。","groupKey");
            CheckOpacity(opacity);
            bool changed=false;
            for(int i=0;i<_items.Count;i++)
            {
                MVI3DSceneItem item=_items[i];
                if(item.GroupKey!=groupKey||item.Opacity==opacity)continue;
                _items[i]=item.WithStyle(item.Visible,opacity);
                changed=true;
            }
            if(changed)ChangedNow();
        }

        private static void CheckOrganization(string layerKey,string groupKey)
        {
            if(string.IsNullOrWhiteSpace(layerKey)||layerKey.Length>128||
                (groupKey!=null&&(string.IsNullOrWhiteSpace(groupKey)||groupKey.Length>128)))
                throw new ArgumentException("场景图层或组身份无效。");
        }

        public void Select(Guid? id)
        {
            if(id.HasValue && !_items.Exists(x=>x.Id==id.Value && x.Visible))
                throw new InvalidOperationException("选择必须引用存在且可见的场景项。");
            if(_selected==id)return;
            _selected=id;
            ChangedNow();
        }

        public void Remove(Guid id)
        {
            int index=FindIndex(id);
            _items.RemoveAt(index);
            if(_selected==id)_selected=null;
            ChangedNow();
        }

        public void Clear()
        {
            if(_items.Count==0 && !_selected.HasValue)return;
            _items.Clear();_selected=null;
            ChangedNow();
        }

        private int FindIndex(Guid id)
        {
            int index=_items.FindIndex(x=>x.Id==id);
            if(index<0)throw new KeyNotFoundException("三维场景引用不存在："+id);
            return index;
        }

        private static void CheckOpacity(double opacity)
        {
            if(double.IsNaN(opacity)||double.IsInfinity(opacity)||opacity<0||opacity>1)
                throw new ArgumentOutOfRangeException("opacity");
        }

        private void ChangedNow()
        {
            _revision=checked(_revision+1);
            EventHandler handler=Changed;
            if(handler!=null)handler(this,EventArgs.Empty);
        }
    }
}
