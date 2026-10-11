# FN13/FN14, FN41–FN43 — Công khai trước, kiểm tra AI sau và report

Cập nhật 10/10/2026. Code BE ở nhánh `feature/reports`, chưa commit/push/merge. Thay đổi FE trong đợt này đã revert theo yêu cầu; giao diện do thành viên khác thực hiện. Migration mới chưa áp dụng vào Supabase chung.

## Luồng đã triển khai

- FN13 lưu bài `published` cùng hàng chờ AI trong một transaction. Feed và chi tiết public đọc được ngay.
- FN14 tăng `contentRevision`, lưu bản sửa và hàng chờ cùng transaction. Bài công khai vẫn công khai; bài bị AI/Admin giữ, ẩn hoặc từ chối vẫn `pending_review` với `moderationHold=true`.
- Khi lưu FN14, khóa và đọc lại trạng thái DB sau upload. AI/Admin vừa giữ bài trong lúc tác giả sửa không thể bị trạng thái cũ của FE ghi đè.
- AI kiểm tra văn bản, nguyên liệu, bước nấu và toàn bộ ảnh ngoài transaction. Kết quả chỉ được ghi khi lease và revision vẫn đúng, bài chưa xóa và job chưa bị Admin vô hiệu hóa.
- `safe`: lưu kết quả và usage; không đổi trạng thái bài, không gửi thông báo duyệt bài vốn đã public, không tự gỡ hold.
- `flagged`/`uncertain`: lưu cờ, bằng chứng, giữ bài thành `pending_review`. Nếu vừa rời trạng thái public, lưu thông báo tác giả và push outbox trong cùng transaction.
- Lỗi quota, timeout, ảnh/video không thể kiểm tra, phản hồi sai schema không chứng minh vi phạm. Bài đang public tiếp tục public; lỗi kiểm tra được ghi riêng để Admin theo dõi.
- Admin quyết định cuối cùng. `approve`/`keep` công khai và bỏ hold; `reject`/`remove` giữ không công khai. Quyết định, flags, audit và thông báo commit/rollback cùng nhau.

Khách/người khác không đọc được bài bị giữ qua feed/search/detail. Tác giả xem bài của mình; Admin xem qua API Admin. Tab đã tải dữ liệu cập nhật khi gọi lại API/tải lại trang; feed không có cơ chế tự xóa nội dung realtime.

Sửa tìm kiếm ngày 11/10/2026: `GET /api/Posts/search` tìm tiêu đề/nội dung của cả `article`, `video` và `recipe`, không còn bỏ qua công thức seed. Vẫn chỉ trả bài `published`, chưa xóa, giữ tìm không phân biệt hoa/thường và phân trang. Riêng sửa bộ lọc này không cần migration; migration publication/report nói bên dưới vẫn là thay đổi riêng chưa áp lên Supabase.

## Report

Member đã đăng nhập report bài public của người khác; reporter lấy từ JWT. Gửi `expectedRevision` của bài đang xem, loại lý do và mô tả. Report không tự ẩn bài và không gửi thông báo vi phạm cho tác giả.

Giới hạn 10 report/giờ/tài khoản, kiểm tra dưới khóa user. Report pending cùng người/bài/revision được trả lại cùng ID, có unique index hỗ trợ. Nhiều người vẫn có thể report cùng bài.

Report lưu snapshot nội dung, nguyên liệu, bước nấu và URL media. Mỗi revision được ưu tiên/coalesce một lượt kiểm tra do report; report tiếp theo không reset vô hạn retry. Khi một lượt đã hoàn tất, lần recheck có ngân sách riêng tối đa 5 lỗi transient. Lịch sử assessment cũ giữ nguyên, có revision/thời gian/request ID. AI safe không tự dismiss report.

Admin xem bản bị report cạnh bản hiện tại và bằng chứng AI. `dismiss` chỉ đóng report đó, không bỏ hold khác. `keep` công khai phiên bản hiện tại và giải quyết flags/report liên quan; `remove` ẩn phiên bản hiện tại. Mọi quyết định yêu cầu revision mới nhất và lý do. Report của bài đã soft-delete vẫn xem và dismiss được; không thể khôi phục bài đã xóa qua quyết định report.

Media được snapshot report/AI flag tham chiếu không bị cleanup khi tác giả sửa/xóa bài, để bằng chứng còn xem được. Chưa có chính sách hết hạn/purge riêng cho các ảnh bằng chứng này. Bằng chứng các ảnh đã bị xóa trước migration không thể khôi phục tự động.

## API

