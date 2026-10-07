using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Shared.Text;

namespace NguyenBinh.Infrastructure.Persistence;

/// <summary>
/// Du lieu khoi tao (muc 7, 59). Nguyen tac:
/// - Chi dung thong tin chu du an cung cap (ten, URL, loai, vai tro). KHONG bia doanh thu, so user,
///   ty le chuyen doi, khach hang, traffic — de trong de admin bo sung.
/// - Moi du an/san pham/trang o trang thai DRAFT: admin xac nhan quyen cong bo roi moi xuat ban.
/// - Du an theo yeu cau: ClientOwned, chua cong bo ten/logo khach hang. Com Thi No: Undisclosed (cho cau hinh).
/// - Idempotent: ban ghi da co (theo slug/path/code) thi bo qua, khong ghi de chinh sua cua admin.
/// </summary>
public sealed class ContentSeeder(AppDbContext db, ILogger<ContentSeeder> logger)
{
    private const string ClientCredit =
        "Nguyên Bình tham gia phát triển và triển khai hệ thống theo yêu cầu của khách hàng.";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var industries = await SeedTaxonomyAsync(db.Set<Industry>(), [
            ("Nhà hàng", "nha-hang"), ("Khách sạn", "khach-san"), ("Bán lẻ", "ban-le"),
            ("Doanh nghiệp", "doanh-nghiep"), ("Nhân sự", "nhan-su"), ("Thương mại điện tử", "thuong-mai-dien-tu"),
        ], ct);

        await SeedTechnologiesAsync(ct);

        var projectCategories = await SeedTaxonomyAsync(db.Set<ProjectCategory>(), [
            ("Website", "website"), ("Mobile App", "mobile-app"), ("Phần mềm doanh nghiệp", "phan-mem-doanh-nghiep"),
            ("Thương mại điện tử", "thuong-mai-dien-tu"), ("POS / PMS", "pos-pms"),
        ], ct);

        await SeedTaxonomyAsync(db.Set<ProductCategory>(), [("Phần mềm bán hàng", "phan-mem-ban-hang")], ct);

        var serviceCategories = await SeedTaxonomyAsync(db.Set<ServiceCategory>(), [
            ("Phát triển phần mềm", "phat-trien-phan-mem"), ("Website & Ecommerce", "website-ecommerce"),
            ("Mobile", "mobile"), ("Tích hợp & vận hành", "tich-hop-van-hanh"),
        ], ct);

        await SeedTaxonomyAsync(db.Set<PostCategory>(), [
            ("Công nghệ", "cong-nghe"), ("Phần mềm doanh nghiệp", "phan-mem-doanh-nghiep"), ("Nhà hàng", "nha-hang"),
            ("Khách sạn", "khach-san"), ("POS", "pos"), ("PMS", "pms"), ("Ecommerce", "ecommerce"), ("React", "react"),
            ("ASP.NET", "asp-net"), ("Mobile", "mobile"), ("Case Study", "case-study"), ("Chuyển đổi số", "chuyen-doi-so"),
        ], ct);

