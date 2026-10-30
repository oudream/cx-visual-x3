using System;
using System.Windows;
using AvalonDock.Layout;
using CxVisual.Mechanism.Data.Messages;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Presentation.Wpf.Interaction;

namespace CxVisual.Studio
{
    public partial class MainWindow
    {
        private MNMMessageCenter _studioMessageHost;
        private MRKCommandCenter _studioCommandHost;
        private bool _studioMessagePublish;
        private bool _studioCommandInvoke;
        /// <summary>可信运行宿主注入其唯一MNM实例，Studio不构建影子消息中心。</summary>
        public void AttachStudioMessageCenter(MNMMessageCenter owner,bool allowPublish=false)
        {
            if(owner==null)throw new ArgumentNullException("owner");
            _studioMessageHost=owner;_studioMessagePublish=allowPublish;
            LayoutDocument doc=FindStudioContent("doc.messages") as LayoutDocument;
            MPWMessageCenterControl view=doc==null?null:doc.Content as MPWMessageCenterControl;
            if(view!=null)view.Bind(owner,allowPublish);
        }
        /// <summary>可信运行宿主注入原MRK实例；所有设备许可仍由接收Host独立复核。</summary>
        public void AttachStudioCommandCenter(MRKCommandCenter owner,bool allowInvoke=false)
        {
            if(owner==null)throw new ArgumentNullException("owner");
            _studioCommandHost=owner;_studioCommandInvoke=allowInvoke;
            LayoutDocument doc=FindStudioContent("doc.commands") as LayoutDocument;
            MPWCommandCenterControl view=doc==null?null:doc.Content as MPWCommandCenterControl;
            if(view!=null)view.Bind(owner,allowInvoke);
        }
        public void OpenStudioMessageCenter(MNMMessageCenter owner,bool allowPublish=false)
        {
            AttachStudioMessageCenter(owner,allowPublish);
            OpenStudioDocument("doc.messages");
        }
        public void OpenStudioCommandCenter(MRKCommandCenter owner,bool allowInvoke=false)
        {
            AttachStudioCommandCenter(owner,allowInvoke);
            OpenStudioDocument("doc.commands");
        }
        private FrameworkElement CreateStudioMessageCenterDocument()
        {
            MPWMessageCenterControl view=new MPWMessageCenterControl();
            if(_studioMessageHost!=null)view.Bind(_studioMessageHost,_studioMessagePublish);
            return view;
        }
        private FrameworkElement CreateStudioCommandCenterDocument()
        {
            MPWCommandCenterControl view=new MPWCommandCenterControl();
            if(_studioCommandHost!=null)view.Bind(_studioCommandHost,_studioCommandInvoke);
            return view;
        }
        private void ReleaseStudioInteractionDocument(string documentId)
        {
            object view;
            if(_studioViews.TryGetValue(documentId,out view))
            {
                IDisposable disposable=view as IDisposable;
                if(disposable!=null)disposable.Dispose();
                _studioViews.Remove(documentId);
            }
        }
    }
}
