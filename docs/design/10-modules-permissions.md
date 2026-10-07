# 10 — Module & Permission

Permission dạng `{module}.{action}`. Action chuẩn: `view, create, update, delete, publish`. Định nghĩa trong code (`NguyenBinh.Shared/Authorization/Permissions.cs`), seed vào bảng `Permissions` khi khởi động. Wildcard (`project.*`) chỉ là cách viết trong tài liệu/seed — lúc kiểm tra luôn so khớp mã đầy đủ.

## 1. Danh sách module & permission

| Module | Permission | Phạm vi |
|--------|-----------|---------|
| Dashboard | `dashboard.view` | |
| Trang / Landing / CTA | `page.view .create .update .delete .publish`, `page.custom_html` | Pages, sections, blocks, CTAs |
| Menu / Header / Footer | `menu.view .update` | |
| Sản phẩm | `product.view .create .update .delete .publish` | Products, categories, features, modules, plans, faqs |
| Dự án | `project.view .create .update .delete .publish` | Projects + categories, industries, technologies, clients |
| Dịch vụ | `service.view .create .update .delete .publish` | Services, categories |
| Blog | `blog.view .create .update .delete .publish` | Posts, categories, tags, authors |
| Thư viện | `library.view .create .update .delete .publish` | Testimonials, partners, team, faqs |
| Media | `media.view .upload .update .delete` | |
| Lead / CRM | `lead.view .create .update .delete .assign .export` | Leads, quotes, demos, contacts, newsletter |
| SEO | `seo.view .update .redirect .sitemap` | Metadata, redirects, sitemap, robots, broken links |
| Website settings | `settings.view .update` | Brand, contact, social, tracking, theme |
| Users | `user.view .create .update .delete` | |
| Roles | `role.view .create .update .delete` | Gán permission |
| Audit | `audit.view` | |
| System | `system.view` (system logs), `system.purge` (xoá vĩnh viễn) | |

## 2. Ma trận role mặc định

| Permission | SuperAdmin | Admin | Editor | SEO | Sales | Viewer |
|-----------|:---:|:---:|:---:|:---:|:---:|:---:|
| dashboard.view | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| page.* | ✔ | ✔ | ✔ (trừ custom_html) | view, update | – | view |
| page.custom_html | ✔ | ✔ | – | – | – | – |
| menu.* | ✔ | ✔ | ✔ | view | – | view |
| product.*, project.*, service.*, library.* | ✔ | ✔ | ✔ | view, update | view | view |
| blog.* | ✔ | ✔ | ✔ | ✔ | – | view |
| media.* | ✔ | ✔ | ✔ | view, upload, update | view | view |
| lead.* | ✔ | ✔ | – | – | ✔ (trừ delete) | – |
| seo.* | ✔ | ✔ | view | ✔ | – | view |
| settings.view / .update | ✔ | ✔ | view | view | – | view |
| user.*, role.* | ✔ | ✔ (không gán/sửa SuperAdmin) | – | – | – | – |
| audit.view | ✔ | ✔ | – | – | – | – |
| system.view | ✔ | ✔ | – | – | – | – |
| system.purge | ✔ | – | – | – | – | – |

Ghi chú:
- SEO role sửa được nội dung (update) để chỉnh SEO/headings nhưng không publish/xoá.
- Viewer chỉ đọc nội dung; không có `lead.view` vì lead chứa dữ liệu cá nhân (họ tên, email, điện thoại). Cấp thêm trong màn Roles nếu cần.
- Role hệ thống (`IsSystem`) không xoá được; quyền của SuperAdmin không sửa được.

## 3. Enforce

- Backend: `[HasPermission(...)]` trên từng action controller. Không có endpoint admin nào thiếu attribute — integration test quét toàn bộ endpoint `/api/v1/admin/**` (trừ `auth/*`) đảm bảo có permission metadata.
- Thay đổi quyền role → xoá cache `perm:role:{id}` → có hiệu lực ở request kế tiếp.
- Mọi thay đổi user/role/permission ghi `AuditLogs`.
