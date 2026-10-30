# CxVisual Automation——07 自动化运行与安全协同 V1.1
> 版本 V1.1｜2026-10-29｜性质：目标架构/实施合同，非功能已完成声明；核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8/WPF/x64。
> 适用范围：状态、流程、资源、互锁、故障与恢复。权威：AGENTS、开发 Skill、DOCS/00～04、F17裁决、五类模块裁决及实际源码；P01～P18编号最终逐项以二进制 DOCS/02 和 Automation能力图对照。
> 说明：PNG、XLSX 已确认仓内路径，当前工具只能读取 GitHub UTF-8 文本，无法直接解析图中条目或 XLSX Sheet；需将原件另行审查后完成逐条核对。状态【S源码】【R遗产】【T目标】【V自动证据】【H现场证据】不可混用。

**核心原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 双层状态
**管理状态**：Defined/Configured/Installed、版本与 Owner 在 MEM/FCM；**运行状态**：Initialized/Ready/Busy/Degraded/Faulted/Unknown/Stopping/Released 在实际设备/Runtime。物理反馈必须单独记录，不以工作流状态覆盖设备状态。
## 2. 运行分工
- **Decision**：能力发现、Plan/Constraint/Command/Trigger 等既有合同；专业动作通过批准的 Handler 暴露。
- **Runtime**：Operation 资源租约、调度、取消、重试策略、并发隔离、健康/Trace；工业能力需要单一设备执行所有权。
- **Automation**：设备动作、状态检查、故障分类、硬件返回码、状态机守卫、设备专有恢复步骤和安全能力限制。
- **Simulation**：虚拟设备/虚拟时钟/故障注入/回放，环境标识不可抹掉。Config/PLC/IO/Device 仍需同一套正式 Descriptor。
## 3. 关键状态机
~~~mermaid
stateDiagram-v2
 [*] --> Inactive
 Inactive --> Initializing
 Initializing --> Ready: 自检/物理确认
 Initializing --> Faulted: 任一条件失败
 Ready --> Executing: Authorized && Lease && Interlock
 Executing --> Ready: 设备Done+Feedback
 Executing --> Unknown: Timeout/NoReadback
 Executing --> Faulted: DeviceFault
 Ready --> Degraded: Disconnect/Stale
 Degraded --> Recovering: 重新联机
 Unknown --> Recovering: 回读+人工确认
 Faulted --> Recovering: 受控Reset
 Recovering --> Ready: 重新标定/检查完成
 Ready --> Stopping
 Stopping --> Released
~~~
## 4. 一个合法动作的时序
~~~mermaid
sequenceDiagram
 actor UI as 工程师
 participant M12 as Command Owner
 participant SEC as 安全/授权
 participant MRK as Runtime Operation
 participant PAD as Automation Provider
 participant HW as PLC/控制器
 UI->>M12: Request(OperationId/Target/Params/Deadline)
 M12->>SEC: 操作授权与策略
 SEC-->>M12: 允许/拒绝
 M12->>MRK: 申请设备资源 Lease
 MRK->>PAD: Invoke(受限上下文)
 PAD->>HW: 真实读写/动作
 HW-->>PAD: ProtocolAck
 PAD-->>M12: Acknowledged ≠ Completed
 PAD->>HW: Readback/WaitDone
 HW-->>PAD: Done/ActualPosition/状态
 PAD-->>M12: Completed 或 Unknown/Faulted + Evidence
 M12-->>UI: 独立最终结果与Trace
