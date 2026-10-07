# Nguyên Bình Official — Bộ thiết kế hệ thống (Phase 0)

Tài liệu thiết kế bắt buộc trước khi triển khai (mục 75 của yêu cầu dự án).

| # | Tài liệu | File |
|---|----------|------|
| 1 | Sitemap toàn hệ thống | [01-sitemap.md](01-sitemap.md) |
| 2 | Database ERD | [02-database-erd.md](02-database-erd.md) |
| 3 | Backend architecture | [03-backend-architecture.md](03-backend-architecture.md) |
| 4 | Frontend architecture (public web) | [04-frontend-architecture.md](04-frontend-architecture.md) |
| 5 | Admin architecture | [05-admin-architecture.md](05-admin-architecture.md) |
| 6 | Danh sách API | [06-api.md](06-api.md) |
| 7–10 | Wireframe: Homepage, Project Detail, Product Detail, Admin Dashboard | [07-wireframes.md](07-wireframes.md) |
| 11 | Design System | [08-design-system.md](08-design-system.md) |
| 12 | SEO architecture | [09-seo-architecture.md](09-seo-architecture.md) |
| 13 | Module & permission | [10-modules-permissions.md](10-modules-permissions.md) |
| 14 | Kế hoạch triển khai theo phase | [11-implementation-plan.md](11-implementation-plan.md) |

## Quyết định kiến trúc chính (tóm tắt)

| Chủ đề | Quyết định | Lý do |
|--------|-----------|-------|
| Backend | ASP.NET Core **.NET 9**, Modular Monolith trên nền Clean Architecture (Domain / Application / Infrastructure / Api / Shared) | Khớp yêu cầu; .NET 9 là SDK đang cài. **.NET 9 hết hỗ trợ 11/2026 → phải nâng lên .NET 10 LTS trước go-live** (đổi `TargetFramework` + version package). |
| DB | SQL Server, EF Core 9, migration code-first | Bắt buộc theo yêu cầu. |
| Id | `Guid` v7 (`Guid.CreateVersion7()`) — tăng dần theo thời gian | Không lộ số lượng bản ghi, không phân mảnh index như Guid v4. |
| Auth admin | ASP.NET Identity + JWT (access 15 phút, giữ trong memory) + refresh token xoay vòng (httpOnly cookie, phát hiện tái sử dụng → thu hồi cả "family") | Mục 44. |
| Phân quyền | Permission granular `module.action`, enforce ở backend bằng policy động `[HasPermission]`; quyền của role được cache và vô hiệu hoá khi role thay đổi | Mục 45. |
| Public web | **React Router v7 (framework mode) SSR** + TypeScript + Tailwind v4 + TanStack Query | Mục 3: dùng React Router và bắt buộc SSR — React Router v7 framework mode đáp ứng cả hai, không cần chuyển sang Next.js. |
| Admin | Vite + React SPA (React Router, TanStack Query/Table, RHF + Zod, Zustand cho auth/UI state) | Mục 3, 66. |
| Monorepo FE | npm workspaces: `apps/web`, `apps/admin`, `packages/shared` (gồm API client) | Tách rõ public/admin, dùng chung type & client. pnpm không dùng được trên máy dev (corepack lỗi). |
| Đa ngôn ngữ | Nội dung mặc định `vi` nằm trong cột chính; bản dịch trong `ContentTranslations` (EntityType, EntityId, Locale, Field, Value). Chuỗi UI public nằm trong file i18n ở frontend, không nằm trong DB | Mục 69. |
| Phân loại dự án | `ContentTypes` (đa giá trị), `CommercialType`, `ProjectRoles` (đa giá trị), `OwnershipType` + cờ hiển thị `CanShow*` | Mục 5, 25 — không bao giờ tự gọi dự án khách hàng là "Sản phẩm của Nguyên Bình". |
| Page Builder | `Pages → PageSections → PageBlocks`; block có `Type` + `Data` (JSON, validate theo schema của từng block) + `Settings` | Mục 22, mở rộng thêm block chỉ cần đăng ký 1 schema (BE) + 1 renderer (FE) + 1 editor (admin). |
| Ảnh | Pipeline libvips (NetVips) sinh biến thể WebP + AVIF nhiều kích thước; lưu qua `IFileStorage` (Local → S3/Azure Blob) | Mục 26, 31. |
| Cache | Output cache + `IDistributedCache` (Memory ở Dev, Redis ở SIT/UAT/Prod); web SSR trả `Cache-Control: s-maxage` cho CDN | Mục 31. |

## Trạng thái

| Phase | Trạng thái |
|-------|-----------|
| 0 — Thiết kế | ✅ Tài liệu này |
| 1 — Foundation | ✅ Backend (auth, RBAC, media, settings, audit) + Admin (đăng nhập, người dùng, vai trò, media, cấu hình, nhật ký) + khung web SSR. 74 test backend, 16 test frontend |
| 2 — CMS | Chưa bắt đầu |
