# CxVisual TestCenter · M36 消息中心专有测试工作区 V1.0

> 状态：本轮实际源码实现，Windows WPF/真实UIA验收以对应GitHub提交及CI为准。归属 Tests/TestCenter/Manual/Message；不新增项目，不创建第二个M36 Owner。

## 1. 接入既有框架

- **资源树**：`专有测试（人工操作）/消息中心测试`，模板ID `manual.message`，创建后的Instance通过既有 `TCTWorkspaceRegistry / TCTCreatedWorkspaceStore / DocumentHost` 保存和激活。
- **中央Frame**：`TCTManualMessageFrame`，Topic、MQTT +/#订阅表达式、Payload、文字/JSON文本/二进制Hex、来源、QoS、分发模式、Retain、请求/回复与有界消息日志。
- **右侧操作中心**：直接导出 `ITCTWorkspaceFrame.Commands`：订阅、取消、发布、请求/响应、刷新指标、暂停/恢复、清空记录。使用既有 Dock/布局/主题，不复制一套Ribbon。
- **底部**：保留TestCenter现有结果/Timeline/History/Diff；**人工消息日志不是正式NUnit/TCT Run/Attempt，不会自动产生Passed**。
- **消息真源**：每个打开实例持有独立 `MNMMessageCenter`，实际调用 `Subscribe<object>`、`PublishAsync<object>`、`RequestAsync<string,string>` 和 `Metrics()`；不建立影子TopicRegistry/Queue/Dispatcher。
- **生命周期**：关闭Tab注销观察订阅和临时应答订阅，Dispose本实例中心；已在途回调可自行完成；不是远端Broker或设备停止确认。

## 2. 真实功能操作

1. 右键“消息中心测试”创建测试实例；中央默认填入示例相机Topic和MQTT通配过滤器。
2. 点击“订阅”，对实际M36注册一个观察者；默认 `ReplayRetained=true`。可以编辑过滤器后重新订阅，非法过滤器拒绝而不拆掉已登记的有效订阅。
3. 输入 `Image001`，点击“发布”。`Text`按string发布，`Binary Hex`按byte[]发布，`JSON文本`是string（明确不冒充强类型DTO），真实返回`MatchedCount/DeliveredCount/FailedCount/Stage`。
4. 点击“请求响应”，默认由标注为“本地测试应答”的同进程测试订阅者响应 `LOCAL-TEST-ACK`，关联CorrelationId。取消勾选后不捏造应答，按真实2秒超时返回。`ReplyTo`与请求Topic可编辑。
5. 页面展示真实消息路由日志（上限250行）和 `Published/Delivered/Queue/订阅/Failed/Retain/P95`。没有订阅者时显示Accepted不等于实际Delivered，更不等于设备ACK。
6. 右侧“取消订阅”“暂停/恢复”“清空记录”均只影响本Frame生命周期和M36实例。

## 3. Windows验收

- `CxVisual.Unified.sln` 必须在 Windows 2022 / .NET Framework 4.8 / Debug x64完整编译。
- TestCenter `--ui-smoke` 原有基础界面不得退化。
- `--ui-automation` 操作真实TestCenter HWND：创建消息实例、树选择、UIAutomation.InvokePattern执行订阅和发布、读取真实MessageCenter交付形成的消息记录，随后关闭页，产出 `TestCenter-13-Message.png`。
- 其他消息性能/边界由现有 `ArchitectureSmoke.exe --message-center` 测试；此界面不声称覆盖外部Broker、跨PC MQTT QoS、设备HIL/硬件ACK。

## 4. 待发展（不冒充现有）

正式 `MessageTypeDefinition` DTO Schema编辑/版本管理、主动创建多订阅者会话、结构化JSON DTO强类型编码、订阅条件表达式编辑、压力测试统计图、实际MPA与设备消息注入、日志持久化与报告联动等后续按真实Owner和权限扩展，不在P0 UI中伪装“已完成”。
