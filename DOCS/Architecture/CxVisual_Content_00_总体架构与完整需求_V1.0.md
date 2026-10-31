# CxVisual Content 体系 #0——总体架构与完整需求 V1.0

> 日期：2026-10-31。状态：完整目标规格/架构审评稿（Proposed），不是已编码或已验收证明。
> 使命：使 Content 成为 CxVisual 各方面能力与各专业目标之间的统一**组织、组合、复用、公开契约及编排边界**，但不取代 Capability、Symbol、Decision、Runtime 或专业实现。
> 依据：AGENTS.md、DOCS/00、01～04、工程组织体系 V1.2、平台公共能力总目录 V1.0、Symbol #0～#7 V2.0、统一决策 #0～#6 V2.0、统一运行体系、Studio 双中心实施记录。DOCS/02～04 Excel 必须在新增公共类型/API 前逐 Sheet 审查；本文不宣称该审查已完成。
> 核心原则：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 00. 一页概览：Content 是什么，为什么需要

Content 是具有独立职责、可组合内部实现、明确外部契约、可持久化/引用/版本化的**工程内容单元**。它既可以是状态展示、图像分析、控制工位、检测流程、三维分析空间，也可以是没有任何图形的后台计算与逻辑。对象不是因为“有图形”或“能运行”才成为 Content，而是因为存在有价值的职责边界与复用契约。

- Group：局部视觉组织/空间变换；不是业务 Scope。
- Symbol：共享视觉、交互、状态及可选行为组件；不是专业执行引擎。
- Capability：专业或机制提供的可发现功能/数据行为合同，不等于 CLR 类或 Module。
- Content：组织 Symbol、嵌套 Content、资源、Capability 引用与 Visual/Logic/Data/Collaboration 定义，向外隐藏实现并公开所需合同。
- Page/Workbench：消费 Content 提供场景与专业工作视角，不强制所有 Content 必须属于 Page。
- MOL：Content 的 Logic Aspect 中唯一持久化逻辑语义；Decision/MOC 解析、验证、编译；MRK 执行；MDS 持有运行数据。
- Project：当前工程唯一 MEM 根；Host/Run/Device 不是另一个 MEM Project。

**两个反例**：纯数学函数可直接调用，无须强行包装 Content；纯静态展示 Content 可以没有流程，不能自动给它造 Start/Stop 引擎。

## 01. 架构全景与唯一事实

~~~mermaid
flowchart TB
  OR[用户目标/专业需求/已有工程] --> A[Studio/SDK: 新建/提取/导入/模板]
  CAP[可信类型与Capability/Module目录] --> A
  SYM[Symbol定义/Group图元/专业Viewer] --> A
  A --> MEM[MEMContent: Definition / Ref / Four Aspects / Public Contract]
  MEM --> PAGE[Page与六专业Workbench: 视图和交互]
  MEM --> DEC[Decision/MOC: 只读发现/静态匹配/构建+SourceMap]
  DEC --> HOST[Host: 准入/配置生效/发布/版本固定]
  HOST --> MRK[Runtime/MRK: Invocation/Flow/调度/Trace]
  MRK --> PRO[Professional Provider: 实际算法/设备/业务]
  MRK --> MDS[MDS: Frame/Heap/Stack/结果/质量]
  MDS --> PAGE
  MRK --> PAGE
~~~

图示为业务关系，**不是 DLL 依赖图**。Core 不依赖 Studio、Decision 或 Runtime；任何 MOC→MRK 的过渡工程依赖需遵循既有解耦裁决，不以本文新建第三种计划模型。

### 1.1 五条架构主线
1. **内容线**：MEMProject → Page/共享 Content → 内嵌 Content / ResourceReference → Symbol/VisualItem。
2. **契约线**：MPM Type/Property/Aspect + MPC Port/Connection + PublicMapping + MTU/FCM 专业约束。
3. **功能线**：MFM Module / MOC Capability Descriptor / 专业 Provider，经显式关联被 Content 组合；“被发现”与“实际可用”分开。
4. **流程线**：MEMContent.Logic Aspect（MOL schema1 或 schema2）→ MOC 编译/SourceMap → MRK 执行；无逻辑则无需创建 Flow。
5. **运行线**：Host/Scope/Run/Invocation → MDS 数据 + 专业实际副作用 → Trace/状态 → Studio/Workbench 只读投影。

## 02. 不可突破的不变量（CNT-I01～I24）

