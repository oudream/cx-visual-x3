> **历史API说明（2026-10-15）**：本文件记录L1～V1.1阶段的实现过程，文内部分旧名（如 CxConfigManager）已在V1.2按V6.4职责前缀更名。**开发与验收请优先使用 [V1.2统一前缀及案例基线](Config_V1.2_统一前缀_能力案例与验证.md)**，避免照抄历史代码段。

# CxVisual统一配置L2～L4：实现、操作与验收基线（2026-10-14）

> 本文是**实际源码实现与Windows测试的验收入口**。继承[配置L1](Config_L1_实现与TDD验收.md)的 `Id / SchemaVersion / Revision / Snapshot`，不建立第二套配置管理器。业务配置示例为模拟参数，**不连接PLC、CT机台或AI实机**。提交记录与实际CI日志才构成“通过”的证据。

## 1. 项目与Owner

| 层/Owner | 真实类型及文件 | 职责 |
|---|---|---|
| Foundation.Data F15 | `CxConfigJsonFile` / `CxConfigDocumentHeader` | 配置文档JSON、基础载体与版本头 |
| Mechanism.Core M04/M07 | `CxConfigManager`、`CxConfigLayeredProfile<T>` | 唯一注册、验证、持久化、备份恢复、显式Schema迁移、Scope层/Template引用/来源解析 |
| Mechanism.Runtime M30 | `CxConfigRuntimeSession<T>`、`CxConfigAtomicSlot<T>` | 已保存配置→待应用→Active快照；任务版本固定、应用边界、无锁原子读 |
| Mechanism.Lifecycle M39 | `CxConfigReleaseBuilder/Receiver/Publisher` | 不可变发布包、校验、目标暂存、激活、前版回滚、报告 |
| Professional.Automation/Vision/CT3D | 各 `*Options` 强类型模型与 `Validate` | 专业校验，不重新开发ConfigHelper |
| Studio与Runtime.Host | 按用途装配上述类 | Studio演示发布、Runtime进程独立确认 |
| ArchitectureSmoke | `ConfigL2L4Tests` | 层/模板/迁移/任务/原子槽/发布/进程读回的可执行验收 |

**项目归属边界**：M07不是M30执行内核，M30不是M39发布器；Studio/Host仅组装。L2～L4对外 API 通过现有L1 `CxConfigManager`读取与保存；只有运行期和发布期按其职责分离。

## 2. L2：作用域、继承、来源与备份

### 2.1 调用形态

```csharp
CxConfigManager m = new CxConfigManager(configRoot);
CxConfigLayeredProfile<CxVisionInspectionOptions> profile =
    CxConfigLayeredProfile<CxVisionInspectionOptions>.Register(
        m, "Vision.Default", CxVisionInspectionOptions.Validate,
        new CxConfigLayerSpec("Vision.Template", 10),
        new CxConfigLayerSpec("Vision.Machine", 20),
        new CxConfigLayerSpec("Vision.Recipe", 30));
m.Initialize();
profile.UpdateLayer("Vision.Template", x => x.GrayThreshold = 1000);
profile.SetTemplate("Vision.Machine", "Vision.Template");
profile.UpdateLayer("Vision.Recipe", x => x.MinDefectArea = 20);
CxResolvedConfig<CxVisionInspectionOptions> effective = profile.Resolve();
// effective.Value、Origins及SourceRevisions可以解释最终有效值来自哪一层。
profile.ClearProperty("Vision.Recipe", "MinDefectArea");
// 复原上一份bak，并作为新的单调递增Revision提交：
m.RestoreBackup<CxVisionInspectionOptions>("Vision.Default");
```

**算法**：`基础强类型配置 → 按优先级升序应用层显式字段 → 递归模板（检测循环）→ 业务Validator检查最终有效快照`。每层差异用现有Manager以独立配置文档保存及Revision追踪；未覆盖字段继承默认值。`Origins`表示最终生效字段来源，`SourceRevisions`记录参与解析的版本。

