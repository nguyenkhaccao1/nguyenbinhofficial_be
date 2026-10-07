using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using ServiceEntity = NguyenBinh.Domain.Content.Service;

namespace NguyenBinh.Application.Content.Services;

public sealed class ServiceInput
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Icon { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? Deliverables { get; set; }
    public List<ServiceProcessStep> Process { get; set; } = [];
    public List<Guid> TechnologyIds { get; set; } = [];
    public List<Guid> RelatedProjectIds { get; set; } = [];
    public Guid? CoverMediaId { get; set; }
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public List<ServiceFeatureInput> Features { get; set; } = [];
    public SeoMeta Seo { get; set; } = new();
}

public sealed class ServiceFeatureInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

public sealed record ServiceListItem(
    Guid Id,
    string Name,
    string Slug,
    string? CategoryName,
    string? ShortDescription,
    bool IsFeatured,
    int SortOrder,
    ContentStatus Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? UpdatedAt);

internal sealed class ServiceInputValidator : AbstractValidator<ServiceInput>
{
    public ServiceInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên dịch vụ.").MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.Icon).MaximumLength(100);
        RuleFor(x => x.Process).Must(p => p.Count <= 12).WithMessage("Tối đa 12 bước.");
        RuleForEach(x => x.Process).ChildRules(s =>
        {
            s.RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên bước.").MaximumLength(150);
            s.RuleFor(x => x.Description).MaximumLength(1000);
            s.RuleFor(x => x.Output).MaximumLength(500);
        });
        RuleForEach(x => x.Features).ChildRules(f =>
        {
            f.RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên hạng mục.").MaximumLength(200);
            f.RuleFor(x => x.Description).MaximumLength(1000);
        });
        RuleFor(x => x.Seo).SetValidator(new SeoMetaValidator());
    }
}

public sealed class ServiceModule : ContentModule<ServiceEntity, ServiceListItem, ServiceInput>
{
    public override string Label => "dịch vụ";
    public override string DefaultSort => "sortOrder,name";

    public override SortMap<ServiceEntity> Sorts { get; } = new SortMap<ServiceEntity>()
        .Add("name", s => s.Name)
        .Add("sortOrder", s => s.SortOrder)
        .Add("status", s => s.Status)
        .Add("updatedAt", s => s.UpdatedAt);

    public override Expression<Func<ServiceEntity, ServiceListItem>> ListProjection => s => new ServiceListItem(
        s.Id, s.Name, s.Slug, s.Category != null ? s.Category.Name : null, s.ShortDescription, s.IsFeatured,
        s.SortOrder, s.Status, s.PublishAt, s.UpdatedAt);

    public override IQueryable<ServiceEntity> ApplySearch(IQueryable<ServiceEntity> query, string search) =>
        query.Where(s => s.Name.Contains(search) || s.Slug.Contains(search));

    public override IQueryable<ServiceEntity> ApplyFilters(IQueryable<ServiceEntity> query, IReadOnlyDictionary<string, string> f) =>
        f.TryGetValue("categoryId", out var c) && Guid.TryParse(c, out var categoryId)
            ? query.Where(s => s.CategoryId == categoryId)
            : query;

    public override IQueryable<ServiceEntity> IncludeDetails(IQueryable<ServiceEntity> query) => query.Include(s => s.Features);

    public override string? SlugSource(ServiceEntity entity) => entity.Name;

    public override ServiceInput ToInput(ServiceEntity s) => new()
    {
        Name = s.Name, Slug = s.Slug, CategoryId = s.CategoryId, Icon = s.Icon, ShortDescription = s.ShortDescription,
        Description = s.Description, Deliverables = s.Deliverables,
        Process = s.Process.Select(p => new ServiceProcessStep { Title = p.Title, Description = p.Description, Output = p.Output }).ToList(),
        TechnologyIds = [.. s.TechnologyIds], RelatedProjectIds = [.. s.RelatedProjectIds], CoverMediaId = s.CoverMediaId,
        IsFeatured = s.IsFeatured, SortOrder = s.SortOrder,
        Features = s.Features.OrderBy(f => f.SortOrder)
            .Select(f => new ServiceFeatureInput { Title = f.Title, Description = f.Description, Icon = f.Icon }).ToList(),
        Seo = s.Seo,
    };

    public override async Task ApplyAsync(ServiceEntity s, ServiceInput i, ContentContext ctx, CancellationToken ct)
    {
        s.Name = i.Name.Trim();
        s.Slug = i.Slug?.Trim() ?? string.Empty;
        s.CategoryId = await References.ExistsAsync<ServiceCategory>(ctx, i.CategoryId, "categoryId", "Nhóm dịch vụ", ct);
        s.Icon = Clean(i.Icon);
        s.ShortDescription = Clean(i.ShortDescription);
        s.Description = ctx.Html.Sanitize(i.Description);
        s.Deliverables = ctx.Html.Sanitize(i.Deliverables);
        s.Process = i.Process.Select(p => new ServiceProcessStep
            { Title = p.Title.Trim(), Description = Clean(p.Description), Output = Clean(p.Output) }).ToList();
        s.TechnologyIds = await References.AllExistAsync<Technology>(ctx, i.TechnologyIds, "technologyIds", "Công nghệ", ct);
        s.RelatedProjectIds = await References.AllExistAsync<Project>(ctx, i.RelatedProjectIds, "relatedProjectIds", "Dự án", ct);
        s.CoverMediaId = await References.MediaAsync(ctx, i.CoverMediaId, "coverMediaId", ct);
        s.IsFeatured = i.IsFeatured;
        s.SortOrder = i.SortOrder;
        s.Seo = i.Seo;
        s.Features.Clear();
        s.Features.AddRange(i.Features.Select((f, idx) => new ServiceFeature
            { ServiceId = s.Id, Title = f.Title.Trim(), Description = Clean(f.Description), Icon = Clean(f.Icon), SortOrder = idx }));
    }

    public override Task ValidatePublishAsync(ServiceEntity s, IDictionary<string, string[]> errors, ContentContext ctx,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.ShortDescription))
            errors["shortDescription"] = ["Cần mô tả ngắn trước khi xuất bản."];
        if (string.IsNullOrWhiteSpace(s.Description))
            errors["description"] = ["Trang dịch vụ cần nội dung chi tiết (tránh trang mỏng nội dung)."];
        return Task.CompletedTask;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(ServiceEntity s) => Refs((s.CoverMediaId, "Cover"));

    public override void PrepareDuplicate(ServiceInput input)
    {
        input.Name = $"{input.Name} (bản sao)";
        input.Slug = null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
