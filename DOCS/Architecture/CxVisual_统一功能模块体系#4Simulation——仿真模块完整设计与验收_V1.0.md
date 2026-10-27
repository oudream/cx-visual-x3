# CxVisual 统一功能模块体系#4Simulation——仿真模块完整设计与验收 V1.0

> **文档类型**：专业模块完整目标规格；**生成日期**：2026-10-26；**来源基线**：旧完整规格 V1.2（承接 V1.1 六篇十八章及附录）、五类裁决 V1.2、Connector 裁决 V1.3、公共交互 V1.4。  
> **性质**：完整目标设计与验收规范，不是源码已实现声明；代码事实须以最新 `master`、实际 Windows CI 和设备 HIL 为准。  
> **原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**  
> **范围**：虚拟模型/时钟/回放/故障注入。  
> **平台**：`cx-visual-x3`，.NET Framework 4.8 / C# / WPF；保持 Foundation → Mechanism → Professional 单向依赖及现有项目 Owner。  
> **状态标识**：【裁决】架构已确认；【继承要求】原规格完整保留；【目标】待实现；【源码已有】需以对应 SHA 核实；【待验】缺 CI/HIL/长稳证据。  

## 文档来源、优先级与版本冲突

1. `CxVisual_统一功能模块体系——完整设计与验收_V1.2.md`（个人文件库，约 226 KB，继承 V1.1 全文）：保留需求编号 `UM-*`、`TFM-*`、`TFC-*`、`TFD-*`、能力/类型表、两角色案例与所有质量边界。
2. `DOCS/Architecture/CxVisual_统一功能模块体系——五类模块与设备驱动架构裁决_V1.2.md`：正式五类、Driver 归位，无第六模块。
3. `DOCS/Architecture/CxVisual_统一功能模块体系——通道规约与数据映射架构_V1.3.md`：**优先覆盖 V1.2 的 Connector 单组/Advanced 旧条款**；当前只有 `MCNConnectorConfiguration.Channel` 和 `.Protocol` 各一个；多对端属 SubChannel。
4. `DOCS/Architecture/CxVisual_公共机制体系#统一交互——四大能力与双层总线完整设计及验收_V1.4.md`：Variable/Data/Message/Command 独立 Owner、全局交互、双层总线，**并非已完成代码的证明**。
5. `AGENTS.md`、工程 Skill、`DOCS/00～04`：工程约束与类型查重；实现前必须逐对象核对 DOCS/02～04 表格及最新真实源码。

**覆盖规则**：五类优先于早期四类；V1.3 的“一 Connector 一 Channel 一 Protocol”优先于 V1.2 的 `Standard/Advanced`、多个 `Channels/Protocols`；驱动 SDK 归 Driver 优先于早期“全部归 Connector”；来源附录为了完整追溯而保留旧表述，**冲突时以本文件前部裁决条款为准**，不能从历史引用恢复已被否决的结构。

## 专业模块的通用操作与对外能力

任何模块均应明确提供：**模块身份**（ModuleId/Kind/SchemaVersion）、**提供者能力**（CapabilityId/版本/Ports/Traits/Inputs/Outputs）、**可配置性**（唯一 FCM Section）、**运行生命周期**（确有实例才创建）、**诊断**（Status/Health/Error/Trace）、**依赖**（Resource/Owner）、**产生/处理接口**（Produces/Handles 四类交互）。

**不要求各模块新建通用 `IModuleBus` 或巨大 `Execute(object)`；具体签名优先复用现有接口。** 设计示例中的新增接口、事件、字段必须经 `DOCS/02～04` 查重、源码核实和 ADR 冻结，不能直接宣称当前可编译。

## 01. 仿真模型与真实体系的边界

**【裁决】Simulation 有专业虚拟模型、时间和场景状态，但不拥有第二套工程、第二套流程执行器或假安全系统。** 真机 Real、Virtual、Replay、Hybrid 共用一套工程资源与流程/命令/状态数据契约，差异通过正式 Provider 选择显式表达；切换必须受权限/版本和目标 Scope 限定。

