using Microsoft.Extensions.DependencyInjection;
using NguyenBinh.Application.Content.Blog;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Content.Library;
using NguyenBinh.Application.Content.Menus;
using NguyenBinh.Application.Content.Pages;
using NguyenBinh.Application.Content.Products;
using NguyenBinh.Application.Content.Projects;
using NguyenBinh.Application.Content.Services;
using NguyenBinh.Application.Content.Taxonomies;
using NguyenBinh.Domain.Common;

namespace NguyenBinh.Application.Content;

internal static class ContentRegistration
{
    public static IServiceCollection AddContentModules(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();
        services.AddScoped<ContentContext>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<Lookups.ILookupService, Lookups.LookupService>();

        services.AddModule<IndustryModule, Domain.Content.Industry, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<TechnologyModule, Domain.Content.Technology, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<ClientModule, Domain.Content.Client, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<ProjectCategoryModule, Domain.Content.ProjectCategory, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<ProductCategoryModule, Domain.Content.ProductCategory, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<ServiceCategoryModule, Domain.Content.ServiceCategory, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<PostCategoryModule, Domain.Content.PostCategory, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<TagModule, Domain.Content.Tag, TaxonomyListItem, TaxonomyInput>();
        services.AddModule<AuthorModule, Domain.Content.Author, TaxonomyListItem, TaxonomyInput>();

        services.AddModule<ProjectModule, Domain.Content.Project, ProjectListItem, ProjectInput>();
        services.AddModule<ProductContentModule, Domain.Content.Product, ProductListItem, ProductInput>();
        services.AddModule<ServiceModule, Domain.Content.Service, ServiceListItem, ServiceInput>();
        services.AddModule<PostModule, Domain.Content.Post, PostListItem, PostInput>();
        services.AddModule<PageModule, Domain.Content.Page, PageListItem, PageInput>();

        services.AddModule<TestimonialModule, Domain.Content.Testimonial, LibraryListItem, TestimonialInput>();
        services.AddModule<PartnerModule, Domain.Content.Partner, LibraryListItem, PartnerInput>();
        services.AddModule<TeamMemberModule, Domain.Content.TeamMember, LibraryListItem, TeamMemberInput>();
        services.AddModule<FaqModule, Domain.Content.Faq, LibraryListItem, FaqInput>();

        return services;
    }

    private static void AddModule<TModule, TEntity, TListItem, TInput>(this IServiceCollection services)
        where TModule : ContentModule<TEntity, TListItem, TInput>
        where TEntity : ContentEntity, new()
        where TInput : class
    {
        services.AddSingleton<TModule>();
        services.AddSingleton<ContentModule<TEntity, TListItem, TInput>>(sp => sp.GetRequiredService<TModule>());
        services.AddScoped<IContentAdminService<TEntity, TListItem, TInput>, ContentAdminService<TEntity, TListItem, TInput>>();
    }
}
