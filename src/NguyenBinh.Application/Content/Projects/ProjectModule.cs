using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Content.Projects;

public sealed class ProjectInput
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ShortDescription { get; set; }
    public string? ShortResult { get; set; }

    public Guid? ClientId { get; set; }
    public Guid? IndustryId { get; set; }
    public Guid? ProductId { get; set; }

    public ProjectContentType PrimaryContentType { get; set; } = ProjectContentType.CustomProject;
    public List<ProjectContentType> ContentTypes { get; set; } = [];
    public CommercialType CommercialType { get; set; } = CommercialType.CustomDevelopment;
    public List<ProjectRole> ProjectRoles { get; set; } = [];

    public OwnershipType OwnershipType { get; set; } = OwnershipType.Undisclosed;
    public string? ProjectOwner { get; set; }
    public string? NguyenBinhContribution { get; set; }
    public string? PublicCreditText { get; set; }
    public bool CanShowClient { get; set; }
    public bool CanShowClientLogo { get; set; }
    public bool CanShowScreenshots { get; set; }
    public bool CanShowMetrics { get; set; }
    public bool CanShowTechnology { get; set; } = true;
    public bool CanShowLiveUrl { get; set; }

    public string? Overview { get; set; }
    public string? Problem { get; set; }
    public string? Requirements { get; set; }
    public string? Solution { get; set; }
    public string? Architecture { get; set; }
    public Guid? ArchitectureMediaId { get; set; }
    public string? Challenge { get; set; }
    public string? ChallengeSolution { get; set; }
    public string? Result { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? LaunchDate { get; set; }
    public ProjectState ProjectState { get; set; } = ProjectState.Live;

    public string? WebsiteUrl { get; set; }
    public string? DemoUrl { get; set; }
    public string? AndroidUrl { get; set; }
    public string? IosUrl { get; set; }
    public string? GithubUrl { get; set; }
    public Guid? QrMediaId { get; set; }

    public Guid? CoverMediaId { get; set; }
    public Guid? ThumbnailMediaId { get; set; }
    public bool IsFeatured { get; set; }
    public int FeaturedOrder { get; set; }
    public int SortOrder { get; set; }

    public List<Guid> CategoryIds { get; set; } = [];
    public List<ProjectTechnologyInput> Technologies { get; set; } = [];
    public List<ProjectFeatureInput> Features { get; set; } = [];
    public List<ProjectMediaInput> Media { get; set; } = [];
    public List<ProjectMetricInput> Metrics { get; set; } = [];
    public List<ProjectLinkInput> Links { get; set; } = [];

    public SeoMeta Seo { get; set; } = new();
}

public sealed class ProjectTechnologyInput
{
    public Guid TechnologyId { get; set; }
    public string? Note { get; set; }
}

public sealed class ProjectFeatureInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public Guid? MediaId { get; set; }
    public bool IsPublic { get; set; } = true;
}

public sealed class ProjectMediaInput
{
    public ProjectMediaKind Kind { get; set; }
    public Guid? MediaId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Caption { get; set; }
    public string? Alt { get; set; }
    public string? GroupKey { get; set; }
    public bool IsPublic { get; set; } = true;
}

public sealed class ProjectMetricInput
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? Description { get; set; }
}

public sealed class ProjectLinkInput
{
    public ProjectLinkKind Kind { get; set; }
    public string? Label { get; set; }
    public string Url { get; set; } = string.Empty;
}

public sealed record ProjectListItem(
    Guid Id,
    string Name,
    string Slug,
    ProjectContentType PrimaryContentType,
    List<ProjectContentType> ContentTypes,
    List<ProjectRole> ProjectRoles,
    OwnershipType OwnershipType,
    string? IndustryName,
    string? ClientName,
    Guid? CoverMediaId,
    bool IsFeatured,
    int FeaturedOrder,
    int SortOrder,
    ContentStatus Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? UpdatedAt);

internal sealed class ProjectInputValidator : AbstractValidator<ProjectInput>
{
    private static readonly ProjectMediaKind[] ExternalVideoKinds =
        [ProjectMediaKind.YouTube, ProjectMediaKind.Vimeo, ProjectMediaKind.ExternalVideo];

    public ProjectInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên dự án.").MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.ShortResult).MaximumLength(300);
        RuleFor(x => x.ProjectOwner).MaximumLength(200);
        RuleFor(x => x.PublicCreditText).MaximumLength(500);

