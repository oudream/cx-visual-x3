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
        // 只删除用户创建的叶实例；不得删除四大根或类型注册。
        public bool RemoveCreated(string id)
        {
            TCTWorkspaceDescriptor found = Find(id);
            if (found == null || !found.IsCreatedInstance || Children(id).Any())
                return false;
            return _items.Remove(id);
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
        // 页面被关闭不移除注册；类型节点和已经创建的节点始终可区分。
        public TCTWorkspaceDescriptor RegisterCreated(TCTCreatedWorkspace item, Func<UserControl> factory)
        {
            if (item == null || factory == null) throw new ArgumentNullException("item/factory");
            TCTWorkspaceDescriptor template = Find(item.TemplateId);
            if (template == null || template.IsCreatedInstance)
                throw new InvalidOperationException("找不到有效工作区类型：" + item.TemplateId);
            TCTWorkspaceDescriptor created = new TCTWorkspaceDescriptor(
                item.Id, item.TemplateId, item.Title, template.Description,
                TCTWorkspaceNodeKind.Leaf, factory, TCTWorkspaceOpenPolicy.SingleInstance, item.TemplateId);
            Register(created);
            return created;
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
            registry.Register(new TCTWorkspaceDescriptor("auto.catalog", "auto.root",
                "NUnit/TCT 测试计划",
                "通过创建的计划页选择真实Catalog用例、运行并保存计划",
                TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("auto.architecture-smoke", "auto.root",
                "ArchitectureSmoke（独立架构回归）",
                "调用原有ArchitectureSmoke.exe；保持原CLI独立可运行，展示真实进程输出与退出码",
                TCTWorkspaceNodeKind.Leaf, () => new TCTArchitectureSmokeFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.module", "manual.root", "模块测试",
                "Connector、Driver、Compute、Simulation、Service 共用同一模块测试 Frame",
                TCTWorkspaceNodeKind.Group, () => new TCTManualModuleFrame()));
            foreach (string kind in new[] { "Connector", "Driver", "Compute", "Simulation", "Service" })
                registry.Register(new TCTWorkspaceDescriptor("manual.module." + kind.ToLowerInvariant(),
                    "manual.module", kind + " 模块",
                    "在唯一Module Frame中切换正式" + kind + "配置类型（非独立测试引擎）",
                    TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("manual.channel", "manual.root", "通道测试",
                "现有生产MCCVirtualChannel的可操作点对点收发，不是真TCP",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualChannelFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.message", "manual.root", "消息中心测试",
                "真实M36进程内Topic发布、订阅过滤、ReqReply、Retain和消息诊断；不是远程MQTT Broker",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualMessageFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.command", "manual.root", "命令中心测试",
                "真实MRKCommandCenter、FDC Schema、MDS参数、Operation控制与Windows命名管道",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualCommandFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.variable", "manual.root", "变量中心测试",
                "真实MDS变量/组织树/LatestOnly派发：定义、更新、分组与50K进程内压力；非现场PLC认证",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualVariableFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.imaging", "manual.root",
                "二维工业视觉工作台",
                "真实FIM/MVI/MPW二维图像：文件加载、16位浏览、ROI、分析、处理结果与PNG输出",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualImageViewerFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.protocol", "manual.root", "规约测试",
                "人工构帧及异常注入：待接入", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("manual.graphics", "manual.root", "图元绘制测试",
                "真实MEM/MVI/MPW图元操作：创建、连接、变换、Undo及Revision；GE逐项验收",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualGraphicsFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.ct3d", "manual.root", "CT/3D测试",
                "真实FVO/MVI/MPW三正交视图与WPF三维切面；OpenGL体绘仍在迁移", TCTWorkspaceNodeKind.Leaf, () => new TCTManualVolumeViewerFrame()));
            registry.Register(new TCTWorkspaceDescriptor("reports.auto", "reports.root", "自动测试报告",
                "入口已注册；真实Run与导出由既有Results/History提供", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("reports.manual", "reports.root", "人工测试报告",
                "ManualSession报告尚未接入", TCTWorkspaceNodeKind.Leaf));
            registry.Validate();
            return registry;
        }
    }
}
