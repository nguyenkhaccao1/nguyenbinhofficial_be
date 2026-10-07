#!/usr/bin/env bash
# Chen/cap nhat block nguyenbinhofficial.com.vn vao nginx dung chung (AciPlatform) mot cach an toan:
#   1. Sao luu nginx.conf (co dau thoi gian, cung kieu cac ban sao luu san co)
#   2. Thay block giua 2 dong danh dau NGUYENBINHOFFICIAL (hoac chen truoc dau "}" cuoi file)
#   3. nginx -t trong container; loi → tu khoi phuc ban sao luu, KHONG reload
#   4. Reload nginx (khong ngat ket noi cac site khac)
#
# Dung: deploy/install-nginx.sh http   (truoc khi co SSL)
#       deploy/install-nginx.sh https  (sau khi cap chung chi)
set -euo pipefail

MODE="${1:-https}"
ACI_ROOT="${ACI_ROOT:-/root/apps/AciPlatform}"
NGINX_CONF="$ACI_ROOT/nginx.conf"
NGINX_CONTAINER="${NGINX_CONTAINER:-aciplatform-nginx}"
HERE="$(cd "$(dirname "$0")" && pwd)"

case "$MODE" in
  http)  SNIPPET="$HERE/nginx/nguyenbinhofficial.http-only.conf" ;;
  https) SNIPPET="$HERE/nginx/nguyenbinhofficial.conf" ;;
  *) echo "Usage: $0 http|https" >&2; exit 2 ;;
esac

BACKUP="$NGINX_CONF.bak-nguyenbinhofficial-$(date +%Y%m%d%H%M%S)"
cp -p "$NGINX_CONF" "$BACKUP"
echo "Backup: $BACKUP"

python3 - "$NGINX_CONF" "$SNIPPET" > /tmp/nginx.conf.nguyenbinh <<'PY'
import re, sys
conf = open(sys.argv[1], encoding="utf-8").read()
snippet = open(sys.argv[2], encoding="utf-8").read().rstrip() + "\n"
pattern = re.compile(r"[ \t]*# --- NGUYENBINHOFFICIAL:.*?# --- NGUYENBINHOFFICIAL END ---[ \t]*\n?", re.S)
if pattern.search(conf):
    conf = pattern.sub(lambda _: snippet, conf, count=1)
else:
    idx = conf.rstrip().rfind("}")          # dau dong cua khoi http {}
    conf = conf[:idx].rstrip() + "\n\n" + snippet + "\n" + conf[idx:]
sys.stdout.write(conf)
PY

# Ghi de NOI DUNG (giu inode): nginx.conf duoc bind-mount dang file don, ghi file moi se khong vao container.
cat /tmp/nginx.conf.nguyenbinh > "$NGINX_CONF"
rm -f /tmp/nginx.conf.nguyenbinh

if ! docker exec "$NGINX_CONTAINER" nginx -t; then
  echo "nginx -t FAILED → khôi phục bản sao lưu" >&2
  cat "$BACKUP" > "$NGINX_CONF"
  docker exec "$NGINX_CONTAINER" nginx -t
  exit 1
fi

docker exec "$NGINX_CONTAINER" nginx -s reload
echo "nginx reloaded with mode=$MODE"
