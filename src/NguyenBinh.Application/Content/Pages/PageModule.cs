using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Shared.Authorization;
using NguyenBinh.Shared.Text;

namespace NguyenBinh.Application.Content.Pages;

public sealed class PageInput
{
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public PageType PageType { get; set; } = PageType.Standard;
    public Guid? IndustryId { get; set; }
    public List<PageSectionInput> Sections { get; set; } = [];
    public SeoMeta Seo { get; set; } = new();
}

public sealed class PageSectionInput
{
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public JsonElement? Settings { get; set; }
    public List<PageBlockInput> Blocks { get; set; } = [];
}

public sealed class PageBlockInput
{
    public string Type { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public JsonElement? Data { get; set; }
    public JsonElement? Settings { get; set; }
}

public sealed record PageListItem(
    Guid Id,
    string Title,
    string Path,
    PageType PageType,
    string? IndustryName,
    int SectionCount,
    ContentStatus Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? UpdatedAt);

internal sealed class PageInputValidator : AbstractValidator<PageInput>
{
    private const int MaxJsonLength = 200_000;

    public PageInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Vui lòng nhập tiêu đề trang.").MaximumLength(200);
        RuleFor(x => x.Path).NotEmpty().WithMessage("Vui lòng nhập đường dẫn.").MaximumLength(300);
        RuleFor(x => x.Sections).Must(s => s.Count <= 50).WithMessage("Tối đa 50 section.");
        RuleForEach(x => x.Sections).ChildRules(section =>
        {
            section.RuleFor(s => s.Name).MaximumLength(150);
            section.RuleFor(s => s.Settings).Must(BeObject).WithMessage("Cấu hình section phải là object JSON.");
            section.RuleFor(s => s.Blocks).Must(b => b.Count <= 30).WithMessage("Tối đa 30 block mỗi section.");
            section.RuleForEach(s => s.Blocks).ChildRules(block =>
            {
                block.RuleFor(b => b.Type).Must(BlockTypes.IsKnown).WithMessage("Loại block '{PropertyValue}' không hợp lệ.");
                block.RuleFor(b => b.Data).Must(BeObject).WithMessage("Dữ liệu block phải là object JSON.")
                    .Must(d => d is null || d.Value.GetRawText().Length <= MaxJsonLength).WithMessage("Dữ liệu block quá lớn.");
                block.RuleFor(b => b.Settings).Must(BeObject).WithMessage("Cấu hình block phải là object JSON.");
            });
        });
        RuleFor(x => x.Seo).SetValidator(new SeoMetaValidator());
    }

    private static bool BeObject(JsonElement? e) =>
        e is null || e.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined;
}

public sealed class PageModule : ContentModule<Page, PageListItem, PageInput>
{
    /// <summary>Tien to do route co dinh cua website quan ly — trang page builder khong duoc chiem.</summary>
    private static readonly string[] ReservedSegments =
        ["admin", "api", "media", "san-pham", "du-an", "dich-vu", "blog", "search", "cong-nghe", "sitemap.xml", "robots.txt",
         "yeu-cau-bao-gia", "yeu-cau-demo", "health", "swagger"];

    public override string Label => "trang";
    public override string DefaultSort => "path";

    public override SortMap<Page> Sorts { get; } = new SortMap<Page>()
        .Add("title", p => p.Title)
        .Add("path", p => p.Path)
        .Add("pageType", p => p.PageType)
        .Add("status", p => p.Status)
        .Add("updatedAt", p => p.UpdatedAt);

    public override Expression<Func<Page, PageListItem>> ListProjection => p => new PageListItem(
        p.Id, p.Title, p.Path, p.PageType, p.Industry != null ? p.Industry.Name : null, p.Sections.Count, p.Status,
        p.PublishAt, p.UpdatedAt);

    public override IQueryable<Page> ApplySearch(IQueryable<Page> query, string search) =>
        query.Where(p => p.Title.Contains(search) || p.Path.Contains(search));

    public override IQueryable<Page> ApplyFilters(IQueryable<Page> query, IReadOnlyDictionary<string, string> f)
    {
        if (f.TryGetValue("pageType", out var t))
        {
            var types = t.Split(',').Select(x => EnumParser.TryParse<PageType>(x, out var v) ? v : (PageType?)null)
                .Where(v => v != null).Select(v => v!.Value).ToList();
            if (types.Count > 0) query = query.Where(p => types.Contains(p.PageType));
        }

        return query;
    }

