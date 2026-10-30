# SMTP OAuth Mail Sender for Umbraco

Send Umbraco email using SMTP with OAuth client credentials. No backoffice login or custom Program.cs registration is needed.

Choose a version matching Umbraco:

| Umbraco | Package | Framework | Umbraco dependency |
| --- | --- | --- | --- |
| 13 | 13.x.x | .NET 8 | [13.0.0,14.0.0) |
| 17 | 17.x.x | .NET 10 | [17.0.0,18.0.0) |
| 18 | 18.x.x | .NET 10 | [18.0.0,19.0.0) |

```sh
dotnet add package Umbraco.Community.SmtpOAuthMailSender --version 13.0.0
```

Installation requires no Program.cs changes. The composer activates this sender only when `Umbraco:CMS:Global:Smtp:OAuth` exists. Without that section, Umbraco's existing sender remains registered. Configure only one of Graph and OAuth. NuGet may warn rather than reject an incompatible version when the application directly overrides dependencies.

## Configuration

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

## Before sending

An administrator must configure SMTP.SendAsApp, Exchange mailbox authorization and SMTP AUTH policy. Only Microsoft Entra is supported in this release.

The Configuration Health Check verifies settings and token acquisition without sending email. Mailbox permission and delivery require a real send test.

See the [complete setup and troubleshooting guide](https://github.com/skttl/umbraco-smtp-oauth-mail-sender/blob/main/docs/nuget-readme.md). Icon: Tabler mail-bolt, MIT licensed.
