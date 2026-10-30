# CxVisual Supervision Web 构建和发布

## 开发

~~~bash
cd Services/Supervision/web
npm install
npm run dev
~~~

访问 Vite 输出的地址；当前 Vite `base` 为 `/assets/comm/`。开发时 `/api` 默认代理至本机 `127.0.0.1:7811`；可用 `VITE_MONITOR_PROXY` 覆盖代理地址。无需增加第三个 Go 后端进程。

## 编译、测试和发布

~~~bash
npm run typecheck
npm run test
npm run build
~~~

构建得到 `web/dist/index.html` 与 `web/dist/assets/`。

在 Windows/macOS/Linux，将 **dist 目录内全部内容**复制到本机 Monitor 工作目录的 `assets/comm/`，形成 `<WorkDir>/assets/comm/index.html`。可通过 Monitor `-assets` 指定上层 `assets` 根目录。原服务 `/index.html` 跳转到 `/assets/comm/index.html`，生产不需要 Node.js。

此处不提供覆盖旧现场 H5 的自动脚本，避免误删客户资产。**安全提示**：本期 API 是只读查询，尚未完成登录和授权；必须部署在受控可信网络，不应直接公开到互联网。静态文件 CI 编译成功不等于设备现场部署验收。

## P0 安全访问和远程部署限制（2026-10-30）

新版进程总览/程序清单/详情与事件/异常历史接口统一使用回环限制，`/api/v1/health` 仅供 HTTP 存活点检。只有浏览器与 Monitor 在**同一台机器、同源、回环访问**时可直接正常查询；从另一台电脑打开 Monitor 静态 H5 时，进程数据 API 会返回 403，页面明确提示“仅允许在 Monitor 本机查询”。**不要通过关掉 LocalOnly、伪造 Origin/Host、或让反代丢弃来源头绕开。**

`/programs_status` 为旧系统兼容而暂未收紧，且旧 UDP 监控控制链仍在；若 HTTP/UDP 绑定 `0.0.0.0` 或实际物理网卡 IP，必须在主机防火墙及产线网络 ACL 限制访问。为最小暴露，尽量将 `conf/sys.config.*` 的 `monitor.http.host` 配为 `127.0.0.1`，在历史调用方需要跨机访问时先评估影响；这不是自动转换旧生产配置。Monitor 对可能非本机的 HTTP 绑定会打印 SECURITY WARNING。

正式远程查看/操作属于后续 B/C/D 分期：需身份认证、授权/Scope、审计、可信 IPC 或网关，以及前后端实测；不能把本机限制当作已实现的认证产品。启动、停止、重启 API 仍未开放。


## Daemon 守护上限闭锁与离线处置（2026-10-30）

Daemon 达到受管程序的 MaxError 或 MaxCount 终止上限后，将终止原因记录于工作目录下的 supervision/daemon-terminal-latches.json。

该文件是**安全控制状态**，不是缓存或诊断临时文件。Daemon 在启动受管进程之前校验，遇到损坏、不可读、版本不匹配或不可写的情况拒绝管理启动（错误退出码 -6），防止静默清空已知上限。终止时使用私有权限临时文件、文件同步和同目录原子替换；工作目录必须在可持久写入、具备 ACL 与备份的存储上。

**现场排障**：先查看 Daemon/Monitor 终止事件确定 ProgramId 与 MaxError/MaxCount，查明进程及设备实际故障，再检查工作目录空间、权限和磁盘。程序设置 Enabled 不表示可以越过锁；不能依靠周期恢复、普通旧 UDP 重启或反复重启 Daemon 清除已持久化的终止原因。

**当前受控离线恢复步骤**：维护人员确认故障排除和设备安全状态；停止 Daemon 与受影响的设备业务流程；备份原文件并记录时间、操作者、ProgramId、终止原因与恢复理由；仅在确认 Daemon 已停止时离线审查修改特定 ProgramId 的终止锁条目，保留版本号 1、其余终止锁和访问权限；重新启动并核对。不要在线编辑或删除整份文件，也不要给原 UDP 新增未认证的清锁操作。

