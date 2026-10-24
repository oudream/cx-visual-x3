# CxVisual 统一消息中心 M36——功能、并发、异常验收矩阵 V1.1

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
