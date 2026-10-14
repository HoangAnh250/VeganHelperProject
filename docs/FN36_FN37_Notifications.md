# FN36 / FN37 — Backend thông báo

Kiến trúc: Controller → BLL service/DTO/AutoMapper → DAL repository → PostgreSQL. FN36 lưu và gửi thông báo; FN37 quản lý trạng thái đọc và badge. Bản cập nhật social notifications làm ở BE, không sửa FE.

## Các sự kiện đang nối

| Sự kiện | Người nhận | Type |
| --- | --- | --- |
| Like bài công khai | Chủ bài, trừ tự like | `post_like` |
| Follow mới | Người được follow | `new_follower` |
| Comment gốc visible trên bài công khai | Chủ bài, trừ tự comment | `comment` |
| Reply visible | Tác giả comment cha, trừ tự reply | `comment_reply` |
| Người đang follow đăng bài mới công khai | Người đã bật nhận bài mới của tác giả đó | `followed_post` |
| Bài bị giữ để kiểm duyệt / quyết định Admin | Tác giả bài | `post_review` |
| Admin xóa comment được chọn | Tác giả comment đó | `comment` |

Reply không gửi thêm một thông báo comment cho chủ bài. Nếu chủ bài chính là tác giả comment cha thì họ nhận một thông báo reply. Comment hidden không phát thông báo lúc tạo. Unlike không phát sự kiện; unlike rồi like lại và unfollow rồi follow lại không tạo thêm thông báo cho cùng cặp user/post hoặc user/user.

Nhắc thực đơn (`meal_reminder`) vẫn là hook dành cho scheduler; bản mở rộng này không bổ sung scheduler hoặc nối trạng thái `completed` của FN26 vào hook legacy `saved`.

## Tùy chọn nhận bài mới theo tác giả

Sau khi follow, `notifyOnNewPost` mặc định `false`. `GET /api/users/{userId}/follow-status` và response follow/unfollow trả thêm thuộc tính này cho quan hệ của người gọi với tác giả đó.

```http
PATCH /api/users/42/follow/notifications
Authorization: Bearer <access_token>
Content-Type: application/json

{ "notifyOnNewPost": true }
```

Đổi thành `false` để tắt. `userId` trong URL là tác giả được follow; người nhận được lấy từ JWT, không nhận từ body. Chưa follow trả 404; thiếu token trả 401; thiếu thuộc tính trong body trả 400. Response là FollowStatusDto có `userId`, `isFollowing`, `followersCount`, `followingCount`, `notifyOnNewPost`.

Tắt chỉ ảnh hưởng bài mới của tác giả này, không unfollow, không tắt thông báo like/comment/reply, không xóa lịch sử đã nhận. Unfollow hủy các sự kiện đang chờ của quan hệ đó. Follow lại bắt đầu ở trạng thái tắt; bật sau không gửi lại bài cũ.

## Giao dịch và hàng đợi bài mới

Like, follow và comment lưu hành động + thông báo + push outbox trong cùng transaction. Event key cố định và unique `(user_id,event_key)` chống trùng khi retry. Quyết định kiểm duyệt và audit tiếp tục dùng transaction hiện có.

Khi tạo post công khai lần đầu, BE lưu snapshot những follower hợp lệ đã bật nhận vào `post_follower_notifications` cùng transaction tạo post. Sửa bài hoặc duyệt lại không tạo thêm sự kiện. Hosted worker chạy mỗi 10 giây, xử lý tối đa 20 người nhận một đợt; hoạt động cả khi WebPush/Gemini tắt. Nhiều máy cùng chạy BE vẫn chia việc qua khóa PostgreSQL và `SKIP LOCKED`; crash trước commit để lại hàng đợi cho lần sau.

Worker kiểm tra lại quan hệ, tùy chọn, tài khoản và trạng thái bài trước khi phát. Khi bài mất trạng thái công khai hoặc bị xóa mềm, trigger PostgreSQL hủy các hàng đợi và push chưa gửi trong cùng transaction; duyệt lại không làm sống lại các thông báo đã hủy. Lịch sử inbox giữ nguyên. Push kiểm tra lại lease và nguồn trước khi gửi; thay đổi tùy chọn/ẩn bài có thể chờ một lần gửi đang diễn ra (HTTP timeout 15 giây). Push đã gửi tới nhà cung cấp không thể thu hồi.

