# CxVisual 统一功能模块体系——完整设计与验收 V1.1

> **文档性质**：统一功能模块体系的完整目标规格、两角色使用说明、体系协作/类型归属与验收基线；**不是已实现报告，也不是当前可编译 SDK 手册**。  
> **版本**：V1.1（2026-10-23；承接 V1.0 完整六篇十八章和附录 A–E，融合新确认的 Channel / Protocol / SubChannel / I/O / 运行线程与配置决议）。  
> **主仓库**：`https://github.com/oudream/cx-visual-x3`；审阅分支 `master`；V1.0 原始核验 HEAD `f18c6648c58ae6a3bd52eb0916b1db6936af672f`；V1.1 专项定向读取 `master` HEAD `69b89db58893b9624a306c4b714c293b95d219e4`。  
> **确定的范围**：**仅四类功能模块**——`ConnectorModule`、`ComputeModule`、`SimulationModule`、`ServiceModule`。其他计算/处理/逻辑/控制/任务/状态/复合等概念不再升格为第五类模块。  
> **适配目标**：C#、.NET Framework 4.8、Windows x64、WPF Studio；支持经审查的 C++ 原生库、厂商 SDK、外部进程及服务。  
> **总原则**：**“各方面都尽能力展现，便捷友好尽支撑各专业达到目标。”**  
> **边界**：MEM 唯一工程真源、既有配置机制、MDS 唯一运行时数据、MOL 唯一持久逻辑定义、MOC 验证/编译与能力目录、MRK 运行与控制、专业 Provider、既有 TestCenter；**不建立第二套工程、配置、数据、端口、调度或测试体系**。  
> **V1.0 源级现状**：已经核对 `AGENTS.md`、工程技能入口、七仓导航、`ProjectCatalog.csv`、相关架构规范、`MOCCapabilityCatalog.cs`、`MCCVirtualChannel.cs`、`MDSDataBindings.cs`、`UnifiedRuntimeHost.cs` 和 Vision 示例；前一轮《ConnectorModule 完整设计与验收 V1.0》本地稿作为详细设计依据。V1.1 再次定向核查 `ReferTo/Channels/{TcpClientBase,TcpClientChannel,TcpClientTransportChannel,TcpServerChannel,TcpServerService,UdpClientChannel,UdpServerChannel}.cs`、`ReferTo/Protocols/Contracts`、GCL3 `src/vdi/`、F19 `FCRWorker/FCRDedicatedExecutor` 与 `Mechanism.Connectivity` 源码。本次**没有**对 V4.9/V2.01/V6.4 三份大型 Excel 进行逐 Sheet 类型复核，类型表全部保留查重门禁；没有新的 Windows 构建、CI、设备或 UI 人工验收。  
> **交付说明**：本文件为可供后续架构审议与实现的完整文档正文；正文“必须”指**目标需求**，除非标注【现有源码】，均不表示已经落地。

---

## 阅读导引与证据等级

- **【已确定】**：用户明确确认的四类模块及已有 CxVisual 顶层职责原则；“已确定”并不等于功能已实现。
- **【现有源码】**：在本次指定仓库与 HEAD 定向读取到的实际对象和实现片段；不自动推出对应行为已通过真实 CI。
- **【目标】**：本文件定义的完整能力、用户使用体验和验收要求，待分阶段设计与实施。
- **【候选 API／类型】**：供审查的拟议名称/签名；**不能当作当前公开 SDK 编译使用**。
- **【待核】**：必须与 V4.9、V2.01、V6.4、真实 `.csproj`、当前提交、源码/CI进一步查重与验证的事项。

### 主文档总目录

| 篇 | 章 | 内容 |
|---|---|---|
| 第一篇 | 01–04 | 总体定位；完整需求；软件开发者期望；工程编排用户期望 |
| 第二篇 | 05–07 | 四类模块总体架构；统一公共契约；实例、生命周期与资源 |
| 第三篇 | 08–11 | 接入、计算、仿真、服务四类模块详细规格 |
| 第四篇 | 12–14 | 组合/数据流；CxVisual/FMP/五仓复用；完整类型/API归属 |
| 第五篇 | 15–16 | 统一开发者 API 与 Studio；六个跨模块完整案例 |
| 第六篇 | 17–18 | TestCenter 验收；实施顺序、安全/质量与完工标准 |
| 附录 | A–E | 需求追踪、类型清单、旧仓映射、可用性案例、验收/证据矩阵 |

---


### V1.1 新增决议索引（2026-10-23）

| 已同意的关键决议 | 主规范位置 | 状态 |
|---|---|---|
| Channel 自主管理 I/O/连接，但不“一通道一线程” | 07.7～07.12、08.6～08.8 | 【设计定案】 |
| AsyncIO 默认、阻塞/线程亲和才 Dedicated；Protocol 同步编写、调度可配置 | 07.7～07.12、08.13 | 【设计定案】 |
| SinglePeer / MultiPeer / SharedBus，子通道有身份、会话代次 | 08.8、08.10 | 【设计定案】 |
| Protocol 统一识别 SubChannel，不拥有 Channel 连接；七大完整数据交互能力 | 08.9～08.13 | 【设计定案】 |
| 配置/运行分离：Module→Channel→ProtocolBinding→可选 Point→MDS/Message/Command | 08.11～08.14 | 【设计定案】 |
| `ReferTo/` C# 只读；复制后直接重构，不保留额外 Legacy Adapter | 08.15、13.6、14.6～14.7 | 【开发约束】 |
| 已有类型 + 待迁源码 + 候选类型及主要方法的归属全集 | 14.6～14.7 | 【设计清单，不是待建数量】 |
| TestCenter 四根结构不变；增加真实模块配置验证与 32 项 TFC 专测 | 15.7、16.8、17.6、18.7 | 【待实现测试需求】 |

# 第一篇｜目标、完整需求与两角色期望

## 01 总体目标、定位与职责

### 01.1 为什么需要统一功能模块体系

CxVisual 目前已经有工程资源、能力目录、配置、运行数据、流程与 Runtime，也已经沉淀真实工业通信、工业视觉、设备控制、CT 与服务程序资产。但“拥有能力”和“开发者自然复用、工程用户自然组合”之间仍缺少统一可用性约定：同样是一个计算函数、一台相机、一台虚拟 PLC、一个重建服务，如何描述、配置、发布、调用、观察和验收，用户不应学习四套互不兼容的方法。

**正式定义**：统一功能模块体系是一套面向开发者和工程编排用户的**功能包装、能力声明、配置实例化、调用与协作的统一契约及使用方式**；四种 Module 是具体能力形态，基础事实和运行执行**复用已有 Owner**。Module 不等同于 DLL、进程、资源节点或可执行流程，也不是第七专业 Workbench。

### 01.2 五个概念必须分离

| 概念 | 定义与责任 | 不可以被偷换成 |
|---|---|---|
| **Module** | 组织一组相关能力及其可选配置/依赖/生命周期的功能单元 | 强制存在的重量级运行对象 |
| **Capability** | 对外可发现、可调用的一项行为，包含输入输出、版本、效果与约束 | 第二套模块注册中心 |
| **Definition/Resource** | 由 MEM 等既有工程 Owner 持久化的模块引用、配置意图、实例化方案 | WPF 控件或运行实例本身 |
| **Provider/Implementation** | C#、Native、SDK、服务等具体实现；专业逻辑由专业 Owner 负责 | MOC 的持久工程定义或 MRK 的调度器 |
| **Runtime Instance/Session** | 需要时持有状态、连接、模型、GPU、进程等资源的运行实体 | 每个纯函数都必须创建的对象 |

关系：`Module（对外能力集合）→ Capability（接口契约）→ Provider（具体实现）`；工程持有 Module 的**定义或引用**，MOC 校验解析，MRK/专业执行器按特征调用，数据经过 MDS。一个 Module 可以发布多个 Capability；普通函数不必有 Module 实例也可以被直接调用。

### 01.3 四种模块的唯一分类

| 模块种类 | 主问题 | 必须擅长的事情 | 不负责 |
|---|---|---|---|
| **ConnectorModule** | 数据和能力怎样进出边界？ | 通信/SDK/驱动/外部系统/无通道适配、事件、命令、媒体流、会话与映射 | 专业算法、统一流程调度、第二数据仓 |
| **ComputeModule** | 输入怎样转成可验证的输出？ | 数学、过滤、信号、图像、AI、CT、规则求值、脚本/原生计算、资源密集计算 | 持久流程模型、硬件执行联锁、通用任务管理器 |
| **SimulationModule** | 虚拟世界如何产生可解释的行为？ | 模型、场景、虚拟时间、状态演化、Replay、故障注入、虚实替换 | 冒充真机验收、重写 MRK 统一调度 |
| **ServiceModule** | 能力怎样持续、稳定、对内或对外提供？ | 服务入口、生命周期、请求/响应、并发、资源池、健康与优雅停止 | 第二套 Host/调度器/权限/审计框架 |

**分类规则**：每个模块声明一个主要 `ModuleKind`；一个实体可以同时具备“流式、异步、有状态、远程部署”等能力特征，但不增加第五分类。若同时涉及两种主要职责，例如“AI 计算服务”，以 `ComputeModule` 表达算法能力，由 `ServiceModule` 负责持续对外提供；允许在同一物理进程共存，但契约不能混成一个万能基类。

### 01.4 不变量与禁止事项（UM-I01～I14）

| ID | 不变量 |
|---|---|
| UM-I01 | 固定四类：Connector/Compute/Simulation/Service；不以 Processing/Logic/Task/State/Composite 增建平行模块类别。 |
| UM-I02 | 简单函数可直接调用、可选导出为能力；不强制无意义的 Connect/Start/Dispose。 |
| UM-I03 | 输入输出与能力目录复用既有 MOC/MPC/MRK 契约；不得创建影子 Port/Capability Registry。 |
| UM-I04 | 工程持久化及资源引用权归 MEM；不产生第二棵 Module 树或 Graph 真源。 |
| UM-I05 | 已有配置 Schema/生效/历史由统一配置 Owner 管理；模块仅声明需求。 |
| UM-I06 | 运行值、Heap/Stack/Frame、数据安全与租约归 MDS/FDT 对应 Owner。 |
| UM-I07 | 控制流、状态机/规则**定义**归 MOL；编译归 MOC；推进/调度归 MRK。 |
| UM-I08 | 物理设备实时、射线/机器人/运动安全联锁归硬件与专业 Owner，软件授权不能代替现场安全。 |
| UM-I09 | 运行实例不得把状态、图像帧、Trace 写成工程 Revision 或长期配置。 |
| UM-I10 | 能力定义版本、配置已保存版本/生效版本、编译计划版本、运行固定版本相互区分。 |
| UM-I11 | 无 Channel 的业务/计算接入自然成立；绝不因类型简单而强行创建 EmptyChannel。 |
| UM-I12 | 不同副作用的失败/重试策略必须分开；未确认完成的设备命令不得报告成功。 |
| UM-I13 | 公共抽象至少支撑两个独立专业，并保持 F→M→P 单向依赖，无反向引用。 |
| UM-I14 | 源码声明、Windows CI、模拟验证、真实硬件/环境验收四种证据必须分别记录。 |

### 01.5 明确非目标

不把所有静态辅助函数强行登记为模块；不要求所有模块有独立项目文件；不建立通用万能脚本引擎、微服务框架或插件商店；不要求所有模块部署为独立进程；不承诺任意原生 DLL 都能无适配直接使用；不将纯模拟行为当成物理安全认证；不因“四种模块”而修改既有六专业工作台层级。


### 01.6 【V1.1新增强制设计不变量 UM-I15～I25】

| ID | 强制基线 |
|---|---|
| UM-I15 | Channel 必须独立维护自身连接/I/O/子通道/关闭生命周期，但异步 I/O 不意味着独占线程；不得强制一通道/一子通道一 Thread。 |
| UM-I16 | Channel 以 I/O 行为能力接口抽象，Topology 采用 SinglePeer/MultiPeer/SharedBus 三类语义；拓扑不是三个新框架。 |
| UM-I17 | `ChannelId/SubChannelId/Generation` 是通信路径身份；UnitId/Station/寄存器/Topic 是 ProtocolAddress；SessionId/CorrelationId 另有独立语义。 |
| UM-I18 | Protocol 是完整数据解释、读写、发送、交互处理组件，七大职责按需组合，不限于 Codec/Parser/基础数字协议。 |
| UM-I19 | Protocol 统一能识别、路由指定 SubChannel，但不自建/占有子通道或 Socket，也不让多个 Parser 同时抢读一条原始流。 |
| UM-I20 | Protocol 代码可以同步实现；默认有界 Serial 处理，MultiPeer 可 KeyedSerial；并发/专用线程复用 F19，复杂业务复用 MRK。 |
| UM-I21 | Protocol 接收、解释、消息/命令操作严格隔离资源 Owner，不建立第二套变量表/消息中心/命令执行器/通用调度器。 |
| UM-I22 | Channel/Protocol/模块/点表可保存的是定义与绑定，Socket/子通道动态实例、Parser/事务、运行值不能写回工程配置。 |
| UM-I23 | `ReferTo` 只读，取用 C# 源码时复制到目标 Owner 后直接改名、去重、重构和测试，不能保留 Legacy→Adapter→New 双层运行链。 |
| UM-I24 | PointTable 仅在设备点地址映射确有意义时使用；无通道 SDK、消息/命令型规约不强制 PointTable/EmptyChannel。 |
| UM-I25 | 各种队列必须有容量及字节预算，安全写入不能静默丢弃；控制指令的 Sent/ACK/Completed/Unknown 不得互相代替。 |

## 02 完整功能需求与应用场景

### 02.1 需求表达和等级

编号格式 `UM-Cxx`（共性）、`UM-CNxx`（接入）、`UM-CPxx`（计算）、`UM-SMxx`（仿真）、`UM-SVxx`（服务）、`UM-Xxx`（跨模块与用户）。“必须”是 V1.0 最终目标；每项验收必须关联**实际 Owner + 执行动作 + 可观测证据 + 负例**，不能单凭 UI 按钮和类名判定通过。

### 02.2 共性完整需求（UM-C01～UM-C20）

| ID | 需求能力 | 具体行为与最低验收 |
|---|---|---|
| UM-C01 | 身份与版本 | 能力 ID 稳定、模块定义与实例 ID 不混淆；修改版本、保存重开引用仍可解析，冲突明确拒绝。 |
| UM-C02 | 能力目录 | 模块公开多个能力，复用现有能力目录；重复 ID、缺依赖、缺 Provider 返回准确诊断。 |
| UM-C03 | 类型化输入输出 | 端口/参数名称、类型、必填、默认值、单位和方向可发现；误连可在 MOC 校验阶段报错。 |
| UM-C04 | 配置 Schema | 用已有配置机制表达参数、来源、校验、历史、生效版本；非法配置不污染已生效配置。 |
| UM-C05 | 直接使用 | 不经工程流程时能直接调用纯计算、独立诊断等能力；不因此建立私有调度器。 |
| UM-C06 | 工程使用 | 同一能力在 C# 与 Studio/MOL 中语义一致；保存重开不产生平行定义。 |
| UM-C07 | 参数绑定 | 常量、节点输出、Heap、变量、资源、合法表达式等使用已有 MDS 绑定来源。 |
| UM-C08 | 同步/异步 | 按能力支持同步、`Task`、流式和异步作业；取消、异常、结果的含义明确。 |
| UM-C09 | 生命周期按需 | 纯计算无实例；有资源模块支持独立实例状态；长服务支持优雅停止。 |
| UM-C10 | 资源安全 | Buffer、SDK 指针、GPU、文件、Socket、SHM 有明确 Owner/租约/释放责任；错误路径无泄漏。 |
| UM-C11 | 并发与隔离 | 不同 Run、产品 SN、设备实例相互隔离；不可重入 Provider 禁止并发或明确串行。 |
| UM-C12 | 异常诊断 | 错误码、发生阶段、模块/能力/实例/Run 关联、Cause 和 Trace 可查；未实现不得默认成功。 |
| UM-C13 | 可观察性 | 显示健康、执行耗时、队列、消息/帧指标、资源用量；大数据默认仅查元信息。 |
| UM-C14 | 授权与副作用 | Read、Write、Control、ExternalSideEffect 等权限分离；未授权无法触发真实动作。 |
| UM-C15 | 资源可复用 | 支持实例参数覆盖、模板、引用、同能力在不同工程/站点使用，无隐式全局单例状态串扰。 |
| UM-C16 | 版本兼容 | Schema、Capability、Provider ABI/协议版本检查；不兼容时阻断并给出迁移指引。 |
| UM-C17 | 执行目标 | Virtual、Replay、Sandbox、HIL、Production 区分；“测试通过”需注明目标和证据。 |
| UM-C18 | 可扩展实现 | C# / Native ABI / SDK / 本地或远程服务可适配；接口不会绑定某一个专业 DLL。 |
| UM-C19 | Studio 便捷体验 | 资源/能力目录、组合配置面板、端口、状态、错误、调试；WPF 不拥有数据或工程真源。 |
| UM-C20 | TestCenter 验证 | 每种模块最少一真实 Owner 案例、一个负例、一跨模块案例；CI/实机证据独立追踪。 |

### 02.3 四种模块专有需求总表

| 需求组 | 关键能力 | 关键反证 |
|---|---|---|
| **UM-CN01～CN10** | 通道可选、协议/驱动、数据采集/写入、订阅/事件/命令、点表、连接生命周期、媒体、路由、质量时序、安全 | 无通道错误拒绝、命令超时误判、采图指针失效、重连重复执行 |
| **UM-CP01～CP10** | 普通函数、数据处理、数学/图像/AI/CT、规则计算、输入输出、异步/GPU、精度/确定性、模型缓存、取消/超时、大数据 | 输入污染、过量复制、数值不一致、GPU 资源泄漏、失败半提交 |
| **UM-SM01～SM10** | 模型/对象、虚拟时间、行为/状态、场景、数据生成、回放、故障注入、虚实切换、确定性、结果比较 | “假时钟”不一致、未授权真机写、回放不能重现、缺故障标签 |
| **UM-SV01～SV10** | 内部/外部服务、生命周期、契约/版本、请求、并发/背压、依赖/资源、部署、健康/恢复、安全、优雅停止 | 双处理、请求串扰、崩溃伪绿、服务停止丢在途结果、重试副作用 |

专有需求在第 08～11 章逐项定义：业务场景、程序员使用、Studio 路径、实现边界与验收证据均不得省略。

### 02.4 覆盖的业务与专业场景

| 范畴 | 代表需求 | 使用模块 | 重点数据/资源 |
|---|---|---|---|
| ICS / SCADA / IoT | 终端、协议、测点、报警、控制、边缘服务 | Connector、Compute、Service、可选 Simulation | 点值、质量、时标、控制确认 |
| PLC / IO / 仪表 | 周期读取、输出、参数设置、状态订阅 | Connector、Compute | 地址表、测量值、命令 |
| Motion / Robot | 运动参数、轨迹规划、下载、状态反馈 | Compute、Connector、Simulation | 坐标、轨迹、工作空间、安全状态 |
| 工业相机 / X-Ray | 触发采集、SDK 图像、校正、失帧监控 | Connector、Compute、Service、Simulation | 图像帧、时间戳、TriggerId |
| 在线 X-Ray 检测 | 图像处理、AI、缺陷融合、结果上传 | 四种按需组合 | 大图、缺陷结果、SN |
| CT / 3D | 投影、重建、三维数据、服务共享 | 四种按需组合 | Projection、Volume、SHM DataHandle |
| AI / GPU | 模型加载、多模型推理、结果归一 | Compute、Service | Tensor、模型、显存 |
| MES / ERP / DB | 业务查询、上传、回调、结果发布 | Connector、Compute、Service | 业务标识、幂等键、错误码 |
| 仿真/测试 | PLC 虚拟调试、模拟采图、Replay、故障注入 | Simulation、Connector、Compute | 虚拟时间、脚本、模拟标签 |
| HMI / 工程观察 | 参数编辑、绑定、趋势、运行 Trace | 四类模块的 Studio 投影 | ResourceRef、Output、Observation |

### 02.5 贯穿式场景的验收行为

1. **采集→处理→判定→上报**：采图数据拥有者明确；计算失败无结果发布；MES 上传失败不改变检测原始结果；按 SN/Run 查证据。
2. **仿真替换真实设备**：编排图、输入输出类型与主要能力保持一致；显式标记 Virtual；切换 Production 需独立授权/实机前置条件。
3. **本地计算升级为服务**：算法仍是 Compute；Service 包装请求入口、并发和生命周期，输入输出版本不漂移。
4. **多实例复用**：两个相机/两条线/两个产品使用同一定义但实例配置、缓冲、状态、异常隔离。
5. **无通道逻辑**：规则计算或内部业务驱动不额外经过虚构的 TCP/NoneChannel；根据用途选择 Compute 或无通道 Connector。

## 03 软件开发者的使用期望与完整案例

### 03.1 开发者评价指标

开发者希望像编写普通 C# 业务类一样写功能：先让代码独立正确，再以**最薄适配**暴露为可发现能力。优先使用成熟的 C# 类型/`Task`/`CancellationToken`，不要求业务函数自行维护 Module Registry、Port Schema、Host、DataHeap 或大型工厂。开发者分三条自然路径：**直接函数调用**、**能力导出与 CxVisual 流程使用**、**带资源的 Provider/Service 实例**。

| 期望 | 体验标准 | 禁止行为 |
|---|---|---|
| 易开发 | 一个正常 C# 类和必要的元信息就能提供简单能力 | 每个纯函数继承大型 ModuleBase |
| 易测试 | 原业务函数无需启动 Studio、MRK、实际设备即可单测 | 仅能通过 UI 或仿真内核验证算法 |
| 易注册 | 统一能力目录和 Resolver 投影，不重复建一套注册中心 | 为 Module 单独保存可调用端口真源 |
| 易调用 | 普通代码可直接使用；工程运行通过现有 MDS/MRK 注入数据 | 强制所有调用都建长流程 |
| 易扩展 | 模型/设备/原生库可通过 Provider 适配 | 修改核心才能支持新品牌 SDK |
| 易排障 | 错误定位到模块、能力、字段、资源、行程/帧、Run | 只返回 `false`、全局静态错误字符串 |
| 易复用 | 同一定义多实例，参数和资源可控 | 单例串状态或手工复制配置代码 |

### 03.2 案例 D-A：普通纯计算，先写标准 C# 再选择发布

**真实可独立编译的业务逻辑片段（标准 C#；不依赖任何拟议 Module API）：**

```csharp
using System;
using System.Collections.Generic;

public sealed class ThresholdStats
{
    public int BadPixels { get; set; }
    public bool Accepted { get; set; }
}

public static class PixelRule
{
    public static ThresholdStats Evaluate(
        IReadOnlyList<ushort> pixels, ushort threshold, int badLimit)
    {
        if (pixels == null) throw new ArgumentNullException("pixels");
        if (badLimit < 0) throw new ArgumentOutOfRangeException("badLimit");
        int bad = 0;
        for (int i = 0; i < pixels.Count; i++)
            if (pixels[i] > threshold) bad++;
        return new ThresholdStats { BadPixels = bad, Accepted = bad <= badLimit };
    }
}
```

普通 C# 使用：`ThresholdStats r = PixelRule.Evaluate(pixels, 3000, 20);`。测试应覆盖空输入、无坏点、阈值边界、大长度数组、负限制。这段代码没有任何统一模块依赖。作为 `ComputeModule` 发布时，**不是**把它改写成单独的调度器，而是声明 `Pixels/Threshold/BadLimit → BadPixels/Accepted` 的类型化契约，并以现有 MOC 能力目录及 MRK DataContext 适配执行。`Accepted` 是计算输出，不直接代表放行机构已动作。

### 03.3 案例 D-B：面向工程导出的候选 API（概念示例，非现有 SDK）

以下仅演示期望用法、不是已经存在的类型/方法：

```csharp
// 【候选伪 API，不能按当前 SDK 原样编译】
// modules.Compute("Inspection.PixelRule")
//   .Version(1)
//   .Input<ushort[]>("Pixels")
//   .Input<ushort>("Threshold")
//   .Input<int>("BadLimit")
//   .Output<int>("BadPixels")
//   .Output<bool>("Accepted")
//   .Use(PixelRule.Evaluate);
// 统一发布后，通过现有 MOC Capability + MRK Resolver 调用。
```

**正式设计约束**：实现时首先验证现有 `MOCCapabilityDescriptor`、`IMOCCapabilityProvider`、`IMRKCapabilityResolver`、`MPCPortDescriptor` 是否已经足以表达上述操作；优先写轻量适配函数而不是照着伪 API 再造注册器。静态类型接口、版本和异常结果必须有真实 C# 与 Studio 的双端测试。

### 03.4 案例 D-C：有资源的 AI/CT 计算

目标流程：读取已生效配置版本 → 装载模型/初始化算法上下文 → 从 MDS 获取安全输入或专业 DataHandle → 执行算法 → 原子发布输出 → 完成后保留/归还模型/GPU 资源。算法失败不把部分结果标记为成功。模型是否共享由 Provider/宿主与资源租约决策；不能在每次函数调用中隐藏重启 GPU，也不能把 GPU 指针序列化到工程文件。普通调用仍可以独立执行，加入 MRK 后才受流程生命周期、超时、取消和资源仲裁约束。

### 03.5 案例 D-D：带连接的设备 Connector

开发者只声明设备类型、参数、数据映射、操作能力、需要时的 Channel/Protocol；实际 SDK 回调、帧缓冲有效期、同步/异步结果由 Provider 维护。默认配置驱动的 PLC 使用既有 Channel/Protocol；特殊相机无需假装它是 Modbus。无通道内部业务 Connector 可以 `ChannelCount=0`，但不能把未发送控制命令伪装成成功。

### 03.6 案例 D-E：仿真与服务开发

仿真开发者只实现模型推进、虚拟输入输出及必要的场景/时钟集成；不维护第二套工程执行流程。服务开发者提供处理函数和必要的资源/健康接口；复用 Host 与 MRK 的并发/取消/授权。服务可以作为本地对象被直接调用，发布成 HTTP/IPC 时只附加合规 Adapter，不重写 Compute 算法。

### 03.7 开发者必须看得懂的失败路径

- 缺参数/类型不匹配：调用前或 MOC 构建时明确给出字段、实际与期望类型；不会默默用 `object` 降级。
- Provider 缺 DLL/ABI 不兼容：说明定位、版本和位数，不能假装模块可运行。
- 设备指令超时：显示 `Unknown` 或实际确认层级，不能自动重试非幂等动作。
- GPU 不足/帧太大：显示预算、来源、是否已提交结果；资源清理可验证。
- 服务已经停止/请求取消：在途请求保留明确终态，不在新 Run 上写旧结果。

## 04 工程编排用户的使用期望与完整案例

### 04.1 工程用户的目标

工程用户不写 C#，在 Studio 中通过**模块/能力目录 → 配置组合面板 → 流程画布 → 构建 → 授权运行/仿真 → 观察/调试**完成工作。资源树展示可独立管理的模块定义或实例引用；节点内部端口、连接与组件不自动晋升工程顶层资源。工程布局按既定 Studio 浅/深色、静态菜单 + 动态 Ribbon、中央组合配置/图元/流程三种编辑器展现，不新造 Module Studio。

### 04.2 功能操作路径与交互要求

| 步骤 | 用户动作 | Studio 应显示 | 底层 Owner |
|---|---|---|---|
| ① | 从模块目录搜索 `PLC` / `Threshold` / `Virtual Camera` / `AI Service` | 分类、描述、输入输出、版本、可用性、目标环境 | 现有能力目录及工程资源索引 |
| ② | 拖入工程资源或配置组合面板 | 定义引用、实例名称、配置 Schema、依赖/来源 | MEM + 配置 |
| ③ | 编辑参数 | 类型、范围、默认值/覆盖值、来源、版本和错误位置 | 统一配置 Owner |
| ④ | 在流程中添加公开能力 | 真实类型化端口、可连接来源、缺项提示 | MOL + MOC/MRK |
| ⑤ | 选择 Simulation/Replay/HIL/Production | 清晰的颜色/文本状态、权限、硬件风险说明 | 发布治理/专业安全 |
| ⑥ | 构建 | 缺 Provider、端口、资源权限、版本、不支持的能力逐项定位 | MOC |
| ⑦ | 开始运行或单独测试 | 已固定配置/Plan、实例状态、输入输出、事件、耗时 | MRK + Provider + MDS |
| ⑧ | 查看故障 | 模块、能力、Run、节点、Trace、外部命令阶段；打开专业 Viewer | 诊断体系/Studio |
| ⑨ | 保存/重开 | 定义、连接、映射、显示布局稳定；运行中的临时值不进入工程 | MEM |

