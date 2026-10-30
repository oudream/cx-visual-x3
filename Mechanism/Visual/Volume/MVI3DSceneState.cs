using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>仅是可视引用类别，源资产/几何/工程定义仍由FVO/CT3D/MEM持有。</summary>
    public enum MVI3DSceneKind
    {
        Volume, Surface, Mesh, Cad, Mask, Roi, Measurement, Defect,
        // 只追加枚举成员，不改变已保存Schema1的旧Kind数值。
        PointCloud
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

        /// <summary>专业结果或工程树通过稳定资源引用定位可见场景项；
        /// 若同来源存在多个实例必须由调用方消歧，不能隐式选错对象。</summary>
        public bool TrySelectSource(string sourceRef,out Guid selectedId)
        {
            if(string.IsNullOrWhiteSpace(sourceRef))
                throw new ArgumentException("来源引用不能为空。","sourceRef");
            selectedId=Guid.Empty;
            MVI3DSceneItem match=null;
            foreach(MVI3DSceneItem item in _items)
            {
                if(!item.Visible||!string.Equals(item.SourceRef,sourceRef,StringComparison.Ordinal))
                    continue;
                if(match!=null)return false;
                match=item;
            }
            if(match==null)return false;
            selectedId=match.Id;
            Select(selectedId);
            return true;
        }

        /// <summary>从真正FGE double点云选择距离相机最近的可见实例，不以显示LOD拾取值做计量。</summary>
        public bool TryPickPointCloudRay(FGEPoint3D origin,FGEVector3D direction,
            double maximumDistance,double radius,Func<MVI3DSceneItem,FGEPointCloud3D> resolve,
            out Guid sceneId,out FGEPointCloudRayHit pointHit,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(resolve==null)throw new ArgumentNullException("resolve");
            if(!(direction.Length>1e-12)||double.IsInfinity(direction.Length)||
                double.IsNaN(maximumDistance)||double.IsInfinity(maximumDistance)||
                maximumDistance<=0||double.IsNaN(radius)||double.IsInfinity(radius)||radius<=0)
                throw new ArgumentOutOfRangeException("direction",
                    "场景点云拾取射线和物理半径必须有效。");
            sceneId=Guid.Empty;pointHit=null;
            long revision=Revision;
            foreach(MVI3DSceneItem item in Snapshot())
            {
                cancellation.ThrowIfCancellationRequested();
                if(!item.Visible||item.Kind!=MVI3DSceneKind.PointCloud)continue;
                FGEPointCloud3D cloud=resolve(item);
                if(cloud==null)continue;
                FGEPointCloudRayHit current;
                if(!cloud.TryPickRay(origin,direction,maximumDistance,radius,
                    out current,cancellation))continue;
                if(pointHit==null||current.RayDistance<pointHit.RayDistance||
                    (current.RayDistance==pointHit.RayDistance&&
                     current.RadialDistance<pointHit.RadialDistance))
                {
                    sceneId=item.Id;pointHit=current;
                }
            }
            if(revision!=Revision)
                throw new InvalidOperationException("拾取期间Scene来源/显隐已经改变，拒绝过期选择。");
            if(!sceneId.Equals(Guid.Empty))
            {
                Select(sceneId);
                return true;
            }
            return false;
        }

        /// <summary>专业来源提交新Revision时，批量撤销旧版本的可视实例；
        /// 不删除MEM工程对象或来源资产。结果整体一次通知。</summary>
        public int RemoveOlderSourceRevisions(string sourceRef,long currentRevision)
        {
            if(string.IsNullOrWhiteSpace(sourceRef))
                throw new ArgumentException("来源引用不能为空。","sourceRef");
            if(currentRevision<0)throw new ArgumentOutOfRangeException("currentRevision");
            int count=0;
            for(int i=_items.Count-1;i>=0;i--)
            {
                MVI3DSceneItem item=_items[i];
                if(!string.Equals(item.SourceRef,sourceRef,StringComparison.Ordinal)||
                    item.SourceRevision>=currentRevision)continue;
                _items.RemoveAt(i);
                if(_selected==item.Id)_selected=null;
                count++;
            }
            if(count!=0)ChangedNow();
            return count;
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

        /// <summary>完整验证后一次替换显示快照；不承接资产所有权。</summary>
        internal void RestoreItems(IReadOnlyList<MVI3DSceneItem> items,Guid? selected)
        {
            if(items==null||items.Count>256)throw new InvalidDataException("三维场景项目数量无效。");
            var ids=new HashSet<Guid>();
            bool selectedExists=!selected.HasValue;
            foreach(MVI3DSceneItem item in items)
            {
                if(item==null||item.Id==Guid.Empty||!ids.Add(item.Id)||
                    !Enum.IsDefined(typeof(MVI3DSceneKind),item.Kind)||
                    string.IsNullOrWhiteSpace(item.SourceRef)||item.SourceRef.Length>1024||
                    item.SourceRevision<0||
                    (item.SpatialFrameRef!=null&&item.SpatialFrameRef.Length>1024))
                    throw new InvalidDataException("三维Scene资源身份或版本无效。");
                CheckOrganization(item.LayerKey,item.GroupKey);
                CheckOpacity(item.Opacity);
                if(selected.HasValue&&selected.Value==item.Id)
                    selectedExists=item.Visible;
            }
            if(!selectedExists)throw new InvalidDataException("三维Scene选中对象不存在或不可见。");
            _items.Clear();
            foreach(MVI3DSceneItem item in items)_items.Add(item);
            _selected=selected;
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

    /// <summary>只将Scene视觉引用存进既有MEM Visual Aspect；保存/Undo由MEM持有。</summary>
    public static class MVI3DSceneLayoutPersistence
    {
        public const string ProviderId="mvi.scene3d.layout";
        private const int Magic=0x3349564d;
        private const int Schema=1;
        private static readonly UTF8Encoding StrictUtf8=new UTF8Encoding(false,true);

        public static MRIObjectAddress Commit(MEMEngineeringSession engineering,MEMContent owner,
            int expectedRevision,MVI3DSceneState scene,CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(owner==null||engineering.Find(owner.Id)!=owner)
                throw new ArgumentException("Scene布局须登记在当前MEM Content。","owner");
            if(scene==null)throw new ArgumentNullException("scene");
            if(engineering.Revision!=expectedRevision)
                throw new InvalidOperationException("org.version.conflict");
            MPMAspect aspect=CreateAspect(scene,cancellation);
            cancellation.ThrowIfCancellationRequested();
            engineering.Edit("提交三维Scene可视配置",expectedRevision,edit=>
            {
                edit.SetAspect(owner,aspect);
                return true;
            }).RequireValue();
            return engineering.GetAddress(owner);
        }

        /// <summary>独立创建可加入现有MEM事务的Visual Aspect，供专业结果与布局原子提交。</summary>
        public static MPMAspect CreateAspect(MVI3DSceneState scene,CancellationToken cancellation)
        {
            if(scene==null)throw new ArgumentNullException("scene");
            var snapshot=scene.Snapshot();
            if(snapshot.Count>256)throw new InvalidOperationException("Scene布局超过256引用预算。");
            byte[] payload;
            using(var memory=new MemoryStream())
            {
                using(var w=new BinaryWriter(memory,StrictUtf8,true))
                {
                    w.Write(Magic);w.Write(Schema);
                    w.Write(snapshot.Count);
                    w.Write(scene.SelectedId.HasValue);
                    if(scene.SelectedId.HasValue)w.Write(scene.SelectedId.Value.ToByteArray());
                    foreach(MVI3DSceneItem item in snapshot)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        w.Write(item.Id.ToByteArray());w.Write((int)item.Kind);
                        WriteText(w,item.SourceRef,1024);w.Write(item.SourceRevision);
                        WriteText(w,item.SpatialFrameRef??"",1024);
                        w.Write(item.Visible);w.Write(item.Opacity);
                        WriteText(w,item.LayerKey,128);WriteText(w,item.GroupKey??"",128);
                    }
                }
                payload=memory.ToArray();
            }
            if(payload.Length>262144)throw new InvalidOperationException("场景Visual Aspect超过256KiB预算。");
            return new MPMAspect(MPMAspectKind.Visual,ProviderId,Schema,payload,true);
        }

        /// <summary>从旧Scene生成新专业ResultId的Visual Aspect。
        /// 保持实例Id、组、透明度与选择不变；调用方只负责将旧专业来源映射到新来源。</summary>
        public static MPMAspect CreateReboundAspect(MVI3DSceneState previous,
            Func<MVI3DSceneItem,string> mapSource,CancellationToken cancellation)
        {
            if(previous==null)throw new ArgumentNullException("previous");
            if(mapSource==null)throw new ArgumentNullException("mapSource");
            var items=new List<MVI3DSceneItem>();
            foreach(MVI3DSceneItem old in previous.Snapshot())
            {
                cancellation.ThrowIfCancellationRequested();
                string updated=mapSource(old);
                if(string.IsNullOrWhiteSpace(updated)||updated.Length>1024)
                    throw new InvalidDataException("专业Scene来源重绑定失败，拒绝保留过期ResultId。");
                items.Add(new MVI3DSceneItem(old.Id,old.Kind,updated,
                    old.SourceRevision,old.SpatialFrameRef,old.Visible,old.Opacity,
                    old.LayerKey,old.GroupKey));
            }
            var rebound=new MVI3DSceneState();
            rebound.RestoreItems(items,previous.SelectedId);
            return CreateAspect(rebound,cancellation);
        }

        /// <summary>资源校验由实际Asset/Result Owner提供；任何未解析的Ref拒绝整体恢复。</summary>
        public static void Open(MEMEngineeringSession engineering,MRIObjectAddress address,
            MVI3DSceneState destination,Func<MVI3DSceneItem,bool> resolveSource,
            CancellationToken cancellation)
        {
            if(engineering==null)throw new ArgumentNullException("engineering");
            if(destination==null)throw new ArgumentNullException("destination");
            if(resolveSource==null)throw new ArgumentNullException("resolveSource");
            var owner=engineering.Resolve(address) as MEMContent;
            if(owner==null)throw new InvalidDataException("MEM场景Content不存在。");
            MPMAspect aspect=null;
            foreach(MPMAspect candidate in owner.Aspects)
                if(candidate.Kind==MPMAspectKind.Visual&&candidate.ProviderId==ProviderId)
                {
                    if(aspect!=null)throw new InvalidDataException("重复三维Scene Aspect。");
                    aspect=candidate;
                }
            if(aspect==null||aspect.SchemaVersion!=Schema||aspect.Payload.Length>262144)
                throw new InvalidDataException("三维Scene布局不存在或Schema无效。");
            var items=new List<MVI3DSceneItem>();
            Guid? selected;
            using(var memory=new MemoryStream(aspect.Payload,false))
            using(var r=new BinaryReader(memory,StrictUtf8,true))
            {
                if(r.ReadInt32()!=Magic||r.ReadInt32()!=Schema)
                    throw new InvalidDataException("三维Scene数据头无效。");
                int count=r.ReadInt32();
                if(count<0||count>256)throw new InvalidDataException("三维Scene数量超限。");
                selected=r.ReadBoolean()?new Guid(ReadExact(r,16)): (Guid?)null;
                for(int i=0;i<count;i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Guid id=new Guid(ReadExact(r,16));
                    MVI3DSceneKind kind=(MVI3DSceneKind)r.ReadInt32();
                    string source=ReadText(r,1024);long revision=r.ReadInt64();
                    string frame=ReadText(r,1024);bool visible=r.ReadBoolean();
                    double opacity=r.ReadDouble();string layer=ReadText(r,128);
                    string group=ReadText(r,128);
                    var item=new MVI3DSceneItem(id,kind,source,revision,
                        frame.Length==0?null:frame,visible,opacity,
                        layer,group.Length==0?null:group);
                    if(!resolveSource(item))
                        throw new InvalidDataException("三维Scene专业资源已失效："+source);
                    items.Add(item);
                }
                if(memory.Position!=memory.Length)
                    throw new InvalidDataException("三维Scene存在多余负载。");
            }
            cancellation.ThrowIfCancellationRequested();
            destination.RestoreItems(items,selected);
        }

        private static byte[] ReadExact(BinaryReader reader,int size)
        {
            byte[] bytes=reader.ReadBytes(size);
            if(bytes.Length!=size)throw new EndOfStreamException("三维场景数据被截断。");
            return bytes;
        }
        private static void WriteText(BinaryWriter writer,string value,int maxLength)
        {
            if(value==null||value.Length>maxLength)throw new ArgumentException("三维Scene文本超限。");
            byte[] bytes=StrictUtf8.GetBytes(value);
            if(bytes.Length>maxLength*4)throw new ArgumentException("三维Scene编码超预算。");
            writer.Write(bytes.Length);writer.Write(bytes);
        }
        private static string ReadText(BinaryReader reader,int maxLength)
        {
            int count=reader.ReadInt32();
            if(count<0||count>maxLength*4)throw new InvalidDataException("三维Scene文本长度无效。");
            return StrictUtf8.GetString(ReadExact(reader,count));
        }
    }
}
