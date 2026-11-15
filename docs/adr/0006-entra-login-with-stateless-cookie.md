# Microsoft Entra decides who may log in; the app keeps a stateless cookie and no users

The whole app sits behind a Microsoft Entra login (server-side OIDC against the club's tenant). Entra is the source of truth for who may use the app: "Assignment required" is switched on for the Enterprise application and users are assigned or invited as guests there. The app has no user table, no roles and no "not authorised" page; an unassigned account is stopped by Entra's own error page. After login the backend issues an encrypted, stateless auth cookie (7 days sliding, HttpOnly, Secure, SameSite=Lax); the browser never sees a token. There is no server-side session.

- **Logout on every redeploy.** Data Protection keys are kept in memory, not persisted, so the cookie cannot be decrypted after a redeploy or a local restart. Everyone logs in again (usually without a prompt, as the Microsoft session is still alive). We accept this over adding a key store for a small club app that deploys rarely.
- **Logout clears only our cookie**, not the Microsoft session. Session revocation of a single user is not possible; removing the assignment in Entra takes effect when the cookie expires or on the next redeploy.
- **No CSRF token and no CORS.** The frontend and the API are served from the same origin (Vite proxies locally), and SameSite=Lax keeps the browser from sending the cookie on cross-site writes. `/api/auth/logout` is a POST for the same reason.
- **`/api` answers 401, not a redirect.** Only `/api/auth/login` talks to Microsoft. The frontend does the full-page navigation to it, on start (`/api/auth/me`) and on any mid-session 401.
- **Behind Railway's TLS proxy**, `UseForwardedHeaders` makes the redirect URI and the cookie https.
- **No local-dev bypass.** Developers use the real Entra login with `dotnet user-secrets`. Tests replace Entra with a test authentication handler.
