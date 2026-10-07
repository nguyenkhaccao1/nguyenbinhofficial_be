using System.Text.Json;
using System.Text.Json.Serialization;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Domain.Common;

namespace NguyenBinh.Application.Content.Common;

/// <summary>Thong tin vong doi chung cua moi noi dung (tra kem du lieu form).</summary>
public sealed record ContentMeta(
    Guid Id,
    ContentStatus Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? PublishedAt,
    bool IsPublic,
    bool IsDeleted,
    string RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Chi tiet = meta + du lieu form (TInput). Form admin gui lai dung TInput khi luu.</summary>
public sealed record ContentDetail<TInput>(ContentMeta Meta, TInput Data);

public class ContentListQuery : PageQuery
{
    public ContentStatus? Status { get; set; }

    /// <summary>true: chi xem thung rac (da xoa mem).</summary>
    public bool Trash { get; set; }
}

public sealed record ReorderItem(Guid Id, int SortOrder);

public sealed record ScheduleRequest(DateTimeOffset PublishAt);

public sealed record BulkRequest(string Action, IReadOnlyList<Guid> Ids);

public sealed record BulkFailure(Guid Id, string Message);

public sealed record BulkResult(int Succeeded, IReadOnlyList<BulkFailure> Failed);

public sealed record ContentVersionDto(
    Guid Id,
    int Version,
    bool IsAutosave,
    string? Note,
    Guid? CreatedBy,
    string? CreatedByName,
    DateTimeOffset CreatedAt);

public static class BulkActions
{
    public const string Publish = "publish";
    public const string Unpublish = "unpublish";
    public const string Delete = "delete";
    public const string Restore = "restore";

    public static readonly IReadOnlyList<string> All = [Publish, Unpublish, Delete, Restore];
}

public static class ContentJson
{
    /// <summary>Cung quy uoc voi API (camelCase, enum UPPER_SNAKE) — dung cho snapshot phien ban.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };
}
