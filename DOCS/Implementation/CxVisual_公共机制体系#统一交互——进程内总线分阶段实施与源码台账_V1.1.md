# CxVisual 公共机制体系#统一交互——进程内总线分阶段实施与源码台账 V1.1

> **性质**：可连续执行的 P0 实施与核实台账；不代表计划项已实现。  
> 修订：2026-10-24；本版完成文档审查，尚未运行新测试。
> 日期：2026-10-24；初始仓库核对 SHA：`36855f3fade9b70ecb7c01cbdbb3e37264802926`。  
> 唯一主规格：`../Architecture/CxVisual_公共机制体系#统一交互——四大能力与双层总线完整设计及验收_V1.2.md`。  
> 配套：`../Testing/CxVisual_公共机制体系#统一交互——TestCenter与Windows验收矩阵_V1.1.md`。  
> **声明**：本轮只编写文档；没有编辑、构建或提交 cx-visual-x3 源码，也未运行 Windows CI、TestCenter 或实机测试。

## 1. 实施总原则

- 进程内先做，MQTT、SHM、文件服务全部属于后续 E 扩展；P0 不能依赖它们。
- **先现状核对，再增构**：重用 Foundation / Mechanism / Professional 已有类型、MDS/MPA/MRK、`ReferTo/`、`gcl3`、`device_communicator` 成熟经验；不以迁移旧代码为由恢复静态全局中心。
- 28 项目既有边界保持；.NET Framework 4.8，WPF，Windows x64；若仅改源码无须创建新项目。
- 真源是对应 Manager/Owner；Address Registry 只定位端点，不能持有第二值副本或执行命令。
- 一轮一个可核验的主题，少量中文 Commit；不写未执行的测试状态；与其它 CxVisual 同时开发工作流协调 master HEAD。
- **每个工作包必须达到** API 可调用 + 正反例 + Windows CI + 必要的 Studio 实际操作，再把它从“源码存在”提升为“本阶段验收”。

## 2. 现有代码基线和未完成差距

| 领域 | 现有实际源码 | 有什么 | P0 具体差距 | 初始状态 |
|---|---|---|---|---|
| M09 | `Mechanism/Data/Variables/MDSVariableRegistry.cs` | ScopeId 管理变量表 | 双地址接入；授权/生命周期查验 | I |
| M09 | `Mechanism/Data/Variables/MDSVariableTable.cs` | Define/Update/Get/Sample/Quality/VariableChanged | Default、Previous、Source/Writer、Revision；原子快照；回调隔离、批量 | I + P |
| M09 | `Mechanism/Data/Variables/MDSVariableContracts.cs` | Sample、IMDSVariableTable | 快照扩展与兼容迁移 | I + P |
| MDS | `Mechanism/Data/RuntimeData/MDSDataHeap.cs`、`MDSDataHeap.Query.cs` | Set/Get/Replace/Remove、元信息查询 | 受控发布元信息、共享权限、Stream 桥接 | I + P |
| MPA | `Mechanism/Connectivity/Protocols/Mapping/MPADataMapping.cs` | Point 到 6 类 TargetKind，Read/Write/Publish/Invoke | 调用真实 Message/Command Owner，错误与反馈类型扩展 | I + P |
| MDS↔MPA | `Mechanism/Data/Variables/MDSProtocolDataTargetAccess.cs` | Variable/Heap/Stack 访问；Publish/Invoke 可注入 | 真 Owner 装配、授权/结果传播 | I + P |
| MRK | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | OperationTracker、CorrelationWaiter、ResourceLease | 接入统一命令；非独立 CommandBus | I + P |
| MEM/MOL | `Mechanism/Core/Engineering/MPCPort.cs` | Data/Control/Event/Command/State/Stream 端口类型 | Studio 利用已有类型和作用域 | I + P |
| 通道 | `Mechanism/Connectivity/` | Channel/Protocol/PointTable/Mapping，部分 Modbus 03/06 | P0 不改 Protocol 语义，仅接口对接 | I |
| 数字寻址 | `ReferTo/DeviceVariable/DeviceSuite/Core/Snowflake.cs` | 旧 Snowflake 生成算法 | 多进程碰撞/时钟回拨/类型位与现有 ID 冲突审核 | 仅 ReferTo |
| 消息 | `ReferTo/DeviceVariable/MessageInterfaces/MessageCenter.cs` | 旧消息发布参考 | 正式 Owner、Scope/Subscription/队列/投递结果 | 仅 ReferTo + P |
| 分布式经验 | `DOCS/Codebases/03_gcl3.md` | GeneralAccess/PSM/Bus/SHM 等历史 | 仅参考，不要求 P0 重建远端服务 | 参考 |

