# CxVisual 公共机制体系#统一交互——TestCenter 与 Windows 验收矩阵 V1.2

> **性质：计划验收矩阵（尚非测试执行报告）**；日期 2026-10-24。  
> 修订：2026-10-24；新增反例，未运行新增测试。  
> 主规格：`../Architecture/CxVisual_公共机制体系#统一交互——四大能力与双层总线完整设计及验收_V1.3.md`。  
> 实施台账：`../Implementation/CxVisual_公共机制体系#统一交互——进程内总线分阶段实施与源码台账_V1.2.md`。  
> 参考 GitHub SHA：`master@36855f3fade9b70ecb7c01cbdbb3e37264802926`。  
> 本轮**未执行 Windows CI、TestCenter、ArchitectureSmoke、实机测试**；以下 ID 是新需求验收候选编号，不能冒充已经登记的 `TCT.*` 官方 Case。实际实施时须与 TestCenter 编号体系映射，不能重复定义同一测试。

## 0. 测试分类、准入规则与验收证据

**层级定义**：`U` = 进程内单元/Contract；`I` = 同进程跨 Owner 集成；`S` = WPF Studio 交互；`P` = 性能/并发；`X` = 两进程/两机；`H` = 实设备 HIL。

**每一项至少记录**：CaseId、Owner/SubArea、SHA、运行环境（Windows build/tool版本）、输入与负面刺激、断言、真实结果、日志链接、未验证限制。

**完成功能不能只看成功用例**：命令发出但 Provider 没执行、订阅抛异常、变量多源并发、地址冲突、过期数据使用、超时重试以及组件停止都需要反例。不能用 mock/loopback 代替真实设备、跨进程及双 PC 证据。

## 1. BUS-ID：统一地址、雪花码、分层查找（P0-1）

| CaseId | 层级 | 正向步骤 / 断言 | 失败或边界反例 |
|---|---|---|---|
| BUS-ID-01 | U | 同一资源分别用 Path 和 NumericId Resolve，得到同一 Owner 身份 | 指向不同 Owner 必须拒绝 |
| BUS-ID-02 | U | 不同 Machine/Container 或 Project/Run 的同名资源隔离 | 同名不能窜到其它 Scope |
| BUS-ID-03 | U | 路径规范化、转义、层级大小写策略稳定 | 非法段、空名称、超深/超长路径明确拒绝 |
| BUS-ID-04 | U/P | 多线程、批量 Snowflake 生成全不重复 | 时间回拨、同毫秒序号耗尽不产生重复 ID |
| BUS-ID-05 | U | Worker/NodeId 分配冲突被发现 | 同机/不同宿主误使用同 WorkerId 不静默重号 |
| BUS-ID-06 | U/I | 注册、注销、重命名，旧别名规则与稳定 ResourceRef 明确 | 已注销对象引用失效，不落到新 Generation |
| BUS-ID-07 | U | JSON 中 int64 ID 用字符串精确往返 | 超出 JS 2^53 的数值表示不允许静默精度丢失 |
| BUS-ID-08 | U/I | 作用域注销后全部地址失效，重新启动注册新的 Generation | 上轮迟到请求不可落在新模块实例 |

## 2. BUS-VAR：高速变量、来源、质量与版本（P0-2）

