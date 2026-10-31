# CxVisual Content 体系 #6——Studio 孵化、组合工作台与开发者 API V1.0

> 目标设计；覆盖 CNT-R55～R60。以 Applications/Studio 现有 Content/Symbol 双中心、StudioShell.ResourceDocuments.cs、MOLFlowCanvas、MVIVisualEditor、MEMEngineeringSession、MTC/MTI 为唯一落点；不是增加新 Studio/第二个 Undo 栈。

## 01. 不再把“新建空 Content”当成交付目标

应从“我要做什么”开始，而不是要求用户先认识 MEM/MPM/MPC/MOL。创建后立即给出**可组合资源、能力/子内容、公开接口、配置、预览/运行验证**；简单用户只需选模板并完成常见绑定，高级工程师可以精确调整四方面。

已有能力：Content/Symbol 两个 Dock 中心、工程/包/类型浏览、按类型创建、引用、覆盖、MTI Import/Export、原 ResourceDocument/Flow 编辑入口。未完成：目标驱动的完整孵化、跨方面无损装配、系统能力只读联动、专业全部适配、生产 HIL；不能因为中心能打开就写“工作台完成”。

## 02. 七种来源与统一创作旅程

| 入口 | 用户输入 | 系统只读预览 | 最终结果 |
|---|---|---|---|
| F1 空白 | Name/Scope/是否共享 | 合法父节点/空定义/默认身份 | MEMContent |
| F2 类型 | 已注册 Content Type | 属性/Port/Aspect/Schema 和依赖 | 类型化 MEMContent |
| F3 模板 | 专业工位/流程/数据处理模板 | 能力与资源缺口、可替换参数 | 派生资源/Ref |
| F4 已有资源 | 本工程/库 Content | Definition/Instance、版本/影响 | MRI Ref 或明确复制 |
| F5 提取 | Page/Content 选区、逻辑和绑定 | 公开边界/IdMap/外链/缺失 | 共享 Content + 可选替换引用 |
| F6 能力组合 | 一个或多个 Capability/Module | Port/参数/必需依赖/安全与目标 | Content 内合法能力/逻辑声明 |
| F7 导入/代码 | MTI 包、可信 Provider、SDK | 只读元数据/信任/版本/重映射 | 受控导入或合法新定义 |

统一流程：Explore（只读）→ Choose（明确身份）→ Compose（同源草稿）→ Expose（公开合同）→ Validate/Preview（未落盘诊断）→ Commit（MEM/MTC）→ Save/Build/Test/Publish（分别门禁）。导入不是安装 Provider，Discover 不执行外部程序集构造器，Preview 不修改工程 Revision。

## 03. Studio 信息架构（AvalonDock，不新增 Shell）

~~~text
Studio / Project-Host-Scope-Run Context
├─ 资源导航：Pages / Content Definitions / Symbol Definitions / 包与类型[只读]
├─ Content Center：发现、来源、模板、分类、搜索、依赖、版本
│  └─ Content Editor（原 ResourceDocument）
│     ├─ Overview：目标/说明/对外公开/依赖/状态
│     ├─ Outline：拥有、引用、版本与实例路径
│     ├─ Visual：Symbol/Group/专业 Viewer/显示和交互
│     ├─ Logic：MOL V1 或 V2 的唯一合法编辑器
│     ├─ Capability：按专业搜索、端口适配、装配建议
│     ├─ Data：变量/Frame声明/PortBinding/质量
│     ├─ Collaboration：消息/命令/资源/事件及权限
│     └─ Configuration：参数/Schema/覆盖/Saved/Effective只读事实
├─ Inspector：公共契约/私有成员/类型/Port/错误/版本
├─ Bottom：Preview/Build/Dependency/Trace/TestCenter/Run Diagnostics
└─ 专业 Workbench 提供自己的 Viewer/属性编辑适配，不换工程真源
~~~

这些是**同一定义的视图**，不是 8 份可分别 Save 的 Content 文件。当前没有对应控件时标目标，用原 Editor/MPW 扩展，不为占位新增每个 Tab 一个 ModelRepository。

## 04. 每个工作区必须完成的具体操作