| 对象/职责 | 主要语义 | 应由谁管理 |
|---|---|---|
| 场景定义 | Model/Scenario/Parameters/Seed/FailureProfile、ResourceRef | MEM/正式 FCM 定义 |
| 仿真时钟 | VirtualUtc、步长、倍率、暂停/恢复、同步策略 | 专业 Simulation Clock |
| 模型状态 | 运动/设备/传感器虚拟状态、内部方程 | 专业 Simulation Provider |
| 仿真调用 | Step/Advance/Pause/Reset/InjectFault | Simulation Capability；业务编排仍 MRK |
| 接入输出 | 虚拟 Variable/Data/Message、授权范围内虚拟 Command Handler | 四大 Owner；标注 IsSimulated/Source |
| 回放输入 | 真实日志/文件/快照、Replay 通道引用 | Replay Channel 或模型回放机制，不复制驱动所有权 |

## 02. 正确性与确定性

- 使用虚拟时间，固定步长与随机种子；同一初始状态+输入+配置与代码版本应可复现同一结果或明确的数值容差；WallClock 仅作性能辅助。不可用 `Thread.Sleep` 冒充仿真时钟。
- 仿真状态推进（Step）与真实物理动作是不同 Operation；消息与状态标记来源 `Virtual/Replay/Hybrid`，不能让假 Ack 使真实 MRK 命令显示完成。
- 提供噪声、时序偏移、网络丢包、设备超时、编码器误差、过载、故障恢复、位置误差与碰撞/安全门模拟；不能以软件联锁替代真实安全 PLC。
- 模型与场景要支持快照/恢复、暂停/继续、单步、断点、轨迹检查、Golden Baseline；恢复新 Generation/Run 不继承旧授权。

## 03. 开发与工程使用

开发者只写模型状态更新和数据产生（可选 C++ 物理模型）；复用正式 Capability、M09/MDS/M36/M12 接口。工程编排者选真实/仿真 Provider、场景/种子、事件故障脚本，启动相同 MOL Flow，观察虚拟时钟/模型状态/Trace/差异。没有 Simulation Provider 时必须拒绝仿真运行，而不是落入真实设备命令。

## 04. 必需反例

不同种子随机过程不能被误报为严格确定；暂停时业务计时不得错误使用真实 WallClock；两个仿真实例不得共享可变场景全局变量；切回真实后必须重新授权真实设备动作；异常仿真状态和已发送真实命令不能被“ResetSimulation”清掉真实 MRK Operation。

## 统一交互：谁产生、谁处理、如何装配

| 输入/输出 | 产生者与处理者 | 由谁登记/通知/调度 | 关键安全及成功语义 |
|---|---|---|---|
| Variable Commit/Changed | 设备/业务/规约提交，HMI/Protocol/流程处理 | M09 原子提交与订阅；运行装配按映射连接 | 质量、SourceId/WriterId、Previous/Revision；变化不同于提交 |
| DataAvailable | Camera/AI/CT 创建，合法消费者按引用读取 | MDS/专业 Owner；Run/Generation/DataHandle 与租约 | 提醒为元信息，不广播 Image/Volume 本体 |
| Message | 任意合法模块发 Topic+Payload，多订阅者处理 | M36 发布/订阅；订阅取消和背压 | Publish/Deliver/Handle 分开；消息不可私自转成动作 |
| Command | HMI/MRK/业务发起，唯一 Handler 处理 | M12 注册精确目标；MRK Operation 协调 | 授权/资源/幂等/执行/现场确认各有结果，超时为 Unknown |

- 同一个模块可同时生产、消费、处理并再产生对象；处理关系**必须**有明确配置、代码注册、订阅、绑定或命令 Target。
- 直接映射留在对应 Owner：`MPADataMappingExecutor`、M36 订阅、M12 Handler、MDS/MOL 绑定各有正式职责；不建立万能 MappingManager。
- **动态能力发现**：`MFMModuleDescriptor.CapabilityIds`、`MOCCapabilityCatalog`、`IMRKCapabilityResolver` 等已有描述与目录为首选；模块可选择专业 Provider，但实际可用性应检查配置、依赖、Provider 状态和执行时权限。
- Connector/Protocol 可处理四大变化：变量订阅出站、数据发布引用出站、消息订阅出站、Command Handler 定向出站；入站由 Protocol 解析后调用真实 Owner。装配由运行宿主按映射创建并在 Stop 释放订阅，**不能让 Connectivity → Mechanism.Data 产生循环依赖**。

