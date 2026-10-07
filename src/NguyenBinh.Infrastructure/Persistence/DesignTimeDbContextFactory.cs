using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NguyenBinh.Application.Common.Abstractions;

namespace NguyenBinh.Infrastructure.Persistence;

/// <summary>
/// Dung cho "dotnet ef migrations add". Lenh "database update" nen chay qua API (Database:MigrateOnStartup)
/// hoac truyen --connection de khong phu thuoc chuoi ket noi o day.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                         ?? "Server=(localdb)\\MSSQLLocalDB;Database=NguyenBinhOfficial_Design;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options, SystemCurrentUser.Instance, TimeProvider.System);
    }
}

/// <summary>Nguoi dung "he thong" cho job nen / design-time (khong co HttpContext).</summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public static readonly SystemCurrentUser Instance = new();

    public Guid? UserId => null;
    public string? UserName => "system";
    public IReadOnlyList<string> Roles => [];
    public bool IsAuthenticated => false;
    public string? IpAddress => null;
    public string? UserAgent => null;
    public string? CorrelationId => null;
}
