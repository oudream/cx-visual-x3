# CxVisual 通用二维视觉工作台 V2.0——全类验收、UML反证与测试矩阵

> **测试策略与尚未执行状态**：2026-10-28制定全类质量门禁，覆盖26类/260二级能力、26张UML和8个Case。本版仅增文档，不等于260项已测试。源代码、专业SDK、现场数据与Windows CI按对应固定SHA填写证据，不允许将既往Smoke成功泛化至目标全功能。

## 1. 七级证据及状态定义（每个V2D-XX.YY独立记录）

| 级别 | 达标条件 | 不允许误报 |
|---|---|---|
| G0 需求定义 | 功能编号、用户场景、输入输出、对象和Owner明确 | 不是实现 |
| G1 设计审核 | 设计与UML、线程/生命周期/参数/异常审查通过 | 不是代码 |
| G2 源码实现 | 当前master真实编译纳入的类与使用入口存在 | 不是可用性 |
| G3 Windows编译 | 固定SHA .NET Framework4.8 x64完整相关解决方案构建 | 不是运行测试 |
| G4 自动数值/行为 | 对应功能正反例、取消/版本/资源、专项/默认回归通过 | 不是物理人机体验 |
| G5 用户操作闭环 | 真实WPF控件/UIA/API与结果/导出，人工交互按需补齐 | 不是设备/长期性能 |
| G6 专业/工业验收 | 代表数据、厂商SDK/许可证、真实相机/探测器及现场HIL、性能 | 不能仅凭模拟截图标记 |

状态必须独立记录：Spec、Review、Coded、BuildPassed、AutoPassed、UiPassed、FieldPassed、Blocked、NotApplicable（附理由）。CI BuildFailed不抹掉已存在源码，但最新高等级必须有匹配SHA和正/反日志。

## 2. 单项统一验收字段

V2D能力ID / 来源需求ID / Scene与角色 / 输入样本/格式/单位/坐标 / 参数与默认/非法 / 输出Schema / SourceRef、RunId、ArtifactRef / Owner和实际路径 / UML ID / 契约版本 / 成功值与容差 / 空结果 / 无效输入 / 异步取消/版本冲突 / 性能预算 / WPF/SDK步骤 / 负向安全/资源反证 / Windows SHA+workflow / 专业设备+环境 / 结论与复测日。

每个二级功能至少要求**一条正例、一条反例、一个用户或开发者可调用入口**；涉及耗时/资源/运行状态者补取消和内存；涉及工程持久性者补保存重开、Undo/Redo和冲突；涉及专业算法者补真实样本和同图对照。

## 3. 26类测试总矩阵（目标，未作为已通过结果）

| 一级能力 | 主要测试重点 | UML | 最小质量证据 |
|---|---|---|---|
| V2D-01 输入接入 | 格式/坏帧/大小/时间戳/Provider故障 | C01/C02/SQ01 | 文件+内存+未实现设备项明确分级 |
| V2D-02 数据模型 | 8/16/float通道、Stride、坐标、元数据 | C01/C02 | 原像素等值及非法大小反证 |
| V2D-03 序列与数据集 | 顺序、时戳、坏帧、可取消预取 | C02/ST04 | 多帧单源与多源保持身份 |
| V2D-04 大图/生命周期 | 4300²、大图峰值/多副本/Lease | C02/DP02 | Windows压力报告+泄漏检测 |
| V2D-05 显示与渲染 | 窗位/伪彩/Mask/GPU回退 | C03/P02 | 原像素不变+截图回环 |
| V2D-06 视口坐标 | Flip/Rotate/Zoom/Spacing、多DPI | C03/SQ02 | 原图↔视口↔物理误差容差 |
| V2D-07 多图比较 | 同步/Swipe/Blend/注册配准 | C03/P02 | 双Pane真实像素/SourceRef |
| V2D-08 ROI标注 | 全形状/孔洞/图层/锁/ID | C03/C08 | 数值区域真实、显示/保存一致 |
| V2D-09 编辑交互 | 端点/控制柄/四点State/Esc | C04/ST01/SQ02 | 重合拒绝/失捕/选中一致 |
| V2D-10 基础处理 | 裁剪/拼接/插值/类型/溢出 | C01/C05 | 数值真值、原图不变 |
| V2D-11 增强修复 | 滤波/参数/边界/质量 | C05 | 基准结果+异常/性能 |
| V2D-12 分割区域 | 阈值/区域生长/Mask/洞/轮廓 | C01/C05 | Gray16/复杂ROI/面积真值 |
| V2D-13 图像分析 | Stats/Histogram/Profile/频域/纹理 | C05/C07 | 准确采样与数据/图表一致 |
| V2D-14 测量计量 | 角/距/圆/亚像素/标定 | C07/SQ04 | 非等距/未标定/拟合残差 |
| V2D-15 匹配配准 | 点、变换、残差、失败判定 | C05/SQ03 | 真实配准/逆变换/容差 |
| V2D-16 工业检测 | 候选区域/分类/专业阈值/证据 | C05/C07 | 专业同样本+结果一致 |
| V2D-17 AI智能视觉 | 模型/预后处理/设备/追溯 | C05/DP02 | 现阶段只核接口，不冒充模型实测 |
| V2D-18 能力/参数 | Descriptor/Schema/TypeId/Provider | C05/C06 | 合法可发现、非法拒绝、版本正确 |
| V2D-19 算法实验 | 端口连接/中间结果/断点/重跑 | C06/ST02/SQ03 | 2—4节点真实链可复现 |
| V2D-20 批量实时 | 顺序/取消/背压/Worker/预算 | ST02/ST04/SQ06 | 长时压力、Drop策略、租约释放 |
| V2D-21 结构化结果 | 多类型Run/稳定Id/Trace/复核 | C07/SQ04 | 不同面板同一结果和错误状态 |
| V2D-22 报告交换 | 图片/CSV/JSON/PDF/Report回读 | C07/SQ04 | 字节/编码/单位/版本/安全 |
| V2D-23 工程版本 | MEM资源/编辑/Undo/Save/Open | C08/ST03/SQ05 | 冲突零提交、资源可恢复 |
| V2D-24 工作台/API | Studio/TCT/嵌入/独立/快捷键 | P02/DP01 | 真实操作/API/多DPI/UIA |
| V2D-25 跨专业 | Automation/CT3D/Inspection/HMI | P03/SQ06/DP02 | 稳定合同/版本/权限和HIL |
| V2D-26 质量治理 | Trace/资源/权限/构建/回归 | P01/DP02 | 对应SHA、长时和现场证据 |