| CaseId | 层级 | 正向步骤 / 断言 | 失败或边界反例 |
|---|---|---|---|
| BUS-VAR-01 | U | Define 默认值与初始化策略分离 | 未采样变量不能伪装成 Good 设备值 |
| BUS-VAR-02 | U | 首次成功提交产生 Current/Source/Writer/Revision，Previous 存在标志明确 | 不能把默认值误写成前值 |
| BUS-VAR-03 | U | 第二次提交 Previous=上次 Current（即便两次值相同），Current/Quality/Source/Revision 同快照 | 线程读取不能观察到一半旧一半新；同值提交不能混同 ValueChanged |
| BUS-VAR-04 | U | ValueChanged/QualityChanged/SourceChanged/Committed 按过滤条件触发 | 质量变化不能误触发值变化；相同值策略已冻结 |
| BUS-VAR-05 | I | MPA Point Mapping 提交真实 SourceId 与 WriterId | 不能把连接器写入者冒充设备数据源 |
| BUS-VAR-06 | U/I | Batch 同范围提交，消费者读取一致快照/批次序号 | 中间失败不出现部分提交却报告成功 |
| BUS-VAR-07 | U | 多源、无权限、类型错误、版本预期冲突被拒绝 | 被拒更新不推进 Revision/不改变 Previous |
| BUS-VAR-08 | I | 失联置 Stale，显示上次值和失效时间，恢复后转有效 | Stale 不得被当成新的 Good 采样 |
| BUS-VAR-09 | P | 持续高频写且有慢观察者，写入时间不随消费者阻塞无界增长 | 回调抛异常/睡眠不能阻塞采集锁 |
| BUS-VAR-10 | I | 同进程两 HostScope 同名变量独立订阅/注销 | 消费者不能收到其它 Scope 变化 |

## 3. BUS-MSG：轻量消息、发布订阅和发送反馈（P0-3）

| CaseId | 层级 | 正向步骤 / 断言 | 失败或边界反例 |
|---|---|---|---|
| BUS-MSG-01 | U/I | Publish 返回 Stage+Outcome，例如 LocalQueueAccepted + Accepted | 不能把 Rejected 当 Stage，或把发送阶段当最终 Delivery/Execution |
| BUS-MSG-02 | I | 精确 Path 与 NumericId 订阅均能定位单一目标 | 不同 Scope 同名资源不可串订阅 |
| BUS-MSG-03 | I | Machine/Container、AppCode、ModuleCode 层级过滤组合正确 | 跨机器/应用消息不误收 |
| BUS-MSG-04 | I | 多订阅者收到同一 EventId，合并多条重叠订阅匹配结果 | 一订阅者不因两层路径同时匹配而重复执行副作用 |
| BUS-MSG-05 | U | Dispose 后不能再入队新回调；Drain/StopAsync 等待已运行回调（如支持） | 不把已开始回调的正常退出误认为注销失败 |
| BUS-MSG-06 | U | 零订阅的普通消息返回 MatchedCount=0 并允许无接收者；有必达要求则拒绝 | 不能报 Delivered，也不能把全部零订阅都判为发送错误 |
| BUS-MSG-07 | P | 有界队列达到容量时按明确溢出策略拒绝/合并/丢旧 | 不允许无限增长，且关键报警不被普通消息挤掉 |
| BUS-MSG-08 | I | 慢/异常订阅者隔离且记录 Trace 与错误 | 不能拖垮其他消费者、变量和 Command |
| BUS-MSG-09 | U | 同一 Source+Topic 按声明 Ordering 保序 | 不承诺跨主题全局严格顺序 |
| BUS-MSG-10 | I | MPA.PublishAsync 接真消息 Owner，能观察结果/错误 | 无 Owner 时 NotSupported，不能返回假 Task.Success |

## 4. BUS-CMD：命令动作、发送与执行区别（P0-4）

| CaseId | 层级 | 正向步骤 / 断言 | 失败或边界反例 |
|---|---|---|---|
| BUS-CMD-01 | U/I | 同一 Target 下注册合法 Handler，Invoke 可命中唯一执行 Owner | 没有 Handler 拒绝，不可空操作成功 |
| BUS-CMD-02 | I | 授权调用者可执行，拒绝/审计未授权调用 | Studio/Connector 不能绕过接收侧授权 |
| BUS-CMD-03 | U | 参数、Schema、版本和目标校验 | 错误 TypeId/值类型/必填字段不能继续执行 |
| BUS-CMD-04 | U/I | SendResult=Accepted 后 Handler 开始/完成分开报告 | 发送成功不导致直接显示 Completed |
| BUS-CMD-05 | I | OperationId 可被 Query/Observe，结果与 CorrelationId 正确对应 | 快速回调抢先不能丢关联；同 ID 不同关联冲突 |
| BUS-CMD-06 | I | 业务执行 Completed、MRK Provider Confirmed 与设备现场 ConfirmationResult 分层 | 位置未达到时不能报告已现场确认；MRK Confirmed 不自动等于反读通过 |
| BUS-CMD-07 | I | 超时观察转 Unknown，晚到应答恢复最终结论 | 超时≠失败，不得盲目重试电机运动 |
| BUS-CMD-08 | I | 相同幂等 OperationId/Key 命中原操作，不重复执行 | ID 相同但参数/Caller/Target 冲突必须拒绝 |
| BUS-CMD-09 | I | Cancel 请求可正确传给 Handler 并观察最终状态 | 取消调用方等待不代表设备已停止 |
| BUS-CMD-10 | I | 同一物理资源声明串行或限并发，MRK 租约释放正常 | 双重占用不能违反资源排他；异常也不泄漏租约 |
| BUS-CMD-11 | I | MPA.InvokeAsync 必须真实 Owner + 显式授权检查 | 无 Owner/授权失败严格抛 NotSupported/Unauthorized |
| BUS-CMD-12 | U/I | 收到异常/停机时保留失败上下文、原始 Operation 查询可审计 | 不允许新建第二 Command 执行器/操作账本 |

