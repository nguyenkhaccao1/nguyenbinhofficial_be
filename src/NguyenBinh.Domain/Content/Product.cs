using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Content;

/// <summary>San pham Nguyen Binh so huu / kinh doanh, co landing page rieng /san-pham/{slug} (muc 11, 17).</summary>
public class Product : ContentEntity, IHasSlug, IHasSeo, ISortable
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
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
    public SeoMeta Seo { get; set; } = new();

    public List<ProductFeature> Features { get; set; } = [];
    public List<ProductModule> Modules { get; set; } = [];
    public List<ProductMedia> Media { get; set; } = [];
    public List<ProductPlan> Plans { get; set; } = [];
    public List<ProductFaq> Faqs { get; set; } = [];
}

public class ProductFeature : Entity
{
    public Guid ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public Guid? MediaId { get; set; }
    public string? Group { get; set; }
    public int SortOrder { get; set; }
}

public class ProductModule : Entity
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public Guid? MediaId { get; set; }
    public List<string> Items { get; set; } = [];
    public int SortOrder { get; set; }
}

public class ProductMedia : Entity
{
    public Guid ProductId { get; set; }
    public ProjectMediaKind Kind { get; set; }
    public Guid? MediaId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Caption { get; set; }
    public string? Alt { get; set; }

    /// <summary>Nhom tab man hinh (vd "Ban hang", "Bep").</summary>
    public string? GroupKey { get; set; }
    public int SortOrder { get; set; }
}

public class ProductPlan : Entity
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? PriceAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Contact;
    public string? PriceNote { get; set; }
    public List<string> Features { get; set; } = [];
    public bool IsHighlighted { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public int SortOrder { get; set; }
}

public class ProductFaq : Entity
{
    public Guid ProductId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
