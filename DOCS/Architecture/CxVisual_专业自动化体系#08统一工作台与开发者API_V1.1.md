# CxVisual Automation——08 统一工作台与开发者API V1.1
> 版本 V1.1｜2026-10-29｜性质：目标架构/实施合同，非功能已完成声明；核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8/WPF/x64。
> 适用范围：Studio/TestCenter/嵌入式与 L0～L3 API。权威：AGENTS、开发 Skill、DOCS/00～04、F17裁决、五类模块裁决及实际源码；P01～P18编号最终逐项以二进制 DOCS/02 和 Automation能力图对照。
> 说明：PNG、XLSX 已确认仓内路径，当前工具只能读取 GitHub UTF-8 文本，无法直接解析图中条目或 XLSX Sheet；需将原件另行审查后完成逐条核对。状态【S源码】【R遗产】【T目标】【V自动证据】【H现场证据】不可混用。

**核心原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 三种入口一种能力
- **独立 Automation**：通过 Runtime.Host/Studio 的装配入口加载同一工程资源、配置与 Provider；不复制核心 DLL 的状态管理。
- **Studio**：提供 Automation Workbench 的设备树/模型库/点表配置/在线监控/受控命令/运动/采集/诊断/Trace。
- **嵌入式/开发者**：L0 原生 SDK / C#函数；L1 强类型设备Provider；L2 MOC Descriptor + M12 Command；L3 MEM Project + Decision Plan + MRK。
- **TestCenter**：实际 WPF 操作与反例，支持虚拟、回放、真实环境明确标记，TestCenter 不是另一个产品实现。
## 2. 工作区布局与交互
~~~mermaid
flowchart LR
 L["左侧: Project/Station/Device/Point树"] --> C["中央: 控制/状态/运动/采集工作页"]
 C --> R["右侧: 参数 Schema/能力/诊断/资源"]
 C --> B["底部: Trace/Alarm/Command结果/实时趋势"]
 B -. "过滤CorrelationId/DeviceRef/Generation" .-> C
~~~
建议页面：①设备/拓扑、②Connector/Driver装配、③Point/Signal监视、④PLC/IO操作、⑤Motion/Robot、⑥XRay/Camera/DAQ、⑦SCADA/IoT、⑧诊断/点检；公共 WPF 样式/布局复用 Presentation.Wpf/Studio，设备业务不在 ViewModel 实现。
## 3. 开发者使用合同
**注意：下列是便捷使用的目标伪调用样式，绝非当前可编译 API 名称，具体以现有 MOC/M12/MRK/FCM 合同对齐后决定。**
~~~csharp
// 概念调用：真实名称由编译项目已有接口决定
DeviceRef device = project.ResolveDevice("station1.plc1");
DeviceCapability read = capabilities.Resolve(device, "plc.point.read");
OperationResult result = await operations.InvokeAsync(read, request, ct);
// 写动作必须提交可信Principal、授权信息、期限和现场反馈验证策略
~~~
另提供无图形入口的 Studio/服务宿主 API、只读 Model/Point/Health Query、订阅 Snapshot、受控动作/取消、Stop/Dispose，避免使用者必须经 UI 调用硬件。
## 4. Studio工程师闭环
新建/打开项目→设备型号模板→拖入工站→添加 PLC/Connector/Driver→配置 FCM（检测重复地址、站号和版本）→导入 CSV/Excel 点表（先 dry-run）→保存→连接（Ready 探测）→观察 MDS 值和质量→受控单点写与回读→扩展 Motion/采集流程→查看 Trace/故障/结果→版本发布/安全关闭。
所有操作可撤销的范围仅限工程编辑；已发送物理命令不能按 UI Undo 反向执行！
## 5. 真实 TestCenter 当前可承接基础
`Tests/TestCenter/Manual/Module/TCTDriverReadinessPanel.cs` 已有 Driver 配置/SDK/OS 端口探针，`ProviderResolved`、`DeviceReady`、`PhysicalActionVerified` 当前返回 false；证明安全地不虚构真机成功。现有 Connector Loop Panel、公共 ModuleFrame 也可扩展；不得用“按钮可点”冒充物理闭环。
## 6. 面向工程师的反馈合同
设备层应同时显示 DeviceName/Ref、Model、Connection/Health/Generation、Value/Quality/Time、SourceId、LastSuccessfulRead、ErrorCode、配置版本；动作层明确 Requested/Accepted/Sent/Ack/Applied/Observed/Unknown，展示设备反馈、主机端异常、审计的 Trace 链；危险操作二次确认与权限控制不得只在前端校验。
## 7. 可复用 UI 合同
属性面板使用唯一 FCM Schema/FCM 校验，不维护 EditableDeviceConfig 副本；公共模块中心动态列出五类模块的 Provider/Version/Ports/CapabilityIds；点表浏览器可过滤 Device/Protocol/Quality/Tag；图表只消费 MDS Snapshot；Diagnostics Panel 应能导出脱敏运行证据。
## 8. 视觉可用性
常见任务 3 步可到操作入口；编辑/运行模式明显区别；危险动作用醒目且可读的模式警示；大点表虚拟化和分页、不同 DPI/缩放、键盘导航、快捷键、UIA 可操作性、错误消息可定位到行与字段；网络断线时不能仅用灰色外观暗示质量。
## 9. 专项验收
UX-001～014：项目打开/关闭/保存、同型号多实例切换、配置热切换门禁、点表导入撤销、连接/断开、授权/拒绝危险命令、实时值质量、超时未知操作、Motion 反馈、采集数据绑定、日志过滤、键盘和 UIA、缩放/DPI、多语言/性能和长时会话内存；真实 UIA 与模拟数据用例分开记录。