### 04.3 完整案例 U-A：在线检测工作站

1. 工程资源选择“X-Ray 检测线”，中央显示设备、检测能力、结果上报等配置组合面板（建议按 2×N 布局，自适应）。
2. 添加 Detector Connector，选择真实厂商 Provider 或显式模拟 Provider；检查驱动版本、可用性、安全状态。
3. 添加 `ImagePreprocess` 和 `DefectJudge` Compute 能力，分别配置参数及输入输出；模型参数使用已生效版本。
4. 添加 `ResultService`；输入读取检测结果（SN、缺陷、图像引用），公开结果查询/上报状态。
5. 打开流程画布，把 `Detector.Frame` 接到 `Preprocess.Frame`、结果接 `Judge.Input`、输出接 `ResultService.Publish`；无需绘制 Heap/Lease/Push/Pop 节点。
6. 执行构建：检查类型、位深、单位、显存预算、端口、设备授权和版本。错误可双击到原工程对象。
7. 在 Virtual 模式运行：查看帧数、掉帧、处理时延、判定结果；需要时打开专业图像 Viewer，默认不复制整图到属性面板。
8. 切换 Production 前单独核验真实探测器、光源/门锁/联锁与安全授权；必须由专业实现提供强制门禁。
9. 任一帧算法失败仅使对应任务失败，不污染下一 SN；相同定义在第二台机器有独立配置和状态。
10. 保存关闭重开，验证引用/映射/配置仍一致；已运行 Trace 历史保留但不成为可编辑工程状态。

### 04.4 案例 U-B：无需硬件的业务计算与服务

工程用户选择“缺陷融合规则” Compute，添加常量/变量及节点输出绑定，再选择内部 Service 提供结果查询。整个场景**没有 Channel**，只需已有工程、数据和能力机制。为业务人员提供“测试输入→查看结果”的单能力调试入口；数据缺失、表达式无权限、服务不可用时准确报错。不因为完全无 Channel 就改写成 Connector 或创建假的 `NoneChannel`。

### 04.5 案例 U-C：仿真调试机器人/运动流程

配置机器人 Simulation 模型和运动参数，由 Connector 公开与真机可对接的控制/状态契约；在相同 MOL 流程里切换虚拟执行目标，测试报警、超时、停机等分支。仿真结果必须带 `Virtual` 标签；切换实机时必须重新编译/验证目标资源与权限，不能因仿真按钮 PASS 而允许真实移动。

### 04.6 用户体验验收红线

搜索“模块”不会出现四种互不相通的目录；常用功能不让用户填写 C# 全限定类名、Json 或指针地址；配置页不另起第二套动态表单系统；流程画布不需要绘制 GC/Heap/Lease 技术节点；运行模式颜色/标签清楚；停止按钮不暗示设备物理急停；被拒绝的危险操作必须说明安全原因和责任归属。

---
# 第二篇｜统一模块总体架构与公共能力

## 05 四种模块分类、总体架构和对外边界

### 05.1 同一功能模型，不强制同一运行模式

```text
开发者 API / Studio 专业工程用户
            │ 同一 Module/Capability 描述、Schema 和权限
            ▼
    MEM Resource / Configuration（工程定义及生效配置）
            │
            ├── MOC Catalog / Validator / Compiler / SourceMap
            │             └── MRK Runtime / Flow / Task / Session
            │                          └── MDS DataContext / Heap / Stack
            │
            └── 具体专业或共性实现（按需出现独立实例）
                   ├── ConnectorModule  → Channel / Protocol / SDK / IPC
                   ├── ComputeModule    → C# / Native / CPU / GPU / AI / CT
                   ├── SimulationModule → Virtual World / Clock / Model / Replay
                   └── ServiceModule    → Local / Background / External Endpoint
                   
TestCenter 对以上真实 Owner 进行隔离、组合、异常和实机证据验证。
```

**分类是正交的功能语义，不是部署/进程拓扑。** 例如 AI 检测可有 `ComputeModule` 与 `ServiceModule` 两个职责，并在同一进程运行；仿真模块可经虚拟 Connector 暴露数据；扫描控制中的轨迹计算是 Compute，外部运控指令是 Connector，执行顺序由 MRK 决定。

### 05.2 不另设第五种模块的归并表

| 常见叫法 | 应归属的四类模块或原有体系 | 归并原则 |
|---|---|---|
| Processing/Transformation/Filter | Compute | 数据输入变换到输出、无外部持续副作用时归 Compute |
| Algorithm/AI/CT Reconstruction | Compute | 不另建 AlgorithmModule；专业实现仍归 Vision/CT3D/Intelligence |
| Logic/Rule/Expression | Compute + MOL | **求值**可作为 Compute；逻辑**定义**与流程结构仍归 MOL |
| StateMachine | MOL + MRK，Simulation 按需持模型状态 | 状态机不是独立模块；仿真对象状态可以由 Simulation 持有 |
| Control/PID/Trajectory | Compute + Connector + 专业安全 | 策略/轨迹计算与硬件指令分离 |
| Event/Listener/Timer | Connector/Service + MRK | 消息边界归 Connector；持续监听归 Service；定时任务归 MRK |
| Data/Cache/Storage | MDS；对外数据访问可用 Service | 数据所有权不得转移给新通用模块库 |
| Task/Job | MRK；服务暴露任务 API | Task 代表运行工作而非第五种模块 |
| Script | Compute 或专业 Provider | 脚本是一种实现载体，必须受隔离、权限与版本约束 |
| Composite/Pipeline/Workflow | MEM + MOL + MOC + MRK | 组合关系直接复用现有工程与执行体系，不定义 CompositeModule |
| Dashboard/Viewer/Widget | HMI / Visual / WPF + 专业 Viewer | 表现层，不升格成计算/服务模块 |
| Historian / Report API | Service + 专业存储 Owner | 服务入口与数据存储各守职责 |

### 05.3 四类模块交叉特征矩阵

| 特征 | Connector | Compute | Simulation | Service |
|---|---|---|---|---|
| 必须有物理 Channel？ | 否 | 否 | 否 | 否 |
| 必须有运行实例？ | 仅需时 | 仅需时 | 多数需要 | 通常需要 |
| 能否同步调用？ | 可 | 可 | 可（单步/查询） | 可 |
| 能否异步/流式？ | 可 | 可 | 可 | 可 |
| 能否长期运行？ | 可 | 可（如模型进程） | 可 | 是常见模式 |
| 是否必然产生外部副作用？ | 否；控制类可能有 | 通常否；受限副作用须声明 | 虚拟世界内副作用 | 可能有，须明确 |
| 是否可单独测试？ | 必须 | 必须 | 必须 | 必须 |
| 是否可进入 MOL 流程？ | 公开能力可 | 公开能力可 | 公开能力可 | 公开能力可 |

### 05.4 “模块”在工程中有三种放置方式

- **能力引用**：函数或无状态组件只需要选择能力 ID 与参数，不创建额外资源；例如图像阈值。
- **共享工程资源定义**：相机配置、AI 模型、仿真场景、长期服务需要独立持久身份、权限和版本时，通过既有 MEM Resource/Content/专业资源模式引用。
- **运行实例**：只有连接、缓存、SDK、GPU 模型或后台工作存在时才实例化；由运行 Host/对应 Owner 管理，而不是在编辑 UI 中保留活的 SDK 对象。

一个工程可以同时引用多个能力与同一个模块定义，不能因“能力节点可选中”就为每个节点自动创建顶层 Resource。

## 06 公共能力、统一契约与开发者体验

### 06.1 最小公共语义面

公共模块描述至少可回答：`是什么(Identity)`、`能做什么(Capabilities)`、`如何配置(Configuration)`、`输入输出是什么(Ports/Schema)`、`怎样运行(ExecutionTraits)`、`依赖什么(Dependencies/Resources)`、`如何授权(SideEffects/Access)`、`如何观察(Observations)`。定义与运行状态严格分离；其中端口、配置、工程身份、能力目录分别从已有 Owner 取得，**不新建权威数据源**。

| 公共契约方面 | 必须呈现给开发者与 Studio 的信息 | 建议优先复用 |
|---|---|---|
| Module Identity | 类型、ID、版本、名称、所属专业、来源、描述 | MEM Resource/Ref 及现有 Descriptor |
| Capability Manifest | 能力 ID、公开方法、版本、端口、可用性 | `MOCCapabilityDescriptor` / `MOCCapabilityCatalog` |
| Port/Type Schema | 输入/输出、方向、必填、大小、单位、坐标空间、可空、结果生命周期 | MPC/MRK/FDT 现有端口与类型体系 |
| Configuration | 字段、默认值、校验、变更生效、覆盖层级、来源 | Foundation/Mechanism Data 的配置体系 |
| Invocation | 同步/异步/流、取消、错误、任务关联 | 现有 MRK 执行能力与专业直调接口 |
| Runtime State | 准备、运行、故障、停止、资源状态 | 现有运行/生命周期；仅补模块特有状态 |
| Diagnostics | 健康、性能、错误、Trace/Correlation、专业观察 | FLD 日志、MRK 追踪及 TestCenter Observation |

### 06.2 能力公开规则

1. 能力的唯一技术身份用 `CapabilityId + Version`；开发者见到的人类可读标题、模块分类、专业类别和说明属于元数据。类型变化、端口不兼容或效果语义发生变化必须有新版本或显式迁移策略。
2. **模块默认是能力集合，而不是一个万能 `Execute(object)`。** 常用能力明确声明如 `ReadPoint`、`GrabFrame`、`Evaluate`、`Advance`、`SubmitJob`、`GetResult`。公开端口由同一权威 Schema 生成。
3. 方法是否纯、是否有外部副作用、是否幂等、能否取消、是否可重入、最大并发、数据大小、执行目标必须可声明/可查。仅有 `bool Success` 不够。
4. 构建时必须区分**能力存在、Provider 可解析、依赖可加载、目标已授权、运行时已就绪**。MOC Catalog 的 `RuntimeAvailable` 是现有可用性入口，但不等于 SDK 物理已连通或现场被授权。
5. 受控默认：读取和纯计算可被普通流程调用；设备控制、真实发布、危险射线/运动、外部不可逆操作需要专业授权。配置保存不是对外动作的授权。

### 06.3 统一输入输出行为

- 端口必须明确 `Type`、`Nullable`、`Optional/Required`、单位/坐标、可变性与容量上限；专业 Image/Volume/Tensor 必须提供 Metadata 而不隐藏以 byte[] 当作通用值。
- 简单计算必须能直接传参返回值；在 MRK 运行时通过已有 `IMDSDataContext` 的 `Get<T>/Set/Return` 风格实现；输出只有在该调用成功提交时才对下游可见。
- 计算多输出采用原子提交；其他模块的事件和流可采用逐条/逐帧投递，必须公开是否允许丢帧、背压或重试。
- 边界错误必须提供 `Missing`/`InvalidType`/`NotReady`/`Unauthorized`/`Timeout`/`UnknownOutcome` 等可区分的语义；不要用空数组替代读取失败。
- 不允许没有所有权证明就把 SDK `IntPtr`、`byte*`、跨进程地址或临时图像句柄持久化，也不允许在 Trace 中自动深拷贝 CT 体数据。

### 06.4 配置与版本契约

模块可以有零个配置字段，但有配置时必须来自统一配置 Schema。分开显示**工程引用版本、保存配置版本、实际生效配置版本、某次 Run 的固定配置版本、能力版本、实现/ABI 版本**。动态配置更新按策略分 Immediate/下一任务生效/停机重启生效；版本切换必须尊重正在运行的任务、Provider 所持资源及专业风险。UI 未授权更改不触发写设备动作。

### 06.5 统一诊断契约

模块至少报告：Module/Capability/Instance/Run/Correlation 标识（无运行实例时标注不适用）、生效配置和实现版本、可用性、当前状态、运行目标、最近错误、计数与时间、专业观察链接。日志和指标应使用现有平台机制，**不能由四种模块各造一种日志仓库**。对含个人/商业秘密或设备凭据的内容做脱敏与权限控制。

### 06.6 对比：不应出现的过度设计

错误设计：每个函数都强制 `IModule`, `IChannel`, `IProtocol`, `IModuleWorker`, `IModuleHost`, `IModuleScheduler`, `IModuleDataStore`；这只是把模块体系变成第二个平台。正确设计：简单纯计算就是普通函数 + 可选能力声明；复杂设备才提供会话、连接、流、命令；长服务才提供 Host 资源和健康接口；仿真模型才需要时间与场景状态。

### 06.7 【V1.1共性补充】四种模块与 Channel/Protocol 的能力关系

| 问题 | 基线 | 否定的错误设计 |
|---|---|---|
| 模块分类与协议分类是否相同？ | 不同；Protocol 属于 Connector 内的一种数据处理/交互组件，四种 Module 分类保持不变 | 新建第五种 `ProtocolModule` |
| 通道是否每个都要启动自己的 OS Thread？ | 否；各通道拥有自己的 I/O 生命周期，优先原生异步接收 | 每 Channel/每 SubChannel 必须一个 Thread |
| 规约逻辑是否只能 `async Task`？ | 否；简单同步 `Process` 最自然，执行器可配置 | 强迫开发者为 CRC/解析写 Task.Run |
| 规约处理线程属于谁？ | 通用并发底座 F19；业务任务 MRK；Protocol 提供并发要求及 Key | 新建 `ProtocolThreadManager` |
| 子通道是否总是设备/站号？ | 否；通道通信身份和规约寻址完全分开 | RS-485 每站号一个 Socket/独立 Reader |
| PointTable 是否必需？ | 否；点表用于外部地址映射，Message/Command/SDK 可不用 | 强制 JSON/SDK 都拥有 PointSheet |
| 是否加一层 Adapter 搬迁成熟 C#？ | **不加**；复制 `ReferTo` 直接重构原类、合并重复接口、统一命名 | `LegacyChannelAdapter → OldChannel` 的长期双实现 |

## 07 模块实例、生命周期、运行与资源管理

### 07.1 六种运行特性（与四类模块正交）

| 特性 | 示例 | 需要的公共保障 |
|---|---|---|
| 无状态同步 | 常量规则、简单矩阵操作 | 输入校验、异常、安全输出 |
| 无状态异步 | 文件解析、CPU 批处理 | 取消/超时、输出提交与日志 |
| 有状态资源 | AI 模型、相机 SDK、GPU Context | 生命周期、租约、隔离、释放 |
| 持续服务 | HTTP/内部订阅服务、边缘守护 | Start/Ready/Stop、健康、并发/恢复 |
| 流式处理 | 相机帧、监控点流、在线推理 | 有界队列、背压、丢帧、订阅释放 |
| 作业型 | CT 重建、批量 AI、后台导出 | JobId、Progress、取消、终态/结果 |

### 07.2 生命周期目标模型（只适用于需要实例的模块）

`Declared → Resolved → Created → Initialized → Ready → Running/Busy → Stopping → Stopped → Disposed`；失败可以进入 `Faulted/Unavailable`，是否可恢复须由 Provider 声明并遵循专业策略。无状态计算无需强制创建这些状态；异步作业有独立的 Job 状态；服务有 Ready/Degraded；虚拟仿真有 Paused/Stepping（**不是**另一个 MRK 流程状态机）。

细分规则：

- `Created` 仅对象创建；`Ready` 意味着该能力准备完成，不暗示真实设备机械安全、射线许可或连接全部就绪。
- `Stop` 是软件生命周期请求，不等于 PLC/机器人安全停止；生产应有专业联锁及实际 Readback。
- `Dispose` 释放本模块持有的资源；对于共享只读缓存和 GPU 池，租约是否最后一个持有者归权威资源管理器裁决。
- 模块创建失败回滚所有已取得资源；重复 `Start`、重复 `Stop`、`Start` 与 `Dispose` 竞态行为必须写合同并测试。

### 07.3 运行实例隔离与资源分层

- **工程定义**：稳定引用，只读快照，不允许 Run 过程中随意改写。
- **模块实现 Provider**：可以按宿主/专业共享已验证的线程安全资源；资源共享策略不能静默默认全局单例。
- **模块运行实例**：设备连接、模型上下文或仿真场景的私有状态；必须有 InstanceId、Owner 和释放策略。
- **流程执行实例**：由 MRK 创建 Run/Flow/Task 身份，多个 Run 不共享不安全的可写输入和未提交结果。
- **数据生命周期**：MDS 管 Input/Local/PendingOutput/FlowHeap；图像和 CT 大数据还需专业受控 Handle/Lease；输出超过固定 Run 寿命须 Move/Copy 到明确拥有者。

### 07.4 并发、异步和背压

公共运行框架提供任务级队列、取消、限流/调度与资源竞争控制；专业 Provider 保留必须串行的 SDK/设备线程约束。对每项能力声明：线程安全、并发上限、是否重入、等待策略、队列上限、输入预算、停机排空规则。异步 Call 的 `Task` 完成必须对应**真实业务层合同**，而不是只表示请求已加入队列。流式消费者过慢时应有有界策略：阻塞上游、丢弃最旧、丢弃最新、抽样、失败停流；不能静默无限堆积。

### 07.5 取消、超时、重试与恢复

| 业务行为 | 超时后的合法语义 | 重试门禁 |
|---|---|---|
| 纯计算 | 取消/失败，未发布结果 | 可按算法幂等及资源状态重试 |
| 设备读点 | 读取未知/失联 | 可按目标地址/速率策略重试 |
| 机器人动作/运动命令 | 可能已开始/已执行，**结果未知** | 禁止盲重试；查询状态并按安全 Owner 决策 |
| MES 创建结果/上传 | 对端可能已接受 | 必须幂等键/业务状态查询或补偿策略 |
| CT 异步重建 | 提交/执行/结果可能属于不同状态 | 按 JobId 查证，不重新盲投递同一昂贵任务 |
| 仿真推进 | 虚拟事件可回放/重算，取决于模型和 RNG | 必须明确时间与种子，否则“重现”不成立 |

### 07.6 测试门禁

最少包含：一秒内重复初始化/停止；并发运行与多实例隔离；取消/超时；Provider 抛异常及部分资源创建失败；大数据句柄提前释放；服务崩溃重启；仿真 Pause/Step；流程失败后 Data Pending 不发布；跨版本升级中保持在途 Run 不漂移。安全类必须设置 HIL/现场独立门禁。

---

### 07.7 【V1.1定案】通信 I/O、规约处理、业务执行三层线程归属

**定案**：Channel 必须自主维护连接、监听、收发、子通道及关闭；但**不要求一 Channel/一 SubChannel 对应一条 OS Thread**。默认使用异步 Socket/Stream I/O（等待期间不独占线程）；同步阻塞 SDK 或有线程亲和要求的原生库才使用现有 `FCRDedicatedExecutor`。接收、解析、业务执行三段有不同的状态所有权、错误、容量和取消语义，不得混作一个执行器。

| 阶段 | 唯一行为 Owner | 默认执行方式 | 可选执行方式 | 明确禁令 |
|---|---|---|---|---|
| **A Channel I/O** | `Mechanism.Connectivity.Channels` | 异步 Accept/Read/Write；自主管理连接/重连和停止 | 专用线程执行不可异步的旧驱动/SDK | 不能在 Socket 接收回调里执行耗时 AI、同步数据库或用户任意脚本；不依赖 MRK Flow 才能维护通信 |
| **B Protocol 接收与解释** | `Mechanism.Connectivity.Protocols` | **有界串行处理**，按会话/接收流保证解释次序 | Inline（严格轻量、非阻塞）、KeyedSerial、受控 Parallel、Dedicated | 禁止无界 Task.Run；共享 Parser 并行 PushBytes；并发读同一 TCP Stream；跨 SubChannel 串帧 |
| **C 后续数据/业务行为** | MDS、未来 Message/Command、Professional；业务流程归 MRK | 对轻量合法更新同步提交；复杂处理调度到 F19/MRK | KeyedSerial/Parallel/ResourceGroup，必要的专用 SDK 执行 | Protocol 不复制 MDS 变量表/消息中心/CommandManager，也不把“已排队”冒充完成 |

**同步处理可作为程序员最简单的 API**：`ProcessReceived(inbound, context)` 是普通同步方法，但运行位置可由 `MPAProtocolDispatchOptions` 配置为 Serial/KeyedSerial/Parallel/Dedicated；异步 I/O 是 Channel 的实现，与处理方法是否 `async` 正交。遇到真正异步等待（远端请求、数据库等）时应提供 `ProcessAsync`，禁止在同步回调中 `.Result` / `.Wait()` 堵塞线程。生产代码只保留一种权威处理入口并按能力组合，表中签名属于目标示意。

### 07.8 默认处理策略及何时允许 Inline

| 模式 | 语义与适用对象 | 顺序/状态约束 | 默认与拒绝条件 |
|---|---|---|---|
| `Inline` | 在 I/O 分发路径立即执行**常量时间、已证明不会阻塞**的处理（例如固定短头校验） | 必须可重入或被显式串行化；失败立即可见 | **不默认**；不得执行用户脚本、MDS 复杂回调、设备写入；超过时延预算记录故障并退化 |
| `Serial` | 单 Protocol Session 的有界队列依次处理 | 同一帧流、同一请求的顺序保证 | **通用默认**；不会要求专用 Thread；满队列必须有 Backpressure/Reject 规则 |
| `KeyedSerial` | 同 Key 顺序，不同 Key 有条件并发 | Key 首选 `(ChannelId, SubChannelId, Generation, ProtocolBindingId)`；必要时加规约地址 | TCP Server、UDP 多 peer 默认候选；空/错误 Key 禁用或回到 Serial |
| `Parallel` | 无共享可变状态的独立工作并发 | 只有解帧完成、数据租约安全、操作支持并发才可采用 | 不给 TCP 字节流原始 chunk 直接并行 PushBytes；无幂等保障不得并发外部副作用 |
| `Dedicated` | 绑定到既有 `FCRDedicatedExecutor` | 所有必须同线程执行的 SDK 操作按其线程要求提交 | 仅 SDK/COM/驱动强约束下启用，不“一个用户配置项就为每个通道创建线程” |
| `MRK Flow` | 规约解释产生可执行业务/工程任务后使用 MRK | 使用已有 ExecutionGroup、Plan、Run 和资源仲裁 | **不负责** Channel Accept/Receive 的生命周期、心跳和重连；不用第二套 ModuleScheduler |

同一 Channel 下多 Protocol 的 demux 必须在**唯一接收所有者**中完成；先识别 Frame/Binding 再按 Session Key 调度。无法可靠区分时**拒绝多 Protocol 共享一个收流配置**，而不是让两个 Parser 竞争 `Read()`。Connection/Channel 与运行 Flow 的 Pause/Cancel 独立，不能由于暂停流程导致心跳失联。

### 07.9 队列与字节预算、背压及关闭时序

| 情形 | 规则与证据 |
|---|---|
| TCP 生产者超过处理能力 | `CapacityItems + CapacityBytes` 双上界；优先暂停读取/减少窗口形成自然背压；记录等待时延，不静默丢可靠业务帧 |
| UDP 高流量/报文突增 | Socket 层本就可能丢包；内部满队列需明确 Drop/Reject 与计数、时间和来源；不谎称可通过 TCP 式背压保证到达 |
| SDK/相机帧 | 原生回调快速安全复制或获取有效 Lease，入有界队列；按产品质量场景可配置 DropOldest/DropNewest/FailStop，不能静默丢关键帧 |
| Point/命令写入 | 不允许对非幂等命令使用默认 `DropOldest`；超时状态为 Unknown 时必须回读或业务查证，禁止自动盲重试 |
| SubChannel 断开 | 禁止分发新帧；撤销相应订阅/事务/队列；迟到响应需检查 Generation 防止串客户端 |
| Module 停止 | 停止接纳 → 取消/排空处理 → 停止协议会话 → 关闭 Channel/子连接 → 释放租约和资源；共享 Channel 按所有权不可被借用方误关 |
| 配置动态修改 | 编辑态先校验为 Pending；旧 Session 按固定配置/版本继续完成或受控停止，再切换生效；不得在数据接收中无锁替换 Parser/地址映射 |

需观察的运行指标包括：`Accepted/Rejected/Dropped`、排队 Item/Byte、单 Key 深度与延迟、每连接 Rx/Tx、协议解帧错误、事务超时、重连、句柄/租约数、停止完成时长。指标必须来自真实 Channel/Protocol/F19/MRK Owner，不造第二份统计真源。

### 07.10 网络连接拓扑与线程关系不可混淆

| 通信拓扑 | 实际连接模型 | SubChannel | 执行策略推荐 | 线程模型约束 |
|---|---|---|---|---|
| `SinglePeer` | 一个端点，如 TCP Client | 允许隐式单端点，不强制创建单元素集合 | `Serial` | 异步读写；无须专用线程 |
| `MultiPeer` | 一个监听端支持多个 TCP/WebSocket Client 或多个 UDP Peer | 每个动态连接/可信端点有唯一身份 | `KeyedSerial`（各子通道顺序） | 异步 Accept+Receive；禁止一连接一 OS Thread 的架构要求 |
| `SharedBus` | 一个串口/CAN 总线，多个逻辑目标 | 站号/CAN ID 一般先归 ProtocolAddress/帧过滤，**不是**自动创建物理 SubChannel | `Serial` 或单总线仲裁，按规约能力控制 | 不因设备数量创建多个并行串口读线程；同总线写互斥 |

`Topology` 是通道的**描述与约束**，不是新建 `SinglePeerChannelBase` / `MultiPeerChannelBase` 两套互相复制的业务基础类。

### 07.11 既有运行与基础设施 Owner 对照（源码事实）

| 现有文件/类型 | 已核实能力 | 作为本设计的复用点 | 不应误报 |
|---|---|---|---|
| `Foundation/Infrastructure/Concurrency/FCRWorker.cs` | 有界共享 Worker、按 Key 串行，接纳、结果、取消及审计 | Protocol Serial/KeyedSerial 的底层执行基础 | 不等同已有完整 Protocol Dispatch 或自动接入完成 |
| `FCRDedicatedExecutor.cs` | 专用 `Thread`、有界队列、`InvokeAsync<T>()`、`StopAsync()` | 特殊 SDK、需固定线程的 IO/调用 | 不表示可取消正在执行的不可中断原生操作 |
| `FCRWorkContracts.cs` | `FCRWorkState`、`FCROverflowPolicy`、`FCRWorkerOptions` 等 | 容量、拒绝/丢弃、任务状态 | 不复制一套 `MPAWorkerState` 或并行任务平台 |
| `MRKExecutionEngine`、统一任务调度 V1.0 | 既有流程/任务 Owner 及组/资源调度设计 | 规约解释后的复杂业务、命令流程、专业计算 | 不允许 MRK 负责原始 Socket Receive/Accept |
| `ReferTo/Channels/TcpClientBase.cs` | 传统 `Thread + Read + Sleep`，具项目经验 | 保留连接/重连行为及错误恢复细节 | 不要求保留“每通道一线程”物理结构 |
| `ReferTo/Channels/TcpClientTransportChannel.cs` | 已有异步 Task、受控读写/生命周期候选 | 对照迁入方向，优先直接融合进正式类 | 旧 `IChannel` 中两代 API 并存需去重 |
| `ReferTo/Channels/TcpServerChannel.cs` | 监听 Thread + 每客户端 Thread | 复用子连接/接收/事件语义 | 不宜作为千连接吞吐设计的默认线程模型 |
| `ReferTo/Channels/TcpServerService.cs` | 异步 Accept + 业务队列 | 复用 I/O 与业务处理分离思想 | 不能再保留另一套独立业务 Worker/Queue Manager |

### 07.12 开发者与工程用户的执行策略体验

- **开发者默认**：实现 `OnData/Process` 与特殊 `Start/Stop` 所需行为即可；普通 TCP/Modbus 无需 `new Thread`、`Task.Run`、`lock` 和手工消息队列；若 SDK 必须同线程，则声明线程亲和要求并由 F19 承载。
- **工程用户默认**：选模块类型、Channel、Protocol、Point/数据绑定即可；“高级设置”才出现 `IoMode/ProcessingMode/ExecutionGroup/Concurrency/Ordering/QueueBytes`，且受 Capability/Topology 校验。
- **UI 验证**：`KeyedSerial` 在 SinglePeer 下不强制多 Key；MultiPeer 下可查看 SubChannel 队列。禁用不支持的组合并显示原因，不默默回退成另一种行为。

