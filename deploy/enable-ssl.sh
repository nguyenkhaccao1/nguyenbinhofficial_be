#!/usr/bin/env bash
# Cap chung chi Let's Encrypt cho nguyenbinhofficial.com.vn (+ www) roi bat HTTPS.
# Dieu kien: ban ghi DNS A cua ca 2 ten mien da tro ve IP server va block HTTP da duoc cai (install-nginx.sh http).
# Gia han tu dong: timer "comthino-certbot-renew" tren server chay "certbot renew" cho TOAN BO chung chi
# trong AciPlatform/data/certbot/conf → chung chi nay cung duoc gia han.
set -euo pipefail

ACI_ROOT="${ACI_ROOT:-/root/apps/AciPlatform}"
EMAIL="${CERTBOT_EMAIL:?Đặt CERTBOT_EMAIL=email nhận cảnh báo hết hạn chứng chỉ}"
HERE="$(cd "$(dirname "$0")" && pwd)"

for host in nguyenbinhofficial.com.vn www.nguyenbinhofficial.com.vn; do
  ip="$(getent ahostsv4 "$host" | awk 'NR==1{print $1}')"
  echo "$host → ${ip:-<chưa có bản ghi DNS>}"
done

docker run --rm --name certbot-nguyenbinhofficial \
  -v "$ACI_ROOT/data/certbot/conf:/etc/letsencrypt" \
  -v "$ACI_ROOT/data/certbot/www:/var/www/certbot" \
  certbot/certbot certonly --webroot -w /var/www/certbot \
  -d nguyenbinhofficial.com.vn -d www.nguyenbinhofficial.com.vn \
  --email "$EMAIL" --agree-tos --no-eff-email --non-interactive --keep-until-expiring

"$HERE/install-nginx.sh" https
echo "HTTPS enabled: https://nguyenbinhofficial.com.vn"
