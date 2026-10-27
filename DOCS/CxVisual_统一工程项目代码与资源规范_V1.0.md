# CxVisual 统一工程、项目、代码与资源规范 V1.0

> **文档性质**：CxVisual统一平台工程实施规范（可指导人工开发和Codex实施）  
> **版本 / 日期**：V1.0 / 2026-10-13  
> **适用对象**：CxVisual.Unified新解决方案、28个正式项目、135个子方面、V7.8迁移与后续增构  
> **状态口径**：工程组织与技术路线**已讨论定案**；具体源码迁移、SDK恢复、Windows构建及工业现场验收**仍须验证**。本文不会把“制定规范”当成“已经实现”。

---

## 0. 定案摘要与依据

### 0.1 一次确定的结论

1. 采用 **Foundation / Mechanism / Professional Domain（F/M/P）** 三层：22＋40＋73＝**135个子方面**。完整解决方案**28个项目**：Foundation 8、Mechanism 9、Professional 6、Applications 2、Intelligence 1、Tests 2。
2. 保留两个解决方案：`CxVisual.Unified.sln`（28项目）与 `CxVisual.Foundation.sln`（基础8项目＋FoundationSmoke测试1项目）。
3. **传统格式 `.csproj` + .NET Framework 4.8 + VS2022/MSBuild17**，维持V7.8项目属性与设计器兼容。传统文件格式可用 `ToolsVersion="15.0"`；它不是VS版本号。
4. 新界面**统一WPF**：旧有少量WinForms界面**按功能改写**为WPF；不把旧Designer生成代码直接改后缀当成WPF。采用`XAML + Code-behind + ViewModel`，真实业务在Mechanism/Professional，不把工程对象复制到ViewModel。
5. 源码根目录统一：`Foundation / Mechanism / Professional / Applications / Intelligence / Tests / DOCS / DLL / Examples / Legacy / Build / Output`。不独设顶层`Migration`、`External`、`Adapters`、`Presentation`项目组。
6. `DLL/`只承载**实体DLL与厂商SDK等真实文件**；**不是NuGet包缓存**。实际引用、版本、路径首先依据**V7.8的`.csproj`、`packages.config`和已有本地DLL目录**，缺少实体库须报告，不擅自添加/替换/升级。
7. 开发编译输出集中到`Output/`，按项目、平台、配置隔离；部署与构建目录严格分离。
8. 软件部署保留**平级多版本**`App-V1.1/`、`App-V1.2/`、`App-V2.3/`，共享独立的`AppData/`，方便回滚。`AppData/Cases/`是**测试案例数据**，不当成产品配方或工程示例。
9. 编码依据 **Microsoft C# Coding Conventions（主规范）＋.NET Framework Design Guidelines（公共API设计）**；Roslynator辅助分析，StyleCop选择性启用，不照搬.NET Runtime全部内部规范。
10. 日志方案参考`cx-vision-x2/CxECTStudio`：**模块/专业分类、普通日志、关键事件专用日志、实时订阅、历史查询、界面过滤**。保留用户使用方式，但改造全局状态、文件名猜测分类、记录回收和线程资源管理。
11. 点检方案以`CxECTStudio`的**模块注册→点检项→专业服务→统一结果→人工确认→总表展示**为基底，完整迁移设计思想，**不增加独立点检csproj**；底座/专业/界面分属现有项目。禁止“默认执行即通过”。
12. 目标是**可编译、可运行、正常/异常闭环可测试**；当前规范本身不宣称任何28项目、点检或真实SDK已通过构建和实机验收。

### 0.2 规范来源与使用顺序（文档放DOCS目录中）

| 文档/源码 | 权威范围 | 说明 |
|---|---|---|
| 《CxVisual_01至05_统一规格基线》 | 需求、能力、对象规格 | 定义**需要提供什么能力**，不代表已经实现 |
| 《CxVisual_FMP_唯一类型体系》 | 类型身份、归属、重构与新增裁决 | 4,277为来源记录，4,206为待裁决身份槽位；不作为已实现类数 |
| 《CxVisual_三方面逐阶段》两册 | 135子方面、M0～M7阶段及验收门禁 | 《最终目标架构与类型实施规格》为目标口径；《原结构归属完善》保留历史审查口径 |
| 《CxVisual_Foundation与Mechanism_统一架构与对象API设计规格》 | 28项目归属、F/M对象API及依赖方向 | 特别是M.Core / M.Visual / Presentation.Wpf边界 |
| `CxVisual-V7.8.zip` | 真实旧代码、第三方引用、迁移证据 | **以实际源码和项目引用为准**，不以规划代替事实 |
| `oudream/cx-vision-x2/CxECTStudio`及`Common/CommonInterfaces` | **日志和点检**可复用原型 | 保留成功做法，迁移时重构耦合/安全缺陷 |

冲突处理：架构与Owner依V6.2和最新F/M/P归属；功能需求依V4.9；源码与DLL事实依V7.8；日志/点检的现有行为以`CxECTStudio`源码为证据。涉及**旧行为与新设计冲突**，记录差异、提出迁移决策和测试，不能静默覆盖。

---

## 1. 解决方案、项目与子方面——冻结

### 1.1 解决方案

| 解决方案 | 项目数 | 目的 |
|---|---:|---|
| `CxVisual.Unified.sln` | 28 | 全平台开发、集成、测试 |
| `CxVisual.Foundation.sln` | 9 | 8个Foundation独立构建＋`CxVisual.Tests.FoundationSmoke` |

`CxVisual.Unified.sln`中的Solution Folder按6个项目组展示；Solution Folder**仅是IDE组织**，实际目录和项目名才是编译真源。**不按135个子方面各建一个项目**。

### 1.2 Foundation：8项目、22子方面