        var pos = await SeedProductAsync(ct);
        await SeedProjectsAsync(industries, projectCategories, pos, ct);
        await SeedServicesAsync(serviceCategories, ct);
        await SeedMenusAsync(ct);
        await SeedPagesAsync(ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<string, Guid>> SeedTaxonomyAsync<T>(DbSet<T> set, (string Name, string Slug)[] items,
        CancellationToken ct) where T : TaxonomyEntity, new()
    {
        var existing = await set.IgnoreQueryFilters().ToDictionaryAsync(x => x.Slug, x => x.Id, ct);
        for (var i = 0; i < items.Length; i++)
        {
            var (name, slug) = items[i];
            if (existing.ContainsKey(slug)) continue;
            var entity = new T { Name = name, Slug = slug, SortOrder = i };
            set.Add(entity);
            existing[slug] = entity.Id;
        }

        return existing;
    }

    /// <summary>Muc 43: chi cong nghe thuc su co nang luc.</summary>
    private async Task SeedTechnologiesAsync(CancellationToken ct)
    {
        (string Name, TechnologyGroup Group)[] items =
        [
            (".NET", TechnologyGroup.Backend), ("ASP.NET Core", TechnologyGroup.Backend),
            ("React", TechnologyGroup.Frontend), ("TypeScript", TechnologyGroup.Frontend),
            ("Flutter", TechnologyGroup.Mobile),
            ("SQL Server", TechnologyGroup.Data), ("Redis", TechnologyGroup.Data),
            ("Docker", TechnologyGroup.Infrastructure), ("Nginx", TechnologyGroup.Infrastructure),
            ("CI/CD", TechnologyGroup.Infrastructure),
        ];

        var set = db.Set<Technology>();
        var existing = await set.IgnoreQueryFilters().Select(t => t.Slug).ToListAsync(ct);
        for (var i = 0; i < items.Length; i++)
        {
            var slug = Slug.From(items[i].Name);
            if (existing.Contains(slug)) continue;
            set.Add(new Technology { Name = items[i].Name, Slug = slug, Group = items[i].Group, SortOrder = i });
        }
    }

    private async Task<Guid> SeedProductAsync(CancellationToken ct)
    {
        const string slug = "pos-nguyen-binh";
        var existing = await db.Set<Product>().IgnoreQueryFilters().Where(p => p.Slug == slug).Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty) return existing;

        var product = new Product
        {
            Name = "POS Nguyên Bình",
            Slug = slug,
            ProductType = ProductType.Pos,
            CommercialType = CommercialType.ForSale,
            OwnershipType = OwnershipType.NguyenBinhOwned,
            Status = ContentStatus.Draft,
        };
        db.Set<Product>().Add(product);
        logger.LogInformation("Seeded product {Slug}", slug);
        return product.Id;
    }

