# Sử dụng SDK của .NET 9.0 làm build environment
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy các file project (.csproj) trước để restore dependencies (tận dụng Docker cache)
COPY ["EducationPlatform/src/API/API.csproj", "EducationPlatform/src/API/"]
COPY ["EducationPlatform/src/Application/Application.csproj", "EducationPlatform/src/Application/"]
COPY ["EducationPlatform/src/Domain/Domain.csproj", "EducationPlatform/src/Domain/"]
COPY ["EducationPlatform/src/Infrastructure/Infrastructure.csproj", "EducationPlatform/src/Infrastructure/"]

# Restore các NuGet packages
RUN dotnet restore "EducationPlatform/src/API/API.csproj"

# Copy toàn bộ mã nguồn còn lại
COPY . .

# Tiến hành Build và Publish ứng dụng dưới dạng Release
WORKDIR "/src/EducationPlatform/src/API"
RUN dotnet publish "API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Environment (Chạy ứng dụng)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy các file đã build từ build stage
COPY --from=build /app/publish .

# Tạo sẵn các thư mục tạm cho StorageService để tránh lỗi phân quyền hoặc thiếu thư mục
RUN mkdir -p /app/storage/temp /app/storage/videos

# Cấu hình cổng chạy mặc định (Render sử dụng cổng 8080 hoặc tự động ánh xạ)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "API.dll"]
