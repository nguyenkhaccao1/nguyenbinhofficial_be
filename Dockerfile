# API Nguyen Binh Official — build Release, chay bang user khong phai root.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore truoc (cache layer) — chi copy file project.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/NguyenBinh.Shared/NguyenBinh.Shared.csproj src/NguyenBinh.Shared/
COPY src/NguyenBinh.Domain/NguyenBinh.Domain.csproj src/NguyenBinh.Domain/
COPY src/NguyenBinh.Application/NguyenBinh.Application.csproj src/NguyenBinh.Application/
COPY src/NguyenBinh.Infrastructure/NguyenBinh.Infrastructure.csproj src/NguyenBinh.Infrastructure/
COPY src/NguyenBinh.Api/NguyenBinh.Api.csproj src/NguyenBinh.Api/
RUN dotnet restore src/NguyenBinh.Api/NguyenBinh.Api.csproj -r linux-x64

COPY src/ src/
RUN dotnet publish src/NguyenBinh.Api/NguyenBinh.Api.csproj -c Release -r linux-x64 --self-contained false \
    --no-restore -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

# Thu muc luu file (private + data-protection keys); mount volume vao day.
RUN mkdir -p /app/storage && chown -R $APP_UID /app/storage
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=Asia/Ho_Chi_Minh
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -fs http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "NguyenBinh.Api.dll"]
