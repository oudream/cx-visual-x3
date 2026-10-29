#!/usr/bin/env bash
# 在同时具有两仓权限的工作站一键完整迁入旧 Go 模块。
# 用法: bash Tools/Migration/import-device-supervision.sh [已检出的旧仓本地目录]
set -euo pipefail
ROOT="$(git rev-parse --show-toplevel)"
DEST="$ROOT/Services/Supervision"
TMP=""
STAGING=""
cleanup() {
  [[ -z "$STAGING" ]] || rm -rf "$STAGING"
  [[ -z "$TMP" ]] || rm -rf "$TMP"
}
trap cleanup EXIT
if [[ $# -gt 0 ]]; then
  SOURCE="$(cd "$1" && pwd)"
else
  command -v gh >/dev/null || { echo "缺少 gh CLI，请提供旧仓路径"; exit 2; }
  gh auth status >/dev/null || { echo "请先 gh auth login 授权访问原私有仓"; exit 2; }
  TMP="$(mktemp -d)"
  gh repo clone oudream/device_communicator "$TMP/source" -- --quiet
  SOURCE="$TMP/source"
fi
for f in "$SOURCE/go.mod" "$SOURCE/go.sum" "$SOURCE/hy/monitor/main.go" "$SOURCE/hy/daemon/main.go"; do
  [[ -f "$f" ]] || { echo "缺少原始文件: $f"; exit 2; }
done
[[ ! -e "$DEST/go.mod" ]] || { echo "正式 Go 模块已存在，拒绝覆盖"; exit 2; }
mkdir -p "$ROOT/Services"
STAGING="$(mktemp -d "$ROOT/Services/.supervision-stage.XXXXXX")"
# 保真复制整个 Go 本地源码依赖（common/hy），排除秘密配置/证书/运行数据/vendor。
while IFS= read -r -d '' f; do
  rel="${f#"$SOURCE"/}"
  mkdir -p "$STAGING/$(dirname "$rel")"
  cp "$f" "$STAGING/$rel"
done < <(find "$SOURCE/common" "$SOURCE/hy" -type f -name '*.go' -print0)
cp "$SOURCE/go.mod" "$SOURCE/go.sum" "$STAGING/"
mv "$STAGING" "$DEST"
STAGING=""
printf '复制 Go 源文件数量: %s\n' "$(find "$DEST" -name '*.go' | wc -l)"
TMP_OUTPUT_MONITOR="$(mktemp)"
TMP_OUTPUT_DAEMON="$(mktemp)"
trap 'rm -f "$TMP_OUTPUT_MONITOR" "$TMP_OUTPUT_DAEMON"; cleanup' EXIT
# 不将生成二进制提交到源码树。
(cd "$DEST" && go build -o "$TMP_OUTPUT_MONITOR" ./hy/monitor && go build -o "$TMP_OUTPUT_DAEMON" ./hy/daemon)
echo "Go 编译成功；仍需 Windows/macOS/Linux 运行验收和凭据安全复核。"