## 双角色使用合同

| 环节 | 软件开发者 | 工程编排用户（Studio/TestCenter） | 平台落实方 |
|---|---|---|---|
| 定义能力 | 提供 `CapabilityId/Version` 与参数/结果契约、真实处理者 | 浏览五类模块和 Produces/Handles | MFM/MOC/MEM |
| 配置 | 定义一份正式 FCM Schema 和专业校验 | 分类表单创建、验证、保存、加载、比较 | FCM/MHS/MPW |
| 交互 | 用现有 Owner API 发布值/消息、数据句柄或受控命令 | 选择来源、目标、触发、参数和订阅关系 | M09/MDS/M36/M12 + 绑定 |
| 调用 | L0 原生函数；L1 强类型 Provider；L2 模块能力；L3 工程运行调用 | 在 MOL 中拖入能力，MOC 校验后 MRK 执行 | 专业 Owner / MRK |
| 诊断 | 收到有意义的错误码、Operation、Trace、版本 | 查看状态、时序、故障原因及证据 | 已有日志/Trace/TestCenter |
| 安全 | 声明副作用、幂等、取消、资源及物理确认能力 | 有授权才执行真实设备动作，离线可仿真 | Target Runtime 与专业 Provider |

## 专项 TestCenter 验收清单

| ID | 场景 | 必要证据 |
|---|---|---|
| SM-P01 | 相同种子和事件序列重复仿真 | 输出相同或落在明示浮点容差内 |
| SM-P02 | 暂停、单步、快进、回滚 | 虚拟时钟与模型状态一致，不偷用真实时间 |
| SM-P03 | 故障注入：拔线、丢包、延迟、设备 Busy | MRK/业务有真实拒绝和恢复轨迹 |
| SM-P04 | Real↔Virtual↔Replay 切换 | 同一 Flow，明确环境标记与权限，绝不把模拟 ACK 当真实执行完成 |
| SM-P05 | 两仿真实例并行 | Scope/Generation 隔离，无跨实例变量污染 |
| SM-P06 | Virtual commissioning Case | 模型、Scenario、Golden、Trace 和差异报告 |

## 与总体规范的关系及修改门禁

- **共性模型、两角色、版本、能力发现、统一交互、总体测试**以 `CxVisual_统一功能模块体系#0总体架构——完整设计与验收_V2.0.md` 为唯一解释。
- 本文是该模块专有行为与验收的权威文件；遇到旧源稿的相反描述，必须按文首覆盖表处理，并在文档里保留来源追溯。
- 任何新增正式接口/类名前须检查 `DOCS/02～04`、最新源码、现有 Owner、`ReferTo`；不能用概念示意代码替代编译测试。修改后同步更新总体文档的能力归属表与本模块 TestCenter 专项验证。


## 原 V1.2 来源条款、需求与案例（逐节承接）

> **来源历史提示**：下列原文保留其需求编号、专业能力和案例，便于追踪。凡涉及旧 `Advanced` 多组、历史“相机 SDK Connector”、未查重 API/状态的文字均只是历史上下文；**本文件前部的现行裁决优先**。

## 10 SimulationModule——仿真模块

### 10.1 正式职责

维护虚拟模型、场景状态与仿真时间，在受控输入/环境条件下产生可观测的虚拟行为、数据和事件。支持虚拟设备、时序/运动模型、工业故障注入、历史回放、虚实联合调试及仿真验证；**不是**“Connector 中随机返回值”，也不是一个新的通用 Flow Runtime。

### 10.2 需求 UM-SM01～SM10

