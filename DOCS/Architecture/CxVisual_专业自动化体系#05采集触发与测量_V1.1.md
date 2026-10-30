# CxVisual Automation——05 采集触发与测量 V1.1
> 版本 V1.1｜2026-10-29｜性质：目标架构/实施合同，非功能已完成声明；核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8/WPF/x64。
> 适用范围：Camera/Detector/X-Ray/DAQ/Measurement/Trigger。权威：AGENTS、开发 Skill、DOCS/00～04、F17裁决、五类模块裁决及实际源码；P01～P18编号最终逐项以二进制 DOCS/02 和 Automation能力图对照。
> 说明：PNG、XLSX 已确认仓内路径，当前工具只能读取 GitHub UTF-8 文本，无法直接解析图中条目或 XLSX Sheet；需将原件另行审查后完成逐条核对。状态【S源码】【R遗产】【T目标】【V自动证据】【H现场证据】不可混用。

**核心原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 专业边界
- **Automation**：设备 SDK、曝光参数、能量源、探测器触发、编码器时间、采样仪器、工况与安全联锁、Frame/Sample 的获取和设备级元数据。
- **Foundation.Imaging/Volume**：基础图像/体数据表示与内存；**Mechanism.Data**：DataHandle、Stream、Lease、流背压/版本/生命周期；**Professional.Vision**：2D 算法/ROI/识别；**Professional.CT3D**：投影几何、重建/体绘；**Inspection**：工艺结果和质量判定；**HMI**：数据显示。Automation 不占用这些专业的算法 Owner。
- **硬件**：X-Ray Source、高压电源、Detector、Camera、DAQ、编码器、触发板与安全 PLC 均可能有独立 Session；设备 SDK 调用实际能力须从厂商库/现场证据确认。
## 2. 主要专业对象合同（建议，不是现有新类）
| 角色 | 关键字段/能力 | 需绑定的公共合同 |
|---|---|---|
| AcquisitionDeviceDefinition | DeviceRef、型号、SDK、支持模式、设备能力版本 | MEM/MFM/FCM |
| AcquisitionSession | Generation、Initialized、Armed、Capturing、Faulted、SDK句柄 | 宿主生命周期/MRK |
| TriggerDefinition | SourceType(Software/Hardware/Encoder/Position)、Edge、Delay、Domain、Clock | FCM + 能力参数 |
| TriggerCorrelation | TriggerId、HardwareTick、MonotonicTime、PTP/NTP源、Precision | MDS/Trace |
| FrameDescriptor | FrameId、DeviceRef、TriggerId、Width/Height/Stride/PixelFormat/BitDepth、Timestamp/Quality | 数据句柄 Owner |
| SampleBatch | DAQ通道、SampleRate、Unit、CalibrationVersion、时间范围、质量/丢样数 | MDS/专业测量 |
| MeasurementResult | Measurand、Value、Unit、Uncertainty、CalibrationRef、Trace | 公共结果/业务处理 |
## 3. UML
~~~mermaid
classDiagram
 class AcquisitionSession {+DeviceRef +Generation +State +Start() +Stop()}
 class TriggerSource {<<interface>> +Arm() +Fire() +Disarm()}
 class FrameDescriptor {+FrameId +TriggerId +Timestamp +Quality}
 class DataHandle {+HandleId +Lease}
 class MeasurementResult {+Value +Unit +Uncertainty}
 AcquisitionSession --> TriggerSource
 AcquisitionSession "1" --> "*" FrameDescriptor
 FrameDescriptor --> DataHandle
 MeasurementResult --> FrameDescriptor : evidenceRef optional
~~~
## 4. 六步物理采集链
1. Prepare：检查授权/工艺/模式/门控/设备型号/校准版本/参数合法性。
2. Arm：运动与探测器设置就绪，建立 TriggerId/Generation，分配受控缓冲。
3. Trigger：由软件或物理控制器触发；高精度/定时同步绝不依赖 WPF/普通 .NET 定时器。
4. Acquire：DMA/厂商回调把数据复制或租约转交给**真实所有者**，立即隔离不安全临时指针；记录丢帧/重复/排序/时间漂移。
5. Publish：只发布 Handle/元数据到机制数据层，按工艺转交 Vision/CT3D/Inspection；对慢消费者启用有界队列与背压。
6. Complete/Stop：释放帧引用、停止触发、归还 SDK 缓冲/取消回调、等待 Generation 隔离，保留运行审计。
## 5. 时序与异常
~~~mermaid
sequenceDiagram
 participant R as M12/MRK/授权
 participant S as 安全PLC
 participant X as XRay/Acquisition Provider
 participant D as 探测器SDK
 participant B as MDS/数据句柄
 participant V as Vision/CT3D
 R->>S: 读取外部安全许可
 S-->>R: 模式/互锁/保护状态
 R->>X: Prepare/Arm(参数版本)
 X->>D: Configure+Arm
 R->>X: Trigger(TriggerId)
 X->>D: 真实触发/曝光
 D-->>X: Frame(Buffer,DeviceTick)
 X->>B: Lease + FrameDescriptor
 B-->>V: HandleRef
 V-->>R: 专业处理事实
 X-->>R: 曝光/采集物理结果及故障