        RuleFor(x => x.ContentTypes)
            .Must((x, types) => !types.Contains(ProjectContentType.OwnProduct) && x.PrimaryContentType != ProjectContentType.OwnProduct
                                || x.OwnershipType == OwnershipType.NguyenBinhOwned)
            .WithMessage("Chỉ dự án do Nguyên Bình sở hữu mới được gắn loại OWN_PRODUCT (Sản phẩm của Nguyên Bình).");
        RuleFor(x => x.ProjectRoles)
            .Must((x, roles) => !roles.Contains(ProjectRole.Owner) || x.OwnershipType is OwnershipType.NguyenBinhOwned or OwnershipType.CoOwned)
            .WithMessage("Vai trò OWNER chỉ dùng khi Nguyên Bình sở hữu hoặc đồng sở hữu.");

        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate is not null && x.EndDate is not null)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");

        RuleFor(x => x.WebsiteUrl).HttpUrl();
        RuleFor(x => x.DemoUrl).HttpUrl();
        RuleFor(x => x.AndroidUrl).HttpUrl();
        RuleFor(x => x.IosUrl).HttpUrl();
        RuleFor(x => x.GithubUrl).HttpUrl();

        RuleFor(x => x.Features).Must(l => l.Count <= 60).WithMessage("Tối đa 60 chức năng.");
        RuleForEach(x => x.Features).ChildRules(f =>
        {
            f.RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên chức năng.").MaximumLength(200);
            f.RuleFor(x => x.Description).MaximumLength(1000);
        });

        RuleFor(x => x.Media).Must(l => l.Count <= 100).WithMessage("Tối đa 100 media.");
        RuleForEach(x => x.Media).ChildRules(m =>
        {
            m.RuleFor(x => x.ExternalUrl).NotEmpty().When(x => ExternalVideoKinds.Contains(x.Kind))
                .WithMessage("Nhập link video.").HttpUrl();
            m.RuleFor(x => x.MediaId).NotNull().When(x => !ExternalVideoKinds.Contains(x.Kind))
                .WithMessage("Chọn file từ thư viện media.");
            m.RuleFor(x => x.Caption).MaximumLength(500);
            m.RuleFor(x => x.Alt).MaximumLength(300);
        });

        RuleForEach(x => x.Metrics).ChildRules(m =>
        {
            m.RuleFor(x => x.Label).NotEmpty().WithMessage("Nhập tên chỉ số.").MaximumLength(100);
            m.RuleFor(x => x.Value).NotEmpty().WithMessage("Nhập giá trị.").MaximumLength(50);
        });

        RuleForEach(x => x.Links).ChildRules(l =>
        {
            l.RuleFor(x => x.Url).NotEmpty().WithMessage("Nhập URL.").HttpUrl();
            l.RuleFor(x => x.Label).MaximumLength(100);
        });

        RuleFor(x => x.Seo).SetValidator(new SeoMetaValidator());
    }
}

public sealed class ProjectModule : ContentModule<Project, ProjectListItem, ProjectInput>
{
    public override string Label => "dự án";
    public override string DefaultSort => "sortOrder,-updatedAt";

    public override SortMap<Project> Sorts { get; } = new SortMap<Project>()
        .Add("name", p => p.Name)
        .Add("sortOrder", p => p.SortOrder)
        .Add("featuredOrder", p => p.FeaturedOrder)
        .Add("status", p => p.Status)
        .Add("updatedAt", p => p.UpdatedAt)
        .Add("createdAt", p => p.CreatedAt);

    public override Expression<Func<Project, ProjectListItem>> ListProjection => p => new ProjectListItem(
        p.Id, p.Name, p.Slug, p.PrimaryContentType, p.ContentTypes, p.ProjectRoles, p.OwnershipType,
        p.Industry != null ? p.Industry.Name : null, p.Client != null ? p.Client.Name : null, p.CoverMediaId,
        p.IsFeatured, p.FeaturedOrder, p.SortOrder, p.Status, p.PublishAt, p.UpdatedAt);

    public override IQueryable<Project> ApplySearch(IQueryable<Project> query, string search) =>
        query.Where(p => p.Name.Contains(search) || p.Slug.Contains(search) ||
                         (p.ShortDescription != null && p.ShortDescription.Contains(search)));

