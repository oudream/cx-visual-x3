# CxVisual 逻辑编排—运行—调试一体化：代码实施与验收记录 V0.1

> 2026-10-18。与《CxVisual 统一工程组织体系完整架构设计 V1.1》§7.4 同源，**不另建独立体系总文档或项目**。本记录真实反映代码与验证等级，不能以通过静态检查代替 Windows CI。

## 本次实际代码

| 实际归属 | 源码 | 已形成的实现 |
|---|---|---|
| Mechanism.Core | Engineering/MOLLogicDefinition.cs | MOLLogicDefinition、MOLControlBlock、MOLControlKind、MOLExpression/Source/Comparison、MOLBranchCase、MOLLoopMode/Definition、MOLExitKind；验证唯一逻辑 Id、深度/数量预算、分支默认规则与逻辑结构；MOLLogicCodec 通过现有 MPMAspectKind.Logic + MEMEngineeringSession.SetAspect 原子保存，使用安全受限 XML 读写；项目快照、文件往返不创建第二工程树。 |
| Mechanism.Orchestration | MOCEngineeringCompiler.cs | MOCEngineeringCompiler、MOCCompiledPlan、MOCSourceMap、MOCStepOrigin、MOCObservedStep、MOCTraceView；调用现有 MRKExecutionPlanCompiler 和 IMRKCapabilityResolver，保持展开步骤到 Content/逻辑块/调用路径/轮次的来源映射。 |
| Mechanism.Runtime | 现有 MRK 编译器/ExecutionEngine/Trace | 不复制执行引擎、Worker、Trace；Sequence→DAG，Branch→Gate/Skipped/Join，Parallel→Fork/Join；有界 For/While/DoWhile/ForEach 展开为 MRK 唯一 Step 并绑定迭代和集合项，明确限额；实际能力来自 Provider。 |
| Mechanism.Visual | MOLFlowLayout.cs | 不依赖 WPF 的逻辑画布位置投影，ID 与逻辑模型保持一致。 |
| Mechanism.Presentation.Wpf | MOLFlowCanvas.cs | 真实 FrameworkElement 绘制分支/普通/循环容器，状态叠加与点击节点事件，不直接运行专业任务。 |
| Applications.Studio | MOLRuntimeOverlay.cs、MainWindow.xaml(.cs) | 原 Studio 新增逻辑页签、保存到当前 Page.RootContent 的逻辑示例、使用 **同一 MRKExecutionEngine 类型** 的 CPU 仿真、读 MRK Trace 并投影回图上的逻辑块。 |
| Tests/ArchitectureSmoke | OrganizationLogicIntegrationTests.cs、OrganizationWpfTests.cs | 测试工程保存重开与逻辑 Id；并行/分支互斥/Skipped；前置节点结果驱动分支；For/While/DoWhile/Foreach 次数及 item/iteration 绑定；子Content调用与来源路径；缺能力拒绝编译；Trace 追溯；真实 WPF STA 布局及绘制。 |

## 不应误称为已经全部完成的能力

1. **动态大循环**：当前是最多 4096 步的有界计划展开，适合小循环的行为闭环；并非 MRK 原生结构化 ControlRegion + 每轮独立 MDS Frame/Invocation。100万集合不应展开成百万个 Step，下一批必须在原 MRK Runtime 内增加复用 BodyPlan 的 Region 执行。
2. **控制逻辑范围**：顺序、If/Else（多Case选择）、有界 For/While/DoWhile/Foreach、Parallel、Content Invoke 已有实现。Try/Catch/Finally、Break/Continue/Return、事件 Wait、真正的 Switch 类型匹配/Case 表达式、并行 Foreach 背压尚未提供完整执行语义，编译器明确拒绝不支持的 Kind，不静默当作普通顺序执行。
3. **工程编排编辑**：Studio 现有新页签支持显示与 CPU 仿真状态覆盖；真正的拖放增删控制块、连线、参数编辑及完整双向导航需要在原 H06 中接续，不把示例当专业编辑器完成。
4. **运行业务集成**：本轮未接入 CT、Vision、PLC 设备及安全授权发布；示例使用 CPU 仿真 Provider。未添加平行的编排 Graph、设备驱动或全局单例。
5. **编译与操作证据**：本轮工作环境没有 Windows .NET Framework 4.8 编译环境。提交后的对应 Windows Actions 构建与真实 WPF/Studio 验收状态必须以实际日志为准，不能声称已通过。

## 验收入口

- Windows full solution x64 Release：原 `.github/workflows/engineering-organization-windows.yml` 会因 Core/Orchestration/Studio/ArchitectureSmoke 文件变更触发。
- `CxVisual.Tests.ArchitectureSmoke.exe --organization`：新增 OrganizationLogicIntegrationTests。
- `CxVisual.Tests.ArchitectureSmoke.exe --organization-wpf`：原 OrganizationWpfTests 的逻辑画布真实 WPF 绘制。
- 默认 ArchitectureSmoke：原执行回归与新增逻辑集成用例同时运行。
- Studio：创建工程与页面→逻辑页签创建 CPU 演示→仿真运行→选择逻辑块→保存/重开→再次编译运行。
- 性能与隔离验收：大节点数量、Trace 缓冲缺口、停止/取消、配置/能力错误、重复子流程调用、并行轮次数据互不覆盖、关闭 Studio 不改变生产运行的专项测试，仍需继续补全。

**验收结论：已有实际代码、测试已接入，但 Windows 全量构建、WPF 实测和完整工业控制语义均不得标记为通过或完成。**
