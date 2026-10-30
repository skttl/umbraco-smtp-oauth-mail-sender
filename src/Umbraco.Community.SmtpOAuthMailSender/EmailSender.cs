using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
namespace Umbraco.Community.SmtpOAuthMailSender;

public sealed class EmailSender : IEmailSender
{
    private readonly MailSenderOptions _options;
    private readonly IEventAggregator _events;
    private readonly TokenCredential _credential;
    private readonly Func<ISmtpClient> _clients;


    public EmailSender(IOptions<MailSenderOptions> options, IEventAggregator events,
        [FromKeyedServices(MailSenderRegistration.ServiceKey)] TokenCredential credential, [FromKeyedServices(MailSenderRegistration.ServiceKey)] Func<ISmtpClient> clients)
    {
        _options = options.Value;
        _events = events;
        _credential = credential;
        _clients = clients;
    }

    public bool CanSendRequiredEmail() => true;

#if !UMBRACO_18
    public Task SendAsync(EmailMessage message, string emailType) => SendInternalAsync(message, emailType, false, null);
    public Task SendAsync(EmailMessage message, string emailType, bool enableNotification) => SendInternalAsync(message, emailType, enableNotification, null);
#endif
#if !UMBRACO_13
    public Task SendAsync(EmailMessage message, string emailType, bool enableNotification = false, TimeSpan? expires = null)
        => SendInternalAsync(message, emailType, enableNotification, expires);
#endif

    private async Task SendInternalAsync(EmailMessage message, string emailType, bool enableNotification, TimeSpan? expires)
    {
#if UMBRACO_18
        expires ??= _options.EmailExpiration;
#endif
        if (expires <= TimeSpan.Zero) throw new OperationCanceledException("The email has expired.");
        using var timeout = new CancellationTokenSource();
        if (expires is not null) timeout.CancelAfter(expires.Value);
        var cancellationToken = timeout.Token;
        if (enableNotification)
        {
            var notification = new SendEmailNotification(MessageConversion.ToNotification(message, _options.From), emailType);
            await _events.PublishAsync(notification, cancellationToken);
            if (notification.IsHandled) return;
        }
        cancellationToken.ThrowIfCancellationRequested();

        using var mime = await MessageConversion.ToMimeAsync(message, _options.From, cancellationToken);
        var token = await _credential.GetTokenAsync(new TokenRequestContext(["https://outlook.office365.com/.default"]), cancellationToken);
        using var client = _clients();
        client.Timeout = 100000;
        await client.ConnectAsync(_options.Host!, _options.Port, (SecureSocketOptions)(int)_options.SecureSocketOptions, cancellationToken);
        await client.AuthenticateAsync(new SaslMechanismOAuth2(_options.Username!, token.Token), cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        // Delivery has succeeded; a failed QUIT must not tell the caller to send the message again.
        try { await client.DisconnectAsync(true, cancellationToken); }
        catch (Exception ex) when (ex is IOException or MailKit.ProtocolException or OperationCanceledException) { }

    }
}
