# ADR（建议审评）：CxVisual Content 体系——对象、能力组合、流程、生命周期和 Studio 体系

> 日期：2026-10-31。状态：**Proposed，待负责人逐项 Accept/Modify/Reject**；不是批准的公共 API，也不代表源码已实现。上位：DOCS/00～04、工程组织 V1.2、Symbol V2.0、Decision/Runtime/配置/数据现行 Owner。总设计入口 [Content #0](../Architecture/CxVisual_Content_00_总体架构与完整需求_V1.0.md)。

## 01. 已有裁决无需重新争论

MEMProject/Page/Content/Symbol/MRCGroup/Element 是唯一工程树；Content 嵌套和资源引用分离；Symbol 唯一 RootGroup；Content 有可选四方面，MOL Logic Aspect 单一真源；MOC/Decision 构建、MRK/F19 执行、MDS 管运行值、MHS 管生效配置、专业管业务/设备安全；Studio 不复制编辑/Undo/Runtime。这些作为已定边界，不在新 ADR 中以“建议”重新打开。

## 02. 待负责人确认的 Content 专属建议

| ADR | 提议裁决 | 必须核查的前提与替代 |
|---|---|---|
| CNT-01 | Content 是统一内容/功能组合与复用主载体，但不强制所有函数/Viewer/服务包装为 Content | 避免吞并纯算法 SDK 与专业 Owner |
| CNT-02 | Content 创建从空白/类型/模板/共享/提取/能力/导入七来源进入同一 MEM 事务 | Studio 既有双中心、MTI/MTC |
| CNT-03 | 可选 Visual/Logic/Data/Collaboration 四方面；不新增“展示/服务/流程”三种 MEMContent 子类 | 现有 MPMAspectKind 与持久化 |
| CNT-04 | PublicContract 只读聚合 MPM/MPC/FCM/MOL/Capability 映射；不建 ContentContractStore | 必须审 DOCS/04 是否缺能力入口稳定身份 |
| CNT-05 | 公开内部组合能力须显式绑定 Capability/Operation 和合法输入输出，不自动注册所有节点 | MOC Catalog、MFM/专业注册 |
| CNT-06 | 共享定义多使用位置优先 MRIResourceReference；删除/升级须影响分析、版本保真与在途 PlanPin | MTI/Impact/Host 现实能力 |
| CNT-07 | 子流程调用需显式 MOL/Invoke 与公开 Port；Ref/Parent/画布顺序均不是自动运行 | 已有 MOL/MOC/MRK 边界 |
| CNT-08 | Logic schema1/schema2 继续互斥；跨Schema先复用子 Content，转换只在证明无损时允许 | 现有编译实际支持范围需正反例 |
| CNT-09 | Config Default/Override/Saved/Effective/Observed 分层并可解释，设备安全值不被任意 Override | FCM/MHS/Host/专业 |
| CNT-10 | Studio 共用一个 Content 编辑器的多视图，Overview/Visual/Logic/Capability/Data/Collaboration/Config/Trace 不复制工程真源 | 既有 ResourceDocuments/MPW |
| CNT-11 | 内容提取 Preview→Commit 有跨界 Port/Aspect/IdMap/Dependency 无损验证；失败整体拒绝 | MTI.ExtractReusable 实际边界 |
| CNT-12 | 内容定义及运行实例/服务绑定分离；没有 Logic 无强制 MRK Flow，服务寿命归服务 Owner | 无 Runtime 生命周期伪造 |
| CNT-13 | CT3D/Vision 专业 Viewer/大数据以受控 Slot/Handle/适配引用，不能复制成二维 Group XML | 现有 MRC/专业 Viewer 与 FDT/FIM |
| CNT-14 | 优先用 G01/G02/G05 纵向验证再批量提升 UI/类层，最终 G01～G08 全链验收 | 以软件/Windows/HIL 不同等级验收 |

## 03. 风险和拒绝路径

- “创建 Content 就能运行”：拒绝；创建只形成定义，编译/发布/真实准入另行处理。
- “每个专业新 Content 子类”：拒绝；专业通过 Type/Capability/Provider/Aspect 组合，不扩张继承。
- “UI 内生成临时可写 Graph 再转换”：拒绝；同源 MEM/MOL 编辑。
- “未知设备/脚本自动扫描并执行”：拒绝；显式信任准入与受控 Provider。
- “重新造功能/数据/消息/配置/调度中心”：拒绝，现有 Owner 已有对应体系。
- “为了编辑方便静默热更生产计划”：拒绝；VersionPin、Effective Config 与安全点必须分离。
- “用大数据序列化/复制换取统一”：拒绝；通过专业 Handle 与租约实现。

## 04. 决策及实施顺序

1. 负责人逐条确认 CNT-01～14；和 Symbol V2 的 Proposed ADR 之间不产生互斥的双标准。
2. Core/Decision/Runtime/专业按 #9 确认真正需要新增的最小 API/字段，并逐 Sheet 审 DOCS/02～04。
3. D0→D1 用 G01/G02/G05 先暴露跨层真实缺口；没有纵向事实就不大规模新建 Helper/Panel。
4. 更动后的每条 ADR 记录理由、影响类/文件、兼容策略、测试ID、批准日期；批准后再更新 #0 和 DOCS/00 索引。
5. 所有目标规范的“已实现”状态依据最新源码与固定 SHA CI 更新，不能由 ADR Accept 代替实际测试。

## 05. 相关资料

[Symbol #5 Content专业编排协同](../Architecture/CxVisual_Symbol_05_Content专业编排协同_V2.0.md)；[Content #9 全类/API](../Architecture/CxVisual_Content_09_全类UML与API合同_V1.0.md)；[Content 实施](../Implementation/CxVisual_Content_实施任务与现状台账_V1.0.md)；[Content 测试](../Testing/CxVisual_Content_完整测试与验收矩阵_V1.0.md)。
