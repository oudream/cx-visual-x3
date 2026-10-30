# CxVisual ICS/SCADA-B HMI Studio 首轮实际实施与验收台账

> 2026-10-30。Owner：Professional.HMI、Mechanism.Presentation.Wpf、Applications.Studio；本文件仅记录真实代码和验收边界，不将CI排队或软件G1当成实机通过。

## 已提交实装

| 阶段 | 真实代码/能力 | 当前边界 |
|---|---|---|
| B1 | Professional/HMI/PHMHmiRuntimeSession.cs：MEM ProjectId/Revision与MDS Scope独立会话，三种运行/设计模式、生命周期与只读最新值 | 不重复创建MEM Page/Symbol或变量仓；完整Faceplate专业语义待做 |
| B2 | PHMHmiPointObservation：MDS Snapshot实际读数、UInt16/Int16、单位、Source/RefreshUtc/Revision、Good/Bad/Stale/Uncertain，质量降级保持最后值 | 客户端超时是显示侧质量判定，不修改MDS源质量 |
| B2 | MPWLiveSceneControl：现有MVISceneProjection+SceneView绘制Page，随MDS变化刷新只读画面与点位品质；WPF定时重评估过期状态 | 无受信Host时禁用图元控件交互；未新建Canvas |
| B3 | RequestActionAsync：从MEM既有MVIVisualEventRouter解析保存ActionId；预览/无Host/旧Revision拒绝，未知宿主结果显示Unknown且不重试 | 真正MRK认证、授权、Version及安全联锁由受信Host执行；当前无跨进程操作通道 |
| B4 | StudioShell.Hmi.cs：原Studio注册hmi.scada.live专业Viewer，Page/中央Dock/工程身份与资源导航复用；MDS Scope更换时旧Viewer关闭 | 高级设备点位编辑/绑定选择尚未完成 |
| B5 | 复用既有MPWWidgetPresenter和MVI状态/动画、Symbol，先保障真实输入来源与安全隔离 | 专业Faceplate、趋势、报警、P&ID尚未完整实现 |
| B6 | 同进程按共享合同消费A的MDS Scope；不创建协议/变量仓/命令中心 | Host远程受信IPC、PLC实机G2及HIL G3等待A的正式合同 |

## 软件验收入口

- Tests/ArchitectureSmoke/Integration/HmiRuntimeTests.cs：G1真实MDS变量、品质、过期、LatestOnly订阅、MEM ActionId与Unknown/版本拒绝。
- Tests/ArchitectureSmoke/Integration/HmiWpfTests.cs：真实WPF STA窗口、MEM Page、MDS数值变化、不污染工程Revision。
- 专项命令：ArchitectureSmoke.exe --hmi 和 --hmi-wpf。
- 工作流：.github/workflows/hmi-mds-g1-windows.yml；Windows 2022、.NET48、x64 Release，编译ArchitectureSmoke与Studio。
- 提交时上述新SHA CI仍在排队，**不得声称通过**；没有实际PLC/跨进程认证/实机操作证据。

## 未关闭验收

- Studio设备/点表配置、选点绑定、HMI控件及工业Faceplate拖拽编辑、保存重开/Undo全流程；
- PLC真实数据跨进程订阅、操作可信认证及Host SessionGeneration/EffectiveVersion联动；
- Studio关闭后独立运行客户端、真实PLC写回读、G2/HIL G3与报警历史；
- 全面WPF UIA鼠键、Symbol多实例、性能和长期运行实证。

统一工程Owner仍为MEM，运行值仍为MDS，执行/授权仍在MRK和A的Host；禁止第二套工程树、变量仓和PLC协议。

## 2026-10-30 第二批：HMI实际Page Text显示与选变量编辑

