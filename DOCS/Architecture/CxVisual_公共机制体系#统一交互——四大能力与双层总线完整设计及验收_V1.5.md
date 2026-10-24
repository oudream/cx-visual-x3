# CxVisual 公共机制体系#统一交互——四大能力与双层总线完整设计及验收 V1.5

> **2026-10-24 新Owner裁决**：命令相关代码正式收敛至 `Foundation.Data.Commands`（FDC参数/定义/契约）与 `Mechanism.Runtime.Commands`（MRK中心/执行/MDS适配）两个已有项目；历史M12/MCE归属不再是当前实现。详见 `DOCS/Decisions/CxVisual_统一命令中心双项目归属裁决_V1.0.md`。

> **状态：架构决议稿（本轮对话确定的方向）；不是代码实现报告**  
> 修订：2026-10-24；V1.4 在轻量消息/命令闭环上补齐 OO/GoF/AOP、41个对象职责角色、管理者、流转与生命周期；未执行新增测试。
> 日期：2026-10-24  
> 主仓库：`https://github.com/oudream/cx-visual-x3`；核对基线 `master@36855f3fade9b70ecb7c01cbdbb3e37264802926`。  
> 权威约束：`AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`、`DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`；目标对象还须与 DOCS/02～04 Excel 逐项核定（本版未完整读取其工作表，**不声称已完成逐类校验**）。  
> 同步维护：`DOCS/Implementation/CxVisual_公共机制体系#统一交互——进程内总线分阶段实施与源码台账_V1.3.md`；`DOCS/Testing/CxVisual_公共机制体系#统一交互——TestCenter与Windows验收矩阵_V1.3.md`。

## 目录

**第一部分**：1 总体目标与职责；2 需求与场景；3 双角色期望；4 架构原则与体系边界。  
**第二部分**：5 高速变量/点表；6 轻量消息；7 受控命令；8 运行数据/大数据。  
**第三部分**：9 双层总线；10 双寻址；11 注册查询订阅；12 交互契约与分层反馈；13 并发隔离；14 来源、可靠性和恢复；15 外部物理总线。  
**第四部分**：16 对象与唯一归属；17 开发者使用；18 工程编排者使用。  
**第五部分**：19 分阶段实施；20 完整验收。  
**附录**：A 决议与禁止项；B 源码与 ReferTo 对照；C 功能—对象—测试矩阵；D 未决参数与变更规则。

---

# 第一部分　总体目标、需求与架构原则

## 1. 总体目标与职责

### 1.1 总目标

**“各方面都尽能力展现，便捷友好尽支撑各专业达到目标。”**

提供四种各自最擅长的通用交互能力：

| 能力 | 首要特性 | 核心动作 | Owner 责任 |
|---|---|---|---|
| **Variable / State（M09）** | **高速**、小值、高频、可观测 | Read/Write/Batch/Subscribe | 当前权威值、旧值、默认值、质量、来源、版本 |
| **Message / Event（M36 + M12事件契约）** | **轻巧**、异步、分发 | Publish/Subscribe | 消息身份、范围订阅、投递结果及必要的可靠交付 |
| **Command / Action（M12 + MRK）** | **做事**、有权限、有结果 | Register/Invoke/Cancel/Query | 目标执行者、请求/执行/确认状态、去重和资源保护 |
| **Data / Heap & Stack（MDS/M10）** | **大数据高效管理**、少复制 | Get/Set/Move/Copy/Load/Inspect | 数据所有权、生命周期、预算、文件/流/专业载体 |

“大数据”是 MDS 的强化优势，不是尺寸限制：小值、流程局部值、输出同样使用现有 MDS。图像连续帧/信号流具有**Stream**独立语义，不当作无限大的 Message 或 Heap 的“每帧持久对象”。

### 1.2 本文负责

1. 四大能力跨 Manager/Module/Workbench 的统一协作规则、共同身份、双寻址、作用域、分层订阅与共享门禁。
2. 进程内逻辑总线：**由各个已有管理中心、Registry、Provider、Handler 以及查询/推送/订阅形成**；无需强制独立 BusManager，也不强制所有 Get/Set 通过消息队列。
3. 进程外物理总线：后续桥接 MQTT、同机 IPC/共享内存、文件/流服务、按需 TCP/TLS；保证跨域能够沿用业务语义。
4. 区分发送、接收、执行、真实状态确认；规定异常、权限、背压、追踪和测试标准。
5. 与现有工程真源 MEM/MOL、构建 MOC、运行 MRK、数据 MDS、FCM 配置、M13/M14 Channel/Protocol、Studio/TestCenter 合作。

### 1.3 本文不负责

不自建第二套 VariableStore、Heap、FlowEngine、定时器、通信协议解析器、工程树、持久化 Graph；不为抽象而创建万能 UniversalEventBus；不把 MQTT Broker、文件复制进程、独立 SHM 守护进程作为本机运行的必备前提；不实现专业 Camera/PLC/Axis/CT 算法，不对 Windows 普通网络声称硬实时保证。

### 1.4 决议 / 现状 / 待验证标签

- **D = 本轮定案原则**；**I = 在核对 SHA 中发现真实源码**；**T = 发现对应测试入口，但不代表本轮已执行**；**P = 待开发或待代码核实**；**E = 扩展预留，本阶段不实现**。
- 所有“建议命名”的接口只供后续与 DOCS/02～04 核对，绝不视为已存在类型。

## 2. 需求场景与功能发挥

### SC-01 机台高速 PLC 采集

Driver/Connector 根据 Protocol 的 PointTable 接收寄存器；MPADataMapping 写入本作用域变量；M09 原子提交 Value/PreviousValue/SourceId/WriterId/Quality/Revision，HMI 仅订阅所需变化。批量变量值不能逐条转 MQTT 才能进入业务。断线时相应变量变 Stale，最后值可显示但须标注过期。

### SC-02 X-Ray 采图、AI 检测

Capture 命令由专业 Provider 执行；成像数据由专业数据 Owner 交给 MDS 或受控 Handle；采集完成 Event 携带 ImageRef/元信息；AI RunInspection 命令引用图像，MRK 处理 Operation、取消、结果；Result 写入专业结果管理；变量更新 Done/Busy/Quality。消息不复制整张图像。

### SC-03 CT 大体积和远程查看

8 GiB Volume 在本机应优先采用文件定位及按需加载的**已存在接口思路**；但当前没有 8 GiB 全链路性能与零拷贝验收证据，不能写成已验证的完成能力。后续 SHM/分块、ROI 流、跨 PC 文件服务按实际需求启用。远程仅暴露数据元信息和经授权的数据获取能力；同步、租约、校验、垃圾回收是后续扩展合同，不声称当前完成。

### SC-04 Studio 工程与诊断

工程用户按机器、AppCode、ModuleCode 范围订阅设备状态；操作按钮发受控 Command，Studio 将发送、接受、执行与现场确认分开呈现。**P0 只能验证 Studio 内嵌/同进程测试宿主上的真实机制，不得把 Studio 连接独立 Runtime.Host 写为进程内已验收；后者属于 P1+ IPC/Bridge。**断开、超时依各层语义呈 Unknown；UI 不直接调用厂商 SDK。

### SC-05 同进程多机台、多项目及多专业共存

相同名称 `Camera.Ready` 出现在两个运行实例，AddressScope 不同，不串值/事件。一个模块注销后，订阅及处理器释放；新实例可以用新 RunId 和新动态路由，不误接收上轮迟到请求。

### SC-06 外部物理总线故障

MQTT Broker 停机，不妨碍本地变量、命令、Heap、流程继续执行；受远程结果依赖的环节按 MRK 策略等待/失败/人工处置，**不能静默成功**；恢复后重新订阅、核实快照与增量缺口；远程命令超时不盲目重发。

## 3. 两类用户的期望

### 3.1 软件开发者

- 变量：定义一次，`Get/Update/Sample`，以后可选择提交来源和旧值快照；不必知道 MQTT/SHM。
- 消息：`Publish` 并能获知本地是否受理，订阅可按机器/App/Module 层级过滤，`Dispose` 退出；不必配置专门线程。
- 命令：注册专业处理者，`InvokeAsync` 得到操作标识与状态，`Query` 或等结果；同步/异步语义一致，安全与超时可观测。
- 数据：沿用现有 `data.Get/Set/Return` 和高级 Heap，必要时使用文件定位/类型适配；业务不显式操作引用计数器或网络分包。
- 可靠性：未配置物理 Bridge 时进程内依然全部可用；错误返回明确、可追踪到 Source/Target/Operation/Run。

### 3.2 工程编排用户

- 在 Studio 以**容器/机器 → AppCode → ModuleCode → 对象**定位；可按路径或数字 ID 查找。
- 可以选“变量读写”“订阅消息”“调用命令”“数据来源/输出”，高级设置才出现队列、背压、重试、远程暴露等。
- 实时面板同时查看当前值、旧值、默认值、来源、质量、版本；命令面板显示发送与执行结果不混淆。
- 可检查订阅者、发布者、命令响应与数据对象状态；取消订阅不会残留后台任务。
- 编辑态只操作工程定义；运行态对真实 Owner 进行授权操作；预览不得直接写 PLC。

## 4. 总体架构原则与职责分离

1. **各司其职**：State 追求快；Message 追求轻；Command 追求准；Data 追求大而高效。
2. **两层总线**：进程内逻辑总线由各 Manager/Provider/Registry 的同步和异步能力组成；进程外物理总线是按需桥接的实际传输机制。
3. **唯一真源**：每份权威 State/Data/Operation 归明确 Owner，缓存与订阅副本不争夺真值；共享读取与写授权不同。
4. **不强迫统一物理路线**：共享语义与身份，不强制 MQTT 传每个变量、Message 载整个 Image、Command 使用同一个事件队列。
5. **弱耦合**：专业 Workbench 只能使用稳定契约，不直接依赖另一专业内部类。遵从架构宪法的 Command/State/Event/Stream 分离。
6. **可组合**：Command 引用 DataHandle，Event 发出完成通知，Variable 对外呈现当前状态，MRK 跟踪 Operation；彼此不替代。
7. **简单部署**：P0 不使用外部 Broker、不需要独立服务进程；远程总线分阶段提供而非先造庞大的分布式系统。

示意：

```text
同一 Runtime.Host / Studio / Worker 进程
  M09 Variable ←→ 直接 Get/Batch/Subscribe
  M36 Message  ←→ Publish/Subscribe/Filter（轻量派发）
  M12 Command  ←→ Registry/Handler + MRK Operation/Resources
  MDS Data     ←→ Heap/Stack/Context/FileLocator/专业数据
        │
        └─【进程内逻辑总线】共同的 Address/Scope/Identity/Subscription/Diagnostics 契约
                               │（只有显式暴露才桥接）
                         ┌─────┴────┐
                    MQTT/IPC      SHM/FileSync/Stream/TCP
                      消息控制      大数据/进程间/远端访问
```

工程定义还是 MEM/MOL 的事实；MOC 构建的可执行计划与 MRK RunId 是运行层事实；逻辑总线不新增持久工程树或执行模型。

---

# 第二部分　四大公共能力的完整机制

## 5. 高速变量与点表（M09）

### 5.1 数据定义与运行态

定义：`VariableId / AddressPath / Type / Unit / DefaultValue / Writable / OwnerScope / InitialPolicy / AccessPolicy`。运行快照：`CurrentValue / PreviousValue / HasValue / Quality / SourceId / WriterId / ObservedUtc / CommittedUtc / Revision`。

- **DefaultValue** 为定义时声明的默认值；不代表当前已成功从设备采样。初始化策略（仅显示默认值、启动时写入默认值、使用持久缓存等）由定义声明，禁止偷把默认值当 Good 现场值。
- **PreviousValue** 始终为**本次成功提交之前的 CurrentValue**，首次提交前 `HasPrevious=false`。即使两次提交数值相同，第二次提交也应有 `HasPrevious=true` 且 PreviousValue=此前 CurrentValue；`Revision` 每次成功提交递增，而 **ValueChanged 只在值实际变化时触发**。如业务需“上一次不同的值”，应另立 `PreviousDistinctValue`，不得偷改 PreviousValue 语义。
- **SourceId** 为原始采集/业务数据来源，如 PLC 或算法对象；**WriterId** 为实际提交此项写入的 Connector/Module。二者可相同，也可不同；不得丢掉传递来源链。
- **Quality** 支持 Good/Bad/Uncertain/Stale；从设备失联变 Stale 不须伪造一个新值。质量变化是否单独推进 Revision 由同一提交规则明确：**任何对外可见状态提交（值、质量、来源）推进版本**；同值/同质量的重复采样可依政策抑制，但不可伪造版本。批量提交另有 Scope 的批次/快照序号。
- 采集时间 `ObservedUtc` 与提交时间 `CommittedUtc` 独立，以便记录延迟与跨设备不同时间域。`DefaultValue/CurrentValue/PreviousValue` 若是可变引用数据，必须遵循现有 Foundation.Data 的冻结/复制契约；快照不可暴露可变内部引用。

### 5.2 写入与订阅机制

1. 定义必须先登记且类型匹配；Update 按 Owner 权限核验后原子提交：值、旧值、质量、来源、时间、版本为同一快照。
2. 读优先获取一致快照；单变量 Get 不必须依赖总线派发；批量快照需统一范围版本或批次一致性声明。
3. 高频路径允许批量写、差量通知、合并仅最新值、按源/路径过滤、节流；**不能在提交线程直接执行不可控的订阅回调**（即使已经离开更新锁，仍可能阻塞写入）。应采用受控的变更快照、独立通知队列/调度，队列有界且故障隔离。
4. 订阅事件至少区分 `ValueChanged`、`QualityChanged`、`SourceChanged`、`Committed`；可以组合过滤。`Committed` 表示完成一次真实提交，不等于值发生变化。合并只用于明确允许只关注最新状态的订阅；报警、审计、边沿触发不能静默合并。
5. 多源同时写由 Owner 依数据源优先级、预期 Revision、显式授权仲裁；拒绝时不产生伪提交；禁止默认随意的 last-write-wins。
6. Protocol PointTable 管**设备地址/站号/权限**，Variable 管运行状态；通过 `MPADataMapping` 映射，不复制出另一个“设备点表变量中心”。
7. 高吞吐与实时边界：Windows C# 保证逻辑/软实时一致性，不承诺 PLC/控制卡级硬实时周期。

### 5.3 现状差异

