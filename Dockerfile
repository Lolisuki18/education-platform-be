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

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/storage/temp /app/storage/videos \
    && chown -R app:app /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false

USER app

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "API.dll"]
