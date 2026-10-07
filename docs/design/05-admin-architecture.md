# 05 — Admin architecture (`apps/admin`)

## 1. Stack

Vite + React 19 + TypeScript, React Router (data router, base `/admin`), TanStack Query (server state), TanStack Table (bảng), React Hook Form + Zod (form), Zustand (auth session + UI prefs), Tailwind v4 + Radix UI primitives (dialog, dropdown, tabs, popover — accessible, không style sẵn), `@dnd-kit` (kéo thả sort/page builder/media), TipTap (rich text), Recharts (dashboard), sonner (toast).

## 2. Cấu trúc

```text
src/
├── main.tsx / router.tsx
├── app/
│   ├── AppShell.tsx        Sidebar + Topbar + Breadcrumb + <Outlet>
│   ├── Sidebar.tsx         Menu theo mục 20, ẩn mục user không có quyền `*.view`
│   └── GlobalSearch.tsx    Cmd/Ctrl+K → /api/v1/admin/search
├── auth/
│   ├── session.store.ts    accessToken (memory), user, permissions
│   ├── http.ts             fetch wrapper: gắn Bearer, 401 → refresh 1 lần (single-flight) → retry; fail → /login
│   ├── RequireAuth.tsx / Can.tsx (ẩn nút theo quyền — chỉ là UX, backend vẫn enforce)
├── components/
│   ├── data-table/         DataTable generic: search, filter chips, sort, column visibility (lưu localStorage), pagination, bulk select + bulk action bar
│   ├── form/               FormSection, Field, SlugField (auto từ tiêu đề, cảnh báo đổi slug → tạo redirect 301), RichText, MediaPicker, EnumSelect, MultiSelect, DateTime, JsonSettings
│   ├── content/            StatusBadge, PublishMenu (Publish/Unpublish/Schedule/Preview), VersionHistoryDrawer, SeoPanel, UnsavedChangesGuard, AutosaveIndicator
│   └── ui/                 Button, Input, Dialog, Drawer, Tabs, Badge, Skeleton, EmptyState, ErrorState, ConfirmDialog
├── features/
│   ├── dashboard/  pages/  menus/  products/  projects/  taxonomies/  services/  blog/
│   ├── leads/ (Kanban pipeline + list + detail timeline)  quotes/  demos/  contacts/  newsletter/  ctas/
│   ├── library/ (testimonials, partners, team, faqs)
│   ├── media/ (folder tree, grid, uploader đa file drag-drop, detail drawer: alt/caption/crop/usages)
│   ├── seo/ (metadata grid, redirects, sitemap, robots, broken links)
│   ├── website/ (header/footer/social/contact/settings/brand color/tracking)
│   └── system/ (users, roles + permission matrix, audit logs, system logs)
└── page-builder/
    ├── registry.ts         { type, label, icon, defaultData, schema (zod), Editor component, Preview component }
    ├── Canvas.tsx          Section → Block, dnd reorder, enable/disable, duplicate, delete
    ├── Inspector.tsx       Tab Nội dung (Editor của block) · Tab Giao diện (background, padding, width, align, responsive, animation, class)
    └── PreviewFrame.tsx    iframe tới web public `?preview=token` ở breakpoint desktop/tablet/mobile
```

## 3. Mẫu một module CRUD

Mỗi module = `api.ts` (hook TanStack Query: `useList`, `useDetail`, `useCreate`, `useUpdate`, `usePublish`, `useBulk`…) sinh từ factory `createContentApi('projects')` + `ListPage.tsx` (DataTable cấu hình cột) + `EditPage.tsx` (form tab). Mutation cập nhật cache cục bộ / invalidate query → **không reload trang** (mục 21).

## 4. Trạng thái UI bắt buộc (mục 73)

Mọi màn hình có: Loading (skeleton), Empty (gợi ý tạo mới), Error (thông báo + Thử lại), Forbidden (403), validation lỗi server 422 map về từng field RHF, 409 RowVersion → dialog "Nội dung đã bị người khác sửa — tải lại / ghi đè".

## 5. Autosave & cảnh báo rời trang (mục 67)

Form Blog/Page builder: debounce 5s → `PUT .../autosave` (ghi `ContentVersions` IsAutosave), indicator "Đã lưu nháp lúc…". `useBlocker` + `beforeunload` khi form dirty.

## 6. Phân quyền ở UI

`GET /admin/auth/me` trả `permissions[]`. Sidebar/nút hành động ẩn theo quyền; route bảo vệ bằng `<RequirePermission code="project.view">`. Đây chỉ là trải nghiệm — backend luôn kiểm tra lại.
