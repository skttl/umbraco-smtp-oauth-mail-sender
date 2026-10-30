# SmtpOAuthMailSender design

Approved in chat on 2026-10-30. Independent repository and NuGet package. Package majors 13, 17 and 18 match Umbraco; main targets 18, v17/main and v13/main target the other lines.

Replace IEmailSender only when Umbraco:CMS:Global:Smtp:OAuth exists. Keep normal Umbraco email sending when absent. Reject simultaneous Graph and OAuth configuration. Reuse Smtp.From, Host, Port, Username and mandatory TLS. Bind new settings through typed options; validate on startup, never fall back to basic authentication after a failure.

Microsoft Entra application authentication supports client secret or a PFX certificate. Credentials are reused for token caching. No user login, refresh-token persistence or backoffice UI. First SMTP provider is Microsoft Entra, not a promise of arbitrary OAuth-provider compatibility.

Preserve notifications and handled semantics, text/HTML, named recipients, Cc/Bcc/ReplyTo, attachments and caller-owned streams. Honor expiry as a cancellation deadline in 17/18. Send errors propagate without secrets or message contents in logs. No automatic retry of ambiguous sends.

SMTP uses MailKit XOAUTH2 and a fresh connection per send, with required STARTTLS or implicit TLS and normal certificate validation.

Unlog-inspired repository README, NuGet README, Marketplace README and metadata, appsettings schema, buildTransitive target, CI and OIDC publishing. Tabler mail-bolt icon, #526358 background and #F5F5F0 foreground; include upstream license notice.

Each package includes a Configuration Health Check for settings and token acquisition; success does not assert mailbox authorization. The user also requested examples/TestSite with local SQLite and project references. The test site follows UmbracoMajor, stores databases and data-protection keys separately per major, and uses the interactive installer with user-chosen admin credentials.
