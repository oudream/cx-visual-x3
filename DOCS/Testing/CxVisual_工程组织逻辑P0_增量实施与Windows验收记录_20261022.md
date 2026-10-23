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


## 4. 2026-10-23：P0-3 多 Guard 显式优先级与 Windows 反证

- **代码修订**：Core 提交 `0eb04106b5cff52661f7c93eb33189fb548c7891`；ArchitectureSmoke 新反证 `e17aa5855eec15775418fa31d75a48ab5a326960`；测试中错误地将 `MOLWiringCodec.Serialize()` 的 `byte[]` 当作字符串，修复提交 `e57c18e5bf8472052863eb1e3060f1aa8350df7c`。**只有修复后的固定 SHA 可以作为本子项 S3 证据**，首次红灯不隐瞒。
- **定义与 Schema**：`MOLStateTransition(..., MOLExpression guard=null, int priority=0)` 保持原无优先级调用的默认语义；同 `Source + Event` 按 `Priority` 降序选取首个 Guard 成立的迁移，冲突的同级候选以及越界优先级明确拒绝。Schema2 为非零优先级追加可选 `priority` 属性；旧字段/未指定字段按 0 读取，恶意/损坏的非法取值拒绝。MOL 仍是唯一持久 Logic Aspect，MOC 继续交给原 MRK 执行。
- **开发者 API 正反例**：`new MOLStateTransition("CameraIdle","start","CameraAccepted", MOLExpression.InputTrue("approve"),30)`；较低的 `defer` 候选优先级 20、无 Guard 默认候选 0。两条同时 True 时明确先选 30；30 为 False 时才选 20；两条 False 时选默认；同优先级重复、优先级超限、缺少必需 `approve` 输入均定向失败。
- **实际工程/执行反证**：`OrganizationLogicIntegrationTests.P03GuardPriorityAndOrthogonalRegions` 验证 Camera/Motion 正交 Region、父状态 Reset、跨调用者 Checkpoint、MEM SaveAs/Open、错误优先级 XML、MOC Build、MRK Flow 成功及失败路径。不将静态单元断言冒充真实 Flow。
- **固定 SHA Windows S3**：`e57c18e5bf8472052863eb1e3060f1aa8350df7c`：工程组织 [39896367291](https://github.com/oudream/cx-visual-x3/actions/runs/39896367291)、统一运行 [39896367324](https://github.com/oudream/cx-visual-x3/actions/runs/39896367324)、Config L1 [39896367409](https://github.com/oudream/cx-visual-x3/actions/runs/39896367409)、配置公共底座 [39896367288](https://github.com/oudream/cx-visual-x3/actions/runs/39896367288) 均成功；工程组织 Job 包含 Windows x64 完整编译、Engineering/执行回归与开发者实例。不能外推专业实机 S4。
- **V1.2 LOG 对应**：LOG-01（新增 Schema2 字段向后兼容与损坏拒绝）、LOG-08（受控多 Guard 确定性迁移、正交 Region 与校验）、LOG-13（同 SHA Windows CI）；LOG-02 的 Studio 工程用户可视优先级编辑与 LOG-10 的逐迁移 Trace/Watch **尚未完成**。持续事件队列/去重、长期 MRK 状态机实例、Journal 恢复、取消与现场安全语义仍按 P0-3 缺口推进；本阶段不能宣告 P0-3 整包完成。


## 5. 2026-10-23：P0-1结构化Try/Catch/Finally与动态Foreach真实执行

- **MOL Owner**：复用 `MOLControlKind.Try`；新增 `MOLTryDefinition`、`MOLControlBlock.TryCatchFinally()` 和 Schema1 `TryRegion/TryBody/CatchBody/FinallyBody` 安全持久化。只属于原 MEMContent Logic Aspect，拒绝无Catch/Finally及非法结构。
- **MOC Owner**：Try/Catch/Finally分别编译为原 `MRKExecutionPlan` 嵌套Plan，共享原CapabilityResolver与SourceMap；Loop/Foreach在内层按当前Invocation导入Index/Item Gate，修复跨计划依赖。
- **MRK/MDS**：新增原执行计划内的 `MRKTryRegion`，复用 `MRKExecutionEngine.RunStepAsync`、F19 Work、MDS Frame与Trace。Try成功跳过Catch但执行Finally；失败用Catch-all处理，未捕获传播；取消不进Catch、独立10秒协作取消预算尝试执行Finally（无法强制中止不合作SDK，安全停机归专业Owner）；Finally错误不隐瞒。
- **ArchitectureSmoke**：`P01TryCatchFinallyRoundtrip` 覆盖非法定义、MEM保存重开、正常/已捕获/未捕获异常、Finally故障、真实Trace/SourceMap、异步等待取消、动态Foreach三轮（中间轮异常，逐轮传递iteration/item）。
- **历史红灯及修复**：首轮CI发现执行组foreach花括号作用域错误，`ddd1012f`修复；测试脏工程Dispose被MEM正常拒绝，`fbd05b6d`修正显式Close(true)；Wait真实轨迹是Scheduled而非Running，`8ba30de0`纠正断言。失败记录不是成功证据。
- **固定SHA Windows S3**：`b58776165ec5b3ca7b8deb8cd01df5e934f363b6`；工程组织 [39904735742](https://github.com/oudream/cx-visual-x3/actions/runs/39904735742)、Runtime [39904735623](https://github.com/oudream/cx-visual-x3/actions/runs/39904735623)、Config L1 [39904735584](https://github.com/oudream/cx-visual-x3/actions/runs/39904735584)、配置底座 [39904735670](https://github.com/oudream/cx-visual-x3/actions/runs/39904735670) 均成功。并行提交后的Studio/TestCenter须按自身同SHA重新核实。
- **P0-1仍未整包完成**：缺多Catch异常类型匹配、Throw/Retry受控定义、持久事件恢复、复杂Return/Break/Continue/异常组合、设备补偿及Studio完整异常区编辑。LOG-09仅增量覆盖，LOG-10/13综合与专业S4不冒称已完成。

## 6. 2026-10-23 MOL D1～D4 定义侧轻量增构与Windows验收

- **唯一真源没有改变**：D1 在 MOLControlBlock/MOLTryDefinition/MOLStateDescriptor 原值体系里补必要声明；D2 使用 MOLLogicDefinition 上下文验证；D3 在原 MEMEngineeringSession/Logic Aspect 完成原子更新；D4 复用工厂 + MOLLogicCodec.Edit + ReplaceBlock，不建新 Graph/Engine/Undo/项目。
- **源码路径**：Mechanism/Core/Engineering/MOLLogicDefinition.cs；Mechanism/Orchestration/MOCEngineeringCompiler.cs（新声明不支持时明确拒绝）；Tests/ArchitectureSmoke/Engineering/Integration/OrganizationLogicIntegrationTests.cs。
- **测试入口**：MOLDefinitionEditingClosure（不可变替换/撤销重做/文件往返/坏Schema）、MOLScalarTypeSafety（数字布尔字符串精确比较及类型拒绝）、MOLTypedDefinitionContracts（具名Catch、State Entry/Exit声明及MOC拒绝）、MOLLosslessSchemaConversion（仅单动作V1/V2无损迁移、复杂转换原子拒绝）、MOLThrowDefinitionContracts（Throw错误码、往返及MOC拒绝）、原 R05 循环退出组合回归。
- **兼容约束**：V1旧Catch-all及无 Throw 定义原格式保留；V2普通项目 Schema2 保留；带 Entry/Exit State Action 时 V2 连线载荷采用扩展 Schema3，旧读取器需拒绝，不能忽略声明。无法证明等价的迁移不做，保持原 ID/Revision/载荷。
- **运行边界**：新具名Catch、Throw、StateAction目前是定义合同，MOC明确报未实现，不可在MRK误按Catch-all/Return/空状态动作执行。这些能力的真正可运行性属于后续 MOC/MRK 工作包，不把它回填进MEM的值模型。
- **已通过固定SHA**：17c5c6685d8e43fbadcc78429379ce9399e380ff 和 1deb0aa3fdf9d751e27709db93345df2920ead2c 对应的工程组织/统一运行/Config L1/配置底座四项CI成功；fd47c09070d02547ccc7b8cbf3ca25f927a6d8e0 的 Studio S1、工程组织、TestCenter、Config L1、配置底座CI成功；c9e9bbadc0a1529e679568830b59e0b6420e39c2 的工程组织/统一运行/Config L1/配置底座CI成功。
- **一次真实失败不隐瞒**：418893da 的 ArchitectureSmoke 报 logic.exit_outside_loop，原因是 Branch() 工厂过早按完整根检查未挂接Loop的子树；fd47c090 改成本地Case规则先验、完整MOLDefinition在真正祖先上下文后验，保留对根部非法Break/Continue的拒绝。
- **最新代码SHA核实状态**：96c71ea82b9c99c71c383028f527415b0692b5bc 的工程组织 Windows、Studio S1、配置底座成功，其余工作流需按最终实际日志再升S3；后续 docs commit不能替代这次代码SHA的测试。
- **定义领域尚未整包收口**：完整事件/触发源合同、复合表达式/短路、跨Schema复杂子 Content 公开端口与版本映射、C#与Studio高级定义同源操作没有达到全量验收。不能用这里的代码量、若干绿灯或“已有Enum”宣布D1～D4全部完成。

**2026-10-23 D1～D4 最终代码固定SHA更新**：`96c71ea82b9c99c71c383028f527415b0692b5bc` 的 [工程组织 Windows Acceptance](https://github.com/oudream/cx-visual-x3/actions/runs/39917578173)、[Studio S1 Windows](https://github.com/oudream/cx-visual-x3/actions/runs/39917578073)、[TestCenter Windows .NET48](https://github.com/oudream/cx-visual-x3/actions/runs/39917578286)、[Config L1 Windows](https://github.com/oudream/cx-visual-x3/actions/runs/39917577976) 和 [配置底座](https://github.com/oudream/cx-visual-x3/actions/runs/39917578067) 均为 `success/completed`。本轮仅证明这五条真实工作流的测试范围及已覆盖定义合同；具名 Catch/Throw/State Action 的真正 MRK 执行、跨Schema复杂 Content、完整事件/组合表达式和高级 Studio 编排仍未完成，不能从 S3 外推出 S4 或整包完成。

## 7. 2026-10-23 MOL定义体系D1～D4 V1.0收口（固定SHA）

**本节只认定义侧 S3，不冒称MOC/MRK全部执行或实机S4。** 代码固定SHA：`dff37fbc0e3d5ef8f6a9b1c677dce87d71f2b5a1`；检查后续master的 Core MOL、MOC、Studio入口、ArchitectureSmoke定义测试和WPF测试文件Blob仍与该SHA一致。

| 四方面 | 实现、可验证拒绝、当前Owner |
|---|---|
| D1 类型 | 原MOLControlBlock可声明Try/多具名Catch、Throw/Exit、循环/调用、Wait/有类型事件契约、State EntryExit能力；原MOLExpression支持All/Any/Not与纯标量严格类型/有界短路，同一表达式用于V1条件和V2状态迁移Guard。复杂度、非法参数、缺输入、版本降级均有反证。Owner：MOL Core。 |
| D2 关系 | 嵌套/Case/循环出口作用域校验，V1/V2跨Content引用经MEM Reference，V1 Content公开端口直接来自MEM已有MPCPort，不造另一端口表。缺Reference/非法端口/循环外BreakContinue均拒绝；跨Schema可声明但真实执行能力未支持时MOC明确拒绝。 |
| D3 管理 | 原MEMSession Revision乐观编辑、不可变ReplaceBlock/Undo/Redo/Save/Open；Schema1/4结构、Schema2/3/5自由连线；畸形字段/降级读取拒绝；仅单Action且无MEM公共端口/映射/连接可原子转换；两侧Set拒绝跨Schema直接覆盖和RequiresRemap。 |
| D4 API | MOLExpression.InputCompare/All/Any/Not，MOLControlBlock.Action/Branch/InvokeContent/Exit/Throw/TryCatchFinally/AwaitEvent，MOLLogicCodec.Edit与ReplaceBlock，V2沿用MOCFlowBuilder.Edit/InvokeContent；不新造Builder或运行引擎。 |

**主要真实测试入口**：OrganizationLogicIntegrationTests中的MOLDefinitionEditingClosure、MOLScalarTypeSafety、MOLCompoundConditions、MOLCompoundStateGuards、MOLEventDeclarationContracts、MOLTypedDefinitionContracts、MOLThrowDefinitionContracts、MOLLosslessSchemaConversion、MOLCrossSchemaContentDeclaration、InvokedContentAndInvalidSource；OrganizationWpfTests用不同Content保留V1/V2两套画布。

**固定SHA Windows实测**：
- 工程组织 Windows Acceptance：[39924666837](https://github.com/oudream/cx-visual-x3/actions/runs/39924666837)，成功，包含Windows x64编译、真实Engineering/ArchitectureSmoke/WPF STA回归。
- Studio S1 Shell Acceptance：[39924666809](https://github.com/oudream/cx-visual-x3/actions/runs/39924666809)，成功。
- Config L1 Windows Build and Smoke：[39924666768](https://github.com/oudream/cx-visual-x3/actions/runs/39924666768)，成功。
- 配置公共底座：[39924666830](https://github.com/oudream/cx-visual-x3/actions/runs/39924666830)，成功。
- TestCenter Windows .NET48：[39924666747](https://github.com/oudream/cx-visual-x3/actions/runs/39924666747)，**失败**，Windows编译与前序用例成功，失败位于MCCChannelConfiguration三列表Schema WPF UI Automation呈现；属于TestCenter/配置GUI的独立缺口，不能冒充MOL回归失败或强行报TestCenter全绿。并行TestCenter修复提交需要另一固定SHA复验。

**定义侧收口判定**：D1～D4的平台核心定义能力及代码侧验收满足S3；不把高级可视编辑完整度、跨Schema结构化子流程真正编译执行、多Catch实际分派、Throw执行、状态动作执行、事件队列投递/恢复、工业设备安全归到MOL定义。上述运行/界面专业缺口依旧按MOC/MRK/MDS/Studio/TestCenter原Owner推进。
