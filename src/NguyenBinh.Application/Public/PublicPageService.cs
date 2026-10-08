using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Content.Pages;
using NguyenBinh.Domain.Content;
using static NguyenBinh.Application.Public.PublicQueries;

namespace NguyenBinh.Application.Public;

/// <summary>Dieu huong + trang page builder (block dong duoc resolve du lieu tai server).</summary>
internal sealed partial class PublicContentService
{
    // ======================= Dieu huong =======================

    public Task<NavigationDto> NavigationAsync(CancellationToken ct = default) =>
        Cached("navigation", async token =>
        {
            var menus = await db.Set<Menu>().AsNoTracking().Include(m => m.Items).ToListAsync(token);
            var products = (await ProductsAsync(token))
                .Select(p => new NavMegaItem(p.Name, $"/san-pham/{p.Slug}", p.Tagline ?? p.ShortDescription, null)).ToList();
            var services = (await ServicesAsync(token))
                .SelectMany(g => g.Services.Select(s => new NavMegaItem(s.Name, $"/dich-vu/{s.Slug}", s.ShortDescription, s.Icon, g.CategoryName)))
                .ToList();
            var solutions = (await IndustriesAsync(token)).Where(i => i.SolutionPath is not null)
                .Select(i => new NavMegaItem(i.Name, i.SolutionPath!, i.Description, i.Icon)).ToList();
            var featured = (await ProjectsAsync(new ProjectQuery { PageSize = 6 }, token)).Items
                .Select(p => new NavMegaItem(p.Name, $"/du-an/{p.Slug}", p.IndustryName, null)).ToList();

            IReadOnlyList<NavMegaItem> Mega(string? source) => source switch
            {
                "PRODUCTS" => products,
                "SERVICES" => services,
                "SOLUTIONS" => solutions,
                "PROJECTS" => featured,
                _ => [],
            };

            List<NavItem> Items(string code)
            {
                var menu = menus.FirstOrDefault(m => m.Code == code);
                if (menu is null) return [];
                List<NavItem> Build(Guid? parent) => menu.Items.Where(i => i.ParentId == parent && i.IsEnabled).OrderBy(i => i.SortOrder)
                    .Select(i => new NavItem(i.Label, i.Url, i.Description, i.OpenInNewTab, Build(i.Id), i.DynamicSource, Mega(i.DynamicSource)))
                    .ToList();
                return Build(null);
            }

            return new NavigationDto(Items("header"), Items("footer-company"), Items("footer-technology"), Items("footer-legal"),
                products, services, solutions);
        }, ct);

    // ======================= Trang =======================

    public Task<PublicPage?> PageByPathAsync(string path, CancellationToken ct = default)
    {
        var normalized = NormalizePath(path);
        return Cached($"page:{normalized}", async token =>
        {
            var page = await db.Set<Page>().AsNoTracking().Visible(Now).Include(p => p.Industry)
                .Include(p => p.Sections).ThenInclude(s => s.Blocks)
                .FirstOrDefaultAsync(p => p.Path == normalized, token);
            if (page is null) return null;

            var sections = new List<PublicSection>();
            var mediaIds = new HashSet<Guid>();
            if (page.Seo.OgImageId is { } og) mediaIds.Add(og);
            if (page.Seo.TwitterImageId is { } tw) mediaIds.Add(tw);

            foreach (var section in page.Sections.Where(s => s.IsEnabled).OrderBy(s => s.SortOrder))
            {
                var blocks = new List<PublicBlock>();
                CollectMediaIds(JsonNode.Parse(section.SettingsJson), mediaIds);
                foreach (var block in section.Blocks.Where(b => b.IsEnabled).OrderBy(b => b.SortOrder))
                {
                    var data = Parse(block.DataJson);
                    CollectMediaIds(JsonNode.Parse(block.DataJson), mediaIds);
                    var resolved = await ResolveBlockAsync(block.Type, data, page, mediaIds, token);
                    blocks.Add(new PublicBlock(block.Type, data, Parse(block.SettingsJson), resolved));
                }

                if (blocks.Count > 0) sections.Add(new PublicSection(section.Name, Parse(section.SettingsJson), blocks));
            }

            var images = await media.ResolveAsync(mediaIds.Select(id => (Guid?)id), token);
            return new PublicPage(page.Id, page.Title, page.Path, page.PageType,
                page.Industry is null ? null : new NamedLink(page.Industry.Name, page.Industry.Slug),
                sections, images, ToSeo(page.Seo, images), page.UpdatedAt ?? page.CreatedAt);
        }, ct);
    }

    internal static string NormalizePath(string path)
    {
        var p = "/" + (path ?? string.Empty).Trim().Trim('/').ToLowerInvariant();
        return p.Length > 300 ? p[..300] : p;
    }

