# CxVisual Content 体系 #3——组织结构、四方面与组合规则 V1.0

> 目标设计。关联 CNT-R25～R30、Symbol #1/#5 V2.0、工程组织 V1.2。四 Aspect 是同一 Content 的正交定义视角，不是四种 Content 子类或四套资源仓。

## 01. 三类组合关系

| 关系 | 定义地点 | 实际含义 | 绝不推断 |
|---|---|---|---|
| Parent/Owned | MEMNode / Project / Content | 唯一父节点、结构、局部资源作用域 | 孩子顺序就是运行顺序 |
| ResourceReference | MRIResourceReference | 定义复用、使用位置、参数覆盖/版本 | 引用就等于调用/订阅 |
| Port/Logic/Visual Connection | MPC/MPM/MOL/MVI | 数据绑定、事件映射、执行依赖、装饰线 | 四种线用一张可写 Graph 互换 |

物理工程真源仍为 MEMNode，Aspect 载荷/Port/Reference 隶属定义。MOL Block、MPC Port、运行 Frame 不继承 MEMNode；数据流和控制流是定义在 Content 上的关系，不是复制一套工程树。

## 02. Four Aspects 完整定义合同

| Aspect | 可包含什么（定义期） | 运行/投影 Owner | 重要排除 |
|---|---|---|---|
| Visual | 内嵌 VisualItem、SymbolRef、Viewer Slot、布局/显示绑定、用户交互映射 | MVI/MPW/专业 Viewer | 真实 WPF 控件/GL Texture/Volume Buffer |
| Logic | 唯一 MOL schema1 或 schema2，条件/规则/执行组与调用合同 | Decision/MOC 只读构建、MRK 执行 | 第二份 Graph、活动委托与当前步骤状态 |
| Data | 类型化输入输出、变量/Frame 使用声明、绑定、质量/作用域规则 | MDS/数据 Owner | 当前图像/体数据、Heap 当前内容 |
| Collaboration | ContentRef/Capability/Module/Message/Command/Resource 关联、权限及业务请求 | MPC/MNM/Command/Host/专业 | 直接持有另一专业内部指针/复建总线 |

MPMAspect 真实字段已有 Kind、ProviderId、SchemaVersion、Payload、Required、RequiresRemap；负载存在上限，不能将 GB 数据或任意 WPF Object 序列化其中。Aspect 的跨版本修改需专业 remapper，未知 required Schema 不得尝试“按空处理”发布。

## 03. 组合并非强制四方面俱全

| Content 形态 | Visual | Logic | Data | Collaboration |
|---|---|---|---|---|
| 静态信息板 | 必需 | 无 | 可选 | 可选 |
| 动态监控板 | 必需 | 可选规则 | 必需 | 订阅/变量引用 |
| 图像流水线 | 可无 | 必需 | 必需 | Vision Provider |
| 长期服务编排 | 无 | 必需 | 按需 | Service/Command |
| CT3D 分析工作台 | 专业Viewer | 可选工具流 | Volume/DataRef | GPU/专业 Viewer |
| 自动化检测工位 | HMI Symbol | 通常必需 | 变量/结果 | PLC/MES/Vision/Inspection |

仅一方面有定义时其他方面不得由 UI 自动生成不可理解的占位 Payload；缺失与 Disabled 也不可混同。组合完成后可支持渐进加方面，不改变 DefinitionId。

## 04. 内部结构组织与公开边界

~~~mermaid
flowchart TB
 C[MEMContent: Station]
 C --> V[Visual: SymbolRef/Group/ViewerSlot]
 C --> L[Logic: one MOL definition]
 C --> D[Data: declared bindings and ports]
 C --> CO[Collaboration: refs and mappings]
 C --> E[Embedded MEMContent: local detail]
 C -. MRIResourceReference .-> S[Shared MEMSymbol]
 C -. MRIResourceReference .-> SUB[Shared MEMContent]
 L -. explicit invoke .-> SUB
~~~

一个 Content 可以拥有内嵌子 Content、直接图元及资源引用；但引用的 Symbol RootGroup 仍属 Symbol Definition，禁止把其子图元扁平拷贝到 Content 作为同步真源。对外通过公开 Port/Property/Operation 映射，内部命名/元素坐标仅为实现细节。

**Viewer Slot（专业适配角色而非必添基类）**：CT3D 的 VTK/OpenTK Viewer、Vision 的高性能 ImageViewer 可能不适宜以二维 Group/Element 实体表达；可由专业定义/适配将布局与 Viewer 运行对象连接，实体身份/数据租约由原 Owner 管。不能把三维 Volume 强塞入 MRCElement XML。

## 05. 视觉—逻辑—数据—协同连接的四分法

1. Visual：装饰连接、空间包含、图元锚点，变换只影响显示。
2. Data：端口类型、值来源、流/订阅、单位/时基、质量与大对象租约。
3. Event/Command：用户/系统事件、请求ID、权限、命令目标及回应，只有 Host/专业实际执行产生副作用。
4. Logic/Control：顺序、分支、循环、等待、并行、异常、子调用，由 MOL/MOC/MRK 形成执行语义。

测试必须使用同一画面分别画装饰线和可执行数据线，修改 ZIndex 或拖动 Symbol 后执行语义不变化；错误混线可定位诊断。

## 06. 层级与跨作用域

- 工程资源默认禁止拥有环、双父、重复 GUID；引用展开环需在相应上下文受限验证。
- 逻辑条件/Loop 合法环与资源依赖环不能一律按工程拥有环规则处理。
- Scope 解析按 Project→Content→ReferencePath→Invocation，外部不能凭子 Content 的简单显示名查其私有变量。
- 同一定义多 Reference 的嵌套调用须保留全路径，不得让一处 Binding 覆盖另一处。
- Content 引用的 Symbol 可多实例；Visual/Logic 双视图按同一 ObjectId/PortId 同步导航，不共享额外可写缓存。

## 07. 面向对象与设计模式

Composite 用于 MEM/MRC 层级，但不要将所有东西继承成 MEMNode；Facade 用于 Content 对外最小公开契约；Adapter 用于专业 Viewer/Capability/历史系统；Strategy 用于可选绑定、专业适配与布局；Command 依赖现有 MTC 统一事务；Observer 通过现有 MDS/MNM 的观测入口。AOP 的鉴权、审计、质量、Trace、版本验证施于既有服务边界，不以 WPF 事件代替。

## 08. Studio 应如何投影同一 Content

Outline（拥有/引用）、Scene（Visual）、Flow（MOL）、Data（绑定/端口）、Collaboration（资源/命令）、Inspector（公开与私有成员）、Dependency（依赖）、Runtime（Trace）必须从同一 MEM Snapshot/Revision 和只读运行证据生成。切换视图、移动节点、改变选中项不能创建新的业务对象；编辑动作进入相同 Session/Undo。ContentRef 在 Outline 的引用节点与 Flow 的调用节点可相互定位，但拥有与调用继续不同。

## 09. 测试/失败模式（CNT-T25～T30）

无视觉逻辑、无逻辑视觉、完整四方面均可 Save/Open；Group 非法嵌套拒绝；Reference 环与数据循环分别处理；版本坏 Aspect 必须可诊断且不丢字节；任意视图选中同一身份；Visual 连接和 MOL Link 不互相污染；专业 Viewer Unavailable 时 Content 定义仍可只读打开。
