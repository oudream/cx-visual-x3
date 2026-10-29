# CxVisual Decision——#2 约束、目标与策略 V2.0

> **定位：Decision 的判断依据与策略求解唯一专题**。本文件定义语义/算法/继承/求解/验证，公开类型/签名只在[#1](CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)定义，专业发布方式只在[#3](CxVisual_统一决策与规划体系%233专业能力声明与注册_V2.0.md)定义。**以下为目标设计，非已有统一ConstraintEngine实现**。现有 FCM/MPM/MOL 规则不作为需复制的代码。

## 01. 为什么约束不是一堆 if

在 Studio 中，从某个 Gray16 图像输出端口选择下一操作，应该自动出现“类型适用的检测、可用的转换、需要GPU复核的算法、无法直接连PLC控制端口的原因”。这需要**在同一只读上下文下**组合类型/单位、专业能力、目标/资源/权限、版本与证据，而不是让工具箱维护另一本白名单。构建与发布时使用同一判断合同，不得出现“工具箱显示可用但Build用另一套规则拒绝”的分裂。

约束是来自权威Owner的**可版本化条件**；目标是希望达到的效果；策略是如何在约束范围内选择/安排/应对变化；证据是结论可信的依据。Decision只评估及建议，不可越权改写这些来源。

## 02. 约束来源、权威及融合规则

| 来源 | 约束和事实 | 归属与优先级 |
|---|---|---|
| Core MEM/MOL/MPC | 工程组织、端口Schema、逻辑依赖、范围/版本/引用 | 定义真源；不能由Decision猜测补写 |
| FCM/MHS | 参数范围、依赖、校验器、配置有效Revision | 唯一配置真源；运行以Effective/Pin版本为准 |
| MFM/专业 Provider | 能力/模型/ABI、格式、单位、幂等、资源/副作用、性能样本 | 专业所有者签署的能力事实；实际执行另核验 |
| Host/Lifecycle/安全 Owner | 授权、角色、联锁要求、放行策略、生产窗口 | 硬门禁，不受软策略权重覆盖 |
| 开发者/工程场景 | 节拍、精度、成本、优先级、偏好 | 本次目标；越权设置无效 |
| Runtime真实观测 | 资源当前占用、健康、Trace/时延/故障率 | 随时间变化的证据，运行时仍需再核验 |
| Simulation/测试 | 合成场景、模型适用范围、估计误差 | 仅作为标明级别的证据，不等于实机许可 |

**合并原则：** 同一逻辑条件按ConstraintId/Version/Scope归并来源，不是按显示名称覆盖。强制的安全/类型条件做逻辑合取；用户偏好仅能补充更严格的条件或软目标，不能覆盖较高授权硬要求。重复条件可去重；矛盾硬约束输出ConflictSet及最小冲突原因（能求得时）；不默默挑一方。不同专业单位经已验证的换算后才可比较。特定工况/有效期不满足则 Unknown。

## 03. 必须具备的约束模型

| 维度 | 可能值 | 特殊考虑 |
|---|---|---|
| 类型 | Hard / SoftObjective / Conditional / DynamicCheck | Hard优先，Unknown不通过 |
| 目标 | 节点、端口、连接、参数、流、设备资源、工程、发布 | Scope与SourceId精确定位 |
| 谓词 | Equal、Assignable、Range、Requires、Excludes、Any/All、Cardinality、Rate、Temporal等 | 版本化、可扩展；不执行任意不受信任脚本 |
| 数据 | CLR类型/FDT TypeId、Shape/Stride/ROI、单位、坐标、版本 | 不强制把GB级像素载入Decision |
| 证据 | Declaration/Static/Measured/Estimated/Virtual/Replay/HIL/Field | 带采集时间/版本/环境/有效期/可信度 |
| 结果 | Pass / Fail / Unknown | 不可把Parse失败或超时当Pass |
| 处置 | Explain、Reject、RequiredCheck、Alternative、PolicyAdvice | 只生成建议和明示前置，不作设备动作 |

规范化单条：`{Id, Owner, SubjectRef, Scope, Kind, PredicateId, PredicateVersion, Operands, Unit, ApplicableWhen, Severity, EvidenceRequirement, Revision}`。原始合同不从属于Decision数据库；它仅生成一次请求内的只读归一表达。通用表达式树/Specification允许Composite(AND/OR/NOT)，但危险操作的默认安全合取不允许被OR不合理绕开。

### 三态硬条件及聚合

| 前提 | 结果 |
|---|---|
| 存在已证实的Hard失败 | Rejected（并输出全部可可靠确定的失败） |
| 无已证实失败，但有任一Hard未知、超时、证据过期或现场条件不可判定 | Unknown + RequiredChecks |
| 全部静态可证明Hard通过 | Allowed *仅对于本次快照/静态范围* |
| 无可行候选 | 不产生Selected；显示各候选拒绝/未知原因 |

软目标绝不改变Hard结论；Runtime/Host的许可和设备健康需在动作前重新检查。算法不应调用副作用函数作为“验证”。

## 04. 用约束实现“过滤、引导、验、推演”

~~~mermaid
flowchart TB
 A["Studio: 当前选择/端口/目标/Revision"] --> B["从Core、FCM、能力目录/专业取不可变声明"]
 B --> C["类型/结构/版本/专业语义硬约束"]
 C --> D{"每个候选的三态"}
 D -->|Fail| E["不适用：原因/可能合法转换"]
 D -->|Unknown| F["待验证：缺少设备/模型/测量"]
 D -->|Pass| G["适用候选"]
 G --> H["按目标/证据/策略排序"]
 H --> I["展示推荐/备选/适用条件"]
 E --> J["Studio统一候选/连线/参数说明"]
 F --> J
 I --> J
 J -->|"确认修改"| K["Core事务 + Undo/Revision"]
 K --> L["Build期全局校验/静态路径分析"]
~~~

编辑时轻量且增量；Build时全局；发布前补现场许可；异常后使用实测快照。用户可查看不适用/未知项和原因，不应完全隐藏以免“能力消失”。

## 05. “该不该做”：必须有基线、收益、成本与风险

与现有方案 `b` 比较拟议变更 `x`。业务目标 `G` 明确时才计算效用：

- 必须说明想改善什么、损失什么；质量/精度/吞吐/时延/内存/成本/可靠性/能耗等指标带单位与测量口径。
- 基线是“保持现状”，应当与新增方案一起评价；缺基线时报告 `NeedsBaseline`。
- 先满足硬约束，再计算可比指标的增量 `ΔMetric(x,b)`、不确定性和变更风险；不能在缺量纲/数据时伪造百分比收益。
- 应给出“建议做/建议不做/暂缺证据”，明确什么样的新证据会改变结论；高风险现场动作的价值评估不代替人工授权。
- 安全与业务规则冲突时，不能以“性能收益”强行绕过。所有Soft目标可以按批准优先级/政策变更，Hard要求只能由原Owner正式修订。

## 06. “怎样做最好”：多目标而不是裸评分

设候选集 X，硬谓词 `H_i(x)`。只对所有H_i=Pass的候选组成 `F={x | ∀i H_i(x)=Pass}`。Unknown集单独保留，不参与自动选中。

1. 明确目标向量：`(p95时延, 错误率, 峰值显存, 维护成本, ...)`及单位/优化方向。
2. 采用业务优先级：安全硬门禁→强制业务目标→Pareto非支配集→词典排序或经批准的归一加权；平分时必须稳定按规则/CapabilityId/Version排序。
3. 资源估算需含同时存活缓冲、拷贝、线程/内存/显存、流背压与并发峰值，保留模型误差与峰值假设。
4. 情景/蒙特卡罗等随机推演必须固定种子、算法版本、负载分布和迭代预算；只有证据支持时才能给出“更适合当前工况”。
5. 必须同时返回Reject列表、Unknown列表、可行候选/备选、主导指标、被牺牲目标与适用前提。Pareto方案可能多于一个，不宣称绝对最优。

**示例（合成数据）**：A P95=35ms/显存4.8GB，B=58ms/3.2GB，C=82ms/0；硬要求P95≤70ms、显存≤4GB，且其他要求均合格且测量有效，则A显存拒绝、C时延拒绝、B唯一可行。若B的时延来自不可比环境则B=Unknown，**不能选它并声称通过**。


## 07. 策略分类、适用前提及 Owner

| 策略 | Decision负责 | 运行/专业Owner负责 | 风险门禁 |
|---|---|---|---|
| 选择/后备 | 候选排序、备选与失效条件 | Host批准、Runtime实际绑定 | 版本/可用性变动重新核验 |
| 分支/并行/资源 | DAG/Region、资源冲突图、并发建议 | MRK ExecutionGroups、F19租约/调度 | 不越过独占资源、内存或队列上限 |
| 超时/取消 | 描述阈值、停止后处理条件 | MRK取消、专业Native任务结算 | 取消等待≠设备/GPU已停止 |
| 重试/退避 | 判断幂等与重试许可、Backoff建议 | MRK实际重试/去重/设备确认 | PLC/运动/NG输出等非幂等不自动重发 |
| 降级/恢复 | 已验证Fallback、适用前置及触发建议 | Host/Runtime实际切换、Journal对账 | Unknown外部动作需人工/设备对账 |
| 模拟/推演 | 情景选择、可达性/风险与比较 | Professional.Simulation模型、MRK虚拟执行 | Virtual不得冒充HIL/Field |
| 工程优化 | 比较变更的风险/收益/影响 | Core有权限的编辑与审批 | 不隐式修改活动Plan |

推荐策略必须是 `PolicyId/Version, AppliesTo, Preconditions, ForbiddenWhen, Trigger, ParameterBounds, FallbackOrder, ExpectedOutcome, Evidence`。**DecisionStrategy** 决定怎样搜索/比较/建议；**ExecutionPolicy** 表达运行时如何行动，由现有 MRK 实现语义，不在Decision增加RetryEngine/ResourceManager。

## 08. 扩展方案：声明与算法插件分层

- **轻声明**：基于MPC端口、FCM属性、标准Range/Required、MFM CapabilityId和专业描述，自动生成基础类型、范围、配置依赖及版本规则。
- **显式规则**：对于跨字段、单位换算、坐标兼容和复杂业务约束，由专业提供纯校验器（尽量复用FCM Validators/已有MOL检查）；不得基于反射猜测硬件安全。
- **专业估算**：CT资源评估、Vision耗时/AI模型质量、Automation时序限制通过注册的版本化 `ResourceEstimator/QualityModel` 投影；元数据不给就返回Unknown，不要求每个L1能力提供。
- **扩展点**：Specification/Composite组织条件，Strategy插入可替换候选排序/资源估算，Adapter连接既有MOC/FCM，Decorator包审计/限时/缓存；不为模式建十个空壳Manager。
- **可信与隔离**：自定义验证器须声明纯度/取消、CPU预算、可复现性、安全日志和适用合同；不允许验证器在Studio筛选时启动设备或锁定GPU。

## 09. 性能、缓存、安全与冲突处理

1. 只对受影响的ConstraintId/Port/Block/Policy依赖子图失效缓存；依据 `ProjectRevision + CapabilitySchemaFingerprint + PolicyRevision + EnvironmentEpoch`，不使用不可稳定的GetHashCode。
2. 编辑期最低延迟目标由Studio固定数据集和工作站基准后测定；若分析超预算，返回“分析中/未知”，显示上一次结果但标记旧版不可直接提交。
3. 约束检查不必全量读取图像/Volume；优先元数据、必要时经专业Owner提供数据摘要/统计样本。多候选场景限制枚举规模、迭代次数、内存与日志量。
4. Policy冲突无自动覆盖：`Fail/Unknown/Conflict` 必有精确 SourceRef 和RuleId；管理员修订原Owner的硬策略需要独立权限审核。
5. 根据错误类型给“合法的下一步”引导，但自动转换只有在类型/精度/安全影响经过验证并取得编辑确认时才执行。
6. 运行反馈数据有敏感信息时需脱敏与权限过滤；没有证据时不得输出AI编造的设备“根因”。

## 10. 可执行验收（测试合同，不代表测试已实现）

| ID | 场景 | 可验证的判定 |
|---|---|---|
| CST-01 | Gray16→Gray8输入的直接连接 | 类型不满足则拒绝；显示允许的显式适配候选/精度风险 |
| CST-02 | GPU/内存预算Hard冲突 vs 软性能高分 | 任意HardFail拒绝，不能Selected |
| CST-03 | 设备许可过期/无现场状态 | Unknown + RequiredRuntimeChecks，不允许自动发布 |
| CST-04 | 软件/专业/业务多源约束相矛盾 | 输出ConflictSet、规则来源与保守拒绝 |
| CST-05 | 计量单位/数据形状不兼容 | 无法精确转换必须Fail或Unknown，不暗中换算 |
| CST-06 | Pareto/候选排序相同输入 | 固定权重/seed/版本结果稳定，平局规则可复核 |
| CST-07 | 非幂等设备指令 + Retry建议 | 明确拒绝自动重试，MRK不可执行二次动作 |
| CST-08 | 变更工程Revision时有延迟异步推荐 | 丢弃过期选择，Core编辑数据不被旧请求覆盖 |
| CST-09 | 无真实性能样本 | 输出Estimated/NeedsEvidence，不宣称节拍实测通过 |
| CST-10 | 大规模约束及并发取消 | 有界时间/内存，取消不可放行，诊断可定位 |

每例记录代码引用、调用入口、固定夹具、期望/实际、硬件/环境、commit、CI URL与EvidenceTier。迁移前以当前MOC/FCM/MRK实际测试为Goldens，不因新Decision文档宣称通过。