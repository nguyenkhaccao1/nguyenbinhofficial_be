using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NguyenBinh.Api.Infrastructure;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Public;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers;

/// <summary>
/// API doc cong khai cho website (docs/design/06-api.md §1). Chi noi dung da xuat ban; quyen cong bo cua du an
/// duoc ap dung o tang Application. Cache ngan o server (60s) + header cho CDN.
/// </summary>
[Route("api/v1")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicApi)]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
public sealed class PublicContentController(IPublicContentService content) : ApiControllerBase
{
    [HttpGet("site/sitemap")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SitemapEntry>>>> Sitemap(CancellationToken ct) =>
        Success(await content.SitemapAsync(ct));

    [HttpGet("site/navigation")]
    public async Task<ActionResult<ApiResponse<NavigationDto>>> Navigation(CancellationToken ct) =>
        Success(await content.NavigationAsync(ct));

    [HttpGet("pages/by-path")]
    public async Task<ActionResult<ApiResponse<PublicPage>>> PageByPath([FromQuery] string path, CancellationToken ct) =>
        Success(await content.PageByPathAsync(path, ct) ?? throw new NotFoundException("Không tìm thấy trang."));

    [HttpGet("projects")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectCard>>>> Projects([FromQuery] ProjectQuery query,
        CancellationToken ct) => Success(await content.ProjectsAsync(query, ct));

    [HttpGet("projects/{slug}")]
    public async Task<ActionResult<ApiResponse<ProjectDetail>>> Project(string slug, CancellationToken ct) =>
        Success(await content.ProjectAsync(slug, ct) ?? throw new NotFoundException("Không tìm thấy dự án."));

    [HttpGet("products")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductCard>>>> Products(CancellationToken ct) =>
        Success(await content.ProductsAsync(ct));

    [HttpGet("products/{slug}")]
    public async Task<ActionResult<ApiResponse<ProductDetail>>> Product(string slug, CancellationToken ct) =>
        Success(await content.ProductAsync(slug, ct) ?? throw new NotFoundException("Không tìm thấy sản phẩm."));

    [HttpGet("services")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServiceGroup>>>> Services(CancellationToken ct) =>
        Success(await content.ServicesAsync(ct));

    [HttpGet("services/{slug}")]
    public async Task<ActionResult<ApiResponse<ServiceDetail>>> Service(string slug, CancellationToken ct) =>
        Success(await content.ServiceAsync(slug, ct) ?? throw new NotFoundException("Không tìm thấy dịch vụ."));

    [HttpGet("industries")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IndustryCard>>>> Industries(CancellationToken ct) =>
        Success(await content.IndustriesAsync(ct));

    [HttpGet("technologies")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TechnologyGroupDto>>>> Technologies(CancellationToken ct) =>
        Success(await content.TechnologiesAsync(ct));

    [HttpGet("blog/posts")]
    public async Task<ActionResult<ApiResponse<PagedResult<PostCard>>>> Posts([FromQuery] PostQuery query, CancellationToken ct) =>
        Success(await content.PostsAsync(query, ct));

    [HttpGet("blog/categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BlogCategoryDto>>>> BlogCategories(CancellationToken ct) =>
        Success(await content.BlogCategoriesAsync(ct));

    [HttpGet("blog/resolve/{slug}")]
    public async Task<ActionResult<ApiResponse<BlogResolveResult>>> BlogResolve(string slug, CancellationToken ct) =>
        Success(await content.BlogResolveAsync(slug, ct) ?? throw new NotFoundException("Không tìm thấy bài viết."));

    [HttpGet("search")]
    [ResponseCache(NoStore = true)]
    public async Task<ActionResult<ApiResponse<SearchResult>>> Search([FromQuery] string? q, CancellationToken ct) =>
        Success(await content.SearchAsync(q ?? string.Empty, ct));
}
