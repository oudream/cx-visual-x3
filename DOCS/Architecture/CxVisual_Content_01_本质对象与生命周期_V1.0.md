# CxVisual Content 体系 #1——本质对象、来源、身份与生命周期 V1.0

> 状态：目标架构规格，继承 [#0](CxVisual_Content_00_总体架构与完整需求_V1.0.md)；关联 CNT-R01～R12、R49～R54。真实定义以 Mechanism/Core/Engineering/MEMNode.cs、MEMEngineeringSession.cs、MTIPackages.cs、MRIReferenceList 及工程组织 V1.2 为准。

## 01. 从业务职责而不是从图形出发

Content 的形成条件：一个可命名的责任、内部组合及外部使用边界。内容可以只负责显示/数据规则/专业算法/任务编排/工位业务，不要求同时具备全部方面。是否提升为可复用共享资源，由稳定契约、复用价值与生命周期决定，而不是因为选中了两个矩形。

**判别算法**：若只需局部变换/选择/图形层级，用 Group；若需可视组件/交互定义复用，用 Symbol；若需跨能力/子流程/数据/资源/公开接口的整体职责，用 Content；若仅是函数调用，用 Capability；若仅是一个专业页面，用 Page + RootContent；不要为此新增“万能 Widget/Content”子类。

## 02. 既有对象与合法关系

~~~mermaid
classDiagram
 MEMNode <|-- MEMProject
 MEMNode <|-- MEMPage
 MEMNode <|-- MEMContent
 MEMNode <|-- MEMSymbol
 MEMNode <|-- MRCVisualItem
 MRCVisualItem <|-- MRCGroup
 MRCVisualItem <|-- MRCElement
 MEMProject *-- MEMPage
 MEMProject *-- MEMContent : shared
 MEMProject *-- MEMSymbol : shared
 MEMPage *-- MEMContent : one RootContent
 MEMContent *-- MEMContent : embedded
 MEMContent *-- MRCVisualItem : visual
 MEMContent o-- MRIResourceReference : use-position
 MRIResourceReference --> MEMContent : referenced
 MRIResourceReference --> MEMSymbol : referenced
 MEMSymbol *-- MRCGroup : unique RootGroup
 MRCGroup *-- MRCVisualItem
~~~

**拥有**具有唯一 Parent 和资源作用域；**引用**具有 ReferenceId、TargetId、Kind、Owner、实例路径/覆盖，不拷贝目标 Children；**运行调用**由 MOL Action/ContentInvoke 等逻辑和 MOC/MRK 解析，不能依据引用出现就自动执行。共享定义可以存在于 Project 独立资源下，Page 的 RootContent 必须存在且受保护。Group 不直接拥有 Content/Symbol 引用。

## 03. 五种来源，一种合法结果

| 来源 | 输入与转换 | 最小操作 |
|---|---|---|
| S1 工程场景 | 新 Page 自动拥有 RootContent；非复用也成立 | 创建 Page→RootContent |
| S2 空白/类型化 | 无模板或可信 MPMTypeDescriptor | AddContent/AddTypedContent→公开成员 |
| S3 原地提取 | Content 的子树、MOL 逻辑、绑定、跨界端口 | Preview Extract→IdMap→共享定义+Ref，事务 |
| S4 组合/模板 | 多 Capability/子 Content/Symbol/专业模板 | 冻结所需依赖、接口与配置；可预览 |
| S5 导入/复制 | MTI Package/库资源/历史工程 | 验证 Schema/哈希/依赖/信任→Import/Copy |

不得把“类型可发现”当作“工程定义已创建”，或把“导入包”当作“Provider 已安装”。MTI.ExtractReusable 目前针对 Content 的直接子节点，**不应假定**已能无损提取逻辑/全部复杂 Aspect、任意 Group→Content；需要相应负例和 Remapper 证据。

## 04. 四种身份与地址

| 层次 | 标识/例 | 生命周期 |
|---|---|---|
| Type | TypeId + SchemaVersion | 可信 Provider/MPM 类型描述 |
| Definition | ProjectId + ContentId + Definition/Resource Version | 工程资源定义/持久版本 |
| Use/Reference | ReferenceId + InstancePath + Overrides | 父 Content 某次使用 |
| Execution | PlanId/Revision + FlowId + InvocationId + Iteration/Frame | 宿主一次或多轮运行 |

“Instance”不要含混：结构引用实例不是 MRK 一次运行 Invocation；ScopeId、PhysicalAssetId、HostId 和 Product SN 不可由 ContentId 冒充。一个定义被两个工位引用，在编辑、绑定、执行、Trace 上都必须可区分。

## 05. 三条生命周期

### 5.1 定义（工程/版本）生命周期
Draft/Preview → MEM Commit → Saved Definition → Validated（按能力/可用环境）→ Release Candidate → Published/Versioned → Deprecated/Archived。状态是**视图/证据**，不是强制新增 MEMContentStatus 枚举；草稿可以保存但不可被误认定可执行。

- Create：确定责任与 Scope，避免在空创建时硬设默认 Start 动作。
- Compose：只编辑一份 MEM+MPM/MOL 正式定义。
- Expose：界定公共属性/Port/事件/功能入口，外部不能读内部 Frame。
- Validate：结构/类型/版本/资源/跨专业安全未知要报告，不随意补假默认。
- Commit/Save：原 Revision 和撤销边界；保存成功不表示运行生效。
- Export/Publish： MTI 包与 Host 发布不同，前者不应产生设备副作用。
- Upgrade/Retire：有受影响引用、端口兼容报告及迁移/回滚方案；在途运行锁旧版本。

### 5.2 引用/装配生命周期
Browse → Reference/Import → ResolveTargetVersion → ParameterOverride/Binding → ValidateCompatibility → Used/Disabled → Upgrade/Remove。共享定义不会因为一个引用删除而销毁；删除定义要先分析引用与发布依赖，绝不隐式级联破坏。跨工程引用需要锁版本/包哈希，不承诺当前已实现完整远程库。

### 5.3 运行/展示生命周期（可选）
Resolve Definition Snapshot → Bind Effective Config/Providers → Prepare Plan/Visual Projection → Invoke/Subscribe → Observe → Complete/Unsubscribe → Release。只做可视投影的 Content 可无 MRK Flow；服务/PLC/GPU 长驻资源由真正 Owner 管，不得把 Content 的结束直接等同共享设备断开。

## 06. 编辑状态与运行状态为什么必须分开

工程草稿版本 r12 正在编辑，工位 A 和 B 仍可能在固定 r10 计划中执行。Studio Save r12 不允许热改 A/B。Run 结果返回需要以 Plan/Definition/ReferencePath/RunId 关联，而非直接覆盖 MEMContent.Properties。用户执行 Undo 只撤销工程变更，**绝不**撤销已发生的物理动作。

## 07. 作用域与可见性

工程级：共享定义/资源与依赖；Page 级：RootContent/场景；Content 级：私有成员、子调用参数、逻辑声明；对象级：Port/Property；实例级：覆盖、布局、设备引用；运行级：Frame/Heap/DataScope/Lease。数据只通过显式映射/公开 Port 跨 Content；不得沿 Parent 链隐式共享全部数据。

## 08. 抽取、复制、升级事务

Preview 必须返回：目标资源、IdMap、跨界 Property/Port/Connection、MOL Block/SourceMap 影响、资源闭包、外部绑定、无法映射条目与具体原因。Commit 使用现有 MEM/MTC 原子事务（若涉及 MHS/Host，则分界提交，不能宣称全局原子）；失败工程不变化。复制内核 ID 需重新映射、外部依赖保留/导入/拒绝显式选择；版本迁移先 dry-run，再确认。无损无法证明时拒绝或保留原定义。

## 09. 具体用户旅程

- 创建 Page：系统自动 RootContent，用户拖入两个 Symbol 并连接变量；Content 是视觉/数据组合而非默认运行子流程。
- 从检测流程提取“采图并预处理”：选定逻辑和数据契约，分析所有输入/输出/资源绑定，确认后生成共享 Content；其他工位 PlaceContent，再在 MOL 中明确 Invoke。
- 复杂装配：一个“检测工位”引用 Acquire/Detect/Judge 三个 Content；复用同一 ProcessDefinition 而参数、ReferencePath、Frame 分离。
- 退役一个共享 Content：先查询本项目/包/发布计划的使用位置，迁移或保留已发布版本，再阻止新的非法引用。

## 10. 验收与反证（关联 CNT-T01～T12、T43～T48）

核实空页面合法、RootContent 受保护、空白/类型化创建、资源/实例身份隔离、保存重开、引用版本、Extract Preview/Commit/Undo、跨界 Aspect/端口 Remap、依赖删除拒绝、双实例不同配置、多 Run 不串数据、历史计划不被 Studio 热改。每个负例需要检查工程 Revision 不变及资源/句柄正确清算。
