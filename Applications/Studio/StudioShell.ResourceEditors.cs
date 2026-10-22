using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AvalonDock.Layout;
using CxVisual.Mechanism.Core;

namespace CxVisual.Studio
{
    /// <summary>Studio UI编辑器描述，不取代MEM ResourceDescriptor或专业配置Schema。</summary>
    public sealed class StudioResourceEditorInfo
    {
        public string EditorId { get; private set; }
        public string Title { get; private set; }
        public bool IsDefault { get; private set; }
        internal StudioResourceEditorInfo(string id,string title,bool isDefault)
        {EditorId=id;Title=title;IsDefault=isDefault;}
    }

    /// <summary>单一MEM工程中的编辑器实例身份；Revision是打开时快照，不是持久化键。</summary>
    public sealed class StudioResourceDocumentIdentity
    {
        public Guid ProjectId { get; private set; }
        public Guid ResourceId { get; private set; }
        public string ResourceTypeId { get; private set; }
        public string EditorId { get; private set; }
        public Guid? InstanceId { get; private set; }
        public string ContentId { get; private set; }
        public int OpenRevision { get; private set; }
        internal StudioResourceDocumentIdentity(Guid projectId,Guid resourceId,
            string typeId,string editorId,Guid? instanceId,string key,int revision)
        {ProjectId=projectId;ResourceId=resourceId;ResourceTypeId=typeId;
         EditorId=editorId;InstanceId=instanceId;ContentId=key;OpenRevision=revision;}
    }

    public partial class MainWindow
    {
        private sealed class StudioViewerRegistration
        {
            internal readonly string Id,Title;
            internal readonly string[] TypeIds;
            internal readonly bool Default;
            internal readonly Func<StudioResourceDocumentIdentity,FrameworkElement> Factory;
            internal StudioViewerRegistration(string id,string title,string[] types,
                bool isDefault,Func<StudioResourceDocumentIdentity,FrameworkElement> factory)
            {Id=id;Title=title;TypeIds=types;Default=isDefault;Factory=factory;}
        }

        private sealed class StudioViewerDocument : IDisposable
        {
            internal readonly MEMEngineeringSession Session;
            internal readonly StudioResourceDocumentIdentity Identity;
            internal readonly FrameworkElement View;
            private bool _disposed;
            internal StudioViewerDocument(MEMEngineeringSession session,
                StudioResourceDocumentIdentity identity,FrameworkElement view)
            {Session=session;Identity=identity;View=view;}
            public void Dispose()
            {
                if(_disposed)return;
                _disposed=true;
                IDisposable disposable=View as IDisposable;
                if(disposable!=null)disposable.Dispose();
            }
        }

        private readonly Dictionary<string,StudioViewerRegistration> _studioViewerRegistry=
            new Dictionary<string,StudioViewerRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string,StudioViewerDocument> _studioViewerDocuments=
            new Dictionary<string,StudioViewerDocument>(StringComparer.Ordinal);

        /// <summary>
        /// 可信专业宿主贡献Viewer工厂。工厂必须使用自己的Professional Owner，
        /// Studio只负责MEM身份校验、Dock、激活、回收；不代管专业配置/运行状态。
        /// </summary>
        public void RegisterStudioResourceViewer(string editorId,string title,
            Func<StudioResourceDocumentIdentity,FrameworkElement> factory,
            bool isDefault,params string[] resourceTypeIds)
        {
            if(string.IsNullOrWhiteSpace(editorId)||editorId.Length>96||
                editorId.Any(c=>!char.IsLetterOrDigit(c)&&c!='.'&&c!='_'&&c!='-')||
                editorId=="mem.visual"||editorId=="mol.flow"||
                string.IsNullOrWhiteSpace(title)||factory==null||
                resourceTypeIds==null||resourceTypeIds.Length==0||
                resourceTypeIds.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>128))
                throw new ArgumentException("无效专业Viewer注册");
            if(_studioViewerRegistry.ContainsKey(editorId))
                throw new InvalidOperationException("EditorId重复："+editorId);
            string[] types=resourceTypeIds.Distinct(StringComparer.Ordinal).ToArray();
            if(isDefault&&_studioViewerRegistry.Values.Any(v=>v.Default&&
                v.TypeIds.Intersect(types,StringComparer.Ordinal).Any()))
                throw new InvalidOperationException("同一资源类型存在多个默认专业Viewer");
            _studioViewerRegistry.Add(editorId,new StudioViewerRegistration(
                editorId,title,types,isDefault,factory));
        }

        /// <summary>从当前MEM真源即时发现候选；不存在/已删除/跨工程对象一律拒绝。</summary>
        public IReadOnlyList<StudioResourceEditorInfo> GetStudioResourceEditors(Guid resourceId)
        {
            if(_viewModel==null||resourceId==Guid.Empty)
                return new StudioResourceEditorInfo[0];
            MEMEngineeringSession session=_viewModel.ProjectSession;
            MEMNode resource=session.Find(resourceId);
            if(resource==null||!(resource is IMEMResource))
                return new StudioResourceEditorInfo[0];
            List<StudioResourceEditorInfo> result=new List<StudioResourceEditorInfo>();
            bool standalone=session.Project.FindResource(resourceId)!=null;
            if(resource is MEMPage||standalone&&(resource is MEMContent||resource is MEMSymbol))
                result.Add(new StudioResourceEditorInfo("mem.visual","图元编辑",true));
            if(resource is MEMContent)
                result.Add(new StudioResourceEditorInfo("mol.flow","流程编排",false));
            foreach(StudioViewerRegistration item in _studioViewerRegistry.Values
                .Where(x=>x.TypeIds.Contains(resource.TypeId,StringComparer.Ordinal)))
                result.Add(new StudioResourceEditorInfo(item.Id,item.Title,item.Default));
            return result.OrderByDescending(x=>x.IsDefault&&_studioViewerRegistry.ContainsKey(x.EditorId))
                .ThenByDescending(x=>x.IsDefault)
                .ThenBy(x=>x.EditorId,StringComparer.Ordinal).ToArray();
        }

