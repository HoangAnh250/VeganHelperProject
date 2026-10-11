# Công khai bài mới/bản sửa trước, kiểm tra AI sau và report bài viết

Trạng thái: người dùng đã duyệt; BE được giữ trên nhánh feature/reports. Theo yêu cầu mới, thay đổi FE đã revert, giao diện do thành viên khác thực hiện; các yêu cầu UI bên dưới là contract bàn giao. Migration mới chưa áp dụng database chung; chưa commit/push.

Yêu cầu bổ sung đã triển khai: Gemini chỉ trả JSON `{"result":"PASS|FAIL","reason":"..."}`; lý do tối đa 200 ký tự. BE kiểm tra schema/enum/độ dài và ánh xạ trạng thái, không yêu cầu confidence hoặc findings từ AI. Confidence mới là null; bằng chứng nội dung do snapshot DB cung cấp. FAIL bao gồm nghi vấn cần Admin xem lại; lỗi API/schema/response incomplete là lỗi kiểm tra, không phải FAIL. Giảm suy luận bằng thinking_level=low, không trả thought summaries, giới hạn tổng generation 2048 token để chừa chỗ cho xử lý ảnh.

## Mục tiêu và quyết định đã xác nhận

- Khi đăng bài mới thành công, bài xuất hiện ngay trên Home/Feed.
- Khi sửa bài đang công khai thành công, bản sửa tiếp tục công khai ngay và được AI kiểm tra lại.
- Gemini kiểm tra nội dung và ảnh bất đồng bộ sau khi đăng.
- Nếu AI đánh giá phù hợp, bài tiếp tục công khai; worker chuyển sang bài khác.
- Nếu AI gắn cờ vi phạm hoặc nghi vấn, tạm ngừng công khai bài, lưu lý do và thông báo tác giả.
- Admin quyết định cuối cùng việc công khai lại. AI không được tự phục hồi bài đã bị gắn cờ, kể cả lần quét bổ sung đánh giá an toàn.
- Hết quota, timeout và lỗi tải ảnh là lỗi kiểm tra, không phải bằng chứng vi phạm; chúng không tự làm bài bị ẩn.
- Người dùng đã đăng nhập có thể report bài của người khác. Report không tự ẩn bài; nó ưu tiên kiểm tra AI và đưa vụ việc cho Admin xử lý.
- Tác giả sửa bài đã bị AI/Admin tạm ẩn: bản sửa vẫn không công khai, AI kiểm tra hỗ trợ và Admin quyết định cuối cùng.

## Phạm vi

Thay đổi FN13, FN14 và xử lý nền FN41–FN43, bổ sung report bài viết, kết nối thông báo FN36–FN37. FN08/FN11/FN12 tiếp tục dựa vào trạng thái công khai để quyết định khả năng hiển thị. FN15 và bảo vệ phiên bản nội dung giữ nguyên. Report chưa được gán mã FN mới trong backlog; không tự đặt một mã chính thức.

Phân biệt sửa bài đang công khai với sửa bài đang bị giữ để kiểm duyệt. Các bài legacy pending_review/rejected/hidden không tự trở thành published chỉ vì lưu bản sửa. Không dùng nội dung safe của lần quét mới để bỏ một lệnh giữ cần Admin.

## Hai trạng thái độc lập

Trạng thái bài quyết định có công khai hay không; trạng thái lần quét chỉ phản ánh tiến độ kiểm tra.

| Tình huống | Trạng thái bài | Trạng thái quét | Công khai |
|---|---|---|---|
| Bài mới vừa lưu | published | queued | Có |
| Worker đang kiểm tra bài mới | published | processing | Có |
| AI đánh giá an toàn | published | safe | Có |
| Lỗi quota/mạng, chờ thử lại | published | queued | Có |
| Không thể quét hoặc hết lượt thử | published | manual_required/failed | Có; không được ghi là đã kiểm tra an toàn |
| AI gắn cờ bài mới | pending_review | flagged | Không |
| Admin giữ bài | published | Giữ lịch sử quét/quyết định | Có |
| Admin gỡ bài | hidden | Giữ lịch sử quét/quyết định | Không |
| Tác giả sửa bài đang published | published | queued của phiên bản mới | Có |
| Tác giả sửa bài đã bị giữ/ẩn | pending_review | queued của phiên bản mới | Không; Admin quyết định |
| User report bài published | published | Ưu tiên kiểm tra phiên bản hiện tại | Có, trừ khi AI/Admin quyết định giữ/ẩn |

