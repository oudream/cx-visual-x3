# CxVisual TestCenter——主界面与专项人工交互实验台设计讨论 V0.1

> **位置**：`Tests/TestCenter/ManualWorkbench_主界面与专项交互实验台_设计讨论_V0.1.md`  
> **日期**：2026-10-22；**性质**：**主界面体验/布局设计草案，尚未批准实现；不是当前功能、API 或 CI 通过声明**。  
> **依托**：现有 `Tests/TestCenter/MainWindow.xaml(.cs)`、AvalonDock 5.0.1、`TCTTestDocumentHostControl`、`ITCTTestViewer`、已存在的 Channel/Geometry/Graph/Runtime 只读 Viewer。  
> **上位规范**：[Tests 总体入口](../README.md)、[TestCenter V4.1 唯一技术主规格](../../DOCS/Architecture/CxVisual_测试体系——完整规格%20V4.1.md)、[TestCenter 当前开发者 README](README.md)。

## 00. 首要设计裁决：不是多一个“测试结果 Viewer”

用户想要的“通道、规约、画图”等**专有界面**是人工参与的**真实操作实验台**，例如连接设备/虚拟服务、手动发送与接收、逐字节构造报文、单步解码、在画布中绘制并调节图元；这与现有自动测试的“选择 Case → Run → Result/Assertion/Observation → 报告”是两种不同工作方式。

| 维度 | 统一自动化测试 | 专项人工交互实验台 |
|---|---|---|
| 入口 | 自动测试 / Test Explorer | 专项交互 / Tool Explorer |
| 核心对象 | Test Definition / Case / Run / Attempt | Tool Definition / **ManualSession** / 操作与状态 |
| 主要动作 | 执行、取消、重跑、比对 | 配置、连接、操控、试验、观察、保存操作证据 |
| 中心显示 | 不可变结果与历史证据 Viewer | 活动可编辑专用工作区 / 实时数据视图 |
| 下方工具栏 | Results / Timeline / History / Diff | TX/RX / Event / Frame / Console / Diagnostics |
| 判定 | 自动断言计算，完整结果合同 | 操作状态与观察值；**不自动给 Passed** |
| 可衔接点 | 使用保存的测试环境或回放证据 | 显式录制、转为 Fixture/Case、运行正式验证 |

**定案倾向**：默认仍采用**一个 TestCenter WPF 程序、两个一级工作区、同一 Docking/Shell 技术底座**；可操作实验台不直接复用只读 Viewer 的运行对象或测试 Result 模型。只有独立运行环境的实证需求，才考虑单独进程/程序。

## 01. 主窗口：首页首先分清两条路径

首页不应直接塞入所有案例或默认只显示某个专业 Viewer。建议 UI 保持现有平台标题栏和浅色 TestCenter 风格、兼容深色 VS 中性主题。

~~~text
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│ CxVisual · TestCenter                                       目标: 本机  |  Virtual  |  帮助  ●   │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 文件   测试   专项工具   视图   诊断   报告   帮助                [浅色/深色] [保存布局]           │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [首页]     [自动化测试]     [专项交互实验台]     [报告与历史]                                        │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 首页 / 选择工作方式                                                                            │
│ ┌─────────────────────────────┐  ┌─────────────────────────────┐  ┌───────────────────────┐ │
│ │ ① 自动化测试                 │  │ ② 专项交互实验台             │  │ 最近使用 / 运行环境     │ │
│ │ 测试发现 · NUnit/TCT         │  │ 通道 · 规约 · 画图 · CT...    │  │ 目标/权限/环境         │ │
│ │ Suite / Run / 断言 / 报告    │  │ 人工操作 · 实时观察 · 复现   │  │ 最近的工具/历史入口    │ │
│ │ [打开自动化测试]             │  │ [选择专项工具]               │  │                       │ │
│ └─────────────────────────────┘  └─────────────────────────────┘  └───────────────────────┘ │
│                                                                                                │
│ 重要提示：人工操作 ≠ 自动测试通过。真实设备需要 Owner 授权、模式检查和物理安全保障。            │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
~~~