## 5. BUS-DAT：数据所有权、大数据与 Stream（P0-5）

| CaseId | 层级 | 正向步骤 / 断言 | 失败或边界反例 |
|---|---|---|---|
| BUS-DAT-01 | U | Heap Set/Get 类型一致，Copy/Move 有清晰语义 | 错型、命名冲突、已释放数据明确失败 |
| BUS-DAT-02 | U | 可变 byte[] 安全捕获，多个只读消费者不共享可写内容 | 消费者不能偷改另一个算法的输入 |
| BUS-DAT-03 | I | 消息通知 ImageRef+元数据，Data API 单独访问实际载荷 | Message 不复制完整大图；普通日志不显示二进制 |
| BUS-DAT-04 | I | Command 在同进程通过 OwnerScope+RunId+Generation+DataName 定位并取得受控安全值（候选契约） | 过期引用或错误 RunId 拒绝；不能依赖未实现通用 MDSDataRef |
| BUS-DAT-05 | U/I | FileLocator 初始 Unloaded，显式 Load/Unload 变化可观测 | Inspect/Search 不得隐式加载整个 Volume |
| BUS-DAT-06 | P | 高速帧连续入流时根据 Capacity/Overflow/Ordering 处理 | 不允许无限 Heap 膨胀或静默缺帧又称完整 |
| BUS-DAT-07 | U/I | 数据对象销毁、作用域结束、异常时释放预算/引用 | 不发生悬挂访问、超过资源预算无界增长 |
| BUS-DAT-08 | I | Variable、Message、Command、Data 四类完成 X-Ray→AI 同进程闭环 | 任一失效/权限拒绝均不能虚报成功 |

## 6. BUS-NFR：性能、故障隔离、资源及质量（P0）

| CaseId | 层级 | 正向步骤 / 断言 | 失败或边界反例 |
|---|---|---|---|
| BUS-NFR-01 | P | 高频 M09 写入吞吐、p50/p95/p99 访问延迟留存基线 | 不因消息订阅增加明显非预期尾延迟；阈值实测后确定 |
| BUS-NFR-02 | P | 同时处理变量批次 + 多订阅 + 命令 + Heap 数据 | 大数据访问不饿死控制路径，记录队列/时间/内存 |
| BUS-NFR-03 | P | 100% 消费者故意慢处理/抛异常，测变量写入独立性 | 不发生死锁、同步回调拖垮采集 |
| BUS-NFR-04 | P | 消息 Burst 下队列有界、Drop/Backpressured 可观测 | 未配置无限重试、无限缓存不可接受 |
| BUS-NFR-05 | P/I | 发布后订阅者注销/模块停止并发进行，资源正确收敛 | 不出现已释放对象执行、死锁和重复派发 |
| BUS-NFR-06 | P | 多消费者读 4300×4300 Image 与 Volume 元信息，测拷贝数及 GC | 不得因轻量消息复制/保留整个 Volume |
| BUS-NFR-07 | U/I | 反向依赖、重复 Manager、跨 Scope 违例扫描 | 不能引入新项目或循环依赖（无批准例外） |
| BUS-NFR-08 | I/P | 停止、重启、多 Run 重建与订阅释放；重复几十次 | 旧 Generation/Operation 不串到新 Run；运行预算不持续泄漏 |

