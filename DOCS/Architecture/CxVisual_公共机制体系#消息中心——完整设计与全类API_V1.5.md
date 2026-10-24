# CxVisual 公共机制体系 # 消息中心——完整设计与全类 API V1.5

> 2026-10-24；适用 CxVisual.NET Framework 4.8 / Mechanism.Data M36；父规格：统一交互 V1.5。  
> 宗旨：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**  
> **状态标记**：I=本轮源码已提交待CI，P=下一阶段规划，E=以后跨进程扩展；不可把 I 理解为 Windows 验证通过。

## 1. 范围、基本原则与不可越界

CxVisual 消息中心用于多模块异步事件、轻量通知和请求响应；开发者只需 Topic / Payload / Publish / Subscribe，对象间通过 Observer/Mediator 解耦，消息不存业务真实数据。进程内无需 MQTT Broker，借鉴 MQTT 5 Topic、Shared Subscribe、QoS 0/1 的**可验证语义**；只提供进程内交付和临时保留，不声称网络 QoS 或磁盘持久化。参考标准：[MQTT Version 5.0，OASIS](https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html)。

**Owner**：Foundation.Data/Core 提供已经存在的通用数据、身份值和异常基础；**消息数据契约/Publisher和Codec接口由现有 Foundation.Data.Messages 持有，MessageCenter及订阅/分发/Codec实现由现有 Mechanism.Data.Messages 持有**。仅这两个既有 .csproj 是消息体系的编译 Owner，不新造工程。M09 VariableTable 与 MDS Heap 仍是真值所有者；M12 Command/Professional Handler/MRK 是执行责任方，任何订阅不能代替授权命令。与 MPA 旧接口通过现有 `MDSProtocolDataTargetAccess.PublishAsync` 的回调适配，不直接替换接口签名。

## 2. Topic 与 Payload 模型

**发布 Topic**：`/machine/{MachineId}/app/{AppCode}/module/{ModuleCode}/message/{TopicCode}` 或 `/container/{ContainerId}/...`。代码段大小写敏感，P0 采用八段 ASCII 安全 Code，`+` 与 `#` 不能出现在真实 Topic。Topic 可用 `MNMMessageTypeDefinition.WithTopic` 将复用 Schema 绑定到具体模块。

**订阅 Filter**：`+` 完整一层；`#` 完整最后一层，允许匹配零层及多层；`/machine/1201/app/XRay/#`、`/machine/1201/app/XRay/module/+/message/Captured`。Shared 订阅通过 `$share/group//machine/...`（两个斜杠表示 CxVisual Topic 的起始绝对路径）标识；同组同 Filter 每消息只选一个订阅者，不同组分别竞争；该形式对应 MQTT 的 `$share/ShareName/TopicFilter`，不是 Broker 会话或 durable share。

| PayloadKind | .NET Payload | 实际约束 |
|---|---|---|
| Text | `string` | UTF-8 字节长度、PayloadType 验证 |
| Binary | `byte[]` | 入站复制、读取再次复制、防生产者改变缓冲区 |
| Structured | 强类型 DTO | 进程内传对象，**调用方保证 DTO 生命周期/不可变性**；不自动执行 JSON 或 Protobuf |
| Reference | DataRef/受控定位 | 不经消息复制 CT Volume/大图；Owner 仍验证引用合法性 |

`ContentType` 只是 wire 表示，不自动生成 Codec。结构化 DTO 的真实编码体积必须由以后 Bridge 使用的 Codec 校验；如果要可变 DTO 的深拷贝，先提供明确 Snapshotter/Lease 契约，不可伪称当前强制冻结。MessageTypeDefinition 有 TypeId/SchemaVersion/Topic/Name/Description/PayloadKind/PayloadType/ContentType/MaxPayloadBytes；具体 Payload 值位于每次 `MNMMessage.Payload`。

## 3. 消息全类职责（已经落位的核心API及扩展点）