    private async Task SeedProjectsAsync(Dictionary<string, Guid> industries, Dictionary<string, Guid> categories,
        Guid posProductId, CancellationToken ct)
    {
        var existing = await db.Set<Project>().IgnoreQueryFilters().Select(p => p.Slug).ToListAsync(ct);

        Project Client(string name, string slug, string? url, ProjectContentType primary, ProjectContentType[] types,
            string? industry, string category)
        {
            var project = new Project
            {
                Name = name,
                Slug = slug,
                WebsiteUrl = url,
                PrimaryContentType = primary,
                ContentTypes = types.Prepend(primary).Distinct().ToList(),
                CommercialType = CommercialType.CustomDevelopment,
                ProjectRoles = [ProjectRole.Developer],
                OwnershipType = OwnershipType.ClientOwned,
                PublicCreditText = ClientCredit,
                CanShowClient = false,
                CanShowClientLogo = false,
                CanShowScreenshots = true,
                CanShowMetrics = false,
                CanShowTechnology = true,
                CanShowLiveUrl = url is not null,
                IndustryId = industry is null ? null : industries[industry],
                Status = ContentStatus.Draft,
            };
            project.Categories.Add(new ProjectCategoryMapping { ProjectId = project.Id, CategoryId = categories[category] });
            if (url is not null)
                project.Links.Add(new ProjectLink { ProjectId = project.Id, Kind = ProjectLinkKind.Website, Url = url });
            return project;
        }

        static void Features(Project p, bool isPublic, params string[] titles)
        {
            for (var i = 0; i < titles.Length; i++)
                p.Features.Add(new ProjectFeature { ProjectId = p.Id, Title = titles[i], IsPublic = isPublic, SortOrder = i });
        }

        var projects = new List<Project>();

        projects.Add(Client("Nam Việt Hưng", "nam-viet-hung", "https://namviethung.nhiha.com/",
            ProjectContentType.CustomProject, [], null, "website"));

        projects.Add(Client("PerfectKey Web", "perfectkey-web", "https://sit.fe.perfectkey.vn/",
            ProjectContentType.CustomProject, [ProjectContentType.EnterpriseSoftware, ProjectContentType.Pms], "khach-san",
            "pos-pms"));

        var asmart = Client("A-Smart", "a-smart", "https://a-smart.vn/", ProjectContentType.CustomProject,
            [ProjectContentType.Ecommerce, ProjectContentType.Website], "ban-le", "thuong-mai-dien-tu");
        Features(asmart, true, "Thương mại điện tử", "Danh mục sản phẩm", "Sản phẩm", "Tồn kho", "Giá", "Khuyến mại",
            "Tin tức", "Trải nghiệm mua hàng", "Responsive", "Quản trị nội dung");
        projects.Add(asmart);

        var pkApp = Client("PerfectKey App", "perfectkey-app", null, ProjectContentType.CustomProject,
            [ProjectContentType.MobileApp, ProjectContentType.Pms], "khach-san", "mobile-app");
        pkApp.CanShowLiveUrl = false;
        projects.Add(pkApp);

        // Muc 7.05: chi cong bo chuc nang duoc phep → seed IsPublic = false, admin bat tung muc.
        var pkw = Client("PerfectKey Workforce", "perfectkey-workforce", "https://web.pkw.perfectkey.vn/login",
            ProjectContentType.CustomProject, [ProjectContentType.Hrm, ProjectContentType.EnterpriseSoftware], "nhan-su",
            "phan-mem-doanh-nghiep");
        Features(pkw, false, "Workforce", "Nhân sự", "Ca làm", "Chấm công", "Vận hành", "Dashboard");
        projects.Add(pkw);

        // Muc 7.06: quyen so huu phai cau hinh trong CMS → Undisclosed, chua co credit.
        var comThiNo = Client("Cơm Thị Nở", "com-thi-no", "https://comthino.com/", ProjectContentType.Website,
            [ProjectContentType.CustomProject], "nha-hang", "website");
        comThiNo.OwnershipType = OwnershipType.Undisclosed;
        comThiNo.PublicCreditText = null;
        Features(comThiNo, true, "Website nhà hàng", "Menu", "Thương hiệu", "SEO Local", "SEO theo khu vực", "Landing page",
            "Mobile");
        projects.Add(comThiNo);

        projects.Add(new Project
        {
            Name = "POS Nguyên Bình",
            Slug = "pos-nguyen-binh",
            PrimaryContentType = ProjectContentType.OwnProduct,
            ContentTypes = [ProjectContentType.OwnProduct, ProjectContentType.Pos, ProjectContentType.CommercialProduct],
            CommercialType = CommercialType.ForSale,
            ProjectRoles = [ProjectRole.Owner],
            OwnershipType = OwnershipType.NguyenBinhOwned,
            ProductId = posProductId,
            IndustryId = industries["nha-hang"],
            CanShowScreenshots = true,
            CanShowTechnology = true,
            Status = ContentStatus.Draft,
        });

        projects.Add(Client("Tường Quang Phát", "tuong-quang-phat", "https://tuongquangphat.vn/",
            ProjectContentType.CustomProject, [ProjectContentType.Website], null, "website"));

        var hotel = Client("Quản Lý Khách Sạn / Perfect Key Management", "quan-ly-khach-san", "https://quanlykhachsan.com/",
            ProjectContentType.CustomProject, [ProjectContentType.Pms, ProjectContentType.EnterpriseSoftware], "khach-san",
            "pos-pms");
        Features(hotel, true, "Front Office", "POS", "Back Office", "Inventory", "Integration");
        projects.Add(hotel);

        for (var i = 0; i < projects.Count; i++)
        {
            if (existing.Contains(projects[i].Slug)) continue;
            projects[i].SortOrder = i;
            db.Set<Project>().Add(projects[i]);
            logger.LogInformation("Seeded project {Slug}", projects[i].Slug);
        }
    }