## 7. BUS-UI：Studio/工程编排可用性（P0-6）

| CaseId | 层级 | 真实 UI 操作 | 必须验证的反例/效果 |
|---|---|---|---|
| BUS-UI-01 | S | 选择机器/容器→App→Module，在资源配置面板定位变量 | 同名不同 Scope 不串；不能额外生成影子工程树 |
| BUS-UI-02 | S | 查看 Path 与 int64 ID，可用任一查到同一对象 | ID 展示不丢精度；重命名兼容契约 |
| BUS-UI-03 | S | 变量面板观察 Default/Previous/Current/Source/Writer/Quality/Revision | 未支持项必须明示，过期值明确 Stale |
| BUS-UI-04 | S | 配置按机器/App/模块分层订阅，启用/取消/查看匹配结果 | 取消后不再收到；未经授权资源不显示敏感数据 |
| BUS-UI-05 | S | **同进程测试宿主**执行 Capture，显示 Send/Delivery(若有证据)/Execution/现场确认独立阶段 | Send Success 不显示 Complete；不伪造独立 Runtime.Host 已接通 |
| BUS-UI-06 | S | 通过 DataInspect 查看 ImageRef，按权限调用专业 Viewer | 不在属性编辑器里塞入整张图像或 8GB 数据 |
| BUS-UI-07 | S | 保存工程重启 Studio、重载**同进程测试宿主**，重新绑定订阅与 Viewer | 只持久化定义/稳定端点，临时运行NumericId不得错误持久化；WPF STA 无异常 |

## 8. BUS-EXT：扩展阶段的进程外与双 PC 验收（**P1～P3，非 P0**）

| CaseId | 计划阶段 | 必须执行的现实验收 | 失败与中断反例 |
|---|---|---|---|
| BUS-EXT-01 | P1 | 两个真正独立 Windows 进程经 IPC 成功查询、推送、取消订阅 | 一侧进程崩溃不会让对端停机或污染 Registry |
| BUS-EXT-02 | P1 | SHM 中 Image 元信息+内容受控读取，索引/代次/租约正确 | 老 Generation 的 SHM 不得被下一代复用误读 |
| BUS-EXT-03 | P2 | MQTT Broker 与两个不同 PC 真实连通，层级订阅按机器/App/Module 生效 | Broker ACK 不显示为目标业务执行成功 |
| BUS-EXT-04 | P2 | 断网→Stale→重连→重新订阅→快照/差量补齐 | 断线期消息丢失导致版本跳跃时重新从 Owner 同步 |
| BUS-EXT-05 | P2 | 远端授权/鉴权/TLS/目标验证以及跨 Bridge Hop/Origin 去重 | 未授权远程 Invoke 拒绝；双桥环路被阻断 |
| BUS-EXT-06 | P2 | 远程 Command 发送超时后 Query 原 Operation，确认实际结果 | 不重复触发不可幂等的运动/采图动作 |
| BUS-EXT-07 | P3 | 两 PC 大文件分块同步，Hash、Size、版本、断点续传 | 校验失败不得报告 DataReady；重复块/缺块拒绝 |
| BUS-EXT-08 | P3/H | 真实 Volume/图像流、受控 CPU/RAM/网络峰值、专业 Viewer 与 HIL | 本机 loopback 和 mock 数据不能代替多机吞吐/设备采图 |

## 8.1 审查补充的 19 个关键反例（全为待实施/NOT_RUN）

