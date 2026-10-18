# CxVisual 工程组织体系 H04：固定版本保存、静态资产与维护边界

2026-10-18；基线master `1f64326`。本批本地Windows验证通过；远端CI跟随实际提交核验。H05～H07继续；不宣称整个工程体系或生产部署完成。

## 实际实现与开发用法

| 文件/Owner | 真实行为 |
|---|---|
| MEMProjectStorage / M01 | IMEMProjectStore、不可变MEMSaveSnapshot、MEMSaveResult；XML固定编码捕获时持有Session门，实际压缩/写盘/校验在门外，新编辑可以继续；收据只标记SavedRevision |
| MEMFileProjectStore / M01 | SHA256 StoreToken；同路径Save以加载/上次保存Token作CAS；SaveAs不覆盖已有目标；同目录随机临时文件、Flush(true)、回读、原子Replace/Move；提交前取消保留原件 |
| MEMFileProjectStore / M01 | 跨进程协作写租约`<工程>.cx-lock`；比较Token和发布持有同一租约，竞争者拒绝；外部非协作写入仍靠Token检测，不宣称任意程序并发写的完全互斥 |
| MEMAssetReference / M01 | 静态资产唯一Id、媒体类型、长度、SHA256，项目内资源查询；只读不可变字节；与Page/Content/Symbol资源投影合并，无第二节点树 |
| MEMFileProjectStore.Assets / M01 | 工程ZIP中manifest＋`assets/<GuidN>`；哈希核验、重复/未知条目和路径拒绝；包搬迁不依赖原路径；资产被属性引用时拒绝删除 |
| MEMResourceEntry / M01,M03 | 资源定义散列、SchemaVersion、依赖Id元信息；按请求派生，不创建第二权威定义，不在普通编辑时重算全树 |
| MEMAccessPolicy / M01 | 宿主提供IMEMAccessPolicy；Open/Read/Edit/Save/Export/PrepareRun操作准入；策略锁外执行，重入修改/关闭拒绝。无需此能力时无策略回调 |
| Session生命周期 / M01 | RequireSaved/Discard/Save三关闭模式；保存进行中拒绝Close，调用方await保存后重试；失败保留Location、Token、脏状态与编辑事实 |
| WritePackage/ReadPackage / M01 | 固定保存快照与真实ZIP流往返，不关闭调用方Stream；流读回工程无Location且待保存 |

简单用法见[H04StorageQuickStart.cs](../../Examples/EngineeringOrganization/H04StorageQuickStart.cs)：`AddAsset`→属性`MPMBinding.Resource(asset.Id)`→`SaveAsAsync`→重新打开；双会话保存冲突抛`MEMStoreConflictException`，用户保留草稿后比较、另存或重新加载。不能将冲突当保存成功。

保存快照是明确的O(N)导出操作，当前将工程定义编码为不可变XML字节；不用于普通重命名/移动，不重建可写工程树。静态资产快照共享只读载荷；运行中的Heap/Stack/设备句柄不进包。当前每资产64MiB、总资产256MiB、4096资产，工程定义512MiB、包展开768MiB。CT采集/重建大数据应走原Data/Storage Owner，不当作静态工程资产强塞入包。

## 历史复用与授权收敛

继承x2 R34/R45/R46/R47/R50的固定版本、脏跟踪、临时提交、依赖/资源元信息和故障边界；改造为现有MEMSession与单一提交。按用户2026-10-18明确要求，旧Cx对象和旧工程不再承担兼容验收；不扩写x2format6迁移器、不恢复旧Cx工厂。现有schema1读取分支仍在，不作为当前接口或迁移完成证明。

未知插件的数据放在已定义的MPMAspect原字节中：保留ProviderId、SchemaVersion、Required及载荷，缺必需Provider可保存且PrepareRun失败。没有授权任意反射实例化，也不静默忽略未知核心XML字段。未知格式版本仍拒绝，避免假装保真。

## 真实测试证据

日志`Output/Debug/x64/OrganizationGuideAudit/`（忽略文件）：

- `h04-cas-red.log` / `h04-assets-red.log`：先写行为测试，缺真实存储/资产API而编译失败。
- `h04-final-green-build.log`：Debug/x64 amd64 MSBuild完整方案exit0。
- `h04-final-green-tests.log`：完整ArchitectureSmoke exit0；真实双会话CAS、固定版本保存中继续编辑、取消、写租约、资产搬迁/哈希、未知插件、授权拒绝无文件泄漏、策略重入、流不关闭通过。
- `h04-final-green-wpf.log`：真实Windows WPF与像素PASS，exit0；FoundationSmoke --concurrency PASS。
- 独立H04StorageQuickStart编译/运行exit0；当前31项目77引用，无新增项目/依赖。
- 损坏资产回归实际检测到`Asset hash mismatch`；测试助手最初漏捕InvalidDataException导致`h04-delivery-tests.log`失败，保留红灯日志，修正异常断言后重新全量通过。

## 尚未完成或不能外推的事项

- 非协作外部写者、网络盘原子性、真实断电恢复未验证；当前证据是本机Windows文件系统和协作进程租约。
- 权限是宿主操作准入，不是隔离恶意进程/插件的安全沙箱；已交给可信调用方的只读身份、快照或字节无法撤回，宿主必须自行管理访问边界。新操作按当前策略再次检查。
- 外部只读固定版资源依赖、跨工程Pin/IncludeClosure与转移预览继续H05；当前包内资源与静态资产已真实可用，不将其冒充外部资源联邦完成。
- 活动保存时Close明确拒绝；当前没有后台强行关闭/取消已发布文件的隐式行为。GUI提示、脏关闭重试属于H06。
- H07仍需原运行编译、配置/数据与专业Owner验收；加载工程文件不是生产切换。

## 本批Windows CI

提交`eb4c14fd7a413f565cda1e602f359bd69cc74cb9`的[工程组织Windows CI 39112344484](https://github.com/oudream/cx-visual-x3/actions/runs/39112344484)成功；同SHA统一运行、配置L1、配置底座、TestCenter五条工作流均成功。后续TestCenter并行提交引入的CS0165另在`7162053`修复并普通合并，不混淆该历史失败与本批H04状态。
