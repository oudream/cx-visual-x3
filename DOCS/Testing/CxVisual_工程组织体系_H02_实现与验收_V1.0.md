# CxVisual 工程组织体系 H02：一般编辑、快照与地址

> 2026-10-18；在H01最终模型上继续。状态：**源码/案例/本机验收已通过；Windows CI待验证**，因本机Git登录失败尚未触发。不得将本报告视为全部门禁已通过。用户已授权自动继续H02～H07；远端验证待凭据恢复后补齐。

## 实现范围与接口

| 文件 / Owner | 实际行为 |
|---|---|
| Mechanism/Core/Engineering/MTCEditDraft.cs / M07 | BeginEdit创建意图队列；Rename/Reparent/Remove/创建/几何/绑定/引用；Preview、Commit、Cancel、Dispose；明确状态与BaseRevision |
| MEMEngineeringSession.Editing.cs / M01,M03,M07 | 单Session闸门内同一校验器预演/逆恢复，不发布历史/修订/通知；冲突不能覆盖新值；GetSnapshot、GetHandle/Read、GetAddress/Resolve、显式范围名称/类型查询 |
| MEMProjectSnapshot.cs / M01 | 显式深冻结只读平面定义投影；节点/引用/几何/绑定无活节点指针，不是第二可写工程树 |
| MRIObjectAddress.cs / M03 | 会话绑定句柄与可序列化定义地址分离；Project/Resource/Object Guid和有序使用记录链，重命名不改身份；不混用运行实例Id |
| MEMNode.cs / M01 | 零新增实例字段的First/Last/Previous/Next/Root/OwnerProject/OwnerPage导航 |
| Session、MTCEditSession / M07 | 变更元数据Kind/TargetId；完整逆值留私有可逆日志，不将操作身份摘要当可靠WAL。仅有订阅时物化公共ChangeSet摘要，普通编辑不复制全树 |

MTCEditDraft只排队意图，不包含另一份模型。Preview在闸门内应用同一生产操作并立即逆恢复，所有Session索引/快照读取等待预演完成；不让外部Provider/IO/用户回调进入预演。原始实时节点引用仍不能跨线程作为固定快照枚举。GetSnapshot显式付出O(N)复制成本，后续写入不修改其只读集合或字段；日常修改不自动全树复制。

同一草稿只能成功Commit一次，过期/取消/已提交后不可再写；同版本两个草稿提交仅一个成功，另一个返回org.version.conflict。未Commit的Dispose不改变工程。Preview被拒不发布临时Parent、索引或修订。旧VisualEditor依然消费唯一Session历史。

使用案例：[H02QuickStart.cs](../../Examples/EngineeringOrganization/H02QuickStart.cs)，内容为创建两工位→草稿重命名/跨容器移动/预览→一次提交→快照仍保持旧值→撤销重做→保存重开验证。稳定地址以资源身份为边界，跨资源改父后旧资源地址拒绝，不按最终叶Id猜补。带实例链的地址必须逐段属于前一目标定义。

## TDD与待验收记录

先加入OrganizationH02Tests，h02-red-build.log真实编译失败（缺BeginEdit/GetSnapshot/Read/Resolve）；实现后H02真实用例通过。第一轮全架构回归另遇既有ExecutionTriggerTests周期触发时序断言失败，原始h02-green.log保留；未修改执行器/断言，复跑h02-regression.log通过。不会删测试掩盖失败。

| 需求 | 真实验证 |
|---|---|
| 草稿预览/取消/失败原子性 | 重命名+改父+位置+尺寸；Preview后原Parent/Name/Revision/通知不变；失败/Dispose无部分状态 |
| 并发版本/历史 | 两草稿一成功一冲突；Undo/Redo整批恢复；元数据摘要指向真实对象 |
| 不可变快照 | 旧Name/几何保持；公开子集合写入拒绝；并发写入时取得100份快照逐父完整 |
| 句柄/定义/实例地址 | 同一Session Read、外来/已关闭拒绝；地址Parse/Resolve；名字变化仍定位；错误使用链拒绝 |
| 索引/导航/顺序/文件 | 名称/类型限定范围查询；同父重排/逆恢复；保存/关闭/重开保持次序与脏状态 |
| 历史内部故障 | 向真实逆日志反射注入失败，实际Undo拒绝并封锁历史/后续写入；没有伪引擎/生产公开绕过接口 |

公共策略授权、通用专业参数/端口、完整保存快照并发与未知载荷分别继续H03/H04；不借H02扩大到设备、MRK执行或第二配置服务。当前SourceMap只提供稳定地址载体，完整翻译仍属H07原Owner。

## 实际命令、结果与兼容

日志位于忽略目录`Output/Debug/x64/OrganizationGuideAudit/`，不能替代远端CI。

| 命令 / 日志 | 实际结果 |
|---|---|
| amd64/MSBuild.exe CxVisual.Unified.sln /t:Build /m:1 /nr:false Debug/x64 UseSharedCompilation=false / h02-final-build.log | exit0；完整现有方案构建，TestCenter既有过时API警告保留 |
| ArchitectureSmoke.exe / h02-final-architecture.log | exit0；H01、H02、原配置/数据/执行/Visual旧操作通过 |
| ArchitectureSmoke.exe --organization / h02-organization.log | exit0；补充显式Cancel、通知异常不妨碍其他订阅、回调重入拒绝/释放后可编辑、Dispose订阅测试均实际调用 |
| 独立csc.exe编译与H02QuickStart.exe / h02-example-build.log、h02-example.log | exit0；实际输出PASS H02QuickStart revision=7及稳定地址，真实文件往返 |
| Verify-Structure / Verify-OwnerDependencies / git diff --check | 31项目/77引用，零非法上行/环；无新增csproj/SDK依赖；空白检查通过 |

两轮自审：结构轮检查草稿不拥有另一棵树、Preview与Commit同一校验器、快照不持活节点、索引/撤销/通知共享Session；易用轮运行独立案例，确认取消不提交、冲突可定位、旧Editor与新API共用历史。源码兼容行为以H01旧矩形与XML回归为准，不承诺任意旧外部派生/程序集二进制布局不变。

下一包H03在此基础上扩展实例覆盖、公开参数/端口、定义与引用的完整持久字段及Group/Ungroup。角色授权钩子由H04接原账户Owner；H02不是独立权限或身份系统。
