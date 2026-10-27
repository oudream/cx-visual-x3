# CxVisual F17 Channel 下沉与唯一 Owner 裁决 V1.0

> 2026-10-27；依据用户批准的完整 Channel 迁移与 FTR 命名；实现基线 `a46705c3`，提交与 CI 以本轮最终 SHA 为准。

## 1. 决议及覆盖关系

1. 全部**正式编译**的通用 Channel 类型（契约、配置、IO、子通道、诊断及 TCP/UDP/Serial/NamedPipe/HTTP/WebSocket/Virtual/Replay）唯一归于 **F17 `CxVisual.Foundation.Transport`**。
2. 基础类/接口统一 `FTR*` / `IFTR*`，CLR namespace `CxVisual.Foundation.Transport`；程序集 `CxVisual.Foundation.Transport`。源文件主类型同名，分类目录为 `Channels/Contracts, Configuration, IO, SubChannels, Diagnostics, Implementations`。
3. `Mechanism.Connectivity` 只持有 `MCN*` Connector（模块装配）及 `MPA*` Protocol（分帧/Codec/设备语义/点表/映射）；不允许复制、包裹出第二套 Channel。
4. 与旧《统一工程项目代码与资源规范 V1.0》中“Channel 归 M13～M15，F17 仅缓冲基础”的条款相比，本裁决调整了**唯一代码 Owner**，其他 Foundation→Mechanism→Professional 依赖纪律不变。
5. 原有 `MCC*` 公共类型 FQN / 程序集身份不再有效。源码调用方随迁移更新，不引入永久 `MCC*` 兼容包装；外部二进制或源码需要升级或按迁移清单重编译。不得把 C# 代码改名误称为既有二进制兼容。
6. `FCMConfigItem` 字段、配置 Kind 枚举值/反序列化迁移、Connector Schema V2、底层网络字节协议及 Replay 文件标识 `MCCRPL01` 不因类型重命名而主动变动；并需有正反例测试保障。
7. 旧未编译 `Porting` 历史文件转移到 `ReferTo/Channels/Porting`，不参与新的生产程序集或工程引用。
8. 仍遵守“一 Connector 一 Channel 一 Protocol”、多对端归 SubChannel、协议分帧归 Protocol、Runtime / F19 唯一执行调度体系，专业 Driver 不被强制使用外部 Channel。

## 2. 依赖 DAG

```text
Foundation.Data ← Foundation.Transport (FCM configuration)
Foundation.Transport ← Mechanism.Connectivity → Mechanism.Core
Mechanism.Connectivity ← Mechanism.Data (MDS Protocol binding)
Mechanism.Connectivity + Foundation.Transport ← Tests
```

箭头表示右侧编译依赖左侧，`Mechanism.Connectivity → Mechanism.Core` 亦为现有项目引用。无 Foundation→Mechanism 的反向引用。

## 3. 不属于本次迁移

- `MPAProtocol*`、`MPALengthFrameCodec`、`MPAModbus*` 和 PointTable/Mapping 保持 Mechanism Owner。
- 不增建通信线程池、分布式 Broker、设备 SDK、隐式授权、安全联锁或运行调度框架。
- 不把 Windows 本机 Loopback 当作现场 PLC/RS-485/WSS/组播/HIL 验收。

## 4. 实施与证据

- 27 个原 Channel 正式源码按主公开类型拆为 56 个目标类型文件；2 个历史 Porting 非编译文件另存为参考。
- 修改 `CxVisual.Foundation.Transport.csproj`、`CxVisual.Mechanism.Connectivity.csproj`、`Mechanism.Presentation.Wpf` 和两项测试项目，保留 .NET Framework 4.8/x64 传统工程。
- 复用原测试的正例、异常、取消、重连、SubChannel Generation、Native Socket Loopback、配置及 UI 操作，补充运行程序集 Owner 的断言。
- 真实 Windows CI 完成与否只能按本轮最终提交的实际运行记录认定；见 [验收台账](../Testing/CxVisual_F17_Channel迁移_源码与Windows验收台账_V1.0.md)。