| 类型/接口 | 核心属性 | 方法、参数、返回值及线程职责 | Owner/状态 |
|---|---|---|---|
| `MNMMessageTypeDefinition` | TypeId,Topic,TopicCode,PayloadKind,PayloadType,ContentType,MaxPayloadBytes,SchemaVersion,Name,Description | `WithTopic(path):Definition`; `ValidateTopic(path)`; `Validate(payload)`，只描述静态契约 | M36/I |
| `MNMTopicFilter` | Filter | `ValidateTopic(path):string`; `ValidateFilter(pattern):string`; `Matches(topic):bool`；纯匹配，无授权副作用 | M36/I |
| `MNMMessageHeader` | Guid MessageId,Topic,TypeId,SourceId,TargetId,CreatedUtc,CorrelationId,ReplyTo,ExpiresUtc,QoS,Headers | 仅由 Center 构造、只读身份与元数据快照，MessageId ≠ EndpointId | M36/I |
| `MNMMessagePayload` | Value,PayloadType,Kind,ContentType | `Get<T>():T`；Binary 读写复制 | M36/I |
| `MNMMessage` | Header,Payload | 一次发送实例；Definition 不保存实际值 | M36/I |
| `MNMPublishOptions` | SourceId,TargetId,TypeId,DispatchMode,QoS,Retain,RequireSubscriber,TTL,CorrelationId,ReplyTo,Headers | 一次发布选项；用户输入 SourceId 不能冒充经认证的 Host 身份 | M36/I |
| `MNMPublishReceipt` | MessageId,Stage,MatchedCount,DeliveredCount,FailedCount,Error | Stage=Rejected/Accepted/Delivered/Failed；同步 `Publish` 默认只返回队列受理状态 | M36/I |
| `MNMSubscriptionOptions` | SubscriberId,ReplayRetained,SourceId,TypeId,NoLocal,Predicate,SynchronizationContext | 订阅行为；Predicate 是应用过滤、不是 MQTT Topic Filter | M36/I |
| `MNMMessageSubscription` | Filter,IsActive,订阅ID/SubscriberId/SharedGroup | `Dispose()` 注销，已开始执行的回调不会被强制中断 | M36/I |
| `MNMMessageContext<T>` | Header,Message,Payload | `ReplyAsync<TReply>(response,CancellationToken):Task<Receipt>` 保留 CorrelationId | M36/I |
| `MNMMessageCenter` | State,队列、订阅快照、消息定义只读映射、保留/死信、Metrics | `RegisterType`、`Subscribe<T>`、`SubscribeAsync<T>`、`Publish<T>`、`PublishAsync<T>`、`PublishBatchAsync<T>`、`RequestAsync<TReq,TResp>`、`Pause/Resume/StopAsync/Dispose` | M36/I |
| `IMNMMessagePublisher` | — | `Publish<T>`,`PublishAsync<T>`；解耦发布者对 Center 实例的直接依赖 | M36/I |
| `IMNMMessageSubscriber` | — | `Subscribe<T>`,`SubscribeAsync<T>` | M36/I |
| `MNMMessageOptions` | QueueCapacity,RetainedCapacity,DeadLetterCapacity,MaxAttempts,RetryDelay,OverflowPolicy,AuthDelegates | `Validate()`；Manager 的有限资源策略 | M36/I |
| `MNMMessageMetrics` | Published,Accepted,Delivered,Failed,Retried,Rejected,Dropped,QueueDepth,SubscriptionCount,... | `Metrics():MNMMessageMetrics` 返回锁内快照 | M36/I |
| `MNMDeadLetter` | MessageId,Topic,SubscriberId,Error,OccurredUtc | `DeadLetters():MNMDeadLetter[]`；有限记录，不包含大Payload | M36/I |
| `MNMDeliveryWork` | Message,匹配订阅者快照,Receipt,TaskCompletionSource | 一次队列传送工作项，由 Center 唯一拥有 | M36/I 内部 |
| MessageRouter | TopicMatcher、SubscriberId、共享组轮询游标 | `ResolveSubscriptions(message)` 暂由 Center 的受锁方法承担；**待大规模订阅性能证据出现才拆成独立路由类** | M36/I（职责内聚） |
| MessageDispatcher + MessageQueue | FIFO Queue、SemaphoreSlim、resume信号、单Consumer | `Enqueue`、`RunWorkerAsync`、`DeliverAsync` 暂由 Center 内聚；以后变化时可以提取为独立对象，不新增项目 | M36/I（职责内聚） |
| MessageScheduler | CallbackContext | `SynchronizationContext.Post` 在订阅选项中驱动，未引入第二线程池管理中心 | M36/I（内聚） |
| MessageFilter | Topic/Type/Source/Predicate | Topic先匹配，Type/NoLocal/Predicate后判断；Predicate抛错进入死信 | M36/I（内聚） |
| MessageRetryPolicy | QoS、MaxAttempts、Delay | QoS0一次，QoS1当前进程内最多N次；失败DLQ | M36/I（内聚） |
| MessageRequestManager | Correlation/ReplyTo/TaskCompletionSource | `RequestAsync<TReq,TResp>` 暂由 Center 承担，临时订阅生命周期受控；非命令执行管理者 | M36/I（内聚） |
| Middleware/Codec/ExternalBridge | 后续协议传输编码、追踪、权限适配 | 现阶段未引入，禁止假成功或声称跨进程可用 | M13/M14/P/E |

**避免为40项需求创建40个没有状态的壳类**：职责明确，经理类管一致性和生命周期，路由/队列/策略可按真实变化点独立抽出。要按GoF Observer/Strategy/Mediator/Facade组合扩展；横切安全审计尽量适配现有Owner，不让高频变量更新强制走消息队列。

## 4. 模块通信时序

~~~mermaid
sequenceDiagram
    participant A as Camera 模块
    participant MC as MNMMessageCenter
    participant R as Topic / Subscription Router
    participant Q as 有界FIFO队列
    participant B as AI/监控 Subscriber
    A->>MC: Publish(Camera/Captured, ImageRef)
    MC->>MC: Topic/Schema/Auth/TTL校验 + Header
    MC->>R: 选匹配Subscription
    R-->>MC: 订阅者快照(可空)
    MC->>Q: Enqueue (或拒绝)
    MC-->>A: Receipt(Accepted/Rejected)
    Q->>B: onMessage(context)
    alt Success
      B-->>Q: Complete
    else QoS1且出错
      Q->>B: 重试(当前进程内)
    else 终局失败
      Q->>MC: 有界死信/失败计数
    end
~~~

### 4.1 Publisher 与 Observer

~~~csharp
using (MNMMessageCenter bus = new MNMMessageCenter())
using (bus.Subscribe<string>(
    "/machine/1201/app/XRay/module/+/message/Captured",
    ctx => Console.WriteLine(ctx.Header.Topic + ": " + ctx.Payload)))
{
    MNMPublishReceipt receipt = bus.Publish(
        "/machine/1201/app/XRay/module/Camera/message/Captured", "Image001");
    // receipt默认Accepted表示队列已接纳，不等于AI已消费。
    MNMPublishReceipt completed = await bus.PublishAsync(
        "/machine/1201/app/XRay/module/Camera/message/Captured", "Image002");
    // completed.DeliveredCount反映同进程回调情况。
}
~~~