~~~
## 5. Safety 边界（不可变）
**危险能量**（X-Ray 出束、高压、运动、气动等）只允许经过实体安全链：安全门、安全 PLC、控制器硬限位、必要的合规急停/保护系统。通用 PC Runtime 不保证硬实时、功能安全等级或急停停机时间；软件授权/二次联锁只增加审计/阻断，不代替物理安全措施。
操作必须检查：Principal/Role、控制模式、设备Ready/Quality、租约、标定版本、物理联锁、设备故障、参数限值、现场占用、环境 Real/Sim。异常时默认 FailClosed；危险指令失败/超时默认 UnknownOutcome，必须查现场状态；再试需幂等或人工批准。
## 6. 资源和线程
每个 DeviceSession 有 Generation、取消域、回调注册表、I/O所有权、输入/输出缓冲租约；Stop 必须停止动作请求、注销回调、关闭资源、等待有界任务与 Flush 证据；迟到回调不能重新恢复旧状态。SDK需 STA/专用线程时明确单一执行上下文，不能在UI线程执行长操作。
## 7. 异常恢复表
| 事件 | 必须立刻做 | 再次 Ready 的门槛 |
|---|---|---|
| PLC断链 | 标记 Quality Bad/Stale、停止新控制 | 重连+会话代次+状态重同步 |
| Motion超时 | Unknown、不盲重发 | 查实际位置/Alarm/模式，重新授权 |
| X-Ray故障 | 不出束/请求安全停止、保留物理告警 | 安全链闭合及人工复核 |
| Camera拔线 | 回调停止、Lease释放、帧标记Invalid | 设备发现/参数/触发链校验 |
| IoT重连 | 反向控制禁止自动重放 | 重新身份/版本/策略校验 |
| 宿主崩溃 | 物理设备按现场安全设计进入保护 | 宿主恢复+设备实际状态确认 |
## 8. Flow/状态/命令不要二次造轮子
AutomationFlow 描述的是**专业步骤/安全约束与参数**，运行执行由现有 Decision/Runtime 的 Loop/Foreach/Parallel/Trigger/Composite 与 MRK；Robot+Camera 或 PLC+Motion 的协作对同一工件/TriggerId 建立可追踪版本化上下文。
## 9. AOP、GoF与设计审查
- OOP：实体与会话分离、接口隔离，定义不依赖厂商实现。
- GoF：Factory(Provider创建)、Strategy(不同厂商/轨迹策略)、State(受控设备生命周期)、Command(公共M12)、Observer(公共MDS/M36)、Composite(工程组织)。不用额外巨大 Adapter 层。
- AOP：Auth/Interlock/Deadline/Trace/Metrics/Exception、统一的调用链门禁；需避免事件通知重入，统一关联码。测试覆盖横切失败和拒绝路径。
## 10. 正反验收
RUN-001～014：不授权不执行、读值 Quality Bad 禁止危险命令、模式错误/限位阻断、资源竞争、超时未知、重复命令ID、同一Device两个Session互斥、取消期间回调、断电重连状态隔离、Stop幂等、运行/工程数据不串写、虚拟设备不能出束、状态重放与故障恢复、HIL确认。


---

