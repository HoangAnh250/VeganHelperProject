# FN38–FN39: quản lý thành viên (BE)

Nhánh `feature/admin`, worktree `C:\Users\ADMIN\.codex\worktrees\admin-foundation\VeganHelperProject`.
Tiếp nối nền tảng FN44; không thay đổi FE, không đổi role, không tạo Admin tự động. Không commit/push.

## API

Tất cả route yêu cầu Bearer JWT hợp lệ và role `admin` hiện tại trong database.

| Method | Route | Chức năng |
|---|---|---|
| GET | `/api/admin/members` | Danh sách, tìm kiếm, lọc, phân trang |
| GET | `/api/admin/members/{id}` | Chi tiết thành viên |
| POST | `/api/admin/members/{id}/ban` | Khóa tài khoản |
| POST | `/api/admin/members/{id}/unban` | Mở khóa |

Query danh sách: `PageIndex=1`, `PageSize=20` (tối đa 100); `Keyword` tối đa 100 ký tự, tìm chứa theo username/email/display name, không phân biệt hoa thường. `%`, `_`, `\` được hiểu là ký tự thông thường. `Role`: `member` hoặc `admin`. `Status`: `active`, `banned`, `locked`, `inactive`, `pending_verification`, `deleted`. Giá trị role/status không phân biệt hoa thường; để trống nghĩa là không lọc.

Status ưu tiên theo thứ tự: deleted → banned → pending_verification → inactive → locked → active. `locked` là khóa đăng nhập tạm do nhập sai password, `banned` là khóa do Admin. `IsActive` phản ánh cột kích hoạt gốc, không phải trạng thái ban. Danh sách và chi tiết dùng chung truy vấn trạng thái. Sắp xếp CreatedAt giảm dần rồi Id giảm dần, trả `PagedResult<AdminMemberDto>` theo convention hiện có.

DTO chỉ có thông tin định danh/liên hệ cơ bản, role/status, thời điểm tạo/đăng nhập/xác minh, khóa đăng nhập và ban hiện tại (`banId`, `banReason`, `banExpiresAt`). Không trả password hash, refresh token, session version hoặc dữ liệu sức khỏe. `banId != null` xác định ban hiện tại; `banExpiresAt = null` có thể là ban vô thời hạn. Lịch sử ban được giữ trong `user_bans`; chưa có API riêng đọc lịch sử.

Body khóa có thời hạn (ISO 8601 với múi giờ):

```json
{ "reason": "Vi phạm quy định cộng đồng", "expiresAt": "2026-12-01T00:00:00Z" }
```

Khóa vô thời hạn: bỏ `expiresAt` hoặc truyền `null`. Mở khóa:

```json
{ "reason": "Đã xem xét khiếu nại" }
```

`reason` bắt buộc, không chỉ khoảng trắng, tối đa 500 ký tự; lưu sau trim. Thời hạn ban phải lớn hơn thời gian server UTC tại lúc lấy được row lock. Chỉ khóa/mở tài khoản `member` chưa bị xóa; không được thao tác chính mình hay tài khoản Admin. Thành viên chưa xác minh email hoặc đang khóa đăng nhập vẫn có thể bị ban; mở ban giữ nguyên các hạn chế đó.

Kết quả thành công trả 200 + chi tiết mới. 400: dữ liệu không hợp lệ; 401: token không hợp lệ/đã thu hồi, tài khoản inactive/deleted/banned; 403: thiếu quyền hoặc target là chính mình/Admin; 404: không tìm thấy (kể cả target đã xóa khi thay đổi ban); 409: đã ban khi ban lại, hoặc không có ban còn hiệu lực khi unban. GET chi tiết vẫn có thể xem tài khoản đã xóa.

## Thu hồi phiên và tính nhất quán

- `users.token_version` và `refresh_tokens.token_version` mặc định 0. Access JWT có claim `token_version`. Ban và unban tăng version, đồng thời thu hồi mọi refresh token chưa thu hồi.
- Mỗi request qua bearer authentication kiểm tra database: phiên đúng version, user active/chưa xóa, không có ban còn hiệu lực. Role trong principal được cập nhật từ database để claim Admin cũ không giữ quyền sau khi bị hạ role.
- Login password, Google và refresh đều kiểm tra ban. Refresh còn đối chiếu version đã lưu; phiên được tạo bởi login/refresh chạy đua với ban không thể hồi sinh sau khi mở khóa.
- Hết hạn tự động có hiệu lực khi kiểm tra thời gian; không cần background job. Bản ghi ban cũ vẫn còn. Token trước ban tiếp tục bị từ chối sau khi hết hạn hoặc unban. Người dùng đăng nhập lại để nhận phiên mới.
- Khóa dùng row lock PostgreSQL trên target; hai Admin ban cùng lúc chỉ một lệnh thành công, lệnh còn lại 409. Ban/unban, version, revoke refresh và audit nằm trong cùng transaction. Lỗi ghi audit làm rollback toàn bộ.
- Audit thành công: `member.list`, `member.view`, `member.ban`, `member.unban`, target type `user`. Actor/IP/trace lấy từ server. Không lưu request body hay secrets vào audit; lý do lưu trong lịch sử ban. Lỗi và thao tác bị từ chối không tạo log thành công.
- Thu hồi có hiệu lực ở lần kiểm tra xác thực sau khi transaction ban commit. Request đã được xác thực trước thời điểm đó có thể hoàn thành; hệ thống không hủy request đang xử lý.

## Migration và chạy thử

Migration mới: `20261007080739_AddAdminMemberBans` thêm hai cột version và bảng `user_bans` với FK/constraints/index. Không bỏ bảng hay dữ liệu hiện có. Bảng mới bật RLS, thu hồi table/sequence grants khỏi PUBLIC, `anon`, `authenticated`; truy cập qua BE với connection có quyền phù hợp (owner/BYPASSRLS + grants). Không cấp quyền ban qua Supabase Data API. Đây là JWT của BE, không chuyển qua Supabase Auth.

Chỉ migration trên PostgreSQL thử nghiệm riêng đã được áp dụng để kiểm chứng. **Chưa áp dụng lên Supabase chung.** Người phụ trách database chạy migration trước khi mở BE mới (nó cũng áp dụng FN44 nếu còn thiếu), sau khi bảo đảm connection/JWT config riêng đã có:

```powershell
Set-Location 'C:\Users\ADMIN\.codex\worktrees\admin-foundation\VeganHelperProject'
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --migrate
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

