# Chuẩn UI/UX — Bootstrap 5.3

## 1. Layout chung

Thanh bên hoặc navbar gồm:

- Dashboard
- Sản phẩm
- Thuộc tính
- Item/SKU
- Kho hàng
- Tồn kho
- Khách hàng/Đại lý
- Nhập kho
- Xuất kho
- Chuyển kho
- Bán hàng
- Thanh toán
- Báo cáo

Header hiển thị tên hệ thống và người dùng hiện tại.

## 2. Danh sách

Mỗi trang danh sách phải có:

- Tiêu đề.
- Nút thêm mới.
- Ô tìm kiếm.
- Bộ lọc phù hợp.
- Bảng responsive.
- Phân trang.
- Cột thao tác.
- Trạng thái rỗng.
- Thông báo lỗi/thành công.

## 3. Form danh mục

Danh mục như Product, Attribute, Warehouse, Customer:

- Thêm/sửa bằng Bootstrap Modal hoặc trang riêng.
- Validation phía client và server.
- Label rõ ràng.
- Nút Lưu, Hủy.
- Khi lỗi phải giữ dữ liệu người dùng đã nhập.

## 4. Form Item

Form Item phải gồm:

- Product.
- SKU/Code.
- Barcode.
- CostPrice.
- SalePrice.
- Danh sách thuộc tính động.

Cách chọn thuộc tính:

- Hiển thị từng nhóm Attribute.
- Mỗi nhóm chọn tối đa một AttributeValue.
- Người dùng có thể không chọn nhóm không áp dụng.
- Không hard-code Size, Color.

Ví dụ:

```text
Size: [S]
Color: [Đỏ]
Voltage: [Không áp dụng]
```

## 5. Màn hình tồn kho

Phải có hai chế độ:

### Theo sản phẩm

| Sản phẩm | SKU | Thuộc tính | Kho A | Kho B | Kho C | Tổng |
|---|---|---|---:|---:|---:|---:|

Các cột kho được sinh động từ danh sách Warehouses, không hard-code ba tên kho.

### Theo kho

Lọc một kho và hiển thị Item cùng số lượng.

## 6. Màn hình chứng từ

Chứng từ không dùng modal toàn trang.

Phần đầu:

- Số chứng từ.
- Ngày.
- Loại chứng từ.
- Kho nguồn/kho đích.
- Khách hàng nếu áp dụng.
- Ghi chú.

Phần chi tiết:

- Tìm/chọn Product hoặc Item.
- Hiển thị thuộc tính Item.
- Hiển thị tồn hiện tại tại kho xuất.
- Quantity.
- Price.
- Amount.
- Thêm/xóa dòng.
- Tổng tiền.

Nút:

- Lưu nháp.
- Hoàn tất.
- Hủy.
- In nếu áp dụng.

## 7. Trạng thái

- Draft: secondary.
- Completed: success.
- Cancelled: danger.

## 8. In hóa đơn

`Print.cshtml` không dùng layout quản trị.

CSS in:

- A4 mặc định.
- Ẩn nút khi in.
- Font dễ đọc.
- Bảng không vỡ trang tùy tiện.
- Có nút `window.print()`.

## 9. Responsive

- Bảng bọc `table-responsive`.
- Form dùng grid Bootstrap.
- Mobile có thể cuộn ngang bảng.
- Không yêu cầu tối ưu thành app di động hoàn chỉnh.