    public override IQueryable<Project> ApplyFilters(IQueryable<Project> query, IReadOnlyDictionary<string, string> f)
    {
        if (f.TryGetValue("contentType", out var ct) && EnumParser.TryParse<ProjectContentType>(ct, out var type))
            query = query.Where(p => p.PrimaryContentType == type || p.ContentTypes.Contains(type));
        if (f.TryGetValue("ownership", out var ow) && EnumParser.TryParse<OwnershipType>(ow, out var ownership))
            query = query.Where(p => p.OwnershipType == ownership);
        if (f.TryGetValue("industryId", out var ind) && Guid.TryParse(ind, out var industryId))
            query = query.Where(p => p.IndustryId == industryId);
        if (f.TryGetValue("featured", out var fe) && bool.TryParse(fe, out var featured))
            query = query.Where(p => p.IsFeatured == featured);
        return query;
    }

    public override IQueryable<Project> IncludeDetails(IQueryable<Project> query) => query
        .Include(p => p.Categories).Include(p => p.Technologies).Include(p => p.Features)
        .Include(p => p.Media).Include(p => p.Metrics).Include(p => p.Links);

    public override string? SlugSource(Project entity) => entity.Name;

    public override ProjectInput ToInput(Project p) => new()
    {
        Name = p.Name, Slug = p.Slug, ShortDescription = p.ShortDescription, ShortResult = p.ShortResult,
        ClientId = p.ClientId, IndustryId = p.IndustryId, ProductId = p.ProductId,
        PrimaryContentType = p.PrimaryContentType, ContentTypes = [.. p.ContentTypes], CommercialType = p.CommercialType,
        ProjectRoles = [.. p.ProjectRoles],
        OwnershipType = p.OwnershipType, ProjectOwner = p.ProjectOwner, NguyenBinhContribution = p.NguyenBinhContribution,
        PublicCreditText = p.PublicCreditText, CanShowClient = p.CanShowClient, CanShowClientLogo = p.CanShowClientLogo,
        CanShowScreenshots = p.CanShowScreenshots, CanShowMetrics = p.CanShowMetrics,
        CanShowTechnology = p.CanShowTechnology, CanShowLiveUrl = p.CanShowLiveUrl,
        Overview = p.Overview, Problem = p.Problem, Requirements = p.Requirements, Solution = p.Solution,
        Architecture = p.Architecture, ArchitectureMediaId = p.ArchitectureMediaId, Challenge = p.Challenge,
        ChallengeSolution = p.ChallengeSolution, Result = p.Result,
        StartDate = p.StartDate, EndDate = p.EndDate, LaunchDate = p.LaunchDate, ProjectState = p.ProjectState,
        WebsiteUrl = p.WebsiteUrl, DemoUrl = p.DemoUrl, AndroidUrl = p.AndroidUrl, IosUrl = p.IosUrl,
        GithubUrl = p.GithubUrl, QrMediaId = p.QrMediaId, CoverMediaId = p.CoverMediaId,
        ThumbnailMediaId = p.ThumbnailMediaId, IsFeatured = p.IsFeatured, FeaturedOrder = p.FeaturedOrder,
        SortOrder = p.SortOrder,
        CategoryIds = p.Categories.Select(c => c.CategoryId).ToList(),
        Technologies = p.Technologies.OrderBy(t => t.SortOrder)
            .Select(t => new ProjectTechnologyInput { TechnologyId = t.TechnologyId, Note = t.Note }).ToList(),
        Features = p.Features.OrderBy(f => f.SortOrder).Select(f => new ProjectFeatureInput
        {
            Title = f.Title, Description = f.Description, Icon = f.Icon, MediaId = f.MediaId, IsPublic = f.IsPublic,
        }).ToList(),
        Media = p.Media.OrderBy(m => m.SortOrder).Select(m => new ProjectMediaInput
        {
            Kind = m.Kind, MediaId = m.MediaId, ExternalUrl = m.ExternalUrl, Caption = m.Caption, Alt = m.Alt,
            GroupKey = m.GroupKey, IsPublic = m.IsPublic,
        }).ToList(),
        Metrics = p.Metrics.OrderBy(m => m.SortOrder).Select(m => new ProjectMetricInput
        {
            Label = m.Label, Value = m.Value, Unit = m.Unit, Description = m.Description,
        }).ToList(),
        Links = p.Links.OrderBy(l => l.SortOrder)
            .Select(l => new ProjectLinkInput { Kind = l.Kind, Label = l.Label, Url = l.Url }).ToList(),
        Seo = p.Seo,
    };

