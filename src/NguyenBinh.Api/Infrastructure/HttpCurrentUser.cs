using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Infrastructure.Auth;

namespace NguyenBinh.Api.Infrastructure;

/// <summary>Nguoi dung hien tai lay tu JWT (claim "sub", "role"). Ngoai request (job nen) tra ve null.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId =>
        Guid.TryParse(Context?.User.FindFirst(AppClaims.Subject)?.Value, out var id) ? id : null;

    public string? UserName => Context?.User.FindFirst(AppClaims.Email)?.Value;

    public IReadOnlyList<string> Roles =>
        Context?.User.FindAll(AppClaims.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated ?? false;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();

    public string? CorrelationId => Context?.Items[CorrelationIdMiddleware.ItemKey] as string ?? Context?.TraceIdentifier;
}