| ID | 能力要求 | 典型场景 | 反证/验收 |
|---|---|---|---|
| UM-SM01 | 模型与虚拟实体 | 轴、机器人、传感器、PLC、X-Ray、相机等状态/参数/约束 | 模型参数非法、物理边界越界必须报错 |
| UM-SM02 | 仿真时钟 | RealTime/Accelerated/Paused/Step；统一时间源或显式桥接 | 同一步多个模块时间不一致时定位原因 |
| UM-SM03 | 状态与行为演化 | 输入命令→虚拟状态迁移→数据/事件输出 | 状态转移不违背模型约束；无隐含真机操作 |
| UM-SM04 | 多对象与场景 | 多设备/站点/坐标/位置关联、初始条件、依赖 | 同对象多实例、相互影响可重复测试 |
| UM-SM05 | 虚拟数据生成 | 点值、图像、投影、运动信号、事件/消息 | 生成数据显式 `Virtual`，类型/单位兼容 |
| UM-SM06 | Replay | 固定历史输入、记录、随机种子、时间、配置版本 | 同条件重放结果可比；缺数据/版本报错 |
| UM-SM07 | 故障注入 | 断线、超时、丢帧、传感器错误、运动异常、服务失败 | 故障只影响指定范围，回收时能复原 |
| UM-SM08 | 虚实替换/联调 | 统一 Connector 能力契约映射虚拟/真实 Provider | 模拟 PASS 不放行真实危险操作 |
| UM-SM09 | 结果与模型验证 | 对目标轨迹、采样质量、时标、结果误差做比较 | 指定误差、初态/终态、可归档仿真报告 |
| UM-SM10 | 生命周期与资源 | 初始化场景、Advance、Pause、Resume、Reset、Dispose | 复位/重跑不遗留旧订阅、旧时钟、旧指针 |

### 10.3 仿真与 MRK 的关键边界

仿真**可以**有 `Advance(dt)`、虚拟世界状态与时钟（专业模型领域事实），但流程推进/调度/分支/循环/并发控制仍由 MRK 完成。仿真时钟与真实 Host 时间未必一致；两者需要显式映射（仿真步时间、墙钟时间、UTC、设备硬件时标）。Replay 不能隐式调用真实设备控制接口；HIL 必须分离虚拟状态与真实执行权限。

### 10.4 仿真与 Connector 的协作方式

推荐“同一对外能力契约，真实 Provider 与 Virtual Provider 可替换”；SimulationModule 产生虚拟状态/响应，ConnectorModule 负责把已决定的数据/命令边界表达出来。不是所有 Simulation 都必须经过 Connector，例如独立运动学仿真可以由 Compute/Simulation 直接使用；只有要模拟真实设备交互时才需要接口适配。

### 10.5 场景案例：虚拟运动卡和探测器

1. 定义虚拟运动轴、行程、加减速、触发点、仿真时钟和虚拟探测器采图速率；初始状态从版本化配置加载。
2. 由 MRK 发出准备、轨迹下载、启动等业务操作；这些操作通过 Virtual Provider 改变仿真模型，不控制物理设备。
3. Simulation 产生定位/触发/帧回调；Connector Adapter 以与真实采图相同的类型化帧元数据输出。
4. Compute 处理虚拟投影，TestCenter 对采样数量、时间误差、边界状态和缺帧恢复断言。
5. 注入一次短断线或漏触发；运行诊断能够定位设备模拟事件、对应流程节点、帧序和最终判定。
6. 全部输出/Trace 都保留执行目标 `Virtual`；切换生产需实机安全与设备确认，不能复用虚拟测试授权。

### 10.6 源码现状与增构约束

当前 `Professional/Simulation/` 在本轮目录检查中只有 `.csproj` 与 `Properties`，还不是完整专业仿真模块库；`Applications/Runtime.Host/UnifiedRuntimeHost.cs` 有软件本地模拟能力和模拟发布策略，不证明虚拟 PLC、机器人模型或 Replay 已存在。首期应先建立一个简单且确定性可验证的虚拟测点/轴模型，用同一 MDS/MRK 和 TestCenter 验证，不先建设复杂数字孪生引擎。

### 04.5 案例 U-C：仿真调试机器人/运动流程

配置机器人 Simulation 模型和运动参数，由 Connector 公开与真机可对接的控制/状态契约；在相同 MOL 流程里切换虚拟执行目标，测试报警、超时、停机等分支。仿真结果必须带 `Virtual` 标签；切换实机时必须重新编译/验证目标资源与权限，不能因仿真按钮 PASS 而允许真实移动。

### CASE-FM-C：机器人 / 运动控制与仿真调试

**目标**：`Compute(Trajectory,CoordinateTransform)`、`Simulation(VirtualRobot/Axis)`、`Connector(RobotSDK/MotionCard)`，可选 `Service(MotionStatus)`，流程由 MOL/MRK 负责。

