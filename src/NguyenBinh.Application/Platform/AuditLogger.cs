using System.Text.Json;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Platform;

namespace NguyenBinh.Application.Platform;

/// <summary>
/// Ghi audit cho hanh dong khong phai thay doi entity thong thuong (dang nhap, publish, doi quyen...).
/// Thay doi entity (CREATE/UPDATE/DELETE) duoc DbContext ghi tu dong.
/// </summary>
public interface IAuditLogger
{
    /// <summary>Them ban ghi vao DbContext; luu cung transaction voi lan SaveChanges ke tiep.</summary>
    void Add(string action, string? entityType = null, string? entityId = null, object? newValues = null,
        object? oldValues = null, Guid? userId = null, string? userName = null);

    /// <summary>Them va luu ngay (dung khi khong co thay doi nao khac, vd LOGIN_FAILED).</summary>
    Task LogAsync(string action, string? entityType = null, string? entityId = null, object? newValues = null,
        Guid? userId = null, string? userName = null, CancellationToken ct = default);
}

internal sealed class AuditLogger(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditLogger
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Add(string action, string? entityType = null, string? entityId = null, object? newValues = null,
        object? oldValues = null, Guid? userId = null, string? userName = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? currentUser.UserId,
            UserName = userName ?? currentUser.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, Json),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, Json),
            IpAddress = currentUser.IpAddress,
            UserAgent = Truncate(currentUser.UserAgent, 512),
            CorrelationId = currentUser.CorrelationId,
            CreatedAt = clock.GetUtcNow(),
        });
    }

    public async Task LogAsync(string action, string? entityType = null, string? entityId = null,
        object? newValues = null, Guid? userId = null, string? userName = null, CancellationToken ct = default)
    {
        Add(action, entityType, entityId, newValues, null, userId, userName);
        await db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
