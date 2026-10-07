using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Content.Products;

public sealed class ProductInput
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Tagline { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductType ProductType { get; set; } = ProductType.Other;
    public CommercialType CommercialType { get; set; } = CommercialType.ForSale;
    public OwnershipType OwnershipType { get; set; } = OwnershipType.NguyenBinhOwned;
    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public string? TargetUsers { get; set; }
    public string? Integration { get; set; }
    public string? Deployment { get; set; }
    public string? Security { get; set; }
    public Guid? LogoMediaId { get; set; }
    public Guid? HeroMediaId { get; set; }
    public string? DemoVideoUrl { get; set; }
    public string? DemoUrl { get; set; }
    public string? PricingNote { get; set; }
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public List<ProductFeatureInput> Features { get; set; } = [];
    public List<ProductModuleInput> Modules { get; set; } = [];
    public List<ProductMediaInput> Media { get; set; } = [];
    public List<ProductPlanInput> Plans { get; set; } = [];
    public List<ProductFaqInput> Faqs { get; set; } = [];
    public SeoMeta Seo { get; set; } = new();
}

public sealed class ProductFeatureInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public Guid? MediaId { get; set; }
    public string? Group { get; set; }
}

public sealed class ProductModuleInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public Guid? MediaId { get; set; }
    public List<string> Items { get; set; } = [];
}

public sealed class ProductMediaInput
{
    public ProjectMediaKind Kind { get; set; } = ProjectMediaKind.Desktop;
    public Guid? MediaId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Caption { get; set; }
    public string? Alt { get; set; }
    public string? GroupKey { get; set; }
}

public sealed class ProductPlanInput
{
    public string Name { get; set; } = string.Empty;
    public decimal? PriceAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Contact;
    public string? PriceNote { get; set; }
    public List<string> Features { get; set; } = [];
    public bool IsHighlighted { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
}

public sealed class ProductFaqInput
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public sealed record ProductListItem(
    Guid Id,
    string Name,
    string Slug,
    string? Tagline,
    ProductType ProductType,
    CommercialType CommercialType,
    OwnershipType OwnershipType,
    string? CategoryName,
    Guid? LogoMediaId,
    bool IsFeatured,
    int SortOrder,
    ContentStatus Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? UpdatedAt);

internal sealed class ProductInputValidator : AbstractValidator<ProductInput>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên sản phẩm.").MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Tagline).MaximumLength(300);
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.PricingNote).MaximumLength(500);
        RuleFor(x => x.DemoVideoUrl).HttpUrl();
        RuleFor(x => x.DemoUrl).HttpUrl();
        RuleForEach(x => x.Features).ChildRules(f =>
        {
            f.RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên tính năng.").MaximumLength(200);
            f.RuleFor(x => x.Description).MaximumLength(1000);
        });
        RuleForEach(x => x.Modules).ChildRules(m =>
        {
            m.RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên module.").MaximumLength(200);
            m.RuleFor(x => x.Items).Must(i => i.Count <= 50).WithMessage("Tối đa 50 chức năng mỗi module.");
        });
        RuleForEach(x => x.Media).ChildRules(m =>
        {
            m.RuleFor(x => x.MediaId).NotNull().When(x => string.IsNullOrEmpty(x.ExternalUrl))
                .WithMessage("Chọn file hoặc nhập link video.");
            m.RuleFor(x => x.ExternalUrl).HttpUrl();
        });
        RuleForEach(x => x.Plans).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên gói.").MaximumLength(100);
            p.RuleFor(x => x.PriceAmount).GreaterThanOrEqualTo(0).When(x => x.PriceAmount is not null);
            p.RuleFor(x => x.PriceAmount).Null().When(x => x.BillingPeriod == BillingPeriod.Contact)
                .WithMessage("Gói 'Liên hệ' không nhập giá.");
            p.RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
            p.RuleFor(x => x.CtaUrl).MaximumLength(500);
        });
        RuleForEach(x => x.Faqs).ChildRules(f =>
        {
            f.RuleFor(x => x.Question).NotEmpty().WithMessage("Nhập câu hỏi.").MaximumLength(300);
            f.RuleFor(x => x.Answer).NotEmpty().WithMessage("Nhập câu trả lời.").MaximumLength(5000);
        });
        RuleFor(x => x.Seo).SetValidator(new SeoMetaValidator());
    }
}

public sealed class ProductContentModule : ContentModule<Product, ProductListItem, ProductInput>
{
    public override string Label => "sản phẩm";
    public override string DefaultSort => "sortOrder,name";

