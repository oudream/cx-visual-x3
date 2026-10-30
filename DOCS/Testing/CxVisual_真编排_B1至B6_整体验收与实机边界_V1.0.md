# CxVisual 真编排 B1～B6 整体验收与实机边界 V1.0

> 2026-10-30；本文件是**验收入口/缺口台账**，不是替代 Core、Decision、MRK、MDS 或专业 Provider 的新架构。每条通过结论必须绑定真实固定 SHA 和 CI Run；后续 master 的改变须重新验证。
>
> 工程原则：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 判定等级

- **源码存在**：能从最新 master 找到 Owner 与真实入口；不等于测试通过。
- **Windows 自动化通过**：固定 SHA 的 .NET Framework 4.8 x64 CI 通过；仅覆盖 CI 实际运行的测试。
- **现场通过**：在目标机台使用实际设备/驱动、PLC、检测原图、MES 和安全链验证；必须另附测试记录，软件仿真不能替代。
- **未验证/阻断**：相关测试排队、失败、缺设备或没有原始记录。绝不可改写成“完成”。

## 2. 六包统一真源与验收入口

| 包 | 真源/Owner | 现有可执行验证 | 完整判定条件 |
|---|---|---|---|
| B1 工程真源 | MEM/MOL；MOCFlowBuilder | OrganizationLogicIntegrationTests；真实 .cxproj Save/Open | 同一 Project/Content、V1/V2、公开 Port、引用及 Revision 往返 |
| B2 计划交接 | Decision 静态 Plan；MRK 侧 MOCManifestRuntimeBinding | MDCDecisionTests；`--orchestration-vertical` | 来源闭包、静态合同、版本/权限失效拒绝，不能伪称完全脱离 Runtime |
| B3 专业执行 | Professional Vision；Runtime/MDS | G06CpuVisionThroughMOC；RuntimeVisionDataFlowTests | 真实软件算法输入/输出与 SourceMap；现场设备 Driver 单独验收 |
| B4 数据控制 | MRK/MDS/FCR | `--orchestration-industrial` | 取消、资源限流、断点、异常隔离、数据作用域和无泄漏 |
| B5 Studio 调试 | Studio WPF 消费 MRK Trace/SourceMap/有界数据摘要 | `--shell-self-test` 的 B5 与 P0-4 断言 | 活跃 Flow 断点、单步、继续、取消、可观察数据及错误工程拒绝 |
| B6 工业验收 | Runtime.Host、CI、现场 Owner | `unified-runtime-windows.yml`；20k MDS soak；真实 Host 子进程 Journal 反例 | 固定 SHA 全门禁、签名发布/防回滚/恢复；实机验证按第 4 节 |

## 3. 软件 CI 运行命令与证据

在 Windows x64 按真实解决方案构建完成后：
```powershell
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --orchestration-vertical
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --orchestration-industrial
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --runtime-data-soak 20000
```

- Workflow：`.github/workflows/engineering-organization-windows.yml`、`decision-windows.yml`、`unified-runtime-windows.yml`、`studio-shell-windows.yml`。
- Studio S1 必须执行真实 WPF STA 自测并检查 B5 调试断言，而非只统计总 PASS 数。
- Runtime.Host 真实独立进程必须验证：正常跨进程重启/恢复；Journal 缺失拒绝；未结案 Flow/外部状态未知拒绝；Journal 不被篡改；签名发布及防回滚。
- MDS 的 20,000 帧资源释放门禁验收生命周期和内存预算；**不能**单独用它证明设备吞吐量、采图速度或现场稳定性。

### 固定提交已取得的历史证据

