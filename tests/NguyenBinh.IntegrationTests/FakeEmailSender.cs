using System.Collections.Concurrent;
using NguyenBinh.Application.Leads;

namespace NguyenBinh.IntegrationTests;

public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public bool IsConfigured => true;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
