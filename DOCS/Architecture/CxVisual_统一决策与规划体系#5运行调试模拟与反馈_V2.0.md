# CxVisual Decision——#5 运行、调试、模拟与反馈协同 V2.0

> **跨体系协作规格，不是第二个Runtime。** Decision只负责静态验证/候选/计划/影响与依据，MRK/F19执行、暂停/单步、资源与Trace，MDS真实数据，Professional.Simulation模型与虚拟行为，Studio呈现。详细执行机制以现有[统一流程执行体系 V2.1](CxVisual_统一运行体系%231流程执行体系_需求归属_双角色使用与分轮实施_V2.1.md)及[运行诊断体系](CxVisual_运行诊断体系%23监控与调试——完整设计与验收_V1.0.md)为权威；新接口在[#1](CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)统一定义。

## 01. 用户最终想做的五件事

| 行为 | 最低使用体验 | 谁负责 |
|---|---|---|
| 编排 | 在Studio拖入专业能力、连线/配参数；或SDK轻量Flow | Core/MOL/Studio + Decision辅助 |
| 运行 | 一键试运行/正式受控执行，数据自动流转、进度/取消、明确输出 | MRK/F19/MDS/Professional |
| 观察 | 同张图看到每个Step/子流程状态、输入/输出摘要、耗时、进度、错误 | MRK Trace/Snapshot、MPW投影 |
| 调试 | 断点/暂停/单步/恢复、失败节点定位、变量/图像/体数据检查 | MRK DebugController + 专业数据观察 |
| 定位与优化 | 通过RunId/Invocation/SourceMap定位真实问题，再按目标比较可行修复方案 | Runtime事实 + Decision解释/影响/优化 |

不要求开发者自建日志状态机、调试线程或MDS数据仓库。**同图编排/调试**是目标用户体验，但现有WPF具体覆盖范围须按测试核验。

## 02. 代码真实基线与差距

| 真实来源 | 已可确认的行为 | 不能因此声称 |
|---|---|---|
| `MRKDeveloperRuntime` | `MRKCodeRuntimeBuilder`、`Flow`、`Build`、`RunAsync/RunValueAsync` | 自动跨设备发布/完整专业可用 |
| `MRKExecutionControls` | 有界Trace `ReadSince`、Gap检查、`MRKExecutionDebugController` 的断点/Pause/Resume/StepOnce | 生产硬件步骤可任意停止或重发 |
| `MRKExecutionEngine` | Flow/Step状态、Invocation上下文、Result/Observations/Snapshot | 所有专业中间数据完整可视化 |
| `MOLRuntimeOverlay` | 从MOC Trace/SourceMap只读映射Studio节点，Gap用真实Step状态重建 | 第二套状态真源或无限历史回放 |
| `MRKExecutionJournal` | 运行记录、RecoveryDisposition（例如RequiresReconciliation） | 自动可安全恢复任意外部副作用 |
| `PSMSimulationCore` | 虚拟时钟、Step/Realtime/Accelerated/Replay Mode基础 | 完整物理设备孪生或现场认证 |
| Vision `RunCpuInspectionPreviewAsync` | 可独立单图真实CPU试运行、复用MRK/MDS | 全套Vision工作台或AI硬件已完成 |

原MOC/MRK静态Plan含有运行Delegate/Resolver耦合；目标由Decision输出不可变纯描述型Plan，Host/MRK执行真实Provider绑定和版本/权限/安全复核。此迁移只在后续代码阶段进行。

## 03. 构建到运行的最重要交接

~~~mermaid
sequenceDiagram
  actor U as 开发者/Studio
  participant C as Core/MOL
  participant D as Decision
  participant H as Host/Lifecycle
  participant R as MRK
  participant P as Professional
  participant M as MDS
  U->>C: 获取ProjectSnapshot/Revision
  U->>D: Assess/Plan/Compile（纯）
  D-->>U: Verdict/Diagnostics/SourceMap/PurePlan
  U->>H: 提交发布候选/权限
  H->>R: ValidateAndLoad(版本/现场许可/资源)
  R->>P: 真实Provider能力和状态核验
  alt 允许
    R->>M: 创建数据作用域与租约
    R->>P: 执行Step/设备或计算
    P-->>M: Pending/Commit/Abort专业输出
    R-->>U: FlowResult/StepState/Trace/Journal
  else 不允许/不确定
    R-->>U: 不放行+明确原因
  end
~~~

**两种Plan**：Decision的`MDCPlanArtifact`仅声明 PlanId/Revision/DAG/Region/CapabilityVersions/Policies/SourceMap，无Task/Delegate/设备句柄；Runtime绑定后才可得执行中的`MRKExecutionPlan`。真实设备不可用或版本漂移时拒绝，不能为“试运行体验好”直接放行。

## 04. 统一的调试和问题追踪身份

### 最小相关字段

`ProjectId + Revision + ContentId/BlockId/LinkId + CapabilityId/Version + SourceMap + PlanId/Fingerprint + RunId + InvocationPath + StepId + AttemptId + CorrelationId + EvidenceId`。

- Trace记录真实运行状态、耗时/错误码/Correlation，不默认保存像素、密码、原始生产敏感数据；图像/Volume和专业结果依MDS Artifact/租约受控读取。
- 多次调用同一子流程与循环必须用InvocationPath/Iteration/StepId区分；只靠显示名称/BlockId不能唯一定位运行实例。
- 在Studio点击失败结果，可跳到指定Run/Step/Content/Block，查看实际端口/配置版本/输入数据**摘要或受控快照**，旁列错误来源和可能原因。
- Runtime事实原因、专业诊断、Decision基于事实的推论分别标注；不能把AI推测当作“已证明根因”。
- Trace有界且会丢失旧记录，必须暴露`LostCount`，从MRK权威Step Snapshot重建**状态**，不重建不存在的历史事件。

## 05. 用户真正需要的一张运行观察图

~~~mermaid
flowchart LR
  A["图形/Code Flow：编排节点"] --> B["MRK实际执行"]
  B --> C["Step/Invocation/Result/Trace"]
  C --> D["SourceMap映射回同一节点"]
  D --> E["画布：Waiting/Running/Paused/Failed/Completed"]
  C --> F["受控数据：图像/Mask/Volume/Measurement"]
  F --> G["专业Viewer/属性/结果表"]
  E --> H["点击失败：错误码/输入版本/可重放性"]
  G --> H
  H --> I["Decision：解释/方案候选/影响范围"]
  I -.->|"用户确认/新Revision"| A
~~~

这张图只定义交互目标，不宣称现有Studio已经有完整数据断点及全类型专业观察器。Studio面板退出不能取消真实MRK运行；关闭Viewer也不能错误释放仍被Run持有的MDS Artifact。

## 06. 调试模式与安全边界

| 模式 | 允许做什么 | 明确禁止 |
|---|---|---|
| Pure/CPU开发 | 断点、单步、改参数形成新Run、固定输入重放 | 篡改已固定活动Plan/复用过期结果 |
| Virtual Simulation | 虚拟模型、虚拟时钟、故障注入、Step/Replay | 无模型时以Static评估冒充真实仿真 |
| Hardware-in-loop | 受控的硬件观察/许可下的测试步骤 | 未经专业负责人批准断点停危险机构 |
| Production | 观察/报警/受控取消、按Host审批切换版本 | 为了调试自动重发PLC/运动/NG命令 |
| Historical Replay | 基于捕获的完整、版本化事件及输入重播 | 有Trace缺口却宣称精确复演 |

不可逆外部动作超时可为Unknown而非Failed；重试前需幂等/去重/设备确认/人工对账，系统不得自动将Unknown作为安全成功。危险动作不得在断点等待时持有关键设备资源导致死锁。

## 07. 静态路径推演、真正虚拟执行和历史回放的区别

| 类型 | 输入 | 能证明 | 不能证明 |
|---|---|---|---|
| Static推演 | Core定义/已知规则/假设输入 | 可达/不可达/分支可能/依赖冲突 | 设备真实行为、实测时间 |
| Estimated资源/性能 | 描述模型/历史统计 | 给区间和瓶颈假设 | 当前GPU/设备实时容量 |
| Virtual | Professional.Simulation模型/场景/种子，通过MRK | 在模型假设下的控制流和故障处理 | 实体设备所有风险已覆盖 |
| Replay | 版本化输入、事件/结果和模型 | 已捕获轨迹的对照与回归 | 未捕获外部动作的确定结果 |
| HIL/Field | 被批准的真实环境、设备与安全证据 | 该硬件/工况范围内的实际验证 | 所有未来工况自动通过 |

仿真需要专业的虚拟模型，不能由Decision靠注解生成高保真物理响应。相同CapabilityId若换虚拟Provider，必须有显式契约兼容、运行模式隔离和生产禁用门禁。


## 08. 调试与优化实际回路

1. **发现**：某次Vision检测失败/CT GPU OOM/Automation超时，先从MRK事实获取RunId/State/Step/Time、专业错误码、配置Effective及Capability版本。
2. **定位**：SourceMap找到原Content/Block/Link、InvocationPath和当前相关输入端口，列出受控数据/产物引用；Trace丢失时明确报告Gap。
3. **解释**：Decision仅在提供完整事实快照后执行D02/D09/D10，区分“已证实规则违反”“高可信假设”“未知”，不能把可能原因当事实。
4. **选方案**：D03/D04/D06/D07/D08针对目标、硬资源/安全约束筛选调整参数/换Provider/降低并发/插入转换等可行候选。
5. **验证**：使用CPU重放、Virtual场景或HIL门禁测试可能方案；验证层次标注清楚，不能用虚拟跑通作为设备已安全。
6. **应用**：用户确认后通过Core编辑与Host发布流程生成**新Revision**，Runtime不在旧活动Plan热改。
7. **评价**：比较目标与实测，记录误差、成本/可靠性和失败变化，创建新的决策证据，但不隐式自学习改变安全政策。

## 09. 最小调试SDK与可视界面（目标行为）

- 简单Run：`RunAsync("Inspect", request)`/具名输出，失败不吞异常；直接专业调用应继续可行。
- 观察：按RunId获取`Snapshot/ReadTrace(cursor)`；Flow/Step/资源状态有清晰区别；可按SourceMap映射画布块。
- 调试：`AddBreakpoint/StepOnce/Resume/Cancel`复用MRK控制器；UI只发受控请求，权限/实际可停条件由Host决定。
- 复盘：`ExplainFailure(RunEvidence)`、`AnalyzeChange(OldRevision, NewRevision)`属于**拟议Decision服务**，不是Runtime真实API。
- 数据：输入输出按具名端口、Data.Return/Export和专业 Artifact 读取；不猜“最后一个节点的值”，不让UI绕过MDS Pending/Commit/Abort。
- 隔离：生产/虚拟/历史回放数据及命令作用域显式隔离；在相同输出合同下才允许替代Provider，不直接复用生产连接会话。

## 10. 验收矩阵（运行与诊断协作）

| ID | 测试场景 | 断言 |
|---|---|---|
| RUN-01 | 单次真实CPU Vision预览 | 共用MRK/MDS，输出可读，取消后清理，不需要完整Core工程 |
| RUN-02 | Flow动态Loop及多层子调用 | StepId/InvocationPath/数据作用域与原Goldens一致 |
| RUN-03 | 断点/暂停/单步/恢复 | 仅在合法步骤门禁暂停；SourceMap与实际节点一致 |
| RUN-04 | Trace容量溢出 | LostCount非零、重新获取真实快照，不伪造缺失历史 |
| RUN-05 | Device动作Unknown后重试 | 未经对账不得重发，不把Timeout自动变为Failed/Success |
| RUN-06 | 运行中修改工程 | 新Revision不改变活动Plan，过期建议失效 |
| RUN-07 | Static/Virtual/Replay交叉对照 | 证据层次正确，Virtual模式真实设备动作数严格为零 |
| RUN-08 | Fault injection与Scenario | 故障注入仅作用在受控模拟环境，重复Seed结果可复核 |
| RUN-09 | 失败节点定位 | RunId/Plan/SourceMap/BlockId/版本完整，UI/SDK结果一致 |
| RUN-10 | 优化建议被采纳 | 新Revision经审批，运行指标与原基线比较，误差/风险可解释 |
| RUN-11 | 并发取消和重启 | 资源/Frame/租约正确释放，等待停止不错误提前成功 |
| RUN-12 | 工业真机HIL | 需专业设备Owner安全许可及现场证据，普通CI不能代替 |

**性能与安全要求**：Trace/诊断存储有界；观察不能阻塞实时运行；模型/优化请求的预算及取消必须生效；调试面板和Viewer不得持有无限数据拷贝。性能指标在固定硬件、工程大小与代表负载上实际测出后批准；不伪造“提升多少百分比”。

## 11. 技术边界与实施路线

先使用现有MRK/SourceMap/FCM/MDS和专业Provider，补统一RunEvidence与Studio只读诊断定位；再做纯Plan/Loader拆分及新Decision解释入口；最后接Simulation Scenario/Replay/专业实测优化。不得为了“有Decision”重写执行引擎、Trace/Journal、专业模型或MDS数据所有权。

---

## 12. V2.0审评：真实问题追踪与推论分离的证据合同

**事实与解释必须分层**：MRK拥有RunId/StepId/InvocationPath/AttemptId和实际Trace/Journal/Snapshot；MDS拥有Frame/Heap/结果的生命周期；专业Provider拥有真实错误码、图像/Volume/设备状态及模型证据；Decision仅对明确传入的**版本化只读证据引用**作约束诊断、方案影响/推荐，不重建第二执行/调试/Trace体系。

| 层次 | 一定要有哪些字段 | 不可越权断言 |
|---|---|---|
| Core定义 | Project/Revision/ContentId/BlockId/PortId + SourceMap | 不能直接代表某次运行实例 |
| 描述Plan | PlanId/Fingerprint/CapabilitySchema/PolicyRevision/SourceMap | 不存Delegate、设备Handle及正在占用的Frame |
| Runtime事实 | RunId/InvocationPath/Iteration/StepId/AttemptId/Code/State/Time、TraceCursor/LostCount | TraceGap表示历史缺失，不能伪造丢失事件 |
| 专业输出 | Model/Algorithm/ConfigEffectiveRevision、受控ArtifactRef/输入输出摘要、设备确认 | 未授权显示敏感数据和真实现场访问不允许 |
| Decision分析 | RequestId/RuleId/ReasonCode/EvidenceId/Tier/Assumptions/Confidence/RequiredCheck | “可能原因”不是“已证明根因”，Virtual不是HIL |

**诊断用例**：一个Vision检测Block在三层Content嵌套调用的第2轮Iteration失败。必须先按MRK的`RunId+InvocationPath+StepId+AttemptId`取真实状态，再以SourceMap定位唯一工程Block，MDS提供经权限授权的输入/Mask摘要，专业错误指明像素格式不符。Decision比较“换16bit算法”和“有损转8bit”两个候选，解释精度/资源/策略与未知证据。**最终由用户决定更改、Core保存新Revision；生产MRK活动Plan不能被此建议直接改变。**

**模拟与调试**：调试控制复用MRK DebugController，严禁危险设备Step被普通断点无限暂停；静态路径推演仅证明可达性/条件可能，Virtual必须有PSM兼容Model/Clock/Seed，Replay需记录完整且标TraceGap，HIL/Field要专业Owner实机证据。无模型/无真实时间证据返回Unknown而不假定成功。

**AOP边界**：MRK观察/指标、Decision解释/性能、Studio展示可共用CorrelationId/诊断码和脱敏机制；不因审计/优化把决策计算插进每帧实时运行路径。异常处理必须保留真实根异常及设备确认状态。

**验收**：REV-06～08/10/12与RUN-01～12；至少对虚拟、纯CPU、实际设备三类设独立测试等级，不能由一个Headless绿色CI证明硬件安全。

## 13. D10工程变更影响集：从能力变更定位到Core节点（新增静态源码）

`MDCCatalogImpactAnalyzer.Compare`先得到能力的Added/Removed/Modified；`MDCProjectImpactAnalyzer.Analyze(MEMProjectSnapshot,changes)`进一步读取**原Core快照**中的V1 MOL逻辑树与V2 MOLWiring定义，返回`DirectBlocks(ContentId,BlockId,CapabilityId,ChangeKind)`和`AffectedContentIds`，将受影响子Content保守传播至引用它的父流程。V2状态机进入/退出能力也列入直接受影响节点；所有结果只引用原Content/Block，不另造持久工程Graph或运行Step状态。

诊断必须区分`IsComplete`与`UnknownContentIds`：逻辑Schema无法解码、引用Content缺失或分析预算溢出时，不得用“未找到受影响节点”解释为安全。现有功能没有直接改Core、不执行MRK、不加载设备、不删除旧Revision；Studio仍需要在实际UI中接入点击定位，Host发布仍须真实复核。这是工程定义影响范围，**不是已证明运行故障根因，也不是虚拟仿真或历史Trace重放**。

## 14. 工业外部副作用与重试静态判断（D06部分实现）

在真实Run出现PLC ACK未知、运动设备超时或结果可能已提交时，Runtime保留`Unknown/RequiresReconciliation`事实，不得将“超时”自动翻译成“ConfirmedFailed”。Decision的`MDCRecoverySafetyAssessor`基于专业Effect与Host提供的事实，返回是否静态具备重试资格，以及对账/权限/幂等证明缺口；**不会主动触达设备、发送命令或改变原MRK执行策略**。Studio/运营人员后续可据ReasonCode明确看到为什么不能直接重试。静态Allowed不是现场许可，工业HIL和授权Owner签署仍为独立门禁。

## 15. D09：MOL原始控制流的可能路径与假设分支推演（静态软件切片）

`MDCStaticPathExplorer.Explore(MOLWiringDefinition, assumedGuards?)`只读取Core权威V2连线定义；以EntryBlockId为起点沿Control边计算可能到达的原始BlockId。没有分支假设时保守展开所有可行控制出口，并返回`UndecidedBranchBlockIds`；有假设（如`bool:true`/`bool:false`）时仅按与ConditionId精确一致的边继续，错误/不可达的假设进入`InvalidAssumptionBlockIds`，`HasCompleteAssumptions`仅说明所有已访问分支有输入，并**不证明真实设备将这样运行**。

这是路径图静态分析，绝非PSM设备模型/仿真引擎或MRK Replay。实际数据条件、并发Join、时间、故障、数据内容、PLC动作必须由专业Simulator与受控MRK/Host验证；静态“可达”不能作为设备安全/发布许可。该切片复用Core Validate与现有MOLLink，不建第二套FlowGraph、第二套执行引擎，也不自动保存假设。
