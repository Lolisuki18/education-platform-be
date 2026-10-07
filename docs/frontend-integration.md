# Ghi chú tích hợp Frontend

Tài liệu ngắn về những điều dễ vấp khi nối frontend vào API này. Mọi thứ ở đây đã được kiểm tra trên response thật của API (xem `EducationPlatform/tests/IntegrationTests`). Danh sách endpoint đầy đủ: Swagger (`/swagger` ở Development, chỉ liệt kê `/api/v1/...`).

## 1. Hình dạng response

Có **hai** dạng, tuỳ thành công hay lỗi:

```jsonc
// Thành công: envelope
{ "isSuccess": true, "statusCode": 200, "message": "Request successful", "data": { ... } }

// Lỗi nghiệp vụ / hệ thống: ProblemDetails (không có envelope)
{ "title": "Unauthorized", "status": 401, "detail": "Invalid credentials.", "instance": "POST /api/auth/login" }

// Lỗi validate (400): ProblemDetails + errors theo tên trường (PascalCase)
{ "title": "Validation Error", "status": 400, "detail": "...", "errors": { "Email": ["Invalid email format."], "Phone": ["Phone must be 10-11 digits."] } }
```

- Lỗi nên đọc từ `detail` (hiển thị cho người dùng được), form lỗi từ `errors`.
- `401`/`403` do JWT thiếu/sai có thể **không có body**. Do tài khoản bị khoá hoặc quyền đổi thì có body (`"Your account is inactive..."`, `"Your permissions have changed. Please refresh your session."`).
- JSON dùng camelCase. **Enum số** (không phải chuỗi) ở: `OrderStatus`, `OrderMethod`, `ComplaintStatus`, `QuizType`, `MaterialType`, `EnrollmentStatus`. Enum chuỗi: `role`, `course.status`.
- Danh sách phân trang: `PagedResult` (`items`, `pageIndex`, `pageSize`, `totalItems`, `totalPages`, ...) ở khoá học (`/courses`), người dùng, coupon, thông báo. **Một số danh sách chỉ trả mảng, không có tổng số**: `/enrollments`, `/orders` (`data.orders`), `/courses/complaints`, `/courses/{id}/reviews`. Với các danh sách này, phân trang kiểu "tải thêm" (hết khi trang trả ít hơn `pageSize`).
- Danh sách rỗng là `200` với mảng rỗng (đơn hàng, lớp, môn, bài học mẫu...), không còn `404`.

## 2. Đăng nhập, token, làm mới

- `POST /api/auth/login` → `data.accessToken` (JWT, mặc định 60 phút) + `data.refreshToken` (7 ngày, **dùng một lần**: mỗi lần refresh trả cặp mới, token cũ vô hiệu).
- Gửi `Authorization: Bearer <accessToken>`. (API còn đọc cookie `access_token`, nhưng không có gì set cookie này; **đừng** lưu JWT vào cookie, vì sẽ mở đường CSRF với các endpoint `multipart/form-data`.)
- Khi nhận `401`: gọi `POST /api/auth/refresh-token` `{ refreshToken }` rồi thử lại request một lần. **Phải gộp (single-flight) các lần refresh đồng thời**: hai tab/hai request cùng gửi một refresh token thì lần thứ hai bị `401 "Invalid refresh token"` và người dùng bị đăng xuất. Lưu cặp token mới ngay khi nhận (và chia sẻ giữa các tab, ví dụ `BroadcastChannel`).
- Nếu refresh token **dùng lại** sau hơn 10 giây thì server coi là bị đánh cắp và đăng xuất mọi thiết bị (`401 "Refresh token was already used"`).
- `401` + `"Your permissions have changed..."` (admin đổi role) hoặc `"Your session has ended..."` (đổi/đặt lại mật khẩu, đăng xuất mọi nơi): refresh một lần để lấy token mới; nếu refresh cũng `401` thì về trang đăng nhập.
- `POST /api/auth/logout` (có `refreshToken` trong body: chỉ thiết bị này; body rỗng: mọi thiết bị, và các kết nối SignalR `/authHub` nhận sự kiện `ForceLogout`).
- Đăng ký → email có mã 6 số (5 phút) → `POST /api/auth/verify-email` → mới đăng nhập được. Chưa xác thực thì login trả `401 Invalid credentials`.
- **Email không phân biệt hoa/thường và được cắt khoảng trắng** (server chuẩn hoá về chữ thường). Tài khoản bị admin khoá: login trả `403`.
- Số điện thoại: 10-11 chữ số, duy nhất toàn hệ thống (cả khi cập nhật hồ sơ: `409` nếu đã có người dùng).
- `403`/`409`/`429` có `detail` rõ ràng; `429` kèm header `Retry-After`. Rate limit theo IP (chưa đăng nhập) hoặc theo user.

## 3. SignalR

- `/courseHub` (thông báo `Notification`, sự kiện `CourseReviewed` gửi cho mọi kết nối) và `/authHub` (`ForceLogout`). Trình duyệt không gắn được header cho WebSocket, nên truyền token qua query: `/courseHub?access_token=<jwt>`.
- Cần CORS: thêm origin của frontend vào `CorsSettings:AllowedOrigins` (bắt buộc ở Production).

## 4. Khoá học và học tập

