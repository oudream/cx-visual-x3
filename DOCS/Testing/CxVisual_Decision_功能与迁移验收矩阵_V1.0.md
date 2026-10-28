# CxVisual Decision——功能、融合迁移及分级验收矩阵 V1.0

> 2026-10-28 建立；当前为**验收计划**，不是通过报告。
> 三层：Core定义唯一真源；Decision判断/决策/规划；Runtime加载/运行唯一内核。
> 设计源：[Decision裁决](../Decisions/CxVisual_Mechanism_Decision_项目融合与归属裁决_V1.0.md)、[能力规格](../Architecture/CxVisual_统一决策与规划体系%230总体架构与10大能力_V1.0.md)、[逐文件迁移](../Migration/CxVisual_Decision_Logic与Orchestration融合及Runtime解耦_V1.0.md)。
> 证据级别：未开始 / 设计 / 源码存在 / 编译通过 / 自动测试通过 / 跨专业联调 / HIL与现场验收。**禁止以文档代替结果**。

## 一、十项能力可验收合同

| ID | 输入/场景与正例 | 必须拒绝或提示的反例 | 产物/通过标准 |
|---|---|---|---|
| D01 | 表达式、端口/规则推导与Core版本一致，逻辑短路和条件可复现 | 错类型/非法空值/未知规则/前后判断不一致 | 语义及来源/原因码可复现 |
| D02 | 兼容的端口、完整能力、依赖及合法版本 | 必需输入缺失、版本错、依赖环、硬安全前置未知或失败 | Allowed/Rejected/Unknown清晰；无安全误放行 |
| D03 | 在既定硬约束下比较节拍/精度/成本优先级 | 以软分数突破硬约束、优先级循环或缺目标 | 权重、取舍与拒绝原因明确 |
| D04 | 2+种 Provider/算法的Schema/Version/环境匹配 | 无候选可行、能力不可用/不兼容、版本变化 | 只从可行集合选择，备选/解释可查 |
| D05 | Sequence/If/Switch、Loop/Foreach、子Content、并行、DataFlow与Join | 错误依赖、无界展开、错误调用层级/端口、并行冲突 | 唯一结构Plan，行为/身份/Trace与旧版等价 |
| D06 | 声明可用重试、超时、回退、降级与补偿策略 | 非幂等设备动作自动重试、取消后继续运行、无授权补偿 | 策略适用条件与Runtime执行限制一致 |
| D07 | 已知硬件及数据大小下资源/线程/队列/内存保留估计 | 过量容量/资源冲突/无实测来源却给出保证 | 预算单位、来源、瓶颈与不确定性明确 |
| D08 | 对多个可行策略作成本/关键路径估算 | 不足样本/过期数据/错误置信区间 | 预测与实测可比，不能伪称现场节拍 |
| D09 | 只读快照验证、Virtual/Replay模拟、改动影响清单 | 仿真成功冒充真实设备安全验收 | 模拟/静态/现场证据严格分级 |
| D10 | 拒绝/选择原因码映射Core BlockId，回显Studio | 丢失SourceMap、理由不可复现、运行反馈隐式修改活动方案 | 可定位可解释，方案版本稳定 |

测试覆盖不仅看数量：必须同时检查正确性、异常、并发、性能、可取消、版本和硬安全约束；不适用项目须写明原因。

## 二、必须保留的真实业务回归案例

| ID | 当前入口/用例 | 目标门禁 |
|---|---|---|
| R01 | `OrganizationLogicIntegrationTests.G05aConditionalBranchesAndJoin` | True/False仅选中一侧，Join只执行一次；错误条件拒绝 |
| R02 | `OrganizationLogicIntegrationTests.G06CpuVisionThroughMOC` | 真实CPU视觉阈值Provider、byte[]数据、Width/Height缺失拒绝、SourceMap |
| R03 | `OrganizationLogicIntegrationTests.R05DynamicLoopAndExit` | 100000最大迭代约束、实际220轮、无100000步展开、Break/Continue/Return |
| R04 | `V2ThreeLevelNamedData` 等集成用例 | Top/Mid/Leaf命名端口数据隔离与失败不提交 |
| R05 | `G07ProjectReleaseLockAndAuthorization` | 工程版本/能力版本/发布授权不跳过，活动Plan稳定 |
| R06 | 现有 `--organization`、`--organization-wpf`、`--concurrency`、`--command-center` | C#与Studio/Runtime现有功能不回退 |
| R07 | 默认ArchitectureSmoke | 全平台未绕过其他Owner的任何失败；失败时定责、整改而非假通过 |

用例方法/CLI名称来自当前已有源码和已有工作流入口；未来执行时按该SHA重新核实其存在，单独报告各类Windows测试结论。

## 三、项目融合/唯一Owner验收

