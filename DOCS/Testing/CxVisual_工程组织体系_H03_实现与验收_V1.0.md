# CxVisual 工程组织体系 H03：资源复用与公开契约

> 2026-10-18；阶段尚未全部完成。第一批完成真实视觉组合，资源参数/端口/实例覆盖仍在继续实现，不以已有类型或局部测试宣称完整H03。H01/H02已在master `7549446`取得Windows CI成功证据。

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