核对 `MDSVariableRegistry.cs`：按 `ScopeId` 建实例表，非全局静态单例。`MDSVariableTable.cs` 已有 Define/Get/TryGet/Update/Sample、类型、Quality 与变更事件；**当前 Entry 没有 DefaultValue、PreviousValue、SourceId、WriterId、Revision，事件为 Action<string>，更新锁外直接同步回调**。完整快照、订阅隔离、批量一致性均列为 P0 待实现，不以“有点表”误认为全部完成。

## 6. 轻量消息、事件与通知（M36 + M12 契约）

### 6.1 结构与语义

消息的最小业务包络：`MessageId、TypeId、SchemaVersion、SourceAddress、TargetAddress/Topic、CreatedUtc、CorrelationId?、CausationId?、Headers、SmallPayload/PayloadRef`；必要时附 `Sequence、DeliveryPolicy、TraceId`。字段为**目标契约**，实际类型要与 DOCS/04 及现有事件类型查重。

- **Message** 是发布/订阅机制中的通用轻量消息；**Event** 表示已经发生的不可变事实；**Notification** 用于提示；三者不产生相互冲突的多个权威事件身份。
- 支持 Publish、精确 Subscribe、路径/层级 Subscribe、取消订阅、过滤；订阅者本机可以多个。订阅作用域必须明确，避免把“全局监听”当默认。
- 默认轻载荷；图片、CT Volume、文件仅发 `DataRef/ArtifactRef`、大小、版本等元信息，不能内嵌原始大数据。

### 6.2 发送与投递

| 模式 | 定义 | 反馈 |
|---|---|---|
| BestEffort | 允许队列满丢弃/按策略丢旧值 | SendResult 说明本次是否受理及实际处理阶段；不能暗示交付 |
| AcceptedLocal | 接纳到进程内有界队列 | SendResult=AcceptedToQueue，不保证消费者处理 |
| ReceiverAck | 需指定接收方应用接收确认 | DeliveryResult，按消费者/目标报告；不代表回调业务处理完成 |
| Durable（扩展） | 持久化再投递、重放 | 需要真实持久化能力，P0 不虚报 |

无订阅者时可选择“允许无消费者发布”或“必须至少一消费者”；默认普通通知可接纳，但必须返回 `MatchedCount=0` / `NoSubscriber` 诊断，**既不能标为业务已送达，也不能把合法的零订阅发布无条件当发送错误**。若声明必须有消费者，则明确拒绝。对 N 个订阅者，应按订阅者身份分别统计匹配、应用接收和失败，不能用一个 `bool` 模糊描述。

### 6.3 并发与生命周期

订阅令牌可取消/Dispose；**Dispose 完成后不得再排入新回调；已经开始的回调允许依契约正常结束，若需等其退出，显式提供 Drain/StopAsync**。回调异常隔离、记录失败、不得带崩发布者。队列有界；慢消费者不阻塞变量采集、设备命令和图像传输。保持**同一个有序 Source+Topic 分区**的本地顺序（若选择有序模式），跨不同 Topic/线程无全局顺序保证。P0 不承诺分布式 exactly-once。重叠订阅去重须以 SubscriberId+EventId 为键，不得抹去业务主动重复发布的新事件。

### 6.4 消息最小可用模型：MQTT 风格 Topic + Payload（V1.3 收敛）

**决议：普通开发者只面对 `Topic + Payload + Publish/Subscribe`；定义/版本、来源、发送状态等作为运行层元数据自动附着，不强制用户构造巨大 MessageEnvelope。** 内核可保留第6.1节的标准包络，但简洁接口是第一公民。

| 层次 | 必需 | 可选/自动完成 | Owner |
|---|---|---|---|
| 消息主题（路由） | Topic 字符路径；可用对应 NumericId 精确路由 | Machine/Container、AppCode、ModuleCode 分层匹配 | M36 分发 + 统一地址索引 |
| 消息数据 | Payload（标量、受控 DTO、轻量数据引用） | TypeId/SchemaVersion：跨模块强类型订阅时声明；临时进程内通知可由类型适配器识别 | 消息发布者提供值，公共类型契约验证 |
| 发送 | Publish(topic,payload) | MessageId、SourceId、CreatedUtc、Trace/Correlation 由环境生成或传入高级选项 | M36 |
| 订阅 | Subscribe(topicPattern,callback) → IDisposable | SubscriberId、过滤、接收方式、背压策略由订阅选项提供 | M36 |
| 反馈 | 发布受理阶段/结果与匹配数量 | 按需接收 ACK；缺少 ACK 不能说 Delivered | M36 |

**Topic 与 EndpointAddress 的关系**：Topic 是消息的路由键，也可以采用第10章的规范化层级路径；EndpointAddress 是发布者/拥有者的对象地址。两者可有关联，**但不必是同一个对象**：一台相机可以发布 `Captured`、`Faulted` 多个 Topic。Path 是层级查询的依据，Snowflake NumericId 是已注册 Topic/端点的精确别名；**MessageId（一次发布）不等于 TopicId（主题身份）**。

推荐示例主题：`/machine/1201/app/XRay/module/Camera/message/Captured`。**V1.5 变更裁决**：订阅过滤与 MQTT 统一使用 `+`（单层）与 `#`（只可末尾多层），旧 V1.4 尚未实现的 `+/#` 草案不再作为公开契约。发布 Topic 不允许含有通配符；Filter 和具体Topic由不同类校验。

**进程内调用示意（目标 API，非已存在可编译代码）：**

~~~csharp
IDisposable sub = bus.Subscribe<CameraCaptured>(
    "/machine/1201/app/XRay/module/Camera/message/Captured",
    msg => OnCaptured(msg));
PublishReceipt receipt = bus.Publish(
    "/machine/1201/app/XRay/module/Camera/message/Captured",
    new CameraCaptured { ImageName = "Image001", Width = 4300, Height = 4300 });
// receipt 仅表示本地受理/实际达到的阶段；不意味着订阅者的业务已执行。
sub.Dispose();
~~~

上例 `CameraCaptured`、`PublishReceipt` 与 `bus` 均为**目标使用体验示意**，正式类名应先查重 DOCS/04 与现有源码。镜像/CT 数据仅发元信息与受控定位，不复制大载荷。

### 6.5 消息完整闭环、异常和边界

~~~text
[可选消息类型/Topic定义] → 发布者 Publish(Topic,Payload)
    → 校验作用域/权限/大小/类型 → 获得消息身份及来源
    → 匹配当前有效订阅（可无订阅者） → 有界队列接纳/失败反馈
    → 各消费者隔离分发 → 取消订阅/释放
    → [要求应用级 ACK 的消息] 另行产生 DeliveryResult
~~~

- **默认轻量**：不必先创建工程资源才能发一条进程内通知；必须注册、持久化或跨域共享的消息类型才记录稳定 Schema/版本，并遵守权限与配置。事件（Event）有明确类型和不可变事实身份；普通消息允许最小结构。
- **一个消费者、多个重叠订阅**：根据 SubscriberId+MessageId 去重同一次投递；两个不同消费者应各收到一次。不能将第11章的 SubscriptionToken 当作消费者身份。
- **队列不能无界**：Publish 要么被有界队列接纳，要么给出 Backpressured/Rejected/Failed；处理回调失败不得让发布者误判已成功处理。允许零订阅者时返回 MatchedCount=0；可选 RequiresSubscriber 策略才拒绝。
- **同 Scope 的消息分发不触发任何命令**：订阅到名为 `MoveAbs` 的消息只表示读到了信息；执行动作必须经过 CommandHandler 的精确路由、参数/授权检查。不得通过 wildcard Message.Subscribe 隐式控制设备。
- **保留给未来 MQTT**：Topic+Payload 映射 MQTT 发布订阅；QoS/Retain、应用级 ACK、Bridge/重连与离线重放必须分开配置，不因 MQTT Broker 接受而宣称业务执行完成。P0 完全不依赖 Broker。

### 6.6 V1.5 MessageTypeDefinition：Topic、Payload 数据类型与MQTT订阅语义

**最终确定**：`MNMMessageTypeDefinition` 具有 `Topic`（实际发布地址）、`TypeId`、`SchemaVersion`、`PayloadKind`、`PayloadType`、`ContentType`、`MaxPayloadBytes`、`Name`、`Description`、`TopicCode`。Definition 只规定数据结构、约束与路由地址，**不持有每次发布时的Payload内容**。每次发布的内容以后放在 MessageEnvelope/MessageInstance 中。

| 字段 | 说明 | 示例 |
|---|---|---|
| `Topic` | 完整发布路径；可复用类型初始为空，实例化后绑定 | `/machine/1201/app/XRay/module/Camera/message/Captured` |
| `TopicCode` | Topic末段；默认由 TypeId尾段推导 | `Captured` |
| `TypeId / SchemaVersion` | Schema稳定身份与版本 | `Camera.Captured` / `1` |
| `PayloadKind` | `Text`、`Binary`、`Structured`、`Reference` | `Structured` |
| `PayloadType` | CLR内存对象类型 | `string`、`byte[]`、`CameraCapturedDto` |
| `ContentType` | 编码格式标识，不是序列化器 | `application/json`、`text/plain` |
| `MaxPayloadBytes` | 内联数据限制；0沿用Host限制 | `65536` |
| `Name / Description` | 显示与说明 | 采图完成 |

`Topic` 可以空仅用于“可复用的消息类型模板”，**不能直接当发布目的地**：由 `WithTopic(fullTopic)` 生成绑定定义，后续发布使用绑定对象的具体 Topic。为了兼容先前已编译的三参数构造，保留未绑定模板用法。真正发布前必须解析并验证合法 Topic。对于单个模块配置，应直接设置 Topic，无需每次发布再绑定。

**四类Payload**：

- `Text`：`string`。纯文本，按 UTF-8 字节数校验。
- `Binary`：`byte[]`。原始Buffer，按Buffer长度校验；**异步入队前必须复制或采用受控租约**，不允许发布后任意修改底层数组。
- `Structured`：强类型 DTO；应用内传对象。`ContentType=application/json` 仅声明未来传输要使用的格式，**不会自动实现JSON/Protobuf**，桥接前需真实Codec并校验编码大小。
- `Reference`：MDS/专业Owner 数据定位，实际图像/CT Volume不通过消息正文复制；引用身份和生命周期仍必须由真实数据Owner检查。

**订阅过滤**：`MNMTopicFilter` 独立于 MessageTypeDefinition，采用 MQTT 原生 `+` 单层 / `#` 多层且仅在末尾的规则。过滤表达式只描述订阅兴趣，不能出现在真实发布Topic中。CtxVisual P0 的具体消息Topic限制为八层绝对路径、ASCII Code（这是平台约束，**不是 MQTT 标准本身的限制**）；将来需要通用MQTT Topic时须版本化扩展。

~~~csharp
MNMMessageTypeDefinition template = new MNMMessageTypeDefinition(
    "Camera.Captured", 1, typeof(CameraCapturedDto),
    payloadKind: MNMPayloadKind.Structured,
    contentType: "application/json", maxPayloadBytes: 65536);
MNMMessageTypeDefinition current = template.WithTopic(
    "/machine/1201/app/XRay/module/Camera/message/Captured");
bool interested = new MNMTopicFilter(
    "/machine/1201/app/XRay/module/+/message/Captured").Matches(current.Topic);
~~~

上面 `CameraCapturedDto` 为专业 DTO 示例，M36只负责定义与校验。**本轮只完成消息定义与Topic匹配，不表示 Publish/Subscribe/队列已开发。** 消息类型的 `PayloadType` 属于进程内.NET身份；跨进程应使用稳定 SchemaId/Version 加明确的Codec，而不是传CLR Type名称。


## 7. 命令与操作（M12，MRK执行，专业 Owner）

### 7.1 命令定义

目标契约字段：`CommandTypeId、TargetAddress、RequestId、OperationId、CallerId、Arguments、CreatedUtc、TimeoutPolicy、AuthorizationContext、IdempotencyKey?`；定义中说明：参数/结果 Schema、执行 Owner、是否幂等、可否取消、是否必须现场确认、并发/资源策略。

公共契约 M12 描述“做什么”；具体 Handler/Provider 执行专业动作；MRK 管调用/资源/运行进度与已存在 Operation 跟踪；M28 负责授权；严禁 MessageBus 自己执行命令或 UI 绕过专业 Provider。

### 7.2 四层不同的结果

| 层级 | 示例状态 | 判断依据 |
|---|---|---|
| **SendResult** | Rejected / AcceptedToQueue / WrittenToTransport / SendFailed | 调用方或底层传输；不代表业务已收到 |
| **DeliveryResult** | Delivered / Undelivered / Unknown | 指定接收方应用级应答；Broker ACK≠最终业务交付 |
| **ExecutionResult** | Accepted / Rejected / Running / Completed / Failed / Cancelled / Unknown | 目标 Command Handler / MRK 操作登记 |
| **ConfirmationResult** | Confirmed / NotConfirmed / Indeterminate | 真设备反馈、位置反读或业务定义的可信证据 |

这些是**不同结果类型/阶段**，不是把它们全塞入一个单向递增的万能枚举。操作可能执行完成但现场反读不一致；发送超时后设备可能仍在执行。`Unknown` 不等于 Failed，也不表示可安全重发。无反读要求的纯计算命令不需要 ConfirmationResult。

### 7.3 执行与恢复

- 注册真实 Handler，先鉴权后受理；无处理者/未授权必须拒绝，不准空实现返回成功。
- Invoke 支持异步长任务，调用方可按 OperationId Query/Observe；同步便利入口仅是等待异步操作，不形成新执行器。
- OperationId 与 CorrelationId 分别用于副作用去重与请求响应追踪；去重必须核验 Caller/Target/CommandType/参数摘要，不得仅凭 ID 命中就放行。重试同一副作用使用相同受保护身份；同一进程的登记表不能保证**崩溃后**的恰好执行一次，跨重启保障需持久化及专业对账。
- 超时标 Unknown、迟到反馈仍可归原 Operation；取消是**请求取消**，不是保证运动立刻停止。紧急停机交由 PLC/安全硬件，不由消息可靠性机制承担。
- 组合多设备命令归 MOL/MRK 编排，绝不靠多订阅广播去隐式执行“分布式事务”。

### 7.4 现有实现与差距

