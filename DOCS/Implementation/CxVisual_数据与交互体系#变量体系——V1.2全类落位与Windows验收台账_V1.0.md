# CxVisual 数据与交互体系 #1 变量体系 V1.2——68项全类源码归属与验收台账（更新）

> 2026-10-27 按主设计逐项重新审阅。唯一目标：[Architecture/V1.2 全类架构](../Architecture/CxVisual_数据与交互体系%231变量体系——重构后全类架构与归属方案_V1.2.md)。本文件是**差距与证据台账**，不改写该讨论稿，也不以新增空类为目标。
>
> 审计基线：本次已读源文件和Repository Tree；本轮最后核实的专项 **PASS** 是 [GitHub Actions #40460216776](https://github.com/oudream/cx-visual-x3/actions/runs/40460216776)，其固定提交为 `4727282`，包含 Windows x64 重建、`--variables`、完整 ArchitectureSmoke、`--variable-benchmark`、TestCenter 真实 STA 工作区。**后续AccessPolicy/Batch/ProviderDescriptor提交仍须独立核对各自CI，源码已提交不等于PASS。**
>
> 标记说明：**已有** = 目标Owner代码可找到；**职责合并** = 由已有对象履行，不能为了凑数另造同类；**部分/待增构** = 声明或主要路径存在但原文功能不完整；**未实现** = 没有可替代的实际能力；**需联调/实机** = 非本地模拟可代替。原文将68项分成10组，以下逐项完整列出。

## 一、逐类型对账（10组、68项）

### Foundation.Data（3）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `FDTDataTypeDescriptor` | 已有 | Foundation/Data/Types/FDTDataTypeDescriptor.cs；基础类型描述复用 |
| `FDTDataAdapters` | 已有/待增构 | Foundation/Data/Types/FDTDataAdapters.cs；复杂可变值适配尚未覆盖所有变量类型 |
| `FDVVariableKey` | 已有 | Foundation/Data/Variables/FDVVariableKey.cs；Scope + StableId，无第二身份仓 |

### Mechanism.Core（3）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `MEMResourceEntry` | 已有/待联调 | Mechanism/Core/Engineering/MEMNode.cs；工程资源真源 |
| `MRIObjectAddress` | 已有 | Mechanism/Core/Engineering/MRIObjectAddress.cs；工程对象定位 |
| `MPMBinding` | 已有/待联调 | Mechanism/Core/Engineering/MPMValue.cs；工程属性与变量映射需贯通编译 |

### Mechanism.Data / Variables（21）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `MDSVariableQuality` | 已有 | MDSVariableTable.cs；Good/Bad/Uncertain/Stale |
| `IMDSVariableTable` | 已有 | MDSVariableContracts.cs；旧公共表接口 |
| `MDSVariableDefinition` | 已有/增构 | MDSVariableTable.cs；含稳定ID、默认值、权限策略；2026-10-27补充显式null默认值与Snapshot/Restore区分，待最新CI确认 |
| `MDSVariableNode` | 已有 | MDSVariableTree.cs；身份/路径/OwnerRef/Tags |
| `MDSVariableGroup` | 已有 | MDSVariableTree.cs；分组层级和移动 |
| `MDSVariable<T>` | 已有/增构 | MDSVariable.cs；单份权威值、前值、质量、版本 |
| `MDSVariableState<T>` | 已吸收 | 变量状态直接封装在MDSVariable<T>私有字段，不另造可写State |
| `MDSVariableSnapshot<T>` | 已有 | MDSVariable.cs；原子单变量快照与防御性复制 |
| `MDSVariableSample<T>` | 已有 | MDSVariableContracts.cs；旧API兼容 |
| `MDSVariableTable` | 已有/增构 | MDSVariableTable.cs；单变量、批量事务、一致快照、导入导出 |
| `MDSVariableRegistry` | 已有/增构 | MDSVariableRegistry.cs；Scope注册与StableId检索 |
| `MDSVariableScope` | 已有/待加固 | MDSVariableScope.cs；代次隔离已落；跨线程注销/写入线性化需加强 |
| `MDSVariableHandle<T>` | 已有/待加固 | MDSVariableScope.cs；按Scope代次预绑定；跨线程注销原子性待强化 |
| `MDSVariableUpdateOptions` | 已有/不完整 | MDSVariableUpdates.cs；Source/Quality/CAS/Principal已落，来源时间与连续序列仍待落实 |
| `MDSVariableUpdateResult` | 已有/待增构 | MDSVariableUpdates.cs；Revision/ValueChanged/Quality/RefreshUtc；诊断信息待统一 |
| `MDSVariableBatch` | 已有 | MDSVariableBatch.cs；批次请求/Scope/BatchId |
| `MDSVariableBatchResult` | 已有 | MDSVariableBatch.cs；逐条结果与通知失败 |
| `MDSVariableStoreSnapshot` | 已有/不完整 | MDSVariableStoreSnapshot.cs；内存范围全状态快照及恢复，持久化编解码未验收 |
| `MDSVariableValueRules` | 已有/待加固 | MDSVariable.cs内嵌；复杂结构/范围/自定义适配不完整 |
| `MDSVariableIndex` | 已合并/部分 | MDSVariableTree.cs内部ID/Path索引，别名与多维查询尚缺 |
| `MDSVariableAccessPolicy` | 已有/待增构 | MDSVariableAccessPolicy.cs；Principal/Provider白名单和读写控制，跨命令授权仍待联调 |

### Variables / Dispatch（8）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `MDSVariableChange` | 已有 | Dispatch/MDSVariableChangeDispatcher.cs；冻结值/前值/版本/世代 |
| `MDSVariableChangeBatch` | 已实现 | Dispatch/MDSVariableChangeDispatcher.cs；与Table.BatchCommitted关联，含Scope/BatchId/BatchSequence和冻结变化列表 |
| `MDSVariableSubscriptionOptions` | 已有/不完整 | Dispatch/MDSVariableChangeDispatcher.cs；ID及Kind筛选，策略选择未完整 |
| `MDSVariableSubscription` | 已有 | Dispatch/MDSVariableChangeDispatcher.cs；退订与生命周期 |
| `MDSVariableChangeDispatcher` | 已有/部分 | Dispatch/MDSVariableChangeDispatcher.cs；LatestOnly后台派发和乱序拒绝；新MDSVariableDispatchRoute对既有表和Journal统一封装派发策略生命周期 |
| `MDSVariableDispatchBuffer` | 职责合并/仍待增构 | LatestOnly队列由Dispatcher内部_latest/_order承担；MDSVariableReliableChangeJournal补充有界排队、Ack/拒绝/缺号补投，未实现磁盘持久化 |
| `MDSVariableDispatchMetrics` | 已有/待增构 | Dispatcher中Received/Delivered/Overflow/Coalesced/Stale/Errors；长稳统计与p99延迟仍未补齐 |
| `MDSVariableDispatchPolicy` | 已实现/可靠性有限 | Dispatch/MDSVariableDispatchRoute.cs；统一选择Immediate/Batch/LatestOnly/Reliable，但Reliable目前只提供进程内批次Ack/重放，不提供断电持久化 |

### Variables / Providers（8）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `IMDSVariableProvider` | 已有/不完整 | Providers/MDSVariableProviderHost.cs；Start/Stop/Dispose/Publish，通用读写/查询能力需扩展 |
| `MDSVariableProviderDescriptor` | 已有 | Providers/MDSVariableProviderHost.cs；只读身份/类型/绑定数/运行标志 |
| `MDSVariableProviderRegistry` | 职责合并 | Host承担注册、查询、Unregister，避免第二套Owner |
| `MDSVariableProviderHost` | 已有/待加固 | 生命周期/代次/白名单映射；异常隔离与恢复策略需完善 |
| `MDSVariableProviderHealth` | 已有/待增构 | 运行/成功/拒绝计数与最后故障；断连时间/重试/质量聚合需扩展 |
| `MDSVariableProviderBinding` | 已有 | Scope+Point+Variable对应及原Scope有效性检查 |
| `MDSProtocolDataTargetAccess` | 已有 | MPA与MDS绑定，读写已去逐次反射 |
| `MDSVariableTypedPointBinding<T>` | 已有 | Provider固定Point强类型绑定，避免逐次反射与表扫描 |

### Mechanism.Connectivity（4）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `MPAPointDefinition` | 已有 | Mechanism/Connectivity/Protocols/Points/MPAPointTable.cs；规约点定义 |
| `MPAPointTable` | 已有 | 同上；协议点索引，不是业务变量真值 |
| `MPADataMapping` | 已有 | Protocols/Mapping/MPADataMapping.cs；数据映射 |
| `MPADataMappingExecutor` | 已有 | 同上；映射执行 |

### Professional.Automation（5）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `PADDeviceVariableCatalog` | 已有/迁移子集 | DeviceVariables/PADDeviceVariableCatalog.cs；静态目录/稳定ID，不复制MDS值 |
| `PADDeviceVariableProvider` | 已有/迁移子集 | DeviceVariables/PADDeviceVariableProvider.cs；类型检查与真实Owner发布 |
| `PADLegacyVariableManagerBridge` | 已有/迁移子集 | 同上；ReferTo事件按StableId汇总最新值，非逐笔必达 |
| `PADDeviceVariableMetadataAdapter` | 已有/迁移子集 | Catalog.cs；生成正式MDS定义，丰富元数据尚需x2对照 |
| `IPADDeviceVariableWriteBackend` | 接口已有/未接线 | Provider.cs；真实工业写入仍需驱动/命令授权与测试 |

### Runtime / Orchestration / Messages（3）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `MRKCommandMdsBinding` | 已有/需联调 | Mechanism/Runtime/Commands/MRKCommandMdsBinding.cs |
| `MOCEngineeringCompiler` | 已有/需扩展 | Mechanism/Orchestration/MOCEngineeringCompiler.cs；MEM至MDS变量编译须验收 |
| `MDSVariableMessageBridge` | 已有/需联调 | Mechanism/Data/Variables/MDSVariableMessageBridge.cs；复用M36八层Topic，仅轻量LatestOnly通知 |

### Presentation.Wpf / Studio（7）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `UVariableTreeViewModel` | 功能部分合并 | MPWVariableWorkspaceViewModel.Roots与WPF TreeView；完整虚拟化/筛选未实现 |
| `UVariableEditorViewModel` | 未完整实现 | 现有运行值写入UI，不等于MEM正式变量定义/权限编辑 |
| `UVariableWatchViewModel` | 已有 | Mechanism/Presentation.Wpf/Variables/MPWVariableWorkspaceViewModel.cs |
| `UVariableChangesViewModel` | 已有 | 同上；有界变化历史 |
| `UVariableProviderViewModel` | 已有/待扩展 | 同上；目前主要显示Binding，需健康/断连诊断 |
| `UVariableMappingViewModel` | 功能部分合并 | ProviderRows与Bindings展示；映射编辑/校验/撤销未完善 |
| `StudioVariableWorkspace` | 装配职责已有 | Applications/Studio/StudioShell.Variables.cs及doc.variables；未另造独立Studio Owner |

### ArchitectureSmoke / TestCenter（6）

| V1.2目标类型 | 核对状态 | 实际Owner、接口及差距 |
|---|---|---|
| `VariableModelSmokeTests` | 测试职责合并 | Tests/ArchitectureSmoke/Integration/ModuleFoundationSmokeTests.cs的VariableObject/Tree/Scope等 |
| `VariableStateSmokeTests` | 测试职责合并 | 同上；CAS/Restore/Batch/AccessPolicy等 |
| `VariableChangeDispatcherTests` | 测试职责合并 | 同上；LatestOnly/乱序/过滤，Reliable尚缺 |
| `VariableProviderIntegrationTests` | 测试职责合并 | 同上；Provider与PAD模拟接口，不等于实机 |
| `VariableThroughputBenchmarks` | 已有/范围有限 | Tests/ArchitectureSmoke/Integration/VariableThroughputBenchmarks.cs；短时吞吐/Gen0，无p99/长稳 |
| `TCTVariableWorkbenchFrame` | 等效实现 | Tests/TestCenter/Manual/Variable/TCTManualVariableFrame.cs；WPF人工页并复用MPW控件 |

## 二、核心归属裁决及复用说明

1. **唯一运行状态**：`MDSVariable<T>`保存Current/Previous/Default/Quality/Source/Revision；MDS Table/Registry/Provider/Dispatcher不再建可写镜像；运行树索引对齐稳定ID，MEM仍是工程定义权威。 `MDSVariableState<T>`与`MDSVariableIndex`可由原类内部职责承担，除非后续能证明单独公开的必要性。
2. **四种派发语义**：目前仅LatestOnly具备明确有界合并与订阅验收。Immediate、Batch、Reliable要分别实现并建立时序、容量、故障、确认/重放测试；尤其不能声称现有Dispatcher已逐笔必达。
3. **Provider不得接管变量真值**：Host兼注册表，只做生命周期、映射与授权；PAD复用x2/ReferTo业务元数据、旧变化回调语义，不迁回旧静态VariableManager。真实设备写入须经专业驱动及MRK等受控命令入口。
4. **WPF显示不等于工程编辑**：`MPWVariableWorkspaceControl`和Studio `doc.variables`已能从正式MDS观察状态；编辑工程变量必须通过MEM Session/事务而非UI修改临时Tree。
5. **工程部署的证据分层**：源文件存在 → Windows编译通过 → 独立功能正反例通过 → 全架构通过 → UI操作验收 → 长稳与实机验收，严格区分。

## 三、仍须完成的独立门禁

| 门禁 | 未完成的可验收内容 |
|---|---|
| VAR-G01 对象/权限 | 默认值null语义、完整可变引用适配、范围检查、带身份策略的写入/Provider权限验收；复杂Schema及旧DLL兼容 |
| VAR-G02 工程到运行 | MEM工程变量Schema→MDS运行定义/Scope/Tree的真实编译、映射变更、保存/撤销/重建、Index/Alias |
| VAR-G03 变化处理 | `MDSVariableChangeBatch`、可执行`MDSVariableDispatchPolicy`、Immediate/Batch、Reliable确认/拒绝/背压/故障恢复/长期顺序及p99 |
| VAR-G04 Provider/生产 | x2目录元数据/读写API补全、设备状态时间戳、断连重试健康、CxECTStudio和气密项目实际驱动/命令/通讯反例 |
| VAR-G05 运行快照 | Batch/Restore已形成独立接口，仍须压力与崩溃恢复、跨线程注销时的写入线性化、持久化格式及原始Schema校验 |
| VAR-G06 Studio/TestCenter | 真实MEM变量编辑器、Provider Mapping编辑/权限、防重复注册、错误状态诊断，Windows UIA/持续吞吐/p99/实机HIL |

## 四、已验证证据与免责声明

- **确认Windows成功**：[`4727282`变量专项](https://github.com/oudream/cx-visual-x3/actions/runs/40460216776)执行`--variables`并通过全架构、TestCenter STA及百万次本地性能；本次记录单线程约1,070万次/s、独立四线程合计约2,451万次/s，均为Windows Runner短时进程内测量。
- **新代码待验收**：`MDSVariableAccessPolicy`/`MDSVariableBatch`/`MDSVariableProviderDescriptor`的提交与相关CI以其固定SHA和Actions实际结论为准；在此之前不能标记已验收。
- **尚未确认**：真实PLC/气密性/CxECTStudio、跨进程/跨PC命令链、Reliable逐笔持久保证、长稳百万人次/s、全量Studio工程编辑的发布级验收；原生产代码经验不等于x3新集成的实机PASS。


## 五、2026-10-27 本次开发与新验收证据

1. [`3a4ba6c`](https://github.com/oudream/cx-visual-x3/commit/3a4ba6ce4cd8d8cfb69270667689da6d916590ca)：修复Reliable丢批风险；运行中注册以原Table批次序号初始化起始水位，容量满和序号缺口分别拒绝，必须补投被拒批次后才能继续Ack；不声称磁盘持久。
2. [`779633f`](https://github.com/oudream/cx-visual-x3/commit/779633f605fc4acb6abbbdb92bc58b9905a433f0)：新增`MDSVariableDispatchPolicy`和`MDSVariableDispatchRoute`，清楚区分Immediate提交线程通知、Batch按批提交、LatestOnly有界合并和Reliable显式Ack，不另造消息中心；[`Windows变量专项#40470851537：Success`](https://github.com/oudream/cx-visual-x3/actions/runs/40470851537)。
3. [`3094ebe`](https://github.com/oudream/cx-visual-x3/commit/3094ebeede38b423e6700d1bfaa8a4cc7b8fd2f4)：变量定义支持`hasDefaultValue=true`且`defaultValue=null`，区分显式null和未指定默认值，贯通`MDSVariable<T>`快照和恢复；对应Windows CI尚需按固定SHA读取结论。
4. 全类清单**仍不是全部生产验收完成**：MEM工程变量编译/编辑、Provider真实设备读写/断连恢复、可靠队列的持久重放与ExactlyOnce边界、复杂对象适配、长稳p99/实机测试仍未关闭。继续按VAR-G01～G06逐项验证，不以类名数量作为完工判断。
