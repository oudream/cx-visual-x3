# CxVisual Decision——#6 六大专业应用与完整开发案例 V2.0

> **案例规范**：每个案例必须展示“普通专业API→最少描述/注册→Studio/SDK使用→Decision判断→Runtime事实→调试/模拟→效果验收”，并与**不采用Decision**的开发成本形成可测对照。文中所有拟议 `MDC*` / Attribute / registry接口仅为**未来合同示意**，合成数据不是工业实测。六个专业Workbench为 Vision、CT3D、Automation、Inspection、HMI、Simulation；模块类别始终只有 Connector/Driver/Compute/Simulation/Service。用法边界见[#0](CxVisual_统一决策与规划体系%230总体架构与全类需求_V2.0.md)及[#3](CxVisual_统一决策与规划体系%233专业能力声明与注册_V2.0.md)。

## 00. 每个完整案例统一记录模板

| 维度 | 必填内容 |
|---|---|
| 场景/目标 | 用户要完成什么；基线方案；硬约束、软目标、测量方法 |
| 专业接入 | 已有或拟议方法、CapabilityId/Schema/版本、最小注册/可选模型 |
| 编辑使用 | 何时触发、Studio/SDK入口、Symbol/Content/Capability选择与连线 |
| Decision | 对应D01～D10组合、输入、硬拒绝/未知、策略/候选、解释 |
| Runtime/数据 | 哪个MRK/MDS/专业Owner实际执行；发布授权与数据交付 |
| 调试/仿真 | SourceMap/RunId/Invocation、错误证据、Static/Virtual/Replay/HIL层次 |
| 结果/效果 | 可检验输出、失败反例、取消/资源/安全及前后对照指标 |
| 证据等级 | Design/Source/Build/Automated/Integrated/HIL/Field 和准确SHA |

**不得把“存在专业项目”当成专业全功能已实现**。六专业的工程规模/SDK可用性差异很大，以下目标用例有意覆盖不同成熟度。

## A. Vision/AI：从单图检测到X-Ray在线产线

**目标**：16-bit 4096×4096检测帧，经预处理、缺陷检测、质量判定、NG分选；节拍/召回率目标、显存4GB硬限制、PLC命令严禁无确认重发（数字为合成夹具）。

**专业输入/代码**：现有 `CxVisionDataFlow.RunCpuInspectionPreviewAsync(image,options,sourceId,ct)` 可做独立CPU预览；`CxVisionThresholdProvider`具有MOC描述与MRK绑定。以后可将更多Vision/AI方法通过 `[CxCapability]` 或显式Provider暴露，端口含PixelFormat/ROI坐标/ModelHash/配置Revision，详细注册见#3。

**Studio/Decision工作链**：
1. 从Camera/X-Ray采集的Gray16端口拖线时，D01/D02筛选兼容预处理/检测；8-bit候选拒绝直接连接但显示可选转换及精度损失。
2. 用户给出`P95≤70ms, GPU≤4GB, 召回率≥99%`；D03/D04/D07比较经相同数据集、硬件、模型版本测得的候选，保留可行/未知/拒绝列表与备选；没有证据则Unknown。
3. D05建立`Acquire→Preprocess→Inspect→Judge→PLC`的数据端口、OK/NG/Unknown分支；D06拒绝“NG命令失败自动重发”，建议设备对账/人工介入。
4. D09静态推演OK、NG、Timeout分支；真正仿真必须注入虚拟PLC/探测器模型，生产由Host/MRK授权。
5. MRK/MDS完成真实执行和具名结果，Studio通过Trace+SourceMap定位Inspect的RawFormat/模型版本/耗时；D10解释后由用户确认下一版调整。

**结果及反例**：错误位深、GPU超限、模型版本错、PLC安全缺失必须Rejected/Unknown；单次结果携ImageId/Mask/Region/版本及RunId；virtual模式真实PLC写调用数为0。对照无Decision方案，评测重复端口校验代码数、错误发现阶段、首次Build成功率和故障定位步骤数；**不宣称现场节拍/召回率已验收**。

## B. CT3D：重建算法、内存资源与方案选择

**目标**：将 ProjectionSet 重建为 Volume，再分割/网格/三维测量；兼顾体素精度、GPU显存、整体耗时与资源竞争。

**专业输入/代码**：当前 `CxCtReconstructionOptions` 已有FCM Section/Range/专业校验；`CT3DMeshPlaneAnalysis` 等有分析代码。对外计算能力应明确 Projection Shape/Spacing、坐标系/单位、输出VolumeType、算法版本/FDK/SART后端、内存估算器和取消/进度能力；真实FCT实现不搬到Decision。

**Studio/Decision工作链**：
1. D01/D02检查投影几何/单位、重建配置交叉条件、版本/标定；错误单位/不满足FDD/FOD关系的工况按专业规则拒绝。
2. D07预算：投影驻留+体数据+重建临时缓存+并发块+拷贝；在硬上限内生成GPU全量、GPU分块、CPU候选；对于缺实测的驱动余量返回Unknown。
3. D03/D04/D08结合重建误差、p95耗时、成本与变更风险，给非支配候选和备用条件，不按算法名称硬编码“GPU一定更快”。
4. D05规划分块/并发/合并的**静态结构**，MRK/F19实际调度，专业Native/GPU真正分配；MDS交付Volume及度量产物。
5. 失败时按RunId定位OOM/Geometry/Tile，Decision基于测量提供减并发/分块建议；PSM只在具备正确模拟模型时用于资源趋势/流程虚拟检验。

**结果及反例**：缺几何、Stride不符、显存超限必须拒绝或Unknown；取消后Native/GPU内存必须在真实完成后释放；不把预测耗时当实测，不复制GB级像素给Decision。对照基线：估算误差、资源溢出事件、定位OOM时间、迭代调参次数。

## C. Automation：PLC、运动、相机与多设备协作

**目标**：相机触发→采集→检测→判定→定位/分选。两个任务共享独占运动轴/相机，网络可能断开。

**专业声明**：Connector/Driver注册稳定CapabilityId、Command/Message/Variable端口、设备协议/配置、独占组、SideEffects/Idempotency、权限和安全前置条件。现有 `PADDeviceVariableProvider`可发布MDS变量样本；`PACModbusTcpDriver`旧Porting文件注明**NOT INCLUDED IN PROJECT**，不可作为可运行正式驱动证据。

**Studio/Decision工作链**：
1. 按数据/命令/状态类型推荐后续节点，检查变量质量及设备权限；缺安全联锁不能“自动帮用户补一个True”。
2. D05/D07分析资源冲突与依赖：两路同时抢独占轴时提出串行或有效的资源隔离方案；MRK真实租约/执行组才决定排队。
3. D06依据设备命令性质建立Timeout/Retry/Unknown对账与安全恢复策略；“运动命令已发出但ACK超时”不能盲重发。
4. Virtual模式由Simulation的兼容Provider模拟设备状态与异常；生产必须由Host与真实PLC安全Owner检查许可/联锁后执行。
5. MRK Trace记录Flow/CommandId/Attempt与设备确认，Decision对照故障和策略提出后续修改建议。

**结果及反例**：未授权启动、无资源租约、独占冲突、Unknown命令重试全部阻断；模拟命令不得触达真机；对照无Decision流程评估错误提前发现率、资源冲突/异常恢复策略覆盖与误动作次数（必须0）。

## D. Inspection：质量判定与结果溯源

**目标**：结合Vision/CT的多个测量结果，按批次/产品版本判定OK/NG/Review，解释哪条业务规则、哪项证据导致结果。

**专业声明**：Inspection提供QualityJudge Capability、`DefectSummary→JudgeResult`强类型端口、规则及产品/批次身份、版本、拒判/复检条件；不能把规则编译到Decision代替专业判定引擎。

**使用链**：Studio将检测结果连接质量判定节点；D01/D02检查类型、单位、规则参数/版本、缺字段/不确定结果；D03/D06协调误报/漏报与复检/人工复核目标；MRK运行专业判定并形成具名结果和证据；Trace绑定产品Run/检测步骤/算法配置；Decision提供影响分析（改阈值可能影响哪些产品/分支）。

**失败反例/效果**：缺关键测量、数据过期、版本不匹配、质量证据不足不得默认OK；不能把设备NG输出当成Decision直接动作。验收跨多层流程能从NG ResultItem追到原图/ROI/算法/规则版本/受控数据。

## E. HMI：Symbol、变量绑定、状态、命令与运行关联

**目标**：工程人员使用已有 MEMSymbol 创建 HMI 页面，用于显示设备点位、流程状态、缺陷统计；在正确权限下发出明确的操作命令。

**专业输入**：Symbol公开属性与类型化绑定、变量源/质量、显示单位、状态动画/允许命令；配置复用MPW/FCM，变量经MDS标准读取。Symbol本身不是可执行Capability，也不是数据真源。

**Studio/Decision工作链**：
1. 用户选一个MDS Bool/Analog变量，Decision按TypeId/Quality/Unit/ReadWrite权限过滤适用的指示灯、趋势、数值Symbol；不兼容项可查看原因。
2. 对写入Button，检查命令端点、角色权限、幂等性、安全策略和现场状态；未授权的“可以画按钮”不意味着可发送设备命令。
3. 变量质量Bad/Unknown时，HMI如实显示故障/无效，不冒称Good；运行状态由MRK Snapshot/Trace只读投影而来，不让Symbol另存一份运行Status。
4. 用户选择失败Step/NG结果时跳到对应Flow/SourceMap、相关View和结构化Evidence；Decision可解释修复候选但HMI只负责呈现与确认。

**验收反例**：把数值端口绑定成未定义Boolean时提示类型/转换；对未经授权的命令不可发送；页面关闭/重开不改变MRK执行。衡量绑定失败定位步骤数、无效绑定拦截和操作员找到运行原因所需时间。

## F. Simulation：统一业务合同、虚拟调试与故障验证

**目标**：无需真实PLC/相机/机器人，就能复用同一流程，在受控虚拟环境中预先检验分支、数据、异常和策略，之后对比真实证据。

**专业输入**：PSM ModelId/Version、CompatibleCapabilityId/InputOutput、虚拟Clock/状态转移/输出生成、Scenario/Seed、可故障注入点、适用边界。仓库已有 `IPSMSimulationModel`、`PSMSimulationClock`、`PSMSimulationModule` 基础，但不代表全设备模型完成。

**Studio/Decision工作链**：
1. Decision先根据目标Capability的类型/语义/版本检查是否有可替代的Virtual Provider；不能只匹配显示名称。
2. 用户选择Scenario：正常采集、相机失帧、PLC ACK超时、运动设备占用、检测NG等。静态D05/D09先算可能路径；运行层按MRK执行Virtual Model。
3. 以固定Seed/场景/模型版本记录Loop/分支/数据/Resource状态；MRK DebugController可在安全虚拟点Pause/StepOnce；Trace/Journal与真实运行保持同一身份语义。
4. Decision比较预期与模型结果，解释未覆盖条件/失败分支；仅在获得HIL/Field实际证据后才可提升结论等级。

**验收反例**：真实设备写操作计数严格0；缺兼容模型返回Unknown/NotSupported，不能回“仿真通过”；相同种子相同模型同版本结果可重复；包含外部Unknown命令时不可按“重放成功”假定实际已完成。

## G. 跨专业完整产线：最重要的贯穿用例

~~~mermaid
flowchart TD
  A["Automation：探测器/相机触发"] --> B["Vision：预处理、检测"]
  B --> C["Inspection：OK/NG/Review"]
  C -->|"OK"| D["Service：结果归档"]
  C -->|"NG"| E["Automation：受控NG分选"]
  C -->|"Unknown"| F["安全处置/人工确认"]
  D --> H["HMI：状态、结果与Trace"]
  E --> H
  F --> H
  V["Simulation：虚拟输入/故障Provider"] -.->|"测试模式替换兼容能力"| A
  CT["CT3D：可选重建分析支线"] -.-> C
~~~

**Core定义**：Page/Content/Symbol引用、数据/控制连线、业务逻辑、工程版本、参数Schema；**Decision负责**：候选/类型/策略/资源/目标协调、静态路径与影响、可解释原因；**Runtime负责**：MRK执行/并发/租约/取消/Trace、MDS数据提交/回收、Host授权；**Professional负责**：真实设备、图像、重建、检验、仿真模型与HMI显示。

故障演示：Vision输入像素格式不符，MRK记录真实错误及RunId，Studio同图标注Block/Port，Decision检查“插入合法转换”与“换16-bit Provider”两个方案，按精度/节拍/硬约束推荐；选择经用户确认生成新Core Revision。不能在运行中偷偷改动，不能自动触发真实NG分选。

## H. C# 最小用法对照（现有真实 vs 拟议）

~~~csharp
// 真实源码中已有的CPU Vision单图快速预览入口（需准备有效image/options/sourceId）
CxVisionRunSummary preview =
    await CxVisionDataFlow.RunCpuInspectionPreviewAsync(
        image, options, sourceId, cancellationToken);
~~~

~~~csharp
// 真实MRK轻量编排类存在；演示方向，完整签名以当前源码和示例为准
var runtime = new MRKCodeRuntimeBuilder()
    .CapabilityForRequest<int, int>("double", (n, ct) =>
        Task.FromResult(n * 2))
    .Flow("calc", f => f.Step("calculate", "double"))
    .Build();
// 生产仍必须通过授权发布入口；本段未宣称是完整可编译示例。
~~~

~~~csharp
// 拟议MDC接口：简单使用不必先建完整工程
var compatible = decision.FindCandidates(
    MDCCandidateRequest.ForPort(outputPort, catalogSnapshot, goals, context), token);
var allowed = decision.Assess(
    MDCAssessmentRequest.ForConnection(outputPort, inputPort, catalogSnapshot, policies), token);
// 点击推荐后由Core Edit提交；Decision不自行添加Symbol/执行设备操作。
~~~

### 评价“轻量”的真实方法

对每个专业分别做两组实验：①只用专业SDK/现有MOC/MRK手工编排；②用注解/注册及Decision辅助。固定同一任务和最终业务结果，记录新增代码/配置行数、能力注册次数、接入编排点击数、有效建议率、编译前错误捕获、故障定位步骤和优化实测差异。不凭模拟数据虚报节省率。

## I. 六专业验收追踪

| Case | 核心需求/Decision能力 | 必测正例 | 必测反例 | 真实设备验收 |
|---|---|---|---|---|
| CASE-V | REG/AUT + D01/02/03/04/06/07/10 | Gray16适配、真实CPU预览、NG分支Trace | 格式错、显存超额、未确认重发 | X-Ray/PLC现场需HIL |
| CASE-C | D02/03/04/07/08/09 | 几何/资源约束下合格分块候选 | 内存未知、非法几何、Native取消 | GPU/CT实机测量 |
| CASE-A | D02/05/06/07/09 | 双设备协作、资源互斥/虚拟故障 | 无许可、重试非幂等操作 | PLC/运动安全审核 |
| CASE-I | D01/02/03/09/10 | NG判定可追到结果证据 | 缺字段却默认OK | 真实质量业务数据 |
| CASE-H | D01/02/04/10 | Symbol与MDS变量类型及质量绑定 | 未授权Button写设备 | HMI/设备联调 |
| CASE-S | D05/06/08/09/10 | 固定Seed虚拟运行/断点/故障 | 无模型却回Passed、触达真机 | Virtual不能替代Field |

**等级**：当前案例整体是Design；对应各类已有函数是Source；每个测试结果必须在未来的[Testing](../Testing/CxVisual_Decision_功能与迁移验收矩阵_V1.0.md)登记SHA/CI/设备证据，不能因案例文字宣称集成完成。


---

## J. V2.0审评：用六专业实际案例反证“轻量”与“全类”

[正式审评报告](CxVisual_统一决策与规划体系%23V2全类反推与OOP-GoF-AOP架构审评_V1.0.md)要求每个专业案例补一张“用户实际做什么→触发与输入→真实或拟议API→谁消费结果→成功/失败→实测对照”的可追溯卡，不能只有解释性文字。下面是实施前必须首先补齐的**最小验证切片**：

| 案例 | 第一条真实可落地路径 | 架构反例（必须失败） | 验收效果指标 |
|---|---|---|---|
| Vision/X-Ray | CxVisionThresholdProvider已具MOC描述+MRK绑定，验证一次合同一致性及单图预览/SourceMap | Gray8/16、模型版本/许可、NG非幂等重发 | 从注册到可编排行数/重复契约数/故障定位步骤 |
| CT3D | 选纯CPU合法几何/参数资源说明→静态候选→专业重建调用（需核实真实可用算法） | 类型/体素几何不正确、显存预测冒充实测 | 资源预测误差/过界拦截/真实运行耗时 |
| Automation | 受控Driver/虚拟设备声明Command+Idempotency→两路独占判断 | ACK超时却自动重试、无Host授权执行 | 实际设备动作0误重发/互斥竞争 |
| Inspection | 从结构化DefectSummary→JudgeResult的拟议能力，先验证规则版本和证据 | 无关键输入却默认OK | 从NG回溯原图/规则/结果链完整率 |
| HMI | Bool/Analog变量→MEMSymbol过滤/绑定/质量状态 | “可显示”即允许写PLC | 属性面板错误提示/类型与权限拦截 |
| Simulation | 以现有PSM基础和兼容虚拟Provider做固定Seed/故障时序 | 无真实模型却报告Virtual Passed或触达设备 | 可重复性、场景覆盖、虚实隔离 |

**真实状态细化**：Vision有实际CPU预览与Threshold Provider；CT3D/Automation/Simulation部分功能需要按真实类、设备/算法后端进一步确认；Inspection/HMI当前主要是工程骨架，本轮只能定义测试夹具与拟议能力。与其他专业共用的实际接口由#1/#3归一，不能在每一专业自建一套Decision SDK。

**反证实验**：同一5节点任务分别使用普通专业API/MRK手工组合与Decision辅助，以相同业务结果记录代码行数、手工端口规则、配置步骤、错误出现时机、定位耗时、P95、内存/资源误差与人机操作；仅有数据之后才能声称“更轻量、更好”。
