# CxVisual 统一消息中心 M36——真实实现、回归与验收台账 V1.3

> 2026-10-24；对应主规格：`DOCS/Architecture/CxVisual_公共机制体系#消息中心——完整设计与全类API_V1.0.md`。
> 本台账登记实际文件与验收方法，**是否 PASS 只以本提交 SHA 的 Windows CI 真实结果为准**。

## 1. 当前项目与Owner

项目：`Mechanism/Data/CxVisual.Mechanism.Data.csproj`（M36）；继续复用 `MNMMessageTypeDefinition`、`MNMTopicFilter`。不新增项目。M09、M10、M12、MRK、MPA/Connector保持各自数据和执行Owner。

| 文件 | 主要对象 | 作用 |
|---|---|---|
| `Messages/MNMMessageContracts.cs` | Header、Payload、Message、MessageContext、Options、Receipt、Metrics、DeadLetter、Publisher/Subscriber interfaces | 生命周期契约、消息实例与格式 |
| `Messages/MNMMessageCenter.cs` | MNMMessageCenter、MNMMessageSubscription、MNMDeliveryWork | 同进程的发布、订阅、路由、有限队列、按组分发、重试、缓存、监控与停止 |
| `Messages/MNMMessageTypeDefinition.cs` | TypeId、Topic、PayloadKind及大小校验 | 已有定义增强复用 |
| `Messages/MNMTopicFilter.cs` | MQTT +/# 规则 | 现有匹配器复用 |
| `Tests/ArchitectureSmoke/Integration/MessageCenterTests.cs` | MessageCenterTests | 模块/并发/正反例回归 |
| `Tests/ArchitectureSmoke/Program.cs` | `--message-center` 与默认回归入口 | 真正执行单元回归 |

## 2. 重要实际行为

- `Publish` **默认 Queue + 快速受理回执**，需要同步回调则 `MNMPublishOptions.DispatchMode=Inline`；`PublishAsync` 等待本次订阅处理完成。批量发布顺序 await。
- 订阅可以有多个消费者；同 SubscriberId 的多条重叠 Filter 仅调用一次；同 SubscriberId + Filter 重复订阅替换旧令牌。空订阅默认 Accepted、MatchedCount=0；RequireSubscriber 才拒绝。
- 使用 MQTT `+/#` Topic 与 `$share/{Group}/{CxVisualAbsoluteFilter}` 共享组，**这不是外部 MQTT 客户端 Session 或持久队列**。
- QoS0一次投递尝试；QoS1抛错时进程内有限重试；异常进入有界死信元信息列表；不承诺重启后恢复及ExactlyOnce。
- 有界队列和Reject/DropOldest；Retain最新消息及容量限制；Expire按UTC检查；暂停恢复/停止排空；Metrics是锁保护快照。
- `RequestAsync` 订阅 ReplyTo/CorrelationId，请求响应无命令权；`ctx.ReplyAsync` 使用内联回复，避免单Worker队列回调等待自身回复造成死锁。M12命令仍需要唯一Handler、授权和MRK。
- 订阅可指定WPF SynchronizationContext，***Inline模式不得等待需要当前UI线程才能完成的异步回调***。
- 进程内DTO不自动深复制，二进制Buffer复制；结构化数据要确保DTO不可变，未来借显式Snapshotter/Codec实现受控序列化；大数据用MDS引用。

## 3. 正反验收（本轮）

Windows VS2022 .NET48 x64真实构建：

```powershell
msbuild CxVisual.Unified.sln /t:Build /p:Configuration=Release /p:Platform=x64 /m
Tests\ArchitectureSmoke\bin\Release\CxVisual.Tests.ArchitectureSmoke.exe --message-center
Tests\ArchitectureSmoke\bin\Release\CxVisual.Tests.ArchitectureSmoke.exe
Tests\ArchitectureSmoke\bin\Release\CxVisual.Tests.ArchitectureSmoke.exe --concurrency
```

实际输出路径由仓库工程输出配置决定，上述只是运行意图，不代表已执行。应通过现有Windows Actions同提交SHA日志确认。覆盖：Publish/PublishAsync、Inline、Topic +/#、重复订阅、共享组轮转、保留重放、NoLocal、Payload Predicate、有限队列满、DropOldest、Pause/Resume、QoS1重试、QoS0单次、死信、Request/Reply、请求超时、400并发发布、来源/订阅授权回调；确认没有假阳性。

## 4. 未完、已知约束及风险

