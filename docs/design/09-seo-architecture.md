# 09 — SEO architecture

## 1. Render

Mọi trang public SSR (React Router v7). Googlebot nhận HTML có đủ: `<title>`, meta description, canonical, OG/Twitter, H1, nội dung chính, link nội bộ, JSON-LD — không cần chạy JS. Bot được chờ `onAllReady` (không stream dang dở).

## 2. Metadata — thứ tự ưu tiên

1. `SeoMetadata` của entity (admin nhập).
2. Tự sinh từ entity: title = `{Name} | {Brand}`, description = `ShortDescription` (cắt 155 ký tự), OG image = cover.
3. `seo.defaults` trong Settings (title template `%s | Nguyên Bình Technology`, default OG image).

Canonical: mặc định URL tuyệt đối không query string (trừ `?page=` > 1 trên trang danh sách). Robots: `index,follow` mặc định; `noindex` cho `/search`, `/blog/tag/*`, preview, trang lọc có nhiều tham số. Toàn bộ SIT/UAT: `noindex` + robots `Disallow: /` (theo biến môi trường, không phụ thuộc admin).

## 3. Structured data (JSON-LD, không spam)

| Trang | Schema |
|-------|--------|
| Mọi trang | `Organization` (1 lần, trong layout: name, url, logo, sameAs từ Social, contactPoint từ Contact), `WebSite` + `SearchAction` (chỉ trang chủ) |
| Có breadcrumb | `BreadcrumbList` |
| `/blog/{slug}` | `Article` (headline, image, datePublished, dateModified, author) |
| `/san-pham/{slug}` | `SoftwareApplication` (name, applicationCategory, operatingSystem, offers chỉ khi có giá thật) |
| `/dich-vu/{slug}` | `Service` (serviceType, provider=Organization, areaServed) |
| Trang có FAQ hiển thị | `FAQPage` (chỉ câu hỏi đang hiển thị trên trang) |
| `/du-an/{slug}` | `CreativeWork` tối giản (không gắn review/rating) |

Admin có thể ghi đè `SchemaJson` nhưng được validate JSON.

## 4. Sitemap

- `/sitemap.xml` = sitemap index → `/sitemap-pages.xml`, `/sitemap-products.xml`, `/sitemap-services.xml`, `/sitemap-projects.xml`, `/sitemap-posts.xml`.
- Sinh động từ DB (chỉ nội dung PUBLISHED, không `noindex`, không `ExcludeFromSitemap`), `lastmod = UpdatedAt`, cache 1 giờ + xoá cache khi publish/unpublish.
- Tự chia file khi > 45.000 URL.

## 5. Robots

`robots.txt` từ Settings + rule cố định: `Disallow: /admin`, `Disallow: /api`, `Disallow: /search`, `Sitemap: https://nguyenbinhofficial.com.vn/sitemap.xml`.

## 6. Redirect & status code

| Mã | Dùng khi |
|----|---------|
| 301 | Đổi slug (tự tạo redirect khi admin đổi slug của nội dung đã publish), gộp trang |
| 302 | Chuyển tạm (chiến dịch) |
| 410 | Nội dung gỡ vĩnh viễn |
| 404 | Không tồn tại — trả status 404 thật + trang 404 đẹp; ghi `BrokenLinks` |

Chuẩn hoá URL: lowercase, bỏ trailing slash (301), `www` → non-www (ở nginx), HTTP → HTTPS. Redirect Manager chặn vòng lặp và rút gọn chuỗi (A→B→C thành A→C).

## 7. hreflang-ready

Bảng `ContentTranslations` + `SeoMetadata.Locale`. Khi có bản `en`, layout sinh `<link rel="alternate" hreflang="vi|en|x-default">`; sitemap thêm `xhtml:link`.

## 8. Internal linking

Case study → dịch vụ/công nghệ/ngành liên quan; dịch vụ → dự án liên quan; landing ngành → sản phẩm + dự án cùng ngành; blog → sản phẩm/dịch vụ qua CTA block. Breadcrumb mọi trang cấp 2+.

## 9. Landing SEO (mục 47)

Landing (`PageType=LANDING`) build bằng Page builder, path tự do ở root. Admin cảnh báo khi 2 landing có nội dung trùng > 70% (so sánh shingle text) và khi thiếu: H1, ≥ 300 từ, ≥ 1 dự án/sản phẩm liên kết, meta description. Không tạo landing hàng loạt từ template.

## 10. Performance là SEO

Mục tiêu Lighthouse Perf ≥ 90, SEO ≥ 95, A11y ≥ 90, BP ≥ 95; CWV LCP < 2.5s, CLS < 0.1, INP < 200ms — kiểm tra bằng Lighthouse CI trong pipeline cho `/`, `/du-an/{slug}`, `/san-pham/{slug}`, `/blog/{slug}`.

## 11. Tracking (mục 39–40)

GA4, GTM, Search Console verification, Meta Pixel, Clarity — ID lấy từ Settings (`tracking`), không hardcode; script nạp `afterInteractive`, tôn trọng consent. UTM + referrer + landing URL lưu sessionStorage ở lần truy cập đầu → gửi kèm mọi form lead.
