# Kế hoạch thực thi bắt buộc cho Agent

## Giai đoạn 1 — Khởi tạo

1. Tạo `WareHouse.sln`.
2. Tạo `WareHouse.Web` ASP.NET Core MVC net10.0.
3. Tạo `WareHouse.Data` class library net10.0.
4. Thêm project reference.
5. Cài EF Core SQL Server.
6. Cấu hình connection string `xx`.
7. Build.

## Giai đoạn 2 — Data model

1. Tạo `Enums.cs`.
2. Tạo toàn bộ entity trong `Poco.cs`.
3. Tạo `WareHouseDbContext` trong `DbContext.cs`.
4. Cấu hình PK, FK, unique index và decimal precision.
5. Tạo migration đầu tiên.
6. Build.

## Giai đoạn 3 — Services nền tảng

1. Tạo `WareHouseServices`.
2. Product CRUD.
3. Attribute/Value CRUD.
4. Item CRUD và kiểm tra tổ hợp.
5. Warehouse CRUD.
6. Customer CRUD.
7. Stock query.
8. Build.

## Giai đoạn 4 — UI nền tảng

1. Layout Bootstrap 5.3.
2. Navigation.
3. Alert/validation partial.
4. Pagination ViewModel/helper đơn giản.
5. Product UI.
6. Attribute UI.
7. Item UI.
8. Warehouse UI.
9. Customer UI.
10. Build.

## Giai đoạn 5 — Tồn kho và chứng từ

1. Inventory matrix.
2. StockDocument CRUD Draft.
3. Import Complete/Cancel.
4. Export Complete/Cancel.
5. Transfer Complete/Cancel.
6. Adjustment.
7. Build.
8. Chạy các tình huống nghiệm thu tồn kho.

## Giai đoạn 6 — Bán hàng và công nợ

1. Sale UI.
2. Complete Sale.
3. Customer debt.
4. Payment.
5. Return.
6. Cancel Sale.
7. Build.
8. Chạy tình huống nghiệm thu.

## Giai đoạn 7 — In và báo cáo

1. Invoice ViewModel.
2. `PrintInvoice.cshtml`.
3. `PrintDelivery.cshtml` nếu có.
4. Dashboard.
5. Inventory report.
6. Sales report.
7. Debt report.
8. Build.

## Giai đoạn 8 — Hoàn thiện

1. Kiểm tra validation.
2. Kiểm tra quyền.
3. Kiểm tra transaction.
4. Kiểm tra dữ liệu trùng.
5. Kiểm tra responsive.
6. Build Release.
7. Không kết thúc khi còn lỗi compile.

## Báo cáo cuối của Agent

Agent phải liệt kê:

- File đã tạo.
- Migration đã tạo.
- Module đã hoàn thành.
- Module chưa hoàn thành.
- Cách cấu hình `xx`.
- Cách chạy ứng dụng.
- Tài khoản mặc định nếu có Identity.
- Các giả định đã sử dụng.