| CaseId | 阶段/层级 | 关键断言 | 反例 |
|---|---|---|---|
| BUS-ID-09 | P0-1/U | 持久定义端点 Snowflake ID 随工程持久化，重命名/重开不改变 | 不能将临时运行 NumericId 写成工程绑定 |
| BUS-ID-10 | P0-1/U | 运行实例重启换 Generation，旧临时端点被拒绝 | 不能因 Path 相同把迟到操作路由至新实例 |
| BUS-VAR-11 | P0-2/U | 同值重复提交 Previous=提交前Current，Committed/ValueChanged 语义独立 | Previous 不得变成“上一次不同的值” |
| BUS-VAR-12 | P0-2/U/I | 值/质量/来源任一可见状态变化遵守 Revision 和快照 | 单独质量变化不能无版本、无来源记账 |
| BUS-VAR-13 | P0-2/I | M09 高频 Update 在慢回调/抛异常下延迟受控 | 不能只把回调挪到更新锁外就声称已异步隔离 |
| BUS-MSG-11 | P0-3/I | SubscriberId + EventId 防止同一消费者重叠订阅重复回调 | 不误抹掉两个独立发布的 EventId |
| BUS-MSG-12 | P0-3/I | Subscribe+Snapshot+Sequence 初始化不丢更新 | 先 Query 后 Subscribe 的窗口变化必须被捕获/重同步 |
| BUS-MSG-13 | P0-3/U/I | SendResult Stage 与 Outcome 正交；应用级 ACK 证据独立 | Broker/本地入队不得直接返回业务交付成功 |
| BUS-MSG-14 | P0-3/P | 普通消息允许合并/丢弃，可靠报警不可静默覆盖 | 消费者溢出必须拒绝/可诊断，而非装作 Delivered |
| BUS-CMD-13 | P0-4/I | MRK Confirmed 不等于现场 ConfirmationResult，按专业验证 | 没有位置/设备到位证据不可直接报设备 Confirmed |
| BUS-CMD-14 | P0-4/U/I | 相同 OperationId 不同 Caller/Target/参数摘要拒绝重放 | 不能仅对比 CorrelationId 就误合并不同副作用 |
| BUS-CMD-15 | P0-4/I | CancelWait、RequestCancel、HandlerCancelled 三阶段独立 | 用户停止等待不允许改写设备实际状态 |
| BUS-CMD-16 | P0-4/U/I | OperationTracker 进程内断点与内存生命周期有界，崩溃后恢复不虚报 | P0 的内存表不当成跨重启 exactly-once |
| BUS-DAT-09 | P0-5/I | Heap/专业数据 Origin 与文件 Source 路径字段分开表达 | 不能把 DataSnapshot.Source 文件路径当 SourceId |
| BUS-DAT-10 | P0-5/I | 同进程数据定位携带 Owner/Run/Generation/Type，错误上下文拒绝 | 当前没有公开通用 DataRef/Lease 时不能照伪接口实现 |
| BUS-DAT-11 | P0-5/U/I | Heap 支持类型受控序列化/反序列化往返，TypeId/SchemaVersion/来源/文件定位可核实 | MDSDataSnapshot 元信息查询不算真正 Serialize/Deserialize |
| BUS-DAT-12 | P0-5/U/I | 坏校验、未知 Codec/Schema、越权路径及不可恢复资源导致恢复拒绝或原子回滚 | 失败恢复不允许半恢复、权限冒用或把缓存标成设备 Good |
| BUS-UI-08 | P0-6/S | Studio 内嵌测试宿主完成业务操作与 UI 观测 | 不得报告进程间 Runtime.Host 通信已实现 |
| BUS-UI-09 | P0-6/S | 工程订阅定义存储与运行 SubscriptionToken 解耦 | 保存重开不能恢复失效令牌/临时 NumericId |

## 8.2 V1.3 MQTT/IoT 风格消息与命令专项闭环（**全部 NOT_RUN**）

本节是以“简单入口 + 完整内核安全语义”为原则新增的 P0 候选测试，使用现有分组编号的连续新号；**不是现有代码已经提供的测试**。

