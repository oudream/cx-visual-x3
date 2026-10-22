# CxVisual 工程组织与逻辑定义 P0 增量实施及 Windows 验收记录（2026-10-22）

> 真实代码库：oudream/cx-visual-x3，master。此文件为按提交固定的证据记录，不替代 V1.2 权威设计或后续最新 CI。并行开发持续推进；S1=源码、S2=实际测试、S3=固定 SHA Windows CI 通过、S4=专业设备与长期性能安全验收。

## 1. 本轮已实施工作包

### P0-3：状态机纯迁移的受校验快照接续

- 提交：`348d840af7c652a97dde4d8803ef1dedfbd6db39`。
- Core：`MOLStateMachineDefinition.Run(events, guardInputs, checkpoint, cancellation)`，从调用方提供的完整 Region→叶状态继续事件计算；未知状态、缺失 Region、跨 Region、非叶状态均拒绝。保持旧入口兼容，复制快照，不修改输入映射。
- MOC：固定编译的状态机可从本次 Flow 输入 `moc.state.checkpoint.<BlockId:N>` 读取 `IReadOnlyDictionary<string,string>`，类型错误即时失败；仍使用原 MRK Flow / SourceMap。
- 真实测试：`OrganizationLogicIntegrationTests.P03StateMachineGuards` 通过纯迁移、保存重开、Flow A 输出接续 Flow B、Flow 未传快照时从初始状态运行、非法/错型输入失败验证。
- 证据：`348d840a` 对应 Engineering Windows Acceptance `39706845943` 成功，Unified Runtime `39706845950` 成功，Studio `39706845922` 成功。
- 边界：这是**外部传入状态快照的受控接续**，不是 MRK 持续驻留实例、持久 Checkpoint、Journal 恢复，也未实现 Action/事件重入协议。

### P0-1：V1 Logic Aspect 的有界事件等待

- 实现提交：`7197f5c483e5ac142494b696433043ff5b66fd9f`；CI 回归修复提交：`155476bd21b21adbd77b379401ceaa9ea54afcf5`。
- Core：新增 `MOLControlBlock.AwaitEvent(inputName, timeoutMilliseconds)` 和 `MOLWaitDefinition` 事件输入；沿用原 schema1 Logic Aspect，XML 往返支持 eventInput，禁止无界超时及空输入。
- MOC/MRK：继续通过现有 `Resolver.RegisterAsync` 和 MRK Worker，在本次 Flow 输入中等待 `Task` 完成；事件唤醒后继续依赖执行，超时抛错、取消响应 Token，缺失或非 Task 输入失败。没有新增第二套事件引擎。
- 正反例：`OrganizationLogicIntegrationTests.P01EventAwaitTimeoutCancellationRoundtrip` 真实运行验证触发前动作不执行、触发后推进、坏输入失败、超时失败、取消无后继动作，以及 MEM Save/Open 保真。
- 首次 `7197f5c4` CI 报红是新增测试将编辑后的脏工程用 Dispose 默认关闭，违反 Core 既有生命周期保护；`155476bd` 修正为显式 `Close(true)`，没有放宽 Core。
- 后续并行 Studio/TestCenter 源码产生 C# 作用域编译冲突，已由其他提交 `c0e9cf4b0fca819c26026881cc4678584938e375` 修正。
- 固定 SHA 证据：`c0e9cf4b` 的 Engineering Windows Acceptance `39707835673` 成功（包含 ArchitectureSmoke 与 WPF/开发者用例步骤）；Studio Windows `39707835687` 成功；Config Foundation `39707835773` 成功。TestCenter、Config L1 此次核查仍在执行，后续按 GitHub 实际状态判断。
- 边界：运行输入 Task 只是进程内的受控事件信号，不承诺断电持久等待、消息确认去重、跨进程事件恢复或真实设备安全停机。

## 2. 对 P0-1～P0-4 和 V1.2 LOG 的实际差距

| 工作包 | 已见源码与自动测试 | 当前不能认定完整的内容 |
|---|---|---|
| P0-1 | Sequence/Branch/Switch/Loop/Exit/Wait（延时与新事件）等已有真实 MOC→MRK 测试，Windows Engineering CI 包含回归 | Try/Catch/Finally 全区域语义、Wait 可靠恢复、完整异常取消组合及专业侧合同 |
| P0-2 | Root→Mid→Leaf 类型端口、真实子流程编译、SourceMap、同 Content 多实例与负例已有测试 | 极深参数/策略混合、多专业 Content、设备异常及吞吐 S4 |
| P0-3 | Guard、分层/正交状态的有限事件纯计算、schema2 往返、Flow 内执行及快照受控接续 | 持续状态机实例生命周期、正交并发事件冲突、State Action/Checkpoint/Journal 真实恢复 |
| P0-4 | Studio 同源画布、Flow/QualifiedStepId 观察、Trace Gap 后权威快照投影及 WPF 测试 | 完整调用实例树导航、Watch/Step 控制授权、复杂状态机编辑及长期 UI 性能/断连反证 |

按 V1.2 的 LOG-01～LOG-13 验收矩阵，现阶段可标识：
- LOG-01/02/03/04/05/06：已有不同程度 S1/S2/S3 子项证据，但全量组合与失效反证尚不能宣布完成。
- LOG-07：MRK/F19 有独立并行及循环测试；专业资源背压、NoDrop、现场异常仍须证据。
- LOG-08：本轮补齐**受控状态接续**；持续驻留状态机及持久恢复仍缺。
- LOG-09：本轮补齐**Task 事件等待**；Try/Catch/Finally、可靠事件投递与恢复仍缺。
- LOG-10/11：已有限定运行观察、Gap 快照投影及基准入口；复杂多级 Trace/迭代、观察开销定量仍须全量验收。
- LOG-12：固定工程 Plan、软件授权发布有代码与测试；真实安全动作及跨重启版本对账未验收。
- LOG-13：上述固定 SHA 工程组织 CI 和 Studio 成功是**对应范围 S3**，不能代替所有 TestCenter/专业 S4。

## 3. 不变量与下一开发增量

保持 Logic Aspect 唯一工程真源，不增加项目、不复制 Graph、MRK 或 MDS。后续优先将 P0-1 Try/Catch/Finally 定义、编译、运行、异常与取消成组闭环；再做 P0-3 持续实例/事件时序/Checkpoint；P0-2 混合嵌套反例与 P0-4 调试导航依真实源码逐项完成。各项都必须提交前重新检查 master HEAD 和目标文件；只有新 SHA 的 Windows CI 成功才提升到 S3。
