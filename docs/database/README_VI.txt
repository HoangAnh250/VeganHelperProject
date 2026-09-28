Mở VeganHelperSystem.drawio trong Draw.io: File > Open From > Device. Có ERD tổng và 5 tab module, chỉnh sửa trực tiếp được. File .mmd là Mermaid dự phòng; .dot/.svg để xem bằng Graphviz hoặc trình duyệt.

Bản cập nhật Sprint 1 có 30 bảng. Đọc CHANGES_VI.md để xem các quyết định và trách nhiệm của backend. DATA_DICTIONARY_VI.md giải thích từng field.

VeganHelperSystem_Full.sql là script tạo database mới, KHÔNG phải migration cho database đã có bảng/dữ liệu. Không chạy trực tiếp lên database đang sử dụng. SQL chưa được thực thi trên SQL Server.

Nhãn 1/N trên Draw.io chỉ thể hiện số lượng tối đa, không thể hiện đầy đủ tính tùy chọn. Mermaid cũng chỉ minh họa quan hệ 1/nhiều trong bộ này. Xem NULL/ràng buộc trong SQL. Bảng được tham chiếu có thể lặp lại giữa các module; database chỉ có một bảng thực tế.
