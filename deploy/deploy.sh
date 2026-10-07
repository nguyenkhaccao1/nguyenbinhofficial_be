#!/usr/bin/env bash
# Build va chay lai cac container Nguyen Binh Official. Khong anh huong container cua site khac.
# Dung tren server:  cd ~/apps/nguyenbinhofficial_be && deploy/deploy.sh [--pull]
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
BE_ROOT="$(cd "$HERE/.." && pwd)"
FE_ROOT="$(cd "$BE_ROOT/../nguyenbinhofficial_fe" && pwd)"

if [[ "${1:-}" == "--pull" ]]; then
  git -C "$BE_ROOT" pull --ff-only
  git -C "$FE_ROOT" pull --ff-only
fi

[[ -f "$HERE/.env" ]] || { echo "Thiếu deploy/.env (xem deploy/.env.example)" >&2; exit 1; }
docker network inspect aciplatform_aciplatform-network >/dev/null 2>&1 \
  || { echo "Không thấy mạng aciplatform_aciplatform-network (nginx dùng chung)" >&2; exit 1; }

cd "$HERE"
docker compose build
docker compose up -d --remove-orphans

echo "Chờ API sẵn sàng..."
for i in $(seq 1 60); do
  status="$(docker inspect -f '{{.State.Health.Status}}' nguyenbinh-api 2>/dev/null || echo starting)"
  [[ "$status" == "healthy" ]] && break
  sleep 3
done
docker compose ps
[[ "$status" == "healthy" ]] || { echo "API chưa healthy — xem: docker logs nguyenbinh-api" >&2; exit 1; }

# Kiem tra tu ben trong mang dung chung (giong duong di cua nginx).
docker run --rm --network aciplatform_aciplatform-network curlimages/curl:8.10.1 -fsS -o /dev/null -w "web: %{http_code}\n" http://nguyenbinh-web:3000/
docker run --rm --network aciplatform_aciplatform-network curlimages/curl:8.10.1 -fsS -o /dev/null -w "admin: %{http_code}\n" http://nguyenbinh-admin/admin/
docker run --rm --network aciplatform_aciplatform-network curlimages/curl:8.10.1 -fsS -w "\napi ready: %{http_code}\n" http://nguyenbinh-api:8080/health/ready
echo "Deploy OK"
