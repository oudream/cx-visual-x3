# CxVisual 统一功能模块体系——Module Center P0共享入口源码实施与Windows验收台账 V1.0

> 2026-10-28；**部分P0已落地，G1分项验收，G2/G3均未宣称完成。**
> 设计基线：[功能模块中心与三入口统一体验完整设计及验收 V1.0](../Architecture/CxVisual_统一功能模块体系——功能模块中心与三入口统一体验完整设计及验收_V1.0.md)。
> 五类模块仍为 Connector、Driver、Compute、Simulation、Service；本中心不是第六类模块或新Owner。

## 1. 真实源码与Git提交

| SHA | 修改 | 事实边界 |
|---|---|---|
| [00a99df](https://github.com/oudream/cx-visual-x3/commit/00a99dfb3feeecc2c2761aaaa7b8bda4a8b4258d) | 五类专业配置分页抽取为公共MPWModuleCenterPages；增加五类正式FCM Schema测试 | 不改变FCM/MHS唯一真源 |
| [35ff9ec](https://github.com/oudream/cx-visual-x3/commit/35ff9ec0a86229b524ea192369b0f2cb06aff135) | MPWModuleCenterControl共享WPF，TestCenter复用；Studio新增顶部按钮、AvalonDock doc.modules和STA检查 | Studio现有MOC源依旧为CPU演示SimulationResolver，不是工业Provider目录 |
| [0bc54a0](https://github.com/oudream/cx-visual-x3/commit/0bc54a05d6c18a1a441fdb8c0166b5956e3fe6ec) | TestCenter默认全宽正式配置布局；增加Connector UIA错误诊断 | 该布局修复自身并未修复Schema字段全隐藏 |
| [c3f6146](https://github.com/oudream/cx-visual-x3/commit/c3f614675b10cc51d2ef6f81ffb6215798eed571) | 全部分类、搜索与重置、FCM CapabilityIds声明只读投影、Studio负例 | Declared≠Resolved≠Available≠Authorized |
| [eb95505](https://github.com/oudream/cx-visual-x3/commit/eb95505fd1f1a8ddaefa7f2f00cf7090ce0c37ad) | 恢复MPWModuleCenterPage.Categories=null语义，五类分页测试 | null表示不按FCM注解类别过滤，空数组会隐藏所有字段 |
| [6d0767a](https://github.com/oudream/cx-visual-x3/commit/6d0767afd68e995ea3ac5280a3ea81efd8d8798d) | TestCenter显示/收起当前模块已声明能力 | 默认全宽编辑，能力只读，不触发设备执行 |

## 2. 真实Owner与公开入口

- Mechanism/Presentation.Wpf/Modules/MPWModuleCenterPages.cs：五类的Schema分页与未覆盖字段扩展页，仅视图分组。
- Mechanism/Presentation.Wpf/Modules/MPWModuleCenterControl.cs：通用WPF布局、五类/全部筛选、搜索、能力只读详情、专业Tab宿主、TestCenter全宽开关；没有私有能力注册中心。
- Tests/TestCenter/Manual/Module/TCTManualModuleFrame.cs：仍为正式MHS配置实例、草稿保护、Validate/Save/Apply/Reopen、FCM验证和TestCenter操作入口；MPW公共控件只提供视图。
- Applications/Studio/StudioShell.ModuleCenter.cs、StudioShell.cs、MainWindow.xaml：从顶部固定按钮或Ribbon进入同一个AvalonDock功能模块中心；当前MOC目录为SimulationResolver示例，只用于演示查询，不冒充实际五类能力支持。
- Applications/Studio/StudioShellSelfTests.cs、Tests/TestCenter/Automated/Cases/TCTModuleConfigurationCases.cs：Studio WPF自测、类别/搜索正反例与五类FCM Schema、Categories=null语义回归。
- Module Descriptor/CapabilityIds归MFM；MOC Catalog/Resolver归现有Owner；FCM/MHS唯一配置；MEM唯一工程身份；MRK唯一执行；M09/MDS、M36、M12各自管理变量/数据、消息、命令。不得新建并行ModuleManager、RuntimeHost、DataHeap。

## 3. Windows CI的固定SHA实测证据

### 提交35ff9ec

- [Studio S1 Windows Shell #40643903978](https://github.com/oudream/cx-visual-x3/actions/runs/40643903978)：**成功**；Studio真正打开共用MPW中心。
- [TestCenter Windows #40643903913](https://github.com/oudream/cx-visual-x3/actions/runs/40643903913)：**失败**，Connector独立UIA找不到正式Channel Schema卡片；解决方案构建步骤成功。

### 提交0bc54a0

- [TestCenter Windows #40644324616](https://github.com/oudream/cx-visual-x3/actions/runs/40644324616)：**失败**；诊断 Page=channels、VisibleEditors=0、HasConfig=True、AutomationCards=False，证明分页和配置Section存在，但由于新共享分页把原Categories=null错误换成空数组，MPW过滤掉所有字段。

### 提交eb95505（Categories语义修复后）

- [TestCenter Windows #40644682991](https://github.com/oudream/cx-visual-x3/actions/runs/40644682991)：GitHub Actions Job Step实际确认 **Build all solution projects 成功**、**Verify isolated Connector UIA PointTable and DataMapping 成功**、**Verify timeout and abnormal Worker exit fail closed 成功**。记录编写时其后续Worker/NUnit仍在运行，不能宣称整个Workflow通过。
- [Studio S1 Windows Shell #40644683008](https://github.com/oudream/cx-visual-x3/actions/runs/40644683008)：**成功**，覆盖MOC示例目录、全部/Driver空结果、搜索缺失/恢复、重复Dock Tab、工程Revision不变。
- [配置公共底座独立编译 #40644683285](https://github.com/oudream/cx-visual-x3/actions/runs/40644683285)：**成功**。
- [Graphics Editor #40644683060](https://github.com/oudream/cx-visual-x3/actions/runs/40644683060)：**成功**。
- [TestCenter Command Workspace #40644683042](https://github.com/oudream/cx-visual-x3/actions/runs/40644683042)：**成功**。
- [Config L1 #40644682953](https://github.com/oudream/cx-visual-x3/actions/runs/40644682953)、[Engineering Organization #40644683240](https://github.com/oudream/cx-visual-x3/actions/runs/40644683240)：**失败**；邻近提交日志曾报告ArchitectureSmoke运行时数据MDS FlowHeap/DataStack 100字节预算案例失败。需按固定SHA/Job继续核实，不作为模块中心专项通过。

### 提交6d0767a

- TestCenter能力目录展开/收起新增交互已经写入源码，但本次尚未取到相应SHA的Windows UIA单项通过证据；不应宣称UIA验收完成。

## 4. 下一轮未完成工作（严格区分目标与实现）

| 阶段 | 目标闭环 | 通过证据 |
|---|---|---|
| P0 | Studio从正式五类Provider/工程Scope发现能力；区别Declared、Supported、Available、Authorized；配置与MOC/MRK一致 | 真实Provider/Resolver、无支持/无权限负例、Studio和TCT双端Windows STA/UIA |
| P1 | Connector实际收发、Driver厂商SDK设备、Compute CPU数值/图像、Simulation Step、Service本地Request/Health各完成真实能力 | 每类实际业务输出、异常、资源回收和可复现报告 |
| P2 | 四大公共交互Produces/Handles、能力/流程装配及Operation/Trace追踪 | M09/MDS/M36/M12和MOC/MRK同Owner跨模块端到端测试 |
| P3 | Studio正式MEM模块资源、配置/流程/MOL/专业Viewer与诊断完全贯通 | 用户完整工作流及持久化/再打开/UIA |
| P4 | 并发背压、重连、故障恢复、实机HIL、性能、安全与工业长稳 | G2/G3实际现场日志，不能用Mock和编译取代 |

结论：**共享WPF中心、五类Schema配置、Studio入口、TestCenter原MHS操作和只读能力发现已阶段性落地并取得部分G1证据；真实五类Provider、工业动作、授权和正式工程闭环未完成。**
