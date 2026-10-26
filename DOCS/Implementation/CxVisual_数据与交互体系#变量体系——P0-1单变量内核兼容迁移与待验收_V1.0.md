# 变量内核 P0-1（2026-10-26）

本次提交是在既有 MDSVariableTable 上增量重构，**不增加新程序集，也不引入第二套 Store**。

- 以 `MDSVariable<T>`（唯一值与状态 Owner）替换旧表 Entry：Current / Previous / Default / Quality / RefreshUtc / ChangeUtc / Revision / Source；
- `MDSVariableTable` 仅作为Scope下的变量索引和兼容 API，`Resolve<T>` 一次解析后可直接强类型访问；
- 保持原有 `Get/TryGet/Update/Sample/VariableChanged` 及其常见异常语义，保留 byte[] 防御复制与首次未初始化行为；
- 吸收 x2 `CxVariableRuntimeState<T>` 的“Previous只在实际变值时推进”，连续同值提交仅刷新时间和 Revision；
- 事件仍旧是同步兼容通知，P0-3 才引入有界异步变化派发。当前暂未声称百万/s吞吐；
- `DefaultValue` 现在仅支持非 null 显式默认值，显式 null 默认值和复杂可变类型 Adapter 留给后续契约；
- `VariableId` 可由工程定义明确提供；未传时暂以 Name 兼容，**这不等于已完成可移动的 ID/Path 变量树**。

ArchitectureSmoke `ModuleFoundationSmokeTests.VariableObjectSmoke` 增加正反例。Windows x64 构建/运行须以对应提交 CI 日志为证，不能把源码提交视作测试通过。