`MRKExternalOperationTracker` 已有 Register/TryGet 与 Pending/Unknown/Confirmed/Failed，`MRKCorrelationWaiter` 已有 Register/Receive/WaitAsync；它们是**可复用的进程内基础**。**已有 `MRKExternalOperation.Confirm()` 仅代表 Provider 声明操作有确定结果，不能等价为第7.2节的独立现场 `ConfirmationResult.Confirmed`**；命令的完整注册/鉴权/Handler、持久幂等及现场确认仍未具备。`MDSProtocolDataTargetAccess.PublishAsync/InvokeAsync` 依赖注入真实回调；缺失时返回 NotSupported，命令还必须显式授权；这正是目标机制接入点，不能认为 Publish/Invoke 完整交付。

### 7.5 IoT 物模型风格的**轻量命令定义**（V1.3 正式收敛）

**决议：命令定义只回答“有什么命令、要什么输入、返回什么输出”；请求 ID、执行 ID、排队、可靠性、审计由平台按需自动提供。** 借鉴 IoT 物模型的 `service_id + command_name + paras/response` 思路，**不照搬 IoT 的设备/服务强制层级**，因为 Vision、CT、Inspection 和普通模块同样需要命令。

| 命令定义字段 | 必需/可选 | 准确含义 |
|---|---|---|
| **CommandId** | 必需 | 统一地址 Registry 中该**命令定义**的稳定数字身份（Snowflake）；与工程 MEM 资源 GUID 建映射，不覆盖工程真源 |
| **CommandCode** | 必需 | Owner 内唯一、稳定的命令编码，如 `MoveAbs`，不把显示名称作为调用键 |
| **Name** | 必需 | 给 Studio/用户看的命令名称，如“绝对运动” |
| **Inputs** | 必需（可为空集） | 输入参数描述集合：名称、类型、必填性、默认值及必要校验 |
| **Outputs** | 必需（可为空集） | 输出参数描述集合：名称、类型、说明，供调用与 MOL 端口生成 |
| Description | 可选 | 作用说明、业务提示 |
| SchemaVersion | 建议持久化定义必需 | 参数契约版本；已有绑定必须进行兼容检查 |
| Group/ServiceCode | 可选 | 需要物模型分组时由既有 AppCode/ModuleCode/Capability 提供；**不新增强制的 IoT ServiceManager** |
| ExecutionPolicy / Permission | 按需 | 可取消性、资源串行、现场确认、授权要求；默认继承专业 Owner 与现有配置/安全机制 |

**每个输入参数**最小形式为 `Code + DataType + Required`；`DefaultValue、Min/Max、Unit、Description` 按需填写。默认值仅在参数**未传且允许缺省**时使用；显式提供的 `0/false/空串` 不得错认成“缺省”。类型与取值校验在调用业务 Handler **之前**完成，不能让错误参数驱动真实设备。持久化配置复用现有 FCM/类型描述和 MOL 端口 Schema，不建设另一套配置或参数库。

**唯一性**：`CommandCode` 在确定的 Owner/Module 下唯一，完整命令端点由 `Scope + TargetOwnerAddress + CommandCode` 精确定位，也可通过稳定 CommandId 查到**同一命令定义**。显示名允许修改；命令编码或参数不兼容改动必须有版本/迁移计划。若命令定义属于可持久工程资源，保持稳定 ID；运行实例的 Handler 另有 RunId/Generation，禁止旧请求误打到新实例。

**实例示例——Axis.MoveAbs（文档契约，非现有 API）：**

~~~json
{
  "CommandId": "3012345678901234567",
  "CommandCode": "MoveAbs",
  "Name": "绝对运动",
  "Inputs": [
    {"Code":"Position","DataType":"Double","Required":true,"Unit":"mm"},
    {"Code":"Velocity","DataType":"Double","Required":false,"DefaultValue":20.0,"Unit":"mm/s"},
    {"Code":"Acceleration","DataType":"Double","Required":false,"DefaultValue":50.0,"Unit":"mm/s2"}
  ],
  "Outputs": [
    {"Code":"ActualPosition","DataType":"Double","Unit":"mm"},
    {"Code":"Success","DataType":"Boolean"}
  ],
  "SchemaVersion": 1
}
~~~

示例中数值/单位仅用于说明参数结构；真实运动轴允许的速度、加速度与单位必须由专业 Provider/配置验证。通用层不替代设备安全校验。

### 7.6 命令**调用实例**与执行结果：IoT 请求-响应 + MRK 已有跟踪

定义与调用实例严格分离：

| 一次调用字段 | 谁产生 | 对开发者是否必填 |
|---|---|---|
| Target（Path 或 CommandId） | 调用者选择 | 是 |
| Arguments（结构化对象/DTO） | 调用者给值，平台合并合法默认值 | 是（可为无参） |
| RequestId / CorrelationId | 平台生成，MRK 关联响应 | 否 |
| OperationId | 平台登记执行操作，用于 Query/Cancel/去重 | 否 |
| Caller / Origin / RunId / Generation | 运行上下文与受权身份 | 否（由宿主注入，不能伪造） |
| Timeout、Cancellation、IdempotencyKey | 高级调用选项 | 否 |
| SchemaVersion | 从命令定义与调用绑定导出，校验不兼容变更 | 否 |

~~~json
{
  "Target": "/machine/1201/app/Automation/module/Axis01/command/MoveAbs",
  "Arguments": {"Position":100.0,"Velocity":20.0},
  "RequestId": "request-example-001",
  "OperationId": "operation-example-001"
}
~~~

JSON 仅是**桥接/诊断示例**，并非要求进程内也进行 JSON 编解码。真实 ID 使用已批准的身份类型生成器。第三方 IoT/MQTT 对接通过 Bridge 映射字段，不能让业务 SDK 硬依赖华为云或某 Broker 特有的字段名。

**进程内最简调用（目标用法示意，非现有 C# API）：**

~~~csharp
// 专业模块启动时注册唯一执行者，输入输出采用强类型参数。
IDisposable registration = bus.RegisterCommand<MoveArgs, MoveResult>(
    "/machine/1201/app/Automation/module/Axis01/command/MoveAbs",
    (args, context) => axisProvider.MoveAbsAsync(args, context));

// 普通短命令：平台隐藏 RequestId/OperationId，直接取得结果。
CommandResult<MoveResult> result = await bus.InvokeAsync<MoveArgs, MoveResult>(
    "/machine/1201/app/Automation/module/Axis01/command/MoveAbs",
    new MoveArgs { Position = 100.0, Velocity = 20.0 });

// 长任务：由现有 MRK 执行协调提供 OperationId/Observe/Query，调用方无需长期阻塞。
// 停止等待、请求取消和实际停止三者是不同的事实。
~~~

`MoveArgs`、`MoveResult`、`CommandResult<T>`、`bus` 均为**使用体验原型而非仓库已有类型**。命令执行者实现归专业 Provider；MRK 已有 OperationTracker/CorrelationWaiter/ResourceLease 优先复用。**Command 层只做轻量受控调度入口，不另建任务执行器。**

### 7.7 从定义到结果的**完整闭环**与异常规则

~~~text
[工程/Capability 上的 CommandDefinition]
        ↓ 校验/发布/装配
[Runtime 注册唯一 Handler(Owner,CommandCode,Generation)]
        ↓
[Studio / MOL / API：Invoke(Target,结构化Arguments)]
        ↓ 平台生成 RequestId + OperationId；参数默认值合并与 Schema 校验
[精确解析 Target + 授权 + 状态/资源/并发检查]
        ├── 失败：Rejected(ErrorCode,Message)，真实 Handler 不执行
        └── 受理：Accepted(OperationId)，交由真实 Handler / MRK
                  ↓ Running/Progress（可选）
            [专业设备/算法实际执行]
                  ├── Completed(结构化Outputs)
                  ├── Failed(ErrorCode,Message)
                  ├── Cancelled（执行方确认）
                  └── Unknown（观察超时，后续按 OperationId 查询）
                  ↓ 可选：现场反读 ConfirmationResult
[调用方获执行结果；MOL 端口得到有类型输出；Studio 展示状态]
~~~

规范细则：

1. **Handler 唯一**：同一 Scope/实例/命令只允许一个执行 Owner；重复注册、过期 Generation、无 Handler、无权限都明确拒绝；广播消息订阅者永远不是命令执行者。
2. **失败可诊断**：至少区分 `UnknownCommand / InvalidArguments / SchemaMismatch / Unauthorized / Busy / ExecutionFailed / Cancelled / TimedOutUnknown` 等稳定原因。未进入 Handler 的拒绝不伪称失败执行。
3. **短任务易用，长任务可追踪**：`InvokeAsync` 可等候业务结果；需要进度/超时恢复时，复用 MRK 的 OperationId 与查询；不强迫所有简单命令申请持久化任务。
4. **结构化 Outputs**：与定义一一对应，完成时校验输出类型；失败时 ErrorCode/Message 独立于业务输出，不能把 `Success=false` 当作唯一错误通道；现场确认属于附加证据，不强制计算类命令配置。
5. **重试安全**：RequestId 只关联一次请求，OperationId 表示一次副作用；重入时核验目标、调用者与参数指纹，时间超时/调用取消不能断言设备没有动作，严禁盲目重发。崩溃后持久幂等属于后续扩展，不伪称 P0 已有。
6. **定义装配**：编辑态的命令参数/说明/版本属于既有 MEM/FCM/Capability/专业定义；Studio 由 Schema 自动生成输入界面、MOL 从相同契约生成输入输出端口；运行态只是对已批准定义绑定 Handler，不创建第二套持久工程真源。
7. **与已有 MPA 映射兼容**：现有 `IMPADataTargetAccess.InvokeAsync(string,object,ct):Task` 只有执行入口、没有强类型结果；P0 以**适配器/兼容扩展**接入真实 Handler，不能直接破坏现有 Connector/Protocol 调用签名，也不能把 `Task` 完成自动宣称现场确认。业务结果走正式 Command Result/Operation 查询链。
8. **未来 MQTT 桥接**：请求 Topic + 编码后的 Arguments；回复 Topic + 关联标识 + 执行结果（MQTT 5 可以利用 Response Topic/Correlation Data）。这些只是传输映射，MQTT 发布 QoS ACK ≠ Handler Accepted/Completed；P0 无需 Broker。

### 7.8 最简可交付面与不该新增的复杂度

P0 只要求**一种已注册命令定义与其 Handler、一套 Topic/Path+NumericId 精确寻址、一个受控调用接口、一种结构化执行结果、可选的操作查询**。消息只要 Topic+Payload+Publish/Subscribe+明确发送反馈。异常隔离、权限、Schema/参数校验与队列限额属于底层安全质量，不应该强迫普通 API 使用者操心。

**禁止**：独立 IoT ServiceManager、第二配置 Schema、第二 CommandExecutor、第二 MRK OperationTable、命令广播执行、统一万能 EventBus、将 Message.Payload 作为默认的大数据持有者。

### 7.9 V1.5 CommandDefinition 定案补充

`MCECommandParameterDefinition` 在原 Code/DataType/Required/Default 的基础上新增 `Unit,Description,MinValue,MaxValue`，输入与输出复用同一参数定义；`BindInputs` 与 `ValidateOutputs` 按定义校验，错误类型、NaN、超过声明范围必须拒绝。缺参时只有显式允许默认值才填入，0/false 不可视为缺参。

`MCECommandDefinition` 在 CommandId/CommandCode/Name/Inputs/Outputs/SchemaVersion 基础上按需新增 `Description,PermissionKey,TimeoutMilliseconds`。权限键只是静态策略引用，不代表已经授权；0超时代表继承Host默认，不是无限等待。命令必须通过精确 Path 或 ID 找到唯一Handler，不能使用 `+/#` 过滤表达式执行多台设备。请求/OperationId与执行结果属于运行实例/MRK，不存回Definition。

旧构造参数位置与既有测试保持兼容；完整CommandHandler/Register/Invoke仍为阶段三实施任务，不以定义类通过校验视为执行闭环完成。


## 8. 运行数据、数据堆及连续数据（MDS/M10）

### 8.1 保持现有运行数据语义

`IMDSDataContext` 的 Node Input/Local/PendingOutput、DataStack 的 CommittedOutput/FlowHeap、MDSDataHeap 的 Set/Get/Replace/Remove/Move/Copy、文件定位/Load/Unload/Search/Inspect，继续以现有 V3.0 为规范；变量不直接塞进 FlowHeap 作为新的变量真源。

| 数据类别 | 进程内默认机制 | 注意事项 |
|---|---|---|
| 普通小值 | 安全值、托管引用/复制 | 类型严格、分清 Owner |
| 可变 byte[] | 防御复制或显式拥有者移交 | 防止外部偷偷修改 |
| Image / Tensor | 专业不变数据或受控读取 | Shape/Stride/PixelType/坐标等由专业 Adapter 校验 |
| CT Volume | 现有 FileLocator/Loader；分块/ROI 属候选能力 | 现有 MDS 不宣称已支持 8GB 零拷贝 ROI |
| 连续 Frames / Projection | **Stream** 通路（有界、背压、丢帧/重放策略） | 不无限向 Heap/Message 堆积 |
| 大文件 | 文件引用、Hash/Size、受控读取 | 跨 PC 再用 FileSync 服务 |

### 8.2 所有权与生命周期

- 同一 Owner 内数据引用合法，跨进程裸内存指针、进程本地文件路径不是合法的远程 DataHandle。
- Move 改变逻辑持有者，Copy 创建独立安全值；不可变 Buffer 可受控共享，专业外部 GPU/SDK 资源需明确 Lease。
- 删除、释放、缓存预算、加载/卸载必须按现有 MDS 行为执行；订阅默认只推送 `Id + Version + Metadata`，**不因订阅自动复制整个 Image/Volume**。P0 若需跨模块数据定位，使用经过校验的 `OwnerScope + RunId + Generation + DataName/Type/Version` 访问上下文（逻辑契约候选），不能假设公开 `MDSDataRef`/`MDSDataLease` 已实现。
- 要支持的 Binary/File/Image/Volume 不等于“通用对象全部自动可跨网络序列化”；编码、Shape、Endian、CheckSum、数据来源、权限需要专门契约与真实验证。

### 8.3 差距说明

MDS V3.0 明确：泛 `MDSDataRef/MDSDataLease/MDSSharedMemoryCarrier` 尚非默认公开契约；Heap 快照当前主要是元信息，不能以“Snapshot”名义宣称全量跨进程恢复。P0 不强制新增通用 Carrier；仅固定未来 Bridge 扩展要求，在实际 SHM/外部 SDK 场景需要时逐步增构。

### 8.4 DataHeap 序列化与反序列化（补充此前已确定的能力）