### 4.2 同步/异步/批量，线程调度

同步回调：订阅使用 `Subscribe<T>(filter,Action<MNMMessageContext<T>>)`，发布可指定 `DispatchMode=Inline`，返回具体回调结果；**Inline在调用线程执行，不适合耗时任务/UI线程回调链**。默认 `Publish` 投递有界队列并立即返回，`PublishAsync` 在队列处理完后返回。批量 `PublishBatchAsync` 顺序等待以保障可追踪性；高吞吐场景可用并发 PublishAsync + 监控队列背压。

WPF UI 使用订阅的 `MNMSubscriptionOptions.SynchronizationContext` 让回调在指定线程运行；后台工作在唯一有界队列 Worker 中串行，避免“每消息创建一线程”。当前实现 **FIFO 入列**、按快照次序执行消费者；不宣称多个外部发布者的自然业务时间顺序，也不为共享组实现严格单会话顺序保证。

### 4.3 共享订阅 / 竞争消费

~~~csharp
IDisposable workerA = bus.Subscribe<string>(
    "$share/inspect//machine/1201/app/XRay/module/Camera/message/Captured",
    ctx => InspectA(ctx.Payload));
IDisposable workerB = bus.Subscribe<string>(
    "$share/inspect//machine/1201/app/XRay/module/Camera/message/Captured",
    ctx => InspectB(ctx.Payload));
~~~

同一 `inspect + Filter` 组每条消息仅选一个，按当前成员轮转；普通订阅仍广播。没有磁盘持久消费者Session，不能保证某机器崩溃后已经受理的消息得到重放。

### 4.4 消息 Request/Reply，不等于 M12 命令

~~~mermaid
sequenceDiagram
    participant Client
    participant Center
    participant Service
    Client->>Center: RequestAsync(RequestTopic, Payload, ReplyTopic, Timeout)
    Center->>Center: 临时订阅 ReplyTopic + CorrelationId
    Center->>Service: 推送 Request(含 ReplyTo/CorrelationId)
    Service->>Center: ReplyAsync(Response)
    Center->>Client: CorrelationId 匹配响应
    Note over Client,Service: 超时=观察未知，不代表业务没有执行
~~~

~~~csharp
using (bus.SubscribeAsync<int>(
    "/machine/1201/app/XRay/module/Camera/message/Captured",
    async ctx => { await ctx.ReplyAsync("OK:" + ctx.Payload); }))
{
    var response = await bus.RequestAsync<int,string>(
        "/machine/1201/app/XRay/module/Camera/message/Captured", 42,
        "/machine/1201/app/XRay/module/Camera/message/Reply",
        TimeSpan.FromSeconds(3));
}
~~~

**安全边界**：请求响应只能表达信息请求或已受控业务反馈；设备运动、采图指令、流程启动仍必须走 M12 CommandDefinition → 唯一 Handler → MRK → 专业 Owner 并验证授权。Header SourceId 是提供方标识/审计字段，**不是可信的认证身份**；当前 Host 注入 Publish/Subscribe AuthDelegate 仅提供拦截点，不是完整 M28 授权集成。

## 5. 质量语义与边界

| 能力 | 当前实际契约 | 非保证 |
|---|---|---|
| QoS 0 | 每个匹配订阅者最多一次回调尝试 | 不保证业务成功或进程重启后的重发 |
| QoS 1（仅本进程） | 订阅者抛异常时有限次数重试，可能重复业务副作用 | 不保证Broker语义、跨进程、恰好一次、重启后可靠交付 |
| 背压 | 有界消息工作队列，满时 Reject 或 DropOldest | 丢弃策略不能丢已经排队的QoS1工作项 |
| Retain | 每Topic保存最新一条消息并对新普通订阅者重放，条数有上限 | 只在内存，不写入磁盘；共享订阅不回放 |
| TTL | 到期后不分发；超时请求释放临时订阅 | 回调已开始时不能强制终止副作用 |
| 顺序 | 单队列按入队先后调度（回调任务等待完成） | 不保证并发业务因果顺序、网络重连顺序 |
| 异常 | 回调隔离、可选重试、错误计数、有界死信元数据 | DeadLetter记录不是持久消息仓库，不自动全量回放 |
| 停止 | StopAsync(drain) 拒新并完成队列处理，Dispose 非等待式强制拒新和清理 | 外部异步回调的资源由业务 Owner 最终回收 |
| 监控 | Pub/Accepted/Delivered/Retry/Failed/Dropped、队列深度、订阅/保留/死信数 | 不预设未经实际Windows测试证明的UPH/尾延迟SLA |

## 6. 类关系与主要流转

~~~mermaid
classDiagram
    class IMNMMessagePublisher{
      +Publish(topic,payload,options) Receipt
      +PublishAsync(topic,payload,options,ct) Task~Receipt~
    }
    class IMNMMessageSubscriber{
      +Subscribe(filter,callback,options) IDisposable
      +SubscribeAsync(filter,callback,options) IDisposable
    }
    class MNMMessageCenter{
      +RegisterType(definition)
      +Subscribe()
      +Publish()
      +PublishAsync()
      +PublishBatchAsync()
      +RequestAsync()
      +Pause()
      +Resume()
      +StopAsync()
      +Metrics()
      +DeadLetters()
    }
    class MNMMessage{
      +Header
      +Payload
    }
    class MNMMessageHeader
    class MNMMessagePayload
    class MNMTopicFilter
    class MNMMessageTypeDefinition
    class MNMMessageSubscription
    class MNMMessageContext
    class MNMDeliveryWork
    class MDSVariableRegistry
    class MDSDataHeap
    IMNMMessagePublisher <|.. MNMMessageCenter
    IMNMMessageSubscriber <|.. MNMMessageCenter
    MNMMessageCenter o-- MNMMessageSubscription
    MNMMessageCenter o-- MNMDeliveryWork
    MNMMessageCenter --> MNMMessageTypeDefinition
    MNMMessageSubscription --> MNMTopicFilter
    MNMDeliveryWork --> MNMMessage
    MNMMessage *-- MNMMessageHeader
    MNMMessage *-- MNMMessagePayload
    MNMMessageContext --> MNMMessage
    MNMMessageCenter ..> MDSVariableRegistry : 不复制变量真值
    MNMMessageCenter ..> MDSDataHeap : 大数据仅引用
