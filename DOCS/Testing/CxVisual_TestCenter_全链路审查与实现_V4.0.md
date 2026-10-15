# CxVisual.TestCenter 全链路补强与Owner核对 V4.0

依据：项目原文《CxVisual_TestCenter_FMP子方面归属与公共接口依赖设计_V2.1.md》及《CxVisual_TestCenter_详细架构与类契约规格_V2.0.md》；代码核对基线为 `cx-visual-x3` 当前 master。本文是**实施审计与剩余门槛**，不可用“有类型文件”代替Windows编译/测试通过。

## 不变的架构裁决
1. 只增加并维护 `Tests/TestCenter/CxVisual.TestCenter.csproj` 一个 WPF NET48 项目，类型 `TCT`、接口 `ITCT`；不是第7 Workbench。
2. 每个Foundation/Mechanism子方面保留自己的语义对象与实现；`Foundation.Infrastructure` 仅按需被引用，不强制所有项目引它。生产工程**绝不能引用 TCT**。
3. Definition、Run、Attempt、ExecutionInstance、ArtifactRef分别有不同ID。TestCenter不创造第二套生产Runtime/Graph/Channel真源。
4. UI和报告只消费执行结果；缺生产Observer时必须显示未接入，不能渲染伪成功。

## V2.0 / V2.1 缺口 → 代码补齐

| 核心差距（源文档） | V4.0实现/动作 | 验收门槛 |
|---|---|---|
| 用例重复运行只有TestId，丢Attempt身份 | TCTTestAttempt（RunId/AttemptId/Iteration/Seed）、TCTTestRunSession、TCTTestEvidence | Worker往返校验所有ID；重复3次ID不重复 |
| 运行结果只有状态、文本，没有系统诊断与产物 | TCTTestDiagnostic、TCTArtifactRef、Core.Result.Artifacts/Diagnostics、TCTTestArtifactStore | SHA256篡改拒绝；XML/JSON/Text回显 |
| 资源冲突无保护，多并发重复写错 | TCTResourceGate排序持锁，单TestId默认互斥；FailFast保留未执行记录 | 并发、独占、故障恢复自测 |
| 无Setup/Teardown合同 | ITCTTestLifecycle、TCTTestLifecycleExecutor | Setup失败仍可Cleanup；Cleanup失败Error |
| 缺运行历史与对照 | TCTTestResultRepository（RunId归档）、TCTTestRunDiff、TCTSnapshotDiffService | 保存/加载JSON与JUnit并查差异 |
| 数值比较/断言偏少 | TCTNumericComparer、AssertNotEqual/Range/Sequence/Same等 | NaN/Infinity/容差/非法参数边界 |
| 观察与诊断缺稳定事件分流 | TCTTestObserverHub，有界、递增Seq，溢出明确异常 | 不静默丢弃 |
| 能力覆盖易被杜撰 | TCTCapabilityCoverageService采用已核实CapabilityId名单 | 未核ID显示Unmapped，不计Verified |
| 沙箱与Fixture数据易污染 | TCTTestSandbox受控清理、Fixture路径保护、Golden显式审批 | 越界与篡改反例自测 |
| CI只覆盖原来的7例 | 基础/机制真测 + TCT框架自测试例，Windows隔离Worker运行 | MSBuild完整成功，JUnit有0失败0错误 |

### 上述阶段**不代表**已经做到的部分
- 若生产层尚未提供正式Graph TypedPort/Compile诊断、Channel Frame/Protocol Session、Runtime ExecutionInstance/Lease/Trace Observer，则这些专业Viewer仍然是**只读扩展入口**，没有实际事件时显示“不支持/未接入”，不能凭TCT数据演示算覆盖。
- 外部NUnit/MSTest没有实际安装/接入时，ITCTTestFrameworkAdapter只是外部适配契约。本工程已有内部 TCTAttributeFrameworkAdapter，不可误报成完整第三方框架兼容。
- 真正Windows WPF手工交互测试、1h+耐久、内存泄漏、跨机器网络隔离、不同语言进程树回收，均需要额外可重复记录的证据。
- Capability正式ID来自经审核的能力规格表，**不能将当前样例中的占位CAP-字符串视为正式映射**。

## 验收命令（Windows x64）
```powershell
./Build/Verify-Structure.ps1
msbuild CxVisual.Unified.sln /m:1 /t:Rebuild /p:Configuration=Debug /p:Platform=x64
./Output/Debug/x64/CxVisual.TestCenter.exe --run --report ./Output/Debug/x64/TestCenter.junit.xml --format junit --isolate
./Output/Debug/x64/CxVisual.TestCenter.exe --run --profile Regression --repeat 3 --parallel 3 --history-dir ./Output/Debug/x64/TestHistory --report ./Output/Debug/x64/Regression.json --format json --isolate
```

报告必须保存RunId/AttemptId/Seed/OS/Commit和每次断言/诊断/Artifact；FailFast、取消、超时不许标绿，零用例不是成功。

## Owner和安全继续遵守
- Clock/日志/并发等生产公共服务只在真正共用时进入对应Foundation Owner；测试专有执行、Fixture、Assertion、Viewer留在TCT。
- 任何证据产物都限制大小、路径/哈希必须校验；远程通信/设备安全另由生产/硬件安全链保证，不等于测试Worker Kill。
- `--inproc` 仅可信开发测试；危险用例一律Worker并限制Timeout；大文件使用ArtifactRef不内联巨型JSON。
- 不得在CI报告暴露Token、密码、真实生产Barcode；不自动更新Golden。
