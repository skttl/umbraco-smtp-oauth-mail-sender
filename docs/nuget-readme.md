# Umbraco.Community.SmtpOAuthMailSender

![SmtpOAuthMailSender icon](https://raw.githubusercontent.com/skttl/umbraco-smtp-oauth-mail-sender/main/icon.png)

Send Umbraco email through SMTP with Microsoft Entra OAuth application authentication. Configuration extends the existing SMTP section.

## Install

Choose a version matching Umbraco:

| Umbraco | Package | Framework | Umbraco dependency |
| --- | --- | --- | --- |
| 13 | 13.x.x | .NET 8 | [13.0.0,14.0.0) |
| 17 | 17.x.x | .NET 10 | [17.0.0,18.0.0) |
| 18 | 18.x.x | .NET 10 | [18.0.0,19.0.0) |

```sh
dotnet add package Umbraco.Community.SmtpOAuthMailSender --version 17.0.0
```

Installation requires no Program.cs changes. The composer activates this sender only when `Umbraco:CMS:Global:Smtp:OAuth` exists. Without that section, Umbraco's existing sender remains registered. Configure only one of Graph and OAuth. NuGet may warn rather than reject an incompatible version when the application directly overrides dependencies.

## Configure

Add the following to appsettings.json, then supply the credential separately:

```json
{
  "Umbraco": {
    "CMS": {
      "Global": {
        "Smtp": {
          "From": "noreply@example.com",
          "Host": "smtp.office365.com",
          "Port": 587,
          "Username": "noreply@example.com",
          "SecureSocketOptions": "StartTls",
          "OAuth": {
            "TenantId": "11111111-1111-1111-1111-111111111111",
            "ClientId": "22222222-2222-2222-2222-222222222222",
            "Provider": "MicrosoftEntra"
          }
        }
      }
    }
  }
}
```

Set `Umbraco__CMS__Global__Smtp__OAuth__ClientSecret` in the hosting environment. Local development can use .NET user secrets with the key `Umbraco:CMS:Global:Smtp:OAuth:ClientSecret`. JSON values such as `"${SECRET}"` are not expanded automatically.

Alternatively, omit ClientSecret and configure `OAuth:CertificatePath` as an absolute PFX path. Register the public certificate on the Entra application, grant the application identity read access to the private certificate file and supply `OAuth:CertificatePassword` through a secret provider if needed. Private keys are loaded with ephemeral storage. Configure exactly one credential method. Plan certificate/secret rotation before expiry. Never commit credentials, private keys or PFX files.

Restart after changing settings or credentials. Options validate at application startup and sender resolution. The authentication credential is reused to preserve its token cache. No interactive login or stored refresh token is required.

Smtp.From supplies the default From address; an explicit EmailMessage.From takes precedence. Smtp.Username identifies the authenticated postbox. StartTls and SslOnConnect are the only accepted TLS settings; normal server certificate verification stays enabled. Remove Smtp.Password. MicrosoftEntra is currently the only supported OAuth provider. Pickup-directory delivery is not supported while this sender is active.

## Microsoft Entra and Exchange setup

Register a single-tenant application in Microsoft Entra. Under Office 365 Exchange Online, grant the application permission `SMTP.SendAsApp` and obtain administrator consent. This is a different resource and permission from Graph Mail.Send.

Register the application's service principal in Exchange Online. Use the Enterprise Applications service-principal object ID. Authorize the intended mailbox and the necessary Send As rights as described in Microsoft's SMTP application-access setup. Confirm SMTP AUTH is allowed for the tenant and mailbox; OAuth alone does not enable a disabled protocol. If tenant security policy disallows SMTP AUTH, use the Graph package instead of weakening that policy.

The package requests `https://outlook.office365.com/.default` and authenticates with SASL XOAUTH2. The site must reach both Entra and the configured SMTP host/port.

Follow [Microsoft's client credentials SMTP instructions](https://learn.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth#use-client-credentials-grant-flow-to-authenticate-smtp-imap-and-pop).

## Check and troubleshoot

Open Umbraco's Health Checks, Configuration group, and run the SMTP OAuth mail sender check. It validates settings and requests an access token with a ten-second timeout. A token may come from the credential cache. A success confirms configuration and token acquisition only; it does not prove mailbox authorization, SMTP connectivity or delivery. No test mail is sent and provider exception details are hidden. When unconfigured, the check reports that the sender is inactive. Invalid startup configuration can prevent the application starting; correct it before attempting to use the backoffice.

The sender preserves plain text/HTML, named addresses, Cc/Bcc/ReplyTo and attachments. Attachment streams are read from their current position and remain owned by the caller. With enableNotification, SendEmailNotification runs first; HandleEmail suppresses delivery. Notifications describe the original message, following Umbraco's handled-notification contract.

Each send opens and disposes a MailKit connection. An authentication, TLS or send failure propagates. A failed SMTP QUIT after successful send does not turn an accepted message into a delivery failure.

For Umbraco 17/18, expires is treated as a cancellation deadline from the start of the send, not a queued-mail TTL. Umbraco 18 also uses Smtp.EmailExpiration when expires is omitted. Network timeout or cancellation can happen after remote acceptance, so delivery status may be unknown. There is no automatic retry or background queue.

For 401/authentication errors check credential expiry, tenant/client IDs, certificate access and token resource. For 403/SMTP authorization errors check mailbox scope, Send As rights and protocol policy. Graph 429/503 failures propagate without retries. Never add blanket retry around sends without accounting for duplicates.

This replaces Umbraco's IEmailSender only. Code and packages that send directly through another SMTP client are unaffected.

## License

MIT. The icon adapts Tabler mail-bolt; see THIRD-PARTY-NOTICES.txt for its MIT license.