| 实际项目 | 子方面Owner | 职责 |
|---|---|---|
| `CxVisual.Foundation.Core` | F01 F02 F08 F09 | 标识、值类型、集合、时钟和量纲 |
| `CxVisual.Foundation.Numerics` | F03 F04 F10 F13 | 数学、数值求解、采样信号、张量 |
| `CxVisual.Foundation.Geometry` | F05 F06 F07 | 2D/3D几何和空间变换 |
| `CxVisual.Foundation.Imaging` | F11 F22 | 图像像素、颜色、视口、基础2D呈现原语 |
| `CxVisual.Foundation.Volume` | F12 | 体数据、体素和网格基础 |
| `CxVisual.Foundation.Data` | F14 F15 F18 F21 | 编码、配置、文件、元数据与溯源 |
| `CxVisual.Foundation.Infrastructure` | F16 F19 F20 | 日志基础、并发、加密基础 |
| `CxVisual.Foundation.Transport` | F17 | 完整通用 Channel 体系：契约、FCM 配置、I/O、SubChannel、诊断、TCP/UDP/Serial/NamedPipe/HTTP/WebSocket/Virtual/Replay（正式类 FTR*/IFTR*）及底层传输基础 |

### 1.3 Mechanism：9项目、40子方面

| 实际项目 | 子方面Owner | 职责 |
|---|---|---|
| `CxVisual.Mechanism.Core` | M01～M08、M38 | CxNode工程、引用、属性、Port/Connection、事务及通用模板 |
| `CxVisual.Mechanism.Data` | M09 M10 M35 M36 | 变量、Binding、DataHandle/Stream、历史、通知 |
| `CxVisual.Mechanism.Logic` | M11 M12 | 表达式/规则/状态/命令/事件 |
| `CxVisual.Mechanism.Connectivity` | M13～M15 | MCN Connector、MPA Protocol、规约会话、PointTable、DataMapping、接入机制（依赖 Foundation.Transport，不再持有正式 Channel 实现） |
| `CxVisual.Mechanism.Visual` | M16 M17 M37 | **与UI框架无关**的绘图、Diagram投影、编辑及撤销重做 |
| `CxVisual.Mechanism.Orchestration` | M18～M25 | 节点拓扑、控制/数据流、跨层子流程及调度 |
| `CxVisual.Mechanism.Runtime` | M26～M34 | 资源、权限/联锁公共门禁、编译计划、执行、监控、调试及公共仿真 |
| `CxVisual.Mechanism.Lifecycle` | M39 M40 | 版本迁移、发布部署、插件注册 |
| `CxVisual.Mechanism.Presentation.Wpf` | **无M编号** | WPF画布、输入转换、呈现控件，不拥有业务工程真源 |

### 1.4 Professional Domain：6项目、73子方面

| 实际项目 | 子方面Owner | 职责 |
|---|---|---|
| `CxVisual.Professional.Automation` | P01～P18 | 设备、PLC、SCADA、运动、机器人、X-Ray及采集 |
| `CxVisual.Professional.HMI` | P19～P29 | 工业HMI和专业画面能力 |
| `CxVisual.Professional.Vision` | P30～P42 | 图像/标定/传统算法/AI |
| `CxVisual.Professional.CT3D` | P43～P53 | CT采集、重建、MPR、体绘、三维分析 |
| `CxVisual.Professional.Inspection` | P54～P65 | 检测工程、判定、复检、质量及追溯 |
| `CxVisual.Professional.Simulation` | P66～P73 | 设备/产线/场景仿真及数字孪生 |

### 1.5 其余5项目

| 项目 | 作用 |
|---|---|
| `CxVisual.Studio` | WPF应用、Shell、各专业界面组合与编辑工作台 |
| `CxVisual.Runtime.Host` | Headless运行/服务宿主，负责依赖装配 |
| `CxVisual.Intelligence` | 设计、生成、检查的智能辅助，不反向侵入Foundation |
| `CxVisual.Tests.FoundationSmoke` | Foundation基础、行为和边界测试 |
| `CxVisual.Tests.ArchitectureSmoke` | 依赖DAG、统一模型、运行/集成/迁移门禁测试 |

---

> **2026-10-27 F17 Owner 更新（覆盖本规范历史项目职责表中的旧描述）**：用户确认将完整通用 Channel 唯一归属迁移至 `Foundation.Transport`，统一 `FTR*`/`IFTR*`。`Mechanism.Connectivity` 只拥有 Connector、Protocol 和接入机制；维持 28 项目、FCM 唯一配置和 Foundation→Mechanism 单向依赖。见 [`F17 裁决`](Decisions/CxVisual_F17_Channel下沉与唯一归属裁决_V1.0.md)、[`全类映射`](Migration/CxVisual_Channel_MCC至FTR迁移映射_V1.0.md)、[`Windows 验收台账`](Testing/CxVisual_F17_Channel迁移_源码与Windows验收台账_V1.0.md)。

## 2. 源码根目录——冻结

```text
CxVisual.Unified/
├── CxVisual.Unified.sln
├── CxVisual.Foundation.sln
├── .editorconfig
├── .gitignore
├── README.md
│
├── Foundation/                  # 8个C#项目
├── Mechanism/                   # 9个C#项目
├── Professional/                # 6个C#项目
├── Applications/                # CxVisual.Studio、CxVisual.Runtime.Host
├── Intelligence/                # CxVisual.Intelligence
├── Tests/                       # 2个测试项目
│
├── DOCS/                        # 架构、规范、迁移、测试、运维文档
│   ├── Architecture/
│   ├── Requirements/
│   ├── API/
│   ├── Engineering/
│   ├── Migration/              # 文档及映射，不是源码项目
│   ├── Testing/
│   └── Deployment/
├── DLL/                         # 按V7.8真实引用与现有实体DLL组织
├── Examples/                    # 5大系统/8案例的源码级示例和验证资料
├── Legacy/                      # V7.8源码及原始ZIP，退出新解决方案编译
├── Build/                       # props/targets、依赖清单、编译/发布/检查脚本
└── Output/                      # 所有构建生成内容，禁止提交Git
    ├── bin/
    ├── obj/
    ├── publish/
    └── reports/
```

