using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Domain.Leads;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Application.Leads;

public interface ILeadService
{
    Task<LeadSubmitResult> SubmitAsync(SubmitLeadRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<PagedResult<LeadListItem>> ListAsync(LeadListQuery query, CancellationToken ct = default);
    Task<LeadDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<LeadDto> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gui lai email thong bao (vd SMTP loi luc dau).</summary>
    Task ResendNotificationAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Hang doi gui email thong bao lead (xu ly nen, form khong phai cho SMTP).</summary>
public interface ILeadNotificationQueue
{
    void Enqueue(Guid leadId);
}

internal sealed class LeadService(
    IAppDbContext db,
    IValidator<SubmitLeadRequest> submitValidator,
    IValidator<UpdateLeadRequest> updateValidator,
    ILeadNotificationQueue notifications,
    ILogger<LeadService> logger) : ILeadService
{
    private const string ThankYou = "Cảm ơn bạn! Nguyên Bình đã nhận được yêu cầu và sẽ liên hệ lại trong thời gian sớm nhất.";
    private const int MinHumanMs = 2500;

    private DbSet<Lead> Leads => db.Set<Lead>();

    public async Task<LeadSubmitResult> SubmitAsync(SubmitLeadRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        await submitValidator.ValidateAndThrowAsync(request, ct);

        // Bot: tra ve thanh cong gia de bot khong thu cach khac, khong luu, khong gui email.
        if (!string.IsNullOrWhiteSpace(request.Website) || request.ElapsedMs is < MinHumanMs)
        {
            logger.LogInformation("Lead bi chan (honeypot/qua nhanh) tu {Ip}", ipAddress);
            return new LeadSubmitResult(null, ThankYou);
        }

        var lead = new Lead
        {
            FormType = request.FormType,
            FullName = request.FullName.Trim(),
            Phone = request.Phone.Trim(),
            Email = Clean(request.Email),
            Company = Clean(request.Company),
            Need = Clean(request.Need),
            ProductSlug = Clean(request.ProductSlug),
            ServiceSlug = Clean(request.ServiceSlug),
            Message = Clean(request.Message),
            PageUrl = Clean(request.PageUrl),
            Referrer = Clean(request.Referrer),
            UtmSource = Clean(request.UtmSource),
            UtmMedium = Clean(request.UtmMedium),
            UtmCampaign = Clean(request.UtmCampaign),
            IpAddress = ipAddress,
            UserAgent = userAgent is { Length: > 400 } ? userAgent[..400] : userAgent,
        };
        Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        notifications.Enqueue(lead.Id);
        logger.LogInformation("Lead moi {Id} ({FormType})", lead.Id, lead.FormType);
        return new LeadSubmitResult(lead.Id, ThankYou);
    }

    public async Task<PagedResult<LeadListItem>> ListAsync(LeadListQuery query, CancellationToken ct = default)
    {
        var q = Leads.AsNoTracking();
        if (query.Status is { } status) q = q.Where(l => l.Status == status);
        if (query.FormType is { } type) q = q.Where(l => l.FormType == type);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(l => l.FullName.Contains(term) || l.Phone.Contains(term) || (l.Email != null && l.Email.Contains(term))
                             || (l.Company != null && l.Company.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(l => l.CreatedAt)
            .Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize)
            .Select(l => new LeadListItem(l.Id, l.FormType, l.Status, l.FullName, l.Phone, l.Email, l.Company, l.Need, l.CreatedAt,
                l.NotifiedAt, l.NotifyError != null))
            .ToListAsync(ct);
        return PagedResult<LeadListItem>.Create(items, query.SafePage, query.SafePageSize, total);
    }

    public async Task<LeadDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Không tìm thấy yêu cầu."));

    public async Task<LeadDto> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var lead = await Leads.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Không tìm thấy yêu cầu.");
        lead.Status = request.Status;
        lead.Note = Clean(request.Note);
        await db.SaveChangesAsync(ct);
        return ToDto(lead);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var lead = await Leads.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Không tìm thấy yêu cầu.");
        Leads.Remove(lead); // xoa mem (SaveChanges chuyen thanh IsDeleted)
        await db.SaveChangesAsync(ct);
    }

    public async Task ResendNotificationAsync(Guid id, CancellationToken ct = default)
    {
        if (!await Leads.AnyAsync(l => l.Id == id, ct)) throw new NotFoundException("Không tìm thấy yêu cầu.");
        notifications.Enqueue(id);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LeadDto ToDto(Lead l) => new(l.Id, l.FormType, l.Status, l.FullName, l.Phone, l.Email, l.Company, l.Need,
        l.ProductSlug, l.ServiceSlug, l.Message, l.PageUrl, l.Referrer, l.UtmSource, l.UtmMedium, l.UtmCampaign, l.IpAddress,
        l.UserAgent, l.Note, l.CreatedAt, l.UpdatedAt, l.NotifiedAt, l.NotifyError);
}