    /// <summary>Ten + slug dich vu (muc 18). Noi dung de trong — admin viet, khong sinh van mau.</summary>
    private async Task SeedServicesAsync(Dictionary<string, Guid> categories, CancellationToken ct)
    {
        (string Name, string Slug, string Category)[] items =
        [
            ("Phát triển phần mềm theo yêu cầu", "phat-trien-phan-mem-theo-yeu-cau", "phat-trien-phan-mem"),
            ("Thiết kế website doanh nghiệp", "thiet-ke-website", "website-ecommerce"),
            ("Phát triển Mobile App", "phat-trien-mobile-app", "mobile"),
            ("ASP.NET Development", "asp-net-development", "phat-trien-phan-mem"),
            ("React Development", "react-development", "phat-trien-phan-mem"),
            ("Flutter Development", "flutter-development", "mobile"),
            ("Xây dựng Ecommerce", "ecommerce", "website-ecommerce"),
            ("Tích hợp API", "tich-hop-api", "tich-hop-van-hanh"),
        ];

        var existing = await db.Set<Service>().IgnoreQueryFilters().Select(s => s.Slug).ToListAsync(ct);
        for (var i = 0; i < items.Length; i++)
        {
            if (existing.Contains(items[i].Slug)) continue;
            db.Set<Service>().Add(new Service
            {
                Name = items[i].Name, Slug = items[i].Slug, CategoryId = categories[items[i].Category], SortOrder = i,
                Status = ContentStatus.Draft,
            });
        }
    }

    /// <summary>Menu header (muc 63) va cac cot footer (muc 62).</summary>
    private async Task SeedMenusAsync(CancellationToken ct)
    {
        var existing = await db.Set<Menu>().IgnoreQueryFilters().Select(m => m.Code).ToListAsync(ct);

        void Add(string code, string name, params (string Label, string? Url, string? Source)[] items)
        {
            if (existing.Contains(code)) return;
            var menu = new Menu { Code = code, Name = name };
            for (var i = 0; i < items.Length; i++)
                menu.Items.Add(new MenuItem
                {
                    MenuId = menu.Id, Label = items[i].Label, Url = items[i].Url, DynamicSource = items[i].Source, SortOrder = i,
                });
            db.Set<Menu>().Add(menu);
        }

        Add("header", "Menu chính",
            ("Sản phẩm", "/san-pham", "PRODUCTS"), ("Giải pháp", "/giai-phap", "SOLUTIONS"),
            ("Dịch vụ", "/dich-vu", "SERVICES"), ("Dự án", "/du-an", null), ("Công nghệ", "/cong-nghe", null),
            ("Blog", "/blog", null), ("Giới thiệu", "/gioi-thieu", null));
        Add("footer-company", "Footer — Công ty",
            ("Giới thiệu", "/gioi-thieu", null), ("Dự án", "/du-an", null), ("Blog", "/blog", null),
            ("Liên hệ", "/lien-he", null));
        Add("footer-technology", "Footer — Công nghệ",
            ("React", "/dich-vu/react-development", null), ("ASP.NET", "/dich-vu/asp-net-development", null),
            ("Flutter", "/dich-vu/flutter-development", null));
        Add("footer-legal", "Footer — Pháp lý",
            ("Chính sách bảo mật", "/chinh-sach-bao-mat", null), ("Điều khoản sử dụng", "/dieu-khoan-su-dung", null),
            ("Chính sách cookie", "/chinh-sach-cookie", null));
    }

