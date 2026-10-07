using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NguyenBinh.Application.Settings;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Domain.Settings;
using NguyenBinh.Shared.Authorization;

namespace NguyenBinh.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Tai khoan SuperAdmin dau tien — chi tao khi chua co SuperAdmin. Lay tu bien moi truong/secret.</summary>
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminFullName { get; set; } = "Quản trị hệ thống";
}

/// <summary>
/// Migrate + seed idempotent: dong bo permission tu code, tao role he thong, SuperAdmin dau tien,
/// cau hinh mac dinh. Chay lai nhieu lan khong ghi de thay doi cua admin.
/// </summary>
public sealed class DbInitializer(
    AppDbContext db,
    RoleManager<AppRole> roleManager,
    UserManager<AppUser> userManager,
    IOptions<SeedOptions> seedOptions,
    ILogger<DbInitializer> logger)
{
    private static readonly Dictionary<string, string> RoleDescriptions = new()
    {
        [SystemRoles.SuperAdmin] = "Toàn quyền hệ thống.",
        [SystemRoles.Admin] = "Quản trị website, người dùng và cấu hình.",
        [SystemRoles.Editor] = "Biên tập nội dung: trang, sản phẩm, dự án, dịch vụ, blog, media.",
        [SystemRoles.Seo] = "Tối ưu SEO: metadata, redirect, sitemap, blog.",
        [SystemRoles.Sales] = "Quản lý lead, yêu cầu báo giá và demo.",
        [SystemRoles.Viewer] = "Chỉ xem.",
    };

    public async Task InitializeAsync(bool migrate, CancellationToken ct = default)
    {
        if (migrate)
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync(ct);
        }

        await SyncPermissionsAsync(ct);
        await SeedRolesAsync(ct);
        await SeedAdminAsync();
        await SeedSettingsAsync(ct);
    }

    private async Task SyncPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.ToListAsync(ct);
        var defined = Permissions.All;

        foreach (var def in defined.Where(d => existing.All(e => e.Code != d.Code)))
            db.Permissions.Add(new Permission { Code = def.Code, Module = def.Module, Action = def.Action });

        var obsolete = existing.Where(e => defined.All(d => d.Code != e.Code)).ToList();
        if (obsolete.Count > 0)
        {
            var codes = obsolete.Select(o => o.Code).ToList();
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(rp => codes.Contains(rp.PermissionCode)));
            db.Permissions.RemoveRange(obsolete);
            logger.LogInformation("Removed obsolete permissions: {Codes}", string.Join(", ", codes));
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        foreach (var name in SystemRoles.All)
        {
            if (await roleManager.RoleExistsAsync(name)) continue;

            var role = new AppRole(name) { IsSystem = true, Description = RoleDescriptions[name] };
            // SuperAdmin luon toan quyen (kiem tra trong code), khong can luu tung quyen.
            if (name != SystemRoles.SuperAdmin)
                foreach (var code in SystemRoles.DefaultPermissions(name))
                    role.Permissions.Add(new RolePermission { PermissionCode = code });

            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Seed role {name} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            logger.LogInformation("Created system role {Role}", name);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminAsync()
    {
        if ((await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin)).Count > 0) return;

        var options = seedOptions.Value;
        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning("Chua co SuperAdmin. Dat Seed__AdminEmail va Seed__AdminPassword de tao tai khoan dau tien.");
            return;
        }

        var user = await userManager.FindByEmailAsync(options.AdminEmail);
        if (user is null)
        {
            user = new AppUser
            {
                Email = options.AdminEmail,
                UserName = options.AdminEmail,
                FullName = options.AdminFullName,
                EmailConfirmed = true,
            };
            var created = await userManager.CreateAsync(user, options.AdminPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Seed admin failed: {string.Join("; ", created.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, SystemRoles.SuperAdmin);
        logger.LogInformation("Created SuperAdmin {Email}", options.AdminEmail);
    }

    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        var existing = await db.SiteSettings.IgnoreQueryFilters().Select(s => s.Key).ToListAsync(ct);
        foreach (var group in SettingKeys.All.Where(g => !existing.Contains(g.Key)))
        {
            db.SiteSettings.Add(new SiteSetting
            {
                Key = group.Key,
                IsPublic = group.IsPublic,
                ValueJson = JsonSerializer.Serialize(Activator.CreateInstance(group.Type), group.Type,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
