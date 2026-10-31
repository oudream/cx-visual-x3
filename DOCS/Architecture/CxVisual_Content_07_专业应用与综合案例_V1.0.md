# CxVisual Content 体系 #7——六专业应用、跨专业组合与八个黄金案例 V1.0

> 目标应用合同；主要覆盖 CNT-R61～R67，案例 CNT-G01～G08。**案例用于反推对象/API/测试，不是当前设备实测报告**。现有各 Professional Owner 与七仓生产代码复用优先，不因新 Content 平台推翻成熟专业实现。

## 01. 六专业最低使用合同

| 专业 | 内容单元边界 | 公开输出/接口示例 | 特有风险与 Owner |
|---|---|---|---|
| Automation | Station/MotionSequence/DeviceProcess | StartRequest、Status、Fault、CycleResult | PLC/运动安全互锁、实时现场确认归专业 |
| HMI/SCADA | EquipmentPanel/AlarmPanel/Dashboard | State、ActionRequested、Quality | 读写分权、Stale/Bad、不把界面事件当命令成功 |
| Vision | ImagePipeline/ROIInspection | ImageHandle、Defects、Measurements | 图像格式、坐标、算法版本和 Source/Run 隔离 |
| CT3D | Reconstruction/VolumeAnalysis/MPRWorkspace | ProjectionSet、VolumeRef、Slice/Measure | GPU/体内存租约、坐标与标定、专业 Viewer |
| Inspection | InspectionRecipe/Judge/Review | Result、NGReason、AuditTrail | 复判/人工判定、产品 SN 和结果可追溯 |
| Simulation | VirtualCell/FaultScenario/Replay | ModelState、VirtualEvents、Provenance | 虚实与时间源隔离、不得误调用实机 |

专业开发者至少提供：现有 Capability/Type/Port/Config/Resolver 中需要的真合同、单位/坐标/时基/质量、失败/Unknown、专业审计与实际可验结果。纯展示/纯计算可以按需缺省某些项；不要求每个专业先写 Symbol。

## 02. CxVisual 既有历史资产如何进入 Content

- cx-visual-x2：工程对象、Studio交互与历史项目映射，只通过新 MEM/MPM/MOL 归一；不搬回平行 Graph。
- cx-vision-x2：X-Ray/CT、检测、AI/传统视觉、采图和结果等长期应用的专业 Provider/Native/Adapter。
- cx-cct-x3：重建、体数据与跨语言接口，采用稳定数据布局/坐标与内存所有权；不复制原重建内核。
- gcl3：ICS/SCADA 变量、设备通道、分布式监控、质量/时间/恢复语义。
- device_communicator：Go 边缘通信/服务，允许外部 Service/Connector，不强制改写 C# 或嵌进 Content 引擎。
- ccxx：C++ 数值/线程/网络等成熟基础，采用 ABI/IPC/Adapter 明确版本与生命周期。
- cx-visual-x3：新定义/组合/编排的唯一主仓库。

原资产成熟不等于新组合已现场验收；每一复用项必须写清原仓库/类或功能/许可证与依赖/适配接口/是否移植/测试环境/故障边界。

## 03. G01 纯视觉复用：双页面状态板

输入：两个 MotorStatus Symbol、一个 Text/Group；作者创建 Dashboard Content，放入两个 Page，不加 Logic Aspect；分别绑定 StationA、StationB 变量、质量和样式。验证 DefinitionId 共享、RefId/Binding 不同、页面保存重开及可见更新。负例：缺变量/BadQuality 显示降级，不能将 Ref 视觉出现判为有 MRK Flow。

## 04. G02 HMI + Automation 电机控制闭环

Content: MotorStation（Motor Symbol、设备与状态映射、StartRequested/StopRequested、命令与流程、故障策略）；用户点击 Start，产生 RequestId，Content 映射到受控 Host/Professional；PLC 接收后报告 Accepted/Done/Unknown，真实状态经 MDS 回到 Symbol。负例：无权限、联锁失败、断线/延时、两实例 A/B 命令误路由必须拒绝或标 Unknown；不以“按钮变绿”代替实机确认。与 Symbol V2 G02 同场景共享事实，测试增加 Content Invocation/权限映射。

## 05. G03 Vision：图像采集和 ROI 检测

