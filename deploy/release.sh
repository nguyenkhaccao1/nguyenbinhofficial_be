#!/usr/bin/env bash
# Chay tren MAY DEV (Git Bash/WSL/macOS): kiem tra → day 2 repo len GitHub → SSH vao server pull + build + chay lai.
#
#   deploy/release.sh                 # test + push + deploy
#   deploy/release.sh --skip-tests    # bo qua test (chi khi sua nho, da test truoc do)
#   deploy/release.sh --import        # sau deploy, nap lai noi dung deploy/showcase (du an, dich vu, trang, logo...)
#
# Bien moi truong (tuy chon): NB_SERVER (mac dinh root@103.200.22.167), NB_SSH_KEY (mac dinh ~/.ssh/vietnix_ed25519)
set -euo pipefail

SERVER="${NB_SERVER:-root@103.200.22.167}"
KEY="${NB_SSH_KEY:-$HOME/.ssh/vietnix_ed25519}"
SITE="https://nguyenbinhofficial.com.vn"
BE="$(cd "$(dirname "$0")/.." && pwd)"
FE="$(cd "$BE/../nguyenbinhofficial_fe" && pwd)"

RUN_TESTS=1
IMPORT=0
for arg in "$@"; do
  case "$arg" in
    --skip-tests) RUN_TESTS=0 ;;
    --import) IMPORT=1 ;;
    *) echo "Tham số không hợp lệ: $arg" >&2; exit 2 ;;
  esac
done

step() { printf '\n\033[1;32m==> %s\033[0m\n' "$*"; }
fail() { printf '\n\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

# 1. Ca 2 repo phai sach (da commit) va dang o nhanh main — tranh deploy code chua commit/nham nhanh.
for repo in "$BE" "$FE"; do
  name="$(basename "$repo")"
  [[ "$(git -C "$repo" branch --show-current)" == "main" ]] || fail "$name không ở nhánh main."
  [[ -z "$(git -C "$repo" status --porcelain)" ]] || fail "$name còn thay đổi chưa commit:
$(git -C "$repo" status --short)
→ git add -A && git commit -m \"...\" rồi chạy lại."
done

# 2. Kiem tra truoc khi day len.
if [[ $RUN_TESTS == 1 ]]; then
  step "Test backend (dotnet test)"
  (cd "$BE" && dotnet test --nologo -v q) || fail "Test backend lỗi — không deploy."
  step "Typecheck frontend"
  (cd "$FE" && npm run typecheck --workspaces --if-present >/dev/null) || fail "Typecheck frontend lỗi — không deploy."
fi

# 3. Day len GitHub (server se pull tu GitHub).
for repo in "$BE" "$FE"; do
  step "Push $(basename "$repo") → GitHub"
  git -C "$repo" push origin main
done

# 4. Server: pull ca 2 repo, build image, chay lai container, cho API healthy.
step "Deploy trên server $SERVER"
ssh -i "$KEY" "$SERVER" "cd ~/apps/nguyenbinhofficial_be && bash deploy/deploy.sh --pull"

if [[ $IMPORT == 1 ]]; then
  step "Nạp nội dung deploy/showcase"
  ssh -i "$KEY" "$SERVER" "cd ~/apps/nguyenbinhofficial_be && docker cp deploy/showcase nguyenbinh-api:/tmp/ \
    && docker exec nguyenbinh-api dotnet NguyenBinh.Api.dll import-showcase /tmp/showcase"
fi

# 5. Kiem tra tu ben ngoai (qua nginx + HTTPS nhu nguoi dung that).
step "Kiểm tra website"
for path in / /du-an /dich-vu /admin/ /api/v1/site/settings; do
  code="$(curl -s -o /dev/null -w '%{http_code}' "$SITE$path")"
  printf '  %-24s %s\n' "$path" "$code"
  [[ "$code" == "200" ]] || fail "$path trả về $code"
done
step "Xong — $(git -C "$BE" log --oneline -1) | $(git -C "$FE" log --oneline -1)"
