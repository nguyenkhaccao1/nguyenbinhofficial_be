using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Media;
using NguyenBinh.Application.Platform;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Domain.Platform;
using NguyenBinh.Shared.Results;
using NguyenBinh.Shared.Text;

namespace NguyenBinh.Application.Content.Common;

/// <summary>Moi thao tac quan tri noi dung (muc 21) cho mot loai noi dung.</summary>
public interface IContentAdminService<TEntity, TListItem, TInput>
    where TEntity : ContentEntity, new()
    where TInput : class
{
    Task<PagedResult<TListItem>> ListAsync(ContentListQuery query, IReadOnlyDictionary<string, string> filters,
        CancellationToken ct = default);
    Task<ContentDetail<TInput>> GetAsync(Guid id, CancellationToken ct = default);
    Task<ContentDetail<TInput>> CreateAsync(TInput input, CancellationToken ct = default);
    Task<ContentDetail<TInput>> UpdateAsync(Guid id, TInput input, string? rowVersion, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ContentDetail<TInput>> RestoreAsync(Guid id, CancellationToken ct = default);
    Task<ContentDetail<TInput>> PublishAsync(Guid id, CancellationToken ct = default);
    Task<ContentDetail<TInput>> UnpublishAsync(Guid id, CancellationToken ct = default);
    Task<ContentDetail<TInput>> ScheduleAsync(Guid id, DateTimeOffset publishAt, CancellationToken ct = default);
    Task<ContentDetail<TInput>> DuplicateAsync(Guid id, CancellationToken ct = default);
    Task ReorderAsync(IReadOnlyList<ReorderItem> items, CancellationToken ct = default);
    Task<BulkResult> BulkAsync(BulkRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ContentVersionDto>> VersionsAsync(Guid id, CancellationToken ct = default);
    Task<TInput> GetVersionAsync(Guid id, Guid versionId, CancellationToken ct = default);
    Task<ContentDetail<TInput>> RestoreVersionAsync(Guid id, Guid versionId, CancellationToken ct = default);
    Task<ContentVersionDto> AutosaveAsync(Guid id, TInput input, CancellationToken ct = default);
}

internal sealed class ContentAdminService<TEntity, TListItem, TInput>(
    ContentModule<TEntity, TListItem, TInput> module,
    IAppDbContext db,
    ContentContext context,
    IMediaUsageTracker mediaUsage,
    IAuditLogger audit,
    TimeProvider clock) : IContentAdminService<TEntity, TListItem, TInput>
    where TEntity : ContentEntity, new()
    where TInput : class
{
    private const int MaxAutosaves = 20;

    private DbSet<TEntity> Set => db.Set<TEntity>();

    public async Task<PagedResult<TListItem>> ListAsync(ContentListQuery query, IReadOnlyDictionary<string, string> filters,
        CancellationToken ct = default)
    {
        var items = query.Trash
            ? Set.IgnoreQueryFilters().Where(e => e.IsDeleted)
            : Set.AsQueryable();
        items = items.AsNoTracking();

        if (query.Status is { } status) items = items.Where(e => e.Status == status);
        if (query.Search is { } q) items = module.ApplySearch(items, q);
        items = module.ApplyFilters(items, filters);
        items = module.Sorts.Apply(items, query.Sort, module.DefaultSort);

        return await items.Select(module.ListProjection).ToPagedAsync(query, ct);
    }

    public async Task<ContentDetail<TInput>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await module.IncludeDetails(Set.IgnoreQueryFilters()).AsNoTracking()
                         .FirstOrDefaultAsync(e => e.Id == id, ct)
                     ?? throw NotFoundException.For(module.Label, id);
        return ToDetail(entity);
    }

    public async Task<ContentDetail<TInput>> CreateAsync(TInput input, CancellationToken ct = default)
    {
        var entity = new TEntity();
        await module.ApplyAsync(entity, input, context, ct);
        await EnsureSlugAsync(entity, ct);
        Set.Add(entity);
        await SaveWithSideEffectsAsync(entity, "Tạo mới", ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<ContentDetail<TInput>> UpdateAsync(Guid id, TInput input, string? rowVersion,
        CancellationToken ct = default)
    {
        var entity = await LoadForUpdateAsync(id, ct);
        EnsureRowVersion(entity, rowVersion);

        await module.ApplyAsync(entity, input, context, ct);
        await EnsureSlugAsync(entity, ct);

        // Noi dung dang cong khai khong duoc luu thanh trang thai thieu thong tin bat buoc.
        if (entity.Status is ContentStatus.Published or ContentStatus.Scheduled)
            await ThrowIfNotPublishableAsync(entity, ct);

        await SaveWithSideEffectsAsync(entity, null, ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Set.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw NotFoundException.For(module.Label, id);
        Set.Remove(entity); // soft delete (AppDbContext)
        await db.SaveChangesAsync(ct);
    }

    public async Task<ContentDetail<TInput>> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Set.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, ct)
                     ?? throw NotFoundException.For(module.Label, id);
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.DeletedBy = null;

        // Slug co the da bi noi dung khac dung trong luc nam trong thung rac → tu doi ten.
        if (entity is IHasSlug slugged && await SlugExistsAsync(slugged.Slug, entity.Id, ct))
            slugged.Slug = await UniqueSlugAsync(slugged.Slug, entity.Id, ct);

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContentDetail<TInput>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await LoadForUpdateAsync(id, ct);
        await ThrowIfNotPublishableAsync(entity, ct);

        entity.Status = ContentStatus.Published;
        entity.PublishAt = null;
        entity.PublishedAt ??= clock.GetUtcNow();
        audit.Add(AuditActions.Publish, module.EntityType, id.ToString());
        await SaveWithSideEffectsAsync(entity, "Xuất bản", ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContentDetail<TInput>> UnpublishAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await LoadForUpdateAsync(id, ct);
        entity.Status = ContentStatus.Unpublished;
        entity.PublishAt = null;
        audit.Add(AuditActions.Unpublish, module.EntityType, id.ToString());
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContentDetail<TInput>> ScheduleAsync(Guid id, DateTimeOffset publishAt, CancellationToken ct = default)
    {
        if (publishAt <= clock.GetUtcNow())
            throw new BusinessValidationException("publishAt", "Thời điểm xuất bản phải ở tương lai.");

        var entity = await LoadForUpdateAsync(id, ct);
        await ThrowIfNotPublishableAsync(entity, ct);
        entity.Status = ContentStatus.Scheduled;
        entity.PublishAt = publishAt;
        audit.Add(AuditActions.Publish, module.EntityType, id.ToString(), new { scheduledAt = publishAt });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContentDetail<TInput>> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var source = await GetAsync(id, ct);
        var sourceSlug = (await Set.AsNoTracking().FirstAsync(e => e.Id == id, ct) as IHasSlug)?.Slug;
        var copy = Clone(source.Data);
        module.PrepareDuplicate(copy);

        var entity = new TEntity();
        await module.ApplyAsync(entity, copy, context, ct);
        if (entity is IHasSlug slugged)
            slugged.Slug = await UniqueSlugAsync(
                Slug.From($"{sourceSlug ?? module.SlugSource(entity) ?? "noi-dung"}-ban-sao"), entity.Id, ct);
        entity.Status = ContentStatus.Draft;
        Set.Add(entity);
        await SaveWithSideEffectsAsync(entity, $"Nhân bản từ {id}", ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task ReorderAsync(IReadOnlyList<ReorderItem> items, CancellationToken ct = default)
    {
        if (!typeof(ISortable).IsAssignableFrom(typeof(TEntity)))
            throw new BusinessValidationException("items", $"Không thể sắp xếp {module.Label}.");

        var ids = items.Select(i => i.Id).ToList();
        var entities = await Set.Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        foreach (var entity in entities)
            ((ISortable)entity).SortOrder = items.First(i => i.Id == entity.Id).SortOrder;
        await db.SaveChangesAsync(ct);
    }

    public async Task<BulkResult> BulkAsync(BulkRequest request, CancellationToken ct = default)
    {
        var failed = new List<BulkFailure>();
        var succeeded = 0;
        foreach (var id in request.Ids.Distinct())
        {
            try
            {
                switch (request.Action)
                {
                    case BulkActions.Publish: await PublishAsync(id, ct); break;
                    case BulkActions.Unpublish: await UnpublishAsync(id, ct); break;
                    case BulkActions.Delete: await DeleteAsync(id, ct); break;
                    case BulkActions.Restore: await RestoreAsync(id, ct); break;
                    default: throw new BusinessValidationException("action", "Thao tác không hợp lệ.");
                }

                succeeded++;
            }
            catch (AppException ex)
            {
                // Bo cac thay doi dang do cua ban ghi loi de khong lan sang ban ghi ke tiep.
                DetachChanges();
                var detail = ex is BusinessValidationException v
                    ? $"{ex.Message} {string.Join(" ", v.Errors.SelectMany(e => e.Value))}"
                    : ex.Message;
                failed.Add(new BulkFailure(id, detail.Trim()));
            }
        }

        return new BulkResult(succeeded, failed);
    }

    public async Task<IReadOnlyList<ContentVersionDto>> VersionsAsync(Guid id, CancellationToken ct = default)
    {
        var versions = await db.ContentVersions.AsNoTracking()
            .Where(v => v.EntityType == module.EntityType && v.EntityId == id)
            .OrderByDescending(v => v.Version).ThenByDescending(v => v.CreatedAt)
            .Take(100)
            .Select(v => new { v.Id, v.Version, v.IsAutosave, v.Note, v.CreatedBy, v.CreatedAt })
            .ToListAsync(ct);

        var userIds = versions.Where(v => v.CreatedBy != null).Select(v => v.CreatedBy!.Value).Distinct().ToList();
        var names = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return versions.Select(v => new ContentVersionDto(v.Id, v.Version, v.IsAutosave, v.Note, v.CreatedBy,
            v.CreatedBy is { } uid ? names.GetValueOrDefault(uid) : null, v.CreatedAt)).ToList();
    }

    public async Task<TInput> GetVersionAsync(Guid id, Guid versionId, CancellationToken ct = default)
    {
        var version = await db.ContentVersions.AsNoTracking()
                          .FirstOrDefaultAsync(v => v.Id == versionId && v.EntityId == id && v.EntityType == module.EntityType, ct)
                      ?? throw NotFoundException.For("phiên bản", versionId);
        return JsonSerializer.Deserialize<TInput>(version.SnapshotJson, ContentJson.Options)
               ?? throw new ConflictException("Không đọc được nội dung phiên bản.");
    }

    public async Task<ContentDetail<TInput>> RestoreVersionAsync(Guid id, Guid versionId, CancellationToken ct = default)
    {
        var input = await GetVersionAsync(id, versionId, ct);
        var entity = await LoadForUpdateAsync(id, ct);
        await module.ApplyAsync(entity, input, context, ct);
        await EnsureSlugAsync(entity, ct);
        if (entity.Status is ContentStatus.Published or ContentStatus.Scheduled)
            await ThrowIfNotPublishableAsync(entity, ct);
        await SaveWithSideEffectsAsync(entity, $"Khôi phục phiên bản {versionId}", ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContentVersionDto> AutosaveAsync(Guid id, TInput input, CancellationToken ct = default)
    {
        if (!await Set.AnyAsync(e => e.Id == id, ct)) throw NotFoundException.For(module.Label, id);

        var version = await AddVersionAsync(id, input, isAutosave: true, "Tự động lưu", ct);

        // Giu toi da N ban autosave gan nhat moi noi dung.
        var stale = await db.ContentVersions
            .Where(v => v.EntityType == module.EntityType && v.EntityId == id && v.IsAutosave)
            .OrderByDescending(v => v.CreatedAt).Skip(MaxAutosaves).ToListAsync(ct);
        db.ContentVersions.RemoveRange(stale);
        await db.SaveChangesAsync(ct);

        return new ContentVersionDto(version.Id, version.Version, true, version.Note, version.CreatedBy,
            context.User.UserName, version.CreatedAt);
    }

    // ---------------------------------------------------------------------------------------

    private async Task<TEntity> LoadForUpdateAsync(Guid id, CancellationToken ct) =>
        await module.IncludeDetails(Set).FirstOrDefaultAsync(e => e.Id == id, ct)
        ?? throw NotFoundException.For(module.Label, id);

    private void EnsureRowVersion(TEntity entity, string? rowVersion)
    {
        if (string.IsNullOrEmpty(rowVersion)) return;
        if (Convert.ToBase64String(entity.RowVersion) != rowVersion)
            throw new ConflictException(
                $"{Capitalize(module.Label)} đã được người khác cập nhật. Tải lại để xem thay đổi mới nhất trước khi lưu.");
    }

    private async Task ThrowIfNotPublishableAsync(TEntity entity, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        await module.ValidatePublishAsync(entity, errors, context, ct);
        if (errors.Count > 0)
            throw new BusinessValidationException(errors, $"Chưa thể xuất bản {module.Label}: còn thiếu thông tin bắt buộc.");
    }

    private async Task SaveWithSideEffectsAsync(TEntity entity, string? note, CancellationToken ct)
    {
        await mediaUsage.SetUsagesAsync(module.EntityType, entity.Id.ToString(), module.MediaRefs(entity), ct);
        await db.SaveChangesAsync(ct);
        await AddVersionAsync(entity.Id, module.ToInput(entity), isAutosave: false, note, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ContentVersion> AddVersionAsync(Guid id, TInput input, bool isAutosave, string? note,
        CancellationToken ct)
    {
        var last = await db.ContentVersions
            .Where(v => v.EntityType == module.EntityType && v.EntityId == id)
            .MaxAsync(v => (int?)v.Version, ct) ?? 0;

        var version = new ContentVersion
        {
            EntityType = module.EntityType,
            EntityId = id,
            Version = last + 1,
            SnapshotJson = JsonSerializer.Serialize(input, ContentJson.Options),
            IsAutosave = isAutosave,
            Note = note,
            CreatedBy = context.User.UserId,
            CreatedAt = clock.GetUtcNow(),
        };
        db.ContentVersions.Add(version);
        return version;
    }

    private async Task EnsureSlugAsync(TEntity entity, CancellationToken ct)
    {
        if (entity is not IHasSlug slugged) return;

        var provided = !string.IsNullOrWhiteSpace(slugged.Slug);
        var slug = Slug.From(provided ? slugged.Slug : module.SlugSource(entity));
        if (string.IsNullOrEmpty(slug))
            throw new BusinessValidationException("slug", "Không tạo được slug — hãy nhập tên hoặc slug.");

        if (await SlugExistsAsync(slug, entity.Id, ct))
        {
            if (provided) throw new BusinessValidationException("slug", $"Slug \"{slug}\" đã được sử dụng.");
            slug = await UniqueSlugAsync(slug, entity.Id, ct);
        }

        slugged.Slug = slug;
    }

    private async Task<bool> SlugExistsAsync(string slug, Guid id, CancellationToken ct) =>
        await Set.AnyAsync(e => ((IHasSlug)e).Slug == slug && e.Id != id, ct)
        || await module.SlugTakenElsewhereAsync(slug, id, context, ct);

    private async Task<string> UniqueSlugAsync(string baseSlug, Guid id, CancellationToken ct)
    {
        if (!await SlugExistsAsync(baseSlug, id, ct)) return baseSlug;
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Slug.From($"{baseSlug}-{i}");
            if (!await SlugExistsAsync(candidate, id, ct)) return candidate;
        }

        return Slug.From($"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(Slug.MaxLength, baseSlug.Length + 33)]);
    }

    private ContentDetail<TInput> ToDetail(TEntity e) => new(
        new ContentMeta(e.Id, e.Status, e.PublishAt, e.PublishedAt, e.IsPublicAt(clock.GetUtcNow()), e.IsDeleted,
            Convert.ToBase64String(e.RowVersion), e.CreatedAt, e.UpdatedAt),
        module.ToInput(e));

    private static TInput Clone(TInput input) =>
        JsonSerializer.Deserialize<TInput>(JsonSerializer.Serialize(input, ContentJson.Options), ContentJson.Options)!;

    private void DetachChanges()
    {
        if (db is DbContext context)
            foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
                entry.State = entry.State == EntityState.Added ? EntityState.Detached : EntityState.Unchanged;
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