- Overview：命名/职责、输入输出、内部能力、适用专业、依赖、状态证据等级；快速打开来源/调用者。
- Outline：可展开嵌套/引用并明确 Link vs Owned；Add/Remove/Extract/Copy/Migrate，重复定义只显示一次并按使用路径定位。
- Visual：画布选择 Symbol/Group、组件绑定/状态规则、专业Viewer；拖动不改流程。
- Logic：结构化/自由连线按当前 Schema 只显示一份编辑界面；Step、Ports、Control/Data Links、InvokeContent、公开 Input/Output、Validate、Trace Overlay。
- Capability：只读目录搜索→Decision 类型匹配/Unknown 原因→选择装配，不隐式 Install/Publish。
- Data：来源选择/单位/坐标/质量/Frame/Handle 性质与跨边界映射；实时值必须从真实 MDS Host 获取。
- Collaboration：Event→Command 请求映射、Message/Target、权限、资源与依赖；静态可编辑但现场未经批准不可执行。
- Configuration：Default/Definition/InstanceOverride/ApprovedSaved/Effective/Observed 分层展示，私有/安全字段拒绝无权操作。
- Test/Diagnosis：结构验证、MOC Build、软件仿真、Runtime Trace、专业证据，不能把这几个按钮都叫“运行成功”。

## 05. 从选区/已有内容提取共享 Content 的完整 UX

1. 选择合法 Content 子树及其绑定/逻辑块；确认作用域与 Revision。
2. 只读识别 Owned 树、所有内/外引用、公开跨界 Port/Property、配置/数据来源、MOL Block/Link 与外部连接。
3. 展示可转换项、无法映射项（Aspect Required、复杂逻辑区域、资源缺失）、IdMap、迁移后实例路径及源代码/版本影响。
4. 用户确认：复制保留/替换为引用；公开哪些功能与数据；是否包括闭包资源。
5. 一次 MEM/MTC Commit 创建共享定义与 Ref 并修改原连接；失败回滚、一次 Undo；跨 MHS/Host 操作分边界提交。
6. 保存重开、包往返、历史 Action/Trace 映射与兼容性诊断；证明未无损时不得静默丢弃。

旧 MTI.ExtractReusable 并不是全内容提取保证，所有现有不支持的输入须准确拒绝或列清候选扩展。

## 06. 代码开发者自然使用入口

无需“为一个 Rectangle 先注册 15 个服务”。简单创建与引用当前已有：

~~~csharp
MEMContent content = session.AddContent(session.Project, "Inspection");
MRIResourceReference reference = session.PlaceContent(page.RootContent, content, "Station1");
~~~

正式逻辑：沿用 MOLLogicCodec.Set/Get(session,content,definition) 或 MOCFlowBuilder.Edit(session,content,builderAction)；InvokeContent(MEMContent) 会读取当前工程快照的公开 Port 合同（适用的 Schema 范围须测试）。不要给 SDK 提供绕过 MEM/MOL 事务直接改 Dictionary 的捷径。

**目标便捷 API（概念，不代表已实现）**：CreateContentFromCapability、PreviewExtractContent、DescribeContentPublicContract、ValidateContent、CreateContentTemplate。实现前先审 DOCS/04、现有 MTC/MTI/MOC，优先在原类型补便捷方法/薄 Facade，不另创万能 ContentManager、第二组 Descriptor。

## 07. Studio 的三种显示/权限角色

Author：编辑定义、预览/构建，不能直接获取生产执行权限；Integrator：配置工程实例与已批准目标，依责任获得发布请求权；Operator/Observer：只读运行和受控命令，不能回写共享定义。UI 的实时状态需明确 Project/Host/Run/Source/时间与质量，不将本机演示数据伪作生产实例。

## 08. 错误、反馈与可访问性

每次失败至少显示原因码、来源 ID、修复建议、是否已提交以及回滚状态。跨层错误应能跳转 Content→Public Port→Capability/Provider→Flow Block→Run/Trace。包括键盘操作、聚焦顺序、DPI/多显示器、拖拽补偿、长列表虚拟化、异步取消和错误时原值可恢复。复杂专业配置默认折叠，高级功能可发现，不强制每次新建填写十张表。

## 09. 性能与一致性

浏览目录/展示 Inspector 不写 Revision；能力目录缓存为可失效的只读投影，Project 改变须解除旧订阅。大量 Content 嵌套需要延迟展开、可取消校验和依赖图增量刷新；不允许 UI 阻塞主线程读取 GB Volume 或实时 PLC 数据。任务取消/焦点变化只退出 UI 请求，不悄悄停止 Host 工业运行。

## 10. Studio 交付验收（CNT-T55～T60）

空白、类型、模板、能力、提取、导入六种入口是否共用一份 MEM 真源；对象变更统一 Undo/Redo；四方面视图选中身份同步；坏端口/缺 Provider 预览可解释；保存重开/包往返/版本升级无损；UIA 键鼠/DPI/多屏和跨进程 Run Trace。最后确认只读浏览不改 Revision，关闭中心不销毁其它中心或生产资源。
