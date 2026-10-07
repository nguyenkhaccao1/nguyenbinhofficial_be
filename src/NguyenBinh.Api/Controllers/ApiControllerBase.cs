using Microsoft.AspNetCore.Mvc;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Infrastructure.Auth;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string? message = null) =>
        Ok(ApiResponse.Ok(data, message));

    protected ActionResult<ApiResponse<object?>> Success(string? message = null) =>
        Ok(ApiResponse.Ok<object?>(null, message));

    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(AppClaims.Subject)?.Value, out var id) ? id : throw new UnauthorizedException();
}
