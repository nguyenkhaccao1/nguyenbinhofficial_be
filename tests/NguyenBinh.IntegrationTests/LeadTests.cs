using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace NguyenBinh.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class LeadTests(ApiFactory factory)
{
    private static object Lead(string phone, string? website = null, int elapsed = 8000) => new
    {
        formType = "QUOTE", fullName = "Nguyễn Văn Test", phone, email = "khach@example.com", company = "Công ty <b>ABC</b>",
        need = "Phần mềm POS", message = "Cần báo giá cho 3 chi nhánh", pageUrl = "https://nguyenbinhofficial.com.vn/lien-he",
        website, elapsedMs = elapsed,
    };

    [Fact]
    public async Task Valid_submission_is_stored_and_notification_email_is_sent()
    {
        var admin = await factory.LoginAsync();
        var forms = new { notificationEmails = new[] { "owner@example.com" }, sendAutoReply = true };
        (await admin.PutAsJsonAsync("/api/v1/admin/settings/forms", forms)).EnsureSuccessStatusCode();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/leads", Lead("0971 170 103"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var id = (await response.ReadAsync<JsonObject>()).Data!["id"]!.GetValue<Guid>();

        // Email gui o hang doi nen → cho toi da 10s.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!factory.Emails.Sent.Any(m => m.Subject.Contains("0971 170 103")) && DateTime.UtcNow < deadline) await Task.Delay(100);
        var notice = factory.Emails.Sent.Single(m => m.To.Contains("owner@example.com") && m.Subject.Contains("0971 170 103"));
        notice.Subject.Should().Contain("Yêu cầu báo giá");
        notice.ReplyTo.Should().Be("khach@example.com");
        notice.HtmlBody.Should().Contain("Công ty &lt;b&gt;ABC&lt;/b&gt;", "du lieu khach phai duoc HTML-encode");
        var reply = factory.Emails.Sent.Single(m => m.To.Contains("khach@example.com"));
        reply.HtmlBody.Should().Contain("<br>", "email cam on xuong dong bang <br>").And.NotContain("&#xA;");

        var detail = (await (await admin.GetAsync($"/api/v1/admin/leads/{id}")).ReadAsync<JsonObject>()).Data!;
        detail["status"]!.GetValue<string>().Should().Be("NEW");
        detail["formType"]!.GetValue<string>().Should().Be("QUOTE");

        var update = await admin.PutAsJsonAsync($"/api/v1/admin/leads/{id}", new { status = "CONTACTED", note = "Đã gọi" });
        update.EnsureSuccessStatusCode();
        var list = (await (await admin.GetAsync("/api/v1/admin/leads?status=CONTACTED")).ReadAsync<JsonObject>()).Data!;
        list["items"]!.AsArray().Should().Contain(l => l!["id"]!.GetValue<Guid>() == id);
    }

    [Fact]
    public async Task Bot_submissions_are_accepted_silently_but_not_stored()
    {
        var anonymous = factory.CreateClient();
        foreach (var bot in new[] { Lead("0900000001", website: "http://spam.example"), Lead("0900000002", elapsed: 300) })
        {
            var response = await anonymous.PostAsJsonAsync("/api/v1/leads", bot);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.ReadAsync<JsonObject>()).Data!["id"].Should().BeNull();
        }

        var admin = await factory.LoginAsync();
        var list = (await (await admin.GetAsync("/api/v1/admin/leads?q=090000000")).ReadAsync<JsonObject>()).Data!;
        list["items"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_phone_is_rejected_and_admin_list_requires_login()
    {
        var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/v1/leads", Lead("abc"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await anonymous.GetAsync("/api/v1/admin/leads")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