| ID | 要求 | 检查方式 |
|---|---|---|
| M01 | 由2项目合并为1 Decision；最终27项目/Mechanism 8 | .sln、项目 GUID/路径及ProjectCatalog与权威规格差异 |
| M02 | 旧 Logic、Orchestration DLL不再独立生成 | .csproj、SLN、Output目录、引用扫描 |
| M03 | Decision不引用Runtime，Runtime只引用必要的Decision合同 | csproj DAG无环、引用与using静态审计、编译 |
| M04 | Core/MOL工程真源不重复，MDS保持数据Owner | 项目/类型唯一性、保存-重开、数据并发隔离 |
| M05 | MRK编译器职责只在Decision，唯一执行器留Runtime | 逐类Owner映射及实例执行引用审计 |
| M06 | Plan声明/运行绑定分离；Plan不可序列化Delegate/Worker/Handle | DTO序列化、指纹、版本、运行Loader测试 |
| M07 | SourceMap、诊断码、版本ID/指纹保真 | 迁移前后对照及Trace回溯测试 |
| M08 | C# SDK、Studio/TestCenter统一使用接口，不需三套规则 | 端到端操作/Debug和开发者API测试 |
| M09 | 旧`MOC*`符号均有Owner/兼容/移除决策 | 逐文件迁移表、编译与消费者扫描 |
| M10 | 简单专业调用不强制经过Decision | 直接专业API/Runtime路径测试 |
| M11 | Runtime加载时对真实环境、权限、能力/版本进行最终验证 | 虚拟环境错版本/资源不足/未授权拒绝 |
| M12 | 回归无损/重试幂等、取消结算、活动Plan锁定 | 压力、并发与负例测试 |

## 四、Decision自有用例与质量量化指标

| 场景 | 最少指标（建议验收门槛在实测前协定） |
|---|---|
| 结构/能力检查 | 100%已列硬拒绝例应在任何专业动作执行前拦截；输出具体原因码/源ID |
| 大工程100/1000/10000节点 | 编译耗时、峰值内存、诊断数量、Plan大小、GC分配记录；不能预设未经实验的速度结果 |
| 动态Loop/Foreach | 编译规模不随最大迭代次数线性扩大；运行轮次与数据隔离正确 |
| 多候选选型 | 无可行项必拒绝；固定输入/约束/规则版本时决策可重现 |
| 资源与成本估算 | 估计来源、模型/硬件版本、p50/p95采样、已知限制，有实测才允许声明提升 |
| 计划复用与版本切换 | 不修改正在运行的授权计划；旧计划版本仍可追溯 |
| 并发/取消 | 请求时限、单次取消、并行评估不共享可变状态、缓存失效正确 |
| 工业安全 | 不可逆动作、未知权限/联锁、非幂等重试一律拒绝或要求真实授权与运行端复核 |
| Studio/TestCenter | 选择节点看到诊断、修复建议、候选比较、方案差异，开发者可复制诊断证据 |

## 五、分阶段的Windows验证计划（实施时填写实际结果）

| 迁移阶段 | 必须执行 | 允许宣布的完成情况 |
|---|---|---|
| P0 基线 | 固定SHA、SLN和Owner扫描、原`--organization`/默认回归 | “现状已审计”，不等于融合 |
| P1 纯合同/判定 | 编译+M01/M03/M06初步验证+D01/D02正反例 | “Decision合同已建立” |
| P2 编译迁移 | 编译/组织、R01～R05、动态/嵌套/SourceMap | “静态编译职责已迁” |
| P3 Runtime解耦 | M03～M12、版本校验、资源/异常/并发测试 | “依赖无环/Runtime可消费Decision” |
| P4 工程改名合并 | Unified.sln x64 Release Rebuild、上面所有CLI、WPF、默认回归、静态工程数量/Owner/输出扫描 | “项目融合完成”，**不代表D01～D10全部实现** |
| P5 完整能力 | D01～D10、仿真/性能/专业联调、HIL/现场及长稳 | 按能力独立分级验收，不能整体一键标生产通过 |

每条记录字段：用例ID、源码/配置、需求/输入、期望、实际、执行命令、Windows SHA/Actions链接、环境、失败原因、Owner及是否已修复。**只允许记录真实运行结果**。

## 六、失败及证据处理

- 仅设计说明不能写“通过”；GitHub Actions没有最终success不能声明完整CI通过。
- Numerics/Channel等其他Owner的历史CI失败不自动归为Decision缺陷；但默认回归未绿时，必须说明影响并不得标全量验收成功。
- 不因避免单一测试失败而跳过`--organization`、默认ArchitectureSmoke或硬约束负例。
- 迁移存在中间提交时，每个步骤必须以其**准确SHA**单独报告，不能混用不同SHA的通过证据。
- 实机PLC/X-Ray、跨进程发布、工业硬实时控制与安全联锁的验证必须单独标记“未验证”，不能用Virtual仿真冒称。
