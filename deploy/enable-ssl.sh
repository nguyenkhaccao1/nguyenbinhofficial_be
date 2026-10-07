#!/usr/bin/env bash
# Cap/mo rong chung chi Let's Encrypt cho nguyenbinhofficial.com.vn (+ www neu DNS da tro ve server), roi bat HTTPS.
# Chay lai sau khi them ban ghi DNS "www" → chung chi duoc cap lai kem www.
# Gia han tu dong: timer "comthino-certbot-renew" tren server chay "certbot renew" cho TOAN BO chung chi
# trong AciPlatform/data/certbot/conf → chung chi nay cung duoc gia han.
set -euo pipefail

ACI_ROOT="${ACI_ROOT:-/root/apps/AciPlatform}"
SERVER_IP="${SERVER_IP:-103.200.22.167}"
HERE="$(cd "$(dirname "$0")" && pwd)"

domains=()
for host in nguyenbinhofficial.com.vn www.nguyenbinhofficial.com.vn; do
  ip="$(getent ahostsv4 "$host" | awk 'NR==1{print $1}')"
  if [[ "$ip" == "$SERVER_IP" ]]; then
    domains+=("-d" "$host"); echo "✔ $host → $ip"
  else
    echo "✘ $host → ${ip:-<chưa có bản ghi DNS>} (bỏ qua)"
  fi
done
[[ ${#domains[@]} -gt 0 && "${domains[1]}" == "nguyenbinhofficial.com.vn" ]] \
  || { echo "nguyenbinhofficial.com.vn chưa trỏ về $SERVER_IP" >&2; exit 1; }

# Co CERTBOT_EMAIL → nhan email canh bao het han; khong co → dang ky khong email.
if [[ -n "${CERTBOT_EMAIL:-}" ]]; then account=(--email "$CERTBOT_EMAIL" --no-eff-email); else account=(--register-unsafely-without-email); fi

docker run --rm --name certbot-nguyenbinhofficial \
  -v "$ACI_ROOT/data/certbot/conf:/etc/letsencrypt" \
  -v "$ACI_ROOT/data/certbot/www:/var/www/certbot" \
  certbot/certbot certonly --webroot -w /var/www/certbot --cert-name nguyenbinhofficial.com.vn \
  "${domains[@]}" "${account[@]}" --agree-tos --non-interactive --expand --keep-until-expiring

"$HERE/install-nginx.sh" https
echo "HTTPS enabled: https://nguyenbinhofficial.com.vn"
