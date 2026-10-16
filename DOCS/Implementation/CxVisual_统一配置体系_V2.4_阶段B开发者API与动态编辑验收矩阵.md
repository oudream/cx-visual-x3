# CxVisual 统一配置体系 V2.4 · 阶段 B 开发者 API 与动态编辑验收矩阵

> 日期：2026-10-16。目标基线：CxVisual_统一配置体系_完整设计与验收_V2.4.md；前置阶段：阶段A实施与验收矩阵。  
> 归属不变：Foundation/Data/Configuration + Mechanism/Data/Configuration。继续复用原事务、权限、配置身份和存储，不新增项目或平行 Facade。  
> 本文件以 **master 实际源码和每个 SHA 的 GitHub Actions** 为证据，不以文档宣称代表运行验收。

## 1. 实现追踪（B1 / B2 / B3）

| ID | 用户可用能力 / 验收要求 | 真实 Owner | 回归证明 | 边界 |
|---|---|---|---|---|
| B1-01 | 普通 POCO 的 Open、Value、UpdateAsync、Watch/WatchSaved、PrepareValue 不变，统一使用现有 Section | MHSConfigSection、MHSConfigService | OrdinaryDeveloper、XRayMultipleDetectors、PreparedReadAndConcurrency | 不另建 Facade |
| B1-02 | Register<T>(key) 返回绑定组身份的 MHSConfigGroupMember<T>；原字符串入口保留 | MHSConfigAtomicGroup | CtLongTask | 注册返回类型变更后旧二进制需要重新编译 |
| B1-03 | 原子 Edit.Get/Set/Remove、GroupSnapshot.Get 支持成员句柄，异组句柄拒绝 | MHSConfigAtomicGroup | CtLongTask（foreign handle） | 同名不同组不可混用 |
| B1-04 | edit.Update(member, Action<T>) 单步安全读改写，回调失败不落草稿 | MHSConfigAtomicEdit | CtLongTask（异常回调） | 仍须由 CommitAsync 统一验证和提交 |
| B1-05 | 原子/独立层 ProfileId、Layers 可读取 | MVBConfigAtomicLayerProfile、MVBConfigLayeredProfile | AtomicProductRecipe、原有 LayeredProfileRegressions | 独立多文件层并非原子提交 |
| B2-01 | 由 selector 设置深层字段 SetValue；原有强类型路径、Codec、编辑草稿不变 | MHSConfigEditorModel | DynamicCollectionEditing | 空父节点及无法到达的路径应失败 |
| B2-02 | 字典 AddKey / RenameKey / RemoveKey；Key 支持引号和反斜杠 | MHSConfigEditorModel | DynamicCollectionEditing | Map Key 限 string |
| B2-03 | List InsertItem / RemoveItemAt / MoveItem（目标为最终索引） | MHSConfigEditorModel | DynamicCollectionEditing | Array 固定长度，HashSet 不可重排 |
| B2-04 | HashSet AddSetItem / RemoveSetItem，重复元素不额外增加 | MHSConfigEditorModel | DynamicCollectionEditing | Set 以元素值为身份，无索引 |
| B2-05 | DescribeCollectionItems 覆盖动态 Map/List/Array/Set，含 KeyJson、Index、ItemPath、Type、Issues | MHSConfigCollectionItem + MHSConfigEditorModel | DynamicCollectionEditing | UI 无关快照，不持久化第二真源 |
| B2-06 | 动态项来自 Draft/Saved/Effective 并集；已删除项仍能看见 HasSaved/HasEffective 和差异 | MHSConfigEditorModel | DynamicCollectionEditing | 依据 HasDraft/HasSaved 区分缺失和显式 null |
| B2-07 | 动态字典校验错误与路径使用同样 JSON 转义；错误准确落到键内字段 | FCMConfigSchemaBuilder | DynamicCollectionEditing（Range 非法） | 原始字符串 Key 不直接拼入路径 |
| B2-08 | 无法保真的非标准 Dictionary/HashSet 比较器显式拒绝 | FCMJsonValueCodec | DynamicCollectionEditing（OrdinalIgnoreCase） | 默认和 string Ordinal 可用；自定义比较器需专门 Converter |
| B3-01 | 所有集合操作走 Modify → Undo/Redo → Preview/PreviewAsync → CommitAsync，结果复用原 Save/Effective | MHSConfigEditorModel / MHSConfigSection | DynamicCollectionEditing / ConfigV13ExtensionTests | Preview 同步通过不意味着异步提交成功 |
| B3-02 | 冲突、无效索引、重复 Key、Range 校验失败不写入；旧编辑会话返回 CONFIG_REVISION_CONFLICT | MHSConfigEditorModel | DynamicCollectionEditing（反向测试） | 版本冲突后需用户重开/刷新，不擅自覆盖 |
| B3-03 | Subscribe、任务快照、版本、权限和安全保障不因便捷接口退化 | 原有 Section/AuthorizedSession/AtomicGroup | ConfigArchitectureCompletionTests / ConfigDeveloperExperienceTests | 真实设备 ACK 不归基础配置自动完成 |
| B3-04 | 原子成员和层级合成异常不返回任意插件异常文本 | MHSConfigAtomicGroup / MVBConfigAtomicLayerProfile | 配置独立 Smoke + 原安全测试 | 仍需要可信宿主为 UI 做授权筛选 |
| B3-05 | Dispose 后集合操作禁止继续执行 | MHSConfigEditorModel | DynamicCollectionEditing | 不自动持久化丢弃的草稿 |

