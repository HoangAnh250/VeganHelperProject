# FN26 — Khảo sát dữ liệu trước khi triển khai

Ngày kiểm tra: 12/10/2026. Nhánh BE: `feature/meal-plan`, tạo từ `developer` tại `ae94011`. Không sửa FE, không viết code ứng dụng, không commit/push.

## Dữ liệu đã lấy

File nguồn và kết quả xử lý nằm trong `.local-data/meal-plan-sources/`, ngoài Git. `source-manifest.json` lưu URL, phiên bản và SHA-256 của từng file.

| Nguồn | Phiên bản | Bản ghi thực phẩm | Trạng thái |
|---|---|---:|---|
| USDA Foundation Foods | 30/04/2026, JSON | 363 | Tải và đọc được; mảng nguồn có thêm 32 phần tử null |
| USDA SR Legacy | 04/2018, JSON | 7.793 | Tải và đọc được |
| USDA FNDDS | 2021–2023, phát hành 31/10/2024, JSON | 5.432 | Tải và đọc được |
| Việt Nam SMILING | 2013, XLSX | 164 | Tải từ FAO; quyền tái sử dụng trong ứng dụng chưa xác minh |
| Bảng thành phần thực phẩm Việt Nam | 2017 | — | FAO ghi sách in; chưa có file dữ liệu chính thức tải được |

USDA tải từ https://fdc.nal.usda.gov/download-datasets/; giấy phép CC0 theo https://fdc.nal.usda.gov/api-guide/.

Bản Việt Nam 2013: https://www.fao.org/food-composition/tables-and-databases/detail/(vietnam--2013)-smiling-food-composition-table-for-vietnam/en. Đây không phải bản 2017. Trang thư viện Viện Dinh dưỡng tìm thấy nhưng truy cập hết thời gian chờ; không dùng bản sao từ bên thứ ba để thay thế.

`usda-food-reference.csv` có 13.588 khóa nguồn duy nhất, 13.545 dòng có energy/kcal và 43 dòng thiếu. Không đổi thiếu dữ liệu thành 0. Lưu nutrient ID của energy: ưu tiên 2048, rồi 2047, rồi 1008 khi đơn vị nguồn là kcal; các phương pháp này không được coi là hoàn toàn tương đương.

CSV là dữ liệu tham chiếu thô, chứa cả thực phẩm động vật. Chưa đánh giá vegan, dị ứng, chuyển tên tiếng Việt hoặc tương đương nguyên liệu. Không dùng trực tiếp để sinh thực đơn. Các biến thể sống/chín/khô phải giữ riêng. Giá trị nutrient thiếu không được suy đoán.

`vietnam-smiling-2013-raw.csv` là xuất nguyên sheet `WP3 FCT`; giữ header và đơn vị nguồn, chưa hợp nhất với USDA. Không dùng chỉ số ENERGY trước khi xác minh đơn vị và phương pháp tính.

## Nguồn combo/thực đơn có bằng chứng chuyên môn

| Nguồn | Bằng chứng | Giới hạn hiện tại |
|---|---|---|
| PCRM Universal Meals | Trang chính thức nêu công thức do chuyên gia ẩm thực phát triển theo hướng dẫn nhóm chuyên gia dinh dưỡng/sức khỏe | Có công thức, chưa phải template 7 ngày được duyệt riêng cho dự án; cần làm rõ quyền nhập/tái xuất bản |
| PCRM 21-Day Vegan Kickstart | Có lịch mẫu 3 tuần và tài liệu chương trình nêu sự tham gia của chuyên gia dinh dưỡng | Không gán tên reviewer cho từng combo nếu nguồn không ghi; chưa xác minh quyền sao chép vào ứng dụng |
| Eat This Much — mẫu vegan 7 ngày | Trang ghi người review là Taylor Berggren, MS, RDN | Điều khoản hạn chế việc tổng hợp dữ liệu cho sản phẩm khác; chưa nhập nội dung |

Nguồn:
- https://www.pcrm.org/universalmeals/recipes
- https://www.pcrm.org/universalmeals
- https://www.pcrm.org/disclaimer
- https://www.pcrm.org/vegankickstart/week1
- https://www.pcrm.org/sites/default/files/2019-07/2018-PCRM-Annual-Report.pdf
- https://www.eatthismuch.com/diet-plan/vegan
- https://www.eatthismuch.com/terms

Không coi mọi công thức từ tổ chức dinh dưỡng là đã được chuyên gia xác nhận riêng. Đổi món, nguyên liệu, khẩu phần hoặc bỏ bữa phụ khỏi thực đơn nguồn tạo ra bản mới, không được giữ nguyên nhãn đã thẩm định. Ví dụ thực đơn Eat This Much có bữa phụ, trong khi schema hiện chỉ cho breakfast/lunch/dinner; không bỏ bữa phụ rồi giữ tổng calo gốc.

## Supabase và điều kiện nhập

Đã đọc trong transaction READ ONLY: `ingredients` có 21 dòng; `meals`, `meal_plans`, `meal_plan_schedule` đều có 0 dòng. Không ghi vào Supabase trong đợt khảo sát này.

Schema `ingredients` hiện chỉ có id/name/default_unit/calories_per_100g. Không có cột nguồn, phiên bản, trạng thái thẩm định hoặc nhóm dị ứng. Vì đang ở giai đoạn không code, chưa thêm schema hay migration để lưu những thông tin này. Hồ sơ nguồn được giữ local.

Người dùng xác nhận chưa có tài liệu combo/thực đơn đã thẩm định và quyền sử dụng. Vì vậy chưa nhập các combo vào `meals`/`meal_plans`.

Trước khi nhập: chọn nguyên liệu phù hợp từ nguồn USDA, xác minh trạng thái chế biến và phép quy đổi khẩu phần; chốt cách giữ provenance; xác minh quyền sử dụng nguồn combo và ghi bằng chứng chuyên môn chính xác. Chỉ nhập vào bảng nghiệp vụ sau khi dữ liệu vượt qua kiểm tra này, không ghi đè 21 nguyên liệu đang có và không tự tạo nhãn chuyên gia.

## Cập nhật sau khảo sát — 12/10/2026

Theo yêu cầu tiếp theo của người dùng, đã tạo và nhập dữ liệu mẫu tự biên soạn, mỗi bữa chỉ có một món. Đây không phải thực đơn được chuyên gia thẩm định hoặc nội dung sao chép từ các nguồn thực đơn ở trên. Năng lượng nguyên liệu tham chiếu USDA SR Legacy 2018; hồ sơ nguồn và giới hạn phép tính được lưu trong [meal-plan-seed/README.md](data/meal-plan-seed/README.md).

Đợt nhập thêm 90 nguyên liệu, tái sử dụng 11 nguyên liệu khớp có sẵn; thêm 200 món (140 vegan, 60 lacto-ovo vegetarian), 1.144 liên kết nguyên liệu, 30 thực đơn mẫu (20 vegan, 10 lacto-ovo vegetarian) và 630 bữa theo lịch. Không ghi đè dữ liệu cũ hoặc sửa schema. Các con số 21 nguyên liệu và 0 món ở mục trên là trạng thái tại thời điểm khảo sát, trước đợt nhập này.

Kiểm tra sau nhập xác nhận đầy đủ 21 bữa cho mỗi thực đơn, không trùng tên món, không sai loại chế độ ăn, không chênh lệch phép tính calories. Chạy lại bản nhập thử tạo thêm 0 dòng. Các mẫu đều ghi rõ chưa được chuyên gia thẩm định; không thay đổi FE hoặc code ứng dụng, không commit/push.