    public override SortMap<Product> Sorts { get; } = new SortMap<Product>()
        .Add("name", p => p.Name)
        .Add("sortOrder", p => p.SortOrder)
        .Add("status", p => p.Status)
        .Add("updatedAt", p => p.UpdatedAt);

    public override Expression<Func<Product, ProductListItem>> ListProjection => p => new ProductListItem(
        p.Id, p.Name, p.Slug, p.Tagline, p.ProductType, p.CommercialType, p.OwnershipType,
        p.Category != null ? p.Category.Name : null, p.LogoMediaId, p.IsFeatured, p.SortOrder, p.Status,
        p.PublishAt, p.UpdatedAt);

    public override IQueryable<Product> ApplySearch(IQueryable<Product> query, string search) =>
        query.Where(p => p.Name.Contains(search) || p.Slug.Contains(search) ||
                         (p.Tagline != null && p.Tagline.Contains(search)));

    public override IQueryable<Product> ApplyFilters(IQueryable<Product> query, IReadOnlyDictionary<string, string> f)
    {
        if (f.TryGetValue("productType", out var t) && EnumParser.TryParse<ProductType>(t, out var type))
            query = query.Where(p => p.ProductType == type);
        if (f.TryGetValue("categoryId", out var c) && Guid.TryParse(c, out var categoryId))
            query = query.Where(p => p.CategoryId == categoryId);
        return query;
    }

    public override IQueryable<Product> IncludeDetails(IQueryable<Product> query) => query
        .Include(p => p.Features).Include(p => p.Modules).Include(p => p.Media).Include(p => p.Plans)
        .Include(p => p.Faqs);

    public override string? SlugSource(Product entity) => entity.Name;

    public override ProductInput ToInput(Product p) => new()
    {
        Name = p.Name, Slug = p.Slug, Tagline = p.Tagline, ShortDescription = p.ShortDescription,
        Description = p.Description, CategoryId = p.CategoryId, ProductType = p.ProductType,
        CommercialType = p.CommercialType, OwnershipType = p.OwnershipType, Problem = p.Problem,
        Solution = p.Solution, TargetUsers = p.TargetUsers, Integration = p.Integration, Deployment = p.Deployment,
        Security = p.Security, LogoMediaId = p.LogoMediaId, HeroMediaId = p.HeroMediaId,
        DemoVideoUrl = p.DemoVideoUrl, DemoUrl = p.DemoUrl, PricingNote = p.PricingNote, IsFeatured = p.IsFeatured,
        SortOrder = p.SortOrder,
        Features = p.Features.OrderBy(f => f.SortOrder).Select(f => new ProductFeatureInput
            { Title = f.Title, Description = f.Description, Icon = f.Icon, MediaId = f.MediaId, Group = f.Group }).ToList(),
        Modules = p.Modules.OrderBy(m => m.SortOrder).Select(m => new ProductModuleInput
            { Name = m.Name, Description = m.Description, Icon = m.Icon, MediaId = m.MediaId, Items = [.. m.Items] }).ToList(),
        Media = p.Media.OrderBy(m => m.SortOrder).Select(m => new ProductMediaInput
        {
            Kind = m.Kind, MediaId = m.MediaId, ExternalUrl = m.ExternalUrl, Caption = m.Caption, Alt = m.Alt,
            GroupKey = m.GroupKey,
        }).ToList(),
        Plans = p.Plans.OrderBy(x => x.SortOrder).Select(x => new ProductPlanInput
        {
            Name = x.Name, PriceAmount = x.PriceAmount, Currency = x.Currency, BillingPeriod = x.BillingPeriod,
            PriceNote = x.PriceNote, Features = [.. x.Features], IsHighlighted = x.IsHighlighted, CtaLabel = x.CtaLabel,
            CtaUrl = x.CtaUrl,
        }).ToList(),
        Faqs = p.Faqs.OrderBy(f => f.SortOrder).Select(f => new ProductFaqInput { Question = f.Question, Answer = f.Answer })
            .ToList(),
        Seo = p.Seo,
    };

