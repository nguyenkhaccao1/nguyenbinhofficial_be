using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Content.Taxonomies;

/// <summary>
/// Du lieu form chung cho moi danh muc. Moi loai chi dung cac truong lien quan
/// (vd Technology dung Group/LogoMediaId, Author dung Title/AvatarMediaId/Links).
/// </summary>
public sealed class TaxonomyInput
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public string? Icon { get; set; }
    public TechnologyGroup? Group { get; set; }
    public Guid? LogoMediaId { get; set; }
    public string? WebsiteUrl { get; set; }
    public bool ShowOnTechPage { get; set; } = true;
    public Guid? IndustryId { get; set; }
    public Guid? ParentId { get; set; }
    public string? Title { get; set; }
    public Guid? AvatarMediaId { get; set; }
    public List<string> Links { get; set; } = [];
    public SeoMeta? Seo { get; set; }
}

public sealed record TaxonomyListItem(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    ContentStatus Status,
    string? Extra,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset CreatedAt);

internal sealed class TaxonomyInputValidator : AbstractValidator<TaxonomyInput>
{
    public TaxonomyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên.").MaximumLength(150);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Icon).MaximumLength(100);
        RuleFor(x => x.Title).MaximumLength(150);
        RuleFor(x => x.WebsiteUrl).HttpUrl();
        RuleFor(x => x.Links).Must(l => l.Count <= 10).WithMessage("Tối đa 10 liên kết.");
        RuleForEach(x => x.Links).HttpUrl();
        RuleFor(x => x.Seo!).SetValidator(new SeoMetaValidator()).When(x => x.Seo is not null);
    }
}

