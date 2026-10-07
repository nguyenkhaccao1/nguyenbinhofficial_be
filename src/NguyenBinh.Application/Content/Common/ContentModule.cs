using System.Linq.Expressions;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Identity;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;

namespace NguyenBinh.Application.Content.Common;

/// <summary>Dich vu ma module can khi ap du lieu form vao entity.</summary>
public sealed class ContentContext(
    IAppDbContext db,
    ICurrentUser user,
    IPermissionService permissions,
    IHtmlSanitizerService html,
    TimeProvider clock)
{
    public IAppDbContext Db { get; } = db;
    public ICurrentUser User { get; } = user;
    public IPermissionService Permissions { get; } = permissions;
    public IHtmlSanitizerService Html { get; } = html;
    public TimeProvider Clock { get; } = clock;

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken ct) =>
        (await Permissions.GetPermissionsAsync(User.Roles, ct)).Contains(permission);
}

/// <summary>
/// Dinh nghia 1 module noi dung: truong du lieu, tim kiem, sap xep, rang buoc xuat ban.
/// Moi thao tac chung (CRUD, publish, lich, nhan ban, thung rac, phien ban, autosave...) do
/// <see cref="ContentAdminService{TEntity,TListItem,TInput}"/> thuc hien mot lan cho tat ca module.
/// Module la singleton, khong giu trang thai.
/// </summary>
public abstract class ContentModule<TEntity, TListItem, TInput>
    where TEntity : ContentEntity, new()
    where TInput : class
{
    /// <summary>Ten entity trong ContentVersions / AuditLogs / MediaUsages (vd "Project").</summary>
    public virtual string EntityType => typeof(TEntity).Name;

    /// <summary>Ten tieng Viet dung trong thong bao loi (vd "dự án").</summary>
    public abstract string Label { get; }

    public abstract string DefaultSort { get; }
    public abstract SortMap<TEntity> Sorts { get; }
    public abstract Expression<Func<TEntity, TListItem>> ListProjection { get; }

    public abstract IQueryable<TEntity> ApplySearch(IQueryable<TEntity> query, string search);

    public virtual IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query, IReadOnlyDictionary<string, string> filters) =>
        query;

    /// <summary>Include cac bang con khi doc chi tiet / sua.</summary>
    public virtual IQueryable<TEntity> IncludeDetails(IQueryable<TEntity> query) => query;

    public abstract TInput ToInput(TEntity entity);

    /// <summary>Ap du lieu form vao entity (ke ca bang con). Nem BusinessValidationException khi tham chieu sai.</summary>
    public abstract Task ApplyAsync(TEntity entity, TInput input, ContentContext context, CancellationToken ct);

    /// <summary>Nguon sinh slug tu dong khi de trong (vd Name/Title).</summary>
    public virtual string? SlugSource(TEntity entity) => null;

    /// <summary>Kiem tra slug trung ngoai bang cua chinh no (vd blog: bai viet va danh muc dung chung /blog/{slug}).</summary>
    public virtual Task<bool> SlugTakenElsewhereAsync(string slug, Guid id, ContentContext context, CancellationToken ct) =>
        Task.FromResult(false);

    /// <summary>Dieu kien de duoc xuat ban (muc 73: khong publish noi dung thieu thong tin bat buoc).</summary>
    public virtual Task ValidatePublishAsync(TEntity entity, IDictionary<string, string[]> errors, ContentContext context,
        CancellationToken ct) => Task.CompletedTask;

    public virtual IEnumerable<MediaUsageRef> MediaRefs(TEntity entity) => [];

    /// <summary>Doi ten/slug cho ban sao (Nhan ban).</summary>
    public abstract void PrepareDuplicate(TInput input);

    protected static MediaUsageRef[] Refs(params (Guid? Id, string Field)[] items) =>
        items.Where(i => i.Id is not null).Select(i => new MediaUsageRef(i.Id!.Value, i.Field)).ToArray();
}
