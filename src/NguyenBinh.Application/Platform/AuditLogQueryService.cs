using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Application.Platform;

public sealed class AuditLogQuery : PageQuery
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Guid? UserId { get; set; }
    public string? Action { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string Action,
    string? EntityType,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    string? ChangedColumns,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    DateTimeOffset CreatedAt);

public interface IAuditLogQueryService
{
    Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken ct = default);
}

internal sealed class AuditLogQueryService(IAppDbContext db) : IAuditLogQueryService
{
    private static readonly SortMap<Domain.Platform.AuditLog> Sorts = new SortMap<Domain.Platform.AuditLog>()
        .Add("createdAt", a => a.CreatedAt)
        .Add("action", a => a.Action)
        .Add("entityType", a => a.EntityType);

    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EntityType)) logs = logs.Where(a => a.EntityType == query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.EntityId)) logs = logs.Where(a => a.EntityId == query.EntityId);
        if (query.UserId is { } userId) logs = logs.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Action)) logs = logs.Where(a => a.Action == query.Action);
        if (query.From is { } from) logs = logs.Where(a => a.CreatedAt >= from);
        if (query.To is { } to) logs = logs.Where(a => a.CreatedAt <= to);
        if (query.Search is { } q)
            logs = logs.Where(a => (a.UserName != null && a.UserName.Contains(q)) ||
                                   (a.EntityId != null && a.EntityId.Contains(q)));

        return await Sorts.Apply(logs, query.Sort, "-createdAt")
            .Select(a => new AuditLogDto(a.Id, a.UserId, a.UserName, a.Action, a.EntityType, a.EntityId, a.OldValues,
                a.NewValues, a.ChangedColumns, a.IpAddress, a.UserAgent, a.CorrelationId, a.CreatedAt))
            .ToPagedAsync(query, ct);
    }
}