| Method | Route | Nội dung |
|---|---|---|
| POST | `/api/posts/{id}/reports` | Member gửi report |
| GET | `/api/admin/moderation/reports` | Admin phân trang report; mặc định pending |
| GET | `/api/admin/moderation/reports/{id}` | Snapshot gốc, bản hiện tại, AI history |
| POST | `/api/admin/moderation/reports/{id}/decision` | `dismiss`, `keep`, `remove` |
| GET | `/api/admin/moderation/posts` | Bài theo status; dùng `Status=all` để xem cả lỗi scan trên bài public |
| GET | `/api/admin/moderation/posts/{id}` | Nội dung, kết quả scan, flags và quyết định |
| POST | `/api/admin/moderation/posts/{id}/decision` | `approve`, `reject` bài pending |
| GET | `/api/admin/moderation/ai-flags` | Cờ pending, gồm bằng chứng cũ của bài đang giữ |
| POST | `/api/admin/moderation/ai-flags/{id}/decision` | `keep`, `remove` với revision hiện tại |
| GET | `/api/admin/moderation/settings` | `publicationMode=immediate`, `autoPublishSupported=false` |
| PUT | `/api/admin/moderation/settings` | Trả 410; toggle duyệt tự động cũ đã ngừng sử dụng |

Body report:

```json
{ "reasonCategory": "non_vegan", "reason": "Ảnh nghi có thịt động vật", "expectedRevision": 1 }
```

Các loại: `non_vegan`, `violence`, `harassment`, `spam`, `other`; mô tả bắt buộc, tối đa 1000 ký tự. Receipt chỉ có id/status, không lộ reporter. Danh sách Admin dùng `PageIndex`, `PageSize` tối đa 100, `Status=pending|dismissed|resolved|all`, `PostId` tùy chọn.

Body quyết định:

```json
{ "action": "remove", "reason": "Nội dung hiện tại không phù hợp", "expectedRevision": 2 }
```

400 input sai; 401 chưa đăng nhập; 403 thiếu quyền/tự report; 404 bài không public hoặc đối tượng không có; 409 revision cũ/quyết định đã xử lý; 429 quá giới hạn report. API Admin kiểm tra quyền trong database và trạng thái token/tài khoản; FE không đọc trực tiếp bảng bằng Supabase anon key.

## Worker và Gemini

Giữ cấu hình BE hiện có: `ModerationAi:Enabled`, `ApiKey`, `Model`, `AllowedMediaHosts`, `TimeoutSeconds`. Key chỉ lưu user-secrets/biến môi trường, không đưa vào FE/Git. `MinAutoPublishConfidence` giữ cho tương thích kết quả có score kiểu cũ; không áp dụng contract PASS/FAIL và không cho AI công khai bài bị giữ.

Harness kiểm tra đầu ra bằng schema và parser nghiêm ngặt. Prompt `PostModeration.v2.json` chỉ yêu cầu hai trường:

```json
{"result":"FAIL","reason":"Ảnh #11 chứa thịt sống; cần Admin kiểm tra."}
```

`result` chỉ nhận `PASS`/`FAIL`; `reason` bắt buộc, tối đa 200 ký tự, một câu ngắn tiếng Việt. PASS chỉ khi tất cả văn bản/ảnh phù hợp; FAIL bao gồm vi phạm hoặc nội dung còn nghi vấn, cần nêu rõ sự không chắc chắn. Không yêu cầu score, danh sách findings hay suy luận dài từ AI. BE ánh xạ sang trạng thái scan cũ (`safe`/`flagged`), lưu lý do và snapshot có sẵn. Confidence mới là null, finding chung ở cấp bài (`field=post`), không bịa score, trích dẫn hoặc ID ảnh từ lý do tự do. Lịch sử có score/chi tiết kiểu cũ vẫn đọc được.

