using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Shared.Results;
using static NguyenBinh.Application.Public.PublicQueries;

namespace NguyenBinh.Application.Public;

/// <summary>
/// Doc noi dung cong khai cho website. Chi tra noi dung da xuat ban va tuan thu quyen cong bo cua tung du an
/// (ten/logo khach hang, anh man hinh, so lieu, cong nghe, link). Ket qua cache ngan (≤ 60s).
/// </summary>
public interface IPublicContentService
{
    Task<NavigationDto> NavigationAsync(CancellationToken ct = default);
    Task<PublicPage?> PageByPathAsync(string path, CancellationToken ct = default);
    Task<PagedResult<ProjectCard>> ProjectsAsync(ProjectQuery query, CancellationToken ct = default);
    Task<ProjectDetail?> ProjectAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCard>> ProductsAsync(CancellationToken ct = default);
    Task<ProductDetail?> ProductAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceGroup>> ServicesAsync(CancellationToken ct = default);
    Task<ServiceDetail?> ServiceAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<IndustryCard>> IndustriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TechnologyGroupDto>> TechnologiesAsync(CancellationToken ct = default);
    Task<PagedResult<PostCard>> PostsAsync(PostQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<BlogCategoryDto>> BlogCategoriesAsync(CancellationToken ct = default);
    Task<BlogResolveResult?> BlogResolveAsync(string slug, CancellationToken ct = default);
    Task<SearchResult> SearchAsync(string q, CancellationToken ct = default);
    Task<IReadOnlyList<SitemapEntry>> SitemapAsync(CancellationToken ct = default);
}

internal sealed partial class PublicContentService(
    IAppDbContext db,
    IPublicMediaResolver media,
    ICacheService cache,
    TimeProvider clock) : IPublicContentService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private DateTimeOffset Now => clock.GetUtcNow();

    /// <summary>
    /// Cache theo "phien ban noi dung": moi lan admin luu noi dung, PublicCacheInvalidator doi phien ban
    /// → toan bo cache public het hieu luc ngay (khong phai cho het TTL).
    /// </summary>
    private async Task<T> Cached<T>(string key, Func<CancellationToken, Task<T>> load, CancellationToken ct)
    {
        var version = await cache.GetOrCreateAsync(PublicCache.VersionKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")[..12]),
            TimeSpan.FromDays(30), ct);
        return await cache.GetOrCreateAsync($"public:{version}:{key}", load, Ttl, ct);
    }

    // ======================= Du an =======================

    private IQueryable<Project> ProjectsWithCardData() =>
        db.Set<Project>().AsNoTracking().Visible(Now)
            .Include(p => p.Industry)
            .Include(p => p.Technologies).ThenInclude(t => t.Technology);

    internal async Task<List<ProjectCard>> ToCardsAsync(List<Project> projects, CancellationToken ct)
    {
        var images = await media.ResolveAsync(projects.Select(CardImageId), ct);
        return projects.Select(p => ToCard(p, images)).ToList();
    }

