# CxVisual Supervision Go 编码与工程规范 V1.0

> **文档状态**：正式工程规范（V1.0，2026-10-29）  
> **适用范围**：`Services/Supervision/` 内 Monitor、Daemon、HTTP/H5 服务、内部包、Go 测试、配置、部署及跨平台 CI；不直接规定 C# 工程编码风格。  
> **定位**：在 CxVisual F/M/P 与 Owner 规则下，为独立 Go 服务提供可执行、可审核、可测试的开发合同。  
> **基本原则**：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。  
> **依据**：主仓 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`、`Services/Supervision/` 实际源码、`device_communicator` 旧仓保真来源及 Go 官方编程建议。  
> **证据边界**：代码审查不等于功能运行验收；原成熟代码已迁入并有三平台编译/部分单元测试记录，但生产运行、H5 操作、权限、跨机器守护与设备实测仍须独立验收。

## 0. 目的、术语与实施边界

本规范追求 **可靠、清晰、低耦合、可组合、跨平台、容易维护**；不为了形式统一而重写旧系统，不建立第二套 Host/任务调度/通信真源。

本文中的“必须”为代码评审的硬门禁；“应”为默认要求，例外需给出理由；“可”为按需扩展。

适用规则：

1. **新增代码与实质改动代码**立即执行强制条款；既有未改源码以兼容性为优先，记录技术债、分阶段调整。
2. **接口、协议、持久化兼容性**优先于重命名：现有 UDP 消息码、CRC/序列化、Program ID、SQLite 表结构、配置键、HTTP 旧路径及字段未经版本迁移方案不得更改。
3. 文档、设计、已提交、编译、自动测试、隔离运行、工业现场验收须分别记录，**不可互相冒充**。
4. Go 项目是 `Services/Supervision` 的**独立模块**，不增加 CxVisual 的 .NET 工程数量，不取代 C# `Mechanism.Runtime`、`Transport`、`Core` 的既有 Owner。
5. 原模块路径 `module gogo` 与 `go 1.19` 是当前**兼容事实**；CI 可使用验证通过的 Go 1.23 工具链。改模块名、提升最低版本、升级依赖均须单独变更和三平台验收。

## 1. 现有代码基线与责任归属

### 1.1 已有实现，不重复建设

| 现有位置 | 已有职责 | 后续改进方向 |
| --- | --- | --- |
| `cmd/monitor` | 程序状态汇聚、心跳/UDP 处理、Gin HTTP 状态接口、启动退出 | 保留 Monitor 为**本机状态/API/H5 后端**，逐步分离启动、路由和应用服务 |
| `cmd/daemon` | 被管理程序启动、异常退出处理、恢复调度及事件上报 | 保留 Daemon 为**本机守护执行者** |
| `internal/monitorc` | Monitor 消息码、帧协议、收发及解析 | 保留线协议兼容，强化恶意报文、超长、CRC、乱序测试 |
| `internal/daemonc` | 子进程启动与等待、次数与故障控制 | 消除无保护的全局运行开关；补取消、退出归因和资源治理 |
| `internal/program` | Program/Status/Event、SQLite、配置与程序清单 | 保留 DB 兼容，分离仓储、运行状态与 API DTO |
| `common/channels` | HTTP、UDP、TCP、MQTT、串口等通用传输能力 | 不重复创造 Transport；对外暴露接口要最小且安全 |
| `common/models`、`common/utilities` | 基础枚举、文件、日志、配置及小工具 | 保留旧接口，新功能按单一职责组织，避免继续扩大万能工具包 |

当前 Monitor 使用 `Gin`，提供 `GET /programs_status`；HTTP 服务可承载静态 H5 资源，且源码中有 `channels.GinRegister` 注册 pprof。Daemon 在本地执行启动、停止、重启等进程操作。**不应仅因增加 H5 就新增第三个 Web 后端进程**。

### 1.2 两进程职责边界

- **Monitor**：状态真源的聚合/查询、HTTP API、H5 静态页面、认证授权、命令受理与审计、异步结果展示；不直接承担 Daemon 的循环守护工作。
- **Daemon**：真正执行进程启停、异常重启、退出判定、退避及结果事件；不嵌入面向浏览器的管理页面。
- **管理客户端**：H5、CxVisual Studio、运维客户端共用受版本约束的 API；**前端不直接发送危险 UDP 命令**。
- **CxVisual**：C# Runtime/Transport 对接时是客户/适配者，不能为了显示健康状态另建一套不一致的进程状态数据库。
- **独立 Gateway**：仅在跨站点、多设备、多租户统一运维有实际需求时另立架构裁决；当前不创建。

命令链设计合同（**目标，不代表已实装**）：

    H5 / Studio -> Monitor HTTP API -> 认证+授权+审计
                -> 受控命令适配器 -> Daemon -> 程序
                <- 结果/状态事件  <- 守护结果 <- Daemon

Monitor 与 Daemon 之间可沿用已核实的历史通信协议，未来增加可靠确认机制时不得隐式改变旧协议；命令的受理状态与完成结果是两件事。

## 2. 目录、模块与包边界

当前**正式目录结构**：

    Services/Supervision/
    ├── cmd/
    │   ├── monitor/           # 进程入口及现有装配
    │   └── daemon/
    ├── internal/
    │   ├── monitorc/          # 监控协议实现
    │   ├── daemonc/           # 守护执行实现
    │   └── program/           # 程序及持久化模型
    ├── common/
    │   ├── channels/
    │   ├── models/
    │   └── utilities/
    ├── go.mod
    ├── go.sum
    └── README.md

规范：

1. `cmd` 只负责参数、配置装配、依赖注入、启动/停止与进程退出码；现有业务逻辑从这里**渐进迁出**，不可一轮重构所有内容。
2. `internal` 放 Supervision 私有实现；Go 的 `internal` 访问限制是语言机制，包仍须遵循最小可见性。
3. `common` 先保留历史公共实现，**不允许把它当作新的万能业务层**。代码真正跨包复用且与监控专业无关，才考虑放这里。
4. 按职责建包，不按设计模式名、文件大小或“一个类一个包”建包。避免 `base`、`helper`、`misc`、`manager2` 等歧义名称。
5. 后续确有代码迁移时，可在 `internal/httpapi`、`internal/supervision`、`internal/security` 内建立**已有真实行为的实现**；这些是候选目录，V1.0 **不要求创建空包**。
6. 不因遵守本规范而改变当前 `module gogo`；未来改为带仓库域名的模块身份需单独 ADR、import 批量修改、兼容审计和 CI。

## 3. 格式、命名与注释

### 3.1 自动格式化

- **必须**使用 `gofmt`（提交代码无差异），推荐 `goimports` 组织 import，标准库、第三方与本模块分组且内部按字母序。
- 源码为 UTF-8；统一 LF 行尾，最终由 `gofmt` 决定缩进（Tab）。禁止只为美化制造无意义大面积 diff。
- 不以固定行数强行拆函数；以职责、可测试性和复杂度为依据。复杂路径应拆分但不能制造单行转发器链。
- 导出的标识符给出完整可读的 godoc 注释，以标识符开头；复杂协议、并发所有权、单位与边界条件要有说明。允许中文业务注释，但公开 API 文档须表述明确。

### 3.2 命名表

| 对象 | 必须采用 | 禁止新增 |
| --- | --- | --- |
| 包 | `monitorc`、`daemonc`（保留旧兼容），新增如 `httpapi` | `MonitorService`（包名）、`utils2` |
| 导出类型/方法 | `ProgramID`、`HTTPServer`、`ParseURL` | `ProgramId`、`HttpServer`（新 API）、`GetPIDValueEx` |
| 内部字段/变量 | `programID`、`httpServer`、`workDir` | `_workDir`、`myProgram`、`tmp2`（新增） |
| 布尔字段 | `enabled`、`isRunning`、`canRestart` | 双重否定、含义不清的 `flag1` |
| 时长/大小 | `restartDelay time.Duration`、`maxFrameBytes` | 无单位的 `delay int`、`maxValue` |
| 常量/错误 | `DefaultHTTPPort`、`ErrProgramNotFound` | 魔法数字、不同包重复定义的错误字符串 |
| JSON | 保留旧线协议字段；新 API 明确 `json:"programId"` 等契约 | 静默变更已有序列化字段 |

缩写统一采用 `ID`、`PID`、`HTTP`、`URL`、`UDP`、`TCP`、`JSON`、`CPU`、`CRC`、`SQL`。局部循环计数可以使用简短变量；错误通常使用 `err`。

### 3.3 兼容例外

旧仓存在 `ProgramId`、`HttpServer`、`_workDir`、`_title`、`MonS` 等历史命名。**不可为命名而破坏外部调用/JSON/DB/旧协议**；迁移时先覆盖测试、梳理导出 API，再统一更名。只更名内部未暴露对象的修改，可随实质功能变更逐步完成。

## 4. Go 对象设计与 OOP、GoF、AOP

Go 的封装与多态基于 `struct`、方法、接口、组合，不照搬 C# 类继承层级。

1. **封装**：有状态组件由类型持有状态与同步责任，优先构造函数（`NewMonitor`、`NewDaemon`、`NewService`）；业务方法不得隐式依赖大批包级变量。
2. **接口**：接口尽量在调用方定义，优先小接口；避免为每个 struct 无条件配一个等名接口。依赖注入应服务于测试、替换或边界隔离，而非形式化。
3. **Strategy**：不同重启退避/进程启动策略确有可变需求时才抽接口；避免把已有稳定循环拆成十几个策略对象。
4. **Adapter**：UDP 协议、SQLite 访问、未来 CxVisual Transport 通过适配器隔离，不复制第二套消息中心。
5. **Observer**：状态事件订阅须定义订阅/取消、缓冲容量、背压和慢消费者策略；防止泄漏或无限积压。
6. **Factory/Builder**：优先普通 `NewXxx` 构造，配置繁杂且有合法性约束才采用 Options/Builder。
7. **AOP 横切职责**：HTTP Middleware 统一认证、授权、请求标识、审计、超时、限流、Trace 和 Panic 恢复；进程命令使用装饰器/显式调用链实现审计，**不能只靠 Gin Middleware 覆盖后台任务**。
8. **单一 Owner**：Program 状态来源、命令受理者、守护执行者、持久化写入方需明确；禁止隐藏的跨包双向循环调用。

**推荐新增代码示例（示意合同，尚非已实现 API）**：

~~~go
type ProgramReader interface {
    Get(ctx context.Context, id int64) (Program, error)
}

type StatusService struct {
    store  ProgramReader
    logger *slog.Logger
}

func NewStatusService(store ProgramReader, logger *slog.Logger) *StatusService {
    return &StatusService{store: store, logger: logger}
}

func (s *StatusService) Get(ctx context.Context, id int64) (Program, error) {
    p, err := s.store.Get(ctx, id)
    if err != nil {
        return Program{}, fmt.Errorf("get program %d: %w", id, err)
    }
    return p, nil
}
~~~

示例仅说明结构，不要求在 Go 1.19 兼容期全面迁移 `log.Logger` 到 `slog`；`log/slog` 是未来统一结构化日志的候选，具体最低 Go 版本与第三方方案必须独立确定。

## 5. 错误、异常与资源所有权

- 必须返回 `error` 并携带上下文；被调用方只说明本层事实，调用方统一决定重试、降级、告警或退出。不得以 `panic` 处理普通输入/IO/数据库错误。
- 包装底层错误用 `fmt.Errorf("...: %w", err)`，上层用 `errors.Is/As` 分类；用户可理解错误与运维诊断详情分开，HTTP 不输出原始 SQL、磁盘密钥路径或堆栈。
- 必须处理关键错误：进程启动失败、DB 提交失败、网络写失败、配置加载失败、日志初始化失败。忽略错误必须写明原因并有测试，禁止习惯性 `_ = err`。
- 清理使用 `defer` 或显式 `Close/Stop`；文件、Rows、DB、Socket、HTTP Server 都要定义 Owner，任何分支不得泄漏。`rows.Next` 后检查 `rows.Err()`。
- `os.Exit` 只能在最外层 `main` 决定退出码；业务包返回错误，不直接调用 `os.Exit`；已使用 `os.Exit` 的旧代码在有测试后渐进迁移。
- 允许可预期的降级结果，但不得把失败伪装成成功；状态至少区分 `Healthy`、`Degraded`、`Unreachable`、`Unknown` 等语义，具体 API 枚举另行版本化。
- 错误消息尽量小写、无多余句号；日志可使用明确中文业务描述。

## 6. 并发、生命周期与进程守护

1. 新长期服务按 `Start(ctx) / Stop(ctx) / Wait()` 或等价合同实现：幂等启停、唯一运行状态、重复停止无副作用。接口不必预先强制公共化。
2. `context.Context` 原则上是受理/执行方法的首参数，传递到 HTTP、网络调用、数据库查询和可中断任务；不要把 Context 作为普通 struct 长期字段，根运行 Context 由进程装配层管理。
3. 每个 goroutine 都必须有明确的退出路径、取消机制、等待方式及异常恢复策略；不能只依赖进程退出强杀。
4. channel 的生产者、消费者、关闭方和缓冲容量必须有合同；由发送方/所有者负责关闭，防止重复关闭、阻塞和丢事件。
5. 共享 map/状态必须通过 `sync.Mutex`、`RWMutex` 或消息串行化统一保护；不可读写混用无锁。禁止为简化接口长期持有锁做 IO/网络操作。
6. 原 `internal/daemonc/daemon.go` 中包级 `DaemonRun` 是**渐进治理对象**：后续替换成实例生命周期时须验证重复启动、停止、停止期间退出、崩溃重启与竞态，禁止在缺测试时直接大改。
7. 进程重启必须定义快速退出/长期稳定的分类、退避、最大重启次数、计数重置规则、停机优先级和人工禁用状态；**不得无限高速重启**。
8. Monitor 与 Daemon 心跳/状态区分：**进程存在、心跳活着、业务就绪、故障恢复完成**不能互相替代。应用业务状态应由业务 Owner 自身提供。
9. 对外命令须有幂等键/命令 ID（设计阶段）、执行状态与确认超时；即使 HTTP 请求断开，也不能误将已受理执行视为未执行。
10. 外部对 Daemon 自身的存活守护，使用合适 OS 服务管理器（Windows Service / launchd / systemd），不允许它在同一进程中自我监督作为唯一容错。

## 7. HTTP、H5、API 与安全

### 7.1 统一 Web 后端

- 默认继续由 **Monitor 同一进程**托管 H5 静态资源和 HTTP API；不专设一个只有转发功能的第三进程。
- Handler **只负责**请求绑定、校验、权限结果、调用应用服务与响应 DTO；不直接调用 `os/exec`、执行 SQL 或实现守护循环。
- 现有 `GET /programs_status` 为旧兼容接口；新 API 推荐挂在 `/api/v1/` 下，采用明确状态码和一致 JSON 模型。旧字段不能无版本修改。
- 受理命令建议返回命令 ID 与受理状态（视同步/异步情况决定 HTTP 200/202）；运行结果通过查询/订阅反馈，不以“请求已提交”冒称“进程已重启”。
- H5、Studio 与第三方客户端共用 API 合同，包含请求 ID、程序 ID、动作、状态、时间和错误代码；所有可选字段写明缺省行为与单位。

### 7.2 安全硬门禁

1. **生产操作接口必须先认证、授权、审计**，再转发 Daemon；权限至少区分只读/操作/管理。重启、停止、修改程序、配置写入和 OS 命令属于高危动作。
2. 当前 `cmd/monitor/config.go` 的 `-h` 默认绑定 `0.0.0.0`，不能视作安全默认值。**新增部署模板应优先回环监听**，必须远程访问时显式配置可信网络/TLS/反向代理与鉴权；不在无迁移说明时偷偷修改旧部署行为。
3. 当前 `cmd/monitor/web.go` 通过 `channels.GinRegister` 注册 pprof。**生产环境默认关闭诊断路由**，仅调试/受控回环与授权条件下启用。不得在公开地址直接开放 pprof。
4. CORS 采用可信 Origin 白名单；不能把 `Access-Control-Allow-Origin: *` 与带凭据跨域作为通用方案。Cookie 方案另考虑 CSRF、防会话固定、SameSite/Secure/HttpOnly。
5. 必须设置服务端读取头/读取请求/写入/空闲超时，限制 HTTP Body、上传大小、并发请求与限流；对远程地址/文件路径做规范化与访问范围校验。
6. H5 静态资源须明确发布目录与缓存策略，避免任意文件访问、路径穿越、不可信脚本注入；动态内容按 HTML/JSON 场景转义，视部署设置 CSP 等安全响应头。
7. 内部 UDP CRC 仅校验完整性，**不能代替鉴权或抗重放**。控制通道必须考虑可信网络、来源验证、消息鉴别、nonce/序列号及过期检查；改动旧报文格式需版本协商。
8. 不得通过浏览器直接暴露任意系统命令、通用 Shell 或任意可执行路径。只允许有权限的白名单 Program ID/动作，记录操作者、目标、结果与拒绝原因。
9. 所有实际密码、密钥、Token、生产数据库、客户地址和日志敏感字段不可进入公开 Git 仓库。CI 使用专有 Secrets，日志默认脱敏。
10. 无鉴权的既有只读接口也应纳入部署访问控制；**将来源可访问的接口误判为“天然内网安全”不符合本规范**。

### 7.3 必备 HTTP 质量测试

使用 `httptest` 验证状态查询、参数错误、认证失败、权限不足、重复请求、超时、后端不可达、并发请求、CORS/CSRF 规则（按认证方式）、静态资源与非预期路由；对可执行操作仅使用 Fake Daemon，不在 CI 直接杀宿主进程。

## 8. 配置、日志、持久化与跨平台

**配置：**

- 可保留现有 Viper/`sys.config` 兼容合同，但装配层必须集中加载和验证；新业务代码不依赖 Viper 的全局单例。
- 配置优先级应固定并写入发布说明（建议命令行/环境变量覆盖配置文件，真实生效顺序以实现和测试为准）。
- 时间使用 `time.Duration`，明示毫秒/秒；路径用 `filepath.Join`；跨平台文件目录和 OS 服务管理分别适配。
- 配置缺失、非法监听地址、端口冲突、DB 不可读、相对目录越界等情况应给出明确失败，不用隐式空值继续运行。
- 运行配置与构建产物分离；不同环境用模板且不包含真实凭据。

**日志与诊断：**

- 日志至少包括时间、级别、组件、事件、程序 ID、请求/命令 ID、结果/错误；普通事件与重要操作事件可区分持久化策略。
- 不通过全局 `log.Printf` 混杂多个业务组件；保留现有 `log.Logger` 时通过实例注入，后续再评估统一结构化日志。
- 关键事件：服务启停、心跳失联/恢复、进程启动/退出/重启、超过重试阈值、人工操作、配置变更、授权拒绝，均须有审计和可追踪的因果信息。
- 日志轮转、限额、保留时间、写盘失败策略必须可配置，防止监控服务耗尽磁盘；不能用无限制内存列表保存历史。

**SQLite / 数据：**

- 保留原 Program 表与数据类型；Schema 变更需有版本号、升级/回滚、兼容测试及真实旧库样本。
- 查询按参数绑定而不是拼接外部输入 SQL；事务必须明确提交/回滚与错误处理，资源释放要在失败路径覆盖。
- DB 查询应提供 context/超时；关注 SQLite 单机多写、busy 处理、连接池和进程间数据一致性。
- 原 DB 类型（如 `program.Program`）不直接作为全新的 Web API DTO 暴露；用显式映射隔离字段与兼容性。
- 状态集合要明确 Snapshot 时刻、字段单位、过期阈值及 Source；禁止将“没收到心跳”直接写成“进程一定退出”。

## 9. 测试、质量门禁与验收等级

| 级别 | 验收内容 | 可声明结果 |
| --- | --- | --- |
| G0 静态 | 编译目标、gofmt、go vet、目录/import、依赖扫描 | 编码及构建合同检查 |
| G1 单元 | 帧 CRC、边界值、状态机、重试策略、错误路径、DB 临时库、HTTP `httptest` | 目标逻辑的自动测试 |
| G2 隔离集成 | Monitor+Daemon+假子程序，正常/异常退出、重复命令、超时、进程关闭、配置兼容 | 非生产隔离运行验收 |
| G3 跨平台 | Windows/macOS/Linux 的构建、CGO/SQLite、进程信号/停止行为和路径测试 | 目标平台兼容性 |
| G4 现场 | 实际设备及生产进程、权限、部署升级、断网/重启/掉电与日志追溯 | 工业环境验收 |

**CI 最低门禁（在 `Services/Supervision` 中执行）：**

~~~bash
test -z "$(gofmt -l ./cmd ./internal ./common)"
go vet ./...
go test -count=1 ./...
go build -o /tmp/monitor ./cmd/monitor
go build -o /tmp/daemon ./cmd/daemon
~~~

- 构建产物放临时输出目录；**Windows 不使用固定 `/tmp` 路径**，CI 应用平台 Runner 临时目录并加 `.exe` 后缀。
- 跨平台矩阵至少 Windows amd64、macOS arm64、Linux amd64；依赖 CGO 的 SQLite 需在每个平台确认 C 编译器与运行能力，不可仅交叉编译宣布运行通过。
- 关键 goroutine/状态读写需在支持的平台执行 `go test -race`；跨平台差异要记录，Race 不通过不能作为工业验收完成。
- 对 UDP 帧、参数解析与外部输入补 `FuzzXxx`（在隔离 CI 单独设置时限）；不能让 fuzz 无限运行。
- 进程生命周期测试用临时目录/测试子进程；不能操作 CI Runner 上与测试无关的真实进程。
- 任何自动任务未执行或因缺少配置跳过，标记 `NotRun`，**绝不标记 Passed**。
- 安全变更和协议变更需有反向用例；覆盖率用于发现盲区，不以单一百分比代替场景验收。

## 10. 代码评审与变更规则

提交应包含：变更目的、唯一 Owner、影响路径、是否涉及 HTTP/UDP/JSON/SQLite 兼容、风险及回滚方式、真实执行的命令与测试输出、未验证事项。

**必须阻止合并的情况**：

1. 新增对外高危操作却无认证、授权或审计；
2. 引入 goroutine/Channel 生命周期泄漏或明显数据竞争；
3. 改动消息码/数据库字段/旧配置而无兼容迁移和用例；
4. 关键 IO 错误吞掉、恢复无上限或危险指令无程序白名单；
5. 提交 Token、私钥、生产 DB/日志或客户敏感配置；
6. 跳过 `go vet`、测试或三平台失败而宣告完成；
7. 新建重复 Host、监控状态真源、长驻进程或无意义基础抽象。

实施中优先做 **一个可审核的纵向切片**，例如“HTTP 请求鉴权→委托 Daemon→状态回传→测试”，不要先新建一整套无功能的空接口/空目录。

## 11. 分阶段收敛路线（不是已实现声明）

| 阶段 | 目标与验收 | 现状 |
| --- | --- | --- |
| P0 规范门禁与安全 | 本文生效、新代码格式/测试、pprof 暴露控制、受控监听和访问权限基线 | **规范目标；安全控制待实装** |
| P1 服务对象化 | Monitor/Daemon 运行状态实例化、Context、可测试退出、HTTP/业务职责分离、仓储接口 | **待实现；保留旧行为** |
| P2 对外服务合同 | /api/v1、权限/审计、命令 ID/确认、状态 DTO、H5/Studio 共用调用 | **待实现** |
| P3 工业成熟与平台集成 | 长驻守护/断电/离线/重试、配置升级、跨平台实机、CxVisual Transport/Runtime 接入 | **待实施和验收** |

迁移顺序：固定原行为和数据库/报文样本 → 增加反例测试 → 封装新组件 → 逐接口迁移 → 保持旧接口适配 → 三平台与现场验证 → 删除旧路径。任何一步没有通过测试，均不能自动推进到破坏性删除。

## 12. 发布前检查清单

- [ ] 本次改动明确唯一 Owner，未增加重复 Host、重复状态真源
- [ ] 新增文件符合 `gofmt`，命名/导出注释/错误处理符合规范
- [ ] 旧 HTTP/UDP/JSON/DB/配置契约未无意变化，必要时有迁移测试
- [ ] 高危操作有鉴权、授权、审计、请求 ID，拒绝执行有记录
- [ ] pprof 与静态资源/跨域/监听地址按部署安全策略处理
- [ ] goroutine、channel、DB、HTTP、子进程均有退出/释放合同
- [ ] 有异常、竞争、超时、重复、断连、资源不足的反向用例
- [ ] Windows、macOS、Linux 实际构建与单测证据可追溯
- [ ] 若仅构建通过，明确说明运行/现场验收尚未完成
- [ ] 中文 Commit、文档与 README 导航更新，风险和剩余项可见

## 附录 A：本版审查发现的真实技术债

以下均是**源码级观察**，不是已证明发生过的生产故障：

| 位置 | 观察 | 应对 |
| --- | --- | --- |
| `cmd/monitor/config.go` | `-h` 默认为 `0.0.0.0`；Viper 全局读配置 | 新部署按安全基线收敛，实例化配置 |
| `cmd/monitor/web.go` | `/programs_status` 与 pprof 注册集中在入口；未见统一鉴权中间件 | 保留旧 API，加入受控中间件与路由分层 |
| `cmd/monitor/web_app.go` | Handler 直接调用进程状态汇总并返回旧 JSON 结构 | 保留兼容，抽离应用服务与 DTO |
| `common/channels/https.go` | HTTP/静态页/CORS 在公共通道中；部分 CORS 实现允许宽泛 Origin | 收敛 Origin/TLS/超时与资源访问 |
| `cmd/monitor/app_status.go` | 使用包级状态 map 与互斥锁 | 逐步建立具明确锁边界的状态实例 |
| `internal/daemonc/daemon.go` | `DaemonRun` 为包级运行开关，循环负责守护重启 | 改造成可取消生命周期前先补回归测试 |
| `internal/program/db.go` | `database/sql`、gorp 与 SQLite 耦合，错误上下文写法不一致 | 保留 Schema，逐步引入可测试仓储封装 |
| `go.mod` | 继承原 IoT 大模块依赖列表，`module gogo`、`go 1.19` | 固定行为后独立清理依赖与模块身份 |

## 附录 B：官方参考与文档关系

- [Effective Go](https://go.dev/doc/effective_go)：命名、注释、错误处理与惯用写法。
- [Go Code Review Comments](https://go.dev/wiki/CodeReviewComments)：Context、goroutine 生命周期、导入分组、错误检查、接口。
- [Go Fuzzing](https://go.dev/doc/tutorial/fuzz)：恶意输入/协议边界模糊测试。
- 主仓 `DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` 是整体 Owner/复用入口；本规范**仅管理独立 Go Supervision 工程**。
- `Services/Supervision/README.md` 是开发运行入口；Go 模块源码和真实 CI 结果才是已实现事实。
- 原 `device_communicator` 生产经验应继续尊重，但迁入 CxVisual 的功能完整性需在新部署环境重验。

**版本控制**：本文 V1.0 自提交之日起作为新代码与有实质改动代码的默认审查标准；需要改变公共协议、进程边界、语言最低版本、授权模型或数据模型时，先通过 CxVisual Owner/ADR 裁决并升级本文版本。