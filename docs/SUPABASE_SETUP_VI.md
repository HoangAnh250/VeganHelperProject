# Chạy Vegan Helper với database Supabase dùng chung

Backend vẫn giữ kiến trúc API → BLL (Services/DTOs) → DAL (Repositories/EF Core). EF Core dùng Npgsql/PostgreSQL thay cho SQL Server. Frontend React tiếp tục gọi .NET API; JWT và các tài khoản hiện tại vẫn do backend quản lý.

## 1. Project và connection string

Project đã dùng để chuyển dữ liệu: `brjltkherhhqqkryhsuq`. Các máy trong nhóm phải kết nối **cùng project và database `postgres`**.

Nếu tạo project khác: mở Supabase Dashboard → New project, chọn organization, đặt tên, mật khẩu database và region; đợi project sẵn sàng. Không tạo project riêng cho từng thành viên nếu muốn dùng chung dữ liệu.

Trong project, bấm **Connect → Session pooler** và sao chép URI. Kết nối này dùng cổng **5432** và username dạng `postgres.PROJECT_REF`, phù hợp backend chạy lâu dài trên mạng IPv4. Lấy hostname từ Dashboard, không tự suy ra từ region. Xem [hướng dẫn kết nối chính thức](https://supabase.com/docs/guides/database/connecting-to-postgres).

Ở thư mục gốc BE, tạo cấu hình riêng của máy:

```powershell
Copy-Item -LiteralPath supabase.example.json -Destination supabase.local.json
```

Chỉ chạy lệnh sao chép khi chưa có file; máy hiện tại đã được cấu hình. Thay `ConnectionStrings.DefaultConnection` trong file mới bằng URI thực tế có mật khẩu database, ví dụ:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "postgresql://postgres.PROJECT_REF:YOUR_PASSWORD@YOUR_POOLER_HOST:5432/postgres"
  }
}
```

Percent-encode các ký tự đặc biệt trong mật khẩu khi dùng URI. Có thể dùng định dạng Npgsql `Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=VerifyFull` thay thế. Connection string database khác với publishable/anon key của Supabase.

`supabase.local.json` và `.local-data/` đã được Git bỏ qua. Mỗi thành viên lấy thông tin kết nối qua kênh riêng và tạo file trên máy mình. Không đặt mật khẩu database vào React hay commit file này. `ConnectionStrings__DefaultConnection` trong environment sẽ ghi đè cấu hình file; xóa hoặc cập nhật biến cũ nếu còn trỏ SQL Server.

Backend bắt buộc xác thực TLS cho hostname Supabase; public CA trong `docs/certificates/supabase-root-2021.crt` được copy cùng output khi build/publish. Nếu Supabase đổi CA, cập nhật chứng chỉ theo [tài liệu SSL](https://supabase.com/docs/guides/platform/ssl-enforcement), không tắt kiểm tra chứng chỉ.

## 2. Chạy backend trên từng máy

Yêu cầu .NET SDK 10. Chạy các lệnh từ thư mục gốc BE. Thay đổi hiện nằm trong checkout:

`C:\Users\ADMIN\.codex\worktrees\4ae8\VeganHelperProject`

```powershell
dotnet restore
dotnet run --project tools/VeganHelper.DatabaseMigration -- check
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

Giữ cấu hình JWT, email, Google và R2 hiện có trong user-secrets/environment. Khi cài máy mới, cấu hình các mục `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (ít nhất 32 ký tự) bằng user-secrets của project API. Không chia sẻ signing key trong Git. Development có thể tạo signing key tạm nếu chưa đặt key, nhưng mỗi lần restart sẽ làm token cũ mất hiệu lực; các BE cùng xác thực một token cần cùng issuer/audience/key.

Swagger: `https://localhost:7180/swagger`. Kiểm tra database ở `https://localhost:7180/api/status` trong Development; kiểm tra tiến trình ở `/health/live`.

Sau thay đổi BE, dừng terminal `dotnet run` cũ rồi chạy lại để biên dịch và áp dụng code. FE giữ cấu hình URL gọi BE hiện tại và chạy `npm start`. Dữ liệu phát sinh từ bất kỳ BE nào sẽ nằm trong cùng Supabase.

## 3. Migration và seed là thao tác chủ động

API không tự migrate hoặc seed mỗi lần khởi động. Database dùng chung hiện đã được migrate/import; thành viên chỉ cần kết nối và chạy BE.

Khi có migration mới, một người phụ trách chạy:

```powershell
dotnet run --project src/VeganHelper.API -- --migrate
```

Seed category và shop:

