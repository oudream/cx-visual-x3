# CxVisual Content 体系——完整正反测试、黄金案例与证据矩阵 V1.0

> 状态：测试规格，尚非实际执行报告。按 [#0需求 CNT-R01～R72](../Architecture/CxVisual_Content_00_总体架构与完整需求_V1.0.md) / [#1～#9](../Architecture/CxVisual_Content_09_全类UML与API合同_V1.0.md) 实施。每项测试必备输入、操作、预期、反例、清算/恢复、源代码/固定 SHA/OS 与日志/证据等级；不允许跳过失败例后宣称通过。

## 00. 统一验收等级与当前状态

E0=目标设计、E1=真实源码/Schema、E2=Headless 自动正反测试、E3=同一 SHA Windows Release/x64/.NET48 Studio/TestCenter/UIA（按适用范围）、E4=真实设备/HIL/工业安全。CurrentStatus=NotRun（本矩阵本身没有运行任何测试）；单一测试可只要求 E2 或 E3，触及危险真实设备才需独立 E4。硬件不可用时标 Blocked/NotRun，不能以软件模拟替代。

## 01. 六十条测试（CNT-T01～T60）

| ID | 主题/触发 | 正常预期 | 反例/必须拒绝 |
|---|---|---|---|
| T01 | 创建 Page.RootContent | 唯一合法 RootContent | 删除/双根拒绝 |
| T02 | 空白共享 Content | 合法 Definition/Revision | 非法 Parent 拒绝 |
| T03 | 类型化 Content | MPM Schema/默认 Port 保真 | 非法 Kind/Schema 拒绝 |
| T04 | 从模板创建 | 来源/依赖可追溯 | 模板缺依赖不假 Ready |
| T05 | ExtractReusable / Preview | IdMap/引用/Source 保存 | 无法 Remap Aspect 不静默清除 |
| T06 | MTI 包导入 | 源/依赖/版本与 GUID 处理 | 重复Id/坏包拒绝 |
| T07 | Type/Def/Ref/Run身份 | 多位置可区分 | 同显示名不串 |
| T08 | Embedded/Shared Parent | 拥有与引用结构合法 | 双父/拥有环拒绝 |
| T09 | 共用定义版本 | 两处引用固定目标 | 更新不热替在途 Plan |
| T10 | 两实例 Override | 不改 Definition，清除恢复继承 | 私有/只读/Infinity 拒绝 |
| T11 | 复制与迁移 | 同源 IdMap/外链保真 | 未知 Schema/断口拒绝 |
| T12 | 退役旧版本 | 影响/回滚可追溯 | 被使用定义不可静默删除 |
| T13 | 能力只读发现 | Catalog/来源/版本可查 | 浏览不改 Revision |
| T14 | 多 Provider/Module | 同 Capability 合同一致 | 重复冲突拒绝 |
| T15 | 能力至 Content 映射 | 显式 Type/Capability 关系 | 不按显示名猜 |
| T16 | 内部装配 | 公共 Port 与内部可映射 | 内部错误类型拒绝 |
| T17 | 对外组合功能 | 公开操作/结果稳定 | 未注册内部步骤不自发公开 |
| T18 | 缺 Capability | Unknown/Rejected 与原因 | 不默默替换 Dummy |
| T19 | Property/Config公开 | 默认/可写/范围正确 | 私有/ReadOnly 越权拒绝 |
| T20 | 强类型 Port | 方向/类型/单位匹配 | 不兼容单位/时基拒绝 |
| T21 | Event→Command | 请求Id与回执因果 | Request 非执行成功 |
| T22 | PublicMapping | 稳定 MemberId/映射 | 私有节点穿透拒绝 |
| T23 | 图像/CT 类型契约 | Coord/Spacing/Source 类型合法 | 单位/坐标不匹配拒绝 |
| T24 | 公共接口升级 | 兼容/破坏性影响可解释 | 偷换 PortId/类型拒绝 |
| T25 | Visual-only Content | 可保存/渲染 | 不要求 MRK 执行 |
| T26 | Logic-only Content | 可构建执行无 Scene | 不强加 Symbol |
| T27 | Data/Collaboration四方面 | 单一 MEM 定义持久 | 不可写第二 Graph |
| T28 | Content+Symbol+Group嵌套 | 合法复用引用、RootGroup 正确 | Group 业务孩子拒绝 |
| T29 | 五类连接区分 | 视觉改变不动 MOLLink | ZIndex 不能影响 Flow |
| T30 | Aspect/视图投影 | Schema/Id/Revision 一致 | unknown Required/坏 Payload 拒绝 |
| T31 | MOL V1/V2 互斥 | 单一 Logic Aspect 保存 | 双持久 Graph/无损不成立拒绝 |
| T32 | Control/Data/Stream | DAG/端口/分支正确 | 缺必填/环条件错误拒绝 |
| T33 | Loop/Branch/State | 合同与运行结果相符 | 非法 Exit/状态迁移拒绝 |
| T34 | InvokeContent | 子公开 Port 自动推导 | 无 Logic/坏引用拒绝 |
| T35 | 跨Schema调用 | 已支持子集能真实执行 | 不支持结构准确拒绝 |
| T36 | MOC Build+SourceMap | 定位 Block/Ref/Port | Build 不写 MEM Revision |
| T37 | 三级子流程 Export | 具名输出正确、私有不可见 | 子失败不泄漏 Pending |
| T38 | 同一共享子 Content 双调用 | Frame/参数独立 | 不能读上轮结果 |
| T39 | Event/Wait/Timer | 等待/唤醒/超时有因果 | 未获命令授权不执行 |
| T40 | Retry/Cancel/Timeout | 符合策略并清算 | 危险副作用禁止盲重试 |
| T41 | Parallel/Join/Resource | 全部分支真实结算 | 不能提前释放租约 |
| T42 | Loop多轮 Trace | Invocation+Iteration 独立 | 同 Block 最后值不能盖历史 |
| T43 | Variable/质量/数据绑定 | Good/Bad/Stale 分明 | 坏质量不作正确结果 |
| T44 | Image/Volume/Tensor Handle | 共享引用寿命合法 | 过期 Lease 拒绝访问 |
| T45 | FCM Config Schema | 类型/范围/单位正确 | 任意字符串偷越约束拒绝 |
| T46 | MHS Saved/Effective | 两版本有明确差异 | Saved 不直接当设备生效 |
| T47 | Definition/Ref Override | Scope/version 隔离 | A 实例配置不覆盖 B |
| T48 | 跨进程/作用域数据 | 来源/版本/质量可追溯 | 断线/旧回调不覆盖新Run |
| T49 | 资源依赖闭包 | Import/Publish 前完整校验 | 缺包/不信任拒绝 |
| T50 | 安全/权限 | Command 经 Host 与专业验证 | 无权仍能实机写入须失败 |
| T51 | 可信来源 | 只读元数据浏览 | 未知 DLL/脚本被执行拒绝 |
| T52 | 版本发布 | 旧 Plan Pin，新版本受控激活 | 在途 Flow 不热改 |
| T53 | 迁移/影响 | Ref/Port/Schema 差异/回滚可见 | 不可无损仍提交拒绝 |
| T54 | 审计/资源清算 | Trace/Unknown/Lease 准确 | 外部状态未知不伪 Completed |
| T55 | Studio Content Center | 三来源、搜索/创建/导航 | 浏览不修改 Project |
| T56 | 七来源统一创作 | Preview/Commit/Undo 单真源 | Cancel 不留下资源 |
| T57 | 四方面编辑与视图同步 | Outline/Visual/Logic/Data 同 ID | UI 私有 Save 与原工程偏离拒绝 |
| T58 | SDK 与 Studio 同义 | 同一 MEM/MOL 数据往返 | 不支持 Schema 显式拒绝 |
| T59 | 实时错误/诊断跳转 | SourceMap→原 Block/Ref | 同名实例误跳拒绝 |
| T60 | Windows UIA/功能回归 | DPI/多窗/键鼠/测试中心/Host 证据 | 关闭 Studio 不停止生产 Host |

以上每项是**测试合同索引**。实施时每条至少建一个成功和一个反例的方法/CaseId，复杂场景需要多个输入组合，不应拿一条简单断言关闭整个需求范围。

## 02. 八个黄金纵向案例（CNT-G01～G08）

| Case | 必须穿过的系统 | 最少软件证据 | 必须反证 |
|---|---|---|---|
| G01 Visual board | MEM→SymbolRef→Content→Page→MDS/Visual | 两页独立绑定、Save/Open、同定义覆写 | 无 Flow 也正常 |
| G02 Motor | Symbol→Content→MPC/MOL→Host→Automation→MDS | 请求/权限/回执/质量因果 | 无权/断线/Unknown |
| G03 Vision ROI | ROI/Source→Content/MOL→Vision→ImageHandle→Overlay | 两 Source/Run 不串 | 坏坐标/旧结果 |
| G04 CT3D | Projection→CT3D Content→Volume/Viewer | 体引用/三视图/释放/诊断 | 坏几何/租约/GPU |
| G05 Inspection | 三级 Content→MOC/MRK→MDS→Audit | 成功 Export/错误不泄漏 | NG/Review/取消 |
| G06 Simulation | Content→虚实可替换能力→Runtime | 虚拟模式及种子追溯 | 虚拟命令不写实机 |
| G07 Extract/Upgrade | MEM/MTC/MTI→Resource→Studio→Rebuild | IdMap/Undo/迁移/VersionPin | Aspect 无损不能证明 |
| G08 Combined | Automation/Vision/CT3D/Inspection/HMI/Simulation | 并发/迭代/多实例/Trace | 资源、跨PC、旧Run/未知设备状态 |

**设备HIL**：G02/G04/G08 中真实 PLC/运动/探测器/GPU/标定与物理时序、故障及安全隔离须 E4 单独签署。G03/G05/纯软件路径可先 E2/E3，但不冒充工厂验收。

## 03. 测试工程与方式

| Owner | 适合的现有路径 | 重点 |
|---|---|---|
| MEM/MPM/MPC/MTI | Tests/ArchitectureSmoke/Engineering | 身份/结构/Schema/包/事务/变更/Undo |
| MOL/MOC/MRK/MDS | Tests/ArchitectureSmoke/Orchestration 等现有目录 | 子调用、数据隔离、Flow/Trace/异常 |
| MVI/MPW | Tests/ArchitectureSmoke/Visual 与 WPF 相关测试 | 视图、DPI、几何与信号端口 |
| Studio/TestCenter | 现有 StudioShellSelfTests/TestCenter/UIA | 内容中心、按键/拖拽/焦点/状态 |
| Professional/Host | 各专业实际测试/Host 软件集成 | Provider、设备准入、质量与真实行为 |
| HIL | 专业设备测试台 | 实物联锁/异常/恢复/性能 |

不得为本矩阵临时复制另一个 Runtime Engine 或 WPF UI 测试假实现。任何测试资产需要具备可重复种子、可控输入、日志/截图、来源和安全隔离。

## 04. CI / 证据包格式

每项至少记录 TestId、RequirementId、CaseId、Owner、基线 SHA、实际变更 SHA、运行工作流/命令、OS/.NET48/x64、输入/配置/依赖/模型/设备、期望/实际、错误码/Trace、断言、执行耗时/资源、结论（Pass/Fail/NotRun/Blocked）、附件链接和遗留项。跨多个 SHA 的成功不合成“同 SHA 全绿”；取消/排队不算通过。

## 05. 严格的阶段出口

D0=规格和 Owner 审计、映射完备；D1=G01/G02/G05 优先验证三种核心 Content；D2=创建/提取/迁移可逆；D3=能力公开/四方面组合一致；D4=真实多级执行/错误/隔离；D5=数据/配置/授权/资源预算；D6=Studio 六专业与 UIA；D7=综合性能/跨PC/HIL 按证据等级关项。任一 P0 破坏性反例失败就不得宣布阶段完成。

## 06. 禁止的验收表述

“类型能注册=Content 可运行”、“画布能显示=专业完成”、“编译成功=设备安全”、“软件模拟=HIL”、“测试函数存在=测试已跑”、“旧 SHA 通过=最新 master 通过”、“导入包成功=Provider 已安装”、“取消请求=设备已安全停”。上述都是必须持续回归的反证。
