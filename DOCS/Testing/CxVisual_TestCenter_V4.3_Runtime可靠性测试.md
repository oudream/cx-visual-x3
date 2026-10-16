# CxVisual.TestCenter V4.3 — Runtime故障、恢复与证据闭环

根据《CxVisual_TestCenter_FMP子方面归属与公共接口依赖设计_V2.1.md》，继续用**生产 Owner 的实际接口**验证运行内核，而不是建立测试版执行器。仅在 `Tests/TestCenter` 原有WPF项目中增加测试类，无新项目、不改变生产Owner。

## 新增9项生产黑盒测试
| TestId | Owner类型 | 验证点 |
|---|---|---|
| TCT.MRK.006 | MRKFileExecutionJournal / MRKJournalRecovery | 持久化重开、序号、外部操作Unknown优先于完成状态、确认后Close |
| TCT.MRK.007 | MRKFileExecutionJournal | 断电尾帧截断，修剪尾部，保留有效前缀并连续Append |
| TCT.MRK.008 | MRKFileExecutionJournal | 完整帧校验损坏拒绝，不跳过非法记录 |
| TCT.MRK.009 | MRKResourceLeaseCoordinator | 资源独占、等待取消、幂等归还及再取得 |
| TCT.MRK.010 | MRKExternalOperationTracker | OperationId幂等、CorrelationId冲突、Unknown与后续确认 |
| TCT.MRK.011 | MRKCorrelationWaiter | 先注册、快速应答、重复拒绝、零等待超时及迟到应答拒绝 |
| TCT.MRK.012 | MRKExecutionPlanSlot | 运行租约阻止热切换、版本单调、释放后激活 |
| TCT.MRK.013 | MRKExecutionGroups | 并发配额1/等待容量0、超额拒绝计数与归还 |
| TCT.MRK.014 | MRKJournalRecovery | 不同Flow不串用、无终态必须核对、无记录不能假绿 |

## CI反证增强
- `Run TestCenter` 输出JUnit必须有真实测试节点，全部状态通过；非零退出码立即失败。
- 并发重复测试数量须为隔离基线的**精确3倍**，且每个TestId恰好3个不同的Iteration，每个AttemptId全局唯一，JUnit有RunId。
- 传入肯定不存在的过滤条件必须退出1并输出0个测试的可检查报告，**零用例不算成功**。
- 继续运行31项目结构/Owner DAG审计、NET48 x64编译、3-worker回归和历史JSON归档。

## 边界不应混淆
运行Journal是单写入者本地文件；程序可以判定需人工核对，但本测试不实现跨进程所有权、主备仲裁、真实设备应答、工业安全回路或灾备恢复。计划热切换仅为软件内存级门槛，不能代替运动控制板卡/PLC联锁确认。现有Capability规格正式ID尚未和所有TestId完成审核映射。WPF人工交互和现场设备验收仍须单列证据。

验收状态：由本次提交对应Windows CI判定，尚未执行时不能标为通过。
