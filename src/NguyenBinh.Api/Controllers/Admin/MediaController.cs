using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenBinh.Api.Authorization;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Media;
using NguyenBinh.Shared.Authorization;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Controllers.Admin;

[Route("api/v1/admin/media")]
public sealed class MediaController(IMediaService media, IAuthorizationService authorization) : ApiControllerBase
{
    private const long MaxRequestBytes = UploadPolicy.VideoMaxBytes + 10 * 1024 * 1024;

    [HttpGet]
    [HasPermission(Permissions.Media.View)]
    public async Task<ActionResult<ApiResponse<PagedResult<MediaDto>>>> List([FromQuery] MediaListQuery query,
        CancellationToken ct) => Success(await media.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Media.View)]
    public async Task<ActionResult<ApiResponse<MediaDetailDto>>> Get(Guid id, CancellationToken ct) =>
        Success(await media.GetAsync(id, ct));

    /// <summary>
    /// Upload nhieu file (multipart, field "files"). Chon thu muc bang folderId, hoac folderPath theo ten hien thi
    /// (vd "Dự án/PerfectKey Workforce" — tu tao cay neu chua co). Moi file tra ket qua rieng.
    /// </summary>
    [HttpPost("upload")]
    [HasPermission(Permissions.Media.Upload)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UploadResultItem>>>> Upload(
        [FromForm] List<IFormFile> files, [FromForm] Guid? folderId, [FromForm] string? folderPath, CancellationToken ct)
    {
        if (files.Count == 0) throw new BusinessValidationException("files", "Chọn ít nhất 1 file.");
        if (files.Count > 50) throw new BusinessValidationException("files", "Tối đa 50 file mỗi lần tải lên.");

        var uploads = files.Select(f => new UploadFile(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var results = await media.UploadAsync(uploads, folderId, folderPath, ct);
        var failed = results.Count(r => !r.Success);
        return Success(results, failed == 0 ? $"Đã tải lên {results.Count} file." : $"{failed}/{results.Count} file bị từ chối.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Media.Update)]
    public async Task<ActionResult<ApiResponse<MediaDto>>> Update(Guid id, UpdateMediaRequest request,
        CancellationToken ct) => Success(await media.UpdateAsync(id, request, ct), "Đã lưu.");

    [HttpPost("{id:guid}/crop")]
    [HasPermission(Permissions.Media.Upload)]
    public async Task<ActionResult<ApiResponse<MediaDto>>> Crop(Guid id, CropMediaRequest request,
        CancellationToken ct) => Success(await media.CropAsync(id, request, ct), "Đã tạo ảnh cắt.");

    /// <summary>409 kem danh sach noi dang dung neu media dang duoc su dung; gui ?force=true de van xoa.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Media.Delete)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, [FromQuery] bool force, CancellationToken ct)
    {
        await media.DeleteAsync(id, force, ct);
        return Success("Đã xoá.");
    }

    [HttpPost("bulk")]
    [HasPermission(Permissions.Media.View)]
    public async Task<ActionResult<ApiResponse<object?>>> Bulk(MediaBulkRequest request, CancellationToken ct)
    {
        var required = request.Action == "delete" ? Permissions.Media.Delete : Permissions.Media.Update;
        var allowed = await authorization.AuthorizeAsync(User, HasPermissionAttribute.PolicyPrefix + required);
        if (!allowed.Succeeded) throw new ForbiddenException();

        await media.BulkAsync(request, ct);
        return Success("Đã thực hiện.");
    }

    [HttpGet("folders")]
    [HasPermission(Permissions.Media.View)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MediaFolderDto>>>> Folders(CancellationToken ct) =>
        Success(await media.ListFoldersAsync(ct));

    [HttpPost("folders")]
    [HasPermission(Permissions.Media.Update)]
    public async Task<ActionResult<ApiResponse<MediaFolderDto>>> CreateFolder(SaveMediaFolderRequest request,
        CancellationToken ct) => Success(await media.CreateFolderAsync(request, ct), "Đã tạo thư mục.");

    [HttpPut("folders/{id:guid}")]
    [HasPermission(Permissions.Media.Update)]
    public async Task<ActionResult<ApiResponse<MediaFolderDto>>> UpdateFolder(Guid id, SaveMediaFolderRequest request,
        CancellationToken ct) => Success(await media.UpdateFolderAsync(id, request, ct), "Đã lưu.");

    [HttpDelete("folders/{id:guid}")]
    [HasPermission(Permissions.Media.Delete)]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteFolder(Guid id, CancellationToken ct)
    {
        await media.DeleteFolderAsync(id, ct);
        return Success("Đã xoá thư mục.");
    }
}
