# 11 — Kế hoạch triển khai theo phase

Định nghĩa Done (mục 73): FE + API + DB + Validation + Permission + Responsive + Error/Loading/Empty state + Test. Không đánh dấu Done khi chỉ có UI.

## Phase 0 — Thiết kế ✅

Bộ tài liệu này.

## Phase 1 — Foundation ✅

| Hạng mục | Backend | Frontend |
|----------|---------|----------|
| Kiến trúc | Solution 5 project + 2 test project, CPM, Serilog, global exception, ApiResponse, rate limiting, health checks, Swagger, CORS, compression | npm workspaces: `apps/web` (RR v7 SSR shell), `apps/admin` (Vite SPA), `packages/shared` |
| Database | DbContext, base entity, soft delete + audit interceptors, migration đầu tiên (Identity, Permissions, RefreshTokens, SiteSettings, Media, AuditLogs, SystemLogs, ContentVersions, ContentTranslations) | |
| Auth | Login/refresh (rotation + reuse detection)/logout/me, lockout | Login page, session store, refresh single-flight, RequireAuth |
| RBAC | Permission constants, seed role matrix, policy provider động, Users/Roles CRUD API | Users/Roles screens + permission matrix |
| Media | Upload đa file, validate (ext/MIME/magic bytes/size), local storage, WebP/AVIF variants, folders, usages, rename/alt/caption, crop (API) | Media library (folder tree, grid, drag-drop upload, detail, bulk move/delete, picker). UI cắt ảnh: Phase 2 |
| Settings | Typed setting groups (brand/theme/contact/social/tracking/seo-defaults), public endpoint | Website settings screens |
| Audit | Audit log interceptor + API | Audit log screen |
| Test | Unit: permission matrix, validators, slug. Integration: login/refresh/reuse, 401/403, upload, settings | Vitest component cho login/DataTable |

## Phase 2 — CMS

Generic content CRUD (draft/publish/schedule/duplicate/preview/sort/bulk/versions/autosave) → Projects (+taxonomies, media, features, metrics, links, ownership/credit), Products (+features/modules/plans/faqs), Services, Blog (posts/categories/tags/authors, rich text sanitize), Library (testimonials/partners/team/faqs), Pages + Page builder (25 block types, schema validate), Menus/Header/Footer. Seed 9 dự án + POS Nguyên Bình + Home page.

## Phase 3 — Public website

Layout (header megamenu, footer động, mobile nav), Homepage (13 section qua block renderer), `/du-an` + case study, `/san-pham` + product landing, `/dich-vu`, `/giai-phap`, `/cong-nghe`, `/gioi-thieu`, `/blog`, `/lien-he`, `/search`, 404, preview mode.

## Phase 4 — SEO

SeoMetadata cho mọi entity + SEO Pages admin, JSON-LD, sitemap index + 5 sitemap, robots theo môi trường, redirect manager (301/302/410, auto redirect khi đổi slug, chống vòng lặp), broken links, canonical/breadcrumb, hreflang-ready, full-text search, tối ưu hiệu năng (image srcset, preload, font, cache headers).

## Phase 5 — Marketing

Form Contact/Quote/Demo (+ attachment, honeypot, captcha tuỳ chọn, UTM/referrer/landing), Lead CRM mini (Kanban pipeline, assign, activities, export CSV), newsletter, CTA library, email thông báo lead mới (SMTP từ config), dashboard số liệu + top landing pages + lead sources, tracking scripts từ Settings.

## Phase 6 — QA & vận hành

Responsive 320→1920, a11y (axe), Lighthouse CI, security review (OWASP ASVS L1 checklist), E2E Playwright (login, CRUD project/product/blog, upload, publish, contact/demo/quote, SEO route, redirect), Docker compose (api, web, admin, sqlserver, redis, nginx), CI/CD (build → test → lint → security scan → docker build → deploy → health check) cho nhánh `develop → staging → main`, backup/restore SQL Server (full hằng ngày + log 15 phút, retention 30 ngày, quy trình restore thử hằng tháng), tài liệu bàn giao (README, Architecture, API, Deployment, Backup/restore, Admin guide, SEO guide).

## Rủi ro & quyết định cần chủ dự án xác nhận

1. **Quyền công bố từng dự án**: tên khách hàng, logo, screenshot, URL live, chức năng PerfectKey/PKW được công bố — seed để `DRAFT`, `CanShowClient=false` cho tới khi xác nhận.
2. **Ownership Cơm Thị Nở**: seed `UNDISCLOSED`.
3. **Tên thương hiệu hiển thị**: mặc định "Nguyên Bình Technology", đổi trong Settings.
4. **.NET 10 LTS**: hiện build trên .NET 9 (SDK đang cài); nâng cấp khi cài SDK 10 — chỉ đổi `TargetFramework` + version package.
5. **Hosting/CDN** (VPS + nginx hay Azure/AWS) — ảnh hưởng Storage provider và pipeline deploy.