- **Giá**: số nguyên VND (không có phần lẻ). `price = 0` hoặc bỏ trống là miễn phí. Khoá miễn phí/giảm hết giá được ghi danh ngay, `requiresPayment=false`.
- **Teacher gọi `GET /api/courses` chỉ thấy khoá của chính mình** (mọi trạng thái); Admin thấy tất cả; khách/học viên chỉ thấy `Published`. Lọc `?status=InReview|Published|Rejected` (chỉ Teacher/Admin).
- `course.teacher` chỉ có `userID`, `name`, `bio` (không còn email/SĐT). Admin cần thông tin liên hệ thì gọi `GET /api/user/{id}`.
- `GET /api/courses/{id}` cho khách/học viên: **không có `chapters`**; nội dung bài học chỉ có ở `GET /api/enrollments/{id}` (học viên đã mua) hoặc ở endpoint này cho Admin/giáo viên chủ khoá.
- `thumbnailName`: nếu lưu local là đường dẫn tương đối (`2026/05/x.jpg`, ghép với `{API}/media/`); nếu dùng Cloudinary là URL tuyệt đối.
- `videoUrl` lưu local là đường dẫn tương đối (`videos/<id>.mp4?exp=...&sig=...`, ghép với `{API}/media/`) và được đổi thành link có chữ ký, hạn dùng (mặc định 240 phút). **Đừng cache `videoUrl`**: khi `403` ở `/media/videos/...` thì lấy lại enrollment/khoá học để có link mới. Video ngoài (https, YouTube...) giữ nguyên.
- **Upload video theo chunk**: `POST /api/courses/upload-chunk` (multipart: `Chunk`, `UploadId` = một GUID do client sinh, `Index` từ 0) cho từng mảnh (≤ 32MB, tên file tuỳ ý), rồi `POST /api/courses/complete-upload` (`UploadId`, `Extension`: mp4/mov/webm/mkv). Mảnh `0` phải là phần đầu file (server kiểm tra chữ ký video khi ghép). Kết quả `data.url` điền vào `videoUrl` của bài học khi tạo khoá.
- **Tạo khoá** `POST /api/courses` là `multipart/form-data` (`Chapters[0].Lessons[0].Quizzes[0].Answer.CorrectAnswers[0]=...`), không phải JSON. Thumbnail: file ảnh `.jpg/.jpeg/.png` (`ThumbnailFile`) *hoặc* `ThumbnailName`.
- Quiz: `Answer.Type` `1` = một đáp án, `2` = nhiều đáp án, `3` = đúng/sai (chỉ cần `TrueOrFalse`, không `CorrectAnswers`/`Options`). Với loại 1 và 2, **mọi đáp án đúng phải nằm trong `Options`**.
- Khoá mới ở `InReview` → Admin duyệt (`POST /api/courses/review`) → `Published`/`Rejected`. Hiện **chưa có endpoint sửa/xoá/nộp lại khoá** (giáo viên tạo khoá mới).

### Tiến độ học

- `POST /api/enrollments/progress/lesson` `{ enrollmentID, chapterID, lessonID, isCompleted }`: đánh dấu hoàn thành bài (`isCompleted=false` chỉ ghi nhận đã mở bài, không hoàn thành).
- `POST /api/enrollments/progress/quiz` `{ enrollmentID, chapterID, lessonID, quizID, selectedAnswers: [..] }` → `{ isCorrect, explanation }`. Bài có quiz hoàn thành khi **mọi** quiz của bài đều đúng.
- `completionRate` tính trên **toàn bộ bài học của khoá** (hoàn thành 1/10 bài = 10%). `chapterID/lessonID/quizID` phải thuộc khoá của enrollment, sai thì `404`.
- `GET /api/enrollments/{id}`: câu hỏi và `options` có, **đáp án đúng bị ẩn** (`correctAnswers` rỗng) và `note` (giải thích) chỉ hiện với quiz đã làm.
- Khi hoàn thành bài cuối: `enrollment.status = 2 (Completed)` và `completedAt` có giá trị.

## 5. Thanh toán (PayOS)

- `POST /api/orders` `{ courseId, selectedCouponIds: [] }` (**chỉ Student**; khoá phải `Published`) → `{ checkoutUrl, requiresPayment }`. Mở `checkoutUrl` (toàn trang). Sau thanh toán PayOS gọi `GET /api/orders/return` rồi server chuyển hướng về `{FrontendUrl}/student?payment=success|cancelled`. Trang đó nên gọi lại `GET /api/enrollments` (webhook có thể đến chậm vài giây).
- Gọi lại `POST /api/orders` cho cùng khoá khi đơn cũ còn hạn (15 phút) trả lại đúng link cũ.
- **Tên trạng thái đơn dễ gây hiểu nhầm**: `OrderStatus` `1 = Created` (chờ thanh toán), `2 = Pending` (**đã thanh toán**), `3 = Cancelled`.
- Danh sách đơn có `courseTitle`; `checkoutUrl` luôn `null` trong danh sách. `GET /api/orders` rỗng là `data.orders = []`.
- Coupon của tôi: `GET /api/orders/coupons`; coupon công khai: `GET /api/coupons/available`.

## 6. Thống kê (Admin / Teacher)

- Tham số ngày `from`/`to` là `DateTime`: `to=2026-10-07` nghĩa là **00:00** ngày đó, tức loại trừ ngày 07. Muốn gồm cả ngày thì gửi `to=2026-10-07T23:59:59`.
- `from > to`, enum ngoài phạm vi, `top` ngoài 1..100 trả `400`.
- Kết quả được cache phía server 2 phút.

## 7. Cache công khai

`GET` công khai (khoá học, lớp, môn, coupon, bài học mẫu) được cache 30 giây **chỉ cho khách chưa đăng nhập**; request có `Authorization` không bị cache. Khoá mới duyệt có thể chậm tối đa 30 giây mới hiện ra cho khách.
