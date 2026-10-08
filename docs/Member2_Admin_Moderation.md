# FN41–FN43 — Kiểm duyệt bài đăng (BE)

Triển khai trên nhánh `feature/admin`, tích hợp vào `developer`. Kế thừa FN44, FN38–FN40. Migration chưa được triển khai lên Supabase chung; FE Admin cần tích hợp các API dưới đây.

## Luồng

Tạo hoặc sửa bài → `pending_review` và hàng chờ AI được lưu cùng transaction. Mỗi lần sửa tăng `contentRevision`; kết quả AI và quyết định Admin chỉ áp dụng đúng phiên bản đó. Worker Hangfire quét tiêu đề, nội dung, nguyên liệu, bước nấu và toàn bộ ảnh bằng Gemini.

- AI đánh dấu vi phạm hoặc không chắc chắn: tạo flag với lý do, confidence và các đoạn/ảnh cần xem; Admin quyết định.
- AI đánh giá an toàn, confidence từ 0.9 trở lên: chỉ tự công khai nếu Admin đã bật toggle. Toggle mặc định **tắt**. Có thể cấu hình ngưỡng cao hơn.
- Flag AI hiện tại chưa được giải quyết: vẫn cần Admin, dù lần quét mới an toàn.
- Video, ảnh không tải được, host ảnh chưa được cho phép, ảnh/input quá lớn, phản hồi sai schema hoặc lỗi provider: giữ chờ duyệt. Không dùng thumbnail thay việc kiểm tra video.
- Timeout/429/5xx được thử lại tối đa 5 lần, có backoff và lease phục hồi khi worker dừng. Các lỗi không thể thử lại chuyển `manual_required`; hết lượt chuyển `failed`, bài vẫn chờ Admin.

AI không xóa bài hoặc media. `remove` chuyển bài thành `hidden`. Tác giả có thể sửa và gửi duyệt lại. Mọi quyết định có lịch sử; quyết định thủ công có audit FN44. Quyết định, trạng thái, audit, thông báo cho tác giả và push outbox commit/rollback cùng nhau. Browser push thực tế cần cấu hình WebPush riêng; thông báo trong website hoạt động độc lập.

Khách/người khác chỉ xem chi tiết bài `published`; tác giả được xem bài chưa công khai của mình. Admin xem nội dung cần duyệt qua API Admin. Bài bị xóa không thể đọc. Feed/search vẫn chỉ trả bài published.

## API

Tất cả route này yêu cầu JWT hợp lệ và quyền Admin hiện tại trong database, có kiểm tra token thu hồi/khóa tài khoản.

| Method | Route | Công dụng |
|---|---|---|
| GET | `/api/admin/moderation/posts` | Hàng chờ/phân trang bài |
| GET | `/api/admin/moderation/posts/{id}` | Nội dung, ảnh, ingredients/steps, kết quả AI, flag, 20 quyết định gần nhất |
| POST | `/api/admin/moderation/posts/{id}/decision` | `approve` hoặc `reject` bài đang chờ |
| GET | `/api/admin/moderation/ai-flags` | Flag AI đang chờ của phiên bản hiện tại |
| POST | `/api/admin/moderation/ai-flags/{id}/decision` | `keep` hoặc `remove` |
| GET | `/api/admin/moderation/settings` | Đọc toggle và version |
| PUT | `/api/admin/moderation/settings` | Cập nhật toggle theo version |

Danh sách bài: `PageIndex=1`, `PageSize=20` (tối đa 100), `Keyword` tối đa 100 ký tự, `Status=pending_review` mặc định. Các status: `pending_review`, `published`, `rejected`, `hidden`, `draft`, `all`. Keyword tìm title không phân biệt hoa thường; `%`, `_`, `\` là ký tự thường. Sắp xếp bài cũ trước, sau đó Id. Danh sách flag: cùng pagination, `PostId` và `MinConfidence` (0–1, có thể bỏ trống). Confidence là đánh giá của AI, không phải bảo đảm nội dung đúng.

Body quyết định lấy `expectedRevision` từ chi tiết bài/flag mới nhất:

```json
{ "action": "reject", "reason": "Ảnh chứa thịt động vật; vui lòng thay ảnh phù hợp.", "expectedRevision": 1 }
```

`approve`/`keep` → published; `reject` → rejected; `remove` → hidden. Reason bắt buộc, trim không rỗng, tối đa 1000 ký tự. Trả 400 khi input sai; 401/403 khi thiếu phiên/quyền; 404 nếu không có bài/flag; 409 khi revision/version cũ hoặc quyết định đã được xử lý. Hai Admin duyệt đồng thời chỉ một quyết định được ghi. Actor/IP/trace lấy phía server.

PUT settings lấy `expectedVersion` từ GET gần nhất:

```json
{ "autoPublishEnabled": true, "expectedVersion": 1 }
```

Toggle có hiệu lực khi worker hoàn tất lần quét tiếp theo. Bật toggle không tự công khai lại các bài đã quét xong khi toggle tắt; các bài đó vẫn có thể duyệt thủ công. `ModerationAi:Enabled` là cấu hình bật worker, khác toggle quyền tự công khai của Admin.

## Cấu hình và chạy

1. Dùng bản BE `developer` đã tích hợp Admin. Sao lưu database và kiểm tra migration trước khi triển khai lên database chung. FN41–FN43 thêm migration `20261007085728_AddPostModeration`, nối sau các migration FN44/FN38–FN39. Lệnh triển khai (chưa chạy lên Supabase trong tác vụ này):

```powershell
Set-Location 'C:\Users\ADMIN\.codex\worktrees\admin-foundation\VeganHelperProject'
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --migrate
```

Migration giữ bảng/dữ liệu cũ; thêm `posts.content_revision`, hai cột liên kết flag và ba bảng `post_moderation_scans`, `post_moderation_decisions`, `post_moderation_settings`. Bài pending cũ được xếp hàng; bài published cũ giữ nguyên. Flag AI cũ được gắn revision ban đầu để không áp dụng vào lần sửa mới. Bảng mới bật RLS, thu hồi quyền PUBLIC/anon/authenticated; lịch sử quyết định chặn UPDATE/DELETE/TRUNCATE. BE dùng kết nối server có quyền cần thiết, FE không đọc trực tiếp các bảng này bằng Supabase anon key.

2. Tạo Gemini API key trong Google AI Studio, lưu trên **BE** bằng user-secrets hoặc biến môi trường. Không đưa key vào FE, git hoặc chat. Dùng key riêng cho kiểm duyệt để quản lý quota độc lập với AI Genie. Ví dụ cấu hình (thay placeholder trên máy riêng):

```powershell
dotnet user-secrets set --project src/VeganHelper.API 'ModerationAi:ApiKey' '<PRIVATE_GEMINI_KEY>'
dotnet user-secrets set --project src/VeganHelper.API 'ModerationAi:Model' 'gemini-3.8-flash'
dotnet user-secrets set --project src/VeganHelper.API 'ModerationAi:AllowedMediaHosts:0' '<YOUR_R2_PUBLIC_HOST>'
dotnet user-secrets set --project src/VeganHelper.API 'ModerationAi:Enabled' 'true'
```

Host chỉ là tên DNS, ví dụ `pub-xxx.r2.dev`, không có `https://` hoặc đường dẫn. Chỉ cho phép host storage tin cậy của nhóm. Ảnh seed ở host khác vẫn có thể được Admin duyệt thủ công. Key môi trường tương ứng `ModerationAi__ApiKey`. Timeout mặc định 60 giây (cấu hình 1–120), ngưỡng mặc định 0.9 (cấu hình 0.9–1). Cấu hình mẫu nằm trong `src/VeganHelper.API/appsettings.example.json`; mặc định worker tắt để BE chạy được khi chưa có key.

