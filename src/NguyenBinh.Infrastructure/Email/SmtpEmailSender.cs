using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NguyenBinh.Application.Leads;

namespace NguyenBinh.Infrastructure.Email;

/// <summary>
/// Cau hinh SMTP (deploy/.env): Smtp__Host, Smtp__Port, Smtp__Username, Smtp__Password, Smtp__FromAddress, Smtp__FromName.
/// Gmail: Host=smtp.gmail.com, Port=587, Username=dia chi Gmail, Password=App Password 16 ky tu (bat xac minh 2 buoc truoc).
/// </summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "Website Nguyên Bình";
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _o = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.Host) && !string.IsNullOrWhiteSpace(_o.FromAddress ?? _o.Username);

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("SMTP chưa cấu hình — bỏ qua email '{Subject}'", message.Subject);
            throw new InvalidOperationException("Chưa cấu hình SMTP (Smtp__Host, Smtp__Username, Smtp__Password).");
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_o.FromName, _o.FromAddress ?? _o.Username));
        foreach (var to in message.To) mime.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrWhiteSpace(message.ReplyTo)) mime.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 20_000 };
        // 465 = SSL ngay tu dau; 587/25 = STARTTLS (bat buoc ma hoa khi server ho tro).
        var security = _o.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_o.Host, _o.Port, security, ct);
        if (!string.IsNullOrWhiteSpace(_o.Username)) await client.AuthenticateAsync(_o.Username, _o.Password, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