- **必须支持可序列化数据的保存与恢复**，但不能把“`MDSDataSnapshot` 元信息可查询”等价成“整个 Heap 已能序列化/反序列化”。当前可核实的 `MDSDataHeap` 主要支持 Set/Get/Move/Copy/FileLocator/Inspect；**尚未找到完整的通用 Heap Serialize/Deserialize 正式公开 API 和相应真实回归证据**，实现状态仍为 P。
- 定义快照范围：指定 OwnerScope/RunId、数据条目白名单、类型 TypeId/SchemaVersion、原始 SourceId/WriterId（如存在）、数据版本、文件 Locator 与校验信息；**不默认保存运行节点活动栈帧、外部 SDK 指针、GPU 句柄和实时订阅令牌**。需要运行中一致快照时由 MRK/Owner 协调安全点，不能读到一半旧一半新。
- 编解码由允许的类型注册表、既有安全 Adapter/专业 Codec 承担；小值可内嵌，Binary/Image/Volume 优先经可信文件/ArtifactRef 与 Hash/Size 校验分离存储。反序列化严禁任意 CLR 类型实例化、外部路径穿越及覆盖未经授权的现有数据。
- 反序列化需要**先解析与校验，再原子提交**；失败时不留下半恢复 Heap。未知/不兼容 Schema、坏校验、大对象不可访问要明确报告；新的运行 Owner/RunId/Generation 必须重建，不能信任旧快照中携带的运行权限与裸 Handle。恢复数据须区分“缓存恢复”与“设备已重新采样”，不能擅自标记 Good。
- P0-5 至少验证**可支持类型的小型 Heap 快照往返、异常回滚、来源元信息与文件 Locator 恢复策略**；跨 PC 大文件同步、SHM/8 GiB 体积完整恢复仍属于 P1～P3 独立验收。

---

# 第三部分　统一总线的核心机制

## 9. 双层总线与拓扑

### 9.1 进程内**逻辑总线**——P0 真正实施对象

逻辑总线指同进程中各业务 Manager/Registry/Provider/Handler 通过**统一地址、订阅/查询/调用与观察契约**协作，**不是必须有一个统一队列或总线服务器**。直接调用优先用于小值读取、局部数据访问与明确可即时返回的操作；异步派发适用于通知和耗时工作；受控命令使用 MRK/专业 Handler；高吞吐数据沿用 MDS/Stream。四类操作允许具有不同线程与性能约束。

业务契约建议按五组划分（具体接口名待 DOCS/04 查重）：

1. `Address + Scope + Registry`：身份登记、查找、别名与动态注销；**不持有业务内容**。
2. `Variable Access`：从 M09 权威实例取得实时状态/订阅；不复制 VariableTable。
3. `Message Dispatch`：Message/Event 的轻量、可取消分发；不创造第二套变量事件真源。
4. `Command Dispatch`：注册 Handler、执行权限/入参校验、转交 MRK/Provider、观察结果；不创建新流程运行引擎。
5. `Data Access`：调用现有 MDS 上下文与堆/文件入口；不创建“BusHeap”。

### 9.2 进程外**物理总线**——E 扩展

不同进程才通过 Adapter/Bridge 连接。成熟服务优先，按业务使用 MQTT、Named Pipe/IPC、SHM/MMF、TCP/TLS、按需大文件同步；一个 Bridge 不需要既解 Modbus 又执行 Camera Command，又保存 CT 体数据。

### 9.3 跨域请求路径

```text
发起 Module ──本地契约──> 逻辑 Owner（同进程，直接完成）
                        └──目标在外域、显式允许──> Bridge──>选定物理通路──>远端 Bridge──>远端 Owner
```

必须保留本地与远程差异：同进程 `Get()` 可以同步，远端请求必须有异步、超时、可用性和权限失败；**禁止用透明的远程同步属性读取掩盖网络故障**。

## 10. 双寻址：字符路径 + 数字 Snowflake

### 10.1 唯一语义、双重表现

- **字符方式**：可读路径；供工程配置、日志、层级订阅、Studio 查找。
- **数字方式**：64 位 Snowflake 风格编码；供高效精确查找、快速关联、紧凑通信。
- 两者解析到**同一个公开端点/对象**；不是两套存储或两个 Manager。已有 MEM GUID/ResourceRef/MRIObjectAddress 为工程权威身份，Snowflake 是公共交互地址，不替换 MEM GUID、MPC PortId、MRK OperationId。**必须区分持久定义端点 ID 与运行实例端点 ID**，否则无法同时满足“保存重开”和“旧实例晚到隔离”。

### 10.2 地址层级和作用域

建议规范化路径模板（**逻辑示例，最终分隔符/大小写规则在 P0-1 冻结**）：

```text
/{ScopeKind}/{ScopeId}/app/{AppCode}/module/{ModuleCode}/{Kind}/{Name}
/machine/1201/app/XRayInspection/module/Camera/variable/Ready
/container/2200/app/Runtime/module/AI/command/RunInspection
```

`ScopeKind=machine|container` 为显式类型，不把 MachineId 与 ContainerId 强制同时存在或随意互换；二者允许父子/所属关系，由 Registry 的元数据描述。`AppCode`、`ModuleCode` 是**分层匹配键**而非必需能从数字 ID 中解码的位字段；每段进行转义、长度校验和规范化。相同名称在两个 Project/Run/实例中不得解析成同一端点。Scope 可以采用已有项目/Host/Run 限定，不重新创造项目树。

### 10.3 Snowflake 生成

- `long/int64`，全域避免碰撞；P0 允许由宿主按实例分配 Worker/NodeId，支持批量创建；时间回拨必须报错或进入明确恢复策略，不能静默重复；同毫秒序号耗尽应有受控处理。
- 不预先把机器、容器、AppCode、ModuleCode 硬塞到固定 64 位段，否则编码结构更动就需要整体迁移。ID 只需能定位对象；分层查询借字符路径及登记索引。
- JSON/JavaScript/UI 将 64 位 ID **作为字符串**交互，避免 `Number` 超过 2^53 精度；二进制协议可原样保留 int64。
- **持久工程定义的数字地址**：若需跨保存重开稳定访问，首次分配后须与工程定义/正式配置绑定并持久化；重命名不变，复制工程/资源应明确生成新 ID 或保留受控引用，不能碰运气重新发号。**运行端点的数字地址**：按实例/宿主生命周期分配，重启可变化，配合 `Generation/RunId` 隔离。规范必须显式声明每个端点属于哪一类，禁止把运行时临时 NumericId 持久写入工程绑定。
- 删除的 ID 不直接复用；动态模块重启的同名实例必须有生命周期 Generation/RunId，迟到消息不能误击中新的 Handler。
- `ReferTo/DeviceVariable/DeviceSuite/Core/Snowflake.cs` 含旧 `SnowflakeManager`/`Snowflake`，其使用 `DataType` 占用数据中心位且 WorkerId 常量为 0，需**核对多进程碰撞、时钟回拨、类型位范围与版本**后适配；不得直接作为 P0 已实现类。

### 10.4 路径重命名、失效与别名

路径变更时稳定工程 ResourceRef/GUID 不变；公共地址 Registry 重建或更新路径与 ID 索引，旧别名是否保留由配置迁移明确。**工程绑定优先存权威 MRIObjectAddress/ResourceRef 或持久端点 ID，绝不能把短命的实例 NumericId 作为唯一工程真源。** 非法/重复/跨 Scope 地址拒绝；数字 ID 必须配合 Kind/Scope/Generation 校验，不能当作权限令牌。

## 11. 注册、发现、查询、分层订阅和访问控制

### 11.1 注册

每个公开对象只登记 `AddressPath / NumericId / IdentityKind(PersistentDefinition|RuntimeInstance) / Owner / TypeId / Scope / AccessPolicy / LifetimeToken`；Registry 返回引用或安全的访问适配，不复制权威值。对象注销按生命周期撤销订阅/Handler 并使旧地址失效；重复 Path 或 ID 必须明确拒绝。进程内 Registry 归 Host/Project Scope 装配，不允许进程级静态全局表串多个 Run。

### 11.2 多层订阅

订阅可以按精确端点、子树与维度过滤：

| 订阅范围 | 示例 | 解释 |
|---|---|---|
| 精确 | `machine/1201/app/XRayInspection/module/Camera/variable/Ready` | 仅该变量/端点 |
| ModuleCode | `machine/1201/app/XRayInspection/module/Camera/**` | 此模块的可订阅对象 |
| AppCode | `machine/1201/app/XRayInspection/**` | 此应用内符合权限的对象 |
| Machine/Container | `machine/1201/**` / `container/2200/**` | 此范围内符合权限的对象 |
| 数字 ID | `NumericId=...` | 一个确定端点，不支持数字前缀推断层级 |

以上通配式是**展示用**，P0-1 必须决定精确语法及合法字符（建议仅 `*` 单层与 `**` 多层），并在标准库中编译一次匹配条件，不能每次推送依赖正则动态创建。`Machine/Container + AppCode + ModuleCode + Kind` 可组合过滤，但匹配始终限制在当前授权 Scope；跨层订阅默认**只触发存在匹配端点**而不进行无界全量扫描。

### 11.3 推送、订阅、查询之间的关系

- `Subscribe` 注册观察者，收到变化可推送；`Query` 是按实时 Owner 读取一次快照，两者并用时通过 Revision/Sequence 补齐初始化快照和后续增量的竞态；不保证所有订阅者必须使用 Query。
- `Publish` 非持久状态替代；`Get` 不应暗中创建订阅；`Read` 本地低延迟，远端必须显式异步策略。
- 分层订阅产生多条匹配路径时同一个 EventId 对同一消费者按订阅策略去重，避免多次业务执行。
- 订阅对象必须 `Dispose/Unsubscribe`，宿主停止后不可再回调。弱引用不代替生命周期注销；异常观察与订阅分发在 Consumer 隔离策略内执行。
- **订阅不是授权**：订阅范围只是选择候选；实际 Read/Write/Invoke/FetchData 权限仍由资源 Owner 或接收端校验。

### 11.4 共享规则

当前阶段**同进程、同授权 Scope**共享；跨模块通过显式公开与订阅访问。默认不外发；跨进程/跨 PC 需在未来 Bridge 配置中显式 Export 与允许的操作、主题/路径、速率、数据类别。大数据只允许授权获取描述或数据；命令订阅不能自动授予执行能力。

### 11.5 快照—增量握手与订阅身份（P0 固定语义）

对于需要“一开始就有当前值且之后不丢变更”的客户端，先在相同 Scope 内登记订阅及序号水位，再取得权威快照，之后只应用大于水位的增量；如发生变更窗口溢出、序列缺口或版本不兼容，**必须重新获取完整快照**。不能简单先 Query 后 Subscribe 而留下丢更新竞态。按需最新值订阅可合并通知，逐笔审计事件不得合并。

注册信息中的 `SubscriberId` 独立于 SubscriptionToken；同一消费者注册多个重叠过滤器时按 EventId 去重（如其订阅策略要求），不同真实消费者仍各自收到消息。Subscribe 需要明确匹配 Scope、消息 Kind、Schema/TypeId 和授权策略，默认拒绝隐式跨运行实例。

## 12. 公共交互契约与结果语义

### 12.1 公共元信息——统一但不做万能 DTO

`EndpointAddress` 包含 Path+NumericId+Scope/Generation；`Origin` 标记 SourceId、WriterId、Machine/Container、AppCode/ModuleCode；`Correlation` 区分 MessageId、RequestId、OperationId、RunId；`Version` 表示 Schema 与资源版本；`Trace` 在需要时串起跨模块调用。

各 Manager 的接口保持强类型契约。Envelope 只对跨边界的事件/消息或日志需要；不得强制 `MDSDataHeap.Get<T>` 包裹成 MQTT Payload，更不应把命令异常隐藏到 `object`。

### 12.2 发送结果是第一等契约

**SendResult 必须拆成正交字段**，而不是一个混合枚举：

- `Stage` 表示有**证据**到达的最远阶段，例如 `CallValidated / LocalQueueAccepted / TransportWritten / BrokerAcknowledged`；进程内直调可用 `LocalDispatchAccepted`，不冒充远端传输。
- `Outcome` 表示这次发送操作的结果，例如 `Accepted / Rejected / Failed / Backpressured / Cancelled / Unknown`。只有确实完成的阶段才可标记，不能把尝试过的阶段当已完成。
- `Reason/ErrorCode、MatchedCount、AcceptedCount、Destination/SubscriptionResults` 在业务适用时补充；广播报告目标维度的结果。零订阅允许发布与“必需接收者”策略分开规定。

若只能确认本地入队，绝不能返回 `TransportWritten` 或 `Delivered`；若没有应用接收 ACK，就不捏造 DeliveryResult。

- 本地 `Publish` 如果采用同步调用，只能承诺当前函数的可控阶段完成；异步队列承诺“接纳队列”，不能承诺所有回调执行成功。
- 真正接收与命令执行分别使用 `DeliveryResult`、`ExecutionResult`，回调可迟到。
- `CancellationToken` 取消调用方等待，不等于收回已发布消息、撤销控制卡已接收运动；**调用方停止等待、请求取消、执行方已取消**三者必须分开；超时与取消原因分别记录。
- 接收方拒绝、不支持 Schema、Owner 停机、权限失败必须可区分；不能用“已发送”代替“已处理”。

### 12.3 时间、身份与错误

统一使用 UTC 记录，额外按硬件时间域记录设备采样时间；本地 Stopwatch 计算耗时，禁止通过各 PC 绝对时间戳推断严格因果顺序。错误使用稳定 ErrorCode/Status + 原因，不同 Owner 具体异常保留诊断链。对于耗时命令，Result 至少可以按 OperationId 查回；P0 支持进程内，跨进程持久化留 E。

## 13. 高并发、排队、背压与隔离

### 13.1 四条不同逻辑执行路径

- **变量高速路径**：读写原子快照与可选批量提交；推送通知旁路处理，不受慢订阅者和网络阻塞。
- **消息路径**：轻量投递、有界等待、可配置溢出。普通通知可丢弃/合并，**可靠报警/Event 不允许按最新值静默覆盖**；若保障不可达，明确失败/降级并产生诊断（P0 不伪造 Durable）。按订阅者隔离回调。
- **命令路径**：每资源/处理者可声明串行、并发上限、队列等待、资源租约；复用 F19/MRK 而不自建新的调度内核。
- **数据路径**：避免全量拷贝，用 DataHeap/专业 Buffer/File/Stream；大量内容与消息/命令分离，通过独立容量与背压约束，避免大对象占满诊断消息队列。

