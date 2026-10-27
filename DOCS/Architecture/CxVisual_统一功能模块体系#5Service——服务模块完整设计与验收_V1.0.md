# CxVisual 统一功能模块体系#5Service——服务模块完整设计与验收 V1.0

> **文档类型**：专业模块完整目标规格；**生成日期**：2026-10-26；**来源基线**：旧完整规格 V1.2（承接 V1.1 六篇十八章及附录）、五类裁决 V1.2、Connector 裁决 V1.3、公共交互 V1.4。  
> **性质**：完整目标设计与验收规范，不是源码已实现声明；代码事实须以最新 `master`、实际 Windows CI 和设备 HIL 为准。  
> **原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**  
> **范围**：长驻服务与请求响应。  
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

## 01. 服务模块的真实职责

**【裁决】Service 是长驻、内部、外部、请求响应/订阅/后台工作等服务能力的提供者**；它不等于每条 HTTP 连接或每个 Socket，也不拥有第二套通道/规约。HTTP/IPC 物理传输可以复用 Connector/Channel，业务路由/API、认证、请求结果及服务生命周期属于 Service。MRK 负责复杂流程/作业调度，MDS 负责数据，业务持久化归各专业 Owner。

| 形态 | 服务示例 | 边界/能力 |
|---|---|---|
| LocalService | AI 推理入口、专业算法复用、查询设备状态 | Scope、服务发现、授权、同步/异步请求 |
| BackgroundService | 数据归档、统计、日志汇总、持续订阅 | Start/Ready/Stop、负载与健康、可控取消 |
| ExternalService | MES/ERP/HTTP API、边缘通信、结果服务 | 网络鉴权、序列化、版本、超时、幂等、可靠交付 |
| LongJobService | CT 重建任务、导出、批量图像处理 | JobId/Progress/Cancel/Query/ArtifactRef |

## 02. 公共对象、API 与运行生命周期

| 角色 | 实现/使用方式 | 说明 |
|---|---|---|
| `IMLCServiceModule` | 服务模块正式公共接口 | 【源码已有】 |
| `MLCServiceModuleBase` | Ready/Faulted/Start/Stop 生命周期约束 | 【源码已有】，需核查实际实现与停止证据 |
| `MLCServiceModuleConfiguration` | 唯一 FCM Section 配置 | 【源码已有】 |
| 服务 Capability | 请求 Schema/响应 Schema/并发/版本/权限/幂等 | 使用 MOC 既有目录，不建独立 ServiceRegistry 真源 |
| 业务 Handler | Query/Submit/Cancel/Subscribe | 专业服务提供者实现，错误不可默默吞掉 |

长服务应建立 `Created→Starting→Ready→Degraded/Faulted→Stopping→Stopped` 的可观测状态，Ready 不是进程已启动，也不是端口已经 Bind；依赖项、数据/设备/模型是否可用需明确。Stop 后不能继续接受新工作；Drain/取消/资源释放有截止期，不能无界等待。没有业务持久化恢复证据时不承诺跨进程 exactly-once。

## 03. AI/CT 与 MES 的使用场景

AI Serving：Service 暴露 RunInference 接口，Compute/专业 Owner 执行实际推理；支持同步小任务和 Job 形式，输入 ImageRef/元数据，输出 DefectResult/版本/Trace；不得将大图像嵌进日志或普通消息。CT Service 类似，提交 Reconstruction Job 并查进度，返回 VolumeRef/ArtifactRef。MES 报告服务接收 InspectionFinished 后必须保证授权、SN/Batch/Station 幂等、失败补传、结果 ACK 含业务语义，而不把 HTTP 200 当业务接受。

## 04. 安全、可靠性与服务性能

认证、授权、端点隔离、请求校验、限流、队列容量、最大 Body、超时/取消、重试策略、幂等键、敏感参数脱敏、TraceId/CorrelationId 是最低治理要求。进程断线/重启后任务必须有真实恢复依据；服务并发不能阻塞真实采图与设备控制；远程服务故障不能成为现场机台的单点依赖。

## 05. 双角色与反例

