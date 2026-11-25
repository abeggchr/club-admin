# SMTP credentials are stored in plain text, behind a write-only API

The treasurer enters the club's SMTP settings (host, port, security, username, password, from address, from name) under "Einstellungen". They live as columns of the single Settings record, and the password is stored in plain text in the database.

- **The API never returns the password.** `GET /api/settings` reports only `passwordSet`. On save, an empty password keeps the stored value; an entirely empty SMTP group clears it. The UI shows "gesetzt" / "nicht gesetzt".
- **Why not ASP.NET Data Protection.** Encrypting the password needs a persisted key ring. ADR 0006 keeps Data Protection keys in memory on purpose, because Railway has no volume we want to depend on; a key ring in the database or object storage would add moving parts, and a lost or rotated key would silently lose the password. For a small club app this is more risk than it removes.
- **What the plain text protects against, and what it does not.** Anyone with database access can read the password. That is the same group that can already read all member data and the Entra client secret in the environment. The mailbox should be a dedicated club account whose password can be rotated.
- **The ePost API key follows the same rule.** It is a column of the Settings record, stored in plain text, never returned (`GET /api/settings` reports only `ePostConfigured`), and an empty value on save keeps the stored key. The same reasoning and the same caveat apply: use a dedicated key that can be rotated.
- **Test email.** The "Test-E-Mail senden" button posts the form values; an empty password falls back to the stored one. The receiver is not stored.