**约束**：`DOCS/Migration`只放旧→新类型和API映射、迁移说明及状态报告；正式新代码进所属项目。`Legacy`不得自动作为任何新项目的`Compile Include`或`ProjectReference`。`Examples/`与部署`AppData/Cases/`不是同一个概念。

### 2.1 项目内部目录

优先以唯一子方面ID分组，**仅为有内容的方面建内部子目录**，不机械制造135套空白的`Models/Services/Utils`。

```text
Mechanism/CxVisual.Mechanism.Core/
├── CxVisual.Mechanism.Core.csproj
├── Properties/AssemblyInfo.cs
├── M01.EngineeringModel/
│   ├── Contracts/
│   ├── Models/
│   └── Persistence/
├── M02.ReusableComponents/
├── M03.ReferenceIndex/
├── M04.PropertyMetadata/
├── M05.PortConnection/
├── M06.TimeSpaceUnits/
├── M07.TransactionConsistency/
├── M08.ValidationEvidence/
└── M38.TemplatesImport/
```

文件名通常与**主类型同名**（`CxNode.cs`），同文件放紧密绑定的小型内部辅助类型可以例外。一个公开类型具有唯一FQN及Owner；命名空间反映项目与语义，可用`M01`等编号作目录/设计追溯标记，**不强制所有公开类加FOI/MEM三字母前缀**。对已经对外使用的旧TypeId/FQN保留显式迁移或别名映射。

---

## 3. 项目格式、C#语言与编译规则

### 3.1 工程格式

- **正式定案**：传统MSBuild C#项目，`TargetFrameworkVersion=v4.8`，使用传统`ProjectGuid`、`AssemblyInfo.cs`、显式资源/XAML编译项、显式`ProjectReference`。
- `ToolsVersion="15.0"`作为工程文件格式兼容设置；VS2022/MSBuild17实际构建。
- 使用共享`Build/CxVisual.Unified.props` / `.targets`，在28个**新项目显式导入**，避免顶层`Directory.Build.props`误影响`Legacy`。
- 不用仅靠`Reference`从别的项目`obj`或`bin`指向输出DLL来取代`ProjectReference`。

### 3.2 C#语言版本的确认方式

- **拟统一**：新工程固定`<LangVersion>12.0</LangVersion>`，不使用`latest`漂移；`<Nullable>enable</Nullable>`可用于新代码（需确认使用的编译器/注解及历史代码兼容性）。
- **冻结前验证门禁**：VS2022、目标MSBuild和.NET Framework 4.8引用程序集下，实际成功构建含WPF的最小工程，并测试所有实际采用的语言特性；**C#12语法与.NET Framework 4.8可用运行API不是一回事**。不支持的运行库能力必须另设计兼容实现。
- V7.8原始代码不为了统一风格而进行大规模无意义改写；迁移时分项目消除语法和可空性问题。

### 3.3 编译组合

| 项目 | Debug | Release |
|---|---|---|
| 优化 | 关闭 | 开启 |
| `DEBUG` | 定义 | 不定义 |
| `TRACE` | 定义 | 定义 |
| 警告级别 | 4 | 4 |
| x64 / AnyCPU | 均有配置 | 均有配置 |
| 真实设备及原生SDK宿主 | 优先验证x64 | 必须验证发布x64 |
| 允许不安全代码 | 默认关闭，逐项目特批 | 与Debug一致 |
| 自动代码分析 | 按已启用的规则执行 | 同一规则，不额外静默放宽 |

严禁“配置名AnyCPU、实际`PlatformTarget=x64`”而不显式说明的情况。不同配置的`OutputPath`、`IntermediateOutputPath`不得相互覆盖。

### 3.4 输出路径

```text
Output/
├── bin/{Debug|Release}/{AnyCPU|x64}/{AssemblyName}/
├── obj/{AssemblyName}/{Debug|Release}/{AnyCPU|x64}/
├── publish/{Studio|Runtime.Host}/
└── reports/{build|test|migration}/
```

构建中间输出与正式安装包不是同一套文件。`publish`下由打包脚本组装**应用实际所需依赖**，不能直接复制全仓库`DLL/`。

---

## 4. 项目依赖与统一对象约束

**法则**：Foundation不能引用Mechanism/Professional；Mechanism不得引用Professional实现；Professional按需使用Mechanism及Foundation公开API；应用宿主负责装配；所有项目DAG无环。

| 项目边界 | 必须遵守 |
|---|---|
| `Mechanism.Core` | 拥有唯一工程定义、修改事务；不得引用Visual、WPF |
| `Mechanism.Visual` | 只引用Core及必要Foundation；不得引用WPF、Runtime、专业实现 |
| `Mechanism.Presentation.Wpf` | 引用Core、Visual、必要Geometry/Imaging；**不得引用Foundation.Volume或专业设备SDK** |
| `Mechanism.Orchestration` | 逻辑拓扑和执行语义；**不依赖Visual/WPF** |
| `Mechanism.Runtime` | Headless编译执行；**不依赖Visual/WPF** |
| `Professional` | 设备会话、专业算法与工艺Owner各自唯一；不能把它们上移成Foundation单例 |
| `CxVisual.Studio` | 编辑界面、专业UI组合及视图装配，不拥有第二套可写工程树 |

正式工程对象：`Project / Page / Content / Symbol / Group / Element`；统一`CxNode`身份、Ref、Port/Connection和四个正交Aspect（可视化、逻辑、数据、协同）。Symbol由Group复用形成；Graph可以是临时投影/编译中间态，**不能再产生第二棵持久化工程业务树**。Definition→Snapshot/Plan→Runtime分层，不在运行中直接任意改持久对象。