| CaseId | 阶段/层级 | 必须成立的动作与断言 | 失败或边界反例 |
|---|---|---|---|
| BUS-MSG-15 | P0-3/U | 普通消息仅给 Topic + Payload 就可 Publish/Subscribe，内部自动保存 MessageId/来源 | 不强迫普通开发者手写整套 Envelope |
| BUS-MSG-16 | P0-3/U | Topic 与 EndpointAddress 关联但不等同，同一 Camera 能发 Captured/Faulted | MessageId、TopicId 不可互换 |
| BUS-MSG-17 | P0-3/U | Path 精确匹配、分层 `*/**` 与 NumericId 路由一致 | 通配命中仅用于观察，不能执行命令 |
| BUS-MSG-18 | P0-3/U | 临时轻消息可省持久类型定义，声明 TypeId/SchemaVersion 的消息要校验 | 类型/版本错误不得被当作有效强类型事件 |
| BUS-MSG-19 | P0-3/U | Publish 收到 Stage/Outcome/MatchedCount；零订阅默认可受理且计0 | 无订阅不能伪造 Delivered；RequiresSubscriber 才拒绝 |
| BUS-MSG-20 | P0-3/I | 两真实订阅者各收到一次；同一 SubscriberId 的重叠订阅按策略去重 | 订阅重复不能触发隐藏的第二业务处理 |
| BUS-MSG-21 | P0-3/I | 订阅回调抛异常/慢处理且新消息快速发布，异常隔离与队列有界 | 不能拖垮采集/命令，也不能无限缓存 |
| BUS-MSG-22 | P0-3/I | MPA.PublishAsync 的真实回调接入不创建新消息 Owner | 未注入发布者必须 NotSupported，不得无声成功 |
| BUS-CMD-17 | P0-4/U | CommandDefinition 最小 CommandId/Code/Name/Inputs/Outputs 可以完整描述命令 | 不要求每个命令先新建 IoT ServiceManager |
| BUS-CMD-18 | P0-4/U | CommandCode 在明确 Owner 下唯一；Name 可更改，CommandId 稳定 | 重复 Code、ID 映射冲突拒绝 |
| BUS-CMD-19 | P0-4/U | Inputs 强类型、必填、默认值；未给 Velocity 用默认值 | 显式 0/false 不得误算“未传” |
| BUS-CMD-20 | P0-4/U | Min/Max/Unit/SchemaVersion 仅按声明校验，错误参数在 Handler 前拒绝 | 未知字段/不兼容版本不得触发设备 |
| BUS-CMD-21 | P0-4/U | Outputs 可为空或包含多个有类型的字段，完成后类型校验 | Handler 返回错型、缺必填输出不能报 Completed |
| BUS-CMD-22 | P0-4/U | Description、Group/ServiceCode 可选，可由 App/Module/Capability 提供分组 | 不增加平行服务树、第二 FCM Schema |
| BUS-CMD-23 | P0-4/I | Path 与 CommandId 找到同一 Owner/CommandCode/Generation | 临时 Run 的过期 Handler 不得被命令重新命中 |
| BUS-CMD-24 | P0-4/I | 注册唯一 Handler；普通调用只填写 Target+Arguments | 第二 Handler/无 Handler 必须拒绝，不允许广播执行 |
| BUS-CMD-25 | P0-4/I | 平台生成 RequestId 与 OperationId，运行记录可查并与结果对应 | 不得把 CommandId/RequestId/OperationId 混为一个身份 |
| BUS-CMD-26 | P0-4/I | 正常短命令 InvokeAsync 返回结构化输出与完成状态 | Send Accepted 不等于 Completed；错误结果不藏在 Success 布尔字段 |
| BUS-CMD-27 | P0-4/I | 耗时命令可 Observe/Query/RequestCancel，超时观察 Unknown | 取消等待不等于设备已停，未知状态不能盲重发 |
| BUS-CMD-28 | P0-4/I | 目标 Owner 最终授权，参数与资源检查在执行前发生 | 未授权、参数错、忙碌不得触发硬件 |
| BUS-CMD-29 | P0-4/I | 同 OperationId/幂等键重试验证 Caller/Target/参数摘要 | 重复设备副作用、重启后虚称 exactly-once 均禁止 |
| BUS-CMD-30 | P0-4/I | 旧 MPA.InvokeAsync(...):Task 能兼容桥接正式 Handler/Result | 不破坏原规约接口，也不把 Task 完成视为现场读回通过 |
| BUS-UI-10 | P0-6/S | Studio 由 FCM/CommandDefinition 自动生成输入参数控件与输出展示 | 错型/缺失参数拦截；不能出现第二工程定义 |
| BUS-UI-11 | P0-6/S | MOL 命令节点从同一 Schema 生成输入输出，保存重开绑定不变 | 参数 Schema 不兼容修改必须阻止编译或提示迁移 |
| BUS-UI-12 | P0-6/S | Studio Topic 订阅/命令执行两种操作区分显示，短命令简单，长任务详情可展开 | 接收回执与执行结果混淆不可验收 |
| BUS-NFR-09 | P0-3/P | 简单消息连发、取消订阅、异常回调压力下持续有界 | 原地同步回调使 Publish 无法返回或无限内存增长 |
| BUS-NFR-10 | P0-4/P | 并发 Command 和高速 Variable 更新、大数据访问同时运行 | 命令排队不阻塞变量采集、图片上传不会饿死控制请求 |