开发者仅提供业务 Handle 和配置，平台复用 MLC 生命周期与统一诊断；Studio 用户按 Capability 搜索服务、设置请求、查看健康、部署/配置版本和实际应答。空实现不准宣称 Ready；一个服务依赖失败不得返回 Success=true；重复 MES 报告应在业务幂等策略下明确处理；无权限远程控制必须由目标 Runtime 拒绝。

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
| SV-P01 | 服务启动与实际 Ready | 依赖都可用才 Ready，缺 Provider 必须 Degraded/Failed |
| SV-P02 | 高并发/请求过载 | 可配置容量、隔离、限流/背压和资源上限 |
| SV-P03 | 远程鉴权/错误 Schema/恶意过大 Body | 非法请求拒绝，不泄露数据或绕过权限 |
| SV-P04 | 重复/失败 MES 结果上传 | 幂等键、业务确认、重试/对账有明确结果 |
| SV-P05 | 服务故障/进程崩溃/重启 | 任务 Unknown/Resume 合法，不伪造完成 |
| SV-P06 | CT 长作业与大体数据 | JobId/Progress/ArtifactRef 正确，内存预算受控 |
| SV-P07 | Stop/Drain/Dispose | 不再接新工作，截止期内取消或明确遗留 |

## 与总体规范的关系及修改门禁

- **共性模型、两角色、版本、能力发现、统一交互、总体测试**以 `CxVisual_统一功能模块体系#0总体架构——完整设计与验收_V2.0.md` 为唯一解释。
- 本文是该模块专有行为与验收的权威文件；遇到旧源稿的相反描述，必须按文首覆盖表处理，并在文档里保留来源追溯。
- 任何新增正式接口/类名前须检查 `DOCS/02～04`、最新源码、现有 Owner、`ReferTo`；不能用概念示意代码替代编译测试。修改后同步更新总体文档的能力归属表与本模块 TestCenter 专项验证。


## 原 V1.2 来源条款、需求与案例（逐节承接）

> **来源历史提示**：下列原文保留其需求编号、专业能力和案例，便于追踪。凡涉及旧 `Advanced` 多组、历史“相机 SDK Connector”、未查重 API/状态的文字均只是历史上下文；**本文件前部的现行裁决优先**。

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

### CASE-FM-E：AI 检测服务和重复调用

**目标**：`Compute(Inference)` 通过 `Service(AIInference)` 对多个检测流程提供长期模型服务。Developer 使用单一推理函数合同、受控模型加载/GPU 池，Service 对内公开同步/异步接口和请求队列。Studio 选择模型版本、配置单/批输入、查看队列/就绪状态/显存/结果与错误。

**测试条件**：10 并发请求、两个模型版本、重启/模型替换时请求固定版本；参数非法、依赖模型缺失、显存溢出、调用者取消、重复请求幂等。一个 Client 超时绝不在另一个 SN 上提交结果；没有真正加载模型时服务不得报告 Ready。

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


## 2026-10-27 专项深化：服务能力终态、完整需求与工业运维验收

> **状态：【目标规范增补】**。继承 UM-SV01～SV10，扩充 SV-Rxx 与 SV-Axx；Service 是**持续能力提供者与服务生命周期**，不是另一套 Socket、消息中心、命令中心、流程执行器或独立的 MRK Job Manager。能够通过接口直接调用的单个算法仍是 Compute；只有需要持续对外提供/管理请求、状态、资源、会话或依赖时才成为 Service。

### SV-1 终态目标与双角色使用期望

支持**进程内、独立进程、跨 PC**三种运行/部署方式，向业务系统、工程界面、工业设备和专业计算提供稳定的类型化服务能力，包括状态/数据查询、结果聚合、AI/CT 算法服务、长期任务提交、MES/ERP 数据交互入口、设备状态/监控及可选专业 API。对外技术可以是 HTTP(S)、IPC、RPC 或已装配的 Connector/Protocol，但**不强制每个 Service 创建 Channel**，不另造业务数据 Owner。

