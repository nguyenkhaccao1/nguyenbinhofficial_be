# 08 — Design System

Phong cách: **Modern Technology / Enterprise SaaS / Premium Software Studio**. Sản phẩm thật là nhân vật chính — chữ lớn, nhiều khoảng trắng, screenshot lớn, ít trang trí.

## 1. Màu (CSS variables, admin chỉnh được `brand.primary`, `brand.accent`)

| Token | Light | Ghi chú |
|-------|-------|---------|
| `--color-bg` | `#FFFFFF` | Nền chính |
| `--color-bg-subtle` | `#F6F7F9` | Section xen kẽ |
| `--color-dark` | `#0A0D14` | Section tối, footer |
| `--color-dark-elevated` | `#121722` | Card trên nền tối |
| `--color-fg` | `#0A0D14` | Chữ chính |
| `--color-fg-muted` | `#5B6474` | Chữ phụ (contrast 5.9:1 trên trắng) |
| `--color-border` | `#E4E7EC` | Viền 1px |
| `--color-primary` | `#1D3FD8` (Deep Blue) | Nút chính, link (contrast 7.0:1) |
| `--color-primary-hover` | `#1633B3` | |
| `--color-accent` | `#22B8E6` (Electric Cyan) | Điểm nhấn nhỏ: chấm, line, highlight trên nền tối — không dùng cho chữ trên nền trắng |
| `--color-success / warning / danger` | `#12A150 / #D98B00 / #D92D20` | Trạng thái (admin) |

Quy tắc: không gradient tràn lan (tối đa 1 radial glow mờ sau hero montage trên nền tối). Không icon gradient. Shadow chỉ 2 cấp (`sm` cho card hover, `xl` cho device mockup).

## 2. Typography

- Font: **Inter Variable** (hỗ trợ tiếng Việt đầy đủ), tự host, `font-feature-settings: "ss01","cv11"`; mono: **JetBrains Mono** cho tech chip/số liệu.
- Thang (desktop / mobile):

| Token | Size / line-height | Weight | Tracking |
|-------|--------------------|--------|----------|
| `display` | 72/76 → 40/44 | 650 | -0.035em |
| `h1` | 56/62 → 34/40 | 650 | -0.03em |
| `h2` | 40/48 → 28/34 | 600 | -0.02em |
| `h3` | 24/32 → 20/28 | 600 | -0.01em |
| `body-lg` | 20/32 → 18/28 | 400 | 0 |
| `body` | 16/26 | 400 | 0 |
| `small` | 14/22 | 450 | 0 |
| `eyebrow` | 13/20 uppercase | 600 | 0.08em |

Đoạn văn tối đa ~68 ký tự/dòng (`max-w-prose`).

## 3. Layout & spacing

- Lưới 12 cột, container `max-w-[1280px]` (nội dung) / `1440px` (montage), gutter 16 (mobile) / 24 / 32.
- Spacing scale 4px. Section padding: `py-16` mobile, `py-24` tablet, `py-32` desktop.
- Bo góc: `6px` (input, button), `12px` (card), `16px` (device frame). Không bo tròn mọi thứ — chip/tag dùng `4px`.

## 4. Component chính

| Component | Mô tả |
|-----------|-------|
| Button | `primary` (nền primary, chữ trắng), `secondary` (viền), `ghost`, `link`; size `md 44px` / `lg 52px` (touch ≥ 44px) |
| Section | `tone: light \| subtle \| dark`, `width: content \| wide \| full`, padding preset |
| ProjectCard | Ảnh 16:10 trong browser frame mảnh → tên → ngành · loại → tech chips (mono) → vai trò → shortResult → "Xem case study →" |
| DeviceFrame | `browser`, `phone`, `tablet`, `pos` — SVG/CSS nhẹ, không ảnh nặng |
| Badge | ContentType, Status; nền nhạt + chữ đậm, 4px radius |
| CreditNote | Icon ⓘ + `PublicCreditText` — luôn hiển thị trên case study không thuộc sở hữu NB |
| Stat | Số lớn mono + nhãn; chỉ dùng khi có số liệu thật |

## 5. Motion

- Reveal: opacity 0→1 + translateY 12px, 400ms, `ease-out`, stagger 60ms, chỉ chạy 1 lần khi vào viewport.
- Hover card: ảnh scale 1.02, 300ms. Megamenu fade 150ms.
- `prefers-reduced-motion: reduce` → tắt toàn bộ transform.
- Không parallax nặng, không auto-carousel, không animation trên phần tử LCP.

## 6. Hình ảnh

Chỉ screenshot/dashboard/app/mockup/diagram thật từ CMS. Không stock photo. Ảnh chưa có → placeholder trung tính có tên dự án (không ảnh giả).

## 7. Admin UI

Cùng token màu/typography; mật độ cao hơn (body 14px, row 44px). Sidebar 260px thu gọn 72px. Nền `#F6F7F9`, card trắng viền 1px. Dark mode admin: phase sau (token đã sẵn sàng).
