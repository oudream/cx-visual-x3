# CxVisual 统一配置体系 V2.4：阶段 C/D 实施与复核验收矩阵

日期：2026-10-16。依据 V2.4 主文档、阶段 A/B 矩阵和当日最新 GitHub master 实际源文件；Owner 仍为 Foundation.Data/Configuration 和 Mechanism.Data/Configuration，WPF/Studio 只是视图，不持有第二配置真源。运行时数据堆、栈、节点入参/出参与配置体系分离；跨进程生效、跨机发布和设备联锁暂不验收。

**证据等级：源码完成 != .NET48 编译 != 自动 STA/UI 测试 != 现场 WPF 人工交互 != 长时间运行和实机安全。**

## 一、本次初检发现并修复

| 缺口 | 改动 | 证据 |
|---|---|---|
| 大字典在 WPF 一次生成全部控件 | Dictionary、List、Set 每批 150 条，按钮按需加载 | MPWConfigEditorDialogV14.cs；250 字典项 STA |
| Dictionary 经 LINQ Cast<DictionaryEntry> 分页时抛 InvalidCastException | 改为 IDictionary.GetEnumerator().Entry，STA 保留异常原始栈 | 84e25b624f CI |
| UI Save / Apply 难以验证 | SaveDraftAsync、ApplySaved 执行原 EditorModel/Section 流程 | ConfigWpfTests |
| 恢复旧版本不易确定结果 | RestoreAsNewSavedAsync 创建新 Saved 版本，UI 按钮仍需确认 | ConfigWpfTests |
| 多模型 UI 只是草稿外观 | StageField、SaveGroupDraftAsync，整组一次事务，一次按边界生效 | MPWConfigAtomicGroupControl + ConfigWpfTests |
| 只有少数简单 UI 类型 | 专业复杂控件注册、动态字典、层级来源和覆盖、Studio 窗口/嵌入和通用组入口 | 现有 WPF/Studio |
| 非可信 UI 可能误用裸 Service | 授权 Section 复用现有 EditorModel；撤权重新检查，敏感值不作通用字段渲染 | MHSConfigAuthorizedSession、ConfigWpfTests |
| D 仅统计吞吐量 | 增加采样分位数、内存/GC、并发、订阅、10k 集合及 1000 修订独立跑批 | ConfigPerformanceTests、新 Windows workflow |

## 二、V2.4 D.2 U01—U12 逐项自检查

| ID | 内容 | 实现与自动验收 | 尚缺证据 |
|---|---|---|---|
| U01 | POCO 自动表单，基本字段/单位 | Schema 字段自动生成；STA 构造真实 WPF 控件 | 不同 DPI/键盘操作 |
| U02 | 嵌套与字典动态键 | 层级递归、键编辑、250 项分页；阶段 B Path 测试 | 六层深嵌入实际窗口体验 |
| U03 | List/Set/Nullable | WPF 控件和原 EditorModel 命令；阶段 B 集合正反向测试 | 鼠标/键盘连续排序与空值手工验收 |
| U04 | Range、Regex、异步校验 | 配置引擎真实校验；WPF STA 失败不改 Saved | 取消/等待交互与错误提示 |
| U05 | Undo/Redo | WPF 撤销/重做调用唯一 EditorModel；机制事务测试 | 连续 WPF 控件手动测试 |
| U06 | Saved/Effective | STA 真实 Save→Effective 不变→Apply→重启 | 设备 Readback 不适用此阶段 |
| U07 | Guard | STA Guard Deny 后仍 Pending、Effective 不变 | 实际设备安全联锁另行验收 |
| U08 | 历史 | 恢复旧修订作为新 Saved，STA 断言版本与值 | 确认对话框的人机操作 |
| U09 | 敏感/权限 | STA 敏感隐藏、授权撤销、底层授权与审计边界 | Clipboard/Tooltip/截图人工隐私审核 |
| U10 | 层级与来源 | 原子/独立层共同来源视图，路径覆盖命令及 SourceOf；STA 控件 | 三、五、六层的全套窗口人工操作 |
| U11 | Studio/独立/嵌入 | 同一控件及 Studio 入口，窗口代码编译、STA 构造 | 专业宿主与独立顶层窗口真人操作 |
| U12 | Dispatcher/关闭 | STA async 操作、取消草稿、权限撤销、Dispose | 反复开关窗口的长时泄漏/高 DPI |

自动测试完整覆盖的条目与需要人工验证的条目不同。**G6 的软件自动部分可由 Windows 同 SHA CI 判定；人工 GUI 门禁在没有实际手工 Windows 运行记录之前不能填写“全部通过”。**

