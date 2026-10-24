# CxVisual 统一交互机制 V1.1 → V1.2 审查意见与修订说明

> 日期：2026-10-24  
> 对象：原 V1.1 架构主文档 + 两份 V1.0 配套文档  
> 参考源码：GitHub `master@36855f3fade9b70ecb7c01cbdbb3e37264802926`（静态核对）  
> 限制：没有运行 Windows CI/TestCenter、真实设备/多进程测试；DOCS/02～04 Excel 工作表尚未完整读取。以下“已修复”指**修正文档契约**，不是源码已经实现。

## 一、原文主要问题（含原版行号）

| 严重度 | 来源/原文 | 发现 | V1.2 处理 |
|---|---|---|---|
| P0 | Architecture §5.1 L139 vs §5.1 L139首句、Testing BUS-VAR-03 | PreviousValue 被同时定义成前次提交值与前次不同值 | 固定为前次成功提交值；同值重复提交仍推进版本，ValueChanged 不触发 |
| P0 | Architecture §20.2 L602、Testing BUS-UI-05/07 | P0 同进程目标，却使用两个独立 Studio 连接外部 Runtime.Host | P0 改为两个同进程订阅者，UI 用嵌入式测试宿主；跨进程列入 P1 |
| P0 | Architecture §10.3 L293-301 | 数字地址未划分稳定工程端点和短期运行实例，持久化/重启语义不明 | 明确持久定义 ID 与运行临时 ID；新增 ID-09/10 |
| P0 | Architecture §12.2 L345 | `Stage` 混有 Rejected/Failed/Backpressured 结果 | 拆成 Stage+Outcome+错误原因，接收 ACK 另行报告 |
| P0 | Architecture §7.2、§7.4、MRK 源码 | MRK Confirmed 和现场反读 Confirmed 容易被同名混用 | 区分操作 Provider 确认与设备现场状态确认 |
| P1 | Architecture §5.2 L148、现有 VariableTable | 虽不在锁内回调，依然在 Update 的调用线程同步执行，可能阻塞采集 | 明确异步/隔离订阅通知、队列背压与专门测试 |
| P1 | Architecture §11.3 L325 | Query+Subscribe 初始化时存在丢更新竞态 | 规定 Snapshot+水位+增量连接、缺口重新同步 |
| P1 | Architecture §14.1 L381 | 用户要求各管理中心记录数据源，但旧文主要只规定变量 SourceId | 添加 Message/Command/Data 来源链，MDS `Snapshot.Source` 文件路径语义不替换 |
| P1 | Architecture §8、§17.4、Testing BUS-DAT-04 | ImageRef/Handle 样例依赖尚未公开实现的 DataRef 通用 API | 改为 P0 同进程 Owner/Run/Generation 限定定位；真实专业 Image/Volume 零拷贝待验 |
| P1 | Architecture §6.2 L177、Testing BUS-MSG-06 | 零订阅者默认 Publish 是成功还是失败有歧义 | 默认为发布可接纳、匹配数0；必达策略才拒绝 |
| P1 | Architecture §6.3 L181 | Dispose 是否保证已排队/正在执行回调完全停止不清楚 | 明确不再新入队和 Drain 的可选等待语义 |
| P1 | Architecture §17.1～17.3 | C# 例子使用顶层语句，不适合直接放进旧式 .NET Framework 4.8 项目 | 改为有 class/method 的使用例子（仍需项目编译验证） |
| P1 | Architecture §7.3 | 仅 OperationId/CorrelationId 不足以防不同参数误用幂等身份 | 增加 Caller/Target/命令类型/参数摘要一致性核验 |
| P1 | Architecture §8、原三份文档 | 遗漏此前确认的 DataHeap 可序列化/反序列化能力，元信息快照与真正恢复未区分 | 增加 §8.4 受控序列化、恢复原子性、Schema/Codec、来源与异常反例 |
| P1 | Architecture §6.2/§13 | 普通通知允许丢，与报警等不可丢事件共享策略可能埋隐患 | 明确按语义选择队列溢出，不可丢事件失败/审计而非静默覆盖 |
| P2 | 原三份文档的索引和完成标志 | 缺变更审查记录、增量反例少 | 新增审查报告、19 项候选测试，总计 90 |

## 二、检查未发现需要推翻的架构

四大能力定位（状态高速、消息轻巧、命令做事、数据堆高效管理大数据）、进程内逻辑总线与进程外可选物理总线、字符路径加雪花码、Machine/Container→AppCode→ModuleCode 分层订阅、各 Owner 权威真值、发送成功不等于执行成功，均与本次用户确认的方向一致。未建议创建全局万能 EventBus、第二 Heap 或强制 MQTT Broker。

## 三、仍存在的真正待决/未核事项（不是文档书写错误）

1. `DOCS/02～04` Excel：未逐工作表复核 M09/M12/M36/M28 的正式 ObjectId、项目和 API 名称，故新增类名仍是候选。
2. Snowflake 持久化究竟挂接哪个正式 MEM 定义对象、跨宿主 Worker/NodeId 分配方式、时钟回拨恢复策略，需 P0-1 验证裁决。
3. Subscription Pattern 语法、大小写、合法段和多 Scope 授权、外部导出级别，需 P0-1/P0-3 冻结。
4. P0 性能目标（吞吐、尾延迟、内存、队列上限）需先真实基准测试，再结合设备负载确定阈值。
5. 完整的业务 ImageRef 与真实专业数据载体、文件分块/SHM/远端流属于后续独立验收，不应由示意代码推出已实现。当前通用 Heap Serialize/Deserialize 没有在核对源码中找到完整公开 API，虽是既定需求，仍须真实编码与验收。
6. 要使 Studio 操控独立的 `Runtime.Host`，必须先存在真实 IPC/Bridge；当前 P0 可用同进程测试宿主，但不替代 P1 实际跨进程验收。
7. 可靠消息的持久化投递、崩溃恢复后幂等、工业联锁/PLC 实机验证属于明确的后续阶段。

## 四、测试矩阵修订

原 71 个 BUS 候选用例保留；新增 19 个关键反例：ID +2、VAR +3、MSG +4、CMD +4、DAT +4、UI +2，合计 **90** 条。新增项及正反断言详见 TestCenter 矩阵 §8.1。

## 五、文件与验证

- 修订主文档 `DOCS/Architecture/`：V1.2；章节沿用 20 章与 4 附录。
- 修订实施台账 `DOCS/Implementation/`：V1.1；更新接口边界和 P0/P1 验收划分。
- 修订测试矩阵 `DOCS/Testing/`：V1.1；90 条候选，用例全部 NOT_RUN。
- 本审查报告放在 `DOCS/Architecture/`；仓库业务源码、Windows CI、TestCenter 均未修改或运行。