Tái sử dụng pending_review cho bài bị AI tạm giữ giúp giữ contract trạng thái hiện có. DTO Admin/My Posts cần giải thích nguyên nhân giữ, report chưa giải quyết và phiên bản hiện tại; không đồng nhất report chưa xác minh với vi phạm đã xác nhận.

## Luồng bài mới

1. FE gửi multipart; BE xác thực JWT, validate và xử lý storage theo pattern hiện có.
2. BE lưu bài published, các quan hệ và hàng chờ quét đúng contentRevision trong cùng transaction.
3. Chỉ trả thành công sau khi commit. FE báo đăng thành công và tải lại danh sách khi điều hướng tới Feed/My Posts.
4. Worker nhận lượt quét published hoặc pending_review hợp lệ, lấy dữ liệu đúng phiên bản và gọi Gemini ngoài transaction khóa.
5. Khi kết thúc, BE kiểm tra lại lease, contentRevision, bài chưa xóa và chưa có quyết định Admin làm lượt quét hết hiệu lực.
6. Nếu safe: chỉ ghi kết quả quét; bài mới vẫn published. Không tạo thông báo "được duyệt" cho bài vốn đã công khai.
7. Nếu flagged/uncertain: cùng transaction chuyển bài sang pending_review, lưu flag/reason/findings, lưu sự kiện tạm giữ và notification/outbox.
8. Feed, tìm kiếm và bộ lọc loại bài này khỏi các response tiếp theo. API chi tiết public cũng không trả nội dung bị tạm giữ; tác giả và API Admin vẫn xem được.
9. Admin mở hàng cờ, xem nội dung/ảnh, lý do AI và đúng phiên bản bài.
10. Admin nhập lý do, gửi keep/remove kèm expectedRevision. keep công khai lại; remove chuyển hidden. Quyết định, giải quyết flag, audit và thông báo commit cùng nhau.

## Thông báo và FE

- Bài mới: "Đăng bài thành công"; không dùng thông báo mặc định "đang chờ duyệt".
- Bản sửa của bài công khai: "Cập nhật bài thành công". Bản sửa của bài bị giữ: "Đã gửi bản sửa để kiểm tra lại".
- AI tạm giữ: "Bài đăng của bạn đang được tạm ẩn để kiểm tra lại", kèm lý do AI và liên kết tới bài trong khu vực cá nhân.
- Admin giữ/gỡ: thông báo kết quả với lý do của Admin.
- My Posts cần hiển thị bài đang tạm giữ; FE chưa nối thông báo hiện tại nên tích hợp list/badge/read là phần cần thiết nếu muốn tác giả nhận thông báo ngay trên giao diện.
- Bài đang mở trên một tab không tự biến mất chỉ vì DB đổi trạng thái. Đợt này tối thiểu cập nhật khi tải lại/gọi API; không tuyên bố realtime khi chưa có polling hoặc realtime được triển khai.

## FN14 và bảo vệ quyết định Admin

1. Tác giả mở bài, thay nội dung/media rồi lưu; BE kiểm tra sở hữu và validate theo FN14 hiện có.
2. BE khóa/kiểm tra lại trạng thái hiện tại khi lưu: nếu AI/Admin vừa giữ bài trong lúc tác giả sửa, request không được dùng trạng thái published cũ để công khai lại.
3. Tăng contentRevision và tạo lượt kiểm tra cho bản sửa. Nếu bài đang published và không có lệnh giữ đang hiệu lực, giữ published. Nếu bài đang bị giữ/ẩn/từ chối, chuyển bản sửa sang pending_review và giữ ràng buộc Admin quyết định.
4. Lưu media/nội dung/hàng chờ trong cùng transaction. Kết quả AI trước lần sửa không được áp dụng vào phiên bản mới.
5. Giữ lịch sử flag/report và lý do giữ cũ để Admin hiểu vì sao bản sửa cần xem. Không xóa lịch sử hoặc tự giải quyết report chỉ vì revision đổi.
6. AI safe không tự công khai bản sửa đang cần Admin. Admin xem bản sửa mới nhất, kiểm tra expectedRevision và đưa quyết định.