**不冒充已完成**：网络桥接、跨进程会话、持久化重放、ExactlyOnce、M28真实认证、真正专业HIL、面向Studio的可视订阅与调试界面、稳定Codec与远端类型版本迁移、完备中间件链、主题Trie高度优化、消息持久死信重放。

**性能性质**：一个有界FIFO消费者将异步回调按顺序执行，稳定且避免无界任务；慢消费者可能拖慢同队列整体吞吐（但不会阻塞默认Publish快速入队直至队列耗尽）。进程内性能压测须记录实际Windows硬件、样本数、处理速率、p95/p99，未测不能填写固定的数值承诺。

**安全性质**：PublishOptions.SourceId 目前是来源标签，不具备认证完整性；AuthorizeSubscribe/Publish 是可注入拦截点，不能取代现有M28身份Owner。跨进程桥接前必须可信Host绑定Caller与Scope。

本轮应继续将待优化项记录为具体缺口，后续根据真实测试拆出MessageRouter/Dispatcher/Queue/Scheduler独立类，不为了类表数量增壳。


## 5. V1.1 路由、安全边界与时序增强

- 新增 `Messages/MNMMessageRouter.cs`：`TargetId` 精确投递、`SourceFilter` 与 `TypeId` 匹配、NoLocal/SharedGroup竞争消费；Center 始终是 Subscription 的唯一 Owner。
- 异步回调内嵌套 `PublishAsync` 以受限内联避免单消费者队列自等待死锁；超过16层明确拒绝。
- `RequestAsync` 计时覆盖请求发送和响应等待，超时是观察 Unknown，不自动重试设备动作。
- 新增目标路由、嵌套发布、发送阶段超时的正反测试，正式 Windows 证据须对应新提交 SHA。


## 6. V1.2 最新源码变更与实施证据

| Owner/文件 | 新增或增构内容 | 真实限制 |
|---|---|---|
| M36/MNMMessageScheduling.cs | 有界ThreadPoolScheduler、实际RetryPolicy | 并行模式不保同Topic顺序；重试仅当前进程 |
| M36/MNMMessageSerialization.cs | Text、Binary、JSON Codec接口及具体实现 | Reference/Protobuf/外部Bridge未交付 |
| M36/MNMMessageCenter.cs | ThreadPool接纳与投递、StopAsync等待在途、Host/Subscription/Publish选项快照 | StopAsync不是设备停止确认 |
| M36/MNMMessageContracts.cs | MaxParallelDispatches、InFlight、有限P95/P99采样 | 采样窗口1024，并非固定产线SLA |
| Tests/ArchitectureSmoke/Integration/MessageCenterTests.cs | Slow/Fast并行、容量拒绝、快照、实际Codec、延迟指标 | 需要正式Windows编译和回归日志 |

**Windows 验证**：针对本轮SHA使用现有 `CxVisual.Unified.sln` x64 Release构建和 `ArchitectureSmoke --message-center`、默认、`--concurrency`，并在此后核对对应CI工作流。新提交没有自动等于PASS；后续外部设备HIL不在此范围。

## 7. V1.3 一基一机的真实源码归档与迁移

|项目Owner|路径|放置的类与接口|禁区|
|---|---|---|---|
| Foundation.Data | `Foundation/Data/Messages/` | MessageTypeDefinition、TopicFilter、Header/Payload/Message、Options、Receipt、Metrics、DeadLetter、Publisher/Codec接口 | 不引入Center/Router/线程池/重试/MRK |
| Mechanism.Data | `Mechanism/Data/Messages/` | Center、Subscription、Context、Subscriber接口、Router、Scheduling、Codec实现/Facade | 不重复存放MessageTypeDefinition、Header或基础枚举 |

**源码检查**：新增的Foundation/Data/Messages下共4个源码文件；机制下保留5个源码文件（其中MessageContext新拆分），旧Mechanism/Data/Messages下的三份定义/契约源码删除。现有项目各只需调整Compile列表，不创建新项目或变更依赖方向。Foundation内部构造/结果写权限只授权Mechanism.Data。

**验收指令**：先以本轮Commit SHA触发Windows/.NET 4.8全解决方案编译和ArchitectureSmoke，核对`PASS BUS.FOUNDATION`、`PASS BUS.M36`、`PASS architecture tests`；再检查其他工作流。注意修订后业务调用者须同时引用`CxVisual.Foundation.Data.Messages`和`CxVisual.Mechanism.Data.Messages`，并执行现有工程模块集成测试；**这是源码迁移，不承诺旧二进制客户端无需重编译。**
