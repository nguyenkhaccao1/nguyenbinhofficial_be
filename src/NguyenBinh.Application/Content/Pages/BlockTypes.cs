namespace NguyenBinh.Application.Content.Pages;

public sealed record BlockTypeDefinition(string Type, string Label, string Category, bool IsDynamic);

/// <summary>
/// Danh muc block cua Page builder (muc 22). Them block moi: khai bao o day + renderer (web) + editor (admin);
/// khong can migration vi Type luu dang chuoi va Data la JSON.
/// Block "dong" chi luu truy van (nguon, so luong, bo loc) — du lieu resolve khi render.
/// </summary>
public static class BlockTypes
{
    public const string Hero = "HERO";
    public const string Heading = "HEADING";
    public const string Text = "TEXT";
    public const string RichText = "RICH_TEXT";
    public const string Image = "IMAGE";
    public const string Video = "VIDEO";
    public const string Gallery = "GALLERY";
    public const string Stats = "STATS";
    public const string LogoCloud = "LOGO_CLOUD";
    public const string FeatureGrid = "FEATURE_GRID";
    public const string Projects = "PROJECTS";
    public const string Products = "PRODUCTS";
    public const string Services = "SERVICES";
    public const string Industries = "INDUSTRIES";
    public const string TechStack = "TECH_STACK";
    public const string Testimonials = "TESTIMONIALS";
    public const string Team = "TEAM";
    public const string Timeline = "TIMELINE";
    public const string Faq = "FAQ";
    public const string Cta = "CTA";
    public const string ContactForm = "CONTACT_FORM";
    public const string Pricing = "PRICING";
    public const string Comparison = "COMPARISON";
    public const string Blog = "BLOG";
    public const string CustomHtml = "CUSTOM_HTML";
    public const string Spacer = "SPACER";
    public const string Divider = "DIVIDER";

    public static readonly IReadOnlyList<BlockTypeDefinition> All =
    [
        new(Hero, "Hero", "Bố cục", false),
        new(Heading, "Tiêu đề", "Văn bản", false),
        new(Text, "Đoạn văn", "Văn bản", false),
        new(RichText, "Rich text", "Văn bản", false),
        new(Image, "Hình ảnh", "Media", false),
        new(Video, "Video", "Media", false),
        new(Gallery, "Thư viện ảnh", "Media", false),
        new(Stats, "Số liệu", "Nội dung", false),
        new(LogoCloud, "Logo", "Nội dung", false),
        new(FeatureGrid, "Lưới tính năng", "Nội dung", false),
        new(Projects, "Dự án", "Dữ liệu động", true),
        new(Products, "Sản phẩm", "Dữ liệu động", true),
        new(Services, "Dịch vụ", "Dữ liệu động", true),
        new(Industries, "Ngành / giải pháp", "Dữ liệu động", true),
        new(TechStack, "Công nghệ", "Dữ liệu động", true),
        new(Testimonials, "Đánh giá khách hàng", "Dữ liệu động", true),
        new(Team, "Đội ngũ", "Dữ liệu động", true),
        new(Timeline, "Quy trình / Timeline", "Nội dung", false),
        new(Faq, "Câu hỏi thường gặp", "Nội dung", false),
        new(Cta, "Kêu gọi hành động", "Bố cục", false),
        new(ContactForm, "Form liên hệ", "Form", false),
        new(Pricing, "Bảng giá", "Nội dung", false),
        new(Comparison, "So sánh", "Nội dung", false),
        new(Blog, "Bài viết", "Dữ liệu động", true),
        new(CustomHtml, "HTML tuỳ chỉnh", "Nâng cao", false),
        new(Spacer, "Khoảng trắng", "Bố cục", false),
        new(Divider, "Đường kẻ", "Bố cục", false),
    ];

    public static bool IsKnown(string type) => All.Any(b => b.Type == type);
}
