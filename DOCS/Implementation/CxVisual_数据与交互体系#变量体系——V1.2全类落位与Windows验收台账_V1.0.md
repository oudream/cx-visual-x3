# CxVisual 数据与交互体系#变量体系——V1.2 全类落位与 Windows 验收台账 V1.0

> 2026-10-27｜唯一目标规格：[`Architecture/变量体系九项全类V1.2`](../Architecture/CxVisual_数据与交互体系%231变量体系——重构后全类架构与归属方案_V1.2.md)。
> 本文是**源码和验证账本**，不是再建第六份架构规格。按「已在x3编译的类型」「本轮完成源码但CI需复核」「迁移/增构未完成」区分，严禁把规划视为实现。

## 1. 唯一权威与基线

- GitHub：`oudream/cx-visual-x3/master`；不新建项目，28项目原Owner不动。
- 正式值 Owner：`Mechanism/Data/Variables/MDSVariable<T>`；`MDSVariableTable`、Tree、Registry、Provider及Dispatcher均不存第二份当前变量值。
- 兼容门面：`MDSVariableTable.Get/TryGet/Update/Sample/VariableChanged`、`MDSVariableRegistry.Register/Find/Remove/Snapshot`。
- 源码来源：`ReferTo/DeviceVariable` 生产经验、`cx-visual-x2` VariableNode/Group/Registry/RuntimeState/CxRuntimeVariableStore/DeviceVariableChangeDispatcher/Provider；避免直接引入旧静态全局Manager。
- 真实证据：[`317a9e4` Windows系列成功](https://github.com/oudream/cx-visual-x3/commit/317a9e48ef4589356e379bea711366622d586c4f)，[`0ce97c2` 变量专项成功](https://github.com/oudream/cx-visual-x3/actions/runs/40423429510)；随后提交必须分别核对其自身CI。
- 生产百万级是负责人提供的既有应用经验，不能等同x3当前百万/s可持续与设备实测。

## 2. 68项目标能力/类型逐组落位（依据V1.2，不另外创建空类）

| 原章节与数量 | 当前源码存在/复用 | 本阶段落位与差距 |
|---|---|---|
| Foundation.Data（3） | `FDTDataTypeDescriptor`、`FDTDataAdapters`，新增 `FDVVariableKey` | 基础值类型复用；变量引用Key已落，跨进程规范仍待统合 |
| Mechanism.Core（3） | `MEMResourceEntry`、`MRIObjectAddress`、`MPMBinding` | 工程真源保留，变量定义从工程编译为运行定义的完整映射待实现 |
| MDS/Variables（21） | `MDSVariableQuality`、`IMDSVariableTable`、`Definition`、`Node`、`Group`、`MDSVariable<T>`、`Snapshot<T>`、`Sample<T>`、`Table`、`Registry`、`Scope`、`UpdateOptions`、`UpdateResult`、内部ValueRules/Tree索引 | 单变量状态已合并（不再公开重复State<T>），ID/Path/作用域/Previous/Default/Quality/Source/Revision/CAS落位；`Handle<T>`当前由直接解析的MDSVariable<T>承担；Batch、BatchResult、StoreSnapshot、完整AccessPolicy与可恢复生命周期仍未交付 |
| MDS/Dispatch（8） | `MDSVariableChange`、`MDSVariableSubscription`、`MDSVariableChangeDispatcher`、`MDSVariableDispatchMetrics`，内部LatestOnly有界Buffer | 正式Update锁内冻结变化版本，异步LatestOnly、订阅过滤、退订、Drain已落；Batch策略/SubscriptionOptions、Reliable必达与失败恢复未交付 |
| MDS/Providers（8） | `IMDSVariableProvider`、`MDSVariableProviderBinding`、`MDSVariableProviderHost`、`MDSVariableProviderHealth`、`MDSProtocolDataTargetAccess`、`MDSVariableTypedPointBinding<T>` | Provider Host兼作唯一注册管理（不建第二个Registry）；Descriptor与外部协议能力发布、恢复策略需完善。设备只读变量可由显式绑定的Provider刷新 |
| Mechanism.Connectivity（4） | `MPAPointDefinition`、`MPAPointTable`、`MPADataMapping`、`MPADataMappingExecutor` | 已存在并复用；点表热路径减少逐次泛型反射；需要真实通道+PLC/HIL |
| Professional.Automation（5） | 老仓ReferTo与x2有工业Provider/Adapter成熟源码 | 5种PAD工业适配的正式x3类尚未整体迁入并做CxECTStudio/气密性生产接口验收，不以模拟Provider代替 |
| Runtime/Orchestration/Messages（3） | `MRKCommandMdsBinding`、`MOCEngineeringCompiler` | 保留已有Owner；变量到编译流程与消息桥的完整对象级实测尚待补齐 |
| Presentation.Wpf/Studio（7） | 已新增TestCenter人工变量工作台（不冒充Studio设计器） | TestCenter可真实定义、更新、建组、移动、监视快照、观察派发及启动热路径；Studio共用6个Variable ViewModel及布局/绑定仍待实施 |
| ArchitectureSmoke/TestCenter（6） | 新增变量对象、组织树、派发、Provider、CAS/Scope等ArchitectureSmoke方法，以及`VariableThroughputBenchmarks`与`TCTManualVariableFrame` | 接入完整ArchitectureSmoke和独立Windows专项；百万级结果需CI固定SHA/硬件环境；UI交互和现场硬件各有独立门禁 |

**注意**：68是V1.2的“全项目候选类型清单”，包含已存在的外围类型、复用与职责合并；不是新增类数量，也不是当前全部实现数量。

## 3. 核心实现与提交线索

| 主题 | 关键实际源码 |
|---|---|
| 单变量Owner/兼容表 | `Mechanism/Data/Variables/MDSVariable.cs`、`MDSVariableTable.cs`、`MDSVariableUpdates.cs` |
| 组织树和稳定身份 | `Variables/MDSVariableTree.cs`、`MDSVariableScope.cs`、`MDSVariableRegistry.cs`、`Foundation/Data/Variables/FDVVariableKey.cs` |
| 版本一致变化派发 | `Variables/Dispatch/MDSVariableChangeDispatcher.cs`、`MDSVariable<T>.Commit`和`Table.ChangeCaptured` |
| Provider | `Variables/Providers/MDSVariableProviderHost.cs`，`MDSProtocolDataTargetAccess.cs` |
| 可操作工作台 | `Tests/TestCenter/Manual/Variable/TCTManualVariableFrame.cs`、`TCTWorkspaceRegistry.cs` |
| 测试与吞吐 | `ModuleFoundationSmokeTests.cs`、`VariableThroughputBenchmarks.cs`、`.github/workflows/variable-system-windows.yml` |

## 4. 优先完成门禁（按未实现差距，不另起一套体系）

- **VAR-G01 完整变量对象**：复核可变引用类型、Default null、质量与来源、版本冲突、跨Scope代次以及旧API与旧DLL兼容；强类型热路径不被同步消费者拖慢。
- **VAR-G02 组织树与工程编译**：ID/Path复用MEM；实例Owner、子组并发一致性、移动/撤销/删除重建、对象身份不重复、Studio工程往返。
- **VAR-G03 变化策略**：Immediate/Batch/LatestOnly/Reliable语义分离；Reliable明确确认、重放、容量失败；不能把LatestOnly当作逐笔必达。
- **VAR-G04 Provider真实生产接入**：从ReferTo/x2迁移实际CxECTStudio与气密项目所用API/Adapter；连接重建与授权边界、设备质量时间戳、真实Point到Variable/消息/命令契约。
- **VAR-G05 运行数据全量快照**：跨变量批量事务与Snapshot/Restore必须真的一致，失败不得半提交或出现旧值漂移。当前只保证每一个MDSVariable<T>单对象原子更新。
- **VAR-G06 专业UI/验收**：Studio配置/Watch/变化诊断/Provider/Mappings复用现有MPW控件，不用第二份值仓；Windows真实UIA、功能异常、长稳性能、实机PLC/HIL分别验收。

### 事实边界

Windows工作流通过只证明它实际执行的单据。真实工业采集每秒百万级、可靠逐笔消息、远端跨PC、跨变量原子事务、旧x2外部程序行为一致性尚不能标为PASS。
