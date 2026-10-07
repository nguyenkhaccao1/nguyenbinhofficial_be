using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NguyenBinh.Application.Content;
using NguyenBinh.Application.Identity;
using NguyenBinh.Application.Identity.Auth;
using NguyenBinh.Application.Identity.Roles;
using NguyenBinh.Application.Identity.Users;
using NguyenBinh.Application.Media;
using NguyenBinh.Application.Platform;
using NguyenBinh.Application.Settings;

namespace NguyenBinh.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.Section));
        // Validator con (dung qua SetValidator) khong dang ky rieng.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true,
            filter: r => r.ValidatorType != typeof(Content.Menus.MenuChildItemValidator));
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();

        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();

        services.AddScoped<ISettingsService, SettingsService>();

        services.AddScoped<IMediaUsageTracker, MediaUsageTracker>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaVariantProcessor, MediaVariantProcessor>();

        services.AddContentModules();

        return services;
    }
}
