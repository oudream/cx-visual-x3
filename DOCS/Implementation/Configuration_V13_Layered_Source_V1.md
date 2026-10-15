# 配置多层来源解析：V1.3 Gate C / S08

- 新增 `MVBConfigLayeredProfile<T>`：明确顺序的模板、工程、机台、产品、运行覆盖，可由调用者配置优先级，重复 Scope/层名/优先级会拒绝。
- 每层仅存显式字段 JSON 覆盖路径；保存事务/版本/文件由原有 `MHSConfigSection<MVBConfigLayerOverrides>` 承担，不引入新存储系统。
- `ResolveEffective()` 获取有效版本快照，输出完整模型、每层版本向量、字段来源链；源版本在解析中变化则重试或拒绝。
- `SetOverrideAsync/RemoveOverrideAsync` 修改同一层，保持 Saved 与 Effective 分离；`GetLayer().ApplyPendingAtTaskBoundary()` 用于已授权的生效边界。
- 回归案例覆盖五层覆盖、动态探测器字典与深层属性、修改前/后生效切换、撤销继承、内存隔离、重启持久化和重复层拒绝。
- **限制**：当前仅为同进程多层读与本地文件版本；多层同时生效不是原子分布式事务。真实设备参数下发、权限/联锁、跨进程回放仍未完成，不能标记 Gate F 全通过。