/// <summary>Co so chung: ten, slug, mo ta, thu tu, SEO (neu co).</summary>
public abstract class TaxonomyModule<TEntity> : ContentModule<TEntity, TaxonomyListItem, TaxonomyInput>
    where TEntity : TaxonomyEntity, new()
{
    public override string DefaultSort => "sortOrder,name";

    public override SortMap<TEntity> Sorts { get; } = new SortMap<TEntity>()
        .Add("name", e => e.Name)
        .Add("sortOrder", e => e.SortOrder)
        .Add("updatedAt", e => e.UpdatedAt)
        .Add("createdAt", e => e.CreatedAt);

    public override Expression<Func<TEntity, TaxonomyListItem>> ListProjection =>
        e => new TaxonomyListItem(e.Id, e.Name, e.Slug, e.Description, e.SortOrder, e.Status, null, e.UpdatedAt,
            e.CreatedAt);

    public override IQueryable<TEntity> ApplySearch(IQueryable<TEntity> query, string search) =>
        query.Where(e => e.Name.Contains(search) || e.Slug.Contains(search));

    public override string? SlugSource(TEntity entity) => entity.Name;

    public override TaxonomyInput ToInput(TEntity e)
    {
        var input = new TaxonomyInput
        {
            Name = e.Name, Slug = e.Slug, Description = e.Description, SortOrder = e.SortOrder,
            Seo = (e as IHasSeo)?.Seo,
        };
        Map(e, input);
        return input;
    }

    public override async Task ApplyAsync(TEntity e, TaxonomyInput input, ContentContext ctx, CancellationToken ct)
    {
        e.Name = input.Name.Trim();
        e.Slug = input.Slug?.Trim() ?? string.Empty;
        e.Description = Clean(input.Description);
        e.SortOrder = input.SortOrder;
        if (e is IHasSeo seo) seo.Seo = input.Seo ?? new SeoMeta();
        await ApplyExtraAsync(e, input, ctx, ct);
    }

    public override void PrepareDuplicate(TaxonomyInput input)
    {
        input.Name = $"{input.Name} (bản sao)";
        input.Slug = null;
    }

    protected virtual void Map(TEntity e, TaxonomyInput input) { }

    protected virtual Task ApplyExtraAsync(TEntity e, TaxonomyInput input, ContentContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    protected static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class IndustryModule : TaxonomyModule<Industry>
{
    public override string Label => "ngành";
    protected override void Map(Industry e, TaxonomyInput i) => i.Icon = e.Icon;

    protected override Task ApplyExtraAsync(Industry e, TaxonomyInput i, ContentContext ctx, CancellationToken ct)
    {
        e.Icon = Clean(i.Icon);
        return Task.CompletedTask;
    }
}

public sealed class TechnologyModule : TaxonomyModule<Technology>
{
    public override string Label => "công nghệ";

    public override Expression<Func<Technology, TaxonomyListItem>> ListProjection =>
        e => new TaxonomyListItem(e.Id, e.Name, e.Slug, e.Description, e.SortOrder, e.Status, e.Group.ToString(),
            e.UpdatedAt, e.CreatedAt);

    public override IQueryable<Technology> ApplyFilters(IQueryable<Technology> query, IReadOnlyDictionary<string, string> f) =>
        f.TryGetValue("group", out var g) && EnumParser.TryParse<TechnologyGroup>(g, out var group)
            ? query.Where(e => e.Group == group)
            : query;

    protected override void Map(Technology e, TaxonomyInput i)
    {
        i.Group = e.Group;
        i.LogoMediaId = e.LogoMediaId;
        i.WebsiteUrl = e.WebsiteUrl;
        i.ShowOnTechPage = e.ShowOnTechPage;
    }

    protected override async Task ApplyExtraAsync(Technology e, TaxonomyInput i, ContentContext ctx, CancellationToken ct)
    {
        e.Group = i.Group ?? TechnologyGroup.Other;
        e.LogoMediaId = await References.MediaAsync(ctx, i.LogoMediaId, "logoMediaId", ct);
        e.WebsiteUrl = Clean(i.WebsiteUrl);
        e.ShowOnTechPage = i.ShowOnTechPage;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Technology e) => Refs((e.LogoMediaId, "Logo"));
}

public sealed class ClientModule : TaxonomyModule<Client>
{
    public override string Label => "khách hàng";

    protected override void Map(Client e, TaxonomyInput i)
    {
        i.LogoMediaId = e.LogoMediaId;
        i.WebsiteUrl = e.WebsiteUrl;
        i.IndustryId = e.IndustryId;
    }

    protected override async Task ApplyExtraAsync(Client e, TaxonomyInput i, ContentContext ctx, CancellationToken ct)
    {
        e.LogoMediaId = await References.MediaAsync(ctx, i.LogoMediaId, "logoMediaId", ct);
        e.WebsiteUrl = Clean(i.WebsiteUrl);
        e.IndustryId = await References.ExistsAsync<Industry>(ctx, i.IndustryId, "industryId", "Ngành", ct);
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Client e) => Refs((e.LogoMediaId, "Logo"));
}

public sealed class ProjectCategoryModule : TaxonomyModule<ProjectCategory>
{
    public override string Label => "danh mục dự án";
}

public sealed class ProductCategoryModule : TaxonomyModule<ProductCategory>
{
    public override string Label => "danh mục sản phẩm";
}

public sealed class ServiceCategoryModule : TaxonomyModule<ServiceCategory>
{
    public override string Label => "nhóm dịch vụ";
}

public sealed class PostCategoryModule : TaxonomyModule<PostCategory>
{
    public override string Label => "danh mục blog";

    protected override void Map(PostCategory e, TaxonomyInput i) => i.ParentId = e.ParentId;

    protected override async Task ApplyExtraAsync(PostCategory e, TaxonomyInput i, ContentContext ctx, CancellationToken ct)
    {
        if (i.ParentId == e.Id && e.Id != Guid.Empty)
            throw new BusinessValidationException("parentId", "Danh mục cha không thể là chính nó.");
        e.ParentId = await References.ExistsAsync<PostCategory>(ctx, i.ParentId, "parentId", "Danh mục cha", ct);
    }

    /// <summary>/blog/{slug} dung chung cho danh muc va bai viet.</summary>
    public override Task<bool> SlugTakenElsewhereAsync(string slug, Guid id, ContentContext ctx, CancellationToken ct) =>
        ctx.Db.Set<Post>().AnyAsync(p => p.Slug == slug, ct);
}

public sealed class TagModule : TaxonomyModule<Tag>
{
    public override string Label => "tag";
}

public sealed class AuthorModule : TaxonomyModule<Author>
{
    public override string Label => "tác giả";

    protected override void Map(Author e, TaxonomyInput i)
    {
        i.Title = e.Title;
        i.AvatarMediaId = e.AvatarMediaId;
        i.Links = [.. e.Links];
    }

    protected override async Task ApplyExtraAsync(Author e, TaxonomyInput i, ContentContext ctx, CancellationToken ct)
    {
        e.Title = Clean(i.Title);
        e.AvatarMediaId = await References.MediaAsync(ctx, i.AvatarMediaId, "avatarMediaId", ct);
        e.Links = i.Links.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Author e) => Refs((e.AvatarMediaId, "Avatar"));
}
