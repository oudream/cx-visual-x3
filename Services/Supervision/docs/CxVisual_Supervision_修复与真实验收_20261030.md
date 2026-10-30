# Supervision 修复与真实验收（2026-10-30）

## 基线与协作保护

- 原工作区 `master@505b2acc` 有未提交 HMI 和 AI 文档修改，直接快进被 Git 拒绝；未暂存、覆盖或清理这些文件。
- 在 `Output/SupervisionRepair` 的隔离 `master` 检出工作，先核对 `e43ae21f`，提交前快进至 `8a9f8ac8`；没有创建开发分支，没有改写共享历史。
- 复核 AGENTS、工程技能、DOCS/00、DOCS/01 与 02～04 原始 XLSX、Supervision Go/Web 规范、V2.0 全类设计及功能矩阵；对照 `ReferTo/device_communicator/hy/{daemon,monitor,common/daemonc}`。没有改变 F/M/P Owner、UDP 字段、SQLite 表和模块最低 Go 版本。

## 复现及根因

历史失败 CI：[41049097391](https://github.com/oudream/cx-visual-x3/actions/runs/41049097391)，源码 SHA `e36cec27d1a4925ef8cae7fb6e580c2b26b2b617`。

| 范围 | 实际失败 | 修复 |
|---|---|---|
| Go 三平台 | guardview 测试第97行非法反斜杠 | 修复 Go 原始字符串语法，保留符号链接反证 |
| launchjournal | 旧凭证测试转为 started 时没有观察时间 | 补齐合法旧凭证 Fixture；保留无 OS 创建时间时隔离的断言 |
| Web CI | identity 正则不能匹配后端的大写 PID | 允许保留大小写的完整证据码，保留既有测试 |
| npm ci | 仓库缺少锁文件 | 提交 package-lock.json，CI 使用 npm ci |
| Go 1.26 vet | UDP 字符串拼接不支持 IPv6；Notify 使用无缓冲通道 | 使用 JoinHostPort，信号通道缓冲及 Stop 清理 |
| 真实 Monitor | Daemon 缺席后每500ms请求状态 | 首次立即请求，其后最短30秒；新鲜响应维持原30分钟检查语义 |

## 真实实现增量

1. `launchjournal.Journal` 方法序列化；跨进程锁覆盖 Begin/Started/Finished/Scan；同 AttemptID 的路径、时间等全部字段变化也拒绝覆盖或清算。
2. `journalfile` 使用 Windows LockFileEx、Linux/macOS flock。锁文件保留，进程退出/被 Kill 后由 OS 释放锁，不以删除锁文件争抢所有权。新增真实子测试进程 Kill/Wait 后重新取得锁的反证。
3. 逐层检查安全目录，拒绝祖先符号链接；停止流水追加后再检查路径与目录身份。依赖受控工作目录 ACL，不能抵御具有同等文件写权限的恶意进程任意替换目录。
4. 停止流水在追加前检查完整的有界历史，拒绝不存在请求的回执、重复请求及跨 Program/PID/Action 的 OperationID；不是只检查 H5 最新200条。拒绝不会污染已有有效记录。
5. Monitor 读取与 Daemon 追加使用同一跨进程锁，避免读取半条记录；持久状态仍只有 Daemon 写入。两者需要协调锁文件的访问权限。读取使用打开文件的固定长度，损坏、截断、超限仍明确不可用。
6. 4MiB 上限内为已有 requested/unknown 预留回执容量，优先拒绝新请求；不自动截断、轮换或删除未结清证据。既有日志若已满/损坏，仍须离线处置。
7. 全局刷新覆盖六页面，工具栏显示对应来源查询时间；Pinia 回归检查失败保留快照、恢复与请求去重。程序详情忽略较早或已关闭请求的迟到响应。

## 本机实际验收

环境：Windows amd64，`D:\Software\Go\bin\go.exe` 为 Go 1.26.10；启用 `CGO_ENABLED=1`，CC 为 `D:\Software\Strawberry\c\bin\gcc.exe`。离线 Go 依赖使用原工作区已有 vendor，只读复用，未纳入提交。CI 继续采用 Go 1.23，平台结果单独核对。

| 命令/验收 | 实际结果 |
|---|---|
| `go build ./...` | 通过 |
| `go test -count=1 ./...` | 全包通过，包括真实 SQLite 初始化、int64 往返和唯一键反证 |
| `go vet ./...` | 通过 |
| `go test -race -count=1 ./...` | 通过；涵盖跨进程追加与并发读者、同一启动凭证并发推进/清算 |
| 关键包 `go test -race -count=10 ... -run ...` | launchjournal、stopjournal、daemonc、cmd/daemon 重复通过 |
| `npm ci` | 修复缺失锁文件后通过 |
| `npm run typecheck` / `npm run test` / `npm run build` | 通过，5个测试文件、25项测试；构建保留现有大资源包警告 |
| 真实 Monitor + 无头 Chrome | 六页面、1440px桌面/390px窄屏、双主题/持久化、int64跳转、失败保留快照及恢复、Origin拒绝403通过；无页面JS异常 |
| 约3.20MiB流水追加基准，3次 | 32.64ms/op，98.16MB/s，约10.7MB分配/op；这是本机小样本，不是现场延迟保证 |

本机日志和 PNG：`Output/SupervisionEvidence/`，含 go-test、go-race、go-repeat、go-benchmark、web-test、web-typecheck、web-build 和 browser-results.json。截图展示隔离工作目录的真实 Monitor；停止条目来自本轮独立测试子进程真实 Start/Kill/Wait，未启动任何现场程序或设备。初次浏览器验收因测试资产目录晚于 Monitor 启动而失败，准备目录后重新启动并通过；没有更改正式静态资源合同。

## CI 与剩余边界

CI 已增加完整 build/vet、Linux全包 race、关键并发重复和锁定依赖安装。新提交三平台结果必须以该 SHA 的实际 GitHub Actions 为准，提交前不宣称通过。

尚未验收：工控机长稳、突然断电持久性、所有文件系统/杀毒软件组合、完整进程树终止、远程身份/RBAC/审计及恢复审批、真实 Service/Runtime/Professional 观测接入。OS进程证据、历史回执、HTTP200和界面均不能证明设备 Ready。停止流水仍为有界诊断历史，不是不可篡改安全审计。

## 提交后 CI 修复与输入核查

- 源码 `ac81a21f` 已推送；Git 凭据不具备 workflow scope，工作流增强改由已连接 GitHub 工具提交为 `83f1c0aa`，没有强推或要求更换凭据。
- [83f1c0aa 实际 CI](https://github.com/oudream/cx-visual-x3/actions/runs/41053407430) 的 macOS 首轮失败来自目录安全检查误拒绝系统 `/var` 别名，而非 Go 锁实现缺失。补充只允许 macOS 根路径 `/var`、`/tmp` 解析到各自 `/private/...` 的系统映射，保留用户目录/祖先链接拒绝；新增原生临时目录与祖先链接未写出边界反证。该修复的三平台结果以后续 SHA 记录为准。
- 继续核查发现原 `MonA` 忽略 Daemon 状态/事件 JSON 解码错误，可能把损坏响应计为新鲜并纳入部分状态。现新增整包校验：拒绝损坏/null、非法或重复 ProgramID、负数计数、运行却没有PID的矛盾状态；拒绝前不改变旧快照/观察时间。保留原帧、字段名及合法旧格式的回归，不把CRC当身份认证。

## 最终固定 SHA 的跨平台结果

**源码 SHA：`5760a30d2ecae7809a2138c978d6f9320287583d`**。已读取每项 job 的最终状态及实际日志，[CI run 41053957545](https://github.com/oudream/cx-visual-x3/actions/runs/41053957545) 的五项任务全部 `completed / success`。

| 任务 | Job ID | 真实结果 |
|---|---|---|
| Windows Go | 124056665604 | Monitor/Daemon及全包构建、全包测试、vet通过；SQLite集成测试实际执行 |
| Linux Go | 124056665585 | 构建/测试/vet、全包race及关键用例10次重复通过 |
| macOS Go | 124056665439 | Go 1.23.12 darwin/arm64构建/全包测试/vet通过，系统目录兼容修复生效，SQLite集成测试实际执行 |
| Vue H5 | 124056665125 | npm ci、typecheck、5文件25项测试、生产build通过 |
| Windows C# | 124056665472 | 原Lifecycle撤销结构检查及Core/Runtime.Host/ArchitectureSmoke项目编译通过；该job没有执行C#测试exe，不冒充运行回归 |

提交链：`ac81a21f`（主体源码/测试/H5/本机验收）、`83f1c0aa`（工作流）、`5760a30d`（实际macOS CI修复及UDP损坏状态防护）。本节为后续文档登记，引用上述已验证源码SHA，不把文档提交本身当作重新执行的设备验收。

本轮完成的是可复核的软件修复与跨平台构建/测试，以及本机真实Monitor/H5验收。S03/S04/S06/S07/S11/S12的完整接入、远程运维身份授权审计、设备Ready、现场长稳和断电仍未完成，不能据此宣称整个工业服务监督目标已经验收。
