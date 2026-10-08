using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Leads;
using NguyenBinh.Application.Settings;
using NguyenBinh.Domain.Leads;
using NguyenBinh.Infrastructure.Persistence;

namespace NguyenBinh.Infrastructure.Email;

/// <summary>Hang doi trong bo nho; lead da luu DB truoc nen restart mat hang doi van gui lai duoc tu Admin.</summary>
internal sealed class LeadNotificationQueue : ILeadNotificationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public void Enqueue(Guid leadId) => _channel.Writer.TryWrite(leadId);
}

/// <summary>
/// Gui email thong bao lead moi toi danh sach trong Cau hinh → Form (mac dinh: email lien he), va email cam on khach (neu bat).
/// Ghi ket qua vao lead (NotifiedAt / NotifyError) de Admin thay va gui lai khi loi.
/// </summary>
internal sealed class LeadNotificationWorker(
    LeadNotificationQueue queue,
    IServiceScopeFactory scopes,
    ILogger<LeadNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await NotifyAsync(id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Khong gui duoc thong bao lead {Id}", id);
            }
        }
    }

    private async Task NotifyAsync(Guid id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var lead = await db.Set<Lead>().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return;

        var forms = await settings.GetAsync<FormSettings>(ct);
        var contact = await settings.GetAsync<ContactSettings>(ct);
        var brand = await settings.GetAsync<BrandSettings>(ct);
        var seo = await settings.GetAsync<SeoSettings>(ct);
        var to = forms.NotificationEmails.Count > 0 ? forms.NotificationEmails
            : contact.Email is { Length: > 0 } fallback ? [fallback] : [];

        string? error = null;
        if (to.Count == 0) error = "Chưa có email nhận thông báo (Cấu hình → Form).";
        else
        {
            error = await TrySendAsync(sender, LeadEmails.Notification(lead, to, seo.SiteUrl), ct);
            if (error is null && forms.SendAutoReply && !string.IsNullOrWhiteSpace(lead.Email))
            {
                var replyError = await TrySendAsync(sender, LeadEmails.AutoReply(lead, brand.SiteName, contact.Hotline ?? contact.Phone), ct);
                if (replyError is not null) logger.LogWarning("Email cam on khach loi (lead {Id}): {Error}", id, replyError);
            }
        }

        lead.NotifiedAt = error is null ? DateTimeOffset.UtcNow : null;
        lead.NotifyError = error is { Length: > 1000 } ? error[..1000] : error;
        await db.SaveChangesAsync(ct);
        if (error is null) logger.LogInformation("Da gui thong bao lead {Id} toi {To}", id, string.Join(", ", to));
    }

    /// <summary>Thu toi da 3 lan (SMTP doi khi tu choi tam thoi).</summary>
    private async Task<string?> TrySendAsync(IEmailSender sender, Application.Leads.EmailMessage message, CancellationToken ct)
    {
        if (!sender.IsConfigured) return "Chưa cấu hình SMTP (Smtp__Host, Smtp__Username, Smtp__Password trong deploy/.env).";
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await sender.SendAsync(message, ct);
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Gui email lan {Attempt} loi", attempt);
                if (attempt >= 3) return ex.Message;
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5), ct);
            }
        }
    }
}
