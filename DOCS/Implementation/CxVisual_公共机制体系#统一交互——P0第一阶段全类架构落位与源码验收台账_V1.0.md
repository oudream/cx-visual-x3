# CxVisual P0 第一阶段全类架构落位与验收台账 V1.0

> 2026-10-24；基线主规格：统一交互 V1.4；本轮只落第一阶段，不冒充 P0-3 Message 分发和 P0-4 Command 执行已完成。

## 源码落位（28 项目不增加项目）
- Foundation.Core：FOIInteractionIdGenerator（41+10+12雪花码、进程内Worker租约、回拨/溢出拒绝）、FOIInteractionEndpointAddress（8段路径、数字ID、Run/Generation）。
- Mechanism.Core：MRIEndpointRegistry / MRIEndpointRegistration（进程内Scoped双索引，Path/ID同一地址，注销防误删、跨Run拒绝）。
- Mechanism.Data M36：MNMMessageTypeDefinition（可选强类型消息Schema校验）。
- Mechanism.Logic M12：MLGCommandParameterDefinition、MLGCommandDefinition、MLGCommandDefinitionCatalog（类型/必填/默认值/未知字段与重复定义校验、只读运行目录；不新增工程持久真源）。
- ArchitectureSmoke：Integration/InteractionStageOneTests.cs；独立入口 `--interaction-stage1`，默认回归也运行。

## 设计/实现区别
- 已有 MRKExternalOperationTracker、MDSVariableRegistry、MRIObjectAddress 均保留为权威Owner；本轮 Registry 只存公开运行端点索引。
- Message Publish/Subscribe/Dispatcher、CommandHandler/Invoke/Result、跨进程 Worker 租约、Studio/MOL 接入与真实设备执行**尚未实现**。
- 命令定义只读 Catalog 是已发布定义快照，不可作为可编辑或持久工程数据库；须与 MEM/FCM 正式定义接入后才允许工程端使用。
- Snowflake 只防本进程内 Worker 冲突，跨进程 WorkerId 必须由部署端保障。
- DOCS/04 的正式对象编号及命名冻结需与本轮源码逐类复核；基于 ProjectCatalog 与实际 csproj 不新增项目。
- Windows VS2022/.NET 4.8 编译与测试结果以本提交关联的 GitHub Actions 为准；在没有执行日志前不声称 PASS。

## 验收入口
`CxVisual.Tests.ArchitectureSmoke.exe --interaction-stage1`；再运行默认回归与 `--concurrency`。必须覆盖重复 Worker、时钟回拨、路径非法、双地址同Owner、跨Run、失效令牌、强类型消息校验、命令默认值及0/false、缺失/多余/错型参数、定义重复。
