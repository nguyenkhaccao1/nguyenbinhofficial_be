using FluentValidation;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.Application.Media;

public sealed class MediaListQuery : PageQuery
{
    public Guid? FolderId { get; set; }

    /// <summary>true: chi file o thu muc goc (FolderId null). Bo qua khi co FolderId.</summary>
    public bool RootOnly { get; set; }

    public MediaKind? Kind { get; set; }
    public string? Tag { get; set; }
}

public sealed record MediaVariantDto(string Format, int Width, int Height, string Url, long SizeBytes);

public sealed record MediaDto(
    Guid Id,
    Guid? FolderId,
    string FileName,
    string OriginalName,
    string? Url,
    string MimeType,
    string Extension,
    MediaKind Kind,
    long SizeBytes,
    int? Width,
    int? Height,
    string? Title,
    string? Alt,
    string? Caption,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MediaVariantDto> Variants,
    MediaProcessingState ProcessingState,
    string? BlurDataUrl,
    bool IsPrivate,
    int UsageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record MediaUsageDto(string EntityType, string EntityId, string Field);

public sealed record MediaDetailDto(MediaDto Media, IReadOnlyList<MediaUsageDto> Usages);

/// <summary>File upload trung lap voi tang HTTP (IFormFile duoc boc o Api).</summary>
public sealed record UploadFile(string FileName, long Length, Func<Stream> OpenReadStream);

public sealed record UploadResultItem(string FileName, bool Success, MediaDto? Media, string? Error);

public sealed record UpdateMediaRequest(
    string FileName,
    string? Title,
    string? Alt,
    string? Caption,
    IReadOnlyList<string>? Tags,
    Guid? FolderId);

public sealed record CropMediaRequest(int X, int Y, int Width, int Height);

public sealed record MediaBulkRequest(string Action, IReadOnlyList<Guid> Ids, Guid? FolderId, bool Force = false);

public sealed record MediaFolderDto(Guid Id, string Name, Guid? ParentId, int FileCount);

public sealed record SaveMediaFolderRequest(string Name, Guid? ParentId);

internal sealed class UpdateMediaRequestValidator : AbstractValidator<UpdateMediaRequest>
{
    public UpdateMediaRequestValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().WithMessage("Vui lòng nhập tên file.").MaximumLength(255)
            .Must(n => n.IndexOfAny(Path.GetInvalidFileNameChars()) < 0).WithMessage("Tên file chứa ký tự không hợp lệ.");
        RuleFor(x => x.Title).MaximumLength(255);
        RuleFor(x => x.Alt).MaximumLength(300);
        RuleFor(x => x.Caption).MaximumLength(1000);
        RuleFor(x => x.Tags).Must(t => t is null || t.Count <= 20).WithMessage("Tối đa 20 tag.");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50);
    }
}

internal sealed class CropMediaRequestValidator : AbstractValidator<CropMediaRequest>
{
    public CropMediaRequestValidator()
    {
        RuleFor(x => x.X).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Width).GreaterThan(0);
        RuleFor(x => x.Height).GreaterThan(0);
    }
}

internal sealed class MediaBulkRequestValidator : AbstractValidator<MediaBulkRequest>
{
    public MediaBulkRequestValidator()
    {
        RuleFor(x => x.Action).Must(a => a is "delete" or "move").WithMessage("Thao tác phải là 'delete' hoặc 'move'.");
        RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 200).WithMessage("Chọn từ 1 đến 200 file.");
    }
}

internal sealed class SaveMediaFolderRequestValidator : AbstractValidator<SaveMediaFolderRequest>
{
    public SaveMediaFolderRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên thư mục.").MaximumLength(100)
            .Must(n => n.IndexOfAny(['/', '\\']) < 0).WithMessage("Tên thư mục không chứa '/' hoặc '\\'.");
    }
}
