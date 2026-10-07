using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Content;

/// <summary>
/// Du an / case study (muc 5, 7, 15, 24, 25). Phan tach ro "Nguyen Binh so huu" va
/// "Nguyen Binh duoc thue phat trien" qua OwnershipType + ProjectRoles + PublicCreditText.
/// </summary>
public class Project : ContentEntity, IHasSlug, IHasSeo, ISortable
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }

    /// <summary>1 cau ket qua cho card (khong bia so lieu).</summary>
    public string? ShortResult { get; set; }

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid? IndustryId { get; set; }
    public Industry? Industry { get; set; }

    /// <summary>Case study trien khai san pham cua Nguyen Binh (vd POS) → lien ket sang trang san pham.</summary>
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    // --- Phan loai ---
    public ProjectContentType PrimaryContentType { get; set; } = ProjectContentType.CustomProject;
    public List<ProjectContentType> ContentTypes { get; set; } = [];
    public CommercialType CommercialType { get; set; } = CommercialType.CustomDevelopment;
    public List<ProjectRole> ProjectRoles { get; set; } = [];

    // --- So huu / credit / quyen cong bo ---
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

    // --- Case study (rich text) ---
    public string? Overview { get; set; }
    public string? Problem { get; set; }
    public string? Requirements { get; set; }
    public string? Solution { get; set; }
    public string? Architecture { get; set; }
    public Guid? ArchitectureMediaId { get; set; }
    public string? Challenge { get; set; }
    public string? ChallengeSolution { get; set; }
    public string? Result { get; set; }

    // --- Thoi gian ---
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? LaunchDate { get; set; }
    public ProjectState ProjectState { get; set; } = ProjectState.Live;

    // --- Link ---
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

    public SeoMeta Seo { get; set; } = new();

    public List<ProjectCategoryMapping> Categories { get; set; } = [];
    public List<ProjectTechnologyMapping> Technologies { get; set; } = [];
    public List<ProjectFeature> Features { get; set; } = [];
    public List<ProjectMedia> Media { get; set; } = [];
    public List<ProjectMetric> Metrics { get; set; } = [];
    public List<ProjectLink> Links { get; set; } = [];
}

public class ProjectCategoryMapping
{
    public Guid ProjectId { get; set; }
    public Guid CategoryId { get; set; }
    public ProjectCategory? Category { get; set; }
}

public class ProjectTechnologyMapping
{
    public Guid ProjectId { get; set; }
    public Guid TechnologyId { get; set; }
    public Technology? Technology { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public class ProjectFeature : Entity
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public Guid? MediaId { get; set; }

    /// <summary>Chi cong bo chuc nang duoc phep cong bo (vd PerfectKey Workforce).</summary>
    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }
}

public class ProjectMedia : Entity
{
    public Guid ProjectId { get; set; }
    public ProjectMediaKind Kind { get; set; }
    public Guid? MediaId { get; set; }

    /// <summary>YouTube/Vimeo/video ngoai.</summary>
    public string? ExternalUrl { get; set; }
    public string? Caption { get; set; }
    public string? Alt { get; set; }

    /// <summary>Ghep cap before/after.</summary>
    public string? GroupKey { get; set; }
    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }
}

public class ProjectMetric : Entity
{
    public Guid ProjectId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class ProjectLink : Entity
{
    public Guid ProjectId { get; set; }
    public ProjectLinkKind Kind { get; set; }
    public string? Label { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