## 9. 推荐映射至现有 TestCenter / ArchitectureSmoke 的方式

- 在当前 `Tests/ArchitectureSmoke/` 基础上新增实际归属目录/用例，不创建第三个 Test 项目；已存在 `Tests/TestCenter/Automated/` 体系里追加必要 TCT Case，编号由既有测试注册机制分配。
- 借用现有 `Tests/ArchitectureSmoke/Runtime/RuntimeCoordinationTests.cs` 的 MRK Unknown/Correlation 样例，以及 `Tests/TestCenter/Automated/Cases/TCTRuntimeReliabilityCases.cs` 相关 Operation 观测测试；避免复制一份同样的基础测试。**必须重新检查 master 实际内容**。
- M09 与 MPA 集成在现有 Module/ArchitectureSmoke 正式目录追加，验证不只可实例化，而且能按真实 PointMapping 更新状态/Source。
- Studio 测试遵守现有 WPF STA 和自动化框架；仅使用代码检查不能替代 UI 交互、保存重开。
- 对 CI 结果分成：源码编译通过、独立 TestCenter Case 通过、Windows GUI 自动测试通过、双 PID 通过、双机通过、实机 HIL 通过；每一项状态分别标记。

## 10. 各阶段验收门槛（退出条件）

| 阶段 | 退出条件 |
|---|---|
| P0-1 | ID-01～10 全部达到执行记录，严格 Path/ID/Scope/Generation/持久与临时身份语义 |
| P0-2 | VAR-01～13 与相关 NFR 完整，旧/当前/默认/来源/质量的一致性验证 |
| P0-3 | MSG-01～22，通过轻量消息全生命周期和真实 SendResult 反例 |
| P0-4 | CMD-01～30，命令全过程及超时 Unknown/授权/迟到确认闭环 |
| P0-5 | DAT-01～12，含同进程数据受控定位、支持类型 Heap 快照往返及失败回滚 |
| P0-6 | UI-01～12、NFR-01～10，具备 Windows CI/可操作同进程测试 Studio 与负面证据 |
| P1～P3 | EXT-01～08 按各期切分验收，不能提前计算入 P0 |

**注意**：每阶段状态若部分测试由其它模块已经覆盖，可以引用其有效 SHA、Runner 和断言，不必为满足本矩阵创建重复测试；但不能仅有“相关测试存在”就标 Completed。性能门槛应提交测量基线、目标设备负载、阈值裁决记录三者之一致性依据。

## 11. 单条执行结果记录模板

```text
CaseId：BUS-...
关联 Owner/类/接口：
源码路径及SHA：
环境：Windows 版本 / .NET Framework 4.8 / x64 / 实机或模拟
构建命令与结果：
执行器/TestCenter 入口：
步骤/断言：
预期反例：
实际日志与截图/CI URL：
结论：PASS / FAIL / BLOCKED / NOT_RUN
与本版本偏差：
遗留问题：
```

### 初始执行结果汇总

**2026-10-24 V1.2：全部新增与原有 `BUS-*` 均为 `NOT_RUN`。** 原 90 条 + 本轮消息/命令闭环新增 27 条 = **117 条候选验收**。编号仅属规格矩阵，未运行 Windows CI、TestCenter、ArchitectureSmoke、实机测试；不得将 `NOT_RUN` 隐藏为 `PASS`。
