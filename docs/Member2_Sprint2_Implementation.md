# Member 2 — Sprint 2

Đã triển khai FN22–FN25 và FN27–FN29 trong backend .NET 10/PostgreSQL. FN26 tạo thực đơn tuần làm sau. Luồng mới đi qua Controller → Service/DTO/AutoMapper → Repository. FE có thể nối API theo `Sprint2_API_Contracts.json`.

## API dùng cho FE

Tất cả route bên dưới yêu cầu `Authorization: Bearer <access token>` của hệ thống VeganHelper. Không dùng Supabase anon key thay JWT. Các route cá nhân lấy UserId từ token, không nhận UserId trong body/query.

| FN | Method và route | Kết quả |
| --- | --- | --- |
| FN22 | GET `/api/HealthProfile` | Hồ sơ và lựa chọn dị ứng hiện tại |
| FN22 | PUT `/api/HealthProfile` | Lưu sức khỏe, BMI, TDEE và một điểm lịch sử |
| FN23 | PUT `/api/HealthProfile/allergies` | Thay toàn bộ danh sách dị ứng |
| FN23 | GET `/api/HealthProfile/ingredients` | Danh sách nguyên liệu có phân trang cho dropdown |
| FN23 | GET `/api/posts/{postId}/allergy-warnings` | Cảnh báo nguyên liệu khớp dị ứng tài khoản |
| FN24 | GET `/api/HealthProfile/bmi` | BMI, phân loại, khoảng cân nặng tham chiếu, calo và gợi ý dinh dưỡng |
| FN25 | GET `/api/HealthProfile/bmi-history` | Lịch sử 6 tháng gần nhất của tài khoản |
| FN27 | GET `/api/Shops/nearby` | Quán đã duyệt trong bán kính 5–10 km |
| FN28 | GET `/api/Shops/search` | Tìm theo tên quán, địa chỉ, món ăn |
| FN29 | GET `/api/Shops/{id}` | Thông tin quán, ảnh, menu, giờ mở cửa, link Google Maps |

Danh sách dùng `PagedResult<T>`: `items`, `totalItems`, `totalCount`, `pageIndex`, `pageNumber`, `pageSize`, `totalPages`. `pageNumber` là alias của `pageIndex`; request vẫn dùng `pageIndex` để tương thích code hiện có. `pageIndex` từ 1–1.000.000, `pageSize` từ 1–100. Mặc định shops/ingredients là 20; history là 100. History giữ `history` làm alias của `items`.

Ví dụ body sức khỏe:

```json
{
  "heightCm": 175,
  "weightKg": 70,
  "biologicalSex": "male",
  "birthDate": "1996-01-01",
  "dietType": "vegan",
  "activityLevel": "sedentary"
}
```

Giới hạn cao 100–250 cm, nặng 30–200 kg. `biologicalSex`: `male`, `female`, `other`; `dietType`: `vegan`, `lacto_ovo_vegetarian`; `activityLevel`: `sedentary`, `light`, `moderate`, `active`, `very_active`. Enum được trim/lowercase. Ngày sinh không ở tương lai và trong 120 năm gần nhất.

Ví dụ body dị ứng:

```json
{
  "allergyIngredientIds": [1, 2],
  "customAllergies": ["Nguyên liệu nhập tay"]
}
```

ID phải có trong danh sách ingredients. Tối đa 100 ID và 50 tên trước deduplicate; tên trim 1–100 ký tự. `null`/tên trắng không hợp lệ; hai mảng rỗng xóa lựa chọn. Cập nhật được transaction bảo vệ, kiểm tra toàn bộ ID trước thay thế; lỗi DB rollback cả nguyên liệu mới lẫn lựa chọn. Các request tạo cùng tên nhập tay được đồng bộ bằng PostgreSQL advisory lock. Tên nhập tay khớp tên ingredient hiện có sẽ tái sử dụng ID. `isCustom` ghi nhận cách chọn cho từng tài khoản, không phân loại ingredient toàn hệ thống.

FE gọi `allergy-warnings` khi mở chi tiết bài rồi hiển thị `allergens` nếu `hasAllergyWarning=true`. Cảnh báo đối chiếu **ingredient ID**; chưa ánh xạ nhóm dị ứng/synonym hay nhiễm chéo. Không khớp dữ liệu không phải kết luận món ăn an toàn. Bài đã xóa hoặc bài chưa published của người khác trả 404.

