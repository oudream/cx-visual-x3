# CxVisual 统一功能模块体系——Module Center P0共享入口源码实施与Windows验收台账 V1.0

> 2026-10-28；**部分P0已落地，G1分项验收，G2/G3均未宣称完成。**
> 设计基线：[功能模块中心与三入口统一体验完整设计及验收 V1.0](../Architecture/CxVisual_统一功能模块体系——功能模块中心与三入口统一体验完整设计及验收_V1.0.md)。
> 五类模块仍为 Connector、Driver、Compute、Simulation、Service；本中心不是第六类模块或新Owner。

## 1. 真实源码与Git提交

| SHA | 修改 | 事实边界 |
|---|---|---|
| [00a99df](https://github.com/oudream/cx-visual-x3/commit/00a99dfb3feeecc2c2761aaaa7b8bda4a8b4258d) | 五类专业配置分页抽取为公共MPWModuleCenterPages；增加五类正式FCM Schema测试 | 不改变FCM/MHS唯一真源 |
| [35ff9ec](https://github.com/oudream/cx-visual-x3/commit/35ff9ec0a86229b524ea192369b0f2cb06aff135) | MPWModuleCenterControl共享WPF，TestCenter复用；Studio新增顶部按钮、AvalonDock doc.modules和STA检查 | Studio现有MOC源依旧为CPU演示SimulationResolver，不是工业Provider目录 |
| [0bc54a0](https://github.com/oudream/cx-visual-x3/commit/0bc54a05d6c18a1a441fdb8c0166b5956e3fe6ec) | TestCenter默认全宽正式配置布局；增加Connector UIA错误诊断 | 该布局修复自身并未修复Schema字段全隐藏 |
| [c3f6146](https://github.com/oudream/cx-visual-x3/commit/c3f614675b10cc51d2ef6f81ffb6215798eed571) | 全部分类、搜索与重置、FCM CapabilityIds声明只读投影、Studio负例 | Declared≠Resolved≠Available≠Authorized |
| [eb95505](https://github.com/oudream/cx-visual-x3/commit/eb95505fd1f1a8ddaefa7f2f00cf7090ce0c37ad) | 恢复MPWModuleCenterPage.Categories=null语义，五类分页测试 | null表示不按FCM注解类别过滤，空数组会隐藏所有字段 |
| [6d0767a](https://github.com/oudream/cx-visual-x3/commit/6d0767afd68e995ea3ac5280a3ea81efd8d8798d) | TestCenter显示/收起当前模块已声明能力 | 默认全宽编辑，能力只读，不触发设备执行 |

## 2. 真实Owner与公开入口

- Mechanism/Presentation.Wpf/Modules/MPWModuleCenterPages.cs：五类的Schema分页与未覆盖字段扩展页，仅视图分组。
- Mechanism/Presentation.Wpf/Modules/MPWModuleCenterControl.cs：通用WPF布局、五类/全部筛选、搜索、能力只读详情、专业Tab宿主、TestCenter全宽开关；没有私有能力注册中心。
- Tests/TestCenter/Manual/Module/TCTManualModuleFrame.cs：仍为正式MHS配置实例、草稿保护、Validate/Save/Apply/Reopen、FCM验证和TestCenter操作入口；MPW公共控件只提供视图。
- Applications/Studio/StudioShell.ModuleCenter.cs、StudioShell.cs、MainWindow.xaml：从顶部固定按钮或Ribbon进入同一个AvalonDock功能模块中心；当前MOC目录为SimulationResolver示例，只用于演示查询，不冒充实际五类能力支持。
- Applications/Studio/StudioShellSelfTests.cs、Tests/TestCenter/Automated/Cases/TCTModuleConfigurationCases.cs：Studio WPF自测、类别/搜索正反例与五类FCM Schema、Categories=null语义回归。
- Module Descriptor/CapabilityIds归MFM；MOC Catalog/Resolver归现有Owner；FCM/MHS唯一配置；MEM唯一工程身份；MRK唯一执行；M09/MDS、M36、M12各自管理变量/数据、消息、命令。不得新建并行ModuleManager、RuntimeHost、DataHeap。

## 3. Windows CI的固定SHA实测证据

### 提交35ff9ec

- [Studio S1 Windows Shell #40643903978](https://github.com/oudream/cx-visual-x3/actions/runs/40643903978)：**成功**；Studio真正打开共用MPW中心。
- [TestCenter Windows #40643903913](https://github.com/oudream/cx-visual-x3/actions/runs/40643903913)：**失败**，Connector独立UIA找不到正式Channel Schema卡片；解决方案构建步骤成功。

### 提交0bc54a0

- [TestCenter Windows #40644324616](https://github.com/oudream/cx-visual-x3/actions/runs/40644324616)：**失败**；诊断 Page=channels、VisibleEditors=0、HasConfig=True、AutomationCards=False，证明分页和配置Section存在，但由于新共享分页把原Categories=null错误换成空数组，MPW过滤掉所有字段。

### 提交eb95505（Categories语义修复后）

- [TestCenter Windows #40644682991](https://github.com/oudream/cx-visual-x3/actions/runs/40644682991)：GitHub Actions Job Step实际确认 **Build all solution projects 成功**、**Verify isolated Connector UIA PointTable and DataMapping 成功**、**Verify timeout and abnormal Worker exit fail closed 成功**。记录编写时其后续Worker/NUnit仍在运行，不能宣称整个Workflow通过。
- [Studio S1 Windows Shell #40644683008](https://github.com/oudream/cx-visual-x3/actions/runs/40644683008)：**成功**，覆盖MOC示例目录、全部/Driver空结果、搜索缺失/恢复、重复Dock Tab、工程Revision不变。
- [配置公共底座独立编译 #40644683285](https://github.com/oudream/cx-visual-x3/actions/runs/40644683285)：**成功**。
- [Graphics Editor #40644683060](https://github.com/oudream/cx-visual-x3/actions/runs/40644683060)：**成功**。
- [TestCenter Command Workspace #40644683042](https://github.com/oudream/cx-visual-x3/actions/runs/40644683042)：**成功**。
- [Config L1 #40644682953](https://github.com/oudream/cx-visual-x3/actions/runs/40644682953)、[Engineering Organization #40644683240](https://github.com/oudream/cx-visual-x3/actions/runs/40644683240)：**失败**；邻近提交日志曾报告ArchitectureSmoke运行时数据MDS FlowHeap/DataStack 100字节预算案例失败。需按固定SHA/Job继续核实，不作为模块中心专项通过。

### 提交6d0767a

- TestCenter能力目录展开/收起新增交互已经写入源码，但本次尚未取到相应SHA的Windows UIA单项通过证据；不应宣称UIA验收完成。

## 4. 下一轮未完成工作（严格区分目标与实现）

| 阶段 | 目标闭环 | 通过证据 |
|---|---|---|
| P0 | Studio从正式五类Provider/工程Scope发现能力；区别Declared、Supported、Available、Authorized；配置与MOC/MRK一致 | 真实Provider/Resolver、无支持/无权限负例、Studio和TCT双端Windows STA/UIA |
| P1 | Connector实际收发、Driver厂商SDK设备、Compute CPU数值/图像、Simulation Step、Service本地Request/Health各完成真实能力 | 每类实际业务输出、异常、资源回收和可复现报告 |
| P2 | 四大公共交互Produces/Handles、能力/流程装配及Operation/Trace追踪 | M09/MDS/M36/M12和MOC/MRK同Owner跨模块端到端测试 |
| P3 | Studio正式MEM模块资源、配置/流程/MOL/专业Viewer与诊断完全贯通 | 用户完整工作流及持久化/再打开/UIA |
| P4 | 并发背压、重连、故障恢复、实机HIL、性能、安全与工业长稳 | G2/G3实际现场日志，不能用Mock和编译取代 |

结论：**共享WPF中心、五类Schema配置、Studio入口、TestCenter原MHS操作和只读能力发现已阶段性落地并取得部分G1证据；真实五类Provider、工业动作、授权和正式工程闭环未完成。**

## 5. P0 下一轮：MFM/MOC能力关联与首个真实Compute专业软件Provider（2026-10-28）

本节是对既有 V1.0 台账的**追加实现记录**，保留所有历史提交与固定 SHA 证据，不将新目标提前写为通过。

| SHA | 真实源码与功能 | 当前验收级别 |
|---|---|---|
| [ecbce6c](https://github.com/oudream/cx-visual-x3/commit/ecbce6ce1684ef798a1741716eccf4bc5cd8ca3f) | 在**现有** `MOCCapabilityCatalog` 增加 `DescribeModules(IEnumerable<MFMModuleDescriptor>)`：按模块身份与能力ID只读关联，保留缺失描述的声明，区分 `Declared/Described/RuntimeResolved`；重复模块ID及空模块拒绝 | 源码已提交，Windows专项结果待依照完整集成提交核实 |
| [a41c702](https://github.com/oudream/cx-visual-x3/commit/a41c70290483129c4fde42ac4c4f10ba1e2536c5) | ArchitectureSmoke新增MFM/MOC模块关联、能力不存在、只有声明未解析、多模块交叉声明与错误模块负例 | 测试源码已提交；不等同于运行通过 |
| [8bc63b9](https://github.com/oudream/cx-visual-x3/commit/8bc63b94df30bdf91790a702be77feace307d2e6)、[cbc9b2e](https://github.com/oudream/cx-visual-x3/commit/cbc9b2eeaf3910ef496beb1e67fad30e929763a8) | 已有 `CxVisionThresholdProvider` 实现原 `IMOCCapabilityProvider`，发表 `RawPixels(byte[])`、`Width(int)`、`Height(int)` 正式输入端口，复用原 `TryResolve` 的真实CPU阈值算法；仅为Professional.Vision新增指向**既有**Mechanism.Orchestration程序集的单向项目引用 | 部分中间 SHA CI失败，不能拆开当成完整编译成品；后续主线验证 |
| [eaf7811](https://github.com/oudream/cx-visual-x3/commit/eaf78114fe16c9f0cb13b972e0dca54d23136473) | MPW结果增 `ModuleId`、UI可见项快照并标识未归属模块；不保存第二能力目录 | 代码待整体验收 |
| [c7511d2](https://github.com/oudream/cx-visual-x3/commit/c7511d2eab29a9620391b74ab78cf3613bb57567) | Studio组合**真实CPU软件算法** `CxVisionThresholdProvider` 与原 `SimulationResolver`，用原MOC `DescribeModules` 关联 `Compute` 示例模块身份；未绑定的演示AI、采图、输出能力仍保持“未归属” | **开发预设参数**，不是已持久化MEM模块、MHS Effective配置或设备就绪 |
| [6fb86d2](https://github.com/oudream/cx-visual-x3/commit/6fb86d236ef0c4c20b5074e20dbb5a4438d2dad1)、[b0c010f](https://github.com/oudream/cx-visual-x3/commit/b0c010f60f2b09950165b2ad5a4061859f0c083d) | Studio WPF验证Compute/Driver/Simulation分类及演示隔离，G06真实CPU像素执行测试前核验专业MOC端口与MFM能力关联 | Windows CI尚需固定最终SHA的实际运行结果 |
| [bced4fd](https://github.com/oudream/cx-visual-x3/commit/bced4fd1f3b57857adb5efe5c0861e8899873495) | Studio中心新增“打开工程专业配置”“打开主流程编排”按钮，仍调用原 `doc.config` / `doc.flow`，不伪造选中能力自动绑定 | 已提交，WPF启动/交互待对应SHA验证 |

**唯一Owner不变**：MFM管理模块描述，MOC管理能力索引与编译，MRK执行，Professional.Vision执行真实算法，FCM/MHS持久配置，MEM持久工程定义，MPW只投影UI。模块中心**没有**创建新的ModuleRegistry、工作队列或运行执行器。

**实际等级说明**：

- 已写入源码：首个真实CPU专业Compute软件算法可在Studio中心被发现，MFM/MOC模块声明关联，保留演示能力不归属的真实边界；已有G06曾用真实像素检查该算法，但新增发现/Studio综合回归仍须以对应新 SHA Windows CI为准。
- 尚未做：把开发预设升级为正式工程MEM资源 + MHS Saved/Effective快照 + MOC构建 + MRK授权结果；真实Driver SDK/HIL，Simulation/Service/Connector全面Provider动态注册与工业运维。
- `RuntimeResolved` 只表示 `IMRKCapabilityResolver` 找到执行者，**不等于** `Supported/Available/Authorized` 四态的其他条件满足；严禁显示物理动作成功。
- 并行开发的中间提交在ArchitectureSmoke缺失`System.Net.Http`引用时报过编译错误；后续主线已补引用，必须按最终固定SHA复验，不得把中间CI失败隐去，也不得把它当作专业算法本身失败。

下一步应使用正式MHS `Effective` 版本加载CPU Provider，并通过真实MEM模块/资源绑定在MOC唯一能力目录形成可用性、依赖与授权证据；先完成一项端到端再逐类推广。

## 6. P0专项收口与最新Windows证据（2026-10-28）

- [bced4fd Studio Windows S1 #40648271048](https://github.com/oudream/cx-visual-x3/actions/runs/40648271048)：**成功**，包含真正软件Compute能力查询和Studio快捷按钮源码。
- [6069083 Studio Windows S1 #40648562112](https://github.com/oudream/cx-visual-x3/actions/runs/40648562112)：**成功**，增加对配置与流程快捷按钮的WPF实际Click导航与原Owner文档复用验证。
- [6069083](https://github.com/oudream/cx-visual-x3/commit/60690832891d15e319bb60b81e7bb552f0ef6cae)：为Studio中心的`OpenConfiguration/OpenFlow`增加真实事件及原`doc.config/doc.flow`的STA断言，不自动绑定MFM能力到MEM。
- [d36262a](https://github.com/oudream/cx-visual-x3/commit/d36262af71db765faa0e49e1fa70bf7f5d560fa3)：TestCenter MHS保存成功才刷新本模块CapabilityIds只读目录；失败不改变可见能力。
- [c865020](https://github.com/oudream/cx-visual-x3/commit/c865020ae6ce8e2da47069b28daa63cfe963c654)：MPW能力详情明确`Declared / Described / RuntimeResolved`的证据及来源；`Supported/Available/Authorized`仍为目标Owner待核。
- [0d10e26](https://github.com/oudream/cx-visual-x3/commit/0d10e268e7eb507e57406a70d21621a06115db67)：TestCenter按FCM草稿`Describe()`获取当前正式MFM模块Id/版本；若没有查询MOC则明示“未核实”，不把未知当不支持。
- [f399ba0](https://github.com/oudream/cx-visual-x3/commit/f399ba06e8708b1c7bb5e2c3d510ae7fba1791e3)：模块名为空、能力ID重复等非法编辑草稿不向MPW返回假模块，也不让界面冒出未处理异常；原FCM验证仍为最终权威。

本节最后四项状态投影与异常保护变更在编写时**尚未取得对应SHA的全部Windows UIA通过结果**，不能与前述Studio S1通过混同。另有并行代码曾使ArchitectureSmoke于`System.Net.Http`程序集引用缺失时编译失败，当前主线已补引用，但该独立问题的CI仍按固定SHA验收。

## 7. P0 MHS Effective与真实MOC输入输出（2026-10-28）

本节追加实际源码及证据，不改变六份主规格。**P0软件进展不等于五类真实Provider全部验收或工业就绪。**

| 提交 | 已落地范围 | 限制 |
|---|---|---|
| [a36ecc2](https://github.com/oudream/cx-visual-x3/commit/a36ecc2a55625ad6d5ea231ca59efe00f72d7845)、[be7ad21](https://github.com/oudream/cx-visual-x3/commit/be7ad2130fb25e32fe1346f364ff2d853456f658) | CxVisionThresholdProvider增加MHS AuthorizedSection读取Effective快照与后续版本/权限再核；修正真实FCMConfigRevision.Effective成员名 | 不将Saved冒充Effective；撤权禁止新的MOC版本发布，不替代硬件安全 |
| [a55f432](https://github.com/oudream/cx-visual-x3/commit/a55f43243327b4f98ecb74c35642657f7cd54cb0) | G06新增无Read拒绝、授权后版本一致、撤权后拒绝旧Provider发布的测试 | 测试源码存在不代表对应CI已通过 |
| [b56e33b](https://github.com/oudream/cx-visual-x3/commit/b56e33b43cd9cd93b6764aeca1ba74fcb52ddeca)、[14f0af8](https://github.com/oudream/cx-visual-x3/commit/14f0af81e7325c9316234f6628644cac858f6e97) | Studio模块中心增加显式读取当前MHS Effective入口，沿用原配置Scope及IAM权限路径，只更新原MOC能力投影 | 不自动Apply/启动设备/写入MEM模块资源 |
| [9b26126](https://github.com/oudream/cx-visual-x3/commit/9b261261ee18e4279f492e43da97c6ee1cb3b5e3) | WPF验证实际点击后获取Effective版本、Compute仍可发现 | 中间SHA曾因不存在的EffectiveVersion成员编译失败，后续已修正 |
| [6487bfd](https://github.com/oudream/cx-visual-x3/commit/6487bfdd49130d2a534d7a3ec7f91cd6ba40e3a6)、[94a9ada](https://github.com/oudream/cx-visual-x3/commit/94a9adad729006e6d8a11f836d8101e6f2c3d073)、[2ff9325](https://github.com/oudream/cx-visual-x3/commit/2ff9325c3d5fccb3c37ffa31d918b2a5bdcd2caa) | MPW从真实MOCCapabilityDescriptor.Ports展示RawPixels byte[]、Width int、Height int等强类型输入/必填字段，Studio添加WPF断言 | 没有正式定义的输出不生成虚假输出端口 |
| [3412cb1](https://github.com/oudream/cx-visual-x3/commit/3412cb15c763bc93ae184e49774c1e887486dba1)、[f44b663](https://github.com/oudream/cx-visual-x3/commit/f44b663e5ca9c2a4a662839f3c61225032901e8c) | MHS Service和AuthorizedSession增加OpenExisting，只读缺失配置时拒绝创建默认Section，损坏元数据拒绝，授权先检查Read | 原Open创建/编辑语义不改，仍是唯一MHS配置Owner |
| [5985ece](https://github.com/oudream/cx-visual-x3/commit/5985ecec0e08454208421c71eb29cb519d041347)、[b7e00c4](https://github.com/oudream/cx-visual-x3/commit/b7e00c42f0023797a258893aba73abc2ec62a6bf) | Studio改用不隐式创建配置的OpenExisting；G06增加缺失时不创建目录的负例 | 尚未取得相应完整Windows CI绿灯时只能列为源码已提交 |

### 固定SHA实际Windows状态

- [Studio S1 #40651853606](https://github.com/oudream/cx-visual-x3/actions/runs/40651853606)（14f0af8）：完整编译通过、S1-03C模块中心**PASS**，但后续配置组合响应式1/2/3列布局断言**失败**，不能宣称整个S1成功。
- [配置公共底座 #40651843551](https://github.com/oudream/cx-visual-x3/actions/runs/40651843551)（14f0af8）：**Success**。
- 中间提交a36ecc2曾在编译中因不存在的FCMConfigRevision.EffectiveVersion属性报错，已修为Effective。
- 最后新增OpenExisting及其负例、MOC端口投影和WPF测试在本节写入时仍缺固定最终SHA全部Windows成功证据。

### 继续开发的明确缺口

现阶段已能真实发现Compute软件Provider、显式读取已存在的MHS Effective、按MOC展示强类型端口；**尚未**完成正式MEM模块资源关联、工程数据输入到MOC/MRK执行/Trace的Studio UI闭环，亦未完成其它四类模块真实Provider与G2/G3实机工业验收。不得把RuntimeResolved当作Supported/Available/Authorized。

## 8. P0受控添加到现有MEM流程（2026-10-28）

| 提交 | 功能及Owner边界 |
|---|---|
| [5e391ff](https://github.com/oudream/cx-visual-x3/commit/5e391ff9b491787e171e6e3bc8551bdccb650707) | 公共MPW只读提供选中的能力对象，不维护模块执行队列或工程状态 |
| [fc2564f](https://github.com/oudream/cx-visual-x3/commit/fc2564fd2ce609b6615dcf880c3692c69b1a0481) | Studio增加“加入当前主流程”；先选择真正的Compute能力并加载MHS Effective，操作前重新检查Provider版本/权限，通过原MOCFlowBuilder.Edit写入当前MEM主Content。无能力选择、演示Resolver、流程运行中、版本失效均拒绝。只插入未连线节点，尚不可宣称执行 |
| [b1717cd](https://github.com/oudream/cx-visual-x3/commit/b1717cd169e085d471760bc4f3fd73ff58ba7080) | Studio WPF新增未选中能力点击时拒绝的负例，MEM工程Revision不应改变；现有G06用原MOC Builder和真实CPU像素执行验证软件侧正常链 |

**验收边界**：当前能安全添加待连接的Compute能力引用，**不是**整套工程资源绑定、数据映射、MOC成功编译和MRK授权执行。该入口真实WPF最终SHA CI仍处于排队/待验，禁止声称已通过。跨进程配置Effective刷新及已捕获Provider版本失效的实时一致性仍需独立验证。

此前 [Studio S1 #40652167269](https://github.com/oudream/cx-visual-x3/actions/runs/40652167269)（5985ece）中模块中心专项S1-03C为PASS，但整个S1后续因统一配置组合1/2/3列响应式布局断言失败，需由对应配置UI Owner处理；不能把该流程的总体Failure误报为Success，也不能凭它认定本轮最新加入流程操作失败。

### G06本轮固定Windows软件回归证据

- [Windows Runtime TDD #40652205605](https://github.com/oudream/cx-visual-x3/actions/runs/40652205605) 对应 `b7e00c4`：**Success**；日志确认 Windows x64 全解构建、ArchitectureSmoke、并发编排、运行Host发布/恢复全部通过；包括本轮G06受权Effective、MOC/MRK真实CPU软件执行与缺失配置不初始化负例。此证据仍不能替代Studio新AddToFlow的WPF UIA或HIL。

## 10. 本轮Studio真实UI专项验收（固定SHA）

- [Studio Windows S1 #40652752253](https://github.com/oudream/cx-visual-x3/actions/runs/40652752253)（提交 b1717cd）：Windows构建已完成，`S1-03C Studio模块中心与TestCenter共享MPW组件` **PASS**，其中本轮包含真实MHS Effective显式读取、端口展示和新增MOL入口未选中时拒绝且不修改MEM Revision的STA测试。**整条Workflow仍Failure**：之后统一配置组合响应式1/2/3列容器断言失败。不得把本条写为整体成功。
- [Studio Windows S1 #40652707576](https://github.com/oudream/cx-visual-x3/actions/runs/40652707576)（提交 fc2564f）：Module Center S1-03C **PASS**，整条仍在同一配置布局断言失败。
- 后续TestCenter/Studio最新主线CI、物理设备HIL和跨进程Effective版本一致性仍需单独证据。完整G06软件侧Windows Runtime TDD成功证据仍以 #40652205605 固定SHA为准。

## 11. 第一条可直接操作的RAW CPU软件闭环（2026-10-28）

本轮用户要求直接到闭环，因此在已有ModuleCenter基础上完成了**可交互的文件输入→正式MHS Effective→MEM工程→MOC强类型构建与发布→MRK执行→专业真实结果/Trace**。软件算法仍使用原Professional.Vision CPU阈值/四邻域检测。此闭环不等于五大模块的工业全量闭环。

| 提交 | 本轮实际交付 |
|---|---|
| [fd79764](https://github.com/oudream/cx-visual-x3/commit/fd7976423dcecd4852d7ab0459001a7da12327d6) | 专业Provider只读公开Effective PixelDepth，给RAW大小及位深核验使用 |
| [8a52219](https://github.com/oudream/cx-visual-x3/commit/8a522198c1236349dd80480cd9af6cc094e61b39) | Studio中心新增选择本机RAW、输入宽高、取消检测；只在已有MHS Effective可读时运行，真实文件大小/位深验证后创建独立MEM检测页。CPU原始像素数据只存在本次运行内存，临时输入Provider声明byte[]端口，MOC Builder创建数据/控制连线，原MOCProjectRelease与MRK授权Publisher执行，获取缺陷及Trace |
| [14dd1d5](https://github.com/oudream/cx-visual-x3/commit/14dd1d50310aabc25cf65ec064682989085e7b2e) | S1-03D采用真实磁盘5×3 RAW字节，测试有效MHS配置、MOC/MRK发布执行、阈值像素4、缺陷区域面积3、配置Revision、指纹、FlowId、完成Trace；故意错误宽高要求拒绝且不更改MEM |
| [7fa8d6c](https://github.com/oudream/cx-visual-x3/commit/7fa8d6c996357ee82024a3086ac040da48223a08)、[54cf9fe](https://github.com/oudream/cx-visual-x3/commit/54cf9feaacdc5da666d6bc4f67936fff86731ee7) | 修复界面新增结果区后旧S1测试依赖最后TextBlock的脆弱选择器，改成明确Effective状态AutomationId |
| [45886d6](https://github.com/oudream/cx-visual-x3/commit/45886d605e77e1158801778571f4d689ea1ad47c)、[e944f7a](https://github.com/oudream/cx-visual-x3/commit/e944f7ac8a578ecfad013a428d8fa1a0ad5e2133) | Studio保留模块中心结果页面显示缺陷明细表，包含面积/范围，并展示从MOC SourceMap读取的实际MRK步骤状态、序号、时间和工程BlockId |
| [75292b1](https://github.com/oudream/cx-visual-x3/commit/75292b176422d5b44a654ce52e7f7999e14a01cd) | 修复新增自测局部report变量与外层Run参数重名导致CS0136的中间编译失败 |
| [d610153](https://github.com/oudream/cx-visual-x3/commit/d610153c49a984ab117972844b82327b2afc9e4e)、[f20c1a0](https://github.com/oudream/cx-visual-x3/commit/f20c1a097b646d1661a1e83fa9f99c727133805b)、[7c7fd72](https://github.com/oudream/cx-visual-x3/commit/7c7fd72464ba63314271249bf876ca19b1542a15) | 同一真实S1-03D逻辑可单独由`--module-center-cpu-self-test`执行，增加独立Windows CI，可在Studio其他界面布局回归故障时独立证明模块闭环 |

### 固定 SHA 的真正 Windows 软件证据

- [Studio S1 #40658778238](https://github.com/oudream/cx-visual-x3/actions/runs/40658778238)（75292b1）日志明确 `PASS: S1-03C` 与 `PASS: S1-03D RAW真实文件→MEM/MOC/MRK→缺陷结果及Trace闭环`。整个S1工作流最终仍因无关的配置组合响应式布局断言失败。
- [Studio S1 #40658954015](https://github.com/oudream/cx-visual-x3/actions/runs/40658954015)（e944f7a）再次明确 `S1-03C`、`S1-03D` PASS；整体S1仍同一布局断言失败。表示模块中心闭环在这两个SHA运行真实成功，但**不意味着整套Studio全绿**。
- [独立ModuleCenter CPU RAW Windows #40659419791](https://github.com/oudream/cx-visual-x3/actions/runs/40659419791)：编写本台账时处于运行中，最终结论必须再核对Actions和上传的StudioShellSelfTest报告。

### 使用方法与安全边界

在Studio打开功能模块中心，先通过正式工程专业配置保存/生效Vision参数，点击“读取当前生效配置”，填写真实RAW图像宽高，再点击“选择RAW并执行CPU检测”。结束后查看缺陷像素/区域/坐标、配置版本、MRK FlowId、MOC指纹、Trace时间线；如需查看流程，点原主流程编排。文件字节数必须等于宽×高×有效位深字节，像素最多32,000,000。

这条流程以用户明确操作创建独立MEM检测页，只有当前内存的RAW像素源；**保存工程后再次打开并不能自动重新采集原图**，需要后续增加MEM Asset/数据引用与重绑定。当前授权来自本机仅CPU软件发布策略，不能替代生产IAM或设备联锁；无Driver实机HIL、跨进程外部数据一致性、整套五类模块专业验收或长期稳定性结论。

## 12. 独立CPU真实软件闭环Windows正式验收（2026-10-28）

- [首次独立CPU验收 #40659419791](https://github.com/oudream/cx-visual-x3/actions/runs/40659419791)：**Failure**，Windows完整编译成功，但专用WPF测试进程未按时退出（120秒超时），不能算验收通过。
- [测试进程终止修复 d1a7285](https://github.com/oudream/cx-visual-x3/commit/d1a72857a63a8062ac936d599ef4ec313b407b7b)：专用CLI模式在完整结果报告落盘后显式退出独立Windows进程，普通Studio与原全S1退出机制不变。
- [独立CPU验收 #40659966291](https://github.com/oudream/cx-visual-x3/actions/runs/40659966291)：**Success**，完整.NET Framework 4.8 Windows解决方案编译与真实RAW磁盘数据→MHS Effective→MEM独立检测页→MOC软件发布→MRK执行→像素阈值/连通域缺陷结果/SourceMap Trace专项测试完成，报告作为Actions artifact上传。
- [Scope保护修复 dad1957](https://github.com/oudream/cx-visual-x3/commit/dad195717962e36552298d5ef2b9938cd6422b00)：用户更换MEM工程/FCM配置Scope或重新读取Effective失败时，失效旧绑定，禁止旧Provider用于新工程加入流程或RAW运行。
- [包含Scope保护的独立CPU验收 #40660064092](https://github.com/oudream/cx-visual-x3/actions/runs/40660064092)：**Success**，Windows完整构建与相同的真实CPU软件闭环测试均成功。

**判定**：第一条可操作的、数据来自真实文件的Compute CPU软件闭环已有两次专用Windows绿色证据。不是完整五模块工业功能交付：RAW字节当前为运行期临时输入，MEM页持久化不等于源数据/执行结果可自动重放；真实设备HIL、业务授权、全Studio S1配置响应式布局故障以及其他四类Provider仍需分别验收。

## 13. TestCenter Compute 真实UI操作闭环（2026-10-28）

> 本节严格区分：正式源码已落地 / 已被Windows编译 / 真正UIA成功 / 工业HIL。此前Studio S1-03D后台软件测试通过，**不等于**TestCenter GUI验收。

### 真实变更

- [共享专业CPU执行入口 141b36d](https://github.com/oudream/cx-visual-x3/commit/141b36da14b96fff4f41f2302102f93cfc70d2d8)：`Professional/Vision/CxVisionRawCpuRunner.cs` 实现唯一RAW文件→真实MHS Effective Provider→MEM项目→MOC准备/版本发布→MRK执行→缺陷区域与SourceMap Trace的软件闭环。Studio已改为委托此类，原重复执行逻辑不再保留。仅本地CPU，不覆盖工业授权和HIL。
- [TestCenter人工Compute页 7f76d43](https://github.com/oudream/cx-visual-x3/commit/7f76d4322a074f94ca8f5409a31458d949fc92d1)：`Tests/TestCenter/Manual/Module/TCTCpuComputePanel.cs` 直接嵌入五类共用模块中心。正式Compute配置要求ProviderTypeId、Device=CPU与CapabilityIds在MHS Effective一致；Vision参数显式保存/Apply，读取只用MHS OpenExisting；RAW路径、宽高、位深、运行/取消/重试、区域表和Trace表完整可操作。
- [共用分页接入 b175ba0](https://github.com/oudream/cx-visual-x3/commit/b175ba0d9c2d347eda21926057142de5fadac9b7)：保留五类统一FCM配置页，Compute新增`cpu-test`专业Tab；原配置编辑器/草稿保护不被替换，切换页面不创建第二配置Owner。
- [真实WPF UIA b5bd108](https://github.com/oudream/cx-visual-x3/commit/b5bd1083dc4b242cf12c44546e75c45069543f0b)：独立临时Scope、磁盘5x3 RAW数据；通过InvokePattern/ValuePattern/SelectionItemPattern操作真实按钮与输入，检查首次无配置读取拒绝、保存生效、实际阈值像素4、区域面积3、Trace行、错误宽度拒绝和第二次独立MRK FlowId。
- [TestCenter 专项Windows CI 3e836df](https://github.com/oudream/cx-visual-x3/commit/3e836dfddabcdf73e54bd9d8e406b2c6b9613864)：新增`--module-cpu-ui-automation`独立验收入口与报告归档；定向构建TestCenter及依赖，保留全量TestCenter Windows验收。
- [MHS Existing API修复 e770c14](https://github.com/oudream/cx-visual-x3/commit/e770c148c8a49035ae319008c930e73d573a93c8)：只读Existing调用归属`MHSConfigService`、显式FCMConfigIdentity，不绕过缺失配置拒绝。
- [Channel编译阻断兼容修复 a870a47](https://github.com/oudream/cx-visual-x3/commit/a870a4720ac4f71ceeec728eecd41920e7d414d2)：补回多通道已使用的`ConfigureSendAdmission`，仅按正式发送预算设置已有计数器。并行Channel独立功能/长稳结论仍需其Owner验证。

### 当前证据与未完成

- 历史Windows日志在早期提交因MHS OpenExisting错误、并行Channel缺失ConfigureSendAdmission而**编译失败**；已对应修复，但不得将修复源码当作重新运行成功。
- [专门TestCenter Compute UIA工作流](https://github.com/oudream/cx-visual-x3/actions/workflows/testcenter-compute-cpu-ui-windows.yml) 已建立，当前最终绿色结果以它实际Run的`TestCenter-Cpu-UIA-Report.txt`为准。**本段写入时尚未核实最终PASS**。
- 本阶段即使CPU真实UIA通过，仍为本地软件G1能力：尚未完成Studio/TestCenter全五类专业G1、真实Driver HIL、长期运行、RAW工程持久化自动重放、跨进程资源租约及生产授权。

### 人工操作

TestCenter → 专有测试 → 模块测试 → Compute；建立正式Compute配置并将ProviderTypeId和CapabilityIds设置为`vision.cpu.threshold.inspect`、Device=CPU，保存/生效。选择“CPU真实检测”Tab，填RAW文件与参数，显式保存Vision参数、读取Effective、点击执行；检查真实区域/Trace，随后重复执行或提供坏输入以检查错误与拒绝。不得以ACK、目录声明、保存配置或演示输出代替真实执行。

## 14. TestCenter Compute真实WPF界面闭环G1专项验收（2026-10-28）

**第一条完整的人工可操作Compute软件测试链已通过实际 Windows UI Automation，不再是“仅源码/待UIA”。**

- [TestCenter Compute CPU Real WPF UI #40666584107](https://github.com/oudream/cx-visual-x3/actions/runs/40666584107)（固定提交 `50843e58593c537a630c0c4b542b887752624134`）：**Success**。Windows .NET Framework 4.8 TestCenter及真实依赖编译通过，独立WPF窗口通过InvokePattern/ValuePattern/SelectionItemPattern操作正式Compute模块、保存/读取MHS Effective、真实5×3 RAW输入，调用统一Professional.Vision → MEM → MOC → MRK，产出像素/缺陷区域和SourceMap Trace。错误宽度拒绝、重复运行得到不同FlowId；报告归档。
- [TestCenter Compute CPU Real WPF UI #40667201069](https://github.com/oudream/cx-visual-x3/actions/runs/40667201069)（固定提交 `1535a2ec49cb96662ce5b5f24e9bf1668bb5ffc7`）：**Success**。包含[正式Compute约束修订](https://github.com/oudream/cx-visual-x3/commit/1535a2ec49cb96662ce5b5f24e9bf1668bb5ffc7)：运行前检查模块Enabled、真实Provider/CapabilityIds、固定Compute Effective版本、MemoryBudgetMb与TimeoutMs。UIA日志再次报告阈值像素4、区域1、Effective config:2以及两个不同MRK FlowId。测试没有分别注入每一种限额/超时负例，不能宣称它们的各项专项异常验证均已完成。
- 人工测试入口：`TestCenter → 专有测试 → 模块测试 → Compute → CPU真实检测`。预先在正式Compute FCM配置设 `Device=Cpu`、`ProviderTypeId=vision.cpu.threshold.inspect`、`CapabilityIds`包含同一ID且启用，再保存/生效；在CPU Tab选RAW、调整参数、保存Vision/Apply、读取Effective、执行、查看区域表和Trace、取消/重试。
- 唯一Owner：MHS管理配置、MEM管理工程与Page、MOC编译及Stage、MRK执行及Trace、Professional.Vision计算、MPW/TestCenter只承载用户操作及结果投影；没有第二套数据/模块注册或执行引擎。
- **软件G1闭环已验证；不等于五模块全类、工业G2/G3 HIL、全部TestCenter软件回归或持久RAW数据/结果历史自动重放。** 本轮最新全量TestCenter Windows运行 [#40667201278](https://github.com/oudream/cx-visual-x3/actions/runs/40667201278) 在记录时仍执行中，不得预报全绿。真正Driver需要设备和安全HIL。

## 15. 同一固定SHA的完整TestCenter Windows验收通过

- [TestCenter Windows .NET48 #40667201278](https://github.com/oudream/cx-visual-x3/actions/runs/40667201278)，提交 `1535a2ec49cb96662ce5b5f24e9bf1668bb5ffc7`：**Success**。全解决方案Windows x64 Debug Rebuild、原TestCenter WPF界面/UIA、Connector PointTable/DataMapping、MDS生命周期、NUnit及Native Worker、故障/重复运行/历史报告和证据归档均返回成功。此Run覆盖了Compute测试面板及运行约束所在版本的完整TestCenter回归。
- 同一固定SHA [Compute独立真实WPF UIA #40667201069](https://github.com/oudream/cx-visual-x3/actions/runs/40667201069)：**Success**，报告明确包含不同MRK FlowId的两次真实RAW检测、阈值命中4像素、缺陷区域1和配置Effective版本；不是模拟执行或直接调用业务函数代替按钮的验证。
- **G1完成范围**：TestCenter Compute CPU软件人工可用的“配置→输入→执行→缺陷明细→Trace→重复/错误输入”闭环，且全量TestCenter原有回归未被破坏。禁止把这两次G1成功推导为Driver硬件HIL、其他四类模块专业执行、跨进程权限或工业G2/G3通过。

## 16. Simulation与Service专业G1真实界面闭环（2026-10-29）

### Simulation：PSM + MRK 真实单步

- [PSMAxisLinearModel `abe880d`](https://github.com/oudream/cx-visual-x3/commit/abe880d51ef7ea29a39998397cb247e4ac20525e) 在原 Professional.Simulation 增加确定性虚拟轴：模型状态、速度/限位、故障注入；仿真时间仍由正式 PSMSimulationClock 持有，不是物理运动轴驱动。
- [TestCenter UI `267eac1`](https://github.com/oudream/cx-visual-x3/commit/267eac1a914c986f176b8080af8a4d66a3f42987) 及 [统一模块分页 `ae22f75`](https://github.com/oudream/cx-visual-x3/commit/ae22f758313cab11a5a9d8047fba2bd93588b5af)：正式MHS Effective、PSMSimulationModule、初始化/Reset、MRKExecutionEngine按用户点击执行单步，显示模型位置/时钟、FlowId/记录；故障不得推进状态。
- [独立真实WPF UIA #40807801093](https://github.com/oudream/cx-visual-x3/actions/runs/40807801093)：**Success**，真实控件/正式FCM、单步两次并校验不同FlowId与位置/时间、故障阻止推进、Reset及重新初始化恢复。
- **使用路径**：TestCenter → 专有测试 → 模块测试 → Simulation → 虚拟轴MRK单步；正式FCM设 ModelId=axis、ProviderTypeId=simulation.axis.linear、CapabilityIds含simulation.axis.step、Mode=Step并生效。只支持显式Step；Realtime/Accelerated/Replay未在此作为已实现执行模式。

### Service：MLC正式生命周期 + 真实本地请求

- [本地服务 `b7372b7`](https://github.com/oudream/cx-visual-x3/commit/b7372b70c3b9bcdd799a07df4382c101724cda9c)、[统一模块页 `d150d3d`](https://github.com/oudream/cx-visual-x3/commit/d150d3d6718cb29ecfe6d0166e227d830d667c61) 使用原 MLCServiceModuleBase Start/Stop/Ready，由测试实现注册/卸载真实进程内请求处理器，按正式MHS Effective固定ProviderTypeId/CapabilityIds；最大并发额度复用正式配置。响应含真实大写转换、SHA256、Sequence与UTC，重复RequestId同内容幂等，变更内容拒绝。
- [服务停止/重启修复 `aabe580`](https://github.com/oudream/cx-visual-x3/commit/aabe5804493b7c393964a02b70e6ba6f72086ea8)：停止校验请求已结算、释放并发资源，允许重新Start重新注册，不假报Ready。
- [独立真实WPF UIA #40808529672](https://github.com/oudream/cx-visual-x3/actions/runs/40808529672)：**Success**，正式模块读取、Start Ready、响应/幂等/内容冲突、Stop后拒绝、Restart恢复及WPF结果表完整测试。
- **使用路径**：TestCenter → 专有测试 → 模块测试 → Service → 本地服务请求。FCM设ProviderTypeId=testcenter.service.local.echo，CapabilityIds含service.local.echo，Enabled=true，生效后装配、启动、提交本地请求、查看结果、停止、重新启动。明确这是**本机进程内G1**，并非对外HTTP/RPC、MES/ERP系统或生产鉴权验收。

### 不可跨越的Owner边界

这两类工作台均复用原MHS正式配置、MFM描述、PSM专业状态、MRK或MLC正式生命周期；TestCenter及MPW只负责人工意图与结果展示。无第二套ModuleCatalog、Host、Scheduler、MDS或流程编排器。虚拟设备与本地回显软件测试不作为工业HIL、物理联锁或可靠性长稳证据。

## 17. Connector通道规约软件测试工作台（待最终独立Windows验证）

- [真实Connector测试页 `e6f89e6`](https://github.com/oudream/cx-visual-x3/commit/e6f89e6748a9deeb64a896704ec879ba9f5a04a6)、[统一模块分页 `f7886c5`](https://github.com/oudream/cx-visual-x3/commit/f7886c57b5a34af7e0822c9c367ca97675f4a286)：从正式MHS Effective读取严格1 Channel/1 Protocol，装配原MCNConnectorModule、FTRVirtualChannel以及MPALengthFrameProtocol，实际发送CRC长度帧并从虚拟Peer解码回传；记录Session、完成次数、故意丢响应超时及Stop/Restart。
- [Connector UIA `aeea460`](https://github.com/oudream/cx-visual-x3/commit/aeea4608d4f3c627a04aef345e694bbfa355ed5b)：通过真实WPF Invoke/Value/Toggle控件验证未Start拒绝、成功回环、故障超时、恢复、Stop后的拒绝与重启。不以发送ACK代表真实设备业务完成。
- [独立Windows WPF UIA #40809446080](https://github.com/oudream/cx-visual-x3/actions/runs/40809446080)，固定提交 `a6532a4d5ad19079b2e40f8368f3db559eab034f`：**Success**，真正的TestCenter依赖编译和自动化控件操作、MCN/FTR/MPA长度帧回环、丢响应超时、停止后的发送拒绝及恢复重连全部通过；此处 Virtual Peer 不是实机HIL。
- **使用路径**：TestCenter → 专有测试 → 模块测试 → Connector → Virtual规约回环。正式FCM设置Channel=Virtual、Protocol=LengthFrame，保存/生效；运行仅为本机软件Peer测试，不支持Modbus TCP在Virtual Channel下伪装真实设备。PointTable/DataMapping可在原正式配置页编辑和检验，但本专项不宣称其运行数据Owner完整贯通。

## 18. 五类模块真实闭环状态与剩余不可替代项

| 模块 | 已验证的软件能力 | 尚未完成 |
|---|---|---|
| Compute | CPU RAW/MHS Effective/MEM/MOC/MRK、真实结果和Trace、WPF UIA | RAW/结果持久化自动重放、通用GPU/AI/CT等专业Provider全类、工业数据精度/性能 |
| Simulation | 虚拟轴Step经MRK推进，虚拟时间、故障拒绝/恢复，WPF UIA | 实时/加速/回放、多设备场景、确定性快照等全规格 |
| Service | MLC进程内生命周期、真实请求/响应/幂等，WPF UIA | HTTP/RPC部署、外部MES/ERP、业务认证及长期服务验收 |
| Connector | 正式MHS/MCN/FTR/MPA软件回环，WPF UIA独立Windows **Success** | 物理Channel/PointTable/MDS真实运行映射及现场HIL尚未验收 |
| Driver | 五类FCM/MFM配置、Driver接口声明可查 | **专业SDK实机控制与采集、设备完成语义、HIL、安全联锁和长稳**；无实机不能宣称达成 |

**工作原则**：不把“有页面、编译通过、Virtual回应或流程候选”误写成工业设备执行完成。G1专项与工业G2/G3必须分别取证。

## 19. Driver真实环境诊断与G2实机闭环前置门禁（2026-10-29）

本轮核查最新主线及GitHub Actions：Compute [#40811544416](https://github.com/oudream/cx-visual-x3/actions/runs/40811544416)、全量TestCenter [#40811544428](https://github.com/oudream/cx-visual-x3/actions/runs/40811544428)、Simulation [#40807801093](https://github.com/oudream/cx-visual-x3/actions/runs/40807801093)、Service [#40808529672](https://github.com/oudream/cx-visual-x3/actions/runs/40808529672)、Connector [#40809446080](https://github.com/oudream/cx-visual-x3/actions/runs/40809446080) 分别为Success。注意它们是固定SHA的软件测试，不等于全类工业能力完成。

### 本轮真实提交

- [Driver预检页面及项目清单 00affbc](https://github.com/oudream/cx-visual-x3/commit/00affbc3cbb36c259b15be7fdfd35febb25df7ae)：仅使用原MHS OpenExisting读取Driver正式Effective；查看配置Enabled/DriverTypeId/DeviceRef、SDK DLL文件版本以及Windows的实际SerialPort COM枚举。**DLL文件存在/COM端口被列出均不等于SDK装配成功或物理操作完成**。
- [共用模块中心分页 2c6db27](https://github.com/oudream/cx-visual-x3/commit/2c6db271e3100052e6b2a1fb345d04156d1f9371)：在原五类TestCenter模块配置工作台中新增Driver“设备与SDK真实环境预检”Tab，未造第二配置库/设备树/通道，也未向无Provider的环境发布DeviceReady。
- [Driver真实UIA负例 b92b8c6](https://github.com/oudream/cx-visual-x3/commit/b92b8c68b1e1beacde3140a341031bbb1e11c755)：通过真实WPF UIAutomation按钮、Tab及字段在独立临时Scope下检查缺配置/SDK/设备时拒绝、正式FCM/MHS保存生效后仍不显示Ready/PhysicalCompleted；G1证明的是安全拒绝而不是实机执行。
- [独立TestCenter CLI 4bc01c1](https://github.com/oudream/cx-visual-x3/commit/4bc01c170a66502484f7d13317795577763cba30)、[Windows工作流 aacf243](https://github.com/oudream/cx-visual-x3/commit/aacf243ea46aa4fdf6c17eb65698da64f1ed200d)：新增`--module-driver-readiness-ui-automation`与明确`HIL=NOT_RUN`的报告；[对应Windows Run #40812877023](https://github.com/oudream/cx-visual-x3/actions/runs/40812877023) 最终结果以Actions实际输出为准，记录时仍在运行。

### 不能越过的验收门槛

**Driver的真实工业闭环尚未完成。** 无特定设备型号、专业SDK许可/二进制、硬件连接、设备回读接口、操作安全授权与真实HIL，不能把Configuration/OS枚举/SDK文件存在或简单通信ACK判定为设备Ready/物理动作Completed。下一步的Driver专业Provider必须基于实际SDK和对应DeviceRef执行发现、打开、参数/状态Readback、操作/采集、完成确认、Stop/Dispose、失联恢复，再逐条取得TFD/DRV-A证据；危险运动/X-Ray操作还需真实硬件联锁、可控测试场与授权。这个G2/G3依赖不能由Windows云Runner或虚拟设备代替。

## 20. Driver只读预检Windows G1实证（2026-10-29）

- [初次Driver预检 UIA #40812877023](https://github.com/oudream/cx-visual-x3/actions/runs/40812877023)：**Failure**，WPF Button Padding 误用不可用的 `Thickness(double,double)` 构造，编译报 CS7036；[修复 c67b843](https://github.com/oudream/cx-visual-x3/commit/c67b843ca50e7a026f6c8974f6f4eb3b0deafcc0)。
- [第二次Driver预检 UIA #40813165814](https://github.com/oudream/cx-visual-x3/actions/runs/40813165814)：**Failure**，完整Windows编译成功，首次未配置拒绝正确；之后测试在旧WPF工作区外用另一个MHS服务保存并生效配置，旧实例配置缓存未刷新，导致等待正例超时。修复为 [fea6f24](https://github.com/oudream/cx-visual-x3/commit/fea6f24baa3d8897c2ec21e8db4d93da717d2b2f)：真实关闭/重新打开测试工作区，从持久化MHS Effective重新读取。
- [Driver环境预检真实WPF UIA #40813621209](https://github.com/oudream/cx-visual-x3/actions/runs/40813621209)，提交 `fea6f24baa3d8897c2ec21e8db4d93da717d2b2f`：**Success**。真正Windows x64 .NET48 TestCenter依赖构建、UIAutomation模块打开/Tab选中/SDK路径输入/Probe点击、正式MHS Effective重读、缺失设备与SDK的安全拒绝均通过。原始报告明确 `PASS Driver.G1.UIA`、`Effective=v2`、`DeviceReady=False`、`PhysicalActionVerified=False`、`HIL=NOT_RUN`。
- [同阶段全量TestCenter Windows #40813165822](https://github.com/oudream/cx-visual-x3/actions/runs/40813165822)：**Success**。它对应修复了WPF Thickness的旧SHA，不等于Driver后续UIA新SHA的全量运行结论，具体时间与覆盖应区分。

**结论**：Driver的“正式配置→真实环境证据查询→明确拒绝假设备就绪”的G1测试界面操作已验收。**不是**“Driver真实动作/采集→设备Readback→物理完成”的G2闭环。当前未提供可连接的目标设备、厂商SDK/许可和HIL安全环境，硬件动作不能凭软件测试伪造；真正工业闭环必须在已授权设备与具体专业Provider上执行、记录实机控制/采集/回读/停机/安全联锁、实测时间戳/资源释放并取得G2/G3日志。
