using Microsoft.AspNetCore.Mvc;
using NguyenBinh.Api.Authorization;
using NguyenBinh.Application.Content.Blog;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Content.Library;
using NguyenBinh.Application.Content.Lookups;
using NguyenBinh.Application.Content.Menus;
using NguyenBinh.Application.Content.Pages;
using NguyenBinh.Application.Content.Products;
using NguyenBinh.Application.Content.Projects;
using NguyenBinh.Application.Content.Services;
using NguyenBinh.Application.Content.Taxonomies;
using NguyenBinh.Domain.Content;
using NguyenBinh.Shared.Authorization;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers.Admin;

// ----- Du an -----

[Route("api/v1/admin/projects"), PermissionModule("project")]
public sealed class ProjectsController(IContentAdminService<Project, ProjectListItem, ProjectInput> s)
    : ContentAdminController<Project, ProjectListItem, ProjectInput>(s);

[Route("api/v1/admin/industries"), PermissionModule("project")]
public sealed class IndustriesController(IContentAdminService<Industry, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<Industry, TaxonomyListItem, TaxonomyInput>(s);

[Route("api/v1/admin/technologies"), PermissionModule("project")]
public sealed class TechnologiesController(IContentAdminService<Technology, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<Technology, TaxonomyListItem, TaxonomyInput>(s);

[Route("api/v1/admin/clients"), PermissionModule("project")]
public sealed class ClientsController(IContentAdminService<Client, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<Client, TaxonomyListItem, TaxonomyInput>(s);

[Route("api/v1/admin/project-categories"), PermissionModule("project")]
public sealed class ProjectCategoriesController(IContentAdminService<ProjectCategory, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<ProjectCategory, TaxonomyListItem, TaxonomyInput>(s);

// ----- San pham -----

[Route("api/v1/admin/products"), PermissionModule("product")]
public sealed class ProductsController(IContentAdminService<Product, ProductListItem, ProductInput> s)
    : ContentAdminController<Product, ProductListItem, ProductInput>(s);

[Route("api/v1/admin/product-categories"), PermissionModule("product")]
public sealed class ProductCategoriesController(IContentAdminService<ProductCategory, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<ProductCategory, TaxonomyListItem, TaxonomyInput>(s);

// ----- Dich vu -----

[Route("api/v1/admin/services"), PermissionModule("service")]
public sealed class ServicesController(IContentAdminService<Service, ServiceListItem, ServiceInput> s)
    : ContentAdminController<Service, ServiceListItem, ServiceInput>(s);

[Route("api/v1/admin/service-categories"), PermissionModule("service")]
public sealed class ServiceCategoriesController(IContentAdminService<ServiceCategory, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<ServiceCategory, TaxonomyListItem, TaxonomyInput>(s);

// ----- Blog -----

[Route("api/v1/admin/posts"), PermissionModule("blog")]
public sealed class PostsController(IContentAdminService<Post, PostListItem, PostInput> s)
    : ContentAdminController<Post, PostListItem, PostInput>(s);

[Route("api/v1/admin/post-categories"), PermissionModule("blog")]
public sealed class PostCategoriesController(IContentAdminService<PostCategory, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<PostCategory, TaxonomyListItem, TaxonomyInput>(s);

[Route("api/v1/admin/tags"), PermissionModule("blog")]
public sealed class TagsController(IContentAdminService<Tag, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<Tag, TaxonomyListItem, TaxonomyInput>(s);

[Route("api/v1/admin/authors"), PermissionModule("blog")]
public sealed class AuthorsController(IContentAdminService<Author, TaxonomyListItem, TaxonomyInput> s)
    : ContentAdminController<Author, TaxonomyListItem, TaxonomyInput>(s);

// ----- Trang -----

[Route("api/v1/admin/pages"), PermissionModule("page")]
public sealed class PagesController(IContentAdminService<Page, PageListItem, PageInput> s)
    : ContentAdminController<Page, PageListItem, PageInput>(s)
{
    [HttpGet("block-types")]
    [ContentPermission(ContentActions.View)]
    public ActionResult<ApiResponse<IReadOnlyList<BlockTypeDefinition>>> BlockTypes() => Success(Application.Content.Pages.BlockTypes.All);
}

// ----- Thu vien -----

[Route("api/v1/admin/testimonials"), PermissionModule("library")]
public sealed class TestimonialsController(IContentAdminService<Testimonial, LibraryListItem, TestimonialInput> s)
    : ContentAdminController<Testimonial, LibraryListItem, TestimonialInput>(s);

[Route("api/v1/admin/partners"), PermissionModule("library")]
public sealed class PartnersController(IContentAdminService<Partner, LibraryListItem, PartnerInput> s)
    : ContentAdminController<Partner, LibraryListItem, PartnerInput>(s);

[Route("api/v1/admin/team-members"), PermissionModule("library")]
public sealed class TeamMembersController(IContentAdminService<TeamMember, LibraryListItem, TeamMemberInput> s)
    : ContentAdminController<TeamMember, LibraryListItem, TeamMemberInput>(s);

[Route("api/v1/admin/faqs"), PermissionModule("library")]
public sealed class FaqsController(IContentAdminService<Faq, LibraryListItem, FaqInput> s)
    : ContentAdminController<Faq, LibraryListItem, FaqInput>(s);

// ----- Menu & tra cuu -----

[Route("api/v1/admin/menus")]
public sealed class MenusController(IMenuService menus) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Menus.View)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuDto>>>> List(CancellationToken ct) =>
        Success(await menus.ListAsync(ct));

    [HttpGet("{code}")]
    [HasPermission(Permissions.Menus.View)]
    public async Task<ActionResult<ApiResponse<MenuDto>>> Get(string code, CancellationToken ct) =>
        Success(await menus.GetAsync(code, ct));

    [HttpPut("{code}")]
    [HasPermission(Permissions.Menus.Update)]
    public async Task<ActionResult<ApiResponse<MenuDto>>> Save(string code, MenuInput input, CancellationToken ct) =>
        Success(await menus.SaveAsync(code, input, ct), "Đã lưu menu.");
}

/// <summary>Danh sach rut gon (id, ten) cho cac o chon trong form admin.</summary>
[Route("api/v1/admin/lookups")]
public sealed class LookupsController(ILookupService lookups) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Dashboard.View)]
    public async Task<ActionResult<ApiResponse<LookupsDto>>> Get(CancellationToken ct) => Success(await lookups.GetAsync(ct));
}