**关键规则**：上述一级行只定义测试重点；必须从01逐一扩展为260个V2D-XX.YY条目和实际测试ID，不能因一级某个Smoke成功将其10条全部勾选。

## 4. 八个端到端Case及可观测反证

| Case ID | 可复制执行步骤 | 必须失败的反例/判据 |
|---|---|---|
| CASE-01 单图 | Gray16→ROI→分析/测量→导出 | Gray16调窗不得改变原图；坏ROI拒绝 |
| CASE-02 算法实验 | 预处理→分割→轮廓→拟合→多类型RunResult | 改参数旧Run不能回写；非法端口拒绝 |
| CASE-03 配准比较 | 两图→Transform→同步视口→差异统计 | 错尺寸/缺Transform/坐标空间误用拒绝 |
| CASE-04 序列批次 | 10/100/1000帧→Batch→汇总/错误明细 | 乱序/取消/坏帧不能错误标成功 |
| CASE-05 在线视觉 | 设备帧→队列→Vision→Inspection/HMI | 满载明确背压/Drop；掉线/恢复/安全 |
| CASE-06 X-Ray/CT投影 | 多帧投影→局部缺陷/灰度→CT3D引用 | FrameId和CalibrationRef不丢/不混 |
| CASE-07 正式工程 | ROI/Pipeline→编辑→保存→关闭→重开→Undo/发布 | 版本冲突/缺引用/权限拒绝无半提交 |
| CASE-08 AI预留 | 图像→传统增强→模型Provider合同→TypedResult | 模型缺失有明确拒绝；无实际模型不标FieldPassed |

**时间预算和性能指标**：大图、帧率、GPU和内存上限依具体客户硬件和图像样本定基线；先量化峰值、P50/P95延迟与CPU/GPU占比，不允许以虚构“60fps”宣称工业可靠。

## 5. UML正反测试要求

| UML族 | 主反证 |
|---|---|
| U01—U03 | 每个用户任务有端到端入口；TCT不作为唯一算法Owner |
| P01—P03 | csproj依赖DAG、跨专业不持Vendor/WPF类型 |
| C01—C08 | 所有权/稳定Id/深浅拷贝/ROI与结果工程边界 |
| ST01—ST04 | 无效转移、取消、重入、超时、源切换时状态清理 |
| SQ01—SQ06 | 晚到结果拒绝、Observer提交后异常、MEM原子Commit、MDS Pending Abort、背压 |
| DP01—DP02 | Studio退出独立Runtime、SDK加载/授权/设备可用性、故障回退 |

## 6. 真实Windows执行与CI登记

现有可核实旧命令：Output/Release/x64/CxVisual.Tests.ArchitectureSmoke.exe --imaging；CxVisual.TestCenter.exe --imaging-viewer-smoke（须通过Windows进程返回码、120秒内结束和截图/数值日志验证）；工作流.github/workflows/imaging-foundation-windows.yml。真实Windows.NET48 x64构建、Owner DAG、专项FIM/MVI、WPF及默认回归和TestCenter UIA为不同门禁。统一测试需保留已有行业/图元/运行回归，不可为单一绿灯把失败Step永久跳过。

