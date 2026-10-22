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
    public sealed class TCTWorkspaceRegistry
    {
        private readonly Dictionary<string, TCTWorkspaceDescriptor> _items =
            new Dictionary<string, TCTWorkspaceDescriptor>(StringComparer.Ordinal);
        public IReadOnlyList<TCTWorkspaceDescriptor> Items { get { return _items.Values.ToArray(); } }
        public TCTWorkspaceDescriptor Find(string id)
        {
            TCTWorkspaceDescriptor item;
            return id != null && _items.TryGetValue(id, out item) ? item : null;
        }
        public void Register(TCTWorkspaceDescriptor item)
        {
            if (item == null) throw new ArgumentNullException("item");
            if (_items.ContainsKey(item.WorkspaceId))
                throw new InvalidOperationException("Duplicate WorkspaceId: " + item.WorkspaceId);
            _items.Add(item.WorkspaceId, item);
        }
        public void Validate()
        {
            foreach (TCTWorkspaceDescriptor entry in _items.Values)
            {
                if (entry.Kind == TCTWorkspaceNodeKind.Root && !string.IsNullOrEmpty(entry.ParentId))
                    throw new InvalidOperationException("Root cannot have parent: " + entry.WorkspaceId);
                if (entry.Kind != TCTWorkspaceNodeKind.Root &&
                    (string.IsNullOrEmpty(entry.ParentId) || !_items.ContainsKey(entry.ParentId)))
                    throw new InvalidOperationException("Missing workspace parent: " + entry.WorkspaceId);
                HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
                TCTWorkspaceDescriptor cursor = entry;
                while (cursor != null)
                {
                    if (!visited.Add(cursor.WorkspaceId))
                        throw new InvalidOperationException("Workspace parent cycle: " + entry.WorkspaceId);
                    cursor = Find(cursor.ParentId);
                }
            }
        }
        public IEnumerable<TCTWorkspaceDescriptor> Children(string parentId)
        {
            return _items.Values.Where(x => string.Equals(x.ParentId, parentId, StringComparison.Ordinal));
        }
        public static TCTWorkspaceRegistry CreateDefaults(Func<string, UserControl> rootFactory)
        {
            if (rootFactory == null) throw new ArgumentNullException("rootFactory");
            TCTWorkspaceRegistry registry = new TCTWorkspaceRegistry();
            registry.Register(new TCTWorkspaceDescriptor("manual.root", null, "专有测试（人工操作）",
                "真实人工操作的Channel、Protocol、Graphics与CT/3D工具",
                TCTWorkspaceNodeKind.Root, () => rootFactory("manual.root")));
            registry.Register(new TCTWorkspaceDescriptor("auto.root", null, "自动测试（NUnit/TCT）",
                "统一Catalog、Run、Attempt与Owner证据",
                TCTWorkspaceNodeKind.Root, () => rootFactory("auto.root")));
            registry.Register(new TCTWorkspaceDescriptor("scenes.root", null, "场景案例",
                "经真实工程及Owner核验的场景目录，尚未注册具体场景",
                TCTWorkspaceNodeKind.Root, () => rootFactory("scenes.root")));
            registry.Register(new TCTWorkspaceDescriptor("reports.root", null, "结果报告",
                "自动Run报告、人工操作记录与历史索引分离",
                TCTWorkspaceNodeKind.Root, () => rootFactory("reports.root")));
            registry.Register(new TCTWorkspaceDescriptor("auto.architecture-smoke", "auto.root",
                "ArchitectureSmoke（独立架构回归）",
                "调用原有ArchitectureSmoke.exe；保持原CLI独立可运行，展示真实进程输出与退出码",
                TCTWorkspaceNodeKind.Leaf, () => new TCTArchitectureSmokeFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.channel", "manual.root", "通道测试",
                "现有生产MCCVirtualChannel的可操作点对点收发，不是真TCP",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualChannelFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.protocol", "manual.root", "规约测试",
                "人工构帧及异常注入：待接入", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("manual.graphics", "manual.root", "图元绘制测试",
                "专用可编辑画布：待接入", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("manual.ct3d", "manual.root", "CT/3D测试",
                "体数据专业Owner未授权接入：待接入", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("reports.auto", "reports.root", "自动测试报告",
                "入口已注册；真实Run与导出由既有Results/History提供", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("reports.manual", "reports.root", "人工测试报告",
                "ManualSession报告尚未接入", TCTWorkspaceNodeKind.Leaf));
            registry.Validate();
            return registry;
        }
    }
}
