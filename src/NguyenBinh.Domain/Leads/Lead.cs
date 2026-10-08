using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Leads;

public enum LeadFormType
{
    Contact,
    Quote,
    Demo,
}

public enum LeadStatus
{
    New,
    Contacted,
    Qualified,
    Won,
    Lost,
    Spam,
}

/// <summary>Yeu cau khach gui tu form tren website (lien he / bao gia / demo).</summary>
public class Lead : AuditableEntity
{
    public LeadFormType FormType { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Company { get; set; }

    /// <summary>Nhu cau/dich vu khach chon (ten hien thi) va slug san pham/dich vu lien quan (neu co).</summary>
    public string? Need { get; set; }
    public string? ProductSlug { get; set; }
    public string? ServiceSlug { get; set; }
    public string? Message { get; set; }

    // Nguon (phan tich marketing).
    public string? PageUrl { get; set; }
    public string? Referrer { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Ghi chu noi bo cua nhan vien sale.</summary>
    public string? Note { get; set; }

    public DateTimeOffset? NotifiedAt { get; set; }
    public string? NotifyError { get; set; }
}