**历史说明**：Imaging #40619073480 对应60992cb5，Windows构建和WPF控件Smoke成功，但默认测试出现F17.PIPE_BULK子进程退出失败，整体Job failure。TestCenter #40619073429 对应同SHA success。上述均非本轮V2.0提交的测试。代码更新后重新查对应SHA的Jobs/Steps/日志，再填写本台账；未知就写“未执行”。

## 7. 证据记录表模板（逐V2D二级功能扩展）

| V2D-ID | 用例/数据集Hash | 功能代码SHA | BuildRun | NumericTest | WPF/UIA | Performance | SDK/HIL | 结论 |
|---|---|---|---|---|---|---|---|---|
| V2D-XX.YY | 待登记 | 待实施 | 未执行 | 未执行 | 未执行 | 未执行 | 未执行 | G0（需求目标） |

结论要求标实际执行时间、图像规格和依赖版本；每次实现新增案例或更改数值语义，更新该行，不修改既往证据为新的虚构成功。

## 8. 验收执行顺序

①先审源码与Owner/ReferTo → ②针对该能力写数值/反例 → ③实现真实API → ④接WPF用户操作或无界面调用 → ⑤Windows .NET48 x64专项/全回归 → ⑥记录SHA/错误/性能 → ⑦专业同样本与设备HIL（适用时）。不能把“UML图写好”当作G2，也不能把“CI成功”当作完整260项FieldPassed。

## 9. 先行架构评审的四道Gate与反证（2026-10-28）

| Gate | 必须完成的设计证据 | 后续代码正反验收 |
|---|---|---|
| A 核心对象/Owner | 00§11和02 X01—X04；MEM/MOL/MPM/MOC/MRK/MDS唯一权威 | 不存在第二持久Graph/History/DataStack；依赖DAG合格 |
| B 数据/交互 | ImageBuffer与CxVisionGrayFrame关系、SourceRef/ROI/Viewport | Gray8/16与Stride/单位/双Pane；切图后旧结果拒绝 |
| C 算法/结果/工程 | MPM端口→MOC编译→MRK/MDS执行→RunResult→MEM显式工程提交 | 类型非法、取消/失败/空、历史Run稳定、Undo/Save/Open |
| D 批量实时/跨专业 | FrameLease/字节预算/DropNoDrop、Provider版本/权限、Inspection契约 | 帧队列压力、设备异常、SDK/GPU/内存泄漏与HIL |

**状态严格区分**：图和合同形成只意味着架构设计可审，不意味着新类型存在、编译或现场通过。本轮架构修订未执行Windows CI，也未运行新的专业算法测试。后续实际测试须逐V2D-XX.YY填SHA、样本规格、反例和运行证据。

## 10. CASE-ARCH-01 核心合同的架构级反证矩阵（新增设计；未执行）

目标：不是把260项目标一口气实现，而是先用一个Gray16增强/分割/区域/测量四节点工作链，验证三条对象契约能否支撑Studio/WPF/API/无头Runtime。以下测试编号为设计标识，不代表代码或测试用例已创建。

| 测试ID | 对象关系 | 输入/操作 | 期待结果和拒绝条件 | 后续验证方式 |
|---|---|---|---|---|
| ARCH-D01 | FIM像素格式 | Gray16有Stride Padding | 原像素不被LUT或ROI更改；Padding不计入统计 | FIM数值单测 |
| ARCH-D02 | FIM/PVC互通 | 同一Gray8、Gray16图经两个像素表达 | 每像素/阈值/区域结果等价或给明确映射说明 | 专业数值测试 |
| ARCH-D03 | Frame身份 | SourceGeneration变更后旧帧晚到 | 旧结果Stale，不覆盖新图 | 异步竞争测试 |
| ARCH-D04 | 非等距标定 | sx≠sy做距离/角度/面积 | 真实物理数值；无标定禁填mm | FGE/FIM计量测试 |
| ARCH-A01 | MOC能力可用性 | Catalog有能力描述但Provider缺失 | 保持可发现、明确Unavailable/Rejected | Catalog/Resolver测试 |
| ARCH-A02 | MPM/MRK端口映射 | 工程TypeId合法、执行CLR类型不匹配 | Compile/Run按错误阶段明确拒绝，不混用object | MOC/MRK契约测试 |
| ARCH-A03 | 参数范围 | Gray16阈值超限/NaN/非法ROI | 前置验证失败，不产生部分结果 | 参数Schema反例 |
| ARCH-A04 | 四节点执行 | 增强→Mask→Region→Measure | 具名输出、中间数据、SourceMap和Trace正确 | 真实MOC/MRK集成 |
| ARCH-A05 | 取消 | 计算中修改原图或参数、取消Token | PendingAbort，旧Run不污染新展示 | 并发/取消测试 |
| ARCH-R01 | 多类型结果 | 单Run有Image、Mask、Region、Measure、Trace | RunId/ResultItemId唯一且可复核 | 专业结果单测 |
| ARCH-R02 | Empty与失败 | 无区域与算子执行错误 | Empty成功与Failed/Cancelled分离 | 结果状态测试 |
| ARCH-R03 | 显示投影 | 结果Mask属于图A但当前选图B | 不叠错图；提示Stale/NeedsRegistration | WPF集成/UIA |
| ARCH-R04 | Run版本 | 参数重跑、旧结果再查看 | 原Run只读，新Run独立；参数与Provider版本可追踪 | 结果版本测试 |
| ARCH-R05 | 正式工程 | Overlay→Promote→Commit→Undo→Save/Open | 工程稳定ID、Revision/SourceRef和样式保持 | MEM/MRC/WPF端到端 |
| ARCH-R06 | 人工复核 | 更改缺陷复核意见 | ReviewRecord派生，原算法Result不可变 | 权限审计与结果测试 |
| ARCH-Q01 | 大图预算 | 4300² Gray16及多个中间Mask | 计入副本、CPU/GPU/SDK/队列开销；超限受控拒绝 | Windows性能/资源 |
| ARCH-Q02 | 真正连续流 | 满队列/设备掉线/重新连接 | 明确背压/Drop/NoDrop、Frame代次和Lease归还 | 回放+真实HIL |
| ARCH-Q03 | 无头与UI | 同样本独立API及MPW工作台 | 算法数值和结果身份语义一致；无头不依赖Dispatcher | SDK+WPF真实控件 |
| ARCH-Q04 | 发布和回滚 | 新旧已发布Plan/能力版本不同 | Run固定Revision、发布失败不破坏现役Plan | MOC/MRK发布测试 |
| ARCH-Q05 | 错误诊断 | 端口错误/资源过载/SDK异常 | ErrorCode、Node/Port、RunId、Trace可复查 | FLD/运行单测 |