~~~

## 7. 场景及完整使用期望

1. **X-Ray采图完成**：相机采图写 MDS/专业图片Owner后 Publish Camera/Captured(ImageRef/尺寸)；AI、统计、HMI各自 Subscribe；原图不在消息体内。
2. **状态通知**：PLC点位真实值由 M09 VariableTable 保持，关键边沿/批次结束才 Publish Topic；不能每次Get/Set都经过消息队列。
3. **设备报警**：Driver 发布 Faulted 结构化DTO；设备健康、安全模块只读订阅；执行复位必须由明确 M12 Command 发起。
4. **检测结果**：Inspection 的批次结果放专业Owner/MDS，对监控发小 DTO；多个消息接收者不再保存第二结果表。
5. **共享任务**：两台分析消费者通过同一个 `$share/.../filter` 竞争处理；如需可靠任务持久分发，应升级到 MRK/专门队列 Owner，不声称进程内QoS1可跨重启恢复。

## 8. 并发、状态、异常与资源生命周期

- Center 只在内部锁下更新索引、订阅快照、指标和有限缓存；**不得在该锁内执行用户 Handler/Predicate/设备调用**。注册取消不再进入新快照，已经开始的 Handler 不能靠 Dispose 自动回滚。
- 单消费者 FIFO 异步队列使用信号唤醒；回调可由订阅指定的 `SynchronizationContext` 调度。同步 Publish(Inline) 由调用者承担阻塞与异步回调死锁风险，特别是 WPF UI 线程。
- 消息内容不共享未经控制的可变 Buffer。完整 JSON/Protobuf Schema/Codec、跨进程身份、持久Session、资源租约在后续桥接实现，不可将当前空缺解释为已经达到外部可靠性。
- StopAsync(true) 是排空已入队工作，StopAsync(false) 丢弃待处理，Dispose 立即停止接受新消息且不等待当前执行 Handler；既有任务的最终取消与设备安全由真实Owner负责。
- 死信队列仅保存有限错误元信息而不保留图片等大Payload，统一记录 MessageId/Topic/SubscriberId/错误；业务不可据此自动重做不可逆操作。

## 9. 实施优先顺序与反例验收

**P0-3/M36 本轮**：消息类型、Topic/Subscription、Publish/Subscribe、Queue/Dispatcher职责、请求响应、共享订阅、Retain、QoS有限重试、DeadLetter、Metrics、生命周期；单元、并发、性能采样、错误反证必须在 Windows CI 编译执行。测试ID由独立Testing矩阵登记；不以Github提交成功代替CI通过。

**后续的显式增强**：独立可注入 codec、用户中间件管道、可配置高性能Topic Trie、持久化队列/ACK重放、跨进程 IPC/SHM/MQTT桥接、M28正式认证信任链、TestCenter真实WPF工作台。需要技术上确实有变化才拆 Scheduler/Router/RetryPolicy 独立实现类；全部留在 Owner M36，不能新增项目或另造全局Bus。

## 10. 40项需求核对

1–5：总体目标与进程内交互；6–10：Header/Payload、Topic、注册、发布、路由；11–12：同步/队列/SyncContext及广播/共享/请求响应；13–14：进程内QoS0/QoS1、保留/TTL、有限队列；15–18：顺序/背压、类型/来源/内容过滤、Correlation与超时、重试和死信；19–20：Pause/Resume/Stop及指标；21：Codec/Bridge及中间件扩展位置留明示待增强；22–30：模型、接口、经理/处理/策略对象及生命周期的逐类归属见§3；31–34：分层、Owner复用、两幅Mermaid图；35–38：工业场景、真实示例、承诺和不承诺的边界；39–40：Tests/ArchitectureSmoke与Windows真实CI为唯一交付证据。

**必须诚实保留的范围**：跨进程/跨PC、多进程持久QoS1、外部MQTT broker、Web Studio操作界面、外部设备HIL均不在进程内中心本轮实际代码内；完整投产前需分阶段实现并验收。


## 11. V1.1 真实路由与嵌套请求修订

- `MNMMessageRouter` 已成为独立内部协作类，只读取 Center 的订阅表快照，按 `Topic + Type + SubscriberId + SourceFilter + TargetId + SharedGroup` 精确解析；**不建立第二个订阅仓库**。
- `TargetId` 绑定订阅者 ID 后为点对点，不会泄漏给其他订阅者；`SourceFilter` 是可选来源过滤，**不是可信身份认证**。
- `RequestAsync` 从发布请求开始计算超时，覆盖请求处理与响应等待；超时只结束调用方的等待，不撤销已开始的业务动作。
- 单 Worker 的回调内嵌套 `PublishAsync` 采用受限（16层）内联分发，避免等待自身队列造成死锁。**这种嵌套消息会先于外层队列后续消息到达，不能再宣称绝对全局FIFO；普通非嵌套发送仍保持入队先后顺序**。