    /// <summary>
    /// Trang chu (muc 64) dung Page builder: thu tu section theo flow cua yeu cau, block dong lay du lieu tu CMS.
    /// Van ban duy nhat duoc dien la cac cau trong yeu cau du an.
    /// </summary>
    private async Task SeedPagesAsync(CancellationToken ct)
    {
        if (await db.Set<Page>().IgnoreQueryFilters().AnyAsync(p => p.Path == "/", ct)) return;

        var home = new Page { Title = "Trang chủ", Path = "/", PageType = PageType.Home, Status = ContentStatus.Draft };

        void Section(string name, string tone, params (string Type, object Data)[] blocks)
        {
            var section = new PageSection
            {
                PageId = home.Id, Name = name, SortOrder = home.Sections.Count,
                SettingsJson = Json(new { tone, width = "content", padding = "lg" }),
            };
            for (var i = 0; i < blocks.Length; i++)
                section.Blocks.Add(new PageBlock
                {
                    SectionId = section.Id, Type = blocks[i].Type, SortOrder = i, DataJson = Json(blocks[i].Data),
                });
            home.Sections.Add(section);
        }

        Section("01 Hero", "light", ("HERO", new
        {
            title = "Chúng tôi xây phần mềm được sử dụng trong vận hành thực tế.",
            subtitle = "Website, Mobile App, POS, PMS, ERP và hệ thống quản trị doanh nghiệp — từ thiết kế, phát triển đến triển khai.",
            primaryCta = new { label = "Xem dự án đã triển khai", url = "/du-an" },
            secondaryCta = new { label = "Trao đổi dự án", url = "/lien-he" },
            visual = new { source = "featuredProjectScreenshots", limit = 4 },
        }));
        Section("02 Trust / Technologies", "light", ("TECH_STACK", new
        {
            capabilities = new[] { "Website", "Mobile", "ERP", "POS", "PMS", "HRM", "Ecommerce", "API Integration" },
            source = "all", layout = "strip",
        }));
        Section("03 Sản phẩm", "dark", ("PRODUCTS", new
        {
            title = "Sản phẩm của Nguyên Bình", source = "featured", limit = 3,
        }));
        Section("04 Dự án nổi bật", "light", ("PROJECTS", new
        {
            title = "Sản phẩm thật. Hệ thống thật. Đã đưa vào vận hành.", source = "featured", limit = 6,
            cta = new { label = "Tất cả dự án", url = "/du-an" },
        }));
        Section("05 Dịch vụ", "subtle", ("SERVICES", new
        {
            title = "Có quy trình riêng? Chúng tôi xây phần mềm theo quy trình của doanh nghiệp bạn.", source = "featured",
            limit = 10, cta = new { label = "Mô tả dự án của bạn", url = "/lien-he" },
        }));
        Section("06 Ngành", "light", ("INDUSTRIES", new { title = "Giải pháp theo ngành", source = "all" }));
        Section("07 Case study nổi bật", "dark", ("PROJECTS", new { source = "highlight", limit = 1, layout = "highlight" }));
        Section("08 Quy trình", "light", ("TIMELINE", new
        {
            title = "Từ ý tưởng đến hệ thống vận hành",
            items = new[]
            {
                new { title = "Discovery", description = "", output = "" },
                new { title = "Business Analysis", description = "", output = "" },
                new { title = "UX/UI", description = "", output = "" },
                new { title = "Development", description = "", output = "" },
                new { title = "Testing", description = "", output = "" },
                new { title = "Deploy & Support", description = "", output = "" },
            },
        }));
        Section("09 Tech stack", "subtle", ("TECH_STACK", new { title = "Công nghệ", source = "all", layout = "grouped" }));
        Section("10 Testimonials", "light", ("TESTIMONIALS", new { source = "all", limit = 6 }));
        Section("11 Blog", "light", ("BLOG", new { title = "Bài viết mới", source = "latest", limit = 3 }));
        Section("12 CTA", "dark", ("CTA", new
        {
            title = "Bạn có một ý tưởng cần triển khai?",
            primaryCta = new { label = "Gửi yêu cầu", url = "/lien-he" },
            secondaryCta = new { label = "Nhận tư vấn", url = "/lien-he" },
            tertiaryCta = new { label = "Yêu cầu Demo", url = "/yeu-cau-demo" },
        }));

        db.Set<Page>().Add(home);
        logger.LogInformation("Seeded home page");
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });
}
