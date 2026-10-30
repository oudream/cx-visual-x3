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

## 2026-10-30 第四批：只读Pipe真实兼容修复、Faceplate双实例、MHS部署可视配置

### 已提交实装

1. **Pipe v1修正**：Professional/HMI/PHMScadaReadOnlyPipeClient.cs 删除 .NET Framework 4.8 NamedPipeClientStream 上无效的ReadTimeout/WriteTimeout设置；连接后对整个IO引入3秒关闭句柄的截止；HasValue=false时RefreshUtc保留未定义（DateTime默认），不将本机时钟当PLC时间。Tests/ArchitectureSmoke/Integration/HmiScadaPipeTests.cs增加真实OS NamedPipe已连接但不回包的截止测试、Host首采样前null字段测试。
2. **Studio实时选点同步**：StudioShell.Hmi.Remote.cs 在接到Host首帧和后续快照时，经WPF Dispatcher通知现有MPM变量选择器，工程切换/释放时注销回调，不建立可写变量仓。
3. **工业Faceplate**：Professional/HMI/PHMFaceplateAuthoring.cs 使用真实MEM Symbol + PublicMapping(Online→visible) + MRI实例SetOverride，每个MDS设备持有独立Boolean Binding。外框/灰色失联底灯/绿色运行覆盖和实例名称均为现有MRC/MEM对象，MTC一次事务创建，复用MEM保存、Undo/Redo。Studio资源树Page右键对本机MDS和独立Host点位都可放置Motor/Valve基本Faceplate。
4. **FailClosed MVI**：Mechanism/Visual/MVISceneProjection.cs 对明确存在PublicMapping的运行态visible实例映射，当外部运行值因Bad/Stale/断线缺失时返回false，不再按普通静态图元的默认visible=true把运行绿灯显示为正常；普通未映射图元与设计态仍保持原行为。ArchitectureSmoke HmiRuntimeTests真实验证一份Symbol/两个设备实例、各自Good/Bad互不串灯、保存重开独立绑定。
5. **SCADA正式部署编辑**：StudioShell.cs 注册A已有PADScadaDeploymentConfiguration进原MHS Schema配置中心；工程Page的HMI菜单增加直达doc.config。新增选择实际MHS存储根目录的WPF入口，以与独立Runtime.Host启动参数mhsRoot一致；更换目录前必须安全关闭原Config文档，未保存草稿拒绝切换。StudioShellSelfTests.RunHmiOnly增加真实Faceplate右键、SCADA配置Schema UI和配置根保护验收；历史“4卡”固定断言按新实际“5卡”调整。
6. 未创建新的PLC协议、MDS仓库、命令中心、Canvas或持久项目对象层；工业电机/阀门为MEM已有Symbol组成的初级Faceplate，不宣称P&ID/历史趋势/报警已完成。

### 验收实情与边界

- GitHub上新SHA的HMI Windows G1、工程综合CI、Studio WPF STA工作流查询时仍排队；**不能声明本批已经编译或回归测试通过**。本运行环境无Windows/.NET48 MSBuild与物理PLC，故未执行本机真正编译/设备测试。
- 正式SCADA工程配置仍遵守MHS Pending/Effective/ACL与唯一Host root；Studio不能自行生效未授权草稿。Pipe只读、同Windows账户，没有命令认证/实机写权限；HMI当前按钮必须保持禁控。
- 未关闭：远程授权Command Schema/PLC回读/UIA实机G2/HIL G3、完整仪表/Valve动画/P&ID/趋势/报警、生产级可安装独立HMI运行客户端。

## 2026-10-30 第五批：Widget属性真编辑与CI队列优化

