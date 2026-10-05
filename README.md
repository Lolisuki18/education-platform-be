# 📚 Education Platform - Backend (API)

![.NET Version](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square&logo=dotnet)
![Database](https://img.shields.io/badge/Database-PostgreSQL-336791?style=flat-square&logo=postgresql)
![Architecture](https://img.shields.io/badge/Architecture-Clean_Architecture-blue?style=flat-square)

Chào mừng bạn đến với repository Backend của **Education Platform** - một nền tảng quản lý giáo dục hiện đại, tối ưu cho việc học tập và giảng dạy trực tuyến. Dự án được xây dựng dựa trên các tiêu chuẩn công nghiệp hiện đại, tích hợp xử lý media và các hệ thống thanh toán tự động.

## 🌟 Tính Năng Chính

Dự án bao gồm các module quản lý cốt lõi:

- **Quản Lý Khóa Học (Course Management):** Quản lý Chương (Chapter), Bài học (Lesson), Tài liệu (Material), Bài tập (Assignment) và Quản lý Chính sách.
- **Quản Lý Học Thuật (Academic Management):** Quản lý Môn học (Subject) và Hệ thống chấm điểm (Grading).
- **Media:** Upload video theo từng chunk (có giới hạn kích thước/số chunk và gắn với người upload), lưu local hoặc Cloudinary.
- **Thanh Toán (Payment):** Tích hợp cổng thanh toán **PayOS**: webhook/redirect có xác thực chữ ký, xử lý idempotent, tự huỷ đơn hết hạn và hoàn lại coupon, đơn 0đ ghi danh ngay không qua PayOS.
- **Bảo Mật:** Hệ thống xác thực và phân quyền dựa trên **JWT (JSON Web Token)**.
- **Thông Báo:** Hệ thống thông báo thời gian thực thông qua **SignalR**.
- **Email:** Gửi thông báo và xác nhận qua Gmail SMTP.
- **Observability:** Structured logging với **Serilog**, correlation ID theo từng request, Health Check tách riêng liveness (`/healthz`) và readiness (`/readiness`, kiểm tra DB), và xuất trace/metric qua **OpenTelemetry (OTLP)** khi cấu hình.

## 🏗️ Kiến Trúc Hệ Thống (Clean Architecture)

Dự án tuân thủ nghiêm ngặt mô hình **Clean Architecture** nhằm đảm bảo tính linh hoạt, dễ bảo trì và mở rộng:

1.  **Domain:** Chứa các Entity, Value Object, Aggregate Root và Domain Logic lõi.
2.  **Application:** Chứa các Use Cases, Interfaces, DTOs và MediatR Commands/Queries.
3.  **Infrastructure:** Triển khai các interface từ Application (Database Persistence, External Services như PayOS).
4.  **API:** Lớp Presentation cung cấp các RESTful API endpoints.

## 🛠️ Công Nghệ Sử Dụng

| Công nghệ                 | Mục đích                             |
| :------------------------ | :------------------------------------ |
| **.NET 9.0**              | Framework chính cho Backend           |
| **Entity Framework Core** | ORM để giao tiếp với Database         |
| **PostgreSQL**            | Cơ sở dữ liệu quan hệ                 |
| **MediatR**               | Thực hiện mô hình CQRS                |
| **AutoMapper**            | Ánh xạ giữa Entity và DTO             |
| **SignalR**               | Truyền thông thời gian thực           |
| **PayOS**                 | Cổng thanh toán                       |
| **Serilog**               | Structured logging (console + file)   |
| **Docker**                | Đóng gói & triển khai                 |

## 🚀 Hướng Dẫn Cài Đặt

### 📋 Yêu Cầu Hệ Thống

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL](https://www.postgresql.org/downloads/)

### ⚙️ Cấu Hình

1.  **Clone repository:**

    ```bash
    git clone https://github.com/Lolisuki18/EducationPlatformBE.git
    cd EducationPlatform
    ```

2.  **Cấu hình file `appsettings.json`:**
    Tạo file `src/API/appsettings.json` từ file `src/API/appsettings.Example.json` và cập nhật các thông tin sau:
    - `ConnectionStrings`: Thông tin kết nối PostgreSQL.
    - `PayOS`: Thông tin API Key từ trang quản trị PayOS. **Bắt buộc** (ứng dụng kiểm tra lúc khởi động và từ chối chạy nếu thiếu).
    - `CorsSettings:AllowedOrigins`: Danh sách origin của frontend. **Bắt buộc ở Production**.
    - `EmailSettings`: Cấu hình tài khoản gửi mail.
    - `JwtSettings:SecretKey`: **Bắt buộc tối thiểu 32 ký tự** (256-bit). Ứng dụng sẽ từ chối khởi động nếu key ngắn hơn mức này. Có thể tạo nhanh bằng:
      ```bash
      openssl rand -base64 48
      ```

3.  **Cập nhật Database:**
    Mặc định ứng dụng tự migrate + seed khi khởi động (có khoá advisory của PostgreSQL nên nhiều instance khởi động cùng lúc vẫn an toàn). Cũng có thể chạy thủ công:
    ```bash
    dotnet ef database update --project src/Infrastructure --startup-project src/API
    ```
    Khi triển khai nhiều instance, nên tách migration ra thành một bước riêng và tắt auto-migrate trên các instance phục vụ traffic:
    ```bash
    dotnet API.dll --migrate        # chạy migration + seed rồi thoát (init container / release step)
    # và đặt Database__AutoMigrate=false cho các instance chạy API
    ```

### ▶️ Chạy Dự Án

```bash
dotnet run --project src/API
```

Sau khi khởi chạy, bạn có thể truy cập Swagger UI tại: `https://localhost:7025/swagger` (hoặc cổng cấu hình tương ứng).

Health check endpoints (dùng cho container orchestration):

- `GET /healthz` — liveness: tiến trình còn sống (không kiểm tra dependency, nên DB chập chờn không làm container bị restart).
- `GET /readiness` — readiness: kết nối được tới database, an toàn để nhận traffic.

### 🔀 Phiên bản API

Mọi endpoint truy cập được qua cả `/api/...` (như trước đây) và `/api/v1/...`. Swagger chỉ liệt kê các route `/api/v1/...`. Frontend nên chuyển dần sang `/api/v1`.

### 🐳 Chạy bằng Docker

```bash
docker build -t education-platform-be .
docker run -p 8080:8080 --env-file .env education-platform-be
```

Image chạy bằng user không phải root và có sẵn `HEALTHCHECK` gọi `/healthz` mỗi 30 giây.

Khi chạy sau reverse proxy / load balancer (nginx, Traefik, ...) phải khai báo proxy tin cậy để rate limit và HTTPS redirect thấy đúng IP/scheme của client:

```
ForwardedHeaders__TrustAll=true                 # chỉ khi app chỉ truy cập được qua proxy (mạng Docker nội bộ)
# hoặc liệt kê cụ thể
ForwardedHeaders__KnownProxies__0=10.0.0.5
ForwardedHeaders__KnownNetworks__0=172.16.0.0/12
```

Production cũng cần `CorsSettings__AllowedOrigins__0=https://app.example.com`.

## 🔑 Biến Môi Trường (Environment Variables)

**Không bao giờ commit secrets vào Git.** Thay vào đó, hãy sử dụng biến môi trường hoặc CI/CD secrets.

| Biến                        | Mô tả                                          | Bắt buộc                     |
| :-------------------------- | :--------------------------------------------- | :--------------------------- |
| `TEST_DB_CONNECTION_STRING` | Chuỗi kết nối PostgreSQL cho Integration Tests | Khi chạy test                |
| `SEED_DEFAULT_PASSWORD`     | Mật khẩu mặc định khi seed tài khoản hệ thống  | Không (Mặc định: `18102004`) |

Các khoá cấu hình vận hành (đặt trong `appsettings.json` hoặc dạng biến môi trường `A__B`):

| Khoá                                  | Mặc định                | Ý nghĩa                                                                  |
| :------------------------------------ | :---------------------- | :----------------------------------------------------------------------- |
| `RateLimiting:Enabled`                | `true`                  | Bật/tắt rate limit (theo user, hoặc theo IP khi chưa đăng nhập)          |
| `Database:AutoMigrate`                | `true`                  | Tự migrate + seed lúc khởi động                                          |
| `Swagger:Enabled`                     | chỉ Development         | Bật Swagger UI                                                           |
| `Security:UseHttpsRedirection`        | `true` ngoài Development | HTTPS redirect + HSTS                                                    |
| `Logging:File:Enabled`                | chỉ Development         | Ghi log ra file `logs/` (container nên chỉ log ra console)               |
| `Upload:MaxChunkBytes` / `MaxChunksPerUpload` / `MaxTotalBytesPerUpload` | 32MB / 2000 / 2GB | Giới hạn upload video theo chunk |
| `Orders:ExpiredOrderCleanupEnabled`   | `true`                  | Job huỷ đơn chưa thanh toán sau ~20 phút và hoàn lại coupon              |
| `OpenTelemetry:OtlpEndpoint`          | (trống = tắt)           | Địa chỉ OTLP collector để xuất trace + metric                            |

### Chạy Integration Tests cục bộ

```bash
# Thiết lập biến môi trường trước khi chạy test
$env:TEST_DB_CONNECTION_STRING = "Host=localhost;Database=EducationPlatformDB_Test;Username=postgres;Password=<your_password>"
dotnet test EducationPlatform/tests/IntegrationTests
```

> CI chạy unit test kèm coverage (tối thiểu 65% dòng cho Domain + Application, xem `.github/scripts/check-coverage.py`) rồi mới chạy integration test.

> **Lưu ý bảo mật:** File `appsettings.json` đã được thêm vào `.gitignore`. Chỉ sử dụng `appsettings.Example.json` làm template.

## 📝 Logging

Ứng dụng dùng **Serilog** để ghi log có cấu trúc:

- Console (khi chạy local/dev).
- File, xoay vòng theo ngày, lưu tại `logs/log-YYYYMMDD.txt` (giữ tối đa 14 ngày).

Mỗi request được gắn một `CorrelationId` (đọc từ header `X-Correlation-Id` nếu có, hoặc tự sinh) — được trả về trong response header và xuất hiện trong mọi dòng log của request đó, giúp truy vết log giữa các service/background task dễ dàng hơn.

## 🔄 CI/CD

- **`.github/workflows/dotnet-ci.yml`**: chạy trên mọi push/PR vào `main`, `develop`, `master` — kiểm tra format, build, quét lỗ hổng NuGet, chạy toàn bộ test (unit + integration, có service PostgreSQL đi kèm).
- **`.github/workflows/docker-publish.yml`**: chạy khi push vào `main` — build + chạy test trước, chỉ build & push Docker image (tag `latest` và tag theo commit SHA) khi test pass.
- **`.github/workflows/codeql.yml`**: quét bảo mật tĩnh (CodeQL) cho code C#.

## 🤝 Nhóm Thực Hiện

- **Lê Nguyễn An Ninh (Lolisuki18)** (Lead Developer)
- **Phan Huỳnh Hải Phượng (boncloudy)** (Developer)
- **Phùng Đức Tiệp (TiepDaCoder)** (Developer)

  ❤️Dự án được phát triển cho môn học **PRM (Mobile Application Development)** - FPT University. ❤️