## Report bài viết

### Người gửi report

1. Member đã đăng nhập mở bài published của người khác và chọn Report.
2. FE mở form chọn loại lý do và nhập mô tả; gửi postId cùng phiên bản nội dung mà người dùng đang xem. ReporterId lấy từ JWT, không lấy từ body.
3. BE kiểm tra bài còn tồn tại/công khai, người gửi không phải tác giả, lý do hợp lệ và giới hạn tần suất. Nếu phiên bản đã đổi, yêu cầu tải lại thay vì âm thầm gắn report vào nội dung mới chưa xem.
4. Lưu flag source_type=user, reporter_id, post_revision, reason và status=pending. Dedupe report chưa giải quyết của cùng người/bài/phiên bản bằng bảo vệ database; nhiều người vẫn có thể report cùng bài.
5. Trả xác nhận đã nhận report. Bài tiếp tục published. Không thông báo cho tác giả rằng bài vi phạm chỉ vì một người vừa report.
6. Đưa report vào hàng chờ Admin và ưu tiên AI kiểm tra lại phiên bản hiện tại, kể cả phiên bản từng được đánh giá safe. Nhiều report không tạo nhiều request AI giống nhau hoặc reset vô hạn ngân sách retry. Lưu lịch sử lần kiểm tra trước khi xử lý lần kiểm tra bổ sung.

### AI và Admin xử lý report

- AI flagged/uncertain: tạm giữ bài và thông báo tác giả theo luồng kiểm tra nội dung; liên kết các bằng chứng AI với report cho Admin xem.
- AI safe: bài tiếp tục công khai, nhưng report vẫn cần Admin quyết định; AI không tự dismiss report người dùng.
- AI hết quota/lỗi: bài giữ công khai nếu chưa có lệnh giữ; Admin xử lý report độc lập với AI.
- Admin xem danh sách/chi tiết report, các lý do, nội dung của phiên bản bị report và phiên bản hiện tại. Không hiển thị danh tính reporter cho tác giả hoặc người dùng khác.
- Admin dismiss khi report không có cơ sở: giải quyết report, giữ trạng thái bài phù hợp; không tự phục hồi một bài đang có lý do giữ khác.
- Admin remove khi nội dung không phù hợp: chuyển bài hidden, giải quyết report, lưu quyết định/audit và thông báo tác giả cùng transaction.
- Nếu bài đã bị AI giữ và Admin xác nhận phù hợp, Admin có thể giữ/công khai lại bản hiện tại, giải quyết các flag liên quan và ghi rõ lý do.
- Report một revision cũ không tự chứng minh revision mới vi phạm. Quyết định thay đổi trạng thái phải kiểm tra expectedRevision hiện tại; lịch sử report cũ vẫn giữ để xem xét.

### API và dữ liệu dự kiến

- POST /api/posts/{id}/reports: tạo report của Member.
- GET /api/admin/moderation/reports và GET /api/admin/moderation/reports/{id}: đọc hàng đợi/chi tiết theo quyền Admin.
- POST /api/admin/moderation/reports/{id}/decision: quyết định cùng lý do và expectedRevision.
- Tận dụng bảng flags đã có ReporterId và source_type=user; migration chỉ bổ sung ràng buộc/index và dữ liệu phục vụ lịch sử/bảo vệ luồng nếu cần. Controller/Service/DTO/Repository riêng theo pattern dự án.
- Không biến endpoint ai-flags hiện có thành hàng report người dùng mà vẫn giữ tên/contract gây nhầm lẫn.

## Quota và AI không khả dụng

