# Validation, transaction và bảo mật

## 1. Validation chung

- ID phải tồn tại.
- Text trim trước khi lưu.
- Code chuyển uppercase nếu phù hợp.
- Decimal không âm trừ Adjustment Quantity.
- Date không để mặc định không hợp lệ.
- Danh sách chi tiết không rỗng.
- Không chấp nhận Item trùng dòng; gộp hoặc báo lỗi.

## 2. Validation tồn kho

Tại thời điểm Complete, service phải đọc lại tồn kho từ database.

Không dựa vào số tồn hiển thị trước đó trên trình duyệt.

## 3. Transaction

Mọi nghiệp vụ nhiều bảng phải dùng transaction.

Ví dụ Sale:

```text
StockDocument
StockDocumentDetails
WarehouseStocks
Customers
Payments
```

Tất cả thành công hoặc tất cả rollback.

## 4. Concurrency

Bản tối thiểu có thể dựa trên transaction và kiểm tra tồn ngay trước khi trừ.

Khuyến nghị thêm `RowVersion` cho `WarehouseStock` nếu Agent triển khai được mà không phá thiết kế. Đây là cột kỹ thuật được phép thêm nếu dùng đúng mục đích concurrency.

## 5. Chống sửa chứng từ

Controller không cho Edit chứng từ Completed.

Service cũng phải kiểm tra lại, không chỉ dựa vào UI.

## 6. Phân quyền

Bản đầu tối thiểu có thể dùng ASP.NET Core Identity với role:

- Admin
- Warehouse
- Sales
- Accounting
- Viewer

Quyền:

- Admin: toàn bộ.
- Warehouse: nhập, xuất, chuyển, tồn.
- Sales: bán hàng, khách hàng, in hóa đơn.
- Accounting: thanh toán, công nợ, báo cáo.
- Viewer: chỉ xem.

Nếu Identity làm tăng phạm vi quá nhiều, Agent có thể hoàn thành nghiệp vụ trước nhưng phải chuẩn bị `[Authorize]` và cấu trúc dễ bổ sung.

## 7. Bảo mật web

- Anti-forgery token cho POST.
- Không tin dữ liệu tính tiền từ client; tính lại server.
- Không đưa exception raw ra UI production.
- Encode output mặc định Razor.
- Không ghép SQL string thủ công.

## 8. Logging

Ghi log lỗi bằng logging mặc định của ASP.NET Core.

Các lỗi transaction phải có message người dùng rõ ràng và log chi tiết phía server.