### 13.2 物理线路共享规则

6/8 项用户决议：**进程外根据业务分开实际线路，部分借助 MQTT，部分可合并借用成熟通路**。但“共享 TCP 连接/线程池”不等于共享无限队列；必须有类别隔离、发送额度和过载策略。P0 不替未来指定统一物理技术栈，先做抽象及进程内功能。

### 13.3 非功能准则

- 任意订阅者超慢或抛异常不拖垮提供方；采图/运动命令具备独立优先级/资源控制。
- 队列/缓冲区必须有上限，并可观测深度、被丢弃/合并数、等待时长；禁止未经证据宣称“无锁”“零拷贝”“硬实时”。
- 进程内公开操作提供取消/Dispose 与生命周期一致性；变更发生在事件派发时不得导致死锁。
- 全局有序性非默认；明确串行化粒度（同变量、同消息 Source+Topic、同命令资源），其他并发不作不实保证。
- 业务价值优先：未测得需要时不预建多个独立 Dispatcher、专用线程及 Broker；先复用现有 F19/MRK/基础线程原语。

## 14. 来源、可靠性、断线及重启恢复

### 14.1 Source 链与版本

`SourceId`（数据最初出自谁）、`WriterId`（谁提交）、`Origin/Producer`（事件发布者）、`Caller/Executor`（命令请求者与执行者）、`Relay/BridgeId`（中继）不能混为同一身份。变更日志至少追踪 `OwnerScope/EndpointId/Revision/ObservedUtc/CommittedUtc/Reason`；审计只存必要元数据或摘要，避免把 8GB Volume 复制成日志。

**覆盖四大管理中心的来源责任**：变量每次提交有 SourceId/WriterId；消息有原始 SourceAddress 与发布者；命令有 CallerId、目标执行者、结果证据来源；MDS 的值/文件/专业数据按其 Owner 能力记录 Provenance/SourceId 或显式“来源未知”。现有 `MDSDataSnapshot.Source` 可能是文件定位路径，**不能直接把该字段解释为新 SourceId**。后续如需新元信息，应通过既有 MDS/专业 Owner 增构、保持兼容；不能在通用 Registry 另存一份事实。

接收端禁止盲信客户端自行声称的 SourceId、WriterId、Role 或 ACL；可信运行上下文/授权接入层负责认证、绑定与审计来源。

### 14.2 四种可靠性默认策略

| 能力 | 默认可靠性 | 不得发生 |
|---|---|---|
| Variable | 当前快照+变化序号；断线标记 Stale；重连比较 Revision 再全量/差量同步 | 丢包后把过期镜像当权威值 |
| Message | BestEffort 或显式要求 DeliveryAck；有界队列；必要事件可持久化扩展 | 无消费者却称业务已收到；重复事件隐式重复执行命令 |
| Command | OperationId 跟踪、超时 Unknown、授权、幂等/查询；业务结果和现场确认分开 | 网络超时无条件重试实际副作用 |
| Data | Owner、生命周期、校验/大小/版本；大数据分块与恢复未来扩展 | 文件未完整/Hash 错却通知数据可用 |

### 14.3 本地重启与跨域重连

P0 进程内：应用关闭需注销 Registry、订阅、Command Handler，取消未完成等待、释放 MDS 资源；下一次启动清理失效 RunId/Generation。对运行中断导致未知的外部物理动作不能将本地进程重启解释为“设备未执行”。

E 扩展：MQTT 重连须重订阅、恢复 Snapshot+Sequence，发现缺口重读权威值；SHM 必须校验代次和租约；文件传输按 ChunkId、版本/Hash 恢复；Command 根据 OperationId 查询远端而非重发；需要恢复存储时与 M35 协同，避免建立第二操作日志。

## 15. 物理总线与跨域桥接（架构预留；不在 P0 实现）

### 15.1 物理分工与部署

| 通路 | 典型数据 | 原则 |
|---|---|---|
| MQTT | 轻量 Message/Event、状态摘要、命令请求/结果通知 | Broker 可选部署；QoS 仅代表协议交付，业务确认靠 Command |
| Named Pipes / IPC | Windows 同机进程间请求、通知、控制 | 先复用本机 OS 能力，不为 IPC 引入独立重型服务 |
| SHM / MMF | 同 PC 大 Buffer/Image/共享索引 | 数据面 + IPC 控制面 + 版本/代次/Lease；SHM 不能跨 PC |
| TCP/TLS | 直连、专用请求响应或稳定高频服务 | 保留现有 Channel/Protocol 的能力，不强制每次远程交互经过 MQTT |
| FileSync / ChunkStream | 大文件、CT Volume、Image、Artifact | 按需服务、分块校验、恢复、限流、权限；不塞到 Broker 消息体 |

### 15.2 Bridge 只做边界转换

桥接步骤：根据订阅/公开清单找到目标 → 核验导出权限与 Schema → 根据业务选择可支持的物理通道 → 编码必要的包络/引用 → 投递/获取下层 SendResult → 接收端校验身份、Generation、权限并交回目标 Owner。禁止 Bridge 持有第二份权威 Variable/Heap 或代替 MRK 执行任务。

### 15.3 订阅路由与环路防护

路径按 `machine/container + AppCode + ModuleCode + Kind` 分层订阅；远端重复转发必须记录 MessageId、Origin、可选 HopCount/VisitedBridge，必要时 TTL、防重放。**跨 PC 要分清共享目录、网络文件、远程服务句柄与本地 SHM 句柄**。部署配置优先沿用正式 FCM，不新建平行 YAML/Manager 真源。

### 15.4 轻量运维约束

默认只需 Runtime.Host 内公共管理者；物理 Broker、独立 FileSync 服务只在真实跨进程/跨 PC 场景启用；首选成熟 MQTT Broker，避免自研 Broker。已有 Channel/Protocol 与 Connector 保持“一 Connector 一 Channel 一 Protocol”架构，不以通用 Bridge 名义推翻当前协议定义。

---

# 第四部分　对象归属、接口与使用

## 16. 对象、接口、枚举的真实归属与增构规则

### 16.1 归属原则

- 先复用已存在类型和项目，不以本章候选名字去“填类数”。所有增构须先核对 DOCS/02～04 中相应 ObjectId、现有类与测试，避免同义异名。
- Foundation 只拥有类型、标识基础、安全数据和值契约，不反向依赖 Mechanism；Mechanism 保持数据、逻辑、Connectivity、Runtime 的稳定依赖方向；Professional 负责业务 Provider；Studio 为操作与观察投影。
- M09、M12、M36、M10/MDS 是**子方面/Owner 标识**，不等于要新建四个 csproj。

### 16.2 当前源码可核实对象（I=源码；测试请见配套矩阵）

| 现有类型/路径 | 唯一归属 | 已核实用途 | 此次增量要求 |
|---|---|---|---|
| `MDSVariableRegistry` `Mechanism/Data/Variables/` | Mechanism.Data / M09 | 按 ScopeId 登记多个变量表 | 保留实例隔离，接统一地址/身份 |
| `MDSVariableDefinition`、`MDSVariableTable` | Mechanism.Data / M09 | Define/Get/TryGet/Update/Sample，Quality 枚举，VariableChanged（**当前在更新锁外同步调用观察者**） | Default/Previous/Source/Writer/Revision、质量更新入口、批量快照、慢订阅者隔离 |
| `MDSVariableSample<T>`、`IMDSVariableTable` | Mechanism.Data / M09 | 采样值、更新时间、初始化信息 | 扩展快照及版本，兼容现有消费端 |
| `MDSDataHeap`、`MDSDataStack` | Mechanism.Data / M10 | 运行值持有/交接，Stack/FlowHeap | 使用现有 API，新增必要的元数据/订阅桥，不做第二 Heap |
| `MDSDataContext` / MDS 查找、FileLoader | Mechanism.Data / MDS | Frame/Context/查询/文件定位 | 保持现有 Scope/Loader；已有 `MDSDataSnapshot.Source` 并不等同 SourceId，不声称 SHM 已实现 |
| `MDSProtocolDataTargetAccess` | Mechanism.Data ↔ Connectivity | Variable/Heap/Stack 映射，Publish/Invoke 可注入 Owner | 接真实 Message/Command，严格授权与错误返回 |
| `MPADataMapping` / `MPADataMappingExecutor` | Mechanism.Connectivity / M14 | Point ↔ Variable/Heap/Stack/Message/Command 操作/多目标映射 | 不重复解释规约或复制 PointTable |
| `IMPADataTargetAccess` | Mechanism.Connectivity / M14 接口 | Read/Write/Publish/Invoke | 对齐新结果，变更必须兼容原协议消费者 |
| `MCCChannelBase`、`MCCSingleChannelBase`、`MCCMultiChannelBase` | Mechanism.Connectivity / M13 | 异步收发、单/多对端、代次 | 保留物理通道职责，不让 Bus 越界解析帧 |
| `MPAProtocolSession`、`MPAProtocolConfiguration`、`MPAPointTable` | Mechanism.Connectivity / M14 | 规约事务、点表、配置 | 只管理规约点，不是统一变量中心 |
| `MRKExternalOperationTracker` / `MRKExternalOperation` | Mechanism.Runtime / MRK | OperationId 去重及 Pending/Unknown/Confirmed/Failed（仅进程内操作观察） | 命令 Dispatcher 复用；其 `Confirmed` 是 Provider **确认操作结论**，不是通用“设备位置已反读到位”证明 |
| `MRKCorrelationWaiter` / `MRKCorrelationTicket` | Mechanism.Runtime / MRK | 一次性请求-响应关联、超时/取消 | 不包装成第二等待器 |
| `MRKResourceLeaseCoordinator` | Mechanism.Runtime / MRK | 进程内资源互斥 | 不替代现场安全联锁 |
| `MPCPortKind` | Mechanism.Core / MEM/MOL | 现有 Data/Control/Event/Command/State/Stream/Resource/Visual 端口分类 | UI/编排复用既有端口种类与身份 |

### 16.3 需要增量补齐的对象职责（P：候选职责，非断言已有类名）

| 功能候选 | 推荐 Owner | 最小能力 | 不允许重复的责任 |
|---|---|---|---|
| `AddressRegistry` / `BusEndpointAddress`（候选名） | 先核对 Foundation 既有 Address/Identity、Mechanism.Core 解析 | Path/Int64/Scope/Generation 的注册、解析、注销、别名 | 不生成第二 MEM 工程身份、二次 ResourceGraph |
| `SnowflakeIdGenerator`（候选名） | Foundation 基础 ID 能力（查重后裁决） | 并发 ID、Worker、回拨、耗尽、唯一性 | 不照抄 ReferTo 全局静态类型分配器 |
| `ScopedSubscription` / Filter（候选名） | M36 公共分发，M09 保留变量事件源 | 按容器/机器/App/Module、Kind 的匹配、释放与隔离 | 不把所有变量推入 Message 才能订阅 |
| `VariableSnapshot/CommitContext`（候选名） | M09 | Default/Previous/Current/Source/Writer/Quality/Revision、原子提交 | 不复制新 VariableManager |
| `MessageDispatcher/DeliveryResult`（候选职责） | M36 | 轻载发布、回调隔离、SendResult、可选 ACK | 不存储大图、不得替代 MRK |
| `CommandRegistry/DispatchContract`（候选职责） | M12 定义/注册，MRK 执行集成，M28 授权 | 目标注册、Invoke/Query/Cancel、结果与未知状态 | 不再造 FlowExecutor 或第二操作追踪器 |
| `DataPublishRef/StreamContract`（候选职责） | MDS 数据所有权 + 专业 Stream 能力 | 元信息变化、受控读取；流容量/帧顺序/背压 | 不先创建泛 SharedMemoryCarrier 空壳 |
| `OutboundBridge`（候选职责，E） | M36 路由 + M13/M14 Transport | 订阅导出、鉴权、编码、代次与转发 | 不成为全系统单点中心 |

**现状证据**：`DOCS/Architecture/CxVisual_统一功能模块体系——通道规约与数据映射架构_V1.3.md` 明确 Message/Command 缺真实 Owner 时 `NotSupported`；`MDS` V3.0 已说明 `MDSDataRef/Lease/SHM` 不是完整公开入口。本文不能擅自反转这些已记录的约束。

### 16.4 建议统一 API 契约（示意签名，待源代码查重与编译）

以下是**目标行为，不是当前可调用源码**：

```csharp
// 建议用途，不作为已实现类声明
AddressResolution Resolve(string pathOrNumericId, RuntimeScope scope);
ISubscription Subscribe(AddressFilter filter, Action<Notification> onChange);
Task<SendResult> PublishAsync(MessageEnvelope message, CancellationToken ct);
Task<CommandOperation> InvokeAsync(CommandRequest request, CancellationToken ct);
Task<CommandStatus> QueryOperationAsync(Guid operationId, CancellationToken ct);
```

每个接口规范必须补充：参数合法性、返回错误状态、持有者、线程模型、取消和 Dispose、事务/原子性、复杂度、角色权限、是否持久、测试标识。在实施前先调查已有 `MPCPort`、`MRIObjectAddress`、`MDS...`、`MRK...` 能否直接复用，可能无需新增上述名字。

### 16.5 V1.3 消息/命令对象闭环——职责而非强制新增类

| 对外概念 | 默认调用面 | 归属/实现边界 | 现状 |
|---|---|---|---|
| Message Topic/Type | `Publish(Topic,Payload)`、`Subscribe(Topic,callback)` | M36 分发；消息事件类型遵守 M12 既有身份 | P：示意调用面 |
| 消息实例与发布回执 | MessageId/Origin/Payload、Stage+Outcome+MatchedCount | M36 内部封装，调用者不必显式 new Envelope | P |
| CommandDefinition | CommandId、CommandCode、Name、Inputs、Outputs、Description/Version | 既有 MEM/FCM/Capability/专业定义，M12 公共契约，不复制配置源 | P：需 DOCS/04 查重 |
| Command Handler 登记 | `RegisterCommand` | M12 轻量路由，真实执行由专业 Provider/MRK | P |
| Command Request/Result | `InvokeAsync`、输出类型、ErrorCode、OperationId | M12 请求入口；MRK 状态/关联/资源，M28 授权 | P：部分 MRK 基础 I |
| 进程外适配 | MQTT Topic/Payload 与 Reply/Correlation 映射 | M13/M14/Bridge，P1+ 实施 | E |

