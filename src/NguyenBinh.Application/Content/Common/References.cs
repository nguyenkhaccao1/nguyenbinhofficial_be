using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.Application.Content.Common;

/// <summary>Kiem tra tham chieu (media, danh muc...) ton tai truoc khi luu — tra 422 theo field thay vi loi FK.</summary>
public static class References
{
    public static async Task<Guid?> MediaAsync(ContentContext ctx, Guid? id, string field, CancellationToken ct)
    {
        if (id is null) return null;
        if (!await ctx.Db.MediaFiles.AnyAsync(m => m.Id == id && !m.IsPrivate, ct))
            throw new BusinessValidationException(field, "Media không tồn tại hoặc đã bị xoá.");
        return id;
    }

    /// <summary>Kiem tra nhieu media mot lan; tra ve tap id hop le.</summary>
    public static async Task EnsureMediaAsync(ContentContext ctx, IEnumerable<Guid> ids, string field, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return;
        var found = await ctx.Db.MediaFiles.Where(m => list.Contains(m.Id) && !m.IsPrivate).Select(m => m.Id).ToListAsync(ct);
        if (found.Count != list.Count)
            throw new BusinessValidationException(field, "Có media không tồn tại hoặc đã bị xoá.");
    }

    public static async Task<Guid?> ExistsAsync<T>(ContentContext ctx, Guid? id, string field, string label,
        CancellationToken ct) where T : Entity
    {
        if (id is null) return null;
        if (!await ctx.Db.Set<T>().AnyAsync(e => e.Id == id, ct))
            throw new BusinessValidationException(field, $"{label} không tồn tại hoặc đã bị xoá.");
        return id;
    }

    public static async Task<List<Guid>> AllExistAsync<T>(ContentContext ctx, IEnumerable<Guid> ids, string field,
        string label, CancellationToken ct) where T : Entity
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return list;
        var found = await ctx.Db.Set<T>().Where(e => list.Contains(e.Id)).Select(e => e.Id).ToListAsync(ct);
        var missing = list.Except(found).ToList();
        if (missing.Count > 0)
            throw new BusinessValidationException(field, $"{label} không tồn tại hoặc đã bị xoá ({missing.Count}).");
        return list;
    }
}

public static class EnumParser
{
    /// <summary>Doc enum tu query string theo ca "CUSTOM_PROJECT" (API) lan "CustomProject".</summary>
    public static bool TryParse<T>(string? value, [NotNullWhen(true)] out T result) where T : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Enum.TryParse(value.Replace("_", string.Empty), ignoreCase: true, out result) && Enum.IsDefined(result);
    }
}

public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string?> HttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(1000)
            .Must(v => string.IsNullOrEmpty(v) ||
                       (Uri.TryCreate(v, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")))
            .WithMessage("URL không hợp lệ (cần bắt đầu bằng http:// hoặc https://).");

    public static bool IsJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed class SeoMetaValidator : AbstractValidator<SeoMeta>
{
    private static readonly string[] RobotsValues = ["index,follow", "noindex,follow", "index,nofollow", "noindex,nofollow"];

    public SeoMetaValidator()
    {
        RuleFor(x => x.Title).MaximumLength(70).WithMessage("Tiêu đề SEO tối đa 70 ký tự.");
        RuleFor(x => x.Description).MaximumLength(300).WithMessage("Mô tả SEO tối đa 300 ký tự.");
        RuleFor(x => x.OgTitle).MaximumLength(100);
        RuleFor(x => x.OgDescription).MaximumLength(300);
        RuleFor(x => x.CanonicalUrl).HttpUrl();
        RuleFor(x => x.Robots).Must(r => r is null || RobotsValues.Contains(r.Replace(" ", "")))
            .WithMessage("Robots chỉ nhận index/noindex, follow/nofollow.");
        RuleFor(x => x.SchemaJson).Must(ValidationRules.IsJsonObject).WithMessage("Schema phải là JSON hợp lệ.")
            .MaximumLength(20000);
    }
}

public static class MediaKindCheck
{
    public static async Task EnsureImagesAsync(ContentContext ctx, IEnumerable<Guid> ids, string field, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return;
        var notImages = await ctx.Db.MediaFiles.Where(m => list.Contains(m.Id) && m.Kind != MediaKind.Image).AnyAsync(ct);
        if (notImages) throw new BusinessValidationException(field, "Chỉ chọn được file hình ảnh.");
    }
}