## V1.1｜企业工况、机器模式、功能安全接口与可靠性交付
### 11. Automation Operation 统一七段式执行合同
`Declared→Validated→Authorized→Reserved→Dispatched→Acknowledged→Observed/UnknownOutcome/Failed→Released`。这是审查时的**语义阶段**，不是要新增第二套Command枚举；实际命名应映射现有 M12/MRK。每一阶段都有 Actor/UTC+MonotonicTime/DeviceGeneration/CorrelationId/GuardEvidence，只有物理现场回读可构成 Applied/Observed。
### 12. 状态的四维正交
| 维度 | 典型状态 | 唯一事实Owner |
|---|---|---|
| Engineering | Draft/Validated/Published/Deprecated | MEM/FCM/Decision |
| Connection/Session | Disconnected/Connecting/Connected/Degraded | F17/MPA/专业设备会话 |
| Device/Work Mode | Auto/Manual/Maintenance/Teach、Ready/Busy/Fault | 专业 Provider 与可信现场反馈 |
| Operation Outcome | Accepted/Sent/Ack/Applied/Observed/Unknown | M12/MRK+专业证据 |
同一设备可能 Connected+Faulted，或者 NotReady+ValueGood（仍有缓存）；不能用单一 IsRunning/IsConnected 布尔量推断实际安全、操作或生产条件。
### 13. Safety & Security 分离模型
**Safety（功能安全）**：SafetyZoneRef、GuardDoorStatus、EStopCircuitStatus、STO/SS1/SLS feedback、ResetPermission、SafetyPLCInterlockRef；这些状态来自经设计验证的控制器/安全系统，仅用于上位机观测、操作门禁和审计。普通 .NET 进程不实现符合 IEC62061/ISO13849 的认证安全控制链。
**Security（网络/操作授权）**：身份、RBAC/ABAC、最小权限、设备控制租约、远程访问、密钥证书、补丁/部署审计、站点网络分区/零信任；参考 ISA/IEC62443。安全状态 Good 和“已授权”必须同时成立才能提交高危请求，但软件门禁并不构成物理安全保证。
### 14. 功能安全状态与危险动作矩阵
| 动作 | 软件前置许可 | 现场强制链/实测结果 | 超时策略 |
|---|---|---|---|
| Axis Move/Jog | Manual/Auto模式、许可、目标/限位/轴资源 | 控制器速度/位置/硬限位/安全驱动 | Unknown+位置回读，不盲目Move |
| Robot Program/Teach | 模式、Tool/Frame版本、工作区域/操作员确认 | 安全区域、控制器安全状态、实际Pose | Unknown+人工确认 |
| XRay Expose | 门禁、参数/热机、源+探测器Ready、独占资源 | HV/辐射互锁、强制防护、真实出束与停止状态 | Unknown；设备物理链主导保护 |
| Fluid/Clamp | 工位占用、预设压力/动作模式 | 压力、位置、阀与安全泄压设计 | Unknown+反馈复核 |
| Recipe Apply | 设备模式、目标配方/物料/工装、审批 | 批量/分步参数回读、工艺许可 | Partial/Unknown，受控核对 |
| Remote PLC/Edge Write | 身份/时间范围/操作租约/映射版本 | PLC/设备反馈与审计 | 不自动重放 |
### 15. 可靠性量化合同：目标数值由实测冻结
必须建立性能基准项而非空泛“高性能”：P50/P95/P99 Cmd Ack与PhysicalDone延迟、PLC点位扫描偏差、系统背压/丢点/丢帧率、错误隔离时延、重连P95、资源释放耗时、长时间GC/句柄/native内存增长、跨工站并发、现场校准漂移。阈值按设备/生产节拍/驱动SDK现场基准确定，不能承诺Windows普通进程1ms硬实时。
### 16. 失联/故障/恢复覆盖五种世界
1. **Control disconnected**：仅能说明主机联系不上，实际运动/出束可能仍在进行，必须由现场控制器/硬件完成安全降级。
2. **Host crashed**：外部保护系统独立接管；重启后所有会话 Generation改变、旧命令结果Unknown、读回与模式/联锁重新核验。
3. **BadQuality/Stale**：UI显示最后有效值时必须标黄/Bad且禁止依赖该值执行危险命令。
4. **Driver callback after stop**：Generation/Lease 失效，任何迟到回调不得访问释放缓冲或重启动作。
5. **Recipe/Plan version drift**：执行单元和配方版本必须锁定，不允许运行中静默替换；可中止后明确升级/回滚。
### 17. 运行架构上的 OOP/GoF/AOP 裁决
- OOP：设备 Identity/Definition/Session/Provider/OperationOutcome 分离，Provider按能力接口组合；状态值公开不可随意外部 set。
- GoF：Factory由已有MFM/MOC注册，Strategy具体SDK/协议策略，State专业设备状态图，Command现有M12，Observer现有MDS/M36，Composite复用MEM/Plan，禁止复制公共框架。
- AOP：授权、安全守卫、租约、时限、校验、Metric/Trace/审计、故障注入通过正式运行Invocation管线处理；避免每个驱动重复抄整套横切逻辑。
- 高级工业要求：同设备Session的命令串行、不同物理资源并行、生产与维护模式互斥、对端断连/取消不使锁永远悬挂，故障传播/隔离策略按Plan版本记录。
### 18. 虚实一体的严格边界
Simulation 提供可复现PLC/Axis/Fluid/Robot/Detector/SCADA故障模型、单步时钟和Trace比对；同一工艺流程通过能力绑定切换 Real/Virtual/Replay。但 `EnvironmentId` 与 Provider真实证据源是硬性门禁，不能在虚拟情景中产出 PhysicalDone 或签发 G2/G3 HIL 报告。
