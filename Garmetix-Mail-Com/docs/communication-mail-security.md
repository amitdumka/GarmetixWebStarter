# Security Checklist

- Encrypt SMTP passwords and API keys at rest using authenticated encryption.
- Keep protection keys outside the database and ephemeral container layers.
- Never return stored secrets; use retain-or-rotate semantics.
- Redact secrets, authorization headers and sensitive provider payloads from logs.
- Enforce permissions and tenant/company/store scope on the backend.
- Reauthorize source records and attachments on every access.
- Validate email headers to prevent CR/LF injection.
- Validate provider hosts/ports and block unsafe production TLS settings.
- Consider SSRF protections for custom hosts according to deployment needs.
- Authenticate and deduplicate webhooks using current official provider rules.
- Limit webhook body size and request rate.
- Escape template variables and sanitize administrator-authored HTML.
- Block scripts, remote active content and dangerous attachment types.
- Apply attachment size/count limits, content checks, checksums and random names.
- Prevent path traversal and direct unauthenticated storage access.
- Rate-limit sends, test emails and wide broadcasts.
- Scope suppression data by tenant unless explicitly designed otherwise.
- Audit provider/configuration/template/retry/suppression and sensitive access.
- Do not expose public SMTP port 25 or create an open relay.
- Test cross-tenant ID enumeration and confidential HR/Payroll access.

## Key-loss warning

Loss of Data Protection/encryption keys can make provider credentials unrecoverable. Document protected backup, restore, access permissions and rotation. Never silently regenerate keys in production and assume old secrets remain readable.