**默认行为**：

- 点击“自动化测试”：进入**现有** TestCenter Explorer/Viewer/Inspector/Results/Timeline/History/Plan/Diff 布局，原有 `RunAll / RunSelected / CLI / UIAutomation` 行为保持不变。
- 点击“专项交互实验台”：进入新的工具导航与可操作 Document Tab；不能将其误显示为只读结果。
- 从自动测试的 Observation 可通过“使用该证据打开实验台”跳入**新建或明确恢复**的独立 ManualSession，但不直接修改旧 Result。
- 可保存不同工作区的 Dock 布局/主题/打开工具列表；恢复布局**不能**自动重新连接真实设备、重发上次命令或恢复设备授权。

## 02. 专项交互工作区：单 Shell、专业画布与面板

建议 1800×1020 基准视口；较小窗口允许 Dock 面板自动隐藏、自由浮动。沿用现有 AvalonDock，不为实验台新写 Dock 框架。

~~~text
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│ CxVisual TestCenter | 专项交互实验台                                      当前环境: VIRTUAL       │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 文件  专项工具  设备与会话  视图  诊断  报告    | [首页] [自动化测试] [专项交互实验台] [历史]    │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 专用上下文 Ribbon（随 Channel / Protocol / Graphics 变化）                                    │
│ [新建会话] [连接/打开] [启动/发送] [暂停] [停止/断开] | [快照] [保存操作记录] [尝试转换为用例]   │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 专项 > 通信 > TCP Client      会话 M-...  |  Mode: VIRTUAL  |  State: CONNECTED  |  Dirty: no    │
├──────────────────┬───────────────────────────────────────────────────────────┬───────────────────┤
│ 专项工具导航      │ 文档Tab：[TCP Client ×] [Protocol ×] [Graphics ×]         │ 右侧上下文 Inspector│
│                  ├───────────────────────────────────────────────────────────┤                   │
│ 通信             │ 当前工具的“可操作”工作区                                 │ 基础参数           │
│   TCP Client     │ 参数、按钮、实时表格、Hex编辑器、画布、视口或协议树      │ 超时/目标/版本     │
│   TCP Server     │ 不同工具的布局与交互可以显著不同                          │                   │
│   UDP / Serial   │                                                           │ 运行状态           │
│ 规约             │                                                           │ 字节数/帧数/错误   │
│   Frame / Modbus │                                                           │                   │
│ 图形             │                                                           │ 权限与安全状态     │
│   Shapes/Path    │                                                           │                   │
│ Vision / CT3D... │                                                           │                   │
├──────────────────┴───────────────────────────────────────────────────────────┴───────────────────┤
│ [操作记录] [TX/RX] [事件时间线] [帧/解码] [错误/日志] [资源/性能] [保存证据]                     │
│ t0: SessionCreated | t1: Connected | t2: Tx | t3: Rx | ...                                     │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│ ● VIRTUAL CONNECTED | ManualSession M-... | 0 未处理错误 | 上次操作: RX 128 bytes               │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
~~~

### 02.1 固定框架与动态区域

| 位置 | 固定能力 | 随工具变化的部分 |
|---|---|---|
| 一级导航 | 首页/自动化/专项/报告历史 | 不随领域名称产生第七 Workbench |
| 菜单与 Ribbon | 文件、会话、诊断、主题、布局、退出/清理 | Connect/Listen/Send、Encode/Decode、绘制/变换等上下文命令 |
| 左侧 | 工具搜索、最近会话、分类、授权/目标标识 | Channel/Protocol/Graphics 等工具列表 |
| 中央 | Document Tabs、唯一 Tab/Session 身份、选中状态 | 真正的人工可操作工作界面 |
| 右侧 | 属性、参数、状态、安全、只读观察、错误 | 当前工具 Schema、协议字段、图元几何参数 |
| 底部 | 统一日志、时间线、错误、证据入口，可停靠 | TX/RX、Frame Decode、Point Inspector、GPU/体数据状态 |
| 状态栏 | Target / Mode / Connection / Dirty / Warning | 专项实时统计或异步动作状态 |

