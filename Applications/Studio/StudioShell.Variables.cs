using System;
using System.Windows;
using AvalonDock.Layout;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Presentation.Wpf.Variables;

namespace CxVisual.Studio
{
    public partial class MainWindow
    {
        private MDSVariableTable _studioVariableScope;
        private MDSVariableProviderHost _studioVariableProvider;
        private bool _studioVariableWritesAllowed;

        // Studio只接受可信调用方现有的MDS运行Scope，不创建临时RTDB或第二工程定义树。
        public void AttachStudioVariableScope(MDSVariableTable table,
            MDSVariableProviderHost provider = null, bool allowRuntimeWrites = false)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (!ReferenceEquals(_studioVariableScope, table)) CloseStudioHmiRuntimeDocuments();
            _studioVariableScope = table;
            if (_viewModel != null) _viewModel.RefreshRuntimeVariableChoices();
            _studioVariableProvider = provider;
            _studioVariableWritesAllowed = allowRuntimeWrites;
            LayoutDocument opened = FindStudioContent("doc.variables") as LayoutDocument;
            MPWVariableWorkspaceControl view = opened == null ? null :
                opened.Content as MPWVariableWorkspaceControl;
            if (view != null) view.Bind(table, provider, allowRuntimeWrites);
        }

        public void OpenStudioVariableWorkspace(MDSVariableTable table,
            MDSVariableProviderHost provider = null, bool allowRuntimeWrites = false)
        {
            AttachStudioVariableScope(table, provider, allowRuntimeWrites);
            OpenStudioDocument("doc.variables");
        }

        private FrameworkElement CreateStudioVariableDocument()
        {
            MPWVariableWorkspaceControl view = new MPWVariableWorkspaceControl();
            if (_studioVariableScope != null)
                view.Bind(_studioVariableScope, _studioVariableProvider, _studioVariableWritesAllowed);
            return view;
        }

        private void ReleaseStudioVariableDocument()
        {
            object view;
            if (_studioViews.TryGetValue("doc.variables", out view))
            {
                IDisposable disposable = view as IDisposable;
                if (disposable != null) disposable.Dispose();
                _studioViews.Remove("doc.variables");
            }
        }
    }
}
