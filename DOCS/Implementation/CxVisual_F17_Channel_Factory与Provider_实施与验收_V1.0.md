# F17 Channel Factory与Provider实施与验收 V1.0

2026-10-28。在原60项完整目标中推进24、34、49、50，保持Foundation.Transport唯一Owner，未增加项目或依赖。此前TCP P0已在固定SHA `549e7ed2be43a32952536b286e8c5ce771461631`通过[F17专项](https://github.com/oudream/cx-visual-x3/actions/runs/40540778043)与[完整TestCenter](https://github.com/oudream/cx-visual-x3/actions/runs/40540777960)。本增量的远端结果另按最终SHA补录。

## 正式行为

- `FTRChannelFactory.Create`在正式FCM转换器中快照配置，创建Closed实例，不连接设备；11种单实例内置通道与原12种能力发现可用。Virtual使用显式`CreateVirtualPair`，不暗造不可访问的对端；其已有收发模型和预算差距仍在完整审计中。
- `IFTRChannelProvider`提供现有`FTRChannelTypeDescriptor`和`Create(FTRCustomChannelConfiguration)`。Host显式注册，没有配置反射加载、进程全局注册表或另一种Channel接口。`SettingsJson`为最多65536字符的JSON对象，保留FCM序列化、迁移和校验；具体字段由Provider负责。
- 重复TypeId、覆盖内置类型、未注册创建、空结果、错Id/拓扑或非Closed结果拒绝。Provider代码在注册锁外运行；创建途中撤销则释放候选，不返回失效创建结果。
- 创建凭据用弱键记录实例/配置/能力，不延长Channel寿命。自定义Connector只能装配同Factory实际创建的实例；只写TypeId、跨Factory或改变配置不能冒充。已交付实例不可重复返回，拒绝时不能Dispose其它调用者的存活通道。
- 注册令牌幂等、按注册代次注销；旧令牌不能移除同名新注册。注销阻止未来创建，已交付实例及其凭据仍由原调用者拥有、关闭和释放。
- `MCNConnectorModule`保留原四参数入口，新增带Factory的正式装配入口；类型判断移回F17，Connectivity仍唯一判断Protocol兼容。`ValidateProtocolCapabilities`同样可接收Factory。未注册Custom仍失败关闭，原类型错配/Protocol能力阴性测试保留。

## 可运行的使用方式

```csharp
var factory = new FTRChannelFactory();
using (factory.Register(provider)) // provider由Host显式提供，声明能力并创建新的Closed实例
using (var channel = factory.Create(customConfiguration))
{
    var connector = new MCNConnectorModule(moduleDescriptor, connectorConfiguration,
        channel, protocol, factory);
    // 仍由既有Connector StartAsync/StopAsync管理运行；注册本身不打开设备。
}
```

可执行真实示例：`Tests/ArchitectureSmoke/Transport/FTRChannelFactoryTests.cs`的`RealProviderConnector`，Provider适配原TCP Client，连接OS回环Listener，经原Empty Protocol完成双向数据。其它专业SDK Provider不因此被声明已实现。

## 测试与边界

`--channel-transport`及默认回归调用`FTRChannelFactoryTests.Run`：12内置发现/11类型Closed创建、Virtual真实对、未注册与内置覆盖拒绝、FCM设置往返/非对象拒绝、实际TCP Connector双向收发、跨Factory与改配置拒绝、复用实例不得关闭原连接、注销后的实例所有权、旧令牌越代反证、Message/LengthFrame能力拒绝、创建途中注销与释放、空/错Id结果及Provider修改快照隔离。

本地Windows .NET48/x64：架构测试工程Restore/Build和完整方案Build成功，`--channel-transport`exit0且输出`F17.FACTORY PASS`，原40MiB双向及全部通道回环保留通过；ArchitectureSmoke默认完整回归exit0。并行Numerics已引入的固定NuGet依赖需Restore，本轮未改Numerics代码或依赖。远端Windows CI仍按最终实际结果补录。

完整Channel目标其余缺口（HTTP/WS Server、SharedMemory、其它类型后台接收/全部预算、自动恢复、全部TestCenter人工操作及长期/HIL）保持未完成，不以工厂可创建替代通道实际可用。

## 固定SHA远端Windows验收

源码`2dd60fa397b97d02373ec3ce2c1a4f0fbb8dab2b`：[F17专项40542193535](https://github.com/oudream/cx-visual-x3/actions/runs/40542193535)与[完整TestCenter 40542193827](https://github.com/oudream/cx-visual-x3/actions/runs/40542193827)均success。F17日志实际包含`F17.FACTORY PASS`、双向2GiB完成标记和62文件唯一Owner；TestCenter保留真实WPF/三维GPU、默认及并行重复回归。此证据不自动覆盖之后的UDP增量。