Migration `20261014060145_AddSocialNotifications` thêm cột preference, source IDs, bảng hàng đợi và trigger hủy; không xóa dữ liệu nghiệp vụ. Bảng hàng đợi bật RLS, thu hồi quyền PUBLIC/anon/authenticated. Không rollback migration nếu đã có lịch sử thông báo social mà chưa xuất và xem xét dữ liệu.

## API cho FE

Tất cả endpoint yêu cầu `Authorization: Bearer <access_token>` của BE hiện tại. Không nhận `userId` từ FE. Timestamp trả về UTC, tên thuộc tính JSON dùng camelCase.

| Method | Endpoint | Kết quả |
| --- | --- | --- |
| GET | `/api/notifications?pageIndex=1&pageSize=20&unreadOnly=false` | Danh sách của user hiện tại, mới nhất trước; pageSize 1–100 |
| GET | `/api/notifications/unread-count` | `{ "unreadCount": 3 }` |
| PATCH | `/api/notifications/{id}/read` | `{ "updatedCount": 1, "unreadCount": 2 }`; không cần body |
| PATCH | `/api/notifications/read-all` | Số bản ghi được cập nhật và badge còn lại; không cần body |
| GET | `/api/notifications/push/config` | `{ "enabled": true, "publicKey": "..." }`; khi tắt, publicKey là null |
| POST | `/api/notifications/push/subscriptions` | HTTP 201, `{ "id": 10 }`; đăng ký lại trả cùng id |
| DELETE | `/api/notifications/push/subscriptions/{id}` | HTTP 204; lặp lại với đăng ký của mình vẫn 204 |

Một item trong danh sách:

```json
{
  "id": 123,
  "type": "comment",
  "title": "New comment",
  "message": "Someone commented on your post.",
  "targetUrl": "/posts/8",
  "isRead": false,
  "createdAt": "2026-10-05T09:00:00Z",
  "readAt": null
}
```

Response phân trang dùng `PagedResult<NotificationDto>` hiện có: `items`, `pageIndex`, `pageSize`, `totalItems`, `totalCount`, `totalPages`. `type` gồm `comment`, `comment_reply`, `post_like`, `new_follower`, `followed_post`, `post_review`, `meal_reminder`. Không trả userId, eventKey, khóa push hoặc endpoint của trình duyệt trong danh sách.

Gọi đọc lại không thay đổi readAt lần đầu. ID không tồn tại hoặc thuộc tài khoản khác đều trả 404. Read-all chỉ áp dụng cho các thông báo đến ranh giới lúc bắt đầu thao tác; nếu có thông báo mới sau đó thì unreadCount có thể lớn hơn 0. FE dùng unreadCount trong response để cập nhật badge.

Body đăng ký push là dữ liệu từ `PushSubscription.toJSON()`:

```json
{
  "endpoint": "https://fcm.googleapis.com/fcm/send/EXAMPLE",
  "keys": { "p256dh": "BASE64URL_PUBLIC_POINT", "auth": "BASE64URL_AUTH_SECRET" }
}
```

Sai body/key/endpoint trả 400. Push chưa bật trả 409. Mỗi user tối đa 20 đăng ký đang hoạt động. Hỗ trợ endpoint HTTPS chuẩn của FCM, Mozilla, Apple và Windows Push; cấm endpoint tùy ý hoặc loopback. Một endpoint đang hoạt động không thể bị tài khoản khác chiếm. Sau khi chủ cũ hủy đăng ký, chủ mới có thể đăng ký endpoint đó.

## Cấu hình và migration

Mặc định `WebPush:Enabled=false`: BE vẫn dùng thông báo trong app sau khi migration được áp dụng. Các cấu hình JWT/database hiện có vẫn cần được cung cấp cho worktree mới; secret và dữ liệu local không được sao chép vào Git.

Migration mới `20261005163153_AddNotificationsAndWebPush` thêm ba bảng, không bỏ hoặc thay thế bảng nghiệp vụ cũ:

- `notifications`: thông báo và trạng thái đọc; khóa duy nhất `(user_id, event_key)` chống tạo trùng.
- `browser_push_subscriptions`: endpoint và khóa mã hóa của từng trình duyệt.
- `notification_push_deliveries`: outbox, số lần gửi, trạng thái và lease của worker.

Ba bảng bật RLS và thu hồi quyền của PUBLIC, anon, authenticated. FE truy cập qua API .NET/JWT, không đọc trực tiếp bằng Supabase Data API. Kết nối BE cần role database phù hợp với mô hình hiện tại; đây không phải chuyển sang Supabase Auth.

