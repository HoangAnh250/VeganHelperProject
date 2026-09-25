# Yêu cầu kỹ thuật & Prompt Template: Sprint 1 (Xác thực & Tài khoản)

Tài liệu này dùng để cung cấp ngữ cảnh (context) và prompt cho các công cụ AI (như Codex, ChatGPT, Claude) nhằm sinh code Backend cho ASP.NET Core Web API theo kiến trúc 3-Layer.

## 1. Yêu cầu chung (General Requirements)
- **Framework**: ASP.NET Core Web API (.NET 8/9/10).
- **Architecture**: 3-Layer (Controllers, Services, Repositories).
- **Database**: Entity Framework Core (Code-First) với SQL Server.
- **Authentication**: JWT (JSON Web Token).
- **Validation**: FluentValidation hoặc DataAnnotations.

## 2. Danh sách chức năng (Sprint 1)
| Mã | Tên chức năng | Yêu cầu kỹ thuật chi tiết |
|:---|:---|:---|
| **FN01** | Đăng ký tài khoản | Validate email RFC, mật khẩu 8+ ký tự, sinh OTP kích hoạt. |
| **FN02** | Đăng nhập hệ thống | Cấp JWT token (Access + Refresh), khóa tài khoản 15p nếu sai 5 lần. |
| **FN03** | Đăng xuất | Thu hồi/Blacklist token. |
| **FN04** | Quên / Đặt lại mật khẩu | Gửi link reset qua email (hết hạn 15p, dùng 1 lần). |
| **FN05** | Xem hồ sơ cá nhân | Authorize, ẩn thông tin nhạy cảm (password hash, token). |
| **FN06** | Chỉnh sửa hồ sơ cá nhân | Không đổi email, avatar <= 5MB, validate SĐT VN. |
| **FN07** | Route guard / Phân quyền | Chặn truy cập API khi chưa đăng nhập (Return 401/403). |

---

## 3. Prompt Templates (Dùng để copy-paste cho AI)

### 🚀 Lần chat 1: Khởi tạo Entity & Database Context
**Mục đích**: Yêu cầu AI tạo bảng User và chuẩn bị cho EF Core.

**Prompt:**
> "Tôi đang xây dựng một dự án ASP.NET Core Web API dùng Entity Framework Core và kiến trúc 3-Layer (Repository Pattern).
> Hãy viết cho tôi Entity `User` bao gồm các trường cần thiết để phục vụ: Đăng nhập bằng Email/Password, quản lý trạng thái khóa tài khoản (lockout), lưu trữ OTP kích hoạt, Refresh Token, Avatar URL và số điện thoại. 
> Đồng thời tạo cấu hình `AppDbContext` và Fluent API (nếu cần) để thiết lập ràng buộc: Email là unique, Avatar URL có thể null."

### 🚀 Lần chat 2: Xây dựng FN01 (Đăng ký) & DTOs
**Mục đích**: Tạo luồng đăng ký với các rule validate nghiêm ngặt.

**Prompt:**
> "Dựa trên Entity User vừa tạo, hãy viết cho tôi luồng Đăng ký tài khoản (Register) theo chuẩn 3-Layer. 
> 1. Tạo `RegisterDto`.
> 2. Cấu hình validation cho DTO này: Email phải đúng chuẩn RFC, Mật khẩu tối thiểu 8 ký tự (có số, chữ, ký tự đặc biệt).
> 3. Viết Interface và Implement cho `IAuthRepository` và `IAuthService`.
> 4. Trong Service, thực hiện băm mật khẩu (dùng BCrypt hoặc tính năng có sẵn của ASP.NET Identity), tạo mã OTP ngẫu nhiên 6 số và lưu vào DB (giả lập việc gửi email OTP).
> 5. Viết `AuthController` để gọi service này."

### 🚀 Lần chat 3: Xây dựng FN02 (Đăng nhập & Lockout)
**Mục đích**: Cấp JWT và xử lý khóa tài khoản.

**Prompt:**
> "Bây giờ hãy viết luồng Đăng nhập (Login) cho `AuthController` và `AuthService`. 
> Yêu cầu:
> 1. Nếu người dùng nhập sai mật khẩu 5 lần, khóa tài khoản trong vòng 15 phút. Lưu trạng thái này vào database.
> 2. Nếu đăng nhập thành công, reset bộ đếm sai mật khẩu.
> 3. Sinh ra 2 token: JWT Access Token (hết hạn trong 30 phút) và Refresh Token (hết hạn trong 7 ngày). Refresh token phải được lưu vào cơ sở dữ liệu.
> Trả về code chi tiết cho DTO, Service, và cách sinh JWT token."

### 🚀 Lần chat 4: Xây dựng FN04 (Quên mật khẩu)
**Mục đích**: Tạo cơ chế reset password an toàn.

**Prompt:**
> "Tôi cần chức năng Quên mật khẩu. Hãy viết 2 endpoint trong `AuthController` và logic trong Service:
> 1. `ForgotPassword(string email)`: Tạo ra một mã token ngẫu nhiên (hoặc mã code) dùng 1 lần, lưu vào CSDL với thời gian hết hạn là 15 phút.
> 2. `ResetPassword(ResetPasswordDto)`: Nhận vào email, mã token và mật khẩu mới. Kiểm tra xem token có hợp lệ không, có bị quá hạn không. Nếu hợp lệ thì cập nhật mật khẩu và hủy token đó (để chỉ dùng được 1 lần)."

### 🚀 Lần chat 5: Xây dựng FN05 & FN06 (Quản lý Profile)
**Mục đích**: Xem và cập nhật thông tin cá nhân với Validation.

**Prompt:**
> "Hãy tạo `UserController` và `UserService` để xử lý hồ sơ cá nhân.
> 1. Endpoint `GetProfile`: Phải yêu cầu xác thực `[Authorize]`. Lấy ID người dùng từ Claims của JWT token. Trả về `UserProfileDto` (chú ý KHÔNG trả về password hash hay refresh token).
> 2. Endpoint `UpdateProfile(UpdateProfileDto)`: Yêu cầu xác thực.
>    - Không cho phép cập nhật Email.
>    - Số điện thoại phải đúng định dạng số điện thoại Việt Nam (ví dụ bắt đầu bằng 03, 09, 08... và có 10 số).
>    - Nếu có upload Avatar, kiểm tra kích thước file phải <= 5MB, và chỉ nhận ảnh (jpg, png). (Gợi ý dùng `IFormFile`)."

### 🚀 Lần chat 6: Xây dựng FN03 & FN07 (Bảo mật & Đăng xuất)
**Mục đích**: Xử lý đăng xuất và cấu hình bảo mật.

**Prompt:**
> "Cuối cùng, hãy viết chức năng Đăng xuất và cấu hình Route Guard.
> 1. `Logout`: Nhận vào access token hoặc lấy từ header. Xóa hoặc vô hiệu hóa Refresh Token tương ứng trong CSDL.
> 2. Hướng dẫn tôi cách đăng ký cấu hình Authentication JWT trong `Program.cs` để các endpoint có `[Authorize]` tự động trả về lỗi 401 (Unauthorized) nếu chưa đăng nhập, hoặc 403 (Forbidden) nếu sai quyền."