| ID | 不变量 |
|---|---|
| I01 | 一个 Project 只有一套 MEM 工程定义真源；Content 不是第二个工程/流程/注册仓。 |
| I02 | MEMContent/MEMPage/MEMSymbol/MRCGroup/Element 的已批准对象关系不重写。 |
| I03 | 定义、类型、引用实例、Runtime Invocation、Flow/Run、物理资产分别独立身份。 |
| I04 | Group 不得拥有业务 ContentRef；Symbol 的 RootGroup 恰好一个。 |
| I05 | Content 可无 Visual、无 Logic、无 Runtime；也可同时拥有四方面。 |
| I06 | 父子包含、共享引用、数据绑定、视觉连线、MOL 依赖、运行调用不是同一种关系。 |
| I07 | 对外只穿越公开 Property/Port/Command/Event/Operation 合同；私有成员不可按字符串访问。 |
| I08 | 公开功能由显式 Capability 或调用合同支持，不能把一组图元默认注册成可执行能力。 |
| I09 | MPM/MPC、MFM/MOC、FCM/MHS 各自单一权威 Schema；不建 ContentRegistry/ContentFlowEngine。 |
| I10 | Content Logic Aspect 同一时刻只保存一种版本化 MOL 表达；V1/V2 不并行保存两个真源。 |
| I11 | 可视排列、ZIndex/Group 顺序不决定执行次序。 |
| I12 | 同一定义的两处实例不同 ReferenceId、InstancePath、配置覆盖及运行数据，禁止串写。 |
| I13 | 定义数据不保存活跃线程、PLC 句柄、GPU Buffer、Flow Frame、设备当前状态。 |
| I14 | 没有发布前验证或真实授权，不得依据图形可见/端口连通推断设备 Ready。 |
| I15 | 已声明/已安装/已解析/已验证/已生效/已授权/已完成为不同证据状态。 |
| I16 | Discovery/Preview 不修改 Project Revision；工程写入通过 MEM/MTC 统一事务。 |
| I17 | MOC/Decision 静态判断不修改定义；运行/现场结果不反写共享 Definition。 |
| I18 | 创建、提取、导入必须保留或显式映射 Identity、Port、Binding、Aspect、引用和外部依赖。 |
| I19 | 保存定义、构建计划、批准发布、运行生效分别返回真实结果，不合并成“成功”。 |
| I20 | 定义升级不静默替换在途 Plan/设备生效配置；必须依赖影响、兼容和回退。 |
| I21 | 同一运行定义需可回溯 Definition/ReferencePath/Block/Port/Plan/Flow/Invocation/Run。 |
| I22 | 大型 Image/Volume/Tensor 使用有寿命的引用及预算，不复制进 XML、绑定对象和 Trace。 |
| I23 | 六专业提供自身算法/设备安全/Viewer/仿真语义，公共 Content 不吞并其 Owner。 |
| I24 | 需求、真实 API、代码实现、Windows CI、UIA、HIL 的证据等级必须分开。 |

## 03. 完整需求索引（CNT-R01～R72）

本表为跨专题唯一需求身份。每一编号都要在 #1～#9 标定 Owner、场景、已有/缺口与测试，不能因为实现某个 Button 就关闭整组。

| 范围 | 六项不可缺少的能力 |
|---|---|
| R01～R06 来源与定义 | 空白新建；Page.RootContent；内嵌创建；专业模板；从选区/流程提取；可信包导入 |
| R07～R12 身份与生命周期 | Type/Definition/Ref 分离；结构合法；定义版本；实例覆盖；复制/迁移；退役回退 |
| R13～R18 能力组合 | 能力只读发现；多 Provider 选择；能力到 Content 映射；内部能力装配；组合能力发布；缺失/Unknown 解释 |
| R19～R24 对外契约 | 属性/参数；强类型端口；事件命令；公共映射/可见性；单位时基坐标；版本兼容 |
| R25～R30 四方面/结构 | Visual；Logic；Data；Collaboration；无视觉/无逻辑；关系正交与嵌套 |
| R31～R36 逻辑构建 | MOL V1/V2 互斥；控制/数据流；条件/循环/状态/异常；子 Content 调用；跨Schema策略；构建与SourceMap |
| R37～R42 执行语义 | 输入参数/Output Commit；Invocation隔离；触发/等待；重试/取消/超时；并行/资源；运行/事件因果 |
| R43～R48 数据配置 | MDS/变量/质量；Heap/Frame/Handle；FCM Schema；MHS Saved/Effective；配置覆盖；跨作用域/进程 |
| R49～R54 资源/治理 | 资源依赖闭包；权限/安全；来源/信任；包版本；升级影响/迁移；回滚/审计 |
| R55～R60 Studio/SDK | 内容中心；七类来源创作；同源四方面编辑；便捷API；预览校验/事务；同图调试 |
| R61～R66 专业融合 | Automation；HMI/SCADA；Vision；CT3D；Inspection；Simulation |
| R67～R72 工业交付 | 跨专业综合；软件级负例；Windows/.NET48/UIA；规模/性能；跨PC/失败恢复；设备HIL与证据 |

编号在各专题按顺序解释，例如 R31=V1/V2 互斥，R36=构建+SourceMap。新增需求先在此登记再进入专题/测试，避免不同对话随意另起编号。

## 04. 六种角色与渐进使用等级

