# Quy tắc bắt buộc dành cho Agent

## 1. Quy tắc kiến trúc

1. Không đổi tên solution hoặc project.
2. Không tạo project thứ ba.
3. Không triển khai Clean Architecture, CQRS, MediatR, Repository Pattern hoặc Unit of Work.
4. Không tách entity thành nhiều file. Toàn bộ entity database phải nằm trong `Poco.cs`.
5. Không tách service theo module. Tất cả phương thức truy cập dữ liệu nằm trong `WareHouseServices.cs`; có thể tổ chức bằng region để dễ đọc.
6. Controller chỉ điều phối request, validation cơ bản và gọi `WareHouseServices`.
7. Không đặt SQL hoặc truy cập `DbContext` trực tiếp trong Controller.
8. Không dùng SPA. Không React, Angular, Vue hoặc Blazor.
9. Không dùng giao diện ngoài Bootstrap 5.3 nếu không được yêu cầu.

## 2. Quy tắc dữ liệu

1. Không hard-code cột `Size`, `Color`, `Voltage`, `Material` trong `Products` hoặc `Items`.
2. Mọi thuộc tính động phải đi qua:
   - `Attributes`
   - `AttributeValues`
   - `ItemAttributes`
3. `Item` là đơn vị tồn kho duy nhất.
4. Không lưu tồn kho tại `Products`.
5. Không lưu tổng tồn tại `Items`.
6. Tồn kho được lưu tại `WarehouseStocks` theo từng kho và từng Item.
7. Mọi nghiệp vụ làm thay đổi số lượng phải cập nhật `WarehouseStocks` trong cùng transaction với chứng từ.
8. Không cho phép xuất hoặc bán khiến tồn kho âm.
9. Không xóa cứng chứng từ đã hoàn tất. Chứng từ hoàn tất chỉ được hủy bằng trạng thái và nghiệp vụ đảo tồn.
10. Không tự ý thêm bảng mới ngoài đặc tả. Nếu cần dữ liệu trình bày, dùng ViewModel.

## 3. Quy tắc mã nguồn

1. Dùng async/await cho thao tác database.
2. Dùng `decimal` cho tiền và số lượng.
3. Dùng `DateTime` hoặc `DateTimeOffset` nhất quán; ưu tiên `DateTime`.
4. Phương thức Save phải phân biệt thêm mới và cập nhật.
5. Mọi Save/Delete trả về kết quả rõ ràng: thành công, thông báo, dữ liệu ID nếu có.
6. Mọi màn hình danh sách phải có search và pagination.
7. Mọi form phải hiển thị validation.
8. Mọi thao tác nguy hiểm phải có xác nhận.
9. Sau khi hoàn thành module phải build solution.
10. Không để TODO quan trọng trong phần nghiệp vụ cốt lõi.

## 4. Quy tắc giao diện

1. Bootstrap 5.3.
2. Giao diện quản trị thống nhất.
3. Desktop-first nhưng vẫn responsive.
4. Form thêm/sửa danh mục có thể dùng Bootstrap Modal.
5. Chứng từ nhập/xuất/bán/chuyển kho dùng trang riêng, không nhồi toàn bộ vào modal.
6. Sử dụng badge cho trạng thái.
7. Bảng dữ liệu phải có tiêu đề, lọc, phân trang và trạng thái rỗng.
8. Màn hình tồn kho phải hiển thị được:
   - Sản phẩm
   - SKU/Item
   - Thuộc tính động
   - Số lượng tại từng kho
   - Tổng tồn

## 5. Quy tắc hoàn thành

Một module chỉ được coi là hoàn thành khi:

- Build không lỗi.
- Có Controller.
- Có View.
- Có service.
- Có validation.
- Có thông báo thành công/thất bại.
- Có quyền truy cập hợp lý.
- Có tiêu chí nghiệm thu tương ứng trong `13-Acceptance-Criteria.md`.
