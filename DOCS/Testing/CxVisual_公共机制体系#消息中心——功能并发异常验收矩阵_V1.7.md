# CxVisual 统一消息中心 M36——功能、并发、异常验收矩阵 V1.7

> 对应 M36 MessageCenter 进程内实现。**测试用例是否 PASS 以最终提交 SHA 的真实 Windows CI 与Console日志为准；目前默认 NOT_RUN。**

| CaseId | 功能与执行路径 | 正向断言 | 反例/边界 | 状态 |
|---|---|---|---|---|
| MC-01 | Topic/Type/Schema | 定义/版本/Topic/PayloadKind匹配，+/#正确 | 错型/错Topic/非法Filter拒绝 | NOT_RUN |
| MC-02 | 发布队列 | 无订阅Accepted；PublishAsync在回调完成后Delivered | RequiresSubscriber无订阅拒绝 | NOT_RUN |
| MC-03 | 同步Inline | 当前线程完成回调且回执Delivered | 不把Accepted误当Consumed | NOT_RUN |
| MC-04 | 多订阅者 | 两订阅者都收到且Count=2 | 同SubscriberId重叠Filter只一次 | NOT_RUN |
| MC-05 | 重复订阅/取消 | 同SubscriberId同Filter替换，Dispose取消 | 旧令牌不删新令牌，取消后不再新入队 | NOT_RUN |
| MC-06 | MQTT共享组 | 一组两个消费者轮转，普通观察者仍全收到 | 不把共享组当普通广播 | NOT_RUN |
| MC-07 | Retain | 新普通订阅重放该Topic最新、容量受限 | Shared组不Replay；过期不回放 | NOT_RUN |
| MC-08 | 过滤 | NoLocal、TypeId、Predicate准确过滤 | Predicate异常进入错误/死信，不逃逸锁 | NOT_RUN |
| MC-09 | Binary | 发布后修改原byte[]，订阅者取值不变 | 订阅者不能借Value取得内部Buffer可变引用 | NOT_RUN |
| MC-10 | QoS0/1 | QoS0一次；QoS1失败后限次重试成功 | 永久失败计入有界死信，不声称持久交付 | NOT_RUN |
| MC-11 | 背压 | Pause时队列容量有限；Resume排队消费 | Reject或DropOldest有明确拒绝/丢弃结果 | NOT_RUN |
| MC-12 | 请求响应 | CorrelationId匹配、ReplyTo/ReplyAsync无死锁 | 正确超时、不推断业务没执行 | NOT_RUN |
| MC-13 | 生命周期 | StopAsync拒绝新发布，Dispose订阅令牌 | 停止/取消不中断已开始业务副作用 | NOT_RUN |
| MC-14 | 并发 | 400个并发PublishAsync完成且消费数正确 | QueueDepth归零、无重复回调 | NOT_RUN |
| MC-15 | ACL边界 | 发布和订阅通过注入的策略校验 | 未授权抛明确异常；标签不等于真实认证 | NOT_RUN |
| MC-16 | 指标 | Published/Accepted/Delivered/Failed/Retry/Depth统计 | Stage混淆/伪投递计数不得通过 | NOT_RUN |
| MC-17 | 工业集成 | ImageRef广播给AI/HMI、Faulted作为事件通知 | 不通过消息广播执行Axis.MoveAbs | NOT_RUN |
| MC-18 | 性能 | 记录真实Windows样本、吞吐、延迟、内存增长、订阅数 | 不能以Linux推导生产设备SLA | NOT_RUN |
| MC-19 | Qt/WPF调度 | 指定SynchronizationContext正确调度回调 | UI线程同步阻塞/未解注册/异常影响其它线程 | NOT_RUN |
| MC-20 | MPA适配与未来桥接 | 保持旧MPA签名、Host可注入消息调用 | 不编造MQTT Broker、网络QoS与硬件通过 | NOT_RUN |

**测试入口**：`CxVisual.Tests.ArchitectureSmoke.exe --message-center`，及默认架构回归。实际源码中的回归覆盖 MC-01～16 的主要正反场景；MC-17～20 属后续集成/UI/性能专项验收，不能因当前基础测试PASS而标成PASS。


## 2. V1.1 增强专项

| CaseId | 内容 | 通过标准 | 初始状态 |
|---|---|---|---|
| MC-21 | 点对点与来源过滤 | TargetId指定SubscriberId不广播；SourceFilter不匹配无接收 | NOT_RUN |
| MC-22 | 嵌套异步发布 | Handler内部await PublishAsync不会把单Worker卡死，返回真实Delivery | NOT_RUN |
| MC-23 | 超时包含请求发送等待 | 慢请求处理者下超时按整个RequestAsync窗口生效、订阅释放 | NOT_RUN |

共23条分级验收，用例未执行前不能标PASS。


## 3. V1.2 新增实际单元/并发反例（7项）

| CaseId | 覆盖 | 正向/反向判据 | 状态 |
|---|---|---|---|
| MC-24 | ThreadPool慢快隔离 | 慢Topic阻塞期间独立Topic先Delivered | NOT_RUN，待本SHA CI |
| MC-25 | 并发容量 | 满MaxParallelDispatches后Rejected，绝不无界Task | NOT_RUN，待本SHA CI |
| MC-26 | 发布/订阅/Host配置快照 | 注册后修改选项不改变已登记过滤与额度 | NOT_RUN，待本SHA CI |
| MC-27 | Text/Binary Codec | 严格UTF8往返、Buffer复制、超长文本拒绝 | NOT_RUN，待本SHA CI |
| MC-28 | 结构化JSON/格式拒绝 | DTO实际序列化往返；未知Codec抛NotSupported | NOT_RUN，待本SHA CI |
| MC-29 | 有界延迟指标 | Samples/Mean/P95/P99确实来自最近交付，不是假SLA | NOT_RUN，待本SHA CI |
| MC-30 | StopAsync排空 | ThreadPool在途任务结束才算Drain完成 | NOT_RUN，待本SHA CI |