Trước khi chạy trên database chung, kiểm tra các nhánh đã hợp nhất và migration lịch sử, sao lưu database rồi áp dụng migration một lần từ đúng thư mục BE:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --migrate
```

Không tự chạy migration khi BE khởi động bình thường. Không dùng `--seed` để thử thông báo trên database chung.

Để bật web push, tạo một cặp VAPID riêng cho môi trường. Lệnh sau chỉ tạo khóa và không cần kết nối database; output có private key nên chỉ chạy trong terminal của bạn, không dán vào chat/log hoặc commit:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --generate-vapid-keys
```

Đặt cấu hình trong user-secrets, biến môi trường hoặc file `supabase.local.json` đã gitignore, ví dụ cấu trúc:

```json
{
  "WebPush": {
    "Enabled": true,
    "Subject": "mailto:YOUR_CONTACT_EMAIL",
    "PublicKey": "YOUR_VAPID_PUBLIC_KEY",
    "PrivateKey": "YOUR_VAPID_PRIVATE_KEY"
  }
}
```

Biến môi trường tương ứng: `WebPush__Enabled`, `WebPush__Subject`, `WebPush__PublicKey`, `WebPush__PrivateKey`. Các máy cùng môi trường dùng cùng cặp khóa; chỉ publicKey được trả cho FE. Khi enabled=true mà khóa thiếu/sai/không khớp, BE báo lỗi cấu hình khi startup. Không bật push trước khi chuẩn bị storage job.

Hangfire dùng PostgreSQL, mặc định cùng `DefaultConnection`. Có thể cung cấp `ConnectionStrings:HangfireConnection` riêng. Dùng direct connection hoặc Supabase **Session pooler port 5432**, không dùng Transaction pooler 6543 vì worker có khóa theo session. Sau khi cấu hình khóa hợp lệ, chuẩn bị schema job một lần:

```powershell
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj -- --prepare-push-jobs
dotnet run --project src/VeganHelper.API/VeganHelper.API.csproj
```

`--prepare-push-jobs` tạo schema Hangfire và thu hồi quyền truy cập schema của PUBLIC/anon/authenticated. Startup thường không tự tạo schema. Không mở Hangfire dashboard công khai. Khi WebPush tắt, Hangfire server không khởi động; các delivery chưa gửi vẫn được lưu trong database.

## Nối các module phát sinh sự kiện

Inject `INotificationPublisher` tại service của module nguồn. Publisher truy vấn bản ghi đã lưu để lấy chủ sở hữu/trạng thái, không cho FE chọn người nhận hoặc gửi nội dung tùy ý.

```csharp
await publisher.NotifyCommentAsync(commentId, cancellationToken);
await publisher.NotifyPostReviewAsync(postId, cancellationToken);
await publisher.NotifyMealReminderAsync(scheduleId, localDate, cancellationToken);
```

- Comment: CreateCommentAsync đã gọi sau SaveChanges và trước commit; chỉ comment visible trên bài published, chưa xóa. Chủ bài tự comment không nhận thông báo. Dedup bằng commentId.
- Duyệt bài: gọi khi quyết định duyệt/từ chối được lưu/commit; bài published/rejected cần UpdatedAt. Không gọi hook khi chỉ đọc/sửa thông tin bài. Dedup bằng postId/status/UpdatedAt của quyết định.
- Nhắc thực đơn: scheduler FN26 gọi với ngày tại múi giờ của người dùng. Hiện quy ước DayOfWeek ISO 1=Thứ Hai, 7=Chủ Nhật; chỉ plan saved, có owner, không template và ngày nằm trong StartDate/EndDate. Dedup bằng scheduleId/ngày. Scheduler/thời điểm nhắc chưa được bổ sung ở task này.

Nếu module nguồn cần bảo đảm không mất sự kiện khi process chết ngay sau commit, module đó phải lưu sự kiện vào outbox của nó trong cùng transaction rồi gọi publisher bằng job retry. Transaction của publisher bảo đảm notification và các delivery được lưu cùng nhau; khi có ambient transaction của module nguồn, publisher tham gia transaction đó. Publisher tham gia transaction đang mở trên cùng DbContext; module nguồn gọi trước commit để lưu thông báo nguyên tử với hành động.

## Worker và FE web push

