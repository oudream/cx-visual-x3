# 架构决策记录

## ADR-001：稳定身份与显示路径分离

`StableReference/VariableId` 是持久身份；Path、Parent、Group 和 Tags 是可变组织信息。目录、产线或页面重排不得改变绑定身份。

## ADR-002：只保留四种正式执行模型

正式模型为 ControlFlow、DataFlow、Workflow、StateMachine。旧 EventDriven 通过 ControlFlow 的触发策略兼容，不冻结为第五种模型。

## ADR-003：Kernel 只协调

`ExecutionRuntimeKernel` 只选择 Adapter、建立执行/资源作用域和记录终态。`GraphExecutionModelKernel` 只选择 Graph 执行模型。两者均不实现 Motion、Vision、CT、Inspection 专业语义。

## ADR-004：资源与策略是公共编排能力

Retry、Fault、SideEffect、Resource Requirement/Lease/Manager 不再由 Graph Core 拥有。旧 Graph 名称仅是 Compatibility API。

## ADR-005：协议不拥有传输

协议会话依赖 `IChannel`；TCP Channel 不理解寄存器或 PLC 业务；Workbench 不解析 Modbus 帧。生产链固定为 Provider → Protocol → Channel。

## ADR-006：Drawing 与 Diagram 使用投影而非继承

HMI Element/Symbol 与 Graph Node 不共享业务基类。它们只投影到公共 Viewport、Selection、Presenter 和 ConnectionInteraction。

## ADR-007：专业 Runtime 由 Workbench 贡献

Host 使用 Factory/Registry 组合专业能力，不直接创建协议或跨 Workbench Runtime。旧 `CxVisualRuntime` 只作二进制与源码兼容入口。

## ADR-008：不以物理 DLL 数量代替架构边界

在 .NET Framework 4.8 和既有部署约束下，部分公共契约与实现仍处于同一程序集。边界以依赖方向、Namespace、Factory/Adapter 和架构门禁保证。
