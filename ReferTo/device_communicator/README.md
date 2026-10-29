# device_communicator 监控与守护源码保真迁移（进行中）

- 来源仓库：https://github.com/oudream/device_communicator
- 固定来源：`18b5313d8c0c350f3a8cc723f57120cf9e551156`（master，2024-04-11）
- 目标：保留旧 Monitor / Daemon / Program 成熟实现，再将正式 Go 工程归位到 `Services/Supervision/`；不修改来源仓库。
- 当前状态：**仅第一批文件已复制，不是完整可构建的 Go 模块，不可宣称已完成迁移或通过 CI。**
- 当前目标主仓：`cx-visual-x3/master`。

## 已搬运源文件

| 原仓相对路径 | 来源 Git blob SHA |
| --- | --- |
| `hy/common/daemonc/daemon.go` | `c2c4ec7ddc600f096bf8353391fee29d3494f89a` |
| `hy/common/program/program.go` | `6488cbeb01023cc5e81459a607c4d1295dae1abf` |
| `hy/monitor/main.go` | `62dff364135a5a27ffcba89d2f2e24aa8d2a8cf9` |
| `hy/daemon/main.go` | `42c4db07937e3f73c86d390a97e662037b801209` |

## 迁移门禁

1. 补全旧 `hy/monitor`、`hy/daemon`、`hy/common/{monitorc,daemonc,program}` 及其所有本地传递依赖；保留原 `go.mod/go.sum` 的依赖身份并审计凭据。不得只凭四个文件声称可构建。
2. 保真验证 Go 原应用的监控、心跳、退出分类、进程启动、重启限制与数据库/配置行为。真实编译与运行结果必须留证。
3. 将完整工作代码归位 `Services/Supervision/`，再逐步解耦 IoT 专属依赖；最初不得直接改变原有业务行为。
4. 在迁走 Service 接口、配置和基类及其所有解决方案/项目引用并完成 Windows 构建和回归后，才能删除 `Mechanism/Lifecycle` 项目和目录。
5. `Mechanism/Core` 放模块身份与纯合同；运行服务实现、部署与监控管理遵循现有 Owner 和项目 DAG，严禁第二套 Host/Scheduler。
6. Windows/macOS/Linux 的交叉编译与实际运行分别验证；Daemon 自身应由操作系统外部监督，且远程重启/停止须有授权和审计。

当前 `ReferTo` 是**只读参考资产**，不是 x3 正式运行代码。此文档仅记录已实施事实与下一步验证门禁。