# 第三篇｜四种模块的完整功能设计

## 08 ConnectorModule——接入模块

> **详细依据**：会话已形成的《CxVisual_统一接入体系#ConnectorModule——完整设计与验收_V1.0.md》（本地配套稿），包含五仓复用、50 项待查重候选类型、通道/规约、专业驱动与 TestCenter 用例。**此章规定统一模块侧边界；接入专有细节由该文档展开，不复制第二套公共配置/能力/运行定义。**

### 08.1 正式职责

把**边界外/边界内异构交互**适配为 CxVisual 可以发现和使用的操作、事件、值、媒体流与任务结果；可以有 0..N Channel、0..N Protocol、可选 Mapping、按需 Provider/Driver。真实物理通信、 SDK、RPC、IPC/SHM、进程内特殊业务适配都属于可能来源；**必须区分“无通道接入”和“不需要接入、只是纯计算”**。

### 08.2 专项需求 UM-CN01～CN10

| ID | 需求 | 目标行为 / 使用场景 | 验收与典型反例 |
|---|---|---|---|
| UM-CN01 | Channel 可选及多形态 | TCP/UDP/Serial/HTTP/IPC/SDK/SHM/None；无通道业务 Connector 不需要假的 Socket | `Channel=0` 运行；未连接不能伪造发送成功 |
| UM-CN02 | Protocol/Driver 分离 | 复用通用帧 Codec、工业规约与专用设备 SDK，实现可配置组合 | 错版帧拒绝、错误协议不可解析、厂商异常可定位 |
| UM-CN03 | 数据读写与点表 | Read/Write、周期采集、变量映射、来源、质量、时标、单位 | 缺地址/类型不匹配报错，旧点值不伪装最新 |
| UM-CN04 | 事件与订阅 | 消息订阅、SDK 回调、设备报警、事件过滤/推送 | 重连不得重复注册导致双事件；退订释放资源 |
| UM-CN05 | 命令与反馈 | Read、Write、Command、ACK、Readback、PhysicalCompleted 分开 | Send 成功≠设备完成；未知结果禁盲重试 |
| UM-CN06 | 图像/媒体/大数据 | 相机/X-Ray 采图、帧序、丢帧、FrameId/TriggerId、Buffer Lease | SDK 指针失效、无界队列、像素尺寸错误明确失败 |
| UM-CN07 | 生命周期与健康 | Connect/Initialize、重连、故障、降级、关闭、线路统计 | 断线状态可见；重连副作用受控 |
| UM-CN08 | 复杂拓扑 | 多通道角色、复合外设、通道共享、分流路由 | 同一连接复用不会争抢帧或错误关闭共享资源 |
| UM-CN09 | 服务/业务适配 | MES、ERP、DB、REST、原生 API 请求/事件与结果映射 | 业务错误不同于网络错；上传幂等与审计 |
| UM-CN10 | 安全和现场诊断 | 权限、写禁令、远程调用、协议帧、地址/命令错误定位 | 未授权真实 Write/Move/X-Ray 必须阻断 |


### 08.2.1 【V1.1新增】Connector 专项需求 UM-CN11～CN20

| ID | 目标需求 | 主要行为 / 验收要点 |
|---|---|---|
| UM-CN11 | 三类通道拓扑 | SinglePeer/MultiPeer/SharedBus 对连接模型、SubChannel/ProtocolAddress、默认调度有明确规则；TFC-004/009 |
| UM-CN12 | 子通道稳定身份 | ChannelId/SubChannelId/Generation 完整传递到接收、规约及回复；旧会话拒绝；TFC-005/006 |
| UM-CN13 | Channel 自主 I/O | 连接/监听/异步接收/重连不依赖 MRK 业务执行；停止可回收；TFC-003/029/031 |
| UM-CN14 | 同步写逻辑、灵活处理调度 | Protocol 可同步 Process，默认 Serial；按配置选择 KeyedSerial/Parallel/Dedicated/MRK；TFC-018～021 |
| UM-CN15 | 统一处理七类 Protocol 能力 | 解帧、访问、传输、交互、会话路由、数据绑定、诊断均能按需发布；不强制空实现；TFC-011～017 |
| UM-CN16 | 规约数据对象多元 | 变量/堆栈/消息/命令/媒体按合法 Owner 读取、解释和提交；TFC-024～028 |
| UM-CN17 | 配置引用及运行隔离 | Module/Channel/ProtocolBinding/Point/Variable 由统一配置持久化，动态 Peer/Socket 不入工程；TFC-001/030 |
| UM-CN18 | 安全有界背压 | TCP 限流、UDP 统计丢包、SDK BufferLease、安全写入不静默丢失；TFC-022/023 |
| UM-CN19 | 多规约共享收流安全 | 单个 Channel 有唯一原始流消费者；显式 Demux 可复用，否则配置拒绝；TFC-012 |
| UM-CN20 | 成熟源码直迁 | ReferTo 不变；直接改名/合并成熟逻辑、删除重复管理层，功能与性能回归；TFC-032/AR-08 |

### 08.3 Connector 与 Compute 的边界判据

- `ReadPLC`, `SendRobotMotion`, `GrabDetectorFrame`, `CallMesApi` 是 Connector：发生受控边界交互、SDK/设备/消息状态管理。
- `FilterFrame`, `JudgeDefect`, `BuildTrajectory`, `ParseAlreadyOwnedText` 是 Compute：对已拥有的数据计算出结果；仅纯内存输入时不应引入 Connector。
- `ReadInternalVariable` 若只是查询 MDS，直接使用已有 MDS API；若“对外暴露工业点表协议”，外部适配部分才算 Connector。
- `BusinessDriver` 可以无 Channel，但必须有可解释的**适配边界与行为**，不能为了分类把普通规则处理都放入 Connector。

### 08.4 五仓关键复用与不复制项

- `ccxx`: `CxChannelBase`、TCP/UDP/串口、`CxChannelNone`、Buffer/Thread/SHM——借鉴独立 Channel、None 模式、IO 资源；旧 C++ 单例等不得无条件迁入。
- `gcl3`: `TerminalManager`、`ProtocolBase`、地址 Worker、Measure、PSM——借鉴配置装配、点表、质量、连接和控制反馈；不复制其旧工程树。
- `device_communicator`: `CommunicatorCustom` 与 Channel/Protocol/Sheet——借鉴创建、停止、定期采集和边缘服务集成；不沿用全局易变单例。
- `cx-vision-x2`: `DriverModuleBase/PointSheet`、`IRayDetectorDriver`、`IRayXRayDriver`、S7/Modbus 驱动——借鉴真实设备行为；SDK 句柄/图像回调需要新的所有权边界。
- `cx-cct-x3`: `module_api/`、`shm_api/`、重建与检测 Bridge——借鉴跨语言 ABI、引用与大数据本体分离、Job 关联；不将原生指针序列化。

### 08.5 两角色最终验收

开发者能配置型创建 PLC 接入和 SDK 型探测器接入，均通过一致的能力描述被发现；工程用户能配置连接、点表/媒体输出、在线诊断，并在仿真与生产之间受控切换。TestCenter 最少覆盖虚拟字节帧、实际 Provider（授权时）、掉线重连、重复回调、丢帧/背压、坏地址、命令未知状态、无通道业务输入输出。

### 08.6 【V1.1定案】Channel 不是线程，不等于 Protocol，也不等于设备

- Channel：**传输资源**，负责连接、监听、收发、端点/子通道、IO 运行与诊断；既可以是 TCP 字节流，也可以是 UDP Datagram、Serial、IPC、Virtual、SHM，具体 I/O 能力按需实现。
- Protocol：**数据解释和数据交互处理**，不只是基础数字/寄存器规约：可解释、读/写、发送、请求响应、订阅事件、处理变量/运行时数据/消息/命令，必要时绑定 Channel/SubChannel；不持有第二套数据中心。
- ConnectorModule：围绕业务目的**组织** 0..N Channel、0..N Protocol、可选 PointTable、专业 Provider/Driver 以及能力引用；没有通道也可以工作。
- Point/PointTable：只负责需要设备地址映射的接入，**不是所有规约的必经层**；JSON 消息、命令请求、SDK 回调可不用传统 Point。
- Variable 与 Heap/Stack 归 MDS；Message/Command 后续归各自独立机制（当前只预留语义需求，不定义第二套完成状态/消息队列）。

### 08.7 Channel 按传输资源与 I/O 行为两轴分类

| 技术资源类别 | 示例 | 默认 IO 行为 | 子通道/拓扑 | 边界说明 |
|---|---|---|---|---|
| 网络字节流 | TCP Client、TCP Server | ByteStream | Client SinglePeer；Server MultiPeer | TCP 只提供无帧边界字节流，帧解释一般归 Protocol |
| 无连接报文 | UDP Client/Server/Multicast | Datagram | MultiPeer/Group | 每包含 Endpoint，IP Multicast 不等于可靠连接 |
| 串口/总线 | Serial、RS-485、CAN（视设备） | ByteStream 或 Frame | SharedBus | 站号/报文 ID 优先是规约/总线地址，不冒充物理 TCP 子连接 |
| 进程间 | Named Pipe、IPC Socket、SHM | ByteStream/Message/Buffer | 按连接或流 | SHM 的句柄/租约及同步机制必须明确定义 |
| 消息/应用级交互 | MQTT、WebSocket、HTTP、RPC | Message 或 RequestResponse | 订阅/会话/端点 | 当库已经封装应用层传输，不必再拆成原始 TCP 字节流 |
| 文件/模拟 | File、Replay、Virtual、Loopback | Stream/Frame/Message（按类型） | 0..N | 模拟来源标记不能冒充实机证据 |
| 厂商 SDK | Camera、X-Ray、Robot、Motion SDK | 直接 Provider 能力 | Channel 可为 0 | 不强制造 `SDKChannel` 或 `EmptyChannel`，SDK 本身有独立通讯句柄时按专业需要表达 |

**I/O 合同按需要组合**：共同通道生命周期 `IMCCChannel`；字节流 `IMCCByteStream`、数据报 `IMCCDatagramIO`、具有消息边界的 `IMCCMessageIO`、传输自带事务的 `IMCCRequestResponseIO`、共享数据 `IMCCSharedBufferIO`、监听 `IMCCChannelListener`。不得把全体方法挤进 `IMCCChannel`，也不强制 TCP 实现 RequestResponse（后者常是规约事务）。

### 08.8 一对一、一对多、共享总线及 SubChannel

| Topology | 情境 | 子通道建模 | 默认处理方式 | 重要反证 |
|---|---|---|---|---|
| `SinglePeer` | TCP Client 对服务端 | 单默认端点可无显式子通道对象 | `Serial` | 远端更换与重连 Generation 不可复用旧路由 |
| `MultiPeer` | TCP Server 多客户端、UDP 多目标 | 动态或配置型 SubChannel，各有稳定运行身份 | `KeyedSerial` | A 的回包不能发送给 B；断开重连不能错路由 |
| `SharedBus` | RS-485 多 Modbus 站，CAN 共享总线 | ProtocolAddress 与 SubChannel 分开；总线发送受序列化约束 | `Serial` | 不能把站号拆成数条同时接收的物理串口 |

**推荐寻址**：`MCCChannelAddress(ChannelId, SubChannelId?, Generation?)` 指真实通信路由；`MPAProtocolAddress` 表示 UnitId、Station、Register、Topic、设备命令码等规约目标；`ProtocolSessionId` 表示规约解析/事务状态。三者不能混用。

**父子生命周期**：父通道拥有 Socket/Listener/总线；子通道管理对端连接或逻辑端点，支持 `Find/List/Close/SendTo`；动态子通道来自 Accept/Receive 并在断开时释放，不能全部写回工程。按需在 `IMCCSubChannel` 表达 `ParentChannelId,Id,Generation,Endpoint,State`；简单 SinglePeer 无须分配繁琐子对象。

### 08.9 Protocol 的正式定义与七大能力（全面超出 Codec/Parser）

| 能力 | 能做的事 | 典型入/出 | 核心能力接口候选 | 由谁保有状态/结果 |
|---|---|---|---|---|
| P1 数据解释 | 编码、解码、Frame 解析、长度/CRC/字段校验、语义转换 | `RawData→Frame→TypedData` | `IMPAProtocolCodec`、`IMPAFrameParser`、`IMPADataInterpreter`、`IMPADataValidator` | ProtocolSession 的流式 Parser；结构化数据经 FDT/MDS |
| P2 数据访问 | 读设备地址、写设备地址、读写合法运行数据源 | `Point/Address+Value` | `IMPAProtocolReader`、`IMPAProtocolWriter`、`IMPAProtocolDataHandler` | 设备交互归 Protocol/Driver；内部值归 MDS |
| P3 数据传输 | 向某通道/子通道发送、接收、回复 | `Payload+Route→Channel` | `IMPAProtocolSender/Receiver/Responder` | 原始 IO 始终归 Channel |
| P4 交互处理 | Request/Response、Ack、订阅、事件、消息、命令 | `Request/Correlation→Response/Outcome` | `IMPAProtocolRequester`、`IMPAProtocolEventHandler`、`IMPAProtocolDataSubscriber` | 事务归 Session，命令最终状态归 Command Owner |
| P5 会话与路由 | 子通道选择、对端寻址、状态隔离、请求关联、超时 | `ChannelAddress+ProtocolAddress` | `IMPAProtocolRouter`、`IMPAProtocolSessionResolver`、`IMPAProtocolTransactionMatcher` | Channel 管子通道，Protocol 管会话 |
| P6 数据映射 | Point/Variable/Heap/Message/Command 的转换及目标选择 | `Field→MDSBinding/Message/Command` | `IMPAProtocolDataMapper`、已有 MDS Binding | Point 归 Connector，数据归 MDS，未来两体系待落实 |
| P7 运行诊断 | 生命周期、健康、计数、错误、处理策略和速率 | `State/Trace/Stats` | `IMPAProtocolHealthProvider`、`MPAProtocolDiagnostics` | 统计来自实际 Owner，F19 提供执行指标 |

**结论**：不是单一庞大 `IProtocol` 包含七类所有方法，也不分立 TransportProtocol/DataProtocol/CommandProtocol 三个体系；保留**统一 Protocol 身份与能力声明**，复杂实现通过内部可选接口/类组合。简单规约一个类足矣；Modbus 可有 Codec/Parser/Session/Point 读写；原生 SDK 无传统 Protocol 时直接由 Connector/Provider 提供能力。

### 08.10 Protocol 与 SubChannel、ProtocolAddress、Session 的三重身份

| 名称 | 必要字段 | 生命周期 | 谁创建/销毁 | 用途 |
|---|---|---|---|---|
| Channel 地址 | `ChannelId`, `SubChannelId?`, `Generation?` | 通道运行 | Channel | 定向发送及来源定位 |
| Protocol 地址 | `UnitId/Station/Area/Offset/Topic/自定义字段` | 配置或单次操作 | Protocol 定义/Binding | 规约寻址和匹配 |
| Protocol Binding | `BindingId`, `ProtocolTypeId`, `ChannelRef?`, `Selector`, `Options`, `DataBindings` | MEM 配置生效版本 | MEM/配置机制 | 将具体规约绑定到目标资源 |
| Protocol Session | `SessionId`, `BindingId`, `ChannelAddress`, `State`, `Parser`, `Transactions` | 连接/交互运行 | Protocol 实例/宿主 | 按流或 Peer 隔离有状态解释器 |
| Request/Response 关联 | `CorrelationId`, `TransactionId`, `Deadline`, `ExpectedPeer` | 一次操作 | Protocol Session | 验证迟到帧与答复归属 |

**不要规定“每个 SubChannel 固定一个独立 OS Thread/Protocol 实例”**。多个 SubChannel 可复用同一不可变 ProtocolDefinition，按 SessionKey 建不同的 Parser/状态。一个 TCP 流只能有明确的**单一入流读取所有者**，多规约共用时在 Parser 之前先解复用；无法可靠区分协议时必须在配置校验阶段拒绝。

### 08.11 Module / Channel / SubChannel / Protocol / Point / Variable 配置归属

| 对象 | 配置内容 | 运行内容 | 权威保存/作用域 | 必须校验 |
|---|---|---|---|---|
| `ConnectorModule` | 模块类型、版本、能力引用、组件引用、Ownership/策略 | 模块实例、聚合健康、专业状态 | MEM 资源/配置 | 同 ID、引用缺失、循环依赖、权限 |
| `Channel` | TypeId、Topology、IO 类型、端点、超时、重连、子通道策略 | Socket/Listener、接收缓冲、Rx/Tx 计数、Generation | 通道定义为 MEM/配置；运行由 Connectivity | 端口、IP、容量、创建/借用资源关系 |
| `SubChannel` | 静态选择规则、远端白名单、容量上限（如需） | 动态 peer/connection 实例、收发状态 | 配置态只保存规则，**不保存动态 Socket 身份** | 来源可信、连接上限、断线后 ID/Generation |
| `Protocol` | 类型、版本、功能、解析和交互参数 | 编解码、事务状态、协议错误 | 规约类型不可变；绑定属 MEM/配置 | I/O 能力匹配、Frame/数据 Schema |
| `ProtocolBinding` | ChannelRef、SubChannelSelector、ProtocolAddress、分发/重试、DataBindings | 解析缓冲及 Session 实例 | 工程定义 | 路由匹配、共享收流冲突、并发能力 |
| `PointTable` | 设备点 ID/地址/格式/单位/权限/批量采集计划 | 原始观测可经 MDS 存储；不保留第二份变量库 | Connector 配置，专业地址规则 | 地址范围、读写权限、重复映射、转换 |
| `VariableDefinition` | 类型、标识、单位、作用域、质量策略 | 值、时间、质量、来源与订阅 | MDS | 名称/类型/作用域/访问权限 |
| 数据堆栈 | 绑定到现有 MDS 运行上下文 | Run/Flow/Step 生命周期的值与 PendingOutput | MDS | 只有合法运行上下文才可写；不越权直接写任意 Run 栈 |
| Message/Command | 未来 Schema/Target/Policy 引用 | 未来统一 Owner 的消息、命令及阶段结果 | 分别由未来体系 | 不重复创建独立 Queue/CommandRunner |

**不要为每行新建一个 ConfigManager**。每种类型可以具有强类型 Options 和 Schema，保存/Undo/校验/版本生效全部由现有配置 Owner 承担。现有 `MCCChannelProfile` 的 `Address+Port+Dictionary<string,string>` 是骨架，不能作为所有通道统一配置真源；TCP/UDP/Serial/SHM 各自的 Options 必须支持类型、默认值、单位及条件可见性。

### 08.12 三种推荐的配置关系

**例 A：TCP Server + 多 SubChannel + 自定义 Protocol**

```text
ConnectorModule Server01 (ModuleRef)
 ├─ Channel TCPServer01 {Type=TcpServer,Topology=MultiPeer,ListenPort=9000,Io=Async}
 │    └─ DynamicSubChannel {Id/Generation by Channel, MaxClients=1000}
 ├─ ProtocolBinding Custom01
 │    ├─ ProtocolType=CustomMessageProtocol
 │    ├─ ChannelRef=TCPServer01; SubChannelSelector=DynamicPeer
 │    └─ Dispatch=KeyedSerial, Key=SubChannel, MaxConcurrency=8
 └─ DataMappings: ParsedMessage→MessageOwner（预留）; DeviceValue→MDS Variable
```

**例 B：RS-485 + Modbus RTU 多站号**

```text
ConnectorModule PLC_BUS
 ├─ Channel Serial01 {Port=COM3, Baud=115200, Topology=SharedBus}
 ├─ ProtocolBinding ModbusRtu01 {ChannelRef=Serial01, Dispatch=Serial}
 ├─ ProtocolAddress UnitId=1 / 2 / 3 （不是三个独立物理 SubChannel）
 └─ PointTable {station:1 register:100→MDS.Variable PLC1.Temp,
                station:2 coil:10→MDS.Variable PLC2.Running}
```

**例 C：厂商相机 SDK，无 Channel、无传统 Protocol**：`ConnectorModule Detector01 → Professional.Vision.Driver/SDK → FrameHandle/TriggerId → MDS/专业数据 Owner`。无 `EmptyChannel`、无伪 Protocol/PointSheet，厂商线程亲和由 F19 Dedicated 保障。

### 08.13 Protocol 接收至 MDS/消息/命令的完整处理链

```text
Channel async Receive / Accept
  → 快速标注 ChannelId/SubChannelId/Generation/Size/Timestamp（安全复制或 Lease）
  → 有界入站队列（按照 Connection/Session 串行，不是每个包新建线程）
  → ProtocolSession（Frame/校验/解释/事务关联；可同步 Process）
  → Mapper/Route（Point、Variable、Heap/Stack、Message、Command 的合法目标）
  → MDS 当前作用域 / 未来消息或命令 Owner / Professional 能力
  → 需要业务流程时通过 MOC/MRK 执行；返回状态/结果
  → Protocol 编码回复 → Channel.SendTo(ChannelId+SubChannelId+Generation)
```

**所有跨执行边界传递的字节或 Frame 必须拥有明确安全生命周期**：复制、不可变对象或受控 BufferLease，不得把下一次 Socket Read 会覆盖的公用 buffer 传给异步任务。真正的 Zero-copy 需以专业数据所有权/共享内存租约验证。

#### 典型数据对象映射

| 原始输入 | 规约动作 | 交付目标 | 关键边界 |
|---|---|---|---|
| `holding register:100` | 解码 UInt16/缩放 | `MDSVariableTable.Update` | 时间、质量、来源、作用域 |
| Frame/Message JSON | schema 校验，解释字段 | 未来 `Message` 发布能力 | 没有消息体系时仅做解析/预留，不返回假成功 |
| `MOVE_AXIS` 指令帧 | 权限/参数识别→命令请求 | 未来 `Command` 能力/专业设备 Owner | Sent、Ack、Complete、Unknown 区分，硬件安全独立 |
| Node 需要的输入 | 按已有 `MDSDataBinding` 从当前运行读取/回写 | 当前合法 `IMDSDataContext` | 禁止跨 Run 越权读写 Heap/Stack |
| 探测器 Frame | FrameId/TriggerId/Shape/Stride 等解析 | 专业 FrameHandle/MDS 引用 | 大内存租约与 SDK 回调寿命 |

### 08.14 Configuration/Run 两种模式与版本切换

- **设计态**：用户从能力目录选 Module/Channel/Protocol，配置端点/拓扑/子通道、点表、绑定、线程/调度策略，校验成功后保存到 MEM/统一配置 Owner；无 Socket、无 SDK 实例。
- **运行态**：从固定已生效配置创建 Channel、加载 Protocol Session、分配 Point/Variable 数据访问、订阅运行事件；每次运行记录 `ConfigurationVersion, ProtocolBindingId, SessionId, ChannelId, SubChannelId/Generation`。
- **切换**：参数生效按 Owner 策略进行 Pause/Drain/Replace；当前流程和在途设备请求不得隐式换到新的地址/客户端。失败可回滚至上一次生效版本，必须可追溯。
- **外部凭据**：密码、证书、设备授权使用现有 Secret/治理机制，主配置中只保存安全引用，TestCenter 日志不输出原文。

### 08.15 ReferTo 直接重构时的合并与裁撤表（非 Adapter）

| 成熟来源 | 直接迁入正式目标 | 必须保留 | 应消除的冗余 |
|---|---|---|---|
| `Channels/TcpClientBase.cs`、`TcpClientChannel.cs`、`TcpClientTransportChannel.cs` | `MCCTcpClientChannel`（唯一正式实现） | 连接重试、状态、读写、异常/断开、诊断 | 旧 `IChannel` 中两代动词、重复 Socket/线程状态、外层 LegacyAdapter |
| `Channels/TcpServerChannel.cs`、`TcpServerService.cs` | `MCCTcpServerChannel` + 真实 SubChannel 管理 | Accept/ClientConnected/Disconnected/定向发送 | 每客户端 OS Thread 默认方案、独立业务队列 Worker/假子通道 |
| `UdpClientChannel.cs`、`UdpServerChannel.cs` | `MCCUdpChannel`（客户端/服务端参数可选） | Endpoint 来源、数据报边界、状态/诊断 | 仅因“Client/Server”创建两套相似线程处理模型 |
| `Protocols/Contracts/CxProtocolSession.cs`、`ProtocolFoundationContracts.cs` | `MPAProtocolDefinition/Binding/Session` 的唯一合同 | 响应关联、CRC/帧校验、Session 生命周期、事务串行 | 双层 Session、Channel 中 Request 与 Protocol Request 混杂、重复 ProviderManager |
| `Protocols/ModbusTcpModule/*` | MPA Codec/Parser + Automation Modbus Driver | MBAP/TID/UnitId/功能码、寄存器读写 | 把 Modbus UnitId 伪装成独立 TCP SubChannel |
| `DeviceVariable/VariableInterfaces/{VariableTable,VariableInfo,VariableManager}.cs` | `MDSVariableTable` 与必要的实例作用域 | 线程安全、Value/Updated/Initial、订阅通知 | static 全局表/后台线程、重复 VariableValue 缓存 |
| `DeviceVariable/DeviceInterfaces/{Point,PointSheet,DriverModuleBase}.cs` | Connector PointTable/PointBinding 与专业 Driver | 地址、点查询、专业设备方法/读写 | 空壳 PointSheet、跨专业全局 DeviceManager、重复 Connector DriverBase |

`ReferTo` 一律只读；先复制目标需要的 C# 代码再**合并、删减、改名并加测试**，最终删除临时 `Porting/` 审查目录；迁移前实际已有的项目成熟行为应有可复现对照，不因新架构损害正确性、吞吐、异常恢复或设备安全。

## 09 ComputeModule——计算模块

### 09.1 正式职责

对一个或多个**已拥有或被合法引用**的输入执行可验证的计算、处理、规则求值及专业算法，输出一个或多个类型化结果。涵盖纯函数、配置化计算、状态/资源型算法、GPU/Native、批处理与异步重建；不另建 Processing/Algorithm/Logic/Script/Control 模块类型。

### 09.2 需求 UM-CP01～CP10

| ID | 能力要求 | 典型场景与行为 | 必须验证 |
|---|---|---|---|
| UM-CP01 | 最短普通调用 | 普通 `Func`、静态/实例函数、业务类可直接单测，选择性导出为能力 | 无强制 Host、Connect、Project 构造 |
| UM-CP02 | 多种计算 | 数学/矩阵/字符串/统计/信号/几何/数据清洗/规则求值 | 数值精度、空值、边界、版本一致 |
| UM-CP03 | 专业算法 | Vision 2D、AI 推理、CT 重建、3D 测量与其他专业算法 | 类型/空间/单位/位深约束明确；不能假数据冒充 |
| UM-CP04 | 输入输出与参数 | 多输入、多输出、可选参数、批量、配置快照、端口发布 | 无错连；计算失败无部分成功输出 |
| UM-CP05 | CPU/GPU/Native | 引用已有 CPU/Native GPU 算法、型号/依赖加载与资源策略 | 位数/ABI、设备选择、显存预算、释放 |
| UM-CP06 | 同步异步/作业 | 快函数直接返回，慢算法 `Task/Job`，可取消/进度/超时 | 取消与任务终态可查，旧结果不写新 Run |
| UM-CP07 | 可变数据安全 | Image/Volume/Tensor、共享只读、修改显式复制、可控缓冲 | 原图不被隐式篡改、Lease 不越寿命 |
| UM-CP08 | 规则与表达式计算 | 条件/判定/阈值/业务计算复用 MOL 表达语义 | 不新建 Graph、循环、分支执行引擎 |
| UM-CP09 | 模型与缓存复用 | AI 模型预热、CT 算法状态、线程安全缓存、配置版本固定 | 模型按声明的共享/隔离策略使用，无全局串扰 |
| UM-CP10 | 性能与可诊断 | 耗时、批量大小、GPU/CPU、复制次数、缓存命中、精度 | 真实结果核对+资源/峰值观察；不以吞吐代替准确率 |

### 09.3 三种计算粒度与工作模型

1. **表达式级**：`Clamp`、比较、字符串格式、四则运算，直接由已有表达式/规则机制或普通函数提供；**不必**建独立 Module 实例。
2. **算法级**：图像滤波、缺陷判定、坐标拟合、多输入输出统计，发布为 ComputeModule 的一个或多个 Capability；参数采用统一配置。
3. **资源级**：AI GPU 模型、FFT 规划器、CT 重建 Context 等拥有资源/异步行为；Provider 管理资源状态，业务计算仍由原专业实现执行。

