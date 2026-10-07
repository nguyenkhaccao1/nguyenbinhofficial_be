using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Infrastructure.Persistence.Configurations;

/// <summary>Cot chung cho danh muc: ten, slug duy nhat (bo qua ban ghi da xoa), mo ta.</summary>
internal abstract class TaxonomyConfiguration<T>(string table) : IEntityTypeConfiguration<T> where T : TaxonomyEntity
{
    public virtual void Configure(EntityTypeBuilder<T> b)
    {
        b.ToTable(table);
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.Slug).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.SortOrder);
    }
}

internal sealed class IndustryConfiguration() : TaxonomyConfiguration<Industry>("Industries")
{
    public override void Configure(EntityTypeBuilder<Industry> b)
    {
        base.Configure(b);
        b.Property(x => x.Icon).HasMaxLength(100);
    }
}

internal sealed class TechnologyConfiguration() : TaxonomyConfiguration<Technology>("Technologies")
{
    public override void Configure(EntityTypeBuilder<Technology> b)
    {
        base.Configure(b);
        b.Property(x => x.WebsiteUrl).HasMaxLength(1000);
    }
}

internal sealed class ClientConfiguration() : TaxonomyConfiguration<Client>("Clients")
{
    public override void Configure(EntityTypeBuilder<Client> b)
    {
        base.Configure(b);
        b.Property(x => x.WebsiteUrl).HasMaxLength(1000);
        b.HasOne(x => x.Industry).WithMany().HasForeignKey(x => x.IndustryId);
    }
}

internal sealed class ProjectCategoryConfiguration() : TaxonomyConfiguration<ProjectCategory>("ProjectCategories");

internal sealed class ProductCategoryConfiguration() : TaxonomyConfiguration<ProductCategory>("ProductCategories");

internal sealed class ServiceCategoryConfiguration() : TaxonomyConfiguration<ServiceCategory>("ServiceCategories");

internal sealed class PostCategoryConfiguration() : TaxonomyConfiguration<PostCategory>("PostCategories");

internal sealed class TagConfiguration() : TaxonomyConfiguration<Tag>("Tags");

internal sealed class AuthorConfiguration() : TaxonomyConfiguration<Author>("Authors")
{
    public override void Configure(EntityTypeBuilder<Author> b)
    {
        base.Configure(b);
        b.Property(x => x.Title).HasMaxLength(150);
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(200);
        b.Property(x => x.ShortDescription).HasMaxLength(500);
        b.Property(x => x.ShortResult).HasMaxLength(300);
        b.Property(x => x.ProjectOwner).HasMaxLength(200);
        b.Property(x => x.PublicCreditText).HasMaxLength(500);
        foreach (var url in new[] { nameof(Project.WebsiteUrl), nameof(Project.DemoUrl), nameof(Project.AndroidUrl),
                     nameof(Project.IosUrl), nameof(Project.GithubUrl) })
            b.Property<string?>(url).HasMaxLength(1000);

        b.PrimitiveCollection(x => x.ContentTypes).ElementType(e => e.HasConversion<string>());
        b.PrimitiveCollection(x => x.ProjectRoles).ElementType(e => e.HasConversion<string>());

        b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
        b.HasOne(x => x.Industry).WithMany().HasForeignKey(x => x.IndustryId);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);

        b.HasMany(x => x.Categories).WithOne().HasForeignKey(x => x.ProjectId);
        b.HasMany(x => x.Technologies).WithOne().HasForeignKey(x => x.ProjectId);
        b.HasMany(x => x.Features).WithOne().HasForeignKey(x => x.ProjectId);
        b.HasMany(x => x.Media).WithOne().HasForeignKey(x => x.ProjectId);
        b.HasMany(x => x.Metrics).WithOne().HasForeignKey(x => x.ProjectId);
        b.HasMany(x => x.Links).WithOne().HasForeignKey(x => x.ProjectId);

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.Status, x.IsFeatured, x.FeaturedOrder });
        b.HasIndex(x => x.IndustryId);
    }
}