    public override async Task ApplyAsync(Product p, ProductInput i, ContentContext ctx, CancellationToken ct)
    {
        p.Name = i.Name.Trim();
        p.Slug = i.Slug?.Trim() ?? string.Empty;
        p.Tagline = Clean(i.Tagline);
        p.ShortDescription = Clean(i.ShortDescription);
        p.Description = ctx.Html.Sanitize(i.Description);
        p.CategoryId = await References.ExistsAsync<ProductCategory>(ctx, i.CategoryId, "categoryId", "Danh mục", ct);
        p.ProductType = i.ProductType;
        p.CommercialType = i.CommercialType;
        p.OwnershipType = i.OwnershipType;
        p.Problem = ctx.Html.Sanitize(i.Problem);
        p.Solution = ctx.Html.Sanitize(i.Solution);
        p.TargetUsers = ctx.Html.Sanitize(i.TargetUsers);
        p.Integration = ctx.Html.Sanitize(i.Integration);
        p.Deployment = ctx.Html.Sanitize(i.Deployment);
        p.Security = ctx.Html.Sanitize(i.Security);
        p.LogoMediaId = await References.MediaAsync(ctx, i.LogoMediaId, "logoMediaId", ct);
        p.HeroMediaId = await References.MediaAsync(ctx, i.HeroMediaId, "heroMediaId", ct);
        p.DemoVideoUrl = Clean(i.DemoVideoUrl);
        p.DemoUrl = Clean(i.DemoUrl);
        p.PricingNote = Clean(i.PricingNote);
        p.IsFeatured = i.IsFeatured;
        p.SortOrder = i.SortOrder;
        p.Seo = i.Seo;

        await References.EnsureMediaAsync(ctx,
            i.Features.Select(f => f.MediaId).Concat(i.Modules.Select(m => m.MediaId)).Concat(i.Media.Select(m => m.MediaId))
                .Where(id => id != null).Select(id => id!.Value), "media", ct);

        p.Features.Clear();
        p.Features.AddRange(i.Features.Select((f, idx) => new ProductFeature
        {
            ProductId = p.Id, Title = f.Title.Trim(), Description = Clean(f.Description), Icon = Clean(f.Icon),
            MediaId = f.MediaId, Group = Clean(f.Group), SortOrder = idx,
        }));
        p.Modules.Clear();
        p.Modules.AddRange(i.Modules.Select((m, idx) => new ProductModule
        {
            ProductId = p.Id, Name = m.Name.Trim(), Description = Clean(m.Description), Icon = Clean(m.Icon),
            MediaId = m.MediaId, Items = m.Items.Select(x => x.Trim()).Where(x => x.Length > 0).ToList(), SortOrder = idx,
        }));
        p.Media.Clear();
        p.Media.AddRange(i.Media.Select((m, idx) => new ProductMedia
        {
            ProductId = p.Id, Kind = m.Kind, MediaId = m.MediaId, ExternalUrl = Clean(m.ExternalUrl),
            Caption = Clean(m.Caption), Alt = Clean(m.Alt), GroupKey = Clean(m.GroupKey), SortOrder = idx,
        }));
        p.Plans.Clear();
        p.Plans.AddRange(i.Plans.Select((x, idx) => new ProductPlan
        {
            ProductId = p.Id, Name = x.Name.Trim(), PriceAmount = x.PriceAmount, Currency = x.Currency.Trim().ToUpperInvariant(),
            BillingPeriod = x.BillingPeriod, PriceNote = Clean(x.PriceNote),
            Features = x.Features.Select(f => f.Trim()).Where(f => f.Length > 0).ToList(), IsHighlighted = x.IsHighlighted,
            CtaLabel = Clean(x.CtaLabel), CtaUrl = Clean(x.CtaUrl), SortOrder = idx,
        }));
        p.Faqs.Clear();
        p.Faqs.AddRange(i.Faqs.Select((f, idx) => new ProductFaq
            { ProductId = p.Id, Question = f.Question.Trim(), Answer = f.Answer.Trim(), SortOrder = idx }));
    }

    public override Task ValidatePublishAsync(Product p, IDictionary<string, string[]> errors, ContentContext ctx,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.ShortDescription))
            errors["shortDescription"] = ["Cần mô tả ngắn trước khi xuất bản."];
        if (string.IsNullOrWhiteSpace(p.Tagline))
            errors["tagline"] = ["Cần thông điệp chính (tagline) cho trang sản phẩm."];
        return Task.CompletedTask;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Product p) =>
        Refs((p.LogoMediaId, "Logo"), (p.HeroMediaId, "Hero"))
            .Concat(p.Features.Where(f => f.MediaId != null).Select(f => new MediaUsageRef(f.MediaId!.Value, "Feature")))
            .Concat(p.Modules.Where(m => m.MediaId != null).Select(m => new MediaUsageRef(m.MediaId!.Value, "Module")))
            .Concat(p.Media.Where(m => m.MediaId != null).Select(m => new MediaUsageRef(m.MediaId!.Value, $"Media:{m.Kind}")))
            .Distinct();

    public override void PrepareDuplicate(ProductInput input)
    {
        input.Name = $"{input.Name} (bản sao)";
        input.Slug = null;
        input.IsFeatured = false;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
