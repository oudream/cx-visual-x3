# CxVisual 工程组织体系 H03：资源复用与公开契约

> 2026-10-18；视觉组合及资源公开契约已实现并通过本地Windows验证；本批远端CI以实际提交记录为准。外部固定版资源包、完整转移、Studio资源消费和运行翻译分别继续H04～H07，不以本阶段Core测试宣称整体体系完成。

## 第一批：混合分组与完整仿射解组

| 文件/Owner | 实际行为 |
|---|---|
| Mechanism/Core/Engineering/MRCTransform.cs / M02 | 可视局部仿射值、组合与QR分解；不拥有节点/图，不是新通用几何服务 |
| MRCVisualItem.cs / M02 | 在统一局部几何中补Shear/LayerId/IsLocked；Transform派生值不另存矩阵真源；父链正逆变换与命中支持剪切 |
| MTCEditSession.Visual.cs / M07,M02 | 同父同层合法选择→Group；原序移入/局部坐标偏移；Ungroup预先计算全部组合仿射后一次事务烘焙，支持非均匀缩放＋子旋转产生的剪切；Undo/Redo共用历史 |
| Store/Snapshot / M01 | 所有新增字段同批保存/加载/固定快照，旧未声明字段默认零剪切/未锁/空层 |
| MTCEditDraft / M07 | 草稿中同样可设置矩阵/锁/层；原字段编辑继续走同一几何提交 |

真实使用：[H03VisualQuickStart.cs](../../Examples/EngineeringOrganization/H03VisualQuickStart.cs)。基础API为`Group(selection,name)`、`Ungroup(group)`、`SetTransform`、`SetLayer`、`SetLocked`。分组保留原兄弟次序；根、双重选择、不同父/层或锁定选择拒绝。解组不静默丢弃Group绑定；端口/映射等后续加入时需同步在用保护。

复用x2 `Platform/Core/CxGroupOperations.cs`的Group同父/同层/编辑锁预检、原序和局部平移规则，以及Bake先算全部仿射再修改的失败边界；x2的SkewX语义以QR上三角剪切分量保留到现有视觉几何中。最终继承仍遵守MRCVisualItem→Element/Group，未照搬旧Group:Element。

D07原候选的Core→Foundation.Geometry引用没有增加：实际需要的是视觉组合局部代数，已在原M02内用轻量值类型完成，不修改Foundation.Geometry、不引入新的基础库。MEMNode仍无可选集合/事件/端口字段。对超出double可表达范围或奇异矩阵明确拒绝，不把极端数值近似成功。

### 测试证据

- OrganizationH03VisualTests先加入，h03-visual-red-build.log真实缺Group/Ungroup/Transform等API失败。
- 实现后h03-visual-green-build.log / h03-visual-green.log exit0：H01/H02回归及混合嵌套分组、非均匀变换＋子旋转的剪切保留、解组逆恢复/索引、真实保存重开、锁/层与奇异矩阵拒绝通过。
- 尚不能从第一批用例推导参数实例、公开端口、专业运行和完整H03阶段完成；下一批补这些真实闭环。
- 完整方案h03-visual-solution-build.log、实际WPF及像素h03-visual-wpf.log、独立案例h03-visual-example-build.log / h03-visual-example.log均exit0；Owner DAG仍31项目/77引用，无新增依赖边。

## 第二批：参数、公开边界、端口与类型工厂

基线为用户清理后的master `2fae486`。旧Cx工程对象、工厂和事务入口已删除，MEMNode独立；本批不恢复旧树或要求旧文件兼容。全方案包含Studio、Runtime.Host、TestCenter的编译及当前MEM接口回归均通过。

