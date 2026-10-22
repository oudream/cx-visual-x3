# CxVisual TestCenter——统一主界面与工作区Frame注册·完整设计与验收 V1.1

> **日期**：2026-10-22 · **范围**：`Tests/TestCenter` 的 WPF 主界面、注册导航与人工专项工作区。  
> **定位**：经合理性审查后合并原《统一主界面与工作区Frame注册——开发任务与验收 V1.0》和《ManualWorkbench 主界面与专项交互实验台设计讨论 V0.1》的**单一界面实施基线**；原稿保留在 Git 历史中，不再并行维护。  
> **权威边界**：[Tests 协作入口](../README.md) · [TestCenter 开发者使用](README.md) · [测试体系技术主规格 V4.1](../../DOCS/Architecture/CxVisual_测试体系——完整规格%20V4.1.md) · [主仓 AGENTS](../../AGENTS.md)。本文件定义界面与协作要求，**不建立第二套 Catalog、Runner、Run/Attempt、MEM 或生产 Owner**。  
> **视觉基准**：[TestCenter 主界面参考图](../../DOCS/Architecture/TestCenter/01_TestCenter_测试主界面_2560x1440.png)：**浅色、简洁、留白、细边框、信息密度适中**。图片仅供视觉参考，示例统计不代表运行证据。

## 01. 定案与合理性审查

| 议题 | 结论 | 理由 |
|---|---|---|
| 根节点 | **四根平级**：专有测试（人工操作）/自动测试（NUnit/TCT）/场景案例/结果报告；各自独立图标及总界面 | 统一导航层级、覆盖完整测试生命周期 |
| 根/分组/叶 | 根节点打开总界面；分组可展开并按需要显示概览；叶节点打开详情；**箭头仅控制展开，文字/Enter 打开或选中** | 多层级清晰，避免强制每个树节点创建页面类 |
| 自动测试 | **一个统一的自动测试子界面**，按真实 Catalog 的 F/M/P/Group/Case 切换 Scope/选中项；专业 Observation 仍由只读 Viewer 呈现 | 避免一专业一 Runner/Frame 的重复实现 |
| 专有测试 | Channel、Protocol、Graphics、CT3D 各有自己的**人工可操作页面** | 交互行为本质不同，不能用只读结果 Viewer 代替 |
| 场景与报告 | 场景可按真实定义扩展专用详情；报告采用统一索引和详情框架 | 保持真实数据与类型化扩展 |
| Shell 布局 | 左树 / 中央文档与内容 / 右操作中心 / 底部日志诊断；AvalonDock 继续复用 | 与已选视觉一致，减少第二套 Dock/导航 |
| 操作入口 | **取消顶部大 Ribbon 按钮带**；顶部保留紧凑菜单；测试执行动作集中右侧 | 减少重复按钮和拥挤。V4.1 Context Ribbon 所列命令**语义与安全预检保留**，仅改变呈现位置 |
| 原 V0.1“自动/专项双一级入口” | **取消作为另一套主导航**；统一归入左侧四根 | 旧入口与四根定案冲突；不影响自动与人工两种运行语义 |
| 人工会话/证据 | ManualSession 与正式 Run/Attempt 隔离；手工记录不能自动产生 Passed | 保持可追溯、不可误报、设备安全 |
| Frame 新项目 | **不新增 csproj**；Frame 是逻辑工作区/页面提供者，不强制等同 WPF `System.Windows.Controls.Frame` | 遵守既定 28 项目与现有 WPF/测试组织 |

**整体原则**：根页统一“组织与概览”，子页聚焦“真实操作/验证”；Frame 是页面呈现能力，**树节点 ≠ 一个 Frame 类**。此表覆盖原 V0.1/V1.0 的冲突 UI 要求；未变更 V4.1 的 Run/Attempt、Observation、Golden 和安全合同。

## 02. 整体 UI 与四根测试项目树

### 2.1 轻量四区（推荐 1800×1020 起步，Dock 可调整）

