using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenBinh.Api.Authorization;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Domain.Common;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers.Admin;

/// <summary>
/// Endpoint chuan cho moi module noi dung (docs/design/06-api.md §3). Controller con chi khai bao
/// [Route] + [PermissionModule]; quyen tung action = module + action (vd project.publish).
/// Ghi dong thoi: gui header If-Match = meta.rowVersion → 409 neu ban ghi da bi nguoi khac sua.
/// </summary>
public abstract class ContentAdminController<TEntity, TListItem, TInput>(
    IContentAdminService<TEntity, TListItem, TInput> service) : ApiControllerBase
    where TEntity : ContentEntity, new()
    where TInput : class
{
    private static readonly HashSet<string> StandardQueryKeys =
        new(["q", "page", "pageSize", "sort", "status", "trash"], StringComparer.OrdinalIgnoreCase);

    [HttpGet]
    [ContentPermission(ContentActions.View)]
    public async Task<ActionResult<ApiResponse<PagedResult<TListItem>>>> List([FromQuery] ContentListQuery query,
        CancellationToken ct)
    {
        var filters = Request.Query.Where(q => !StandardQueryKeys.Contains(q.Key))
            .ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        return Success(await service.ListAsync(query, filters, ct));
    }

    [HttpGet("{id:guid}")]
    [ContentPermission(ContentActions.View)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Get(Guid id, CancellationToken ct) =>
        Success(await service.GetAsync(id, ct));

    [HttpPost]
    [ContentPermission(ContentActions.Create)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Create(TInput input, CancellationToken ct)
    {
        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Meta.Id }, ApiResponse.Ok(created, "Đã tạo."));
    }

    [HttpPut("{id:guid}")]
    [ContentPermission(ContentActions.Update)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Update(Guid id, TInput input,
        [FromHeader(Name = "If-Match")] string? rowVersion, CancellationToken ct) =>
        Success(await service.UpdateAsync(id, input, rowVersion?.Trim('"'), ct), "Đã lưu.");

    [HttpDelete("{id:guid}")]
    [ContentPermission(ContentActions.Delete)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Success("Đã chuyển vào thùng rác.");
    }

    [HttpPost("{id:guid}/restore")]
    [ContentPermission(ContentActions.Delete)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Restore(Guid id, CancellationToken ct) =>
        Success(await service.RestoreAsync(id, ct), "Đã khôi phục.");

    [HttpPost("{id:guid}/publish")]
    [ContentPermission(ContentActions.Publish)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Publish(Guid id, CancellationToken ct) =>
        Success(await service.PublishAsync(id, ct), "Đã xuất bản.");

    [HttpPost("{id:guid}/unpublish")]
    [ContentPermission(ContentActions.Publish)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Unpublish(Guid id, CancellationToken ct) =>
        Success(await service.UnpublishAsync(id, ct), "Đã gỡ xuất bản.");

    [HttpPost("{id:guid}/schedule")]
    [ContentPermission(ContentActions.Publish)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Schedule(Guid id, ScheduleRequest request,
        CancellationToken ct) => Success(await service.ScheduleAsync(id, request.PublishAt, ct), "Đã đặt lịch xuất bản.");

    [HttpPost("{id:guid}/duplicate")]
    [ContentPermission(ContentActions.Create)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> Duplicate(Guid id, CancellationToken ct) =>
        Success(await service.DuplicateAsync(id, ct), "Đã nhân bản (bản nháp).");

    [HttpPatch("reorder")]
    [ContentPermission(ContentActions.Update)]
    public async Task<ActionResult<ApiResponse<object?>>> Reorder(List<ReorderItem> items, CancellationToken ct)
    {
        await service.ReorderAsync(items, ct);
        return Success("Đã lưu thứ tự.");
    }

    [HttpPost("bulk")]
    [ContentPermission(ContentActions.View)]
    public async Task<ActionResult<ApiResponse<BulkResult>>> Bulk(BulkRequest request, CancellationToken ct)
    {
        var action = request.Action switch
        {
            BulkActions.Publish or BulkActions.Unpublish => ContentActions.Publish,
            BulkActions.Delete or BulkActions.Restore => ContentActions.Delete,
            _ => throw new BusinessValidationException("action", "Thao tác hàng loạt không hợp lệ."),
        };
        await EnsurePermissionAsync(action);

        var result = await service.BulkAsync(request, ct);
        return Success(result, result.Failed.Count == 0
            ? $"Đã xử lý {result.Succeeded} mục."
            : $"Thành công {result.Succeeded}, lỗi {result.Failed.Count}.");
    }

    [HttpGet("{id:guid}/versions")]
    [ContentPermission(ContentActions.View)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContentVersionDto>>>> Versions(Guid id, CancellationToken ct) =>
        Success(await service.VersionsAsync(id, ct));

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    [ContentPermission(ContentActions.View)]
    public async Task<ActionResult<ApiResponse<TInput>>> Version(Guid id, Guid versionId, CancellationToken ct) =>
        Success(await service.GetVersionAsync(id, versionId, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/restore")]
    [ContentPermission(ContentActions.Update)]
    public async Task<ActionResult<ApiResponse<ContentDetail<TInput>>>> RestoreVersion(Guid id, Guid versionId,
        CancellationToken ct) => Success(await service.RestoreVersionAsync(id, versionId, ct), "Đã khôi phục phiên bản.");

    [HttpPut("{id:guid}/autosave")]
    [ContentPermission(ContentActions.Update)]
    public async Task<ActionResult<ApiResponse<ContentVersionDto>>> Autosave(Guid id, TInput input, CancellationToken ct) =>
        Success(await service.AutosaveAsync(id, input, ct));

    private async Task EnsurePermissionAsync(string action)
    {
        var module = GetType().GetCustomAttributes(typeof(PermissionModuleAttribute), true)
            .Cast<PermissionModuleAttribute>().First().Module;
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(User, HasPermissionAttribute.PolicyPrefix + $"{module}.{action}");
        if (!result.Succeeded) throw new ForbiddenException();
    }
}
