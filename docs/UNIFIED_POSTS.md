# Bài viết không phân loại

Tất cả bài viết là Post. API tạo/sửa không yêu cầu hoặc sử dụng PostType; API feed, search, chi tiết, bài cá nhân, bài đã lưu, bài của người dùng và danh sách Admin không trả postType.

- Vẫn bắt buộc: tài khoản hợp lệ, tiêu đề, nội dung, danh mục; khi tạo vẫn cần media theo validation hiện có.
- Tùy chọn: difficultyLevel, dietType, prepTimeMins, cookingTimeMins, IngredientsJson và StepsJson. Thời gian là số nguyên không âm; 0 hợp lệ cho món không cần nấu. Các kiểm tra giá trị và quyền sở hữu vẫn áp dụng.
- Với PUT, bỏ các trường độ khó/chế độ ăn/thời gian sẽ xóa giá trị cũ thành null. Gửi IngredientsJson=[] và StepsJson=[] để xóa nguyên liệu/bước nấu; bỏ hai trường JSON này thì BE giữ danh sách cũ.
- FE để trống thông tin nấu ăn nếu chưa chọn. Chi tiết chỉ hiển thị nguyên liệu/bước nấu khi có dữ liệu. Upload media, danh mục, lượt xem, like, thông báo và kiểm duyệt không thay đổi.

## Migration và triển khai

Migration: 20261015155044_RemovePostClassification. Up chỉ bỏ constraint CK_posts_1 và cột posts.post_type. Các bảng post_media, post_ingredients, post_steps, quan hệ danh mục và dữ liệu khác được giữ nguyên. Không sửa migration lịch sử.

Đây là thay đổi schema/API không tương thích với BE cũ: BE cũ vẫn truy vấn post_type. Ngược lại, BE mới không ghi cột này nên tạo bài sẽ lỗi nếu database vẫn yêu cầu post_type NOT NULL. Không chạy lẫn hai phiên bản.

1. Sao lưu database trước khi thay đổi trên Supabase chung; xác nhận kế hoạch triển khai và quyền áp dụng.
2. Chuẩn bị bản BE mới và FE mới đã qua CI. Tạm dừng BE cũ, bao gồm tiến trình local của các thành viên dùng chung database và background worker.
3. Từ đúng bản code mới, cấu hình kết nối qua secret/environment rồi áp dụng migration bằng công cụ EF hoặc lệnh ứng dụng --migrate. Không thêm mật khẩu vào tài liệu/lệnh lưu trong Git.
4. Khởi động BE mới; kiểm tra health/live, tạo/sửa một bài không có thông tin nấu ăn và một bài có công thức, feed, search và Admin.
5. Triển khai FE mới sau BE mới. FE mới không gửi PostType nên sẽ không tạo được bài khi còn kết nối BE cũ.

Không tự động migrate khi BE khởi động. File migration trong repository không đồng nghĩa Supabase đã được cập nhật.

## Rollback

Down khôi phục cột post_type và constraint, gán giá trị community cho tất cả bài hiện có. Không thể khôi phục nhãn recipe/article/video ban đầu từ migration này; cần bản sao lưu nếu muốn giữ nhãn cũ. Down không xóa nội dung hay dữ liệu liên quan. Dừng BE trước khi rollback và khôi phục phiên bản BE/FE tương ứng.

## Kiểm thử

Các regression test HTTP xác nhận tạo bài không có PostType, các trường công thức có thể bỏ trống hoặc lưu/được xóa khi sửa, và thời gian 0 hợp lệ. Kiểm thử migration dùng PostgreSQL cô lập trên máy để xác nhận dữ liệu của cả bốn loại cũ, ảnh, nguyên liệu, bước nấu, trạng thái và lịch sử kiểm duyệt còn nguyên; kiểm tra Down/Up và model snapshot. Không dùng Supabase chung cho bộ test.
