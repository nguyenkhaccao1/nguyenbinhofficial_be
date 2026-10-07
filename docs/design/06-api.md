# 06 — Danh sách API (`/api/v1`)

Mọi response bọc trong `ApiResponse<T>`. Danh sách trả `PagedResult<T>`.
Tham số danh sách chung: `q, page (1), pageSize (20, max 100), sort (vd "-updatedAt,name"), status, includeDeleted (admin)`.

## 1. Public (anonymous, rate limit "public-api", output cache)

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/site/settings` | Brand, theme màu, contact, social, tracking IDs (chỉ nhóm `IsPublic`) |
| GET | `/site/navigation` | Menu header (kèm dữ liệu megamenu sản phẩm/giải pháp/dịch vụ) + footer |
| GET | `/pages/home` | Trang chủ: sections + blocks, block động đã được resolve dữ liệu |
| GET | `/pages/by-path?path=` | Page theo path (STANDARD/LANDING/SOLUTION/LEGAL) |
| GET | `/projects?contentType=&industry=&technology=&role=&category=&featured=&page=` | Dự án đã publish (đã lọc theo cờ `CanShow*`) |
| GET | `/projects/{slug}` | Case study đầy đủ |
| GET | `/projects/{slug}/related` | Dự án liên quan (cùng ngành/loại/công nghệ) |
| GET | `/products` · `/products/{slug}` | Sản phẩm + features/modules/plans/faqs/media |
| GET | `/services` · `/services/{slug}` · `/service-categories` | Dịch vụ |
| GET | `/industries` · `/technologies?group=` · `/project-categories` | Danh mục |
| GET | `/blog/posts?category=&tag=&author=&page=` · `/blog/posts/{slug}` | Blog |
| GET | `/blog/resolve/{slug}` | `{ kind: "category" \| "post", data }` cho `/blog/{slug}` |
| GET | `/blog/categories` · `/blog/tags/{slug}` · `/blog/authors/{slug}` | |
| GET | `/testimonials?productId=&projectId=` · `/partners` · `/team-members` · `/faqs?scope=&scopeId=` | |
| GET | `/search?q=&types=project,product,service,post&page=` | Tìm kiếm toàn site |
| GET | `/seo/meta?path=` | SEO cho route tĩnh (vd `/du-an`) |
| GET | `/seo/redirects` | Bản đồ redirect đang active (web server cache) |
| POST | `/seo/redirects/hit` · `/seo/broken-links` | Web báo hit redirect / 404 (khoá bằng header `X-Internal-Key`) |
| GET | `/seo/sitemap` · `/seo/sitemap/{section}` | Dữ liệu sitemap (pages/products/services/projects/posts) |
| GET | `/seo/robots` | Nội dung robots.txt theo môi trường + settings |
| GET | `/preview/{token}` | Bản nháp cho preview (token ngắn hạn) |
| POST | `/leads/contact` · `/leads/quote` · `/leads/demo` | Gửi form (rate limit "public-forms", honeypot, captcha tuỳ chọn, UTM/landing/referrer) |
| POST | `/leads/attachments` | Upload đính kèm (≤10MB, PDF/DOC/DOCX/XLS/XLSX/PNG/JPG/ZIP) → trả `attachmentToken` |
| POST | `/newsletter/subscribe` · GET `/newsletter/unsubscribe?token=` | |

## 2. Admin auth (`/admin/auth`)

| Method | Path | Mô tả |
|--------|------|-------|
| POST | `/admin/auth/login` | `{email, password, rememberMe}` → `{accessToken, expiresIn, user}` + set cookie refresh |
| POST | `/admin/auth/refresh` | Cookie → token mới (xoay vòng) |
| POST | `/admin/auth/logout` | Thu hồi refresh token hiện tại |
| GET | `/admin/auth/me` | User + roles + permissions |
| PUT | `/admin/auth/profile` · POST `/admin/auth/change-password` | |

## 3. Admin CRUD chuẩn (áp dụng cho mọi resource nội dung)

Với `{resource}` ∈ `pages, ctas, products, product-categories, projects, project-categories, industries, technologies, clients, services, service-categories, posts, post-categories, tags, authors, testimonials, partners, team-members, faqs`:

| Method | Path | Permission |
|--------|------|-----------|
| GET | `/admin/{resource}` | `{module}.view` |
| GET | `/admin/{resource}/{id}` | `{module}.view` |
| POST | `/admin/{resource}` | `{module}.create` |
| PUT | `/admin/{resource}/{id}` (body có `rowVersion`) | `{module}.update` |
| DELETE | `/admin/{resource}/{id}` (soft; `?force=true` khi đang được tham chiếu) | `{module}.delete` |
| POST | `/admin/{resource}/{id}/restore` | `{module}.delete` |
| DELETE | `/admin/{resource}/{id}/permanent` | `system.purge` |
| POST | `/admin/{resource}/{id}/publish` · `/unpublish` · `/schedule` `{publishAt}` | `{module}.publish` |
| POST | `/admin/{resource}/{id}/duplicate` | `{module}.create` |
| POST | `/admin/{resource}/{id}/preview-token` | `{module}.view` |
| PUT | `/admin/{resource}/{id}/autosave` | `{module}.update` |
| GET | `/admin/{resource}/{id}/versions` · POST `/versions/{versionId}/restore` | `{module}.view` / `.update` |
| PATCH | `/admin/{resource}/reorder` `[{id, sortOrder}]` | `{module}.update` |
| POST | `/admin/{resource}/bulk` `{action: publish\|unpublish\|delete\|restore, ids}` | theo action |

Endpoint đặc thù:

| Method | Path | Mô tả |
|--------|------|-------|
| PUT | `/admin/pages/{id}/tree` | Lưu toàn bộ sections + blocks (1 transaction, validate data theo block schema) |
| GET | `/admin/pages/block-types` | Danh sách block + JSON schema |
| GET/PUT | `/admin/menus` · `/admin/menus/{code}` | Menu + items dạng cây |
| GET/PUT | `/admin/settings` · `/admin/settings/{group}` | brand, theme, contact, social, tracking, seo-defaults, forms, header, footer |

## 4. Media (`/admin/media`, permission `media.*`)

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/admin/media?folderId=&kind=&q=&tag=&page=` | Danh sách |
| GET | `/admin/media/{id}` · `/admin/media/{id}/usages` | Chi tiết + nơi đang dùng |
| POST | `/admin/media/upload` (multipart, nhiều file, `folderId`) | Validate → lưu → sinh WebP/AVIF |
| PUT | `/admin/media/{id}` | Rename, alt, caption, title, tags, folder |
| POST | `/admin/media/{id}/crop` `{x,y,width,height}` | Tạo bản crop (file mới) |
| DELETE | `/admin/media/{id}` | 409 + danh sách usages nếu đang dùng; `?force=true` để xoá |
| POST | `/admin/media/bulk` | move/delete/tag |
| GET/POST/PUT/DELETE | `/admin/media/folders[/{id}]` | Cây thư mục |

