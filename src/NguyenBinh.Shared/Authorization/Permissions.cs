using System.Reflection;

namespace NguyenBinh.Shared.Authorization;

public sealed record PermissionDefinition(string Code, string Module, string Action);

/// <summary>
/// Nguon su that duy nhat cua permission. Seeder dong bo bang Permissions tu danh sach nay
/// khi khoi dong; backend enforce bang [HasPermission(Permissions.X.Y)].
/// Quy uoc ma: "{module}.{action}".
/// </summary>
public static class Permissions
{
    public static class Dashboard
    {
        public const string View = "dashboard.view";
    }

    public static class Pages
    {
        public const string View = "page.view";
        public const string Create = "page.create";
        public const string Update = "page.update";
        public const string Delete = "page.delete";
        public const string Publish = "page.publish";
        public const string CustomHtml = "page.custom_html";
    }

    public static class Menus
    {
        public const string View = "menu.view";
        public const string Update = "menu.update";
    }

    public static class Products
    {
        public const string View = "product.view";
        public const string Create = "product.create";
        public const string Update = "product.update";
        public const string Delete = "product.delete";
        public const string Publish = "product.publish";
    }

    public static class Projects
    {
        public const string View = "project.view";
        public const string Create = "project.create";
        public const string Update = "project.update";
        public const string Delete = "project.delete";
        public const string Publish = "project.publish";
    }

    public static class Services
    {
        public const string View = "service.view";
        public const string Create = "service.create";
        public const string Update = "service.update";
        public const string Delete = "service.delete";
        public const string Publish = "service.publish";
    }

    public static class Blog
    {
        public const string View = "blog.view";
        public const string Create = "blog.create";
        public const string Update = "blog.update";
        public const string Delete = "blog.delete";
        public const string Publish = "blog.publish";
    }

    public static class Library
    {
        public const string View = "library.view";
        public const string Create = "library.create";
        public const string Update = "library.update";
        public const string Delete = "library.delete";
        public const string Publish = "library.publish";
    }

    public static class Media
    {
        public const string View = "media.view";
        public const string Upload = "media.upload";
        public const string Update = "media.update";
        public const string Delete = "media.delete";
    }

    public static class Leads
    {
        public const string View = "lead.view";
        public const string Create = "lead.create";
        public const string Update = "lead.update";
        public const string Delete = "lead.delete";
        public const string Assign = "lead.assign";
        public const string Export = "lead.export";
    }

    public static class Seo
    {
        public const string View = "seo.view";
        public const string Update = "seo.update";
        public const string Redirect = "seo.redirect";
        public const string Sitemap = "seo.sitemap";
    }

    public static class Settings
    {
        public const string View = "settings.view";
        public const string Update = "settings.update";
    }

    public static class Users
    {
        public const string View = "user.view";
        public const string Create = "user.create";
        public const string Update = "user.update";
        public const string Delete = "user.delete";
    }

    public static class Roles
    {
        public const string View = "role.view";
        public const string Create = "role.create";
        public const string Update = "role.update";
        public const string Delete = "role.delete";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class System
    {
        public const string View = "system.view";
        public const string Purge = "system.purge";
    }

    private static readonly Lazy<IReadOnlyList<PermissionDefinition>> _all = new(() =>
        typeof(Permissions).GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Select(code =>
            {
                var dot = code.IndexOf('.');
                return new PermissionDefinition(code, code[..dot], code[(dot + 1)..]);
            })
            .ToList());

    public static IReadOnlyList<PermissionDefinition> All => _all.Value;

    public static IEnumerable<string> AllCodes => All.Select(p => p.Code);

    public static bool Exists(string code) => All.Any(p => p.Code == code);
}