**命名裁决顺序**：先从 DOCS/04 找正式 TypeId/Owner，再从源码查找既有接口，最后才允许增构；本节列的是**功能合同而非一次批准六个新类**。


### 16.6 OO、GoF、AOP 三维完整设计原则

**全类不等于堆类数。** 先从 Definition → Runtime Instance → Registry/Manager → Dispatcher/Handler → Result → Lifecycle 区分对象，再以 GoF 隔离真实变化点，用 AOP 处理权限、审计、追踪、参数和异常等横切关注点。Foundation/Mechanism/Professional 是程序集及 Owner 维度，不是 AOP 的替代品。

**当前事实门禁**：`DOCS/ProjectCatalog.csv` 将 M09/M10/M35/M36 归 Mechanism.Data、M11/M12 归 Mechanism.Logic；`Mechanism/Logic/*.csproj` 尚无正式命令处理体系；`Mechanism/Data/Variables/Porting` 下的复制源码不属于正式编译文件。现有 `MRIObjectAddress`、`MDSVariableTable/Registry`、MDS Heap 与 MRK 操作追踪优先复用。下述英语名字除标 `[已有]` 的实际类型外均为**类/接口候选名而非已完成类或已批准的正式类型名**。编写源码前还须对照 DOCS/02～04 逐表核实 Owner/TypeId。

### 16.7 对象关系与双管理者边界

```text
编辑态：MEM / FCM / Capability / MOL ——[CommandDefinition / Message Type]
                                         ↓ 构建、发布、运行装配
运行态：Host Composition Root / InteractionScope / 统一 AddressRegistry
        ├── MessageManager (M36) → Dispatcher → SubscriptionRegistry → N个 Subscriber
        │       ↑ Topic + Payload/MessageEnvelope             ↓ Dispose/ACK(可选)
        ├── CommandManager (M12) → HandlerRegistry → 一个专业 CommandHandler
        │       ↑ CommandRequest              ↓ typed CommandResult
        │       └───── MRK Operation/Correlation/Resource（唯一执行真源）
        ├── MDS/M09：Value、Image、Volume 的唯一数据Owner
        └── IInteractionClient：简洁 Facade，不是第五个全局管理器
未来外部 Bridge（MQTT/IPC/SHM）只在边界编码转发，不替代上述 Owner。
```

**Manager 区别**：AddressRegistry 只管端点身份；MessageManager 负责发布/订阅组织，SubscriptionRegistry 只管观察关系；Command Definition Catalog 读取工程发布定义、HandlerRegistry 管当前运行处理者、CommandManager 负责调用入口；MRK 管真实执行操作。不同状态可以用不同类，若没有独立生命周期和变化理由可以合并为同一实现，**禁止创建多个互相争夺真值的管理者**。

### 16.8 公共身份/寻址/作用域与入口：U01～U08

| ID | 对象 / 形式 | 关键成员与方法（目标语义） | 对象职责与协作边界 | 生命周期 / 状态 |
|---|---|---|---|---|
| U01 | EndpointAddress（值） | Path/NumericId/Scope/Generation/Kind；Normalize/Equals | 基础身份契约，适配现有 MRIObjectAddress；不复制工程真源 | 随引用存续，P |
| U02 | IEndpointRegistry（接口） | Register/Resolve(Path或Id)/Unregister/Query | 同 Scope 双索引访问合同，不管理权威数据 | 由 Host 提供，P |
| U03 | ScopedEndpointRegistry（实现） | 双索引、Owner、Generation；Dispose | 唯一端点目录的运行实现，维护注册关系 | Scope 停止销毁，P |
| U04 | SnowflakeIdGenerator（服务） | Worker/Clock/Sequence；NextId() | Foundation ID 生成；复用 ReferTo 思想但修复多实例冲突 | Host 生命周期，P |
| U05 | ITopicMatcher（策略） | Compile(pattern)/Match(topic) | Topic精确、单层*与多层**匹配，不能当权限 | 无状态/可缓存，P |
| U06 | InteractionScope（组合根角色） | Host/Project/Run；Start/Stop/Dispose | 装配、停止并释放对象；不生成第二资源树 | Host生命周期，P |
| U07 | IInteractionClient（Facade） | Publish/Subscribe/InvokeAsync | 面向开发者/Studio 简洁入口，不存真值 | 由组合根注入，P |
| U08 | InteractionContext（只读值） | Caller/Source/Scope/Trace/Run/Cancellation | 可信运行上下文，不接受伪造身份 | 一次调用，P |

### 16.9 消息类：定义、实例、管理、处理、订阅、反馈 M01～M11

| ID | 对象 / 形式 | 关键成员与方法（目标语义） | 对象职责与协作边界 | 生命周期 / 状态 |
|---|---|---|---|---|
| M01 | MessageTypeDescriptor（定义） | TypeId/SchemaVersion/PayloadType/Validate | 强类型或持久消息可选，临时消息无需正式定义 | 定义期，P |
| M02 | MessageEnvelope<T>（实例） | MessageId/Topic/Source/Time/Payload/Correlation | 一次发布事实，轻量不可变 | 一次发布，P |
| M03 | PublishOptions（选项） | DeliveryMode/RequiresSubscriber/Overflow | 个别发布覆盖默认策略 | 一次发布，P |
| M04 | PublishReceipt（结果） | Stage/Outcome/MatchedCount/ErrorCode | 仅表示受理/发送，不冒充业务执行 | 一次发布结果，P |
| M05 | MessageManager（管理者） | Publish<T>/Subscribe<T>/Stop | M36 聚合入口，持 M06/M07，不重建基础地址表 | Scope生命周期，P |
| M06 | MessageDispatcher（处理者） | Dispatch/Enqueue/Drain/Stop | 队列、有界背压、回调隔离 | Scope生命周期，P |
| M07 | MessageSubscriptionRegistry（管理者） | Register/Match/Remove/Snapshot | 保存 SubscriberId/Filter/订阅关系，不保存消息真值 | Scope生命周期，P |
| M08 | MessageSubscription（订阅对象） | SubscriberId/Pattern/IsActive/Dispose | 一次注册的状态及取消令牌 | 从注册至注销，P |
| M09 | IMessageSubscriber<T> 或委托 | OnMessageAsync(payload,context) | 业务观察者；简单场景用委托即可 | 订阅有效期间，P |
| M10 | IMessageDeliveryPolicy（策略） | Overflow/Ordering/Ack/SelectQueue | 通知与关键事件按需不同可靠性，不每消息创建子类 | 由管理者持有，P |
| M11 | MessageDeliveryObservation（可选） | MessageId/SubscriberId/Ack/ReceivedUtc | 要求应用级 ACK 才存在，不能代替发送回执 | 受控投递期，P/E |

**消息闭环**：可选 TypeDefinition → `Publish(Topic,Payload)` → 可信来源/类型/Scope校验 → 生成 Envelope → MessageManager 使用 TopicMatcher/SubscriptionRegistry 匹配 → Dispatcher 接纳有界队列 → 返回 PublishReceipt（受理阶段） → 各 Subscriber 隔离回调 → 可选应用级 ACK → Dispose/Drain。**Publish 成功不等于消费完成**。同一个 Source/Topic 可有多订阅者，但订阅授权不意味着有 Invoke 权限。

### 16.10 命令类：定义、参数、注册、处理、执行结果 C01～C14

| ID | 对象 / 形式 | 关键成员与方法（目标语义） | 对象职责与协作边界 | 生命周期 / 状态 |
|---|---|---|---|---|
| C01 | CommandDefinition（定义） | CommandId/Code/Name/Inputs/Outputs/SchemaVersion | 在现有 MEM/FCM/Capability 作为唯一发布定义 | 工程定义期，P |
| C02 | CommandParameterDefinition（参数） | Code/Type/Required/HasDefault/Default/Min/Max/Unit | 参数值与约束，默认0/false与缺省分开 | 随定义存在，P |
| C03 | ICommandDefinitionCatalog（查询接口） | Find(id或Owner+Code)/List | 仅查询既有工程定义/Descriptor，不建第二持久库 | 工程/运行装配，P |
| C04 | CommandHandlerRegistration（注册令牌） | DefinitionId/Owner/Generation/Dispose | 把定义绑定当前实例 Handler，失效可注销 | 注册至注销，P |
| C05 | CommandHandlerRegistry（管理者） | Register/Resolve/Unregister | 一个Target+Code+Scope+Generation只能有一个执行者 | Scope生命周期，P |
| C06 | ICommandHandler<TIn,TOut>（行为接口） | ExecuteAsync(inputs,context) | 真实业务由 Professional Provider 执行 | 专业服务生命周期，P |
| C07 | CommandRequest<T>（请求实例） | Target/Arguments/RequestId/OperationId/Caller | 一次调用，身份由平台生成、参数校验后执行 | 一次调用，P |
| C08 | CommandInvocationContext（上下文） | Identity/Auth/Run/Correlation/Cancellation | 将已受信任上下文传给业务 Handler | 一次调用，P |
| C09 | CommandResult<T>（结果） | Status/Outputs/ErrorCode/Message/OperationId | 结构化输出独立于执行状态和现场确认 | 一次调用结果，P |
| C10 | CommandManager（入口管理者） | Register/InvokeAsync/Query/Cancel | 调用门面与 Handler 协调，不持第二 MRK Operation | Scope生命周期，P |
| C11 | CommandInvoker（可选内部处理） | Resolve/Validate/Authorize/Dispatch | 流转复杂时才从 C10 分离，不为套模式独立建壳 | 可内聚 C10，O |
| C12 | ICommandExecutionPolicy（策略） | EvaluateConcurrency/Retry/Resource | 声明串行、并发、重试、资源，不替代专业安全 | 按命令配置，P |
| C13 | ICommandMiddleware（可选横切契约） | InvokeAsync(context,next) | 承接权限/追踪/审计等横切逻辑；不依赖反射代理 | 可内聚于管道，O |
| C14 | CommandOperationView（投影） | OperationId/State/Progress/Result/Query | 只投影已有 MRK Tracker/Correlation，不存第二真值 | Operation生命周期，P |

**命令闭环**：由 MEM/FCM/Capability 保存 CommandDefinition + Input/Output Schema → MOC 检查版本、绑定与类型 → Runtime 实例将唯一 Handler 绑定到当前 Generation → `InvokeAsync(Target,Inputs)` → 生成 RequestId/OperationId → 校验必填、默认值、类型、目标/权限 → MRK 关联与资源仲裁 → Professional Handler 执行 → 类型化 Outputs、错误与 MRK Operation 状态 → `Query/Cancel`（需要时） → 可选设备实际状态反读。**CommandId 是定义身份，RequestId 是请求身份，OperationId 是动作身份，三者不能混用**。普通用户只需填写目标和参数。

### 16.11 既有 Owner 和专业适配：X01～X08

| ID | 对象 / 形式 | 关键成员与方法（目标语义） | 对象职责与协作边界 | 生命周期 / 状态 |
|---|---|---|---|---|
| X01 | MDSVariableTable/Registry [已有] | Define/Get/Update/Sample/Register/Find | M09权威变量Owner；不要把高频状态写入消息总线 | Scope生命周期，I |
| X02 | MDSDataHeap/Stack/Context [已有] | Get/Set/Move/Copy/Inspect/Load | MDS 权威数据Owner；消息仅发数据定位 | Run生命周期，I |
| X03 | MRKExternalOperationTracker/CorrelationWaiter [已有] | Register/TryGet/Observe/Receive | MRK 唯一执行关联与状态；C14只读投影 | Runtime生命周期，I |
| X04 | MRKResourceLeaseCoordinator [已有] | AcquireAsync/AcquireManyAsync/Release | 软件资源互斥，非硬件安全联锁 | Runtime生命周期，I |
| X05 | MDSProtocolDataTargetAccess [已有] | Read/Write/PublishAsync/InvokeAsync | 适配旧 MPA 与正式消息/命令Owner；拒绝虚假成功 | Connector生命周期，I |
| X06 | MPADataMappingExecutor [已有] | ApplyInboundAsync/ReadOutboundAsync | 点表数据映射，复用现有协议/规约能力 | Protocol会话，I |
| X07 | Professional XxxHandler（实现候选） | ExecuteAsync(typedArgs,context) | Camera/Axis/Vision/CT的具体业务执行，隔离厂商 SDK | 专业设备生命周期，P |
| X08 | MQTT/IPC/SHM Bridge（扩展） | Export/Subscribe/Map/Forward/Reconnect | 按需物理传输，不复制命令/消息/数据Owner | P1+ 扩展，E |

**实现数量约束**：U8 + M11 + C14 + X8 = **41个职责角色**，其中有接口/值对象/可选策略及既有类；**不是批准新增41个 C# 类**。优先增构已存在类、合并无独立状态的实现角色，再决定新增文件。M12/Logic 不依赖具体 Professional SDK；MRK 没有被 CommandManager 复制出第二套 Executor；Studio 不产生第二持久命令表。项目依赖方向以实际 `.csproj` 为准，不允许 Data/Logic/Runtime 循环引用。

### 16.12 GoF 模式映射——解决变化而不是装饰架构

| 模式 | 使用位置 | 价值与限制 |
|---|---|---|
| Observer | MessageSubscription/MessageDispatcher | 一对多通知与取消；不用于广播执行设备命令 |
| Command | CommandRequest/ICommandHandler/CommandResult | 调用者与实际执行者解耦；不产生新的 FlowEngine |
| Mediator | MessageManager/CommandManager | 模块之间由路由中心协调，业务不直接握对方内部实例 |
| Facade | IInteractionClient | 给用户 Publish/Subscribe/Invoke 三个熟悉入口，保持底层对象体系完整 |
| Strategy | IMessageDeliveryPolicy/ICommandExecutionPolicy | QoS、背压、并发和资源策略可替换；普通场景用默认策略 |
| Adapter | MDSProtocolDataTargetAccess、未来 MQTT Bridge | 适配旧 Protocol/MPA 与新消息命令，不破坏原有签名 |
| Chain of Responsibility / Decorator | ICommandMiddleware/显式 Pipeline | 组合认证、参数校验、授权、日志与计量，不能让拦截器重复执行业务 |
| State（按需） | MRK 运行状态 + CommandOperationView | 投影已有 MRK 状态，绝不另建 CommandStateMachine 账本 |
| Factory（按需） | 专业模块装配 Handler | 确实存在多厂商/多 Provider 才使用，不为单一命令创建工厂 |
| Bridge（GoF）与跨进程 Bridge（术语） | 交互抽象与 MQTT/IPC 策略 | 后者是实际通信接入，**P0不要求 Broker** |

