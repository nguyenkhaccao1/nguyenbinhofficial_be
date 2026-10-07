# 07 — Wireframes

Ký hiệu: `[IMG]` ảnh thật từ CMS · `(CTA)` nút · `{cms}` dữ liệu động. Nền **D** = section tối (#0A0D14), **L** = sáng.

## 7.1 Homepage (desktop 1440)

```text
┌──────────────────────────────────────────────────────────────────────────────────┐
│ ◆ NGUYÊN BÌNH  Sản phẩm▾ Giải pháp▾ Dịch vụ▾ Dự án Công nghệ Blog Giới thiệu (Trao đổi dự án)│
├──────────────────────────────────────────────────────────────────────────────────┤ L
│ 01 HERO                                                                          │
│  Chúng tôi xây phần mềm được          ┌──────────────[IMG dashboard PMS]─────┐   │
│  sử dụng trong vận hành thực tế.      │                                       │   │
│                                       │      ┌─[IMG POS]──┐  ┌[mobile]┐       │   │
│  Website, Mobile App, POS, PMS, ERP   │      └────────────┘  │ [IMG]  │       │   │
│  và hệ thống quản trị doanh nghiệp…   └──────────────────────│        │───────┘   │
│  (Xem dự án đã triển khai) (Trao đổi dự án)                  └────────┘           │
│  ─ montage lấy từ {cms: screenshot các dự án featured, CanShowScreenshots=1}      │
├──────────────────────────────────────────────────────────────────────────────────┤
│ 02 TRUST  Website · Mobile · ERP · POS · PMS · HRM · Ecommerce · API Integration  │
│           [.NET] [React] [Flutter] [SQL Server] [Redis] [Docker] [Azure/AWS]      │
├──────────────────────────────────────────────────────────────────────────────────┤ D
│ 03 SẢN PHẨM CỦA NGUYÊN BÌNH                                                       │
│ ┌──────────────────────────────────────┬─────────────────────────────┐            │
│ │ POS Nguyên Bình                      │  [IMG POS màn hình bán hàng] │            │
│ │ Phần mềm bán hàng cho nhà hàng/cafe  │                              │            │
│ │ • Order • Bếp • Kho • Báo cáo        │                              │            │
│ │ (Xem sản phẩm) (Nhận Demo) (Báo giá) │                              │            │
│ └──────────────────────────────────────┴─────────────────────────────┘            │
├──────────────────────────────────────────────────────────────────────────────────┤ L
│ 04 Sản phẩm thật. Hệ thống thật. Đã đưa vào vận hành.        (Tất cả dự án →)     │
│ ┌──────────[IMG]──────────┐ ┌──────────[IMG]──────────┐ ┌────────[IMG]─────────┐  │
│ │ PerfectKey Workforce    │ │ A-Smart                  │ │ Quản Lý Khách Sạn     │  │
│ │ HR & Workforce · HRM    │ │ Bán lẻ · Ecommerce       │ │ Khách sạn · PMS       │  │
│ │ React • ASP.NET • SQL   │ │ …                        │ │ …                     │  │
│ │ Vai trò: Phát triển     │ │ Vai trò: …               │ │ Vai trò: …            │  │
│ │ theo yêu cầu            │ │                          │ │                       │  │
│ │ {shortResult}  → Case   │ │                          │ │                       │  │
│ └─────────────────────────┘ └──────────────────────────┘ └───────────────────────┘│
│   (grid 2 lớn + 4 nhỏ, 4–6 dự án IsFeatured theo FeaturedOrder)                   │
├──────────────────────────────────────────────────────────────────────────────────┤
│ 05 DỊCH VỤ — Có quy trình riêng? Chúng tôi xây phần mềm theo quy trình của bạn.   │
│  Web App │ ERP │ CRM │ HRM │ PMS │ POS │ Mobile │ Ecommerce │ API │ Integration     │
│  (grid 5×2, mỗi ô: tên + 1 dòng mô tả + link /dich-vu/..)  (Mô tả dự án của bạn)  │
├──────────────────────────────────────────────────────────────────────────────────┤
│ 06 NGÀNH   [Nhà hàng] [Khách sạn] [Bán lẻ] [Doanh nghiệp] [Nhân sự] [TMĐT]        │
│            mỗi tile: icon + số dự án liên quan + → /giai-phap/{slug}              │
├──────────────────────────────────────────────────────────────────────────────────┤ D
│ 07 CASE STUDY HIGHLIGHT (1 dự án)                                                 │
│  [IMG lớn full-width]                                                             │
│  Bài toán ─────── Giải pháp ─────── Kết quả     (Đọc case study →)                │
├──────────────────────────────────────────────────────────────────────────────────┤ L
│ 08 Từ ý tưởng đến hệ thống vận hành                                               │
│  01 Discovery → 02 BA → 03 UX/UI → 04 Development → 05 Testing → 06 Deploy        │
│  mỗi bước: mô tả ngắn + "Đầu ra: …" (vd: Tài liệu phạm vi, backlog, wireframe…)   │
├──────────────────────────────────────────────────────────────────────────────────┤
│ 09 TECH STACK  Backend │ Frontend │ Mobile │ Data │ Infrastructure (từ Technologies)│
├──────────────────────────────────────────────────────────────────────────────────┤
│ 10 TESTIMONIALS (ẩn nếu không có testimonial CanPublish)                          │
├──────────────────────────────────────────────────────────────────────────────────┤
│ 11 BLOG  3 bài mới nhất                                                           │
├──────────────────────────────────────────────────────────────────────────────────┤ D
│ 12 Bạn có một ý tưởng cần triển khai?   (Gửi yêu cầu) (Nhận tư vấn) (Yêu cầu Demo)│
├──────────────────────────────────────────────────────────────────────────────────┤ D
│ 13 FOOTER  Công ty │ Sản phẩm{cms} │ Dịch vụ{cms} │ Giải pháp{cms} │ Công nghệ │   │
│            Legal │ Contact{settings: phone, email, Zalo, Messenger, địa chỉ}      │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Mobile (390): Hero chữ trước, montage dưới (1 ảnh chính + strip cuộn ngang). Card dự án 1 cột. Header: logo + nút "Trao đổi" + hamburger → drawer có accordion Sản phẩm/Giải pháp/Dịch vụ. Sticky bottom bar (Gọi · Zalo · Gửi yêu cầu) từ Settings.

Mỗi section ở trên là 1 `PageSection` trong Page HOME → admin bật/tắt, đổi thứ tự, đổi nội dung mà không sửa code.

## 7.2 Project detail `/du-an/{slug}`

```text
┌──────────────────────────────────────────────────────────────────────────────────┐
│ Trang chủ / Dự án / PerfectKey Workforce                                          │
│ [HRM] [Enterprise Software]                                                       │
│ PerfectKey Workforce                                                              │
│ Hệ thống quản lý nhân sự, ca làm và chấm công                                     │
│ ┌────────────┬────────────┬────────────┬──────────┬───────────────┬────────────┐  │
│ │ Ngành      │ Khách hàng │ Vai trò    │ Năm      │ Công nghệ     │ Website    │  │
│ │ Khách sạn  │ (ẩn nếu    │ Phát triển │ 2025     │ React·.NET    │ ↗ (nếu     │  │
│ │            │ !CanShow)  │ theo y/c   │          │               │ CanShowUrl)│  │
│ └────────────┴────────────┴────────────┴──────────┴───────────────┴────────────┘  │
│ ⓘ {PublicCreditText} — "Nguyên Bình tham gia phát triển và triển khai hệ thống    │
│   theo yêu cầu của khách hàng."                                                   │
│ [IMG cover trong browser frame]                                                   │
├──────────────────┬───────────────────────────────────────────────────────────────┤
│ Mục lục (sticky) │ Tổng quan                                                     │
│ · Tổng quan      │ Bài toán                                                      │
│ · Bài toán       │ Yêu cầu                                                       │
│ · Yêu cầu        │ Giải pháp                                                     │
│ · Giải pháp      │ Kiến trúc hệ thống   [IMG diagram]                            │
│ · Kiến trúc      │ Chức năng chính      grid feature (icon/ảnh + mô tả)          │
│ · Chức năng      │ Giao diện            [IMG desktop] [IMG desktop] (lightbox)   │
│ · Giao diện      │ Mobile               [phone][phone][phone]  App Store/Play/QR │
│ · Công nghệ      │ Công nghệ            chip theo nhóm                           │
│ · Thách thức     │ Thách thức → Cách giải quyết                                  │
│ · Kết quả        │ Kết quả              metrics (chỉ khi CanShowMetrics & có số) │
│                  │ Gallery · Video (lite embed)                                   │
├──────────────────┴───────────────────────────────────────────────────────────────┤
│ Dự án liên quan (3 card)                                                          │
├──────────────────────────────────────────────────────────────────────────────────┤ D
│ Bạn cần một hệ thống tương tự?          (Trao đổi với chúng tôi)                  │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Section rỗng không render (không hiển thị tiêu đề trống). Mục lục chỉ liệt kê section có nội dung.

## 7.3 Product detail `/san-pham/{slug}`

```text
┌──────────────────────────────────────────────────────────────────────────────────┐
│ [logo] POS Nguyên Bình                     │ [IMG hero: POS + tablet + mobile]    │
│ {tagline}                                  │                                      │
│ (Yêu cầu Demo) (Nhận báo giá)  ▶ Xem video │                                      │
├──────────────────────────────────────────────────────────────────────────────────┤
│ Vấn đề {problem}  ───────────►  Giải pháp {solution}                              │
├──────────────────────────────────────────────────────────────────────────────────┤
│ Tính năng   grid 3 cột (icon + tiêu đề + mô tả), nhóm theo Group                  │
├──────────────────────────────────────────────────────────────────────────────────┤ D
│ Màn hình    tab: [Bán hàng] [Bếp] [Kho] [Báo cáo] → screenshot lớn                │
├──────────────────────────────────────────────────────────────────────────────────┤
│ Module      card mỗi module + danh sách chức năng                                 │
│ Dành cho    {targetUsers}: Nhà hàng · Cafe · Chuỗi F&B …                          │
│ Tích hợp    {integration}   Triển khai {deployment}   Bảo mật {security}          │
├──────────────────────────────────────────────────────────────────────────────────┤
│ Bảng giá    [Gói A] [Gói B ★] [Gói C]   hoặc "Liên hệ báo giá"                     │
├──────────────────────────────────────────────────────────────────────────────────┤
│ Khách hàng nói gì (testimonials gắn ProductId) · Case study triển khai (Projects) │
│ FAQ (accordion, FAQPage schema)                                                   │
├──────────────────────────────────────────────────────────────────────────────────┤ D
│ Đăng ký Demo   [Họ tên][Công ty][Email][Điện thoại][Ngày mong muốn][Ghi chú] (Gửi)│
└──────────────────────────────────────────────────────────────────────────────────┘
```

## 7.4 Admin Dashboard

```text
┌────────────┬─────────────────────────────────────────────────────────────────────┐
│ ◆ NB Admin │ 🔍 Tìm kiếm (Ctrl+K)                          🔔   Nguyễn A ▾        │
│            ├─────────────────────────────────────────────────────────────────────┤
│ Dashboard  │ Dashboard                                         [7 ngày|30|90 ▾] │
│ Nội dung ▸ │ ┌─────────┬─────────┬─────────┬─────────┬─────────┬────────┬───────┐ │
│ Sản phẩm ▸ │ │Projects │Products │Posts    │Leads    │New leads│Demo req│Quote  │ │
│ Dự án    ▸ │ │   9     │   1     │  12     │  48     │   5 ▲   │   2    │  3    │ │
│ Dịch vụ  ▸ │ └─────────┴─────────┴─────────┴─────────┴─────────┴────────┴───────┘ │
│ Blog     ▸ │ ┌──────────────────────────────────┐ ┌────────────────────────────┐ │
│ Marketing▸ │ │ Leads theo ngày (line)           │ │ Nguồn lead (bar/donut)     │ │
│ Media    ▸ │ │                                  │ │ google / zalo / direct …   │ │
│ SEO      ▸ │ └──────────────────────────────────┘ └────────────────────────────┘ │
│ Website  ▸ │ ┌──────────────────────────────────┐ ┌────────────────────────────┐ │
│ Hệ thống ▸ │ │ Top landing pages (theo lead)    │ │ Lead gần đây               │ │
│            │ │ /san-pham/pos-…        12        │ │ Trần B · POS · NEW · 2h    │ │
│            │ │ /dich-vu/thiet-ke-…     8        │ │ …                    (→)   │ │
│            │ └──────────────────────────────────┘ └────────────────────────────┘ │
│            │ ┌──────────────────────────────────────────────────────────────────┐│
│            │ │ Nội dung cập nhật gần đây   Loại · Tiêu đề · Trạng thái · Người · ││
│            │ └──────────────────────────────────────────────────────────────────┘│
└────────────┴─────────────────────────────────────────────────────────────────────┘
```

Màn danh sách (mẫu chung):

```text
Breadcrumb: Dự án / Dự án
Dự án                                                    (+ Tạo dự án)
[🔍 Tìm…] [Trạng thái ▾] [Loại ▾] [Ngành ▾] [Nổi bật ▾]     [Cột ▾] [Đã xoá ☐]
┌─┬──────┬──────────────────┬────────┬──────────┬──────────┬───────────┬────┐
│☐│ Ảnh  │ Tên ↕            │ Loại   │ Vai trò  │ Trạng thái│ Cập nhật ↕│ ⋯ │
└─┴──────┴──────────────────┴────────┴──────────┴──────────┴───────────┴────┘
[3 đã chọn: Publish · Unpublish · Xoá]                     ‹ 1 2 3 › 20/trang
```