## 2. 开发者快速使用（对应源码中的真实模型）

### 2.1 普通配置和复杂集合（依赖已有 Mechanism.Data）

~~~csharp
var cfg = new MHSConfigService(root)
    .ForMachine("Line-B").Open<ConfigDynamicCollections>();

using (var edit = new MHSConfigEditorModel<ConfigDynamicCollections>(cfg, "工艺调整"))
{
    edit.AddKey(x => x.Detectors, "D-01", new ConfigDynamicNode { Gain = 3 });
    edit.SetValue(x => x.Detectors["D-01"].Gain, 9);
    edit.InsertItem(x => x.Segments, 0, new ConfigDynamicNode { Gain = 4 });
    edit.AddSetItem(x => x.Tags, "CT");
    var scalarFields = edit.DescribeFields();
    var collectionEntries = edit.DescribeCollectionItems();
    var preview = edit.Preview();  // 同步预览；提交另跑异步校验
    var result = await edit.CommitAsync(CancellationToken.None);
    // 由宿主按 result.Success / ErrorCode 展示结果。
}
cfg.ApplyPendingAtTaskBoundary(); // 边界与授权满足时才改变 Effective
~~~

ConfigDynamicCollections、ConfigDynamicNode 是已纳入 Tests/ArchitectureSmoke 的真实模型；await 所在方法需为 async。编辑模型拿的是受信任宿主的裸 Section，**不允许直接交给无权限的操作人员/插件**。

### 2.2 原子 CT 扫描 + 重建成员

~~~csharp
var group = service.OpenAtomicGroup("CT.ScanAndRebuild");
var scan = group.Register<V13ConfigSample>("Scan");
var rebuild = group.Register<V13AsyncConfig>("Reconstruction");
group.Seal();
var result = await group.UpdateAsync(edit =>
{
    edit.Update(scan, x => x.ExposureMs = 135);
    edit.Update(rebuild, x => x.Count = 4);
}, "CT联合配置", CancellationToken.None);
if (result.Success)
    group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);

using (var pinned = group.CaptureForTask("CT-Scan-01"))
{
    int exposure = pinned.Get(scan).ExposureMs;
    int loops = pinned.Get(rebuild).Count;
}
~~~

Register<T> 生成的成员句柄自动包含所属原子组身份；无需使用第二服务。已有的组名字符串方法保持可用。

## 3. 验收记录与不能夸大的范围

- 成功代码 SHA **a1af5af64ee70ab6a09d2fc7f275fbe0d8a530af**：独立配置 Windows CI https://github.com/oudream/cx-visual-x3/actions/runs/38874941525；完整解决方案 Windows Build/Smoke https://github.com/oudream/cx-visual-x3/actions/runs/38874941541；TestCenter .NET48 https://github.com/oudream/cx-visual-x3/actions/runs/38874941520。均实际 Success。
- 以上证据只对应给定 SHA，不自动证明本文件及后续提交的 CI。最终验收需对 **最后一次 master HEAD** 的配置和完整构建各取得 Success。
- Stage C 未完成：自动控件选择、嵌入 Studio、动态集合控件、权限 UI、Saved/Effective 可视化操作链、四层完整配置界面、WPF 手工/STA 验收均不能冒充完成。
- Stage D 未完成：大规模 10K 条目/深层集合性能、长期运行、资源泄漏与压力指标、磁盘物理断电耐久性尚需独立证据。动态目录有节点数量和深度上限，并不构成性能保证。
- 当前 JSON 编解码不会保存任意 CLR EqualityComparer 身份。自定义 comparer 不可保真时返回 CONFIG_COLLECTION_COMPARER_NOT_PERSISTED；如需要自定义语义，应使用明确 Converter、Schema 与测试，不可用默认 HashSet 在重启后偷偷替代。
- 所有 Git 提交保持中文；阶段 C/D 修改仍仅限已定项目归属，不新建 ConfigManager、独立 Publisher 或安全项目。

## 4. 阶段 B 总结判定

B 目标以“专业只用普通 C# 类、简单调用、动态编辑、一次事务、真实验证”为指标，不以新增类数量为指标。原有基础配置最简入口完全保留；本轮新增的 MHSConfigGroupMember<T> 与 MHSConfigCollectionItem 是必要的类型安全成员句柄和动态编辑视图，不是新框架。达到上述 Windows 同一 SHA CI 成功后，将 B 标为软件自动验收通过；Stage C 才是完整自动配置界面交付。
