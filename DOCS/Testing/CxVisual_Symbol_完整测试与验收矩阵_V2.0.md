# CxVisual Symbol 体系——完整测试、反证与验收矩阵 V2.0

> 日期 2026-10-31；**测试规范（计划），不是测试结果**。源头：[Symbol #0](../Architecture/CxVisual_Symbol_00_总体架构与完整需求_V2.0.md)与 #1～#7；现场执行需引用固定 commit SHA、真实 Windows workflow/测试命令、日志及设备授权证据。
> 基准门禁：G0 源码/Schema/静态合同；G1 Headless 软件可运行；G2 Windows .NET48、WPF UIA/跨进程与规模；G3 真实专业硬件/HIL、工业安全。不同等级不得互相冒充。

## 01. 判定规则与共通场景
- **通过**：测试目的、输入、前置条件、触发动作、期望、实际输出、复现脚本、Commit SHA、责任 Owner 一致；无未解释副作用。
- **失败**：实际不满足测试断言，必须有可复制诊断、旧状态对照和最小复现；不得把一条失败测试跳过后标通过。
- **未测/环境不可用**：清楚标 NotRun/Blocked/Unknown 和原因；不能靠历史 CI 或截图代替证据。
- **软件模拟**：仅可证明相同 API 的软件语义/协议边界，不能证明真实 PLC、探测器、运动硬安全。
- **并行修改**：结果绑定唯一 SHA；跨多个 SHA 的成功不能拼成“一个版本全通过”。
- **安全测试**：主动模拟无授权、断线、超时、未知结果和错误绑定，必须无真实设备写入。

## 02. 测试分层与测试代码归属
| 层次 | 合适测试位置 | 测什么 | 不证明 |
|---|---|---|---|
| Core/MPM/MPC/MTI | `Tests/ArchitectureSmoke/Engineering/*` | 定义实例/类型/连接/事务/迁移/Undo、Schema 存储 | 真 UI 鼠标 |
| MVI/Geometry | `Tests/ArchitectureSmoke/Visual/*` | 图形/曲线、精确附着、连接路由、Group 变换 | PLC 真执行 |
| Decision/MOL/MOC/MRK | `Tests/ArchitectureSmoke/Orchestration/*` 等当前真实目录 | 只读发现、构建/端口匹配、SourceMap、软件执行 | 设备 HIL |
| Studio/TestCenter | 既有 StudioShell/Graphics WPF Tests、TestCenter 自动/手工 | Dock、Resource、UIA/键鼠/撤销/数据绑定 | 纯 UI 一项涵盖所有业务 |
| Professional | 相应 HMI/Vision/CT3D/Automation 专项 | 接口真实行为/专业数据/配置/质量/权限 | 其它专业 |
| HIL | 工厂/测试台专业验收套件 | 真实设备时序、联锁、故障恢复、物理结果 | 未接设备的 CI |

## 03. 原子测试索引（42 项）
每项都必须记录成功和反例。`SYM-T01～T32` 是 V1.0 沿用的归档编号，#1～#7 新增 OBJ/CON/REG/UI/INT/GOV/API 专题编号可作为二级标签；**不再为相同场景创建多份相互矛盾的测试真源**。

| ID | 类别 | 最小输入/操作 | 正常期望 | 必须反证/失败 |
|---|---|---|---|---|
| SYM-T01 | 注册 | 现有 IMPMTypeProvider 返回合法 Symbol Type | 注册且可检索 | 空/null/非法 kind 拒绝 |
| SYM-T02 | 注册 | 提议 RegisterSymbols 与显式 Provider 相同 Type | 单一规范描述 | 差异 Schema 拒绝 |
| SYM-T03 | 注册 | 提议 Attribute/入口函数 | 只读发现 | 未信任 DLL 不执行入口 |
| SYM-T04 | 注册 | 重复 TypeId/CapabilityId、core.* | 稳定错误 | 不覆盖旧定义 |
| SYM-T05 | 目录 | 无 Runtime Resolver 的 MOC Catalog | Described、Known=false | 不误显示设备 Ready |
| SYM-T06 | 目录 | 工程/类型/资源包三来源浏览 | 只读，Revision 不变 | 不自动 Import/RegisterTypes |
| SYM-T07 | 目录 | 一个 Symbol 关联多 Capability/反查 | 稳定显式关系 | 同名不同 ID 不误连 |
| SYM-T08 | 目录 | 第二专业加入已注册类型 | Studio 可搜/放置 | 不必改硬编码专业分支 |
| SYM-T09 | 提升 | 普通 Group/多选→Symbol | 唯一 RootGroup+实例 | 不多建资源根 |
| SYM-T10 | 提升 | 嵌套 Group 旋转/缩放/Shear | 世界位置/样式不变 | 奇异变换拒绝 |
| SYM-T11 | 提升 | 内外连线、公开映射、绑定 | 依赖有计划/修复 | 未公开/无法映射拒绝 |
| SYM-T12 | 提升 | 提升中模拟异常/Revision stale | 原子回滚一次 Undo | 不留孤 Port/半成品 |
| SYM-T13 | 实例 | 同定义双实例不同 Caption | 定义相同/覆盖隔离 | 改 A 不污染 B |
| SYM-T14 | 实例 | MotorA、MotorB 变量绑定 | 值/质量按各自实例显示 | 结果不串/不可访问私有 |
| SYM-T15 | 实例 | 删除公开成员/改变 TypeId | Impact 与兼容拒绝 | 不静默覆盖实例 |
| SYM-T16 | 存储 | Symbol/MTI/跨工程导出导入 | IdMap/资源闭包正确 | 未知 Required Aspect 拒绝 |
| SYM-T17 | 锚点 | 旧 north/east/south/west | 位置、PortId、路由保持 | 新版本不可悄悄浮动 |
| SYM-T18 | 锚点 | FixedLocal 与 FreeLocal | 精确局部点/变换一致 | 任意点不默默贴轮廓 |
| SYM-T19 | 锚点 | 圆/三角/Arc/Bezier Floating | 点落实际轮廓、法线合理 | 不以 AABB 代替 |
| SYM-T20 | 锚点 | Fixed↔Floating 混接/重连 | ID/Undo/路线稳定 | 取消不增 Revision |
| SYM-T21 | 类型 | Data/Control/Event/Visual 端口 | Kind/Direction/TypeId 校验 | 不能将视觉线当任务 |
| SYM-T22 | 类型 | 单位/坐标/时基不一致 | Unknown/Rejected 原因 | 不自动补默认换算 |
| SYM-T23 | 数据 | Good/Bad/Stale/Unknown 变化 | 状态样式正确降级 | Bad 不伪 false/0 |
| SYM-T24 | 数据 | MDS 旧 Run/数据租约失效 | 不刷新错误数据 | 无悬挂订阅/重复回调 |
| SYM-T25 | 交互 | Click → public Event → Host | 完整实例地址/参数 | 事件不直通设备 |
| SYM-T26 | 安全 | 无授权/无 Effective 配置 | Host 拒绝、设备无动作 | UI 动画不显示完成 |
| SYM-T27 | 安全 | 超时/ACK未知/断线 | Unknown/Trace | 不自动重发危险控制 |
| SYM-T28 | 脚本 | 纯规则/未信任 DLL/恶意脚本 | 只读或安全拒绝 | 不编译/执行任意输入 |
| SYM-T29 | Studio | Symbol 中心搜索/创建/双击/实例 | 同源 MEM/UI | 无多份 ResourceCenter |
| SYM-T30 | Studio | 画布连接/控制柄/键盘取消/Undo | 一次事务 | 失焦不产生额外 Edit |
| SYM-T31 | Windows | .NET48 x64 Build、Smoke/UIA/DPI | 固定 SHA 证据 | 纯 STA 不冒充 OS 注入 |
| SYM-T32 | 工业 | 六专业真实 Provider/HIL | 各专业安全签收 | 模拟数据不能充当设备结果 |
| SYM-T33 | Content | 一 Symbol 被两个不同 Content 引用 | 实例路径/作用域清晰 | 子流程同名不混淆 |
| SYM-T34 | Content | 多重嵌套 Content + Symbol 公开映射；覆盖 Symbol 自持 MRI References 的合法引用与防环 | 类型与引用链正确、逐级公开 | 私有路径访问/多级 InstancePath 直连拒绝 |
| SYM-T35 | MOC | Capability 版本锁、静态目录变更 | 旧 Stamp 拒绝 | 运行引用不会自动热更 |
| SYM-T36 | 运行 | MOL Branch/Loop/Invoke Content 含 Symbol | SourceMap/Trace 精准实例定位 | 视觉位置不改变执行顺序 |
| SYM-T37 | Vision | ROI→检测→报告/叠加 | SourceId/RunId/坐标一致 | 过期原图/ROI 不误绘 |
| SYM-T38 | CT3D | MPR 控件→专业 Viewer/VolumeRef | 坐标/体租约正确 | 无效体/错误单位拒绝 |
| SYM-T39 | Automation | 受控 PLC/运动请求 | 安全层及设备回读 | 模拟 ACK 不等于完成 |
| SYM-T40 | Simulation | Virtual/Replay/Real 模式切换 | 数据来源/时钟明确 | 无授权不切到 Real |
| SYM-T41 | 资源 | 删除/改公共 Port、版本迁移 | 提示所有受影响实例与 Flow | 升级不吞缺依赖 |
| SYM-T42 | 规模 | 大量实例/复杂 Path/目录/订阅 | 实测延迟/内存/错误率 | 卡顿/泄漏/过载有诊断 |

## 04. 黄金案例 G01～G08：贯穿链和主断言
| Case | 端到端动作 | 所属主要测试 |
|---|---|---|
| G01 纯视觉 | Page 绘制多 Shape→Group→Promote→复制/替换→不同页面引用→修改定义→Undo/Redo→重开 | T09～20/T29～30 |
| G02 HMI Motor | 已注册 Type→共享定义→A/B 变量→Bad质量→StartRequested→Host 权限→回读→状态/Trace | T01～08/T13～14/T23～27 |
| G03 Vision/Inspection | ImageRef + ROI Symbol→Content Flow→CPU Detection Capability→Result/Overlay/SourceMap | T21～24/T33～37 |
| G04 CT3D | Symbol 工具→MPR Viewer→VolumeRef/坐标→体租约释放→状态退化 | T21～24/T38 |
| G05 Automation | 工位状态 Symbol→Content/MOL→PLC Provider→ACK/Done/Unknown | T25～27/T39 |
| G06 Simulation | 真实/虚拟共享 UI Contract→Simulation Clock→状态→Real 模式门禁 | T26～28/T40 |
| G07 库与脚本 | 包只读→导入与 IdMap→纯规则预览→未信任脚本拒绝→发布版本 | T03～04/T16/T28/T41 |
| G08 跨 Content | 两层子 Content + 多 Symbol + Loop/Join→SourceMap/Run→两实例互不污染 | T13～15/T33～36 |

### 4.1 G01 具体逐步脚本（不许只拍最终截图）
1. 创建 Page，绘制旋转三角形 Path、文字和报警灯三个 Element，组合为 Group，记录每个 ElementId、Transform、Bounds、Style。
2. 将 Group 的一个数据端口与其它对象连接，制造一个外部绑定和一个内部连接，并记录 ConnectionId/PortId。
3. 打开提升预览，确认需要公开的端口/属性和引用闭包；先模拟非法映射，断言工程无 Revision/对象数变化。
4. 修正公开映射并批准“替换”；断言 RootGroup 与原视觉外观、外部连线/InstanceIdMap 合法，提交一次。
5. Undo 一次恢复原 Group/原线路，Redo 恢复 Symbol；保存重开仍可找到相同实例/端口身份。
6. 在第二个 Page 创建另一引用，设置不同 Caption/Variable；Definition 改外观，两处都反映，但覆盖/绑定互不串值。
7. 将一端改为 Outline 与另一端 Fixed，再旋转并移动目标，验证线落实际轮廓且 ConnectionId 不变。
8. 导出 MTI 包并导入另一工程，验证资源闭包/身份迁移/公开端口与外部引用策略。

### 4.2 G02 操作与禁止误判
1. 用已有 HMI Faceplate 真实 Type/定义作为金样，不用纯示意 Motor 类替代。
2. 绑定 MotorA、MotorB 不同的 MDS 变量源，分别注入 Good/Bad/Unknown 与 Start/Stop 对应的状态。
3. 点击 MotorA 发出带实例上下文的 Event；未授权条件下专业 Host 拒绝，验证两 Motor/PLC 均未发生写操作。
4. 模拟已获授权但设备超时：请求结果标 Unknown，不允许 UI 变为“Running=true”并声称成功。
5. 在真实 PLC HIL 条件下另做操作对象/联锁/回读/事故避免签收，不合并进无硬件的软件测试。

## 05. Schema/文件/事务迁移负例
- 未注册 TypeId、重复 TypeId、同名不同 Schema、非法 `core.*`、端口方向/Multiplicity 错误。
- 删除正在被 Content/实例/MOL 引用的公开 MemberId，场景错误应带具体对象/成员/来源。
- 同时存在旧 `visual.north` 和新 `FloatingOutline` 的线，保存/重开各自保持原行为。
- 导入包外部依赖策略 Reject/Pin/IncludeClosure，签名/内容不一致、缺图片/Required Aspect、离线缺 Provider。
- 复杂 Group 转换时中断、异常/Revision stale，需整体原样无残留；Undo 与 Redo 应与原工程历史兼容。
- Config Saved 但未 Effective，Symbol 允许显示设置但不可执行真实设备依赖的动作。
- 发布后有人在 Studio 修改 Definition：旧 Runtime Plan 固定原版本并要求重新发布，而不是直接热更新。

## 06. 交互和几何精度验收
- 鼠标坐标使用 DIP→World→Local，DPI 100/125/150/200%，Win10/11、缩放/多屏与焦点切换；鼠标真实路径/屏幕截图与对象几何一致。
- 端点命中：固定四向、用户定义局部点、真实轮廓自动浮动、自由点，选中 Group/实例时符号与锚点位置一致。
- 复杂几何：Bezier 二次/三次曲线、Arc 跨角和全圆、闭合/开放轮廓、多轮廓 Path、镜像、旋转/非均匀缩放、边界退化。
- 路由：Straight/Orthogonal/Manual、障碍绕行、折点新增/移动/删除、两端重连 ID、不合法端点、隐藏/锁定图元。
- 模式互斥：普通选区、几何编辑、连接编辑、插入图元、Text 原位编辑；Esc 取消短命 Ghost 不触发工程提交。
- 确认 UI 验收时要记录真实 OS 输入注入能力；`TestCenter --graphics-viewer-smoke` 只能证明其确实覆盖的 WPF 操作。

## 07. 性能/稳定性测量协议
- 固定环境：CPU/RAM/GPU、Windows 版本、DPI、.NET48、Release/Debug、资产数量/实例数/端口和连线数、后台负载。
- 场景大小：小、中、大三档由项目测试夹具定义（例如 10/100/1000 实例作为**建议测试档位而非已认可阈值**），图元包含 Group/Bezier/多线/动画/数据订阅。
- 指标：Create/Promote/Save/Open/Import/SceneProjection/HitTest/Route 的 P50/P95/P99，UI 最大卡顿，内存峰值/GC/泄漏、连接诊断错误率、事件吞吐/重连次数。
- 通过阈值由项目负责人和工业应用实测确定；没有实际数据时填 NotMeasured，不能写凭空的“<16 ms”工业承诺。
- 资源超预算必须按既有 Owner 返回可读异常/降级；运行控制命令不得因动画帧率背压静默丢弃。

## 08. CI 与证据包（统一格式）
Windows 有 `.github/workflows/testcenter-windows.yml`；其实际命令包括结构/Owner 依赖验证、`CxVisual.Unified.sln` x64 Rebuild、`--graphics-viewer-smoke`、`--ui-automation` 等。新增 Symbol 代码必须在**触达的正式测试项目**注册测试，不创造单独“空测试工程”。

```yaml
testId: SYM-Txx
caseId: G0x
commitSha: "<exact SHA>"
sourceFiles: ["..."]
windows:
  workflowUrl: "<actual URL or NotRun>"
  conclusion: "Pass|Fail|NotRun|Blocked"
  os: "Windows ..."
  config: ".NET Framework 4.8 / x64 / Debug or Release"
headless:
  command: "<exact>"
  result: "Pass|Fail|NotRun"
ui:
  injection: "OS-UIA|WPF-STA|NotRun"
  dpi: "100%/125%/150%/200%"
professional:
  mode: "None|Virtual|Replay|Real"
  device: "<if Real>"
  safetyApproval: "<if Real>"
evidence:
  logs: ["..."]
  screenshots: ["..."]
  outputDigest: "<if applicable>"
issues:
  - { code: "<error>", owner: "<owner>", reproduction: "<steps>" }
```

## 09. 阶段出口门禁
| 阶段 | 最低通过门槛 |
|---|---|
| D0 | owner/源文件/Excel 02～04/API 差异已核，现有黄金回归有固定证据 |
| D1 | G02 + 另一个专业软件级真实纵向使用，全链可追踪、命令否定路径可证明 |
| D2 | 注册/发现 T01～08/T35 通过，浏览不改 Revision，跨专业无硬编码 |
| D3 | G01 T09～16/T29～30 与无损/失败回滚成立 |
| D4 | T17～22 + 几何/兼容 UIA 成立，旧四向不变 |
| D5 | G07 规则/脚本安全及版本/Studio 全流程负例成立 |
| D6 | G01～G08 G0～G2 均有真实证据，G3 按专业实机独立签收、未测范围明确 |

## 10. 现状结论
截至本次文档提交，**本矩阵没有产生任何新 Windows/设备实测结果**。已存在的源码/历史测试只作为后续 Fixture 与回归参考；每个测试 ID 的最新状态均视作 **NotRunForThisDesign**，直到对应实现提交的固定 SHA 证据填写。