    public override IQueryable<Page> IncludeDetails(IQueryable<Page> query) =>
        query.Include(p => p.Sections).ThenInclude(s => s.Blocks);

    public override PageInput ToInput(Page p) => new()
    {
        Title = p.Title, Path = p.Path, PageType = p.PageType, IndustryId = p.IndustryId, Seo = p.Seo,
        Sections = p.Sections.OrderBy(s => s.SortOrder).Select(s => new PageSectionInput
        {
            Name = s.Name, IsEnabled = s.IsEnabled, Settings = Parse(s.SettingsJson),
            Blocks = s.Blocks.OrderBy(b => b.SortOrder).Select(b => new PageBlockInput
            {
                Type = b.Type, IsEnabled = b.IsEnabled, Data = Parse(b.DataJson), Settings = Parse(b.SettingsJson),
            }).ToList(),
        }).ToList(),
    };

    public override async Task ApplyAsync(Page p, PageInput i, ContentContext ctx, CancellationToken ct)
    {
        var path = NormalizePath(i.Path, i.PageType);
        if (await ctx.Db.Set<Page>().AnyAsync(x => x.Path == path && x.Id != p.Id, ct))
            throw new BusinessValidationException("path", $"Đường dẫn \"{path}\" đã có trang khác sử dụng.");

        await EnsureCustomHtmlAllowedAsync(p, i, ctx, ct);

        p.Title = i.Title.Trim();
        p.Path = path;
        p.PageType = i.PageType;
        p.IndustryId = await References.ExistsAsync<Industry>(ctx, i.IndustryId, "industryId", "Ngành", ct);
        p.Seo = i.Seo;

        // Cay section/block duoc thay toan bo theo thu tu tren canvas (1 transaction).
        p.Sections.Clear();
        for (var s = 0; s < i.Sections.Count; s++)
        {
            var sectionInput = i.Sections[s];
            var section = new PageSection
            {
                PageId = p.Id, Name = string.IsNullOrWhiteSpace(sectionInput.Name) ? null : sectionInput.Name.Trim(),
                IsEnabled = sectionInput.IsEnabled, SortOrder = s, SettingsJson = Serialize(sectionInput.Settings),
            };
            for (var b = 0; b < sectionInput.Blocks.Count; b++)
            {
                var blockInput = sectionInput.Blocks[b];
                section.Blocks.Add(new PageBlock
                {
                    SectionId = section.Id, Type = blockInput.Type, IsEnabled = blockInput.IsEnabled, SortOrder = b,
                    DataJson = SanitizeData(blockInput.Type, blockInput.Data, ctx),
                    SettingsJson = Serialize(blockInput.Settings),
                });
            }

            p.Sections.Add(section);
        }

        await References.EnsureMediaAsync(ctx, MediaIdsIn(p), "sections", ct);
    }

    public override Task ValidatePublishAsync(Page p, IDictionary<string, string[]> errors, ContentContext ctx,
        CancellationToken ct)
    {
        if (!p.Sections.Any(s => s.IsEnabled && s.Blocks.Any(b => b.IsEnabled)))
            errors["sections"] = ["Trang cần ít nhất 1 section có nội dung đang bật."];
        if (p.PageType is PageType.Landing or PageType.Solution && string.IsNullOrWhiteSpace(p.Seo.Description))
            errors["seo.description"] = ["Trang landing/giải pháp cần mô tả SEO riêng (tránh trang trùng lặp nội dung)."];
        return Task.CompletedTask;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Page p) =>
        MediaIdsIn(p).Select(id => new MediaUsageRef(id, "Block")).Concat(Refs((p.Seo.OgImageId, "OgImage"))).Distinct();

    public override void PrepareDuplicate(PageInput input)
    {
        input.Title = $"{input.Title} (bản sao)";
        input.Path = input.Path == "/" ? "/trang-chu-ban-sao" : $"{input.Path.TrimEnd('/')}-ban-sao";
        if (input.PageType == PageType.Home) input.PageType = PageType.Standard;
    }

