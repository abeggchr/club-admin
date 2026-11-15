# SCW Backoffice

Backoffice for the club: persons, bookkeeping and invoices. ASP.NET Core backend (`backend/`), React frontend (`frontend/`), Postgres, and S3-compatible object storage for invoice PDFs.

## Local development

Prerequisites: .NET 10 SDK, Node.js, Docker.

Command using Apple's container engine. Login with `container registry login --username xxx dhi.io`

### 1. Postgres

```sh
container run --name scw-postgres -d -p 5432:5432 \
  -e POSTGRES_PASSWORD=secret -e POSTGRES_DB=railway postgres:18
```

This matches the connection string in `backend/appsettings.Development.json`. Migrations are applied on backend startup.

### 2. MinIO (object storage)

```sh
container run --name scw-minio -d -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin -e MINIO_ROOT_PASSWORD=minioadmin \
  dhi.io/minio server /data --console-address ":9001"
```

- S3 API: http://localhost:9000, web console: http://localhost:9001
- Dev credentials: `minioadmin` / `minioadmin`
- Bucket name: `scw-backoffice`. In Development the backend creates it on startup if missing. It stays private; PDFs are served through the backend.

These values are configured in `backend/appsettings.Development.json` (section `ObjectStorage`).

### 3. Entra login

The app requires a Microsoft Entra login and has no user table (ADR 0006). The backend refuses to start without these settings. Set them with `dotnet user-secrets` locally:

```sh
cd backend
dotnet user-secrets set "Authentication:Microsoft:TenantId" "<tenant id>"
dotnet user-secrets set "Authentication:Microsoft:ClientId" "<application (client) id>"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "<client secret value>"
```

Entra setup (once):

1. App registration in the club tenant, single tenant ("Accounts in this organizational directory only"), platform "Web".
2. Redirect URIs: `https://<railway-domain>/signin-oidc` and `http://localhost:3000/signin-oidc` (registering `http://localhost:5210/signin-oidc` is optional).
3. Certificates & secrets: create a client secret.
4. Enterprise application: Properties, switch "Assignment required" on.
5. Users and groups: assign users directly (free tier). Invite outside addresses (gmail, outlook.com) as guests first.
6. Production: set `Authentication__Microsoft__TenantId`, `Authentication__Microsoft__ClientId` and `Authentication__Microsoft__ClientSecret` as Railway variables.

Locally, browse via Vite on `http://localhost:3000`, not the backend port. Every restart of the backend logs you out.

### 4. Backend

```sh
dotnet run --project backend
```

### 5. Frontend

```sh
cd frontend
npm install
npm run dev
```

## Production

The three `Authentication__Microsoft__*` variables (see Entra login) must be set before deploying.

Storage is configured from the variables Railway injects for a bucket: `BUCKET`, `ENDPOINT`, `ACCESS_KEY_ID`, `SECRET_ACCESS_KEY`, `REGION`. No extra setup is needed.

## Tests

```sh
dotnet test
```