    /// <summary>Du lieu dong cho block (du an, san pham, dich vu...). Block tinh tra ve null.</summary>
    private async Task<object?> ResolveBlockAsync(string type, JsonElement data, Page page, HashSet<Guid> mediaIds, CancellationToken ct)
    {
        var source = Str(data, "source");
        var limit = Math.Clamp(Int(data, "limit") ?? 6, 1, 24);

        switch (type)
        {
            case BlockTypes.Projects:
            {
                var query = ProjectsWithCardData();
                var ids = Guids(data, "projectIds");
                List<Project> projects;
                if ((source is "manual" or "highlight") && ids.Count > 0)
                {
                    projects = await query.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
                    projects = projects.OrderBy(p => ids.IndexOf(p.Id)).ToList();
                }
                else
                {
                    if (source == "industry" && Guid.TryParse(Str(data, "industryId"), out var industryId))
                        query = query.Where(p => p.IndustryId == industryId);
                    else if (source == "latest") query = query.OrderByDescending(p => p.PublishedAt);
                    else if (page.IndustryId is { } pageIndustry && source == "industryOfPage") query = query.Where(p => p.IndustryId == pageIndustry);
                    else query = query.Where(p => p.IsFeatured);

                    projects = await (source == "latest" ? query : query.OrderBy(p => p.FeaturedOrder).ThenBy(p => p.SortOrder))
                        .Take(source == "highlight" ? 1 : limit).ToListAsync(ct);
                }

                return await ToCardsAsync(projects.Take(source == "highlight" ? 1 : limit).ToList(), ct);
            }
            case BlockTypes.Products:
            {
                var ids = Guids(data, "productIds");
                var query = db.Set<Product>().AsNoTracking().Visible(Now);
                var products = source == "manual" && ids.Count > 0
                    ? (await query.Where(p => ids.Contains(p.Id)).ToListAsync(ct)).OrderBy(p => ids.IndexOf(p.Id)).ToList()
                    : await (source == "all" ? query : query.Where(p => p.IsFeatured)).OrderBy(p => p.SortOrder).Take(limit).ToListAsync(ct);
                return await ToProductCardsAsync(products, ct);
            }
            case BlockTypes.Services:
            {
                var ids = Guids(data, "serviceIds");
                var query = db.Set<Service>().AsNoTracking().Visible(Now).Include(s => s.Category).AsQueryable();
                if (source == "manual" && ids.Count > 0)
                {
                    var manual = await query.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
                    return await ToServiceCardsAsync(manual.OrderBy(s => ids.IndexOf(s.Id)).ToList(), ct);
                }

                if (source == "category" && Guid.TryParse(Str(data, "categoryId"), out var categoryId))
                    query = query.Where(s => s.CategoryId == categoryId);
                else if (source != "all") query = query.Where(s => s.IsFeatured);
                return await ToServiceCardsAsync(await query.OrderBy(s => s.SortOrder).Take(limit).ToListAsync(ct), ct);
            }
            case BlockTypes.Industries:
            {
                var all = await IndustriesAsync(ct);
                var ids = Guids(data, "industryIds");
                return source == "manual" && ids.Count > 0 ? all.Where(i => ids.Contains(i.Id)).ToList() : all;
            }
            case BlockTypes.TechStack:
                return await TechnologiesAsync(ct);
            case BlockTypes.Testimonials:
            {
                var query = db.Set<Testimonial>().AsNoTracking().Visible(Now);
                if (source == "product" && Guid.TryParse(Str(data, "productId"), out var productId))
                    query = query.Where(t => t.ProductId == productId);
                var items = await query.OrderBy(t => t.SortOrder).Take(limit).ToListAsync(ct);
                var images = await media.ResolveAsync(items.Select(t => t.AvatarMediaId), ct);
                return items.Select(t => ToTestimonial(t, images)).ToList();
            }
            case BlockTypes.Team:
            {
                var team = await db.Set<TeamMember>().AsNoTracking().Visible(Now).OrderBy(t => t.SortOrder)
                    .Take(Math.Clamp(Int(data, "limit") ?? 12, 1, 48)).ToListAsync(ct);
                var images = await media.ResolveAsync(team.Select(t => t.PhotoMediaId), ct);
                return team.Select(t => new { t.FullName, t.Title, t.Bio, t.Links, Photo = Image(images, t.PhotoMediaId) }).ToList();
            }
            case BlockTypes.Blog:
            {
                var query = PostsWithCardData();
                if (source == "featured") query = query.Where(p => p.IsFeatured);
                if (source == "category" && Guid.TryParse(Str(data, "categoryId"), out var categoryId))
                    query = query.Where(p => p.Categories.Any(c => c.CategoryId == categoryId));
                return await ToPostCardsAsync(await query.OrderByDescending(p => p.PublishedAt).Take(limit).ToListAsync(ct), ct);
            }
            case BlockTypes.LogoCloud:
                return await LogosAsync(source, ct);
            case BlockTypes.Pricing:
            {
                if (!Guid.TryParse(Str(data, "productId"), out var productId)) return null;
                var product = await db.Set<Product>().AsNoTracking().Visible(Now).Include(p => p.Plans)
                    .FirstOrDefaultAsync(p => p.Id == productId, ct);
                return product?.Plans.OrderBy(p => p.SortOrder).Select(x => new PublicPlan(x.Name, x.PriceAmount, x.Currency,
                    x.BillingPeriod, x.PriceNote, x.Features, x.IsHighlighted, x.CtaLabel, x.CtaUrl)).ToList();
            }
            case BlockTypes.Faq when source == "global":
                return await db.Set<Faq>().AsNoTracking().Visible(Now).Where(f => f.Scope == FaqScope.Global).OrderBy(f => f.SortOrder)
                    .Select(f => new PublicFaq(f.Question, f.Answer)).ToListAsync(ct);
            case BlockTypes.Hero when data.TryGetProperty("visual", out var visual) && Str(visual, "source") == "featuredProjectScreenshots":
                return await FeaturedScreenshotsAsync(Math.Clamp(Int(visual, "limit") ?? 4, 1, 8), ct);
            case BlockTypes.ContactForm when Guid.TryParse(Str(data, "productId"), out var demoProductId):
                return await db.Set<Product>().AsNoTracking().Visible(Now).Where(p => p.Id == demoProductId)
                    .Select(p => new NamedLink(p.Name, p.Slug)).FirstOrDefaultAsync(ct);
            default:
                return null;
        }
    }

