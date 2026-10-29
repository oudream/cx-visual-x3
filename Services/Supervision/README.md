# Supervision — 原 Go Monitor / Daemon 保真迁移

> **开发规范**：[《CxVisual Supervision Go 编码与工程规范 V1.0》](docs/CxVisual_Supervision_Go编码与工程规范_V1.0.md)。新增或实质修改 Go 源码时必须遵守；已迁移旧代码按兼容性和测试证据渐进治理。

此目录是可独立构建的 **Go 模块**，不是 `CxVisual.Mechanism.Lifecycle` 的重命名，也不包含第二套 C# Host。

## 来源与迁移范围

- 原仓库：`oudream/device_communicator`
- 原仓固定提交：`18b5313d8c0c350f3a8cc723f57120cf9e551156`
- 用户上传源码：`device_communicator.zip`（本次迁移交叉核验来源；ZIP 为 CRLF，Git Blob 为 LF，源码语义一致）
- 保留原 `module gogo`、现已整理为 `cmd/monitor`、`cmd/daemon`、`internal/{monitorc,daemonc,program}`（原仓分别位于 `hy/monitor`、`hy/daemon`、`hy/common/`）、`common/{channels,models,utilities}` 共 8 个本地 Go 包，以及 `go.mod/go.sum`。
- 迁入 55 份原始 Go 源码。未复制原 IoT HTTP Server、数据采集业务、生产数据库、密钥、用户配置、可执行文件等不属于两个入口编译闭包的文件。

## 构建（在本目录运行）

```bash
go mod download
go build -o ./out/monitor ./cmd/monitor
go build -o ./out/daemon ./cmd/daemon
go test ./cmd/monitor ./cmd/daemon ./internal/monitorc ./internal/daemonc ./internal/program ./common/channels ./common/models ./common/utilities
```

目录约定：`cmd/` 为独立程序入口；`internal/` 为 Go 限制外部引用的内部实现；原 `common/` 仍是基础工具代码。首次迁移 commit `d23e46e` 保留了原结构，本次只调整目录和 import，不修改监控守护业务逻辑。

Go 模块含 `github.com/mattn/go-sqlite3`，请在目标系统提供可用的 CGO/C 工具链和 SQLite3 编译条件。**仅编译通过不等于数据库驱动在目标 OS 可工作。** `go.mod` 为旧仓版本（Go 1.19）；当前不做依赖升级。

## 原有部署语义

- Monitor：`cmd/monitor`，提供运行状态、心跳/UDP 和 HTTP `GET /programs_status`；从工作目录读取 `conf/sys.config.*`。
- Daemon：`cmd/daemon`，按工作目录的进程数据库启动与守护所配置的子程序；涉及进程终止/重启，**不得在 CI 中无隔离直接运行**。
- 两者原有 `-d` 参数指定工作目录。原代码使用 `db/`、`log/`、`backup/` 等目录，且数据库中程序条目需对应部署机器环境。请在隔离环境准备真实配置与程序清单。
- 部署示例配置不含秘密或现场地址。不要将真实 `conf/`、`db/`、`log/`、API 令牌、生产设备地址提交到此公开仓库。

## 与 CxVisual 的关系

- Go 进程监视/守护仍由本服务独立承担。
- Service 模块 C# 基类已迁入 `Mechanism/Core/Modules/Service`，不是 Go 进程监督者。
- CxVisual Runtime/Transport 的统一观测接入尚未实施，不得宣称已完成双向控制、鉴权、统一状态或真实设备验收。
- GitHub Actions `.github/workflows/supervision-migration.yml` 负责 Windows C# 检查和 Windows/macOS/Linux Go 构建。自动编译结果与实机部署验收分别记录。


## 本地架构与前端文档

1. [整体架构与目录设计 V1.0](docs/CxVisual_Supervision_整体架构与目录设计_V1.0.md)
2. [Go 编码与工程规范 V1.0](docs/CxVisual_Supervision_Go编码与工程规范_V1.0.md)
3. [Web 前端架构与编码规范 V1.0](docs/CxVisual_Supervision_Web前端架构与编码规范_V1.0.md)

## V1 实际可用的只读查询

保留原 `GET /programs_status`；新增 `GET /api/v1/health`、`/api/v1/overview`、`/api/v1/programs`、`/api/v1/programs/:id`。所有新增接口从现有 Monitor 内存状态生成快照，不修改 SQLite 或监控协议；暂不提供启停、重启和配置写入。

## Vue H5 前端

```bash
cd Services/Supervision/web
npm install
npm run test
npm run build
npm run dev
```

开发期前端通过 Vite 代理 Monitor 的 `/api`；部署时复制 `web/dist/` **内的文件**到 Monitor 工作目录的 `assets/comm/`，原 `/index.html` 跳转兼容。详见 [deploy/README.md](deploy/README.md)。`npm` 不参与 Go 进程的运行。

注意：V1 无用户登录/操作授权，**仅用于受控可信网络中的只读监控**。UI 显示真实查询状态；不能把 HTTP 连通或历史心跳看作业务健康。

## 十二大功能域及本期 P0 实现

[《12 大功能域与实施验收矩阵 V1.0》](docs/CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 统一记录完整目标、唯一 Owner、现有状态和验收边界。

当前只读接口新增 `GET /api/v1/events?limit=30`，直接读取旧事件记录文件，单次读取末尾最多 2 MiB，范围 1～100；**暂仅允许来自回环地址的请求**。新增 H5「事件记录」、程序详情查看、浅色桌面风格/科技蓝深色风格切换。原 `/programs_status` 和 Monitor/Daemon 行为保留。

远程日志读取、身份登录、权限、操作审计、启停/重启、告警确认、Runtime/专业状态接入仍未完成；禁止将双主题界面及查询能力称作整体工业验收完成。

## P0 异常历史（2026-10-30）

新增本机只读 `GET /api/v1/alerts/history?limit=30`（范围 1～100）：在旧事件日志末尾最多 2 MiB 中筛选明确异常，返回真实事件码、程序 ID、时间与文字；不新建事件数据库。H5 增加「异常历史」页，支持搜索和严重项过滤；已有「事件记录」页继续展示原始流水。它们共享本机访问限制。

**注意：该接口仅是历史故障线索，不是“当前存在 N 个活动告警”，更不包含告警确认/恢复或可操作的远程控制。**
