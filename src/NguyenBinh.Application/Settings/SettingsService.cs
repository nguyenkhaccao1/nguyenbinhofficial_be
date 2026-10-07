using System.Reflection;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Settings;

namespace NguyenBinh.Application.Settings;

public sealed record SettingGroupDefinition(string Key, Type Type, bool IsPublic);

public static class SettingKeys
{
    public const string Brand = "brand";
    public const string Theme = "theme";
    public const string Contact = "contact";
    public const string Social = "social";
    public const string Tracking = "tracking";
    public const string Seo = "seo";
    public const string Forms = "forms";

    public static readonly IReadOnlyList<SettingGroupDefinition> All =
    [
        new(Brand, typeof(BrandSettings), true),
        new(Theme, typeof(ThemeSettings), true),
        new(Contact, typeof(ContactSettings), true),
        new(Social, typeof(SocialSettings), true),
        new(Tracking, typeof(TrackingSettings), true),
        new(Seo, typeof(SeoSettings), true),
        new(Forms, typeof(FormSettings), false),
    ];

    public static SettingGroupDefinition? Find(string key) =>
        All.FirstOrDefault(g => string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string KeyOf<T>() => All.First(g => g.Type == typeof(T)).Key;
}

public interface ISettingsService
{
    /// <summary>Tat ca nhom (admin).</summary>
    Task<IReadOnlyDictionary<string, object>> GetAllAsync(CancellationToken ct = default);

    Task<object> GetGroupAsync(string key, CancellationToken ct = default);

    Task<T> GetAsync<T>(CancellationToken ct = default) where T : class, new();

    Task<object> UpdateGroupAsync(string key, JsonElement value, CancellationToken ct = default);

    /// <summary>Cac nhom public (website), co cache.</summary>
    Task<IReadOnlyDictionary<string, object>> GetPublicAsync(CancellationToken ct = default);
}

internal sealed class SettingsService(
    IAppDbContext db,
    ICacheService cache,
    IFileStorage storage,
    IMediaUsageTracker usageTracker,
    IServiceProvider services) : ISettingsService
{
    internal const string PublicCacheKey = "settings:public";
    private const string UsageEntityType = "SETTINGS";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyDictionary<string, object>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await db.SiteSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.ValueJson, ct);
        var result = new Dictionary<string, object>();
        foreach (var group in SettingKeys.All)
            result[group.Key] = Deserialize(group, rows.GetValueOrDefault(group.Key));
        await ResolveMediaAsync(result.Values, ct);
        return result;
    }

    public async Task<object> GetGroupAsync(string key, CancellationToken ct = default)
    {
        var group = SettingKeys.Find(key) ?? throw new NotFoundException($"Không có nhóm cấu hình '{key}'.");
        var json = await db.SiteSettings.AsNoTracking().Where(s => s.Key == group.Key).Select(s => s.ValueJson)
            .FirstOrDefaultAsync(ct);
        var value = Deserialize(group, json);
        await ResolveMediaAsync([value], ct);
        return value;
    }

    public async Task<T> GetAsync<T>(CancellationToken ct = default) where T : class, new() =>
        (T)await GetGroupAsync(SettingKeys.KeyOf<T>(), ct);

    public async Task<object> UpdateGroupAsync(string key, JsonElement value, CancellationToken ct = default)
    {
        var group = SettingKeys.Find(key) ?? throw new NotFoundException($"Không có nhóm cấu hình '{key}'.");

        object typed;
        try
        {
            typed = value.Deserialize(group.Type, Json) ?? Activator.CreateInstance(group.Type)!;
        }
        catch (JsonException)
        {
            throw new BusinessValidationException("value", "Dữ liệu cấu hình không đúng định dạng.");
        }

        var validator = (IValidator?)services.GetService(typeof(IValidator<>).MakeGenericType(group.Type));
        if (validator is not null)
        {
            var result = await validator.ValidateAsync(new ValidationContext<object>(typed), ct);
            if (!result.IsValid) throw new ValidationException(result.Errors);
        }

        var mediaRefs = MediaProperties(group.Type)
            .Select(p => (Field: p.Name, Ref: (MediaRef?)p.GetValue(typed)))
            .Where(x => x.Ref is not null)
            .ToList();
        var mediaIds = mediaRefs.Select(x => x.Ref!.Id).Distinct().ToList();
        var found = await db.MediaFiles.Where(m => mediaIds.Contains(m.Id) && !m.IsPrivate).Select(m => m.Id)
            .ToListAsync(ct);
        var missing = mediaRefs.Where(x => !found.Contains(x.Ref!.Id)).ToList();
        if (missing.Count > 0)
            throw new BusinessValidationException(missing.ToDictionary(
                x => char.ToLowerInvariant(x.Field[0]) + x.Field[1..], _ => new[] { "Media không tồn tại." }));

        // Chi luu Id cua media; Url/Alt duoc dien lai luc doc.
        foreach (var (_, mediaRef) in mediaRefs)
        {
            mediaRef!.Url = null;
            mediaRef.Alt = null;
            mediaRef.Width = null;
            mediaRef.Height = null;
        }

        var row = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == group.Key, ct);
        if (row is null)
        {
            row = new SiteSetting { Key = group.Key, IsPublic = group.IsPublic };
            db.SiteSettings.Add(row);
        }

        row.ValueJson = JsonSerializer.Serialize(typed, group.Type, Json);
        row.IsPublic = group.IsPublic;

        await usageTracker.SetUsagesAsync(UsageEntityType, group.Key,
            mediaRefs.Select(x => new MediaUsageRef(x.Ref!.Id, x.Field)), ct);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(PublicCacheKey, ct);

        await ResolveMediaAsync([typed], ct);
        return typed;
    }

    public Task<IReadOnlyDictionary<string, object>> GetPublicAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync<IReadOnlyDictionary<string, object>>(PublicCacheKey, async token =>
        {
            var all = await GetAllAsync(token);
            return SettingKeys.All.Where(g => g.IsPublic).ToDictionary(g => g.Key, g => all[g.Key]);
        }, TimeSpan.FromMinutes(10), ct);

    private static object Deserialize(SettingGroupDefinition group, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Activator.CreateInstance(group.Type)!;
        try
        {
            return JsonSerializer.Deserialize(json, group.Type, Json) ?? Activator.CreateInstance(group.Type)!;
        }
        catch (JsonException)
        {
            return Activator.CreateInstance(group.Type)!;
        }
    }

    private async Task ResolveMediaAsync(IEnumerable<object> groups, CancellationToken ct)
    {
        var refs = groups
            .SelectMany(g => MediaProperties(g.GetType()).Select(p => (Owner: g, Prop: p, Ref: (MediaRef?)p.GetValue(g))))
            .Where(x => x.Ref is not null)
            .ToList();
        if (refs.Count == 0) return;

        var ids = refs.Select(x => x.Ref!.Id).Distinct().ToList();
        var media = await db.MediaFiles.AsNoTracking().Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        foreach (var (owner, prop, mediaRef) in refs)
        {
            if (!media.TryGetValue(mediaRef!.Id, out var file))
            {
                prop.SetValue(owner, null); // media da bi xoa
                continue;
            }

            mediaRef.Url = storage.GetPublicUrl(file.StorageKey);
            mediaRef.Alt = file.Alt;
            mediaRef.Width = file.Width;
            mediaRef.Height = file.Height;
        }
    }

    private static IEnumerable<PropertyInfo> MediaProperties(Type type) =>
        type.GetProperties().Where(p => p.PropertyType == typeof(MediaRef) && p.CanWrite);
}
