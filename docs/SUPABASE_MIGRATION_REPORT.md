# Kết quả chuyển database — 2026-10-01

Database đích: Supabase project `brjltkherhhqqkryhsuq`, PostgreSQL 17.11, Session pooler cổng 5432, database `postgres`. Không ghi connection string/mật khẩu vào báo cáo.

Đã chuyển **193 bản ghi gốc của tất cả 32 bảng ứng dụng**, giữ ID, quan hệ, nội dung và password hash. Công cụ đã đối chiếu từng trường trước khi commit. SQL Server nguồn không bị cập nhật/xóa. Snapshot gốc nằm riêng trong `.local-data/sqlserver-export.json`, không được Git theo dõi.

Sau import, đã chạy seed 20 bài demo có sẵn, rồi kiểm tra lại toàn bộ bản ghi gốc bằng `verify-preserved`.

| Bảng có dữ liệu | Dữ liệu gốc | Thêm từ seed | Tổng trên Supabase |
| --- | ---: | ---: | ---: |
| categories | 15 | 0 | 15 |
| shops | 100 | 0 | 100 |
| posts | 3 | 20 | 23 |
| users | 6 | 1 tác giả seed | 7 |
| user_profiles | 6 | 1 | 7 |
| roles | 2 | 0 | 2 |
| post_categories | 3 | 20 | 23 |
| post_media | 5 | 20 | 25 |
| post_steps | 0 | 60 | 60 |
| user_identities | 1 | 0 | 1 |
| email_verification_tokens | 4 | 0 | 4 |
| password_reset_tokens | 7 | 0 | 7 |
| refresh_tokens | 41 | 0 | 41 |
| Các bảng còn lại | 0 | 0 | 0 |
| **Tổng** | **193** | **122** | **315** |

Bài gốc bị soft-delete vẫn được giữ nguyên nên feed hiển thị 22 bài, trong khi bảng posts chứa 23 bài. API categories trả 10 category bài viết; 5 category shop vẫn có trong database.

## Thay đổi code

- EF Core chuyển sang Npgsql; mapping Unicode, boolean, identity, timestamp UTC, date, JSON/check constraint được chuyển sang PostgreSQL. Migration SQL Server giữ làm lịch sử; PostgreSQL có bộ migration riêng.
- Email/username giữ lookup và uniqueness không phân biệt hoa/thường. Feed/MyPosts có thứ tự phụ theo ID để phân trang ổn định; MyPosts load category; filter difficulty/diet/status dùng so sánh đã chuẩn hóa.
- API category đi qua repository/service/DTO và chỉ trả category bài viết đang active.
- Migrate/seed chạy bằng lệnh chủ động; seed không ghi đè bài/category đã có; shop seed giữ tiếng Việt và reset identity.
- Công cụ export/import có kiểm tra schema, transaction, foreign key, đối chiếu dữ liệu và bảo vệ database đích không trống.
- RLS và quyền Data API được cấu hình cho các bảng backend quản lý; TLS dùng VerifyFull cùng public CA.

## Kiểm chứng

- API và công cụ migration build thành công. Build API còn cảnh báo nullable/using trùng đã có trước trong các phần khác.
- **14 unit tests + 9 PostgreSQL integration tests pass**, gồm filter/projection, phân trang, category, auth lookup/uniqueness, cập nhật view count, soft-delete và seed lặp lại. Shop seed được kiểm tra trên bảng tạm riêng, ID tiếp theo là 101.
- Integration tests ghi trong transaction rồi rollback; đối chiếu sau tests xác nhận dữ liệu gốc còn nguyên.
- HTTP 200: `/health/live`, `/api/status` (databaseAvailable=true), `/api/Categories`, `/api/Posts`, feed có filter và `/swagger/v1/swagger.json`.
- Kiểm tra import lại: công cụ từ chối bảng đích không trống; `verify-preserved` sau đó vẫn pass.
- Supabase MCP đã thêm và đăng nhập OAuth; CLI xác nhận enabled/OAuth. Việc chuyển dữ liệu thực hiện qua công cụ Npgsql, không phụ thuộc MCP.
- File cấu hình local và snapshot được Git ignore. **Chưa commit hoặc push** theo yêu cầu.

## Chạy cho cả nhóm

Xem [hướng dẫn cấu hình và chạy](SUPABASE_SETUP_VI.md). Thay đổi nằm trong checkout `C:\Users\ADMIN\.codex\worktrees\4ae8\VeganHelperProject`; checkout BE khác chưa được đồng bộ tự động. Các thành viên cần nhận code mới và cấu hình cùng Supabase project. Dừng terminal BE cũ và chạy lại `dotnet run` sau khi nhận thay đổi.

Database chỉ chứa URL ảnh. Một avatar gốc dùng đường dẫn `/uploads/...`; file này vẫn cần sao chép hoặc đưa lên storage chung để các máy khác có thể phục vụ ảnh. JWT/Auth và Cloudflare R2 hiện có tiếp tục dùng cơ chế của .NET backend.
