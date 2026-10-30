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
