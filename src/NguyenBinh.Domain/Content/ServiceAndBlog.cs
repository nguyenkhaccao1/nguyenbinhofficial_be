using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Content;

/// <summary>Dich vu phat trien (muc 18) — /dich-vu/{slug}.</summary>
public class Service : ContentEntity, IHasSlug, IHasSeo, ISortable
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public ServiceCategory? Category { get; set; }
    public string? Icon { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }

    /// <summary>Dau ra ban giao cua dich vu.</summary>
    public string? Deliverables { get; set; }

    public List<ServiceProcessStep> Process { get; set; } = [];
    public List<Guid> TechnologyIds { get; set; } = [];
    public List<Guid> RelatedProjectIds { get; set; } = [];
    public Guid? CoverMediaId { get; set; }
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public SeoMeta Seo { get; set; } = new();

    public List<ServiceFeature> Features { get; set; } = [];
}

public class ServiceProcessStep
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Dau ra cua buoc (muc 14 — khong mo ta chung chung).</summary>
    public string? Output { get; set; }
}

public class ServiceFeature : Entity
{
    public Guid ServiceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Bai viet blog / knowledge hub (muc 19).</summary>
public class Post : ContentEntity, IHasSlug, IHasSeo
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }

    /// <summary>HTML da sanitize — duoc render tren web.</summary>
    public string? ContentHtml { get; set; }

    public Guid? CoverMediaId { get; set; }
    public Guid? AuthorId { get; set; }
    public Author? Author { get; set; }
    public int ReadingMinutes { get; set; }
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
    public SeoMeta Seo { get; set; } = new();

    public List<PostCategoryMapping> Categories { get; set; } = [];
    public List<PostTag> Tags { get; set; } = [];
}

public class PostCategoryMapping
{
    public Guid PostId { get; set; }
    public Guid CategoryId { get; set; }
    public PostCategory? Category { get; set; }
    public bool IsPrimary { get; set; }
}

public class PostTag
{
    public Guid PostId { get; set; }
    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }
}
