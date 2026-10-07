# 📚 Education Platform - Backend (API)

![.NET Version](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square&logo=dotnet)
![Database](https://img.shields.io/badge/Database-PostgreSQL-336791?style=flat-square&logo=postgresql)
![Architecture](https://img.shields.io/badge/Architecture-Clean_Architecture-blue?style=flat-square)

Chào mừng bạn đến với repository Backend của **Education Platform** - một nền tảng quản lý giáo dục hiện đại, tối ưu cho việc học tập và giảng dạy trực tuyến. Dự án được xây dựng dựa trên các tiêu chuẩn công nghiệp hiện đại, tích hợp xử lý media và các hệ thống thanh toán tự động.

## 🌟 Tính Năng Chính

Dự án bao gồm các module quản lý cốt lõi:

- **Quản Lý Khóa Học (Course Management):** Quản lý Chương (Chapter), Bài học (Lesson), Tài liệu (Material), Bài tập (Assignment) và Quản lý Chính sách.
- **Quản Lý Học Thuật (Academic Management):** Quản lý Môn học (Subject) và Hệ thống chấm điểm (Grading).
- **Media:** Upload video theo từng chunk (có giới hạn kích thước/số chunk và gắn với người upload), lưu local hoặc Cloudinary. Video lưu trên server **không công khai**: chỉ truy cập được qua link có chữ ký và hạn dùng (xem mục *Video được bảo vệ*).
- **Thông báo:** thông báo trong app (lưu DB, đẩy realtime qua SignalR `courseHub` và gửi kèm email) cho duyệt khoá học, khoá học mới chờ duyệt, khiếu nại.
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

### 🎬 Video được bảo vệ

File trong `Storage/videos` không còn phục vụ công khai. `GET /media/videos/...` cần `?exp=...&sig=...` hợp lệ, nếu không trả 403. Link này được API tự cấp:

- `GET /api/enrollments/{id}` (học viên đã ghi danh hoặc admin): mọi `videoUrl` lưu trên server được thay bằng link có chữ ký, sống `Media:UrlLifetimeMinutes` phút (mặc định 240).
- `GET /api/courses/{id}` cho admin / giáo viên chủ khoá: tương tự.
- Video ngoài (https://…, ví dụ YouTube) giữ nguyên. Video đã upload lên **Cloudinary** vẫn là link công khai của Cloudinary (chưa dùng được chữ ký) — nếu cần bảo vệ nội dung trả phí tuyệt đối, hãy để `Cloudinary` trống và dùng lưu trữ local.
- Khoá ký lấy từ `Media:SigningKey`, để trống thì suy ra từ `JwtSettings:SecretKey`. Link `videoUrl` của giáo viên nhập vào chỉ được là đường dẫn trong storage hoặc `https://`; đặt `Media:AllowedExternalHosts` để giới hạn thêm domain.

### 🔔 Thông báo

`GET /api/notifications?unreadOnly=&pageIndex=&pageSize=`, `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`. Thông báo mới cũng được đẩy realtime tới mọi kết nối `/courseHub` của người nhận qua sự kiện `Notification`. Tắt email đi kèm bằng `Notifications:EmailEnabled=false`. Thông báo tự xoá sau `Retention:NotificationDays` ngày.

### 🔐 Quên mật khẩu & đổi mật khẩu

- `POST /api/auth/forgot-password` `{ "email" }`: gửi mã 6 số (hiệu lực 10 phút, chỉ lưu dạng băm, gửi lại cách nhau ≥ 60 giây). Luôn trả `202` với cùng một nội dung dù email có tồn tại hay không, để không lộ ai đã đăng ký. Chỉ tài khoản đã xác thực, đang hoạt động mới nhận được mã.
- `POST /api/auth/reset-password` `{ "email", "otp", "newPassword" }`: đặt mật khẩu mới bằng mã trên. Mã dùng một lần; đoán sai nhiều lần thì khoá theo email một thời gian. Thành công thì **đăng xuất mọi thiết bị**.
- `POST /api/auth/change-password` `{ "currentPassword", "newPassword" }` (cần đăng nhập): đổi mật khẩu, đăng xuất các thiết bị khác và trả token mới cho thiết bị đang dùng. Nhập sai mật khẩu hiện tại nhiều lần thì bị khoá tạm.
- Lưu ý: access token đã cấp cho các thiết bị khác vẫn dùng được đến khi hết hạn (`JwtSettings:ExpiryMinutes`); refresh token của chúng thì đã bị thu hồi.

### 🔒 Dữ liệu cá nhân (xuất dữ liệu & xoá tài khoản)

- `GET /api/user/me/export`: tải bản sao dữ liệu của chính mình (hồ sơ, ghi danh, đơn hàng, đánh giá, khiếu nại, thông báo, khoá học đang dạy) dưới dạng JSON. Không bao giờ có hash mật khẩu, mã OTP hay token.
- `DELETE /api/user/me` (body `{ "password": "..." }`): người dùng tự xoá tài khoản, phải nhập lại mật khẩu. `DELETE /api/user/{id}` (admin): xoá theo yêu cầu của người khác. Cả hai giới hạn 5 lần / 10 phút.
- Đây là **ẩn danh hoá, không xoá dòng**: đơn hàng, ghi danh và đánh giá vẫn trỏ tới người dùng nên dòng `Users` được giữ lại nhưng email, số điện thoại, tên, giới thiệu, mã OTP bị xoá (email/sđt được giải phóng, đăng ký lại bằng chính chúng được), mật khẩu bị thay bằng giá trị ngẫu nhiên, mọi phiên đăng nhập bị thu hồi. Thông báo của người đó bị xoá, các dòng audit cũ chứa email/sđt cũng bị xoá và audit mới không còn ghi các trường này.
- Không cho xoá khi: còn đơn đang chờ thanh toán, giáo viên còn khoá học đã xuất bản (admin phải gỡ trước), hoặc là admin cuối cùng (HTTP 409).
- Còn được giữ lại có chủ đích: đơn hàng (nghĩa vụ kế toán), nội dung đánh giá và khiếu nại (hiển thị dưới tên "Deleted user").

### 🔀 Phiên bản API

Mọi endpoint truy cập được qua cả `/api/...` (như trước đây) và `/api/v1/...`. Swagger chỉ liệt kê các route `/api/v1/...`. Frontend nên chuyển dần sang `/api/v1`.

### 🐳 Chạy toàn bộ môi trường dev bằng Docker Compose

```bash
docker compose up --build
```

Khởi động PostgreSQL, API (http://localhost:8080, Swagger ở trang gốc) và **Mailpit** (http://localhost:8025): mọi email (mã OTP, thông báo) rơi vào hộp thư giả này nên không cần cấu hình SMTP. Có dữ liệu demo và tài khoản admin `admin@example.com` / `Admin-dev-only-1`. Đổi giá trị bằng file `.env` (xem `.env.example`). **Chỉ dùng cho phát triển.**

### 🐳 Chạy bằng Docker

```bash
docker build -t education-platform-be .
docker run -p 8080:8080 --env-file .env education-platform-be
```

Image dùng nền `aspnet:9.0-noble-chiseled` (không shell, không curl, chạy bằng user `app`). `HEALTHCHECK` chạy `dotnet API.dll --healthcheck`: ứng dụng tự gọi `/healthz` của chính nó và thoát với mã 0/1.

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
| `Database:SeedDemoData`               | chỉ Development        | Seed user/khoá học/đơn hàng giả. **Không bật ở Production**              |
| `Admin:Email` / `Admin:Password`      | (trống)                 | Tài khoản admin đầu tiên, chỉ tạo khi chưa có admin nào (mật khẩu ≥ 8 ký tự, có chữ và số) |
| `Retention:*`                         | bật, 7 / 365 / 7 ngày   | Dọn refresh session hết hạn, audit log cũ, tài khoản không xác thực email |
| `JwtSettings:ExpiryMinutes`           | `60`                    | Thời hạn access token (cho phép số lẻ)                                   |
| `Media:*`                             | khoá suy ra từ JWT, 240 phút | Chữ ký video, thời hạn link, danh sách host ngoài được phép          |
| `Notifications:EmailEnabled`          | `true`                  | Gửi email kèm mỗi thông báo trong app                                    |
| `OutputCache:Enabled` / `PublicSeconds` | bật, 30 giây          | Cache GET công khai (danh sách khoá học, lớp, môn…) — **chỉ cho khách chưa đăng nhập** |
| `Caching:Enabled`                     | `true`                  | Cache 2 phút cho báo cáo thống kê (theo người dùng và tham số)           |
| `Swagger:Enabled`                     | chỉ Development         | Bật Swagger UI                                                           |
| `Security:UseHttpsRedirection`        | `true` ngoài Development | HTTPS redirect + HSTS                                                    |
| `Logging:File:Enabled`                | chỉ Development         | Ghi log ra file `logs/` (container nên chỉ log ra console)               |
| `Upload:MaxChunkBytes` / `MaxChunksPerUpload` / `MaxTotalBytesPerUpload` | 32MB / 2000 / 2GB | Giới hạn upload video theo chunk |
| `Orders:ExpiredOrderCleanupEnabled`   | `true`                  | Job huỷ đơn chưa thanh toán sau ~20 phút và hoàn lại coupon              |
| `OpenTelemetry:OtlpEndpoint`          | (trống = tắt)           | Địa chỉ OTLP collector để xuất trace + metric                            |

### Chạy Integration Tests cục bộ

Không cần cài PostgreSQL nếu máy có Docker: test tự dựng một container `postgres:16-alpine` (Testcontainers). Thứ tự ưu tiên: biến `TEST_DB_CONNECTION_STRING` → container Testcontainers (tắt bằng `TESTCONTAINERS_DISABLED=true`) → `ConnectionStrings:Test` trong `tests/IntegrationTests/appsettings.json`.


```bash
# Thiết lập biến môi trường trước khi chạy test
$env:TEST_DB_CONNECTION_STRING = "Host=localhost;Database=EducationPlatformDB_Test;Username=postgres;Password=<your_password>"
dotnet test EducationPlatform/tests/IntegrationTests
```

> **Tài khoản & mật khẩu:** hệ thống không còn tự tạo tài khoản mặc định. Ở Production hãy đặt `Admin__Email` + `Admin__Password` cho lần chạy đầu tiên. Nếu DB được seed bởi phiên bản cũ, tài khoản admin trong đó có mật khẩu mặc định đã bị lộ trong source: **đổi ngay**, ứng dụng sẽ ghi log mức Critical mỗi lần khởi động cho đến khi bạn đổi. Dữ liệu demo cũ (user `studentN@gmail.com`, khoá học, đơn hàng giả) cần được xoá thủ công.
>
> Chính sách mật khẩu: 8–72 byte, có ít nhất một chữ và một số. Mã OTP chỉ nằm trong email (DB lưu hash), gửi lại tối đa 1 lần/60 giây. Email được gửi nền qua hàng đợi có retry nên SMTP lỗi không làm chậm hay hỏng request.
>
> CI chạy unit test kèm coverage (tối thiểu 65% dòng cho Domain + Application, xem `.github/scripts/check-coverage.py`) rồi mới chạy integration test.

> **Lưu ý bảo mật:** File `appsettings.json` đã được thêm vào `.gitignore`. Chỉ sử dụng `appsettings.Example.json` làm template.

## 📝 Logging

Ứng dụng dùng **Serilog** để ghi log có cấu trúc:

- Console (khi chạy local/dev).
- File, xoay vòng theo ngày, lưu tại `logs/log-YYYYMMDD.txt` (giữ tối đa 14 ngày).

Mỗi request được gắn một `CorrelationId` (đọc từ header `X-Correlation-Id` nếu có, hoặc tự sinh) — được trả về trong response header và xuất hiện trong mọi dòng log của request đó, giúp truy vết log giữa các service/background task dễ dàng hơn.

## 🔄 CI/CD

- **`.github/workflows/dotnet-ci.yml`**: chạy trên mọi push/PR vào `main`, `develop`, `master`.
  - Job `build-and-test`: kiểm tra format, build, quét lỗ hổng NuGet, chạy toàn bộ test (unit + integration, có service PostgreSQL đi kèm), kiểm tra ngưỡng coverage.
  - Job `docker` (chạy sau khi test pass): build Docker image trên mọi PR (Dockerfile hỏng sẽ làm PR đỏ), quét bằng **Trivy** (fail khi có lỗ hổng CRITICAL/HIGH đã có bản vá), và chỉ khi push vào `main` mới đẩy image lên Docker Hub (tag `latest` và tag theo commit SHA). Cần secret `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`.
- **`.github/dependabot.yml`**: mỗi tuần mở PR nâng NuGet (gộp minor/patch thành một PR), GitHub Actions và image Docker. Bỏ qua bản major của EF Core/ASP.NET/image .NET (app đang ở .NET 9) và MediatR (từ v13 là phần mềm có phí).
- **`.github/workflows/codeql.yml`**: quét bảo mật tĩnh (CodeQL) cho code C#.

## 🤝 Nhóm Thực Hiện

- **Lê Nguyễn An Ninh (Lolisuki18)** (Lead Developer)
- **Phan Huỳnh Hải Phượng (boncloudy)** (Developer)
- **Phùng Đức Tiệp (TiepDaCoder)** (Developer)

  ❤️Dự án được phát triển cho môn học **PRM (Mobile Application Development)** - FPT University. ❤️
