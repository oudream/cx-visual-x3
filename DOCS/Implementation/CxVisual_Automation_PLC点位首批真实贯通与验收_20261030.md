# CxVisual Automation——PLC/IO真实链路 WP01～WP02 首批实施与验收记录（2026-10-30）

> 状态：本次**正式源码/自动测试编写完成，Windows CI和实机验收以实际运行结果为准**。不是 PLC 产品功能完整、不是硬件HIL、不是已实现全部18专业领域。

## 一、实施目的与Owner

依据 Automation #00～#10、DOCS/01 统一架构，实装一条真实软件协议事务链：

`PAD稳定变量ID → MCNConnectorModule → MPAModbusTcpProtocol → FTRVirtualChannel/真实TCP → 回应与数据映射 → MDS唯一变量表`。

控制操作：`PADDeviceVariableProvider.WriteAsync(含宿主授权) → 临时请求值 → MPA 06协议确认 → MPA 03现场寄存器回读 → MDS提交事实`。协议已应答但回读超时/值不一致必须报告 `PADPlcOutcomeUnknownException`，不得无理由自动重发或宣称物理动作完成。

Owner保留：F17拥有Channel，MCN/MPA拥有Connector/Protocol/点地址，MDS拥有Current，PAD拥有设备变量定义与专业操作桥接。**不创建新项目、新协议栈、新CommandCenter、新变量仓。**

## 二、本轮正式文件

| 文件 | 真正新增/修改内容 |
|---|---|
| `Professional/Automation/Plc/PADPlcPointBridge.cs` | `PADPlcPointBinding`、`PADPlcPointBridge`、`PADPlcOutcomeUnknownException`；现有PAD写授权+MPA真实点表/读回 |
| `Professional/Automation/CxVisual.Professional.Automation.csproj` | 编译PLC桥接；增加对既有 Mechanism.Connectivity 与 Foundation.Transport 的单向直接项目引用 |
| `Tests/ArchitectureSmoke/Integration/AutomationPlcPointTests.cs` | 真实虚拟F17 Channel和Modbus MBAP/03/06的正反协议测试、MDS映射、未授权、回读不符、超时Unknown、恢复 |
| `Tests/ArchitectureSmoke/Program.cs` | `--automation-plc` 专项入口及默认回归入口 |
| `.github/workflows/automation-plc-g1-windows.yml` | Windows 2022/.NET48 Release x64 编译+专项G1，同 SHA 保留真实构建/测试证据 |

## 三、能力边界与工业安全

本轮仅实现 Modbus TCP Holding 单寄存器的 **Int16/UInt16** 读/写/回读逻辑；点表必须存在唯一的必需入站 MDS Variable Mapping，写点必须存在唯一且同变量的出站 Mapping，且地址/类型/读写权限匹配。首次读取与回读经 `MDSProtocolDataTargetAccess` 更新原有MDS，不在Automation复制 `Current`，临时写值不预先写入MDS。

写入权限仍必须由可信宿主的 `PADDeviceVariableProvider` 授权回调决定。现场部署时其上游须接入**既有 M12/MRK 身份、角色、设备互锁、独占资源租约**；本轮桥接本身**不具备认证安全控制和完整的跨宿主资源仲裁**。桥接仅验证 Holding寄存器的回读一致，不表示电机已到位、气缸已夹紧、X-Ray已停止出束。非幂等/危险控制不得直接以本次写函数取代现场安全控制与专业命令。

连接断开、协议超时、回读矛盾和设备代次变化均不得返回“物理完成”。自动G1采用 **VirtualChannel中的真实 Modbus FC03/FC06 报文及协议解析**，不将其称为实机G2/HIL。TestCenter工业设备面板和真实 PLC、Motion、X-Ray G2/G3 均仍待实施。

## 四、验收条目与证据等级

| 用例 | 正反场景 | 状态/验收 |
|---|---|---|
| AUT-PI-001 | 按 MPA 点地址读取真实 03 响应并映射 MDS | G1待 Windows CI |
| AUT-PI-002 | 授权操作后 06 写入+03 回读一致才成功 | G1待 Windows CI |
| AUT-PI-003 | 错误身份/错误类型不允许发送写命令 | G1待 Windows CI |
| AUT-PI-004 | FC06已应答但设备未修改：结果 Unknown、不自动重试 | G1待 Windows CI |
| AUT-PI-005 | 已写入但回读超时：Unknown、不能虚构最新MDS值 | G1待 Windows CI |
| AUT-PI-006 | 恢复后重新读取现场事实；停止后禁止再读 | G1待 Windows CI |
| AUT-PI-007 | 真实PLC型号/固件/网络/线缆/故障与读回 | G2未执行 |
| AUT-PI-008 | 实机安全链、工位互锁、负载与长稳 | G3未执行 |

执行：

```powershell
msbuild.exe Tests/ArchitectureSmoke/CxVisual.Tests.ArchitectureSmoke.csproj /t:Build /p:Configuration=Release /p:Platform=x64
Output/Release/x64/CxVisual.Tests.ArchitectureSmoke.exe --automation-plc
```

以上是后续现场/CI的测试命令，不表示在当前环境已经执行。

## 五、下一批任务

1. 若 Windows CI 报错，先修复当前小包，不放大为全专业空接口。
2. 承接当前 M12/MRK 的实装授权、资源租约和真实 Operation 结果，使非幂等命令跨进程/多设备都受正式控制。
3. 读点采样策略、断线 Bad/Stale 与现场PLC诊断/重连、点表导入/版本保存重开。
4. TestCenter/Studio 的真实 PLC/IO 专业操作页，先复用通用设备/变量视图和模块中心。
5. 再推进真实 Motion、气动/真空、搬运与 X-Ray/Detector 的多设备专业动作，G2/G3依设备而定。

**注意：文档 #09/#10 的206项类型是架构词汇，本包只新增必要的PLC桥接对象，没有批量生成候选类。**