- `7e8dda67`：Engineering、Decision、Runtime、MDS RuntimeData Windows CI 成功，含 B4 工业专项。
- `2790de87`：Studio S1 / Engineering 成功，含活跃 Flow 断点/单步/继续/取消。
- `3cdb6299`：Runtime 成功，含 MDS 20,000 帧长稳。
- `ae56ba57`：Runtime 成功，含 Host 五轮跨进程运行恢复。
- `98294ee3`：Runtime 成功，含 Host 未结案/未知外部操作 Journal 的独立进程拒绝反例。
- `0511c75c`：Studio S1 Windows CI 成功，已覆盖运行时结果摘要查看。
- `f5cb1084`：Studio S1 Windows CI 成功，已覆盖 Flow 完成后摘要保留及跨工程拒绝。
- `a9cca000`：Unified Runtime、Decision Windows CI 成功，覆盖 72 节点有界摘要。
- `8ffa037c`：固定容量算法与 Studio 完整组合对应的 Studio S1 CI 尚需单独取得成功结果；不同 SHA 的成功证据不能冒充同 SHA 全绿。

### 3.1 B1～B6 八项整体验收跟踪（2026-10-30 增量）

此表是**按能力关闭**的证据索引；历史成功仅覆盖其对应的固定SHA，不推定新master自动全绿。