不得把 Dashboard 的“案例查看器”改成发送设备命令的宿主；上下文 Ribbon 和右侧属性随**当前活动工具 Tab**确定。Tab 切换不自动断开会话；窗口关闭应显式提示并安全释放会话。浮动窗口仍能识别所属 ManualSession。

## 03. 三种优先落地的人工实验台

### 03.1 Channel 通道实验台（人工通信调试）

**左侧工具类型**：TCP Client/Server、UDP、Serial、Web/API 等按生产 Owner 实际支持情况开放，未有适配的项目显示“暂不支持”，不可空实现冒充可运行。

**中央可操作页面建议**：

~~~text
目标地址 [127.0.0.1] 端口 [9000] 模式 [Virtual TCP] [连接] [断开]  状态 ● Connected
发送编辑器   [HEX/ASCII/UTF8] [字节序] [加载载荷] [发送一次] [循环发送(受控)] [清空]
  01 03 00 00 00 02 C4 0B
接收流       [HEX/Text] [按帧] [暂停显示] [导出] [过滤关键字]  |  接收 3 帧
时间戳                 方向      Length   Data / Notes
11:06:22.123             TX         8       01 03 ...
11:06:22.138             RX         9       01 03 ...
底部：Error / Connect / Disconnect / Timeout / Bytes TX-RX / Throughput / Latency
~~~

**人可以操作**：连接/断开、发送/接收、原始与编码文本切换、帧边界观察、模拟丢包/延迟/半包/粘包、筛选、保存复现。默认环境 Virtual/Replay；真实 PLC/设备必须单独经 Owner 安全检查。

**Owner**：调用 `Mechanism.Connectivity` 实际 Channel/Session 契约及已核实的兼容 Provider；`ReferTo/Channels` 仅为可审核的参考源码，不能视为已经集成的正式实现。跨线程收发不得直接阻塞 WPF Dispatcher，实时展示有界缓存与丢弃/背压提示。

**不能**：把“显示一条 RX 记录”自动判为 TestCenter 测试通过。

### 03.2 Protocol 规约实验台（人工编码与解码）

**中央分栏**：左侧帧编辑器（字段树、地址、功能码、长度、CRC、帧头、原始十六进制），右侧解析树（Expected/Actual、原始偏移/长度、字段值、错误定位）；可折叠展示请求和响应。

**人可以操作**：选择实际受支持的 Protocol/Codec、修改字段、单步 Encode/Decode、手动注入错 CRC、长度异常、未知功能码、半包、粘包、端序变化、响应超时；选中一个字段同步高亮原始字节；导出报文作为 Fixture 候选。

**Owner**：生产规约解释器和帧状态机，不在 UI 中复制第二份协议校验算法。其与 Channel 通道**可联动但可独立测试**：Protocol 离线解码可无需连接 Channel；联动时双方显示同一具有关联 ID 的操作与数据记录。

### 03.3 Graphics 图元绘制实验台（人工交互与几何）

**中央大画布**（不是只读 Expected/Actual Viewer）：工具箱九 Shape（Line/Rect/Circle/Ellipse/Polyline/Polygon/Arc/Bezier/Path）、选中/框选、拖动、控柄、路径节点、旋转缩放、视口缩放/网格/标尺。右侧显示图形类型、坐标系、几何参数、命中状态、编辑操作与 Undo/Redo。

**人可以操作**：绘制、移动、旋转、缩放、改控制点、层级调整、锁定、Group/Symbol、撤销重做、保存/重开测试 Fixture，观察每步变化与异常边界。

**Owner**：`Foundation.Geometry`、`Mechanism.Visual`、`Mechanism.Core` 与 `Mechanism.Presentation.Wpf` 的真实 API/投影/事务；不以测试画布取代正式 Studio 工程编辑，也不维护平行的持久化工程真源。临时画布场景与正式 MEM 工程要区分。需要保存工程时经唯一 MEM API。

### 03.4 后续专业扩展