开发者先用普通算法单测逆运动学/坐标与轨迹约束，专业 Provider 负责 SDK 会话、命令/ACK/Readback、设备回零/联锁；Simulation 实现虚拟关节、速度、加速度、限位和故障。工程用户在同一流程绑定虚拟或生产目标，先 Virtual 运行并查看轨迹/时标，再申请 HIL/实机权限。

**验收必须分开**：轨迹数学正确、软件流程正确、Virtual 预期响应、真实设备实际位置和安全联锁。命令超时可能已在机械系统执行；没有可靠 Readback 不能做“重试同一运动命令”。


## 2026-10-27 专项深化：仿真期望、完整模型需求与虚拟调试验收

> **状态：【目标规范增补】**。本章扩展 UM-SM01～SM10，新增 SIM-Rxx、SIM-Axx 作为需求/验收追踪号；不得将虚拟调试通过理解为生产机台安全验收，也不得创建第二个 Flow Runtime/Command Center。Simulation 是**虚拟世界状态与时间的专业 Owner**，公共数据/事件/命令仍由正式 Owner 实现。

### SIM-1 最终产品期望

建立能服务机器开发、产线调试、工艺评估、软件回归、算法数据生成、故障演练和虚实联合调试的通用 Simulation 体系。至少覆盖：虚拟 PLC/数字 IO、编码器/运动轴/机器人、相机/光源/X-Ray 探测器、采集触发和生产节拍；按专业实际需要逐步扩展到物理过程、材料/射线投影与几何场景。**模型支持粒度和精度必须声明**，不以物理真实为名使用简单随机数冒充专业仿真。

- **开发者**：实现模型初始化/推进/复位与状态输出，依赖标准虚拟时间、随机数种子、场景及公共交互接口；可用无硬件自动测试验证状态机与计算，内核不自行跑一套调度线程。
- **工程用户**：Studio 从工程设备树切换 Real/Virtual/Replay（Hybrid 需显式选择每个目标），选择模型版本、场景、初始条件、步长/速度、故障脚本和 Golden，点击启动/暂停/单步/快进/重置、查看模型状态/采样序列/Trace；同一 MOL 流程可在虚拟与真实目标间重新绑定，但**必须重新校验权限、安全/资源和目标身份**。
- **工业质量期望**：同一模型代码版本+参数+初始快照+种子+事件序列，在明示的数值容差下可复现；任何虚拟结果都保留 IsSimulated、ModelVersion、Scope 和来源链，不能充当真实设备校准或安全证明。

### SIM-2 专业能力矩阵

