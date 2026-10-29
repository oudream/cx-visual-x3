# device_communicator 源码参考快照

- 原仓：`oudream/device_communicator`，固定来源 `18b5313d8c0c350f3a8cc723f57120cf9e551156`。
- **正式迁入位置：`Services/Supervision/`**；首次完整迁入 GitHub commit `d23e46e9db6b7777e70ddb21dc4d6c6a3ddea4ee`。
- 正式工程包括 Monitor/Daemon 两入口、6 个本地依赖包（55 份 Go 文件）和原 `go.mod/go.sum`；公开仓库不含原运行数据库、生产凭据与部署配置。
- 此 `ReferTo/device_communicator/` 目录仅保存早期抽取的参考文件，不参与构建，**禁止在这里继续维护第二份正式实现**。
- 已将原 `Mechanism/Lifecycle` 的 Service 模块源码迁入 `Mechanism/Core/Modules/Service/` 并移除 Lifecycle 独立工程。Go Monitor/Daemon 与 C# 模块生命周期属于不同职责。
- 实际构建与运行前提见 `Services/Supervision/README.md`，Windows C# / Go 三平台 CI 见 `.github/workflows/supervision-migration.yml`。
- **构建成功不等于生产设备及运行配置验收通过**；原监控进程的心跳、数据库、守护重启和异常处理应在隔离工作目录与生产测试环境分别验收。

本目录的旧源文件和 README 不应被理解为仍需手工逐个迁移的待办清单。