3. Chuẩn bị Hangfire một lần bằng kết nối PostgreSQL session pooler/direct, không dùng transaction pooler. Có thể cấu hình `ConnectionStrings:HangfireConnection`; mặc định dùng DefaultConnection:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --prepare-background-jobs
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

Hangfire dùng schema riêng, thu hồi quyền browser roles, job `posts-ai-moderation` chạy mỗi phút, mỗi batch tối đa 2 bài. Các máy BE dùng cùng DB chia sẻ claim/lease để không xử lý trùng. Muốn chỉ một máy xử lý AI, chỉ bật worker trên máy đó; những máy khác vẫn tạo hàng chờ và dùng Admin API được.

Prompt/schema được load một lần từ `src/VeganHelper.API/Prompts/PostModeration.v1.json` và copy khi build/publish. Client dùng Interactions REST, structured JSON, không gửi key trong URL, không tự theo redirect, kiểm tra DNS/IP và giới hạn kích thước tải ảnh. Lưu model/prompt version/confidence/findings/tokens/latency và usage; không ghi raw request/key vào log.

## Test nhanh trong Swagger

1. Đăng nhập Admin, GET settings xác nhận toggle false.
2. Tạo bài bằng tài khoản Member, GET danh sách Admin/chi tiết để lấy revision. Với worker tắt, bài vẫn pending và Admin duyệt được.
3. POST approve/reject kèm reason → kiểm tra status, notification của tác giả và audit. Gửi lại cùng quyết định → 409.
4. Bật worker bằng key thật, tạo bài có nội dung/ảnh phù hợp; chờ job và đọc `scanState`. Toggle false vẫn pending. Bật toggle rồi tạo bài mới an toàn → có thể published khi đủ confidence.
5. Tạo bài chứa nội dung/ảnh cần xem lại → GET ai-flags thấy lý do, đoạn văn/MediaId và confidence. `keep` hoặc `remove` kèm reason, revision. Kiểm tra thông báo và lịch sử.
6. Sửa bài trước khi gửi quyết định cũ → 409; kiểm tra revision tăng, ảnh FN14 vẫn được thêm/xóa đúng. Guest/Member gọi Admin → 401/403.

Kiểm chứng tự động dùng PostgreSQL local riêng và HTTP/AI giả, không gọi Gemini trả phí hoặc Supabase chung. Chưa xác minh phản hồi thực tế bằng API key thật; bước smoke test Gemini thực tế cần cấu hình riêng như trên.

Kết quả ngày 08/10/2026: **181 unit + 232 integration passed**, không failed/skipped. Trong đó 73 case cho boundary Gemini, 34 case worker, 32 case Admin moderation và một migration regression tạo DB tạm để kiểm tra dữ liệu cũ rồi dọn DB. Các test FN14/recipe hiện có kiểm tra thêm revision và scan được lưu cùng bài. Review độc lập đã được xử lý; kiểm tra EF không có model change thiếu migration. Lệnh chuẩn bị Hangfire chạy thành công trên DB local khi chỉ bật moderation (WebPush tắt). TRX cuối ở `.local-data/test-results/phase4-final/`.

Tài liệu provider đã đối chiếu: [Gemini models](https://ai.google.dev/gemini-api/docs/models), [Interactions API](https://ai.google.dev/api/interactions-api), [Gemini 3.8 migration](https://ai.google.dev/gemini-api/docs/latest-model), [structured output](https://ai.google.dev/gemini-api/docs/structured-output), [image understanding](https://ai.google.dev/gemini-api/docs/image-understanding).