### 16.13 AOP/横切方面：可组合但不拖慢实时路径

```text
Publish: Scope/Origin → Topic/Schema/大小 → Publish ACL → MessageId
         → Topic/订阅匹配 → 有界队列 → Receipt → Callback/异常诊断 → 可选ACK
Invoke : Caller/Scope/Generation → Definition/Handler解析 → 参数默认/Schema
         → Owner最终授权 → MRK操作登记/幂等/资源 → 真实Handler
         → 输出类型校验/结果 → Trace/审计/资源释放 → [需要时]现场确认
```

横切机制应复用现有安全、FCM校验、MRK、F19和诊断 Owner，优先由显式接口/管道组合，不依赖 PostSharp、动态代理或新依赖。认证失败不得进入 Handler；业务失败必须释放租约；SendResult 不得由日志组件改写为 Completed；Variable.Get/Set 高频路径只保留必要的类型/权限/原子提交，不强制重型 AOP 链。拦截器重复调用 `next()` 会导致设备动作重复，必须作为反例测试。

### 16.14 生命周期、线程/状态的一致性

| 时机 | 管理对象 | 必须满足的条件 |
|---|---|---|
| 编辑/保存 | MEM/FCM/CommandDefinition | 保存稳定定义，不保存 Handler 委托、运行 Operation 或临时 Token |
| Host 启动 | Scope/AddressRegistry/MessageManager/CommandManager | 按 Project/Host/Run 隔离，不使用全局静态真源 |
| 注册 | SubscriptionRegistry/HandlerRegistry | 可重复查询、冲突拒绝、一个命令只有一个正式 Handler |
| 发布/调用 | Dispatcher/Handler/MRK | 发布与消费分离、发送与执行分离、过载拒绝/Unknown 不虚报成功 |
| 注销/停止 | Subscription/HandlerRegistration/Scope | 阻止新入列，新旧 Generation 隔离，已在执行的 Operation 由 MRK 结算 |
| 故障/重试 | MRK/Owner/Policy | 取消等待不等于设备取消，重试先查原 Operation，资源/队列不泄漏 |
| 数据共享 | M09/MDS/专业 Owner | 回调仅通知引用与元信息，Image/Volume 不被消息复制 |

### 16.15 API轮廓、完整类型设计验收与精简规则

```csharp
// 以下为目标契约的简化示意，不是仓库现有可编译API。
public interface IInteractionClient
{
    PublishReceipt Publish<T>(string topic, T payload, PublishOptions options = null);
    IDisposable Subscribe<T>(string pattern, Func<T, Task> callback);
    Task<CommandResult<TOut>> InvokeAsync<TIn,TOut>(
        string commandPath, TIn args, CancellationToken cancellation);
}
public interface ICommandHandler<TIn,TOut>
{
    Task<TOut> ExecuteAsync(TIn inputs, CommandInvocationContext context);
}
// 与现有 MDS Heap / MRK / MPA 保持唯一真实 Owner，不在 Facade 中复制它们。
```

**逐类验收格式**：`角色ID → 完整类名/现有替代 → Owner/项目 → 属性 → 方法签名 → 关系/依赖 → 生命周期/线程 → GoF/AOP → 已实现或待实现 → 用例ID`。每一职责至少有成功与拒绝/异常测试；`I/A/P/O/E` 分别表示源码已实现/已有类增构/候选/按需模式/跨域后续。**第16章为设计级全类闭环；仍未逐表核实 DOCS/04 Excel，也未声明新增类已编译或验收**。


## 17. 软件开发者实际与目标使用案例

### 17.1 已有源码：变量定义、写入、读与变化观察（I）

```csharp
using System;
using CxVisual.Mechanism.Data.Variables;

public static class VariableApiExample
{
    public static void Run()
    {
        MDSVariableTable table = new MDSVariableTable("machine-1201/camera");
        table.Define(new MDSVariableDefinition("Ready", typeof(bool)));
        table.VariableChanged += name => Console.WriteLine("变量更新: " + name);
        table.Update<bool>("Ready", true);
        bool ready = table.Get<bool>("Ready");
        MDSVariableSample<bool> sample = table.Sample<bool>("Ready");
        Console.WriteLine("Ready=" + ready + ", initialized=" + sample.HasInitialValue);
    }
}
```

上述是核对过的现有 API 用法，**未体现旧值/默认值/SourceId 等尚待扩充的功能**；回调目前直接同步调用，因此不得据此声称高速订阅已经与消费者隔离。

### 17.2 已有源码：Heap 安全保存、读取、元信息观察（I）

```csharp
using CxVisual.Mechanism.Data.RuntimeData;

public static class DataApiExample
{
    public static void Run()
    {
        using (MDSDataHeap heap = new MDSDataHeap())
        {
            heap.Set<string>("SN", "A0001");
            heap.Set<byte[]>("ImageHeader", new byte[] { 1, 2, 3 });
            string sn = heap.Get<string>("SN");
            byte[] copy = heap.Get<byte[]>("ImageHeader");
            MDSDataSnapshot meta = heap.Inspect("ImageHeader");
            // meta 是元数据；完整 Image/Volume 还需专业 Loader/Adapter。
        }
    }
}
```

### 17.3 已有源码：外部命令不确定状态观察（I）

```csharp
using System;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Execution;

public static class OperationApiExample
{
    public static async Task<MRKExternalOperationObservation> RunAsync()
    {
        MRKExternalOperationTracker tracker = new MRKExternalOperationTracker();
        bool created;
        Guid operationId = Guid.NewGuid();
        MRKExternalOperation operation = tracker.Register(operationId, "capture-001", out created);
        MRKExternalOperationObservation observation =
            await operation.ObserveAsync(TimeSpan.FromMilliseconds(10));
        // 可能 TimedOut=true 且 State=Unknown；不得立即认定 Capture 失败。
        // Provider 根据自己掌握的执行证据调用 Confirm/Fail，
        // 不意味着完成了现场状态反读。
        return observation;
    }
}
```

这是操作观察与状态登记的现有能力，**不是**已实现完整 Camera Command Dispatcher。

### 17.4 P0 目标完整组合场景（调用流程示意，非现有 SDK）

1. 在 Scope `machine/1201` 注册 Camera.Ready 变量与 Capture Handler，发布 Camera.Captured 消息定义，授权读取 ImageHandle。
2. Studio 以 `machine/1201/app/XRayInspection/module/Camera/**` 订阅变化，并按 Address/Id 查询最新 Ready 快照；订阅者结束就释放令牌。
3. Studio 发 `Capture`；拿到的第一阶段是 `SendResult`，最多只能说明本地/物理发送的明确阶段成功。
4. Command Owner 校验操作者身份、参数与设备状态，创建/查找 OperationId，MRK 运行受控 Capture；业务接受、实际采图完成通过独立状态上报。
5. Image 放入专业数据 Owner 或 MDS 持有的合法数据对象，Camera.Captured 消息只携带 ImageRef；AI 通过 `RunInspection` 执行并读取受控 ImageRef。
6. AI Result 经受控数据/专业结果 API 输出，Inspector 订阅事件、设备变量呈现最新状态。任何一步失败、超时、来源失效，都能依据 Id/OperationId/RunId 追查。

该场景既需要软件开发者 API，也需要 Studio 操作和 Windows 真实测试，不能以设计中存在这六步宣布已验收。

### 17.5 消息 + IoT 风格命令：一段完整使用路径（目标示意）

~~~text
1. 工程配置 Camera.Capture：CommandCode=Capture，Inputs=[ExposureMs]，Outputs=[ImageName]。
2. Runtime 的 Camera Provider 登记此命令的真实 Handler；不注册则 Invoke 返回 UnknownCommand。
3. MOL 或 Studio 执行 Invoke(Camera/Capture,{ExposureMs:50})；平台生成 RequestId/OperationId。
4. Handler 真实采图，把图像放入受控 DataHeap/专业 Owner，返回 {ImageName:"Image001"}。
5. Runtime 另外 Publish(Camera/Captured,{ImageName:"Image001"})；AI 和监控可各自 Subscribe。
6. AI 从真实 Data Owner 获取图像，另行 Invoke(Vision/RunInspection,{ImageName:"Image001"})；
   命令输出结构化检测结果，MDS/MRK/专业结果 Owner 保持唯一真源。
7. 任一错误经 CommandResult/ErrorCode 反馈，Studio 显示 Operation 状态；
   消息推送失败不倒写“采图命令已成功”；设备动作是否发生以专业证据为准。
~~~

第4步 Command 返回结果，第5步 Message 另行发布，两者可关联 `CorrelationId`，但**并不互相替代**。进程内 ImageName 必须受 Owner/Run/Generation/Type 校验；不能裸用同名字符串在跨 Scope 任意取图。

## 18. 工程编排用户的 Studio 使用指南（目标体验）

### 18.1 Studio 面板归位

- 左：复用现有 Resource Explorer、资源/模块/变量对象树与工具箱，不另建隐藏工程树。
- 中：已有配置组合面板与流程/图元主画布；普通总线配置采用组合面板，流程调用使用现有 MOL Port/Edge。P0 UI 明确装配**同进程真实测试宿主**，不是与独立 Runtime.Host 直接通信。
- 右：FCM 现有 Property/Binding/Source/Permission/Subscriber 等属性配置；高级 Bridge 仅外域开发阶段显示。
- 下：诊断、实时数据、变量变化、消息收发、操作状态、调用关联、日志与测试证据，使用现有停靠体系。

### 18.2 具体界面验收操作

1. 进入机器 `1201` / App `XRayInspection` / Module `Camera`，选择变量 `Ready`，显示 Path/ID、Current/Previous/Default、Quality、SourceId、WriterId、Timestamp、Revision；当前未支持字段标“未实现”，不得显示伪值。
2. 在订阅编辑器中选 `MachineId=1201, AppCode=XRayInspection, ModuleCode=Camera`，选择“变量变化”或“事件”，即时预览匹配数、授予的资源，保存后可在订阅列表注销。
3. 选择 `Capture` 命令，填写参数，点击执行；先显示发送受理，再显示业务接受/进度/完成；超时显示 Unknown 并提供 Query/Refresh，不显示虚构的 Completed。
4. 在数据面板查看 `ImageHandle` 的类型、大小、来源与归属；图像按授权打开专业 Viewer，默认不复制或在日志中渲染全部二进制。
5. 保存工程并重开，仅持久化**订阅定义/过滤配置和稳定工程引用**；运行中的订阅令牌、临时数字 ID、缓存值、Operation 等不作为工程事实保存；重新进入运行态时按新实例 Scope/Generation 重新装配并获取权威快照。

### 18.3 适应进程内阶段的简化

P0 不在 Studio 强制要求 Broker 地址、TCP 端口或共享内存名称；仅配置可读的 Address/Scope 和业务订阅/绑定。新手默认看四种操作，诊断或高级用户才展开 DeliveryPolicy、Revision、Backpressure 和限额。

---

### 18.4 V1.3 IoT 风格命令编辑与调用体验

- **资源/模块的命令清单**：列表展示 Code、Name、输入/输出参数；高级属性折叠 SchemaVersion、权限、执行策略。无需单独设计一套 IoT“服务树”。
- **参数编辑**：通过现有 FCM Schema/类型描述生成输入控件，必填、默认、范围和单位可见；布尔 false、数字零不能被误判断为缺省。高级用户可以绑定变量、堆或前序节点输出。
- **MOL 命令节点**：从已发布 CommandDefinition 生成真实输入与输出端口；MOC 编译前校验缺参数、类型不兼容、错误作用域、无 Handler/未授权风险；不复制第二端口定义。
- **消息订阅**：选择 Topic/机器/App/模块范围，界面可显示 MatchedCount、发布受理与消费异常，不把 Publish 回执混写为业务处理成功。
- **命令执行面板**：显示等待/已受理/运行/完成/失败/未知和 OperationId（高级详情）；短命令直接显示结构化输出，长命令可 Query/Cancel；现场确认只在该命令声明需要时出现。
- **保存与重开**：只保存稳定 CommandDefinition/订阅表达式，不持久化运行时 Handler 委托、SubscriptionToken 或临时 Operation 对象。P0 的真 Studio/UI 验证使用同进程测试宿主，不声称已接远端 Runtime.Host。


# 第五部分　实施与验收

## 19. 分阶段开发计划

**优先顺序：真正可用的进程内交互 → Studio/集成 → 有证据的跨进程 → 多 PC。** 不为后续 Bridge 先写假的 Owner。与其它正在开发的模块相互协调，以 `master` 最新 HEAD 做实际差距判断。

| 工作包 | 功能闭环 | 必须拥有的证据 | 不属于本包 |
|---|---|---|---|
| P0-1 双寻址 | Scoped Registry、Path/Snowflake、注册/注销/冲突、别名、精确查找 | API+重命名/碰撞/时钟回拨/多Scope测试 | 远端发现服务 |
| P0-2 变量 | Default/Previous/Current、Source/Writer、Revision、Quality 原子更新；高频订阅/批量策略 | 一致性/并发/断订阅/压力测试 | PLC 真实 HIL |
| P0-3 消息 | Publisher/Subscriber、分层过滤、SendResult、队列/异常隔离 | 单/多订阅、无消费者、异常、顺序/去重 | MQTT Broker |
| P0-4 命令 | Handler 注册、权限、Operation、Invoke/Cancel/Query、分层执行与确认 | 成功/拒绝/超时/Unknown/去重/迟到应答 | 远程安全设备验收 |
| P0-5 数据协作 | Heap/Stack/变量/消息/命令受控引用闭环；可支持 Heap 条目序列化/反序列化 | 生命周期、并发、来源追踪、Codec校验与恢复回滚 | SHM/跨 PC 8GB 实装 |
| P0-6 Studio+TestCenter | **同进程测试宿主**中的真实 UI 配置、运行观察、诊断与恢复；不宣称外部独立 Runtime.Host 已联通 | WPF STA、保存重开、x64 Windows CI、错误反证 | 独立 Studio↔Runtime.Host 的 IPC（P1）与双机 |
| P1 IPC/SHM | 两进程可访问范围、代次/共享缓存、异常恢复 | 两独立进程测试、进程崩溃与释放 | 多机网络 |
| P2 MQTT/TCP | 可选 Bridge、分层导出、接收/命令关联、掉线同步 | Broker/双 PC 故障注入、鉴权/TLS | 大文件业务完成 |
| P3 FileSync/Stream | 大文件、分块/校验、续传、Volume/ROI | 双 PC 完整性、背压/资源释放、故障重试 | 纯设计冒充大体积性能 |

