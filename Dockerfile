FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["EducationPlatform/src/API/API.csproj", "EducationPlatform/src/API/"]
COPY ["EducationPlatform/src/Application/Application.csproj", "EducationPlatform/src/Application/"]
COPY ["EducationPlatform/src/Domain/Domain.csproj", "EducationPlatform/src/Domain/"]
COPY ["EducationPlatform/src/Infrastructure/Infrastructure.csproj", "EducationPlatform/src/Infrastructure/"]

RUN dotnet restore "EducationPlatform/src/API/API.csproj"

COPY . .

WORKDIR "/src/EducationPlatform/src/API"
RUN dotnet publish "API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# The final image has no shell, so the storage folders are prepared here and copied over with the right owner
RUN mkdir -p /app/storage/temp /app/storage/videos

# "Chiseled" image: no shell, no package manager, no curl, runs as the non-root "app" user (uid 1654).
# Far smaller attack surface and image size than the full aspnet image.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build --chown=1654:1654 /app/storage /app/storage

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false

USER app

EXPOSE 8080

# No curl in this image: the application probes itself (GET /healthz on its own port) and exits 0 or 1
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "API.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "API.dll"]
