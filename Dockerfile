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

COPY --from=build /app/publish .

RUN mkdir -p /app/storage/temp /app/storage/videos

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "API.dll"]