Không đặt mật khẩu/connection string vào Git. Không chạy bản BE cũ và mới đồng thời khi triển khai: phiên bản cũ chưa kiểm tra ban/version. **Khởi động lại BE sau thay đổi và đăng nhập lại** vì JWT trước nâng cấp thiếu claim version. Không cần thay đổi BCrypt hoặc JWT expiry, không thêm API key/cấu hình mới. Rollback migration làm mất lịch sử ban/cơ chế thu hồi mới; cần xem xét dữ liệu trước khi rollback.

Swagger: đăng nhập Admin → Authorize → GET danh sách/detail → ban member → dùng token member cũ gọi `/api/notifications` (401), login member bị ban (403), refresh cũ (401) → unban → token cũ vẫn 401, login mới dùng được. Kiểm tra bảng `user_bans`, `admin_audit_logs` với tài khoản database quản trị.

## Kiểm chứng

`AdminMemberTests` kiểm tra API thật + PostgreSQL: quyền, DTO/search tiếng Việt/ký tự wildcard, filters/pagination, ban tạm/vô thời hạn, expiry, unban giữ nguyên activation/login lock, token revocation, phiên chạy đua, Google, concurrency, rollback audit và quyền Data API. `AdminFoundationTests` cập nhật mong đợi 401 cho phiên/tài khoản không hợp lệ sau global token validation. Fixture JWT issuance và validation cùng test issuer/audience, nên kiểm thử đăng nhập mới gọi được API được bảo vệ.

Test dùng PostgreSQL riêng loopback với ICU locale, qua `VEGANHELPER_MEMBER2_TEST_POSTGRES` và `VEGANHELPER_TEST_POSTGRES` (fixture từ chối database chung), hoặc Testcontainers mặc định. Dùng `--logger "trx;LogFilePrefix=admin-members"` để kết quả hai project không ghi đè nhau.

Kết quả ngày 07/10/2026: **108 unit + 135 integration passed**, 0 failed/skipped; build 0 warnings/errors. Model/snapshot parity qua và EF không còn pending model changes. Review độc lập không phát hiện lỗi production cần sửa. Kết quả TRX nằm ở `.local-data/test-results/admin-members-final_net10.0_20261007151426.trx` và `admin-members-final_net10.0_20261007151445.trx`.
