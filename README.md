# Nguyên Bình Official — Backend API

API cho website **nguyenbinhofficial.com.vn** và Admin CMS. ASP.NET Core (.NET 9), Clean Architecture / Modular Monolith, SQL Server.

Tài liệu thiết kế (sitemap, ERD, API, wireframe, design system, SEO, permission, kế hoạch): [docs/design/](docs/design/00-index.md).

## System Requirements

| Thành phần | Phiên bản |
|-----------|-----------|
| .NET SDK | 9.0.x (`global.json` cho phép roll-forward lên SDK mới hơn) |
| SQL Server | 2019+ (dev: LocalDB hoặc SQL Server từ xa) |
| Redis | 7+ (tuỳ chọn — không cấu hình thì dùng memory cache) |
| dotnet-ef | `dotnet tool install -g dotnet-ef` |

> .NET 9 hết hỗ trợ tháng 11/2026. Trước khi go-live nên nâng lên .NET 10 LTS: cài SDK 10, đổi `TargetFramework` trong `Directory.Build.props` và version package `9.0.*` → `10.0.*` trong `Directory.Packages.props`.

## Architecture

```text
src/
├── NguyenBinh.Shared          ApiResponse, PagedResult, Permissions, SystemRoles, Slug
├── NguyenBinh.Domain          Entity, enum (không phụ thuộc EF/ASP.NET)
├── NguyenBinh.Application     Use-case theo module: Identity, Settings, Media, Platform (audit)
├── NguyenBinh.Infrastructure  EF Core + migrations, Identity, JWT, cache, storage, xử lý ảnh (libvips)
└── NguyenBinh.Api             Controllers, middleware, permission policy, rate limit, Swagger, health
tests/
├── NguyenBinh.UnitTests        Slug, ma trận quyền, upload policy, validator, publish rule
└── NguyenBinh.IntegrationTests API thật trên LocalDB tạm: auth, phân quyền, settings, media, audit
```

Chi tiết: [docs/design/03-backend-architecture.md](docs/design/03-backend-architecture.md).

## Local Setup

```bash
git clone <repo> nguyenbinhofficial_be && cd nguyenbinhofficial_be
dotnet restore
```

### Environment Variables / Secrets

Không bao giờ ghi secret vào `appsettings*.json`. Ở máy dev dùng **user-secrets**:

```bash
cd src/NguyenBinh.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=...;Database=NguyenBinhOfficial;User Id=...;Password=...;TrustServerCertificate=True;Encrypt=True;Connect Timeout=30"
dotnet user-secrets set "Jwt:Secret" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"
dotnet user-secrets set "Seed:AdminEmail" "admin@nguyenbinhofficial.com.vn"
dotnet user-secrets set "Seed:AdminPassword" "<mật khẩu ≥ 10 ký tự, có chữ thường và số>"
```

Ở server/Docker dùng biến môi trường (dấu `__` thay cho `:`) — xem [.env.example](.env.example).

| Biến | Bắt buộc | Mô tả |
|------|:-------:|-------|
| `ConnectionStrings__Default` | ✔ | SQL Server |
| `Jwt__Secret` | ✔ | Khoá ký JWT, ≥ 32 byte |
| `Seed__AdminEmail`, `Seed__AdminPassword` | lần đầu | Tạo SuperAdmin đầu tiên khi chưa có |
| `Redis__ConnectionString` | | Bật Redis cache |
| `Cors__AllowedOrigins__0..n` | ✔ prod | Origin được gọi API kèm cookie |
| `Storage__RootPath`, `Storage__PublicBaseUrl` | | Thư mục lưu file, origin/CDN phục vụ `/media` |
| `Database__MigrateOnStartup` | | `true` ở Development; production chạy migration có kiểm soát |
| `ForwardedHeaders__TrustAllProxies` | | `true` khi API chỉ lộ sau nginx trong mạng nội bộ |
| `Swagger__Enabled` | | `true` ở Development/SIT |

## Database Setup

1. Tạo database rỗng (Serilog cần DB tồn tại để tạo bảng `SystemLogs`):
   ```sql
   CREATE DATABASE [NguyenBinhOfficial] COLLATE Vietnamese_CI_AS;
   ```
2. Ở Development, API tự chạy migration + seed khi khởi động (`Database:MigrateOnStartup=true`).

Seed (idempotent, chạy mỗi lần khởi động): đồng bộ bảng `Permissions` từ code, tạo 6 role hệ thống với ma trận quyền mặc định (chỉ khi role chưa tồn tại — không ghi đè chỉnh sửa của admin), SuperAdmin đầu tiên, cấu hình website mặc định.

