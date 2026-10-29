# CxVisual Automation——07 自动化运行与安全协同
> 版本 V1.0｜2026-10-29｜性质：目标架构/实施合同，非功能已完成声明；核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8/WPF/x64。
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
