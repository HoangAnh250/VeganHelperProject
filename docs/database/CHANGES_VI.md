# Các thay đổi đã thống nhất

## Tài khoản và hồ sơ

- Bỏ user_roles. Thêm users.role_id NOT NULL, FK tới roles: một role có nhiều user, một user chỉ có một role. Backend gán role member khi đăng ký; không cho client tự chọn admin.
- user_profiles giữ birth_date và biological_sex cho phép NULL, chỉ phục vụ hồ sơ. Không gửi hai thông tin này/tuổi cho AI tạo thực đơn.
- Bỏ health_goal và activity_level khỏi user_profiles.
- Giữ diet_type: vegan hoặc lacto_ovo_vegetarian. NVARCHAR(30) để đủ độ dài tên giá trị.

## Thực đơn ăn chay

- Bỏ age_years, biological_sex, health_goal, activity_level, meals_per_day, daily_calorie_target khỏi meal_plans.
- Giữ chiều cao/cân nặng/BMI tại thời điểm tạo, diet_type, dị ứng và nguyên liệu snapshot. Đây không phải chương trình đặt mục tiêu giảm cân.
- meals độc lập với bài đăng: bỏ meals.post_id. Giữ owner_user_id tùy chọn từ bản trước; nếu không có món cá nhân, có thể để NULL.
- meals.diet_type bắt buộc để phân loại món dùng trong gợi ý. Lacto-ovo có thể nhận món vegan; vegan không nhận món có trứng/sữa. Dị ứng lọc riêng bằng nguyên liệu, không suy ra từ diet_type.
- Bỏ slot_number trong meal_plan_schedule; meal_time chỉ gồm breakfast, lunch, dinner. UNIQUE(meal_plan_id, day_of_week, meal_time).
- Backend kiểm tra đủ 21 mục trước khi đánh dấu thực đơn completed/saved, trong cùng giao dịch ghi dữ liệu. UNIQUE không tự bảo đảm đủ 21 mục.
- Backend chỉ cho AI chọn meal_id hợp lệ từ danh sách món trong database đã được lọc. Kiểm tra lại quyền truy cập món, nguyên liệu/dị ứng và kiểu ăn chay khi lưu. Không cần RAG để làm luồng này.

## RAG dự phòng

- Giữ post_embeddings cho hướng phát triển. MVP có thể để trống và không gọi embedding API.

## AI đánh dấu bài ngoài chủ đề

- flags thêm source_type (user/ai), ai_model_name (NULL với báo cáo người dùng); reporter_id cho phép NULL khi nguồn AI.
- CHECK bảo đảm báo cáo user có reporter_id, không có ai_model_name; flag AI không có reporter_id, có ai_model_name và chỉ nhắm đến post_id.
- Tận dụng reason, status và các field xử lý admin có sẵn. Không thêm bảng kiểm duyệt riêng.
- ai_usage.feature_type thêm content_moderation. Lượt kiểm tra bài gắn với tác giả qua user_id để theo dõi sử dụng.
- Quy trình backend: gửi bài -> pending_review (không công khai) -> gọi AI. Phù hợp: published. Ngoài chủ đề/chưa chắc: lưu flag và giữ pending_review. API lỗi: chưa công khai, thử lại hoặc admin duyệt. Admin chấp nhận: published và flag dismissed; admin xác nhận vi phạm: rejected và flag resolved. Cập nhật bài và flag trong cùng giao dịch.
- Quy trình trên chưa phải code ứng dụng; SQL chỉ lưu trạng thái và ràng buộc. Không thay is_deleted để biểu diễn bài chờ duyệt.
- Backend xử lý bất đồng bộ phải đối chiếu phiên bản nội dung, không áp dụng kết quả AI cũ cho bài đã sửa. Có thể dùng updated_at với thao tác cập nhật có điều kiện; mọi sửa nội dung phải cập nhật updated_at. Chỉ công khai phiên bản đã được duyệt.
- Nội dung chỉ gửi text thì chỉ được xem là kiểm tra text. Muốn kiểm tra ảnh/video phải bổ sung đầu vào media/transcript. Nội dung bài là dữ liệu không tin cậy, không phải chỉ dẫn cho AI.

## Phạm vi file

- 27 bảng, bản SQL tạo mới, không phải migration. Chưa thực thi lên SQL Server hay gọi API AI.
- Giữ nguyên file nguồn và bộ Draw.io cũ; bộ này nằm trong thư mục phiên bản riêng.
- Không thay các field khác chưa thống nhất, ví dụ context_summary và summary_through_message_id vẫn được giữ như bản trước.