### 09.4 计算质量与结果合同

- 精度：声明数值类型、舍入与误差容许、像素/毫米/角度单位、坐标变换；纯函数尽量确定性，涉及并行/GPU 非确定性时明确误差与种子。
- 数据：允许流式分块处理，但中间可变数据不能直接无保护共享。输出是类型化结果、受控 DataHandle 或明确失败状态。
- 配置：例如阈值、模型/版本、GPU 选择、迭代次数必须在一次运行中固定；新配置不改变在途 Run。
- 可取消：算法对 CPU 轮询取消或 GPU 作业使用受支持的取消点；请求取消不代表底层 GPU 已停止，也不等于 Output 已安全释放。
- 外部副作用：Compute 默认无外部设备控制；若算法写文件/调用服务，必须声明副作用并优先交给已有文件/Connector/Service Owner，而不是隐蔽在规则内部。

### 09.5 与已实现源码的连接点

现有 `Professional/Vision/CxVisionDataFlow.cs` 使用 `MRKExecutionPlan`、`ctx.Data.Get<T>`、`ctx.Data.Return`，对 8/16 位 RAW 做 CPU 解码、预处理和检查；它是**真实生产 Owner 代码路径**，适合作为首批 Compute 审核/用例，但不能据此宣称 AI、GPU、CT 全部已被统一 Compute 体系覆盖。CT 原生 `cx-cct-x3` 可通过已审核 C ABI/SHM 边界直接复用，必须验证实际依赖与 ABI 版本。

### 09.6 开发者与 Studio 示例

开发者先写 `PixelRule.Evaluate` 并单测，再将端口契约暴露给 MOC；Studio 用户将 `Frame/Threshold/BadLimit` 连接到能力节点，运行后看到 `BadPixels/Accepted` 及计算耗时。若图像类型、位深不匹配，构建阶段失败；若运行期发现资源不足，不发布假的 `Accepted=true`。

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

## 11 ServiceModule——服务模块

### 11.1 正式职责与与 Connector 的区别

服务模块**对消费者持续提供或管理一组能力**，对内可以是长期对象、数据服务或计算服务，对外可以通过 HTTP/IPC/RPC/消息端点暴露。Connector 侧重与系统/设备的**边界适配**，Service 侧重**持续供给、生命周期、并发、服务可用性**。访问远端 MES 的一端通常是 Connector；本地启动并对外提供“查询当前检测结果”的一端是 Service。两者可在同一进程共存但职责分开。

### 11.2 需求 UM-SV01～SV10

| ID | 能力要求 | 业务场景 | 反证/验收 |
|---|---|---|---|
| UM-SV01 | 内部服务 | 进程内数据、计算或业务服务，未必有网络端点 | 无 HTTP 也可运行，不强制 Connector |
| UM-SV02 | 外部接口 | HTTP/IPC/RPC/消息服务接入，契约与鉴权受控 | 类型、协议版本、认证失败可解释 |
| UM-SV03 | 生命周期 | Initialize/Ready/Start/Degraded/Stop/Dispose | 多次启动、异常停止无资源泄漏 |
| UM-SV04 | 请求结果语义 | 请求关联、幂等键、响应、流式结果、异步任务 | 超时不默认重复执行不可逆请求 |
| UM-SV05 | 并发与流控 | 并发上限、队列、限流、背压、速率和公平性 | 压力下不 OOM、不无限排队 |
| UM-SV06 | 依赖与资源 | 可复用 Compute/Connector、模型/GPU/数据库池依赖 | 依赖失效能 Degraded/Rejected，而非伪 Ready |
| UM-SV07 | 部署与启动 | 同进程、独立进程、远端服务，部署版本/配置及启动探测 | 不把独立服务强制复制进 28 项目 DLL |
| UM-SV08 | 健康与诊断 | Readiness/Liveness、请求指标、故障、日志、Trace | “进程存在”不自动视为服务 Ready |
| UM-SV09 | 授权和副作用 | 请求调用者身份、能力级授权、审计、幂等/补偿 | 未授权修改配置/启动 X-Ray 被阻断 |
| UM-SV10 | 优雅停止和恢复 | 停止接新请求、排空/取消在途、恢复未知任务、关闭资源 | 进程崩溃不伪造成功、不重复危险指令 |

### 11.3 三种服务形态

1. **内部服务**：例如图像结果缓存查询、运行指标聚合、规则计算结果提供者；无需 Web Server。
2. **本地独立进程服务**：例如高显存 AI 推理、CT 重建、跨语言 Native 服务；需要 IPC、ABI、进程健康和任务恢复。
3. **对外业务服务**：例如设备结果 API、MES/ERP 回调入口、边缘数据查询；需要协议、安全、版本、审计与流控。

服务接口若需要底层 HTTP/TCP/IPC 通信，**复用已有 Transport/Connector 能力**；它不重建一套 Socket/Protocol。长作业的通用调度、取消、超时复用 MRK；服务本身管理 Ready、依赖、端点、请求生命周期和协议边界。

### 11.4 服务的计算复用示例

场景：检测服务保持 AI 模型加载并提供推理。Compute 公开 `Infer(ImageRef,ModelVersion) → DefectResult`；Service 提供 `Submit/Status/Result` 和限流/实例管理。直接在 C# 中调用 Compute 时不必启动 Service；需要多客户端、远程访问或长期池化时启用 Service。两者**不能**各自存储不一致的模型版本、输出 Schema 或 Trace 身份。

### 11.5 与 Runtime.Host/部署治理的边界

`Runtime.Host` 已有模拟权限、PlanSlot、Journal、Flow 启动和未结案任务保护等源码；不应把 ServiceModule 做成平行通用执行 Host。具体服务可以由 Host 装载，运行治理控制发布/激活/回滚，专业服务/算法保留实际行为。单进程演示 Host 不能推断为已拥有多机编排、跨进程自动恢复、远端高可用或工控安全验收。

### 11.6 双角色使用与典型反证

开发者希望只写实际请求处理与健康/清理所需方法，能够托管服务而不必实现完整 MRK；Studio 用户选择服务实例、Endpoint/接口、鉴权/部署目标、并发与状态面板即可使用其能力。测试必须覆盖：无网络的内部服务、错误鉴权、并发溢出、依赖失效、停止中收到请求、服务崩溃后未知作业、版本不兼容、幂等重复请求。

---
# 第四篇｜与 CxVisual 的融合、模块组合和类型/API 归属

## 12 模块组合、数据流、同步异步与跨专业协作

### 12.1 组合不是第五种模块

“一个 Connector + 两个 Compute + 一个 Service 形成检测功能”只是**已有工程资源的引用与 MOL 逻辑编排**，不是独立 `CompositeModule` 基类。模块内部可以引用其他模块的能力，但可见的业务组合、顺序、并行、超时、分支、子流程、重试、等待应通过现有 MOL/MOC/MRK 机制表达，使 Studio、Run Trace 与 C# 调用一致。

**调用层级**：Module 暴露 Capability → MEM/MOL 保存能力引用及端口映射 → MOC 解析真实 Provider、验证类型/依赖/可用性 → MRK 执行 → MDS 提供运行数据 → Provider 完成专业动作 → Trace 经 SourceMap 返回工程。直接 C# 调用允许省略 MEM/MOL/MRK，但不能绕开外部副作用的专业授权。

### 12.2 五种连接模式

| 模式 | 典型模块关系 | 输入/输出合同 | 组合责任 |
|---|---|---|---|
| 值到值 | Compute → Compute | 类型化结果；失败原子发布 | MDS 端口/数据，MOC 类型，MRK 推进 |
| 请求响应 | Compute → Connector/Service | Correlation、业务结果与超时 | Provider 提供真实交互结果；MRK 控制任务 |
| 持续订阅 | Connector/Simulation → Compute/Service | 事件/流、订阅取消、背压、质量 | 数据流拥有者+已有运行协调 |
| 长作业 | Service → Compute(CT/AI) → Service | JobId、版本、进度、终态、结果定位 | MRK 作业控制；专业执行处理 |
| 仿真替身 | Simulation → Connector Adapter → 原流程 | 同一类型和语义契约，明确虚拟来源 | 发布/专业 Owner 验证目标切换 |

### 12.3 数据契约与共享原则

普通小数据用既有 MDS 类型安全值；Image/Projection/Volume/Tensor 使用专业不可变对象或受控大数据 Handle，数据真正 Owner 决定读写/Lease/Copy/Move。流媒体须携带 `FrameId`、时间戳、源、关联 Run/Trigger、数据质量、格式、尺寸、位深、像素/坐标单位、有效期与订阅策略。`MDSBoundedByteStream` 的能力不能被直接扩展宣称为零拷贝跨进程共享内存。大数据只在实际需要时读取，Studio 默认查看元信息。

### 12.4 依赖与循环

- 工程定义允许**引用**其他资源，但结构/能力依赖图必须被 MOC 验证；组件构造的强依赖环应拒绝或使用显式延迟绑定合同。
- 服务 A 调服务 B、B 反调 A 可以存在业务交互，但若形成无终止条件的隐藏同步调用环，应被防护为限深/超时并诊断。
- Provider 内部短小算法可直接调用普通函数，无需强制为每一行计算创建 MOL 节点。
- 同一物理资源（一个相机/一块运动卡/GPU）由专业 Owner 决定独占、引用计数、可重入和仲裁；MOC 静态校验资源合同，MRK 执行态使用租约管理。

### 12.5 版本固定、热更和 SourceMap

模块能力描述版本、Provider 二进制版本、配置生效版本、MOC 计划版本、运行实例/Job 版本必须可关联。一次 Run 开始后配置快照和能力语义不应在未受控情况下改变；开发者发布新版本不能悄悄替换已执行计划。MOC SourceMap 能定位到模块调用的工程源节点及复用路径，而专业 Provider Trace 可进一步定位设备、模型、阶段和外部命令。

### 12.6 组合反例与验收

1. 采图流有两个消费者，谁有权修改底层数组？必须只读共享或显式复制。
2. 同一模块定义生成两个相机实例，不应共享未授权的 SDK Callback/Socket 缓冲。
3. 真实运动命令超时后，流程不能盲重试并让机器人重复运动。
4. Runtime.Host 进程被杀，服务与异步重建任务不能被伪标记为成功；恢复前状态为未知/待确认。
5. 仿真模块发出的模拟 ACK 不能替换真实设备执行完成证明。
6. 同一 CT 数据块被提前释放时，下游必须收到失效/拒绝，而不是读到另一 Run 的数据。

### 12.7 【V1.1】Protocol 与数据、消息、命令协作的权威流向

| 进入 Protocol 的数据 | Protocol 负责 | 交付的实际 Owner | 成功语义 |
|---|---|---|---|
| 设备变量/寄存器 | 解释地址、编码/解码、读写外部设备、字段映射 | MDS `VariableTable`、数据绑定 | 实际值、时间、质量、来源合法更新；发请求不等于目标已写 |
| MDS Input/Heap/Stack | 按权限解释数据目标、使用合法运行上下文 | MDS Run/Flow/Step 数据 Owner | 当前执行的数据提交完成；其他 Run 不可见 |
| 消息内容 | 解析消息类型/字段、构造发布输入 | 未来统一消息体系 | 接收/发布/投递/业务处理阶段分开 |
| 命令内容 | 识别参数/目标，编码请求，解释响应 ACK | 未来统一命令体系 + Professional Driver + MRK | Accepted、Sent、Ack、PhysicalCompleted、Unknown 分开 |
| 帧/图像/Volume | 解释结构与元数据、选通道或 SHM 路由 | FDT/MDS/专业 Imaging/CT 数据 Owner | 数据句柄/租约有效，不能只返回过期 `IntPtr` |

当未来消息/命令体系尚未拥有正式 API 时，Protocol 可先提供结构化解析结果、待执行意图与明确 `Unsupported`，但**绝对禁止创建一个始终成功的假 IMessageBus/ICommandBus**。日后通过独立 Owner 的正式能力直接集成，不需为成熟的 C# Protocol 再外套兼容 Adapter。

## 13 现有 CxVisual 体系和五大代码的复用、Owner 与依赖

### 13.1 统一 Owner 总表

| Owner/项目 | 在模块体系中负责 | 明确不负责 |
|---|---|---|
| `Foundation.Core`/Infrastructure | 普通值契约、线程/并发、基础日志、资源安全与公共设施 | 持久工程模块树、工控 SDK |
| `Foundation.Data` | 类型安全、Buffer/数据适配、基础配置类型、数据值语义 | MRK Run 数据仓、专业算法 |
| `Foundation.Transport` | 通用字节传输/IPC/基础协议适配支撑（应依当前实现核对） | MES/机器人等专业语义 |
| `Foundation.Imaging/Volume/Numerics` | 纯基础图像、体数据、数学几何能力（按现有归属） | 全专业 CT/AI 业务模型 |
| `Mechanism.Core`（MEM/MOL） | 工程 Resource、Content/引用、唯一持久定义、事务/逻辑组织 | Module 运行态、Provider SDK |
| `Mechanism.Data`（Configuration/MDS） | 模块配置生效、变量及运行值、Heap/Stack/Frame/Binding | 第二套设备运行实例/主工程树 |
| `Mechanism.Connectivity` | 接入/Channel/Protocol/Session 与通用 Connector 机制（M13～M15） | 图像算法、硬实时设备运动 |
| `Mechanism.Orchestration`（MOC） | 能力目录、端口类型校验、资源解析、构建与 SourceMap | 串口 IO、GPU 推理、另套流程真源 |
| `Mechanism.Runtime`（MRK） | Flow/Task/Job 调度、执行、取消、资源仲裁、Trace | 专业 Provider 与实际设备联锁 |
| `Mechanism.Lifecycle` | 通用运行/部署生命周期合同需按 V6.4 归属审核 | 服务专有业务和独立复制 MRK Host |
| `Mechanism.Presentation.Wpf` / Studio | 描述、配置和运行观察的 WPF 投影/使用入口 | 独立模块配置/工程/运行真源 |
| `Professional.Automation` | PLC/Robot/Motion/DAQ 专业行为与授权、部分 Connector/Compute/Simulation | 通用 TCP/协议核心和第二套 MRK |
| `Professional.Vision`、`Professional.CT3D` | 算法/模型/Detector/CT 实际专业实现与 Viewer | 通用模块能力目录/工程规则 |
| `Professional.Inspection` / HMI / Simulation | 检测规则与结果、显示绑定、仿真模型/场景与 Replay | 通用端口真源/第二套数据层 |
| `Applications/Runtime.Host` | 宿主装配、启动、治理与已发布计划运行 | 新的通用服务调度内核 |
| `Tests/TestCenter` | 真实模块能力与跨专业验证、结构化结果和证据 | 替代生产 Owner 或在界面中伪造 PASS |

**架构约束**：保持 Foundation → Mechanism → Professional 的依赖方向（这里指依赖从上层指向下层，Foundation 不依赖 Mechanism/Professional，Mechanism 不反向依赖 Professional）。功能模块体系**不新增第 29 个项目**，先使用已有 28 项目；命名空间与子目录归属在 V6.4 类型表、`.csproj`/依赖 DAG 审查后定案。`Professional.Simulation` 的仿真模型是专业语义，即使 Module 公共契约由 MOC/Core 提供，也不将物理仿真核心搬进基础项目。

### 13.2 与六个既有核心体系的关系

| 体系 | 模块怎样利用 | 不得重建的对象 |
|---|---|---|
| **MEM** | 保存模块引用、持久配置意图、所属专业和工程身份 | 第二个 ModuleProject/ResourceGraph/Undo 栈 |
| **统一配置** | 属性 Schema、存储、生效与版本固定 | `ModuleConfigManager` 独立文件仓/编辑器 |
| **MDS** | 统一数据类型、Input/Local/Output/Heap/Binding/预算 | 独立 ModuleDataHeap/Stack/Variable Center |
| **MOL** | 保存控制/数据/事件逻辑的单一持久定义 | ModuleFlowDefinition 新树 |
| **MOC** | 能力发现、静态验证、编译、Provider 可用性和 SourceMap | 第二套 ModuleCapabilityCatalog/Compiler |
| **MRK** | 调度、执行、取消、并发、资源、Trace、流程实例 | ModuleScheduler/WorkflowEngine/JobRunner |

### 13.3 已有新主仓源码事实（有限范围）

| 路径/对象 | 本次确认的事实 | 不可推断 |
|---|---|---|
| `Mechanism/Orchestration/MOCCapabilityCatalog.cs` | 存在 `MOCCapabilityDescriptor`、`IMOCCapabilityProvider`、`MOCCapabilityCatalog` 和 `RuntimeAvailable` 发现语义 | 不能等同全部模块已注册或实机就绪 |
| `Mechanism/Connectivity/Channels/MCCVirtualChannel.cs` | `IMCCChannel`/`MCCVirtualChannel`、帧/事件状态代码存在 | 没有据此确认所有真实 TCP/Modbus/工业设备支持 |
| `Mechanism/Data/RuntimeData/MDSDataBindings.cs` | 既有 `MDSBindingSource` 包含 Constant/CurrentSlot/NodeOutput/FlowHeap/Variable/EngineeringResource/Expression | 不等于高速零拷贝帧/CT 体数据全部验收 |
| `Professional/Vision/CxVisionDataFlow.cs` | 有 MRK+MDS 端口绑定的 CPU Vision 路径、8/16 bit RAW 类型化数据处理 | 不等于真实 Detector SDK、AI/GPU/CT 已全部统一 |
| `Professional/Simulation` 目录 | 本次目录仅显示工程骨架文件 | 未确认完整虚拟 PLC/Robot/CT 模型 |
| `Applications/Runtime.Host/UnifiedRuntimeHost.cs` | 有本地 Simulation Policy、MRK PlanSlot/Journal/Session 源码 | 不等于跨进程高可用/完整 ServiceModule |
| `DOCS/ProjectCatalog.csv` | 记录 28 项目与 M13～M15、P66～P73 等归属 | 文件的早期“Skeleton”状态不应覆盖最新源码实测 |

### 13.4 五大代码真实复用清单