| ID | 目标能力 | 行为及验收边界 |
|---|---|---|
| SIM-R01 | 模型定义与类型发现 | 模型 TypeId/版本、支持的实体与命令、输入输出/单位/物理量、依赖、精度/简化假设和可用状态；同类模型可替换但能力契约不随意漂移。 |
| SIM-R02 | 场景/世界建模 | 设备、工件、工位、坐标系、几何/物理参数、初始条件、关联约束及对象依赖；多场景/多实例身份隔离。 |
| SIM-R03 | 仿真时钟 | 固定步长、实时、加速、暂停、单步、回放四种模式，区分 SimTime/UTC/设备硬件时间；不得用 Thread.Sleep 作为虚拟时间推进的事实。 |
| SIM-R04 | 确定性与可复现 | 固定随机种子、事件序列/顺序、初始快照、模型和配置版本；记录浮点容差、计算后端和线程并发条件；重放能比较差异。 |
| SIM-R05 | 输入事件和命令 | 命令 Request/接受/模拟执行/模拟确认、读写 State、触发源、参数范围及失效条件；命令必须对应 Virtual Handler 并显式声明模拟确认。 |
| SIM-R06 | 状态/事件演化 | 状态机、连续量积分、规则、反馈、过程间延迟、限位/行程、Busy/Fault 等边界；产生 Variable、Message、DataRef 时带模拟标签。 |
| SIM-R07 | 运动与机器人模型 | 多轴位置/速度/加速度/加加速度、软硬限位模拟、编码器、回零、轨迹、同步触发、碰撞/越界和误差噪声；模型精度分级。 |
| SIM-R08 | 虚拟 PLC/现场系统 | 数字/模拟 IO、寄存器/点值、扫描周期、报警逻辑、互锁状态模拟、通信时延/断线；只有声明需要外部字节规约时才通过 Virtual Channel + Protocol。 |
| SIM-R09 | 相机、X-Ray 与传感器 | 传感器状态、触发与帧序、曝光/噪声、缺帧、深度/点云/投影模拟；生成图像或 Volume 必须走合法 Data Owner，不无界分配内存。 |
| SIM-R10 | 物理与工艺过程 | 按需要提供热、电、力、机械、几何/射线投影/材料等专业模型；明确简化假设、有效区域、单位、边界、数值求解误差和标定来源。 |
| SIM-R11 | 故障注入 | 断线、延迟、丢包、丢帧、传感器漂移、超时、设备 Busy、限位、卡料、GPU OOM、服务不可用等，需有事件可追踪与结束/恢复条件。 |
| SIM-R12 | 录制与 Replay | 可导入合法录制、时标、模型快照/配置版本/种子；Replay Channel 只回放已记录数据，Simulation 负责必要的模型演化，两者不重复持有设备驱动。 |
| SIM-R13 | 虚实切换与 HIL | Real/Virtual/Replay/Hybrid 按 ResourceRef/Scope/Generation 明示；禁止自动回落真实设备、复制授权、虚假 ACK 或混淆结果。 |
| SIM-R14 | 联合仿真与多模型 | 多模型时间协调、同步/异步边界、因果顺序、相互依赖以及跨模型事件延迟；不可重复创建 MRK/F19 的流程调度。 |
| SIM-R15 | 暂停、快进与检查点 | Pause/Resume/Step/Reset、受控快照/恢复、时间线、关键状态 Watch；跨版本/代次不兼容快照必须拒绝。 |
| SIM-R16 | 质量与真值对照 | 对轨迹、帧序、采样时间、误差、传感器模型与真实 HIL 样本比较，生成 Golden/Baseline/差异报告；分开数值准确与业务正确。 |
| SIM-R17 | 生命周期及预算 | Init/Advance/Reset/Stop/Dispose、模型资源/历史缓存/事件队列有容量，模型失败不推进虚拟时钟，故障后可安全重置。 |
| SIM-R18 | 统一交互与能力发现 | MFM/MOC 暴露仿真模型 Produces/Handles 及参数；MDS/M09/M36/M12 仍是数据/变量/消息/命令 Owner；虚拟 Provider 与真实专业 Provider 需等价契约的可比部分。 |
| SIM-R19 | 高级场景和数据合成 | 多工位工件流、虚拟相机数据、缺陷注入、训练/测试样本生成、参数扫频/蒙特卡洛批次；记录每组参数、种子和生成规则，不混同真实产品合格数据。 |
| SIM-R20 | 操作体验与审计 | 场景编辑、模型属性/层级、时间轴、输入输出绑定、注入脚本、对照图表、运行日志和报告可在 Studio/TestCenter 观察，未实现 UI 不得作为完成依据。 |

### SIM-3 软件模型分层与现实源码核对