**未核实边界**：`DOCS/02～04` 具体 Excel 工作表、本领域全部生产提交/CI 最新历史和所有 UI 对应类型。本台账不假称已经审查它们。

## 3. P0-1：地址与 Registry

**目标**：同进程所有公开能力都可通过字符路径或雪花数字码访问同一个端点，并支持容器/机器、App、Module 分层索引。

实施步骤：

1. 核对 MEM 的 `MRIObjectAddress/ResourceRef`、MPC PortId、Config 标识及 DOCS/04；保留现有工程 GUID。**持久定义端点 Snowflake ID 首次生成即随正式定义保存，运行实例端点 Snowflake ID 仅随 Host 运行生命周期存在；分配规则先审核再编码**。
2. 读取 `ReferTo/.../Snowflake.cs` 全部算法，处理它把 DataType 当 datacenter 位、WorkerId 默认为 0 的风险；采用真实唯一 Worker/Node 分配，针对时间回拨与毫秒内序号耗尽做测试。
3. 在既有合适 Owner 内完成路径规范化、作用域封闭、路径/数字查询、冲突拒绝、别名/重命名和生命周期注销；**不建第二资源库**。
4. 统一 Registry 支持“按 Id 精确”和“按 Scope/AppCode/ModuleCode/Kind 枚举可公开对象”；查询结果返回 Owner 的可调用契约或安全句柄，不直接暴露内部字典。
5. 与现有 MDSVariableRegistry 做最小桥接；不要求变量值先序列化。

**必要反例**：同 Path 不同 Scope、同 Scope 重复 Path、同 NumericId 不同 Owner、非法路径段、时间倒退、多线程发号、旧实例注销之后迟到消息、保存重开路径变更、JSON 64 位数字精度。

**验收**：BUS-ID-01～10；状态：**待开发、未测试**。

## 4. P0-2：高速变量与源追踪

**目标**：Current/Previous/Default、Source/Writer、Quality、Revision 的原子可读快照，在高频写入下不被慢订阅者阻塞。

步骤：

1. 对 `MDSVariableDefinition/MDSVariableTable/MDSVariableSample` 做兼容性审查；先按主规格确定快照模型，旧 `Get<T>/Update<T>/VariableChanged` 不得突然失效。
2. 将 SourceId、WriterId、采集/提交时间、Revision 整体写入 Entry；锁内只做验证与提交，不执行用户订阅回调。
3. 定义初始默认值/旧值含义：声明默认值≠现场 Good；**每次成功提交 Previous=提交前的 Current，Revision 随对外可见状态提交推进；同值提交仍是 Committed 但非 ValueChanged**。同步回调虽在现有表锁外，仍会阻塞提交线程，必须做通知隔离。
4. 按值、质量、来源、提交四类事件通知，支持必要批量提交、快照读取；在已有线程机制上隔离慢观察者与异常处理。
5. 专业 Connector 通过 `MDSProtocolDataTargetAccess/MPADataMapping` 写到权威变量表；来源来自实际 Point/Device，Writer 是 Connector；多来源竞争有 Owner 规则。若接口签名扩展 Source/Writer 元数据，必须先与既有 `IMPADataTargetAccess` 的调用者完成兼容迁移，不得悄悄丢掉来源。

**验收**：BUS-VAR-01～13，BUS-NFR-01～03；状态：**基础类已存在，完整功能待开发/未测试**。

## 5. P0-3：轻量消息和层级订阅

**目标**：进程内按 Container/Machine → AppCode → ModuleCode → Endpoint 订阅；轻量发布可返回“本地受理/未受理”而不冒充业务完成。

步骤：