Content: Acquire → Preprocess → Detect → Measure → Publish，ROI Symbol 仅负责 Geometry/编辑与结果 Overlay；数据通过 ImageHandle/SourceId/RunId/CoordinateFrame，公开 Defects、Measurements/Quality。两个 Source 同时运行时各自结果按 Run + Source 投影，坏 ROI 坐标、错误图像大小/格式、旧 Run 到达、缺算法版本必须明确拒绝/退化；运行前检查可用 Provider/依赖与内存预算。

## 06. G04 CT3D：投影到三维分析

Content 分为 Acquisition、Reconstruction、VolumeAnalysis、Presentation/MPR。输入 ProjectionSet（几何/能量/角度/标定），重建输出 VolumeRef（Spacing/Origin/Orientation/Lease），专业 Viewer 负责 MPR/3D/等值面等显示；Symbol 提供窗宽窗位与工具状态，不能把 GPU Volume 序列化进二维 RootGroup。负例：重建几何不符、WorldToVoxel 失效、GPU 预算不足、体租约已释放、Viewer 未安装必须有明确结果而不误绘。

## 07. G05 Inspection：分层检测与复判

RootInspection Content = AcquireContent → AnalyzeContent → JudgeContent → optional ReviewContent → CommitResult，明确 Recipe、ProductSN、InspectionResult、DefectEvidence 和 OperatorReview 输入输出。失败/取消时子 Content Pending 输出不得向父导出；复判必须保留原判定和人工动作证据，不能覆盖历史。双设备并发、不同 SN、超时与复判流程均需 SourceMap/Trace 定位。

## 08. G06 Simulation：同接口虚实替代

同一检测 Content 在软件仿真环境绑定模拟 Camera/PLC/Detector，但任何运行结果标注 Mode/ModelVersion/Seed/VirtualTime。真实模式切换需要 Host 重新绑定、校验、有效配置与安全授权；不允许自动将 Virtual Resolver 回退到 Real。负例：虚拟状态触发真实设备写入、数据时间基不兼容或同名 State 混流。

## 09. G07 提取共享复用与版本升级

从一个真实工位选择局部 Content 子树和流程关系 → Preview 公开端口/IdentityMap/依赖 → MTI/MTC 提取与替换 → 将定义放入第二 Project/Station → 独立 Override → 修改 V2 接口时计算两处引用影响 → 显式迁移或保留 V1。失败用例：跨界未映射端口、旧 Schema 不支持、外部引用丢失、无授权变更现场绑定；均不能留下半成品或静默热更。

## 10. G08 真实综合编排：多专业工作站

~~~text
ProductionCell Content [Automation + HMI]
  ├─ Feed/Move [Automation Capability/PLC]
  ├─ Inspect2D Content [Vision; Acquire/Detect]
  ├─ Inspect3D Content [CT3D; Reconstruct/Analyze, optional branch]
  ├─ Judge Content [Inspection]
  ├─ Publish/MES [Connector/Service]
  └─ Dashboard [HMI Symbols, Data/Status, no forced Flow]
~~~

流程由显式 MOL 顺序/分支/并行/等待/汇合定义；设备侧安全、专业数据内存、网络异常/幂等回执由各自 Owner 管。输入 ProductSN/RecipeRef，输出 Result/DefectRefs/TraceRef；两个并发产品不串 Scope/Trace，任意设备超时不可谎称已安全停止。G08 必须覆盖三级共享子 Content、多次 Invoke、同 Plan 并发、版本锁及分支失败回滚。

## 11. 使用者真正要获得的结果

- 专业开发者：已有功能只需声明必要端口/配置/Provider，再由 Content 组合；不重复写运行/监控基础。
- 工程师：在 Studio 找到能力，组合成 Content/子 Content，配置公开接口、控制与数据连线，验证、调试、复用。
- 操作员：一个页面看到正确状态并发出安全受控命令，不被要求理解内部 DAG/Frame。
- 运维：按 Product/Run/Content/Ref/Capability/Port 定位故障、版本与依赖，失败不丢证据。

## 12. 黄金案例验收合同

每例记录：固定工程样例/输入与输出、实际 Provider、是否真设备、具体操作/API、各对象/端口/实例 ID、预期与实际结果、负例与恢复、内存/时间指标、源文件和测试、SHA/CI/设备证据。软件级通过只证明 API/逻辑，不证明实机安全。八例与原工程组织八案例保持**功能映射**而不随意覆写原 REAL 编号；在 Implementation 台账建立映射字段。