    /// <summary>
    /// Chuan hoa duong dan: chu thuong, khong dau "/" cuoi, moi doan la slug hop le.
    /// HOME luon la "/", SOLUTION nam duoi /giai-phap/.
    /// </summary>
    internal static string NormalizePath(string raw, PageType type)
    {
        if (type == PageType.Home) return "/";

        var segments = raw.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Slug.From(s)).Where(s => s.Length > 0).ToList();
        if (segments.Count == 0)
            throw new BusinessValidationException("path", "Chỉ trang chủ mới dùng đường dẫn \"/\".");
        if (segments.Count > 3)
            throw new BusinessValidationException("path", "Đường dẫn tối đa 3 cấp.");

        if (type == PageType.Solution)
        {
            if (segments[0] != "giai-phap") segments.Insert(0, "giai-phap");
            if (segments.Count < 2)
                throw new BusinessValidationException("path", "Trang giải pháp cần dạng /giai-phap/{ten-nganh}.");
        }
        else if (segments[0] == "giai-phap")
        {
            throw new BusinessValidationException("path", "/giai-phap/ chỉ dành cho trang loại Giải pháp.");
        }

        if (ReservedSegments.Contains(segments[0]))
            throw new BusinessValidationException("path", $"/{segments[0]} là đường dẫn hệ thống, hãy chọn đường dẫn khác.");

        return "/" + string.Join('/', segments);
    }

    /// <summary>Block HTML tuy chinh chi nguoi co quyen page.custom_html moi them/sua duoc.</summary>
    private static async Task EnsureCustomHtmlAllowedAsync(Page p, PageInput i, ContentContext ctx, CancellationToken ct)
    {
        var incoming = i.Sections.SelectMany(s => s.Blocks).Where(b => b.Type == BlockTypes.CustomHtml)
            .Select(b => Serialize(b.Data)).Order().ToList();
        if (incoming.Count == 0) return;

        var existing = p.Sections.SelectMany(s => s.Blocks).Where(b => b.Type == BlockTypes.CustomHtml)
            .Select(b => Serialize(Parse(b.DataJson))).Order().ToList();
        if (incoming.SequenceEqual(existing)) return;

        if (!await ctx.HasPermissionAsync(Permissions.Pages.CustomHtml, ct))
            throw new ForbiddenException("Bạn không có quyền thêm hoặc sửa block HTML tuỳ chỉnh.");
    }

    /// <summary>Lam sach cac truong HTML trong block (rich text). Van ban thuong duoc web escape khi render.</summary>
    private static string SanitizeData(string type, JsonElement? data, ContentContext ctx)
    {
        if (type != BlockTypes.RichText || data is null || data.Value.ValueKind != JsonValueKind.Object)
            return Serialize(data);

        var node = JsonNode.Parse(data.Value.GetRawText())!.AsObject();
        if (node["html"] is JsonValue html && html.TryGetValue<string>(out var value))
            node["html"] = ctx.Html.Sanitize(value);
        return node.ToJsonString();
    }

    /// <summary>Thu thap moi gia tri "mediaId"/"mediaIds" trong du lieu block de theo doi su dung media.</summary>
    private static IEnumerable<Guid> MediaIdsIn(Page p)
    {
        var ids = new HashSet<Guid>();
        foreach (var block in p.Sections.SelectMany(s => s.Blocks))
            Collect(JsonNode.Parse(block.DataJson), ids);
        return ids;

        static void Collect(JsonNode? node, HashSet<Guid> ids)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (key, value) in obj)
                    {
                        if (key.EndsWith("MediaId", StringComparison.OrdinalIgnoreCase) || key == "mediaId")
                        {
                            if (value is JsonValue v && v.TryGetValue<string>(out var s) && Guid.TryParse(s, out var g)) ids.Add(g);
                        }
                        else if (key.EndsWith("MediaIds", StringComparison.OrdinalIgnoreCase) && value is JsonArray arr)
                        {
                            foreach (var item in arr)
                                if (item is JsonValue iv && iv.TryGetValue<string>(out var s) && Guid.TryParse(s, out var g)) ids.Add(g);
                        }
                        else
                        {
                            Collect(value, ids);
                        }
                    }

                    break;
                case JsonArray array:
                    foreach (var item in array) Collect(item, ids);
                    break;
            }
        }
    }

    private static JsonElement? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string Serialize(JsonElement? element) =>
        element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? "{}"
            : element.Value.GetRawText();
}