1. 先核对 DOCS/04 和旧 `MessageCenter`，选择现有 M36 Owner 或唯一合适机制落点，不新建一个 `UniversalEventBus`；与 M09 原生变化事件、M12 事件身份对接。
2. 定义小消息包络、MessageId、Source、Topic/Target、Schema、PayloadRef；大型对象被拒绝或转为引用。
3. 实现实例作用域订阅、精确/层级路径匹配、解除订阅及 SubscriberId；匹配结果再做权限检查，避免“订阅=授权”。Subscribe+Snapshot+Revision 水位需无缝衔接；取消订阅后新回调不可再排入，已在执行的回调可依契约结束。
4. Publish 返回 `SendResult(Stage,Outcome,Reason,MatchedCount)`，不混用阶段与失败状态；可选 Delivery ACK 必须有接收方证据。零订阅默认允许发布并标识 MatchedCount=0，声明要求接收者时才拒绝。
5. 采用有界队列、消费异常隔离、取消、可能的单分区排序策略；复用已有 F19，绝不建立第二调度内核。
6. `MDSProtocolDataTargetAccess.PublishAsync` 接真实 Owner，不可保持 dummy 成功；测试未装配时 NotSupported 行为。

**验收**：BUS-MSG-01～14、BUS-NFR-04～05；状态：**待开发/未测试**。

## 6. P0-4：受控命令

**目标**：真实 Handler 注册与执行，Invoke/Cancel/Query、授权与资源仲裁、消息发送和执行结果分离。

步骤：

1. 核对 M12 公共 Command/Event 定义、M28 鉴权接口、MRK 运行入口、专业 Provider 的现有契约和 DOCS/04。
2. 定义 RequestId（请求关联）、OperationId（副作用身份）、Caller/Target、入参/结果 Schema、Deadline/CancelPolicy、是否需要现场确认。
3. Handler 由专业模块注册并声明资源占用；接收方做最终鉴权，未知 Handler/未授权/参数不兼容直接拒绝。
4. MRK 复用 `MRKExternalOperationTracker`、`MRKCorrelationWaiter`、`MRKResourceLeaseCoordinator`；必要时扩展，而不是新建另一本 Operation 表。
5. 分别暴露 SendResult、ExecutionResult 和可选现场 ConfirmationResult。特别标明现有 MRK.Confirmed 只是 Provider 的确定结论，不是自动证明设备到位。超时 Unknown 保留后续查询；取消仅为请求取消；OperationId 重入核验 Caller/Target/参数摘要。
6. `MDSProtocolDataTargetAccess.InvokeAsync` 接真实授权 Handler，验证无授权和无 Owner 均报错，不创建新 Shadow Dispatcher。

**验收**：BUS-CMD-01～16；状态：**MRK 基础存在，完整命令闭环待开发/未测试**。

## 7. P0-5：四大机制组合与 Data 协作

**目标**：DataHeap 不复制到消息层，变量、消息、命令可对其引用和组合，保留唯一数据 Owner。

步骤：

1. 使用现有 Heap/Stack/DataContext `Get/Set/Return/Move/Copy/Inspect` 与专业 Image Adapter；保护可变数组的安全捕获。
2. 统一公开“数据可用/数据改变”的轻量元信息通知；禁止消息附带完整 4300×4300 Image。
3. Command 参数携带**同进程、Owner/Run/Generation 限定**的数据定位契约，执行中从真实 Owner 取得安全数据；Owner 释放后不允许继续使用过期引用。不可把尚不存在的公开 `MDSDataRef/MDSDataLease` 视为已有调用入口。
4. TestCenter 验证至少两消费者读取同一不变数据；需要修改者显式复制；关闭/异常能释放持有。
5. 实现或验证 Heap **支持类型的序列化与反序列化往返**：限定条目/TypeId/SchemaVersion/来源/文件定位、校验及恢复时的原子提交与失败回滚；未知 Codec/外部设备资源明确不可恢复，禁止把元信息 Inspect 当整个 Heap 的 Serialize。
6. 对外部 SDK/GPU、SHM 场景只设计契约边界，P0 不冒充它们已支持零拷贝。

**验收**：BUS-DAT-01～12、BUS-NFR-06；状态：**MDS 基础存在，跨机制目标待开发/未测试**。

## 8. P0-6：Studio、TestCenter、Windows CI

目标：使工程编排者可在 Studio 的既有工作区编辑范围订阅、查看变量的完整快照、发命令并看到独立阶段结果，在数据面板安全查看元信息与专业 Viewer。

- 所有 UI 使用现有 WPF Studio/FCM/MOL/MEM 事实，避免第二套工程持久定义。
- 真实 UI 操作：**Studio 内嵌同进程测试宿主**的机器/容器→App→Module→变量；按范围订阅；命令执行显示 Accepted/Unknown/Completed；数据只显示元信息。独立 Studio 到 Runtime.Host 的访问属于 P1，不属于 P0。
- 保存重开：工程配置/订阅定义和持久工程引用保留；临时 SubscriptionToken/RunId/运行端点ID/镜像不当工程定义，重新加载必须验证 Scope、Generation 并重新订阅取快照。
- Windows CI：至少在 `CxVisual.Unified.sln` 既有 x64 工作流中运行相关 Smoke、TestCenter（WPF STA）与集成示例。Linux 只可做静态/非 WPF 检查。
- 各结果记录 run URL、commit SHA、步骤日志、Pass/Fail、异常反证和未测设备。

