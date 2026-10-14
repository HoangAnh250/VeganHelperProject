# Luồng chỉnh sửa code và phát hành Vegan Helper System

## 1. Quy ước chung

| Repository | Nhánh tích hợp hằng ngày | Nhánh phát hành | Công nghệ |
|---|---|---|---|
| BE — VeganHelperProject | `developer` | `main` | ASP.NET Core .NET 10, PostgreSQL/Supabase |
| FE — frontend-VeganHelperSystem | `dev` | `main` | React 18, Vite, Tailwind CSS 3 |

Luồng BE: `feature/...` → `developer` → kiểm thử → `main` → deploy BE.

Luồng FE: `feature/...` → `dev` → kiểm thử → `main` → deploy FE.

`main` lưu bản được nhóm chọn phát hành. Không sửa tính năng trực tiếp trên `main`. Sửa trên nhánh riêng, review rồi tích hợp. Việc merge local chỉ thay đổi Git trên máy; push mới đưa commit lên GitHub. Deploy chỉ diễn ra khi có workflow/kết nối Azure được cấu hình và thực thi thành công.

Hai repo có lịch sử Git riêng: phải đứng đúng thư mục và xác nhận nhánh trước mọi commit, merge hoặc push. Không commit/push thay cho thành viên khác khi chưa được họ đồng ý; không force-push các nhánh dùng chung.

## 2. Làm việc với BE

### Bắt đầu tính năng hoặc sửa lỗi

```powershell
cd "D:\Study\tài liệu\RUSH\SWP391\VeganHelperProject"
git status
git switch developer
git pull --ff-only origin developer
git switch -c feature/<ten-chuc-nang>
```

Chỉ switch/pull khi working tree sạch hoặc thay đổi đã được lưu có chủ đích. Nếu pull báo nhánh phân kỳ, fetch và xem lịch sử, rồi merge sau khi review; không dùng reset để bỏ code local.

Giữ kiến trúc Controller → Service/BLL → Repository/DAL. Dùng DTO cho request/response, kiểm tra quyền tại API và giữ các quy tắc nghiệp vụ. Không đưa entity trực tiếp thành hợp đồng API.

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

Sau khi sửa BE, khởi động lại tiến trình để áp dụng code mới.

### Kiểm thử và tích hợp

```powershell
dotnet build VeganHelper.sln -c Release
dotnet test tests/VeganHelper.UnitTests/VeganHelper.UnitTests.csproj -c Release
```

Integration test cần Docker/Testcontainers hoặc PostgreSQL local riêng. Các fixture nhận `VEGANHELPER_MEMBER2_TEST_POSTGRES` và `VEGANHELPER_TEST_POSTGRES`; chỉ dùng database local với tên kiểm thử hợp lệ, không dùng Supabase chung. Xem `docs/FN36_FN37_Notifications.md` để cấu hình PostgreSQL test.

Khi môi trường test đã sẵn sàng:

```powershell
dotnet test VeganHelper.sln -c Release
```

Chạy thêm kiểm tra Swagger/API liên quan và đối chiếu FE nếu thay hợp đồng API. Stage đúng các file đã review; không stage `.env`, `supabase.local.json`, appsettings local, dữ liệu xuất hoặc khóa bí mật. Commit với thông điệp cụ thể, push nhánh tính năng khi được phép, rồi mở Pull Request vào `developer`.

### Phát hành BE

Khi `developer` đã ổn định, nhóm review Pull Request **`developer` → `main`**, chạy build/test trên kết quả merge và chỉ phát hành khi các check bắt buộc đạt.

Nếu nhóm thực hiện merge local thay vì PR, dùng working tree sạch:

```powershell
git fetch origin
git switch main
git merge --ff-only origin/main
git merge --no-ff developer
dotnet build VeganHelper.sln -c Release
dotnet test VeganHelper.sln -c Release
```

Nếu có conflict, giải quyết và review trước khi hoàn tất merge. Nếu test thất bại, sửa trước khi push. Khi được phép phát hành và repo cho phép push trực tiếp:

```powershell
git push origin main
git switch developer
```

Nếu `main` được bảo vệ, dùng PR và tuân thủ check/review; không tìm cách bỏ qua bảo vệ. Sau PR có sửa conflict hoặc commit bổ sung trên `main`, merge `origin/main` về `developer` để hai nhánh không bỏ sót bản sửa phát hành.

## 3. Làm việc với FE

### Bắt đầu tính năng hoặc sửa lỗi

```powershell
cd "D:\Study\tài liệu\RUSH\SWP391\frontend-VeganHelperSystem"
git status
git switch dev
git pull --ff-only origin dev
git switch -c feature/<ten-chuc-nang>
npm ci
npm start
```

Dùng Node 24.15+ trong dòng Node 24 (hoặc Node 22.22.2+ trong dòng Node 22), phù hợp `engines` của dự án. `npm start` và `npm run dev` đều chạy Vite tại `http://localhost:3000`. Dừng server cũ nếu cổng 3000 đang bị chiếm.

Giữ cấu trúc `pages`, `components`, `services`, `utils`; tái sử dụng component và bảng màu hiện có. Đối chiếu DTO/API thực tế khi nối dữ liệu; không suy đoán trường response.

### Kiểm thử và tích hợp

```powershell
npm test
npm run build
npm run preview
```