## 12. V1.2 补齐：ThreadPool/Codec/策略、线程安全与观测

**设计核心**：一个真实Owner(M36)管理定义、消息实例、订阅、路由、队列与生命周期；Foundation仅复用通用Data/ID。面向外部的惯用接口仍是 `Publish/PublishAsync/Subscribe/SubscribeAsync/RequestAsync`，不会要求开发者操作十几个管理者或Broker。M09变量、MDS大数据及M12命令/MRK执行是独立Owner。

### 12.1 新增类和签名（与现有对象协作，而不是创建影子管理中心）

| 类/接口 | 属性/关键签名 | 关系/生命周期/线程责任 |
|---|---|---|
| `MNMMessageThreadPoolScheduler`(internal) | `TrySchedule(Func<Task>):bool`, `InFlight:int`, `DrainAsync():Task` | Center组合，锁保护有限Task；不持数据真值 |
| `MNMMessageRetryPolicy`(internal) | `Attempts(MNMMessageQoS):int`, `BackoffAsync():Task` | Strategy：QoS0一次，QoS1有限次；绝非持久化重试或跨重启保障 |
| `IMNMMessageCodec` | `ContentType:string`, `Encode(object,Type):byte[]`, `Decode(byte[],Type):object` | 传输编码策略接口，不能自动决定外部消息来自可信类型 |
| `MNMTextMessageCodec` | 严格UTF-8编码/解码 | 纯文本，非法UTF8拒绝 |
| `MNMBinaryMessageCodec` | Buffer双向复制 | 保护调用方与消费者之间的字节数组所有权 |
| `MNMJsonMessageCodec` | .NET48 `DataContractJsonSerializer` | 显式强类型DTO、JSON读写；不根据Payload中类型名动态反射 |
| `MNMMessageSerialization` | `Serialize(definition,payload):byte[]`; `Deserialize(definition,bytes):object` | Codec适配门面，按Kind/ContentType选择，不支持的格式抛NotSupported，编码后校验MaxPayloadBytes |
| `MNMMessageOptions`(增构) | `MaxParallelDispatches:int` (默认8) | Host构造时复制；不能运行中篡改额度 |
| `MNMDispatchMode`(增构) | `Inline / Queued / ThreadPool` | 默认仍为Queued |
| `MNMMessageMetrics`(增构) | `ParallelInFlight:int`、`LatencySamples:int`、`Mean/P95/P99DeliveryMilliseconds:double` | 最近1024个耗时样本的有限环形缓冲，不承诺硬件SLA |
| `MNMMessageCenter`(增构) | `DispatchParallel`、`CaptureOptions`、`CaptureSubscription`、`CapturePublish` | 订阅配置注册时快照；发布配置每次发送快照；以Center为唯一管理者 |

**既有职责继续复用**：Header/Payload/Message是一次消息实例；MNMTopicFilter只匹配Topic；Subscription代表订阅和IDisposable；Router按TypeId/SourceFilter/TargetId/共享组选择；Queue是有界FIFO；Context支持ReplyAsync；DeadLetter为有限错误元信息；RequestAsync负责当前进程内临时回复订阅。没有必要为这些职责再建设第二个消息服务/订阅表。

### 12.2 线程与时序保证

| 模式 | 执行方式 | 顺序/背压保证 | 适用场景 |
|---|---|---|---|
| `Queued` (默认) | FIFO单Worker、订阅者顺序回调 | 通常按入队先后；慢消费者可能拖住该队列，容量耗尽会Reject/DropOldest | 明确顺序的模块通知、业务事件 |
| `Inline` | 发布者线程同步执行 | 发布者承担阻塞；不允许在有SynchronizationContext的GUI线程同步等待 | 简单本地同步回调、测试 |
| `ThreadPool` | 单次发布在受限线程池任务内完成 | 并发量MaxParallelDispatches硬限制，容量满立即Rejected；**不保证同Topic先后顺序** | 互不依赖的统计、通知、慢回调隔离 |

**特别说明**：Handler内部递归await PublishAsync可能转为有深度上限的内联执行，所以严格全局FIFO并不适用于嵌套触发消息。对必须有顺序的同Topic，使用Queued并限制业务在回调中对顺序敏感的递归发送。

**生命周期**：StopAsync(drain:true)排空FIFO后还等待正在运行的ThreadPool任务；StopAsync(false)拒绝未处理队列工作，但不把已开始的外部动作伪称已取消。Dispose快速释放注册，不等待外部设备操作；已经发出的真实设备命令必须走M12/MRK的安全路径。

### 12.3 四种Payload与实际编码

| PayloadKind | 当前内存值 | 当前实现的Codec | 不能推断的能力 |
|---|---|---|---|
| Text | `string` | 严格UTF-8 | 字符长度不等于编码字节长度 |
| Binary | `byte[]` | 读写Buffer拷贝 | Buffer存在不等于可拥有外部大图像 |
| Structured | 强类型DTO | 明确`application/json`使用DataContractJsonSerializer | 没有给出Codec时不会自动JSON或Protobuf；DTO默认不深拷贝 |
| Reference | 受控定位/引用对象 | **通用Codec尚无，NotSupported** | 需要MDS/专业Owner的作用域、Lease和生命期，不能凭字符串随意读图 |

~~~csharp
MNMMessageSerialization serializer = new MNMMessageSerialization();
MNMMessageTypeDefinition definition = new MNMMessageTypeDefinition(
    "Inspection.Result", 1, typeof(InspectionDto),
    payloadKind: MNMPayloadKind.Structured,
    contentType: "application/json", maxPayloadBytes: 65536);