~~~text
┌─────────────────────────────────────────────────────────────────────────────┐
│ CxVisual · Platform TestCenter                 环境/主题/窗口状态           │
│ 文件  编辑  视图  测试  运行  工具  帮助        ← 紧凑菜单，无大按钮Ribbon  │
├────────────────┬─────────────────────────────────────┬──────────────────────┤
│ 测试项目       │ [首页][当前根页][当前子页] 文档Tab   │ 操作中心             │
│ [搜索/筛选]    ├─────────────────────────────────────┤ 当前Frame相关命令    │
│ ▾ 专有测试     │ 标题/路径/简短说明                  │ 运行/停止/发送/导出  │
│   ▸ 通道测试   │                                     │ 参数/权限/运行状态   │
│   ▸ 规约测试   │ 根页：简要入口/目录/最近真实记录    │ 依据Context动态变化  │
│   ▸ 图元绘制   │ 子页：真正的工具/用例/场景/报告内容 │                     │
│   ▸ CT/3D     │                                     │                     │
│ ▸ 自动测试     │                                     │                     │
│ ▸ 场景案例     │                                     │                     │
│ ▸ 结果报告     ├─────────────────────────────────────┴──────────────────────┤
│                │ [日志][测试结果][诊断][Trace][性能][资源]                  │
└────────────────┴────────────────────────────────────────────────────────────┘
状态栏：当前Frame / Session或Run / Virtual|Real / 错误数
~~~

- **参考风格**：背景白/`#F8FAFD`，细线`#D9E3EE`，标题文字`#244467`，浅蓝选中`#E6F1FE`；状态色只点缀。避免深色大底、巨卡片、多层按钮、无意义统计。保持 DPI/键盘/窗口缩放与 Dock 停靠。
- **中央**：可编辑输入/绘图/Hex/字段检查属于工作内容；**连接、运行、发送、停止、导出等主动命令集中右侧操作中心**。禁用项解释原因，不重复按钮。
- **底部**：从同一真实 Run/Attempt 或 ManualSession 过滤日志、Trace、诊断和性能数据。切 Tab/布局恢复不得自动重发设备命令或恢复真实设备授权。
- **顶部**：保留紧凑主菜单及必要标题、状态；V4.1 的命令预检、取消、重跑、Golden 审核、安全规则不随 Ribbon 消失。

### 2.2 测试项目树（注册/数据投影，不是硬编码第二工程树）

~~~text
测试项目
├─ 专有测试（人工操作） [独立根页 + 专属图标]
│  ├─ 通道测试       → 专业操作Frame（TCP/UDP等需真实Owner）
│  ├─ 规约测试       → 专业操作Frame
│  ├─ 图元绘制测试   → 专业操作Frame
│  └─ CT/3D测试      → 专业操作Frame
├─ 自动测试（NUnit/TCT） [独立根页 + 专属图标]
│  ├─ Foundation ─ Geometry / ... ─ Case
│  ├─ Mechanism  ─ ... ─ Case        → 同一个统一自动测试Frame
│  └─ Professional ─ ... ─ Case
├─ 场景案例 [独立根页 + 专属图标]
│  ├─ X-Ray检测线 / CT重建流程 / 设备通信集成（真实配置才展示）
└─ 结果报告 [独立根页 + 专属图标]
   ├─ 自动测试报告 / 人工测试报告 / 场景报告 / 历史与差异
~~~

**四根使用完全一致的缩进、箭头、字体、选中与搜索规则，但各自具有不同图标和根总界面。** 树的每一级都可按真实数据继续展开；不要求每一级独立创建 WPF 类型。未注册、无 Catalog、无报告或缺 Owner 时准确显示空态/Unsupported，不伪造已实现/已通过条目。

## 03. 注册、Tab、右侧操作中心的协作合同

### 3.1 复用已存在的注册骨架

实际源码已有 `TCTWorkspaceRegistry`、`TCTWorkspaceDescriptor`、`TCTWorkspaceNodeKind(Root/Group/Leaf)`、`TCTWorkspaceOpenPolicy`、`ITCTWorkspaceFrame` 和 `TCTWorkspaceAction`（`UI/TCTWorkspaceFrames.cs`），以及 `TCTTestExplorerControl`、`TCTTestDocumentHostControl`、`TCTMainWindow`、AvalonDock。**先补现有代码缺口，不以本文字段清单为理由重建框架。**

