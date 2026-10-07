using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NguyenBinh.Shared.Authorization;

namespace NguyenBinh.Api.Authorization;

/// <summary>Module quyen cua controller noi dung (vd "project" → project.view, project.create...).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PermissionModuleAttribute(string module) : Attribute
{
    public string Module { get; } = module;
}

/// <summary>
/// Quyen cho action dung chung trong ContentAdminController: ghep module cua controller voi action
/// ("project" + "publish" → project.publish) roi kiem tra bang policy permission thong thuong.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ContentPermissionAttribute(string action) : Attribute, IAsyncAuthorizationFilter
{
    public string Action { get; } = action;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor) return;

        var module = descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(PermissionModuleAttribute), true)
            .Cast<PermissionModuleAttribute>().FirstOrDefault()?.Module
            ?? throw new InvalidOperationException($"{descriptor.ControllerName} thiếu [PermissionModule].");

        var permission = $"{module}.{Action}";
        if (!Permissions.Exists(permission))
            throw new InvalidOperationException($"Permission '{permission}' không được khai báo.");

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var authorization = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(user, HasPermissionAttribute.PolicyPrefix + permission);
        if (!result.Succeeded) context.Result = new ForbidResult();
    }
}

public static class ContentActions
{
    public const string View = "view";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Publish = "publish";
}
