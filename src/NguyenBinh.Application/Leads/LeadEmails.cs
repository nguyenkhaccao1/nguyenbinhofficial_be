using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using NguyenBinh.Domain.Leads;

namespace NguyenBinh.Application.Leads;

public sealed record EmailMessage(IReadOnlyList<string> To, string Subject, string HtmlBody, string TextBody, string? ReplyTo = null);

/// <summary>Gui email (SMTP o Infrastructure). Chua cau hinh SMTP → chi ghi log.</summary>
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Noi dung email thong bao lead moi va email cam on gui khach. Moi gia tri tu khach deu duoc HTML-encode.</summary>
public static class LeadEmails
{
    public static string FormTypeLabel(LeadFormType type) => type switch
    {
        LeadFormType.Quote => "Yêu cầu báo giá",
        LeadFormType.Demo => "Yêu cầu demo",
        _ => "Liên hệ",
    };

    public static EmailMessage Notification(Lead lead, IReadOnlyList<string> to, string siteUrl)
    {
        var rows = new (string Label, string? Value)[]
        {
            ("Loại", FormTypeLabel(lead.FormType)),
            ("Họ tên", lead.FullName),
            ("Điện thoại", lead.Phone),
            ("Email", lead.Email),
            ("Công ty", lead.Company),
            ("Nhu cầu", lead.Need),
            ("Sản phẩm", lead.ProductSlug),
            ("Dịch vụ", lead.ServiceSlug),
            ("Nội dung", lead.Message),
            ("Trang gửi", lead.PageUrl),
            ("Nguồn", string.Join(" / ", new[] { lead.UtmSource, lead.UtmMedium, lead.UtmCampaign }.Where(v => v is not null)) is { Length: > 0 } utm ? utm : lead.Referrer),
            ("Thời gian", lead.CreatedAt.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy")),
        };

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,sans-serif;font-size:14px;color:#0b110d\">");
        html.Append($"<h2 style=\"margin:0 0 12px;color:#1e5a36\">{Enc(FormTypeLabel(lead.FormType))} mới từ website</h2>");
        html.Append("<table cellpadding=\"8\" style=\"border-collapse:collapse;width:100%;max-width:640px\">");
        foreach (var (label, value) in rows.Where(r => !string.IsNullOrWhiteSpace(r.Value)))
        {
            html.Append("<tr><td style=\"border:1px solid #e4e7ec;background:#f6f7f9;width:130px;vertical-align:top\"><b>")
                .Append(Enc(label)).Append("</b></td><td style=\"border:1px solid #e4e7ec;white-space:pre-line\">")
                .Append(Enc(value!)).Append("</td></tr>");
        }
        html.Append("</table>");
        html.Append($"<p style=\"margin-top:16px\"><a href=\"tel:{Enc(lead.Phone)}\">Gọi {Enc(lead.Phone)}</a> · ")
            .Append($"<a href=\"{Enc(siteUrl)}/admin/leads/{lead.Id}\">Mở trong trang quản trị</a></p></div>");

        var text = string.Join('\n', rows.Where(r => !string.IsNullOrWhiteSpace(r.Value)).Select(r => $"{r.Label}: {r.Value}"))
                   + $"\n\nQuản trị: {siteUrl}/admin/leads/{lead.Id}";

        return new EmailMessage(to, $"[Website] {FormTypeLabel(lead.FormType)} — {lead.FullName} — {lead.Phone}", html.ToString(), text,
            ReplyTo: lead.Email);
    }

    public static EmailMessage AutoReply(Lead lead, string siteName, string? hotline)
    {
        var call = string.IsNullOrWhiteSpace(hotline) ? "" : $" Nếu cần gấp, vui lòng gọi {hotline}.";
        var text = $"Xin chào {lead.FullName},\n\n{siteName} đã nhận được {FormTypeLabel(lead.FormType).ToLowerInvariant()} của bạn "
                   + $"và sẽ liên hệ lại trong thời gian sớm nhất.{call}\n\nTrân trọng,\n{siteName}";
        var html = $"<div style=\"font-family:Arial,sans-serif;font-size:14px;line-height:1.6\">{string.Join("<br>", text.Split('\n').Select(Enc))}</div>";
        return new EmailMessage([lead.Email!], $"{siteName} đã nhận được yêu cầu của bạn", html, text);
    }

    // Giu nguyen chu tieng Viet (khong doi thanh &#...;), chi ma hoa ky tu HTML nguy hiem.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private static string Enc(string value) => Html.Encode(value);
}
