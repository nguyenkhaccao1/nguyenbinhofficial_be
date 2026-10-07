using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NguyenBinh.Api.Authorization;
using NguyenBinh.Api.Infrastructure;
using NguyenBinh.Application.Platform;
using NguyenBinh.Application.Settings;
using NguyenBinh.Shared.Authorization;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers.Admin;

[Route("api/v1/admin/settings")]
public sealed class SettingsController(ISettingsService settings) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Settings.View)]
    public async Task<ActionResult<ApiResponse<IReadOnlyDictionary<string, object>>>> GetAll(CancellationToken ct) =>
        Success(await settings.GetAllAsync(ct));

    [HttpGet("{group}")]
    [HasPermission(Permissions.Settings.View)]
    public async Task<ActionResult<ApiResponse<object>>> Get(string group, CancellationToken ct) =>
        Success(await settings.GetGroupAsync(group, ct));

    [HttpPut("{group}")]
    [HasPermission(Permissions.Settings.Update)]
    public async Task<ActionResult<ApiResponse<object>>> Update(string group, [FromBody] JsonElement value,
        CancellationToken ct) => Success(await settings.UpdateGroupAsync(group, value, ct), "Đã lưu cấu hình.");
}

[Route("api/v1/admin/audit-logs")]
public sealed class AuditLogsController(IAuditLogQueryService auditLogs) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Audit.View)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> List([FromQuery] AuditLogQuery query,
        CancellationToken ct) => Success(await auditLogs.ListAsync(query, ct));
}

/// <summary>Cau hinh public cho website (brand, theme, contact, social, tracking, seo).</summary>
[Route("api/v1/site")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicApi)]
public sealed class SiteController(ISettingsService settings) : ApiControllerBase
{
    [HttpGet("settings")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<IReadOnlyDictionary<string, object>>>> Settings(CancellationToken ct) =>
        Success(await settings.GetPublicAsync(ct));
}