    public override async Task ApplyAsync(Project p, ProjectInput i, ContentContext ctx, CancellationToken ct)
    {
        p.Name = i.Name.Trim();
        p.Slug = i.Slug?.Trim() ?? string.Empty;
        p.ShortDescription = Clean(i.ShortDescription);
        p.ShortResult = Clean(i.ShortResult);

        p.ClientId = await References.ExistsAsync<Client>(ctx, i.ClientId, "clientId", "Khách hàng", ct);
        p.IndustryId = await References.ExistsAsync<Industry>(ctx, i.IndustryId, "industryId", "Ngành", ct);
        p.ProductId = await References.ExistsAsync<Product>(ctx, i.ProductId, "productId", "Sản phẩm", ct);

        p.PrimaryContentType = i.PrimaryContentType;
        p.ContentTypes = i.ContentTypes.Append(i.PrimaryContentType).Distinct().ToList();
        p.CommercialType = i.CommercialType;
        p.ProjectRoles = i.ProjectRoles.Distinct().ToList();

        p.OwnershipType = i.OwnershipType;
        p.ProjectOwner = Clean(i.ProjectOwner);
        p.NguyenBinhContribution = ctx.Html.Sanitize(i.NguyenBinhContribution);
        p.PublicCreditText = Clean(i.PublicCreditText);
        p.CanShowClient = i.CanShowClient;
        p.CanShowClientLogo = i.CanShowClientLogo;
        p.CanShowScreenshots = i.CanShowScreenshots;
        p.CanShowMetrics = i.CanShowMetrics;
        p.CanShowTechnology = i.CanShowTechnology;
        p.CanShowLiveUrl = i.CanShowLiveUrl;

        p.Overview = ctx.Html.Sanitize(i.Overview);
        p.Problem = ctx.Html.Sanitize(i.Problem);
        p.Requirements = ctx.Html.Sanitize(i.Requirements);
        p.Solution = ctx.Html.Sanitize(i.Solution);
        p.Architecture = ctx.Html.Sanitize(i.Architecture);
        p.Challenge = ctx.Html.Sanitize(i.Challenge);
        p.ChallengeSolution = ctx.Html.Sanitize(i.ChallengeSolution);
        p.Result = ctx.Html.Sanitize(i.Result);

        p.StartDate = i.StartDate;
        p.EndDate = i.EndDate;
        p.LaunchDate = i.LaunchDate;
        p.ProjectState = i.ProjectState;

        p.WebsiteUrl = Clean(i.WebsiteUrl);
        p.DemoUrl = Clean(i.DemoUrl);
        p.AndroidUrl = Clean(i.AndroidUrl);
        p.IosUrl = Clean(i.IosUrl);
        p.GithubUrl = Clean(i.GithubUrl);

        p.ArchitectureMediaId = await References.MediaAsync(ctx, i.ArchitectureMediaId, "architectureMediaId", ct);
        p.QrMediaId = await References.MediaAsync(ctx, i.QrMediaId, "qrMediaId", ct);
        p.CoverMediaId = await References.MediaAsync(ctx, i.CoverMediaId, "coverMediaId", ct);
        p.ThumbnailMediaId = await References.MediaAsync(ctx, i.ThumbnailMediaId, "thumbnailMediaId", ct);
        await References.EnsureMediaAsync(ctx,
            i.Features.Where(f => f.MediaId != null).Select(f => f.MediaId!.Value)
                .Concat(i.Media.Where(m => m.MediaId != null).Select(m => m.MediaId!.Value)), "media", ct);

        p.IsFeatured = i.IsFeatured;
        p.FeaturedOrder = i.FeaturedOrder;
        p.SortOrder = i.SortOrder;
        p.Seo = i.Seo;

        var categoryIds = await References.AllExistAsync<ProjectCategory>(ctx, i.CategoryIds, "categoryIds", "Danh mục", ct);
        p.Categories.RemoveAll(c => !categoryIds.Contains(c.CategoryId));
        foreach (var id in categoryIds.Where(id => p.Categories.All(c => c.CategoryId != id)))
            p.Categories.Add(new ProjectCategoryMapping { ProjectId = p.Id, CategoryId = id });

        var techIds = await References.AllExistAsync<Technology>(ctx,
            i.Technologies.Select(t => t.TechnologyId), "technologies", "Công nghệ", ct);
        p.Technologies.RemoveAll(t => !techIds.Contains(t.TechnologyId));
        for (var index = 0; index < i.Technologies.Count; index++)
        {
            var input = i.Technologies[index];
            var mapping = p.Technologies.FirstOrDefault(t => t.TechnologyId == input.TechnologyId);
            if (mapping is null)
            {
                mapping = new ProjectTechnologyMapping { ProjectId = p.Id, TechnologyId = input.TechnologyId };
                p.Technologies.Add(mapping);
            }

            mapping.Note = Clean(input.Note);
            mapping.SortOrder = index;
        }

        // Bang con nho: thay toan bo theo thu tu tren form (thu tu = SortOrder, keo tha o admin).
        p.Features.Clear();
        p.Features.AddRange(i.Features.Select((f, idx) => new ProjectFeature
        {
            ProjectId = p.Id, Title = f.Title.Trim(), Description = Clean(f.Description), Icon = Clean(f.Icon),
            MediaId = f.MediaId, IsPublic = f.IsPublic, SortOrder = idx,
        }));
        p.Media.Clear();
        p.Media.AddRange(i.Media.Select((m, idx) => new ProjectMedia
        {
            ProjectId = p.Id, Kind = m.Kind, MediaId = m.MediaId, ExternalUrl = Clean(m.ExternalUrl),
            Caption = Clean(m.Caption), Alt = Clean(m.Alt), GroupKey = Clean(m.GroupKey), IsPublic = m.IsPublic,
            SortOrder = idx,
        }));
        p.Metrics.Clear();
        p.Metrics.AddRange(i.Metrics.Select((m, idx) => new ProjectMetric
        {
            ProjectId = p.Id, Label = m.Label.Trim(), Value = m.Value.Trim(), Unit = Clean(m.Unit),
            Description = Clean(m.Description), SortOrder = idx,
        }));
        p.Links.Clear();
        p.Links.AddRange(i.Links.Select((l, idx) => new ProjectLink
        {
            ProjectId = p.Id, Kind = l.Kind, Label = Clean(l.Label), Url = l.Url.Trim(), SortOrder = idx,
        }));
    }