---

## 5. WPF设计界面、业务逻辑、样式及资源——冻结

### 5.1 主要组合方式

```text
CxVisual.Studio/
├── CxVisual.Studio.csproj
├── App.xaml                   # WPF应用入口
├── App.xaml.cs
├── App.config                 # 最小宿主启动配置
├── Shell/
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   └── MainWindowViewModel.cs
├── Features/
│   ├── Project/
│   │   ├── Views/ProjectExplorerView.xaml
│   │   ├── Views/ProjectExplorerView.xaml.cs
│   │   └── ViewModels/ProjectExplorerViewModel.cs
│   ├── Automation/
│   ├── HMI/
│   ├── Vision/
│   ├── CT3D/
│   ├── Inspection/
│   └── Simulation/
├── Commands/
├── Converters/
├── Behaviors/
├── Navigation/
├── Themes/                   # 应用主题覆盖
├── Styles/                   # 应用级样式
├── Assets/                   # 图标、图片等
├── Localization/             # 多语言resx
└── Properties/AssemblyInfo.cs
```

- **XAML**只写布局、视觉树、绑定和资源引用；**`.xaml.cs`**只承载WPF事件技术适配、生命周期及少量不可避免的UI操作；**`ViewModel`**承载页面状态、Command、校验反馈和公共机制调用协调。
- 设备、重建、AI、检测算法、真实编排**不写在ViewModel内**。
- WPF的`Page`/`ApplicationDefinition`、Code-behind `Compile`、`DependentUpon`、资源`Page/Resource/EmbeddedResource/Content`项按传统项目的正确MSBuild元数据声明，Visual Studio设计器应能正确打开。
- 旧WinForms可做迁移**参考**，正式WPF功能以同等可用性和行为测试验收。确有不可替代原生控件时，单列兼容问题审批；不把大量WinForms组件作为平台默认结构。
- `Mechanism.Presentation.Wpf`维护**跨专业复用的**控件、输入桥、DrawingTarget、公共样式；Studio维护应用主题覆盖。SVG资源优先；大型数据使用按需加载，不将大体数据变成XAML Resource。

### 5.2 WPF资源管理

| 文件 | 责任 | 规则 |
|---|---|---|
| `Themes/Dark.xaml`、`Light.xaml` | 主题总入口 | ResourceDictionary可切换，不重复保存业务状态 |
| `Styles/*.xaml` | 通用控件样式 | 公共在Presentation.Wpf，应用特有在Studio |
| `Assets/Icons/`、`Images/` | UI视觉资源 | Build Action按实际打包；记录版权来源；支持高DPI |
| `Localization/*.resx` | 多语言字符串 | 不在XAML/代码各自复制大量硬编码词条 |
| `*.xaml.cs` | XAML的技术行为 | 随`.xaml`嵌套显示，迁移时测试Designer加载 |

---

## 6. C#编码、对象及API规范——冻结

### 6.1 采用来源

