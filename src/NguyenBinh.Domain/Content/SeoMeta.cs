namespace NguyenBinh.Domain.Content;

/// <summary>
/// SEO rieng cua tung noi dung (muc 27), luu JSON ngay tren bang cua entity de luu/khoi phuc phien ban
/// cung luc voi noi dung. Truong trong → dung gia tri tu sinh tu noi dung / SEO mac dinh.
/// </summary>
public class SeoMeta
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public Guid? OgImageId { get; set; }
    public Guid? TwitterImageId { get; set; }

    /// <summary>vd "index,follow" (mac dinh) hoac "noindex,follow".</summary>
    public string? Robots { get; set; }

    /// <summary>JSON-LD ghi de (tuy chon), phai la JSON hop le.</summary>
    public string? SchemaJson { get; set; }

    public bool ExcludeFromSitemap { get; set; }
}

public interface IHasSeo
{
    SeoMeta Seo { get; set; }
}

public interface ISortable
{
    int SortOrder { get; set; }
}

public interface IHasSlug
{
    string Slug { get; set; }
}
