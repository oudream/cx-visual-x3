#!/usr/bin/env bash
# 从已检出的旧仓批量复制Go Monitor/Daemon及完整本地源码依赖。
# 使用: bash Tools/Migration/import-device-supervision.sh /path/device_communicator
set -euo pipefail
SOURCE="${1:?请传入原device_communicator仓库本地路径}"
ROOT="$(git rev-parse --show-toplevel)"
DEST="$ROOT/Services/Supervision"
test -f "$SOURCE/go.mod" && test -f "$SOURCE/go.sum"
test -f "$SOURCE/hy/monitor/main.go" && test -f "$SOURCE/hy/daemon/main.go"
test ! -e "$DEST/go.mod" || { echo "拒绝覆盖现有正式Go工程"; exit 2; }
mkdir -p "$DEST"
# 不重写package/import路径；复制整个common、hy下全部Go源文件以完整保留相互依赖。
# 原仓包含业务部署密钥/私有配置，不直接复制非源码文件；vendor可通过go.mod/go.sum恢复。
while IFS= read -r -d '' f; do
  rel="${f#"$SOURCE"/}"
  mkdir -p "$DEST/$(dirname "$rel")"
  cp -p "$f" "$DEST/$rel"
done < <(find "$SOURCE/common" "$SOURCE/hy" -type f -name '*.go' -print0)
cp -p "$SOURCE/go.mod" "$SOURCE/go.sum" "$DEST/"
echo "源文件数量: $(find "$DEST" -name '*.go' | wc -l)"
echo "在 $DEST 中运行: go build ./hy/monitor && go build ./hy/daemon"
# 不进行git commit；先由操作者验证依赖、配置、凭据、跨平台与Windows工程。
