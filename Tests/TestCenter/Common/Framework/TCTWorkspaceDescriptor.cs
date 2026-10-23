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
    public enum TCTWorkspaceNodeKind { Root, Group, Leaf }
    public enum TCTWorkspaceOpenPolicy { SingleInstance, PerSession, MultiInstance }

    public sealed class TCTWorkspaceDescriptor
    {
        public string WorkspaceId { get; private set; }
        public string ParentId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public TCTWorkspaceNodeKind Kind { get; private set; }
        public TCTWorkspaceOpenPolicy OpenPolicy { get; private set; }
        public Func<UserControl> Factory { get; private set; }
        // 仅TestCenter窗口实例身份，不等于MEM工程对象，也不复制正式MHS模块配置。
        public bool IsCreatedInstance { get; private set; }
        public string TemplateId { get; private set; }
        public TCTWorkspaceDescriptor(string id, string parent, string title, string description,
            TCTWorkspaceNodeKind kind, Func<UserControl> factory = null,
            TCTWorkspaceOpenPolicy openPolicy = TCTWorkspaceOpenPolicy.SingleInstance,
            string templateId = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("WorkspaceId and Title are required");
            WorkspaceId = id; ParentId = parent; Title = title; Description = description ?? "";
            Kind = kind; Factory = factory; OpenPolicy = openPolicy;
            TemplateId = templateId;
            IsCreatedInstance = !string.IsNullOrEmpty(templateId);
        }
    }

    /// <summary>TestCenter UI导航注册表。不是MEM工程树，也不复制TCT Catalog或Run状态。</summary>
}
