using System.Text.Json;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Public;

// ---------- Chung ----------

/// <summary>Anh cong khai: URL goc + cac nguon resize (AVIF/WebP) cho &lt;picture srcset&gt;.</summary>
public sealed record PublicImage(
    Guid Id,
    string Url,
    string? Alt,
    int? Width,
    int? Height,
    string? BlurDataUrl,
    IReadOnlyList<PublicImageSource> Sources);

public sealed record PublicImageSource(string Format, int Width, string Url);

public sealed record PublicSeo(
    string? Title,
    string? Description,
    string? CanonicalUrl,
    string? Robots,
    string? OgTitle,
    string? OgDescription,
    string? OgImage,
    string? TwitterImage,
    string? SchemaJson);

public sealed record NamedLink(string Name, string Slug);

// ---------- Du an ----------

/// <summary>
/// Card du an. IsOwnProduct chi true khi Nguyen Binh so huu — chi khi do website moi ghi
/// "San pham cua Nguyen Binh"; con lai hien vai tro + CreditText (muc 25).
/// </summary>
public sealed record ProjectCard(
    Guid Id,
    string Name,
    string Slug,
    string? ShortDescription,
    string? ShortResult,
    string? IndustryName,
    ProjectContentType PrimaryContentType,
    IReadOnlyList<ProjectContentType> ContentTypes,
    IReadOnlyList<ProjectRole> ProjectRoles,
    bool IsOwnProduct,
    string? CreditText,
    IReadOnlyList<string> Technologies,
    PublicImage? Image,
    int? Year,
    string? ProblemExcerpt,
    string? SolutionExcerpt,
    string? ResultExcerpt);

public sealed record PublicClient(string Name, PublicImage? Logo, string? WebsiteUrl);

public sealed record PublicTechnology(string Name, string Slug, TechnologyGroup Group, PublicImage? Logo, string? Description);

public sealed record PublicFeature(string Title, string? Description, string? Icon, PublicImage? Image);

public sealed record PublicProjectMedia(ProjectMediaKind Kind, PublicImage? Image, string? ExternalUrl, string? FileUrl,
    string? Caption, string? Alt, string? GroupKey);

public sealed record PublicMetric(string Label, string Value, string? Unit, string? Description);

public sealed record PublicLink(ProjectLinkKind Kind, string? Label, string Url);

public sealed record ProjectDetail(
    ProjectCard Card,
    PublicClient? Client,
    NamedLink? Industry,
    NamedLink? Product,
    CommercialType CommercialType,
    ProjectState ProjectState,
    string? NguyenBinhContribution,
    string? Overview,
    string? Problem,
    string? Requirements,
    string? Solution,
    string? Architecture,
    PublicImage? ArchitectureImage,
    string? Challenge,
    string? ChallengeSolution,
    string? Result,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateOnly? LaunchDate,
    IReadOnlyList<PublicFeature> Features,
    IReadOnlyList<PublicProjectMedia> Media,
    IReadOnlyList<PublicMetric> Metrics,
    IReadOnlyList<PublicLink> Links,
    IReadOnlyList<PublicTechnology> Technologies,
    PublicImage? Cover,
    PublicImage? Qr,
    IReadOnlyList<ProjectCard> Related,
    PublicSeo Seo,
    DateTimeOffset? UpdatedAt);

