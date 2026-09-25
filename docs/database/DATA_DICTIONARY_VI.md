# Từ điển dữ liệu VeganHelperSystem

Bản đã chỉnh theo thống nhất: 27 bảng, 40 khóa ngoại. Dựa trên 24 bảng nguồn, thêm 4 bảng và thay user_roles bằng users.role_id.

Mỗi field ghi rõ kiểu, khả năng NULL, khóa và ý nghĩa. CHECK/DEFAULT/INDEX xem SQL đi kèm. SQL là bản tạo mới, không phải migration cho database có dữ liệu.

## users

Tài khoản ứng dụng; hỗ trợ username/password và đăng nhập Google, có xóa mềm.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh tài khoản. |
| username | NVARCHAR(100) | UNIQUE | Không | Tên đăng nhập; backend chuẩn hóa và kiểm tra trùng không phân biệt hoa/thường. |
| email | NVARCHAR(255) | UNIQUE | Không | Email; backend chuẩn hóa trước khi lưu. |
| password_hash | NVARCHAR(500) |  | Có | Hash từ thư viện mật khẩu; NULL khi chỉ đăng nhập Google. |
| email_verified_at | DATETIME2 |  | Có | Thời điểm email được xác minh; không thay thế cơ chế token xác minh. |
| is_active | BIT |  | Không | Cho phép tài khoản hoạt động. |
| created_at | DATETIME2 |  | Không | Thời điểm tạo UTC. |
| updated_at | DATETIME2 |  | Có | Thời điểm cập nhật; backend cập nhật. |
| last_login_at | DATETIME2 |  | Có | Lần đăng nhập thành công gần nhất. |
| deleted_at | DATETIME2 |  | Có | Thời điểm xóa mềm tài khoản. |
| role_id | INT | FK | Không | Vai trò duy nhất của tài khoản; FK tới roles. |