Gemini dùng `thinking_level=low`, `thinking_summaries=none`, `max_output_tokens=2048`. Giới hạn này gồm cả suy luận nội bộ nên không đặt quá nhỏ; schema/parser giới hạn riêng câu trả lời cuối. Xem [tài liệu Gemini về thinking và giới hạn token](https://ai.google.dev/gemini-api/docs/thinking). Phản hồi bị cắt, sai schema, thiếu lý do hoặc lỗi API được ghi lỗi kiểm tra, không trở thành PASS/vi phạm. Giảm token đầu ra không làm tăng giới hạn request/ngày hay loại bỏ token đầu vào ảnh.

```powershell
dotnet user-secrets set --project src/VeganHelper.API 'ModerationAi:ApiKey' '<PRIVATE_GEMINI_KEY>'
dotnet user-secrets set --project src/VeganHelper.API 'ModerationAi:AllowedMediaHosts:0' '<YOUR_R2_PUBLIC_HOST>'
dotnet user-secrets set --project src/VeganHelper.API 'ModerationAi:Enabled' 'true'
```

Host là tên DNS storage tin cậy, không chứa scheme/path. Job `posts-ai-moderation` chạy mỗi phút, tối đa 2 scan/batch, chia sẻ claim/lease trên PostgreSQL. Một máy hoặc nhiều máy chạy BE đều dùng hàng chờ chung. Có thể chỉ bật worker trên một máy. Push trình duyệt cần WebPush riêng; thông báo trong website không phụ thuộc WebPush.

HTTP 429 lưu cooldown chung `provider_retry_after`: dùng Retry-After khi có, giới hạn 1 phút–24 giờ; thiếu header chờ 15 phút. Quota không tiêu retry budget của bài, không đổi trạng thái public. Sau cooldown, worker thử lại; nếu provider tiếp tục trả 429, tiếp tục hoãn. Timeout/5xx vẫn backoff tối đa 5 lần; không tự ghi safe. Scan report được ưu tiên, scan cũ trên 1 giờ được nâng ưu tiên để tránh bỏ quên bài thông thường. Xem [hướng dẫn retry của Gemini](https://ai.google.dev/gemini-api/docs/troubleshooting).

## Contract bàn giao cho người làm FE

- Create cần báo công khai thành công; Edit đọc `status`/`moderationHold` từ BE để phân biệt đã cập nhật với bản sửa vẫn chờ Admin.
- My Posts có thể hiển thị Public / Temporarily hidden / Hidden / Rejected / Draft theo `status`.
- Gửi report qua API với lý do và `expectedRevision`; chỉ cho người đăng nhập report bài public của người khác.
- Kết nối API notification để lấy badge/danh sách và đánh dấu đọc. Thông báo tác giả đã được BE lưu cùng transaction giữ bài.
- Kết nối API Admin report để xem snapshot/bản hiện tại/lịch sử AI và quyết định theo revision mới nhất. Role có trong profile BE; quyền thực tế luôn được BE kiểm tra.

Các component, route, services và tests FE từng thêm cho luồng này đã được gỡ. Không có màn hình report/notification mới được bàn giao dưới dạng code FE trong đợt này.

## Migration và chạy thử

Migration `20261010084021_PublishFirstAndPostReports` nối sau `20261007085728_AddPostModeration`. Thêm hold, cooldown, recheck/priority, snapshot report và bảng assessment; không bỏ bảng cũ. Bài legacy pending/hidden/rejected được backfill hold, không bulk publish. Bảng assessment bật RLS/thu hồi quyền browser; lịch sử chặn UPDATE/DELETE/TRUNCATE, snapshot report chặn sửa bằng chứng.

Script xem trước: `docs/database/2026-10-10-publish-first-reports.sql`. Script chỉ nâng bản AddPostModeration lên migration mới; database chung cần có đầy đủ các migration trước đó. Trong tác vụ này **chưa chạy lên Supabase**. Kiểm tra backup/schema rồi chỉ cần một người áp dụng trước khi các máy chạy code mới:

```powershell
Set-Location 'C:\Users\ADMIN\.codex\worktrees\4ae8\VeganHelperProject'
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --migrate
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

Lệnh migrate tác động database trong connection string đang cấu hình. Không chạy nhầm connection test hoặc migration trước khi sẵn sàng triển khai chung. Với worker mới chưa chuẩn bị Hangfire, chạy `--prepare-background-jobs` như cấu hình hiện có trước khi khởi động worker.

FE hiện dùng lại code trước đợt thay đổi này. Thành viên làm FE cần tích hợp các contract mới ở trên trước khi test đầy đủ giao diện report/notification/hold.

Kiểm thử BE ngày 10/10: 181 unit + 256 integration qua trên PostgreSQL local, EF không thiếu migration. Có kiểm thử migration từ schema cũ, hold trong lúc upload, report đồng thời/rate limit, Admin stale/concurrent decisions, quota/cooldown hồi phục và giữ ảnh bằng chứng. Review độc lập đã xác nhận các phát hiện được sửa. Trước khi revert, FE mới đã qua 14 tests/build; kết quả đó là lịch sử của bản FE đã gỡ, không xác nhận việc tích hợp giao diện hiện tại. Chưa gọi Gemini thật hoặc áp migration vào Supabase; cần smoke test với key/storage thật sau khi triển khai.
