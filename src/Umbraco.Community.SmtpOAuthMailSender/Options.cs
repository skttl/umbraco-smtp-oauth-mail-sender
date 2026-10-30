using System.Net.Mail;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
namespace Umbraco.Community.SmtpOAuthMailSender;

public sealed class EntraOptions
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    public string Provider { get; set; } = "MicrosoftEntra";
}

public sealed class MailSenderOptions : SmtpSettings
{
    public EntraOptions OAuth { get; set; } = new();
}

internal sealed class MailOptionsValidator : IValidateOptions<MailSenderOptions>
{
    public ValidateOptionsResult Validate(string? name, MailSenderOptions options)
    {
        var errors = new List<string>();
        var auth = options.OAuth;
        if (auth is null) return ValidateOptionsResult.Fail("Smtp:OAuth must be configured.");
        if (!Guid.TryParse(auth.TenantId, out _)) errors.Add("OAuth:TenantId must be a tenant GUID.");
        if (!Guid.TryParse(auth.ClientId, out _)) errors.Add("OAuth:ClientId must be an application GUID.");
        if (string.IsNullOrWhiteSpace(auth.ClientSecret) == string.IsNullOrWhiteSpace(auth.CertificatePath))
            errors.Add("Configure exactly one of OAuth:ClientSecret and OAuth:CertificatePath.");
        if (!string.IsNullOrWhiteSpace(auth.CertificatePath) && !Path.IsPathFullyQualified(auth.CertificatePath))
            errors.Add("OAuth:CertificatePath must be an absolute path to a PFX certificate.");
        if (!MessageConversion.IsMailbox(options.From)) errors.Add("Smtp:From must be a valid email address.");
        if (options.DeliveryMethod != SmtpDeliveryMethod.Network || !string.IsNullOrWhiteSpace(options.PickupDirectoryLocation))
            errors.Add("SmtpOAuthMailSender requires Network delivery and no PickupDirectoryLocation.");
        if (auth.Provider != "MicrosoftEntra") errors.Add("OAuth:Provider must be MicrosoftEntra.");
        if (string.IsNullOrWhiteSpace(options.Host) || Uri.CheckHostName(options.Host) == UriHostNameType.Unknown)
            errors.Add("Smtp:Host must be a hostname or IP address.");
        if (options.Port is < 1 or > 65535) errors.Add("Smtp:Port must be between 1 and 65535.");
        if (!MessageConversion.IsBareMailbox(options.Username)) errors.Add("Smtp:Username must be the mailbox email address.");
        if (options.SecureSocketOptions is not (SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect))
            errors.Add("SMTP OAuth requires StartTls or SslOnConnect.");
        if (!string.IsNullOrEmpty(options.Password)) errors.Add("Remove Smtp:Password when using OAuth.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
