using System;
using System.Windows;
using AvalonDock.Layout;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Presentation.Wpf.RuntimeData;

namespace CxVisual.Studio
{
    public partial class MainWindow
    {
        private MDSDataHeap _studioRuntimeDataHeap;
        private bool _studioRuntimeDataPoliciesEditable;

        /// <summary>绑定真正的MRK/MDS运行Heap，Studio只负责展示，绝不拥有或关闭该Heap。</summary>
        public void AttachStudioRuntimeDataHeap(MDSDataHeap heap,
            bool allowRuntimePolicyChanges = false)
        {
            if (heap == null) throw new ArgumentNullException("heap");
            _studioRuntimeDataHeap = heap;
            _studioRuntimeDataPoliciesEditable = allowRuntimePolicyChanges;
            LayoutDocument doc = FindStudioContent("doc.runtime-data") as LayoutDocument;
            var view = doc == null ? null : doc.Content as MPWDataLifecycleWorkspaceControl;
            if (view != null) view.Bind(heap, allowRuntimePolicyChanges);
        }

        public void OpenStudioRuntimeDataWorkspace(MDSDataHeap heap,
            bool allowRuntimePolicyChanges = false)
        {
            AttachStudioRuntimeDataHeap(heap, allowRuntimePolicyChanges);
            OpenStudioDocument("doc.runtime-data");
        }

        private FrameworkElement CreateStudioRuntimeDataDocument()
        {
            var view = new MPWDataLifecycleWorkspaceControl();
            if (_studioRuntimeDataHeap != null)
                view.Bind(_studioRuntimeDataHeap, _studioRuntimeDataPoliciesEditable);
            return view;
        }

        private void ReleaseStudioRuntimeDataDocument()
        {
            object view;
            if (_studioViews.TryGetValue("doc.runtime-data", out view))
            {
                IDisposable disposable = view as IDisposable;
                if (disposable != null) disposable.Dispose();
                _studioViews.Remove("doc.runtime-data");
            }
        }
    }
}