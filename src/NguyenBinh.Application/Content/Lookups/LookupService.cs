using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Content.Lookups;

public sealed record LookupItem(Guid Id, string Name, string? Extra = null);

public sealed record LookupsDto(
    IReadOnlyList<LookupItem> Industries,
    IReadOnlyList<LookupItem> Technologies,
    IReadOnlyList<LookupItem> Clients,
    IReadOnlyList<LookupItem> ProjectCategories,
    IReadOnlyList<LookupItem> ProductCategories,
    IReadOnlyList<LookupItem> ServiceCategories,
    IReadOnlyList<LookupItem> PostCategories,
    IReadOnlyList<LookupItem> Tags,
    IReadOnlyList<LookupItem> Authors,
    IReadOnlyList<LookupItem> Products,
    IReadOnlyList<LookupItem> Projects);

public interface ILookupService
{
    Task<LookupsDto> GetAsync(CancellationToken ct = default);
}

internal sealed class LookupService(IAppDbContext db) : ILookupService
{
    public async Task<LookupsDto> GetAsync(CancellationToken ct = default) => new(
        await Taxonomy<Industry>(ct),
        await db.Set<Technology>().AsNoTracking().OrderBy(t => t.Group).ThenBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new LookupItem(t.Id, t.Name, t.Group.ToString())).ToListAsync(ct),
        await Taxonomy<Client>(ct),
        await Taxonomy<ProjectCategory>(ct),
        await Taxonomy<ProductCategory>(ct),
        await Taxonomy<ServiceCategory>(ct),
        await Taxonomy<PostCategory>(ct),
        await Taxonomy<Tag>(ct),
        await Taxonomy<Author>(ct),
        await db.Set<Product>().AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new LookupItem(p.Id, p.Name, p.Slug)).ToListAsync(ct),
        await db.Set<Project>().AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new LookupItem(p.Id, p.Name, p.Slug)).ToListAsync(ct));

    private async Task<IReadOnlyList<LookupItem>> Taxonomy<T>(CancellationToken ct) where T : TaxonomyEntity =>
        await db.Set<T>().AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new LookupItem(t.Id, t.Name, t.Slug)).ToListAsync(ct);
}