    public Task<PagedResult<ProjectCard>> ProjectsAsync(ProjectQuery q, CancellationToken ct = default) =>
        Cached($"projects:{q.ContentType}:{q.Industry}:{q.Technology}:{q.Page}:{q.PageSize}", async token =>
        {
            var query = ProjectsWithCardData();
            if (EnumParser.TryParse<ProjectContentType>(q.ContentType, out var type))
                query = query.Where(p => p.PrimaryContentType == type || p.ContentTypes.Contains(type));
            if (!string.IsNullOrWhiteSpace(q.Industry))
                query = query.Where(p => p.Industry != null && p.Industry.Slug == q.Industry);
            if (!string.IsNullOrWhiteSpace(q.Technology))
                query = query.Where(p => p.CanShowTechnology && p.Technologies.Any(t => t.Technology!.Slug == q.Technology));

            var page = Math.Max(1, q.Page);
            var size = Math.Clamp(q.PageSize, 1, 48);
            var total = await query.CountAsync(token);
            var items = await query.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.FeaturedOrder).ThenBy(p => p.SortOrder)
                .ThenByDescending(p => p.PublishedAt).Skip((page - 1) * size).Take(size).ToListAsync(token);
            return PagedResult<ProjectCard>.Create(await ToCardsAsync(items, token), page, size, total);
        }, ct);

    public Task<ProjectDetail?> ProjectAsync(string slug, CancellationToken ct = default) =>
        Cached($"project:{slug}", async token =>
        {
            var p = await ProjectsWithCardData()
                .Include(x => x.Client).Include(x => x.Product).Include(x => x.Features).Include(x => x.Media)
                .Include(x => x.Metrics).Include(x => x.Links)
                .FirstOrDefaultAsync(x => x.Slug == slug, token);
            if (p is null) return null;

            var screenshots = p.CanShowScreenshots;
            var images = await media.ResolveAsync(new[]
                {
                    CardImageId(p), screenshots ? p.CoverMediaId : null, p.ArchitectureMediaId, p.CanShowLiveUrl ? p.QrMediaId : null,
                    p.CanShowClient && p.CanShowClientLogo ? p.Client?.LogoMediaId : null, p.Seo.OgImageId, p.Seo.TwitterImageId,
                }
                .Concat(screenshots ? p.Features.Select(f => f.MediaId) : [])
                .Concat(screenshots ? p.Media.Select(m => m.MediaId) : [])
                .Concat(p.CanShowTechnology ? p.Technologies.Select(t => t.Technology?.LogoMediaId) : []), token);

            var links = new List<PublicLink>();
            if (p.CanShowLiveUrl)
            {
                void Add(ProjectLinkKind kind, string? url, string? label = null)
                {
                    if (!string.IsNullOrWhiteSpace(url) && links.All(l => l.Url != url)) links.Add(new PublicLink(kind, label, url));
                }

                Add(ProjectLinkKind.Website, p.WebsiteUrl);
                Add(ProjectLinkKind.Demo, p.DemoUrl);
                Add(ProjectLinkKind.AppStore, p.IosUrl);
                Add(ProjectLinkKind.GooglePlay, p.AndroidUrl);
                Add(ProjectLinkKind.Other, p.GithubUrl, "GitHub");
                foreach (var l in p.Links.OrderBy(l => l.SortOrder)) Add(l.Kind, l.Url, l.Label);
            }

            var relatedQuery = ProjectsWithCardData().Where(x => x.Id != p.Id);
            var related = await relatedQuery
                .OrderByDescending(x => x.IndustryId == p.IndustryId && p.IndustryId != null)
                .ThenByDescending(x => x.PrimaryContentType == p.PrimaryContentType)
                .ThenByDescending(x => x.IsFeatured).ThenBy(x => x.SortOrder)
                .Take(3).ToListAsync(token);

            return new ProjectDetail(
                ToCard(p, images),
                p.CanShowClient && p.Client is not null
                    ? new PublicClient(p.Client.Name, p.CanShowClientLogo ? Image(images, p.Client.LogoMediaId) : null, p.Client.WebsiteUrl)
                    : null,
                p.Industry is null ? null : new NamedLink(p.Industry.Name, p.Industry.Slug),
                p.Product is null || !p.Product.IsPublicAt(Now) ? null : new NamedLink(p.Product.Name, p.Product.Slug),
                p.CommercialType, p.ProjectState, p.NguyenBinhContribution,
                p.Overview, p.Problem, p.Requirements, p.Solution, p.Architecture, Image(images, p.ArchitectureMediaId),
                p.Challenge, p.ChallengeSolution, p.Result,
                p.StartDate, p.EndDate, p.LaunchDate,
                p.Features.Where(f => f.IsPublic).OrderBy(f => f.SortOrder)
                    .Select(f => new PublicFeature(f.Title, f.Description, f.Icon, screenshots ? Image(images, f.MediaId) : null)).ToList(),
                screenshots
                    ? p.Media.Where(m => m.IsPublic).OrderBy(m => m.SortOrder).Select(m => ToMedia(m.Kind, m.MediaId, m.ExternalUrl,
                        m.Caption, m.Alt, m.GroupKey, images)).Where(m => m.Image is not null || m.ExternalUrl is not null || m.FileUrl is not null).ToList()
                    : [],
                p.CanShowMetrics
                    ? p.Metrics.OrderBy(m => m.SortOrder).Select(m => new PublicMetric(m.Label, m.Value, m.Unit, m.Description)).ToList()
                    : [],
                links,
                p.CanShowTechnology
                    ? p.Technologies.OrderBy(t => t.SortOrder).Where(t => t.Technology is not null)
                        .Select(t => new PublicTechnology(t.Technology!.Name, t.Technology.Slug, t.Technology.Group,
                            Image(images, t.Technology.LogoMediaId), t.Note)).ToList()
                    : [],
                screenshots ? Image(images, p.CoverMediaId) : null,
                p.CanShowLiveUrl ? Image(images, p.QrMediaId) : null,
                await ToCardsAsync(related, token),
                ToSeo(p.Seo, images),
                p.UpdatedAt ?? p.CreatedAt);
        }, ct);

    private static PublicProjectMedia ToMedia(ProjectMediaKind kind, Guid? mediaId, string? externalUrl, string? caption, string? alt,
        string? groupKey, IReadOnlyDictionary<Guid, PublicImage> images)
    {
        var file = Image(images, mediaId);
        var isFile = kind is ProjectMediaKind.Video or ProjectMediaKind.Pdf;
        return new PublicProjectMedia(kind, isFile ? null : file, Clean(externalUrl), isFile ? file?.Url : null,
            caption, alt ?? file?.Alt, groupKey);
    }

    // ======================= San pham =======================

    public Task<IReadOnlyList<ProductCard>> ProductsAsync(CancellationToken ct = default) =>
        Cached("products", async token =>
        {
            var products = await db.Set<Product>().AsNoTracking().Visible(Now)
                .OrderByDescending(p => p.IsFeatured).ThenBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(token);
            return (IReadOnlyList<ProductCard>)await ToProductCardsAsync(products, token);
        }, ct);

    internal async Task<List<ProductCard>> ToProductCardsAsync(List<Product> products, CancellationToken ct)
    {
        var images = await media.ResolveAsync(products.SelectMany(p => new[] { p.LogoMediaId, p.HeroMediaId }), ct);
        return products.Select(p => ToProductCard(p, images)).ToList();
    }

    private static ProductCard ToProductCard(Product p, IReadOnlyDictionary<Guid, PublicImage> images) =>
        new(p.Id, p.Name, p.Slug, p.Tagline, p.ShortDescription, p.ProductType, Image(images, p.LogoMediaId), Image(images, p.HeroMediaId));

    public Task<ProductDetail?> ProductAsync(string slug, CancellationToken ct = default) =>
        Cached($"product:{slug}", async token =>
        {
            var p = await db.Set<Product>().AsNoTracking().Visible(Now)
                .Include(x => x.Features).Include(x => x.Modules).Include(x => x.Media).Include(x => x.Plans).Include(x => x.Faqs)
                .FirstOrDefaultAsync(x => x.Slug == slug, token);
            if (p is null) return null;

            var testimonials = await db.Set<Testimonial>().AsNoTracking().Visible(Now).Where(t => t.ProductId == p.Id)
                .OrderBy(t => t.SortOrder).Take(6).ToListAsync(token);
            var caseStudies = await ProjectsWithCardData().Where(x => x.ProductId == p.Id)
                .OrderBy(x => x.SortOrder).Take(6).ToListAsync(token);

            var images = await media.ResolveAsync(new[] { p.LogoMediaId, p.HeroMediaId, p.Seo.OgImageId, p.Seo.TwitterImageId }
                .Concat(p.Features.Select(f => f.MediaId)).Concat(p.Modules.Select(m => m.MediaId))
                .Concat(p.Media.Select(m => m.MediaId)).Concat(testimonials.Select(t => t.AvatarMediaId)), token);

            return new ProductDetail(
                ToProductCard(p, images), p.Description, p.CommercialType, p.Problem, p.Solution, p.TargetUsers, p.Integration,
                p.Deployment, p.Security, p.DemoVideoUrl, p.DemoUrl, p.PricingNote,
                p.Features.OrderBy(f => f.SortOrder).Select(f => new PublicFeature(f.Title, f.Description, f.Icon, Image(images, f.MediaId))).ToList(),
                p.Modules.OrderBy(m => m.SortOrder).Select(m => new PublicProductModule(m.Name, m.Description, m.Icon,
                    Image(images, m.MediaId), m.Items)).ToList(),
                p.Media.OrderBy(m => m.SortOrder).Select(m => ToMedia(m.Kind, m.MediaId, m.ExternalUrl, m.Caption, m.Alt, m.GroupKey, images))
                    .Where(m => m.Image is not null || m.ExternalUrl is not null || m.FileUrl is not null).ToList(),
                p.Plans.OrderBy(x => x.SortOrder).Select(x => new PublicPlan(x.Name, x.PriceAmount, x.Currency, x.BillingPeriod,
                    x.PriceNote, x.Features, x.IsHighlighted, x.CtaLabel, x.CtaUrl)).ToList(),
                p.Faqs.OrderBy(f => f.SortOrder).Select(f => new PublicFaq(f.Question, f.Answer)).ToList(),
                testimonials.Select(t => ToTestimonial(t, images)).ToList(),
                await ToCardsAsync(caseStudies, token),
                ToSeo(p.Seo, images),
                p.UpdatedAt ?? p.CreatedAt);
        }, ct);

    internal static PublicTestimonial ToTestimonial(Testimonial t, IReadOnlyDictionary<Guid, PublicImage> images) =>
        new(t.AuthorName, t.AuthorTitle, t.Company, Image(images, t.AvatarMediaId), t.Quote, t.Rating);

    // ======================= Dich vu =======================

    public Task<IReadOnlyList<ServiceGroup>> ServicesAsync(CancellationToken ct = default) =>
        Cached("services", async token =>
        {
            var services = await db.Set<Service>().AsNoTracking().Visible(Now).Include(s => s.Category)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(token);
            var cards = await ToServiceCardsAsync(services, token);
            return (IReadOnlyList<ServiceGroup>)services.Zip(cards)
                // Gom theo Id: AsNoTracking tao object Category rieng cho moi dich vu → khong gom theo tham chieu.
                .GroupBy(x => x.First.CategoryId)
                .Select(g => (Category: g.First().First.Category, Cards: g.Select(x => x.Second).ToList()))
                .OrderBy(g => g.Category?.SortOrder ?? int.MaxValue)
                .Select(g => new ServiceGroup(g.Category?.Name, g.Category?.Slug, g.Cards))
                .ToList();
        }, ct);

    internal async Task<List<ServiceCard>> ToServiceCardsAsync(List<Service> services, CancellationToken ct)
    {
        var images = await media.ResolveAsync(services.Select(s => s.CoverMediaId), ct);
        return services.Select(s => new ServiceCard(s.Id, s.Name, s.Slug, s.Icon, s.ShortDescription, s.Category?.Name,
            Image(images, s.CoverMediaId))).ToList();
    }

    public Task<ServiceDetail?> ServiceAsync(string slug, CancellationToken ct = default) =>
        Cached($"service:{slug}", async token =>
        {
            var s = await db.Set<Service>().AsNoTracking().Visible(Now).Include(x => x.Category).Include(x => x.Features)
                .FirstOrDefaultAsync(x => x.Slug == slug, token);
            if (s is null) return null;

            var technologies = await db.Set<Technology>().AsNoTracking().Where(t => s.TechnologyIds.Contains(t.Id))
                .OrderBy(t => t.Group).ThenBy(t => t.SortOrder).ToListAsync(token);
            var projects = await ProjectsWithCardData().Where(p => s.RelatedProjectIds.Contains(p.Id)).ToListAsync(token);
            projects = projects.OrderBy(p => s.RelatedProjectIds.IndexOf(p.Id)).ToList();
            var faqs = await db.Set<Faq>().AsNoTracking().Visible(Now).Where(f => f.Scope == FaqScope.Service && f.ScopeId == s.Id)
                .OrderBy(f => f.SortOrder).ToListAsync(token);
            var others = await db.Set<Service>().AsNoTracking().Visible(Now).Include(x => x.Category)
                .Where(x => x.Id != s.Id).OrderByDescending(x => x.CategoryId == s.CategoryId).ThenBy(x => x.SortOrder)
                .Take(4).ToListAsync(token);

            var images = await media.ResolveAsync(new[] { s.CoverMediaId, s.Seo.OgImageId, s.Seo.TwitterImageId }
                .Concat(technologies.Select(t => t.LogoMediaId)), token);

            return new ServiceDetail(
                (await ToServiceCardsAsync([s], token))[0], s.Description, s.Deliverables,
                s.Process.Select(p => new PublicProcessStep(p.Title, p.Description, p.Output)).ToList(),
                s.Features.OrderBy(f => f.SortOrder).Select(f => new PublicFeature(f.Title, f.Description, f.Icon, null)).ToList(),
                technologies.Select(t => new PublicTechnology(t.Name, t.Slug, t.Group, Image(images, t.LogoMediaId), t.Description)).ToList(),
                await ToCardsAsync(projects, token),
                faqs.Select(f => new PublicFaq(f.Question, f.Answer)).ToList(),
                await ToServiceCardsAsync(others, token),
                ToSeo(s.Seo, images),
                s.UpdatedAt ?? s.CreatedAt);
        }, ct);

    // ======================= Nganh / cong nghe =======================

    public Task<IReadOnlyList<IndustryCard>> IndustriesAsync(CancellationToken ct = default) =>
        Cached("industries", async token =>
        {
            var industries = await db.Set<Industry>().AsNoTracking().Visible(Now).OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                .ToListAsync(token);
            var solutionPaths = await db.Set<Page>().AsNoTracking().Visible(Now)
                .Where(p => p.PageType == PageType.Solution && p.IndustryId != null)
                .Select(p => new { p.IndustryId, p.Path }).ToListAsync(token);
            var counts = await db.Set<Project>().AsNoTracking().Visible(Now).Where(p => p.IndustryId != null)
                .GroupBy(p => p.IndustryId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(token);

            return (IReadOnlyList<IndustryCard>)industries.Select(i => new IndustryCard(i.Id, i.Name, i.Slug, i.Icon, i.Description,
                solutionPaths.FirstOrDefault(s => s.IndustryId == i.Id)?.Path,
                counts.FirstOrDefault(c => c.Key == i.Id)?.Count ?? 0)).ToList();
        }, ct);

    public Task<IReadOnlyList<TechnologyGroupDto>> TechnologiesAsync(CancellationToken ct = default) =>
        Cached("technologies", async token =>
        {
            var technologies = await db.Set<Technology>().AsNoTracking().Visible(Now).Where(t => t.ShowOnTechPage)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(token);
            var images = await media.ResolveAsync(technologies.Select(t => t.LogoMediaId), token);
            return (IReadOnlyList<TechnologyGroupDto>)technologies.GroupBy(t => t.Group).OrderBy(g => g.Key)
                .Select(g => new TechnologyGroupDto(g.Key, g.Select(t =>
                    new PublicTechnology(t.Name, t.Slug, t.Group, Image(images, t.LogoMediaId), t.Description)).ToList()))
                .ToList();
        }, ct);

    // ======================= Blog =======================

    private IQueryable<Post> PostsWithCardData() =>
        db.Set<Post>().AsNoTracking().Visible(Now).Include(p => p.Author)
            .Include(p => p.Categories).ThenInclude(c => c.Category);

    internal async Task<List<PostCard>> ToPostCardsAsync(List<Post> posts, CancellationToken ct)
    {
        var images = await media.ResolveAsync(posts.Select(p => p.CoverMediaId), ct);
        return posts.Select(p => ToPostCard(p, images)).ToList();
    }

    private static PostCard ToPostCard(Post p, IReadOnlyDictionary<Guid, PublicImage> images)
    {
        var category = (p.Categories.FirstOrDefault(c => c.IsPrimary) ?? p.Categories.FirstOrDefault())?.Category;
        return new PostCard(p.Id, p.Title, p.Slug, p.Excerpt, Image(images, p.CoverMediaId), p.Author?.Name,
            p.PublishedAt ?? p.PublishAt, p.ReadingMinutes, category is null ? null : new NamedLink(category.Name, category.Slug));
    }

    public Task<PagedResult<PostCard>> PostsAsync(PostQuery q, CancellationToken ct = default) =>
        Cached($"posts:{q.Category}:{q.Tag}:{q.Page}:{q.PageSize}", async token =>
        {
            var query = PostsWithCardData();
            if (!string.IsNullOrWhiteSpace(q.Category))
                query = query.Where(p => p.Categories.Any(c => c.Category!.Slug == q.Category));
            if (!string.IsNullOrWhiteSpace(q.Tag))
                query = query.Where(p => p.Tags.Any(t => t.Tag!.Slug == q.Tag));

            var page = Math.Max(1, q.Page);
            var size = Math.Clamp(q.PageSize, 1, 48);
            var total = await query.CountAsync(token);
            var posts = await query.OrderByDescending(p => p.PublishedAt ?? p.PublishAt).Skip((page - 1) * size).Take(size)
                .ToListAsync(token);
            return PagedResult<PostCard>.Create(await ToPostCardsAsync(posts, token), page, size, total);
        }, ct);

    public Task<IReadOnlyList<BlogCategoryDto>> BlogCategoriesAsync(CancellationToken ct = default) =>
        Cached("blog-categories", async token =>
        {
            var now = Now;
            var categories = await db.Set<PostCategory>().AsNoTracking().Visible(now).OrderBy(c => c.SortOrder)
                .Select(c => new
                {
                    c.Name, c.Slug, c.Description, c.Seo,
                    Count = db.Set<Post>().Visible(now).Count(p => p.Categories.Any(m => m.CategoryId == c.Id)),
                })
                .ToListAsync(token);
            var empty = new Dictionary<Guid, PublicImage>();
            return (IReadOnlyList<BlogCategoryDto>)categories
                .Select(c => new BlogCategoryDto(c.Name, c.Slug, c.Description, c.Count, ToSeo(c.Seo, empty))).ToList();
        }, ct);

    public Task<BlogResolveResult?> BlogResolveAsync(string slug, CancellationToken ct = default) =>
        Cached($"blog:{slug}", async token =>
        {
            var post = await PostsWithCardData().Include(p => p.Tags).ThenInclude(t => t.Tag)
                .FirstOrDefaultAsync(p => p.Slug == slug, token);
            if (post is not null)
            {
                var categoryIds = post.Categories.Select(c => c.CategoryId).ToList();
                var related = await PostsWithCardData().Where(p => p.Id != post.Id && p.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
                    .OrderByDescending(p => p.PublishedAt).Take(3).ToListAsync(token);
                var images = await media.ResolveAsync(new[] { post.CoverMediaId, post.Author?.AvatarMediaId, post.Seo.OgImageId,
                    post.Seo.TwitterImageId }, token);
                var author = post.Author is null ? null : new PublicAuthor(post.Author.Name, post.Author.Slug, post.Author.Title,
                    post.Author.Description, Image(images, post.Author.AvatarMediaId), post.Author.Links);

                return new BlogResolveResult("post", new PostDetail(
                    ToPostCard(post, images), post.ContentHtml, author,
                    post.Categories.Where(c => c.Category != null).Select(c => new NamedLink(c.Category!.Name, c.Category.Slug)).ToList(),
                    post.Tags.Where(t => t.Tag != null).Select(t => new NamedLink(t.Tag!.Name, t.Tag.Slug)).ToList(),
                    await ToPostCardsAsync(related, token),
                    ToSeo(post.Seo, images),
                    post.UpdatedAt ?? post.CreatedAt), null);
            }

            var category = (await BlogCategoriesAsync(token)).FirstOrDefault(c => c.Slug == slug);
            return category is null ? null : new BlogResolveResult("category", null, category);
        }, ct);

    // ======================= Tim kiem =======================

    // ======================= Sitemap =======================

    /// <summary>
    /// Moi URL cong khai cho sitemap.xml: trang page builder, du an, san pham, dich vu, bai viet, danh muc blog co bai,
    /// cac trang danh sach co dinh. Bo qua noi dung dat robots "noindex".
    /// </summary>
    public Task<IReadOnlyList<SitemapEntry>> SitemapAsync(CancellationToken ct = default) =>
        Cached("sitemap", async token =>
        {
            static bool Indexable(string? robots) => robots is null || !robots.Contains("noindex", StringComparison.OrdinalIgnoreCase);
            static DateTimeOffset? Last(DateTimeOffset? updated, DateTimeOffset? published, DateTimeOffset created) =>
                updated ?? published ?? created;

            var entries = new List<SitemapEntry>();
            var pages = await db.Set<Page>().AsNoTracking().Visible(Now)
                .Select(p => new { p.Path, p.PageType, p.Seo.Robots, p.UpdatedAt, p.PublishedAt, p.CreatedAt }).ToListAsync(token);
            entries.AddRange(pages.Where(p => Indexable(p.Robots)).Select(p =>
                new SitemapEntry(p.Path, Last(p.UpdatedAt, p.PublishedAt, p.CreatedAt), p.PageType == PageType.Home ? "home" : "page")));

            async Task Add<T>(IQueryable<T> query, string prefix, string kind) where T : ContentEntity, IHasSlug, IHasSeo
            {
                var rows = await query.AsNoTracking().Visible(Now)
                    .Select(e => new { e.Slug, e.Seo.Robots, e.UpdatedAt, e.PublishedAt, e.CreatedAt }).ToListAsync(token);
                entries.AddRange(rows.Where(r => Indexable(r.Robots))
                    .Select(r => new SitemapEntry($"{prefix}/{r.Slug}", Last(r.UpdatedAt, r.PublishedAt, r.CreatedAt), kind)));
            }

            await Add(db.Set<Project>(), "/du-an", "project");
            await Add(db.Set<Product>(), "/san-pham", "product");
            await Add(db.Set<Service>(), "/dich-vu", "service");
            await Add(db.Set<Post>(), "/blog", "post");

            var categories = await db.Set<Post>().AsNoTracking().Visible(Now)
                .SelectMany(p => p.Categories.Select(c => c.Category!.Slug)).Distinct().ToListAsync(token);
            entries.AddRange(categories.Select(slug => new SitemapEntry($"/blog/{slug}", null, "category")));

            // Trang danh sach co dinh (neu chua co trang page builder trung duong dan).
            var lastContent = entries.Max(e => e.LastModified);
            foreach (var path in new[] { "/", "/du-an", "/san-pham", "/dich-vu", "/giai-phap", "/cong-nghe", "/blog", "/lien-he" })
                if (entries.All(e => e.Path != path)) entries.Add(new SitemapEntry(path, lastContent, path == "/" ? "home" : "listing"));

            return (IReadOnlyList<SitemapEntry>)entries.DistinctBy(e => e.Path).OrderBy(e => e.Path).ToList();
        }, ct);

    public async Task<SearchResult> SearchAsync(string q, CancellationToken ct = default)
    {
        q = q.Trim();
        if (q.Length < 2) return new SearchResult(q, []);
        if (q.Length > 100) q = q[..100];

        var projects = await ProjectsWithCardData()
            .Where(p => p.Name.Contains(q) || (p.ShortDescription != null && p.ShortDescription.Contains(q)))
            .OrderBy(p => p.SortOrder).Take(8).ToListAsync(ct);
        var products = await db.Set<Product>().AsNoTracking().Visible(Now)
            .Where(p => p.Name.Contains(q) || (p.Tagline != null && p.Tagline.Contains(q)) ||
                        (p.ShortDescription != null && p.ShortDescription.Contains(q)))
            .Take(8).ToListAsync(ct);
        var services = await db.Set<Service>().AsNoTracking().Visible(Now).Include(s => s.Category)
            .Where(s => s.Name.Contains(q) || (s.ShortDescription != null && s.ShortDescription.Contains(q)))
            .Take(8).ToListAsync(ct);
        var posts = await PostsWithCardData()
            .Where(p => p.Title.Contains(q) || (p.Excerpt != null && p.Excerpt.Contains(q)))
            .OrderByDescending(p => p.PublishedAt).Take(8).ToListAsync(ct);

        var hits = new List<SearchHit>();
        hits.AddRange((await ToProductCardsAsync(products, ct)).Select(p => new SearchHit("product", p.Name, $"/san-pham/{p.Slug}",
            p.Tagline ?? p.ShortDescription, p.Hero ?? p.Logo)));
        hits.AddRange((await ToCardsAsync(projects, ct)).Select(p => new SearchHit("project", p.Name, $"/du-an/{p.Slug}",
            p.ShortDescription, p.Image)));
        hits.AddRange((await ToServiceCardsAsync(services, ct)).Select(s => new SearchHit("service", s.Name, $"/dich-vu/{s.Slug}",
            s.ShortDescription, s.Cover)));
        hits.AddRange((await ToPostCardsAsync(posts, ct)).Select(p => new SearchHit("post", p.Title, $"/blog/{p.Slug}", p.Excerpt, p.Cover)));
        return new SearchResult(q, hits);
    }
}
