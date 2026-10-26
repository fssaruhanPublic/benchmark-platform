# Benchmark dashboard: API + the built web UI in one image.
#
#   docker build -t benchmark-dashboard .
#   docker run -p 8080:8080 benchmark-dashboard                      # SQLite inside the container (ephemeral)
#   docker run -p 8080:8080 -e Database__Provider=SqlServer \
#     -e ConnectionStrings__Benchmarks="Server=...;Authentication=Active Directory Managed Identity;..." benchmark-dashboard
#
# Hosted use should always set Database__Provider=SqlServer: a container's file system is not
# durable and the whole point of the runs stored here is to be compared over time.

# --- Web UI -------------------------------------------------------------------------------
FROM node:22-alpine AS web
WORKDIR /src/src/web
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci
COPY src/web/ ./
# vite.config.ts writes the build to ../BenchmarkPlatform.Api/wwwroot.
RUN npm run build

# --- API ----------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props BenchmarkPlatform.slnx ./
COPY src/BenchmarkPlatform.Core/BenchmarkPlatform.Core.csproj src/BenchmarkPlatform.Core/
COPY src/BenchmarkPlatform.Api/BenchmarkPlatform.Api.csproj src/BenchmarkPlatform.Api/
RUN dotnet restore src/BenchmarkPlatform.Api/BenchmarkPlatform.Api.csproj
COPY src/BenchmarkPlatform.Core/ src/BenchmarkPlatform.Core/
COPY src/BenchmarkPlatform.Api/ src/BenchmarkPlatform.Api/
COPY --from=web /src/src/BenchmarkPlatform.Api/wwwroot src/BenchmarkPlatform.Api/wwwroot
RUN dotnet publish src/BenchmarkPlatform.Api/BenchmarkPlatform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "BenchmarkPlatform.Api.dll"]
