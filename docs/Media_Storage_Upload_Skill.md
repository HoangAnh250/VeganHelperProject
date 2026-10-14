# Skill: Media Storage & Upload

This skill defines the SOP for handling file upload, storage, and cleanup for any feature involving images/video in this project — chủ yếu phục vụ Media Gallery của Post (đăng bài nhiều ảnh/video) và avatar user.

## Prerequisites
- **Quyết định nơi lưu file thật:**
  - Local (`wwwroot`) — đơn giản, đủ cho demo đồ án, không cần tài khoản cloud.
  - Cloud (Azure Blob Storage / Cloudinary) — nếu muốn deploy thật hoặc lưu trữ bền hơn khi restart server.
- Dù chọn cách nào, code nghiệp vụ (Service) không được biết chi tiết implementation — luôn thao tác qua 1 interface chung để đổi provider sau này không phải sửa Service.
- Hiểu đúng cấu trúc bảng `post_media` (`media_type`, `url`, `thumbnail_url`, `duration_seconds`, `display_order`) — `display_order = 0` luôn là ảnh bìa (cover), đúng theo kiến trúc Media Gallery đã chốt.

## Step 1: Định nghĩa Upload DTO
Navigate to `src/VeganHelper.API/DTOs/`.
- Vì Upload Modal gửi cả text (title, category) lẫn nhiều file cùng lúc, dùng `[FromForm]` với `IFormFileCollection`, KHÔNG dùng DTO JSON thường như các form khác:
```csharp
public class CreatePostDto {
    public string Title { get; set; }
    public int CategoryId { get; set; }
    public List<int> IngredientIds { get; set; }
    public IFormFileCollection Files { get; set; }  // thứ tự trong list = display_order
}
```
- Tạo riêng `UploadAvatarDto` đơn giản hơn (chỉ 1 `IFormFile`) — không dùng chung DTO với Post vì logic khác hẳn (không có gallery, không có thứ tự).

## Step 2: Validate File TRƯỚC KHI upload (fail fast)
- **Whitelist extension + MIME type:** ảnh (`.jpg`, `.png`, `.webp`), video (`.mp4`, `.mov`, `.webm`) — không tin vào đuôi file, kiểm tra cả ContentType thật.
- **Giới hạn dung lượng riêng theo loại:** ảnh ≤ 5MB, video ≤ 200MB (điều chỉnh theo hạ tầng thật có).
- **Giới hạn số lượng media/post** (vd tối đa 10 item) — khớp với UI lưới thumbnail đã dựng ở Modal Upload.
- Reject NGAY ở Service, trả lỗi rõ ràng qua `ValidationException`, TRƯỚC KHI gọi bất kỳ lệnh ghi file nào — tránh upload dở dang rồi mới báo lỗi.

## Step 3: Implement Storage Client (abstraction)
Navigate to `src/VeganHelper.API/Integrations/` (cùng chỗ với `IGooglePlacesClient`, `ILlmClient` đã có).
- Tạo `IMediaStorageClient` với 3 method tối thiểu: `UploadAsync(file) → url`, `DeleteAsync(url)`, `GetPublicUrl(path)`.
- Viết 2 implementation tuỳ môi trường: `LocalDiskStorageClient` (ghi vào `wwwroot/uploads/`, trả về relative URL) và `CloudStorageClient` (nếu dùng Azure Blob/Cloudinary).
- Service nghiệp vụ (`IPostPublishingService`) chỉ inject `IMediaStorageClient`, không bao giờ tự viết `File.WriteAllBytes` hay gọi SDK cloud trực tiếp.

## Step 4: Xử lý riêng cho Video (bắt buộc chạy nền)
- Video cần 2 thứ ảnh không có: `duration_seconds` và `thumbnail_url` (ảnh đại diện trước khi bấm Play) — 2 việc này tốn thời gian xử lý (FFmpeg/FFprobe), không được làm đồng bộ trong request vì sẽ treo HTTP request của user.
- Sau khi file video upload xong (chỉ lưu file thô), đẩy 1 job vào Background Job Processing (skill riêng): trích `duration_seconds`, sinh `thumbnail_url`, update lại đúng dòng `post_media` khi xong.
- Trong lúc job chạy, UI hiển thị trạng thái "đang xử lý" (khung xám + spinner) thay vì chờ trắng màn hình — khớp đúng UX đã thống nhất khi review Stitch.
- Sau khi có `duration_seconds`, TRIGGER tiếp job `video_summarize_jobs` (Speech-to-Text + Summarization) nếu tính năng Auto-Summarize đang bật.

## Step 5: Ghi vào Database — nằm trong CÙNG transaction với Post
- **Thứ tự bắt buộc:** upload file lên storage TRƯỚC (lấy được URL) → rồi mới insert `post_media` — không insert DB trước rồi upload sau (sẽ tạo record trỏ tới file chưa tồn tại).
- Việc insert `posts` + nhiều dòng `post_media` + nhiều dòng `post_ingredients` phải nằm trong 1 transaction duy nhất (đúng lỗ hổng đã nêu ở skill 3-layer trước đó) — nếu bất kỳ bước nào lỗi giữa chừng, toàn bộ rollback.
- **Compensating action bắt buộc:** nếu file đã upload lên storage thành công nhưng transaction DB sau đó fail (rollback), phải có bước dọn lại — gọi `DeleteAsync` xoá các file vừa upload đó, tránh rác mồ côi (orphaned file) tồn tại trên storage mà không có DB record nào trỏ tới.
- `display_order` lấy đúng theo thứ tự index trong `IFormFileCollection` mà Frontend gửi lên — Frontend đã tự sắp xếp qua kéo-thả, Backend chỉ cần lưu nguyên thứ tự đó.

## Step 6: Xử lý Xoá (Cleanup)
- Khi user xoá 1 media trong lúc Edit, hoặc xoá cả bài viết: xoá row `post_media` VÀ gọi `IMediaStorageClient.DeleteAsync()` xoá file thật — thiếu bước 2 sẽ để lại rác chiếm dung lượng vĩnh viễn.
- Thao tác xoá file phải idempotent (gọi xoá file không tồn tại không được ném lỗi làm fail cả transaction) — vì có thể job cleanup chạy lại hoặc user bấm xoá 2 lần do mạng chậm.
- Nên có thêm 1 Background Job định kỳ (vd chạy hàng đêm) quét storage tìm file không có `post_media` nào trỏ tới (orphaned files) để dọn — làm lưới an toàn cho các trường hợp Step 5.3/Step 6.1 lỡ sót.

## Step 7: Serving Media
- Nếu dùng local storage: cấu hình Static File Middleware trong `Program.cs` (`app.UseStaticFiles()`), thêm cache header hợp lý cho ảnh/video vì chúng không đổi sau khi upload.
- Nếu dùng cloud: trả thẳng URL/CDN link từ provider, KHÔNG proxy file qua backend (tốn băng thông + chậm không cần thiết).
- Không bao giờ để lộ credential/API key của storage provider ở phía Frontend — mọi lệnh upload đều đi qua Backend endpoint, Frontend không gọi thẳng cloud storage.

## Final Step: Verification
- Test upload nhiều file trộn ảnh/video cùng lúc → đúng thứ tự `display_order`, cover đúng item đầu tiên.
- Test upload file sai định dạng/quá dung lượng → bị reject TRƯỚC khi có file nào được ghi lên storage.
- Test xoá 1 post có 5 media → xác nhận cả 5 file thật đã biến mất khỏi storage, không chỉ mất row DB.
- Test giả lập DB transaction fail sau khi file đã upload → xác nhận file được dọn lại (không mồ côi).