---

## V1.1｜把通用工业能力变成“真正好用”的工作台
### 10. 用户角色与三入口统一合同
| 角色 | 入口 | 关键操作体验 | 唯一真源 |
|---|---|---|---|
| 工程开发者 | C#强类型API、Provider/Capability注册、嵌入式组件 | 一行读状态、少量参数执行强类型操作、明确错误和结果 | MOC/M12/MDS/MFM |
| 工程实施人员 | Studio Automation Workbench | 拖设备→模型/点表→连线→校验→仿真→联机→现场签核→部署 | MEM/FCM/Decision/Runtime |
| 现场操作员 | HMI/Faceplate/受控面板 | 状态/告警/操作反馈、逐步引导、可追溯操作 | 同一设备/变量/命令 |
| 调试/质量人员 | TestCenter/Commissioning | 真实UIA操作、正反用例、性能/Trace/HIL证据 | 相同Provider+配置 |
| 维护人员 | Asset/Health/Recovery | 故障定位、工单、版本对比、校准/换件后复验 | 同一 AssetRef/History |
### 11. 八个专业面板：不提供空白演示页
1. **Machine/Station Navigator**：按现实资产拓扑导航，设备模型库、PhysicalAssetRef/安全域、依赖与Schema变更对比。
2. **PLC/IO & Fieldbus**：硬件机架/站号/点表编辑；监视 Value/Quality/Timestamp；在线读点、授权写点、回读与协议帧。
3. **Motion/CNC + Robot**：轴/伺服/机器人姿态与Tool Frame；曲线、Homing/Jog/定位、真实完成和跟踪误差；禁用状态可解释。
4. **Fluid/Handling**：气动/真空回路状态、压力、阀/气缸到位、输送路线、CarrierId与工站交接握手。
5. **Acquisition/Measurement**：相机/探测器/源/DAQ、触发时序、波形/帧/采样统计、Frame/Trigger关联。
6. **SCADA/Process/Recipe**：Tag监控、P&ID、PV/SP/MV、报警SOE、历史趋势；参数差异、下载与Readback。
7. **Diagnostics & Maintenance**：通讯→规约→设备→命令→物理反馈的 Trace DrillDown、故障/告警、校准/点检/备件/工单。
8. **Commissioning/Simulation/Deploy**：Real/Virtual/Replay、故障注入、FAT/SAT、资源检查、Manifest/部署回滚与证据。
全部专业面板只在 Studio/现有 Presentation.Wpf 上组合控件；Automation DLL 不增加 WPF 依赖。
### 12. 所有设备共用的“七点操作卡片”
选择任一 DeviceRef，应直观显示：①名称/型号/资产与位置②连接/Protocol/DeviceReady的区别③值+Quality+SourceTime④Capability与参数Schema⑤模式/资源/授权/联锁⑥命令各阶段及真实反馈⑦健康、版本、Trace、维护入口。操作入口根据当前权限和安全条件禁用并解释原因。
### 13. 强类型API期望——分级使用而非万能 Execute(object)
~~~text
L0 Native: 直接调用已有厂商SDK或强类型函数，必须受有权宿主管理硬件资源。
L1 Provider: DeviceRef + CapabilityId + TypedRequest/TypedResult（设备专有细节适配）。
L2 Runtime: M12 Command + MOC注册 + MRK Operation+Lease + MDS值/结果/Trace。
L3 Engineering: MEM Project + FCM Config + Decision Plan + Runtime执行与 Studio可视化。
~~~
> 这些是调用层次，不代表存在同名C#方法或可以直接复制编译。实际API签名先在最新 master 搜索现有 FQN。严禁为了便利把现有 Command/Operation/Result 各再封装一层同义接口。
### 14. 工作流体验：工程师无需读源码
`创建工程→设备型号模板→拖入工站→选择通用Connector或专有Driver→自动生成建议点表（可校验、非自动生效）→配置互锁/模式/能力→Simulation dry-run→现场连接→带权限的真实动作→Readback与Trace→FAT/SAT→部署/版本回滚`。
**配方变化**、**设备物理动作**和**Studio工程Undo**是三种独立事务；不得为“撤销”自动发送反向运动、逆向出束或隐藏的设备写入。
### 15. 跨六Workbench交互规范
- Automation提供 `DeviceRef, CapabilityRef, PointRef, TriggerId, FrameId, CoordinateFrameRef, OperationId, EvidenceRef` 的稳定身份；各Viewer或工艺只消费引用和合法数据句柄。
- Vision定位结果→Robot目标位姿时带标定/坐标系版本；CT3D接 ProjectionSeries 时带几何/触发来源；Inspection消费测试事实而非临时 UI值；HMI仅呈现Status/Quality/Command结果；Simulation不得伪装真机。
- 在工作区中选中一个工件或设备，可跨自动化、图像、CT、检测结果和Trace按同一 CorrelationId 交叉定位。
### 16. 可用性、性能与国际化
大点表 Grid 需要虚拟化/差量刷新/过滤/批量编辑、设备树万节点下响应性、UIA自动操作、多DPI与浅/深主题、中文和英文、键盘/撤销/Redo、错误跳转到字段、危险操作审核提示、网络质量差时可读的状态表达。性能阈值由 G1+G2/现场场景实测冻结。
### 17. 一键“诊断为何不能运行”
运行门禁应将 MissingProvider / Unconfigured/ProtocolDown / BadQuality / ModeDenied / InterlockUnknown / LeaseBusy / VersionDrift / CalibrationExpired / PermissionDenied 按证据链呈现，而不是一个“启动失败”弹框。对工程师给定位路径与可执行修复建议，对开发者给稳定错误码与相关Trace。