以上属于现场离线运维规程，**不代表软件已具备身份认证、授权审批、不可篡改审计或安全在线解锁**；S11 仍须实现这些能力。已记录的 Kill 成功仅表示向 OS 发出了终止请求，尚非受管进程退出或业务 Ready 的证明。


## Daemon异常退出的遗留PID隔离（2026-10-30）

**行为变化**：Daemon 启动时不再根据旧 `pid/*.pid` 文件自动 Kill 可能存活的受管进程。相反，它只读检查文件和 OS 进程状态，无法确认属于安全的“进程已结束”情况时，把对应 Program 放入当前 Daemon 进程的隔离表，**拒绝首次自动启动、周期恢复及配置启用/续接**。日志关键词为 `ORPHAN QUARANTINE`，含 ProgramId 与原因。历史 PID 文件不被自动删改。

出现隔离时：先查看 PID 文件、Daemon/Monitor 历史记录、OS 实际进程及设备业务状态。此类文件只记录旧整数 PID，不带可靠进程创建代次，**禁止为了让设备“恢复运行”直接反复重启 Daemon 或盲目删除 PID 文件、强制 Kill**。由获授权的现场维护人员确认设备安全与程序真实归属，停止 Daemon 和受影响业务流程，备份 PID/事件/日志并留下维护记录，再离线处置遗留进程和文件；随后启动并查验当前 Worker 及业务 Ready。对生产部署必须核对同名可执行文件是否被多个 Program 共用旧 PID 名称，以及配置中 PID 文件实际写入的历史范围。

**残余限制**：现有旧 PID 文件只在主循环收到 EventStart 后保存；如果 Daemon 在子进程 Start 成功与该事件消费之间被强制终止，这套只读扫描可能缺少证据，不能称已彻底解决孤儿进程。下一阶段需在原 Worker 启动成功后同步持久化创建代次。保守隔离不是安全在线解锁、身份认证、审批/审计或进程树停止确认，不得代替 S11 正式安全运维。


## H5守护诊断与本机数据来源（2026-10-30）

新版 H5 增加“守护诊断”导航，`GET /api/v1/guard` 只允许 Monitor 本机回环请求读取，**不提供启停、重启、清闭锁和远程操作**。它通过工作目录的两份只读来源展示独立记录：`supervision/daemon-terminal-latches.json`（Daemon MaxError/MaxCount 持久终止锁）和 `supervision/daemon-orphan-observation.json`（最近一次 Daemon 启动时遗留 PID 只读扫描，含 UTC 时间）。Monitor 与 Daemon 必须配置可访问**同一工作目录**并由文件 ACL 赋予读取权；不同工作目录或权限不够时，UI 必须显示来源不可用，不能把数量 0 当作安全。

使用：本机打开 Monitor H5 → 左侧“守护诊断”查看数据来源、时刻和原因 → 点击 Program ID 在“程序与服务”定位对应程序 → 在“事件记录”或“异常历史”核查旧事件，最终结合现场操作日志和设备实际状况判断。运行总览同步提供守护上限与孤儿隔离两类计数；历史扫描不随程序运行动态更新，**并不代表当前 OS/业务 Ready**。只有来源确实可用且未记录该 Program 时才显示“无已记录保护项”，并不是自动解除锁。

发布：按照现有 web/dist/ → assets/comm/ 方式部署新版 H5，确保 Go Monitor/Daemon 同版本、且已有文件布局符合规范；远程跨机器访问新诊断端点仍返回 403。必须走现场受控离线恢复，不要通过修改浏览器请求、清空 PID 或删除闭锁文件绕过守护策略。完整 Go 与 Web CI 的对应 SHA 成功记录取得前不得把该版本宣布为生产验收通过。
