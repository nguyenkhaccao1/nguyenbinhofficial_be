using FluentValidation;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Domain.Leads;

namespace NguyenBinh.Application.Leads;

/// <summary>Du lieu form khach gui tu website.</summary>
public sealed class SubmitLeadRequest
{
    public LeadFormType FormType { get; set; } = LeadFormType.Contact;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Company { get; set; }
    public string? Need { get; set; }
    public string? ProductSlug { get; set; }
    public string? ServiceSlug { get; set; }
    public string? Message { get; set; }
    public string? PageUrl { get; set; }
    public string? Referrer { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }

    /// <summary>Bay bot: o an, nguoi that de trong. Bot dien → coi la spam (tra ve thanh cong gia).</summary>
    public string? Website { get; set; }

    /// <summary>So mili-giay tu luc hien form den luc gui; bot gui ngay lap tuc (&lt; 2.5s) → coi la spam.</summary>
    public int? ElapsedMs { get; set; }
}

public sealed record LeadSubmitResult(Guid? Id, string Message);

public sealed record LeadListItem(Guid Id, LeadFormType FormType, LeadStatus Status, string FullName, string Phone, string? Email,
    string? Company, string? Need, DateTimeOffset CreatedAt, DateTimeOffset? NotifiedAt, bool NotifyFailed);

public sealed record LeadDto(Guid Id, LeadFormType FormType, LeadStatus Status, string FullName, string Phone, string? Email,
    string? Company, string? Need, string? ProductSlug, string? ServiceSlug, string? Message, string? PageUrl, string? Referrer,
    string? UtmSource, string? UtmMedium, string? UtmCampaign, string? IpAddress, string? UserAgent, string? Note,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, DateTimeOffset? NotifiedAt, string? NotifyError);

public sealed class LeadListQuery : PageQuery
{
    public LeadStatus? Status { get; set; }
    public LeadFormType? FormType { get; set; }
}

public sealed class UpdateLeadRequest
{
    public LeadStatus Status { get; set; }
    public string? Note { get; set; }
}

internal sealed class SubmitLeadRequestValidator : AbstractValidator<SubmitLeadRequest>
{
    public SubmitLeadRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.").MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Vui lòng nhập số điện thoại.")
            .Matches(@"^\+?[0-9 .\-()]{8,20}$").WithMessage("Số điện thoại không hợp lệ.")
            .Must(p => p.Count(char.IsDigit) is >= 9 and <= 15).WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.Email).EmailAddress().WithMessage("Email không hợp lệ.").MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Company).MaximumLength(200);
        RuleFor(x => x.Need).MaximumLength(200);
        RuleFor(x => x.ProductSlug).MaximumLength(200);
        RuleFor(x => x.ServiceSlug).MaximumLength(200);
        RuleFor(x => x.Message).MaximumLength(4000).WithMessage("Nội dung tối đa 4000 ký tự.");
        RuleFor(x => x.PageUrl).MaximumLength(500);
        RuleFor(x => x.Referrer).MaximumLength(500);
        RuleFor(x => x.UtmSource).MaximumLength(100);
        RuleFor(x => x.UtmMedium).MaximumLength(100);
        RuleFor(x => x.UtmCampaign).MaximumLength(100);
        RuleFor(x => x.FormType).IsInEnum();
    }
}

internal sealed class UpdateLeadRequestValidator : AbstractValidator<UpdateLeadRequest>
{
    public UpdateLeadRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(4000);
    }
}
