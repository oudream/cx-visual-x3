# CxVisual Decision——#4 上下文编排与智能引导 V2.0

> **本文件是Studio/SDK使用行为的唯一专题**；Decision不拥有画布、Core编辑事务或实际Runtime。现有 `MOLFlowCanvas` 已有 Block/Port选择、连线事件，`MOCBuildService` 有Build/诊断，`MOCCapabilityCatalog` 有发现，`MOLRuntimeOverlay` 有运行态投影；统一Context-Aware筛选/推荐的V2能力**尚未完整实现**。总纲[#0](CxVisual_统一决策与规划体系%230总体架构与全类需求_V2.0.md)；约束语义[#2](CxVisual_统一决策与规划体系%232约束目标与策略_V2.0.md)。

## 01. 真实用户目标：工具箱不只是“列出全部能力”

专业工程用户希望按当前选择（Page/Content/Symbol/Flow/端口/ROI/图像/体数据/设备/变量）得到**可执行、可解释、不过度打扰**的建议：什么可以放、什么不匹配、如何补条件、怎么连、下一步可能走哪里、为什么Build失败、哪些策略更合适。

**Symbol、Content、Capability必须区分**：
- MEMSymbol：复用的视觉组件定义，建议依类型、属性、变量/权限等适用性过滤；
- MEMContent：逻辑/工程内容及引用，适合子流程组合，不可当任意视觉Leaf；
- Capability：执行语义（五类模块可发布），用MPC端口和专业合同匹配；不等于设备实例；
- Module/Provider：可发现的能力集合及真正执行者，Declared/Described/Resolved/Authorized不同；
- MRC Element/Group：视觉呈现对象，不因为可选中就复制成独立工程资源。

## 02. 一张用户操作图

~~~mermaid
flowchart TB
   A["选择对象/拖出端口/编辑参数/新增目标"] --> B["解析上下文：ProjectRev/Selection/Port/Target/Policy"]
   B --> C["读取MOC+MFM/MEM/FCM/专业Snapshot"]
   C --> D["D01/D02：轻量语义/硬约束三态"]
   D --> E{"候选分类"}
   E --> F["Applicable：可用"]
   E --> G["Unknown：缺设备/模型/证据"]
   E --> H["Rejected：说明原因/合法转换"]
   F --> I["D03/D04/D07：按目标/策略排序"]
   G --> J["工具箱/属性面板/提示区"]
   H --> J
   I --> J
   J --> K{"用户选择"}
   K -->|"接受建议/编辑"| L["Core统一事务与Undo/Redo"]
   K -->|"只查看"| A
   L --> M["Build全局检查/可选静态路径推演"]
~~~

Studio是消费者：UI不能写另一套专业端口/资源判断；Decision没有`InsertSymbol`、`SaveProject`、`ClickButton`等副作用。只有用户确认且权限允许，才由现有Core编辑Owner提交。

## 03. 七类交互触发与轻量输出

| 触发 | 输入/处理深度 | 输出/操作体验 | 延迟/安全 |
|---|---|---|---|
| 搜索能力/Symbol | 选中上下文、Category、Capability/Port合同、Scope | 最适用优先，显示不适用/未知原因与适配入口 | 纯读、分页/缓存 |
| 开始端口连线 | SourcePort→候选TargetPort、Direction、TypeId、单位、版本 | 合法高亮，不兼容提示，可建议转换但不自动加节点 | 鼠标拖动只跑轻检查 |
| 拖入节点/Content | 当前Flow作用域、声明、依赖、冲突规则 | 允许放置、缺参提示、阻止结构非法 | Core commit才改变Revision |
| 调整参数 | FCM Schema、有效配置/草稿、专业Validator、跨字段依赖 | 范围/单位即时提示，依赖项引导/Unknown | Save不等于Apply生效 |
| 组织分支/Loop/Foreach/子流程 | MOL结构/端口/子Content/作用域 | 可达性、出口/Join、循环边界、数据寿命问题 | 静态预览不实际执行 |
| Build/发布准备 | 工程快照、Catalog/Policy/目标、资源 | 全局诊断、静态Plan、SourceMap、备选/发布缺项 | Host/Runtime还需现场门禁 |
| 运行失败/优化 | RunId/Trace/专业错误/SourceMap、目标与环境 | 定位到原图节点；原因证据、可能修复、影响范围 | 不能从失败自动热改生产Plan |

**一次编辑不必完成D01～D10**；动态规则按所需深度触发，耗时优化在用户明确点“比较/优化”后异步执行。

## 04. 工具箱候选及说明的规范状态

每项显示：`Name/Category/Kind/CapabilityVersion/Preview/ReasonCodes/RequiredActions/EvidenceAge`。

| 判定 | UI显现 | 能否直接提交 |
|---|---|---|
| Applicable | 清楚显示适用、当前满足的已知条件 | 仍按Core结构/权限进一步验证 |
| ApplicableWithAdaptation | 需显式插入有证据的转换/参数调整 | 用户确认后编辑，不能默认有损 |
| Unknown | 待验证，显示缺失证据、前置/环境检查 | 不能当作“已证明可执行”，需明确处理 |
| Rejected | 不兼容原因、违法约束、源定位、可替代项 | 阻断不合法连线/发布 |
| Deprecated/Unavailable | 版本下线、Provider未加载或未授权 | 显示发现状态与不适用原因，不用空按钮 |

**不要只呈现“推荐”结果**：允许用户勾选“显示全部含不适用项”，学习为什么不可用，浏览文档和合法替代。默认UI渐进呈现：简单工具箱/端口提示，高级面板再显示资源策略和Pareto证据。

## 05. 例：从 Gray16 X-Ray 输出选择下一步

当前：`Acquire` 的图像为Gray16 4096×4096；编排者拖出 Image 输出端口。

1. 检索包含Image输入的Capability，结合Context/Model/Safety/FCM查元数据。
2. Gray16预处理满足格式时可列为Applicable；Gray8阈值检测若输入只支持Gray8，则直接连线Rejected，另展示“显式位深转换（可能损失精度）”的前置步骤。
3. AI GPU检测缺模型/显存或未授权，标Unknown/需运行复核，而非“自动可用”；PLC分选命令输入需要Inspection结果，不能被Gray16直接连。
4. 若有多个可行检测能力，按业务精度/节拍/资源和有效样本比较，显示推荐及备选；没有证据只标候选不做最优声称。
5. 点击候选后在Core中插入节点/引用，补上必要输入，自动刷新诊断；保存/生效/发布仍经过原Owner。用户取消则不修改工程。

## 06. 局部验证、全局构建、场景推演三种不同保证

- **局部**：对当前Block/Link/Property及直接依赖运行D01/D02/D10；在拖动端口中只计算可缓存的浅检查，避免每像素Build。
- **全局**：Build分析整条DAG、能力/类型/版本、数据作用域、子流程/动态Region、组资源及权限前置，输出带BlockId/LinkId的可定位Diagnostics/SourceMap。
- **静态情景**：根据已知条件求可能分支、永不可达路径、条件出口、异常分支和可能资源冲突；对含未知外部输入/副作用的分支标Unknown，不冒称会实际走该路径。
- **仿真执行**：需明确Virtual Provider/PSM模型和安全模式，由MRK运行；Static Preview ≠ Virtual Run ≠ Replay ≠ 真实产线。此责任见[#5](CxVisual_统一决策与规划体系%235运行调试模拟与反馈_V2.0.md)。

## 07. Studio / SDK / API 行为合同

~~~csharp
// Proposed: 不等于当前已有MDC API
var choices = decision.FindCandidates(MDCCandidateRequest.ForPort(
    sourcePort, capabilitySnapshot, currentGoals, editorContext), token);
ShowCandidateGroups(choices.Feasible, choices.Unknown, choices.Rejected);
// 用户选择后，调用现有Core工程事务添加MOL能力节点、连接端口；不由Decision写MEM。
~~~

**关键交互状态**：RequestId、ProjectRevision、SelectionVersion、CapabilitySnapshotRevision；异步完成必须比较这些键，仅在吻合当前画布时展示为有效/允许应用。WPF不在UI线程做重建；非模态诊断附详细码、来源和下一步建议。Keyboard/拖拽/Undo/Redo遵循Studio现有统一输入规范。

## 08. 协作图：同一套端口/版本/工程真源

~~~mermaid
sequenceDiagram
 actor User as 编排者
 participant UI as Studio/MOLFlowCanvas
 participant Core as MEM/MOL + MFM/MPC/FCM
 participant D as Decision
 participant MRK as Runtime
 User->>UI: 选择Gray16输出/拖动连线
 UI->>Core: 读取Revision/Port/参数Schema快照
 UI->>D: FindCandidates / AssessConnection
 D-->>UI: Pass/Fail/Unknown+RuleId+SourceRef
 UI-->>User: 候选/原因/转换建议
 User->>UI: 确认添加/连线
 UI->>Core: 正式Edit/Commit/Undo
 Core-->>UI: NewRevision
 User->>UI: Build / 试运行
 UI->>D: Build/Compile（纯结构）
 D-->>UI: 诊断/SourceMap/描述Plan
 UI->>MRK: 经授权的Runtime装载/运行
 MRK-->>UI: RunId/StepSnapshot/Trace
 UI-->>User: 同一画布高亮真实运行节点
~~~

## 09. 体验与性能质量

1. **交互层级**：常用动作工具箱→连线→输入关键参数→运行；高级设置折叠，开放高级规则/资源/证据入口，不让所有用户先学习约束术语。
2. **增量检查**：Port/Block/Rule依赖图只失效受到影响的部分；全局Build不修改Core快照；改变Revision/配置Effective/CapabilityVersion后缓存必须失效。
3. **建议可解释**：推荐必须能回答“为什么排第1/与备选差在哪/哪些条件未知/能否只执行检查”，而不是黑箱综合分。
4. **错误提前暴露**：输入中立即指出非法范围、缺端口、版本不兼容与危险策略；对无法静态判断的情况给出可操作的验证入口。
5. **复杂工程可扩展**：支持Branch/Join/状态机/动态Loop/Foreach/Invoke Content、多并行资源约束、Data.Return及三层命名数据；不为了画布简化而把动态Loop静态展开。
6. **稳定性**：异步取消/结果版本校验/错误隔离，关闭面板不影响正在运行的MRK；UI显示性能的基准由固定测试机与工程规模测出后批准。
7. **工业安全**：危险动作的连线合法仅说明“逻辑可表达”，不代表设备Ready或获批；不得为预览发送真实 PLC/运动/X-Ray 指令。

## 10. 可真正验收的场景

| 测试ID | 用户操作 | 断言 |
|---|---|---|
| AUT-01 | 当前选中Gray16输出、搜索算法 | Gray16候选正常列出、Gray8原因可见；原有候选不被无声隐藏 |
| AUT-02 | 拖动不兼容端口连线 | 非法连接没有进入Core事务、画布有可定位说明 |
| AUT-03 | 配置FCM范围外值 | 即时提示与Build校验一致，不污染Effective |
| AUT-04 | 推荐有损转换 | 不经显式确认绝不增加转换节点，解释风险 |
| AUT-05 | 用户快速切换工程/选项 | 过期异步建议被丢弃，无跨工程误操作 |
| AUT-06 | 子Content + Loop/Foreach + 数据作用域 | 结构校验保真，动态循环不按MaxIterations膨胀 |
| AUT-07 | 并行共享独占设备 | 给出冲突/串行建议，不能越权自动执行 |
| AUT-08 | Build失败点击诊断 | 跳转正确Content/Block/Link，保留真实SourceMap |
| AUT-09 | Static/Virtual/Replay三种预览 | 标识证据层次，静态路径不冒充真实动作 |
| AUT-10 | Studio/SDK一致性 | 同Revision同规则的硬约束判定与ReasonCode一致 |

**开发者价值指标**：完成典型5节点工程的操作数/耗时、误连拦截率、首次Build通过率、定位首个错误所需点击数和解释覆盖率；有固定使用者与夹具前仅作为验收目标，不声明已实现改善百分比。