- Applications/Studio/StudioViewModel.Engineering.cs：原MPM属性窗可选择和应用现有Widget的content/tooltip/enabled、Progress/Slider的minimum/maximum。Literal和Variable仍使用同一SetBinding事务；minimum>=maximum或maximum<=minimum的非法常量更改拒绝，不能污染工程Revision/Undo。不是新HMI控件Schema。
- Applications/Studio/StudioShellSelfTests.cs：真实WPF STA通过Page菜单添加进度条、在原属性面板修改最大值、检测原MEM绑定已保存、再提交非法量程必须拒绝且Revision不变。
- .github/workflows/hmi-mds-g1-windows.yml：增加master工作流concurrency及cancel-in-progress，减少并行频繁提交产生的重复CI排队，仅最新版HMI G1作为完整当前批证据；Studio综合/工程综合CI保留各自独立证据边界。
- 最新一次验收核查：HMI G1新SHA与Studio S1真实Windows运行仍为queued；此前旧SHA通过记录不能涵盖新增的Pipe、Faceplate、MHS和Widget属性源码。禁止声称本批已验收或实机PLC操作可用。

## 2026-10-30 第六批：受信操作入口的真实WPF Widget事件和结果显示

- `Mechanism/Presentation.Wpf/Visual/MPWLiveSceneControl.cs`：保留原六参默认只读构造；新增显式受信操作委托，只有注入时才允许原SceneView真实Widget点击。委托是UI适配，不承载MRK命令、PLC协议、凭据或新运行仓。
- 每次Widget事件均校验原MEM工程Revision、Page、完整MRI实例地址；同一视图操作未结算前不重复发，宿主异常/无结果显示Unknown，不自动重试，也不把回读当作机械动作到位。页面销毁取消生命周期Token、解除事件订阅；已有只读Pipe始终调用旧构造，不能控制设备。
- `Tests/ArchitectureSmoke/Integration/HmiWpfTests.cs`：实际STA Window+原MEM Button，经`PHMHmiScreenAuthoring`原子保存ActionId；分别验证默认预览禁控、显式受信委托的Click→`PHMHmiRuntimeSession.RequestActionAsync`→已保存ActionId→Unknown反馈、MEM Revision不受影响。测试委托只返回软件回执，不是PLC现场执行。
- 验收状态：本批提交后的Windows `hmi-mds-g1-windows.yml` 及真实HMI STA执行仍需以固定SHA工作流结果为准；远程认证命令服务未交付，Runtime.Host只读Pipe不开放写控制。

## 2026-10-30 第七批：Unknown/Accepted旧会话重复操作门禁

- `MPWLiveSceneControl` 收到受信宿主`Unknown`或仍只是`Accepted`的状态后，关闭本画面的Widget点击权限，显示现场状态待核实，不能在旧上下文快速重复控制。没有自动清除门禁；需重新验证身份/设备真实状态后建立新的受信运行会话。
- `HmiWpfTests` 新增真实WPF同一Button第二次Click的负面验证：首次Host回执Unknown以后，第二次不会再次调用受信宿主，且工程Revision不变。
- 本门禁是HMI前端的辅助Fail-Closed；Host仍必须以自身认证、幂等操作号、版本和联锁最终裁决，不可将UI锁定冒充硬件安全或跨进程授权。

## 2026-10-30 第八批：独立Host单工站换代自动只读恢复与品质唯一口径

- `Professional/HMI/PHMScadaReadOnlyPipeClient.cs` 为Host Snapshot提供保留原始`HostQuality`的只读显示降级：Good超时/未来时间/零时效按本地展示策略降Stale，未采样降Uncertain；原MDS时间/Revision/Source不变。
- `PHMScadaRemoteReadSession.Points()` 提供当前时刻的只读显示投影，`Resolve()`共用`EffectiveQuality`；Studio点表不再硬编码另一份5秒判定。
- A的`--recover-device`旧Generation被拒绝时，只在真实Pipe再Discover发现同DeviceRef/Scope/DeploymentVersion/ConfigVersion、不同Generation的设备后重建**只读**观察。旧Good先撤销，新Generation下一轮才采样；版本漂移/跨设备不会自动采纳，不能恢复操作授权。
- `HmiScadaPipeTests`增加Quality源值不变/时效降级/未来时间反证，以及真实Windows Pipe旧Generation拒绝→重新发现→新快照恢复G1测试。未实施跨进程认证控制或PLC G2/HIL G3。