Vision：图像、ROI、算法输入/输出、参数与处理对比；CT3D：切片/MPR/体绘/数据加载，显存和 CPU/GPU 占用；Automation：受控 IO/PLC/运动状态与命令；Inspection：人工判定/复检流程；Simulation：故障注入/重放。**先有专业 Owner 与可验证契约，再建可操作界面**，不能为了目录或 UI 图补假的产品功能。

## 04. ManualSession、自动 Run/Attempt 与数据边界

### 04.1 人工操作的会话语义（候选内部模型）

目标 Session 至少具备：`ManualSessionId`、`ToolId`、`CreatedUtc`、`TargetDescriptor`、`Mode(Replay/Virtual/Real)`、`ConnectionState`、`PermissionsSnapshot`、`OperationLog`、`LastError`、`ResourceCleanupState`。这些是**拟讨论的数据项，不是当前存在的正式类或属性**。

- 一个 Tab 可以绑定一个 ManualSession；同一工具允许多会话，但必须有明确实例身份/资源上限，不能因 UI 多开偷偷复制设备会话。
- 用户操作经 Controller/Session 转给生产 Owner；响应经可取消/有界事件队列回到界面。UI 可变参数不自动写入原工程或生产全局状态。
- 用户启动/停止、关闭 Tab、切换主题、断线/重连、目标不存在或设备权限撤销时有明确清理与审计。
- **RunId/AttemptId** 属于 TestCenter 正式测试运行；**ManualSessionId** 属于交互探索，不能互换或从界面操作时间戳推导正式通过。
- 若实验台已选 Real 模式但安全检查未通过，应显示具体拒绝原因，并禁用写操作；不能“先连接成功后补授权”。

### 04.2 实验台与 TestCenter 的合法衔接

~~~text
人工操作 → 真实 Owner 反馈 → ManualSession 记录/截图/样本/日志
                                     │ 用户显式选择“保存为候选证据/回归样例”
                                     ▼
                             校验身份、Schema、目标版本与权限
                                     │
                      Fixture/Dataset/脚本候选（非已通过）
                                     │ 审核 + 独立用例定义和断言
                                     ▼
                      标准 NUnit/TCT Case → TestCenter Run/Attempt
                                     │
                                     ▼
                    Result / Observation / History / Diff
~~~

可选“录制操作并回放”同样必须有版本、幂等/不可重复动作、外部副作用和授权门禁。**不得把人工设备动作直接录制成无人值守生产回放。** 在缺少事件/数据合同的情况下只保存有界观察日志，不创建伪造的 TCT Result。

## 05. 模块目录建议（尚未创建）

不新增测试 `csproj`；在现有 TestCenter 内增量组织：

~~~text
Tests/TestCenter/
├── Cases/                         已有：正式 NUnit/TCT 自动测试
├── Core/ Discovery/ Execution/     已有：自动化测试框架与运行
├── UI/                            已有：WPF Shell/Viewer
│   └── Manual/                    候选：人工专项视图（不与 TCTTestViewBase 混为一体）
│       ├── Home/
│       ├── Channel/
│       ├── Protocol/
│       ├── Graphics/
│       └── [未来专业目录]/
└── Manual/                        候选：人工操作会话/控制器/有界记录/接口适配
    ├── Sessions/
    ├── Evidence/
    └── [工具适配]/
~~~

**现有 `ITCTTestViewer`** 继续主要承担 Result/Observation 只读显示；人工实验台建议定义独立的工具生命周期契约（如“创建/打开/关闭会话、是否存在未保存操作、控件激活、能力校验”），具体命名、签名和项目 Owner 在编码前审查。基础日志、并发和事件仍复用现有生产/测试公共能力，不建第二个 MRK/Worker/Schema/Catalog。

特殊情况下可独立进程：原生 SDK 不兼容、位数冲突、崩溃隔离、设备独占线程模型、远程运行目标或专业 GPU 重资源。须提供可复现理由，审查进程边界、Target 授权和结果协议；**不是常规每工具独立 WPF EXE**。

## 06. 主界面状态、导航与交互细节

