# MailArchive API (.NET 10) — build context: ./backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for better layer caching
COPY MailArchiveSystem.sln ./
COPY MailArchive.Domain/MailArchive.Domain.csproj MailArchive.Domain/
COPY MailArchive.Application/MailArchive.Application.csproj MailArchive.Application/
COPY MailArchive.Persistence/MailArchive.Persistence.csproj MailArchive.Persistence/
COPY MailArchive.API/MailArchive.API.csproj MailArchive.API/
RUN dotnet restore MailArchive.API/MailArchive.API.csproj

COPY . .
RUN dotnet publish MailArchive.API/MailArchive.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app/publish .
# /app/storage is mounted from Azure Files in Azure Container Apps
RUN mkdir -p /app/storage/imports /app/storage/attachments
EXPOSE 8080
ENTRYPOINT ["dotnet", "MailArchive.API.dll"]
