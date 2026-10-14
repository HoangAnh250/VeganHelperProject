# FN40 — Quản lý danh mục bài viết (BE)

Nhánh `feature/admin`, worktree `C:\Users\ADMIN\.codex\worktrees\admin-foundation\VeganHelperProject`. Kế thừa phân quyền FN44 và kiểm tra phiên từ FN38–FN39. Không commit/push, không sửa FE, không ghi Supabase chung.

## API

Tất cả endpoint dùng Bearer JWT còn hiệu lực và kiểm tra quyền Admin hiện tại trong database.

| Method | Route | Kết quả |
|---|---|---|
| GET | `/api/admin/categories` | 200 + `PagedResult<AdminCategoryDto>` |
| GET | `/api/admin/categories/{id}` | 200 + DTO chi tiết |
| POST | `/api/admin/categories` | 201 + DTO và Location |
| PUT | `/api/admin/categories/{id}` | 200 + DTO sau cập nhật |
| DELETE | `/api/admin/categories/{id}` | 204, chỉ khi không còn liên kết |

FN40 quản lý `category_type = post`, gồm các loại hiện có `recipe`, `food`, `topic`. Các danh mục `shop` không xuất hiện và không thể sửa/xóa qua API này. Không đổi cấu trúc bảng hoặc loại `category_type`.

Query danh sách: `PageIndex` mặc định 1, `PageSize` mặc định 20 (tối đa 100), `Keyword` tối đa 100 ký tự (tìm chứa name/slug không phân biệt hoa thường), `PostCategoryKind` (`recipe`, `food`, `topic`), `IsActive` (true/false). Bỏ bộ lọc nghĩa là lấy mọi giá trị trong phạm vi danh mục bài viết. `%`, `_`, `\` trong keyword là ký tự thường. Sắp xếp Name rồi Id tăng dần, dùng DTO/pagination chung và AutoMapper.

DTO: `id`, `name`, `slug`, `categoryType`, `postCategoryKind`, `isActive`, `postCount`. `postCount` đếm toàn bộ liên kết còn lưu, bao gồm draft, pending review, rejected, hidden và bài soft-delete, phù hợp điều kiện chặn xóa. Danh sách Admin có thể xem danh mục inactive.

Body POST/PUT bắt buộc đủ ba trường:

```json
{
  "name": "Món hấp chay",
  "slug": "mon-hap-chay",
  "postCategoryKind": "recipe"
}
```

Tên sau trim không rỗng, input tối đa 100 ký tự. Slug tối đa 120 ký tự, được trim/lowercase và chỉ nhận a–z, số, dấu gạch nối đơn giữa các từ. Kind được trim/lowercase. Tên danh mục bài viết và slug được kiểm tra trùng không phân biệt hoa thường; slug kiểm tra trên cả danh mục shop vì khóa slug dùng chung toàn bảng.

Danh mục mới active. PUT đổi name/slug/kind, giữ nguyên `IsActive` hiện có; API này không thêm chức năng bật/tắt danh mục. Khi danh mục còn được dùng, có thể đổi tên/slug nhưng không đổi kind để tránh làm thay đổi phân loại bài viết đang liên kết. Trường description trên giao diện FE hiện là nội dung mockup, chưa có trong model; contract FN40 sử dụng các trường database hiện có.

## Lỗi và bảo toàn dữ liệu

- 400: dữ liệu/query không hợp lệ, vượt giới hạn, kind/slug sai.
- 401: chưa đăng nhập, token hết hạn/thu hồi hoặc tài khoản bị khóa/vô hiệu hóa theo FN38–FN39.
- 403: tài khoản hợp lệ nhưng không có quyền Admin.
- 404: ID không tồn tại hoặc trỏ đến danh mục shop.
- 409: trùng tên/slug, xóa danh mục còn bài/shop liên kết, hoặc đổi kind của danh mục đang được dùng.

DELETE chặn mọi liên kết, kể cả bài đã soft-delete. Không cascade xóa bài hoặc tự gỡ liên kết. Xóa danh mục không dùng là xóa vật lý; không có endpoint khôi phục. Nếu database có liên kết shop bất thường đến một danh mục post, nó cũng được bảo vệ.

Create/update/delete và audit commit cùng transaction. Nếu ghi audit lỗi, toàn bộ thay đổi rollback. Audit dùng `category.list`, `category.view`, `category.create`, `category.update`, `category.delete`; actor/IP/trace lấy từ server. Thao tác lỗi không ghi log thành công.

Các lệnh ghi Admin dùng PostgreSQL advisory transaction lock chung để tránh hai lệnh tạo/đổi tên vượt qua kiểm tra trùng. Update/delete khóa row danh mục trước khi kiểm tra liên kết; foreign key là lớp bảo vệ cuối. Hai Admin xóa cùng lúc: một 204, lệnh còn lại 404. Đổi tên/slug dùng `ExecuteUpdateAsync` vì hai trường là alternate key trong model EF hiện tại; không sửa tracked key hoặc bỏ constraints.

Kiểm tra trùng không phân biệt hoa thường có hiệu lực giữa các writer dùng repository này; sửa SQL trực tiếp hoặc writer khác không dùng lock vẫn chịu constraints gốc của database (case-sensitive). Không thay collation/constraint hay tự sửa dữ liệu legacy trong FN40.

## Chạy và test

FN40 **không có migration mới**. Nếu chưa áp dụng migration của FN44/FN38–FN39, cần áp dụng chúng trước để BE đọc audit và phiên token; các migration đó vẫn chưa được triển khai lên Supabase chung trong tác vụ này.

```powershell
Set-Location 'C:\Users\ADMIN\.codex\worktrees\admin-foundation\VeganHelperProject'
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

Khởi động lại terminal BE để biên dịch mã mới. Dùng tài khoản Admin thật, đăng nhập lại nếu token cũ thiếu claim version, rồi Authorize trong Swagger.

Kịch bản:

1. GET danh sách, thử keyword/kind/phân trang.
2. POST body mẫu với slug chưa tồn tại → 201; GET URL từ Location.
3. PUT đổi tên/slug/kind khi chưa liên kết → 200.
4. POST trùng tên hoặc slug (kể cả khác hoa thường) → 409.
5. Dùng một danh mục đang có bài: DELETE → 409, bài và liên kết vẫn còn; PUT đổi kind → 409, đổi tên được.
6. DELETE danh mục vừa tạo chưa được dùng → 204; GET lại → 404.
7. Kiểm tra audit; thử cùng API với Member → 403, Guest → 401.

`GET /api/categories` public vẫn giữ contract array và chỉ trả danh mục post active cho FE hiện tại. Không cần sửa FE để tiếp tục sử dụng API cũ; màn hình Admin cần tích hợp các endpoint mới riêng.

## Kiểm chứng ngày 07/10/2026

- 30 integration case mới trong `AdminCategoryTests`: quyền trên mọi phương thức, CRUD/rename alternate key, validation, trùng tên/slug và concurrency, filters/search Unicode, pagination, shop isolation, giữ liên kết bài soft-delete, rollback audit cho cả ba lệnh ghi, API public regression.
- Toàn bộ suite: **108 unit + 165 integration passed**, không failed/skipped, trên PostgreSQL 17 riêng với ICU locale.
- Review độc lập chỉ đọc: không phát hiện lỗi cần sửa. Không truy cập database chung.
- TRX: `.local-data/test-results/admin-categories-full_net10.0_20261007152827.trx` (unit) và `admin-categories-full_net10.0_20261007152847.trx` (integration).
