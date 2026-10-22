using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Text;
using CxVisual.Mechanism.Connectivity;

namespace CxVisual.TestCenter.UI
{
    public sealed class TCTWorkspaceAction
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public Action Execute { get; private set; }
        public Func<bool> CanExecute { get; private set; }
        public TCTWorkspaceAction(string id, string label, Action execute, Func<bool> canExecute = null)
        {
            Id = id; Label = label; Execute = execute;
            CanExecute = canExecute ?? (() => true);
        }
    }

    public interface ITCTWorkspaceFrame
    {
        string WorkspaceId { get; }
        IEnumerable<TCTWorkspaceAction> Commands { get; }
        event Action StateChanged;
        void OnWorkspaceClosed();
    }

    /// <summary>仅根据注册条目、真实Catalog计数呈现根页；空态不冒充场景和结果。</summary>
}
