# CxVisual 数据与交互体系#1变量体系——完整设计与验收 V1.0

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