byte[] json = serializer.Serialize(definition, new InspectionDto());
object dto = serializer.Deserialize(definition, json);
~~~

上例InspectionDto是专业工程的数据模型。Codec显式在边界使用，并不表示外部MQTT Bridge已经完成。

### 12.4 用法与处理时序

~~~mermaid
sequenceDiagram
    participant Pub as Camera Publisher
    participant Center as MessageCenter
    participant Router as MessageRouter
    participant Pool as Bounded ThreadPool
    participant Handler as AI Subscriber
    Pub->>Center: Publish(Topic,Payload,Mode=ThreadPool)
    Center->>Center: Type/ACL/TTL + Snapshot Options
    Center->>Router: Resolve(Topic/Target/Source/Group)
    Router-->>Center: 有效消费者快照
    Center->>Pool: TrySchedule
    alt 超出并发上限
      Center-->>Pub: Receipt(Rejected)
    else 接纳
      Center-->>Pub: Receipt(Accepted)
      Pool->>Handler: Execute callback
      opt 进程内QoS1且回调抛错
        Pool->>Handler: 有限重试
      end
      Handler-->>Center: Completed/Failed
      Center->>Center: Metrics + DeadLetter
    end
~~~

### 12.5 GoF、AOP与完整性边界

- **Observer**：Topic订阅对多个业务消费者解耦；**Mediator**：Center/Router负责关系与匹配；**Strategy**：RetryPolicy/Codec/DispatchMode；**Facade**：Publisher/Subscriber API；**Adapter**：未来MPA/IPC/MQTT。
- **横切关注点**：Topic/Type/ACL/TTL校验、异常隔离、追踪、Metrics统一处理；不让每次M09高速Variable.Get/Set都走昂贵分发链。
- **安全**：SourceId是调用者标签，不是已认证Caller；生产应用必须与可信Host/M28授权能力集成，避免伪造来源绕过权限。
- **当前不能宣称**：持久化QoS1、跨进程/跨PC、多点位可靠离线重放、可插拔完整Middleware和持久DLQ、真正工业设备HIL、Studio实际工作台。
- **状态区分**：M36功能代码存在并经过既有Windows烟测，但V1.2新增线程池/Codec与latency用例，必须等最终SHA对应CI通过才能从提交状态标注为验证状态。

## 13. V1.3 消息体系仅两个项目——已定案项目归属与源码迁移

**目标：一基一机，能力完整而接口简洁。**

```text
CxVisual.Foundation.Data                 (只提供基础契约，零Mechanism依赖)
  Messages/MNMMessageTypeDefinition.cs    消息类型、Topic绑定、PayloadKind、版本
  Messages/MNMTopicFilter.cs              MQTT +/# 过滤值对象和合法性验证
  Messages/MNMMessageContracts.cs         Header/Payload/Message/Options/Receipt/Metrics/
                                          DeadLetter/IMNMMessagePublisher
  Messages/IMNMMessageCodec.cs            Codec策略接口

                  ↓ 单向引用

CxVisual.Mechanism.Data                  (只实现消息运行机制)
  Messages/MNMMessageCenter.cs           Facade、订阅注册、生命周期、队列
  Messages/MNMMessageContext.cs          回调上下文、ReplyAsync、IMNMMessageSubscriber
  Messages/MNMMessageRouter.cs           Topic匹配、共享组及定向路由
  Messages/MNMMessageScheduling.cs       有界ThreadPool、QoS重试策略
  Messages/MNMMessageSerialization.cs    Text/Binary/JSON Codec实现与序列化Facade
```

### 13.1 不应跨项目迁入的对象

- M09变量表仍归原有MDS VariableTable与Registry；消息发布不得取代其状态真源。
- M10/MDS大数据堆、文件、BufferStream仍归原有MDS Owner；大图/CT体不进普通消息正文。
- M12命令和MRK操作执行由原有Owner管理；不能用广播Topic直接执行设备命令。
- `MNMConfigChangePublisher` 为现有配置领域专用通知，**不是第二个通用MessageCenter**；保持原配置机制文件位置，后续适配订阅而非强行混入通用消息Schema。

### 13.2 编译边界与API

- **基础命名空间** `CxVisual.Foundation.Data.Messages`：类型定义、TopicFilter、Envelope数据契约、发布/订阅选项与Result、`IMNMMessagePublisher`、`IMNMMessageCodec`。只引用System BCL，不引用Mechanism。
- **机制命名空间** `CxVisual.Mechanism.Data.Messages`：`MNMMessageCenter`、订阅令牌、`MNMMessageContext<T>`、`IMNMMessageSubscriber`、真实队列/Router/Codec实现与生命周期。
- 过去仅 `using CxVisual.Mechanism.Data.Messages;` 的业务源代码，**迁移后应同时引入** `using CxVisual.Foundation.Data.Messages;`。基础类型公开的全限定名发生变化；依赖者必须在同一轮解决方案构建中重编译，不能谎称二进制无缝兼容。
- `Foundation.Data` 的实例构建及Receipt/Metrics内部写操作限定给唯一运行Owner `CxVisual.Mechanism.Data`，使用单个限定的 `InternalsVisibleTo`，**不公开内部setter**；外部消费者仍看到只读结果。
- `MNMMessageContext<T>` 暂留机制层，因为回调ReplyAsync当前与Center生命周期/发布实现绑定；`IMNMMessageSubscriber`因此也留在机制层，避免基础项目反向引用机制。未来若定义纯基础Context可以采用委托/Port而不是循环项目依赖。
- 实际Publisher与Subscriber简洁操作API维持 `Publish/PublishAsync/Subscribe/SubscribeAsync/RequestAsync`；仅编译归属/命名空间迁移，不重建第二个MessageManager。