1. [Microsoft C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)：首选编码、缩进、可读性和惯例。
2. [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/)：公开API和类型设计主规范。
3. [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)：大型工程参考，不要求逐条照搬。
4. [Roslynator](https://github.com/dotnet/roslynator)：辅助诊断/重构；[StyleCop Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)：选择性启用必要规则。

**执行文件**：根`.editorconfig`、`DOCS/Engineering/CSharpCodingStandards.md`、`DOCS/Engineering/ApiDesignStandards.md`，构建时输出分析报告。`editorconfig`规则应有明确严重度和例外审核机制；**不以美化格式掩盖功能变更**。

### 6.2 必须统一的基本规则

| 项目 | 统一约定 |
|---|---|
| Namespace、Class、Struct、Enum | PascalCase；项目与Owner对应 |
| Interface | `I`前缀＋PascalCase；接口代表实际契约，不为每类机械加接口 |
| 公开属性、方法、事件 | PascalCase；异步方法`Async`后缀；参数`camelCase` |
| 私有字段 | `_camelCase`；常量与readonly按Microsoft规则统一 |
| 类型文件 | 一公开主类型对应一文件；大类按职责拆分，不借`partial`逃避设计 |
| 日志及错误 | 带ModuleId、事件标识、运行上下文，不吞异常、不记录密钥 |
| 公共API | 完整参数/返回/异常/取消/幂等/线程/生命周期及兼容性说明 |
| 静态类 | **无状态纯计算优先静态**；设备会话、运行服务、点检Session必须是有状态实例 |
| IDisposable | 明确资源所有权、释放顺序及异常关闭语义 |
| 可空值 | 定义空值意义；公共集合默认尽量非null；不以`!`掩盖风险 |
| 注释 | 解释设计原因、工业约束和异常语义；公共API用XML文档 |
| 警告 | 逐项修复/有据抑制；不全局`NoWarn`清空不懂的警告 |

设计文档的F/M/P编号及短前缀服务于**归属和追溯**；代码命名不强制每个类按前缀重命名。`CxNode`等历史契约的兼容性比机械重命名优先。

---

## 7. DLL与NuGet：以V7.8真实源码为依据——冻结

### 7.1 确认的事实（已检查`CxVisual-V7.8.zip`）

- 包内共有**25个`.csproj`**，均使用.NET Framework 4.8传统项目格式。
- 包内**没有任何实体`.dll`文件**；因此不能声称原ZIP已经提供可运行SDK。
- 在V7.8 `.csproj`中，直接通过`DLL`相对路径引用的第三方程序集明确包含`halcondotnetxl.dll`和`hdevenginedotnetxl.dll`，原始`HintPath`示例为`..\..\DLL\halcon\halcondotnetxl.dll`等。**旧HintPath须按各自`.csproj`所在目录解析**，不能假定相同文本一定指向同一个绝对目录；新工程迁移时统一映射到`$(CxVisualDllRoot)`，核对实际文件后才能编译。
- 此外旧项目有来自`packages/`或`$(USERPROFILE)\.nuget\packages/`的程序集与包引用，例如：**Serilog、Serilog.Sinks.File、Newtonsoft.Json、YamlDotNet、OpenCvSharp、OpenTK、Roslyn、ZstdSharp、S7netplus**等。这些属于**包管理依赖**，不自动迁入`DLL/`。
- 用户现有设备仓库的DALSA、相机、探测器、ActiViz等**实体DLL目录**可保持，但**不能凭存在就给28项目全部添加引用**；只有V7.8真实代码或已批准迁移功能用到时才纳入项目依赖。

### 7.2 定案组织原则

```text
DLL/
├── halcon/             # 与V7.8的实际引用保持可映射
├── <实际需要的厂商或设备>/
└── ...                 # 按现有实体库实况组织，不预建大量空厂商目录
```

1. 每个第三方引用登记：**来源项目、原始HintPath、物理文件、版本、目标架构、许可/安装前提、目标Owner项目、是否CopyLocal、发布装配规则**。
2. 新工程可通过统一`$(CxVisualDllRoot)`及明确路径映射替代旧项目层级相对路径，但**实际文件名/版本未经验证不得改变**。
3. **实体DLL与NuGet严格分离**：NuGet沿用可恢复包的`packages.config`/`PackageReference`对应版本；不将包缓存变成手工管理DLL目录。
4. `.NET Framework`程序集绑定重定向、原生依赖、x64位数、PATH搜索、驱动安装及授权许可必须在每个发布目标机验证。
5. `DLL/`中不是所有文件都能自由公开分发；仓库是否提交二进制按版权/许可/体积单独审批。
6. 原始V7.8包含的WinForms/GLControl等依赖只作为迁移证据；WPF实现取代旧控件时，**不把旧依赖无条件带入新宿主**。

### 7.3 推荐恢复顺序

`分析V7.8.csproj → 建立映射清单 → 核实本机DLL与许可证 → 恢复NuGet精确版本 → 单项目编译/加载 → 按Studio/Runtime宿主收集 → 目标机验证 → 固化构建日志`。

**绝不因找不到SDK而创建同名空DLL、伪造实现或把设备点检结果默认设为通过。**

---

## 8. 开发配置、运行数据、日志、案例——冻结

### 8.1 三套目录严格区分

| 层次 | 例子 | 作用 |
|---|---|---|
| 源码 | `CxVisual.Unified/Foundation`、`DOCS`、`DLL` | 开发及版本管理 |
| 编译/发布中间产物 | `CxVisual.Unified/Output` | Debug/Release/obj/publish报告 |
| 实际安装运行 | `CxVisual/App-V1.1`、`AppData` | 可运行程序、共享运行数据、版本回滚 |

### 8.2 部署运行目录——保留现有平级版本

```text
CxVisual/
├── App-V1.1/                # 完整、可运行、只读视角的旧版本
├── App-V1.2/                # 完整、可运行的新版本
├── App-V2.3/                # 允许多个版本共存
├── AppData/
│   ├── Cases/              # **测试案例数据**（已明确）
│   ├── Configs/            # 设备/软件/业务配置与版本迁移
│   ├── Data/
│   │   ├── Projects/       # 工程数据（建议子目录）
│   │   ├── Results/        # 运行/检测结果（建议子目录）
│   │   └── CheckRuns/      # 点检运行记录和证据（建议子目录）
│   ├── Logs/               # 分应用/专业/用途的日志
│   ├── Cache/              # 可清理缓存
│   └── Temp/               # 可清理临时数据
├── Commands/               # 维护与版本切换命令
└── Tools/                  # 独立工具
```

保留用户熟悉的`App-Vx.y`平级目录，**不改为`App/Vx.y`**。版本切换仅改变启用的应用版本，不直接覆盖旧`App-Vx.y`或共享AppData。当前不为版本切换器单独添加第29个项目；先由`Commands/`脚本或现有启动方案承担，若需常驻服务另立审批。

### 8.3 配置管理

- `App.config`仅放宿主启动、BindingRedirect以及最小运行参数；业务/设备/点检方案放共享`AppData/Configs/`并通过统一配置服务访问。
- 配置具备`SchemaVersion`、来源优先级、验证、默认值、变更日志、迁移与备份/回滚。
- 关键配置变更使用**临时文件写入→校验→原子替换/备份**，多进程并发读写明确锁或版本冲突规则。
- 旧版应用与新版共享`AppData`前，必须执行**向前/向后兼容检查**；可能破坏兼容的升级先做数据快照，再允许切换。
- `Data/`及大型2D/CT文件支持配置独立数据盘；不默认把数GB～TB写到系统盘；数据索引、体数据文件、快照的拥有者和清理策略应明确。
- 安装在Program Files等受保护位置时，不可假定旁边AppData可写；提供显式`DataRoot`配置，默认为用户已授权的工业数据盘/目录，并检测权限。
- `Cases/`测试数据应标记环境和真机许可，**不能在生产运行默认自动执行带运动/出束的测试案例**。

---

## 9. 日志：继承CxECTStudio专业分类及专用日志——冻结方向

### 9.1 已核实的旧实现

已查看以下源文件：

- `cx-vision-x2/Common/CommonInterfaces/LogHelper.cs`：Serilog、异步队列/工作线程、日志事件、按天文件轮转（目前普通日志默认7个文件）。
- `cx-vision-x2/CxECTStudio/Common/StudioLog.cs`：`Modules`及Debug/Information/Warning/Error/Fatal统一入口，当前基于文件名匹配自动归类。
- `cx-vision-x2/CxECTStudio/Business/LogPageBusiness.cs`与`View/LogPageView.cs`：实时事件、历史记录、时间/级别/模块过滤、详情/导出，界面实时缓存最近300项。
- `cx-vision-x2/Common/CommonInterfaces/ImportantEventLog.cs`：关键事件的独立文件记录，含事件类型、操作者、模块、描述及扩展数据；有历史清理逻辑（目前基于日期的70天清理片段）。

这些属于**旧实现事实**，不意味着其所有异常路径已验证。

### 9.2 新平台保留/优化的行为

| 功能 | 规范 |
|---|---|
| 统一入口 | 保持简单的`Debug/Information/Warning/Error/Fatal`调用体验，底层允许Serilog实现 |
| 领域归属 | 每条日志显式记录`ModuleId`（F/M/P子方面或专业模块），而不是仅凭源文件名推测 |
| 用途分类 | `Operational`、`Error/Diagnostics`、`Performance/Trace`、`Audit`、`Check`等可区分 |
| 上下文 | 时间（含时区）、Level、ModuleId、Host、Process/Thread、CorrelationId、Task/RunId、DeviceId、UserId（按需脱敏）、EventId |
| 接入 | Runtime/各专业/Studio共用公共日志契约，不各建一个`LogHelper`实现 |
| 专用日志 | 操作审计、点检结果、关键设备/安全事件须独立存储和权限/保留政策，**不可仅依赖7天滚动诊断日志** |
| 实时UI | WPF日志页可筛选模块、时间、级别、关键词、设备、关联ID，支持暂停滚动、详情、导出 |
| 可靠性 | 文件IO异常不得递归写日志；异步队列有容量策略、停机Flush/Dispose、掉电恢复边界 |
| 性能 | 高频图像/采样日志可限流/聚合；不要把完整大图/体数据写入普通日志 |

**层次Owner建议**：F16负责最小日志基础接口/基础Sink；M08负责通用验证证据；M28负责安全/审计门禁；M33负责追踪性能诊断；M35负责历史/存储策略；各专业定义自己的`ModuleId`和专用事件；Studio只负责WPF日志浏览器。按现有M子方面组合，不新增`CxVisual.Logging`第29项目，也不在每层重复自造一套记录系统。

### 9.3 日志物理布局（建议默认，可配置）

```text
AppData/Logs/
├── Studio/
├── Runtime/
├── Automation/
├── Vision/
├── CT3D/
├── Inspection/
├── Check/
├── Audit/
└── Diagnostics/
```

**按专业/用途逻辑分类是必需项，具体是否独立物理文件可配置**；避免创建无数小文件。建议诊断日志先延续旧7天配置默认值，审计/点检证据保留周期由项目合规和生产需求确定，不能套用统一7天或统一70天。真实数据库、文件格式和恢复策略在具体实施时验证。

### 9.4 从旧代码迁移时必须修正

- `StudioLog`“文件名模式→模块”的规则改成**显式注册/注入ModuleId**；旧标签可做过渡兼容。
- `LogHelper`全局静态实例/事件要能区分进程、Host、设备Session，避免多应用生命周期相互影响。
- `ImportantEventLog`跨日切换时严格释放旧文件句柄；重要事件写入、保留与错误处理有测试。
- 日志页面不强行只保留300条历史数据；300只是内存/UI窗口约束，磁盘和查询应按分页/流式读取。
- 特定的审计事件不能静默被管理员点击“清空界面”而物理删除。

---

## 10. CxECTStudio点检框架的复用与改造——冻结方向

### 10.1 现有真实框架

参考文件：

- `CxECTStudio/Business/CheckPageModels.cs`：`ICheckModule`、`CheckItemDefinition`、`CheckExecutionContext`、`CheckItemExecutionResult`、`CheckItemResult`、Status模型。
- `CxECTStudio/Business/CheckModuleRegistry.cs`：模块注册表/基类；目前**运动、光源、探测器、其他**4个模块启用，共20项。
- `CxECTStudio/Business/CheckModules/*`：模块只管点检项清单和分发。
- `CxECTStudio/Business/CheckServices/*`：真实设备点检逻辑。
- `CxECTStudio/Business/CheckPageBusiness.cs`：结果合并、人工确认、自动执行到失败停止、总表快照。
- `CxECTStudio/View/CheckPageView.cs`：旧WinForms页面；新平台改WPF。

模体成像、几何放大比点检模块**有源代码，但在现有注册表中被注释停用**；不自动当成已启用/已验证的点检项。

### 10.2 目标抽象及唯一归属

| 目标能力 | 归属项目/子方面 | 说明 |
|---|---|---|
| `CheckDefinition/CheckItem/CheckResult`等稳定数据契约（最终类型名待V5.2去重） | `Mechanism.Core` M08及必要M01/M04契约 | 不绑定具体设备、WPF及存储格式 |
| CheckSession、状态流转、执行/取消/超时/恢复 | `Mechanism.Runtime` M30/M31，结合M18～M25编排 | **有状态实例**，不做进程全局静态业务类 |
| 结果历史、证据引用、审计 | M08/M28/M33/M35对应现有机制 | 使用公共证据/日志/存储，不再单造独立体系 |
| 运控、光源、探测器点检 | `Professional.Automation` P01～P18内具体Owner | 通过设备公开Provider/契约，避免绑Xob/COM单例 |
| CT成像/几何/重建质量点检 | `Professional.CT3D` | 领域算法归CT专业 |
| 相机/视觉算法点检 | `Professional.Vision` | 领域算法归Vision专业 |
| 检测和工艺结果验证 | `Professional.Inspection` | 判定/产品/缺陷质量归检测专业 |
| 模块列表、点检总表、人工确认WPF | `CxVisual.Studio/Features/Check/` | UI不直接持有设备硬件对象 |

这里的`CheckDefinition`等为**设计概念/候选命名**，不是承诺新加未裁决类型；必须先与V5.2的唯一身份清单比对后确定正式FQN。

### 10.3 标准业务闭环

```text
注册专业Check Provider
    → 读取点检方案与阈值、能力/设备绑定
    → 校验权限、互锁、设备可用性
    → 创建独立CheckSession、记录启动操作者与设备
    → 执行单项/自动批量（可取消、超时和失败停止）
    → 自动测量与/或等待人工确认
    → 记录状态、参数、原始证据与故障原因
    → 结果持久化、汇总状态、独立日志
    → WPF实时显示、历史查询与导出
```

状态最少包括：`Pending / Running / WaitingManualConfirmation / Passed / Failed / Skipped`；建议增加“取消/超时/不可用”的明确语义，不能映射成Pass。点检成功/失败需要能够找到由哪个Provider、何时、以什么设备状态与阈值判定的证据。

### 10.4 必须修正的旧代码问题

1. `CheckModuleBase.Execute`旧默认实现直接`Passed`，**新基类禁止默认通过**；未实现返回Unsupported/Failed并显式诊断。
2. `CheckPageBusiness`旧为静态全局状态、Results仅驻内存；改成多实例Session、线程安全结果、持久化和恢复。
3. 旧实现用`Task.Run`包裹实际硬件调用；新实现需要设备超时、CancellationToken、异常捕获、不可取消动作的补偿和状态机保护。
4. 仿真/离线案例**明确标记Simulation**；不能用模拟通过作为实机点检合格。
5. 人工确认有操作者、权限、时间、原因和前置测量；没有先实际执行的人工点检不能凭按钮伪装为自动检测通过。
6. 运动/高压/出束点检必须受现场硬件安全许可约束，软件Interlock不能替代安全PLC、急停、安全继电器；必要时进行双确认和受限模式控制。
7. 新点检不绑定`CxECTStudio`的旧UI类型、旧设备单例或静态业务类；旧点检方法的可复用算法与工艺阈值要逐方法迁移、测试。

### 10.5 点检最小验收

无设备（模拟Provider）：4模块/20旧点检项的展示、顺序、故障/取消/人工等待、失败停止、重复执行、历史证据、权限、日志可测试；有设备时才开展运动和出束真实专项测试。按专业增加新点检项不用修改公共点检运行内核。

---

## 11. 版本、升级、回滚与运行安全

1. 程序文件按`App-Vx.y`保持不可变；版本切换前停止Studio/Runtime及正在执行的任务，释放设备Session。
2. 每个版本发布包包含**Build Manifest**：程序版本、程序集版本、工程Schema兼容区间、依赖清单、设备驱动版本、文件SHA256/签名（按许可）。
3. 共享`AppData/Configs`迁移前快照；每次Schema迁移记录原/目标版本、操作结果和可回滚性。升级后若旧版本不兼容新数据，必须通过**恢复备份**回滚，不能假设旧版自动支持新Schema。
4. 禁止覆盖旧版DLL/EXE，也不要让多个版本同时写同一设备专用数据库或控制同一硬件。并发冲突使用运行实例/设备资源租约管理。
5. 发布中区分**Studio、Runtime.Host、Commands、Tools**实际依赖；对DLL缺失、x64不匹配、驱动未安装、HALCON许可证异常给出明确诊断。
6. 业务配置、点检证据、审计日志、检测图像与CT大数据不随程序文件卸载而被无条件清除；数据清理遵守Retention政策。

---

## 12. 测试、CI与实施门禁

### 12.1 创建骨架时立即执行（不等功能移植后）

- 生成**28个真实`.csproj`**、2个SLN，目标子方面目录和唯一归属映射，且无额外虚假项目。
- 扫描**所有项目引用构成无环DAG**；禁止Foundation→Mechanism/Professional、Mechanism→Professional实现、Core→Visual/WPF、Runtime→WPF。
- 检查`Output/bin/obj`无项目、平台与配置的路径碰撞；`Legacy`源码无意外编译引用。
- WPF Studio能显示最小Shell，并能切换内置资源主题、载入演示View，设计器能够打开相关XAML。
- Logger在Studio/Runtime/专业模块均可记录、按模块过滤、关闭Flush；专用审计/点检日志独立存在。
- 点检框架在**Fake Provider**下可完整运行“注册→检查→人工确认→失败停止→保存→重载→显示”，没有未实现即Pass。
- `.editorconfig`、公共props/targets、依赖清单及检测脚本生效，测试退出码明确。

### 12.2 逐阶段实施

| 阶段 | 主要目标 | 验收标准 |
|---|---|---|
| M0 | V7.8类型、DLL、API、现状基线 | 可追溯映射与依赖报告，区分源码和规划 |
| M1 | 统一CxNode工程对象、保存加载 | 完整工程往返，Ref/Port/Connection一致 |
| M2 | 编排、编译、运行 | 三层子流程和正常/取消/失败闭环 |
| M3 | WPF可视编辑 | 编辑→Undo/Redo→保存→运行态投影一致 |
| M4 | Automation/HMI/虚拟设备 | 虚拟设备完整运行、异常恢复和安全检查 |
| M5 | Vision/Inspection | 真实图像、缺陷、判定与证据回放 |
| M6 | CT3D | 投影→重建→MPR/体绘→结果；大内存生命周期 |
| M7 | 平台全面验收 | 五大系统与八案例逐项闭环，部署与回滚 |

**四级证据分别记录**：源码声明、编译结果、自动化/手工集成结果、设备/工业现场实证。不能用单一“完成”列替代。

### 12.3 构建建议

```powershell
# 示例脚本名，创建工程时实施并验证实际工作路径
.\Build\Build-Unified.ps1 -FoundationOnly -Configuration Debug -Platform x64
.\Build\Build-Unified.ps1 -Configuration Release -Platform x64
.\Build\Check-Architecture.ps1
.\Build\Check-DllReferences.ps1
.\Build\Check-Outputs.ps1 -Configuration Release -Platform x64
.\Build\Run-SmokeTests.ps1
```

上述命令是**应实现的脚本接口规范**，不是现在已经创建且执行成功的命令。

---

## 13. Codex实施规程

### 13.1 必须使用的输入

1. 本V1.0工程规范；2. 四大文档当前版本；3. `CxVisual-V7.8.zip`真实源码；4. 日志与点检迁移以`cx-vision-x2/CxECTStudio`源码为证据；5. 实际本机`DLL/`与NuGet安装情况。

### 13.2 Codex一次任务应提供

| 类别 | 必须交付 |
|---|---|
| 修改文件 | 新增/迁移/变更的完整路径及说明 |
| 归属 | 项目、F/M/P子方面ID、唯一Owner |
| API | 可编译签名、行为、异常、线程/资源边界 |
| 引用 | ProjectReference、DLL/NuGet依赖、版本、发布项 |
| 兼容 | 旧FQN/TypeId、工程Schema、升级与回滚处理 |
| 质量 | 构建输出、静态分析、单测/集成/工业案例证据 |
| 限制 | 不能实机测试的原因与未验证列表 |

### 13.3 明确禁止

- 为了达成类数量新增没有实用场景的空类/接口；不经V5.2审批强行去重或重命名。
- 改回48项目、额外建立`Adapters`或`Presentation`项目组，或给135子方面都建单独csproj。
- 将WPF/UI、设备SDK、专业算法写入Mechanism.Core或Foundation；建立第二持久Graph/工程树。
- 构造虚假的SDK DLL、模拟硬件通过当作真实运行、点检默认Passed。
- 将`DLL/`与NuGet混合、随意升级包版本、用绝对本机路径作为唯一构建方案。
- 把`AppData/`打包进版本升级覆盖，或将所有运行日志、项目文件写到`App-Vx.y/`。
- 在没有Windows构建证据时宣称VS2022编译通过；没有现场证据时宣称工业闭环完成。

---

## 14. 当前剩余事项：属于实施验证，不再重新讨论大架构

| 项目 | 当前结论 | 完成条件 |
|---|---|---|
| 28项目、135子方面、根目录及Owner | **设计冻结** | 新SLN和全部项目通过结构检查 |
| WPF为主及少量WinForms重写 | **定案** | Designer、Shell及专业示例可运行 |
| 传统.net48 `.csproj` | **定案** | Windows VS2022/MSBuild实际编译 |
| **C#12统一编译** | **暂定，须验证** | 目标IDE/引用程序集/特性通过真实编译运行 |
| V7.8 DLL沿用 | **原则冻结** | 恢复实体DLL和NuGet、固定版本及加载/授权测试 |
| CxECTStudio分模块日志 | **目标设计冻结** | 新结构化日志、专用审计、UI查询与异常测试 |
| CxECTStudio点检框架复用 | **目标设计冻结** | Session＋真实专业Provider/Fake Provider双路验收 |
| 多版本App-Vx.y平级部署 | **定案** | 升级、切换、Schema备份及真实回滚测试 |
| 运行数据物理路径与权限 | **路径结构定案，部署环境待验证** | 不同机器/数据盘/权限/多进程场景通过 |
| 审计/点检/结果保留期限 | **独立策略定案，具体天数留业务配置** | 现场工艺与合规期限确定并有自动清理测试 |

**最终裁决**：现在可以依据本V1.0创建完整28项目工程骨架；进入代码迁移时，应将上述“待验证”门禁变成测试证据，不以无法验证为理由修改已定案的总体目录或项目数。

---

## 附录A：关键源码位置（已核对）

**V7.8上传包**：`目录（D:\cyg\cx-visual-x2）`内的`CxVisualX2.sln`，`Host/Studio/Studio.csproj`、`Platform/Platform.csproj`、`Platform.Presentation.Wpf/Presentation.Wpf.csproj`、`Platform/Image/Halcon/HalconDriverModule.csproj`、`Workbenches/*/*.csproj`、`Tools/CxECT3DViewer/CxECT3DViewer.csproj`等。V7.8包里**未附实体DLL二进制文件**。
- 可以在目录（D:\cyg\cx-visual-x2）中找到V7.8最新源码

**CxECTStudio参考源码**：

- https://github.com/oudream/cx-vision-x2/blob/master/CxECTStudio/Common/StudioLog.cs
- https://github.com/oudream/cx-vision-x2/blob/master/Common/CommonInterfaces/LogHelper.cs
- https://github.com/oudream/cx-vision-x2/blob/master/Common/CommonInterfaces/ImportantEventLog.cs
- https://github.com/oudream/cx-vision-x2/blob/master/CxECTStudio/Business/LogPageBusiness.cs
- https://github.com/oudream/cx-vision-x2/blob/master/CxECTStudio/Business/CheckModuleRegistry.cs
- https://github.com/oudream/cx-vision-x2/blob/master/CxECTStudio/Business/CheckPageModels.cs
- https://github.com/oudream/cx-vision-x2/blob/master/CxECTStudio/Business/CheckPageBusiness.cs
- https://github.com/oudream/cx-vision-x2/tree/master/CxECTStudio/Business/CheckModules
- https://github.com/oudream/cx-vision-x2/tree/master/CxECTStudio/Business/CheckServices


**四大文档在DOCS目录**：

**五大代码在github**：
- https://github.com/oudream/ccxx/tree/master/ccxx
- https://github.com/oudream/device_communicator
- https://github.com/oudream/gcl3
- https://github.com/oudream/cx-vision-x2
- https://github.com/oudream/cx-cct-x3

> **审核提示**：上述GitHub仓库可能需要既有访问权限。文档以读取到的仓库源码为依据，不假定所有参考分支与本地已部署版本字节完全相同。真正迁移时应锁定Git Commit SHA并记录差异。
