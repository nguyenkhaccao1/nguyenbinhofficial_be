# 04 — Frontend architecture (public website)

## 1. Monorepo (`nguyenbinhofficial_fe`)

```text
nguyenbinhofficial_fe/
├── package.json            npm workspaces
├── apps/
│   ├── web/        Public website — React Router v7 framework mode, SSR (Node)
│   └── admin/      Admin CMS — Vite SPA, base "/admin/"
└── packages/
    └── shared/     API client (fetch + ApiResponse unwrap + ApiError), kiểu settings, mã permission + nhãn tiếng Việt,
                    helper format/slug. Zod schema dùng chung (lead form…) và enum nội dung thêm ở Phase 2–5.
```

## 2. Vì sao React Router v7 framework mode

Yêu cầu: React + React Router + **SSR** để Googlebot đọc được HTML trước khi JS chạy. RR v7 framework mode cho:
- `loader` chạy trên server → HTML đầy đủ nội dung ở response đầu tiên;
- `meta`/`links` theo route → title/description/canonical/OG nằm trong HTML SSR;
- resource route → `/sitemap.xml`, `/robots.txt` trả XML/text;
- `headers` → `Cache-Control: public, s-maxage=300, stale-while-revalidate=86400` cho CDN;
- hydrate + client navigation như SPA; code splitting theo route tự động.

## 3. Cấu trúc `apps/web`

```text
app/
├── root.tsx                 <html lang>, font preload, theme CSS variables từ settings, tracking scripts (theo Settings, trì hoãn sau consent)
├── entry.server.tsx         renderToPipeableStream, bot → chờ onAllReady (HTML đầy đủ)
├── routes.ts                Khai báo route (xem 01-sitemap)
├── routes/
│   ├── _site.tsx            Layout: Header (megamenu) + Footer (dynamic) — loader lấy settings + navigation (cache)
│   ├── _site._index.tsx     Home → render Page builder (PageType=HOME)
│   ├── _site.san-pham.tsx / _site.san-pham.$slug.tsx
│   ├── _site.giai-phap.tsx / _site.giai-phap.$slug.tsx
│   ├── _site.dich-vu.tsx / _site.dich-vu.$slug.tsx
│   ├── _site.du-an.tsx / _site.du-an.$slug.tsx
│   ├── _site.cong-nghe.tsx
│   ├── _site.blog.tsx / _site.blog.$slug.tsx (resolve category|post) / _site.blog.tag.$slug.tsx
│   ├── _site.lien-he.tsx / _site.yeu-cau-bao-gia.tsx / _site.yeu-cau-demo.tsx
│   ├── _site.search.tsx
│   ├── _site.$.tsx          Catch-all: Page theo path → 404
│   ├── sitemap[.]xml.ts / sitemap-$section[.]xml.ts / robots[.]txt.ts
│   └── api.leads.ts         (tuỳ chọn) proxy POST form → API, giữ IP thật
├── server/
│   ├── api.server.ts        Fetch API nội bộ (INTERNAL_API_URL), timeout, cache ngắn trong process
│   ├── redirects.server.ts  Redirect map (cache 60s) — áp dụng trong middleware Express trước khi RR xử lý
│   └── seo.server.ts        Build meta, JSON-LD, breadcrumbs
├── blocks/                  Renderer Page builder — 1 file / block type, registry `blockRenderers[type]`
├── components/
│   ├── layout/   Header, MegaMenu, MobileNav, Footer, Breadcrumbs
│   ├── project/  ProjectCard, ProjectFilters, CaseStudyHero, CreditBadge (dựa OwnershipType), MediaGallery, DeviceFrame
│   ├── product/  ProductHero, ModuleGrid, PricingTable, FaqList
│   ├── forms/    LeadForm (RHF + Zod), QuoteForm, DemoForm, Attachment upload, UTM capture
│   └── ui/       Button, Container, Section, Badge, Tag, Image (srcset WebP/AVIF), VideoEmbed (lite YouTube)
├── lib/          utm.ts (lưu UTM + landing + referrer vào sessionStorage lần vào đầu tiên), i18n.ts, format.ts
└── styles/       tailwind.css (@theme tokens)
server.ts         Express: compression, helmet, static (immutable), redirect middleware, RR request handler
```

## 4. Nguyên tắc dữ liệu

- **Không mock data**: mọi nội dung (projects, products, services, blog, sections trang chủ, media, SEO, contact) đi qua API.
- Loader gọi API trên server; client chỉ dùng TanStack Query cho phần tương tác (search gợi ý, lọc dự án không reload, form submit).
- Lỗi API → `ErrorBoundary` theo route; 404 → `throw data(null, {status:404})` → trang 404 đẹp.
- Ownership: `CreditBadge` và mọi chữ "Sản phẩm của Nguyên Bình" chỉ render khi `ownershipType === "NGUYEN_BINH_OWNED"`; ngược lại hiển thị `publicCreditText` + vai trò. Ảnh/khách hàng/metrics/link bị API lọc theo cờ `CanShow*` — frontend không phải là lớp bảo vệ duy nhất.

## 5. Hiệu năng (mục 31–32)

- Font Inter variable tự host (`@fontsource-variable/inter`, subset latin + vietnamese), `font-display: swap`, preload 1 file.
- Ảnh: `<picture>` AVIF → WebP → fallback, `srcset` theo variants, `width/height` cố định (CLS), `loading="lazy"` trừ ảnh LCP (`fetchpriority="high"` + preload).
- Hero không dùng video nặng; video demo dùng lite-embed (chỉ tải iframe khi bấm).
- Framer Motion (`motion` / `LazyMotion` + `domAnimation`) chỉ cho reveal nhẹ, tôn trọng `prefers-reduced-motion`; không animation chặn LCP.
- JS public tối thiểu: không UI kit nặng; Tailwind v4; tách chunk theo route.
- Cache: HTML `s-maxage` ngắn + SWR; asset hash `immutable, max-age=31536000`.

## 6. Accessibility (mục 51)

Semantic landmarks (`header/nav/main/footer`), skip link, megamenu điều khiển bằng bàn phím (Esc/Tab/Arrow), focus-visible rõ, contrast ≥ 4.5:1, mọi input có `<label>`, lỗi form gắn `aria-describedby`, ảnh có alt từ Media Library.

## 7. Responsive (mục 50)

Mobile-first, breakpoint Tailwind: `sm 640 · md 768 · lg 1024 · xl 1280 · 2xl 1440` + container max 1440, kiểm tra 320→1920. Megamenu → drawer trên mobile; montage hero → 1 screenshot chính + strip ngang; bảng giá → stack card.

## 8. i18n

`vi` mặc định không prefix URL; `en` (sau này) dưới `/en/...`. Chuỗi UI tĩnh trong `app/locales/{vi,en}.json`; nội dung lấy theo `?lang=` từ API. `hreflang` sinh khi có bản dịch.