### 10.1 评审签收格式

每个测试在实施时写：实际输入文件及Hash、运行环境、Owner/类路径、实际输出/容差、异常或性能截图/日志、Git SHA、Windows工作流链接、测试日期、是否专业设备HIL。架构P设计只能列为G0/G1，不能列G2源码或G4/G6通过。MDS预算只证明当前已提交托管载荷，不证明所有GPU/SDK工作缓存；完整相机流不能通过MDSBoundedByteStream的基本字节块测试替代。

## 11. 固定SHA实证：FIM↔Vision数据合同第一条闭环（2026-10-28）

**代码提交**：[`c424abcf004d670790a68d6c6c6976f175cf7b5b`](https://github.com/oudream/cx-visual-x3/commit/c424abcf004d670790a68d6c6c6976f175cf7b5b)。**真实CI**：[CxVisual FIM MVI Imaging Windows Acceptance #40657978967](https://github.com/oudream/cx-visual-x3/actions/runs/40657978967)，Windows 2022 / VS MSBuild / .NET Framework 4.8 x64，全job **success**。

| 关联能力/反证 | 对应实际测试行为 | 固定SHA证据级别 |
|---|---|---|
| V2D-02 / ARCH-D01：Gray8 Stride | 3×2 Gray8/Stride=5；去Padding后往返逐像素一致 | G4 自动测试通过 |
| V2D-02 / ARCH-D02：16位小端 | 2×2 Gray16/Stride=6，500/500/0/0，像素值和紧排字节正确 | G4 自动测试通过 |
| V2D-02 / 所有权 | 输入byte[]变更、CopyPixels和FDTReadOnlyBuffer外部副本变更均不污染原图 | G4 自动测试通过 |
| V2D-18/20：现有MRK集成 | ToCpuInspectionInputs→CxVisionDataFlow.CreateCpuInspectionPlan→MRK，ConfigRevision=57、GraySum=1000、两个阈值像素、一个面积2连通域 | G4 自动测试通过（仅此CPU算法） |
| V2D-02/18：错误格式 | null、GrayFloat32、Bgr24、与已编译Plan不匹配的位深均明确拒绝 | G4 自动测试通过 |
| 结构/回归 | Windows x64完整构建、Owner/DAG、二维WPF Viewer、`--imaging`、默认ArchitectureSmoke、三维WPF Viewer | 对应CI所有步骤success；并非新桥的WPF交互闭环 |

**未验证/不可提升等级**：大图/32M性能与所有工作副本峰值、完整Frame/Mask/Artifact身份模型、SourceGeneration失效/设备Lease、通用VisionRunResult、正式工程Pipeline、专业SDK/AI推理、实时相机流、独立UI算法工作流及现场HIL。现有WPF回归只证明旧图窗未退化，**不是新专业输入桥接功能已经通过WPF人工操作**。不得按整个一级类V2D-02或V2D-20统一标为G4；仅上述受覆盖的细能力/案例有效。

## 12. P1真实结果投影测试登记（2026-10-28）

> 代码实施提交[0a5e658](https://github.com/oudream/cx-visual-x3/commit/0a5e658ccf96015e6c7eea77380e873e43d59058)，[Imaging Windows #40660774027](https://github.com/oudream/cx-visual-x3/actions/runs/40660774027)已触发，**本节首次登记时CI仍排队，未记通过**。明确按固定SHA复核并更新，不将测试文件存在冒充运行成功。

| 关联V2D/反证 | 新增真实用例 `RuntimeVisionResultTests` | 预期 |
|---|---|---|
| V2D-21 / RES-01 | `RealMrkResultProjection` | 同一真实MRK FlowId生成检测摘要、阈值统计、Region、GraySum和FlowDecision五项；ItemId唯一 |
| V2D-21 / RES-02 | `IndependentRunsAndStudioProjection` | 同源重跑产生不同Run/ItemId，旧数据不受新Run影响 |
| V2D-21 / RES-03 | 所有结果项带SourceId与RunId；区域像素边界/面积 | 不继承可变`CxVisionDefectRegion`引用 |
| V2D-21 / RES-04 | `EmptyAndFailedFlow` | 无缺陷返回合法空区域摘要，损坏RAW的MRK失败不允许产生正式结果 |
| V2D-21 / RES-05 | 本次模型从MRK`GetCommittedOutput<T>`读取 | 不绕过已存在MDS/FDT提交屏障，集合只读 |
| V2D-24 / API兼容 | `FromInspection`服务MOC编译后的Studio真实StepResult | 无GraySum/Passed来源时不伪造；真实MOC指纹与Provider版本从Studio传入 |
| V2D-26 / 结构回归 | `--vision-results`、`--imaging`及默认ArchitectureSmoke、完整Windows构建及WPF | 需对应Actions结果；现场UI/性能待额外验证 |

**尚未覆盖**：Image/Mask/Contour/Measurement/Report/AI等完整多类型Run，持久ArtifactRef，自动复核审计，配准坐标转换、异步旧结果过期策略，工业设备HIL及GPU/长时稳定性。已有四类Kind不能被宣传为完整26类能力已经交付。

## 13. P2 Image/Mask真实执行/安全合同的专项登记（2026-10-28）

**代码**：[c98d933](https://github.com/oudream/cx-visual-x3/commit/c98d933bd31a688e083c88a815dae59751d8c48c)、[8d5c027](https://github.com/oudream/cx-visual-x3/commit/8d5c027e87b8360246ca5e71a91db157d2512aaf)、[9316a139](https://github.com/oudream/cx-visual-x3/commit/9316a13903a7bd69d9c1f71c9d8fe2354d408b46)。最新固定[Windows Imaging #40662918228](https://github.com/oudream/cx-visual-x3/actions/runs/40662918228) 初次登记为**queued（未执行完成）**。本章测试仅是已提交可执行回归源码，CI未有结果时不可称G4通过。

| ID/能力 | 已编码正反验收 | 当前证据限制 |
|---|---|---|
| ART-01 / V2D-02,10 | Gray8真实Preprocess回传FIM Image，数值与源图一致；输入不变 | 需Windows运行结果 |
| ART-02 / V2D-12,21 | Gray8 0/255 ThresholdMask四像素命中，Region只接受面积3的候选连通区域，阈值与面积过滤严格分义 | 需Windows运行结果 |
| ART-03 / V2D-02 | FIM Gray16带Stride输入、紧排MRK、处理图保留16位500/500，Mask为Gray8 255/255 | 需Windows运行结果 |
| ART-04 / V2D-20,21 | 原MRK API二参签名及3输出不变；Opt-in仅增加2个具名数据出口 | 需Windows编译及回归 |
| ART-05 / V2D-21 | RunId/SourceId/ItemId与类型端口一致，两个FIM Image产物可以从ResultItem访问且像素不可修改 | 需Windows结果 |
| ART-06 / V2D-26 | Invalid RAW失败无任何正式CommittedOutputs，不能冒充成功Run | 需Windows运行结果 |
| ART-07 / V2D-20 | 两个Flow共享Plan并行执行但Source/Mask/ItemId各自独立 | 需Windows运行结果 |
| ART-08 / V2D-04,26 | `MRKFlowResult.TryEstimateCommittedBytes`对FIM Image/Mask经FDT专业估算且包含有效像素载荷 | 需Windows运行结果 |
| ART-09 / V2D-24 | `--vision-results`、`--imaging`、默认ArchitectureSmoke均挂载本次测试 | WPF二维/三维CI仅证明共享回归，不代表新图像产物已在Studio或TestCenter中可视操作 |

**工业验收仍未覆盖**：单独的FrameId/FrameLease、磁盘Artifact持久化、Mask与正式ROI坐标配准、WPF可视化用户交互、AI/GPU/厂商设备HIL和长期运行内存。已有的MDS容量统计不等于这些全部满足。

**ART-10补充（源码已提交，待Windows验证）**：对应提交 [`97630512`](https://github.com/oudream/cx-visual-x3/commit/97630512a2c3d7d958d6fad325a61ab494ae6810)，新增`RuntimeVisionArtifactsTests.MviImageMaskProjection`：同一真实Gray16 MRK Run产生Gray8 Mask→经`CxVisionRunSummary`按RunId/SourceId/尺寸验证→提交现有`MVIImageSession.SetResult`→Original/Result双槽切换，原16位像素不变；载入新原图后自动清空旧结果。此测试是**MVI非WPF状态集成**，不能冒充TestCenter/Studio鼠标可视操作或Viewer级自动防错源校验；执行前上层仍须显式检查SourceId与坐标。对应[Imaging Windows #40663246695](https://github.com/oudream/cx-visual-x3/actions/runs/40663246695)，首次登记时排队，尚无通过结论。

## 14. P3 二维专业CPU图像/Mask实际WPF交互与来源安全反证（2026-10-28）

**代码已提交，当前仍待Windows执行证明**：[最新Imaging #40666425061](https://github.com/oudream/cx-visual-x3/actions/runs/40666425061)（`4f91211`），初次登记为queued。不要套用P1成功Run #40660774027的证据到P3新增WPF交互。

| ID | 实际测试/真实操作 | 通过条件 | 现阶段 |
|---|---|---|---|
| UI-VIS-01 | `TCTImageViewerSmoke.Run`真正点击`CpuVisionRun` | 使用4×4 Gray16真实MRK，处理图保留原像素1..16；Mask中10..16七个命中且候选区域面积7 | 源码测试待Windows |
| UI-VIS-02 | 输出页`CpuResultItems`实际点击Image/Mask/Region | Image恢复Gray16；Mask为Gray8/0/255；选择Region不会改写图像 | 源码测试待Windows |
| UI-VIS-03 | Mask→`ExtractMaskContours`按钮 | 使用既有FIM追踪真实边界、MVI写入PolygonAnnotation、SignedArea=7，原16位图不变 | 源码测试待Windows |
| UI-VIS-04 | 同一FIM图像对象重新载入 | 原结果槽、结果列表与本次`CpuEvidence`清空；旧Run不写回 | 源码测试待Windows |
| UI-VIS-05 | 用户换图/连续执行 | CTS取消、原图引用+SourceVersion+SourceId+Request身份多条件阻止过期提交 | 源码及测试部分覆盖，长时竞态压测待补 |
| UI-VIS-06 | 缺失标定与孔洞面积 | 无有效Spacing不得出现mm²；有Spacing时使用sx×sy乘真实有符号环面积 | 源码已实现，另需专项mm²/孔洞UI反证 |
| UI-VIS-07 | CPU原图非法类型及参数 | 原图限Gray8/16；阈值范围按位深，MinArea[1..10000000]；非法调用不发布新Run | 源码已实现，另需WPF反例确认 |
| UI-VIS-08 | 旧本轮CI failures | #40663246695有Vision CS0136；#40665129173有FTR CS0103。不能写作P3测试通过或同一个Owner失败。 | 失败根因日志已查，待对应Owners及新CI确认 |

**边界**：`TestCenter`仍是人工/自动化专业测试工作区；正式Studio可视面板、本次SourceId的硬件稳定身份、持久ArtifactRef、可恢复历史Run、几何配准、工业SDK/HIL/AI均不在上述测试通过范围内。


## 15. P3参数编辑、MVI槽生命周期与孔洞测量专项反证（2026-10-28）

**对应源码固定SHA**：[becc050](https://github.com/oudream/cx-visual-x3/commit/becc0505850493a27adf302bc37f3d64fe4ffeaf)、[a287f45](https://github.com/oudream/cx-visual-x3/commit/a287f45d2cda4a6004d3e9c9ff8250361c4333f1)、[3b15550](https://github.com/oudream/cx-visual-x3/commit/3b15550baacc72db8b06cd8678982eae9d23c497)。本节更新UI-VIS-05/06/07的**可执行源码覆盖**，不把此前“待补测试”按文字改成已执行通过。

| 用例ID / 关联能力 | 真实行为与要求 | 反向判据 | 状态 |
|---|---|---|---|
| UI-VIS-09 / V2D-16,21 | 已有7px CPU Run后把Gray16阈值改为70000，MVI应回原图、Result置空、专业Run/结果列表失效，Original引用与像素保持 | 无效输入不得启动或重用原Run；源图不变 | 自动WPF用例已编码；待CI |
| UI-VIS-10 / V2D-12,16,21 | 同原图阈值10→11，真实再次点击CPU执行，RunId必须变化、Region由7px变6px、原图Gray16的像素10不变 | 不得复用旧Image/Mask或旧RunId | 自动WPF用例已编码；待CI |
| UI-VIS-11 / V2D-20,21,26 | 新Run在途时修改阈值至12，CTS取消并清Result；Dispatcher允许晚到回调后仍无旧Evidence/结果项 | 旧异步结果不得覆盖本次当前图 | 自动WPF用例已编码；待CI及更强压力测试 |
| UI-VIS-12 / V2D-08,12,14 | 3×3真实MRK CPU环形Mask有8px命中和一个孔洞；FIM轮廓外环9px²、孔洞−1px²；FIM非等距0.2×0.4测量合计0.64mm² | 不得把带孔洞区域按外框9px²报告或修改原图/结果Mask | 自动WPF用例已编码；待CI |
| MVI-CLR-01 / V2D-05,21 | MVIImageSession.ClearResult首次清理返回true、代次+1、Observer通知一次、激活原图且保留Original | 再次调用必须返回false且不再改变代次/通知；在Original激活时同样不得清源图 | ArchitectureSmoke已编码；待CI |
| CI-VIS-09 / V2D-26 | [Imaging #40672752671](https://github.com/oudream/cx-visual-x3/actions/runs/40672752671)与[TestCenter #40672752691](https://github.com/oudream/cx-visual-x3/actions/runs/40672752691)匹配提交3b15550 | 无完整成功日志则不可登记G3/G4或G5 | 初次登记queued，G2源码 |

**特别说明**：现有面积测量按图像格点与各向异性像素间距计算，未引入畸变标定或几何配准，不能视为完整工业计量G6。WPF自动点击可用于G4/G5部分自动证据；G5人工实际体验、UIA可访问性、现场硬件和长时性能仍需单独记录。

## 16. 用户截图反证：显示参数和绘图类别真实WPF验收（2026-10-28）

本组UI专项对应源码提交 [9394e3b](https://github.com/oudream/cx-visual-x3/commit/9394e3b0d8c88bdaad4483460307daf3bb41eea5)，不得用前一批已通过构建替代。新WPF断言按真实Routed Button.Click执行，不虚构真人鼠标/设备HIL。

| 用例 | 正反行为 | 代码状态 / 验收等级 |
|---|---|---|
| UI-UX-01 / V2D-08,09 | 显示页四个分类`SelectMode.ROI/测量/标注/缺陷`点击后必须对应RectangleRoi/Distance/Point/DefectBox，边框高亮与Viewer.ToolMode一致；仍有原`OverlayColor.ROI`选中图元改色回归 | 已编码G2；WPF Windows待结果 |
| UI-UX-02 / V2D-05 | 实际点击Window按钮以1..16窗位映射Gray16样本，测量中间像素产生有限Gray8显示值且原位深像素不变；将下限改成“b”后操作必须拒绝并通过AutomationProperties报告具体输入错误，不破坏旧映射 | 已编码G2；WPF Windows待结果 |
| UI-UX-03 / V2D-05 | 点击AutoWindow进入自动灰度范围+自动对比，避免16位图实际窄灰度区间在0..65535默认窗口下视觉发黑 | 已编码G2；WPF Windows待结果 |
| UI-UX-04 / V2D-05 | 输入Gamma=2、亮度=0.1、对比度=1.5并点击ApplyDisplay，值与真实输出PNG像素必须改变；勾选反色应立即提交；非法Gamma保留旧状态并有可查询WPF错误；Reset恢复IsIdentity且滑块1/0/1 | 已编码G2；WPF Windows待结果 |
| UI-UX-05 / V2D-05,26 | MVI.SetTone一次设置所有显示参数只发一个Changed；幂等无通知；非法NaN完整拒绝且无部分状态发布，随后Reset可以恢复 | 已编码G2；ArchitectureSmoke Windows待结果 |

**匹配工作流**：[Imaging #40683606082](https://github.com/oudream/cx-visual-x3/actions/runs/40683606082)、[TestCenter #40683606116](https://github.com/oudream/cx-visual-x3/actions/runs/40683606116)；首次登记为in_progress，尚不可写G3/G4/G5通过。后续需要核实WPF像素PNG与Auto Contrast失败日志、最终用户手工完整操作、工业大图性能，不得将单张截图当作验收完成。

### 16.1 固定SHA执行结果与新Gray8反证

- `9394e3b0d8c88bdaad4483460307daf3bb41eea5`：[Imaging Windows #40683606082](https://github.com/oudream/cx-visual-x3/actions/runs/40683606082) **success**：VS/.NET48 x64全项目构建、结构与依赖验证、真实WPF ImageViewer/PNG roundtrip、FIM/MVI专项/默认回归通过。此为UI-UX-01～05在该固定SHA的实际自动运行证据；不等于人工视觉体验或工业G6。
- 同SHA [TestCenter Windows #40683606116](https://github.com/oudream/cx-visual-x3/actions/runs/40683606116) **failure**：步骤`Verify real Windows UI Automation user workflow`报Channel Connectivity `viewerPresent=False`、TX/RX/error无证据；未见本轮二维图窗编译/PNG测试阻断。须由Channel/TestCenter Owner核查，不修改或静默跳过。
- **UI-UX-06**：Gray8原图重新载入后`WindowHigh=255`而非65535，真实点按Window并导出PNG黑0白255，原图255保持；新图同步Tone/CheckBox/Slider避免已输入非法旧值继续残留。代码 [884b75c](https://github.com/oudream/cx-visual-x3/commit/884b75cc1a9e1002f7a59fecc884f1635fe0863e)、[3661cca](https://github.com/oudream/cx-visual-x3/commit/3661ccad0bcbd882499ac261bdecf97ad3440079)。对应[Imaging #40683998349](https://github.com/oudream/cx-visual-x3/actions/runs/40683998349)，初次登记in_progress，尚未提升此新增项G3/G4。

## 17. WinForms交互等价专项：WW/WL滑条、自动取窗、灰阶与选色（2026-10-28）

| 用例 | 真实正反测试约束 | 当前状态 |
|---|---|---|
| UI-UX-07 | 取消显示页重复4彩色分类和4固定色块，保留可访问工具选择及各专业图元工具；图层勾选只影响可见性 | WPF反例源码G2 |
| UI-UX-08 | Gray16 4×4样本窗宽15、窗位8.5产生原值10像素的有效灰度显示；非法数字必须拒绝、旧参数不被部分修改 | WPF与原像素反例源码G2 |
| UI-UX-09 | WPF真实窗宽/窗位滑条设置12、6并用Thumb.DragCompleted提交，实际MVI WindowWidth/Center同步；原像素保持 | WPF事件反例源码G2 |
| UI-UX-10 | 框选ROI(0..1,0..1)，真实统计1..6得到窗宽5、窗位3.5；关闭ROI后全图自动拉伸；需补多边形/锁定/大图性能 | WPF ROI专项源码G2 |
| UI-UX-11 | Gamma滑条Thumb释放后变更MVI Gamma=2.5，亮度UI 26级映射26/255，非法Gamma保留旧值；反色及复位正常；Gray8换图后滑条/窗范围无旧状态 | WPF专项源码G2 |
| UI-UX-12 | 区域页选中图元有真正Windows系统颜色对话框入口，取消不改变MVI，对象颜色/线宽/标签仍由原MVI属性流程提交 | 对话框接入源码G2，人工验收待证据 |
| CI-UX-17 | [Imaging #40687637635](https://github.com/oudream/cx-visual-x3/actions/runs/40687637635) / [TestCenter #40687637624](https://github.com/oudream/cx-visual-x3/actions/runs/40687637624)，固定SHA`e77cae8d91d14f2f54358c3c912dc93605029745`；先前92986dc的Windows失败是测试局部重名CS0128，已改源码，不等于已重新运行通过 | 初次登记queued/in_progress |

只改变展示/交互；不把Gamma、亮度、ROI自动取窗结果当成原始像素或算法Artifact。Windows截图、实际鼠标拖动和高位深大图性能仍需另证。

### 17.1 CI已执行结果补记

固定`e77cae8d91d14f2f54358c3c912dc93605029745`的 [Imaging Windows #40687637635](https://github.com/oudream/cx-visual-x3/actions/runs/40687637635) 结论**success**，Windows构建/WPF ImageViewer/png roundtrip/FIM MVI/default/3D slice全部job步骤通过；UI-UX-07～11有此固定SHA的自动测试证据，UI-UX-12原色板对话框仍需现场人工选择确认。
最新Owner修复提交[4d6c558](https://github.com/oudream/cx-visual-x3/commit/4d6c558cddf78e8c9af367e27aca731b03bf59aa)绑定ColorDialog到真实WPF窗口，触发[Imaging #40687957944](https://github.com/oudream/cx-visual-x3/actions/runs/40687957944)和[TestCenter #40687957971](https://github.com/oudream/cx-visual-x3/actions/runs/40687957971)，待实际结束后再更新此固定SHA结论。工业长时、大图P95和人工拖拉手感仍独立验收。

### 17.2 UI-UX-07/12 对应用户截图修正：第一组分类删除，第二组色板保留

- **UI-UX-07修订**：原第1组四个彩色工具类别冗余且已删除，仍须保持WPF可访问的工具ComboBox、顶部高频工具、专业ROI/测量/标注/缺陷工具，图层可见性独立。
- **UI-UX-12修订**：原第2组四图层颜色按钮不应删除；应不依赖选中对象而打开WinForms系统`ColorDialog`，取消不变，确认后改变现有同层未锁定图元与后续默认绘制，不改变明确指定的算法产物ARGB，保留原图及Id。当前选中对象单独颜色编辑仍在区域属性页。
- 测试源码 [788d5fe](https://github.com/oudream/cx-visual-x3/commit/788d5fec78111784052ef9080c72a42bcf369e1a)：WPF四入口存在、移除第一组按钮、`MPW.SetOverlayLayerColor`改变现有和后续多边形ROI且Id不变，并维持属性编辑；系统模态色板仍需现场鼠标选择作人工确认。
- 前版 [Imaging #40687957944](https://github.com/oudream/cx-visual-x3/actions/runs/40687957944) success不等于新色板四入口已过。新固定`788d5fe`对应 [Imaging #40688664400](https://github.com/oudream/cx-visual-x3/actions/runs/40688664400) / [TestCenter #40688664402](https://github.com/oudream/cx-visual-x3/actions/runs/40688664402)，初次登记queued；未经实跑不得标记G4完成。
