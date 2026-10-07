using FluentValidation;

namespace NguyenBinh.Application.Settings;

/// <summary>
/// Tham chieu media trong settings. Chi Id duoc luu; Url/Alt/kich thuoc duoc dien lai luc doc
/// de doi CDN/storage khong lam hong du lieu.
/// </summary>
public sealed class MediaRef
{
    public Guid Id { get; set; }
    public string? Url { get; set; }
    public string? Alt { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public sealed class BrandSettings
{
    public string SiteName { get; set; } = "Nguyên Bình Technology";
    public string ShortName { get; set; } = "Nguyên Bình";
    public string? LegalName { get; set; }
    public string? Tagline { get; set; } = "Xây dựng phần mềm, nền tảng số và hệ thống vận hành cho doanh nghiệp.";
    public string? Description { get; set; } =
        "Từ ý tưởng đến sản phẩm thực tế — Website, Mobile App, SaaS, POS, PMS, ERP và các hệ thống quản trị chuyên biệt.";
    public MediaRef? Logo { get; set; }
    public MediaRef? LogoDark { get; set; }
    public MediaRef? Favicon { get; set; }
}

public sealed class ThemeSettings
{
    public string PrimaryColor { get; set; } = "#1D3FD8";
    public string AccentColor { get; set; } = "#22B8E6";
    public string DarkColor { get; set; } = "#0A0D14";
}

public sealed class ContactSettings
{
    public string? Phone { get; set; }
    public string? Hotline { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? MapUrl { get; set; }
    public string? ZaloPhone { get; set; }
    public string? ZaloUrl { get; set; }
    public string? MessengerUrl { get; set; }
    public string? WorkingHours { get; set; }
    public string? TaxCode { get; set; }
}

public sealed class SocialSettings
{
    public string? Facebook { get; set; }
    public string? LinkedIn { get; set; }
    public string? YouTube { get; set; }
    public string? TikTok { get; set; }
    public string? GitHub { get; set; }
    public string? X { get; set; }
}

/// <summary>ID tracking — khong hardcode trong code (muc 39).</summary>
public sealed class TrackingSettings
{
    public string? Ga4MeasurementId { get; set; }
    public string? GtmContainerId { get; set; }
    public string? GoogleSiteVerification { get; set; }
    public string? BingSiteVerification { get; set; }
    public string? MetaPixelId { get; set; }
    public string? ClarityProjectId { get; set; }
}

public sealed class SeoSettings
{
    public string SiteUrl { get; set; } = "https://nguyenbinhofficial.com.vn";
    public string TitleTemplate { get; set; } = "%s | Nguyên Bình Technology";
    public string? DefaultTitle { get; set; } = "Nguyên Bình Technology — Phát triển phần mềm cho doanh nghiệp";
    public string? DefaultDescription { get; set; } =
        "Website, Mobile App, POS, PMS, ERP và hệ thống quản trị doanh nghiệp — từ thiết kế, phát triển đến triển khai.";
    public MediaRef? DefaultOgImage { get; set; }
    public string? TwitterHandle { get; set; }

    /// <summary>Dong bo sung vao robots.txt (chi ap dung o Production).</summary>
    public string? RobotsExtra { get; set; }
}

/// <summary>Cau hinh form lead — private (khong tra ra public).</summary>
public sealed class FormSettings
{
    public List<string> NotificationEmails { get; set; } = [];
    public bool SendAutoReply { get; set; }
}

internal static class SettingRules
{
    public const string HexColor = "^#[0-9A-Fa-f]{6}$";

    public static IRuleBuilderOptions<T, string?> HttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(500)
            .Must(v => string.IsNullOrEmpty(v) ||
                       (Uri.TryCreate(v, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")))
            .WithMessage("URL không hợp lệ (cần bắt đầu bằng http:// hoặc https://).");
}

internal sealed class BrandSettingsValidator : AbstractValidator<BrandSettings>
{
    public BrandSettingsValidator()
    {
        RuleFor(x => x.SiteName).NotEmpty().WithMessage("Vui lòng nhập tên thương hiệu.").MaximumLength(100);
        RuleFor(x => x.ShortName).NotEmpty().WithMessage("Vui lòng nhập tên ngắn.").MaximumLength(50);
        RuleFor(x => x.LegalName).MaximumLength(200);
        RuleFor(x => x.Tagline).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

internal sealed class ThemeSettingsValidator : AbstractValidator<ThemeSettings>
{
    public ThemeSettingsValidator()
    {
        RuleFor(x => x.PrimaryColor).Matches(SettingRules.HexColor).WithMessage("Màu phải dạng #RRGGBB.");
        RuleFor(x => x.AccentColor).Matches(SettingRules.HexColor).WithMessage("Màu phải dạng #RRGGBB.");
        RuleFor(x => x.DarkColor).Matches(SettingRules.HexColor).WithMessage("Màu phải dạng #RRGGBB.");
    }
}

internal sealed class ContactSettingsValidator : AbstractValidator<ContactSettings>
{
    public ContactSettingsValidator()
    {
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Email không hợp lệ.");
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^[0-9+()\s.-]*$").WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.Hotline).MaximumLength(30).Matches(@"^[0-9+()\s.-]*$").WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.ZaloPhone).MaximumLength(30).Matches(@"^[0-9+()\s.-]*$").WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.WorkingHours).MaximumLength(200);
        RuleFor(x => x.TaxCode).MaximumLength(30);
        RuleFor(x => x.MapUrl).HttpUrl();
        RuleFor(x => x.ZaloUrl).HttpUrl();
        RuleFor(x => x.MessengerUrl).HttpUrl();
    }
}

internal sealed class SocialSettingsValidator : AbstractValidator<SocialSettings>
{
    public SocialSettingsValidator()
    {
        RuleFor(x => x.Facebook).HttpUrl();
        RuleFor(x => x.LinkedIn).HttpUrl();
        RuleFor(x => x.YouTube).HttpUrl();
        RuleFor(x => x.TikTok).HttpUrl();
        RuleFor(x => x.GitHub).HttpUrl();
        RuleFor(x => x.X).HttpUrl();
    }
}

internal sealed class TrackingSettingsValidator : AbstractValidator<TrackingSettings>
{
    public TrackingSettingsValidator()
    {
        RuleFor(x => x.Ga4MeasurementId).Matches("^G-[A-Z0-9]{4,20}$")
            .When(x => !string.IsNullOrEmpty(x.Ga4MeasurementId)).WithMessage("GA4 ID có dạng G-XXXXXXX.");
        RuleFor(x => x.GtmContainerId).Matches("^GTM-[A-Z0-9]{4,12}$")
            .When(x => !string.IsNullOrEmpty(x.GtmContainerId)).WithMessage("GTM ID có dạng GTM-XXXXXX.");
        RuleFor(x => x.MetaPixelId).Matches("^[0-9]{6,20}$")
            .When(x => !string.IsNullOrEmpty(x.MetaPixelId)).WithMessage("Meta Pixel ID chỉ gồm chữ số.");
        RuleFor(x => x.ClarityProjectId).Matches("^[a-z0-9]{6,20}$")
            .When(x => !string.IsNullOrEmpty(x.ClarityProjectId)).WithMessage("Clarity Project ID không hợp lệ.");
        RuleFor(x => x.GoogleSiteVerification).MaximumLength(100).Matches("^[A-Za-z0-9_-]*$")
            .WithMessage("Mã xác minh không hợp lệ.");
        RuleFor(x => x.BingSiteVerification).MaximumLength(100).Matches("^[A-Za-z0-9_-]*$")
            .WithMessage("Mã xác minh không hợp lệ.");
    }
}

internal sealed class SeoSettingsValidator : AbstractValidator<SeoSettings>
{
    public SeoSettingsValidator()
    {
        RuleFor(x => x.SiteUrl).NotEmpty().HttpUrl()
            .Must(v => v is null || !v.EndsWith('/')).WithMessage("Không kết thúc bằng '/'.");
        RuleFor(x => x.TitleTemplate).NotEmpty().MaximumLength(100)
            .Must(v => v.Contains("%s")).WithMessage("Mẫu tiêu đề phải chứa %s.");
        RuleFor(x => x.DefaultTitle).MaximumLength(70);
        RuleFor(x => x.DefaultDescription).MaximumLength(300);
        RuleFor(x => x.TwitterHandle).MaximumLength(50);
        RuleFor(x => x.RobotsExtra).MaximumLength(2000);
    }
}

internal sealed class FormSettingsValidator : AbstractValidator<FormSettings>
{
    public FormSettingsValidator()
    {
        RuleFor(x => x.NotificationEmails).Must(l => l.Count <= 10).WithMessage("Tối đa 10 email.");
        RuleForEach(x => x.NotificationEmails).EmailAddress().WithMessage("Email '{PropertyValue}' không hợp lệ.");
    }
}
