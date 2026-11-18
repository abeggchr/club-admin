# Deployment

## Productive development

### 1. Railway

Deploy the app to [Railway](https://railway.com/) as follows:

1. Create a Railway account.
2. Create a new project, connect to the GitHub repository. 
3. Setup public networking. This is where you set the `<railway-domain>`.
4. Add a Bucket. Storage is configured from the variables Railway injects for a bucket: `BUCKET`, `ENDPOINT`, `ACCESS_KEY_ID`, `SECRET_ACCESS_KEY`, `REGION`. No extra setup is needed.
5. Add a Postgres database.
6. Set the following variables in the App:
    *  `ConnectionStrings__Default="Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.POSTGRES_DB}};Username=${{Postgres.POSTGRES_USER}};Password=${{Postgres.POSTGRES_PASSWORD}}"`

If desired:
- use the serverless flag on database and app
- set up a staging environment on a different branch

### 2. Entra

The app requires a Microsoft Entra login and has no user table (ADR 0006).

1. Create an Azure account for your club. So that you get an Entra Directory for the club.
2. App registration in the club tenant, single tenant ("Accounts in this organizational directory only"), platform "Web".
3. Redirect URIs: `https://<railway-domain>/signin-oidc` and `http://localhost:3000/signin-oidc`
4. Certificates & secrets: create a client secret.
5. In the [Entra Admin Center](https://entra.microsoft.com/) under Enterprise application: Properties, switch "Assignment required" on.
6. In the [Entra Admin Center](https://entra.microsoft.com/) under Enterprise application: Users and groups, assign users directly. Invite outside addresses (gmail, outlook.com) as guests first.
7. Set the following variables in the Railway-App:
    * `Authentication__Microsoft__TenantId="<clientId>"`
    * `Authentication__Microsoft__ClientSecret="<clientSecret>"`
    * `Authentication__Microsoft__ClientId="<tenantId>"`

## Local development

Prerequisites: 
* .NET 11 SDK
* Node.js
* [Apple Container](https://github.com/apple/container) (or docker).

### 1. MinIO (object storage)

```sh
container registry login --username xxx dhi.io
container run --name scw-minio -d -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin -e MINIO_ROOT_PASSWORD=minioadmin \
  dhi.io/minio server /data --console-address ":9001"
```

- S3 API: http://localhost:9000, web console: http://localhost:9001
- Dev credentials: `minioadmin` / `minioadmin`
- Bucket name: `club-admin`. In Development the backend creates it on startup if missing. It stays private; PDFs are served through the backend.

These values are configured in `backend/appsettings.Development.json` (section `ObjectStorage`).

### 2. Postgres

```sh
container run --name scw-postgres -d -p 5432:5432 \
  -e POSTGRES_PASSWORD=secret -e POSTGRES_DB=railway postgres:18
```

This matches the connection string in `backend/appsettings.Development.json`. Migrations are applied on backend startup.

### 3. Entra login

The app requires a Microsoft Entra login and has no user table (ADR 0006). First setup Entra (see above).

The backend refuses to start without these settings. Set them with `dotnet user-secrets` locally:

```sh
cd backend
dotnet user-secrets set "Authentication:Microsoft:TenantId" "<tenant id>"
dotnet user-secrets set "Authentication:Microsoft:ClientId" "<application (client) id>"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "<client secret value>"
```

Every restart of the backend logs you out.

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