注册/树/打开至少满足：

1. `WorkspaceId` 唯一；`ParentId` 合法，拒绝重复、孤儿、环；支持清晰分类、排序、图标/可用性，按已存在代码进行最小扩展。
2. Root/专业操作类型/报告入口由注册提供；**自动测试 F/M/P/分组/Case 来自真实 `TCTTestCatalog` 投影**。分组或 Case 打开统一 AutoTest Frame（逻辑共用页面类），只更新 Scope、Case、Run/Attempt 的上下文；不批量注册独立 WPF 类型/Runner。
3. Tree 选中/打开 → 激活现有 DocumentHost Tab → 绑定右操作中心到当前页面可用动作；Tab 反向定位树。若节点有子项，点击箭头只展开；点击名称才打开该页。
4. 遵循单例/每会话/多实例策略；关页释放订阅与资源，切页不篡改旧 Run 结果；注册错误、缺工厂、Factory 抛异常需要可诊断提示，不能崩溃或生成幽灵 Tab。
5. WPF GUI 操作必须经现有控制/服务与生产 Owner；共享 Dispatcher 安全及有界事件缓存。**不复制 MEM 工程模型、MRK 执行器或 TestCenter Catalog/Worker/Run/Attempt。**

### 3.2 四根的“总界面 vs 子界面”

| 根节点及总界面（均独立） | 子界面策略 | 右侧操作中心 |
|---|---|---|
| **专有测试总览**：已注册工具、简要介绍、真实最近 Session | Channel/Protocol/Graphics/CT3D 各自专业交互页面 | 打开工具/创建会话；子页按实际状态出现连接/发送/撤销/保存 |
| **自动测试总览**：真实 Catalog、计划、Run 与近期结果 | **统一 AutoTest Frame**，Scope=Foundation/Mechanism/Professional/Group/Case；专业证据按需打开只读 Viewer | 运行当前/分组/计划、取消、重跑、查看结果 |
| **场景案例总览**：真实场景目录、环境、最近验证 | 各场景按真实工程能力显示流程、步骤、Trace | 打开场景、授权后开始/暂停/单步/停止、导出 |
| **结果报告总览**：报告类型、索引、真实历史/统计 | 统一报告详情框架按自动/人工/场景/比较切换，必要时专用预览 | 查询、打开、生成/导出（仅真正支持的格式）、比较/审批 |

根节点文字触发自己的总界面；例如点击“自动测试”展示 Catalog 总览，点击“Foundation/Geometry/Case”仍使用同一个统一子界面，仅筛选和选择不同。**只有专业证据显示需要不同 Viewer，不为 Geometry/Runtime/Vision 分别开发一套自动测试 UI。**

## 04. 专有测试——可操作界面和真实职责（吸收 V0.1 的有效细节）

| 子页 | 中央主要交互（输入/查看） | 右操作中心主动命令 | 真实 Owner / 禁止事项 |
|---|---|---|---|
| **Channel** | 目标、超时、发送编辑器 UTF-8/Hex、TX/RX 流、帧边界、时间/字节/错误统计；多会话按需 | 建立/断开、发送、受控循环、导入、保存证据 | `Mechanism.Connectivity` 实际通道/帧。当前有 `MCCVirtualChannel` 点对点，不等于 TCP Client/PLC |
| **Protocol** | 字段树、地址/功能码/长度/CRC、原始 Hex 与解析字段联动、请求/响应、异常定位 | Encode、Decode、CRC 检查、错包/半包/粘包注入、关联通道、保存样本 | 生产 Codec/协议状态机，不能在 UI 复制规则。离线 Decode 可不依赖连接 |
| **Graphics** | 大画布、选择/多选、九类图元及控制点、拖动/旋转/缩放、网格/坐标、Undo/Redo 状态 | 新建/清空、Undo/Redo、保存临时 Fixture、导出操作证据 | 真实 Geometry/Visual/Presentation/事务；不在 TestCenter 新建持久工程真源 |
| **CT/3D** | 真实体数据、三方向 MPR/切片、体绘、窗宽窗位、十字线/坐标、帧率与内存信息 | 加载/取消/释放、重置/渲染方式/采集性能/导出 | 复用现有专业 Viewer/Owner，未有真实数据或 Provider 显示 Unsupported/NoEvidence |