总计30项候选分类；MQTT/IPC物理Bridge、正式M28授权、TestCenter WPF面板与真实X-Ray设备仍需分别验收。

## 4. V1.3 双项目收敛验证（独立于原30项）

| CaseId | 验证内容 | 通过标准 | 初始状态 |
|---|---|---|---|
| MC-31 | Foundation独立构建/层次DAG | Foundation.Data.Messages没有任何Mechanism编译依赖 | NOT_RUN |
| MC-32 | 纯基础消费者 | FoundationMessageContractsTests运行，能构造Topic/Type、使用IMNMMessageCodec | NOT_RUN |
| MC-33 | 单向类型引用与重建 | M36仅引用Foundation.Data；不存在重复Header/定义类型；全Solution重编译 | NOT_RUN |
| MC-34 | 行为回归 | 原MessageCenterTests全部功能/并发/异常继续PASS | NOT_RUN |
| MC-35 | Scope/资源隔离 | 注销订阅、StopAsync与Retain/QoS行为不受改动 | NOT_RUN |
| MC-36 | 其他工程引用方 | ArchitectureSmoke默认与WindowsCI无旧namespace编译错误 | NOT_RUN |

总计**36项分类验收**；MC-31～MC-36用于架构收敛，不能凭已有V1.2测试成功而自动PASS。


## 5. V1.4 真实缺陷反证与横切处理验证（MC-37～44）

| CaseId | 验收场景 | 通过判据 | 状态（待CI） |
|---|---|---|---|
| MC-37 | 保留消息的TargetId | AI订阅不可收到定向给HMI的旧消息 | NOT_RUN |
| MC-38 | 保留消息的SourceFilter | 只有来源匹配才会Retain重放 | NOT_RUN |
| MC-39 | 保留消息的TypeId | 强类型定义不匹配则无历史投递 | NOT_RUN |
| MC-40 | NoLocal过滤 | 真实相同来源不重放，未配置SourceId拒绝 | NOT_RUN |
| MC-41 | 有效订阅历史重放 | 全部条件匹配时只能得到一次历史消息 | NOT_RUN |
| MC-42 | BeforePublish异常 | 向调用方抛错，不入队、不计Published | NOT_RUN |
| MC-43 | AfterDelivery失败/注销 | 保留原Delivered，计InterceptorFailures和Error，注销后不触发 | NOT_RUN |
| MC-44 | 路由优化/指标 | Topic路径单次拆分为订阅列表复用，MeanPublishPerSecond有意义 | NOT_RUN |

合计**44项分类用例**。MC-01～36历史兼容与回归不可省略；外部Bridge/持久化QoS/HIL继续单独记录。


## 6. V1.5 MPA→M36 实接入专项（MC-45～47）

| CaseId | 验证 | 判据 | 状态（本轮提交前） |
|---|---|---|---|
| MC-45 | 原MPA发布委托接入M36 | 专业消息由MPA.PublishAsync经过真实MessageCenter交付Subscriber，回执Delivered | NOT_RUN |
| MC-46 | 错Topic与预取消 | 非法发布Topic拒绝，已取消令牌下接收者不发生副作用 | NOT_RUN |
| MC-47 | 未接消息Owner及无订阅者 | 未装配Message回调显式NotSupported；RequireSubscriber=true时明确失败而不假成功 | NOT_RUN |

累积**47项分类验收候选**；真实状态以本SHA的Windows CI与ArchitectureSmoke日志为准，现场设备/HIL独立验收。


## 7. V1.6 Retain过期回收与管理（MC-48～51）

| CaseId | 场景 | 判据 | 初始状态 |
|---|---|---|---|
| MC-48 | Retain容量2，先有效A后短TTL B，B到期后写C | A与C重放，B不重放；不得错误淘汰有效A | NOT_RUN（以Windows CI为准） |
| MC-49 | ClearRetained同Topic重复清理 | 首次true、次次false；Metrics显示实际保留数 | NOT_RUN |
| MC-50 | 清理后重发同Topic + 第三Topic触发容量淘汰 | 新保留项不被旧FIFO残留键误删 | NOT_RUN |
| MC-51 | 发布鉴权与停止状态 | 未授权Clear拒绝且数据仍保留，授权Clear成功；Stop后抛Disposed | NOT_RUN |

累计**51项分类验收候选**，自动测试是对关键正反例的覆盖，不意味着所有51项都各自独立执行及现场验收；现场HIL/外部Broker/Studio继续分开记录。


## 8. V1.7 请求响应终止与回调生命周期（MC-52～55）

| CaseId | 测试场景 | 预期判据 | 初始状态 |
|---|---|---|---|
| MC-52 | ReplyTo已注册后中心StopAsync(false) | 长超时请求立刻ObjectDisposed，临时订阅清理，不等待30秒 | NOT_RUN（最新CI为准） |
| MC-53 | ReplyTo已注册后中心Dispose() | 请求立刻ObjectDisposed；非等待Dispose不伪称设备操作已停 | NOT_RUN |
| MC-54 | 在Queued/ThreadPool的Handler内await StopAsync | 拒绝自等待InvalidOperationException，Handler仍可结束，外部Host能正常Stop | NOT_RUN |
| MC-55 | 正在等待ReplyTo时取消CancellationToken | OperationCanceledException，临时订阅恢复到原计数，timer资源释放 | NOT_RUN |

累计**55项候选验收条件**，不是55项已经实际独立执行的Windows测试；原51项仍保持，Windows/.NET48全解决方案与现场HIL分开给证据。