## Run API

```bash
cd src/NguyenBinh.Api
dotnet run            # http://localhost:5080, Swagger: http://localhost:5080/swagger
```

> Dùng profile `http` (mặc định) hoặc `ASPNETCORE_ENVIRONMENT=Development`. Với .NET 9 SDK, `dotnet run --environment X` **không** đặt môi trường ứng dụng.

Health: `GET /health/live`, `GET /health/ready` (kiểm tra SQL).

## Run Frontend / Admin

Xem repo `nguyenbinhofficial_fe` (Vite dev server proxy `/api` và `/media` về `http://localhost:5080`).

## Migration

```bash
# Tạo migration mới
dotnet ef migrations add <Ten> -p src/NguyenBinh.Infrastructure -s src/NguyenBinh.Api -o Persistence/Migrations

# Xuất script SQL idempotent để review/chạy ở production
dotnet ef migrations script --idempotent -p src/NguyenBinh.Infrastructure -s src/NguyenBinh.Api -o migrate.sql

# Rollback về migration trước
dotnet ef database update <MigrationTruoc> -p src/NguyenBinh.Infrastructure -s src/NguyenBinh.Api --connection "<conn>"
```

Production: luôn backup trước khi chạy script migration; mỗi migration phải có `Down()` dùng được.

## Build

```bash
dotnet build -c Release    # Release bật TreatWarningsAsErrors
```

## Testing

```bash
dotnet test
```

Integration test khởi động API thật trên một database LocalDB tạm (`NguyenBinhOfficial_Test_*`), tự xoá sau khi chạy — không chạm vào database dev/production. Cần SQL Server LocalDB (có sẵn với Visual Studio / SQL Server Express).

## API conventions

- Base: `/api/v1` (public), `/api/v1/admin` (JWT + permission).
- Response: `{ "success", "data", "message", "errors" }`; danh sách: `data = { items, page, pageSize, totalItems, totalPages }`.
- Enum trả về dạng `UPPER_SNAKE` (`DRAFT`, `IMAGE`, …).
- Status: 400 sai định dạng · 401 chưa đăng nhập · 403 thiếu quyền · 404 · 409 trùng/xung đột/đang được dùng · 422 validation (`errors` theo field camelCase) · 429 rate limit · 500 (chỉ trả mã tham chiếu).
- Auth admin: `POST /admin/auth/login` → access token (15 phút, giữ trong memory) + refresh token trong cookie httpOnly `nb_rt` (SameSite=Strict, path `/api/v1/admin/auth`). `refresh`/`logout` yêu cầu header `X-Requested-With: nb-admin`. Refresh token xoay vòng; dùng lại token cũ → thu hồi cả phiên.

Danh sách endpoint đầy đủ: [docs/design/06-api.md](docs/design/06-api.md) và Swagger.

## Deploy

Phase 6 sẽ bổ sung Dockerfile, docker-compose (api, web, admin, sqlserver, redis, nginx) và pipeline CI/CD. Yêu cầu tối thiểu khi deploy thủ công:

1. `dotnet publish src/NguyenBinh.Api -c Release -o out`
2. Đặt biến môi trường như bảng trên (`ASPNETCORE_ENVIRONMENT=Production`).
3. Chạy migration script, khởi động API sau nginx (HTTPS, `client_max_body_size 220m` cho upload video).
4. Thư mục `Storage__RootPath` phải nằm trên volume bền vững và được backup cùng database.

## Troubleshooting

| Lỗi | Nguyên nhân / Cách xử lý |
|-----|---------------------------|
| `Thiếu ConnectionStrings:Default` | Chưa set user-secrets, hoặc chạy app không ở môi trường Development (user-secrets chỉ nạp ở Development). |
| `Cannot open database ... The login failed` | Database chưa được tạo — xem Database Setup bước 1. |
| `Connection Timeout Expired ... pre-login handshake` | Mạng/tường lửa tới SQL Server chậm; thêm `Connect Timeout=30`, kiểm tra port 1433. |
| `Jwt:Secret phải dài tối thiểu 32 byte` | Đặt `Jwt__Secret`. |
| Upload bị từ chối "Nội dung file không khớp" | File bị đổi đuôi; hệ thống kiểm tra magic bytes, không tin phần mở rộng. |
| 429 khi đăng nhập | Rate limit 10 lần/phút/IP cho `/admin/auth`. |