**验收**：BUS-UI-01～09、BUS-NFR-07～08；状态：**待开发/未测试**。

## 9. P1/P2/P3 只留技术扩展门

| 阶段 | 扩展业务 | 必须有的独立证据 | 不做事项 |
|---|---|---|---|
| P1 | Windows 两进程 IPC/SHM | 双 PID、生成代次、进程崩溃/重启、数据一致性 | 不以同进程共享模拟 IPC |
| P2 | MQTT / TCP/TLS 跨 PC | 实际 Broker、授权、断线、订阅重建、命令查询 | 不因 MQTT QoS 误称实际命令完成 |
| P3 | 大文件/Volume 同步 | 真实两机、分块 Hash/Size、断点恢复、内存峰值 | 不复制整个 CT Volume 到消息队列 |

技术选型（Broker、IPC 编码、分块大小、文件服务部署方式）根据工作包测量再定；不要现在新建大而全的外部服务。

## 10. 每轮开发现场记录模板

```text
日期：
工作包/范围：
起始master HEAD：
受影响Owner及csproj：
已有同义类及ReferTo复核：
本轮增/改源码路径：
新增/修改Contract/Schema及兼容风险：
测试编号：
实际运行命令/运行平台：
测试结果/Windows CI链接：
已验证行为：
尚未验证行为（含实机、跨PC）：
中文Git提交SHA：
遗留问题/下轮动作：
```

## 11. 当前初始台账（生成日期 2026-10-24）

| 工作包 | 设计现状 | 代码事实 | 编译/CI事实（本轮） | TestCenter事实（本轮） | 结论 |
|---|---|---|---|---|---|
| P0-1 | 本文第10/11章 | ReferTo Snowflake，主仓双地址服务未确认 | 未执行 | 未执行 | 待开发/复核 |
| P0-2 | 第5章 | MDSVariableTable 基础存在 | 未执行 | 未执行 | 需补 Previous/Default/Source/Revision |
| P0-3 | 第6章 | MPA Message 目标入口存在；正式完整 Owner 未确认 | 未执行 | 未执行 | 待开发 |
| P0-4 | 第7章 | MRK Operation/Correlation 部分存在；正式 Handler 闭环未确认 | 未执行 | 未执行 | 待开发 |
| P0-5 | 第8章 | Heap/Stack 基础存在 | 未执行 | 未执行 | 需组合闭环 |
| P0-6 | 第18/20章 | 现有 Studio、TestCenter 是扩展基础；独立 Runtime.Host 需 IPC | 未执行 | 未执行 | P0 仅验同进程宿主 UI，远端连接延期 |
| P1～P3 | 已有边界设计 | 未逐模块验证 | 未执行 | 未执行 | 延后，不得标完成 |

**结论**：这份台账是真实开发的起点，不是实施完成表。任何新版本完成声明必须由实际源码和 Windows CI/设备证据共同支撑。


## 12. V1.1 审查后新增加的实施前置核查

- **地址**：定义类 ID 与运行端点 ID 的长期稳定性和迁移规则，并与工程 `MRIObjectAddress` 交叉校验。
- **订阅**：SubscriberId、异步队列、Snapshot→Sequence 握手、注销并发；保证回调不直接阻塞 M09 写入线程。
- **发送**：Stage 与 Outcome 分字段；零订阅策略、接收应用 ACK 的证据来源。
- **命令**：MRK Confirmed/现场反读隔离；重复 OperationId 的调用者/参数/目标一致性。
- **数据**：MDS.DataSnapshot.Source 的现有文件来源与拟议 SourceId 不混用；先通过 Owner Scoped Locator 测通 Image 安全访问。
- **测试**：新增 BUS-ID-09～10、VAR-11～13、MSG-11～14、CMD-13～16、DAT-09～12、UI-08～09，共19条候选，未运行。
- **未决且不得推定已核实**：02～04 Excel 的逐 Sheet Owner 对照；Windows CI、新代码编译、实机性能、独立 Studio↔Runtime.Host IPC。
