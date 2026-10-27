# CxVisual Channel MCC → FTR 迁移映射 V1.0

> 2026-10-27；F17 唯一 Owner / CLR 完整类型身份变更。机械更名不等于二进制兼容；外部调用必须重新引用 `CxVisual.Foundation.Transport.dll` 并重编译。

## 类型迁移清单

旧命名空间为 `CxVisual.Mechanism.Connectivity`；下表新类型均在 `CxVisual.Foundation.Transport`，所属程序集 `CxVisual.Foundation.Transport`。同名旧协议/模块类 `MPA*`、`MCN*` 仍处于旧命名空间。

| 旧类型 | 新类型 |
|---|---|
| `MCCChannelKind` | `FTRChannelKind` |
| `MCCChannelConfiguration` | `FTRChannelConfiguration` |
| `MCCChannelConfigurations` | `FTRChannelConfigurations` |
| `MCCChannelConfigurationConverter` | `FTRChannelConfigurationConverter` |
| `MCCChannelTypeDescriptor` | `FTRChannelTypeDescriptor` |
| `MCCChannelState` | `FTRChannelState` |
| `MCCChannelTopology` | `FTRChannelTopology` |
| `MCCChannelIOMode` | `FTRChannelIOMode` |
| `MCCIOCapabilities` | `FTRIOCapabilities` |
| `MCCFrameDirection` | `FTRFrameDirection` |
| `MCCChannelFrame` | `FTRChannelFrame` |
| `MCCChannelDiagnostics` | `FTRChannelDiagnostics` |
| `MCCChannelDiagnosticsSnapshot` | `FTRChannelDiagnosticsSnapshot` |
| `MCCNetworkAddress` | `FTRNetworkAddress` |
| `MCCChannelReceiveContext` | `FTRChannelReceiveContext` |
| `MCCChannelBase` | `FTRChannelBase` |
| `MCCSingleChannelBase` | `FTRSingleChannelBase` |
| `MCCMultiChannelBase` | `FTRMultiChannelBase` |
| `MCCDatagram` | `FTRDatagram` |
| `MCCSubChannelAddress` | `FTRSubChannelAddress` |
| `MCCSubChannelState` | `FTRSubChannelState` |
| `MCCSubChannelInfo` | `FTRSubChannelInfo` |
| `MCCSubChannel` | `FTRSubChannel` |
| `MCCSubChannelDirectory` | `FTRSubChannelDirectory` |
| `MCCTcpClientChannel` | `FTRTcpClientChannel` |
| `MCCTcpServerChannel` | `FTRTcpServerChannel` |
| `MCCUdpChannel` | `FTRUdpChannel` |
| `MCCUdpServerChannel` | `FTRUdpServerChannel` |
| `MCCUdpMulticastChannel` | `FTRUdpMulticastChannel` |
| `MCCSerialChannel` | `FTRSerialChannel` |
| `MCCNamedPipeClientChannel` | `FTRNamedPipeClientChannel` |
| `MCCNamedPipeServerChannel` | `FTRNamedPipeServerChannel` |
| `MCCHttpClientChannel` | `FTRHttpClientChannel` |
| `MCCWebSocketClientChannel` | `FTRWebSocketClientChannel` |
| `MCCVirtualChannel` | `FTRVirtualChannel` |
| `MCCReplayChannel` | `FTRReplayChannel` |
| `MCCCustomChannelConfiguration` | `FTRCustomChannelConfiguration` |
| `MCCHttpClientChannelConfiguration` | `FTRHttpClientChannelConfiguration` |
| `MCCNamedPipeClientChannelConfiguration` | `FTRNamedPipeClientChannelConfiguration` |
| `MCCNamedPipeServerChannelConfiguration` | `FTRNamedPipeServerChannelConfiguration` |
| `MCCReplayChannelConfiguration` | `FTRReplayChannelConfiguration` |
| `MCCSerialChannelConfiguration` | `FTRSerialChannelConfiguration` |
| `MCCTcpClientChannelConfiguration` | `FTRTcpClientChannelConfiguration` |
| `MCCTcpServerChannelConfiguration` | `FTRTcpServerChannelConfiguration` |
| `MCCUdpChannelConfiguration` | `FTRUdpChannelConfiguration` |
| `MCCUdpMulticastChannelConfiguration` | `FTRUdpMulticastChannelConfiguration` |
| `MCCUdpServerChannelConfiguration` | `FTRUdpServerChannelConfiguration` |
| `MCCVirtualChannelConfiguration` | `FTRVirtualChannelConfiguration` |
| `MCCWebSocketClientChannelConfiguration` | `FTRWebSocketClientChannelConfiguration` |
| `IMCCChannel` | `IFTRChannel` |
| `IMCCChannelTransport` | `IFTRChannelTransport` |
| `IMCCMultiPeerChannel` | `IFTRMultiPeerChannel` |
| `IMCCByteStream` | `IFTRByteStream` |
| `IMCCDatagramIO` | `IFTRDatagramIO` |
| `IMCCChannelListener` | `IFTRChannelListener` |
| `IMCCSubChannelAware` | `IFTRSubChannelAware` |

## 源码/配置调用方

- `Mechanism.Connectivity/Connectors`、`Protocols`：显式 `using CxVisual.Foundation.Transport;`，维持 Connector 对 Channel、Protocol 的既定组合；Protocol 调用 Channel I/O，不把 CRC/Modbus 迁入 Foundation。
- `Mechanism.Presentation.Wpf`：通用 FCM 配置编辑器无需引入第二个编辑模型，动态 Channel.Kind 的创建仍通过 FCM 多态选择；`Mechanism.Connectivity` 项目直接引用 FTR。
- `Tests/ArchitectureSmoke` 和 `Tests/TestCenter`：通道示例、配置案例、Native Loopback、异常案例使用新类型；测试断言 Channel 真实程序集为 Foundation.Transport。
- `Foundation.Transport` 通过 `Foundation.Data` 复用现有 FCM，不依赖 Mechanism。
- `FCMConfigConverter`、`FCMConfigItem` 属性、`FTRChannelKind` 数值与旧 JSON Kind 字段维持；旧平铺配置仍走原 Converter 的显式迁移验证。编译后需实测 Roundtrip。
- Replay 的在盘魔数 `MCCRPL01` **原样保留**；不改变 Socket / Serial 线格式和 Protocol Codec。

## 兼容边界

类型重命名是经过用户批准的破坏性 API 变更。不保留双版本 `MCC*` 通道运行，原二进制客户必须更新。旧文档出现 MCC 名字视为历史引用，已由 F17 新裁决覆盖。生产现场、跨机器安全以及长期压测仍需分别实证。
