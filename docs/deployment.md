# Triển khai — nguyenbinhofficial.com.vn

## 1. Kiến trúc trên server `103.200.22.167`

Server dùng chung với AciPlatform và Cơm Thị Nở.

```text
Internet ──80/443──► aciplatform-nginx (nginx dùng chung, repo AciPlatform)
                        │  mạng docker: aciplatform_aciplatform-network
                        ├─► nguyenbinh-web   :3000  website SSR
                        ├─► nguyenbinh-admin :80    /admin (SPA tĩnh)
                        └─► nguyenbinh-api   :8080  /api, /media, /health
                                 ├─► nguyenbinh-redis (mạng nội bộ)
                                 ├─► SQL Server trên host (host.docker.internal:1433), DB NguyenBinhOfficial, login nb_app
                                 └─► ImageKit (ảnh: nguyenbinhofficial/<thư mục>/<tên-file>-<id>.<ext>)
```

| Thư mục trên server | Nội dung |
|---|---|
| `~/apps/nguyenbinhofficial_be` | Repo backend; `deploy/` chứa compose, script, `.env` (chmod 600) |
| `~/apps/nguyenbinhofficial_fe` | Repo frontend (build web + admin) |
| `~/apps/AciPlatform/nginx.conf` | Nginx dùng chung — block nằm giữa `# --- NGUYENBINHOFFICIAL` … `END ---` |
| `~/apps/AciPlatform/data/certbot/conf` | Chứng chỉ Let's Encrypt (dùng chung) |

Site Nguyên Bình **không mở cổng nào** ra ngoài. Nginx dùng chung phân giải tên container lúc có request (`resolver 127.0.0.11`), nên khi container Nguyên Bình dừng, các site khác vẫn hoạt động bình thường.

## 2. Tên miền (Vietnix)

Trong trang quản lý DNS của `nguyenbinhofficial.com.vn` tại Vietnix:

| Loại | Tên (Host) | Giá trị | TTL |
|---|---|---|---|
| A | `@` | `103.200.22.167` | 300 |
| A | `www` | `103.200.22.167` | 300 |

Xoá các bản ghi A/AAAA/CNAME cũ trùng tên `@` và `www` (nếu có, ví dụ bản ghi trỏ về trang đỗ tên miền). Không thêm bản ghi AAAA vì server chưa cấu hình IPv6 cho site này.

Kiểm tra: `nslookup nguyenbinhofficial.com.vn 8.8.8.8` trả về `103.200.22.167` (thường 5–30 phút).

## 3. SSL (Let's Encrypt)

Sau khi DNS đã trỏ về server:

```bash
cd ~/apps/nguyenbinhofficial_be
CERTBOT_EMAIL=<email-nhận-cảnh-báo> deploy/enable-ssl.sh
```

Script cấp chứng chỉ cho `nguyenbinhofficial.com.vn` + `www`, rồi chuyển nginx sang HTTPS (HTTP → 301 HTTPS, `www` → không `www`, HSTS). Gia hạn tự động: timer `comthino-certbot-renew` đang chạy `certbot renew` cho toàn bộ chứng chỉ trong thư mục dùng chung.

> Trước khi có SSL, **không đăng nhập admin** qua HTTP (mật khẩu đi dạng rõ; cookie phiên chỉ gửi qua HTTPS nên phiên cũng không duy trì được).

## 4. Cập nhật phiên bản

Từ máy dev (khuyên dùng): `deploy/release.sh` — test, push GitHub, SSH vào server pull + build. Hướng dẫn đầy đủ ở README, mục *Cập nhật & triển khai*.

Trên server:

```bash
cd ~/apps/nguyenbinhofficial_be
deploy/deploy.sh --pull     # git pull cả 2 repo, build, chạy lại, kiểm tra health
```

Script dừng với lỗi nếu API không `healthy`. Migration database tự chạy khi API khởi động (`Database__MigrateOnStartup=true`).

**Rollback**: `git -C ~/apps/nguyenbinhofficial_be checkout <commit-trước>` (và repo fe nếu cần) → `deploy/deploy.sh`. Migration đã chạy không tự lùi — trước khi deploy thay đổi schema lớn, sao lưu database (mục 6).

## 5. Bí mật (`deploy/.env`)

Mẫu: [deploy/.env.example](../deploy/.env.example). File thật chỉ có trên server, quyền `600`, không commit. Gồm: chuỗi kết nối SQL (`nb_app`), `Jwt__Secret`, khoá ImageKit.

Đổi bí mật: sửa `.env` → `cd deploy && docker compose up -d api`.

## 6. Sao lưu

- **Database**: `NguyenBinhOfficial` trên SQL Server của host — đưa vào lịch sao lưu chung của server (full hằng ngày, giữ ≥ 30 ngày). Lệnh tay:
  ```sql
  BACKUP DATABASE [NguyenBinhOfficial] TO DISK = N'/var/opt/mssql/backup/NguyenBinhOfficial_YYYYMMDD.bak' WITH COMPRESSION, INIT;
  ```
- **Ảnh**: lưu trên ImageKit (đã có bản gốc trên CDN). Volume `nguyenbinhofficial_api-storage` chỉ chứa khoá Data Protection và file private (đính kèm lead — Phase 5) → sao lưu cùng database.

## 7. Kiểm tra nhanh

```bash
docker compose -f ~/apps/nguyenbinhofficial_be/deploy/docker-compose.yml ps
curl -s -H "Host: nguyenbinhofficial.com.vn" http://127.0.0.1/health/ready
docker logs --tail 100 nguyenbinh-api
```