## 2026-10-30 第九批：先撤回旧Good，再执行可能阻塞的重发现

- `PHMScadaRemoteReadSession.Poll` 在Host拒绝旧Generation或返回异常时先清除最新只读帧、通知所有仍存活的WPF观察者，然后才执行`Discover`；新Generation被确认后再次通知但**不提前恢复Good**，必须等待下一轮新Snapshot。
- `HmiScadaPipeTests` 故意将新Generation的Discover响应延迟650ms；G1必须先观察旧GEN1的Good被撤回，再观察GEN2身份及新采样恢复。防止慢网络/故障恢复期间出现错误的“仍正常”画面。
- 全程仍无远程控制或命令自动重放，且不修改A的Runtime.Host及共享协议。

## 2026-10-30 第十批：Studio正式Runtime.Host工程预检与设备点表

- `Professional/HMI/PHMScadaEngineeringInspection.cs`：只反序列化A5已提交的`scada-project-check` JSON，验证SchemaVersion=1、EngineeringValidated、DeploymentVersion、唯一MEM DeviceRef/Scope/Module、点表字段与预算。拒绝伪称DeviceCommunicationValidated=true；WritableDefinition只代表点表定义，绝非操作授权。
- `Applications/Studio/StudioShell.Hmi.Preflight.cs`：在已有Studio、当前MEM保存路径及已设置MHS根上，调用用户明确选定的`CxVisual.Runtime.Host.exe scada-project-check`（无Shell、无PLC控制、15秒超时、ExitCode=0与解析双门禁）。项目Dirty、未保存、Root与Host不一致、工程Revision或工程身份切换时拒绝；预检结果表显示正式Scope、DeviceRef、PointId、变量地址、类型与可写定义。
- `StudioShell.Hmi.cs`：原Page HMI右键菜单增加“工程/设备点表预检”入口，配置编辑仍由原MHS配置中心完成，预检不建立第二套工程模型/设备配置。
- `HmiRuntimeTests`：正式A5协议JSON成功、冒充通信已验证、版本错误、重复点位反例；`StudioShellSelfTests.RunHmiOnly`检查真Page菜单存在正式预检入口。上述仅为软件协议与UI集成测试，真正Host运行、SCADA生效配置和Windows STA需固定SHA CI核实，实机G2/HIL G3不据此宣告。

### 第十批补充：专业设备身份透明展示

- Studio工程预检窗口可浏览选择已构建的正式Runtime.Host，增加ConnectorModuleId、ConnectorEffectiveVersion、ChannelId三列，支持查看设备与点表的实际装配身份；无Point的设备仍单独展示，不能因点表为空被隐藏。
- 以上均使用A5正式预检返回值，不额外存储PLC凭据/地址表，不把WritableDefinition或配置有效误认为已登录或已授权。Windows STA和真实CLI联调仍按固定提交SHA验收。

## 2026-10-30 第十一批：A侧FCR真采集轮次在HMI在线工作区展示

- `PHMScadaReadOnlyPipeClient`消费原只读Pipe v1新增的`CompletedCycles/SuccessfulCycles/PartialCycles`三个可选字段；没有字段按0兼容，反常负数或成功/部分失败超出完成轮次的报文FailClosed。值来自Host当前SessionGeneration，不另建统计仓或跨代次累加。
- `PHMScadaRemoteReadSession.CycleDiagnostics()`以当前快照显示Generation和FC03轮次；断线/旧Generation撤回时不继续显示上次正常统计。Studio原`MPWLiveSceneControl`通过只读诊断描述贡献展示，保留原SceneView/MDS数据/命令Owner和六参只读构造。
- `HmiScadaPipeTests`用真实Windows NamedPipe合同夹具验证已存在样本的三个真实计数、GEN1旧计数5和GEN2新计数1不得跨Generation累加，未开真实PLC写入。PLC实际双设备G1/G2及HIL仍需A侧独立证据。