| 角色 | 最小自然路径 | 高级诉求 |
|---|---|---|
| 基础开发者 | 直接方法/Provider，不被迫建 Content | 把能力显式注册/封装为可复用 Content |
| 专业开发者 | 声明类型、端口、配置与受控 Provider | 将多专业资源组合并保持专业 Owner |
| 工程编排者 | 选 Content/Capability → 组合→绑定→验证 | 多级子流程、复杂数据/事件与实时诊断 |
| HMI/页面作者 | 拖入共享 Content/Symbol、实例配置 | 状态/行为/事件/质量与授权请求 |
| 调试/维护者 | 从 Run/Trace 反查 Content/Port/实例 | 版本、故障、诊断、恢复、迁移 |
| 生产操作员 | 使用已发布的页面与功能 | 可解释失败、权限、确认、审计与安全 |

L0 直接 SDK/静态 Visual；L1 声明与离线发现；L2 编辑/保存/仿真软件验证；L3 发布后授权运行与真实专业证据。等级描述能力成熟度，**不是四种 Content 子类**。

## 05. 专题职责与阅读顺序

| 文档 | 负责 | 不重复定义 |
|---|---|---|
| #1 本质对象与生命周期 | Definition/引用/实例、来源与演进 | 工程组织已批准的 MEM 树 |
| #2 能力体系与公开契约 | 组合能力、Port/Property/Action/事件、发现 | 第二份 Capability Catalog |
| #3 组织结构与四方面组合 | 四方面、合法结构、关系与适配 | 另一套 Graph/Node |
| #4 逻辑编排与流程协同 | MOL→MOC→MRK 子调用/异常/SourceMap | 第二执行器 |
| #5 数据配置资源与协同 | MDS、MHS/FCM、资源/消息/命令桥 | 新配置仓/消息中心 |
| #6 Studio 创作与开发者API | 用户旅程、面板、Preview/Commit、SDK | 新 WPF 工程/Undo |
| #7 专业应用与综合案例 | 六专业与八黄金案例的输入输出、失败反证 | 专业算法实现规格 |
| #8 构建运行发布与诊断 | Plan/Release/Trace、治理/恢复 | 改写 MRK/Host 内部调度 |
| #9 全类 UML 与 API 合同 | 已有类清单、候选增量、时序及Owner | 未核对的“已实现”API |
| Implementation/Testing/ADR | 任务事实、独立证据、负责人裁决 | 混淆设计与验收结果 |

## 06. 必须跑通的八个黄金案例（CNT-G01～G08）

| 案例 | 纵向闭环 | 主要反证 |
|---|---|---|
| G01 纯展示 | 组合 Symbol → 共享 Content → 双Page → 配置覆盖→保存重开 | 无逻辑不得强制编译 Flow；同定义覆写不串 |
| G02 HMI/Automation | Motor Symbol → Station Content → 事件 → 授权命令 → 真实状态回显 | 未授权/坏质量/超时不能显示动作完成 |
| G03 Vision | 图像/ROI → 预处理/检测子Content → 结果 Overlay | 错 Source/坐标/Run 的结果拒绝 |
| G04 CT3D | Projection→重建→Volume/Viewer→三视图/测量 | 坐标不符、体租约过期、GPU预算拒绝 |
| G05 Inspection | 检测/判定/复判/汇总三级Content | 失败不泄漏未提交输出，复判审计保真 |
| G06 Simulation | 同一组合 Content 绑定 Real/Virtual 可替代能力 | 虚实不互串，不自动切实机 |
| G07 提取与发布 | 现有场景/子树→提取Content→公开契约→共享包→升级 | 无损映射/撤销/依赖变更或明确拒绝 |
| G08 综合流程 | Automation+Vision+Inspection+CT3D+HMI，多级并发和Trace | 多实例/循环/跨专业状态、异常与版本隔离 |

每个案例最终必须给出：输入样本/装配路径/明确合同/操作步骤/真实结果/负例/固定SHA/证据等级；不得以示意图替代真实工程运行。

## 07. 完成标准与证据等级

E0=目标设计；E1=源码合同可核对；E2=Headless 真软件执行与正反测试；E3=同一 SHA Windows Release/x64/.NET48 + Studio/TestCenter/UIA；E4=真实设备 HIL/现场权限与物理验证。设计项状态与验证等级为两个独立维度。测试编号与分层在 Testing 矩阵统一记录，Implementation 台账禁止无证据标 Closed。

## 08. 文档治理与正式交付门禁

- 每一需求至少一个 Owner、可观察行为、失败反例和测试索引；冲突进入 ADR。
- 类型增改先核查 DOCS/02～04 XLSX 对应 Sheet、真实 csproj/源码、Symbol V2 全类文档。
- 新工程、新持久化树、新流程引擎、新总注册表均不是本轮默认方案。
- 命名以已有 MEM/MPM/MPC/MOL/MOC/MRK/MDS/MRI/MTC/MTI/FCM/MHS/MFM 为基础，不因文档概念新增公共类。
- 本系列与 Symbol V2.0 为兄弟规范；Symbol #5 的跨系统规则保持有效；若产生冲突需明确修改建议和 Owner 决议。
- 下一步编码前需按**最新 master**重新比对改动与 CI，不得以本日期推断后续仓库状态。
