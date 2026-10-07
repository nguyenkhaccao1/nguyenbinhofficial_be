namespace NguyenBinh.Shared.Authorization;

/// <summary>
/// Role he thong va ma tran quyen mac dinh (docs/design/10-modules-permissions.md).
/// Ma tran chi duoc ap khi role duoc tao lan dau; sau do admin tu chinh trong man Roles
/// (tru SuperAdmin luon co toan quyen).
/// </summary>
public static class SystemRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Seo = "SEO";
    public const string Sales = "Sales";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, Admin, Editor, Seo, Sales, Viewer];

    public static IReadOnlyCollection<string> DefaultPermissions(string role)
    {
        var all = Permissions.AllCodes.ToList();

        IEnumerable<string> codes = role switch
        {
            SuperAdmin => all,
            Admin => all.Where(c => c != Permissions.System.Purge),
            Editor =>
            [
                Permissions.Dashboard.View,
                .. Module(all, "page").Where(c => c != Permissions.Pages.CustomHtml),
                .. Module(all, "menu"),
                .. Module(all, "product"), .. Module(all, "project"), .. Module(all, "service"),
                .. Module(all, "library"), .. Module(all, "blog"), .. Module(all, "media"),
                Permissions.Seo.View, Permissions.Settings.View,
            ],
            Seo =>
            [
                Permissions.Dashboard.View,
                Permissions.Pages.View, Permissions.Pages.Update, Permissions.Menus.View,
                Permissions.Products.View, Permissions.Products.Update,
                Permissions.Projects.View, Permissions.Projects.Update,
                Permissions.Services.View, Permissions.Services.Update,
                Permissions.Library.View, Permissions.Library.Update,
                .. Module(all, "blog"),
                Permissions.Media.View, Permissions.Media.Upload, Permissions.Media.Update,
                .. Module(all, "seo"), Permissions.Settings.View,
            ],
            Sales =>
            [
                Permissions.Dashboard.View,
                Permissions.Products.View, Permissions.Projects.View, Permissions.Services.View,
                Permissions.Library.View, Permissions.Media.View,
                .. Module(all, "lead").Where(c => c != Permissions.Leads.Delete),
            ],
            Viewer =>
            [
                Permissions.Dashboard.View, Permissions.Pages.View, Permissions.Menus.View,
                Permissions.Products.View, Permissions.Projects.View, Permissions.Services.View,
                Permissions.Library.View, Permissions.Blog.View, Permissions.Media.View,
                Permissions.Seo.View, Permissions.Settings.View,
            ],
            _ => [],
        };

        return codes.ToHashSet();
    }

    private static IEnumerable<string> Module(IEnumerable<string> all, string module) =>
        all.Where(c => c.StartsWith(module + ".", StringComparison.Ordinal));
}