- **软件开发者**：实现简单的请求处理或长期专业 Provider，声明服务 Operation/输入输出契约、依赖、生命周期和健康检查；平台协助完成标准装配、限流/追踪/鉴权。相同业务能力在本地代码与服务入口复用，不复制两份算法实现。
- **工程用户**：Studio 查看服务列表、Capability、绑定来源/目标、依赖/端点、运行状态/健康、配置、访问权限、并发和指标；可启动/停止、测试调用、查看请求/错误和 Trace；离线时给出具体依赖原因。
- **运维人员**：能部署、启动、重启、关闭、版本升级和回滚服务配置；区分 ProcessAlive、Liveness、Readiness、Degraded 和 Faulted；服务实际 Ready 必须来自依赖就绪及健康验证，不靠进程存在推断。

### SV-2 完整需求基线

| ID | 能力域 | 期望与必须满足的规则 |
|---|---|---|
| SV-R01 | 服务分类与 Provider | 进程内同步/异步服务、长驻实例、独立进程与远程服务、纯查询/有副作用服务；能力与部署方式正交，不强制接口全部网络化。 |
| SV-R02 | 身份、能力与发现 | ServiceId/InstanceId、Scope/Generation、CapabilityId/版本、Operation 输入输出/错误/是否幂等/权限/成本与资源要求；经现有 MFM/MOC 注册而非第二套全局服务目录。 |
| SV-R03 | 唯一配置 | ProviderTypeId、DependencyRefs、Startup/ShutdownTimeout、MaxConcurrentRequests、部署/监听/连接参数由专业配置继承与现有 FCM 保存；升级需显式迁移/兼容性校验。 |
| SV-R04 | 生命周期 | Created/Starting/Ready/Degraded/Stopping/Stopped/Faulted；启动检查依赖、能力和资源；启动失败不假 Ready；反复 Start/Stop、异常关闭及回收可追踪。 |
| SV-R05 | 请求/响应模型 | Query/Invoke/Submit/Cancel/GetStatus/GetResult/Subscribe 取其有意义的部分；RequestId、CorrelationId、Caller、Deadline、SchemaVersion、结果和错误码一致；发送成功与业务结果分开。 |
| SV-R06 | 长作业支持 | AI 推理批量、CT 重建、数据导出等受理后返回可信 Ticket/OperationId；进度、取消、超时、恢复和结果定位复用 MRK/专业 Owner，服务不得自造第二套 Job 真源。 |
| SV-R07 | 并发、队列、流控 | 最大并发、有界等待队列、内存预算、限流/背压、公平性、请求与流的隔离；过载有显式 Rejected/RetryAfter 或等价语义，不无限排队。 |
| SV-R08 | 服务依赖与资源管理 | 引用 Compute/GPU/Native/数据 Owner/Connector/其他 Service 的正式资源；依赖就绪才 Ready，失效时进入 Degraded/Unready，恢复后明确重建 Session/能力。 |
| SV-R09 | 统一数据与四大交互 | Variable 查询/订阅、MDS DataRef 或受控跨域传输、M36 消息与 M12 命令调用共用正式 Owner；跨 PC 使用 Bridge/Connector，不在 Service 私有复制数据堆。 |
| SV-R10 | 安全与授权 | 调用者身份、租户/工程 Scope、服务 Operation 级权限、审计、证书/密钥配置、安全错误处理；权限检查发生在执行目标且每次调用核验；X-Ray/运动等危险动作不得绕过 M12。 |
| SV-R11 | 幂等、重试与对账 | 明确读操作和有副作用操作的幂等性；MES 上传/命令任务使用稳定 IdempotencyKey、业务确认和查询/对账；超时 Unknown 不盲目重试不可逆操作。 |
| SV-R12 | 协议与版本兼容 | Schema 前后兼容规则、API/Capability 版本、输入输出校验、可选字段默认/弃用告警；不兼容版本拒绝执行，不悄悄解释成另一条操作。 |
| SV-R13 | 大数据与流结果 | 图像/体积/Projection 通过授权 Artifact/DataRef、分块流、共享内存/文件等合法交付，带大小/校验/生命周期；不得把 8 GiB 体积塞入一次 JSON 或 Message。 |
| SV-R14 | 连接与外部访问 | HTTP/IPC/WebSocket 等选择已实现且验证的 Channel/Protocol 或专业服务底座；REST/SOAP 等是协议/服务接口语义，Service 不重建同义网络传输。 |
| SV-R15 | 可观测与健康检查 | Liveness、Readiness、Degraded 原因、队列深度、请求延迟分布、成功/错误/取消、外部依赖、资源内存/GPU、Trace/OperationId；日志隔离敏感负载。 |
| SV-R16 | 优雅关闭与在途工作 | Stop 首先拒新请求，再按 Deadline 排空/取消；无法安全取消的命令/长作业标明 Unknown/仍在处理、保留查询入口，最后回收资源。 |
| SV-R17 | 故障恢复、部署与升级 | 进程崩溃/网络分区/服务重启后的 Unknown/恢复/重放区分；配置、二进制、模型、API 版本和迁移记录；运行中的旧请求沿用锁定契约。 |
| SV-R18 | 高可用与多实例 | 明确可无状态扩展与只能单 Owner 的工作；多实例隔离、请求一致性、幂等/租约边界与故障切换条件；不得因多副本并行重复下发物理控制命令。 |
| SV-R19 | 专业服务能力 | AI 推理/模型池、CT 重建 Job、结果查询、图像/体积轻量浏览、检测结果/MES 上报、监测统计/报告数据等，都复用 Compute/MDS/Driver Owner。 |
| SV-R20 | 使用体验与测试 | SDK 最小 Service Handler、Studio 配置/依赖关系、测试调用、权限和运行指标面板、虚实来源标识与可读错误；无 UI 的 Windows/Host 集成测试与 UIA 分别验收。 |
| SV-R21 | 环境与隔离 | Windows/.NET48 主目标，跨进程/跨 PC 按能力声明实际可用性；不能把 WPF 页面与 SDK 绑定为服务运行必需依赖；多工程 Scope 不越权。 |
| SV-R22 | 长稳与 SLA | 对每种服务明确基准并发、延迟分位、错误预算、恢复目标、峰值内存、持续运行及依赖条件；性能/可用性需要负载和现场实测，不凭配置值宣称达成。 |