1. **新用户**：首页两个大入口；默认模拟/回放；“真实设备”需先经过权限/风险提示与专业 Owner 预检。
2. **打开工具**：左侧选择类别 → 创建新的、可识别的 ManualSession → 新 Document Tab → 工具上下文 Ribbon 与 Inspector 同步切换。
3. **多 Tab**：每 Tab 保有独立参数、操作记录、连接/渲染状态；标题/状态栏显示 Tool、Session、Virtual/Real、Dirty；不继承其他 Tab 的目标或权限。
4. **多工作区切换**：从“专项”回“自动化”不得改变已有 Run 的 TestResult；正在运行的人工外部动作不得隐式中止/重放；需要终止时明确提示。
5. **状态优先**：Disconnected/Connecting/Connected/Error/Closing、Busy、Canceled、Unknown 等来源于实际工具/Owner；未经确认不得将未知设备结果显示为已停止。
6. **可观测**：有界日志、数据量单位、时间戳/序列、数据来源（Real/Virtual/Replay）、线程/Dispatcher安全、诊断详情与导出。
7. **错误反馈**：无 Provider、无权限、断线、超时、非法输入、无目标、未知协议版本都应明确说明，**不能灰默忽略或制造假数据**。
8. **无障碍与分辨率**：小于 1800px 宽度时优先保留中央操作区，Inspector/日志可收起；保留键盘操作、DPI、多显示器和主题一致性。

## 07. 研发顺序与验收切片（均为建议，未授权直接开发）

| 顺序 | 增量 | 软件验收必须证明 | 待核验 |
|---|---|---|---|
| M0 | 首页双入口 + 新专项工作区骨架 | 现有自动 Case/CLI/UIA 不回退；工具 Tab/布局/主题正确 | WPF 小屏、键盘、人工视觉 |
| M1 | Virtual TCP Channel 人工台 | 参数→连接→TX/RX→断线/超时/重连、负例、日志可回放；主线程无阻塞 | Windows 同 SHA、后续真实网络环境 |
| M2 | Protocol 规约工具 | HEX 字段编辑、Codec 真正编解码、坏包/半包、与 Channel 关联 | 不支持规约的真实缺口、必要设备版本 |
| M3 | Graphics 图元操作实验台 | 真正鼠标绘制/选择/变换/Undo，真实 Geometry/Visual 反馈和保存重开反例 | DPI、复杂路径、人机签认 |
| M4 | 手工证据到自动测试的受控衔接 | 不自动变 PASS、正确绑定手工 Session 与新 Case/Attempt 身份，版本和授权守卫 | 标准化 Fixture/Golden 审核合同 |
| M5 | 其余专业工具与安全隔离 | Viewer/操作复用，真实 Owner；设备拒绝、故障清理与长稳 | 实机 HIL、外部设备安全审批 |

**每阶段四个不可替代的结果**：(1) 真实代码与接口；(2) 正常/故障回归；(3) 同提交 Windows x64 编译和 UI 自动化；(4) 仍未验证的人工/工业现场项。设计文档和效果图只处于“设计”证据层次。

## 08. 当前实现与提议的严格区别

| 项目 | 当前基线是否已有 |
|---|---|
| TestCenter WPF + AvalonDock 主窗、Explorer/DocumentHost/Inspector/Results/Timeline/History | **是**（源文件可核对） |
| TestCenter NUnit/TCT 真实执行、回放及只读 Geometry/Channel/Graph/Runtime Viewer | **是，范围依源码及 CI** |
| 一级“专项交互实验台”导航、人工操作 Workbench、ManualSession 管理 | **否，本草案提议** |
| TCP/UDP/规约人工编辑与真实连接、九图元操作实验台 | **尚未作为所述新交互工具实现/验收** |
| 人工操作 → 自动 Fixture/Case → Run/Attempt 正式审签闭环 | **目标，待审查及实施** |
| 真实设备联锁、长期性能、人工 UI 签认 | **未因本设计成立而获得验收** |

**下一步架构决策**：是否确认“一个 TestCenter Shell 的两大一级工作区”；确认后先做 M0/M1 小闭环并用实际 Windows WPF 截图与正反案例复核。提交本讨论文档不代表已经批准未来 API、目录新增或业务代码变更。