**边界**：这是**L2单配置组/单进程内解析+版本化持久化**，尚不是多文件跨机台的原子事务。复杂嵌套结构、模板字段权限和不同层业务安全白名单需要进一步丰富M04元数据与专业规则；当前通用层覆盖主要针对可描述的普通强类型属性。

### 2.2 Schema升级与回滚

`Register<T>(..., schemaVersion:2, migrate:(oldVersion, oldJson)=>new T { ... })` 显式决定老文档如何升级。旧版本备份保留、生成新Revision；没有Migration或遇将来Schema必须拒绝加载，不默默覆盖。`RestoreBackup<T>`按旧数据写入**新Revision**而不是修改版本计数，留下可追溯链。

## 3. L3：Snapshot、任务边界、实时参数

### 3.1 任务生效策略

```csharp
CxConfigRuntimeSession<CxVisionInspectionOptions> runtime =
    new CxConfigRuntimeSession<CxVisionInspectionOptions>(
        m, "Vision.Live", cfg => cfg.MinDefectArea > 300 ? "设备繁忙/禁止应用" : null);
using (CxConfigTaskSnapshot<CxVisionInspectionOptions> task = runtime.BeginTask())
{
    // 一次任务固定 task.Revision 和 task.Value，不因Studio后续保存新版本而改变。
}
runtime.RequestApply(CxConfigApplyPolicy.NextTask);
// 在下次空闲任务边界进入新ActiveRevision；还可选 Immediate/RestartSession/RestartHost。
```

- `CaptureActive` / `BeginTask` 生成独立快照；它们**不是硬实时循环读取API**。持有任务快照的工作持续使用自己的版本。
- `CxConfigAtomicSlot<T> where T:struct` 将整个值类型参数组和Revision封装在不可变State对象中；`Read()`单次原子获取，多字段一致、无文件IO和无锁，`Publish`只在版本递增时切换。
- `RequestApply`校验已保存版本和专业许可；失败不修改ActiveRevision。注意：**本版完成的是软件ActiveSnapshot状态管理**，尚未在具体设备Provider中执行PLC/探测器/运动轴参数实际下发、设备ACK或安全联锁，这些需对应专业实施和实机验收。

## 4. L4：不可变发布包、跨进程验证、双目标激活/回滚

### 4.1 发布包

`CxConfigReleaseBuilder.Build(manager, new[]{"Vision.Live"}, releasesRoot,"release-001")`生成：

```text
ReleasePackages/
  release-001/
    manifest.json
    payload/
      Vision.Live.json
```

Manifest记录ReleaseId、CreatedUtc、配置Id、SchemaVersion、Revision、SHA-256。Hash按**文件UTF-8字节**计算，接收端逐文件验证；发布源必须无未保存更改且磁盘Revision与内存一致。包名固定、不可原地覆盖。**SHA只校验意外损坏，不是数字签名或来源认证**，不能将此直接暴露到不可信网络通道。

### 4.2 接收、分发与反馈

```csharp
CxConfigReleaseReceiver rA = new CxConfigReleaseReceiver(runtimeDirectoryA);
CxConfigReleaseReceiver rB = new CxConfigReleaseReceiver(runtimeDirectoryB);
CxConfigPublishReport result = CxConfigPublisher.Publish(package.Path, rA, rB);
// Stage全部成功才依次Activate，失败会尽力回滚已切换的目标。
// result.Success/Error/StagedCount/ActivatedCount/RolledBackCount显示发布结果。
rA.Rollback();
```

每个目标维护`releases/{ReleaseId}`及单独的`active.json`指针，备份前一个指针。激活和回滚使用**跨进程排他锁+原子文件替换**；同一ReleaseId不同payload不得替换已暂存版本。多目标发布是**尽力补偿**，不是分布式原子提交；部分网络/磁盘故障可能仍需人工核对。

