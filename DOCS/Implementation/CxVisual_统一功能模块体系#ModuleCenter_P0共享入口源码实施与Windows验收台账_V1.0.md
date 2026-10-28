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
