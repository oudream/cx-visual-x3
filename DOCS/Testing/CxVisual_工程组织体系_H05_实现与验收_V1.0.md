# CxVisual 工程组织体系 H05：跨工程复用、转移预览与提取

2026-10-18；接续H04 `eb4c14f`，同时普通合并TestCenter并行提交至`7d6720d`；未新建分支/项目。本批真实功能通过本地Windows验收，远端CI须按提交核实；H06/H07继续。

## 实际API与语义

| 文件/Owner | 已实现并验证 |
|---|---|
| MTITransferPlan / M38 | 固定来源快照/散列/Revision、目标Session及BaseRevision、选择根/依赖根/IdMap/Issues/Warnings；重叠/重复选择拒绝；预览实际运行生产事务并完全逆转，无Revision/通知/历史泄漏 |
| PreviewImport/Import / M38 | 节点、引用、端口、连接、资产新身份；所有权、ResourceRoot、实例链、Member/Resource绑定、公开映射、校准地址重写；配置/变量来源标识保持其原Owner语义 |
| MTIExternalPolicy / M38,M03 | Reject拒绝森林外依赖；IncludeClosure带入共享资源及资产；Pin要求目标存在同Id/同Schema/同内容散列及其依赖的固定定义，不按名字猜版本，不隐式在线解析 |
| PreviewDuplicate/Duplicate / M38 | 同工程新拥有身份，原共享依赖固定Pin；原件不修改；一次Undo/Redo恢复/重建完整索引 |
| ExportPackage/MTIEngineeringTemplates.Create / M38 | 真实文件包导出闭包，返回新身份映射；模板生成新ProjectId和新对象身份；完整原身份保存仍使用SaveAs/CaptureSaveSnapshot |
| ExtractReusable / M38,M02 | Content直属子选择→共享Content＋原位置引用；边界端口生成公开映射并重接外部连接，保留外部连接Id；全部一次提交，Undo恢复原对象与原连接 |
| IMTIAspectRemapper / M38,M04 | Provider锁外重写私有载荷，使用完整IdMap；回调不能修改目标工程；未知载荷原字节保留并标RequiresRemap，准备运行阻断，不能把字节保真冒充可执行语义重映射 |

独立例子：[H05ReuseQuickStart.cs](../../Examples/EngineeringOrganization/H05ReuseQuickStart.cs)。常用过程：`PreviewImport(source,new[]{cabinet.Id},target.Project)`→查看Issues/Warnings/IdMap→`Import(plan)`→保存；共享Motor定义、双实例、实例覆盖与静态资产一起可用。源/目标修改后计划拒绝，重新预览。

转移过程不反射实例化、不调用另一个执行引擎、不复制运行Heap或设备句柄。计划只保留不可变事实与意图Id，真实对象始终只进入目标Session。所有新增字段与格式同批往返。未知载荷可后续通过原Provider设置完成重映射后的Aspect，清除RequiresRemap后再准备运行。

## 继承、测试与证据

继承x2 R43/R45/R48的克隆/重键/依赖闭包/事务失败边界，改造为Guid身份、MRIObjectAddress实例链和既有MTC日志；没有搬回旧Cx树或旧工程格式。

- `h05-transfer-red.log`：先加入真实闭包、完整IdMap、撤销与文件测试，缺API编译失败。
- `h05-final-build.log`：Debug/x64完整方案exit0。
- `h05-final-tests.log`：完整ArchitectureSmoke exit0；覆盖双工程Cabinet/Motor/资产、类型/端点/覆盖映射、预览无泄漏、一次撤销重做、Reject/Pin/IncludeClosure、目标陈旧/来源变更、真实保存重开。
- 同测试还覆盖：提取真实外部连接→公开边界重接→Undo恢复原连接对象；未知载荷原字节/标记保存至模板；Provider用IdMap重写载荷内Guid；回调重入拒绝；提取后期遇到外部Member约束时，所有已复制/映射/引用完整回滚。
- `h05-final-wpf.log`：真实WPF及像素PASS，exit0；H05ReuseQuickStart独立编译/运行exit0。
- 日志位于`Output/Debug/x64/OrganizationGuideAudit/`，不入库。没有新增项目/SDK/依赖边。

## 明确边界和未覆盖场景

- Pin是目标中已存在的固定版本校验，不是任意网络/远端资源Resolver；缺版或不同版本直接诊断，IncludeClosure可实际复制带入。
- 不能自动猜部分资源森林外的私有依赖；若依赖要求回收整个同一资源而与选择重叠，预览要求提升到资源根，原子拒绝。
- ExtractReusable已实现外部端口边界；外部Member、校准或其他不能安全重写的用法当前整体拒绝并回滚。不能宣称任意半选子图的所有语义自动提取完成。
- 普通复制不隐式复制归属于未选择祖先的外部连线；完整资源/Content内拥有的连接会复制。提取另有明确边界连线重接规则。
- 不透明载荷没有可信Remapper时保真但禁运行；不是丢弃载荷，也不是假装原Provider已完成适配。
- 跨工程在线协同/分布式事务、目标生产激活、硬件验证不属于本批证据。