### 13.3 使用示例（两个项目、一个入口）

```csharp
using CxVisual.Foundation.Data.Messages;  // Header、Definition、Topic、Options
using CxVisual.Mechanism.Data.Messages;   // Center、Context、Subscriber

MNMMessageTypeDefinition schema = new MNMMessageTypeDefinition(
    "Camera.Captured", 1, typeof(string),
    topic: "/machine/1201/app/XRay/module/Camera/message/Captured");
using (MNMMessageCenter center = new MNMMessageCenter())
using (center.Subscribe<string>("/machine/1201/app/XRay/module/+/message/Captured",
    ctx => System.Console.WriteLine(ctx.Payload)))
{
    center.RegisterType(schema);
    MNMPublishReceipt accepted = center.Publish(schema.Topic,"Image001",
        new MNMPublishOptions { TypeId = schema.TypeId });
}
```

### 13.4 验收边界

1. Foundation.Data 必须可独立编译且不增加到 Mechanism 的引用；核心消息定义/Filter/Codec接口能在纯 Foundation 测试中使用。
2. Mechanism.Data 使用现有对 Foundation.Data 的单向 ProjectReference，并且其余现有 MDS/配置能力不被删除。
3. 重构前的消息路由、共享订阅、QoS、Retain、RequestAsync、队列、Stop与Codec所有正反例继续通过，**不得因移动文件导致任何功能倒退**。
4. 强制检查旧文件不在机制项目 Compile 中重复定义、全解决方案零二义性重复类型、保持28项目数量、Windows CI对应SHA才标PASS。



## 14. V1.4 源码审查、路由一致性与可选拦截器

### 14.1 保留消息与正常路由共用唯一规则

