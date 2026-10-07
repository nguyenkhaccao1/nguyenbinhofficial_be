using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NguyenBinh.Application.Identity;
using NguyenBinh.Infrastructure.Auth;
using NguyenBinh.Shared.Authorization;

namespace NguyenBinh.Api.Authorization;

/// <summary>[HasPermission(Permissions.Projects.Create)] — backend luon enforce, khong dua vao frontend.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PolicyPrefix + permission)
{
    public const string PolicyPrefix = "perm:";

    public string Permission { get; } = permission;
}

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Tao policy dong cho moi ma "perm:{code}" thay vi dang ky truoc hang tram policy.</summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
        if (!Permissions.Exists(permission))
            throw new InvalidOperationException($"Permission '{permission}' không được khai báo trong Permissions.");

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}

internal sealed class PermissionHandler(IPermissionService permissionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        var roles = context.User.FindAll(AppClaims.Role).Select(c => c.Value).ToList();
        var granted = await permissionService.GetPermissionsAsync(roles);
        if (granted.Contains(requirement.Permission)) context.Succeed(requirement);
    }
}