| ID / 包 | 真正产品条件与源代码入口 | 自动测试与反例 | 固定SHA / Windows证据 | 状态及剩余边界 |
|---|---|---|---|---|
| ORCH-01 / B1 | Studio经MEM/MOL维护真实Page/Content/Block/Port和版本，保存关闭重开 | `OrganizationLogicIntegrationTests`、`--orchestration-vertical` | [7e8dda67 / Runtime 40971502958](https://github.com/oudream/cx-visual-x3/actions/runs/40971502958) 成功 | **软件历史专项已验证**；新master及跨专业全部对象往返仍需回归 |
| ORCH-02 / B2 | `MDCDeclarativePlanCompiler` 静态描述；`MOCManifestRuntimeBinding` 在MRK一侧校验签名和版本 | `MDCDecisionTests`、`--orchestration-vertical`；伪签名/未知能力/过期版本拒绝 | [7e8dda67 / Runtime 40971502958](https://github.com/oudream/cx-visual-x3/actions/runs/40971502958) 成功（既有垂直链） | **部分软件验证**；完整可移植纯Plan Loader、全专业版本封闭仍待验 |
| ORCH-03 / B3 | Professional Vision CPU真实算法通过唯一MRK执行并写回MDS，SourceMap回指工程 | `G06CpuVisionThroughMOC`、`RuntimeVisionDataFlowTests` | [7e8dda67 / Runtime 40971502958](https://github.com/oudream/cx-visual-x3/actions/runs/40971502958) 成功 | **Vision CPU 软件已验证**；AI、CT、Automation/Inspection真实Provider与HIL未覆盖 |
| ORCH-04 / B4 | `MRKExecutionEngine` + MDS/FCR实现并发隔离、取消、资源/异常保护 | `--orchestration-industrial`、`--runtime-data-soak 20000`；新增`ResultSummariesSeparateFailedAndSuccessfulProducts`异常隔离反例 | [3cdb6299 / Runtime 40991988104](https://github.com/oudream/cx-visual-x3/actions/runs/40991988104) 成功；新增[`db6ac7b1`](https://github.com/oudream/cx-visual-x3/commit/db6ac7b1a862c259923d72d05c6054c339e6b497)对应[Runtime #40998392632](https://github.com/oudream/cx-visual-x3/actions/runs/40998392632)成功 | **B4异常产品反例固定SHA通过**；长稳、极限并发和现场资源互斥须独立证据 |
| ORCH-05 / B5 | Studio真实WPF按钮消费`MRKFlowHandle` Trace及有界安全摘要；活跃和完成结果按ProjectId/Revision隔离 | `--shell-self-test`：活跃断点单步取消；新增「B5 活跃运行结果跨工程版本与多Flow隔离真实WPF反例」及跨工程单步拒绝 | [f5cb1084 / Studio 40994344962](https://github.com/oudream/cx-visual-x3/actions/runs/40994344962) 成功；[`11e0553a`](https://github.com/oudream/cx-visual-x3/commit/11e0553a9acdcfdc40bbe8df53a2d821a25e6852)对应[Studio #40998319052](https://github.com/oudream/cx-visual-x3/actions/runs/40998319052)成功 | **跨工程单步与双Flow查看固定SHA通过**；查看自动刷新和可视状态失效见3.3待验；完整多窗口、Trace丢失/异常与取消可观察性仍需专项 |
| ORCH-06 / B4/B6 | 多Flow的MRK身份/数据隔离、终止释放、结果来源可追溯 | `ExecutionControlTests`、`--orchestration-industrial`；失败节点不发布、其它产品结果不串账 | [a9cca000 / Runtime 40994568906](https://github.com/oudream/cx-visual-x3/actions/runs/40994568906) 成功；[`db6ac7b1`](https://github.com/oudream/cx-visual-x3/commit/db6ac7b1a862c259923d72d05c6054c339e6b497)待CI | **历史72节点及新增混合故障隔离专项各自固定SHA成功** |
| ORCH-07 / B6 | `Runtime.Host`签名发布、Journal、独立进程恢复及未知外部状态Fail-Closed | Host五次重启、未结案/未知设备状态拒绝、签名防回滚及损坏记录拒绝 | [98294ee3 / Runtime 40993920204](https://github.com/oudream/cx-visual-x3/actions/runs/40993920204) 成功 | **软件历史专项已验证**；断电、PLC状态、操作ACK的现场核实未做 |
| ORCH-08 / B6 | Windows Release/x64全解决方案、Decision/Engineering/Runtime/Studio门禁与现场HIL | `studio-shell-windows.yml`、`unified-runtime-windows.yml`；异常、取消、并发及实机矩阵 | [40994344962](https://github.com/oudream/cx-visual-x3/actions/runs/40994344962)、[40994568906](https://github.com/oudream/cx-visual-x3/actions/runs/40994568906) 各自成功 | **历史SHA及Debug/Release双配置专项成功**；最新SHA全量交叉CI及设备HIL未验证 |

### 3.2 本轮新增B5/B4真实回归与安全边界

- Studio `OnFlowInspectData` 对**正在运行**的Flow与已完成摘要使用相同的MEM ProjectId/Revision门禁。跨工程或在运行中编辑Revision后必须拒绝查看旧数据；结束异步回调不能再把旧结果发布到当前工程。
- 调试暂停/继续/单步拒绝操纵当前工程Revision之外的活跃Flow；**取消**仍允许安全终止原会话，不能为了防串账阻止危险流程的停止。
- 两条真实MRK Flow在同一WPF视图间切换时，结果只显示所选FlowId及对应提交节点；一个产品的异常节点不能暴露为已提交结果，也不能污染正常产品摘要。
- B4多产品故障隔离、B5跨工程控制保护与Trace丢失一致性已有**分别对应的固定SHA成功证据**，见3.3。后续新增自动刷新、切工程即时失效、旧Overlay清除在对应新版SHA的Windows验收前仍维持待验证；若失败须修复并重跑。
- [Studio S1 #40998299316](https://github.com/oudream/cx-visual-x3/actions/runs/40998299316) 在固定`b1e22574`已成功：全量Release/x64 Build、真实WPF STA（含跨工程版本/多Flow查看断言）、Owner依赖门禁；不覆盖之后`11e0553a`增加的跨工程单步拒绝反例。
- Runtime `4d3f519e`增加Trace历史丢失后读取已提交结果的反例；[B6 Debug/Release双配置门禁提交`40fae6d0`](https://github.com/oudream/cx-visual-x3/commit/40fae6d0fb02d1a2fa1922768513a852461df171)在既有`unified-runtime-windows.yml`内增加Debug/x64全解决方案Build和真实`--orchestration-vertical`、`--orchestration-industrial`及默认回归。该门禁已由[Runtime #40998705667](https://github.com/oudream/cx-visual-x3/actions/runs/40998705667)在`40fae6d0`固定SHA成功验证，不代表之后全部master提交均通过。
- 结果投影只包含节点ID、受控类型/数值/长度、最多32项的Studio摘要；不把大图像、体数据、敏感原文或MRK Flow句柄存入历史UI缓存。

### 3.3 B4/B5/B6 已转绿的固定SHA证据与新回归（2026-10-30）

| 工作包及能力 | 固定源码SHA | Windows Actions | 严格结论 |
|---|---|---|---|
| B4 同一引擎的故障产品/正常产品结果隔离 | [`db6ac7b1`](https://github.com/oudream/cx-visual-x3/commit/db6ac7b1a862c259923d72d05c6054c339e6b497) | [Runtime #40998392632](https://github.com/oudream/cx-visual-x3/actions/runs/40998392632) **Success** | `ExecutionControlTests`所在真实工业专项、全量Release构建通过；不是设备实机 |
| B5 跨工程暂停/单步保护、双Flow查看和WPF交互 | [`11e0553a`](https://github.com/oudream/cx-visual-x3/commit/11e0553a9acdcfdc40bbe8df53a2d821a25e6852) | [Studio #40998319052](https://github.com/oudream/cx-visual-x3/actions/runs/40998319052) **Success** | 该SHA的WPF STA及工程门禁通过 |
| B5 Trace环形缓冲丢失后结果仍取真实MRK提交值 | [`4d3f519e`](https://github.com/oudream/cx-visual-x3/commit/4d3f519e4e0d9ef3e11f080093d75edc62920878) | [Runtime #40998548123](https://github.com/oudream/cx-visual-x3/actions/runs/40998548123) **Success** | 不能补造已丢Trace，但可重读MRK状态/安全摘要 |
| B6 Debug/Release x64全量构建及工业执行回归 | [`40fae6d0`](https://github.com/oudream/cx-visual-x3/commit/40fae6d0fb02d1a2fa1922768513a852461df171) | [Runtime #40998705667](https://github.com/oudream/cx-visual-x3/actions/runs/40998705667) **Success** | Debug/Release双配置软件专项首次获得对应SHA绿灯 |
| B6 并行变更引发的完整编译红灯及后续修复 | `7fa19df` → `86a35e79` | [Studio #40998899991](https://github.com/oudream/cx-visual-x3/actions/runs/40998899991) **Failure** → [Runtime #41004371334](https://github.com/oudream/cx-visual-x3/actions/runs/41004371334)、[Decision #41004371419](https://github.com/oudream/cx-visual-x3/actions/runs/41004371419)、[Engineering #41004371335](https://github.com/oudream/cx-visual-x3/actions/runs/41004371335) **Success** | 旧版本的`TCTVolumeViewerSmoke.cs:1236`存在`roiEditCommand`未定义编译错误；后续固定SHA完整软件构建通过。并非忽略旧红灯，也不等于所有晚于该SHA的代码均已验证 |
| B5 查看后自动更新、空闲Tick节流、工程切换即时失效及旧Overlay清除 | [`1c0a42a0`](https://github.com/oudream/cx-visual-x3/commit/1c0a42a08ce10124c9bd09f1f509014d4228c986)、[`3d7ede39`](https://github.com/oudream/cx-visual-x3/commit/3d7ede391dd80e391e52de1eacec0c92899dbe3f) | [Studio S1 #41010014446](https://github.com/oudream/cx-visual-x3/actions/runs/41010014446) 固定`0a4cef4d` **Success** | 真实MRK、按钮、自动刷新与工程切换反例在后续包含这些代码的固定SHA上通过WPF STA；后续master仍须新证据 |

**B5 后续软件提交与待确认门禁：**
- [`d7cf592d`](https://github.com/oudream/cx-visual-x3/commit/d7cf592d4545d673bac56d08bc0e5de1c7d67701) 实际`RefreshScene`与MRK Trace增量观察拒绝回填过期工程；
- [`1799b6c3`](https://github.com/oudream/cx-visual-x3/commit/1799b6c3f6e1a578f42d71cecab930faaef140ca) 工程失效立即清除旧`MOLRuntimeOverlay`图元颜色，Flow收尾不再将旧工程`VisualRuntimeSource`回绑新工程；
- [`55fa2ba8`](https://github.com/oudream/cx-visual-x3/commit/55fa2ba8b9f8999616fccc63a119e877d82fa453) 异步旧Flow完成/异常不向当前工程`LogicInfo`展示旧FlowId/专业异常文本；
- [`adfd6514`](https://github.com/oudream/cx-visual-x3/commit/adfd6514c6a3b45f3fc18c7e4bd559adf05ecf27) `StudioShellSelfTests`直接使用两个真实MRK完成结果证明不同工程终态隔离；其[Studio S1 #41007593878](https://github.com/oudream/cx-visual-x3/actions/runs/41007593878)、[Engineering #41007593876](https://github.com/oudream/cx-visual-x3/actions/runs/41007593876)当时曾因并行Automation编译阻断而Failure；其后包含B5改动的[Studio #41010014446](https://github.com/oudream/cx-visual-x3/actions/runs/41010014446) 已Success。
- 以上补的是Studio既有MRK/MOL可视化生命周期；旧Flow依然由原MRK安全取消和结算，**不宣称设备联锁验证**。

**B5 代码边界：**`MainWindow.xaml.cs`仅缓存用户主动开启查看后的有界安全文本；增量更新仍取唯一MRK Trace/StepResult，空闲150ms Tick不扫描大量结果。工程Revision改变时，`RefreshScene`失效旧结果/Trace，旧Flow仍可安全取消、不制造外部设备已完成事实。应用测试为`StudioShellSelfTests`的两个B5真实MRK/WPF测试，不增建另一套调试引擎或数据中心。

### 3.4 B2/B3 关闭MEM后的静态Plan归档与真实专业CPU执行

- **已存在并验证的B2机制：** `Mechanism/Decision`的`MDCPlanArchiveCodec`实现可序列化、带摘要检查的静态Plan归档；`Mechanism/Runtime/Execution/DecisionBinding/MOCManifestRuntimeBinding.cs`的`Bind(MDCDeclarativePlan,...)`从完全独立的Plan闭包降级到同一MRK运行计划，要求Host提供完整的专业Capability版本固定，未提供/变更时拒绝。`MDCDecisionTests.DetachedPlanRuntimeLowering`在关闭MEM后覆盖V1动态Loop和V2嵌套Content、稳定SourceMap、损坏归档、缺版本和版本漂移等反例；[Decision Windows #41004371419](https://github.com/oudream/cx-visual-x3/actions/runs/41004371419)在固定`86a35e79` **Success**。
- **本轮增强（后续固定SHA软件CI已成功）：** `OrganizationLogicIntegrationTests.G06ArchivedPlanExecutesRealCpuProvider`采用真实`CxVisionThresholdProvider`执行灰度阈值与连通区域运算，复用唯一MDS具名`Pixels`端口和MRK结果；MEM关闭后重新读取静态Plan归档，检验像素真值、SourceMap及升级后的专业版本拒绝。[源码`c2e2bd95`](https://github.com/oudream/cx-visual-x3/commit/c2e2bd95aeb53f09049133731bfd7e42ca7aae14)，并由[`da403dab`](https://github.com/oudream/cx-visual-x3/commit/da403dab441227ff5aae92eb6fdd3fe2ef78887b)增强为真实临时Plan文件Write/Read/清理；首批对应[Engineering #41008186529](https://github.com/oudream/cx-visual-x3/actions/runs/41008186529)、[Decision #41008186541](https://github.com/oudream/cx-visual-x3/actions/runs/41008186541)、[Runtime #41008186566](https://github.com/oudream/cx-visual-x3/actions/runs/41008186566) 最终**Failure（Automation PLC在先的编译问题）**；后续固定`d4710675`的[Engineering #41011050353](https://github.com/oudream/cx-visual-x3/actions/runs/41011050353)、[Decision #41011050368](https://github.com/oudream/cx-visual-x3/actions/runs/41011050368)、[Runtime #41011050377](https://github.com/oudream/cx-visual-x3/actions/runs/41011050377) 均**Success**，包括`--orchestration-vertical`的真实Plan落盘/专业CPU执行和完整架构回归。
- **尚未关闭：** 跨Vision以外专业的严格CLR合同/版本闭包、真正Host进程加载此归档并实施发布授权、真实PLC/MES/采图及设备操作确认；单一软件CPU案例不能冒充以上覆盖。

### 3.5 B6 可信签名Plan激活后的真实MRK执行（2026-10-30）

- **原有已验证**：`Applications/Runtime.Host/MRHTrustedReleaseSmoke.cs`通过真实Host受信证书Pin、RSA签名、损坏Envelope拒绝、原有MRK Publisher的Stage/Activate/安全点、独立新Host实例打开持久Ledger时防重放与损坏记录拒绝；此前只检查激活后的PlanSlot，不等于已执行签名Plan。
- **本轮新增（待当前提交CI）**：[`bd622abf`](https://github.com/oudream/cx-visual-x3/commit/bd622abf4d38fcbae82948de7590deca2ea3172e)在相同测试的安全激活之后，获取`MRKExecutionPlanSlot.Capture()`的正式Lease，启动真实`MRKExecutionEngine`（F19 Worker），验证MRK FlowId、Completed、可信专业Capability的输出`9`且只执行一次；[`04f994eb`](https://github.com/oudream/cx-visual-x3/commit/04f994ebf141074735783dfee54c5085da4497eb)加入12秒运行门禁及超时安全取消，避免软件验收无界等待。这是明确测试用的确定性`CiCapability`，不伪称真实工业检测算法，真实Vision CPU测试由3.4提供。
- 此批`unified-runtime-windows.yml`的`Verify pinned Host certificate and real signed Plan publication`必须在对应SHA成功后才能登记通过；查看[Runtime #41011469736](https://github.com/oudream/cx-visual-x3/actions/runs/41011469736)在固定`bd622abf`整条Job **Success**，其`Verify pinned Host certificate and real signed Plan publication`步骤执行真实激活后MRK Flow及结果断言成功；其后包含超时限制的`04f994eb`仍须独立核对[Runtime #41011684090](https://github.com/oudream/cx-visual-x3/actions/runs/41011684090)。**即使软件成功，依旧不等于对真实PLC/运动控制器/X-Ray安全链验证通过。**

## 4. 实机验收矩阵：没有现场记录的项目维持未验证

| 现场场景 | 必须记录的证据 | 安全要求 |
|---|---|---|
| 真实采图及多探测器竞争 | 原始图像/元数据、Trigger/FlowId、时间戳、结果对应关系 | 拒绝跨产品串帧、重复触发和错误输出 |
| PLC 触发、反馈、断链、超时 | PLC 事件日志、握手序号、设备侧状态、MDS/MRK 轨迹 | 未确认动作不可自动重发或误判成功 |
| 运动控制与急停、安全门/联锁 | 控制器实际回读、硬件安全链记录、恢复前检查 | 软件停止不能替代硬件安全链 |
| MES 离线、超时、失败重试 | 请求ID、业务流水、确认ACK、结果及重试轨迹 | 未确认提交需人工/系统对账，禁止重复记账 |
| 运行中取消和任务切换 | 设备侧终态、MRK取消、资源租约释放、下一任务结果 | 对正在进行的实际动作不得伪造撤销 |
| 断电/进程崩溃与重启 | 原始 Journal、重启日志、外部实际状态、人工核对记录 | 未结案外部动作必须 Fail-Closed |
| 配置 Effective 与版本切换 | MHS Saved/Effective、Provider ReleaseVersion、签名发布记录 | 进行中任务不得混用新旧生效配置 |
| 长稳与吞吐 | 目标产线条件、批次、并发、内存/CPU/GPU、丢帧、错误率 | 由机台规格定义阈值，禁止凭空指定合格数 |

机台设备型号、驱动/SDK、PLC 协议、现场安全设计、真实异常注入权限及性能阈值由设备负责人提供并在测试记录中锁定。没有实际硬件时，只能完成**软件层验收**；不能声称实机联锁与工业生产验收通过。

## 5. 关闭规则

1. 以最新 master 的完整 CI 固定 SHA 记录所有需要的成功证据；CI 失败先修复，不用旧 SHA 成功掩盖新 SHA 失败。
2. 为每个现场场景保存执行者、设备序列/版本、日期、输入、预期、实际、日志和异常恢复结果。
3. 对真实设备相关项目保留“现场未验证”，直到设备负责人明确签署；不以模拟 Provider 的结果代替。
4. 真编排不新建第二套工程定义、运行数据、执行调度或设备控制 Owner；ReferTo 只作成熟实现复用来源。
