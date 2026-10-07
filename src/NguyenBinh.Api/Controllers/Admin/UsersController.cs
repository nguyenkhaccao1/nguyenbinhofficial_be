using Microsoft.AspNetCore.Mvc;
using NguyenBinh.Api.Authorization;
using NguyenBinh.Application.Identity.Roles;
using NguyenBinh.Application.Identity.Users;
using NguyenBinh.Shared.Authorization;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers.Admin;

[Route("api/v1/admin/users")]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserListItemDto>>>> List([FromQuery] UserListQuery query,
        CancellationToken ct) => Success(await users.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Users.View)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> Get(Guid id, CancellationToken ct) =>
        Success(await users.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.Users.Create)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, ApiResponse.Ok(user, "Đã tạo người dùng."));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> Update(Guid id, UpdateUserRequest request,
        CancellationToken ct) => Success(await users.UpdateAsync(id, request, ct), "Đã lưu.");

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Users.Delete)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return Success("Đã xoá người dùng.");
    }

    [HttpPost("{id:guid}/lock")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<ActionResult<ApiResponse<object?>>> Lock(Guid id, CancellationToken ct)
    {
        await users.LockAsync(id, ct);
        return Success("Đã khoá tài khoản.");
    }

    [HttpPost("{id:guid}/unlock")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<ActionResult<ApiResponse<object?>>> Unlock(Guid id, CancellationToken ct)
    {
        await users.UnlockAsync(id, ct);
        return Success("Đã mở khoá tài khoản.");
    }

    [HttpPost("{id:guid}/reset-password")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<ActionResult<ApiResponse<object?>>> ResetPassword(Guid id, ResetPasswordRequest request,
        CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, request, ct);
        return Success("Đã đặt lại mật khẩu.");
    }
}

[Route("api/v1/admin")]
public sealed class RolesController(IRoleService roles) : ApiControllerBase
{
    [HttpGet("roles")]
    [HasPermission(Permissions.Roles.View)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> List(CancellationToken ct) =>
        Success(await roles.ListAsync(ct));

    [HttpGet("roles/{id:guid}")]
    [HasPermission(Permissions.Roles.View)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Get(Guid id, CancellationToken ct) =>
        Success(await roles.GetAsync(id, ct));

    [HttpPost("roles")]
    [HasPermission(Permissions.Roles.Create)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Create(CreateRoleRequest request, CancellationToken ct)
    {
        var role = await roles.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = role.Id }, ApiResponse.Ok(role, "Đã tạo vai trò."));
    }

    [HttpPut("roles/{id:guid}")]
    [HasPermission(Permissions.Roles.Update)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Update(Guid id, UpdateRoleRequest request,
        CancellationToken ct) => Success(await roles.UpdateAsync(id, request, ct), "Đã lưu.");

    [HttpDelete("roles/{id:guid}")]
    [HasPermission(Permissions.Roles.Delete)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, CancellationToken ct)
    {
        await roles.DeleteAsync(id, ct);
        return Success("Đã xoá vai trò.");
    }

    [HttpGet("permissions")]
    [HasPermission(Permissions.Roles.View)]
    public ActionResult<ApiResponse<IReadOnlyList<PermissionDto>>> ListPermissions() => Success(roles.ListPermissions());
}