~~~
必须区分曝光命令已发送、X-Ray 硬件已出束/停止、探测器有帧、数据已可用、图像算法完成这五个独立阶段。失败后出束仍可能尚未停止，安全链由硬件保证。
## 6. 历史资产
`cx-vision-x2/Drivers/IRayXRayDriverModule/IRayXRayDriver.cs`、`Drivers/IRayDetectorDriverModule/IRayDetectorDriver.cs`/ `DalsaDetectorDriver.cs`；`CxECTStudio/Business/DetectorImageService.cs`、`MotionExecutionService.cs` 为待函数级审计的来源。【R】不可宣称已落入 x3 或通过 G2/G3。
## 7. 专项验收
ACQ-001～014：软触发、硬触发、位置触发、双设备同一 TriggerId、帧率/BitDepth/Stride、丢帧/乱序/重复、慢消费背压、Stop瞬间回调、热拔插、源/探测器状态矛盾、高压门控、DAQ越界/异常测量、长期采样内存/句柄、现场同步精度及完整 Trace。测试必须标识 Real/Virtual，并保存原始时间源及硬件型号固件版本。


---

## V1.1｜采集—时钟—测量—结果证据的完整工业链
### 8. 采集系统的四个独立维度
1. **设备/物理**：Sensor、Camera/Detector、XRaySource、LightController、DAQChannel、Encoder/TriggerBox、机械位置与电缆/供电，每一实体通过 AssetRef 关联，硬件固件与SDK版本独立。
2. **时空/配置**：曝光/能量/快门/模拟增益、像素/BitDepth/Stride、ROI、设备坐标、触发源/边沿/偏移、时基(PTP/Encoder/Clock)、参数/标定版本。
3. **数据/缓冲**：Frame/Sample/Waveform/ProjectionSeries 数据句柄 + Lease + Producer/Consumer + 背压；图像基础表示由 Imaging，CT几何与重建由 CT3D，Inspection的产品判定独立。
4. **测量/证据**：触发到帧关联、采样窗口、单位/量程/不确定度/校准、质量/时间戳、物理发束状态、停止与故障证据。
### 9. 设备族与动作要求
| 设备族 | 最低能力合同 | 输出与异常 |
|---|---|---|
| 工业相机/线扫/TDI | Discover/Configure/Arm/Trigger/StartStream/Stop/FrameMeta | FrameId、TriggerId、Seq、BitDepth/Stride、Drop/Dup/Overrun |
| XRay源/高压/冷却 | HVConfig/Warmup/Expose/Stop/ReadHV/InterlockHealth | 实际kV/mA/出束状态、温度冷却、超时Unknown，硬件安全链 |
| Detector平板/线阵 | Dark/Flat、ExposureSync、ReadProjection、CalibRef | 像素/时间/几何/坏点状态、流背压、硬件帧完成 |
| DAQ/Instrument | Range/Rate/Trigger/Channel/Stimulus/SampleWindow | SampleBatch/Value+Unit+Uncertainty、超量程/丢样 |
| 触发/同步板与编码器 | TriggerRoute/Divisor/Delay/PositionCompare/TimeSync | TriggerId、DeviceTick、时钟漂移、抖动和物理计数 |
| 工业测试工装 | DUT/Fixture/Stimulus/Settle/Measure/Trace | 原始量测与试验曲线，产品级OK/NG仍归Inspection |
### 10. 多设备采集时序与一致性
~~~mermaid
sequenceDiagram
 participant Run as Runtime/Operation
 participant Safety as SafetyPLC/Controller
 participant Mot as Motion+TriggerHardware
 participant Src as XRaySource
 participant Det as Detector/Camera
 participant Data as DataHandle/Stream
 participant Inspect as Vision/CT3D/Inspection
 Run->>Safety: ReadPermit+Mode+Door
 Run->>Mot: Prepare path/position comparison
 Run->>Det: Arm(buffer, configRevision)
 Run->>Src: PrepareExposure(HVVersion)
 Mot-->>Det: HardwareTrigger(TriggerId)
 Src-->>Det: Actual exposure/sync evidence
 Det-->>Data: FrameLease(FrameId, TriggerId, Clock, Generation)
 Data-->>Inspect: HandleRef+time/geometry refs
 Inspect-->>Run: Algorithm/inspection result
 Src-->>Run: Physical state stopped/ready proof
~~~
严格区分 Send(Ack)、DeviceArmed、ExposureActive、FrameReceived、FrameValid、InspectionFinished、SafeStopped 七个阶段；触发缺帧/帧无TriggerId/多帧乱序不得冒充“同步成功”。
### 11. 试验计量与测量学合同
`MeasurementSpec` 概念字段：Measurand、Unit、Scale、Range、Tolerance、EnvironmentalConditions、CalibrationCertificateRef、TraceabilityChain、UncertaintyModel、SampleWindow、ExpectedSamplingRate、Timebase、EvidenceRefs。实际计量模型可参考 ISO 10012/ISO/IEC17025 相关测量和溯源原则，但不自动宣称实验室认可。气密测试的压力/温度稳定等待属于专业试验过程；检验最终OK/NG由Inspection基于测量证据执行。
### 12. 工业长稳与真实时间
- 外部同步以有确定性能力的硬件提供；CxVisual表示时钟域/校正/同步偏差，不在Windows定时器中产生硬实时脉冲。
- SDK callback 禁止直接阻塞昂贵图像/AI处理，应将数据移交有界管道；DataHandle引用计数、归还事件和Stop后回调注销均应追踪。
- 高带宽数据禁止每个订阅者无界拷贝；记录最大在途帧、消费者积压、GC/Native内存、落盘比率、触发到帧分位时延。
- 真实 X-Ray现场HIL应由有资质的安全人员按辐射防护和设备风险评估执行，CI不得自动出束。
### 13. 交互与验收
面板能显示设备热机、曝光曲线、帧率、波形、ROI、Trigger Correlation、水冷与故障状态；参考`cx-vision-x2/Drivers/IRayXRayDriverModule`、`IRayDetectorDriverModule`源行为，按目标 Owner 实装前逐函数复核SDK/共享内存/内存所有权。负例包含帧重复、错帧、暗场失效、校准过期、工件丢失、联锁不许曝光、Stop回调竞态、采集源重启和时间源漂移。
