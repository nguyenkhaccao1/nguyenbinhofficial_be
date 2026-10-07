# 01 — Sitemap toàn hệ thống

## 1. Public website (SSR — `apps/web`)

```text
/                                   Trang chủ (Page builder, PageType=HOME)
│
├── /san-pham                       Danh sách sản phẩm Nguyên Bình sở hữu/kinh doanh
│   └── /san-pham/{slug}            Product landing (vd: /san-pham/pos-nguyen-binh)
│
├── /giai-phap                      Tổng quan giải pháp theo ngành
│   └── /giai-phap/{slug}           Landing ngành (Page, PageType=SOLUTION, gắn Industry)
│       ├── nha-hang  khach-san  ban-le  doanh-nghiep  nhan-su  thuong-mai-dien-tu ...
│
├── /dich-vu                        Danh sách dịch vụ (nhóm theo ServiceCategory)
│   └── /dich-vu/{slug}             Chi tiết dịch vụ (vd: /dich-vu/react-development)
│
├── /du-an                          Dự án đã triển khai (lọc: ngành, loại, công nghệ, vai trò)
│   └── /du-an/{slug}               Case study
│
├── /cong-nghe                      Công nghệ (nhóm Backend/Frontend/Mobile/Data/Infrastructure)
│
├── /blog                           Knowledge hub
│   ├── /blog/{category-slug}       Danh sách theo danh mục   ┐ cùng không gian slug,
│   ├── /blog/{post-slug}           Bài viết                  ┘ API /blog/resolve/{slug}
│   ├── /blog/tag/{tag-slug}        Theo tag (noindex mặc định)
│   └── /blog/tac-gia/{slug}        Trang tác giả
│
├── /gioi-thieu                     Giới thiệu (Page builder)
├── /lien-he                        Liên hệ + form lead đầy đủ
├── /yeu-cau-bao-gia                Form báo giá
├── /yeu-cau-demo                   Form demo (preselect ?product=slug)
├── /search?q=                      Tìm kiếm (noindex)
│
├── /chinh-sach-bao-mat  /dieu-khoan-su-dung  /chinh-sach-cookie    (Page, PageType=LEGAL)
│
├── /{landing-path}                 SEO landing (Page, PageType=LANDING), vd:
│     /phat-trien-phan-mem-theo-yeu-cau   /cong-ty-phat-trien-phan-mem-ha-noi
│     /thiet-ke-website-doanh-nghiep      /phan-mem-pos-nha-hang  ...
│
├── /sitemap.xml                    Sitemap index
│   ├── /sitemap-pages.xml   /sitemap-products.xml   /sitemap-services.xml
│   ├── /sitemap-projects.xml   /sitemap-posts.xml
├── /robots.txt                     Từ Settings (SIT/UAT luôn Disallow: /)
└── *                               Redirect (301/302/410) → Page theo path → 404 đẹp
```

### Thứ tự phân giải route ở server web

1. Static/asset → trả thẳng.
2. **Redirect map** (cache 60s từ API): khớp `OldUrl` → 301/302, hoặc 410 Gone.
3. Route cố định (`/san-pham/:slug`, `/du-an/:slug`, ...).
4. Catch-all `*` → `GET /api/v1/pages/by-path?path=` → render Page builder.
5. Không có → 404 (HTTP status 404 thật, không phải soft-404).

## 2. Admin (`/admin` — SPA, `noindex`, chặn trong robots)

```text
/admin/login
/admin                                   Dashboard
/admin/content/pages  [/new | /:id]      Trang (Page builder)
/admin/content/landing-pages             Trang landing (lọc PageType=LANDING/SOLUTION)
/admin/content/menus  /admin/content/footer
/admin/products  /admin/products/:id     (tab: Thông tin · Tính năng · Module · Bảng giá · FAQ · Media · SEO)
/admin/products/categories
/admin/projects  /admin/projects/:id     (tab: Thông tin · Phân loại & quyền công bố · Case study · Media · Links · Metrics · SEO)
/admin/projects/categories | industries | technologies | clients
/admin/services  /admin/services/:id  /admin/services/categories
/admin/blog/posts  /admin/blog/posts/:id  /admin/blog/categories  /admin/blog/tags  /admin/blog/authors
/admin/marketing/leads  /admin/marketing/leads/:id   (Kanban + bảng)
/admin/marketing/quotes  /admin/marketing/demos  /admin/marketing/contacts
/admin/marketing/newsletter  /admin/marketing/ctas
/admin/library/testimonials | partners | team | faqs
/admin/media  (folder tree + grid; ?folder= &type=image|video|document)
/admin/seo/pages  /admin/seo/redirects  /admin/seo/sitemap  /admin/seo/robots  /admin/seo/broken-links
/admin/website/header | footer | social | contact | settings  (brand, màu, tracking, Zalo/Messenger)
/admin/system/users  /admin/system/roles  /admin/system/audit-logs  /admin/system/system-logs
/admin/profile
```

## 3. API

`/api/v1/...` (public) và `/api/v1/admin/...` (cần JWT) — chi tiết ở [06-api.md](06-api.md).
`/health/live`, `/health/ready`, `/swagger` (chỉ Development/SIT).

## 4. Triển khai theo domain

```text
nguyenbinhofficial.com.vn
  /            → web (Node SSR)
  /admin/*     → admin (static, nginx)
  /api/*       → api (ASP.NET Core)
  /media/*     → api static files hoặc CDN (cache 1 năm, immutable)
```