Giữ bài published trong lúc chờ kiểm tra. Không bỏ qua ảnh để ghi kết quả safe. Lỗi API không tạo flag vi phạm. Tất cả lần quét chưa hoàn thành vẫn có trạng thái vận hành để theo dõi.

Đã triển khai cooldown dùng chung trong PostgreSQL cho lỗi 429: chờ Retry-After (giới hạn 1 phút–24 giờ) hoặc 15 phút, không tiêu retry budget bài; worker tiếp tục sau cooldown. Lỗi transient khác vẫn giới hạn 5 lượt. Đây là xử lý độ tin cậy, không tăng quota thực tế của Gemini.

## Cấu hình và tương thích

- ModerationAi:Enabled tiếp tục bật/tắt worker.
- Toggle AutoPublishEnabled cũ không còn điều khiển bài mới/bản sửa công khai ngay. Cần ngừng sử dụng contract này có chủ đích; không âm thầm đổi nó thành quyền AI phục hồi bài bị giữ. Các trường hợp giữ đều cần Admin.
- Kế hoạch phải lưu/nhận biết lệnh giữ độc lập với contentRevision để sửa bài không vượt kiểm duyệt.
- Không tự công khai hàng loạt bài pending_review/rejected/hidden cũ khi triển khai. Bài legacy và các lượt quét cũ cần được giữ nguyên hoặc backfill theo lý do/trạng thái rõ ràng.
- Giữ DTO/service/repository, JWT, RLS và audit hiện có. Không chuyển kiểm tra hoặc lưu API key sang FE.

## Nhất quán và trường hợp đồng thời

- Kết quả quét bản cũ không thay đổi trạng thái bản mới.
- Kết quả đến sau khi bài bị xóa không hồi sinh bài.
- Quyết định Admin làm lượt quét tương ứng hết hiệu lực; kết quả đến muộn không ghi đè quyết định đó.
- Flag và thông báo tạm giữ chống trùng theo bài/phiên bản; worker retry không tạo nhiều thông báo giống nhau.
- Hỏng transaction tạm giữ không được chỉ lưu flag mà để trạng thái/notification lệch nhau.

## Tiêu chí kiểm chứng

1. Tạo bài mới: API thành công, Feed/detail public thấy bài ngay trước khi AI chạy.
2. AI safe: bài vẫn công khai, kết quả được lưu, không phát thông báo duyệt thừa.
3. AI flagged/uncertain: bài bị loại khỏi Feed/detail public; tác giả vẫn xem được, Admin có lý do và notification đúng một lần.
4. Quota/timeout/ảnh không tải được: không ẩn bài, không ghi safe, hàng chờ được theo dõi/thử lại theo chính sách.
5. Admin keep/remove: phục hồi hoặc giữ ẩn đúng trạng thái, có audit và thông báo.
6. Sửa/xóa/duyệt đồng thời với worker: không áp dụng kết quả cũ hoặc hồi sinh bài đã xử lý.
7. Không công khai nhầm bài legacy đang chờ, bị từ chối hoặc bị ẩn.
8. Regression PostgreSQL cho Sprint 1, Admin và thông báo; FE test nội dung phản hồi đăng bài và hiển thị trạng thái/thông báo.
9. Sửa bài published vẫn công khai; sửa bài bị giữ vẫn chờ Admin; chạy đua sửa với AI/Admin giữ không làm bài công khai lại.
10. Report cần login, không tự report, không lộ reporter, validate reason/revision và bảo vệ chống trùng/spam.
11. Report không tự ẩn bài; ưu tiên kiểm tra kể cả scan safe trước đó; nhiều report không nhân bản lời gọi AI.
12. Admin xử lý report vẫn được khi AI hết quota. AI safe không tự dismiss report; quyết định Admin ghi audit/notification đúng transaction.

## Giới hạn được chấp nhận

Bài chưa được AI kiểm tra vẫn công khai. Khi hết quota hoặc worker dừng, khoảng thời gian công khai trước khi kiểm tra có thể kéo dài. Nếu nội dung thực sự không phù hợp, hệ thống chỉ tạm giữ sau khi AI/Admin phát hiện, không bảo đảm ngăn xuất hiện ngay từ đầu.
