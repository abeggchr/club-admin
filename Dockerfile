# --- Frontend build ---
FROM node:22-alpine AS frontend
WORKDIR /src
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# --- Backend build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/backend.csproj ./
RUN dotnet restore
COPY backend/ ./
RUN dotnet publish -c Release -o /app --no-restore

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# SwissQRBill.NET renders the payment part to PNG with SkiaSharp (self-contained native lib, no libfontconfig needed),
# but the slim aspnet image ships no fonts at all: install Liberation (Arial/Helvetica metrics) for the slip text.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
COPY --from=backend /app ./
COPY --from=frontend /src/dist ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production
# Railway injects PORT; fall back to 8080 locally.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet backend.dll"]
