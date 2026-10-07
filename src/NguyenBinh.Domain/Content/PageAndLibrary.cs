using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Content;

/// <summary>Page builder: Page → Section → Block (muc 22). Duong dan (Path) duy nhat, "/" la trang chu.</summary>
public class Page : ContentEntity, IHasSeo
{
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public PageType PageType { get; set; } = PageType.Standard;
    public Guid? IndustryId { get; set; }
    public Industry? Industry { get; set; }
    public SeoMeta Seo { get; set; } = new();
    public List<PageSection> Sections { get; set; } = [];
}

public class PageSection : Entity
{
    public Guid PageId { get; set; }
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>JSON: tone (light/subtle/dark), width, padding, alignment, background, an tren mobile/desktop, animation, customClass, anchorId.</summary>
    public string SettingsJson { get; set; } = "{}";

    public List<PageBlock> Blocks { get; set; } = [];
}

public class PageBlock : Entity
{
    public Guid SectionId { get; set; }

    /// <summary>Loai block (HERO, PROJECTS...) — xem BlockTypes. Luu chuoi de them block moi khong can migration.</summary>
    public string Type { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>Noi dung block. Block dong (PROJECTS, BLOG...) chi luu truy van, du lieu resolve luc render.</summary>
    public string DataJson { get; set; } = "{}";
    public string SettingsJson { get; set; } = "{}";
}

public class Testimonial : ContentEntity, ISortable
{
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorTitle { get; set; }
    public string? Company { get; set; }
    public Guid? AvatarMediaId { get; set; }
    public string Quote { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProductId { get; set; }
    public int SortOrder { get; set; }
}

public class Partner : ContentEntity, ISortable
{
    public string Name { get; set; } = string.Empty;
    public Guid? LogoMediaId { get; set; }
    public string? Url { get; set; }
    public string? Kind { get; set; }
    public int SortOrder { get; set; }
}

public class TeamMember : ContentEntity, ISortable
{
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Guid? PhotoMediaId { get; set; }
    public string? Bio { get; set; }
    public List<string> Links { get; set; } = [];
    public int SortOrder { get; set; }
}

public class Faq : ContentEntity, ISortable
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public FaqScope Scope { get; set; } = FaqScope.Global;
    public Guid? ScopeId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Menu header/footer (muc 62, 63). Item co the tro toi URL hoac sinh dong (megamenu).</summary>
public class Menu : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<MenuItem> Items { get; set; } = [];
}

public class MenuItem : Entity
{
    public Guid MenuId { get; set; }
    public Guid? ParentId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Description { get; set; }
    public bool OpenInNewTab { get; set; }

    /// <summary>Megamenu sinh tu CMS: PRODUCTS, SERVICES, SOLUTIONS (null = item thuong).</summary>
    public string? DynamicSource { get; set; }

    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}