public sealed class ProjectQuery
{
    public string? ContentType { get; set; }
    public string? Industry { get; set; }
    public string? Technology { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

// ---------- San pham ----------

public sealed record ProductCard(
    Guid Id,
    string Name,
    string Slug,
    string? Tagline,
    string? ShortDescription,
    ProductType ProductType,
    PublicImage? Logo,
    PublicImage? Hero);

public sealed record PublicProductModule(string Name, string? Description, string? Icon, PublicImage? Image, IReadOnlyList<string> Items);

public sealed record PublicPlan(string Name, decimal? PriceAmount, string Currency, BillingPeriod BillingPeriod, string? PriceNote,
    IReadOnlyList<string> Features, bool IsHighlighted, string? CtaLabel, string? CtaUrl);

public sealed record PublicFaq(string Question, string Answer);

public sealed record PublicTestimonial(string AuthorName, string? AuthorTitle, string? Company, PublicImage? Avatar, string Quote,
    int? Rating);

public sealed record ProductDetail(
    ProductCard Card,
    string? Description,
    CommercialType CommercialType,
    string? Problem,
    string? Solution,
    string? TargetUsers,
    string? Integration,
    string? Deployment,
    string? Security,
    string? DemoVideoUrl,
    string? DemoUrl,
    string? PricingNote,
    IReadOnlyList<PublicFeature> Features,
    IReadOnlyList<PublicProductModule> Modules,
    IReadOnlyList<PublicProjectMedia> Media,
    IReadOnlyList<PublicPlan> Plans,
    IReadOnlyList<PublicFaq> Faqs,
    IReadOnlyList<PublicTestimonial> Testimonials,
    IReadOnlyList<ProjectCard> CaseStudies,
    PublicSeo Seo,
    DateTimeOffset? UpdatedAt);

// ---------- Dich vu ----------

public sealed record ServiceCard(Guid Id, string Name, string Slug, string? Icon, string? ShortDescription, string? CategoryName,
    PublicImage? Cover);

public sealed record PublicProcessStep(string Title, string? Description, string? Output);

public sealed record ServiceDetail(
    ServiceCard Card,
    string? Description,
    string? Deliverables,
    IReadOnlyList<PublicProcessStep> Process,
    IReadOnlyList<PublicFeature> Features,
    IReadOnlyList<PublicTechnology> Technologies,
    IReadOnlyList<ProjectCard> RelatedProjects,
    IReadOnlyList<PublicFaq> Faqs,
    IReadOnlyList<ServiceCard> OtherServices,
    PublicSeo Seo,
    DateTimeOffset? UpdatedAt);

public sealed record ServiceGroup(string? CategoryName, string? CategorySlug, IReadOnlyList<ServiceCard> Services);

// ---------- Nganh / cong nghe ----------

public sealed record IndustryCard(Guid Id, string Name, string Slug, string? Icon, string? Description, string? SolutionPath,
    int ProjectCount);

public sealed record TechnologyGroupDto(TechnologyGroup Group, IReadOnlyList<PublicTechnology> Items);

// ---------- Blog ----------

public sealed record PostCard(Guid Id, string Title, string Slug, string? Excerpt, PublicImage? Cover, string? AuthorName,
    DateTimeOffset? PublishedAt, int ReadingMinutes, NamedLink? Category);

public sealed record PublicAuthor(string Name, string Slug, string? Title, string? Bio, PublicImage? Avatar, IReadOnlyList<string> Links);

public sealed record PostDetail(
    PostCard Card,
    string? ContentHtml,
    PublicAuthor? Author,
    IReadOnlyList<NamedLink> Categories,
    IReadOnlyList<NamedLink> Tags,
    IReadOnlyList<PostCard> Related,
    PublicSeo Seo,
    DateTimeOffset? UpdatedAt);

public sealed record BlogCategoryDto(string Name, string Slug, string? Description, int PostCount, PublicSeo Seo);

/// <summary>/blog/{slug} dung chung cho bai viet va danh muc.</summary>
public sealed record BlogResolveResult(string Kind, PostDetail? Post, BlogCategoryDto? Category);

public sealed class PostQuery
{
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

// ---------- Trang (page builder) ----------

public sealed record PublicBlock(string Type, JsonElement Data, JsonElement Settings, object? Resolved);

public sealed record PublicSection(string? Name, JsonElement Settings, IReadOnlyList<PublicBlock> Blocks);

/// <summary>Trang da resolve: block dong co du lieu kem theo, Media chua moi anh duoc tham chieu theo id.</summary>
public sealed record PublicPage(
    Guid Id,
    string Title,
    string Path,
    PageType PageType,
    NamedLink? Industry,
    IReadOnlyList<PublicSection> Sections,
    IReadOnlyDictionary<Guid, PublicImage> Media,
    PublicSeo Seo,
    DateTimeOffset? UpdatedAt);

// ---------- Dieu huong ----------

public sealed record NavItem(string Label, string? Url, string? Description, bool OpenInNewTab, IReadOnlyList<NavItem> Children,
    string? MegaSource, IReadOnlyList<NavMegaItem> Mega);

/// <summary>Group: ten nhom (vd nhom dich vu) de megamenu chia cot.</summary>
public sealed record NavMegaItem(string Label, string Url, string? Description, string? Icon, string? Group = null);

public sealed record NavigationDto(
    IReadOnlyList<NavItem> Header,
    IReadOnlyList<NavItem> FooterCompany,
    IReadOnlyList<NavItem> FooterTechnology,
    IReadOnlyList<NavItem> FooterLegal,
    IReadOnlyList<NavMegaItem> Products,
    IReadOnlyList<NavMegaItem> Services,
    IReadOnlyList<NavMegaItem> Solutions);

// ---------- Tim kiem ----------

public sealed record SearchHit(string Kind, string Title, string Url, string? Excerpt, PublicImage? Image);

public sealed record SearchResult(string Query, IReadOnlyList<SearchHit> Hits);

// ---------- Sitemap ----------

/// <summary>Kind: home | page | listing | project | product | service | post | category.</summary>
public sealed record SitemapEntry(string Path, DateTimeOffset? LastModified, string Kind);