每个工作包都在实施台账记录：准确 Git SHA、Owner、源码/测试、实际运行命令、结果链接、未满足反例、协作冲突。**当前文档生成不等于任何 P0 包已完成。**

## 20. TestCenter、Windows CI 与最终验收

### 20.1 验收层级

1. **Contract/API**：所有输入约束、对象 Scope、类型、生命周期、Send/Delivery/Execution/Confirmation 语义一致。
2. **机制集成**：M09/M36/M12/MDS/MRK/MPA 只有一个真源；一对多 Point Mapping 的失败传播正确；没有新增万能 Manager。
3. **并发异常**：变量高频更新时慢订阅者不阻塞提交；消息异常隔离；命令超时 Unknown/迟到结果；Heap 不被普通消息投递放大拷贝。
4. **Studio**：选择范围、保存重开、变量状态面板、消息列表、命令运行结果、DataRef Viewer 与诊断。
5. **Windows/真实设备**：构建 `CxVisual.Unified.sln` 目标项目及对应 Smoke/TestCenter，跑实际 Windows CI；未来双进程、双 PC、实机 HIL 与网络故障分开记录。

### 20.2 P0 最小闭环（不可删减）

同一进程的受控测试 Host 装载两个独立 Scope，各自有 `Camera.Ready`；同一个源采集点更新一组数据并触发正确的分层订阅；同进程消费者 A 按 App 订阅、消费者 B 按 Module 订阅（Studio 可通过内嵌测试 Host 验证 UI）；快速变量写持续发生时慢消费者不会阻塞；一次 Capture Command 正确区分 Accepted、Completed 和设备 Confirmed；另一次超时转 Unknown 并在迟到反馈后确定；产生的 ImageRef 可受控被 AI 消费，释放后不可继续访问；注销模块/订阅后旧代次不会收到新数据；权限失败/地址冲突/Schema 不匹配均返回可定位的错误。

### 20.3 质量门禁

- 任何一条验证必须记录 **TestId、Prerequisite、Owner、刺激、断言、反例、Test Runner、SHA、运行环境、实际结果**。
- **模拟≠实机**；Socket loopback≠工业 PLC；进程内 Registry≠ IPC/MQTT；成功 CI 编译≠ Studio 人工交互验收。
- 对已实现但本轮未重跑测试的类型标“I/历史测试入口存在”，不写“本轮通过”。
- 性能阈值（变量更新吞吐、订阅延迟、队列容量、峰值内存、Image/Volume 数据面速度）由第一轮基准实测、目标业务负载和设备预算三者共同冻结；不凭空写 1ms/无阻塞/零拷贝。
- 完整细项、编号和失败注入见本套文件的 TestCenter 验收矩阵。

---

### 20.4 V1.3 消息/命令轻量闭环的专项验收

须在配套 TestCenter V1.2 矩阵中独立验证：Topic/Path/ID 一致、允许轻消息未登记类型、强类型消息 Schema 失败、零订阅/重叠订阅/慢消费者、明确发布回执；CommandDefinition 输入输出/默认值/必填/范围/SchemaVersion、Studio 自动编辑、MOL 端口同源、唯一 Handler、无 Handler/未授权、参数不合法不执行、调用与操作身份自动生成、执行结果与现场确认分层、异步长任务 Query/Cancel、旧 MPA 映射兼容。P0 所有这些用例当前都是**新增计划，NOT_RUN**；不得写成已通过。


# 附录 A　决议与禁止事项

| ADR | 已确定（D） | 强制约束 |
|---|---|---|
| ADR-BUS-01 | **四大能力：变量高速、消息轻巧、命令做事、数据堆大数据高效管理** | 各有独立语义/性能路线 |
| ADR-BUS-02 | **进程内逻辑总线 + 进程外真实物理总线** | P0 先完成进程内；不强制集中 BusManager |
| ADR-BUS-03 | **双寻址：路径 + 数字 Snowflake** | 双地址解析同一端点，不替换 MEM GUID |
| ADR-BUS-04 | **各管理中心记录数据来源与权威值** | 变量必须支持 Previous/Current/Default、SourceId 等 |
| ADR-BUS-05 | **通过 Machine/Container、AppCode、ModuleCode 分层订阅和桥接** | 权限校验独立于订阅匹配 |
| ADR-BUS-06 | **共享遵循分层范围和明确授权** | 不默认导出全部 Heap/Variable |
| ADR-BUS-07 | **可靠性按业务各自选择，发送≠执行** | 支持明确 SendResult、Command Operation/Query、未知结果 |
| ADR-BUS-08 | **物理并发分开真实线路、部分共用 MQTT 或其它成熟通路** | 大数据流不能阻塞控制与报警；不一刀切总线 |
| ADR-BUS-09 | **断线、重启、结果查询、状态恢复** | 不重复执行未知副作用 |
| ADR-BUS-10 | **部署与维护先做进程内** | P0 不要求 Broker/SHM/文件同步服务 |

**永久禁止**：新全局静态万能 EventBus、第二个 VariableStore/Heap/OperationTracker/调度器、Bridge 直接执行专业命令、在 Channel 重新实现规约分帧、把整张图送消息正文、将任何 `SendAsync` success 称为设备动作成功、硬实时控制交给 Windows/MQTT。

# 附录 B　已核对源码及 ReferTo 与文档对照

> 核对日：2026-10-24，仓库 `master@36855f3`。路径为相对仓库根目录。

| 依据 | 事实 | 与本设计关系 |
|---|---|---|
| `AGENTS.md`；`DOCS/00...md` | 28 项目、源码/文档/测试分级、七仓成熟复用 | 不凭规划或参考源码宣称功能完成 |
| `DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md` §10–§14 | Command/State/Event/Stream 分离；专业契约；硬实时边界 | 两层总线不得破坏最高约束 |
| `Mechanism/Data/Variables/MDSVariableTable.cs` / `MDSVariableRegistry.cs` / `MDSVariableContracts.cs` | Scope 变量表、值/质量/同步变更事件 | 旧值/默认值/来源/Revision 尚待真实开发 |
| `Mechanism/Data/RuntimeData/MDSDataHeap.cs` / `.Query.cs` | Set/Get/Replace/Inspect 与值/文件元信息 | Heap 不具备已证实跨进程数据总线 |
| `Mechanism/Connectivity/Protocols/Mapping/MPADataMapping.cs` | TargetKind 有 Variable/Heap/Stack/Message/Command | 正式映射入口，不可重建第二张表 |
| `Mechanism/Data/Variables/MDSProtocolDataTargetAccess.cs` | 接入外部 Publish/Invoke 回调，缺 Owner 会拒绝 | P0-3/4 接入目标，不得“默认成功” |
| `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | MRKExternalOperationTracker、MRKCorrelationWaiter、ResourceLease | 命令复用真实机制，不建第二执行器 |
| `Mechanism/Core/Engineering/MPCPort.cs` | MPCPortKind 有 Data/Control/Event/Command/State/Stream | Studio/MOL 类型在旧端口体系中已有对应语义 |
| `DOCS/Architecture/CxVisual_统一功能模块体系——通道规约与数据映射架构_V1.3.md` | 一 Connector 一 Channel/Protocol、Mapping独立、TCP/UDP/Serial/Virtual、Modbus 03/06 增量闭环 | Bridge 与既有 Connector 分工，不推翻新裁决 |
| `DOCS/Architecture/CxVisual_运行时数据体系——完整对象规格与双角色使用指南_V3.0.md` | 两 Data Owner、MDS 简单 API、共享/Lease 候选范围 | 不强迫每个业务拿底层 DataRef/Lease |
| `DOCS/Codebases/03_gcl3.md` | GeneralAccess、PSM/Bus、数据共享与消息协议复用建议 | 历史能力参考，非 x3 已落地 |
| `ReferTo/DeviceVariable/DeviceSuite/Core/Snowflake.cs` | 旧 SnowflakeManager/Snowflake 使用时间戳、类型位、Worker/Sequence | P0-1 审核后迁移，不能直接照搬 |
| `ReferTo/DeviceVariable/MessageInterfaces/MessageCenter.cs` | 旧消息中心参考 | 仅抽取必要行为，禁止恢复静态全局中心 |
| `DOCS/02～04` 三份 Excel | 在仓库入口列明为规格/归属权威文档；**本版未逐 Sheet 核对** | 类名、Namespace 与 ObjectId 最终冻结前必须补查 |

# 附录 C　需求—Owner—验收追踪入口

| 需求 | 真源/责任 | 测试组 |
|---|---|---|
| 双寻址/多作用域 | Foundation ID 候选 + Mechanism Scoped Registry | BUS-ID-01～10 |
| 变量旧/当前/默认、来源、Revision | M09 | BUS-VAR-01～13 |
| 轻量消息/分层订阅 | M36 | BUS-MSG-01～14 |
| 命令授权/执行/Unknown/幂等 | M12/M28/MRK/Professional | BUS-CMD-01～16 |
| Heap/Stream/序列化/来源追踪 | MDS/Professional | BUS-DAT-01～12 |
| 无堵塞/资源预算/故障隔离 | F19/MRK/M09/M36/MDS | BUS-NFR-01～08 |
| Studio 真实配置/诊断 | Studio/FCM/MOL | BUS-UI-01～09 |
| 跨进程/跨 PC（未来） | M36 Bridge + M13/M14 + 专业数据 Owner | BUS-EXT-01～08 |

# 附录 D　尚未裁决的实现参数（不阻碍总体架构）

1. **地址语法**：斜杠与百分号转义、大小写敏感性、MaxDepth/MaxPath、`*`/`**` 语法；P0-1 定。
2. **Snowflake 编码布局**：时钟起点、WorkerId 分配、NodeId、跨系统唯一性、跨进程时钟回拨策略；P0-1 结合 ReferTo 实测定。
3. **变量旧值细节**：同值重复更新是否推进 Previous、Default 的初始化政策、批量快照的原子粒度；P0-2 通过测试冻结。
4. **消息保障默认值**：本地同步/队列的默认选择、无订阅者返回状态、队列与溢出策略；P0-3 以测试和性能证据定。
5. **命令完成/现场确认边界**：纯计算命令无需现场确认；设备命令由专业定义反馈证明标准，P0-4 定义细目。
6. **容量与性能 SLA**：高频变量吞吐、发布延迟、消息容量、并发命令数、图像块大小/SHM 最大尺寸；依据真实测量设定，不预造数字。
7. **跨域技术选型**：MQTT Broker 具体产品、IPC 格式、文件服务协议、DataHandle 网络形态；P1～P3 结合设备情况定。
8. **DOCS/02～04 Owner/Class 逐项一致性**：实施前补全 Excel 原类身份、项目/子方面及 API 冲突清单；未完成之前所有新增类名为“候选”。

**版本原则**：用户已明确的 ADR 不得仅因编码便利静默推翻；若存在与最高宪法、真实源码或 DOCS/04 不一致的地方，记录冲突、影响、迁移方案后单独裁决。每次开发必须更新实施台账与测试矩阵的实际 SHA，不能直接改写本规格宣称已完成。


## V1.2 审查修订记录（不改变已批准 ADR）

1. 明确 PreviousValue 始终是上次成功提交值，同值重复提交也必须保持严格语义，区分 ValueChanged/Committed。
2. 将发送阶段 Stage 与发送结果 Outcome 拆开；接收方 ACK 与 Handler 执行结果分开。
3. 区分工程持久端点地址与运行实例地址；保存订阅定义不保存临时路由身份。
4. 区分 MRKExternalOperation.Confirmed 和设备现场 ConfirmationResult。
5. 明确进程内 P0 的 UI 需同进程测试宿主，独立 Studio↔Runtime.Host 放 P1。
6. 增补四类管理中心 Source 责任、订阅快照握手、慢回调/注销语义以及真正可编译形态的 C# 使用例子。
7. 补充 DataHeap 序列化/反序列化的受控快照、恢复、兼容性和失败回滚要求；不得把元信息查询当成完整持久化。
8. 逐项类名/Owner 仍需核对 DOCS/02～04 Excel；本版未运行 Windows CI、TestCenter、工业 HIL。


## V1.3 本轮收敛记录

- 简化消息：对外 Topic + Payload + Publish/Subscribe；内部默认附来源/消息身份/回执，IoT/MQTT 只是使用风格，不要求内嵌 Broker。
- 简化命令：对外 CommandId + CommandCode + Name + Inputs + Outputs，继承既有参数类型与配置；Service/分组可选。
- 命令定义不等于调用实例：RequestId/OperationId/Caller 由平台生成并复用 MRK；CommandResult 结构化、可以查询未知状态。
- 注册→受理→处理→反馈→订阅/释放的正反闭环已纳入规格、台账与矩阵；**不宣称已有代码或测试**。
- IoT 参考：`device_communicator` 历史代码只证明 MQTT 控制入口与点表/事件，**不证明其已有完整命令模型**；参考华为云 [IoTDA命令接口](https://support.huaweicloud.com/api-iothub/iot_06_v5_0038.html) 的服务/命令名/参数/响应结构，但不耦合云厂商字段。


## V1.4 全类设计增补记录

V1.4 将定义、处理、流转、管理者、策略、生命周期在41个职责角色上逐项映射，并规定 GoF/AOP 的实际使用及避免过度设计的约束。角色数不是新增C#类数；DOCS/04逐表类型查重、正式签名冻结、WindowsCI和TestCenter仍待后续开发完成。


## V1.5 决议补记

- 消息定义增加`Topic`和结构化/文本/二进制/引用Payload的约束；实际Payload在消息实例中。
- Topic订阅使用标准`+/#`，具体消息路由与订阅Filter分开；不把订阅当授权。
- 命令参数补单位/范围/说明，命令定义补默认超时/权限键/说明；继续不复制MRK操作状态。

> **2026-10-24 新Owner裁决**：命令相关代码正式收敛至 `Foundation.Data.Commands`（FDC参数/定义/契约）与 `Mechanism.Runtime.Commands`（MRK中心/执行/MDS适配）两个已有项目；历史M12/MCE归属不再是当前实现。详见 `DOCS/Decisions/CxVisual_统一命令中心双项目归属裁决_V1.0.md`。
