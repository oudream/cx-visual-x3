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
4. [整体架构与全类 UML 设计 V2.0](docs/CxVisual_Supervision_整体架构与全类UML设计_V2.0.md)——完整对象地图、运行边界、Go/C#/Web 协同与分阶段实施；设计候选不是已实现代码。

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

注意：V1 无用户登录/操作授权，**敏感的新版本化进程查询 API 仅支持 Monitor 本机回环访问**；远程打开 H5 时可能收到 403，不可绕过该门禁。旧 `/programs_status` 为兼容保留原访问行为，监听公网/产线网卡时必须以绑定地址和防火墙 ACL 限制。详见 [部署与访问控制](deploy/README.md)。UI 显示真实查询状态；不能把 HTTP 连通或历史心跳看作业务健康。

## 十二大功能域及本期 P0 实现

[《12 大功能域与实施验收矩阵 V1.0》](docs/CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 统一记录完整目标、唯一 Owner、现有状态和验收边界。

当前只读接口新增 `GET /api/v1/events?limit=30`，直接读取旧事件记录文件，单次读取末尾最多 2 MiB，范围 1～100；**暂仅允许来自回环地址的请求**。新增 H5「事件记录」、程序详情查看、浅色桌面风格/科技蓝深色风格切换。原 `/programs_status` 和 Monitor/Daemon 行为保留。

远程日志读取、身份登录、权限、操作审计、启停/重启、告警确认、Runtime/专业状态接入仍未完成；禁止将双主题界面及查询能力称作整体工业验收完成。

## P0 异常历史（2026-10-30）

新增本机只读 `GET /api/v1/alerts/history?limit=30`（范围 1～100）：在旧事件日志末尾最多 2 MiB 中筛选明确异常，返回真实事件码、程序 ID、时间与文字；不新建事件数据库。H5 增加「异常历史」页，支持搜索和严重项过滤；已有「事件记录」页继续展示原始流水。它们共享本机访问限制。

**注意：该接口仅是历史故障线索，不是“当前存在 N 个活动告警”，更不包含告警确认/恢复或可操作的远程控制。**


## Daemon异常退出后的安全恢复（P0增量，2026-10-30）

旧PID文件可能指向上一代Daemon遗留的仍存活子进程，也可能已被OS复用。新版Daemon启动时不再按这些PID自动Kill，而是**只读识别并隔离无法安全确认的Program**；隔离覆盖首次启动、周期恢复和配置续接。运维日志查找 `ORPHAN QUARANTINE`，需先离线核验目标进程及设备运行安全，不能直接删除证据文件或通过旧UDP重启强行绕过。详细操作见 [部署与离线恢复](deploy/README.md)。

该能力目前仍依赖旧PID文件，存在子进程已经Start但事件尚未写盘时Daemon被强制终止的遗漏窗口；**不代表孤儿进程问题已经全部关闭**。三平台Go CI、设备长稳和强制断电恢复仍须正式验收。


## 守护诊断与多页面联动（2026-10-30）

已新增 `GET /api/v1/guard` **本机只读诊断接口**与 H5 “守护诊断”页。终止闭锁读取原 Daemon 持久锁文件；孤儿隔离读取最近一次 Daemon 启动扫描发布的带时间戳文件。来源不可用、损坏或不同工作目录时显示“未知”，不得误报数量为零。页面提供闭锁/隔离分类筛选、Program ID 跳转、原因及观察时间；“运行总览”“程序与服务”同步显示已观察到的保护信息，“异常历史”“事件记录”支持点击 Program ID 定位程序。

仍沿用原 Go Monitor/Daemon、Gin、Vue 3、Element Plus、Pinia、ECharts 与旧 UDP/SQLite。没有新增远程启停、重启、在线解锁、审核或业务 Ready 判定，H5 仍需同机访问。启动阶段孤儿凭证窗口、停止结果持久化及 S11 权限审计仍待继续开发。详见 [守护诊断部署](deploy/README.md)、[V2.0全类架构](docs/CxVisual_Supervision_整体架构与全类UML设计_V2.0.md) 第25章及 [S01～S12验收矩阵](docs/CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 第20章。


## 启动前意图持久化与崩溃隔离（2026-10-30）

已新增正式 Daemon 子进程启动凭证：`internal/launchjournal` 在每次 OS Start 前写入 `intent`，成功后更新 `started + PID`，原 Worker `Wait` 后清算。下次 Daemon 启动扫描会合并旧 PID 文件与未清算凭证，**即使旧 PID 未写入，也不会把无证据的状态当作安全可重新启动**。写盘/核验/删除失败按安全默认拒启，不通过旧 UDP、H5 自动清隔离。

H5 “守护诊断”、运行总览和程序详情已补充“启动隔离（含凭证）”说明，并继续展示原隔离原因、Program ID 与扫描时刻。随机 AttemptID 不等于 OS PID 创建代次；断电耐久性、原生进程树归属及真实设备 Ready 均未完成。此为上段旧版“遗留 PID 独立扫描仍有窗口”的增量修复记录，正式验收仍以 CI 和现场数据为准。

详见 [V2.0整体架构第26章](docs/CxVisual_Supervision_整体架构与全类UML设计_V2.0.md)、[S01-S12验收矩阵第21章](docs/CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)、[现场部署与离线处置](deploy/README.md)。


## OS创建时间身份核验与H5证据显示（2026-10-30）

启动凭证在 `Started(PID)` 时尝试补记 OS 创建时间 `processStartMs`；Daemon 异常重启时使用同一已有 `launchjournal.Scan` 只读观察当前 PID 创建时间及真实可执行文件是否吻合，核验结果合并到隔离原因，并通过原 `GET /api/v1/guard` 呈现在“守护诊断”和“程序详情”。新增的是诊断能力，不是进程归属或远程权限。即使同一 PID、OS 创建时间及程序文件都观察一致，也不得自动 Kill、自动清账或自动解除守护隔离；无法获取 OS 证据时状态仍为未知。

实现与验收详情：[全类架构第27章](docs/CxVisual_Supervision_整体架构与全类UML设计_V2.0.md)及[S01-S12验收矩阵第22章](docs/CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)。正式 Go/Windows/macOS CI 和现场 PID 重用验收尚需固定 SHA 结果。
