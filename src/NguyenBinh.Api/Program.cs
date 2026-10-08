using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NguyenBinh.Api.Authorization;
using NguyenBinh.Api.Infrastructure;
using NguyenBinh.Application;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Infrastructure;
using NguyenBinh.Infrastructure.Auth;
using NguyenBinh.Infrastructure.Persistence;
using NguyenBinh.Infrastructure.Storage;
using NguyenBinh.Shared.Results;
using Serilog;
using Serilog.Sinks.MSSqlServer;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    builder.Host.UseSerilog((context, services, logger) =>
    {
        logger.ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();

        var connection = context.Configuration.GetConnectionString("Default");
        if (context.Configuration.GetValue("Serilog:SqlSink:Enabled", false) && !string.IsNullOrWhiteSpace(connection))
            logger.WriteTo.MSSqlServer(connection,
                new MSSqlServerSinkOptions { TableName = "SystemLogs", AutoCreateSqlTable = true },
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
    });

    // ---- Application & Infrastructure ----
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
    builder.Services.AddApplication(config);
    builder.Services.AddInfrastructure(config, builder.Environment);
    builder.Services.Configure<RefreshCookieOptions>(config.GetSection(RefreshCookieOptions.Section));

    // ---- MVC + JSON: camelCase, enum dang UPPER_SNAKE (CUSTOM_PROJECT...) ----
    builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            // Giu nguyen tieng Viet trong JSON (an toan vi luon tra application/json).
            o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        })
        .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(ApiResponse.Fail("Yêu cầu không hợp lệ.",
                context.ModelState.Where(e => e.Value?.Errors.Count > 0).ToDictionary(
                    e => string.IsNullOrEmpty(e.Key) ? "body" : char.ToLowerInvariant(e.Key[0]) + e.Key[1..].TrimStart('$', '.'),
                    e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Giá trị không hợp lệ." : x.ErrorMessage).ToArray()))));

    // ---- Auth: JWT bearer + permission policy dong ----
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwt.SigningKey(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = AppClaims.Name,
                RoleClaimType = AppClaims.Role,
            };
            o.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await WriteJsonAsync(context.Response, StatusCodes.Status401Unauthorized,
                        "Bạn cần đăng nhập để thực hiện thao tác này.");
                },
                OnForbidden = context => WriteJsonAsync(context.Response, StatusCodes.Status403Forbidden,
                    "Bạn không có quyền thực hiện thao tác này."),
            };
        });
    builder.Services.AddAuthorization(o =>
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

    // ---- Rate limiting (muc 44) ----
    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.OnRejected = async (context, ct) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
            await WriteJsonAsync(context.HttpContext.Response, StatusCodes.Status429TooManyRequests,
                "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.");
        };

        o.AddPolicy(RateLimitPolicies.Auth, http => FixedWindowByIp(http, config.GetValue("RateLimits:AuthPerMinute", 10)));
        o.AddPolicy(RateLimitPolicies.PublicForms, http => FixedWindowByIp(http, config.GetValue("RateLimits:FormsPerMinute", 5)));
        o.AddPolicy(RateLimitPolicies.PublicApi, http => FixedWindowByIp(http, config.GetValue("RateLimits:PublicPerMinute", 300)));

        // Admin API: gioi han theo user (hoac IP neu chua dang nhap).
        o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            http.Request.Path.StartsWithSegments("/api/v1/admin")
                ? RateLimitPartition.GetFixedWindowLimiter(
                    http.User.FindFirst(AppClaims.Subject)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = config.GetValue("RateLimits:AdminPerMinute", 600), Window = TimeSpan.FromMinutes(1),
                    })
                : RateLimitPartition.GetNoLimiter("public"));
    });

    // ---- CORS, proxy, nen, health, swagger ----
    var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Retry-After")));

    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        if (config.GetValue("ForwardedHeaders:TrustAllProxies", false))
        {
            // API chi lo ra sau nginx trong mang docker noi bo.
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        }
    });

    builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
    builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

    var swaggerEnabled = config.GetValue("Swagger:Enabled", false);
    if (swaggerEnabled)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "Nguyên Bình Official API", Version = "v1" });
            o.CustomSchemaIds(t => t.FullName?.Replace('+', '.'));
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                    { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
            });
        });
    }

    var app = builder.Build();

    // ---- Migrate + seed ----
    await using (var scope = app.Services.CreateAsyncScope())
    {
        if (config.GetValue("Database:SeedOnStartup", true))
            await scope.ServiceProvider.GetRequiredService<DbInitializer>()
                .InitializeAsync(config.GetValue("Database:MigrateOnStartup", false));
    }

    // Lenh mot lan: nhap noi dung trinh bay du an (khong mo cong HTTP). Vd: dotnet NguyenBinh.Api.dll import-showcase /seed
    if (args is ["import-showcase", var showcaseDirectory, ..])
    {
        await using var scope = app.Services.CreateAsyncScope();
        var failures = await scope.ServiceProvider.GetRequiredService<NguyenBinh.Infrastructure.Persistence.ShowcaseImporter>()
            .ImportAsync(showcaseDirectory);
        Log.Information("import-showcase xong, {Failures} loi", failures);
        Environment.ExitCode = failures == 0 ? 0 : 1;
        return;
    }

    // ---- Pipeline ----
    app.UseForwardedHeaders();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(o => o.GetLevel = (http, _, ex) =>
        ex is not null || http.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
        : Serilog.Events.LogEventLevel.Information);
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    if (!app.Environment.IsDevelopment()) app.UseHsts();
    app.UseResponseCompression();

    // Media public: key khong bao gio tai su dung → cache 1 nam immutable (CDN-ready).
    var storageOptions = config.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
    var publicMediaDirectory = Directory.CreateDirectory(storageOptions.PublicDirectory(app.Environment.ContentRootPath));
    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".avif"] = "image/avif";
    contentTypes.Mappings[".webp"] = "image/webp";
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(publicMediaDirectory.FullName),
        RequestPath = storageOptions.PublicRequestPath,
        ContentTypeProvider = contentTypes,
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        },
    });

    if (swaggerEnabled)
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"));
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })
        .AllowAnonymous();
    if (swaggerEnabled)
        app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static RateLimitPartition<string> FixedWindowByIp(HttpContext http, int permitsPerMinute) =>
    RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) });

static Task WriteJsonAsync(HttpResponse response, int status, string message)
{
    if (response.HasStarted) return Task.CompletedTask;
    response.StatusCode = status;
    response.ContentType = "application/json; charset=utf-8";
    return response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message),
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
}

public partial class Program;