\`IPSMSimulationModule\` 已有 \`Initialize/Advance/Reset/SimulatedTime\`，\`IPSMSimulationModel\` 已有 \`Initialize(seed)/Advance(delta)/Reset\`；\`PSMSimulationClock\`、\`PSMSimulationModule\`、\`PSMSimulationModuleConfiguration\`（Mode/StepMilliseconds/Seed/ModelId/ConnectorRef）在当前仓库存在。**因此原 10.6 节“Professional/Simulation 只有 csproj 和 Properties”的历史审计已经过时**，应以本章当前源码核对为准；这些类证明最小模型/虚拟时钟骨架，不证明虚拟 PLC、相机、机器人/数字孪生等完整模型已存在。

目标内部层级：

1. **Model Definition**：TypeId、参数/Schema、范围/单位、能力与可重复性等级。
2. **Model Instance**：专属状态、\`Initialize/Advance/Reset\`、种子和受控快照；没有独立的永久任务循环。
3. **Scenario**：一组模型实例、初始条件、事件脚本、故障、观察点和确定性顺序。
4. **Binding**：绑定现有 MEM/MOC/MRK 的真实/虚拟资源引用；输出经公共 Owner 而不是模型私有全局变量。
5. **Evidence**：黄金基线、误差/时钟映射、仿真与 HIL 对照、Trace/版本归档。

接口新增候选按**可选**能力提出：\`ISimulationSnapshotProvider\`、\`ISimulationFaultInjector\`、\`ISimulationScenarioProvider\`、\`ISimulationObservationProvider\`；不修改现有 \`IPSMSimulationModel\` 的最小签名来强迫所有模型支持物理回滚、GUI 或复杂离散事件引擎。所有类名需实施时按 DOCS/02～04 查重。

### SIM-4 典型工业用例

1. **虚拟 PLC 与输送线**：模拟工件到位/卡料、光电/编码器输入、扫码/剔除动作；M09 给出模拟状态，MRK 运行同一检测 Flow；切换真机必须校验设备绑定与安全授权。
2. **机器人/运动轨迹**：用 Compute 计算轨迹，Simulation 推进模型位置/限位/故障；画轨迹/碰撞/速度曲线并与实际编码器历史对比；模拟 Stop 不等价硬件急停。
3. **CT 与 X-Ray 虚拟采图**：按已选扫描几何/投影序号生成可追溯的合成数据或 Replay Frame，经 MDS 投递给 Compute；比较几何、帧数、角度同步、重建结果；模拟投影不用于判定真实探测器性能。
4. **故障演练与恢复**：在指定 SimTime 注入 TCP 断线、帧丢失或设备 Fault；检查 MRK 的 Timeout/Retry/Unknown/报警和恢复结果，退出故障场景后清理订阅/资源。

### SIM-5 TestCenter 可执行验收矩阵（SIM-A01～A12）

| 验收 ID | 正反例与判定 | 层次 |
|---|---|---|
| SIM-A01 | 相同种子/步长/初态/命令序列两次得到相同结果或定义内误差 | G1 |
| SIM-A02 | 未 Initialize/零步长/负步长/模型异常不得推进 SimTime | G1 |
| SIM-A03 | 暂停、单步、加速、Reset 与快照一致；非法快照版本拒绝 | G1 |
| SIM-A04 | 运动限位、加速度、速度/停止状态按模型参数约束正确变化 | G1/G2 |
| SIM-A05 | 虚拟 PLC、相机帧/触发/数据引用的 Schema、帧序、质量标记可验证 | G1 |
| SIM-A06 | 故障注入只作用在指定资源/代次，退出后无残留 | G1 |
| SIM-A07 | Replay 缺字段/乱序/错误模型版本时准确诊断而非自动补造有效结果 | G1 |
| SIM-A08 | 相同 MOL 流程虚拟/真实目标切换，必须重新绑定、验证权限与安全 | G1/G2 |
| SIM-A09 | 模拟事件/ACK/数据在 UI/Trace/Message 中显式 Virtual，不误判为实机 | G1 |
| SIM-A10 | 双场景多实例同时运行不串变量、时钟、事件、随机源和 DataRef | G1 |
| SIM-A11 | 场景与 HIL 数据差异有误差定义、Golden、版本和可读报告 | G2 |
| SIM-A12 | 长时间/大场景/高频注入仍满足内存预算、线程清理、Reset/Stop | G1/G3 |

**实施顺序**：P0 从可确定性单步的虚拟轴/测点、公共能力绑定、Studio Watch 起步；P1 做相机/PLC、故障注入、Replay 和场景；P2 加多模型同步、工艺/物理/CT 数据；P3 虚拟投产调试、对照 HIL、长稳。**可仿真**不等于**有足够模型保真度**，每个 Model 都必须注明适用范围和限制。


## 文档维护与验收工作包

- 主仓库：`https://github.com/oudream/cx-visual-x3`。
- 先看 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00`；接口归位以 `DOCS/01～04` 为准。
- 新代码必须核实真实 Owner/源码、查看 `ReferTo` 原始成熟代码，**可直接重构融入，不加永久 Legacy/Porting Adapter**；每轮一个主题性中文提交。
- 验收分别记录 **G0 设计/类型、G1 Windows 编译与自动测试、G2 真实工业 I/O/HIL、G3 长期现场稳定与安全证据**。G0/G1 不能推导 G2/G3。

**待执行工作包**：逐类对象归属核查 → 现有源码能力差距 → 最小变更开发 → 单测/ArchitectureSmoke → Windows CI → TestCenter 双角色操作 → 设备/长稳证据 → 更新文档版本与工作台账。
