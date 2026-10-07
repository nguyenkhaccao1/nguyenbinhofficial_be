# 03 — Backend architecture

## 1. Solution

```text
NguyenBinhOfficial.sln
Directory.Build.props          net9.0, Nullable, TreatWarningsAsErrors (Release)
Directory.Packages.props       Central Package Management
src/
├── NguyenBinh.Shared          Không phụ thuộc gì: ApiResponse, PagedResult, Error codes, Permission constants, Guard, Slug helper
├── NguyenBinh.Domain          Entity, enum, value object, domain rule (không phụ thuộc EF/ASP.NET)
├── NguyenBinh.Application     Use-case theo module: DTO, validator (FluentValidation), service interface + impl, mapping, abstraction (IAppDbContext, ICurrentUser, IFileStorage, IClock, ICacheService)
├── NguyenBinh.Infrastructure  EF Core DbContext + configuration + migrations + interceptors, Identity, JWT, Storage, Image processing (NetVips), Redis cache, Email, Background jobs, Seed
└── NguyenBinh.Api             Controllers, middleware, auth policies, rate limiting, Swagger, health checks, output cache, composition root
tests/
├── NguyenBinh.UnitTests          Domain + Application (validators, slug, permission matrix, publish rule…)
└── NguyenBinh.IntegrationTests   WebApplicationFactory + SQL Server (LocalDB / Testcontainers): Login, CRUD, Publish, Upload, Lead forms, Redirect, SEO route
```

Phụ thuộc: `Api → Infrastructure → Application → Domain → Shared` (Api cũng tham chiếu Application). Domain và Application **không** tham chiếu EF Core SqlServer, ASP.NET.

> Ghi chú thực dụng: Application dùng `IAppDbContext` (expose `DbSet<>`), không thêm lớp Repository bọc EF — EF Core đã là Unit of Work + Repository; thêm 1 lớp nữa chỉ làm code dài hơn mà không tăng khả năng test (integration test chạy trên SQL thật).

## 2. Modular monolith — tổ chức theo module

Application chia thư mục theo module, mỗi module độc lập về DTO/service/validator:

```text
Application/
├── Common/          Abstractions, Behaviors, Paging, Querying (filter/sort), Errors, Content/ (CRUD generic)
├── Identity/        Auth, Users, Roles, Permissions
├── Settings/        Site settings (typed groups)
├── Media/
├── Pages/           Page builder + Block registry
├── Navigation/      Menus
├── Projects/        Projects, Categories, Industries, Technologies, Clients
├── Products/
├── Services/
├── Blog/
├── Library/         Testimonials, Partners, Team, Faqs, Ctas
├── Leads/           Leads, Quote, Demo, Contact, Newsletter
├── Seo/             Metadata, Redirects, Sitemap, Robots, BrokenLinks
├── Search/
├── Dashboard/
└── System/          AuditLogs, SystemLogs, ContentVersions
```

Module chỉ giao tiếp qua interface public (vd `IMediaUsageTracker`, `ISeoService`, `IContentVersionService`), không truy cập trực tiếp entity "nội bộ" của module khác ngoài navigation property cần thiết.

## 3. CRUD nội dung dùng chung

Các yêu cầu mục 21 (Create/Read/Update/Delete/Soft delete/Restore/Publish/Unpublish/Draft/Schedule/Duplicate/Preview/Sort/Filter/Search/Pagination/Bulk) giống nhau cho mọi module → hiện thực **một lần**:

```text
IContentAdminService<TListItem, TDetail, TUpsert>
  ListAsync(ContentQuery)            filter/search/sort/page, includeDeleted, status
  GetAsync(id)
  CreateAsync(TUpsert)               validate → map → SaveChanges → version snapshot → media usage → audit
  UpdateAsync(id, TUpsert, rowVersion)
  DeleteAsync(id)                    soft; cảnh báo nếu bị tham chiếu
  RestoreAsync(id)
  PublishAsync(id) / UnpublishAsync(id) / ScheduleAsync(id, publishAt)
  DuplicateAsync(id)                 slug "-copy", Status=DRAFT
  ReorderAsync([{id, sortOrder}])
  BulkAsync(action, ids)             publish/unpublish/delete/restore
  CreatePreviewTokenAsync(id)
```

`ContentAdminServiceBase<TEntity,…>` chứa logic chung; module chỉ override mapping, include, search fields, validation đặc thù. `ContentAdminController<…>` gắn route + permission prefix (`project`, `product`…).

## 4. Pipeline request

```text
Request
 → ForwardedHeaders → CorrelationId → Serilog request logging
 → GlobalExceptionMiddleware (ProblemDetails → ApiResponse, không lộ stack trace ở non-Development)
 → ResponseCompression (Brotli/Gzip) → CORS (whitelist từ config)
 → RateLimiter (policy: "public-forms" 5/phút/IP, "auth" 10/phút/IP, "public-api" 300/phút/IP, "admin" 600/phút/user)
 → Authentication (JWT Bearer) → Authorization (PermissionPolicyProvider)
 → OutputCache (public GET, tag theo module; vô hiệu khi admin ghi)
 → Controller → FluentValidation (filter tự động → 422) → Application service
```

## 5. Chuẩn response & lỗi

```json
{ "success": true, "data": {}, "message": null, "errors": null }
```

Phân trang: `data = { items, page, pageSize, totalItems, totalPages }`.

