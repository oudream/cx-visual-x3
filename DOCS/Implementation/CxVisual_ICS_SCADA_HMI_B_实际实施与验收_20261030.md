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
