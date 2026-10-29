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
