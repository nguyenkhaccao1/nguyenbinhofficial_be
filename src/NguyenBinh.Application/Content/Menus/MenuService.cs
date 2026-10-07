using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Content.Menus;

public sealed class MenuItemInput
{
    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Description { get; set; }
    public bool OpenInNewTab { get; set; }
    public string? DynamicSource { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<MenuItemInput> Children { get; set; } = [];
}

public sealed class MenuInput
{
    public string Name { get; set; } = string.Empty;
    public List<MenuItemInput> Items { get; set; } = [];
}

public sealed record MenuDto(Guid Id, string Code, string Name, List<MenuItemInput> Items, DateTimeOffset? UpdatedAt);

public static class MenuSources
{
    public static readonly IReadOnlyList<string> All = ["PRODUCTS", "SERVICES", "SOLUTIONS", "PROJECTS"];
}

internal sealed class MenuInputValidator : AbstractValidator<MenuInput>
{
    public MenuInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên menu.").MaximumLength(100);
        RuleFor(x => x.Items).Must(i => i.Count <= 30).WithMessage("Tối đa 30 mục cấp 1.");
        RuleForEach(x => x.Items).SetValidator(new MenuItemInputValidator());
    }
}

/// <summary>Muc cap 1 (duoc co muc con).</summary>
internal sealed class MenuItemInputValidator : MenuItemRules
{
    public MenuItemInputValidator()
    {
        RuleFor(x => x.Children).Must(c => c.Count <= 30).WithMessage("Tối đa 30 mục con.");
        RuleForEach(x => x.Children).SetValidator(new MenuChildItemValidator());
    }
}

/// <summary>Muc cap 2 (khong duoc co muc con — menu toi da 2 cap).</summary>
internal sealed class MenuChildItemValidator : MenuItemRules
{
    public MenuChildItemValidator()
    {
        RuleFor(x => x.Children).Empty().WithMessage("Menu tối đa 2 cấp.");
    }
}

internal abstract class MenuItemRules : AbstractValidator<MenuItemInput>
{
    protected MenuItemRules()
    {
        RuleFor(x => x.Label).NotEmpty().WithMessage("Nhập nhãn.").MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(200);
        RuleFor(x => x.Url).Must(BeValidUrl).WithMessage("URL phải bắt đầu bằng / hoặc http(s)://");
        RuleFor(x => x.Url).NotEmpty().When(x => x.DynamicSource is null && x.Children.Count == 0)
            .WithMessage("Nhập URL hoặc chọn nguồn megamenu.");
        RuleFor(x => x.DynamicSource).Must(s => s is null || MenuSources.All.Contains(s))
            .WithMessage("Nguồn megamenu không hợp lệ.");
    }

    private static bool BeValidUrl(string? url) =>
        string.IsNullOrEmpty(url) || url.StartsWith('/') || url.StartsWith('#') ||
        url.StartsWith("mailto:") || url.StartsWith("tel:") ||
        (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"));
}

public interface IMenuService
{
    Task<IReadOnlyList<MenuDto>> ListAsync(CancellationToken ct = default);
    Task<MenuDto> GetAsync(string code, CancellationToken ct = default);
    Task<MenuDto> SaveAsync(string code, MenuInput input, CancellationToken ct = default);
}

internal sealed class MenuService(IAppDbContext db) : IMenuService
{
    public async Task<IReadOnlyList<MenuDto>> ListAsync(CancellationToken ct = default)
    {
        var menus = await db.Set<Menu>().AsNoTracking().Include(m => m.Items).OrderBy(m => m.Code).ToListAsync(ct);
        return menus.Select(ToDto).ToList();
    }

    public async Task<MenuDto> GetAsync(string code, CancellationToken ct = default)
    {
        var menu = await db.Set<Menu>().AsNoTracking().Include(m => m.Items).FirstOrDefaultAsync(m => m.Code == code, ct)
                   ?? throw new NotFoundException($"Không có menu '{code}'.");
        return ToDto(menu);
    }

    public async Task<MenuDto> SaveAsync(string code, MenuInput input, CancellationToken ct = default)
    {
        var menu = await db.Set<Menu>().Include(m => m.Items).FirstOrDefaultAsync(m => m.Code == code, ct)
                   ?? throw new NotFoundException($"Không có menu '{code}'.");

        menu.Name = input.Name.Trim();
        menu.Items.Clear();
        for (var i = 0; i < input.Items.Count; i++)
        {
            var parent = Create(menu.Id, null, input.Items[i], i);
            menu.Items.Add(parent);
            for (var c = 0; c < input.Items[i].Children.Count; c++)
                menu.Items.Add(Create(menu.Id, parent.Id, input.Items[i].Children[c], c));
        }

        menu.UpdatedAt = DateTimeOffset.UtcNow; // de audit ghi nhan thay doi menu
        await db.SaveChangesAsync(ct);
        return await GetAsync(code, ct);
    }

    private static MenuItem Create(Guid menuId, Guid? parentId, MenuItemInput i, int order) => new()
    {
        MenuId = menuId, ParentId = parentId, Label = i.Label.Trim(),
        Url = string.IsNullOrWhiteSpace(i.Url) ? null : i.Url.Trim(),
        Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(),
        OpenInNewTab = i.OpenInNewTab, DynamicSource = i.DynamicSource, IsEnabled = i.IsEnabled, SortOrder = order,
    };

    private static MenuDto ToDto(Menu menu)
    {
        List<MenuItemInput> Build(Guid? parentId) => menu.Items.Where(i => i.ParentId == parentId).OrderBy(i => i.SortOrder)
            .Select(i => new MenuItemInput
            {
                Label = i.Label, Url = i.Url, Description = i.Description, OpenInNewTab = i.OpenInNewTab,
                DynamicSource = i.DynamicSource, IsEnabled = i.IsEnabled, Children = Build(i.Id),
            }).ToList();

        return new MenuDto(menu.Id, menu.Code, menu.Name, Build(null), menu.UpdatedAt);
    }
}
