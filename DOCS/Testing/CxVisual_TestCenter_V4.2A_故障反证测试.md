# CxVisual TestCenter V4.2A 故障反证用例

实施日期 2026-10-16。所有用例调用现存的实际Owner代码；TestCenter不创建生产替身，不增加项目。

- TCT.MDATA.007：MHSConfigSection 并发编辑、旧Revision拒绝覆盖。
- TCT.MDATA.008：MHSConfigPackage SHA256完整性校验与篡改拒绝（哈希不等于授权签名）。
- TCT.MDATA.009：MDEExecutionDataScope Owned清理即使出现异常也执行余下资源回收，并在关闭后禁止修改。
- TCT.MVIS.003：CxVisualEditor Undo后发生新事务，旧Redo分支作废，工程Revision继续增长。
- TCT.MRK.005：MRKExecutionPlanCompiler 编译阶段拒绝循环依赖，专业委托调用数保持零。

**验收门槛**：Windows NET48 Debug/x64，Owner依赖DAG，单Worker隔离全量运行及 --parallel 3 --repeat 3，JUnit和History证据保存；真实设备安全、通道SDK、Graph工程定义仍未接入。