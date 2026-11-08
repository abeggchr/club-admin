# SCW Backoffice

Backoffice for the club: persons, bookkeeping and invoices. ASP.NET Core backend (`backend/`), React frontend (`frontend/`), Postgres, and S3-compatible object storage for invoice PDFs.

## Local development

Prerequisites: .NET 10 SDK, Node.js, Docker.

### 1. Postgres

```sh
docker run --name scw-postgres -d -p 5432:5432 \
  -e POSTGRES_PASSWORD=secret -e POSTGRES_DB=railway postgres:17
```

This matches the connection string in `backend/appsettings.json`. Migrations are applied on backend startup.

### 2. MinIO (object storage)

```sh
docker run --name scw-minio -d -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin -e MINIO_ROOT_PASSWORD=minioadmin \
  dhi.io/minio server /data --console-address ":9001"
```

- S3 API: http://localhost:9000, web console: http://localhost:9001
- Dev credentials: `minioadmin` / `minioadmin`
- Bucket name: `scw-backoffice`. In Development the backend creates it on startup if missing. It stays private; PDFs are served through the backend.

These values are configured in `backend/appsettings.Development.json` (section `ObjectStorage`).

### 3. Backend

```sh
dotnet run --project backend
```

### 4. Frontend

```sh
cd frontend
npm install
npm run dev
```

## Production

Storage is configured from the variables Railway injects for a bucket: `BUCKET`, `ENDPOINT`, `ACCESS_KEY_ID`, `SECRET_ACCESS_KEY`, `REGION`. No extra setup is needed.

## Tests

```sh
dotnet test
```