internal sealed class ProjectChildConfiguration :
    IEntityTypeConfiguration<ProjectCategoryMapping>,
    IEntityTypeConfiguration<ProjectTechnologyMapping>,
    IEntityTypeConfiguration<ProjectFeature>,
    IEntityTypeConfiguration<ProjectMedia>,
    IEntityTypeConfiguration<ProjectMetric>,
    IEntityTypeConfiguration<ProjectLink>
{
    public void Configure(EntityTypeBuilder<ProjectCategoryMapping> b)
    {
        b.ToTable("ProjectCategoryMappings");
        b.HasKey(x => new { x.ProjectId, x.CategoryId });
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
    }

    public void Configure(EntityTypeBuilder<ProjectTechnologyMapping> b)
    {
        b.ToTable("ProjectTechnologyMappings");
        b.HasKey(x => new { x.ProjectId, x.TechnologyId });
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId);
    }

    public void Configure(EntityTypeBuilder<ProjectFeature> b)
    {
        b.ToTable("ProjectFeatures");
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Icon).HasMaxLength(100);
    }

    public void Configure(EntityTypeBuilder<ProjectMedia> b)
    {
        b.ToTable("ProjectMedia");
        b.Property(x => x.ExternalUrl).HasMaxLength(1000);
        b.Property(x => x.Caption).HasMaxLength(500);
        b.Property(x => x.Alt).HasMaxLength(300);
        b.Property(x => x.GroupKey).HasMaxLength(50);
    }

    public void Configure(EntityTypeBuilder<ProjectMetric> b)
    {
        b.ToTable("ProjectMetrics");
        b.Property(x => x.Label).HasMaxLength(100);
        b.Property(x => x.Value).HasMaxLength(50);
        b.Property(x => x.Unit).HasMaxLength(30);
        b.Property(x => x.Description).HasMaxLength(500);
    }

    public void Configure(EntityTypeBuilder<ProjectLink> b)
    {
        b.ToTable("ProjectLinks");
        b.Property(x => x.Label).HasMaxLength(100);
        b.Property(x => x.Url).HasMaxLength(1000);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(200);
        b.Property(x => x.Tagline).HasMaxLength(300);
        b.Property(x => x.ShortDescription).HasMaxLength(500);
        b.Property(x => x.DemoVideoUrl).HasMaxLength(1000);
        b.Property(x => x.DemoUrl).HasMaxLength(1000);
        b.Property(x => x.PricingNote).HasMaxLength(500);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.HasMany(x => x.Features).WithOne().HasForeignKey(x => x.ProductId);
        b.HasMany(x => x.Modules).WithOne().HasForeignKey(x => x.ProductId);
        b.HasMany(x => x.Media).WithOne().HasForeignKey(x => x.ProductId);
        b.HasMany(x => x.Plans).WithOne().HasForeignKey(x => x.ProductId);
        b.HasMany(x => x.Faqs).WithOne().HasForeignKey(x => x.ProductId);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.Status, x.IsFeatured });
    }
}

internal sealed class ProductChildConfiguration :
    IEntityTypeConfiguration<ProductFeature>,
    IEntityTypeConfiguration<ProductModule>,
    IEntityTypeConfiguration<ProductMedia>,
    IEntityTypeConfiguration<ProductPlan>,
    IEntityTypeConfiguration<ProductFaq>
{
    public void Configure(EntityTypeBuilder<ProductFeature> b)
    {
        b.ToTable("ProductFeatures");
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Icon).HasMaxLength(100);
        b.Property(x => x.Group).HasMaxLength(100);
    }

    public void Configure(EntityTypeBuilder<ProductModule> b)
    {
        b.ToTable("ProductModules");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Icon).HasMaxLength(100);
    }

    public void Configure(EntityTypeBuilder<ProductMedia> b)
    {
        b.ToTable("ProductMedia");
        b.Property(x => x.ExternalUrl).HasMaxLength(1000);
        b.Property(x => x.Caption).HasMaxLength(500);
        b.Property(x => x.Alt).HasMaxLength(300);
        b.Property(x => x.GroupKey).HasMaxLength(100);
    }

    public void Configure(EntityTypeBuilder<ProductPlan> b)
    {
        b.ToTable("ProductPlans");
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.PriceAmount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false);
        b.Property(x => x.PriceNote).HasMaxLength(300);
        b.Property(x => x.CtaLabel).HasMaxLength(100);
        b.Property(x => x.CtaUrl).HasMaxLength(500);
    }

    public void Configure(EntityTypeBuilder<ProductFaq> b)
    {
        b.ToTable("ProductFaqs");
        b.Property(x => x.Question).HasMaxLength(300);
        b.Property(x => x.Answer).HasMaxLength(5000);
    }
}

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>, IEntityTypeConfiguration<ServiceFeature>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("Services");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(200);
        b.Property(x => x.Icon).HasMaxLength(100);
        b.Property(x => x.ShortDescription).HasMaxLength(500);
        b.OwnsMany(x => x.Process, p => p.ToJson());
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.HasMany(x => x.Features).WithOne().HasForeignKey(x => x.ServiceId);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
    }

    public void Configure(EntityTypeBuilder<ServiceFeature> b)
    {
        b.ToTable("ServiceFeatures");
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Icon).HasMaxLength(100);
    }
}