- Mechanism/Visual/MVISceneProjection.cs：普通core.text的"看到原工程文字"扩展为运行时读取原SetBinding("text")和MDS Resolver，没运行源时保留MEM原文；不在现场刷新里持久化工程。
- Applications/Studio/StudioViewModel.Engineering.cs + MainWindow.xaml：属性面板仍用原MPM/MTC，选中Text图元产生真实text属性，可选择变量绑定；可编辑ComboBox候选来自当前已附加的MDS Scope，允许未接入时继续保留手工工程绑定，切换Scope会通知候选变化。
- Applications/Studio/StudioShell.Hmi.cs：RuntimeVariableChoices只是当前MDS Definitions名称的只读投影，不保存第二份变量目录。StudioShell.Variables.cs在Scope切换后清理旧Viewer并刷新下拉。
- Tests/ArchitectureSmoke/Integration/HmiRuntimeTests.cs：真实MEM text变量绑定磁盘保存重开和MDS=99动态显示、静态原文不变；HmiWpfTests.cs额外断言WPF实时投影91和原文。
- 固定提交：动态Text 4800d17、Studio变量选择5c1f842/334ffaa/491c0c5/cea6db6、Text属性30eba84、WPF回归865400e、保存重开9776a12。
- 第一版G1 #41028059482已SUCCESS，但其SHA **未包含**本段改动。新增完整G1/Studio编译/STA仍需新SHA Windows CI通过后才可登记Automated。

## 2026-10-30 第三批：工业控件、Studio真编辑与独立Host只读Pipe联调

**真实源码（已提交，不等于相应SHA的CI通过）**

- B1/B5：Professional/HMI/PHMHmiScreenAuthoring.cs 使用原MEM Page/MTC事务生成Numeric Text、布尔状态灯、ProgressBar及默认无Action的Button；AssignSavedAction仅保存ActionId，所有现场控制仍由可信MRK Host授权。StudioShell.Hmi.cs与StudioShell.ResourceTree.cs将组件生成注册进原资源树的Page右键，候选来自现有MDS或受信Pipe真实StablePointId。
- B2：MPWLiveSceneControl 增加高频Dispatcher刷新合并、Bad/Stale画面降亮、Source/UTC时间、质量统计；仍使用原MVI+SceneView，不创建第二个Canvas。StudioShell.ResourceEditors.cs对已过工程Revision的HMI Viewer重新打开重建订阅。
- B3：PHMHmiRuntimeSession多Action点击逐项验证工程版本/取消/会话，不允许第一个Action后版本改变继续执行剩余动作；部分下发的结果是Unknown。
- B4：Studio --hmi-studio-smoke 真WPF STA测试：Page右键选真实MDS点、单MTC事务创建控件、Studio HMI Viewer、cxproj保存重开、旧Scope释放。HmiRuntimeTests补足灯状态、Undo/Redo、行为/绑定持久化和版本漂移反例。
- B6：A并行新增正式的同账户Windows只读Pipe v1，字段、格式、长度预算以DOCS/Implementation/CxVisual_ICS_SCADA_共享接口与联调契约_V1.0.md §7 为准。B新建PHMScadaReadOnlyPipeClient及PHMScadaRemoteReadSession消费Discover/Snapshot，检查DeploymentVersion、ConfigVersion、Generation、UInt16/Int16、Quality与时间；断线/版本拒绝立即撤回旧Good，不建立第二个变量仓或远程写接口。
- B6 Studio：StudioShell.Hmi.Remote.cs发现当前Host设备与有效MEM DeviceRef，选择工站打开原Page上的MPWLiveSceneControl，工程/Scope切换释放原订阅，禁用全部现场控制。
- B6协议门禁：HmiScadaPipeTests / --hmi-pipe使用真正Windows NamedPipe做v1请求/回复、点位类型/版本/品质、代次拒绝。服务端是协议测试夹具，不等于运行真实Runtime.Host。A真实双PLC TCP/Host Pipe测试另取其对应CI证据。

**验收边界**：首版软件G1 #41028059482只属于旧SHA成功。上述新代码在Windows .NET48 x64的构建、HMI Runtime/WPF/Studio/Pipe门禁与现场PLC G2/HIL G3尚不能声明通过。跨账户/远程身份与命令授权、独立客户端正式发布、P&ID/趋势/报警与完整Faceplate仍未完成。