        /// <summary>选择真实资源可用的Editor，默认优先专业声明，否则保持现有MEM图元。</summary>
        public string OpenStudioResourceEditor(Guid resourceId,string editorId=null,
            Guid? instanceId=null)
        {
            MEMEngineeringSession session=_viewModel.ProjectSession;
            MEMNode resource=session.Find(resourceId);
            StudioResourceEditorInfo[] candidates=GetStudioResourceEditors(resourceId).ToArray();
            StudioResourceEditorInfo chosen=string.IsNullOrWhiteSpace(editorId)
                ?candidates.FirstOrDefault():candidates.FirstOrDefault(x=>x.EditorId==editorId);
            if(chosen==null||resource==null)
                throw new InvalidOperationException("资源不存在或未注册该Editor");
            if(chosen.EditorId=="mem.visual"||chosen.EditorId=="mol.flow")
            {
                if(instanceId.HasValue)
                    throw new InvalidOperationException("内置编辑器不支持实例化的DocumentKey");
                return OpenStudioResourceDocument(resourceId,chosen.EditorId=="mol.flow");
            }
            if(instanceId.HasValue&&instanceId.Value==Guid.Empty)
                throw new ArgumentException("实例身份不能为空Guid","instanceId");
            StudioViewerRegistration registration=_studioViewerRegistry[chosen.EditorId];
            string key="doc.editor."+session.Project.Id.ToString("N")+"."+
                resourceId.ToString("N")+"."+registration.Id+"."+
                (instanceId.HasValue?instanceId.Value.ToString("N"):"definition");
            LayoutDocument existing=FindStudioContent(key) as LayoutDocument;
            if(existing==null)
            {
                LayoutDocumentPane pane=StudioDock.Layout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault();
                if(pane==null)throw new InvalidOperationException("中央文档容器不存在");
                var identity=new StudioResourceDocumentIdentity(
                    session.Project.Id,resourceId,resource.TypeId,registration.Id,
                    instanceId,key,session.Revision);
                FrameworkElement view=registration.Factory(identity);
                if(view==null)throw new InvalidOperationException("专业Editor工厂未创建实际视图");
                var owned=new StudioViewerDocument(session,identity,view);
                var document=new LayoutDocument{ContentId=key,
                    Title=resource.Name+" ["+registration.Title+"]",
                    Content=view,CanClose=true,CanFloat=true};
                document.Closed+=(sender,args)=>ReleaseStudioViewerDocument(key);
                _studioViewerDocuments.Add(key,owned);
                try
                {
                    RefreshStudioCommandContextMenu(view,key);
                    pane.Children.Add(document);
                }
                catch
                {
                    _studioViewerDocuments.Remove(key);
                    owned.Dispose();
                    throw;
                }
                existing=document;
            }
            existing.IsActive=true;
            SelectStudioContext(key);
            RefreshStudioMenus();
            return key;
        }

        private void ReleaseStudioViewerDocument(string key)
        {
            StudioViewerDocument entry;
            if(!_studioViewerDocuments.TryGetValue(key,out entry))return;
            _studioViewerDocuments.Remove(key);
            entry.Dispose();
            if(_activeStudioDocumentId==key)
            {
                LayoutDocument next=StudioDock.Layout.ActiveContent as LayoutDocument;
                SelectStudioContext(next==null||next.ContentId==key?
                    "doc.home":next.ContentId);
            }
        }

        private bool IsStudioCustomEditorDocument(string key)
        {
            StudioViewerDocument entry;
            if(key==null||!_studioViewerDocuments.TryGetValue(key,out entry)||
                _viewModel==null)return false;
            MEMEngineeringSession session=_viewModel.ProjectSession;
            MEMNode resource=session.Find(entry.Identity.ResourceId);
            return ReferenceEquals(session,entry.Session)&&
                session.Project.Id==entry.Identity.ProjectId&&
                resource!=null&&resource.TypeId==entry.Identity.ResourceTypeId;
        }

        /// <summary>活动Tab的强身份，可在命令前再次核验Project和Resource。</summary>
        public StudioResourceDocumentIdentity GetStudioResourceDocumentIdentity(string key)
        {
            StudioViewerDocument viewer;
            if(IsStudioCustomEditorDocument(key)&&
                _studioViewerDocuments.TryGetValue(key,out viewer))
                return viewer.Identity;
            Guid id;string kind;
            if(!TryParseResourceDocumentId(key,out id,out kind)||
                !ResourceDocumentExists(key))return null;
            MEMEngineeringSession session=_viewModel.ProjectSession;
            MEMNode resource=session.Find(id);
            return new StudioResourceDocumentIdentity(session.Project.Id,id,
                resource.TypeId,kind=="flow"?"mol.flow":"mem.visual",null,key,
                session.Revision);
        }

        private void InvalidateStudioViewerDocuments(MEMEngineeringSession session,bool all)
        {
            foreach(string key in _studioViewerDocuments.Keys.ToArray())
            {
                StudioViewerDocument viewer=_studioViewerDocuments[key];
                if(!all&&ReferenceEquals(viewer.Session,session)&&
                    IsStudioCustomEditorDocument(key))continue;
                LayoutDocument document=FindStudioContent(key) as LayoutDocument;
                if(document!=null){document.Content=null;document.Close();}
                ReleaseStudioViewerDocument(key);
            }
        }
    }
}
