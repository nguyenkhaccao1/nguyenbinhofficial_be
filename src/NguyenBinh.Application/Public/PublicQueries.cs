using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.Application.Public;

/// <summary>Quy tac hien thi public dung chung (noi dung da xuat ban, quyen cong bo cua du an...).</summary>
internal static partial class PublicQueries
{
    /// <summary>Published, hoac Scheduled va da toi gio (khong phu thuoc job doi trang thai).</summary>
    public static IQueryable<T> Visible<T>(this IQueryable<T> query, DateTimeOffset now) where T : ContentEntity =>
        query.Where(e => e.Status == ContentStatus.Published || (e.Status == ContentStatus.Scheduled && e.PublishAt <= now));

    public static Expression<Func<Project, bool>> IsVisibleProject(DateTimeOffset now) =>
        e => e.Status == ContentStatus.Published || (e.Status == ContentStatus.Scheduled && e.PublishAt <= now);

    public static PublicSeo ToSeo(SeoMeta? seo, IReadOnlyDictionary<Guid, PublicImage> media) => new(
        Clean(seo?.Title), Clean(seo?.Description), Clean(seo?.CanonicalUrl), Clean(seo?.Robots), Clean(seo?.OgTitle),
        Clean(seo?.OgDescription),
        seo?.OgImageId is { } og && media.TryGetValue(og, out var ogImg) ? ogImg.Url : null,
        seo?.TwitterImageId is { } tw && media.TryGetValue(tw, out var twImg) ? twImg.Url : null,
        Clean(seo?.SchemaJson));

    /// <summary>Anh dai dien cua du an — chi khi duoc phep cong bo anh man hinh.</summary>
    public static Guid? CardImageId(Project p) => p.CanShowScreenshots ? p.ThumbnailMediaId ?? p.CoverMediaId : null;

    public static ProjectCard ToCard(Project p, IReadOnlyDictionary<Guid, PublicImage> media)
    {
        var own = p.OwnershipType == OwnershipType.NguyenBinhOwned;
        var image = CardImageId(p) is { } id && media.TryGetValue(id, out var img) ? img : null;
        var year = (p.LaunchDate ?? p.EndDate ?? p.StartDate)?.Year;
        var technologies = p.CanShowTechnology
            ? p.Technologies.OrderBy(t => t.SortOrder).Where(t => t.Technology != null).Select(t => t.Technology!.Name).ToList()
            : [];

        return new ProjectCard(p.Id, p.Name, p.Slug, p.ShortDescription, p.ShortResult, p.Industry?.Name, p.PrimaryContentType,
            p.ContentTypes, p.ProjectRoles, own, own ? null : p.PublicCreditText, technologies, image, year,
            Excerpt(p.Problem, 240), Excerpt(p.Solution, 240), Excerpt(p.Result, 240));
    }

    public static string? Excerpt(string? html, int max)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = WebUtility.HtmlDecode(Spaces().Replace(Tags().Replace(html, " "), " ")).Trim();
        if (text.Length <= max) return text.Length == 0 ? null : text;
        var cut = text[..max];
        var space = cut.LastIndexOf(' ');
        return (space > max / 2 ? cut[..space] : cut).TrimEnd(',', '.', ';', ':') + "…";
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static PublicImage? Image(IReadOnlyDictionary<Guid, PublicImage> media, Guid? id) =>
        id is { } key && media.TryGetValue(key, out var img) ? img : null;

    public static string? FileUrl(IReadOnlyDictionary<Guid, PublicImage> media, Guid? id, MediaKind expected) =>
        id is { } key && media.TryGetValue(key, out var img) && expected != MediaKind.Image ? img.Url : null;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
