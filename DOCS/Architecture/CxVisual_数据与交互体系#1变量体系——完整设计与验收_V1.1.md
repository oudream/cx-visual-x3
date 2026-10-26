# CxVisual 数据与交互体系#1变量体系——完整设计与验收 V1.1


> **全类规划与归属（讨论稿）**：[《变量体系 V1.2 九项全类架构与归属方案》](CxVisual_数据与交互体系%231变量体系——重构后全类架构与归属方案_V1.2.md)。主功能设计及已实现差距仍以本 V1.1 为准；九项方案中的候选类型不代表已编译或验收。

> **2026-10-26 增强版。** 在 V1.0 完整内容上增补 `ReferTo/DeviceVariable`、`cx-visual-x2` 与 x3 `Porting` 的实际源码核查；正文第11～18节为本次增补，对此前定义和对象粒度作出正式细化。
> **完整使用原则**：优先复用生产使用过的线程安全读写和批量通知，再结合x2变量树/稳定身份/前值及x3现有Owner优化；确保百万级性能目标可实测，而不是继续建新系统。
> **证据约束**：现场百万级是项目负责人提供的生产经验；未获取准确吞吐统计环境和本次x3 Windows CI结果。不能当成本次实机性能PASS。


> 2026-10-26｜Unified Data & Interaction System｜正式五份主设计之一。
> **范围：运行期变量（State / Variable）、变量表以及点表到变量的映射。** 不包含数据库、工程配置、日志、UI交互或底层Channel/Protocol传输实现。
> **状态：基于真实源码的设计与差距审查，不是宣告以下所有候选功能已实现或Windows验收通过。**
> 上位总纲：[#0 总体架构 V1.5](CxVisual_数据与交互体系%230总体架构——完整设计与验收_V1.5.md)；兄弟规范：[#2 运行时数据 V3.0](CxVisual_数据与交互体系%232运行时数据体系——完整设计与验收_V3.0.md)、[#3 消息中心 V1.7](CxVisual_数据与交互体系%233消息中心——完整设计与验收_V1.7.md)、[#4 命令中心 V1.0](CxVisual_数据与交互体系%234命令中心——完整设计与验收_V1.0.md)。

## 0. 目标、职责与完整边界

变量体系回答“**现在是什么值、质量如何、何时更新、谁更新、如何及时取得变化**”。在多机台、多模块、多专业中提供低延迟、高频、作用域隔离、强类型的实时状态管理。面向PLC/IoT/X-Ray/HMI/Inspection，而不要求用户了解MQTT、Socket、Heap或MRK执行细节。必须兼顾开发者最简单的Get/Update/Subscribe与高级可靠性、诊断、批量、性能能力。

| 体系 | 拥有的权威事实 | 不应越界负责 |
|---|---|---|
| 变量 M09/MDS Variables | 当前状态值、初始化、质量、时间、来源、更新版本、订阅 | 不充当设备寄存器配置、MDS流程局部存储、指令执行日志 |
| Protocol PointTable（MPA） | 规约点位地址、站号、数据类型、读写属性和活动索引 | 不建立第二份权威变量值；点位地址≠变量业务名 |
| DataMapping（MPA） | Point→Variable/Heap/Stack/Message/Command/ProfessionalData 的方向与映射规则 | 不直接实现消息分发器和命令执行器 |
| MDS Heap/Stack（M10） | 节点入参、局部、输出、跨流程数据交接及大对象所有权 | 不把设备实时变量表混为流程堆 |
| 消息中心（M36） | 发布/订阅事件及通知，必要时携带变量快照/引用 | 不是高速设备值库或可靠命令执行中心 |
| 命令中心（FDC/MRK） | 命令定义、受控执行、反馈和查询 | 变量更新不代表设备命令已经执行 |
| Channel/Protocol/Driver | 物理连接、分帧、协议会话与厂商设备 | 不因传输成功便宣告值有效或设备命令完成 |

**不可变原则**：Foundation无Mechanism依赖；单一变量Owner；不建新的VariableManager或第二套配置/数据/通信真源；高频变量读写不强制绕道消息队列；设备硬实时和安全联锁仍归现场控制器及专业Owner。

## 1. 源码审计：2026-10-26主仓存在什么

| 真实文件与对象 | 当前已存在的API/行为 | 仍缺乏证据、不能当作已有API |
|---|---|---|
| Mechanism/Data/Variables/MDSVariableTable.cs：MDSVariableDefinition | Name、ValueType、Unit、Writable，构造校验 | DefaultValue、Source策略、完整标准Metadata与SchemaVersion |
| 同文件：MDSVariableTable | ScopeId、Define、Remove、Contains、Definitions、Get<T>、TryGet<T>、Update<T>、Sample<T>、VariableChanged | Previous、Revision、批量原子提交、分层通知、异步背压、权限及CAS |
| 同文件：MDSVariableQuality | Good/Bad/Uncertain/Stale | 现场来源级诊断与持久恢复不由枚举本身保证 |
| Mechanism/Data/Variables/MDSVariableContracts.cs | IMDSVariableTable；MDSVariableSample<T> 的 Value/UpdatedUtc/HasInitialValue | 质量/来源/提交版本并未出现在当前Sample契约 |
| Mechanism/Data/Variables/MDSVariableRegistry.cs | Register、Find、Remove、Snapshot；按ScopeId唯一注册 | 跨进程地址目录、租约/代次与自动注销 |
| Mechanism/Data/Variables/MDSProtocolDataTargetAccess.cs | 以scope/name引用变量；通过反射调用泛型Get/Update，提供真实Mapping入口 | 经过验证的高频免反射热路径、来源元数据贯通或远程适配 |
| Mechanism/Connectivity/Protocols/（MPA） | MPAPointDefinition、MPAPointTable、MPADataMapping/Executor和六类TargetKind | 完整设备驱动/HIL及远端桥接不由这些接口直接证明 |

**当前源码真实约束：**
- 表使用锁保护字典，Entry保存Value、Initialized、UpdatedUtc、Quality；Define时质量为Uncertain，Update成功置Good。
- 非空byte[]写入和读取做防御复制；值类型和string直接交付；可变复杂引用类型默认拒绝；与将来数据适配器的集成需另验。
- Update在锁外触发同步VariableChanged；避免用户回调处在锁内，但**慢回调仍可能拖慢Update调用线程**，不能宣称隔离完成。
- Get<T>在未定义、未初始化、类型不匹配时统一抛InvalidOperationException；TryGet<T>在这几种情况下均返回false。下面提出的错误分级是**待实现目标**，不能套用到当前接口行为。
- Registry按宿主/工程会话实例管理；不使用进程级全局静态VariableManager。解除Registry映射不等于正在使用它的对象自动Dispose。

对应已有集成测试素材：Tests/ArchitectureSmoke/Integration/ModuleFoundationSmokeTests.cs 与 ModuleChannelTransportSmokeTests.cs 等；测试存在≠本轮实际执行。最新Windows成功与失败需以同一SHA的工作流日志确认。

## 2. 运行对象与职责映射

| 概念 | 唯一Owner与建议模型 | 生命周期 |
|---|---|---|
| 变量静态定义 | MDSVariableDefinition（在现有对象上扩展，不另建一份Definition树） | 定义/加载→注册→注销 |
| 变量活动条目 | MDSVariableTable私有Entry | 未初始化→有效/不确定/坏/陈旧→移除 |
| 单次采样 | MDSVariableSample<T>，建议补全质量、Source/Writer、Revision、Previous等不可变快照字段 | 每次读取/提交生成只读观察结果 |
| 作用域管理 | MDSVariableRegistry | Host/Run作用域创建、隔离、释放 |
| 点位描述与运行索引 | MPAProtocolConfiguration.Points + MPAPointTable | 规约配置/协议会话 |
| 点位到变量映射 | MPADataMapping、MPADataMappingExecutor、MDSProtocolDataTargetAccess | 配置验证→应用→撤销 |
| 应用观察者 | 现有VariableChanged及后续订阅过滤（建议在原Owner内增构） | 订阅→回调/合并→解除订阅 |
| 可选跨进程桥 | 既有Transport/IPC/消息桥的明确导出层 | 在真实鉴权与协议版本验收后开放 |

不必为定义、样本、策略、回调每一个名词创建空接口。只有出现独立实现或组合变化点，才拆出Registry/Subscription/NotificationDispatcher等对象；已有Owner必须复用。

## 3. 完整能力需求与阶段边界

| 编号 | 功能和要求 | 实现/验收层级 |
|---|---|---|
| VAR-01 | Define/Get/TryGet/Update/Remove/Contains/Definitions | 当前有限API；需要异常与线程回归 |
| VAR-02 | ScopeId命名、注册查找、同名隔离、注销 | 现有代码；跨Host验证 |
| VAR-03 | 数据类型、只读、单位、初始未赋值分离 | 当前有限；默认值Schema待补 |
| VAR-04 | Current/Previous/Default/HasValue原子快照 | 待实现契约 |
| VAR-05 | Quality、SourceId、WriterId、DeviceTimestamp、CommitUtc、Revision | Quality/UpdatedUtc部分存在；其余待补 |
| VAR-06 | 原子批量提交及一致Snapshot，失败不得半写入 | 待实现 |
| VAR-07 | Value/Quality/Source/Committed变化通知、过滤、节流、合并 | 只有VariableChanged；其余待实现 |
| VAR-08 | 订阅生命周期、无界慢用户回调隔离、异常收集 | 当前锁外同步通知，待加固 |
| VAR-09 | 并发CAS/版本前提、多来源冲突与只读授权 | 待实现 |
| VAR-10 | Stale/断线/恢复，坏质量和过期时间明确区分 | Quality枚举存在；自动生命周期待实现 |
| VAR-11 | Point站号/地址→业务变量映射、多源和类型单位换算 | MPA已有映射底座；全路径专项验收 |
| VAR-12 | 统一路径/数值寻址与跨机台实例隔离 | ScopeId有限实现；数值地址和跨进程待实现 |
| VAR-13 | 变量列表/枚举、批量采样、监听、指标、搜索 | 部分定义枚举；统一观察/性能待实现 |
| VAR-14 | WPF Studio/TestCenter可编辑定义、监视实际值、质量和来源 | 待专项UIA验收 |
| VAR-15 | 远端变量只读/写入权限、鉴权、重连一致性、重放缺口 | P1+，不能冒充P0 |
| VAR-16 | 高频、压力、异常、边界与工业HIL | 各自独立验收，不承诺未实测SLA |

必须支持小值直接读写，特殊大对象只放MDS受控Ref/Handle并由变量维护状态或引用，不通过变量表每帧复制4300×4300图像或8GB体数据。

## 4. 静态定义、一次采样与提交模型（目标契约）

静态Definition含稳定地址/Code、Type、Unit、Writable、可选Default、质量/来源规则及版本；不应保存当前值。活动Entry保存Current/HasValue/Quality/Times/Source/Writer/Revision及Previous；Sample是同一锁内或不可变版本生成的整体快照，禁止逐字段读出不同时刻的组合。

**未初始化与默认值**：声明Default不表示已采样，也不意味着Good；首次Update才写Current并设置HasValue。Previous具有独立的HasPrevious，不能靠default(T)假装有旧值。拒绝更新不得改变旧值、质量、时间或Revision。

**提交语义**：每次接受的状态提交使Revision前进，ValueChanged仅在值真的变化时触发；仅Quality改变是QualityChanged；Source/Writer改变是SourceChanged；Committed表示成功提交。对于同值数据更新，是否刷新UpdatedUtc必须定义且在生产者/订阅者两端一致，采用“成功提交更新CommitUtc，是否更新DeviceTimestamp由来源决定”的目标契约。

**并发模型**：单值和批量都应校验全部Definition、Schema、Writable、Authorization、ExpectedRevision后再原子发布；长耗时回调不在变量锁内进行；跨机台Scope绝不共享可写Entry。批量的原子性只保证明确的同Owner同Scope范围，不能自动扩展为多PC事务。

**快照及可变引用**：对byte[]输入/输出维持防御复制；专业复杂类型使用Foundation.Data已批准的值快照/Adapter规范；不因实现高性能而允许共享可写数组逃逸。多订阅者事件负载为同一不可变版本信息，而非各自重新读最新值后错误归因。

## 5. 点表与变量的关系

1. MPAPointDefinition描述站号、寄存器/对象地址、值类型与读写权限；由唯一MPAProtocolConfiguration拥有配置。MPAPointTable为运行索引，不是再保存一份项目配置。
2. MPADataMapping允许一个Point扇出到多个Target，Direction/Operation分别控制入站和出站。写Variable时先按定义进行类型、单位、来源和权限校验；不允许通过点表写入绕过现有变量Owner。
3. 入站流程：Channel接字节 → Protocol解析并校验 → Point产生样本 → Mapping解析为Scope/Variable → VariableTable提交快照 → 通知必要订阅者。
4. 出站流程：上层发**授权的命令** → Professional/Protocol组织设备动作；若只更新机台期望值，它是应用变量变化，并不意味着PLC动作成功。设备实际值必须等下一次真实读回确认。
5. Protocol断链只可更新相应变量Quality（例如Stale），不能将旧Good值静默保持为有效；恢复后按实际来源重新采样。
6. Point身份、Scope/变量身份、命令身份各自稳定，Mapping显式连接。两台设备同名点位不得误更新同一宿主状态。

## 6. 与其余三大体系的协同契约

| 触发事件 | Variable | Data/Heap/Stack | Message | Command |
|---|---|---|---|---|
| 设备采样成功 | 存最新值和Quality/Source | 需要时将本次快照映射为流程输入 | 发布必要的值变化事件，不逐值强制MQTT | 不执行动作 |
| 用户要求采图 | 展示Ready/Busy/Done状态 | 图像Handle、算法输入和结果按生命周期管理 | Captured等轻量通知 | 经授权Invoke Capture并跟踪执行 |
| 视觉子流程运行 | 可公布业务状态/进度 | Frame内保存Input/Local/PendingOutput | 完成/异常可通知 | 需要明确受控动作时再调用 |
| 设备断线 | Stale且保留最后值供标注 | 当前Run按既有MRK策略处理 | 可发布断线事件 | 后续远程命令应拒绝/返回Unknown |
| 进程外访问 | 只有显式导出项对外可见 | 大数据走受控Ref/SHM/FileService | Broker/IPC等按需桥接 | 真实身份验证/超时/幂等/结果查询 |

**共享的是身份、作用域、授权、Correlation/Operation以及诊断语义，不是四者共用同一个队列或对象仓。** Studio只显示同一个权威变量Owner的投影，不保存另一份可写运行真值。

## 7. 开发者与工程编排者完整使用路径

### 7.1 开发者：当前能直接调用的真实API

~~~csharp
using System;
using CxVisual.Mechanism.Data.Variables;

MDSVariableTable table = new MDSVariableTable("LineA");
table.Define(new MDSVariableDefinition("Camera.Ready", typeof(bool), writable: true));
table.Update<bool>("Camera.Ready", true);
bool ready = table.Get<bool>("Camera.Ready");
MDSVariableSample<bool> sample = table.Sample<bool>("Camera.Ready");
MDSVariableRegistry registry = new MDSVariableRegistry();
registry.Register(table);
MDSVariableTable scoped = registry.Find("LineA");
// 使用后取消订阅并从宿主Registry注销；当前API不自动关闭下游设备。
~~~

以上与2026-10-26读取的代码一致；注意Sample目前不包含Quality、Revision和Source。VariableChanged是同步锁外回调，订阅代码须自行避免堵塞更新线程。

### 7.2 工程用户：目标体验（设计，不代表UI已交付）

工程用户在机器/应用/模块下创建变量定义或从已配置的PointMapping绑定；在Studio选取当前变量时看到 Current/Previous、质量/时间、来源、版本以及从哪个Point写入。状态监视可按模块/质量/类型筛选；绑定HMI显示或MOL变量输入，不要求创建消息订阅节点；执行设备动作必须通过命令中心，禁止表单直接调用厂商SDK。断线显示Stale而非正常绿色状态；只读用户看值但不能改写现场设备。

### 7.3 两台机器、同名变量的隔离

两个Registry/Scope分别创建 LineA.Camera.Ready 和 LineB.Camera.Ready；按ScopeId寻址应读取不同值。一个作用域注销后，旧订阅的Generation/Run不得被新实例复用为有效投递。当前Registry支持名称查找和移除，但更严格的代次和自动订阅清理属于后续实现要求。

## 8. 生命周期、错误与性能门禁

- **Define**：不允许空名、空类型、重复名或未批准的可变类型；不允许假定有默认值即有现场值。
- **Update**：类型不符/只读/无授权/预期版本冲突必须明确失败；拒绝不得推进版本。当前代码已经拒绝类型不符/只读，但尚无权限与CAS API。
- **Get/Sample**：分别报告未定义、未初始化、类型不符、质量坏与权限不足；兼容旧TryGet的false语义如何演进必须单独审查。
- **Subscribe**：确保回调异常不会破坏采样线程、慢消费者不会使Producer无界阻塞；有界队列、Drop/Coalesce等策略须可检查，不能静默丢失必达事件。
- **Stop/Remove**：先停Producer及外部Bridge，再Drain/Cancel订阅与注销，消除旧Run迟到写；强制中止不得破坏其他Scope。
- **Performance**：重点测并发读写、十万级点表规模（作为压测维度而非已证明容量）、回调延迟、内存峰值、批量事务和网络断线重连。普通Windows网络/GC不承诺硬实时。
- **跨PC**：仅通过明确鉴权/版本/授权后的Transport/Bridge提供，Local Registry存在不能等价声明远程连接完成。

## 9. 验收矩阵（候选Case ID，不是已执行记录）

| Case | 正向验证 | 必测反例 | 状态 |
|---|---|---|---|
| VAR-T01 定义 | 合法类型与单位，重复检测 | 空名、void、重复定义 | 当前可执行路径，未本轮运行 |
| VAR-T02 基础值 | Get/Update/Sample相同结果 | 未初始化、错误类型、只读 | 当前可执行路径，未本轮运行 |
| VAR-T03 快照隔离 | byte[]写后改原值不污染表 | 非批准可变引用对象被拒 | 当前可执行路径，未本轮运行 |
| VAR-T04 Scope | 两个Scope同名不同值 | 重复Scope登记拒绝 | 当前可执行路径，未本轮运行 |
| VAR-T05 Previous | 两次成功提交旧值与新值正确 | 首次无Previous、拒写不改Previous | 待实现 |
| VAR-T06 质量来源 | Good/Bad/Stale与来源分离 | 断线旧值不得假Good | 待实现 |
| VAR-T07 Revision/CAS | 原子快照与单调版本 | 并发版本冲突拒绝且不半写 | 待实现 |
| VAR-T08 Batch | 批量一致提交与一次通知 | 中间校验失败全不提交 | 待实现 |
| VAR-T09 订阅 | 多订阅类型/过滤准确 | 慢回调/抛异常/取消无死锁 | 现有基础事件，待增强 |
| VAR-T10 Point Mapping | 入站点位正确更新Variable | 站号错误、悬空映射、类型错 | 需真实MPA专项 |
| VAR-T11 设备反馈 | Command发出与实际读回区分 | 不把WriteValue等同设备确认 | 专业/HIL待验收 |
| VAR-T12 Host隔离 | 两机台同名隔离且可释放 | 上一代迟到通知不串新Run | 当前有限基础，需增强 |
| VAR-T13 性能 | 多写单读、批量吞吐、统计延迟 | Slow Subscriber与资源泄露 | 待实测 |
| VAR-T14 UI体验 | 变量列表、质量/来源/版本显示 | 只读拒写、失联状态明确 | Studio/TestCenter待验收 |
| VAR-T15 远程桥接 | 经认证远程读取及反馈 | 断网、重放、认证失败 | P1+，不能当作当前完成 |

已有统一交互验收候选位于 DOCS/Testing/CxVisual_公共机制体系#统一交互——TestCenter与Windows验收矩阵_V1.4.md 的 BUS-VAR-01～10；新增VAR-T编号仅作本文需求追踪，实施时应映射既有测试ID，**不建立另一套重复测试用例身份**。

## 10. 实施顺序与兼容边界

- **P0-A** 对照V4.9/V6.4与现有源，确定变量定义/作用域/来源/快照合同；保留现有MDSVariableTable正式入口和原有序列化身份。
- **P0-B** 在同一MDS变量Owner内增加Current/Previous/Default/Quality/Source/Revision原子只读视图；包含同值提交、空值、拒写和多源规则。
- **P0-C** 在已有F19并发设施基础上完成通知隔离、过滤及批量提交；不自造第二套消息中心或线程池。
- **P0-D** 经MPA PointTable/DataMapping和MDSProtocolDataTargetAccess贯通真实Point→Variable→订阅，完成Windows x64构建、ArchitectureSmoke、TestCenter功能正反例。
- **P1+** Studio便捷编辑器、授权/远程桥接、长期高吞吐与现场PLC/HIL压力验收；明确指标及环境。

文档裁决范围仅为设计、源码对照和分阶段验收要求；**未修改生产源码，未执行Windows CI、设备HIL，不得将需求矩阵勾选为通过。**

---

## 11. V1.1 正式增补：以 DeviceVariable 生产源码为底座，而不是重造变量系统

> **增补日期：2026-10-26。** 本章至第19章覆盖和细化 V1.0 中的对象粒度、迁移路径、通知语义、高吞吐与优先验收；与前述需求出现冲突，以本节明确的粒度及性能裁决为准。
> **证据分级**：E1=已读源码，E2=对应测试源码存在，U=用户生产实践信息，D=本次设计建议，P=尚待实现/实测。不能把E1、E2或U单独当作x3实机性能PASS。

### 11.1 这次直接复用的三个来源

| 来源 | 已核对的确切源码和行为 | 与现行x3的关系 |
|---|---|---|
| **ReferTo/DeviceVariable（E1）** | `ReferTo/DeviceVariable/VariableInterfaces/VariableTable.cs`：按设备建表，表内 `Dictionary<string,IVariableInfo>`，锁内读取/写入，值未变也刷新时间，区分 `ScanUpdate/ValueChange/VariableCreation`；`VariableManager.cs`：`PushUpdate`登记变化，`AutoResetEvent`唤醒处理线程并批量推送 `OnVariableChanged`；`VariableInfo.cs`：Value/LastUpdated/HasInitialValue | **明确优先迁移的生产经验与机制**，不可把旧全局静态Manager原样恢复为x3的权威Owner |
| **cx-visual-x2（E1+E2）** | `Platform/Variables/VariableNode.cs`、`VariableRegistry.cs`、`VariableValue.cs`、`Legacy/VariableTable.cs`、`CxVariableRuntimeState.cs`；`Tests/Tests/Tests/GeneralVariableFoundationTests.cs`、`Tests/Tests/V13/CxVariableTests.cs` | 已有变量树、稳定ID、路径迁移、`Variable<T>`旧值/版本及运行态Quality/Source/Sequence；应直接比较并选取更成熟的代码，而非凭想象新增第二套 |
| **cx-visual-x3（E1）** | `Mechanism/Data/Variables/MDSVariableTable.cs`、`MDSVariableRegistry.cs`、`MDSVariableContracts.cs` 已编译归属；`MDSProtocolDataTargetAccess.cs` 提供MPA写入桥。另有 `Variables/Porting/MDSVariableTable.cs`、`MDSVariableInfo.cs`、`MDSVariableRegistry.cs` | **已经迁入部分能力**。Porting文件首行明确写着 `NOT COMPILED`，并且不列入 `CxVisual.Mechanism.Data.csproj` 的Compile列表；这是迁移参考，不是另一个正式运行系统 |

**生产场景信息（U）**：负责人明确 `DeviceVariable` 用于 `CxECTStudio` 和气密性项目，具有每秒百万级变化的生产使用经验。这里作为**必须保留和回归的性能基线**，不是本次读取了现场压测报告、也不是x3已达100万/s的宣称。相关项目的具体调用点、环境、变量规模、平均及峰值频率、订阅者数量需在迁移实施时记录；本次未取得这两个现场应用的完整调用/性能证据。

### 11.2 对“一个变量表就是一个变量”的业务模型做精确定义

DeviceVariable原版从代码看**一个设备有一张表，表内可以有多个变量**；但实际业务往往把一张表作为一个**业务变量对象/测量对象**使用，其内部的 `Value/OldValue/DefaultValue/Quality/...` 是这一个对象的多个属性；在 x2 也有 `VariableObject -> VariableTable` 的实体关系，以及通用 `VariableGroup -> Variable<T>` 变量树。

**V1.1裁决（D）**：同时兼容三个粒度，不要求用户为一个温度值额外创建“设备 + 表 + Value叶节点”的繁琐结构，也不允许把任意属性名误当独立设备点。

| 颗粒度 | 说明 | 典型使用 |
|---|---|---|
| **变量（Variable）** | 一个稳定 VariableId 的业务值；自带当前、前值、默认、质量、来源、时间、版本等**状态属性**（不是多个独立业务变量） | `/Line1/StationA/AirPressure` 一项压力；`Current/Previous/Default` 属于同一变量 |
| **变量组 / 兼容变量表（VariableGroup / VariableTable）** | 一组变量，或一个业务对象的多个真实属性；不与单个Variable强制1:1，也不与单台设备强制1:1 | `StationA` 下 `Pressure/Temperature/LeakRate`；一个DeviceVariable旧表映射为对象属性组 |
| **变量目录（Registry）** | 按Host/Scope管理变量和组，稳定ID、路径、层级、生命周期、OwnerRef与检索；不复制Value | 多条线、多工位、多个实例及专业资源复用 |

例如，旧系统在表 `LeakTester` 里定义 `Pressure`、`Result` 是两个业务变量；若在表 `Pressure` 中存 `Value`、`OldValue`、`DefaultValue`，则它是**一个变量的状态属性包**，不得再次把OldValue注册为一个与Pressure并列的可写变量真源。解析旧表时需要**显式的迁移/映射规则**；不能凭名字自动猜测整表属于一个变量还是变量组。

推荐对象关系（逻辑类名/候选，不宣称所有类型已存在）：

~~~text
MDSVariableRegistry (现有Owner；实例作用域)
 ├─ VariableGroup / Legacy VariableTable Adapter (x2成熟能力；需审查迁移)
 │   ├─ Variable<T>  → Current / Previous / Default / Quality / Source / Time / Revision
 │   ├─ Variable<T>  → Current / Previous / Default / Quality / Source / Time / Revision
 │   └─ 子 Group...
 └─ 稳定ID/可变Path索引（x2 VariableRegistry逻辑）
 
 MPA PointTable/Mapping ──(显式映射)──> 正式Variable Owner
 消息中心 ──(轻量事件)──> 订阅消费者
 MDS Heap/Stack ──(受控数据端口)──> 流程业务
~~~

### 11.3 独立身份与归属

1. **VariableId 永久稳定，VariablePath 可变**：吸收x2 `VariableNode/VariableRegistry` 的ID/Path双索引和Move/Rename回滚检查；更换父组后不因路径变化换一个业务身份，旧Path不得误命中新节点。运行实例附 `ScopeId/Generation`，远程重连要防迟到写。
2. **OwnerRef与组织树正交**：既可以归属设备，也可归属算法、HMI、MOL流程或业务对象，迁移组节点不改变专业Owner。归属不等于设备寄存器地址。
3. **定义与运行态分离**：变量声明（Id、Code、Type、Unit、可选Default、访问和数据质量策略）遵守唯一工程真源及既有配置体系；运行表只能保存此定义的活动值，不复制新项目定义树。
4. **单变量直接用，组合作为可选容器**：给专业开发者`Get<T>(id)`/`Update<T>(id,value)`的简短入口；旧`GetVariable(deviceName, variableName)`可通过受控适配而不是恢复全局静态字典。

## 12. V1.1 运行态数据完整字段与语义裁决

| 字段 | 语义与更新规则 | 现有证据 |
|---|---|---|
| `CurrentValue` | 最近一次有效提交的业务值；质量坏时可保留最后值，但必须随质量呈现 | ReferTo Value；x2 Variable<T> / RuntimeState |
| `PreviousValue` | **上一次不同的业务值**，仅ValueChanged时推进；连续同值扫描不清掉前值 | x2 `CxVariableRuntimeState<T>.Refresh` 已有此语义 |
| `LastSampleValue`（可选） | 若业务确实需要“紧前一次采样值”，单独定义；不能混用PreviousValue | x2 `Variable<T>.SetValue` 每次写入更新OldValue，**与上条语义不同** |
| `DefaultValue` | 声明/复位使用的静态默认值，永不凭默认值宣称已采样、在线或Good | x2工程定义中已有Default；x3字段待补；旧运行State初始化默认值时可能置Good，迁移须裁决 |
| `HasValue / HasPreviousValue` | 明确区分合法null、尚未采样、首采样无前值 | x2 RuntimeState有两标志；x3仅Initialized |
| `Quality / OnlineState` | Good/Bad/Uncertain/Stale与Online/Offline可明确映射；断链旧值不能被当Good | x3 Quality四值；x2另有Disconnected与OnlineState，需映射 |
| `LastRefreshUtc / LastChangeUtc` | 每次成功扫描刷新Refresh；仅实际值变化才更新Change；另可记DeviceTime、CommitUtc | x2 RuntimeState两时钟语义可复用 |
| `SourceId / WriterId / Reason` | Source是真实设备/算法来源，Writer是提交者，Reason为状态原因，分离审计与数据溯源 | x2 Source/Reason存在；x3待扩展 |
| `Revision / Sequence` | 每次已接受提交单调递增，拒绝重复/倒退序号；不等同Message EventId | x2 Variable<T>.Version及RuntimeState.Sequence；x3待补 |
| `Unit / Type / Writable / Metadata` | 定义固定，运行快照引用或呈现不可变描述；类型验证和只读检查 | x3 Definition已有Type/Unit/Writable |

**语义差异必须显式记录**：x2 `Variable<T>` 在每次Set时推进 `OldValue`；x2 `CxVariableRuntimeState<T>` 在`changed`为真时才更新 `_previous`。V1.1为HMI、报警、趋势、变化条件统一选用**最近一次不同值**。需要“上次采样”的专业用途应另拿采样字段或带序列时间窗的专业数据流。保证 `ValueChange`、`Refresh`、`QualityChange`、`SourceChange` 和 `Committed` 可明确区分，**提交成功不等于业务值变化**。

相同值每秒刷新百万次时，允许Revision与Refresh更新，但不能每次都生成一个大量消费者的ValueChanged业务通知；扫描频率、推送频率、展示频率解耦。变更的因果序列由Owner维护，WPF不能借UI节流伪造采样次数。复位默认值必须经过授权运行操作，不能在重新绑定UI时悄悄重写当前值。

## 13. V1.1 百万级变化：保留成熟通路并分层强化

### 13.1 现有ReferTo真实算法值得直接吸收的方面

- **生产者写入与消费通知分离**：`VariableTable.UpdateVariable`在表锁外调用`VariableManager.PushUpdate`，后者用`Interlocked`编号、在队列锁内收集条目，由`AutoResetEvent`唤醒专用后台工作者，周期批量触发`OnVariableChanged`。避免对每条采样直接向每个UI/应用同步回调。
- **变化种类区分**：`ScanUpdate`、`ValueChange`、`VariableCreation`；针对气密、采集类高频数据的订阅者可以只订真正变值的事件；保留周期刷新作为单独指标和选用订阅。
- **强类型Get/TryGet与最近采样时间**：原版 `GetVariable2/3` 对于需要值/时间/初始化标记的使用者比较直接。
- **代码需要保留实际语义而不是机械复制**：原版每个Push仍入List、更新时间还触发Push、静态列表可能无界；`OnVariableChanged`的消费者异常可能导致后台工作线程中断；这些在x3不能原封照搬。百万级性能仍需新宿主真实测量。

### 13.2 建议形成三条独立路径（D）

~~~text
PLC / 高速采集 / 算法结果
     │
     ├─ [Hot Path] Resolve一次 → Typed Slot/Handle → 原子Update
     │                         → 当前值/前值/质量/时间/Revision
     │                         → 不调用UI，不序列化，不为单次刷新建消息DTO
     │
     ├─ [Change Path] 按值/质量/来源判定 → 有界批次/合并 → 订阅者
     │                                      └→ 必须逐件观察时显式可靠策略
     │
     └─ [Observe Path] 按需Snapshot/统计/调试 → Studio/TestCenter节流显示
                                             → IPC/MQTT仅按需Bridge
~~~

**高频热路径要求**：避免每次 `Definitions().SingleOrDefault`、反射 `MakeGenericMethod/Invoke`、名称重新解析、JSON/JToken序列化、全表锁、每次大量小对象分配、等待WPF线程或等待MQTT回执。针对固定Schema/Point映射允许编译一次`VariableSlot<T>`/绑定句柄（**候选API，不是现有类型**），热路径只做类型明确的写入和必要同步。复杂可变引用值沿既有Foundation.Data值安全规则处理，不为了压测放开危险别名。

**分区与背压**：保留x2/ReferTo已证明有用的异步批量思路；对不同Scope/生产者可分片存储和订阅队列；队列容量、批量上限、超时、`DropOldest/Coalesce/Reject`等策略明确。HMI只关心最新状态时允许按变量ID合并；要求每笔必达的统计/审计流不能静默合并或丢弃，应交给专业Stream/MDS或明确可靠队列。重连先Snapshot后Delta，并检测丢失版本区间。

**同步与一致性**：通用实现先采用可测试的每Variable原子状态更新和读快照（锁或不可变快照）；确认收益后再在局部尝试Interlocked/原子引用置换，保证32/64位读、版本、对象快照一致性；无真实压测不把“无锁”当架构要求。同Scope批量原子提交是高级专门路径，不让每条单变量热写承受全局批量事务锁。

### 13.3 性能指标需要拆开定义而非只写“百万级”

把负责人已有百万级生产实践当作**回归目标/优化起点**，先整理旧现场吞吐测量条件，再在x3 Windows x64上分别测：

| 测试 | 核心测量与必须记录 |
|---|---|
| **P-VAR-01 单线程写入** | 1、10、100、1万变量的合计提交/s、p50/p95/p99、分配字节、峰值内存、CPU |
| **P-VAR-02 多线程写入** | 2/4/8/16生产者、同变量/不同变量、争用与公平性；连续运行、异常和丢计数 |
| **P-VAR-03 真正变值** | 100%同值刷新、1%/50%/100%变值；Refresh与Changed分别计数；Previous语义不退化 |
| **P-VAR-04 变化分发** | 0/1/10/100订阅者、慢订阅者、抛异常、取消/注销、合并、背压和漏序列记录 |
| **P-VAR-05 点表映射** | MPA Point→MDS通过“缓存类型句柄”与现有反射路径的真实性能比较；类型错误/来源正确 |
| **P-VAR-06 真实设备与x2对照** | 同主机、同编译优化、同负载记录x2/ReferTo及x3结果；采集现场测各环节瓶颈，不凭Mock声称产线已证实 |

百万/s是**跨测试维度的吞吐目标**，不自动意味着一百万条消息、WPF绑定刷新或网络发布每秒均可实现。规定预热、采样区间、进程内存、GC Gen0/1/2、CPU核心数、线程和订阅策略、数据类型与错误率。短时间突发峰值和可持续吞吐单独验收；回归门禁以上次相同硬件与配置的实际基准作对照。

## 14. V1.1 从旧生产源码到x3的精准迁移表

| 能力 | ReferTo / x2 现有来源 | x3真正编译的源码 | 下一步，不再建重复体系 |
|---|---|---|---|
| 数据字典及基本值 | ReferTo VariableTable；x2 Legacy VariableTable | **已有** `Mechanism/Data/Variables/MDSVariableTable.cs` | 继续扩展当前Owner，兼容现有`IMDSVariableTable` |
| 值、前值、默认值与版本 | x2 `VariableValue.cs`、`CxVariableRuntimeState.cs`、工程定义 | x3有Current/UpdatedUtc/Quality，**缺完整快照** | 从x2移植并消除两种Previous语义冲突，Default保留在定义 |
| 组、树、变量身份 | x2 `VariableNode.cs`、`VariableRegistry.cs`、`Legacy/VariableTable.cs` | x3有Scope变量表与注册器，**无等价完整树迁移证据** | 复用ID/Path/OwnerRef/Move等逻辑，映射唯一MEM工程对象身份，不建第二工程树 |
| 变化事件批量通知 | ReferTo `VariableManager.PushUpdate/ProcessUpdates`；x2兼容Manager | x3 `VariableChanged`是锁外同步回调 | 移植分批通知本质，并加有界背压/过滤/订阅回收，不照搬静态线程 |
| 旧生产API兼容 | ReferTo Manager/Table；x2 `DirectVariableManagerAdapter` | x3现有`IMDSVariableTable`、Registry | 按需有限适配；不要把适配层提升为第二个Owner |
| Protocol/点表 | ReferTo DeviceInterfaces/PointSheet；x2协议适配 | x3 MPA PointTable/Mapping + MDSProtocolDataTargetAccess | 不动已统一的Channel/Protocol单一真源，先缓存高频写的类型句柄 |
| 源码审查拷贝 | 无需 | `Variables/Porting/*.cs` 标注 `NOT COMPILED` | **不得说已迁移编译**；迁移能力后根据仓库规则决定清理无用影子文件 |
| x2变量Provider | x2 `DirectVariableManagerAdapter`、`DeviceVariableRuntimeHost` | x3没有相同正式宿主的全链证据 | 参考生命周期与编译期适配，统一宿主仍归现行x3 |

### 14.1 关键迁移纪律

1. 先逐类审查x2的实际实现和`DeviceVariable`生产调用点；生产项目继续服务现有设备，`ReferTo/`原样保留，不直接改参考源码。
2. **x3的正式编译入口始终唯一**：已有`Mechanism.Data.Variables`、数据/连接器Owner不改变；Foundation.Data只接收无机制依赖的类型/安全值契约。不得增加新 `.csproj`、新静态VariableManager、额外Flow/DataHeap。
3. 对x3当前`MDSVariableTable`提供兼容性的增量API，再逐项整合x2稳定ID、Previous、Default、分层刷新、批次通知；不能先大规模复制类再声称已合并。
4. `Porting`标注为源码研究素材，不能计入本次成功编译的类清单；已被现行正式类型替代的部分不重新回填同名影子类。
5. 当前用户明确要求的**设计文档更新**不自动授权x3生产源码迁移或部署；迁移待实施任务按单主题提交并逐项测试。

## 15. 开发者、CxECTStudio与气密项目的代表性场景

### 15.1 单变量业务对象（D，目标使用体验）

~~~csharp
// 意图示例：下面是假定完成V1.1增强后的API，不是x3当前已编译方法。
IVariableHandle<double> pressure = variables.Resolve<double>("Line1/LeakTester/Pressure");
pressure.Update(0.125, sourceId: "PLC1", quality: VariableQuality.Good);
VariableSnapshot<double> s = pressure.Snapshot();
double current = s.CurrentValue;
double lastDifferentValue = s.PreviousValue;
double defaultValue = s.DefaultValue;
~~~

`IVariableHandle<T>`只是热路径句柄概念，用于说明简单API，不新增作为已存在类。可在现有接口增构/适配并凭压测再决定是否引入。默认值在定义中只读，可由Snapshot展示，但真正采样必须独立确认。

### 15.2 气密检测

气密设备的压力、流量、泄漏率、阈值判定与联机状态，各自作为具稳定身份的运行变量；连续同值刷新更新Refresh/Revision但不消耗“值变化”推送能力。压力曲线若要**保留每个采样点**，采用已有MDS Stream/专业采样缓冲，而非不断发Message或堆积全历史于VariableTable。判定命令属于MRK Command，测得结果和值属于各自Owner。现场是否确实使用某一精确类/方法仍需对应项目真实源码佐证。

### 15.3 CxECTStudio与X-Ray检测

多个探测器的Ready/Busy/Temperature/TriggerCount/ScanState适合变量中心；图像和AI输出本体归MDS/专业数据Owner，变量只保存状态及受控引用。状态变化可以发布轻量Message、设备采图经授权Command，二者不得合并为写一个bool的伪设备动作。这里是复用设计用例，不声称本轮完成CxECTStudio现场接口或图像硬件验收。

## 16. 统一变量管理最小公共API（目标而非当前SDK）

| 面向开发者 | 最简动作 | 高级行为归原有Owner |
|---|---|---|
| 获取 | `TryGet<T>(id, out value)` / `Get<T>(id)` | 按ID或Path解析、Scope/权限、Snapshot一致性 |
| 提交 | `Update<T>(id, value)` | Source/Quality/ExpectedRevision/原子批量/设备来源 |
| 观察 | `Snapshot<T>(id)` / `Subscribe(id, options)` | ValueChanged/Refreshed/QualityChanged/SourceChanged及背压 |
| 组织 | `Define` / `Remove` / `FindById` / `FindByPath` | x2的Group/稳定ID/Move、工程身份与生命周期 |
| 点表接入 | `Map(point, variableRef)` | MPA配置、类型转换、协议错误与源追踪 |
| 诊断 | `Inspect(scope)` | 只读快照、来源、Revision、变化/合并/丢弃计数 |

**这些是面向使用者的契约草案，不要求为每一行专设一个接口类。** 真正实现须检查当前Interface、主架构V6.4 Excel和项目兼容性；现有`MDSVariableTable.Update<T>`/`Get<T>`保持原行为直到相应兼容验收。

## 17. 功能补强检查单与逐项验收

| 任务ID | 关键成功断言 | 关键反例 | 当前状态 |
|---|---|---|---|
| V11-01 源码复用账本 | ReferTo/x2/Porting/x3每类有Owner与转化去向 | Porting未编译却宣称已迁移 | 源码核实，文档已补 |
| V11-02 变量粒度 | 单变量可独立使用；旧一表多属性按显式规则兼容 | 错将Default/Previous注册为多份真值 | 待实施 |
| V11-03 ID与层级 | 组移动/改名不换Id，旧Path失效，OwnerRef不变 | 两Scope串值、代次复用、冲突Path | x2有相应测试源码；x3待实施 |
| V11-04 完整状态快照 | Current/Previous/Default、HasValue、Quality、Source、Time、Revision同一次一致读 | 同值刷新冲掉Previous、拒写改Version、默认值伪Good | x2已有部分；x3待实施 |
| V11-05 高频热路径 | 类型句柄不通过每次反射，写入不等待通知线程 | 慢回调阻塞采集、队列无界、丢变更无指标 | 待实施与Windows性能测试 |
| V11-06 批次与丢失感知 | 变值/采样分离，批次通知有次数/范围/背压指标 | UI丢包后仍宣称连续、必达事件被合并 | 待实施 |
| V11-07 Point/MPA | Point→Variable真实Owner，拒类型错、源错、站号错 | 无真实授权而写设备，数据映射失败报成功 | 当前有限基础；待集成验收 |
| V11-08 正式Windows构建 | MSBuild x64/ArchitectureSmoke/TestCenter与正反例均有SHA | 只有源码声明或Linux构建就写PASS | 未本轮执行 |
| V11-09 生产场景 | CxECTStudio/气密性生产调用路径与旧版行为对照；同负载性能报告 | 拿模拟负载取代设备HIL或用户百万级经验当x3数值 | 待现场资料与真实验收 |

建议验收集：`BUS-VAR-01～10`依然是统一交互矩阵的既有ID，`VAR-T01～15`与本V11编号只用于需求追踪，落地TestCenter时必须映射到已有Case身份，不创建三套重复测试系统。

## 18. 优先开发顺序（保持每轮一个增量、一个提交）

1. **P0-1：复用落位核查**：把`ReferTo/DeviceVariable`、x2 `Variable<T>/Group/Registry/RuntimeState`、x3`MDSVariableTable/Registry`和`Porting`逐类型去重，确认Provider调用及现有接口消费者。
2. **P0-2：单变量状态/作用域**：在唯一Owner增补Previous/Default语义、Quality/Source/Time/Revision与原子Snapshot；补x2同值和跨Scope反例。
3. **P0-3：百万级通路**：把旧`PushUpdate/ProcessUpdates`的好机制按x3 F19/订阅Owner迁移，合并策略与慢回调保护；MPA热路径去反射或预绑定，完整正反例与基准对照。
4. **P0-4：业务复用与测试**：以CxECTStudio/气密性实际代码调用方式为参考，完成可运行的开发者/Studio/TestCenter用例和Windows同SHA验收；工业HIL分别标记。

**裁决结论**：`DeviceVariable`的线程安全与通知批处理应实质复用；x2的`Variable<T>/VariableGroup/VariableRegistry/CxVariableRuntimeState`补足它在身份、组织、当前/旧值、质量上的短板；x3以已编译`Mechanism.Data.Variables`为唯一运行真源，进一步提供便捷API与可量化的高性能能力。不因复用迁移再制造另一套VariableStore或与工程/数据堆重复。
