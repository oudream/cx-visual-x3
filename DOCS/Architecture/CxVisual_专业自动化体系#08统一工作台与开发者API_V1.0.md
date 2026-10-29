# CxVisual Automation——08 统一工作台与开发者API
> 版本 V1.0｜2026-10-29｜性质：目标架构/实施合同，非功能已完成声明；核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8/WPF/x64。
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