- FK: users(role_id) → roles(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## user_profiles

Hồ sơ người dùng; ngày sinh và giới tính không bắt buộc, chỉ dùng hiển thị hồ sơ, không dùng sinh thực đơn.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| user_id | BIGINT | PK, FK | Không | Tài khoản sở hữu; đồng thời là PK/FK. |
| display_name | NVARCHAR(100) |  | Không | Tên hiển thị. |
| avatar_url | NVARCHAR(1000) |  | Có | URL ảnh đại diện. |
| height_cm | DECIMAL(5,2) |  | Có | Chiều cao, đơn vị cm; đổi tên height để rõ đơn vị. |
| weight_kg | DECIMAL(6,2) |  | Có | Cân nặng, đơn vị kg; đổi tên weight để rõ đơn vị. |
| birth_date | DATE |  | Có | Ngày sinh; dùng để tính tuổi theo thời điểm yêu cầu. |
| biological_sex | NVARCHAR(20) |  | Có | Giá trị người dùng cung cấp khi cần tính toán. |
| diet_type | NVARCHAR(30) |  | Không | vegan: thuần chay; lacto_ovo_vegetarian: có thể dùng trứng/sữa. Lọc dị ứng độc lập. |
| updated_at | DATETIME2 |  | Có | Thời điểm cập nhật hồ sơ. |

- FK: user_profiles(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 hoặc 1 dòng con.

## roles

Danh mục quyền: chỉ Member và Administrator; không thêm Chef.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | INT | PK, IDENTITY | Không | Định danh vai trò. |
| role_name | NVARCHAR(50) | UNIQUE | Không | member hoặc admin. |

## user_identities

Bổ sung để liên kết tài khoản Google bằng provider subject ổn định.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh liên kết. |
| user_id | BIGINT | FK | Không | Tài khoản nội bộ. |
| provider | NVARCHAR(20) |  | Không | Nhà cung cấp đăng nhập ngoài. |
| provider_subject | NVARCHAR(255) |  | Không | Google sub đã được backend xác minh. |
| created_at | DATETIME2 |  | Không | Thời điểm liên kết. |

- FK: user_identities(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- UNIQUE ghép: provider, provider_subject.

## posts

Bài blog/video/công thức/cộng đồng. Không khôi phục bảng recipes của thiết kế cũ.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh bài đăng. |
| author_id | BIGINT | FK | Không | Tác giả. |
| post_type | NVARCHAR(20) |  | Không | article, video, recipe hoặc community. |
| title | NVARCHAR(255) |  | Không | Tiêu đề. |
| content | NVARCHAR(MAX) |  | Có | Nội dung bài. |
| meal_type | NVARCHAR(20) |  | Có | Loại bữa phù hợp: breakfast, lunch, dinner, snack. |
| prep_time_mins | INT |  | Có | Phút chuẩn bị, không âm. |
| cooking_time_mins | INT |  | Có | Phút nấu, không âm. |
| servings | INT |  | Có | Số khẩu phần của công thức. |
| calories_per_serving | DECIMAL(10,2) |  | Có | Kcal trên một khẩu phần, không phải cả công thức. |
| diet_type | NVARCHAR(30) |  | Có | vegan: thuần chay; lacto_ovo_vegetarian: có thể dùng trứng/sữa. Lọc dị ứng độc lập. |
| ingredients_verified | BIT |  | Không | Danh sách nguyên liệu đã được kiểm tra theo quy trình nhóm; không phải bảo đảm y tế. |
| status | NVARCHAR(20) |  | Không | Trạng thái xuất bản/kiểm duyệt. |
| view_count | BIGINT |  | Không | Lượt xem do backend cập nhật. |
| created_at | DATETIME2 |  | Không | Thời điểm tạo. |
| updated_at | DATETIME2 |  | Có | Chuẩn hóa tên update_at trong hình thành updated_at. |
| is_deleted | BIT |  | Không | Cờ xóa mềm; cần đồng bộ với chỉ mục RAG. |
| deleted_at | DATETIME2 |  | Có | Thời điểm xóa mềm. |

- FK: posts(author_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## post_media

Một file ảnh/video đính kèm; URL và một cloud_public_id theo lựa chọn trước đó.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh media. |
| post_id | BIGINT | FK | Không | Bài sở hữu file. |
| media_url | NVARCHAR(1000) |  | Không | URL phát/hiển thị file. |
| media_type | NVARCHAR(20) |  | Không | image hoặc video. |
| cloud_public_id | NVARCHAR(255) |  | Có | Public ID Cloudinary; file cũ hoặc ngoài hệ thống có thể NULL. |
| thumbnail_url | NVARCHAR(1000) |  | Có | URL ảnh xem trước. |
| duration_seconds | INT |  | Có | Thời lượng video tính bằng giây. |
| is_primary | BIT |  | Không | Media đại diện của bài. |
| display_order | INT |  | Không | Thứ tự hiển thị. |
| processing_status | NVARCHAR(20) |  | Không | uploading, processing, ready, failed. |
| created_at | DATETIME2 |  | Không | Thời điểm tạo. |

- FK: post_media(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- UNIQUE ghép: post_id, id.

- UNIQUE có điều kiện: post_id WHERE is_primary = 1.

## post_summaries

Một kết quả tóm tắt hiện hành trên mỗi bài; xác định rõ video nguồn và lưu transcript.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| post_id | BIGINT | PK, FK | Không | Bài được tóm tắt; PK/FK. |
| source_media_id | BIGINT | FK | Không | Video nguồn thuộc chính bài này. |
| transcript | NVARCHAR(MAX) |  | Có | Văn bản tiếng nói được nhận dạng; hỗ trợ xem/copy transcript. |
| ai_generated_text | NVARCHAR(MAX) |  | Có | Tóm tắt các bước thực hiện. |
| status | NVARCHAR(20) |  | Không | pending, processing, completed, failed, stale. |
| model_name | NVARCHAR(100) |  | Có | Model/phiên bản tóm tắt. |
| error_message | NVARCHAR(2000) |  | Có | Lỗi xử lý để backend/admin theo dõi. |
| requested_at | DATETIME2 |  | Không | Thời điểm yêu cầu xử lý. |
| generated_at | DATETIME2 |  | Có | Thời điểm hoàn tất; chưa hoàn tất thì NULL. |

- FK: post_summaries(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 hoặc 1 dòng con.

- FK: post_summaries(post_id, source_media_id) → post_media(post_id, id). Mỗi dòng con: 1 cha; mỗi cha: 0 hoặc 1 dòng con.

## categories

Danh mục cho bài hoặc quán; loại con cho món ăn/công thức/chủ đề.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | INT | PK, IDENTITY | Không | Định danh danh mục. |
| name | NVARCHAR(100) |  | Không | Tên hiển thị. |
| slug | NVARCHAR(120) | UNIQUE | Không | Tên URL duy nhất. |
| category_type | NVARCHAR(20) |  | Không | post hoặc shop theo nguồn. |
| post_category_kind | NVARCHAR(20) |  | Có | food, recipe, topic khi category_type=post. |
| is_active | BIT |  | Không | Danh mục đang sử dụng. |

- UNIQUE ghép: category_type, name.

## post_categories

Bảng nối bài và danh mục; chuẩn hóa tên POST_CATEGORY trên hình.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| post_id | BIGINT | PK, FK | Không | Bài đăng. |
| category_id | INT | PK, FK | Không | Danh mục bài. |

- FK: post_categories(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: post_categories(category_id) → categories(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## ingredients

Danh mục nguyên liệu; dùng DECIMAL thay FLOAT cho số lượng/dinh dưỡng.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh nguyên liệu. |
| name | NVARCHAR(100) | UNIQUE | Không | Tên nguyên liệu. |
| default_unit | NVARCHAR(20) |  | Không | Đơn vị mặc định. |
| calories_per_100g | DECIMAL(10,2) |  | Có | Kcal/100g; không dùng trực tiếp cho đơn vị củ/cốc nếu chưa quy đổi. |

## post_ingredients

Nguyên liệu và lượng dùng của toàn công thức trong bài.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| post_id | BIGINT | PK, FK | Không | Bài đăng. |
| ingredient_id | BIGINT | PK, FK | Không | Nguyên liệu. |
| quantity | DECIMAL(12,3) |  | Có | Lượng dùng; NULL khi chưa định lượng. |
| unit | NVARCHAR(20) |  | Không | Đơn vị cụ thể, tránh phụ thuộc default_unit về sau. |

- FK: post_ingredients(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: post_ingredients(ingredient_id) → ingredients(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## user_allergies

Giữ mô hình dị ứng theo ingredient_id của nguồn mới; không dùng lại allergies của DB cũ.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| user_id | BIGINT | PK, FK | Không | Người dùng. |
| ingredient_id | BIGINT | PK, FK | Không | Nguyên liệu người dùng khai báo dị ứng. |
| created_at | DATETIME2 |  | Không | Thời điểm khai báo. |

- FK: user_allergies(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: user_allergies(ingredient_id) → ingredients(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## user_available_ingredients

Bổ sung cho tính năng sử dụng nguyên liệu sẵn có.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| user_id | BIGINT | PK, FK | Không | Chủ kho nguyên liệu. |
| ingredient_id | BIGINT | PK, FK | Không | Nguyên liệu hiện có. |
| quantity | DECIMAL(12,3) |  | Có | Số lượng hiện có. |
| unit | NVARCHAR(20) |  | Không | Đơn vị lượng hiện có. |
| updated_at | DATETIME2 |  | Không | Thời điểm cập nhật kho. |

- FK: user_available_ingredients(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: user_available_ingredients(ingredient_id) → ingredients(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## post_likes

Giữ like theo quyết định trước đó và bảng POST_LIKES trong file, không tự thêm downvote.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| user_id | BIGINT | PK, FK | Không | Người thích. |
| post_id | BIGINT | PK, FK | Không | Bài được thích. |
| created_at | DATETIME2 |  | Không | Thời điểm thích. |

- FK: post_likes(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: post_likes(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## saved_posts

Bài được đánh dấu xem lại.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| user_id | BIGINT | PK, FK | Không | Người lưu. |
| post_id | BIGINT | PK, FK | Không | Bài đã lưu. |
| saved_at | DATETIME2 |  | Không | Thời điểm lưu. |

- FK: saved_posts(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: saved_posts(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## comments

Bình luận lồng nhau; FK ghép ép bình luận cha thuộc cùng bài.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh bình luận. |
| post_id | BIGINT | FK | Không | Bài được bình luận. |
| user_id | BIGINT | FK | Không | Tác giả bình luận. |
| parent_comment_id | BIGINT | FK | Có | Bình luận cha nếu là trả lời. |
| content | NVARCHAR(1000) |  | Không | Nội dung bình luận. |
| status | NVARCHAR(20) |  | Không | visible hoặc hidden, độc lập với xóa mềm. |
| created_at | DATETIME2 |  | Không | Thời điểm tạo. |
| updated_at | DATETIME2 |  | Có | Thời điểm sửa. |
| is_deleted | BIT |  | Không | Cờ xóa mềm. |
| deleted_at | DATETIME2 |  | Có | Thời điểm xóa mềm. |

- FK: comments(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: comments(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: comments(post_id, parent_comment_id) → comments(post_id, id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

- UNIQUE ghép: post_id, id.

## meal_plans

Thực đơn ăn chay 7 ngày, cố định 3 bữa mỗi ngày; lưu đầu vào BMI, kiểu ăn chay, dị ứng và nguyên liệu. Không đặt mục tiêu giảm cân hoặc năng lượng.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh thực đơn/lần sinh. |
| user_id | BIGINT | FK | Không | Chủ sở hữu. |
| start_date | DATE |  | Không | Ngày bắt đầu tuần. |
| end_date | DATE |  | Không | Ngày cuối tuần; cách start_date 6 ngày. |
| height_cm | DECIMAL(5,2) |  | Không | Chiều cao tại lần tạo. |
| weight_kg | DECIMAL(6,2) |  | Không | Cân nặng tại lần tạo. |
| bmi_value | DECIMAL(6,2) |  | Không | BMI được backend tính từ đầu vào. |
| diet_type | NVARCHAR(30) |  | Không | vegan: thuần chay; lacto_ovo_vegetarian: có thể dùng trứng/sữa. Lọc dị ứng độc lập. |
| allergies_snapshot | NVARCHAR(MAX) |  | Không | JSON array nguyên liệu dị ứng tại lần tạo, gồm ID/tên. |
| available_ingredients_snapshot | NVARCHAR(MAX) |  | Không | JSON array nguyên liệu/số lượng/đơn vị đã gửi AI. |
| generation_source | NVARCHAR(20) |  | Không | ai hoặc manual. |
| model_name | NVARCHAR(100) |  | Có | Model sinh thực đơn nếu có. |
| status | NVARCHAR(20) |  | Không | pending, generating, completed, failed, saved, archived. |
| error_message | NVARCHAR(2000) |  | Có | Lỗi sinh hoặc kiểm tra đầu ra. |
| created_at | DATETIME2 |  | Không | Thời điểm tạo yêu cầu. |
| updated_at | DATETIME2 |  | Có | Thời điểm cập nhật. |
| saved_at | DATETIME2 |  | Có | Thời điểm người dùng lưu. |

- FK: meal_plans(user_id) → users(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## meals

Danh mục món/combo trong database để gợi ý thực đơn, độc lập với bài đăng. owner_user_id chỉ dùng nếu có món cá nhân.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh món/combo. |
| owner_user_id | BIGINT | FK | Có | Người sở hữu món AI riêng; NULL cho món chung đã được quản lý. |
| combo_name | NVARCHAR(255) |  | Không | Tên món/combo. |
| description | NVARCHAR(MAX) |  | Có | Mô tả và cách thực hiện ngắn. |
| servings | DECIMAL(6,2) |  | Không | Số khẩu phần mà total_calories và lượng nguyên liệu đang mô tả. |
| total_calories | DECIMAL(10,2) |  | Có | Kcal cho toàn bộ combo với servings này. |
| diet_type | NVARCHAR(30) |  | Không | vegan: thuần chay; lacto_ovo_vegetarian: có thể dùng trứng/sữa. Lọc dị ứng độc lập. |
| created_at | DATETIME2 |  | Không | Thời điểm tạo. |

- FK: meals(owner_user_id) → users(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

## meal_plan_schedule

Sửa PK để cùng một món có thể lặp trong tuần; thay end_date ở từng ô bằng day_of_week và meal_time.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh ô lịch, không dùng cặp plan/meal làm PK. |
| meal_plan_id | BIGINT | FK | Không | Thực đơn sở hữu ô lịch. |
| meal_id | BIGINT | FK | Không | Món/combo được xếp lịch. |
| day_of_week | TINYINT |  | Không | Vị trí ngày 1–7 tính từ start_date của thực đơn. |
| meal_time | NVARCHAR(20) |  | Không | Bữa cố định: breakfast, lunch hoặc dinner; backend kiểm tra đủ 21 bữa khi hoàn tất tuần. |
| portion_multiplier | DECIMAL(6,2) |  | Không | Hệ số nhân cho toàn bộ khẩu phần/lượng nguyên liệu/kcal của meals. |

- FK: meal_plan_schedule(meal_plan_id) → meal_plans(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: meal_plan_schedule(meal_id) → meals(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- UNIQUE ghép: meal_plan_id, day_of_week, meal_time.

## meal_ingredients

Nguyên liệu cụ thể của combo; không phụ thuộc công thức gốc có bị sửa hay không.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| meal_id | BIGINT | PK, FK | Không | Combo/món. |
| ingredient_id | BIGINT | PK, FK | Không | Nguyên liệu. |
| calculated_quantity | DECIMAL(12,3) |  | Có | Lượng nguyên liệu cho combo gốc; NULL khi chưa định lượng. |
| unit | NVARCHAR(20) |  | Không | Đơn vị đi kèm lượng dùng. |

- FK: meal_ingredients(meal_id) → meals(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: meal_ingredients(ingredient_id) → ingredients(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## chat_sessions

Phiên chat cho thành viên hoặc khách, kèm context summary và mốc tóm tắt.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh hội thoại. |
| user_id | BIGINT | FK | Có | Tài khoản; NULL khi khách chưa đăng nhập. |
| guest_session_id | UNIQUEIDENTIFIER |  | Có | Mã phiên khách do backend cấp; không phải secret dùng để bỏ qua xác thực. |
| context_summary | NVARCHAR(MAX) |  | Có | Nội dung tóm tắt phần hội thoại cũ. |
| summary_through_message_id | BIGINT |  | Có | Mốc tin đã tóm tắt, backend xác minh cùng phiên; không tạo FK vòng. |
| started_at | DATETIME2 |  | Không | Thời điểm bắt đầu. |
| last_activity_at | DATETIME2 |  | Không | Thời điểm hoạt động gần nhất. |

- FK: chat_sessions(user_id) → users(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

## chat_messages

Tin nhắn trong một cuộc chat; giữ nguyên lịch sử khi tạo summary.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh tin nhắn. |
| session_id | BIGINT | FK | Không | Hội thoại sở hữu. |
| sender_type | NVARCHAR(20) |  | Không | user hoặc ai theo nguồn. |
| message_text | NVARCHAR(MAX) |  | Không | Nội dung tin. |
| created_at | DATETIME2 |  | Không | Thời điểm tạo. |

- FK: chat_messages(session_id) → chat_sessions(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## ai_usage

Bổ sung để theo dõi một lần gọi AI và áp dụng giới hạn dùng thử phía backend.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh bản ghi sử dụng. |
| request_id | UNIQUEIDENTIFIER | UNIQUE | Không | Mã request hỗ trợ chống ghi nhận trùng. |
| user_id | BIGINT | FK | Có | Người dùng đã đăng nhập. |
| guest_session_id | UNIQUEIDENTIFIER |  | Có | Phiên khách nếu có. |
| feature_type | NVARCHAR(30) |  | Không | chatbot, meal_planner, video_summary, embedding, chat_summary, content_moderation. |
| provider | NVARCHAR(50) |  | Không | Ví dụ gemini hoặc ollama. |
| model_name | NVARCHAR(100) |  | Không | Model được gọi. |
| status | NVARCHAR(20) |  | Không | pending, completed hoặc failed. |
| input_tokens | INT |  | Có | Token đầu vào khi nhà cung cấp trả về. |
| output_tokens | INT |  | Có | Token đầu ra. |
| created_at | DATETIME2 |  | Không | Thời điểm ghi nhận request. |

- FK: ai_usage(user_id) → users(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

## post_embeddings

Dự phòng phát triển RAG/tìm kiếm vector; có thể để trống trong MVP, không bắt buộc gọi embedding API.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh đoạn. |
| post_id | BIGINT | FK | Không | Bài nguồn. |
| chunk_index | INT |  | Không | Thứ tự đoạn, bắt đầu từ 0. |
| vector_id | NVARCHAR(255) |  | Có | ID ngoài SQL, chưa có khi chờ tạo. |
| content_hash | VARCHAR(64) |  | Không | Hash nội dung tạo embedding. |
| embedding_model | NVARCHAR(100) |  | Không | Model/phiên bản embedding. |
| status | NVARCHAR(20) |  | Không | pending, processing, completed, failed, stale. |
| embedded_at | DATETIME2 |  | Có | Thời điểm tạo thành công. |

- FK: post_embeddings(post_id) → posts(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- UNIQUE ghép: post_id, chunk_index.

## shops

Quán chay được gợi ý/kiểm duyệt; có Google Place ID cho tích hợp bản đồ.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh quán. |
| suggested_by_user_id | BIGINT | FK | Có | Người đề xuất; NULL nếu nhập từ dịch vụ/hệ thống. |
| google_place_id | NVARCHAR(255) |  | Có | Google Place ID; unique khi có giá trị. |
| name | NVARCHAR(255) |  | Không | Tên quán. |
| address | NVARCHAR(1000) |  | Có | Địa chỉ. |
| latitude | DECIMAL(10,7) |  | Có | Vĩ độ. |
| longitude | DECIMAL(10,7) |  | Có | Kinh độ. |
| is_approved | BIT |  | Không | Đã được duyệt để hiển thị theo luồng sản phẩm. |
| is_deleted | BIT |  | Không | Cờ xóa mềm. |
| created_at | DATETIME2 |  | Không | Thời điểm thêm địa điểm. |

- FK: shops(suggested_by_user_id) → users(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

- UNIQUE có điều kiện: google_place_id WHERE google_place_id IS NOT NULL.

## shop_categories

Danh mục quán phục vụ tìm kiếm theo loại món.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| shop_id | BIGINT | PK, FK | Không | Quán. |
| category_id | INT | PK, FK | Không | Danh mục dành cho quán. |

- FK: shop_categories(shop_id) → shops(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: shop_categories(category_id) → categories(id). Mỗi dòng con: 1 cha; mỗi cha: 0 đến nhiều dòng con.

## flags

Báo cáo từ người dùng hoặc AI. AI đánh dấu bài ngoài chủ đề; bài giữ pending_review, admin quyết định công khai hay từ chối.

| Field | Kiểu | Khóa | NULL? | Ý nghĩa |
|---|---|---|---|---|
| id | BIGINT | PK, IDENTITY | Không | Định danh báo cáo. |
| reporter_id | BIGINT | FK | Có | Người báo cáo; bắt buộc với source_type=user, NULL với source_type=ai. |
| post_id | BIGINT | FK | Có | Bài bị báo cáo, nếu có. |
| comment_id | BIGINT | FK | Có | Bình luận bị báo cáo, nếu có. |
| shop_id | BIGINT | FK | Có | Quán bị báo cáo, nếu có. |
| reason | NVARCHAR(500) |  | Không | Lý do. |
| status | NVARCHAR(20) |  | Không | pending, resolved hoặc dismissed. |
| resolved_by_admin_id | BIGINT | FK | Có | Người xử lý; backend phải kiểm tra quyền admin. |
| resolution_note | NVARCHAR(2000) |  | Có | Ghi chú xử lý. |
| created_at | DATETIME2 |  | Không | Thời điểm báo cáo. |
| resolved_at | DATETIME2 |  | Có | Thời điểm xử lý. |
| source_type | NVARCHAR(10) |  | Không | Nguồn báo cáo: user hoặc ai. |
| ai_model_name | NVARCHAR(100) |  | Có | Model tạo flag AI; NULL với báo cáo từ người dùng. |

- FK: flags(reporter_id) → users(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: flags(post_id) → posts(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: flags(comment_id) → comments(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: flags(shop_id) → shops(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.

- FK: flags(resolved_by_admin_id) → users(id). Mỗi dòng con: 0 hoặc 1 cha; mỗi cha: 0 đến nhiều dòng con.