| 原仓库 | 已核对的代表源资产 | 新模块候选归属 | 复用策略与风险 |
|---|---|---|---|
| [ccxx](https://github.com/oudream/ccxx) | `ccxx/cxchannel*.h/.cpp`、`cxchannel_none.*`、`cxsharememory.*`、线程/Buffer | Connector、Service、基础资源 | 优先保留成熟 C++ 行为，经 ABI/IPC 封装，避免全量 C# 重写；须审查线程、编码、内存释放与单例 |
| [gcl3](https://github.com/oudream/gcl3) | `src/vdi/terminal_manager.cpp`、`protocol_base`、Measure/Worker、`src/protocol` | Connector + 仿真/计算扩展 | 复用 Terminal 配置/点表/规约与 SCADA 行为；不能复制旧工程和全局 Manager |
| [device_communicator](https://github.com/oudream/device_communicator) | `hy/common/communicatorc/communicator.go`、`hy/communicator/` | Connector、Service | 复用 Channel+Protocol+Sheet+服务管理；Go 服务可继续独立运行，经明确定义的 API 对接 |
| [cx-vision-x2](https://github.com/oudream/cx-vision-x2) | `DeviceVariable/DeviceInterfaces/DriverModuleBase.cs`、`Drivers/IRayDetectorDriverModule`、S7/Modbus、`CxECTStudio/Business/*` | Connector、Compute、Service | 保留设备真实行为、采图时序与安全规则；SDK 指针、Halcon、DLL/许可证/WinForms 需逐项适配 |
| [cx-cct-x3](https://github.com/oudream/cx-cct-x3) | `module_api/` 的 CT/Detect ABI、`shm_api/`、`service/cct_shm_*_bridge` | Compute、Service、Connector 数据交接 | 原生计算留在 C++；接口版本、数组布局、坐标、异步 Job 和 SHM 内存所有权必须固定 |

原代码多年实际项目经验是**复用资产来源**，不表示搬入 x3 后完成编译、接口兼容、TestCenter 或现场验收。`ReferTo/` 参考源码也仅是复用候选，不能当成正式实现。此前 `ConnectorModule V1.0` 中约 50 个候选类型只是待查重清单，在此不得直接与新类型相加并批量创建。

### 13.5 关键冲突裁决

1. 若共性 Module Descriptor 与 `MOCCapabilityDescriptor` 重叠：保留 MOC 作为能力事实，最多补一个面向 UI/分类的**只读聚合视图**。
2. 若 Module 配置模型与 MHS/FCM 重叠：只新增该模块实际专有配置 Schema，生效/持久化仍归原有 Owner。
3. 若 Module Workflow 与 MOL 重叠：移除新 Workflow，保留引用已有 MOL Definition 的组合使用说明。
4. 若 Service Runner 与 MRK/Host 重叠：剥离通用任务执行，把服务特有的 Readiness、Endpoint、请求处理交给具体 Service Provider。
5. 若 Simulation Clock 与运行计时冲突：显式定义仿真域时间与 MRK 实际时间的映射，不将两者合并为一个错误抽象。

### 13.6 【V1.1】GCL3、ReferTo 与 F19 的直接复用对照

| 历史/现有类型 | 已核实的真实职责 | CxVisual 的目标落位 | 直接重构取舍 |
|---|---|---|---|
| GCL3 `TerminalBase`, `TerminalManager` | 组合 Terminal/Protocol/Business/Channel，统一创建启动 | ConnectorModule + MEM 引用/实例 | 保留组合思想，不回迁另一工程树与全局终端表 |
| GCL3 `ProtocolBase` | 通道回调、变量观察、WorkerTable、Case、Timer 和 Control | Protocol 七类可选能力 + MDS/消息/命令/F19 | 拆分大基类中的 Owner，不拆 Protocol 为多套体系 |
| GCL3 `WorkerBase/WorkerTableBase` | 外部地址、点表、load/save 与查询 | MCNPointDefinition/Table、已有统一配置 | 保留地址模型、数据映射、批量读写策略 |
| GCL3 `MeasureBase/MeasureManagerTemplate` | 测点身份、值变化、查询、通知 | MDS VariableDefinition/Table | 去除跨工程静态单例管理 |
| GCL3 `CasesBase/SendMessageCases/SendControlCases` | 规约向外提供消息和控制能力 | MOC 公开 Capability + 未来 Command/Message | 不因功能多而建立第二个 Case 注册中心 |
| ReferTo `TcpClientBase/TcpClientChannel` | 独立 Thread 与同步 Read，实际项目成熟行为 | 唯一 `MCCTcpClientChannel` | 保留重连/收发和错误行为，和异步候选融合，废重复线程包装 |
| ReferTo `TcpClientTransportChannel` | 受控异步 Task 接收/发送、Connect/Close | 同上 | 优先比对，合并两代 `IChannel` 动词与资源管理 |
| ReferTo `TcpServerChannel/TcpServerService` | 专用 Accept/每客户端线程、异步接入+业务队列两代 | `MCCTcpServerChannel + SubChannel` | 优化为异步 IO，业务队列用 F19，不复制旧 QueueManager |
| ReferTo `UdpClientChannel/UdpServerChannel` | 专用接收线程，后者携带远端 Endpoint | `MCCUdpChannel` | 保留 Datagram/Peer 来源与动态标识 |
| ReferTo `CxProtocolSession/Provider` | 包装 Channel、请求串行、帧/响应解析与生命周期 | `MPAProtocolDefinition/Binding/Session` | 一套 Session，协议拥有事务/帧状态，Channel 只做 IO |
| ReferTo `VariableInfo/Table/Manager` | 变量值、初始状态、更新时间、全局异步通知 | MDS VariableTable/Scoped Registry | 保留真实值/通知，去静态全局和重复运行表 |
| `FCRWorker`, `FCRDedicatedExecutor` | 有界、按 Key 串行及专用 Thread，已有真实 C# 实现 | Protocol Dispatch 的执行能力 | 不新建通用 ProtocolWorker/ChannelThreadPool |

此表只对照已核查的公开方法/逻辑片段；尚未完成所有 `ReferTo` 子目录/所有 GCL3 方法/生产运行日志的全量功能迁移复测。

## 14 完整类型、接口、枚举及统一 API 归属

> **类型审计原则**：先列“已确认存在”的真实类型，再列“必须表达的逻辑合同”，最后给出“候选新增类型”；**需求有 40+ 条不意味着要创建 40+ 个类**。本版是完整设计归位索引，最终 `.csproj` 与具体签名须和 V6.4/最新代码逐个查重后批准。

### 14.1 已确认真实类型及拟复用关系

| 类/接口/枚举（现有） | 所在 Owner | 模块中用途/复用边界 |
|---|---|---|
| `MOCCapabilityDescriptor` | `Mechanism.Orchestration` | 能力 ID、名称、Category、Version、Ports，**唯一能力目录来源** |
| `IMOCCapabilityProvider` | 同上 | 专业 Provider 以只读描述向 MOC 公开能力 |
| `MOCCapabilityCatalog` | 同上 | 查询/Search/Find 与运行可用性观察，不新建 ModuleCatalog |
| `MOCCapabilityCatalogItem` | 同上 | 概念上的声明与 RuntimeAvailable 结果 |
| `MPCPortDescriptor` | `Mechanism.Core`（实际引用路径） | 公开能力端口信息，禁止影子 Port Schema |
| `IMRKCapabilityResolver` | `Mechanism.Runtime.Execution` | 从能力 ID 解析真实可执行入口 |
| `MRKResolvedCapability` | 同上 | 解析后的执行回调入口 |
| `MRKExecutionPlan` | 同上 | 统一执行计划，禁止新 ModulePlan |
| `MRKExecutionStep` | 同上 | 调用节点及其输入输出的现有执行定义 |
| `MRKExecutionInput` | 同上 | 输入映射/类型校验接入点 |
| `MRKExecutionEngine` | 同上 | 统一流程执行与调度，不另建 ModuleRunner |
| `MDSDataBinding` | `Mechanism.Data.RuntimeData` | 数据来源及类型化绑定 |
| `MDSBindingSource` | 同上 | 常量/节点/Heap/变量/资源/表达式来源 |
| `MDSDataBindingResolver` | 同上 | 统一数据绑定解析 |
| `IMDSExternalBindingResolver` | 同上 | 原有专业/工程外部绑定适配入口 |
| `MDSDataHeap`、`MDSDataStack` | 同上 | 运行数据所有权、堆栈，不另建 ModuleData* |
| `IMDSDataContext` | 同上 | 运行步骤 `Get/Set/Return` 数据使用入口 |
| `FDTDataKind` | `Foundation.Data` | Scalar、Buffer、Image、Volume、Tensor 等类型分类 |
| `FDTDataTypeDescriptor` | 同上 | 类型描述、安全与跨专业校验 |
| `FDTDataAdapterRegistry` | 同上 | 专业类型/复制/不可变/估算适配 |
| `IMCCChannel` | `Mechanism.Connectivity` | 通道通用合同（按需） |
| `MCCVirtualChannel` | 同上 | 有独立收发行为的虚拟字节通道，**并非仿真世界模型** |
| `MCCChannelFrame`、`MCCChannelState` | 同上 | 通道帧观测与通道状态 |
| `MEMEngineering` / `MEMEngineeringSession` | `Mechanism.Core` | 工程资源、事务、保存与引用，不另建 ModuleProject |
| `MEMContent`、`MOLLogicDefinition` / `MOLWiringDefinition` | `Mechanism.Core` | 已有持久逻辑组织与端口/子流程，不另建 ModuleFlow |
| `MOCBuildService`、`MOCEngineeringCompiler` | `Mechanism.Orchestration` | 编译能力引用、类型、依赖与 Trace 来源 |
| `CxVisionDataFlow` | `Professional.Vision` | 首个真实 Compute 候选接入点，已有 CPU 图像数据流程 |

表中部分类型根据此前已查阅源码及当前架构文档确定其位置；**并非每一项均于本次重新打开完整声明**。正式开发时须以固定 SHA 文件和编译引用再次确认。例如 `FDTDataKind` 的选项及 `MPCPortDescriptor` 的完整构造参数必须直接查当前代码，不可据表猜测签名。

### 14.2 逻辑合同与唯一 Owner（不必一一生成同名类）

| 合同 | 唯一 Owner 倾向 | 是否已有对应机制 | 新增判据 |
|---|---|---|---|
| 模块分类（4 种） | MEM Resource/Descriptor 的已有可扩展元数据；能力视图 MOC | 有 Resource/Capability 的分类与元数据模式 | 仅已有 Category 无法稳定表达四种类型且实际使用确需区分时新增枚举 |
| 模块对能力的归属/公开关系 | MEM 引用 + MOC 能力目录 | 已有目录，模块聚合需检查 | 不创建新 Catalog，仅补稳定关联字段/适配器 |
| 模块配置模板 | 原统一配置 Owner | 已有 Schema/生效/版本 | 仅补专业专有字段和验证器 |
| 模块执行特征 | MOC 能力元数据 + MRK 执行合同 | 部分异步/端口/运行支持 | 缺少 SideEffect、Reentrancy、Stream 等真实需求时增量扩展 |
| 模块生命周期 | MRK Host/机制 + 专业 Provider；M.Lifecycle 待归位核对 | 有运行/宿主基本机制 | 仅存在真实持资源场景且当前合同无法描述时增加可选接口 |
| 模块性能/诊断 | FLD/MRK/专业观测 + TestCenter | 有日志/Trace/观察机制 | 仅加结构化模块相关字段/投影 |
| 大数据缓冲/流 | MDS/FDT + 专业 Imaging/CT3D | 有 Buffer/Stream 与专业数据结构 | 真正跨进程/零拷贝需求且旧数据类型不足时补安全 Handle |
| 模块级权限与发布 | 运行治理、MRK Access、专业安全 | 有运行访问和发布策略部分源码 | 能力级副作用特征/目标授权有缺口时扩展 |
| 仿真模型、场景、时钟 | `Professional.Simulation` | 当前仅工程骨架，缺完整实测 | 实现具体模型/Clock/Replay，不建通用 Flow Engine |
| 服务端点与就绪状态 | Provider + Host/运行治理/Connectivity | Host 有阶段性基础 | 真实服务需要才新增 Endpoint/Readiness 语义 |

### 14.3 建议新增的最小候选类型（名称非定案）

**候选仅用于“讨论模型有无缺口”**；任何一项若可由现有类型字段/轻量映射满足，必须删除候选。

| 候选名与类型 | 初步建议归属 | 负责的唯一增量职责 | 与现有对象关系/裁撤条件 |
|---|---|---|---|
| `MFMModuleKind`（Enum） | `Mechanism.Core` 或已存在的 Module Resource Schema Owner | 四种模块分类，序列化兼容 | 若已有 Resource/Capability 分类稳定可用，不创建 |
| `MFMModuleDescriptorView`（Class） | `Mechanism.Orchestration` 只读投影 | 聚合模块身份、公开 Capability 与可用性供 Studio | 无权修改 `MOCCapabilityCatalog`；若可直接投影则不建 |
| `MFMModuleCapabilityLink`（Class/记录） | MEM 引用 Owner | 模块 ResourceRef 与外部能力 ID 的稳定关系 | 复用 MEM Reference 后可能不需要 |
| `MFMExecutionTraits`（Class/DTO） | MOC 描述/运行合同 Owner | 纯度、幂等、串行、SideEffect、Async/Stream、目标 | 若现有 Descriptor 可扩展字段，取消独立类 |
| `IMFMLifecycleContract`（Interface，可选） | Host/Lifecycle 需核对 | 仅资源型实例 Init/Ready/Stop/Dispose 可选合同 | 用已有生命周期/IDisposable/专业接口足够则不建 |
| `MFMModuleInstanceStatus`（Class/只读） | Runtime/专业 Owner | 汇总实例健康、绑定配置版本和关联身份 | 若现有 Runtime 状态模型可复用则不建 |
| `MFMModuleObservationView`（Class/只读视图） | 诊断/Studio/TestCenter 适配层 | 将已有 Observation 与模块分类关联 | 不创第二 Observation 仓 |
| `MFMModuleCompatibilityIssue`（Class/DTO） | MOC Validation Owner | 模块版本/端口/依赖不兼容时标准定位 | 复用现有 ValidationIssue 即删除 |
| `ICPSimulationModel`（Interface，可选） | `Professional.Simulation` | 虚拟实体状态与行为推进，非流程调度 | 需首个模型验证后再冻结 |
| `CPSimulationScenario`（Class/配置实体） | `Professional.Simulation` + MEM 引用 | 场景参数/关联模型/初态的专业表达 | 使用已有 MEM Resource；不建第二项目文件格式 |
| `CPSimulationClock`（Class/服务） | `Professional.Simulation` | 仿真域的 Pause/Step/Scale/Time | 必须桥接 MRK/墙钟，不取代现有调度 |
| `CPSReplaySource`（Class/Provider） | `Professional.Simulation` | 带版本、时间和 RNG 的可重现数据源 | 若直接由 Adapter 实现，独立类可删 |
| `ISVServiceEndpoint`（Interface，可选） | 宿主/服务机制经归位核对 | 声明服务端点/就绪与关闭行为 | 可通过已有 Host + Connector 合同实现则不建 |
| `SVServiceHealthSnapshot`（Class/DTO） | Runtime/诊断 | Readiness/Degraded/请求统计专有状态 | 复用现有 HealthSnapshot 优先 |
| `IMCNConnectorProvider`（Interface，可选，须查重） | `Mechanism.Connectivity` | 在 Connector 规格已有 Provider 机制上统一四类模块的对外能力 | Connector V1.0 既有候选同职责则合并，不新增第二个 Provider |

以上 **15 项为“最大待审候选”，非首轮建议创建数量**。首轮应只围绕**分类元数据、能力关联、可选生命周期**这三个缺口进行查重和最小实现；专业仿真/服务在各自实际案例中逐个产生类。`ConnectorModule` 细分的原 50 项候选仍由其专项文档独立审议，同一语义不能在两个清单中重复落地。

### 14.4 直接禁止新增的重复类型

`ModuleProject`、`ModuleResourceGraph`、`ModuleDataHeap`、`ModuleDataStack`、`ModuleConfigManager`、`ModuleCapabilityCatalog`、`ModulePortSchemaRegistry`、`ModuleWorkflowEngine`、`ModuleScheduler`、`ModulePlanCompiler`、`ModuleUndoManager`、`ModuleTestRunner`、`ModuleGlobalManager`。如遇项目确需它们的某个局部功能，应提交“旧 Owner 为什么不能满足”的可证据化差距审计，否则不批准。

### 14.5 API 最小使用层级与命名原则

- **L0 纯函数**：普通 C#、Native 或算法库调用，不依赖 CxVisual Module。
- **L1 能力发布**：使用现有 `MOCCapabilityDescriptor/IMOCCapabilityProvider`，若要便捷语法通过现有 Provider 的直接方法或扩展方法表达，不创建第二能力真源。
- **L2 工程使用**：MEM ResourceRef/Content/MOL 声明模块调用、参数、连线；MOC 编译至 MRK。
- **L3 运行管理**：只有有状态 Provider/Service/Simulation 要暴露生命周期与专有观察；资源/任务由既有 MRK/MDS 负责。

建议实际新增命名遵守原 Owner 前缀，如 MOC、MRK、MDS、MEM、MCC 等；`MFM`/`CPS`/`SV` 只是本稿帮助分类的**候选标签**，不构成既定项目前缀。**不要为统一模块体系开新 `.csproj`。**

---
### 14.6 【V1.1】全类审查方式、Owner 与状态标记

本节完整吸收 Channel/Protocol/点表/变量/Connector 的类、接口、枚举讨论。**这些是能力/职责全集，不是要一次建立的类清单**。`【实】`当前 x3 实际源码；`【迁】`ReferTo 的成熟 C# 源码需复制、改名、原位重构并回归；`【候】`缺口设计候选；`【复】`直接复用既有 Owner；`【预】`消息/命令体系预留，暂不创建。复合表中“主要方法”是 API 目标，不等于现有 C# 已含该签名。每个候选冻结前核对 V4.9/V2.01/V6.4 及具体 `.csproj`；有重复职责应**合并或裁撤**。

#### A. Module / Capability 公共对象

| 类型 / 类型类别 | Owner | 主要属性、方法或事件（目标） | 定位 / 状态 |
|---|---|---|---|
| `MFMModuleKind`（Enum） | Mechanism.Core.Modules | `Connector, Compute, Simulation, Service` | 【实】四类唯一分类 |
| `MFMModuleDescriptor`（Class） | Mechanism.Core.Modules | `Id, Name, Kind, Version, CapabilityIds` | 【实】模块身份及能力引用；不复制 Capability 描述 |
| `IMFMModule`（Interface） | Mechanism.Core.Modules | `Descriptor` | 【实】仅有状态/组合需要实现，普通函数无须继承 |
| `MFMModuleDefinition`（Class） | MEM 工程定义 | `ModuleId,TypeId,ConfigRef,ComponentRefs,Validate()` | 【候】先核查 MEM Resource 可否直接承载 |
| `MFMModuleBinding`（Class） | MEM 资源引用 | `ModuleRef,InstancePath,CapabilityRefs` | 【候】优先用已有 ResourceRef |
| `MFMModuleInstance`（Class） | 相关 Professional / Host | `InstanceId,State,Dispose()` | 【候】不为无状态函数实例化 |
| `IMFMModuleProvider`（Interface） | MOC/专业 Owner | `Describe(),Create(options)` | 【候】优先复用 `IMOCCapabilityProvider` |
| `MOCCapabilityDescriptor`（Class） | Mechanism.Orchestration | `Id,Name,Category,Ports,Version` | 【复】唯一能力元数据 |
| `IMOCCapabilityProvider`（Interface） | Mechanism.Orchestration | 描述与公开已注册 Capability | 【复】不设 ModuleCapabilityCatalog |
| `MOCCapabilityCatalog`（Class） | Mechanism.Orchestration | `Find(id),Search(query,limit)` | 【复】唯一能力目录 |

#### B. Channel 定义、I/O 与诊断

| 类型 / 类型类别 | Owner | 主要方法 / 属性（目标） | 定位 / 状态 |
|---|---|---|---|
| `IMCCChannel`（Interface） | Connectivity.Channels | `Id,State,Open/Close,Send/ReadFrames`（现有）；目标补异步生命周期 | 【实】现有合同待原位统一；勿建第二版 `IChannel` |
| `MCCChannelProfile`（Class） | Connectivity.Channels | `Id,Kind,Address,Port,Settings`（现有）；目标 `TypeId,Options,Validate` | 【实】配置骨架待类型化 |
| `MCCChannelTypeDescriptor`（Class） | Connectivity.Channels | `TypeId,Capabilities,Topology,ConfigSchema` | 【候】用于 Studio/TestCenter 类型选择 |
| `MCCChannelOptions`（Class） | Connectivity.Channels | `Timeout,IoMode,BufferPolicy,ReconnectPolicy,Validate()` | 【候】可与类型 Schema 合并 |
| `MCCChannelEndpoint`（Class） | Connectivity.Channels | `Scheme,Host,Port,Path,ToString()` | 【迁】ReferTo `CxChannelEndpoint` |
| `MCCChannelHealth`（Class） | Connectivity.Channels | `State,LastError,ChangedUtc,IsHealthy()` | 【迁】ReferTo Health |
| `MCCChannelDiagnostics`（Class） | Connectivity.Channels | `GetSnapshot(),TraceSend(),TraceReceive(),TraceError()` | 【迁】ReferTo 收发统计 |
| `MCCBufferPolicy`（Class） | Connectivity.Channels | `CapacityItems,CapacityBytes,OverflowPolicy,Validate()` | 【迁/候】ReferTo 配置 + F19 背压避免重复 |
| `MCCReconnectPolicy`（Class） | Connectivity.Channels | `Delay,MaxAttempts,Backoff,ShouldRetry(n)` | 【迁】保留成熟重连规则 |
| `MCCChannelFrame`（Class） | Connectivity.Channels | `ChannelId,SubChannelId?,Generation?,Bytes,Timestamp,Correlation` | 【实】已有帧，目标扩展身份 |
| `IMCCChannelFactory`（Interface） | Connectivity.Channels | `CanCreate(type),Create(profile)` | 【候】仅工厂的具体能力有价值时 |
| `MCCChannelFactory`（Class） | Connectivity.Channels | `Create(profile),Validate(profile)` | 【迁】合并旧工厂，不创建第二注册中心 |
| `IMCCByteStream`（Interface） | Connectivity.Channels.IO | `ReadAsync(buffer,offset,count,ct),WriteAsync(...)` | 【候】TCP、Serial、Pipe |
| `IMCCDatagramIO`（Interface） | Connectivity.Channels.IO | `SendToAsync(endpoint,data,ct),ReceiveFromAsync(ct)` | 【候】UDP |
| `IMCCMessageIO`（Interface） | Connectivity.Channels.IO | `PublishAsync(topic,message,ct),SubscribeAsync(...)` | 【候】仅消息型传输真正需要时 |
| `IMCCRequestResponseIO`（Interface） | Connectivity.Channels.IO | `RequestAsync(request,ct)` | 【候】仅 HTTP/RPC/SDK 自带事务语义，TCP 不强制实现 |
| `IMCCSharedBufferIO`（Interface） | Connectivity.Channels.IO | `AcquireAsync(handle,ct),PublishAsync(handle,ct),Release()` | 【候】MDS/CT 专业租约 Owner 优先 |
| `IMCCChannelListener`（Interface） | Connectivity.Channels.IO | `StartListeningAsync(ct),AcceptAsync(ct),StopListeningAsync(ct)` | 【候】TCP/WebSocket Server |
| `IMCCChannelObservable`（Interface） | Connectivity.Channels.IO | `Subscribe(handler),Unsubscribe(id),StateChanged` | 【候】避免独立事件总线 |
| `IMCCChannelDiagnostics`（Interface） | Connectivity.Channels | `GetHealth(),GetDiagnostics()` | 【候】仅跨类型调用要求时 |

#### C. Channel 子通道与具体实现

| 类型 / 类型类别 | Owner | 主要方法 / 属性（目标） | 定位 / 状态 |
|---|---|---|---|
| `IMCCSubChannel`（Interface） | Connectivity.Channels.SubChannels | `Id,ParentChannelId,Generation,Endpoint,State` | 【候】重点，子通道只归 Channel 管理 |
| `MCCSubChannel`（Class） | 同上 | `CloseAsync(ct),GetHealth()` | 【候】可合并进具体 TcpClientConnection |
| `MCCSubChannelAddress`（Class/Struct） | 同上 | `ChannelId,SubChannelId?,Generation?` | 【候】防错误定向、旧连接误用 |
| `IMCCSubChannelManager`（Interface） | 同上 | `Find(id),List(),CloseAsync(id,ct)` | 【候】复杂 MultiPeer Channel 按需 |
| `MCCSubChannelCollection`（Class） | 同上 | `Add(),Remove(),Find(),Snapshot()` | 【候】仅属于父通道，不是全局注册中心 |
| `MCCSubChannelProfile`（Class） | 同上 | `Selector,AllowedPeers,Capacity,Validate()` | 【候】配置型子通道规则，动态端点不写 MEM |
| `MCCSubChannelEvent`（Class） | 同上 | `ChannelId,SubChannelId,Generation,Kind,Timestamp` | 【候】创建/断开/故障观测 |
| `MCCSubChannelDiagnostics`（Class） | 同上 | `RxBytes,TxBytes,LastError,LastActive` | 【候】可合并到 Channel Health 快照 |
| `IMCCSubChannelSelector`（Interface） | 同上 | `Select(context),Validate()` | 【候】按收发身份选择路径 |
| `MCCChannelReceiveContext`（Class/Struct） | Connectivity.Channels | `Address,Timestamp,Payload,CorrelationId` | 【候】带来源和 BufferLease 的安全交接 |
| `MCCTcpClientChannel`（Class） | Connectivity.Channels | `OpenAsync(),ReadAsync(),WriteAsync(),CloseAsync()` | 【迁】ReferTo 三份 TCP Client 收敛成唯一正式实现 |
| `MCCTcpServerChannel`（Class） | Connectivity.Channels | `StartListeningAsync(),AcceptAsync(),SendToAsync(),StopAsync()` | 【迁】ReferTo Server 与 Service 合并，非每连接一线程 |
| `MCCTcpClientSubChannel`（Class） | Connectivity.Channels | `ReadAsync(),WriteAsync(),CloseAsync()` | 【候】只在 TCP Server 的客户端子连接需要时 |
| `MCCUdpChannel`（Class） | Connectivity.Channels | `SendToAsync(),ReceiveFromAsync()` | 【迁】ReferTo UDP Client/Server 去重复 |
| `MCCSerialChannel`（Class） | Connectivity.Channels | `OpenAsync(),ReadAsync(),WriteAsync()` | 【迁/候】具体接口以 ReferTo 现有实现审核为准 |
| `MCCCanChannel`（Class） | Professional.Automation / Connectivity 扩展 | `SendFrameAsync(),ReceiveFrameAsync()` | 【候】必须有真硬件 Provider 才落地 |
| `MCCUsbChannel`（Class） | 同上 | `OpenAsync(),ReadAsync(),WriteAsync()` | 【候】不强迫 USB 设备使用字节流 |
| `MCCNamedPipeChannel`（Class） | Connectivity.Channels | `OpenAsync(),ReadAsync(),WriteAsync()` | 【候】IPC |
| `MCCSharedMemoryChannel`（Class） | Connectivity + 数据 Owner | `Open(),Acquire(),Publish(),Release()` | 【候】须与 CT/MDS SHM 统一资源所有权 |
| `MCCWebSocketChannel`（Class） | Connectivity.Channels | `ConnectAsync(),SendAsync(),ReceiveAsync()` | 【候】按实际接口决定 |
| `MCCMqttChannel`（Class） | Connectivity.Channels | `PublishAsync(),SubscribeAsync()` | 【候】可直接由 SDK Provider 提供，不必强制存在 Channel |
| `MCCFileChannel`（Class） | Connectivity 或专业 Owner | `Open(),Read(),Write(),Seek()` | 【候】离线场景按需 |
| `MCCReplayChannel`（Class） | Simulation/Connectivity | `Load(),Start(),Pause(),Step(),Seek()` | 【候】Replay 时间归 Simulation，不另建 Worker |
| `MCCVirtualChannel`（Class） | Connectivity.Channels | `CreatePair(),Open(),Send(),Close(),ReadFrames()` | 【实】现有虚拟点对点，业务帧解析边界需审查 |
| `MCCLoopbackChannel`（Class） | Connectivity.Channels | `Send(),Receive(),Reset()` | 【候】测试专用 |

#### D. Protocol 基础、定义、配置、会话

| 类型 / 类型类别 | Owner | 主要方法 / 属性（目标） | 定位 / 状态 |
|---|---|---|---|
| `IMPAProtocol`（Interface） | Connectivity.Protocols | `Id,Descriptor,Capabilities` | 【候】与现有 `IMPAProtocolDefinition` 二选一/合并 |
| `IMPAProtocolDefinition`（Interface） | Connectivity.Protocols | `Id,RequiredChannelKind,Codec`（现有） | 【实】必须按七大能力扩展并避免超大接口 |
| `MPAProtocolDescriptor`（Class） | Connectivity.Protocols | `Id,Name,Version,Capabilities,SupportedIO` | 【迁】ReferTo `CxProtocolDescriptor` |
| `MPAProtocolOptions`（Class） | Connectivity.Protocols.Configuration | `Parameters,Timeout,RetryPolicy,Validate()` | 【候】复用统一配置 Schema |
| `MPAProtocolBinding`（Class） | Connectivity.Protocols | `ProtocolRef,ChannelRef,SubChannelSelector,Validate()` | 【迁】ReferTo `CxProtocolBinding`；与 `MCNProtocolBinding` 合并 |
| `IMPAProtocolSession`（Interface） | Connectivity.Protocols.Sessions | `Id,State,StartAsync(),StopAsync()` | 【候】仅需要状态的规约 |
| `MPAProtocolSession`（Class） | 同上 | `ProcessAsync(),HandleReceivedAsync(),Dispose()` | 【迁】合并 ReferTo 两代 Session |
| `MPAProtocolContext`（Class） | Connectivity.Protocols | `BindingId,ChannelAddress,ProtocolAddress,CorrelationId,DataContext` | 【候】一次处理上下文，不是第二数据堆栈 |
| `MPAProtocolSessionContext`（Class） | 同上 | `SessionId,ChannelAddress,State` | 【候】若 Context 可承载则不建 |
| `MPAProtocolTransaction`（Class） | Connectivity.Protocols.Sessions | `RequestId,CorrelationId,Deadline,Complete(),Fail()` | 【候】请求响应事务 |
| `MPAProtocolTransactionManager`（Class） | 同上 | `Register(),MatchResponse(),Cancel(),Expire()` | 【候】复杂规约专用；不建全局管理器 |
| `IMPAProtocolSessionResolver`（Interface） | 同上 | `Resolve(binding,address),Release(sessionId)` | 【候】Peer/Binding 到 Session 的定位 |
| `MPAProtocolSessionCollection`（Class） | 同上 | `Find(),Add(),Remove(),Clear()` | 【候】复杂 Server 可内置集合 |
| `MPAProtocolPeerState`（Class） | 同上 | `Address,LastSeen,Sequence,State` | 【候】有状态 Peer |
| `MPAProtocolReceiveContext`（Class） | Connectivity.Protocols | `Address,Bytes,Timestamp,CorrelationId` | 【候】优先合并为统一 Context |
| `MPAProtocolSendContext`（Class） | Connectivity.Protocols | `Route,ProtocolAddress,CorrelationId` | 【候】优先合并为统一 Context |
| `MPAProtocolFactory`（Class） | Connectivity.Protocols | `Create(definition,context)` | 【迁】ReferTo 工厂与 Provider 合并 |
| `IMPAProtocolProvider`（Interface） | Connectivity.Protocols | `Describe(),CreateSession()` | 【候】不要复制一份能力目录/MOC Provider |

#### E. Protocol 七大能力接口

| 类型 / 类型类别 | Owner | 主要方法（目标） | 功能和裁决 |
|---|---|---|---|
| `IMPAProtocolCodec`（Interface） | Connectivity.Protocols | `Encode(payload),Decode(frame)` | 【实】字节编解码仅为 P1 子能力 |
| `IMPAFrameParser`（Interface） | Connectivity.Protocols | `PushBytes(),TryReadFrame(),Reset()` | 【迁】ReferTo FrameParser 直接整理，状态按流隔离 |
| `IMPADataInterpreter`（Interface） | Connectivity.Protocols | `InterpretAsync(input,context,ct)` | 【候】P1 规约语义解释 |
| `IMPADataFormatter`（Interface） | Connectivity.Protocols | `FormatAsync(data,context,ct)` | 【候】P1 专业格式化可并 Codec |
| `IMPADataValidator`（Interface） | Connectivity.Protocols | `Validate(data,context)` | 【候】P1 Schema/字段/校验 |
| `IMPAChecksum`（Interface） | Connectivity.Protocols | `Calculate(bytes),Verify(bytes,checksum)` | 【候】只在独立校验算法有复用价值时 |
| `IMPAProtocolReader`（Interface） | Connectivity.Protocols | `ReadAsync(address,context,ct)` | 【候】P2 远端/规约数据读 |
| `IMPAProtocolWriter`（Interface） | Connectivity.Protocols | `WriteAsync(address,value,context,ct)` | 【候】P2 远端/规约数据写，不能等同 MDS Set |
| `IMPAProtocolDataHandler`（Interface） | Connectivity.Protocols | `ProcessInboundAsync(...),ProcessOutboundAsync(...)` | 【候】P2 非必备聚合处理接口 |
| `IMPAProtocolDataMapper`（Interface） | Connectivity.Protocols | `MapInbound(),MapOutbound(),ValidateMapping()` | 【候】P6 尽量复用 MDS DataBinding/FDT 转换 |
| `IMPAProtocolDataSubscriber`（Interface） | Connectivity.Protocols | `SubscribeAsync(),UnsubscribeAsync()` | 【候】P4 规约订阅，不是新消息中心 |
| `IMPAProtocolSender`（Interface） | Connectivity.Protocols | `SendAsync(data,context,ct)` | 【候】P3 规约编码后的定向发送 |
| `IMPAProtocolReceiver`（Interface） | Connectivity.Protocols | `ReceiveAsync(),HandleReceivedAsync()` | 【候】P3 数据接收，消费端不得二次读取 Socket |
| `IMPAProtocolRequester`（Interface） | Connectivity.Protocols | `RequestAsync(request,context,ct)` | 【候】P4 请求及响应关联 |
| `IMPAProtocolResponder`（Interface） | Connectivity.Protocols | `RespondAsync(response,context,ct)` | 【候】P3 回复给原 SubChannel/Generation |
| `IMPAProtocolEventHandler`（Interface） | Connectivity.Protocols | `HandleEventAsync(event,context,ct)` | 【候】P4 规约事件转交统一消息机制 |
| `IMPAProtocolStreamHandler`（Interface） | Connectivity.Protocols | `HandleStreamAsync(stream,context,ct)` | 【候】P4 连续数据处理 |
| `IMPAProtocolRouter`（Interface） | Connectivity.Protocols.Routing | `ResolveRoute(),ValidateRoute()` | 【候】P5 规约内选择真实 ChannelAddress |
| `IMPAProtocolChannelBinder`（Interface） | Connectivity.Protocols | `Bind(),Unbind(),ValidateBinding()` | 【候】P5 配置/运行边界，不再包旧 Channel Adapter |
| `IMPAProtocolTransactionMatcher`（Interface） | Connectivity.Protocols.Sessions | `RegisterRequest(),MatchResponse()` | 【候】P5 响应按 TID/Peer/会话匹配 |
| `IMPAProtocolAddressResolver`（Interface） | Connectivity.Protocols.Routing | `ResolveAddress(),ValidateAddress()` | 【候】P5 UnitId/地址解析 ≠ SubChannel |
| `IMPAProtocolLifecycle`（Interface） | Connectivity.Protocols | `StartAsync(),StopAsync()` | 【候】P7 可选有状态规约 |
| `IMPAProtocolHealthProvider`（Interface） | Connectivity.Protocols | `GetHealth(),GetDiagnostics()` | 【候】P7 健康与诊断 |
| `MPAProtocolDiagnostics`（Class） | Connectivity.Protocols.Diagnostics | `RecordRequest(),RecordResponse(),RecordError(),GetSnapshot()` | 【迁】P7 ReferTo 已有计数和错误 |
| `MPAProtocolError`（Class） | Connectivity.Protocols.Diagnostics | `Code,Category,Message,Retryable` | 【迁】P7 错误模型，严禁未知完成盲重试 |
| `MPAProtocolRetryPolicy`（Class） | Connectivity.Protocols | `CanRetry(operation,error)` | 【候】P7 优先使用 MRK/治理策略共同裁决 |
| `MPAProtocolExecutionPolicy`（Class） | Connectivity.Protocols | `Timeout,Ordering,Concurrency` | 【候】P7 合并到 DispatchOptions |

#### F. Protocol 消息、路由、结果与执行参数

| 类型 / 类型类别 | Owner | 主要方法/属性（目标） | 定位/状态 |
|---|---|---|---|
| `MPAProtocolFrame`（Class） | Connectivity.Protocols | `RawBytes,Timestamp,Metadata,ValidateChecksum()` | 【迁】ReferTo `CxProtocolFrame` |
| `MPAProtocolMessage`（Class） | Connectivity.Protocols | `MessageType,Fields,GetField(),SetField()` | 【迁】规约内部消息，不替代未来 MessageEnvelope |
| `MPAProtocolData`（Class） | FDT/MDS | `DataType,Value,Metadata` | 【候】若 FDT 已有值类型，则**不得新增** |
| `MPAProtocolAddress`（Class） | Connectivity.Protocols | `Station,Area,Offset,Parameters` | 【候】仅规约寻址，非物理子通道 |
| `MPAProtocolRoute`（Class） | Connectivity.Protocols.Routing | `ChannelAddress,ProtocolAddress,BindingId` | 【候】一次交互的目标 |
| `MPAProtocolRouteRule`（Class） | Connectivity.Protocols.Routing | `Match(),Resolve()` | 【候】高级路由，简单场景无此类 |
| `MPAProtocolOperationRequest`（Class） | Connectivity.Protocols | `OperationId,Kind,Target,Data,CorrelationId` | 【候】尽量重用现有调用/命令合同 |
| `MPAProtocolOperationResult`（Class） | Connectivity.Protocols | `Success,Data,Error,Stage` | 【候】发送/确认/完成阶段不得合并 |
| `MPAProtocolDataTarget`（Class） | MDS/Connectivity | `SourceKind,TargetRef,ValueType` | 【候】已有 `MDSDataBinding` 优先 |
| `MPAProtocolActionDefinition`（Class） | MOC | `ActionId,OperationKind,InputSchema,OutputSchema` | 【候】优先用 `MOCCapabilityDescriptor` |
| `MPAProtocolSubscription`（Class） | Connectivity.Protocols | `SubscriptionId,Route,StopAsync()` | 【候】有持续订阅的协议才需要 |
| `MPAProtocolEvent`（Class） | Connectivity.Protocols | `EventType,Data,Context` | 【候】未来交给统一消息体系 |
| `MPAProtocolDispatchOptions`（Class） | Connectivity.Protocols + 现有 F19 | `ProcessingMode,QueueCapacity,ByteBudget,Concurrency,OrderingKey,ExecutionGroupRef` | 【候】**配置型**，不得持有第二调度器 |
| `MPAProtocolDispatchContext`（Class） | Connectivity.Protocols | `ChannelAddress,SessionId,CorrelationId,Cancellation` | 【候】可并入 `MPAProtocolContext` |
| `MPAProtocolSessionScope`（Enum） | Connectivity.Protocols | `Channel,SubChannel,ProtocolAddress,Binding` | 【候】按实际 Parser/事务隔离粒度 |

#### G. PointTable / Variable / MDS

| 类型 / 类型类别 | Owner | 主要方法/属性（目标） | 状态与防重 |
|---|---|---|---|
| `MCNPointDefinition`（Class） | Connectivity.Connectors | `VariableName,Address,ValueType,CanRead,CanWrite` | 【实】已有，进一步补 PointId/规约地址/质量绑定 |
| `MCNPointTable`（Class） | Connectivity.Connectors | `Add(),Remove(),Find(),Query(),Validate()` | 【迁】ReferTo PointSheet；真正需要索引/批量采集才独立建表 |
| `IMCNPointTable`（Interface） | Connectivity.Connectors | `GetPoint(),FindPoints()` | 【候】一个具体类足够时删接口 |
| `MCNPointAddress`（Class） | Connectivity.Connectors | `Area,Station,Offset,Length` | 【候】专业字段可用 `MPAProtocolAddress` |
| `MCNPointBinding`（Class） | Connectivity.Connectors | `PointRef,DataBinding,Direction` | 【候】使用已有 MDS 来源定义 |
| `MCNPointAccessPolicy`（Class） | Connectivity.Connectors | `CanRead,CanWrite,CanControl,Validate()` | 【候】可并入 PointDefinition |
| `MCNPointConversion`（Class） | Connectivity.Connectors | `Scale,Offset,DataType,Convert()` | 【候】优先 FDT/Compute 已有转换 |
| `MCNPointGroup`（Class） | Connectivity.Connectors | `AddPoint(),RemovePoint(),Find()` | 【候】按读周期/设备组 |
| `MCNPointReadPlan`（Class） | Connectivity.Connectors/Professional.Automation | `Points,Interval,Build(),Validate()` | 【迁/候】成熟 Modbus 批量读取优化 |
| `MCNPointWritePlan`（Class） | 同上 | `Points,Build(),Validate()` | 【候】必须核对设备写命令顺序安全 |
| `MCNPointUpdate`（Class） | Connectivity / MDS | `PointId,Value,Timestamp,Quality` | 【候】优先复用 MDS Sample |
| `IMDSVariableTable`（Interface） | Mechanism.Data.Variables | `Get<T>(),TryGet<T>(),Update<T>(),VariableChanged` | 【实】接口骨架 |
| `MDSVariableTable`（Class） | Mechanism.Data.Variables | `Add(),Remove(),Get(),Update(),Find(),Subscribe()` | 【迁】ReferTo 成熟线程安全变量表 |
| `MDSVariableDefinition`（Class） | Mechanism.Data.Variables | `Id,Name,Type,Unit,Access` | 【候】核查 Foundation/Data/DataSpace |
| `MDSVariableInfo<T>`（Class） | Mechanism.Data.Variables | `Value,HasInitialValue,LastUpdated` | 【迁】ReferTo VariableInfo |
| `MDSVariableSample<T>`（Class） | Mechanism.Data.Variables | `Value,UpdatedUtc,HasInitialValue` | 【实】骨架，质量/来源待扩展 |
| `MDSVariableScope`（Class/Enum） | Mechanism.Data | `Project,Module,Device,Session` | 【候】依已有 DataSpace 规则定案 |
| `MDSVariableSubscription`（Class） | Mechanism.Data | `SubscriptionId,Stop()` | 【候】可能并入已有通知机制 |
| `MDSVariableQuery`（Class） | Mechanism.Data | `NamePattern,Tags,Type` | 【候】按需要索引查询 |
| `MDSVariableChange`（Class） | Mechanism.Data | `OldValue,NewValue,Timestamp,Source` | 【候】现有数据事件合并优先 |
| `MDSVariableQuality`（Enum） | Mechanism.Data | `Good,Bad,Uncertain,Stale` | 【候】查重，不建第二质量码体系 |
| `MDSVariableRegistry`（Class） | Mechanism.Data | `FindTable(),RegisterTable(),RemoveTable()` | 【迁】清除旧静态全局 Manager，作用域归工程/Host |
| `MDSDataBinding`（Class） | Mechanism.Data.RuntimeData | `Source,ValueType,SourceName` | 【实/复】绑定唯一事实 |
| `IMDSDataContext`（Interface） | Mechanism.Data.RuntimeData | `Get<T>(),Set<T>(),Return<T>(),HeapGet<T>(),HeapSet<T>()` | 【复】Run/Flow/Step 数据 Owner |
| `MDSDataBindingResolver`（Class） | Mechanism.Data.RuntimeData | `Resolve<T>()` | 【实/复】不得二次创建 DataBinding 管理器 |
| `IMDSExternalBindingResolver`（Interface） | Mechanism.Data.RuntimeData | `Resolve()` | 【实/复】专业/工程数据来源协作 |

#### H. Connector / Driver / 装配与配置

| 类型 / 类型类别 | Owner | 主要方法/属性（目标） | 状态/建议 |
|---|---|---|---|
| `IMCNConnectorModule`（Interface） | Connectivity.Connectors | `Channels,Protocols,Points`（现有） | 【实】运行视图须与 ModuleDefinition/Binding 区分 |
| `MCNConnectorDefinition`（Class） | MEM+Connectivity | `ModuleId,ChannelRefs,ProtocolBindings,PointRefs` | 【候】先查 MEM 工程资源容器 |
| `MCNConnectorInstance`（Class） | Connectivity.Connectors | `StartAsync(),StopAsync(),GetState()` | 【候】仅资源型接入需要 |
| `MCNConnectorContext`（Class） | Connectivity.Connectors | `ModuleId,InstanceId,DataContext,Resources` | 【候】复用 MRK/MDS 运行身份 |
| `MCNConnectorAssembler`（Class） | Connectivity.Connectors | `Validate(),Create(),Bind()` | 【候】最小装配；不是第二 Runner |
| `MCNChannelBinding`（Class） | Connectivity.Connectors | `ChannelRef,Ownership,Options` | 【候】优先用 Module 组件引用 |
| `MCNProtocolBinding`（Class） | Connectivity.Connectors | `ProtocolRef,ChannelRef,SubChannelSelector` | 【候】**必须与 MPAProtocolBinding 合并** |
| `MCNDataMapping`（Class） | Connectivity/MDS | `Source,Target,Direction,Transform` | 【候】优先 MDS Binding |
| `MCNConnectorHealth`（Class） | Connectivity.Connectors | `State,Components,LastError` | 【候】优先诊断聚合 |
| `MCNConnectorDiagnostics`（Class） | Connectivity.Connectors | `GetChannelStats(),GetProtocolStats(),GetPointStats()` | 【候】只读投影，不建独立统计仓 |
| `IMCNConnectorProvider`（Interface） | Connectivity/Professional | `Describe(),Create()` | 【候】复用 MOC 能力 Provider |
| `IMCNDeviceDriver`（Interface） | Professional.Automation | `ReadAsync(),WriteAsync(),ExecuteAsync()` | 【迁】DeviceInterfaces DriverModuleBase 职责 |
| `PACConnectorDriverBase`（Abstract Class） | Professional.Automation | `CreatePoint(),ReadPoint(),WritePoint()` | 【迁】仅确实共享生命周期的设备驱动使用基类 |
| `PACModbusTcpDriver`（Class） | Professional.Automation | `ReadRegisters(),WriteRegisters(),StartPolling(),StopPolling()` | 【迁】现 Porting 未编译，成熟实现待正式直接迁入 |
| `PACModbusRtuDriver`（Class） | Professional.Automation | `ReadRegisters(),WriteRegisters()` | 【候/迁】以实际 ReferTo 代码审核后决定 |
| `PACDeviceDefinition`（Class） | Professional.Automation | `DeviceId,DriverType,PointTableRef` | 【候】复用原专业设备类型 |
| `PACDeviceInstance`（Class） | Professional.Automation | `Initialize(),Read(),Write(),GetState()` | 【候】避免第二 DeviceManager |
| `MCNChannelBindingOwnership`（Enum） | Connectivity.Connectors | `Owned,Borrowed,Shared` | 【候】防错误关闭共享 Channel，使用已有资源租约语义 |

#### I. 消息与命令体系（本轮只确认接口需求，禁止提前生产化）

| 候选类型 | 未来 Owner | 主要方法或字段 | 与 Protocol 的关系 |
|---|---|---|---|
| `IMessagePublisher` | 消息体系（未定名） | `PublishAsync(message,ct)` | 【预】Protocol 可提交解析后的消息 |
| `IMessageSubscriber` | 同上 | `Subscribe(filter,handler),Unsubscribe(id)` | 【预】Protocol/Connector 可获得订阅内容 |
| `MessageDescriptor` | 同上 | `MessageType,Schema,Version` | 【预】不能与内部 `MPAProtocolMessage` 混淆 |
| `MessageEnvelope` | 同上 | `MessageId,Source,Payload,CorrelationId` | 【预】实际消息统一上下文 |
| `MessageSubscription` | 同上 | `SubscriptionId,Filter,Dispose()` | 【预】订阅归 Owner 管理 |
| `MessageRoute` | 同上 | `Source,Target,Rule` | 【预】不可再建全局 ProtocolMessageRouter |
| `ICommandInvoker` | 命令体系（未定名） | `ExecuteAsync(command,ct)` | 【预】Protocol 解释远程指令并提交 |
| `ICommandHandler` | 同上 | `CanExecute(),ExecuteAsync()` | 【预】真正执行权限/状态不归 Protocol |
| `CommandDescriptor` | 同上 | `CommandId,Parameters,RequiredPermission` | 【预】平台统一命令身份与治理 |
| `CommandRequest` | 同上 | `CommandId,Target,Parameters,CorrelationId` | 【预】一次命令请求 |
| `CommandResult` | 同上 | `Stage,Result,Error,Evidence` | 【预】Accepted/Sent/Ack/Completed/Unknown |
| `CommandContext` | 同上 | `Caller,Permission,Cancellation` | 【预】权限/执行上下文 |
| `CommandExecutionRecord` | 同上 | `StartedAt,CompletedAt,Status,Trace` | 【预】审计/回放 |

#### J. 枚举全集与裁撤策略

| 枚举 | 目标成员 / 语义 | 状态/裁决 |
|---|---|---|
| `MFMModuleKind` | `Connector,Compute,Simulation,Service` | 【实】已用 |
| `MCCChannelKind` | `TcpClient,TcpServer,Udp,Serial,Virtual,...` | 【候】**更推荐开放 TypeId+Descriptor**，不硬编码所有厂商 |
| `MCCChannelState` | `Closed,Opening,Open,Reconnecting,Faulted,Closing` | 【实】现有枚举为 Closed/Open/Faulted；只扩展一份 |
| `MCCIOCapability` | `ByteStream,Datagram,Message,RequestResponse,SharedBuffer` | 【候】TypeDescriptor 能表达则删 Enum |
| `MCCChannelTopology` | `SinglePeer,MultiPeer,SharedBus` | 【候】本轮确定的三种通信拓扑 |
| `MCCChannelIoMode` | `AsyncIO,DedicatedBlocking` | 【候】仅允许该通道实现确实支持的选项 |
| `MCCSubChannelKind` | `PhysicalConnection,LogicalEndpoint,Stream,Subscription` | 【候】按需要，避免把站号当 Socket |
| `MCCSubChannelState` | `Created,Open,Closed,Faulted` | 【候】能复用 ChannelState 则不建 |
| `MCCChannelOwnership` | `Owned,Shared,Borrowed` | 【候】共享/关闭行为由 Owner 决定 |
| `MCCBufferDropPolicy` | `Reject,DropOldest,DropNewest,Block` | 【候】先用 F19 OverflowPolicy/专业策略 |
| `MPAProtocolCapabilities` | `Interpret,Read,Write,Send,Request,Subscribe,...` | 【候】能力目录仍用 MOC |
| `MPAProtocolState` | `Created,Ready,Active,Stopped,Faulted` | 【候】可与生命周期现有状态合并 |
| `MPAProtocolOperationKind` | `Interpret,Read,Write,Send,Request,Subscribe,Publish` | 【候】一次操作的意图 |
| `MPAProtocolAddressKind` | `Station,Register,Tag,Topic,Object,Custom` | 【候】开放专业寻址可优先 TypeId |
| `MPAProtocolResultStage` | `Accepted,Sent,Acknowledged,Completed,Failed,Unknown` | 【候】与未来统一命令结果合并 |
| `MPAProtocolErrorKind` | `InvalidFrame,Timeout,Disconnected,AddressError,Unsupported` | 【候】错误码结构可无需 Enum |
| `MPAProcessingMode` | `Inline,Serial,KeyedSerial,Parallel,Dedicated,Runtime` | 【候】本轮确定行为；不意味着第二 Scheduler |
| `MPAProtocolSessionScope` | `Binding,Channel,SubChannel,ProtocolAddress` | 【候】按真实会话能力选择 |
| `MCNPointAccess` | `Read,Write,Control,Subscribe` | 【候】可用 Flags 或声明式权限 |
| `MCNPointDirection` | `Input,Output,Bidirectional` | 【候】独立于实际命令权限 |
| `MDSVariableQuality` | `Good,Bad,Uncertain,Stale` | 【候】必须和现有 FDT/MDS/ICS 质量码查重 |
| `MDSBindingSource` | `Constant,CurrentSlot,NodeOutput,FlowHeap,Variable,EngineeringResource,Expression` | 【实】现有枚举，不创建新数据绑定分类 |

### 14.7 第一轮 **唯一 Owner** 与“类不需要全建”的收敛门禁

1. `MCCSubChannelManager/Collection/Diagnostics` 只对真正的 MultiPeer/复杂 IO 子连接有意义；普通 TCP Client 不因为表里有它们就额外生成集合。
2. `MPAProtocolContext/ReceiveContext/SendContext/DispatchContext/SessionContext` 是**概念视角**，正式源码应首选一个轻量基础 Context + 必要只读子集，不要机械生成五个重复 DTO。
3. `IMPAProtocol/IMPAProtocolDefinition`、`MCNProtocolBinding/MPAProtocolBinding`、`MCCChannelState/MCCSubChannelState`、`MPAProtocolExecutionPolicy/DispatchOptions` 是典型**互斥合并候选**，正式类型总数须小于此参考全集。
4. `MDSVariableRegistry` 不得恢复 ReferTo 进程全局静态表；即使要跨模块共享变量也必须由工程/Host 的明确 Scope 管理。
5. `IMPAProtocolReader/Writer/Sender/Requester` 是可选能力接口集合，不强制一个类逐项写空实现；复杂协议可以通过内部私有方法和一两个最小公开接口实现相同能力。
6. `MFMModuleDescriptor` 与 `MOCCapabilityDescriptor` 不重复声明端口；连接器通过已有 Catalog 提供用户可发现操作。
7. 对应类变更必须经过 **ReferTo 真实行为比较 → 清理/合并 → 编译 → 正反测试 → CI/真机独立证据**；这份表不意味着当轮全部创建或构建通过。

# 第五篇｜统一开发使用与完整应用案例

## 15 统一开发者 API 与 Studio 操作设计

### 15.1 两角色共同使用同一份能力事实

模块资源或属性面板显示的能力列表、参数、执行特征、可用性均投影自现有 MEM/配置/MOC/Runtime Owner。程序员定义函数和能力后，Studio 不复制签名；工程用户在 Studio 修改工程引用和配置，程序员通过相同的资源/Schema 读取，不另维护 XML/JSON 表作为“UI 真相”。

### 15.2 开发者从 L0 到 L3 的实际使用方式

**L0：最常见的算法独立调用。** 只需普通 C# 方法或 Native/SDK 调用，见 §03.2 示例；不进入 MRK 不影响其单元测试。

**L1：用现有 Capability Catalog 对外暴露。** 已有 `MOCCapabilityDescriptor` 构造接收 `id/name/category/ports/version`（见本次核读真实源码）；已有 `IMOCCapabilityProvider.DescribeCapabilities()` 返回描述列表；运行能力解析由 `IMRKCapabilityResolver.TryResolve()` 负责。对外包装时必须校验实际端口类型与委托签名一致，扩充时以原 Owner 文件与测试为准。

**L2：工程/流程引用。** 在 MEM 工程里保存引用和 MOL 的输入输出连接；MOC 静态校验、编译至 MRK Plan，结果的 SourceMap 与调用路径稳定；MDS 提供运行值。四类模块均用该路径，不因类别不同维护四种节点定义。

**L3：有资源/服务/仿真时加生命周期。** Provider 自己描述可用能力与资源需求；Host、运行治理、MRK 管控运行；专有仿真模型或服务入口不把状态写回 MEM。

### 15.3 候选便捷编写风格（仅使用体验演示）

```csharp
// 【设计期伪 API，非当前 x3 可编译接口】
// 最简单的 Compute：不用 Channel、不用 Host、不用生命周期。
// module.Compute("Vision.GrayThreshold")
//       .From<GrayImage, ThresholdResult>(GrayThreshold.Apply)
//       .WithConfiguration<ThresholdOptions>();
//
// Connector：只需自己的具体差异，连接/驱动/订阅由原 Connectivity 支撑。
// module.Connector("Station.Detector")
//       .UsingProvider(new DetectorSdkAdapter())
//       .Outputs<FrameHandle>("Frame");
//
// Simulation：实现模型/时钟，不重新实现 MOL 执行。
// module.Simulation("VirtualAxis")
//       .UsingModel(new AxisSimulationModel());
//
// Service：对外提供能力；任务仍委托已有 MRK。
// module.Service("InspectionResultApi")
//       .UsingHandler(new ResultQueryHandler());
```

**审查规则**：这类链式语法只有在证明比现有 Provider/能力目录 API 显著降低真实成本、没有第二真源时才值得开发；不能为了语法好看创建大量空类。优先做到“普通 C# + 极少适配代码”而不是强迫 DSL 化。

### 15.4 Studio 工作区与模块入口

模块体系应融入现有 Studio：

| 区域 | 推荐呈现内容 | 来源与门禁 |
|---|---|---|
| 顶部菜单/Ribbon | 模块添加、配置、校验、连接测试、模拟、构建、运行、调试；危险动作按目标动态授权 | 既有命令体系；不同模块显示不同上下文 Ribbon |
| 左侧资源/能力树 | 四类模块、专业、类型、可复用资源、可用版本、来源及引用关系 | MEM/MOC，不建影子 ModuleTree |
| 中央配置组合面板 | 模块卡片按 1×N/2×N/3×N 自适应；通用参数、实例、依赖、状态、测试 | 已有工程呈现化组合面板模式 |
| 中央流程画布 | 能力节点和真实类型端口、数据连接、控制/事件关系 | MOL/MOC；不画裸 GC/Heap 操作 |
| 右侧属性/绑定 | 统一 Schema、来源、实例覆盖、生效版本、专业参数和只读状态 | 配置/MDS/专业 Provider |
| 底部运行/诊断 | Input/Local/Output、Trace、调用路径、异常、帧率、健康、资源/服务状态 | MRK/MDS/诊断/TestCenter Observation |

### 15.5 四类模块的 Studio 专有面板

| 模块 | 必须有的专有交互 | 不应强迫出现 |
|---|---|---|
| Connector | 连接方式、地址/SDK、点表/映射、实际/模拟、收发/帧/质量、命令阶段 | 无 Channel 的业务接入也显示 TCP 地址 |
| Compute | 输入输出、算法/模型版本、配置、单次测试、批量/GPU 资源和结果 Viewer | `Connect`/物理断线/串口速率 |
| Simulation | 场景树、模型、Clock、Pause/Step/Replay、故障注入、对照曲线 | 真实设备联锁已验收提示 |
| Service | Endpoint/内部入口、Readiness/依赖、并发、请求、Job 状态、停止策略 | 所有服务都必须有 Web 端口 |

### 15.6 保存/构建/运行/调试一致性验收

1. C# 以 Resource ID/Capability ID 找到的对象与 Studio 显示的是同一工程事实。
2. Studio 保存/关闭/重开，模块配置来源、映射、引用、版本保持不变；旧运行状态不回填工程。
3. MOC 编译错误能够映射到模块的原能力/端口/工程节点，而非只报“Runtime Exception”。
4. 一个运行计划在多个 Run 下可按身份查看不同参数/结果/Trace；同一相机资源的并发约束明确。
5. 用户在虚拟模式可单步或重放，但生产模式受到真实设备安全门禁、权限、任务级状态约束。

### 15.7 Studio / TestCenter：创建—选择—配置—装配—运行同一套流程

**统一工作区要求**：沿用 TestCenter 四根节点“专有测试 / 自动测试 / 场景案例 / 结果报告”；在“专有测试”下增加“模块测试”叶或组，**不另建第五个根节点**。操作按钮集中右侧操作中心，中央是紧凑配置组合面板（模块信息、Channel、Protocol、PointTable/DataBinding、运行/诊断），底部保留原始帧、日志、队列、Trace/Attempt。所有操作调用真实 CxVisual 定义/装配 Owner，TestCenter 不允许另建假的 `TestModuleManager`。

| 工作区/页面 | 面板配置 | 右侧操作中心真实按钮 | 结果区与状态 |
|---|---|---|---|
| 模块总览 | 类型/ID/名称/版本、Capability、引用与拥有者 | 新建、复制配置、保存、配置校验 | 是否已定义/已保存/已生效/运行中 |
| Channel 测试 | 类型、Topology、端点、IoMode、重连、Queue、子通道策略 | 创建、Open/Close、监听、选子通道、发送/接收 | ChannelId/Peer/Generation、Rx/Tx、错误/Backpressure |
| Protocol 测试 | 类型、Codec/Parser、ProtocolBinding、SessionScope、Dispatch、DataTarget | 配置检查、解码/编码、处理/发送/请求、注入错包 | Frame、SessionKey、操作结果、事务匹配 |
| 点表/变量配置 | 设备地址、类型/位序、转换、权限、变量/堆栈绑定 | 添加、删除、查重、批量读计划、模拟更新 | 变量值、时间、质量、来源/作用域 |
| 模块联合测试 | Connector 引用 Channel/Protocol/Point、Owner 与执行目标 | 装配、启动、运行、停止、释放、导出证据 | 组件与模块健康分开、命令结果阶段分开 |
| 四模块切换 | Connector/Compute/Simulation/Service 专业配置 | 按所选模块的能力显示操作 | 未支持的专有 API 标明 `Unsupported/NotRun` |

**关键使用感受**：平时用户只需选择通道、规约和配置点表即可；“高级”部分才出现 `IoMode/ProcessingMode/QueueCapacity/ByteBudget/ExecutionGroupRef`。无 Channel Connector 不显示无意义的 TCP 地址。TCP Server 可查看动态 SubChannel，但不能把它们当作可持久工程资源默认保存。

## 16 六个完整应用案例（从场景到验收）

> 每个案例都指定**开发者操作、Studio 使用、运行链、正常结果、反证与 Owner**。示意模块名称属于需求实例，不证明当前已有同名 SDK。

### CASE-FM-A：PLC / ICS / IoT 数据采集与控制

**目标**：PLC 测点采集、单位换算、阈值计算及数据服务；涉及 Connector、Compute、Service，Simulation 用于离线联调。

| 阶段 | 开发者做什么 | 工程用户做什么 | 关键行为与证据 |
|---|---|---|---|
| 定义 | 复用通道、规约 Provider 和点表类型；公开 `Read/Write/Subscribe` | 选 PLC、设 IP/站号、导入点表、配置周期 | 配置版本、通道可用性、变量类型、质量时标可核 |
| 计算 | 定义 `Scale(raw,unit)`、报警规则能力 | 将 `Temperature` 接到换算与阈值判定 | 计算输出与上游测点质量/时间相关联 |
| 服务 | 提供只读监控查询/订阅 | 将处理后的变量映射到服务输出 | 订阅资源可释放、流控/超时可观察 |
| 验收 | 用虚拟设备、真实 PLC（单独授权）分别测试 | 在 Studio 查看帧/地址/变量/报警 | 错地址、断线、非法控制、质量异常不伪绿 |

**硬件安全**：PLC 控制写入与状态读取是不同能力；上位机写失败/超时，不能未经确认重复执行可能产生机械动作的指令。

### CASE-FM-B：X-Ray 在线检测（四类组合）

**目标**：真实或虚拟 Detector 帧进入图像预处理、AI/传统缺陷计算、规则融合、结果服务。`Connector(Detector)` → `Compute(Preprocess,AI,CV,Judge)` → `Service(Result)`；`Simulation(Detector/Trigger)` 是可选虚拟源。

**开发者**：保留原 `IRayDetectorDriver` SDK 回调的真实采图行为，对 Frame/Trigger/Time/BufferLease 作适配；复用已有 `CxVisionDataFlow` CPU 路径作为最低闭环；AI Provider 在模型/显存/版本/精度上独立验收；结果服务可查询并通过 Connector 上报 MES。

**工程用户**：选机台、探测器模式、产品检测规则、算法模型版本；画布将图像输出连到检测能力，将检测结果连判定/复检/上报。实时显示帧序、延迟、丢帧、SN 和结果，同时能从 Trace 回到原工程节点。

**失败链**：某帧 SDK 指针释放、尺寸/位深错误或 GPU OOM → 当前帧的计算失败，结果不误判 OK；后续 SN/Run 不串数据。MES 失败不会改变已确认检测结果，是否重发按幂等与业务审批。

**验收**：真实数据路径、回调有效期、类型化多消费者、图像预览、内存峰值、吞吐量、Windows x64 真构建、实机采图与 X-Ray 联锁分别证据化。

### CASE-FM-C：机器人 / 运动控制与仿真调试

**目标**：`Compute(Trajectory,CoordinateTransform)`、`Simulation(VirtualRobot/Axis)`、`Connector(RobotSDK/MotionCard)`，可选 `Service(MotionStatus)`，流程由 MOL/MRK 负责。

开发者先用普通算法单测逆运动学/坐标与轨迹约束，专业 Provider 负责 SDK 会话、命令/ACK/Readback、设备回零/联锁；Simulation 实现虚拟关节、速度、加速度、限位和故障。工程用户在同一流程绑定虚拟或生产目标，先 Virtual 运行并查看轨迹/时标，再申请 HIL/实机权限。

**验收必须分开**：轨迹数学正确、软件流程正确、Virtual 预期响应、真实设备实际位置和安全联锁。命令超时可能已在机械系统执行；没有可靠 Readback 不能做“重试同一运动命令”。

### CASE-FM-D：CT 采图、重建及体数据服务

**目标**：Connector 接收投影与运控反馈，Compute 调用 C++ CT Reconstruction / Image Processing，Simulation 提供投影/轨迹虚拟源，Service 对外发布 Job 状态和结果浏览定位。

1. **配置**：工程引用探测器/扫描几何/重建算法/体素尺寸/能力版本，逐任务固定快照；坐标、单位、数据布局与各 ABI 参数明示。
2. **扫描**：由专业运动控制器或采图触发链提供硬件级同步，上位 MRK 只做任务级组织；触发记录、FrameId、角度/位置映射可追踪。
3. **交接**：大块 Projection/Volume 通过 `cx-cct-x3` 的受控 SHM/ABI，元数据和 DataHandle 独立，跨进程引用不包含原始指针；过期句柄拒绝访问。
4. **计算**：重建算法报 JobId、进度、错误、取消和资源使用；成功才提交有效 Volume/Result，任务失败仍保留诊断且释放资源。
5. **服务**：按 SN/TaskId 查询状态和结果定位；Image/Volume Viewer 按需读取，不把整 8GB Volume 复制进 HMI 属性面板。
6. **仿真**：利用固定投影回放/模拟扫描验证几何与重建输出；Virtual 结果不得用作真实投影质量认证。
7. **反证**：未对齐的几何版本、丢角度、SHM 失效、GPU OOM、Native ABI 错版或取消未确认都不伪造成功。

**来源边界**：`cx-cct-x3/module_api`、`shm_api`、`service/*bridge` 为可复用的真实原生资产，现有 x3 未因此自动取得 CT 模块级验收。

### CASE-FM-E：AI 检测服务和重复调用

**目标**：`Compute(Inference)` 通过 `Service(AIInference)` 对多个检测流程提供长期模型服务。Developer 使用单一推理函数合同、受控模型加载/GPU 池，Service 对内公开同步/异步接口和请求队列。Studio 选择模型版本、配置单/批输入、查看队列/就绪状态/显存/结果与错误。

**测试条件**：10 并发请求、两个模型版本、重启/模型替换时请求固定版本；参数非法、依赖模型缺失、显存溢出、调用者取消、重复请求幂等。一个 Client 超时绝不在另一个 SN 上提交结果；没有真正加载模型时服务不得报告 Ready。

### CASE-FM-F：完全无通道的逻辑、数据计算与内部服务

**目标**：证明“不需要 Channel 并不意味着缺一种新模块”。Compute 以输入字典/类型化数据执行质量判定规则，Service 供内部查询、订阅判定结果；MDS 保存合法执行数据。开发者直接调用算法，也可用统一能力接口发布；工程用户在 Studio 拖节点与连端口。

**典型规则**：`Accepted = (DefectCount == 0) && (Area <= Limit) && (ImageQuality == Good)`；规则的判断计算属于 Compute，流程 If/Else 和结果下发的节点关系属于 MOL/MRK。可选把结果发给外部 MES 时才追加 Connector。

**负例**：输入类型不匹配、缺某个必须参数、无权访问变量来源、配置版本改变/运行未固定、Service 关闭后仍尝试调用，均须有准确诊断；**不允许**因为“无 Channel”自动创建一个永远返回成功的 EmptyChannel。

### 16.7 六案例共同的最终闭环

六个案例必须都证明：工程定义/来源一致；同一能力既能直接被开发者使用又可在 Studio 中被发现；专业场景不会强迫所有模块具备相同启动/连接行为；运行结果具有真实 Owner；异常不会污染工程及其他 Run；TestCenter 能从需求号追溯到实际接口、测试、Windows CI 和适当的实机证据。**仅六张 UI 图或者代码声明不构成通过。**

---

### 16.8 本轮新增的四个贯穿完整场景（S-CN01～S-CN04）

#### S-CN01 TCP Client + Modbus TCP PLC

定义：一个 `TcpClient Channel`（SinglePeer/AsyncIO）；一个 `Modbus TCP ProtocolBinding`（Serial）；含 UnitId/寄存器地址的 PointTable 与 MDS VariableTable。开发者能直接调用协议操作，工程用户可选择通道/规约/点表并保存；异步 I/O 不强制专用线程。测试连接恢复、TID/UnitId 匹配、错误站号、错帧、VariableValue 时间/质量、重复 Write 禁止盲重试。**已迁移审查中的 Modbus Porting 文件目前不代表实际联通 PLC**。

#### S-CN02 TCP Server + 三个动态 SubChannel

一个 TcpServer Channel 监听端口 9000；动态识别 SubChannel A/B/C 并有各自 Generation，ProtocolDefinition 可共用，FrameParser/会话按流隔离；处理配置为 KeyedSerial，`MaxConcurrency=3`（用户显式设值，仅为示例）。A 连续发送帧 A1/A2，B 发送 B1，C 断开重连为 C′；断言 A 处理顺序不变，B 不被 A 长任务无界阻塞，A 回复不去 B，旧 C Session 请求不得发送到 C′。队列满必须显式拒绝/背压，Stop 后无僵尸客户端及残留线程。

#### S-CN03 RS-485 共享总线 + 三个 Modbus RTU 站号

一个 Serial Channel，Topology 为 SharedBus，ProtocolBinding Serial；`UnitId=1/2/3` 以 ProtocolAddress 表示，不创建三个 Socket/接收线程；全总线写有序、请求响应正确匹配。测试串口断线、站号超时、异步命令与自动重连、取消后迟到响应；不可将操作超时当作设备一定未执行。

#### S-CN04 无 Channel 的相机 SDK + C# 直接计算

一个无通道、无传统 Protocol 的 Camera Connector，SDK 必须同线程时使用 F19 DedicatedExecutor；触发与图像在专业 Driver 中管理，交付安全 `FrameHandle`；后续 Compute 直接调用标准算法或 MOC/MRK。TestCenter 明确 Live/Virtual/HIL 证据；源指针不因跨线程异步交付失效；无真实 SDK 时不能让假相机“Passed”。

# 第六篇｜TestCenter、工程质量、实施与最终验收

## 17 TestCenter——四种模块完整能力验证

### 17.1 不为模块体系新建测试框架

采用现有 `CxVisual.TestCenter` 单一 Catalog/Worker/Run/Attempt/Result 和真实 Owner 测试。普通 C# 用例优先 NUnit；需要结构化运行证据时使用既有 TCT 适配。必须标记 ExecutionTarget（CPU、Virtual、Replay、HIL、Production）、依赖 DLL/模拟与真实设备、对应 Git SHA/CI、数据版本及已测功能范围。**自动模拟不能替代现场实机验收**。

### 17.2 四类模块与公共能力测试（首版 TFM-001～TFM-040）

| ID | 类别 | 操作/条件 | 成功期望、失败反证或证据 |
|---|---|---|---|
| TFM-001 | 公共 | 直接运行普通 Compute 函数 | 无 Module Host；值准确；无额外运行状态 |
| TFM-002 | 公共 | 同函数公开到 MOC 能力目录并查询 | ID/Version/Ports 唯一；重复 ID 拒绝 |
| TFM-003 | 公共 | MEM 引用同一能力保存关闭重开 | 身份、参数、端口、版本不漂移 |
| TFM-004 | 公共 | MOC 编译正确/错误类型连接 | 正确可执行，错误定位具体端口 |
| TFM-005 | 公共 | Studio 对同一能力配置并保存 | 与 C# Schema/工程事实一致；无影子配置 |
| TFM-006 | 公共 | 两个 Run/实例并发执行 | Local/Output/参数隔离、无串状态 |
| TFM-007 | 公共 | 在运行期间修改模块配置 | 旧 Run 固定版本，新任务按批准策略切换 |
| TFM-008 | 公共 | 未授权副作用能力调用 | 阻断、日志有原因，不能执行物理动作 |
| TFM-009 | 公共 | Provider 抛异常/取消/超时 | 正确终态；不发布失败 PendingOutput |
| TFM-010 | 公共 | 模块描述或 Provider 版本不兼容 | 拒绝装配；诊断可定位来源/版本 |
| TFM-011 | Connector | 两个 MCCVirtualChannel 分片与收发 | 帧/关联 ID 完整；没有物理网络副作用 |
| TFM-012 | Connector | 错帧、超长帧、错误规约 | 拒绝并记录，连接状态清晰 |
| TFM-013 | Connector | 无 Channel 业务 Connector | 输入输出正常，不产生假 Socket/Send 成功 |
| TFM-014 | Connector | 断线后重连、重复订阅 | 无双回调、状态/质量/时标正确 |
| TFM-015 | Connector | PLC 点表读写与异常地址 | 地址/类型错误精确报告，不误写邻点 |
| TFM-016 | Connector | 相机 SDK 帧回调/生命周期 | 读帧期间数据有效；释放后禁止使用 |
| TFM-017 | Connector | 流量超限或慢消费者 | 背压/丢帧策略生效且有丢失计数 |
| TFM-018 | Connector | 非幂等控制超时、ACK 与完成不同 | 未确认动作标 Unknown，绝不盲重发 |
| TFM-019 | Compute | 同函数直接执行 vs MRK 执行 | 类型与结果一致，且结果精度在规定范围 |
| TFM-020 | Compute | 多输入/多输出，故意抛异常 | 全组失败原子回滚，不部分公布结果 |
| TFM-021 | Compute | 空图/非法位深/尺寸不一致 | 输入校验失败，未越界访问 |
| TFM-022 | Compute | 多消费者读取同一 Image/Volume | 不可变共享或受控复制，源不污染 |
| TFM-023 | Compute | 取消/超时/并发批量计算 | 旧 Run 结果不落入新 Run，资源释放 |
| TFM-024 | Compute | Native/GPU 缺依赖、OOM 或错版 | 状态失败、错误清晰，显存/句柄无泄漏 |
| TFM-025 | Compute | 同规则不同配置版本 | 运行快照固定，可复测与可比较 |
| TFM-026 | Compute | AI/CT 大数据调用及资源预算 | 数据句柄/预算/耗时可信，不自动深拷贝 |
| TFM-027 | Simulation | 固定模型、初态、输入序列、种子重复运行 | 轨迹和状态在容差内重复 |
| TFM-028 | Simulation | Pause/SingleStep/Accelerate 虚拟时钟 | 行为按仿真时刻推进，不混淆墙钟 |
| TFM-029 | Simulation | 虚拟 PLC/轴状态推进、越界输入 | 输入正确响应；越界拒绝并诊断 |
| TFM-030 | Simulation | 故障注入：断线、丢帧、报警 | 只影响目标，日志含注入标签，结束可复位 |
| TFM-031 | Simulation | Replay 版本/种子/时间缺失 | 不允许声称可重现；结果状态 Partial/Invalid |
| TFM-032 | Simulation | 同流程 Virtual→Production 目标切换 | 重新验证目标及安全权限，模拟 PASS 不放行真机 |
| TFM-033 | Service | 无网络的内部服务打开、调用、关闭 | 不强制 HTTP/Connector；状态准确 |
| TFM-034 | Service | HTTP/IPC 请求校验、未授权请求 | 类型/版本/鉴权失败准确，未触发副作用 |
| TFM-035 | Service | 10 并发、队列满、限流 | 不无限排队、不 OOM，拒绝/背压可观察 |
| TFM-036 | Service | 服务依赖不可用或挂起 | Readiness=NotReady/Degraded；不伪 Ready |
| TFM-037 | Service | 优雅停止/取消在途请求 | 不接新请求；旧任务状态可查，资源释放 |
| TFM-038 | Service | 超时/重复幂等业务请求 | 不重复不可逆操作，结果可关联 Correlation/Idempotency |
| TFM-039 | 跨模块 | Connector→Compute→Service 完整运行 | 帧/业务值/结果/Trace/Run 与所有权贯通 |
| TFM-040 | 跨模块 | Simulation 替代实机后再重新连接实机 | 同端口语义、明确目标/安全门禁，保存重开与 Viewer 一致 |

### 17.3 八维覆盖要求

每项需求/案例按 **定义/类型与实现、正例、反例、安全、性能/资源、Studio 交互、异常/恢复、版本与追溯** 八维建立映射。可以写 `NotApplicable` 并说明理由，不能以无证据的 `Pass` 掩盖缺失。TestCenter 案例必须能定位到模块分类、CapabilityId、Owner、运行目标、Run/Attempt、原生来源、版本和实际断言，支持从 Studio 错误跳转到用例和实际代码。

### 17.4 CI 与 HIL 的分层门槛

| 等级 | 证据要求 | 允许宣称 |
|---|---|---|
| E0 设计 | 本规格、需求 ID、类型 Owner 和设计审查记录 | “已设计，未实现” |
| E1 静态源级 | 固定 SHA 的类/接口源码、项目引用/Owner DAG 审计 | “声明存在/静态检查完成” |
| E2 自动运行 | Windows x64 编译、NUnit/TCT、真实 Owner 测试日志，含负例 | “指定能力在指定提交和环境通过” |
| E3 Studio 完整使用 | WPF 真实操作、保存重开、端口绑定、构建、运行/Trace、UIA/人工证据 | “指定界面场景已验证” |
| E4 虚拟/回放 | 确定性仿真/历史数据、故障注入、性能与结果对照 | “虚拟/回放目标通过” |
| E5 HIL/生产现场 | 实体设备、授权、独立联锁、现场安全/质量指标、签署记录 | “指定真实设备场景现场通过” |

禁止拿旧提交的 E2 结果证明新 `master`；也不能拿 E4 结果替换 E5。指标如吞吐、内存、响应时延必须标明数据尺寸、机器配置和目标，不能在规格中任意编造“已经达到”的数值。

### 17.5 性能与容量的指定测试

- 计算：记录每批输入数量/尺寸、P50/P95/P99 延迟、吞吐、CPU/GPU/峰值内存、复制次数、缓存命中、误差；阈值由对应专业产品和设备配置验收基线确定。
- 接入：收发/采集帧率、丢帧、背压、断线恢复时间、异常占比、时钟/Trigger 偏差；真实采图与纯 Virtual 分开。
- 仿真：每虚拟秒处理事件数量、步进确定性、时钟偏差、资源曲线及 Replay 偏差；虚实时间不可混测。
- 服务：Readiness 时间、并发、队列/拒绝、请求 P95/P99、CPU/GPU/内存、优雅停止时长和未知结果数量；业务幂等正确性优先于单一吞吐。

### 17.6 Channel/Protocol/SubChannel 与执行调度专项 TestCenter 验收矩阵（TFC-001～TFC-032）

> 本矩阵**新增**于原 TFM-001～040，共同构成要求追踪；TFC 编号避免覆盖已有 TFM。`Owner` 是真实功能提供者，`证据` 是待实际执行的要求，不代表现在运行通过。每条需有配置/动作、预期状态和负例；模拟、真实 TCP 回环、实机 HIL 分级。

| ID | Owner / 测试对象 | 执行动作 | 断言、负例与真实证据要求 |
|---|---|---|---|
| TFC-001 | Module/配置 | 新建四类模块、保存、重开 | ID/Kind/版本一致，重复 Id 拒绝 |
| TFC-002 | Channel Type Schema | 选择 TCP/Serial/UDP，切换配置页 | 默认值/单位/条件可见性正确；坏端口拒绝 |
| TFC-003 | Channel | TCP Client 异步 Open/Close | 在线、断开、重复调用终态一致；无未回收句柄 |
| TFC-004 | Channel | TCP Server 连入 3 客户端 | 生成 3 个有别的 SubChannelId/Generation |
| TFC-005 | Channel | A 向 B 发送方向测试 | 子通道定向无串包/错回包 |
| TFC-006 | Channel | C 重连且尝试使用旧 C 路由 | 旧 Generation 拒绝，不发送到 C′ |
| TFC-007 | Channel | UDP 两远端并发报文 | Endpoint 识别正确；数据报边界不丢失在内部拼接中 |
| TFC-008 | Channel | 单 TCP Client 不配置 SubChannel | 默认单端点成功，不必创建假客户端对象 |
| TFC-009 | Channel/Topology | Serial+SharedBus 配三站号 | 一个物理收流、站号归 ProtocolAddress、总线写受控 |
| TFC-010 | Channel/Topology | 配不兼容 I/O 与 Channel | Validate 明确拒绝，不能运行后才崩溃 |
| TFC-011 | Protocol/Binding | 绑定 Channel + Protocol + Selector | 找到唯一可消费原始收流的规约会话 |
| TFC-012 | Protocol/Binding | 两不兼容 Protocol 抢同 TCP Stream | 编译/配置校验拒绝或要求明确 Demux |
| TFC-013 | Protocol/Parser | 同一 Frame 切 1 字节发送 | 按每 Session 流式组帧，原始数据没有错误复用 |
| TFC-014 | Protocol/Parser | 错 CRC/非法长度 | 结构化错误，缓存复位/断流行为符合声明 |
| TFC-015 | Protocol/Session | A/B 交叉收帧 | Parser/事务状态按 SubChannel 隔离 |
| TFC-016 | Protocol/Transaction | Modbus TID/UnitId 不匹配 | 明确拒绝，不错误更新点表/变量 |
| TFC-017 | Protocol/Reply | 收到 A 请求，异步回复 | 回复仍到 A 的当前 Session/Generation |
| TFC-018 | Protocol/Default | 轻量同步解释默认 Serial | 无单独 OS Thread 要求、顺序可复现 |
| TFC-019 | Protocol/Inline | Inline 注入延迟/阻塞 handler | 违反 Inline 合同被禁用或检测，不允许拖垮 I/O |
| TFC-020 | F19/KeyedSerial | A1/A2 与 B1 交叉提交 | A 顺序不变、B 可按预算并行、无共享 Parser 并发 |
| TFC-021 | F19/Dedicated | 需固定线程 SDK 连续调用 | 相同 OS Thread，资源回收完成；不可误报动作已取消 |
| TFC-022 | Queue/Backpressure | TCP 接收速度高于处理 | bounded Items/Bytes，不无界增长；超限显式等待/拒绝 |
| TFC-023 | Queue/Backpressure | UDP 队列满及突发流量 | 统计 Reject/Drop，有来源，不伪造可靠送达 |
| TFC-024 | Protocol/MDS | 解释 Frame 并更新设备变量 | Value/质量/时间/Source/Scope 正确 |
| TFC-025 | MDS Context | 规约尝试写其他 Run 的 Heap/Stack | 权限/作用域拒绝，无跨 Run 泄漏 |
| TFC-026 | PointTable | 重复/超范围/类型错误地址 | 添加或构建拒绝，正常点不污染 |
| TFC-027 | 消息/命令接口（预留） | 接受消息/命令数据目标配置 | Owner 缺失应 Unsupported/NoEvidence，不假通过 |
| TFC-028 | 设备控制/命令 | 故意制造已发送未 ACK/未知终态 | 不盲重试；Sent 与 Completed 分开 |
| TFC-029 | Channel + Flow | MRK Pause 时执行心跳/网络接收 | 通道维持所需 I/O，业务执行按流程暂停语义隔离 |
| TFC-030 | Config+Run | 已有 Session 时修改 Channel/Protocol 配置 | 新配置待生效；旧事务固定版本或按规则排空 |
| TFC-031 | Stop/Dispose | 关闭拥有与借用两类 Channel | Owned 释放，Borrowed 不误关；无队列/订阅泄漏 |
| TFC-032 | TestCenter UI/Registry | 模块类型页、操作中心、诊断结果 | 使用现有 Workspace 注册和 TCT Run/Attempt；人工操作不产生自动 Passed |

**测试落位**：`Tests/TestCenter/Manual/Module` 采用既有 `TCTWorkspaceRegistry + ITCTWorkspaceFrame`；通道/规约专项沿用现有相应叶，自动用例加入原 `Automated/Cases`；共同依赖实际 Connectivity/Protocol/Simulation/MDS Owner。前 12 条完成后可验收配置/基本 I/O，只有实际 TFC-013～032 与场景证据形成后才能宣称完整协议/线程协作已可用。

## 18 工程实施、开发顺序、风险、质量与最终验收

### 18.1 推荐实施顺序——先收口公共合同，再以四个真正案例证实

| 工作包 | 内容与最小真实输出 | 不应做的事情 | 验收条件 |
|---|---|---|---|
| **FM-P0 归属审计** | 固定 `master`，读 V4.9/V2.01/V6.4、真实代码/目录/ReferTo；冻结现有 Owner/新缺口/类型表 | 凭候选清单一次性建 15/50 空类 | 类型清单逐项裁决、依赖无环 |
| **FM-P1 公共契约收敛** | 能力目录分类与聚合、配置/端口/执行特征桥接、最小 C# 直接与 MRK 调用样例 | 新建 ModuleScheduler、ModuleDataHeap、ModuleCatalog | TFM-001～010 核验 |
| **FM-P2 Compute 先行** | 复用 `CxVisionDataFlow` 或真实纯计算，实现一条 C#→能力→MOC→MRK→MDS→Studio/测试链 | 为简单函数强建生命周期、复制新算法 | TFM-019～026 与双角色闭环 |
| **FM-P3 Connector 合并** | 复用上一份 Connector V1.0 和现有 MCC/Protocol，选无通道业务 + 虚拟协议 + 真实相机/PLC 单项 | 重建新的第二 Connector Core 或复制 SDK | TFM-011～018；实机单独签署 |
| **FM-P4 Simulation/Service 实例** | 一套确定性虚拟设备、一套内部服务及按需对外服务，复用 Host/MRK | 建第二 Runtime Host/任务引擎 | TFM-027～038，真实请求与停止 |
| **FM-P5 跨专业完整验收** | CASE-FM-A～F、Studio 保存重开、四类组合、数据/异常/调试、负例、性能、Windows CI | 只做演示图，或只报告“能编译” | TFM-039～040 + 每案例 E2/E3，HIL 按产品另核 |

用户批准后可以按工作包执行；本次是文档交付，**没有**因此授权自动修改仓库、创建项目、拷贝 DLL 或提交代码。

### 18.2 依赖、平台与第三方组件约束

保持现有 .NET Framework 4.8、WPF、x64、传统 `.csproj` 工程规范。C++/Halcon/相机 SDK/CT GPU/机器人厂家 DLL 版本、许可、加载路径、位数与设备驱动必须按实际环境验证；未获取真实实体 DLL 不声称迁移完成。配置文件/模型资源/原生算法结果与工程/服务发布包分开治理。跨进程 Native Handle、SharedMemory、接口 ABI 要有源/目标所有权、失效/版本/容量合同；不要凭 `IntPtr` 直接穿越进程边界。

### 18.3 安全和可靠性最高优先级

- **设备**：射线源、运动卡、机器人、PLC 的实际互锁/安全必须由厂家控制器、PLC 与专业安全机制提供；WPF 的禁用按钮和 Runtime AccessPolicy 仅是上层防线。
- **数据**：不允许跨 Run 使用过期帧、未授权共享 GPU/SHM 引用、把失败计算结果当作最终判定。
- **服务**：权限/凭据不可在日志、配置 UI、历史版本中泄漏；不可逆操作使用严格业务幂等或人工确认。
- **恢复**：异常退出后的在途设备动作、远端请求与重建任务状态应报告未知/待确认，不能自动假设失败然后重复执行。
- **兼容**：变更 Module Category、能力版本、参数 Schema、Provider ABI、原生结构布局必须有迁移、版本门禁与回滚方案。

### 18.4 四类模块的完成判定

| 维度 | 最低“完成”标准 |
|---|---|
| 软件开发者 | 一个纯计算函数可单测/直接用；四类各有真实 Provider 样例；公开能力无需重复写 Port/Config/Registry；异常可定位 |
| 工程编排用户 | Studio 能搜索、添加、配置、连接、保存/重开、构建、运行、观察、调试四类代表性模块，交互符合现行 Studio 规范 |
| 组织一致性 | MEM 是定义真源，MOL 是编排定义真源，MOC 能验证/构建，MRK 是执行真源，MDS 是运行数据真源 |
| 能力完整 | Connector 的读写/事件/设备、Compute 的计算/算法、Simulation 的时钟/模型/故障、Service 的稳定供给均通过真实 Owner 测试 |
| 安全/质量 | 未授权危险动作反证、资源释放、并发/隔离、失败不假成功、命令未知状态和数据生命周期测试通过 |
| 验收证据 | 固定 SHA/Windows CI/真实 WPF 操作/按场景的 Virtual 与 HIL 证明，需求号与 TestCenter Case/Run 可追溯 |

### 18.5 本版未完成清单（必须公开保留）

- 未把 02/03/04 三份大型 Excel 中所有类/枚举逐 Sheet 与此规格完成全量交叉审核；需在 FM-P0 补做。
- 本地 V1.0 形成时尚未新增四模块公共合同；之后主仓已新增 `MFMModuleKind`、`MFMModuleDescriptor`、`IMFMModule` 及部分 Channel/Protocol/Variable 骨架，具体主仓代码在最新 SHA 查核；仍未形成四模块完整可运行实现或本版 Windows CI 证据；本稿候选 API 不可按实际程序集调用。
- 未新增完整 SimulationModule、ServiceModule 生产实现，也未执行本次 Windows CI/设备/Studio 实测。
- 未就 AI/CT/机器人 SDK 的许可证、DLL、ABI 与现场环境给出部署签署证据；旧仓生产经验不自动等于新框架验收。
- 四种模块如何映射现有 ResourceDescriptor 的最终序列化 Schema、模块级 SideEffect 字段、跨进程作业恢复与仿真时钟协议，需要在实际测试/类型审计后进一步定案。

### 18.6 最终裁决与版本规则

本 V1.1 正文约束四类模块**共同目标与业务使用方式**；接入模块的专有协议/Driver/SHM 内容沿用并向本文公共合同对齐已有《ConnectorModule V1.0》。今后的类型变更先检查现有 MEM/MDS/MOL/MOC/MRK/Connectivity 和 V6.4 归属，保留 ADR 决策；成功的实施记录与 CI 进入 `DOCS/Testing/` 台账，不把“实现进度”反复复制进规格正文。只有当真实使用暴露公共缺口，才允许新增通用类型。

---
### 18.7 【V1.1】新增架构审查与实施门禁（不更改原 FM-P0～P5）

| 门禁 | 审查问题 | 必须交付 |
|---|---|---|
| AR-01 | Channel 是否已经区分生命周期、IO 能力与拓扑？ | 正确配置/类型/父子通道/子通道来源的 API 与负例 |
| AR-02 | 是否严格分开 SubChannel、ProtocolAddress、SessionId？ | TCP Server 与 RS-485 两场景的身份追踪 |
| AR-03 | 同一 Channel 的原始流由谁读取/解复用？ | 唯一 Owner；多 Protocol 冲突反证 |
| AR-04 | Protocol 是否具有七类能力且简单规约无需实现全部？ | 一个简单协议 + 一个完整 Modbus 的双案例 |
| AR-05 | Channel 是异步 I/O，Protocol 用 F19/MRK 而非新线程池吗？ | Thread/Task/队列/资源统计与关闭测试 |
| AR-06 | 配置保存/生效和 Run 固定版本分开吗？ | Studio 保存重开、在途事务改变配置的负例 |
| AR-07 | Point/Variable/MDS/Message/Command 有唯一 Owner 吗？ | 类型/作用域/权限/结果阶段校验 |
| AR-08 | ReferTo 是否只读、没有额外 C# Legacy Adapter？ | 来源→目标→直接迁移/合并/删除对照表及差异测试 |
| AR-09 | TestCenter 是否直接调用真实生产 Owner？ | Workspace 注册、TFC 证据、CI/实机分级 |

**推荐实施次序**：审查 Channel/Protocol/变量正式接口 → 完善 Channel/SubChannel/Topology/Config 的可编译合同 → 提供至少一套正式 Virtual/TCP 的真实 IO 用例 → Protocol Binding/Session/Dispatch 使用现有 F19 → PointTable/MDS 数据映射 → TestCenter 专用创建/配置/运行面板 → 拓展 Modbus/SDK/HIL。此顺序允许先完成可审查架构并不保证初版跑出实机效果，符合先搭架构、直接重构成熟代码的开发决定。

# 附录 A｜完整需求编号、责任与验收追踪矩阵

> 本附录把第 02/08～11 章的需求拆成可检查的“需求 → Owner → 实际案例 → 测试 ID”。每项在进入开发时补充固定 SHA、API/文件、用例日志、风险标记、Windows CI 与 Studio/HIL 证据。对当前未执行的测试，状态统一为【目标／未验收】。

## A.1 共性需求 UM-C01～C20

| 需求 | 简称 | 主证据与入口 | 主要测试 |
|---|---|---|---|
| C01 | 身份/版本 | MEM、ResourceRef、MOC Version；CASE A-F | TFM-003、010 |
| C02 | 能力发现 | 既有 MOCCapabilityCatalog；四种能力样例 | TFM-002 |
| C03 | 端口和类型 | 现有 MPCPort/MRK Input/MDS；CASE B/D | TFM-004、019、021 |
| C04 | 配置 | FCM/MHS Schema 与固定版；CASE A/B/E | TFM-005、007、025 |
| C05 | 直接调用 | 纯函数和服务单测；CASE F | TFM-001、019 |
| C06 | 工程使用 | Studio/MEM/MOL/MOC/MRK；CASE A-F | TFM-003～005、039 |
| C07 | 数据绑定 | MDSBindingSource；CASE B/F | TFM-004、020、039 |
| C08 | 多执行模式 | MRK Task、专业 Provider；CASE D/E | TFM-023、037 |
| C09 | 生命周期按需 | Connector/AI/Service/Simulation；CASE B/C/E | TFM-014、024、033、037 |
| C10 | 资源所有权 | FDT/MDS/专业 SHM/GPU；CASE B/D | TFM-016、022、026 |
| C11 | 并发隔离 | MRK/MDS/专业 SDK；CASE B/E | TFM-006、023、035 |
| C12 | 错误和 Trace | MRK/SourceMap/诊断；CASE A-F | TFM-009、018、038 |
| C13 | 可观察性 | FLD/MRK/TestCenter/Studio | TFM-005、017、035～037 |
| C14 | 安全/副作用 | 运行治理/专业硬件；CASE A/C | TFM-008、018、032 |
| C15 | 实例复用 | MEM 引用、Provider Resource Lease | TFM-003、006、014 |
| C16 | 兼容性 | 配置、Capability/ABI；CASE D/E | TFM-010、024、031 |
| C17 | 目标隔离 | Virtual/Replay/HIL/Production；CASE C | TFM-032、040 |
| C18 | 实现扩展 | C#/Native/SDK/远端；CASE B/D/E | TFM-016、024、034 |
| C19 | Studio 使用 | 配置/流程/结果/调试；CASE A-F | TFM-005、039、040；E3 |
| C20 | TestCenter | 单模块+组合+反证 | TFM-001～040；E2–E5 |

## A.2 四类模块完整需求映射

| 需求 ID | 核心场景 | 归属侧重点 | 对应测试 |
|---|---|---|---|
| CN01 Channel 可选 | NoChannel、Serial、IPC | Connectivity/Connector Provider | 011、013 |
| CN02 Protocol/Driver | Modbus、Vendor SDK | Connectivity + Professional | 012、015 |
| CN03 读写与点表 | PLC/ICS/IoT | Connectivity/MDS | 015 |
| CN04 事件订阅 | 相机回调/报警 | Connector/MRK | 014、016 |
| CN05 命令反馈 | 机器人/运控 | Professional/Runtime | 018 |
| CN06 高速媒体 | Detector/CT | Professional/FDT/MDS | 016、017、026 |
| CN07 生命周期健康 | 设备重连 | Connectivity/诊断 | 014 |
| CN08 多通道路由 | 复合设备 | Connectivity | 011、014、017 |
| CN09 业务适配 | MES/API | Connector/Inspection | 034、038、039 |
| CN10 权限/现场诊断 | 设备写入 | Professional/治理 | 008、018 |
| CP01 普通函数 | 规则/数学 | 专业/纯代码 | 001、019 |
| CP02 通用计算 | 数据/几何 | Foundation/Professional | 019、020 |
| CP03 专业算法 | Vision/CT/AI | Professional | 021、024、026 |
| CP04 参数/端口 | 多输入/输出 | MOC/MDS | 004、020 |
| CP05 CPU/GPU/Native | 推理/重建 | Professional/ABI | 024、026 |
| CP06 异步作业 | CT Job | MRK/Professional | 023、026 |
| CP07 不变性 | 图像/体数据 | FDT/MDS | 022、026 |
| CP08 规则表达式 | 质量判定 | MOL/Compute | 019、025 |
| CP09 模型/缓存 | AI 模型 | 专业资源/Host | 006、024、025 |
| CP10 性能/诊断 | 批处理 | 专业+TestCenter | 023、026 |
| SM01 模型对象 | Virtual PLC/轴 | Professional.Simulation | 029 |
| SM02 仿真时钟 | Step/Scale | Professional.Simulation | 028 |
| SM03 状态/行为 | 运动与报警 | Professional.Simulation | 027、029 |
| SM04 多对象场景 | 产线仿真 | Professional.Simulation/MEM | 027、030 |
| SM05 数据生成 | 虚拟相机/测点 | Simulation+MDS | 029、040 |
| SM06 Replay | CT/图像回放 | Simulation | 031 |
| SM07 故障注入 | 断线/丢帧 | Simulation/TestCenter | 030 |
| SM08 虚实替换 | 机器人/相机 | Simulation+Connector | 032、040 |
| SM09 误差验证 | 轨迹/投影 | Simulation/专业 | 027、040 |
| SM10 生命周期 | Reset/Pause/Dispose | Simulation/Host | 028、030 |
| SV01 内部服务 | 本地计算查询 | Service Provider/Host | 033 |
| SV02 外部服务 | HTTP/IPC | Connectivity/Service | 034 |
| SV03 服务状态 | Ready/Stop | Service/Host | 036、037 |
| SV04 请求结果 | Job/Correlation | Service/MRK | 038 |
| SV05 并发流控 | AI 服务 | Service/MRK | 035 |
| SV06 依赖/资源 | GPU/DB | Host/Professional | 036 |
| SV07 部署/启动 | 外部 C++ 服务 | 运行治理/Host | 036～037 |
| SV08 健康诊断 | API Readiness | Service/诊断 | 036 |
| SV09 授权/幂等 | MES/结果 API | 专业/治理 | 034、038 |
| SV10 停止/恢复 | 服务崩溃/取消 | Host/MRK/Service | 037、038 |

## A.3 六案例与两角色覆盖

| 案例 | Module 分类 | 开发者验收 | Studio 验收 | 运行与风险 |
|---|---|---|---|---|
| A PLC/IoT | Connector+Compute+Service（可加 Simulation） | 复用规约/点表、计算、服务 | 配置、绑定、诊断 | 真机控制授权、Quality/Time |
| B X-Ray | Connector+Compute+Service+Simulation | SDK Frame/算法/结果 | 模型、帧、Viewer、Trace | 指针寿命、射线安全、GPU/吞吐 |
| C Robot/Motion | Connector+Compute+Simulation | 轨迹、位置与命令 | 虚拟、构建、授权、回读 | 非幂等命令、硬件联锁 |
| D CT | 四类 | ABI/SHM、重建 Job、查询 | 几何/算法/Run/Viewer | 坐标、内存、GPU、版本 |
| E AI Service | Compute+Service | 模型/并发/取消 | 选模型、服务健康、查询 | 显存、版本、请求隔离 |
| F NoChannel | Compute+Service（可适配 Connector） | 纯 C# 直调、规则 | 端口、参数、无通道 | 不能伪装网络执行成功 |

---

# 附录 B｜完整类型和 API 归属复核表

## B.1 “现有/候选/禁止”三层总账

- **现有**：正文 §14.1 列出的已发现类/接口，需在 FM-P0 将全限定名/所在源文件/原 Assembly/可见级别与当前 master 固定 SHA 复核；保持原 Owner。
- **待审候选**：§14.3 共 15 项“上限候选”，不等于要新增 15 项；每个候选必须填下表并说明为何不能增强已有合同。
- **明确禁止重复**：§14.4 中工程、端口、配置、数据、调度、编译、Undo 和测试 Runner 等重复体系，不可创建。
- **Connector 专业类**：此前 Connector 规格的 50 项设计候选在其专门表审查，本统一模块主规格只保留重叠防护关系，不再次生成 50 项空壳。

## B.2 新增候选逐类落地审核字段

任何新增类提交前必须完成下列表格的每一列，且附真实单元/集成测试：

| 字段 | 必须填写的实际内容 |
|---|---|
| 设计类名/类别 | Class/Interface/Enum、前缀、Namespace、访问级别 |
| 目标职责 | 唯一可观察行为，不能只是另一类字段搬运 |
| 实际 Owner | 现有 28 项目之一 + 目录 + 子方面 ID（V6.4 对照） |
| 来源需求 | UM-XXX 或专业需求、使用案例 |
| 现有重复对象 | 源码路径、复用不足的技术原因、排除合并的证据 |
| 可见 API | 构造、方法、属性、错误、线程、版本、生命周期、序列化 |
| 依赖 | 哪些 Assembly/第三方 SDK，是否满足依赖 DAG |
| 维护者与扩展 | 哪个 Owner 负责变更、专业怎样实现接口 |
| 测试 | TFM 相关用例 + 正负/边界/资源/性能证据 |
| 最终决议 | 复用/增强/合并/新增/拒绝/暂缓，及 ADR 链接 |

## B.3 公共 API 拟议面（合同而非新接口数量）

| API 能力簇 | 所需行为 | 首选现有入口 / 审查点 |
|---|---|---|
| Describe | 通过 Provider 提供能力 Id/Name/Ports/Version/Category | `IMOCCapabilityProvider` / `MOCCapabilityDescriptor` |
| Find/Search | 按类别、关键字发现能力和当前可用性 | `MOCCapabilityCatalog` |
| Resolve/Invoke | 类型校验后取得能力执行入口，支持直调与 MRK | `IMRKCapabilityResolver`、`MRKResolvedCapability` |
| Bind | 选择常量/变量/Heap/NodeOutput/资源并解析 | `MDSDataBinding` / Resolver |
| Input/Output | 节点 DataContext，Return 成功后原子可见 | `IMDSDataContext` / `MRKExecutionInput` |
| Build | 校验能力引用、版本、端口、生成 Plan/SourceMap | `MOCBuildService` / `MOCEngineeringCompiler` |
| Start/Stop/Cancel | 按能力特征、运行身份进行控制 | MRK Host/Lifecycle + 专业 Provider |
| Observe | 按实例/Run/Job 查询状态、健康和 Trace | 现有诊断/日志/TestCenter Observation |
| Configure | Schema 编辑、校验、保存/生效、固定版本 | 配置体系，不新 `ModuleConfigManager` |
| Publish/Authorize | 服务/设备副作用的目标环境发布与安全门禁 | 运行治理/专业安全及 MRK 访问策略 |

## B.4 首期建议真正编码的对象范围

1. 以纯计算/CPU Vision 的现有对象为最小使用场景验证 `Capability` 元数据是否充分，优先修复原类型缺口而非加新 Base。
2. 若必须表达四分类，用现有 Category/资源元数据是否即可持久化？先设计 schema 迁移与兼容测试，再决定 `ModuleKind` 枚举是否真正必要。
3. 按 Connector V1.0 复核 `IMCCChannel`、Protocol 和 SDK 接入合同，检查其 50 候选去重；第一批只做一个 Virtual、一个 NoChannel、一个真实专业 Driver 的联合测试。
4. Simulation 先实现真正可测试的模型/Clock 与 Virtual I/O；Service 先实现内部 Service 的状态/请求/优雅停止，不为展示而扩充框架。
5. 只在上述真实功能触发新需求时新增 `Lifecycle Adapter`/ExecutionTraits 等，禁止先批量造类后找场景。

---

# 附录 C｜五大代码与现有 CxVisual 的复用审计索引

## C.1 五大代码路径、代表对象和核心约束

| 仓库 | 具体源路径/对象 | 可继承的经验 | 禁止的错误移植 |
|---|---|---|---|
| ccxx | `ccxx/cxchannel.h`、`cxchannel_none.h/.cpp`、`cxchannel_tcpclient.*`、`cxchannel_serial.*`、`cxsharememory.*` | Channel 组件化、NoneChannel、IO 资源和基础缓冲 | 整个 C++ 全局管理器与内存裸指针直接注入 C# 工程 |
| gcl3 | `src/vdi/terminal_manager.cpp`、`protocol_base`、`measure_*`、`src/protocol/modbus_protocol*` | Terminal、点表、规约装配、实时质量/控制 | 复制旧工程资源树、旧 Protocol/Measure 数据中心 |
| device_communicator | `hy/common/communicatorc/communicator.go`，`hy/communicator/{iec104,modbus,modbustcp,...}` | Communicator、Protocol/Channel/Sheet 配置，边缘服务 | 把 `CommunicatorManagerSingle` 全局状态当成新运行 Owner |
| cx-vision-x2 | `DeviceVariable/DeviceInterfaces/DriverModuleBase.cs`、`Drivers/IRayDetectorDriverModule/IRayDetectorDriver.cs`、`CxECTStudio/Business/DetectorImageService.cs` | 驱动模板、测点、SDK 帧、采集异步工作 | SDK 临时 `IntPtr` 直接暴露为永久 MDS 值，或丢弃原专业安全规则 |
| cx-cct-x3 | `module_api/module_api.md`、`module_api/cct_recon_api.h`、`shm_api/shared_memory_design_v_2.md`、`service/cct_shm_recon_bridge.h` | 稳定 C ABI、异步任务、体数据大块/元数据分离 | 将原生句柄序列化、错用坐标或把 SHM 视为任意指针内存 |

## C.2 当前 x3 的最短核对源表

| 验证方向 | 入口路径 | 优先检查问题 |
|---|---|---|
| 工程规则 | `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md` | Owner/依赖、提交、ReferTo、验证要求 |
| 七仓导航 | `DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` | 生产资产与 x3 新实现状态不混同 |
| 四大基线 | `DOCS/01_*.md`、`02_*.xlsx`、`03_*.xlsx`、`04_*.xlsx` | 子方面、唯一类型、职责边界、规划/已实现区分 |
| 工程组织 | `DOCS/Architecture/CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md` | MEM/MOL 事实、MOC/MRK 分工 |
| 数据 | `DOCS/Architecture/CxVisual_运行时数据体系——完整对象规格与双角色使用指南_V3.0.md` | MDS Get/Set/Return、Heap/Stack/Frame/大数据 |
| 配置 | `DOCS/Architecture/CxVisual_配置体系——完整设计与验收 V2.4.md` | Scope/Schema/生效/版本/自动属性编辑 |
| 运行 | `DOCS/Architecture/CxVisual_统一运行体系#0总体架构——完整设计与职责划分_V1.0.md` | 运行控制/管理/调度/协同不重复 |
| TestCenter | `DOCS/Architecture/CxVisual_测试体系——完整规格 V4.1.md` | 真实 Owner/证据、测试中心单一目录 |
| 类型/项目 | `DOCS/ProjectCatalog.csv`、`DOCS/DirectoryLayout_V1.1.md` | 28 项目/子方面、相互引用与现状 |
| 能力 | `Mechanism/Orchestration/MOCCapabilityCatalog.cs` | 描述/发现/可用性是否足够 |
| 通信 | `Mechanism/Connectivity/Channels/MCCVirtualChannel.cs` | 真实/模拟通道与帧交接 |
| 运行数据 | `Mechanism/Data/RuntimeData/MDSDataBindings.cs` | 统一数据来源/绑定与安全值 |
| 专业计算 | `Professional/Vision/CxVisionDataFlow.cs` | 正式 MRK+MDS 计算闭环 |
| 宿主 | `Applications/Runtime.Host/UnifiedRuntimeHost.cs` | 模拟策略、发布/Journal、恢复边界 |
| 参考代码 | `ReferTo/{Channels,Protocols,DeviceVariable,Vision}` | 历史能力适配，不能视作产品已实现 |

## C.3 本次查阅范围与未作的核验

- 本次于 GitHub 读取 2026-10-22 `master` HEAD 和相关仓库文件/目录；源码核对属于**定向审阅**，不是全仓逐文件或整个 V4.9/V6.4 类型 Excel 审计。
- 先前 ConnectorModule 文档已经对 ccxx、gcl3、IoT、Vision、CCT 五大旧仓的相关源代码做定向核实，本文件延续其具体路径/行为归属，不对旧仓代码是否可直接在 x3 环境编译作推断。
- 未对所有第三方设备真实厂商 SDK、版本/许可、Windows DLL/x64 和测试机台重新检查，外部依赖状态统一标注【待核】。

---
# 附录 D｜软件开发者完整示例与 Studio 操作清单

## D.1 示例使用规则

- **示例类型 A：标准 C# 可独立使用**，无需引用不存在的 Module API；可抽离到当前 C# 7.3/.NET 4.8 工程中单测，但本次并未编译执行。
- **示例类型 B：伪 API/目标集成方式**，用于确认“开发者最短路径”；不得作为当前 SDK 复制运行。
- **示例类型 C：Studio 操作验收步骤**，为未来真实界面目标；现有 UI 是否每个按钮都可操作，须在具体提交实际验证。

## D.2 示例 A：完全无通道的检测规则 Compute

```csharp
using System;

// 独立于 CxVisual 模块框架的普通业务对象
public sealed class DefectSummary
{
    public int DefectCount { get; set; }
    public double LargestArea { get; set; }
    public bool ImageQualityGood { get; set; }
}

public sealed class JudgeOptions
{
    public int MaximumDefects { get; set; }
    public double MaximumArea { get; set; }
}

public sealed class JudgeResult
{
    public bool Accepted { get; set; }
    public string Reason { get; set; }
}

public static class QualityRule
{
    public static JudgeResult Judge(DefectSummary input, JudgeOptions config)
    {
        if (input == null) throw new ArgumentNullException("input");
        if (config == null) throw new ArgumentNullException("config");
        if (config.MaximumDefects < 0 || config.MaximumArea < 0)
            throw new ArgumentException("判定参数不能为负值");
        if (input.DefectCount < 0 || input.LargestArea < 0 ||
            double.IsNaN(input.LargestArea))
            throw new ArgumentException("检测结果数据无效");

        if (!input.ImageQualityGood)
            return new JudgeResult { Accepted = false, Reason = "图像质量不合格" };
        if (input.DefectCount > config.MaximumDefects)
            return new JudgeResult { Accepted = false, Reason = "缺陷数量超过阈值" };
        if (input.LargestArea > config.MaximumArea)
            return new JudgeResult { Accepted = false, Reason = "缺陷面积超过阈值" };
        return new JudgeResult { Accepted = true, Reason = "OK" };
    }
}

// 普通开发者直接使用：
// JudgeResult result = QualityRule.Judge(
//     new DefectSummary { DefectCount=0, LargestArea=0.05, ImageQualityGood=true },
//     new JudgeOptions { MaximumDefects=1, MaximumArea=0.1 });
```

**期望导出**：能力 ID `Inspection.QualityJudge`，输入 `DefectSummary`、`JudgeOptions`，输出 `Accepted`、`Reason`，并由专业类型适配注册明确 Copy/Immutable 语义。这个导出可以采用现有 `IMOCCapabilityProvider`+MOC 端口契约，再由 MRK DataContext 适配；不可在新模块体系里平行存储同一 Port 定义。

**必须验证**：两个 Run 各有自己的配置/数据，`MaximumArea` 无效被拒绝，结果不触发设备下料/放行副作用，只有 MOL/MRK 的授权动作能力才能真正操作设备。

## D.3 示例 B：异步 CT 计算与独立 Service 的接口边界（C# 示意合同）

```csharp
// 【以下代码示意业务接口，不是当前正式 CxVisual 类型】
// 业务的输入输出/异步语义独立于 Module 技术实现。
public interface IReconstructionComputer
{
    System.Threading.Tasks.Task<ReconstructionResult> ReconstructAsync(
        ReconstructionRequest request,
        System.Threading.CancellationToken cancellationToken);
}

public interface IReconstructionQueryService
{
    System.Threading.Tasks.Task<JobTicket> SubmitAsync(
        ReconstructionRequest request,
        System.Threading.CancellationToken cancellationToken);
    System.Threading.Tasks.Task<JobStatus> GetStatusAsync(
        string jobId,
        System.Threading.CancellationToken cancellationToken);
}

// ReconstructionRequest/Result、JobTicket/Status 属于此示意合同的数据 DTO；
// 正式产品中须复用实际 CT ABI/DataHandle/MRK Job/Result，不能直接复制建立第二套 Job Manager。
```

算法独立实现 `ReconstructAsync`，服务入口只承诺受理/JobId/查询/结果访问，不等于每次请求创建一个新的计算框架。真实 `cx-cct-x3` 重建可能通过 C ABI + SHM Adapter 实现，必须确保跨进程数据所有权和终态一致。

## D.4 示例 C：模块组合的工程用户操作单（端到端检查）

| 序号 | Studio 操作 | 观察点 | 对应结果 |
|---|---|---|---|
| U01 | 新建工程/Content，添加检测线资源 | 稳定 ResourceId、空状态、正确专业 | MEM 新 Revision |
| U02 | 打开模块/能力目录搜索相机、阈值、虚拟模型、结果服务 | 四种模块分类，同一搜索入口 | MOC 目录/专业可用性 |
| U03 | 添加 Detector Connector 的实际/模拟资源定义引用 | 按来源区分配置/实例、无默认生产授权 | MEM ResourceRef |
| U04 | 在中央组合配置面板编辑探测器参数 | 默认/覆盖、Schema、已保存/已生效版本 | 统一配置 Owner |
| U05 | 添加 Compute 预处理和 Judge 能力 | 输入输出类型正确，算法版本可选 | MOC/MPC 端口 |
| U06 | 添加 Simulation VirtualDetector 并指定时钟/场景 | 明确虚拟标签、模型/种子/场景版本 | Simulation 专业资源 |
| U07 | 添加 Internal Result Service | 无需网络也可查询状态，健康可见 | Service Provider/Host |
| U08 | 打开流程主画布并连接 Detector→Compute→Service | 类型不兼容直接标红，来源可定位 | MOL/MOC |
| U09 | 配置数据来源：常量/节点/变量/Heap/工程资源 | 不出现另一个“模块变量仓” | MDS Bindings |
| U10 | 按 Virtual 模式构建/运行 | 计划版本/权限/依赖/RunId 可查 | MOC/MRK |
| U11 | 在底部面板查看 Frame/算法输入输出/服务状态 | 大图只显示元信息，按需专业 Viewer | MDS/Trace/Viewer |
| U12 | 故意制造错误：坏位深、断线、超时、队列满 | 错误可定位模块/能力/端口/Run | TestCenter/诊断 |
| U13 | 保存、退出 Studio、重开工程 | 工程配置/引用/连接保留；运行态不成为 Revision | MEM |
| U14 | 切换 Production 目标 | 重新验证 Provider、机台授权、独立安全联锁 | 治理/专业 Owner |
| U15 | 在授权真机模式点检并运行 | 设备实际 ACK/状态/Frame/业务确认有区别 | E5 HIL 证据 |
| U16 | 检查 CI/日志/用例报告 | TestId/Run/Attempt/Git SHA 与实际结果一致 | TestCenter |

**判定**：U01～U13 可以成为软件/虚拟目标的阶段性闭环；U14～U16 属于更严格的发布、实机与追溯目标。任一操作现有 Studio 不支持，就记录为待实现，不画假截图假装已经操作成功。

## D.5 开发者 API 完整使用期望快速清单

| 能力需求 | 最短理想体验 | 高级扩展入口 |
|---|---|---|
| 调用普通计算 | 普通方法调用 | MOC 端口/模型/GPU Provider |
| 发布计算能力 | 元数据 + 适配现有能力目录 | 输入类型/版本/单位与配置 Schema |
| 使用 PLC | 选 Channel/Protocol/PointSheet 配置，拿到读写事件 | 会话、安全策略、链路重连/时序诊断 |
| 使用相机 | 使用专业 Adapter，订阅安全 FrameHandle | SDK 指针寿命、背压、缓存池、Trigger 关联 |
| 使用虚拟设备 | 初始化模型并按时钟推进 | 多实体/Replay/故障注入/虚实对照 |
| 建内部服务 | handler + readiness + stop | 远程 Endpoint、并发/部署/高可用审查 |
| 加入工程 | 创建资源/引用和公开能力 | 编译/SourceMap/权限/跨工程版本 |
| 运行调试 | 读 Run/Trace/Input/Output/错误 | 专业 Viewer、性能、实机点检 |

---

# 附录 E｜TestCenter 实施证据、验收清单与版本维护模板

## E.1 一个测试用例的最小证据记录

```yaml
# 仅为测试证据记录示例格式；不是当前 TestCenter 的真实可导入 Schema。
requirement_id: UM-CP07
case_id: TFM-022
module_kind: ComputeModule
owner: CxVisual.Professional.Vision
capability_id: Vision.ReadOnlyImageConsumer
execution_target: Virtual
repository: oudream/cx-visual-x3
commit_sha: "<fixed-git-sha>"
configuration_version: "<actual-version>"
plan_version: "<actual-version-or-not-applicable>"
run_id: "<actual-run-id>"
attempt_id: "<actual-attempt-id>"
assertions:
  - "两消费者不会修改共享原始图像"
  - "输出错误不会污染其他 Run"
  - "资源结束后引用失效"
result: "NotRun" # 真实执行后由 TestCenter 赋结果，禁止编造 Passed
artifacts: []
limitations: "未执行 GPU/设备现场验收"
```

TestCenter 真实存储与字段应以其 V4.1 正式 API 和当前实现为准，此处仅说明哪些事实必须能追溯，不规定另一个测试证据仓库。

## E.2 工作包与验收证据预期

| 工作包 | 目标产物 | 工程验证 | Windows/Studio/HIL 门禁 |
|---|---|---|---|
| FM-P0 | 类型/Owner/功能差距与 ADR | 查重、依赖 DAG、方案决议 | 静态 E1；不声称功能通过 |
| FM-P1 | Module/Capability 统一最小合同 | 编译、模块发现/类型/版本正反例 | E2；适当 E3 |
| FM-P2 | Compute 真实 CPU/专业路径 | 值/质量、精度、取消、数据/资源 | E2 + 计算 Viewer/E3 |
| FM-P3 | Connector 完整单体路径 | Virtual/NoChannel/真实驱动、超时、安全 | E2/E4，真机另做 E5 |
| FM-P4 | Simulation/Service 真正实例 | 模型 Clock、Request/Stop/Readiness | E2/E4/E3；外部服务环境另核 |
| FM-P5 | 六案例跨体系验收 | 保存重开/构建/运行/Trace/故障/复用 | E2/E3/E4；受控专业用例 E5 |

## E.3 每轮实施必须更新的动态台账

- `任务编号 / 需求ID / 状态 / Owner / 工程文件 / 变更理由 / 相关接口 / 实际用例 / GitSHA / CI URL / 设备/目标 / 尚未实现 / 风险 / 下个动作`。
- 规范在本主文件维护，**实际进度**和同 SHA 构建/测试证据留在已有 `DOCS/Testing/` 的相应实施记录或统一台账。避免本主文档随每次提交写一次“当前已全部完成”的易腐烂声明。
- 新 API 与原规格冲突时先差距分析、Owner 裁决和 ADR，再改文档/代码/测试；不得只为获得绿灯而删掉异常测试。

## E.4 文档质量自检清单

- [x] 四类模块固定，不引入第五种 Module 类型。
- [x] 完整职责、共性 20 项、各类 10 项专有需求列出。
- [x] 软件开发者与工程编排用户的使用路径、正反例都有具体场景。
- [x] Definition/Instance/Capability/Provider/Runtime 的身份分离。
- [x] MEM、配置、MDS、MOL、MOC、MRK 与专业唯一 Owner 边界明确。
- [x] 六个工业组合案例、40 项 TestCenter 起步测试及证据等级列出。
- [x] 复用五大代码的真实路径，明确旧仓生产经验不能冒充新平台集成验收。
- [x] 已有类与候选类分列，重复体系和候选裁撤条件明确。
- [x] 目标用法与当前可编译 API 明确区别；未编译/未运行/未实机验证如实写明。
- [ ] **待 FM-P0**：V4.9/V2.01/V6.4 大型 Excel 全 Sheet 类型去重、API 唯一 Owner 最终签署。
- [ ] **待真实实现**：全部 TFM 用例编码、Windows CI、完整 Studio 操作和必要的 HIL 证据。

## E.5 参考资料与适用顺序

1. 用户确认的**四种模块归并决议**和 CxVisual 总体架构核心原则。
2. `DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`；`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`；相关 02～04 的 F/M/P 权威类型与能力基线。
3. `DOCS/Architecture/CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md`；`CxVisual_运行时数据体系——完整对象规格与双角色使用指南_V3.0.md`；`CxVisual_配置体系——完整设计与验收 V2.4.md`；统一运行各分体系及 TestCenter V4.1。
4. 既有 `ConnectorModule V1.0` 详细规格（本地配套文件）、`DOCS/Codebases/01_ccxx.md`、`02_device_communicator.md`、`03_gcl3.md`，及已核对的 Vision/CCT 源码。
5. 最新 `master` 的真实 `.csproj`、源码、测试、Windows CI 和真实设备现场记录；源码与 CI 的客观事实始终优先于过期的实施进度文字。

---

## V1.1 最终摘要

**完整公共能力、最简使用体验、真实专业扩展、唯一工程与运行体系**。C# 开发者正常写函数/Driver/模型/服务，按需公开能力；工程编排用户在 Studio 配置、连接、构建、运行与观察；四种 Module 通过现有 MEM/MDS/MOL/MOC/MRK 协作。**Connector 解决接入，Compute 解决计算，Simulation 解决虚拟行为，Service 解决持续供给；其它能力归属四类或保留在现有机制，不再增列模块种类。Channel 自主维护异步 I/O 和子通道；Protocol 统一处理完整数据交互；通用任务由现有 F19/MRK 承担；成熟 C# 在目标 Owner 原位重构，不增加长期 Adapter。**

*当前交付为目标设计规格：不代表四模块已实现，不代表本次执行了 Windows 构建、CI 或实机验收。*
