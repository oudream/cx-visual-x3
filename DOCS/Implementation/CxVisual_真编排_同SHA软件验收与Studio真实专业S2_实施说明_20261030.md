# CxVisual 真编排 B1～B6｜同SHA软件验收与Studio真实专业S2实施说明

> 日期：2026-10-30。依据`AGENTS.md`、`DOCS/00～04`、已批准F/M/P Owner、现有MOC/MRK/MDS/Studio；**本文件说明真实代码与验收门禁，不以文档代替实际CI。**
>
> 核心原则：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**
>
> 不新增工程项目、二次Graph、命令中心、运行引擎或数据仓；保持.NET Framework 4.8与WPF。

## 1. 一致软件验收基线

唯一新增综合工作流：`.github/workflows/orchestration-software-baseline-windows.yml`，名称`CxVisual B1-B6 Unified Software Acceptance`，支持手动触发与master的相关源码变更自动触发。

同一次GitHub Actions运行固定一个`GITHUB_SHA`、一台Windows 2022 runner，顺序验证：

1. 同SHA的全解决方案Release/x64重新构建及TestCenter/Studio NuGet Restore。
2. B1～B4正式`--orchestration-vertical`、`--orchestration-industrial`、`--automation-plc`（真实OS TCP/Modbus回环）、`--concurrency`、`--command-center`、工程组织回归及默认ArchitectureSmoke。
3. Debug/x64全解决方案构建、真编排和默认回归。
4. B6真实Host签名Plan发布/激活后MRK执行、Journal恢复、MDS 20k帧生命周转。
5. B5 Studio WPF STA自测；必须包含旧B5调试隔离、既有CPU专业以及新增`S2 真实专业WPF能力目录、RAW强类型MEM编辑和授权边界`断言。
6. Owner结构、项目依赖门禁，上传STA报告与WPF截图。

历史CI只证明历史SHA；新改动必须以自己的综合门禁结果为准。**G1是本机真实TCP协议软件验收，不是PLC、探测器、出束或运动安全实机验收。**

## 2. Studio专业能力真实接入

### 2.1 能力与执行Owner

- `Applications/Studio/StudioProfessionalFlowResolver.cs`仅为Studio软件宿主组装器；正式专业实现仍为`CxVisionThresholdProvider`、`CxVisionMedianEnhanceProvider`、`CxVisionBinaryThresholdProvider`、`CxVisionMaskRegionsProvider`、`CxVisionAcceptedRegionMeasureProvider`。
- 本地RAW经过既有`CxVisionRawCpuRunner.ReadRaw`严格校验文件字节数、宽高、8/16位深，随后固定本次输入字节的SHA256快照；MRK仅使用固定快照，操作系统文件后续变化不会回写已运行产品。
- `MOCCapabilityCatalogRuntimeFactory`仍是唯一在线能力可用性目录；`SimulationResolver`保留给旧AI/CV仿真工程；只有当前可达Content包含`vision.cpu.*`或`automation.plc.*`才选择真正专业软件Resolver。
- `PADPlcMrkWriteCapabilityProvider.DescribeOfflineContract()`是Automation正式专业Owner公开的唯一PLC UInt16输入/输出合同；Studio可搜索、加入、设置参数、保存重开，但**绝不构造`MRKCommandCenter`、注入`trustedCaller`或调用FC06**。没有独立Runtime.Host授权，Build/Run拒绝。
- 使用原有Studio配置中心的`LoadStudioEffectiveVisionProvider`与`MHSConfigService`（勾选“使用当前MHS Effective”）捕获已持久化配置、权限及Effect版本栅栏；旧配置失效/授权撤销拒绝执行。未勾选时仅为明确标记的本地CPU临时参数，**不宣称MHS Effective或工业发布配置**。

### 2.2 实际用户流程

1. 打开Studio主流程`doc.flow`或独立Content流程Tab，在能力搜索中选择真实Vision CPU或离线Automation PLC；输入/输出Port自动取专业Owner的类型化声明。
2. 可以在空Content点击“插入真实RAW→Vision CPU节点”：同一`MOCFlowBuilder.Edit`事务创建RAW、阈值/四邻域CPU两个节点，保存`RawPixels:byte[]`以及`Width/Height:int`，Control/Data连接、布局；已有V1/V2 Logic不覆盖。
3. 通过现有节点Inspector、Port、Data连线、撤销/重做、保存与重开修改MOL；切换能力时同一工程事务同步端口合同，有已使用的不兼容端口时拒绝，不删除链接或破坏项目。
4. 在Studio离线工作栏显式选择RAW文件、宽高及8/16位深，可选用现有MHS Effective专业配置；点击验证，使用`MOCBuildService`对当前活动Content编译、展示原SourceMap诊断。
5. 点击“准备CPU软件发布”，仅锁定软件候选和Provider版本；再点软件执行，使用已有`MOCProjectRelease.Stage`/MRK PlanSlot授权策略及`MRKExecutionEngine`；沿用原Flow断点、暂停、单步、取消、Trace及有界结果摘要。
6. Vision结果预览只投影`ThresholdPixelCount`、区域数、Passed、ConfigRevision，不把RAW图像或任意对象`ToString()`进入Studio历史摘要。
7. 含PLC写入或不可解析专业能力的工程在Studio中允许离线保存但**不能软件运行成功**；后续正式设备运行必须经过Host配置校验、用户身份、资源租约、安全点、签名Plan、有效回读与现场联锁。