本版支持**同机不同进程/可信共享目录**中的收发与版本协同。尚未实现网络传输驱动、mTLS鉴权、多机台批量灰度编排、签名验证及回滚审批；这些都不能被“两个目标测试通过”所替代。

### 4.3 Studio和Runtime.Host真实入口

Studio顶部选择Studio/Automation/Vision/CT3D专业配置：
1. `编辑所选配置 (L1)`：修改并保存；
2. `发布示例配置 (L4)`：打包并激活到当前用户目录`%LOCALAPPDATA%/CxVisual/Runtime/Samples`；
3. `回滚发布`：切回上一个发布版本，或返回无活动版本。

Runtime.Host可由**另一进程**运行：

```powershell
$root = Join-Path $env:LOCALAPPDATA "CxVisual\Runtime\Samples"
.\CxVisual.Runtime.Host.exe config-status $root
.\CxVisual.Runtime.Host.exe config-check $root "Samples.Vision.Inspection" 1
# 受控本机操作员命令，非远程RPC：
.\CxVisual.Runtime.Host.exe config-stage $root "C:\release-package\release-001"
.\CxVisual.Runtime.Host.exe config-activate $root "release-001"
.\CxVisual.Runtime.Host.exe config-rollback $root
```

本地CLI不会自动连接设备或发出运动/X-Ray控制指令；实际机台发布需由专业Provider核准安全应用并返回设备实际完成结果。

## 5. 已有自动测试与新增验证链

| 编号 | 自动化用例 | 通过时证明 |
|---|---|---|
| L2-01 | Template/Layer优先级和字段来源 | 值正确、Origin正确 |
| L2-02 | 模板引用循环 | 循环拒绝且已保存层未损坏 |
| L2-03 | 清除覆盖字段/重开 | 正确回退到模板并完成持久化 |
| L2-04 | 专业跨字段校验 | 不允许使最终有效配置非法 |
| L2-05 | 备份Restore新Revision | 单调版本、恢复持久化 |
| L2-06 | 显式SchemaMigration | 旧Schema备份、新版本可加载 |
| L3-01 | 任务中保存新版本 | 正在执行的任务仍为旧快照 |
| L3-02 | NextTask / RestartSession | 只在允许的边界切换 |
| L3-03 | 专业检查拒绝 | Active不改变、错误可读 |
| L3-04 | AtomicSlot多字段快照 | 连续版本读取不撕裂 |
| L4-01 | 不可变打包+SHA校验 | 文件篡改拒绝 |
| L4-02 | 两个目标先Stage后Activate | 两个运行目录独立有版 |
| L4-03 | 激活历史回滚 | 可以读到上个有效发布配置 |
| L4-04 | 发布失败 | 保留旧Active，返回明确失败 |
| L4-05 | Runtime.Host独立进程CLI | 非单一进程内模拟，读取相同Active和Revision |
| L1回归 | 强类型编辑、WPF STA交互、F16基础诊断 | 公共基础与原功能不回退 |

主验证命令`.\Build\BuildAndTest.ps1 -Configuration Release`；CI `config-l1-ci.yml`运行Windows Release/x64完整解决方案、ArchitectureSmoke、Runtime.Host演示、WPF STA编辑交互。**是否通过以对应GitHub Actions运行日志为准**；编写测试≠测试通过，GUI手工发布按钮仍要在Windows检查。

## 6. 本轮交付边界和后续缺口

已提供L2～L4的**可运行基础闭环**，并不宣称所有原规划的企业级功能均已达到现场投产标准。仍需在后续专项中验收：多资源跨文件事务/引用权限、跨服务信任与签名/mTLS、真正多机台灰度与审批、专业设备的安全应用/ACK/状态回读、灾难恢复/断电耐久、性能/容量基准与真实X-Ray机台联调。以上功能不得使用当前软件测试替代实机验收。