后续 Vision/Automation/Inspection/Simulation 专业实验台同理：**先有真实 Owner、数据契约和安全约束，再注册可操作页面**。`ReferTo/` 仅作为待审核参考，不直接标成已集成能力。

### 4.1 ManualSession 不等于自动测试 Run

目标人工会话记录至少包含 `ManualSessionId`、`ToolId`、`TargetDescriptor`、`Mode(Replay/Virtual/Real)`、`ConnectionState`、`OperationLog`、`LastError` 和资源清理状态；**这些是职责建议，除已存在的 `ManualSessionId` 等实际源码外，不声称完整模型已实现**。

- 一个操作 Tab 可持一个会话，多个会话必须隔离目标、参数、操作记录和资源。关闭/断线/失败/权限变更后正确停止订阅并清理；不可因界面关闭默认宣布设备已经安全停机。
- GUI 命令经受控 Controller/真实生产 Owner，异步不可阻塞 Dispatcher；日志有界、带来源/时间/关联身份。未知连接/设备状态显示 Unknown，不以颜色推断。
- **ManualSession 操作完成 ≠ Passed**；人工证据先作为候选记录/Fixture，经显式校验、审核及定义断言后，才可由既有 NUnit/TCT Case 产生新 Run/Attempt。旧结果、Golden 与历史 SHA 不受 UI 编辑影响。
- 实机 **Real** 模式默认禁止主动写命令，必须由设备 Owner 明确授权、权限/目标/模式预检及物理联锁保证。Worker Kill/窗口停止不是物理安全停止；禁止自动回放非幂等设备动作。
- 需要专用进程时须证明 SDK/位数冲突、崩溃隔离、设备线程约束、独立部署或 GPU 大资源原因；**不按每个专业新建一个项目**，优先保持统一 TestCenter Shell 与证据入口。

## 05. 源码现状、缺口与合理性风险

> 此处是**2026-10-22 对实际源码的一次核查快照**，不是持续同步的 CI 看板；开始下一轮编码需重新获取 HEAD 和同 SHA 结果。

| 事项 | 核查结论 |
|---|---|
| TestCenter WPF Shell | 已有 `TCTMainWindow`、AvalonDock、Explorer、DocumentHost、Inspector、Result/History/Diff |
| 四根和 Frame 注册 | `TCTWorkspaceRegistry.CreateDefaults` 已登记四根、部分专有子项、报告入口，并由主窗 `BindWorkspaces`/`OpenRegisteredWorkspace` 接入；**部分项 Factory 缺失显示 Unsupported** |
| 右侧操作中心/顶部 | `MainWindow.xaml` 顶部大按钮区已改为零高度，右侧已有 Auto/Manual ActionHost；视觉 DPI/人工签认仍需验证 |
| 人工 Channel | `TCTManualChannelFrame` 使用真实 `MCCVirtualChannel` + `MPALengthFrameCodec`，支持 Virtual P2P 基本连接/收发/断开；**非真实 TCP/UDP/Serial**，不等于设备 HIL |
| 自动测试统一子 Frame | 既有 Catalog、自动测试 Viewer/测试执行已存在；**按 F/M/P Scope/Case 复用统一 AutoTestFrame 的完整树/Tab 合同仍需核验和实现** |
| Protocol/Graphics/CT3D 人工操作 | 注册入口不等于真实功能；缺 Factory/Owner 时不得声称通过 |
| 报告/场景 | 使用已有 Run/Attempt/History；场景目录与报告人工证据衔接需逐项核验，不允许凭设计图生成假数据 |
| 构建验收阻断 | 同日 `d5ce93d2` 的 TestCenter Windows、Config L1 CI 构建失败：`Cases/TCTWorkspaceRegistrationCases.cs(11,10)` 中 `TCTTestAttribute` / `TCTTest` 无法解析（`CS0246`）；最新 HEAD 需另核是否已修复，不把历史失败宣称当前必然失败 |

