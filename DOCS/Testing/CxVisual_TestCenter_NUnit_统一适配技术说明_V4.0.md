# CxVisual.TestCenter — NUnit Engine统一适配技术说明（V4.0 S1）

## 结论及Owner
仅在原有 `Tests/TestCenter` WPF .NET Framework 4.8 项目中使用 NUnit 4.4.0 / NUnit Engine API 3.22.0。生产 Foundation/Mechanism 不引用 TCT/NUnit。TestCenter仍是唯一测试入口、结果汇总和UI。NUnit负责原生 `[Test]`、`[TestCase]`、`async Task`、`[SetUp]` / `[TearDown]`。

## 实际落地
- `TCTNUnitFrameworkAdapter` 使用正式 Engine API 的 `Explore` 创建稳定 `NUNIT.<fullname>` 测试ID，不借用NUnit内部易变的 `id=mmm-nnn`。
- `TCTNUnitTestCase` 在受控线程内使用 `<filter><test>fullname</test></filter>` 单项真实执行；按 NUnit `result/label` 映射Passed/Failed/Error/Skipped/Inconclusive/Cancelled。
- `ITCTExternalResultTestCase` 只位于TestCenter Core，`TCTTestRunner` 将外部原生结果原样接入统一 Attempt/Worker/JUnit/JSON，完全不要求 Foundation 实现测试接口。
- NUnit 原生完整结果 XML 保存为 `Artifact`，`TCTAssertionResult` 只有桥接层的 NUnit verdict 元数据，**不冒充每一条 NUnit Assert 细目**；能力八维正式覆盖仍要显式审核。
- CLI `--run` 和 WPF Explorer 默认发现NUnit；独立 `--worker` 只有在 `NUNIT.` 目标时才加载NUnit，以降低历史TCT执行开销。
- NUnit执行由TCT已有Worker进程隔离和Timeout/Cancel治理；NUnit Engine内部ProcessModel=InProcess，避免无监管的多级worker。

## Windows CI验收
- NUnit Engine对WPF EXE的正常和故意失败PoC均真实运行。
- 统一TCT Catalog包含5个NUnit用例，JUnit报告包含这些用例，Worker能逐项恢复。
- `TCT_NUNIT_EXPECT_NEGATIVE=1`时仅执行一项NUnit真实失败，TestCenter退出1，JUnit恰好一条failed并保留原断言消息。
- 既有62项TCT回归继续通过；总用例数随NUnit增加而更新，并行Repeat=3保持每个AttemptId唯一。
- 不将上述验证替代用户实际WPF手工交互验收或专业能力正式规格覆盖验收。

## 非目标和后续
这不是立即删除所有TCT测试类；旧TCT自有功能/工业观察继续保留，逐步让常规同步/异步/参数化测试改用NUnit。若需异步动态Fixture、真正跨程序集发现、Golden审批、事件实时流、完整NUnit每条Assert追踪，应在后续迭代单独验收。