### SV-3 服务类型、操作与结果接口的归属

| 能力/对象 | 当前已有/应直接复用 | 必须避免 |
|---|---|---|
| 公共模块描述 | \`MFMModuleDescriptor\`、\`MFMModuleKind.Service\`、FCM 唯一配置 | 第二个服务类型枚举和独立能力目录 |
| 实例生命周期 | \`IMLCServiceModule\`、\`MLCServiceModuleBase\`（\`StartAsync/StopAsync/IsReady\`）、\`MLCServiceModuleConfiguration\` | 将基本 Running 当作所有依赖真实 Ready |
| 操作能力 | MOC 能力/端口、MRK 执行与 Operation、专业 Compute/Driver Handler | 第二套 ServiceOperationRegistry/JobManager |
| 数据访问 | M09/MDS/M36/M12 + 现有 Bridge/Connector 的合法跨域路径 | 直接传跨进程指针或把命令当发布消息 |
| 运行宿主 | 现有 Runtime.Host/Lifecycle 的资源与协调机制 | 再创建第二个统一 Host 或复制工程编译/调度 |

**目标接口职责**（名称仅作查重清单）：\`IServiceOperationProvider.DescribeOperations\`、\`IServiceRequestHandler.HandleAsync\`、\`IServiceReadinessProbe.CheckAsync\`、\`IServiceRequestAdmission.TryAccept\`、\`IServiceOperationStatus.Query\`。普通无状态服务不应强迫实现所有可选接口；Service 可将异步请求提交给真实 MRK Operation。实际命令执行授权在 M12/Target Runtime；请求受理结果与现场动作完成结果不可混淆。

当前 \`MLCServiceModuleBase\` 已实现基本 Start/Stop/Faulted 状态和抽象专业 Start/Stop 方法，但**不表示健康依赖、Drain、重试、HA、认证或多机服务已完成**。应先实现一个具备真依赖、真实结果和负面测试的进程内 Service，再扩展独立进程和外部服务。

### SV-4 工业标杆场景与完整流转

1. **AI 在线检测服务**：Service 管模型池就绪/并发请求/Batch 和 API，Compute 真正执行推理；请求带 ImageRef、ModelVersion、SN/RunId；结果由 MDS Owner 管理，服务只返回结构化 DefectResult/ResultRef；模型加载失败不 Ready，10 并发不能混工件结果。
2. **CT 重建服务**：Submit 投影与几何快照 → MRK/Compute 正式作业 → Progress/Status → Result VolumeRef；服务超时只表示调用超时，不能表示重建已取消；进程重启后查询恢复/Unknown 必须对账。
3. **MES/ERP 结果集成**：Service 对内提供查询/上报能力，Connector/Protocol 负责实际外部规约；失败/网络分区记录业务确认与 IdempotencyKey，避免重复上传/错误 SN 回写。
4. **设备运行状态服务**：读取 M09/Driver 正式状态、历史结果与健康，聚合提供本地或远程接口；读接口不能借查询身份执行 Device.Reset、运动或曝光。

### SV-5 端到端专项验收（SV-A01～A12）

| ID | 验收场景与反证 | 层次 |
|---|---|---|
| SV-A01 | 无网络进程内服务可启动、调用/Stop；依赖缺失时 IsReady 不可误判 | G1 |
| SV-A02 | 参数/Schema 错、未授权、跨 Scope 均拒绝并返回稳定错误码 | G1/G2 |
| SV-A03 | 10/100 并发、超载、队列/内存预算、限流规则有实测与拒绝证据 | G1/G2 |
| SV-A04 | 同一 IdempotencyKey 重复请求不重复提交 MES 或危险动作 | G1/G2 |
| SV-A05 | 断网/超时/取消时，RequestAccepted、JobExecuting、Confirmed/Unknown 分离 | G1/G2 |
| SV-A06 | AI 模型热更新、两个版本、GPU OOM 与多请求数据/Scope 不串扰 | G1/G2 |
| SV-A07 | CT 超大 Volume 只传合法引用/流，Lease、大小、校验、取消可见 | G1/G2 |
| SV-A08 | 进程崩溃、重启、未结 Job 和恢复策略不伪造完成 | G1/G2 |
| SV-A09 | Stop 拒新请求、排空/合法取消在途、释放外部资源 | G1 |
| SV-A10 | Studio 可发现真实 Operation、配置服务、发测试请求并查看状态/错误/指标 | G1 |
| SV-A11 | 单/多实例服务发现与授权、版本兼容及更新回滚留存审计 | G1/G2 |
| SV-A12 | SLA/错误率/延迟、资源、恢复时间及长期运行结果满足项目指标 | G3 |

**实现顺序**：P0 进程内服务/生命周期/能力及真实请求结果；P1 有界并发、Readiness、长期 Job 和标准诊断；P2 外部 API/IPC、多实例、幂等对账与升级；P3 长稳/安全/真实 MES/CT HIL。各阶段均以运行日志、性能报告和 Windows CI/HIL 为证据。


## 文档维护与验收工作包

- 主仓库：`https://github.com/oudream/cx-visual-x3`。
- 先看 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00`；接口归位以 `DOCS/01～04` 为准。
- 新代码必须核实真实 Owner/源码、查看 `ReferTo` 原始成熟代码，**可直接重构融入，不加永久 Legacy/Porting Adapter**；每轮一个主题性中文提交。
- 验收分别记录 **G0 设计/类型、G1 Windows 编译与自动测试、G2 真实工业 I/O/HIL、G3 长期现场稳定与安全证据**。G0/G1 不能推导 G2/G3。

**待执行工作包**：逐类对象归属核查 → 现有源码能力差距 → 最小变更开发 → 单测/ArchitectureSmoke → Windows CI → TestCenter 双角色操作 → 设备/长稳证据 → 更新文档版本与工作台账。