### 2.3 真实运行与持久化约束

- 工程事实仍为唯一MEM Project/Page/Content及MOL Logic Aspect；输入本机文件路径仅为本次软件运行现场绑定，不自动持久化到其他用户工程，也不随工程包嵌入机密RAW。
- 工程中的Width/Height和端口/连接原样保存；专业算法阈值来自当次MHS Effective或明确标注的临时软件选项。要在设备生产环境重现参数必须选用已批准的Effective配置；本地临时选项不等同生产配方。
- 配置版本和RAW hash均由运行/发布候选固定；工程修改Revision、变更参数、文件内容或MHS权限时不得沿用旧软件发布候选。
- 真实设备状态、操作ACK、硬件安全联锁、现场MES/PLC、探测器、设备看门狗属于后续G2/G3现场验收，不得以仿真通过替代。

## 3. 回归及证据

- WPF STA：`Applications/Studio/StudioShellSelfTests.cs`中`S2 真实专业WPF能力目录、RAW强类型MEM编辑和授权边界`。验证真实目录中CPU可解析、PLC仅合同、无RAW不能运行、同一MEM事务、保存/重开、MRK实算像素/SourceMap、可信标量摘要、未授权PLC拒绝；不修改用户当前工程。
- 已取得的首批实际单项执行证据：固定旧S2初稿的[Studio #41021244126](https://github.com/oudream/cx-visual-x3/actions/runs/41021244126)出现该S2测试`PASS`，但整条Job仍因旧模拟发布兼容性失败，所以**不能计为全绿**。
- 已修复旧模拟兼容：`MainWindow.ResolverForFlow`只在真实专业节点的可达Content中切换Resolver；原AI/CV软件候选、Studio模拟Provider及MRK调试接口必须维持历史行为，直到完整WPF STA验证成功。
- Stage1综合门禁历史成功例：[B1～B6统一软件CI #41020412054](https://github.com/oudream/cx-visual-x3/actions/runs/41020412054)固定`466fa2a0` **Success**，只是软件基线工作流初版（不覆盖后续S2源码）。
- **第一轮完整Studio WPF S2绿灯**：固定[`e7ea28a4`](https://github.com/oudream/cx-visual-x3/commit/e7ea28a4732170b90520d738438a1d74dd5614f0)的[Studio #41021976116](https://github.com/oudream/cx-visual-x3/actions/runs/41021976116) **整条Job Success**。原WPF STA日志确有`PASS: S2 真实专业WPF能力目录、RAW强类型MEM编辑和授权边界`与旧P0-4模拟发布/运行断言PASS。
- **本轮随后增强（单独验收）**：活动独立Content的真正WPF按钮编辑/执行、原MRK结果安全标量投影、MHS Effective配置捕获与变更拒绝、强类型非法非有限参数负例及相关WPF强制门禁，均为晚于`e7ea28a4`的提交；固定对应SHA的整个Studio和B1～B6同SHA工作流仍应根据实际Actions结论登记，**不能复用旧绿灯声称最新master已通过**。
- 新的`S2 真正WPF按钮创建RAW专业流程并执行MRK真实Vision CPU`回归在实际WPF STA上触发绑定按钮、同一MEM一次事务、原MOC编译、正式MRK异步执行及结果/Trace，不仅是直接调用辅助方法；在对应Windows CI成功以前标记“待验”。

## 4. 不属于本轮完成的内容

- 生产Runtime.Host接收Studio工程的正式授权包、签名发布后真实PLC执行及远程工业权限管理；
- 现场多探测器采集、运动/出束联锁、MES ACK、停机后设备侧事实及长时间硬件压测；
- 各专业WorkBench全集的G2/G3完整验收、超出本轮Vision/PLC首批Owner的专业能力。

这些项目继续走下一阶段Host正式发布/现场验收，不应因为S2完成而宣布工业生产版完成。
