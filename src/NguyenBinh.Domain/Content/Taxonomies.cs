using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Content;

/// <summary>Danh muc dung chung — mac dinh Published (khong can quy trinh duyet).</summary>
public abstract class TaxonomyEntity : ContentEntity, IHasSlug, ISortable
{
    protected TaxonomyEntity()
    {
        Status = ContentStatus.Published;
    }

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class Industry : TaxonomyEntity, IHasSeo
{
    public string? Icon { get; set; }
    public SeoMeta Seo { get; set; } = new();
}

public class Technology : TaxonomyEntity
{
    public TechnologyGroup Group { get; set; }
    public Guid? LogoMediaId { get; set; }
    public string? WebsiteUrl { get; set; }

    /// <summary>Chi hien thi tren /cong-nghe nhung cong nghe thuc su co nang luc (muc 43).</summary>
    public bool ShowOnTechPage { get; set; } = true;
}

public class Client : TaxonomyEntity
{
    public Guid? LogoMediaId { get; set; }
    public string? WebsiteUrl { get; set; }
    public Guid? IndustryId { get; set; }
    public Industry? Industry { get; set; }
}

public class ProjectCategory : TaxonomyEntity, IHasSeo
{
    public SeoMeta Seo { get; set; } = new();
}

public class ProductCategory : TaxonomyEntity, IHasSeo
{
    public SeoMeta Seo { get; set; } = new();
}

public class ServiceCategory : TaxonomyEntity, IHasSeo
{
    public SeoMeta Seo { get; set; } = new();
}

public class PostCategory : TaxonomyEntity, IHasSeo
{
    public Guid? ParentId { get; set; }
    public SeoMeta Seo { get; set; } = new();
}

public class Tag : TaxonomyEntity;

public class Author : TaxonomyEntity
{
    public string? Title { get; set; }
    public Guid? AvatarMediaId { get; set; }
    public Guid? UserId { get; set; }
    public List<string> Links { get; set; } = [];
}
