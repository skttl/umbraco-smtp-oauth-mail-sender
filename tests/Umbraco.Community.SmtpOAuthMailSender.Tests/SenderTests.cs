using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Notifications;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
namespace Umbraco.Community.SmtpOAuthMailSender.Tests;

public class SenderTests
{
    private static EmailMessage Message() => new(null, new[] { "Recipient <to@example.com>" }, null, new[] { "bcc@example.com" }, null, "Subject", "Body", false, null);
    private static MailSenderOptions Options() => new()
    {
        From = "sender@example.com",
        Host = "smtp.office365.com",
        Port = 587,
        Username = "sender@example.com",
        SecureSocketOptions = Umbraco.Cms.Core.Configuration.Models.SecureSocketOptions.StartTls,
        OAuth = new EntraOptions { TenantId = "11111111-1111-1111-1111-111111111111", ClientId = "22222222-2222-2222-2222-222222222222", ClientSecret = "test-secret" }
    };
    private sealed class Credential : TokenCredential
    {
        internal string[]? Scopes;
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken cancellationToken)
        { Scopes = context.Scopes; return new AccessToken("access-token", DateTimeOffset.UtcNow.AddHours(1)); }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken cancellationToken) => new(GetToken(context, cancellationToken));
    }

    [Fact]
    public async Task SmtpUsesXoauth2RequiredTlsAndPreservesMessage()
    {
        var client = new Mock<ISmtpClient>(); var credential = new Credential();
        string? authentication = null; byte[]? bytes = null; string? host = null; int port = 0; SecureSocketOptions security = default;
        client.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, SecureSocketOptions, CancellationToken>((h, p, s, c) => { host = h; port = p; security = s; }).Returns(Task.CompletedTask);
        client.Setup(x => x.AuthenticateAsync(It.IsAny<SaslMechanism>(), It.IsAny<CancellationToken>()))
            .Callback<SaslMechanism, CancellationToken>((s, c) => authentication = Encoding.UTF8.GetString(Convert.FromBase64String(s.Challenge(null, c))))
            .Returns(Task.CompletedTask);
        client.Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
            .Callback<MimeMessage, CancellationToken, ITransferProgress>((m, c, p) => { using var stream = new MemoryStream(); m.WriteTo(stream); bytes = stream.ToArray(); })
            .ReturnsAsync("accepted");
        client.Setup(x => x.DisconnectAsync(true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), credential, () => client.Object);
        Assert.True(sender.CanSendRequiredEmail());
        await sender.SendAsync(Message(), "test");
        Assert.Equal("smtp.office365.com", host); Assert.Equal(587, port); Assert.Equal(SecureSocketOptions.StartTls, security);
        Assert.Equal("user=sender@example.com\u0001auth=Bearer access-token\u0001\u0001", authentication);
        Assert.Equal(new[] { "https://outlook.office365.com/.default" }, credential.Scopes);
        Assert.NotNull(bytes); using var mime = MimeMessage.Load(new MemoryStream(bytes));
        Assert.Equal("Body", mime.TextBody?.TrimEnd('\r', '\n')); Assert.Equal("bcc@example.com", mime.Bcc.Mailboxes.Single().Address);
    }
    [Fact]
    public async Task SmtpAuthenticationFailurePropagates()
    {
        var client = new Mock<ISmtpClient>();
        client.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(x => x.AuthenticateAsync(It.IsAny<SaslMechanism>(), It.IsAny<CancellationToken>())).ThrowsAsync(new AuthenticationException("denied"));
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), () => client.Object);
        await Assert.ThrowsAsync<AuthenticationException>(() => sender.SendAsync(Message(), "test"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandledNotificationSuppressesDelivery(bool handled)
    {
        var events = new Mock<IEventAggregator>(); NotificationEmailModel? notification = null; string? emailType = null;
        events.Setup(x => x.PublishAsync(It.IsAny<SendEmailNotification>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailNotification, CancellationToken>((n, c) => { notification = n.Message; emailType = n.EmailType; if (handled) n.HandleEmail(); })
            .Returns(Task.CompletedTask);
        var client = new Mock<ISmtpClient>();
        client.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(x => x.AuthenticateAsync(It.IsAny<SaslMechanism>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>())).ReturnsAsync("accepted");
        client.Setup(x => x.DisconnectAsync(true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var connections = 0;
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), events.Object, new Credential(), () => { connections++; return client.Object; });
        await sender.SendAsync(Message(), "password-reset", true);
        Assert.NotNull(notification); Assert.Equal("sender@example.com", notification.From!.Address);
        Assert.Equal("password-reset", emailType); Assert.Equal("Body", notification.Body);
        Assert.Equal(handled ? 0 : 1, connections);
    }
#if !UMBRACO_13
    [Fact]
    public async Task ExpiredMessageDoesNotReachTransport()
    {
        var calls = 0;
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), () => { calls++; return Mock.Of<ISmtpClient>(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendAsync(Message(), "test", false, TimeSpan.Zero));
        Assert.Equal(0, calls);
    }
#endif

    [Fact]
    public async Task FailedQuitDoesNotReportAnAcceptedMailAsFailed()
    {
        var client = new Mock<ISmtpClient>();
        client.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(x => x.AuthenticateAsync(It.IsAny<SaslMechanism>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var accepted = false;
        client.Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>())).Callback(() => accepted = true).ReturnsAsync("accepted");
        client.Setup(x => x.DisconnectAsync(true, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("QUIT failed"));
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), () => client.Object);
        await sender.SendAsync(Message(), "test");
        Assert.True(accepted);
    }
    [Fact]
    public async Task SmtpSendFailurePropagatesAndDisposesConnection()
    {
        var client = new Mock<ISmtpClient>();
        client.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(x => x.AuthenticateAsync(It.IsAny<SaslMechanism>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var failure = new IOException("send failed"); var attempts = 0; var disposed = false;
        client.Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>())).Callback(() => attempts++).ThrowsAsync(failure);
        client.Setup(x => x.Dispose()).Callback(() => disposed = true);
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), () => client.Object);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => sender.SendAsync(Message(), "test")));
        Assert.True(disposed); Assert.Equal(1, attempts);
    }
}