## BMI và lịch sử

BMI làm tròn 2 số, dùng ngưỡng tham chiếu người lớn 18,5 / 25 / 30 cho bốn nhóm. Khoảng cân nặng tham chiếu lấy BMI 18,5–24,9. Khi biết tài khoản dưới 18 tuổi, vẫn lưu/tính BMI nhưng `category`, `bmiCategory`, `idealWeightRange` và TDEE trả `null`, vì phân loại trẻ em cần tham chiếu khác. Hồ sơ cũ thiếu ngày sinh vẫn nhận BMI theo ngưỡng tham chiếu người lớn, không nhận TDEE.

TDEE là ước tính calo duy trì theo Mifflin–St Jeor cho người trưởng thành `male/female`, nhân mức vận động 1,2 / 1,375 / 1,55 / 1,725 / 1,9. `other`, thiếu ngày sinh/mức vận động hoặc tuổi không phù hợp trả `null`, không suy đoán giới tính. API không tự đặt mục tiêu tăng/giảm calo. Gợi ý dinh dưỡng là nội dung tổng quát có nguồn [NHS — The vegan diet](https://www.nhs.uk/live-well/eat-well/how-to-eat-a-balanced-diet/the-vegan-diet/); chưa tạo thực đơn FN26.

PUT sức khỏe chỉ sửa trường sức khỏe, giữ displayName/avatar. Profile và điểm lịch sử lưu cùng transaction. History lọc UserId và `recordedAt >= DateTime.UtcNow.AddMonths(-6)`, sort timestamp rồi ID tăng dần. FE phân trang để tải đủ điểm cho biểu đồ.

## Tìm quán và dữ liệu bổ sung

```text
GET /api/Shops/nearby?lat=10.75&lng=106.7&radiusKm=5&pageIndex=1&pageSize=20
GET /api/Shops/search?keyword=chay&lat=10.75&lng=106.7&pageIndex=1&pageSize=20
GET /api/Shops/1?lat=10.75&lng=106.7
```

FE lấy geolocation và gửi `lat/lng`; API kiểm tra tọa độ hữu hạn trong ±90/±180. Nearby bắt buộc cả hai, radius mặc định 5 km và chỉ cho 5–10 km. Search/detail có thể bỏ cả hai; không gửi một tọa độ riêng. Search keyword trim 2–100 ký tự.

Haversine, lọc, đếm, sort và phân trang chạy trong PostgreSQL. Nearby sort khoảng cách tăng, rating giảm, ID; search sort rating giảm, khoảng cách tăng nếu có, tên/ID. Tìm substring case-insensitive trên tên/địa chỉ/menu; ký tự `%` và `_` không phải wildcard. Chỉ trả quán approved, chưa xóa. Nearby bỏ quán thiếu tọa độ. Search không có vị trí trả `distanceKm=null`.

Migration bổ sung cột `shops.description`, `contact_phone`, `rating`, `opening_hours` và bảng `shop_media`, `shop_menu_items`, `shop_opening_periods`. Dữ liệu quán cũ được giữ nguyên; metadata mới nullable, ba bảng mới ban đầu rỗng. Nhóm cần bổ sung **dữ liệu thực** vào các trường/bảng này để có rating, menu, ảnh và trạng thái mở cửa. Không tạo rating/giờ/ảnh giả cho seed cũ. CRUD quản trị quán không thuộc FN27–FN29.

`opening_hours` là văn bản hiển thị. Trạng thái `isOpenNow` dùng `opening_periods`: thứ 1 = thứ Hai, 7 = Chủ nhật; giờ UTC+7 Việt Nam, `closes_at <= opens_at` đóng ngày kế tiếp, bằng nhau là ca 24 giờ; `is_closed=true` không mở. Không có ca nào thì `isOpenNow=null`. Các ngày không có ca trong lịch đã khai báo được xem là đóng. Một ngày có thể có nhiều ca. Link Google Maps có destination khi quán có tọa độ; origin chỉ khi FE gửi vị trí người dùng.

## Chạy migration và backend

Migration mới: `20261002092558_AddMember2HealthAndShops`. Chỉ bổ sung schema, không xóa bảng/cột/dữ liệu cũ, không sửa migration cũ. Ba bảng mới bật RLS và thu hồi quyền table/identity sequence của PUBLIC/anon/authenticated, tiếp nối `SharedDatabaseSafety`; backend dùng database owner hiện tại và kiểm soát quyền qua JWT. Đã chạy migration trên PostgreSQL 17 riêng để kiểm thử và **áp dụng lên Supabase chung ngày 05/10/2026 sau khi người dùng phê duyệt**. Không còn migration pending; số lượng dữ liệu kiểm tra trước/sau không đổi. Search đã trả HTTP 200 trực tiếp trong Swagger, keyword `chay` có 85 kết quả. Nhóm dùng database chung này không cần áp dụng lại; lệnh dưới dùng cho database khác chưa được nâng cấp.

Từ thư mục backend đang chứa thay đổi, dùng connection string Supabase hiện tại và chạy **một lần cho database chung**:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --migrate
```

Lệnh hoàn thành rồi thoát, không khởi động API. Sau đó chạy hoặc khởi động lại BE:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

API không tự migrate khi khởi động thường. Giữ connection string ở local configuration đã có hoặc .NET user-secrets; không đưa secrets vào Git. Nếu chưa cấu hình, template không chứa secrets là `src/VeganHelper.API/appsettings.example.json`; cấu hình khóa JWT ổn định bằng `dotnet user-secrets set "Jwt:SigningKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARACTERS" --project src/VeganHelper.API`. Các giá trị ví dụ phải được thay bằng cấu hình của nhóm. Không chạy seed lại để áp dụng migration này.

Theo quy định mapping, đã thêm AutoMapper 16.2.0 và một `MappingProfile` dùng chung. Key tùy chọn mới `AutoMapper:LicenseKey` được khai báo trong template và đọc qua Options Pattern. Đặt key phù hợp giấy phép của nhóm trong user-secrets hoặc `AutoMapper__LicenseKey` khi có. Theo [tài liệu AutoMapper](https://docs.automapper.io/en/stable/License-configuration.html), thiếu key sinh log cảnh báo, không khóa tính năng runtime; nhóm cần kiểm tra điều kiện giấy phép trước khi phát hành.

## Kiểm thử

Unit tests kiểm tra BLL, boundary chiều cao/cân nặng, ngày sinh, BMI/TDEE, dữ liệu cũ nullable, deduplicate dị ứng, ca mở qua nửa đêm và query không hợp lệ. Integration tests chạy EF migration/truy vấn trên **PostgreSQL thật**, đi qua HTTP và JWT: 400/401/404, dữ liệu tài khoản tách biệt, lịch sử 6 tháng, dropdown, cảnh báo dị ứng, custom name đồng thời, rollback khi FK lỗi, tìm quán theo menu, khoảng cách, phân trang và chi tiết.

Mặc định integration tests dùng Testcontainers PostgreSQL 17, cần Docker engine hoạt động:

```powershell
dotnet test tests/VeganHelper.UnitTests
dotnet test tests/VeganHelper.IntegrationTests
```

Nếu Docker chưa chạy, có thể dùng PostgreSQL riêng với database tên bắt đầu `member2_test` trên loopback. Member 2 test fixture từ chối endpoint ngoài máy hoặc tên database khác, không kết nối Supabase. Hai biến chỉ trỏ vào database test riêng:

```powershell
$env:VEGANHELPER_MEMBER2_TEST_POSTGRES='Host=127.0.0.1;Port=55439;Database=member2_test_icu;Username=postgres'
$env:VEGANHELPER_TEST_POSTGRES=$env:VEGANHELPER_MEMBER2_TEST_POSTGRES
dotnet test tests/VeganHelper.IntegrationTests --artifacts-path .local-data/member2-artifacts
```

Lần kiểm thử này Docker engine lỗi khởi động; đã dùng PostgreSQL 17.11 portable chính thức trên port riêng, database mới UTF-8/ICU để kiểm tra tiếng Việt. Kết quả: **63 unit tests + 48 integration tests đều pass**, không skip; build API 0 warning/error; EF không có model change chưa nằm trong migration. Kiểm tra RLS đã mô phỏng default privileges của anon/authenticated và xác nhận migration thu hồi quyền. Server test riêng được dừng sau kiểm tra. Không chạy test lên database chung. Không thay đổi FE, không commit/push tác vụ này.
