using System.Linq.Expressions;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Content.Blog;

public sealed class PostInput
{
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Excerpt { get; set; }
    public string? ContentHtml { get; set; }
    public Guid? CoverMediaId { get; set; }
    public Guid? AuthorId { get; set; }
    public bool IsFeatured { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
    public Guid? PrimaryCategoryId { get; set; }
    public List<Guid> TagIds { get; set; } = [];
    public SeoMeta Seo { get; set; } = new();
}

public sealed record PostListItem(
    Guid Id,
    string Title,
    string Slug,
    string? AuthorName,
    Guid? CoverMediaId,
    int ReadingMinutes,
    bool IsFeatured,
    ContentStatus Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? UpdatedAt);

internal sealed class PostInputValidator : AbstractValidator<PostInput>
{
    public PostInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Vui lòng nhập tiêu đề.").MaximumLength(250);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Excerpt).MaximumLength(500);
        RuleFor(x => x.ContentHtml).MaximumLength(500_000).WithMessage("Bài viết quá dài.");
        RuleFor(x => x.TagIds).Must(t => t.Count <= 20).WithMessage("Tối đa 20 tag.");
        RuleFor(x => x.Seo).SetValidator(new SeoMetaValidator());
    }
}

public sealed partial class PostModule : ContentModule<Post, PostListItem, PostInput>
{
    private const int WordsPerMinute = 220;

    public override string Label => "bài viết";
    public override string DefaultSort => "-publishedAt,-updatedAt";

    public override SortMap<Post> Sorts { get; } = new SortMap<Post>()
        .Add("title", p => p.Title)
        .Add("publishedAt", p => p.PublishedAt)
        .Add("status", p => p.Status)
        .Add("updatedAt", p => p.UpdatedAt);

    public override Expression<Func<Post, PostListItem>> ListProjection => p => new PostListItem(
        p.Id, p.Title, p.Slug, p.Author != null ? p.Author.Name : null, p.CoverMediaId, p.ReadingMinutes,
        p.IsFeatured, p.Status, p.PublishAt, p.PublishedAt, p.UpdatedAt);

    public override IQueryable<Post> ApplySearch(IQueryable<Post> query, string search) =>
        query.Where(p => p.Title.Contains(search) || p.Slug.Contains(search) ||
                         (p.Excerpt != null && p.Excerpt.Contains(search)));

    public override IQueryable<Post> ApplyFilters(IQueryable<Post> query, IReadOnlyDictionary<string, string> f)
    {
        if (f.TryGetValue("categoryId", out var c) && Guid.TryParse(c, out var categoryId))
            query = query.Where(p => p.Categories.Any(x => x.CategoryId == categoryId));
        if (f.TryGetValue("tagId", out var t) && Guid.TryParse(t, out var tagId))
            query = query.Where(p => p.Tags.Any(x => x.TagId == tagId));
        if (f.TryGetValue("authorId", out var a) && Guid.TryParse(a, out var authorId))
            query = query.Where(p => p.AuthorId == authorId);
        return query;
    }

    public override IQueryable<Post> IncludeDetails(IQueryable<Post> query) =>
        query.Include(p => p.Categories).Include(p => p.Tags);

    public override string? SlugSource(Post entity) => entity.Title;

    public override Task<bool> SlugTakenElsewhereAsync(string slug, Guid id, ContentContext ctx, CancellationToken ct) =>
        ctx.Db.Set<PostCategory>().AnyAsync(c => c.Slug == slug, ct);

    public override PostInput ToInput(Post p) => new()
    {
        Title = p.Title, Slug = p.Slug, Excerpt = p.Excerpt, ContentHtml = p.ContentHtml, CoverMediaId = p.CoverMediaId,
        AuthorId = p.AuthorId, IsFeatured = p.IsFeatured, CategoryIds = p.Categories.Select(c => c.CategoryId).ToList(),
        PrimaryCategoryId = p.Categories.FirstOrDefault(c => c.IsPrimary)?.CategoryId,
        TagIds = p.Tags.Select(t => t.TagId).ToList(), Seo = p.Seo,
    };

    public override async Task ApplyAsync(Post p, PostInput i, ContentContext ctx, CancellationToken ct)
    {
        p.Title = i.Title.Trim();
        p.Slug = i.Slug?.Trim() ?? string.Empty;
        p.Excerpt = string.IsNullOrWhiteSpace(i.Excerpt) ? null : i.Excerpt.Trim();
        p.ContentHtml = ctx.Html.Sanitize(i.ContentHtml);
        p.ReadingMinutes = Math.Max(1, (int)Math.Ceiling(CountWords(p.ContentHtml) / (double)WordsPerMinute));
        p.CoverMediaId = await References.MediaAsync(ctx, i.CoverMediaId, "coverMediaId", ct);
        p.AuthorId = await References.ExistsAsync<Author>(ctx, i.AuthorId, "authorId", "Tác giả", ct);
        p.IsFeatured = i.IsFeatured;
        p.Seo = i.Seo;

        var categoryIds = await References.AllExistAsync<PostCategory>(ctx, i.CategoryIds, "categoryIds", "Danh mục", ct);
        if (i.PrimaryCategoryId is { } primary && !categoryIds.Contains(primary))
            throw new BusinessValidationException("primaryCategoryId", "Danh mục chính phải nằm trong các danh mục đã chọn.");
        var primaryId = i.PrimaryCategoryId ?? categoryIds.FirstOrDefault();
        p.Categories.RemoveAll(c => !categoryIds.Contains(c.CategoryId));
        foreach (var id in categoryIds)
        {
            var mapping = p.Categories.FirstOrDefault(c => c.CategoryId == id);
            if (mapping is null) p.Categories.Add(mapping = new PostCategoryMapping { PostId = p.Id, CategoryId = id });
            mapping.IsPrimary = id == primaryId;
        }

        var tagIds = await References.AllExistAsync<Tag>(ctx, i.TagIds, "tagIds", "Tag", ct);
        p.Tags.RemoveAll(t => !tagIds.Contains(t.TagId));
        foreach (var id in tagIds.Where(id => p.Tags.All(t => t.TagId != id)))
            p.Tags.Add(new PostTag { PostId = p.Id, TagId = id });
    }

    public override Task ValidatePublishAsync(Post p, IDictionary<string, string[]> errors, ContentContext ctx,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.Excerpt)) errors["excerpt"] = ["Cần đoạn tóm tắt trước khi xuất bản."];
        if (CountWords(p.ContentHtml) < 150) errors["contentHtml"] = ["Bài viết cần tối thiểu 150 từ trước khi xuất bản."];
        if (p.Categories.Count == 0) errors["categoryIds"] = ["Chọn ít nhất 1 danh mục."];
        return Task.CompletedTask;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Post p) => Refs((p.CoverMediaId, "Cover"));

    public override void PrepareDuplicate(PostInput input)
    {
        input.Title = $"{input.Title} (bản sao)";
        input.Slug = null;
        input.IsFeatured = false;
    }

    internal static int CountWords(string? html) =>
        string.IsNullOrWhiteSpace(html) ? 0 : WordRegex().Matches(TagRegex().Replace(html, " ")).Count;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