本轮源码审查发现：旧Retain重放仅匹配Topic及Payload Type，遗漏SourceFilter、TypeId、NoLocal、TargetId。其后果是后来订阅者可能收到不符合其来源、类型、目标约束的历史消息。改为单一的 \`MNMMessageRouter.MatchesSubscription\`：检查Subscription活跃、Topic、Payload CLR类型、NoLocal、SourceFilter、TypeId及TargetId。正常Publish和Retain复用同一判断，而可选Predicate仍在实际消费前由Dispatcher求值。共享组不自动重放Retain。NoLocal必填订阅者SourceId以避免歧义。**SourceId标签不是经过M28认证的调用者身份，匹配也不是权限授权。**

MNMTopicFilter新增内部 \`MatchesValidatedSegments(string[]):bool\`；已验证的发布Topic仅在Router.Resolve中Split一次，不再每名订阅者重复解析，仍沿用MQTT \`+/#\`层级匹配。只调整内存局部性能，正式高负载下的吞吐/内存/时延必须单独测量，不宣称已达工业实时SLA。

### 14.2 横切扩展类、方法和线程责任

| 类型 / 所在Owner | 关键方法、参数、返回值 | 实际行为及生命周期 |
|---|---|---|
| \`IMNMMessageInterceptor\` / Foundation.Data | \`BeforePublish(MNMMessage):void\` | 消息基本验证后、入队前调用；抛异常则拒绝发布，不虚计Published |
| \`IMNMMessageInterceptor\` / Foundation.Data | \`AfterDelivery(MNMMessage,MNMPublishReceipt):void\` | 实际投递或过期判定后调用；回调失败被隔离，不伪造业务失败 |
| \`MNMMessageCenter.AddInterceptor(interceptor)\` / Mechanism.Data | \`IMNMMessageInterceptor → IDisposable\` | 注册/注销按锁管理；发布时快照，已开始的工作不因注销被撤销 |
| \`MNMMessageCenter.InterceptorError\` | \`Action<Exception>\` | 诊断观察事件，监听器异常继续隔离 |
| \`MNMMessageMetrics.InterceptorFailures\` | \`long\` | 投递后拦截器异常数 |
| \`MNMMessageMetrics.MeanPublishPerSecond\` | \`double\` | 当前Center生命周期内平均发布量/秒；并非瞬时SLA |
| \`MNMMessageRouter.MatchesSubscription\` | \`(Subscription,Message,string[]?) → bool\` | Publish与Retain共用过滤；不另外建立Subscription Registry |

拦截器属于可选**进程内同步钩子**，不是完整的异步Middleware插件框架。复杂耗时逻辑不得放在同步钩子中；真实业务仍由Subscriber/Professional负责。GoF应用：Observer订阅、Mediator/Facade中心、Strategy路由/Codec/重试、Decorator式诊断钩子（不承担安全认证）。

### 14.3 交互时序和对象关系

\`\`\`mermaid
sequenceDiagram
    participant P as Publisher
    participant C as MessageCenter
    participant X as Interceptor
    participant R as Router
    participant W as Bounded Queue/Worker
    participant H as Subscriber
    P->>C: PublishAsync(topic,payload)
    C->>C: Schema/Topic/Host ACL
    C->>X: BeforePublish
    alt 校验不通过
        X-->>P: Exception, 未受理
    else 校验通过
        C->>R: Resolve 订阅快照
        R-->>C: Source/Type/Target/Topic规则匹配
        C->>W: 有界入队
        W->>H: Handler(MessageContext)
        H-->>W: Delivery Result
        W->>X: AfterDelivery
        Note over W,X: 诊断错误隔离，回执不伪造
        C-->>P: Receipt(Delivered/Failed/Accepted)
    end
\`\`\`

类型关系：Foundation.Data.Messages中的消息模型、TopicFilter、Options、Receipt、Metrics、IMNMMessageInterceptor由Mechanism.Data.Messages中的单一MNMMessageCenter复用；MNMMessageRouter、MNMMessageRetryPolicy、MNMMessageThreadPoolScheduler与Codec实现是Center组合/调用的对象。注册返回IDisposable令牌，Host负责StopAsync/Dispose，不创建第三个消息项目。

### 14.4 场景与进程内示例

\`\`\`csharp
using CxVisual.Foundation.Data.Messages;
using CxVisual.Mechanism.Data.Messages;
using (MNMMessageCenter center=new MNMMessageCenter())
using (center.Subscribe<string>(
    "/machine/1201/app/XRay/module/+/message/Captured",
    ctx=>System.Console.WriteLine(ctx.Payload)))
{
    MNMPublishReceipt result=await center.PublishAsync(
       "/machine/1201/app/XRay/module/Camera/message/Captured","Image001")
       .ConfigureAwait(false);
    // 只有检查真实DeliveredCount才能判断订阅者实际处理，而非仅凭Accepted。
}
\`\`\`

适用：Camera采图完成通知AI/HMI，AI检测结果告知统计工具，设备Faulted状态事件显示，跨模块Req/Reply获取状态；**不通过Topic通配执行Axis.MoveAbs等设备命令**。变量点表仍由M09管理，图像/CT Volume仍只发布MDS受控引用。

### 14.5 对原40条要求的真实状态审查

| 条目 | 状态 / 不能扩大声称的边界 |
|---|---|
| 1–12 | 核心进程内功能已有真实实现和测试：Topic、发布/订阅、同步/异步/指定线程、共享组/定向/ReqReply；不等于MQTT Broker |
| 13–16 | QoS0与当前进程有限次QoS1、Retain、TTL、Backpressure、来源/类型/谓词过滤；ThreadPool模式**不保同Topic顺序** |
| 17–20 | CorrelationId、超时/取消观察、异常隔离、停止排空和滚动诊断存在；未持久化跨进程会话 |
| 21 | Text/Binary/JSON Codec与可选前后钩子已实现；**完整可插拔异步Middleware和外部MQTT/IPC/SHM Bridge尚未交付** |
| 22–30 | 核心类和接口由现有Foundation.Data与Mechanism.Data协作落位，Queue/Dispatcher/RequestManager等职责被Center内部组合复用而不是机械新增Manager |
| 31–36 | 两个项目边界、时序、案例及实际SDK使用接口；Studio实际交互和专业MPA集成需单独验证 |
| 37–40 | ArchitectureSmoke已覆盖消息功能、并发/性能样本和本轮路由反例；Windows CI与现场设备HIL分别判定，不能直接宣称所有长期能力投产 |



## 15. V1.5 MPA DataMapping→M36实接入（不新增项目）

现有 \`MDSProtocolDataTargetAccess\` 提供带取消令牌的 \`Func<string,object,CancellationToken,Task> publish\` 注入通道，原先仅为“如果未来消息Owner存在”的留空扩展。此阶段直接接入**已经存在的**M36 \`MNMMessageCenter\`，不增建Mapper/Publisher/MessageManager，也不让MPA引用新的项目。

\`\`\`csharp
using CxVisual.Foundation.Data.Messages;
using CxVisual.Mechanism.Data;
using CxVisual.Mechanism.Data.Messages;

using (MNMMessageCenter center = new MNMMessageCenter())
using (center.Subscribe<string>(
    "/machine/1201/app/XRay/module/Camera/message/Captured",
    ctx => System.Console.WriteLine(ctx.Payload)))
{
    MDSProtocolDataTargetAccess access = new MDSProtocolDataTargetAccess(
        publish: async (targetRef, value, cancellationToken) =>
        {
            MNMPublishReceipt result = await center.PublishAsync<object>(
                targetRef, value,
                new MNMPublishOptions { RequireSubscriber = true },
                cancellationToken).ConfigureAwait(false);
            if (result.Stage != MNMPublishStage.Delivered || result.DeliveredCount == 0)
                throw new InvalidOperationException("MPA消息未完成实际投递");
        });
    await access.PublishAsync(
        "/machine/1201/app/XRay/module/Camera/message/Captured",
        "Image001", CancellationToken.None).ConfigureAwait(false);
}
\`\`\`

此代码使用现有公开API，\`RequireSubscriber=true\` 是**示例中主动选择的严格配置**：没有订阅者不得当作传达成功；普通事件通知也可以根据需求容许无人订阅，但不能把Accepted无条件定义为Deliver。错误Topic由MNMTopicFilter验证、取消令牌在发送前终止；来源认证仍必须由Host提供，SourceId并非认证凭据。MPA命令映射另归M12及MRK的Handler权限控制，不能拿M36的Publish冒充设备命令执行。

**验证层次**：ArchitectureSmoke实际通过此注入调用同步既有M36业务监听者、并做无订阅者/非法Topic/取消/空MessageOwner负向断言。真正Connector到现场PLC/相机、Windows Studio订阅编辑以及跨PC MQTT Bridge尚未因此自动完成。

通信流：
\`\`\`mermaid
flowchart LR
  A[MPA DataMapping] --> B[MDSProtocolDataTargetAccess.PublishAsync]
  B --> C[Injected M36 MessageCenter.PublishAsync]
  C --> D[Topic Router / Bounded Dispatch]
  D --> E[AI / HMI / Stats Subscriber]
  E --> F[Delivery Receipt]
  F --> B
\`\`\`