## 5. Marketing (`lead.*`)

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/admin/leads?status=&source=&assignedTo=&from=&to=&utmSource=&q=` | Danh sách / Kanban |
| GET | `/admin/leads/{id}` | Chi tiết + activities + submissions + attachments |
| POST | `/admin/leads` | Tạo lead thủ công |
| PUT | `/admin/leads/{id}` | Sửa thông tin |
| PATCH | `/admin/leads/{id}/status` `{status, note, lostReason}` | Ghi activity STATUS_CHANGE |
| PATCH | `/admin/leads/{id}/assign` `{userId}` | |
| POST | `/admin/leads/{id}/activities` | Note/call/email/meeting |
| GET | `/admin/leads/{id}/attachments/{mediaId}` | Tải file private |
| GET | `/admin/leads/export?…` | CSV |
| GET/PATCH | `/admin/quote-requests` · `/admin/demo-requests` · `/admin/contact-messages` | Danh sách + đổi trạng thái |
| GET/DELETE | `/admin/newsletters` · GET `/admin/newsletters/export` | |

## 6. SEO (`seo.*`)

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/admin/seo/metadata?entityType=&missing=title\|description` | Tổng hợp SEO toàn site, lọc thiếu meta |
| GET/PUT | `/admin/seo/metadata/{entityType}/{entityId}` · `/admin/seo/routes/{path}` | |
| CRUD | `/admin/seo/redirects` (+ `/import` CSV, `/test?path=`) | Phát hiện vòng lặp/chuỗi redirect |
| GET | `/admin/seo/sitemap/preview` · POST `/admin/seo/sitemap/ping` | |
| GET/PUT | `/admin/seo/robots` | |
| GET/PATCH | `/admin/seo/broken-links` (`/resolve` → tạo redirect) | |

## 7. Hệ thống

| Method | Path | Permission |
|--------|------|-----------|
| CRUD | `/admin/users` (+ `/{id}/lock`, `/unlock`, `/reset-password`) | `user.*` |
| CRUD | `/admin/roles` · PUT `/admin/roles/{id}/permissions` | `role.*` |
| GET | `/admin/permissions` | `role.view` |
| GET | `/admin/audit-logs?entityType=&entityId=&userId=&action=&from=&to=` | `audit.view` |
| GET | `/admin/system-logs?level=&from=&to=&q=` | `system.view` |
| GET | `/admin/dashboard` | `dashboard.view` |
| GET | `/admin/search?q=` | Global search admin (theo quyền) |

## 8. Health & docs

`GET /health/live`, `GET /health/ready`, `/swagger` (Development, SIT).