Vitest chạy bộ test FE; build xuất vào `build/`. Preview chỉ phục vụ kiểm tra local. Kiểm tra các trang vừa sửa, màn hình mobile, đường dẫn mở trực tiếp, trang 404 và các luồng cần BE. Test tự động/mocks không thay thế kiểm tra tích hợp online.

Review file thay đổi, commit và push nhánh tính năng khi được phép. Mở Pull Request vào **`dev`**. Cập nhật `package-lock.json` khi thêm/đổi dependency để các máy khác cài cùng bộ phiên bản bằng `npm ci`.

### Phát hành FE

Nhóm review Pull Request **`dev` → `main`** và chạy lại test/build trên kết quả merge. Với merge local, dùng working tree sạch:

```powershell
git fetch origin
git switch main
git merge --ff-only origin/main
git merge --no-ff dev
npm ci
npm test
npm run build
```

Khi được phép phát hành và repo cho phép push trực tiếp:

```powershell
git push origin main
git switch dev
```

Repo bảo vệ `main` phải dùng PR. Nếu có sửa bổ sung trên `main`, đưa `origin/main` về `dev` để các lần phát hành sau giữ bản sửa đó.

## 4. Chuẩn bị Azure và cấu hình môi trường

Mục tiêu: FE trên Azure Static Web Apps; BE trên Azure App Service; database Supabase và media Cloudflare R2 giữ nguyên. Đây là hướng triển khai, không có nghĩa hosting/workflow đã được tạo.

- Workflow BE theo dõi `main`, dùng .NET 10, build/test và publish `src/VeganHelper.API/VeganHelper.API.csproj`; chỉ deploy khi các bước bắt buộc thành công.
- Workflow FE theo dõi `main`, dùng Node 24, `npm ci`, test và build; deploy thư mục `build`. Nếu tự build trước, cấu hình Static Web Apps upload thư mục này với `skip_app_build: true`.
- FE production cần `VITE_API_BASE_URL=https://<BE-host>/api`, `VITE_GOOGLE_CLIENT_ID` và `VITE_GOONG_MAPTILES_KEY` tại **bước build**. Đổi giá trị cần build/deploy lại. `.env` trên máy cá nhân không tự xuất hiện trên GitHub runner.
- Các biến `VITE_*` công khai trong trình duyệt. Không đặt Gemini API key, JWT signing key hoặc mật khẩu PostgreSQL tại FE.
- BE dùng Environment variables/Key Vault trên Azure cho connection string, JWT, Gemini, SendGrid, R2 và Web Push; cấu hình lồng nhau dùng `__`, ví dụ `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey`. Giữ signing key cố định qua lần deploy/restart.
- BE production cần HTTPS, CORS cho đúng origin FE, cấu hình reverse proxy phù hợp và Always On để các worker thông báo/AI không bị dừng vì ít truy cập. Cập nhật Google OAuth origin và hạn chế domain Goong nếu đang dùng.
- FE cần `public/staticwebapp.config.json` với navigation fallback về `/index.html` để React Router xử lý URL trực tiếp và trang 404. File phải có mặt trong `build/` khi deploy.
- BE có `/health/live`; endpoint này chỉ xác nhận tiến trình còn hoạt động, không bảo đảm Supabase/AI/email hoạt động. Swagger hiện chỉ bật trong Development.

## 5. Database và thứ tự phát hành

Deploy code không đồng nghĩa áp dụng migration. Review migration riêng, sao lưu/phương án khôi phục và xin phép trước khi sửa database chung. Không sửa migration đã được áp dụng để cố đổi schema; thêm migration mới.

Hiện các máy local cùng dùng Supabase nên thao tác ghi, seed hoặc migration từ local có thể ảnh hưởng dữ liệu website. Tránh seed lại khi khởi động hosting. Khi hosting hoạt động, cấu hình các worker local phù hợp để không vô tình cùng xử lý công việc production; môi trường phát triển và production nên có database riêng khi cần tách dữ liệu.

Khi thêm API/field tương thích: triển khai migration bổ sung (nếu cần) → BE tương thích cả FE cũ/mới → FE mới. Tránh xóa/đổi field ngay khi FE cũ còn chạy. Thay đổi phá vỡ hợp đồng phải lên kế hoạch phối hợp cả hai repo.

Sau deploy, kiểm tra đăng nhập, phân quyền, ảnh, tạo/sửa bài, meal plan và thông báo/AI liên quan. Kiểm tra workflow/deployment logs và ghi lại commit đã phát hành của cả hai repo.

## 6. Sửa lỗi trên website và khôi phục

Tạo `hotfix/<ten-loi>` từ `main` mới nhất; sửa, test và review rồi merge lại `main`. Đồng bộ bản sửa về `developer` (BE) hoặc `dev` (FE). Dùng quy trình branch protection hiện có.

Nếu bản mới lỗi, redeploy artifact/commit ổn định trước đó hoặc revert commit phát hành qua PR. Không force-push/reset nhánh chung. Rollback code không tự rollback database; cần kiểm tra phiên bản cũ còn tương thích schema và không chạy migration Down một cách tùy tiện.

## 7. Trạng thái của lần chuẩn bị này

Tài liệu mô tả quy trình làm việc và phát hành. Merge local, push GitHub và deploy Azure là ba thao tác riêng. Hãy xác nhận trạng thái thực tế trong Git/GitHub/Azure; không xem việc có file hướng dẫn là bằng chứng website đã được triển khai.
