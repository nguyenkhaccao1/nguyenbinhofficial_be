using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Infrastructure.Auth;
using NguyenBinh.Infrastructure.Caching;
using NguyenBinh.Infrastructure.Media;
using NguyenBinh.Infrastructure.Persistence;
using NguyenBinh.Infrastructure.Storage;

namespace NguyenBinh.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                                   "Thiếu ConnectionStrings:Default (đặt qua user-secrets hoặc biến môi trường ConnectionStrings__Default).");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 5);
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        }));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DbInitializer>();
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));

        // Identity (khong dung cookie auth cua Identity — admin dung JWT).
        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<VietnameseIdentityErrorDescriber>();

        var storageOptions = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
        services.AddDataProtection()
            .SetApplicationName("NguyenBinhOfficial")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(
                storageOptions.PrivateDirectory(environment.ContentRootPath), "dp-keys")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Cache: Redis khi co cau hinh, nguoc lai memory (Development/test).
        var redis = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redis))
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "nb:";
            });
        else
            services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.AddSingleton<IFileStorage>(sp =>
            new LocalFileStorage(sp.GetRequiredService<IOptions<StorageOptions>>(), environment.ContentRootPath));

        services.AddSingleton<IImageProcessor, VipsImageProcessor>();
        services.AddSingleton<IMalwareScanner, NoopMalwareScanner>();
        services.AddSingleton<MediaProcessingQueue>();
        services.AddSingleton<IMediaProcessingQueue>(sp => sp.GetRequiredService<MediaProcessingQueue>());
        services.AddHostedService<MediaProcessingWorker>();

        return services;
    }
}
