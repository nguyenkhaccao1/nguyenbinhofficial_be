using System.Linq.Expressions;
using FluentValidation;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;

namespace NguyenBinh.Application.Content.Library;

/// <summary>Thu vien noi dung tai su dung trong Page builder: testimonial, doi tac, doi ngu, FAQ.</summary>
public sealed record LibraryListItem(
    Guid Id,
    string Title,
    string? Subtitle,
    Guid? MediaId,
    int SortOrder,
    ContentStatus Status,
    DateTimeOffset? UpdatedAt);

public sealed class TestimonialInput
{
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorTitle { get; set; }
    public string? Company { get; set; }
    public Guid? AvatarMediaId { get; set; }
    public string Quote { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProductId { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class TestimonialInputValidator : AbstractValidator<TestimonialInput>
{
    public TestimonialInputValidator()
    {
        RuleFor(x => x.AuthorName).NotEmpty().WithMessage("Nhập tên người đánh giá.").MaximumLength(150);
        RuleFor(x => x.AuthorTitle).MaximumLength(150);
        RuleFor(x => x.Company).MaximumLength(200);
        RuleFor(x => x.Quote).NotEmpty().WithMessage("Nhập nội dung đánh giá.").MaximumLength(2000);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).When(x => x.Rating is not null);
    }
}

public sealed class TestimonialModule : ContentModule<Testimonial, LibraryListItem, TestimonialInput>
{
    public override string Label => "đánh giá";
    public override string DefaultSort => "sortOrder";

    public override SortMap<Testimonial> Sorts { get; } = new SortMap<Testimonial>()
        .Add("sortOrder", t => t.SortOrder).Add("title", t => t.AuthorName).Add("updatedAt", t => t.UpdatedAt);

    public override Expression<Func<Testimonial, LibraryListItem>> ListProjection => t =>
        new LibraryListItem(t.Id, t.AuthorName, t.Company, t.AvatarMediaId, t.SortOrder, t.Status, t.UpdatedAt);

    public override IQueryable<Testimonial> ApplySearch(IQueryable<Testimonial> q, string s) =>
        q.Where(t => t.AuthorName.Contains(s) || (t.Company != null && t.Company.Contains(s)) || t.Quote.Contains(s));

    public override TestimonialInput ToInput(Testimonial t) => new()
    {
        AuthorName = t.AuthorName, AuthorTitle = t.AuthorTitle, Company = t.Company, AvatarMediaId = t.AvatarMediaId,
        Quote = t.Quote, Rating = t.Rating, ProjectId = t.ProjectId, ProductId = t.ProductId, SortOrder = t.SortOrder,
    };

    public override async Task ApplyAsync(Testimonial t, TestimonialInput i, ContentContext ctx, CancellationToken ct)
    {
        t.AuthorName = i.AuthorName.Trim();
        t.AuthorTitle = Clean(i.AuthorTitle);
        t.Company = Clean(i.Company);
        t.AvatarMediaId = await References.MediaAsync(ctx, i.AvatarMediaId, "avatarMediaId", ct);
        t.Quote = i.Quote.Trim();
        t.Rating = i.Rating;
        t.ProjectId = await References.ExistsAsync<Project>(ctx, i.ProjectId, "projectId", "Dự án", ct);
        t.ProductId = await References.ExistsAsync<Product>(ctx, i.ProductId, "productId", "Sản phẩm", ct);
        t.SortOrder = i.SortOrder;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Testimonial t) => Refs((t.AvatarMediaId, "Avatar"));
    public override void PrepareDuplicate(TestimonialInput input) => input.AuthorName += " (bản sao)";
    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class PartnerInput
{
    public string Name { get; set; } = string.Empty;
    public Guid? LogoMediaId { get; set; }
    public string? Url { get; set; }
    public string? Kind { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class PartnerInputValidator : AbstractValidator<PartnerInput>
{
    public PartnerInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên đối tác.").MaximumLength(150);
        RuleFor(x => x.Url).HttpUrl();
        RuleFor(x => x.Kind).MaximumLength(50);
    }
}

public sealed class PartnerModule : ContentModule<Partner, LibraryListItem, PartnerInput>
{
    public override string Label => "đối tác";
    public override string DefaultSort => "sortOrder";

    public override SortMap<Partner> Sorts { get; } = new SortMap<Partner>()
        .Add("sortOrder", p => p.SortOrder).Add("title", p => p.Name).Add("updatedAt", p => p.UpdatedAt);

    public override Expression<Func<Partner, LibraryListItem>> ListProjection => p =>
        new LibraryListItem(p.Id, p.Name, p.Kind, p.LogoMediaId, p.SortOrder, p.Status, p.UpdatedAt);

    public override IQueryable<Partner> ApplySearch(IQueryable<Partner> q, string s) => q.Where(p => p.Name.Contains(s));

    public override PartnerInput ToInput(Partner p) => new()
        { Name = p.Name, LogoMediaId = p.LogoMediaId, Url = p.Url, Kind = p.Kind, SortOrder = p.SortOrder };

    public override async Task ApplyAsync(Partner p, PartnerInput i, ContentContext ctx, CancellationToken ct)
    {
        p.Name = i.Name.Trim();
        p.LogoMediaId = await References.MediaAsync(ctx, i.LogoMediaId, "logoMediaId", ct);
        p.Url = string.IsNullOrWhiteSpace(i.Url) ? null : i.Url.Trim();
        p.Kind = string.IsNullOrWhiteSpace(i.Kind) ? null : i.Kind.Trim();
        p.SortOrder = i.SortOrder;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(Partner p) => Refs((p.LogoMediaId, "Logo"));
    public override void PrepareDuplicate(PartnerInput input) => input.Name += " (bản sao)";
}

public sealed class TeamMemberInput
{
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Guid? PhotoMediaId { get; set; }
    public string? Bio { get; set; }
    public List<string> Links { get; set; } = [];
    public int SortOrder { get; set; }
}

internal sealed class TeamMemberInputValidator : AbstractValidator<TeamMemberInput>
{
    public TeamMemberInputValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Nhập họ tên.").MaximumLength(150);
        RuleFor(x => x.Title).MaximumLength(150);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleForEach(x => x.Links).HttpUrl();
    }
}

public sealed class TeamMemberModule : ContentModule<TeamMember, LibraryListItem, TeamMemberInput>
{
    public override string Label => "thành viên";
    public override string DefaultSort => "sortOrder";

    public override SortMap<TeamMember> Sorts { get; } = new SortMap<TeamMember>()
        .Add("sortOrder", t => t.SortOrder).Add("title", t => t.FullName).Add("updatedAt", t => t.UpdatedAt);

    public override Expression<Func<TeamMember, LibraryListItem>> ListProjection => t =>
        new LibraryListItem(t.Id, t.FullName, t.Title, t.PhotoMediaId, t.SortOrder, t.Status, t.UpdatedAt);

    public override IQueryable<TeamMember> ApplySearch(IQueryable<TeamMember> q, string s) =>
        q.Where(t => t.FullName.Contains(s) || (t.Title != null && t.Title.Contains(s)));

    public override TeamMemberInput ToInput(TeamMember t) => new()
        { FullName = t.FullName, Title = t.Title, PhotoMediaId = t.PhotoMediaId, Bio = t.Bio, Links = [.. t.Links], SortOrder = t.SortOrder };

    public override async Task ApplyAsync(TeamMember t, TeamMemberInput i, ContentContext ctx, CancellationToken ct)
    {
        t.FullName = i.FullName.Trim();
        t.Title = string.IsNullOrWhiteSpace(i.Title) ? null : i.Title.Trim();
        t.PhotoMediaId = await References.MediaAsync(ctx, i.PhotoMediaId, "photoMediaId", ct);
        t.Bio = string.IsNullOrWhiteSpace(i.Bio) ? null : i.Bio.Trim();
        t.Links = i.Links.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();
        t.SortOrder = i.SortOrder;
    }

    public override IEnumerable<MediaUsageRef> MediaRefs(TeamMember t) => Refs((t.PhotoMediaId, "Photo"));
    public override void PrepareDuplicate(TeamMemberInput input) => input.FullName += " (bản sao)";
}

public sealed class FaqInput
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public FaqScope Scope { get; set; } = FaqScope.Global;
    public Guid? ScopeId { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class FaqInputValidator : AbstractValidator<FaqInput>
{
    public FaqInputValidator()
    {
        RuleFor(x => x.Question).NotEmpty().WithMessage("Nhập câu hỏi.").MaximumLength(300);
        RuleFor(x => x.Answer).NotEmpty().WithMessage("Nhập câu trả lời.").MaximumLength(5000);
        RuleFor(x => x.ScopeId).NotNull().When(x => x.Scope != FaqScope.Global)
            .WithMessage("Chọn đối tượng áp dụng.");
    }
}

public sealed class FaqModule : ContentModule<Faq, LibraryListItem, FaqInput>
{
    public override string Label => "câu hỏi";
    public override string DefaultSort => "sortOrder";

    public override SortMap<Faq> Sorts { get; } = new SortMap<Faq>()
        .Add("sortOrder", f => f.SortOrder).Add("title", f => f.Question).Add("updatedAt", f => f.UpdatedAt);

    public override Expression<Func<Faq, LibraryListItem>> ListProjection => f =>
        new LibraryListItem(f.Id, f.Question, f.Scope.ToString(), null, f.SortOrder, f.Status, f.UpdatedAt);

    public override IQueryable<Faq> ApplySearch(IQueryable<Faq> q, string s) =>
        q.Where(f => f.Question.Contains(s) || f.Answer.Contains(s));

    public override IQueryable<Faq> ApplyFilters(IQueryable<Faq> q, IReadOnlyDictionary<string, string> f) =>
        f.TryGetValue("scope", out var s) && EnumParser.TryParse<FaqScope>(s, out var scope) ? q.Where(x => x.Scope == scope) : q;

    public override FaqInput ToInput(Faq f) => new()
        { Question = f.Question, Answer = f.Answer, Scope = f.Scope, ScopeId = f.ScopeId, SortOrder = f.SortOrder };

    public override Task ApplyAsync(Faq f, FaqInput i, ContentContext ctx, CancellationToken ct)
    {
        f.Question = i.Question.Trim();
        f.Answer = i.Answer.Trim();
        f.Scope = i.Scope;
        f.ScopeId = i.Scope == FaqScope.Global ? null : i.ScopeId;
        f.SortOrder = i.SortOrder;
        return Task.CompletedTask;
    }

    public override void PrepareDuplicate(FaqInput input) => input.Question += " (bản sao)";
}
