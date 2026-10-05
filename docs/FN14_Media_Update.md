# FN14 — chỉnh sửa nhiều ảnh của bài viết

BE: nhánh `feature/FN14-update-post`, dựa trên `developer` tại `c9c2da4`.
Worktree riêng: `C:\Users\ADMIN\.codex\worktrees\fn14-media-edit\VeganHelperProject`.
Không thay đổi schema database; không cần migration cho FN14.

## API và cách FE gửi dữ liệu

`GET /api/Posts/{id}` trả mỗi phần tử `media` gồm `id`, `mediaUrl`,
`mediaType`, `isPrimary`, `displayOrder`. Danh sách được sắp theo `displayOrder`.
FE dùng **id của media**, không dùng id bài viết hoặc vị trí trong mảng, để xóa.

`PUT /api/Posts/{id}` dùng JWT của tác giả và `multipart/form-data`.
Giữ các trường bắt buộc `Title`, `Content`, `CategoryId` và các trường nội dung khác
theo DTO hiện có. Ví dụ gửi xóa hai ảnh và thêm hai ảnh:

```javascript
const data = new FormData();
data.append('Title', title);
data.append('Content', content);
data.append('CategoryId', String(categoryId));
removedMediaIds.forEach((id, index) => {
  data.append(`MediaIdsToRemove[${index}]`, String(id));
});
newFiles.forEach(file => data.append('MediaFilesToAdd', file));
// Gửi PUT bằng API client hiện có; để trình duyệt tự đặt multipart boundary.
```

Ảnh cũ không nằm trong `MediaIdsToRemove` được giữ lại; chỉ upload các file mới.
Không gửi lại file cho những ảnh cũ cần giữ. Có thể xóa toàn bộ ảnh cũ khi cùng
request có ảnh mới thay thế. Không truyền hai trường media nếu chỉ sửa nội dung.
Sau khi lưu thành công, FE tải lại detail để lấy ID các ảnh mới.
FE chưa được sửa trong nhánh BE này.

## Quy tắc

- Chỉ tác giả được sửa. ID xóa phải thuộc chính bài viết; ID trùng được xử lý một lần.
- Sau khi sửa phải có từ 1 đến 10 media.
- Ảnh: JPEG, PNG, WebP, tối đa 5 MiB/file. Video hiện có: MP4, MOV, WebM,
  tối đa 200 MiB/file. Kiểm tra đuôi file, MIME và chữ ký header trước khi upload
  bất kỳ file nào trong batch. Đây không phải kiểm tra giải mã toàn bộ file.
- Giữ ảnh bìa cũ nếu không bị xóa. Nếu bị xóa, chọn media còn lại đầu tiên;
  nếu thay hết, chọn file mới đầu tiên. Luôn có một media bìa và thứ tự liên tục từ 0.
- Bài viết trở về `pending_review` sau cập nhật, theo hành vi FN14 hiện có.
- Upload hoặc lưu DB lỗi: cố gắng dọn các file mới đã upload; không xóa file cũ.
  Xóa file cũ trên storage chỉ sau khi commit DB thành công.
- Repository đánh dấu xóa rõ ràng chỉ các quan hệ `NoAction` bị loại bỏ; category,
  ingredient và step được giữ lại vẫn dùng bản ghi/key cũ của luồng Supabase.
  Nếu media bị xóa là nguồn của `post_summaries`, xóa bản tóm tắt phụ thuộc trong
  cùng transaction để không vi phạm foreign key.
- Trong transaction cập nhật, repository bỏ cờ bìa trên DB trước khi lưu bìa cuối cùng
  để tránh unique index `IX_post_media_1` khi EF cập nhật bìa mới trước khi xóa bìa cũ.
  Thay đổi này rollback cùng toàn bộ bài viết nếu lưu lỗi; không đổi schema/index.
- Nếu dọn storage lỗi sau commit, giữ kết quả cập nhật thành công và ghi warning
  để xử lý file dư sau; chưa có cơ chế tự động retry dọn file trong bản sửa này.

## Kiểm tra

Unit test kiểm tra thêm/xóa hàng loạt, thay toàn bộ ảnh, sửa nội dung, ID sai/trùng,
giới hạn số lượng/dung lượng, header sai, upload lỗi giữa batch, lỗi lưu DB/rollback,
lỗi dọn ảnh sau commit và các trường ID/thứ tự trong response detail.
Storage được mock; các test này không upload hoặc xóa dữ liệu trên cloud thật.
Phiên bản hợp nhất kiểm tra EF ChangeTracker với model PostgreSQL, giữ lại key của
category/ingredient không đổi và cập nhật media đúng trạng thái. Có kiểm tra HTTP
multipart trên PostgreSQL thật: giữ/xóa bìa, thay toàn bộ ảnh, thay category/ingredients,
giữ step cũ hoặc thay step, xóa summary phụ thuộc và rollback khi DB từ chối cập nhật.
Cloud storage được thay bằng adapter test, không upload/xóa file thật.

Nguồn `feature/FN14-update-post` vẫn giữ nền SQL Server cũ để bảo toàn lịch sử;
phiên bản tích hợp trên `codex/integrate-be-developer` giữ các sửa Supabase và auth mới.
Sau khi cập nhật checkout developer, khởi động lại BE từ checkout đó để dùng code mới.
Không sao chép connection string/mật khẩu vào repository.