Hangfire gọi dispatcher mỗi phút. Mỗi lượt nhận tối đa 20 delivery với `FOR UPDATE SKIP LOCKED`, lease 10 phút và token riêng. Worker chết thì delivery được nhận lại sau lease; token cũ không thể ghi nhận kết quả cho lượt mới. Tối đa 5 lần gửi mỗi delivery; lỗi mạng/408/429/5xx lùi 30, 120, 600, 1800 giây. 404/410 chuyển expired và vô hiệu hóa đăng ký; lỗi 4xx khác chuyển failed. Nếu process chết ở lần cuối, lease hết hạn sẽ chuyển failed. Delivery failed cần xử lý vận hành, không tự retry vô hạn. Theo dõi log lỗi và các hàng failed, bao gồm `lease_exhausted`.

Push gửi qua `Lib.Net.Http.WebPush` với VAPID và mã hóa aes128gcm. Nội dung thông báo dùng thông điệp chung. Không log endpoint/khóa. Gửi push có thể lặp khi kết quả mạng không rõ; payload có tag `notification-{id}` để service worker thay thế cùng thông báo. TTL là 1 giờ. Hủy đăng ký không thể thu hồi push đã gửi hoặc đã bắt đầu gửi.

FE redesign hiện chưa nối module này. Cần:

1. Gọi list/unread-count khi đăng nhập; refresh định kỳ hoặc sau thao tác. Task này không thêm SignalR/Supabase Realtime.
2. Khi click, gọi PATCH read rồi chuyển targetUrl; read-all dùng badge server trả về.
3. Trên HTTPS hoặc localhost, đăng ký service worker; chỉ xin quyền Notification khi người dùng chủ động bật. Lấy publicKey từ push/config, dùng PushManager.subscribe và POST toJSON(), lưu subscription id theo tài khoản/trình duyệt.
4. Khi logout/tắt push, DELETE subscription trước khi xóa JWT, sau đó gọi browser subscription.unsubscribe(). Nếu DELETE thất bại, vẫn unsubscribe phía trình duyệt và xử lý đăng ký mới khi đăng nhập lại.
5. Service worker nhận push, showNotification với title/body/tag và xử lý notificationclick/url cùng origin. Nếu push bị từ chối, tiếp tục dùng thông báo trong app.

## Kiểm chứng

Unit test kiểm tra DTO mapping, ownership, endpoint/key validation, publisher và trạng thái retry. Integration test dùng PostgreSQL thật, migration, HTTP/JWT, dedup đồng thời, read idempotency, đổi tài khoản sau unsubscribe, lease recovery/final failure, quyền bảng, VAPID/mã hóa và lưu job Hangfire. HTTP push được thay bằng handler test, không gửi ra dịch vụ bên ngoài.

Kết quả ngày 2026-10-14: **241 unit test và 292 integration test đạt, không skipped**. API build: 0 warning, 0 error. Đã áp dụng `20261014060145_AddSocialNotifications` lên Supabase project `brjltkherhhqqkryhsuq` sau khi được phép; BE khởi động và Swagger/API status trả HTTP 200. Code đang là thay đổi local chưa commit/push trên `developer` tại thư mục D:.

Các test mới bao phủ like/unlike/re-like, concurrent like/follow, root/reply recipient, hidden comment, quyền công tắc, no backfill/edit events, audience capture cạnh tranh với tắt, batch/concurrent workers, hide→approve khi worker đã claim, in-flight push, rollback nguyên tử và RLS. HTTP push trong test được thay bằng mock, không gửi đến nhà cung cấp thật.

Test chỉ chấp nhận database local có tên bắt đầu bằng `member2_test` hoặc `veganhelper_test`; không dùng Supabase chung. PostgreSQL test cần locale Unicode (ví dụ ICU en-US); locale `C` của bản portable Windows không hỗ trợ case-fold tiếng Việt và làm các test tìm kiếm/dị ứng cũ thất bại.

```powershell
$env:VEGANHELPER_MEMBER2_TEST_POSTGRES='Host=127.0.0.1;Port=55439;Database=member2_test_notifications_icu;Username=postgres'
$env:VEGANHELPER_TEST_POSTGRES='Host=127.0.0.1;Port=55439;Database=veganhelper_test_notifications_posts_icu;Username=postgres'
dotnet test VeganHelper.sln --logger trx --results-directory .local-data/fn36-fn37/test-results
```

Các port/database ở ví dụ là môi trường kiểm tra của task, không phải cấu hình BE hằng ngày. Test engine phải đang chạy trước khi dùng các biến này. Chưa kiểm chứng push thật trên trình duyệt vì FE nằm ngoài phạm vi và WebPush/VAPID vẫn cần cấu hình. In-app notifications hoạt động độc lập với browser push.