| 类型/文件 | 已实现并验证的行为 |
|---|---|
| MPMValue/MPMBinding/MPMPropertyDescriptor/MPMProperty | 精确long/decimal、Unicode及NUL文本；Literal/FCMConfigReference/Variable/Resource/Member/Expression分离；类型/范围/公开性校验，Member环拒绝；数据源仅声明，Core不求值 |
| MPMDefinitionState/IMPMDefinition/MPMAspect | Content/Symbol/VisualItem可选能力，MEMNode不增加能力字段；四种Aspect原字节复制保有，缺必需Provider允许保存、阻断准备运行 |
| MPCPort/MPCEndpoint/MPCConnection/MTUContractReference | 独立端口身份、实例地址、方向/类型/基数/单位/坐标/时基约束；连接单一Owner，引用/端口/成员的反向派生索引 |
| MRCPublicMapping | 属性映射及公开端口映射；外部不能直接连内部私有端点；在用定义/映射/端口受保护 |
| MRIResourceReference | 两实例覆盖隔离、ClearOverride、实例仿射布局、Rename；Retarget按公开端口名匹配契约、保留连接Id，失败无修改，Undo恢复原连接对象 |
| MPMTypeDescriptor/IMPMTypeProvider/MPCPortDescriptor | 注册冻结的语义定义与默认属性/端口/Aspect；Provider锁外枚举、异常/递归/写入/冲突拒绝，整批原子；不持久化CLR工厂或活动对象 |
| AddTypedContent/AddTypedSymbol | 使用同一MEM载体创建专业语义节点；后台Content不需要Page或Element；默认值/声明与对象同一事务创建和撤销 |
| MTCEditDraft/Session | 公开参数/覆盖/布局/端口/映射/Aspect进入原有预览与提交；无第二套事务或对象树 |
| MEMFileProjectStore.Declarations/Types/Snapshot | 本批所有声明、类型注册表、覆盖/布局/连接/映射同批保存重开；未知结构字段明确拒绝，不静默忽略；固定快照复制可变字典 |

真实公开用法：[H03ContractsQuickStart.cs](../../Examples/EngineeringOrganization/H03ContractsQuickStart.cs)。`RegisterTypes(provider)`后`AddTypedContent(project.Project,"Motor","demo.motor")`即可获得默认参数与端口，再`PlaceContent`两次、`SetOverride`其中一个、`Connect`公开端点；代码独立编译执行真实文件往返。

复用去向：x2的公开属性/默认值/实例覆盖、公开边界映射、方向与单位检查行为改造到M04/M05；不搬旧字符串Id/可变对象注册表。资源声明与CxNode导航历史经验保留，旧API兼容按用户新授权不再承担。源类型/历史位置仍见V1.1复用映射R19～R40；当前具体API以本记录和实际源码为准。

### 本地验收证据与约束

日志目录`Output/Debug/x64/OrganizationGuideAudit/`（不入库）。先运行`h03-types-red.log`，真实缺类型注册API编译失败；补实现后最终：

- `h03-contracts-final-build.log`：amd64 MSBuild，Debug/x64完整方案exit0；TestCenter有既有CS0618弃用调用警告。
- `h03-contracts-full.log`：完整ArchitectureSmoke exit0，包含H01/H02/H03、配置与原运行体系回归。
- `h03-contracts-wpf.log`：真实STA窗口布局/多边形命中/拖动、像素PASS，exit0。
- `h03-contracts-foundation.log`：FoundationSmoke --concurrency，PASS，exit0。
- `h03-contracts-benchmark.log`：1000/100000元素真实创建、查询、移动、文件往返；100000元素创建127ms、清历史后活对象内存22682552B、保存560ms/加载487ms。本机一次观测，不是性能承诺。
- 独立H03ContractsQuickStart编译及运行exit0；结构/DAG检查31项目77引用，未新增项目/依赖边；git diff --check通过。

**范围限制**：Retarget对已公开端口连接支持无损替换；涉及嵌套内部地址、公开映射或Member引用需显式H05转移计划，当前原子拒绝。Expression/Variable/Config不在Core求值；Provider可用性不是设备验证。固定外部版本/资产包属于H04，不把项目内引用当作跨工程包已实现；H06/H07继续真实UI/运行衔接。