## 三、阶段 D 实测用例与指标合同

| 子项 | 日常轻量 smoke | 独立完整规模 |
|---|---|---|
| PrepareValue/Value/Pinned/复杂值 | 20k 采样标量、400 采样复杂值 | 100k 采样标量、2000 采样复杂值 |
| P50/P95/P99/总耗时 | 实际 Stopwatch ticks→微秒；GC0/1/2、内存变化 | 同左，更多采样 |
| 并发读取与修改 | 1/4/16 读线程，每线程 1000 次，穿插保存和生效 | 同左 |
| 实时订阅 | 100 个，要求生效前零、成功后恰一次 | 1000 个 |
| 慢/异常消费者 | 8ms 延迟 + 抛异常，其他订阅者仍正确收到 | 同左 |
| 大集合 | 1k Dictionary 保存/Apply/重启/节点描述 | 10k Dictionary |
| 版本历史 | 100 次持久提交、Cursor 回放及重启核对 | 1000 次持久提交 |
| 故障保护 | 原阶段 A/CAS/文件锁/孤立修订恢复测试一并运行 | 同一软件代码基础，另做规模压力 |

打印 CONFIG_D_ENV / CONFIG_D_METRIC / CONFIG_D_CHECK：Windows OS、CLR/x64、CPU 核数、采样量、总耗时、p50/p95/p99、GC、内存、磁盘容量。绝不在无真实机台时序预算前发明“性能达标阈值”，采样本身不是性能 SLA 认证。记录原始日志/运行链接后方能做可靠性结论。

### 实际运行方式

Windows .NET Framework 4.8 Release / x64；两个入口均复用 Tests/ConfigFoundationSmoke 工程，不创建新项目：

- CxVisual.Tests.ArchitectureSmoke.exe --config-wpf
- CxVisual.Tests.ConfigFoundationSmoke.exe --config-performance-smoke
- CxVisual.Tests.ConfigFoundationSmoke.exe --config-performance-full

日常独立 Config CI 强制运行轻量 D；独立工作流 config-performance-acceptance.yml 在改动专项代码或人工触发时执行完整规模、保存指标日志 artifact。长时运行/实际断电/真实工业负载尚未运行，不能以当前数据虚构通过。

## 四、真实 CI 证据与剩余 Gate

- SHA 8edc4dbdf4ab8620fee41df6de3241baa914e011：完整 Build/Smoke https://github.com/oudream/cx-visual-x3/actions/runs/38925307159 与独立配置 https://github.com/oudream/cx-visual-x3/actions/runs/38925307117 及 TestCenter https://github.com/oudream/cx-visual-x3/actions/runs/38925307071：Success。
- SHA 84e25b624f950a9dbd9fc6ffdfa43993dab8f114：包含字典分页问题修复与 STA 堆栈保留，完整 Build/Smoke https://github.com/oudream/cx-visual-x3/actions/runs/38926229771、独立配置 https://github.com/oudream/cx-visual-x3/actions/runs/38926230019：Success。
- 完整规模 D 运行：https://github.com/oudream/cx-visual-x3/actions/runs/38925125043 。新增异常/慢订阅后再次运行：https://github.com/oudream/cx-visual-x3/actions/runs/38925861468 。**应以后续真实工作流最终状态为准，未 Success 不标为完成。**
- 其后任何提交（包括本矩阵入库）需按该最后 SHA 再核实独立配置和完整构建。先前 SHA 的绿灯不能直接当作新 SHA 绿灯。

G6 自动部分、G7 软件压测以实际对应 SHA 执行结果决定。G6 手工图形操作、24 小时以上持续运行、物理断电和生产安全验证尚待合适环境/真实工位。原 V2.4 需求、既有四类开发案例与阶段 A/B 的故障测试均不得删除。

## 五、最终复核清单

- [x] 未新建配置项目、配置真源、Facade、Security/Publisher 框架。
- [x] 验证 .NET Framework 4.8 Windows 构建；失败后保留原有测试而非跳过。
- [x] C 的保存、生效、历史、授权和多模型同组事务有 STA 正反向用例。
- [x] D 有性能分位数、GC、并发、订阅、规模集合与历史正确性断言。
- [ ] 最后一笔 master HEAD 逐项 Windows CI 和独立完整规模 D 的最终状态须复核。
- [ ] 完整 UI 人工体验、真机长时运行、断电恢复与目标 SLA 审批尚待执行。

**源码完备与“工业生产全面验收”不是同一件事。未达到的证据门禁应继续呈现为未验证。**
