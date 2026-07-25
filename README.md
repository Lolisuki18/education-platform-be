# 📚 Education Platform - Backend (API)

![.NET Version](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square&logo=dotnet)
![Database](https://img.shields.io/badge/Database-PostgreSQL-336791?style=flat-square&logo=postgresql)
![Architecture](https://img.shields.io/badge/Architecture-Clean_Architecture-blue?style=flat-square)

Chào mừng bạn đến với repository Backend của **Education Platform** - một nền tảng quản lý giáo dục hiện đại, tối ưu cho việc học tập và giảng dạy trực tuyến. Dự án được xây dựng dựa trên các tiêu chuẩn công nghiệp hiện đại, tích hợp trí tuệ nhân tạo (AI) và các hệ thống thanh toán tự động.

## 🌟 Tính Năng Chính

Dự án bao gồm các module quản lý cốt lõi:

- **Quản Lý Khóa Học (Course Management):** Quản lý Chương (Chapter), Bài học (Lesson), Tài liệu (Material), Bài tập (Assignment) và Quản lý Chính sách.
- **Quản Lý Học Thuật (Academic Management):** Quản lý Môn học (Subject) và Hệ thống chấm điểm (Grading).
- **Tích Hợp AI & Xử Lý Media:**
  - Xử lý file đa phương tiện với **FFmpeg** (nén và chuẩn hóa video bài giảng).
  - Tích hợp mô hình ngôn ngữ lớn (LLM) nội bộ (Ollama) để hỗ trợ học tập.
- **Thanh Toán (Payment):** Tích hợp cổng thanh toán **PayOS** cho việc mua khóa học và quản lý đơn hàng.
- **Bảo Mật:** Hệ thống xác thực và phân quyền dựa trên **JWT (JSON Web Token)**.
- **Thông Báo:** Hệ thống thông báo thời gian thực thông qua **SignalR**.
- **Email:** Gửi thông báo và xác nhận qua Gmail SMTP.

## 🏗️ Kiến Trúc Hệ Thống (Clean Architecture)

Dự án tuân thủ nghiêm ngặt mô hình **Clean Architecture** nhằm đảm bảo tính linh hoạt, dễ bảo trì và mở rộng:

1.  **Domain:** Chứa các Entity, Value Object, Aggregate Root và Domain Logic lõi.
2.  **Application:** Chứa các Use Cases, Interfaces, DTOs và MediatR Commands/Queries.
3.  **Infrastructure:** Triển khai các interface từ Application (Database Persistence, External Services như PayOS).
4.  **API:** Lớp Presentation cung cấp các RESTful API endpoints.

## 🛠️ Công Nghệ Sử Dụng

| Công nghệ                 | Mục đích                      |
| :------------------------ | :---------------------------- |
| **.NET 9.0**              | Framework chính cho Backend   |
| **Entity Framework Core** | ORM để giao tiếp với Database |
| **PostgreSQL**            | Cơ sở dữ liệu quan hệ         |
| **MediatR**               | Thực hiện mô hình CQRS        |
| **AutoMapper**            | Ánh xạ giữa Entity và DTO     |
| **SignalR**               | Truyền thông thời gian thực   |
| **PayOS**                 | Cổng thanh toán               |
| **FFmpeg**                | Xử lý video/audio             |

## 🚀 Hướng Dẫn Cài Đặt

### 📋 Yêu Cầu Hệ Thống

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL](https://www.postgresql.org/downloads/)
- [FFmpeg](https://ffmpeg.org/download.html) (Thêm vào PATH hệ thống)

### ⚙️ Cấu Hình

1.  **Clone repository:**

    ```bash
    git clone https://github.com/Lolisuki18/EducationPlatformBE.git
    cd EducationPlatform
    ```

2.  **Cấu hình file `appsettings.json`:**
    Tạo file `src/API/appsettings.json` từ file `src/API/appsettings.Example.json` và cập nhật các thông tin sau:
    - `ConnectionStrings`: Thông tin kết nối PostgreSQL.
    - `FFmpeg`: Đường dẫn đến file thực thi FFmpeg trên máy của bạn.
    - `PayOS`: Thông tin API Key từ trang quản trị PayOS.
    - `EmailSettings`: Cấu hình tài khoản gửi mail.

3.  **Cập nhật Database:**
    Sử dụng Entity Framework để tạo schema:
    ```bash
    dotnet ef database update --project src/Infrastructure --startup-project src/API
    ```

### ▶️ Chạy Dự Án

```bash
dotnet run --project src/API
```

Sau khi khởi chạy, bạn có thể truy cập Swagger UI tại: `https://localhost:7025/swagger` (hoặc cổng cấu hình tương ứng).

## 🔑 Biến Môi Trường (Environment Variables)

**Không bao giờ commit secrets vào Git.** Thay vào đó, hãy sử dụng biến môi trường hoặc CI/CD secrets.

| Biến                        | Mô tả                                          | Bắt buộc      |
| :-------------------------- | :--------------------------------------------- | :------------ |
| `TEST_DB_CONNECTION_STRING` | Chuỗi kết nối PostgreSQL cho Integration Tests | Khi chạy test |

### Chạy Integration Tests cục bộ

```bash
# Thiết lập biến môi trường trước khi chạy test
$env:TEST_DB_CONNECTION_STRING = "Host=localhost;Database=EducationPlatformDB_Test;Username=postgres;Password=<your_password>"
dotnet test EducationPlatform/tests/IntegrationTests
```

> **Lưu ý bảo mật:** File `appsettings.json` đã được thêm vào `.gitignore`. Chỉ sử dụng `appsettings.Example.json` làm template.

## 🤝 Nhóm Thực Hiện

- **Lê Nguyễn An Ninh (Lolisuki18)** (Lead Developer)
- **Phan Huỳnh Hải Phượng (boncloudy)** (Developer)
- **Phùng Đức Tiệp (TiepDaCoder)** (Developer)

  ❤️Dự án được phát triển cho môn học **PRM (Mobile Application Development)** - FPT University. ❤️