    /// <summary>Anh man hinh cua du an noi bat (chi du an cho phep cong bo anh) cho hero montage.</summary>
    private async Task<List<PublicImage>> FeaturedScreenshotsAsync(int limit, CancellationToken ct)
    {
        var projects = await db.Set<Project>().AsNoTracking().Visible(Now).Include(p => p.Media)
            .Where(p => p.IsFeatured && p.CanShowScreenshots).OrderBy(p => p.FeaturedOrder).Take(limit).ToListAsync(ct);
        var preferred = new[] { ProjectMediaKind.Dashboard, ProjectMediaKind.Desktop, ProjectMediaKind.Mobile };
        var ids = projects.Select(p => p.CoverMediaId ?? p.Media.Where(m => m.IsPublic && preferred.Contains(m.Kind))
            .OrderBy(m => Array.IndexOf(preferred, m.Kind)).ThenBy(m => m.SortOrder).Select(m => m.MediaId).FirstOrDefault()).ToList();
        var images = await media.ResolveAsync(ids, ct);
        return ids.Where(id => id.HasValue && images.ContainsKey(id.Value)).Select(id => images[id!.Value]).ToList();
    }

    private async Task<object?> LogosAsync(string? source, CancellationToken ct)
    {
        switch (source)
        {
            case "partners":
            {
                var partners = await db.Set<Partner>().AsNoTracking().Visible(Now).OrderBy(p => p.SortOrder).ToListAsync(ct);
                var images = await media.ResolveAsync(partners.Select(p => p.LogoMediaId), ct);
                return partners.Select(p => new { p.Name, p.Url, Logo = Image(images, p.LogoMediaId) }).ToList();
            }
            case "clients":
            {
                // Chi khach hang cua du an da cong bo VA duoc phep hien logo.
                var clients = await db.Set<Project>().AsNoTracking().Visible(Now)
                    .Where(p => p.CanShowClient && p.CanShowClientLogo && p.Client != null && p.Client.LogoMediaId != null)
                    .Select(p => p.Client!).Distinct().ToListAsync(ct);
                var images = await media.ResolveAsync(clients.Select(c => c.LogoMediaId), ct);
                return clients.Select(c => new { c.Name, Url = c.WebsiteUrl, Logo = Image(images, c.LogoMediaId) }).ToList();
            }
            case "custom":
                return null;
            default:
            {
                var groups = await TechnologiesAsync(ct);
                return groups.SelectMany(g => g.Items).Select(t => new { t.Name, Url = (string?)null, t.Logo }).ToList();
            }
        }
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.Clone();
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s) ? s : null;
    }

    private static List<Guid> Guids(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => Guid.TryParse(x.GetString(), out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList()
            : [];

    /// <summary>Gom moi "mediaId", "...MediaId", "mediaIds" trong JSON block de resolve URL anh mot lan.</summary>
    private static void CollectMediaIds(JsonNode? node, HashSet<Guid> ids)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key.EndsWith("MediaId", StringComparison.OrdinalIgnoreCase) && value is JsonValue v &&
                        v.TryGetValue<string>(out var s) && Guid.TryParse(s, out var g)) ids.Add(g);
                    else if (key.EndsWith("MediaIds", StringComparison.OrdinalIgnoreCase) && value is JsonArray arr)
                    {
                        foreach (var item in arr)
                        {
                            if (item is JsonValue iv && iv.TryGetValue<string>(out var si) && Guid.TryParse(si, out var gi)) ids.Add(gi);
                        }
                    }
                    else
                    {
                        CollectMediaIds(value, ids);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array) CollectMediaIds(item, ids);
                break;
        }
    }
}