**特别纠偏**：原 V0.1 的“尚未实现任何人工操作页”已过时；原 V1.0 所写“Virtual TCP”已被真实源码证明不准确。**这两句均不继承**。V4.1 的 Context Ribbon 命令属行为合同，转移到右侧只改呈现、不删功能。

## 06. 实施与验收（单一任务清单）

### 6.1 页面清单：四根各一张 + 代表子页各一张 + 三张专有扩展，共 11 个

| 编号 | 对象 | 必须证明 |
|---|---|---|
| UI-01/02 | 专有测试根页 / Channel 子页 | 注册导航、右侧可控动作、真实 Virtual TX/RX、失败清理 |
| UI-03/04 | 自动测试根页 / F/M/P 任意真实 Case 子页 | **同一 AutoTestFrame** 切换 Scope/Case；真实 Assertion/Attempt/Viewer |
| UI-05/06 | 场景案例根页 / X-Ray 检测线子页 | 场景定义真实、权限与运行状态一致；无 Owner 即 Unsupported |
| UI-07/08 | 结果报告根页 / 自动测试报告子页 | 真 Run/Attempt 汇总、历史/差异、导出不篡改旧结果 |
| UI-09 | Protocol 人工页 | 原始报文、解析字段/CRC、错误注入，连接可选 |
| UI-10 | Graphics 人工页 | 鼠标绘制/控柄/Undo/Redo、真实 Geometry 反馈 |
| UI-11 | CT/3D 人工页 | 真实体数据、MPR/3D 渲染、资源释放/错误路径 |

统一屏幕样式：四区布局、无顶部大按钮条、左树树规一致、各根独立图标、右侧随上下文切换、底部日志。**设计图/拟态图必须标注设计；验收截图必须由真实 Windows WPF 程序捕获并附 SHA/环境。**

### 6.2 逐轮推进（每轮可独立验证）

1. **R0 源码+基线**：复核当前 HEAD、四根/树/Frame 和 CI；处理已知编译阻断，再谈完整 UI 通过。
2. **R1 统一壳与注册**：完善树的展开/文字打开、不同图标/总页、搜索/Tab 回选、右侧操作/布局/主题。
3. **R2 自动测试统一**：将 F/M/P/Group/Case 路由到统一 AutoTest Frame，复用 Catalog/Worker 与只读证据 Viewer。
4. **R3 人工试验**：以已有 Virtual Channel 做错误路径与资源清理，再按真实 Owner 实现 Protocol/Graphics/CT3D。
5. **R4 场景/报告**：按真实数据接入，人工 Session 到正式 Case 的证据升级需有审核。
6. **R5 Windows 验收**：同 SHA 编译、CLI、`--ui-smoke`、`--ui-automation`、人工界面对图和可用性、必要时 HIL 分别记录。

### 6.3 不可省略的正反验证

- **树注册**：四根各有不同 IconKey/根页，缩进与展开行为一致；重复 ID/孤儿/环/无 Factory/构造异常给出诊断；多级节点、搜索、Tab 双向同步与关页资源释放正确。
- **自动测试**：不同 F/M/P 分组切换时仍是同一 AutoTest UI 类型；Run/Attempt/断言/历史准确，不伪造覆盖率与 PASS。单例/多会话策略可核对。
- **人工通道**：Virtual P2P 正常打开/收发/断开；未连接发送、断线/错误/超时、重复连接、关 Tab 清理、UI 线程不阻塞；不误报 TCP/实机通过。
- **报告与场景**：无数据展示 NoEvidence/Unsupported；结果不可变、原始身份可追溯；未授权 Real 动作必须被拒绝，Worker 退出不等于联锁停机。
- **窗口**：浅色 100%/125%/150%/200% DPI、键盘、鼠标、AvalonDock 停靠/浮窗/布局保存恢复、长日志有界/性能及多 Tab 切换；顶部无重复命令。
- **交付证据**：列出代码变更、真实命令及退出码、Windows 同 SHA CI、实际 WPF 截图/人工签认、未实现和外部环境门禁；提交中文 Commit，默认 `master`。

**实现状态严守四类**：设计要求 ≠ 源码存在 ≠ Windows 自动测试通过 ≠ 人工/设备验收通过。**本文为设计与验收合并文档，本次仅编辑文档，不修改源代码或修复 CI。**
