# CxVisual Studio：Content / Symbol 独立管理中心——实现与验收（2026-10-30）

> **状态**：已提交 C# 源码与 WPF STA 自测；本次环境不具备 Windows/.NET Framework 4.8 构建环境，真实编译、Studio 自动测试和桌面 UIA 必须以固定 SHA 的 GitHub Actions 结果为准。本报告不将排队/取消的工作流视为成功。
>
> **上位规范**：[平台公共能力总目录 V1.0](../Architecture/CxVisual_平台公共能力总目录_V1.0.md)，[工程组织完整架构 V1.2](../Architecture/CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md)。
>
> **Owner 边界**：MEMProject/MEMContent/MEMSymbol、MPMTypeDescriptor、MEMEngineeringSession、MTI 资源包与 MTC 编辑事务为唯一工程真源；Studio 仅提供目录、操作、编辑导航和只读外部包加载。新内容没有增加 csproj 或第二套资源持久化。

## 1. 本轮真实源码落点

| 文件 | 实际实现 |
|---|---|
| `Applications/Studio/StudioShell.SharedResourceCenters.cs` | Content与Symbol两个独立Dock工作区、资源/包/类型三路分类、筛选、真实创建、类型化创建、原编辑器导航、外部包加载/预览/导入、资源导出、目标页面实例放置、依赖影响、实例参数覆盖/恢复继承、项目切换与包会话释放 |
| `Applications/Studio/StudioShell.cs` | `doc.content-center` 和 `doc.symbol-center` 两个正式DocumentId，AvalonDock打开/关闭/布局恢复、动态Ribbon/视图入口、工程刷新回调 |
| `Applications/Studio/StudioShell.ResourceTree.cs` | Project左侧 Content/Symbol 分组的右键和双击入口 |
| `Applications/Studio/CxVisual.Studio.csproj` | 显式包含新增源文件，保持现有 .NET Framework 4.8/WPF 工程 |
| `Applications/Studio/StudioShellSelfTests.cs` | 软件级 WPF STA 检查两个中心唯一身份、只读打开不修改 Revision、类型注册/类型化创建、引用实例及覆盖、资源包导入导出和不同中心独立关闭 |

## 2. 已接通的用户操作链

```text
可信代码的IMPMTypeProvider或MEM工程内既有MPMTypeDescriptor
  → RegisterStudioSharedResourceTypes（调用MEM.RegisterTypes，真实工程事务）
  → Content中心 / Symbol中心列出已注册类型
  → 按类型新建 MEMContent / MEMSymbol 或直接创建空白定义
  → 打开原有Studio ResourceDocument编辑器（Content可编辑视觉与流程；Symbol编辑RootGroup视觉）
  → 在当前Project共享定义目录检索/管理
  → 目标页面选择 → MEM.PlaceContent/PlaceSymbol创建实例引用
  → 选择某个实例与公开属性 → MEM.SetOverride/ClearOverride
  → MEM.AnalyzeResourceImpact 查询依赖与需重构建资源
  → MTI ExportPackage → 加载外部包（只读）→ PreviewImport(IncludeClosure) → 授权确认Import
```

**对象与版本约束**：
- 类型注册是`IMPMTypeProvider → MPMTypeDescriptor`的可信显式路径；不扫描未知外部 DLL、不执行注册表中的 CLR 工厂。跨程序集注解/Fluent 一次自动注册尚属 Decision V2.0 目标。
- 本工程 Content/Symbol 定义、外部包定义、注册类型三种列表**不同对象身份和生命周期**；外部包打开为只读 Session，不因浏览而写入当前 Project。
- 导入新实例使用 MTI 的身份映射与依赖处理，不通过 `new` 节点绕过校验、事务与授权。版本/Schema冲突交给现有 MEM/MTI 机制拒绝。
- 实例二次配置只编辑 `MRIResourceReference` 的公开参数覆盖；未公开/只读/类型不匹配/非法数值由 MEM 统一拒绝。
- Project 改变后两个中心重新读取真实 MEM Session；同中心重复打开只保留一个 Dock 文档。运行值/Message队列/Heap 不进入工程树。

## 3. 已编码的 WPF STA 反证场景

测试分组：`CAT-03/04 Content与Symbol双管理中心、类型化创建、实例及资源包往返`，位于 `StudioShellSelfTests.Run`。

1. 两个 `doc.*-center` 唯一工作区、打开不改工程 Revision。
2. 注册两种 Schema；创建真实 MEMContent/MEMSymbol，保留属性、唯一 RootGroup；错误 Kind 被拒。
3. 向一个 Page 放置 Content/Symbol 两个真实引用；覆盖 `Exposure`/`Caption`，原定义值保持不变；错误属性/Infinity 被拒，ClearOverride 恢复继承。
4. 分别 ExportPackage → ImportStudioSharedDefinition，验证新 ID、类型属性/依赖及错误包种类拒绝。
5. 依赖影响来自 `MEM.AnalyzeResourceImpact`；重复打开不产生第二个中心；关闭 Content 不销毁 Symbol。

**尚未通过/尚需补齐的验收**：
- Windows 编译及上述 STA 真实执行的固定 SHA 日志、CI 结果；需要关注并行提交导致取消。
- 真实界面人工 DPI/多显示器/焦点恢复/大量资源性能验收，外部资源版本升级、非空复杂 Aspect Remapper、跨 PC 包共享与设备使用场景。
- 来自代码注解的跨程序集自动发现、全局系统内容库/组件库的长期制品目录与版本锁；目前仅支持现有可信 Provider 类型注册、MEM 本工程定义及 MTI 资源包，不应称它们已全部完成。
- 更丰富的专业配置/Provider 实例化、安全身份绑定和 Studio 全局共享中心联动另按各正式 Owner 规格推进。

## 4. 保持不变的架构边界

1. `MEMProject` 是一个 Project 的持久定义根；中心不建立平行的可写 Resource Graph。
2. Content 和 Symbol 是被注册能力组合出的定义，而不是某个 CLR 类型/注册表项的别名；实例引用与共享定义分离。
3. 所有编辑、二次覆盖、导入、版本和 Undo 仍委托 MEM/MTC/MTI；UI 不缓存第二份真源。
4. `DOCS/02～04` 是 XLSX 设计基线，未在本次逐 Sheet 审查；新增公共类型、Owner、API 前仍需针对具体归属复核。
