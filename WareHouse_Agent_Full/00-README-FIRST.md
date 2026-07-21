# WareHouse Framework — Hướng dẫn bắt đầu cho Agent

## 1. Mục đích bộ tài liệu

Bộ tài liệu này là đặc tả chính thức để Agent xây dựng hệ thống quản lý kho tổng quát. Agent phải đọc toàn bộ tài liệu trước khi tạo hoặc sửa mã nguồn.

Hệ thống phải áp dụng được cho nhiều loại hàng hóa như xe đạp, xe điện, điện máy, thời trang, phụ tùng và thiết bị. Không được thiết kế cứng theo `Size`, `Color` hoặc bất kỳ ngành hàng cụ thể nào.

## 2. Thứ tự đọc bắt buộc

1. `01-Agent-Rules.md`
2. `02-Solution-Architecture.md`
3. `03-Database-Design.md`
4. `04-Data-Access-And-Services.md`
5. `05-UI-UX-Standards.md`
6. `06-Product-Attribute-Item.md`
7. `07-Warehouse-And-Inventory.md`
8. `08-Stock-Document-Flows.md`
9. `09-Sales-Debt-Invoice.md`
10. `10-Dashboard-And-Reports.md`
11. `11-Module-Specifications.md`
12. `12-Validation-Transaction-Security.md`
13. `13-Acceptance-Criteria.md`
14. `99-Agent-Execution-Plan.md`

## 3. Các yêu cầu đã chốt

- Solution: `WareHouse.sln`
- Chỉ có hai project:
  - `WareHouse.Web`
  - `WareHouse.Data`
- Framework: ASP.NET Core MVC trên .NET 10.
- UI: Bootstrap 5.3, Razor View, JavaScript/jQuery.
- Database: SQL Server.
- Tên connection string: `xx`.
- `WareHouse.Data/Poco.cs` chứa toàn bộ entity.
- `WareHouse.Data/WareHouseServices.cs` chứa các thao tác truy cập dữ liệu và nghiệp vụ database.
- Không dùng Repository Pattern.
- Không dùng Unit of Work tự xây.
- Hệ thống có ba kho và phải hỗ trợ thêm kho mới.
- `Product` là sản phẩm gốc.
- `Item` là SKU/tổ hợp thuộc tính thực tế được nhập, xuất và quản lý tồn.
- Thuộc tính phải động thông qua `Attributes`, `AttributeValues`, `ItemAttributes`.
- Tồn kho được lưu theo `WarehouseId + ItemId`.
- Có nhập kho, xuất kho, chuyển kho, bán hàng, trả hàng, điều chỉnh kho.
- Có khách hàng/đại lý, công nợ, thanh toán.
- Có in hóa đơn bán hàng bằng Razor View.
- Có dashboard và báo cáo nhập–xuất–tồn, doanh thu, công nợ.

## 4. Nguyên tắc thực thi

Agent không được bắt đầu bằng việc tự thay đổi thiết kế. Khi tài liệu có điểm chưa được mô tả, Agent phải chọn cách đơn giản nhất phù hợp với kiến trúc hiện tại, không tạo thêm tầng hoặc bảng không cần thiết.

Sau mỗi nhóm chức năng, Agent phải build solution và sửa hết lỗi compile trước khi chuyển bước tiếp theo.