| Status | Khi nào |
|--------|---------|
| 400 | Request sai định dạng (JSON hỏng, tham số sai kiểu) |
| 401 | Thiếu/sai/hết hạn token |
| 403 | Đủ xác thực nhưng thiếu permission |
| 404 | Không tìm thấy hoặc chưa publish (public) |
| 409 | Trùng slug/unique, xung đột `RowVersion`, xoá thứ đang được dùng mà không `force` |
| 422 | Validation (FluentValidation) — `errors: { field: [messages] }` |
| 429 | Vượt rate limit (kèm `Retry-After`) |
| 500 | Lỗi không lường trước — chỉ trả `traceId` |

Application ném exception có kiểu (`NotFoundException`, `ConflictException`, `ValidationException`, `ForbiddenException`) → middleware chuyển thành status tương ứng.

## 6. Auth & bảo mật

- **Identity**: `IdentityUser<Guid>` → bảng `Users`; password policy ≥ 10 ký tự; lockout 5 lần/15 phút; hash PBKDF2 (mặc định Identity v3, 100k+ iterations).
- **Access token**: JWT HS256 (khoá ≥ 256 bit từ secret store), 15 phút, claim `sub, name, email, role[], sstamp, jti`. Không nhét toàn bộ permission vào token → thu hồi quyền có hiệu lực ngay.
- **Refresh token**: 64 byte random, chỉ lưu SHA-256; httpOnly + Secure + SameSite=Strict cookie, path `/api/v1/admin/auth`; xoay vòng mỗi lần refresh; dùng lại token đã bị thay → thu hồi toàn bộ family + ghi audit `TOKEN_REUSE`. Hạn 7 ngày (30 ngày nếu "ghi nhớ").
- **CSRF**: endpoint refresh/logout dùng cookie → yêu cầu header `X-Requested-With: nb-admin` + SameSite=Strict + kiểm tra `Origin`. API khác dùng Bearer header → không bị CSRF.
- **Permission**: `[HasPermission(Permissions.Projects.Create)]` → `PermissionPolicyProvider` tạo policy động → `PermissionHandler` lấy permission của các role trong token từ cache (`perm:role:{id}`), SuperAdmin bypass. Kiểm tra `sstamp` khi refresh để vô hiệu phiên sau đổi mật khẩu/khoá user.
- **Input**: FluentValidation mọi request; EF parameterized (không SQL nối chuỗi); HTML rich text sanitize bằng `HtmlSanitizer` (whitelist tag); block `CUSTOM_HTML` chỉ role có `page.custom_html`.
- **Upload**: whitelist extension + MIME + kiểm tra magic bytes; giới hạn size theo loại (ảnh 15MB, video 200MB, tài liệu 25MB, đính kèm lead 10MB); chặn `.exe .dll .bat .cmd .sh .js .msi .ps1 .svg`(SVG chỉ cho phép sau khi sanitize); đổi tên file ngẫu nhiên; lưu ngoài web root cho file private; hook `IMalwareScanner` (mặc định no-op, có thể gắn ClamAV).
- **Header**: HSTS, `X-Content-Type-Options`, `Referrer-Policy`, CSP cho API.
- **Secret**: chỉ từ biến môi trường / user-secrets / secret store. `appsettings.json` không chứa secret thật.

## 7. Audit & version

- `AppDbContext.SaveChanges` (override): set Created*/Updated*/Deleted*, đổi `Remove()` thành soft delete (giữ entity owned/JSON ở trạng thái Unchanged).
- `AuditTrail`: duyệt ChangeTracker, ghi `AuditLogs` trong cùng transaction (old/new JSON chỉ cột thay đổi; bỏ qua cột nhạy cảm như PasswordHash, SecurityStamp).
- `IContentVersionService.SnapshotAsync(entityType, id)` sau mỗi Create/Update/Publish; autosave ghi version `IsAutosave=1` (giữ 20 bản autosave gần nhất/entity).

## 8. Cache & hiệu năng

- `ICacheService` trên `IDistributedCache` (Memory dev, Redis prod) cho settings, menu, permission, redirect map.
- OutputCache cho GET public, tag `projects`, `products`, `pages`, `settings`… → admin ghi gọi `EvictByTagAsync`.
- Truy vấn public: `AsNoTracking`, projection thẳng ra DTO (`Select`), `AsSplitQuery` khi include nhiều collection.

## 9. Background jobs

`IHostedService` đơn giản (không cần Hangfire ở phase đầu):
- `ScheduledPublishJob` (1 phút): chuyển SCHEDULED → PUBLISHED khi tới giờ, xoá output cache, ghi audit.
- `MediaVariantJob`: tạo biến thể ảnh nền (channel queue) để upload trả về nhanh.
- `RefreshTokenCleanupJob` (hằng ngày), `AutosavePruneJob`.

## 10. Observability

Serilog → Console (JSON ở prod) + File rolling + `SystemLogs` (Warning+). CorrelationId xuyên suốt (header `X-Correlation-Id`). Health checks: `/health/live` (process), `/health/ready` (SQL, Redis, storage).

## 11. Cấu hình môi trường

`appsettings.json` → `appsettings.{Development|SIT|UAT|Production}.json` → biến môi trường (`ConnectionStrings__Default`, `Jwt__Secret`, `Redis__ConnectionString`, `Storage__Provider`, `Seed__AdminEmail`…). File mẫu: `.env.example`.
