# CxVisual 工程组织体系 H01：实现与验收 V1.0

> 2026-10-17；H00 D01～D04已由用户明确确认。最终采用MEMContent/MEMSymbol，MRCVisualItem为MRCElement/MRCGroup共同基类。直接master开发，不新增csproj。本文区分每批真实结果与后续边界。

## 1. 第一批：真实核心闭环

状态：**已实现并验证基础A/B/C及旧D回归**；异常加固/规模基准将在下一批补充，不把它们提前报通过。同步源码基线为正常合并远端47e3a90后的master。

| 文件/Owner | 实际行为 |
|---|---|
| Foundation/Core/FOIStableId.cs / F01 | 非空Guid、解析/格式化/值相等，default禁止作为有效身份使用；旧CxStableId保持兼容 |
| Mechanism/Core/Engineering/MEMNode.cs / M01 | 轻量身份/导航；Project/Page/Content/Symbol类型化拥有与只读视图；Page/Content/Symbol实现IMEMResource；引用独立于拥有树 |
| Engineering/MRCVisualItem.cs / M02,M04 | Element/Group共同视觉属性；局部平移/缩放/旋转、父链组合、准确逆变换命中；按需Binding声明，不求值 |
| Engineering/MEMEngineeringSession.cs / M01,M03,M07 | 唯一树与增量Id索引、资源查询、受控编辑、同一领域历史/观察、Save/Open/Close |
| Engineering/MTCEditSession.cs / M07 | 创建/命名/删除/改父/变换/绑定/引用一批原子提交；局部逆操作回滚，无每次全树复制 |
| Engineering/MEMFileProjectStore.cs / M01 | H01 format2单ZIP内project.xml，流式读写；schema1 XML兼容；随机同目录临时件/Flush/回读/可靠替换 |
| Core/Cx*.cs兼容面 | 同一实体/同一Session，CxNode只读兼容基类无实例字段；不维护第二棵树。旧内嵌Symbol仅作为显式legacy兼容结构 |
| Mechanism/Visual/CxVisualEditor.cs、CxSceneItem/Projection / 原M37/M16 | 去除私有领域撤销栈，消费同一Session；保留旧矩形命令，复用局部变换投影/命中 |

普通用法与可执行案例：[H01QuickStart.cs](../../Examples/EngineeringOrganization/H01QuickStart.cs)。所有方法为当前真实API；与V1.1候选签名不同处以本报告及源码为准。

```csharp
var p = MEMEngineering.Create("工程");
var page = p.AddPage("操作页");
var content = p.AddContent(page.RootContent, "工位");
var rectangle = p.AddRectangle(content, "状态灯", 20, 40);
p.Rename(rectangle, "运行状态");
p.SaveAs(path);
p.Close();
var loaded = MEMEngineering.Open(path);
var sameId = loaded.Find(rectangle.Id);
```

基础便利方法直接返回已提交对象；失败抛明确异常。复杂Edit返回MTCEditResult<T>，不成功时Value无效，ErrorCode/Message说明拒绝原因。对象是只读**实时身份**，不是深度冻结快照；跨线程的一致组合读取必须由Session受控入口完成，固定快照/异步保存时继续编辑属于后续H04，不伪装已实现。

## 2. 本批真实验证

- 测试先加入ArchitectureSmoke，初次构建按预期失败（MEMEngineering/MRCElement等尚不存在），日志h01-red.log。
- 真实项目构建：MSBuild ArchitectureSmoke /t:Build /m:1 Debug/x64 /p:UseSharedCompilation=false，**exit0**，h01-build.log。
- ArchitectureSmoke真实执行：**exit0**，输出`PASS H01 real cases A/B/C, ownership, transactions, references, transforms, persistence`及原配置/运行/矩形回归通过，h01-architecture.log。
- 验证包含保存/关闭/重开/修改/再次保存、资源同一身份、引用保护/往返、跨容器移动、事务拒绝、Undo/Redo、索引一致性、Group局部变换。

本机日志位于忽略的Output/Debug/x64/OrganizationGuideAudit目录；不是远端CI。尚未执行的完整方案、FoundationSmoke、WPF真实操作与大规模基准不能记为本批已通过。

## 3. x2实际改造复用与有意变化

继承x2的单父/保根/引用展开环规则、批量修改失败恢复、通知在提交之后、克隆/加载恢复导航等反例设计。节点强类型拥有集合仍是唯一存储，资源分类是投影；组局部坐标/父变换语义沿用CxGroupOperations。没有将x2 Platform、JSON框架、旧运行/数据栈整体引入x3。

有意变化：最终用户选择共同MRCVisualItem，而非x2的Group:Element；名称前缀与Guid使用新规范；H01用局部逆操作日志及Session锁保证统一编辑，不实现候选全树隔离草稿。旧格式1 XML受保护根须合法，错误多根明确拒绝。旧Cx名称保留；API兼容为源码级，不承诺未发布程序集的二进制继承布局兼容。

## 4. 明确未完成

完整H02（长草稿/自动合并/全部专业修改）、完整H04（保存Token/跨进程协调/固定快照并发保存/未知插件保真/完整x2 format6迁移/账户授权）、H03公开参数/端口映射与实例覆盖、H06完整WPF工程编辑与实例渲染、H07编译/生产接续均未宣称完成。当前仅可信本机创作API，不是面向不可信远端的授权服务。

H01允许基本资源引用定义/独立实例Id及往返，不表示共享Symbol已经有完整实例渲染/参数化运行。绑定只保存来源声明，求值仍归既有配置/数据/运行。工程关闭不启动或停止设备。