internal sealed class PostConfiguration :
    IEntityTypeConfiguration<Post>, IEntityTypeConfiguration<PostCategoryMapping>, IEntityTypeConfiguration<PostTag>
{
    public void Configure(EntityTypeBuilder<Post> b)
    {
        b.ToTable("Posts");
        b.Property(x => x.Title).HasMaxLength(250);
        b.Property(x => x.Slug).HasMaxLength(200);
        b.Property(x => x.Excerpt).HasMaxLength(500);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId);
        b.HasMany(x => x.Categories).WithOne().HasForeignKey(x => x.PostId);
        b.HasMany(x => x.Tags).WithOne().HasForeignKey(x => x.PostId);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.Status, x.PublishedAt });
        b.HasIndex(x => x.AuthorId);
    }

    public void Configure(EntityTypeBuilder<PostCategoryMapping> b)
    {
        b.ToTable("PostCategoryMappings");
        b.HasKey(x => new { x.PostId, x.CategoryId });
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
    }

    public void Configure(EntityTypeBuilder<PostTag> b)
    {
        b.ToTable("PostTags");
        b.HasKey(x => new { x.PostId, x.TagId });
        b.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId);
    }
}

internal sealed class PageConfiguration :
    IEntityTypeConfiguration<Page>, IEntityTypeConfiguration<PageSection>, IEntityTypeConfiguration<PageBlock>
{
    public void Configure(EntityTypeBuilder<Page> b)
    {
        b.ToTable("Pages");
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Path).HasMaxLength(300);
        b.HasOne(x => x.Industry).WithMany().HasForeignKey(x => x.IndustryId);
        b.HasMany(x => x.Sections).WithOne().HasForeignKey(x => x.PageId);
        b.HasIndex(x => x.Path).IsUnique().HasFilter("[IsDeleted] = 0");
    }

    public void Configure(EntityTypeBuilder<PageSection> b)
    {
        b.ToTable("PageSections", t => t.HasCheckConstraint("CK_PageSections_Settings", "ISJSON([SettingsJson]) = 1"));
        b.Property(x => x.Name).HasMaxLength(150);
        b.HasMany(x => x.Blocks).WithOne().HasForeignKey(x => x.SectionId);
    }

    public void Configure(EntityTypeBuilder<PageBlock> b)
    {
        b.ToTable("PageBlocks", t =>
        {
            t.HasCheckConstraint("CK_PageBlocks_Data", "ISJSON([DataJson]) = 1");
            t.HasCheckConstraint("CK_PageBlocks_Settings", "ISJSON([SettingsJson]) = 1");
        });
        b.Property(x => x.Type).HasMaxLength(40).IsUnicode(false);
    }
}

internal sealed class LibraryConfiguration :
    IEntityTypeConfiguration<Testimonial>, IEntityTypeConfiguration<Partner>, IEntityTypeConfiguration<TeamMember>,
    IEntityTypeConfiguration<Faq>
{
    public void Configure(EntityTypeBuilder<Testimonial> b)
    {
        b.ToTable("Testimonials");
        b.Property(x => x.AuthorName).HasMaxLength(150);
        b.Property(x => x.AuthorTitle).HasMaxLength(150);
        b.Property(x => x.Company).HasMaxLength(200);
        b.Property(x => x.Quote).HasMaxLength(2000);
    }

    public void Configure(EntityTypeBuilder<Partner> b)
    {
        b.ToTable("Partners");
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.Url).HasMaxLength(1000);
        b.Property(x => x.Kind).HasMaxLength(50);
    }

    public void Configure(EntityTypeBuilder<TeamMember> b)
    {
        b.ToTable("TeamMembers");
        b.Property(x => x.FullName).HasMaxLength(150);
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Bio).HasMaxLength(2000);
    }

    public void Configure(EntityTypeBuilder<Faq> b)
    {
        b.ToTable("Faqs");
        b.Property(x => x.Question).HasMaxLength(300);
        b.Property(x => x.Answer).HasMaxLength(5000);
        b.HasIndex(x => new { x.Scope, x.ScopeId });
    }
}

internal sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>, IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<Menu> b)
    {
        b.ToTable("Menus");
        b.Property(x => x.Code).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.MenuId);
    }

    public void Configure(EntityTypeBuilder<MenuItem> b)
    {
        b.ToTable("MenuItems");
        b.Property(x => x.Label).HasMaxLength(100);
        b.Property(x => x.Url).HasMaxLength(1000);
        b.Property(x => x.Description).HasMaxLength(200);
        b.Property(x => x.DynamicSource).HasMaxLength(30).IsUnicode(false);
    }
}
