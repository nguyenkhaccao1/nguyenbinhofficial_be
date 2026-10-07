using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NguyenBinh.Api.Infrastructure;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Identity.Auth;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers.Admin;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

/// <summary>
/// Dang nhap admin. Access token tra trong body (FE giu trong memory); refresh token chi nam trong
/// cookie httpOnly + SameSite=Strict, path gioi han o /api/v1/admin/auth. Endpoint dung cookie
/// (refresh/logout) yeu cau them header X-Requested-With de chan CSRF.
/// </summary>
[Route("api/v1/admin/auth")]
public sealed class AuthController(IAuthService authService, IOptions<RefreshCookieOptions> cookieOptions)
    : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken ct)
    {
        var session = await authService.LoginAsync(request, ct);
        return Success(Issue(session));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh(CancellationToken ct)
    {
        EnsureAntiForgeryHeader();
        try
        {
            var session = await authService.RefreshAsync(Request.Cookies[cookieOptions.Value.Name] ?? string.Empty, ct);
            return Success(Issue(session));
        }
        catch (UnauthorizedException)
        {
            ClearCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object?>>> Logout(CancellationToken ct)
    {
        EnsureAntiForgeryHeader();
        await authService.LogoutAsync(Request.Cookies[cookieOptions.Value.Name], ct);
        ClearCookie();
        return Success("Đã đăng xuất.");
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken ct) =>
        Success(await authService.GetCurrentUserAsync(CurrentUserId, ct));

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> UpdateProfile(UpdateProfileRequest request,
        CancellationToken ct) =>
        Success(await authService.UpdateProfileAsync(CurrentUserId, request, ct), "Đã cập nhật hồ sơ.");

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> ChangePassword(ChangePasswordRequest request,
        CancellationToken ct)
    {
        var session = await authService.ChangePasswordAsync(CurrentUserId, request, ct);
        return Success(Issue(session), "Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.");
    }

    private LoginResponse Issue(AuthSession session)
    {
        var options = cookieOptions.Value;
        Response.Cookies.Append(options.Name, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.Secure,
            SameSite = SameSiteMode.Strict,
            Path = options.Path,
            Expires = session.IsPersistent ? session.RefreshTokenExpiresAt : null,
            IsEssential = true,
        });
        return new LoginResponse(session.AccessToken, session.AccessTokenExpiresAt, session.User);
    }

    private void ClearCookie()
    {
        var options = cookieOptions.Value;
        Response.Cookies.Delete(options.Name, new CookieOptions
        {
            HttpOnly = true, Secure = options.Secure, SameSite = SameSiteMode.Strict, Path = options.Path,
        });
    }

    private void EnsureAntiForgeryHeader()
    {
        if (Request.Headers[RefreshCookieOptions.AntiForgeryHeader] != RefreshCookieOptions.AntiForgeryValue)
            throw new ForbiddenException("Thiếu header xác thực yêu cầu.");
    }
}