```powershell
dotnet run --project src/VeganHelper.API -- --seed
```

Seed thêm các bài demo có sẵn trong dự án:

```powershell
dotnet run --project src/VeganHelper.API -- --seed-posts
```

Category thêm theo slug còn thiếu; shop chỉ seed khi bảng trống. Post seed bỏ qua bài đã tồn tại của tác giả seed theo tiêu đề, không ghi đè bài đã chỉnh sửa. Lệnh seed dùng transaction và advisory lock để tránh hai máy seed đồng thời. Endpoint HTTP seed cũ trả `410 Gone`.

Các migration SQL Server cũ ở `src/VeganHelper.DAL/Migrations` chỉ được lưu làm lịch sử và không compile. Migration PostgreSQL nằm trong `src/VeganHelper.DAL/PostgresMigrations`; dùng thư mục này cho migration mới.

## 4. Công cụ chuyển toàn bộ dữ liệu

Phần này chỉ dùng khi chuyển sang **database đích mới trống**. Không import lại database đang được nhóm sử dụng; công cụ sẽ từ chối để bảo vệ dữ liệu.

Đặt SQL Server nguồn trong `Migration.SourceConnectionString` của file local. Dừng các BE ghi vào nguồn trong lúc export; công cụ kiểm tra schema, xuất tất cả 32 bảng mapped, ID, quan hệ, password hash và token. Nó không cập nhật SQL Server.

```powershell
dotnet run --project tools/VeganHelper.DatabaseMigration -- export
dotnet run --project tools/VeganHelper.DatabaseMigration -- import
dotnet run --project tools/VeganHelper.DatabaseMigration -- verify
```

Mặc định snapshot là `.local-data/sqlserver-export.json`; có thể truyền đường dẫn snapshot sau tên lệnh. Export không ghi đè file backup đã tồn tại. Snapshot chứa dữ liệu riêng tư và password hash/token; giữ riêng trên máy, không đưa lên Git.

Import tạo schema PostgreSQL, kiểm tra bảng đích trống (cho phép hai role bootstrap), nạp dữ liệu trong một transaction, kiểm tra foreign key và từng giá trị, reset identity sequence rồi commit. Sai schema, dữ liệu hay constraint sẽ dừng và rollback dữ liệu import.

Ngay sau import, `verify` yêu cầu dữ liệu khớp hoàn toàn snapshot. Sau khi thêm seed, có thể kiểm tra toàn bộ bản ghi gốc vẫn nguyên vẹn bằng:

```powershell
dotnet run --project tools/VeganHelper.DatabaseMigration -- verify-preserved
```

Lệnh này cho phép các bản ghi mới nhưng vẫn yêu cầu mọi ID và giá trị gốc tồn tại, không thay đổi. Sau khi người dùng sửa dữ liệu gốc một cách hợp lệ, kiểm tra với snapshot cũ có thể báo khác biệt; đây không phải lỗi kết nối.

SQL Server `datetime2` đã được hiểu là UTC theo cách BE tạo dữ liệu; PostgreSQL lưu timestamp đến microsecond, nên phần nhỏ hơn 1 microsecond được cắt khi đối chiếu. Ngày thuần vẫn lưu dạng `date`. Bản export giữ độ chính xác gốc.

## 5. Quyền truy cập và file ảnh

Migration bật RLS và thu hồi quyền trực tiếp của `PUBLIC`, `anon`, `authenticated` trên 32 bảng ứng dụng. Backend truy cập bằng tài khoản database, kiểm soát quyền qua JWT/.NET API. Không dùng Supabase Data API trực tiếp từ React cho các bảng này; chưa chuyển sang Supabase Auth/Storage.

URL media/profile đã được chuyển cùng dữ liệu. File avatar có URL `/uploads/...` vẫn nằm trên máy BE, không tự được đưa vào database. Để avatar đó xuất hiện trên máy khác, sao chép file tương ứng hoặc chuyển avatar sang storage dùng chung; ảnh bài viết đã có URL ngoài tiếp tục dùng URL đó.

## 6. Kiểm thử

```powershell
dotnet test tests/VeganHelper.UnitTests
dotnet test tests/VeganHelper.IntegrationTests
```

Integration tests mặc định dùng PostgreSQL 17 Testcontainers (cần Docker). Có thể đặt `VEGANHELPER_TEST_POSTGRES` bằng connection string một database test đã migrate. Các fixture ghi trong transaction rồi rollback, shop seed được thử bằng bảng tạm riêng; sequence PostgreSQL có thể tăng dù transaction rollback. Ưu tiên database test riêng khi nhóm đang ghi dữ liệu đồng thời.