    /// <summary>
    /// Khong cong bo du an khi chua ro quyen so huu / vai tro (muc 6, 25): khach hang phai nhin thay
    /// ro Nguyen Binh lam gi, khong tu nhan la san pham cua minh.
    /// </summary>
    public override Task ValidatePublishAsync(Project p, IDictionary<string, string[]> errors, ContentContext ctx,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.ShortDescription))
            errors["shortDescription"] = ["Cần mô tả ngắn trước khi xuất bản."];
        if (p.OwnershipType == OwnershipType.Undisclosed)
            errors["ownershipType"] = ["Cần cấu hình quyền sở hữu (Nguyên Bình sở hữu / khách hàng sở hữu…) trước khi xuất bản."];
        if (p.ProjectRoles.Count == 0)
            errors["projectRoles"] = ["Cần chọn vai trò của Nguyên Bình trong dự án."];
        if (p.OwnershipType != OwnershipType.NguyenBinhOwned && string.IsNullOrWhiteSpace(p.PublicCreditText))
            errors["publicCreditText"] = ["Dự án không thuộc sở hữu Nguyên Bình cần câu ghi nhận đóng góp công khai."];
        return Task.CompletedTask;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Project p) =>
        Refs((p.CoverMediaId, "Cover"), (p.ThumbnailMediaId, "Thumbnail"), (p.ArchitectureMediaId, "Architecture"),
                (p.QrMediaId, "QR"))
            .Concat(p.Features.Where(f => f.MediaId != null).Select(f => new MediaUsageRef(f.MediaId!.Value, "Feature")))
            .Concat(p.Media.Where(m => m.MediaId != null).Select(m => new MediaUsageRef(m.MediaId!.Value, $"Media:{m.Kind}")))
            .Distinct();

    public override void PrepareDuplicate(ProjectInput input)
    {
        input.Name = $"{input.Name} (bản sao)";
        input.Slug = null;
        input.IsFeatured = false;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
