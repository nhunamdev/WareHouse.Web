# WareHouse Framework — Full Consolidated Agent Specification


---

<!-- SOURCE: 00-README-FIRST.md -->

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

---

<!-- SOURCE: 01-Agent-Rules.md -->

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

---

<!-- SOURCE: 02-Solution-Architecture.md -->

# Kiến trúc Solution

## 1. Cấu trúc bắt buộc

```text
WareHouse.sln
├── WareHouse.Web
└── WareHouse.Data
```

## 2. Project WareHouse.Web

Loại project:

```text
ASP.NET Core Web App (Model-View-Controller)
TargetFramework: net10.0
```

Cấu trúc đề xuất:

```text
WareHouse.Web
├── Controllers
│   ├── HomeController.cs
│   ├── ProductsController.cs
│   ├── AttributesController.cs
│   ├── ItemsController.cs
│   ├── WarehousesController.cs
│   ├── CustomersController.cs
│   ├── StockDocumentsController.cs
│   ├── SalesController.cs
│   ├── PaymentsController.cs
│   └── ReportsController.cs
├── ViewModels
│   ├── ProductViewModels.cs
│   ├── ItemViewModels.cs
│   ├── StockViewModels.cs
│   ├── SalesViewModels.cs
│   └── ReportViewModels.cs
├── Views
├── wwwroot
├── Program.cs
└── appsettings.json
```

`ViewModels` được phép chia nhiều file vì đây không phải entity database.

## 3. Project WareHouse.Data

```text
WareHouse.Data
├── DbContext.cs
├── Poco.cs
├── Enums.cs
└── WareHouseServices.cs
```

### DbContext.cs

Chứa lớp:

```csharp
public class WareHouseDbContext : DbContext
```

Phải đọc connection string tên `xx`.

### Poco.cs

Chứa toàn bộ entity:

- Product
- ProductAttribute
- AttributeValue
- Item
- ItemAttribute
- Warehouse
- WarehouseStock
- Customer
- StockDocument
- StockDocumentDetail
- Payment

### Enums.cs

Chứa enum:

- StockDocumentType
- DocumentStatus
- CustomerType
- PaymentMethod

### WareHouseServices.cs

Chứa:

- CRUD danh mục.
- Query Item và thuộc tính.
- Query tồn kho.
- Tạo/cập nhật/hủy chứng từ.
- Bán hàng.
- Thanh toán/công nợ.
- Dashboard và báo cáo.

Có thể dùng `#region` để chia module nhưng không tách thành nhiều service.

## 4. Dependency

`WareHouse.Web` tham chiếu `WareHouse.Data`.

`WareHouse.Data` không tham chiếu ngược lại `WareHouse.Web`.

## 5. Dependency Injection

Trong `Program.cs`:

- Đăng ký `WareHouseDbContext`.
- Đăng ký `WareHouseServices` dạng scoped.
- Bật MVC.
- Bật static files.
- Cấu hình routing mặc định.
- Cấu hình xử lý lỗi theo môi trường.

## 6. Connection string

Trong `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "xx": "Server=...;Database=WareHouseDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Agent không được đổi tên `xx`.

---

<!-- SOURCE: 03-Database-Design.md -->

# Thiết kế Database

## 1. Mục tiêu thiết kế

Database phải đủ gọn để dễ triển khai nhưng vẫn linh hoạt cho nhiều ngành hàng. Điểm cốt lõi là tách:

```text
Product -> Item -> WarehouseStock
```

`Product` là sản phẩm gốc. `Item` là SKU cụ thể. `WarehouseStock` là tồn kho của Item tại từng kho.

## 2. Bảng Products

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | int identity | No | PK |
| Code | nvarchar(50) | No | Unique |
| Name | nvarchar(200) | No | Tên sản phẩm |
| Category | nvarchar(100) | Yes | Nhóm hàng dạng text để đơn giản |
| Unit | nvarchar(50) | Yes | Cái, chiếc, bộ... |
| Description | nvarchar(1000) | Yes | Mô tả |
| IsActive | bit | No | Mặc định true |

Index:

- Unique `Code`
- Index `Name`

## 3. Bảng Attributes

Tên entity trong code nên là `ProductAttribute` để tránh trùng với `System.Attribute`.

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | int identity | No | PK |
| Name | nvarchar(100) | No | Size, Color, Voltage... |
| IsActive | bit | No | Mặc định true |

Unique:

- `Name`

## 4. Bảng AttributeValues

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | int identity | No | PK |
| AttributeId | int | No | FK -> Attributes |
| Value | nvarchar(100) | No | S, M, Đỏ, Đen... |
| SortOrder | int | No | Mặc định 0 |
| IsActive | bit | No | Mặc định true |

Unique:

- `(AttributeId, Value)`

## 5. Bảng Items

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | int identity | No | PK |
| ProductId | int | No | FK -> Products |
| Code | nvarchar(80) | No | SKU, unique |
| Barcode | nvarchar(100) | Yes | Unique khi có |
| CostPrice | decimal(18,2) | No | Giá vốn hiện tại/tham khảo |
| SalePrice | decimal(18,2) | No | Giá bán mặc định |
| IsActive | bit | No | Mặc định true |

Lưu ý:

- Item không chứa cột Size, Color hoặc thuộc tính cố định.
- Sản phẩm không có biến thể vẫn phải có đúng một Item.
- Giá nhập thực tế nằm trong chi tiết chứng từ; `CostPrice` là giá tham khảo hoặc giá vốn cập nhật gần nhất.

## 6. Bảng ItemAttributes

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| ItemId | int | No | FK -> Items |
| AttributeValueId | int | No | FK -> AttributeValues |

Primary key:

```text
(ItemId, AttributeValueId)
```

Quy tắc:

- Một Item không được có hai giá trị thuộc cùng một Attribute.
- Quy tắc này kiểm tra ở service trước khi lưu.
- Không giới hạn số lượng thuộc tính của Item.

## 7. Bảng Warehouses

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | int identity | No | PK |
| Code | nvarchar(50) | No | Unique |
| Name | nvarchar(150) | No | Tên kho |
| Address | nvarchar(300) | Yes | Địa chỉ |
| IsActive | bit | No | Mặc định true |

Seed hoặc nhập thủ công ba kho ban đầu. Thiết kế không giới hạn ở ba kho.

## 8. Bảng WarehouseStocks

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| WarehouseId | int | No | FK -> Warehouses |
| ItemId | int | No | FK -> Items |
| Quantity | decimal(18,2) | No | Tồn hiện tại |

Primary key:

```text
(WarehouseId, ItemId)
```

Quy tắc:

- Mỗi Item chỉ có một dòng tồn trong mỗi kho.
- Không cho phép Quantity âm.
- Khi chưa từng phát sinh, có thể không có dòng; query phải coi là 0.
- Khi ghi tăng tồn lần đầu, service phải tạo dòng mới.

## 9. Bảng Customers

Dùng chung cho khách lẻ và đại lý.

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | int identity | No | PK |
| Code | nvarchar(50) | No | Unique |
| Name | nvarchar(200) | No | Tên |
| CustomerType | int | No | Retail/Dealer |
| Phone | nvarchar(30) | Yes | SĐT |
| Address | nvarchar(300) | Yes | Địa chỉ |
| TaxCode | nvarchar(50) | Yes | MST |
| Debt | decimal(18,2) | No | Dư nợ hiện tại |
| IsActive | bit | No | Mặc định true |

`Debt` là số dư nhanh. Chi tiết phát sinh được truy ra từ Sale documents và Payments.

## 10. Bảng StockDocuments

Dùng chung cho chứng từ kho và bán hàng.

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | long identity | No | PK |
| DocumentNo | nvarchar(50) | No | Unique |
| DocumentType | int | No | Import/Export/Transfer/Sale/Return/Adjust |
| Status | int | No | Draft/Completed/Cancelled |
| DocumentDate | datetime2 | No | Ngày chứng từ |
| CustomerId | int | Yes | Khách hàng/đại lý |
| FromWarehouseId | int | Yes | Kho xuất |
| ToWarehouseId | int | Yes | Kho nhập |
| TotalAmount | decimal(18,2) | No | Tổng tiền |
| PaidAmount | decimal(18,2) | No | Đã thanh toán |
| DebtAmount | decimal(18,2) | No | Còn nợ |
| Remark | nvarchar(1000) | Yes | Ghi chú |
| CreatedAt | datetime2 | No | Ngày tạo |
| CreatedBy | nvarchar(100) | Yes | Người tạo |

Quy tắc kho:

| DocumentType | FromWarehouse | ToWarehouse |
|---|---|---|
| Import | null | bắt buộc |
| Export | bắt buộc | null |
| Transfer | bắt buộc | bắt buộc |
| Sale | bắt buộc | null |
| Return | tùy loại trả; mặc định null | kho nhận hàng |
| Adjust | kho điều chỉnh đặt tại ToWarehouse hoặc FromWarehouse theo dấu số lượng |

Để đơn giản, `Adjust` dùng `ToWarehouseId` và chi tiết Quantity có thể dương hoặc âm.

## 11. Bảng StockDocumentDetails

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | long identity | No | PK |
| DocumentId | long | No | FK |
| ItemId | int | No | FK |
| Quantity | decimal(18,2) | No | Luôn dương, trừ Adjust |
| Price | decimal(18,2) | No | Đơn giá |
| Amount | decimal(18,2) | No | Quantity * Price |

Unique không bắt buộc, nhưng service nên gộp các dòng trùng Item trong cùng chứng từ.

## 12. Bảng Payments

| Cột | Kiểu | Null | Ghi chú |
|---|---|---:|---|
| Id | long identity | No | PK |
| CustomerId | int | No | FK |
| DocumentId | long | Yes | Chứng từ bán liên quan |
| Amount | decimal(18,2) | No | Số tiền |
| PaymentDate | datetime2 | No | Ngày thu |
| PaymentMethod | int | No | Cash/BankTransfer/Other |
| Remark | nvarchar(500) | Yes | Ghi chú |
| CreatedAt | datetime2 | No | Ngày tạo |

Khi thêm Payment:

- Giảm `Customer.Debt`.
- Nếu gắn với Sale document, tăng `PaidAmount` và giảm `DebtAmount`.
- Không cho thanh toán âm.
- Không cho thanh toán vượt công nợ liên quan nếu không có chủ đích trả trước.

## 13. Quan hệ chính

```text
Products 1---n Items
Attributes 1---n AttributeValues
Items n---n AttributeValues via ItemAttributes
Warehouses n---n Items via WarehouseStocks
Customers 1---n StockDocuments
StockDocuments 1---n StockDocumentDetails
Customers 1---n Payments
StockDocuments 1---n Payments
```

## 14. Xóa dữ liệu

- Danh mục đã được sử dụng: không xóa cứng, chuyển `IsActive = false`.
- Item có tồn kho khác 0: không được vô hiệu hóa nếu chưa xác nhận.
- Chứng từ Completed: không xóa cứng.
- Payment: chỉ cho xóa khi có nghiệp vụ hoàn tác công nợ trong transaction.

---

<!-- SOURCE: 04-Data-Access-And-Services.md -->

# Truy cập dữ liệu và WareHouseServices

## 1. Mục tiêu

`WareHouseServices` là điểm truy cập database duy nhất từ project Web. Không tạo Repository hoặc service nhỏ khác.

## 2. Constructor

```csharp
public class WareHouseServices
{
    private readonly WareHouseDbContext _db;

    public WareHouseServices(WareHouseDbContext db)
    {
        _db = db;
    }
}
```

## 3. Kết quả thao tác

Nên tạo lớp dùng chung trong `Poco.cs` hoặc ViewModel Web:

```csharp
public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public long? Id { get; set; }
}
```

## 4. Nhóm phương thức bắt buộc

### Product

- `GetProductsAsync(keyword, page, pageSize)`
- `GetProductAsync(id)`
- `SaveProductAsync(Product model)`
- `DeleteProductAsync(id)` — thực tế là disable nếu đã dùng

### Attribute

- `GetAttributesAsync(...)`
- `GetAttributeAsync(id)`
- `SaveAttributeAsync(...)`
- `DeleteAttributeAsync(id)`
- `GetAttributeValuesAsync(attributeId)`
- `SaveAttributeValueAsync(...)`
- `DeleteAttributeValueAsync(id)`

### Item

- `GetItemsAsync(productId, keyword, page, pageSize)`
- `GetItemAsync(id)`
- `SaveItemAsync(Item model, List<int> attributeValueIds)`
- `DeleteItemAsync(id)`
- `GetItemDisplayNameAsync(id)`
- `GetItemsForSelectionAsync(productId, warehouseId, keyword)`

### Warehouse

- `GetWarehousesAsync()`
- `GetWarehouseAsync(id)`
- `SaveWarehouseAsync(...)`
- `DeleteWarehouseAsync(id)`

### Stock

- `GetWarehouseStockAsync(warehouseId, itemId)`
- `GetStockMatrixAsync(productId, keyword)`
- `GetLowStockAsync(threshold)`
- `IncreaseStockAsync(...)` — private/internal
- `DecreaseStockAsync(...)` — private/internal
- `SetStockAsync(...)` — dùng cho điều chỉnh

### Customer

- CRUD khách hàng/đại lý.
- `GetCustomerDebtAsync(customerId)`
- `GetCustomerStatementAsync(customerId, fromDate, toDate)`

### Document

- `GetDocumentsAsync(type, status, fromDate, toDate, keyword, page, pageSize)`
- `GetDocumentAsync(id)`
- `SaveDraftDocumentAsync(...)`
- `CompleteDocumentAsync(id)`
- `CancelDocumentAsync(id)`
- `CreateImportAsync(...)`
- `CreateExportAsync(...)`
- `CreateTransferAsync(...)`
- `CreateSaleAsync(...)`
- `CreateReturnAsync(...)`
- `CreateAdjustmentAsync(...)`

### Payment

- `GetPaymentsAsync(...)`
- `CreatePaymentAsync(...)`
- `DeletePaymentAsync(id)` với hoàn tác công nợ

### Dashboard/Report

- `GetDashboardAsync(fromDate, toDate)`
- `GetInventoryReportAsync(...)`
- `GetStockMovementReportAsync(...)`
- `GetSalesReportAsync(...)`
- `GetDebtReportAsync(...)`

## 5. Query Item và thuộc tính

Kết quả Item phải tạo chuỗi thuộc tính động, ví dụ:

```text
Size: S, Color: Đỏ, Voltage: 48V
```

Không viết code cố định theo tên thuộc tính.

## 6. Transaction

Bắt buộc dùng transaction cho:

- Complete Import
- Complete Export
- Complete Transfer
- Complete Sale
- Complete Return
- Complete Adjustment
- Cancel completed document
- Create/Delete payment có cập nhật công nợ

Trình tự:

1. Begin transaction.
2. Validate lại dữ liệu.
3. Lưu/chuyển trạng thái chứng từ.
4. Cập nhật WarehouseStocks.
5. Cập nhật Customer.Debt nếu có.
6. Commit.
7. Rollback khi exception.

## 7. Tồn kho

`DecreaseStockAsync` phải:

1. Lấy dòng stock theo WarehouseId + ItemId.
2. Nếu không tồn tại hoặc Quantity < số cần trừ: trả lỗi.
3. Trừ số lượng.
4. Không cho âm.

`IncreaseStockAsync` phải:

1. Tìm dòng stock.
2. Nếu chưa có thì tạo.
3. Cộng số lượng.

## 8. Tính tiền

Mỗi chi tiết:

```text
Amount = Quantity * Price
```

Chứng từ:

```text
TotalAmount = Sum(Details.Amount)
DebtAmount = Max(0, TotalAmount - PaidAmount)
```

Khi Sale hoàn tất:

```text
Customer.Debt += DebtAmount
```

## 9. Hủy chứng từ

Hủy chứng từ Completed phải đảo nghiệp vụ:

- Import: trừ tồn đã nhập.
- Export: cộng lại tồn đã xuất.
- Transfer: cộng kho nguồn, trừ kho đích.
- Sale: cộng lại tồn, giảm công nợ còn nợ.
- Return: đảo lại tồn và công nợ.
- Adjust: đảo chênh lệch.

Nếu không thể đảo vì thiếu tồn ở kho cần trừ, phải chặn và báo lỗi.

---

<!-- SOURCE: 05-UI-UX-Standards.md -->

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

---

<!-- SOURCE: 06-Product-Attribute-Item.md -->

# Sản phẩm, thuộc tính và Item

## 1. Khái niệm

### Product

Sản phẩm gốc, ví dụ:

```text
Xe đạp A
```

### Attribute

Nhóm thuộc tính:

```text
Size
Color
Battery
Material
```

### AttributeValue

Giá trị thuộc tính:

```text
Size -> S
Size -> M
Color -> Đỏ
Color -> Đen
```

### Item

SKU thực tế:

```text
Xe đạp A / Size S / Color Đỏ
```

Mọi tồn kho và chứng từ đều tham chiếu Item, không tham chiếu Product trực tiếp.

## 2. Ví dụ hoàn chỉnh

Product:

```text
Code: BIKA
Name: Xe đạp A
```

Items:

| ItemCode | Thuộc tính |
|---|---|
| BIKA-S-RED | Size=S, Color=Đỏ |
| BIKA-S-BLK | Size=S, Color=Đen |
| BIKA-M-RED | Size=M, Color=Đỏ |

WarehouseStocks:

| Kho | Item | Quantity |
|---|---|---:|
| A | BIKA-S-RED | 10 |
| B | BIKA-S-RED | 5 |
| C | BIKA-S-RED | 2 |

Hệ thống trả lời được:

```text
Sản phẩm A, Size S, Màu Đỏ:
Kho A = 10
Kho B = 5
Kho C = 2
Tổng = 17
```

## 3. Sản phẩm không có thuộc tính

Ví dụ "Dầu bôi trơn 500ml" không có biến thể.

Vẫn tạo:

- 1 Product.
- 1 Item.
- 0 ItemAttributes.

Điều này giữ mọi nghiệp vụ thống nhất.

## 4. Kiểm tra trùng tổ hợp

Khi tạo Item mới cho một Product, service phải kiểm tra xem đã có Item khác có cùng tập `AttributeValueId` hay chưa.

Hai tập thuộc tính giống nhau được coi là cùng tổ hợp dù thứ tự chọn khác nhau.

Không cho tạo trùng.

## 5. Quy tắc một giá trị trên mỗi nhóm

Một Item không thể có:

```text
Size = S
Size = M
```

Service phải phát hiện hai AttributeValue cùng AttributeId và trả lỗi.

## 6. Hiển thị tên Item

Tên hiển thị:

```text
{Product.Name} - {AttributeName}: {Value}, ...
```

Nếu không có thuộc tính:

```text
{Product.Name}
```

## 7. Xóa/vô hiệu hóa

Không cho xóa Item nếu:

- Đã có StockDocumentDetail.
- Có tồn kho khác 0.

Trong trường hợp đó chỉ đặt `IsActive = false`.

---

<!-- SOURCE: 07-Warehouse-And-Inventory.md -->

# Kho hàng và tồn kho

## 1. Nhiều kho

Hệ thống ban đầu có ba kho nhưng kiến trúc phải hỗ trợ số kho không giới hạn.

Không hard-code `Kho A`, `Kho B`, `Kho C` trong code hoặc ViewModel.

## 2. Tồn kho hiện tại

Nguồn dữ liệu chính:

```text
WarehouseStocks
```

Không tính tồn mỗi lần bằng cách cộng toàn bộ chứng từ.

## 3. Ma trận tồn kho

Query ma trận:

- Danh sách Item theo Product/keyword.
- Danh sách kho đang active.
- Ghép Quantity theo `(WarehouseId, ItemId)`.
- Nếu không có dòng thì Quantity = 0.
- Tổng tồn = tổng Quantity của các kho.

## 4. Nhập kho

Tăng kho đích.

## 5. Xuất kho và bán hàng

Giảm kho nguồn.

Trước khi giảm phải kiểm tra tồn.

## 6. Chuyển kho

Trong một transaction:

```text
Kho nguồn -= Quantity
Kho đích += Quantity
```

Không cho chuyển cùng một kho.

## 7. Điều chỉnh

Điều chỉnh dùng khi kiểm kê:

- Quantity dương: cộng tồn.
- Quantity âm: trừ tồn.
- Không cho kết quả âm.

## 8. Trả hàng

Trả hàng bán:

- Hàng quay lại kho đích.
- Cộng tồn.
- Giảm công nợ hoặc tạo khoản hoàn tiền theo số tiền trả.

## 9. Cảnh báo tồn thấp

Dashboard cho phép ngưỡng mặc định, ví dụ 5.

Một Item được coi là tồn thấp tại một kho khi:

```text
Quantity <= threshold
```

Có thể hiển thị tổng tồn thấp hoặc theo từng kho.

## 10. Tính giá trị tồn

Giá trị tồn tham khảo:

```text
WarehouseStock.Quantity * Item.CostPrice
```

Tổng giá trị tồn là tổng tất cả kho và Item.

## 11. Đồng thời

Khi hai người bán cùng một Item:

- Transaction phải đọc tồn tại thời điểm hoàn tất.
- Không chỉ tin dữ liệu tải lên từ UI.
- Nếu không đủ tồn, rollback và trả thông báo.

---

<!-- SOURCE: 08-Stock-Document-Flows.md -->

# Luồng chứng từ kho

## 1. Trạng thái

### Draft

- Có thể sửa.
- Chưa cập nhật tồn.
- Có thể xóa.

### Completed

- Đã cập nhật tồn.
- Không sửa chi tiết trực tiếp.
- Chỉ được hủy bằng nghiệp vụ đảo.

### Cancelled

- Đã đảo tồn nếu trước đó Completed.
- Không được kích hoạt lại bằng sửa trạng thái thủ công.

## 2. Nhập kho — Import

Yêu cầu:

- ToWarehouseId bắt buộc.
- FromWarehouseId null.
- Ít nhất một dòng chi tiết.
- Quantity > 0.
- Price >= 0.

Khi Complete:

1. Validate.
2. Tính Amount và TotalAmount.
3. Cộng từng Item vào kho đích.
4. Đặt Status = Completed.
5. Commit.

## 3. Xuất kho — Export

Yêu cầu:

- FromWarehouseId bắt buộc.
- ToWarehouseId null.
- Quantity > 0.

Khi Complete:

1. Kiểm tra tồn từng Item.
2. Trừ tồn.
3. Lưu chứng từ Completed.
4. Commit.

## 4. Chuyển kho — Transfer

Yêu cầu:

- FromWarehouseId và ToWarehouseId bắt buộc.
- Hai kho khác nhau.
- Quantity > 0.

Khi Complete:

1. Kiểm tra tồn kho nguồn.
2. Trừ kho nguồn.
3. Cộng kho đích.
4. Complete chứng từ.
5. Commit.

## 5. Điều chỉnh — Adjust

Yêu cầu:

- Chọn kho.
- Quantity có thể dương hoặc âm nhưng không bằng 0.

Khi Complete:

- Dương: tăng tồn.
- Âm: giảm tồn sau khi kiểm tra.

## 6. Hủy chứng từ

Mỗi loại phải đảo đúng nghiệp vụ ban đầu.

Hủy Import chỉ được thực hiện nếu kho đích còn đủ số lượng để trừ lại.

Hủy Transfer phải đảm bảo kho đích còn đủ số lượng để chuyển ngược.

## 7. Đánh số chứng từ

Gợi ý:

```text
IMP-202607-0001
EXP-202607-0001
TRF-202607-0001
SAL-202607-0001
RET-202607-0001
ADJ-202607-0001
```

Agent có thể sinh số dựa trên loại, năm-tháng và sequence. Phải đảm bảo unique.

---

<!-- SOURCE: 09-Sales-Debt-Invoice.md -->

# Bán hàng, công nợ và hóa đơn

## 1. Bán hàng

Bán hàng sử dụng `StockDocuments` với `DocumentType = Sale`.

Yêu cầu:

- CustomerId bắt buộc, có thể dùng một khách "Khách lẻ".
- FromWarehouseId bắt buộc.
- Có ít nhất một Item.
- Quantity > 0.
- Price >= 0.
- PaidAmount >= 0.
- PaidAmount <= TotalAmount.

## 2. Hoàn tất đơn bán

Trong một transaction:

1. Validate khách hàng và kho.
2. Validate tất cả Item.
3. Kiểm tra tồn.
4. Tính TotalAmount.
5. Tính DebtAmount.
6. Trừ WarehouseStocks.
7. Nếu DebtAmount > 0:
   - `Customer.Debt += DebtAmount`
8. Nếu PaidAmount > 0:
   - Tạo Payment liên quan đến Sale, hoặc lưu PaidAmount và tạo Payment để có lịch sử.
9. Đặt Status = Completed.
10. Commit.

Khuyến nghị: luôn tạo Payment khi có PaidAmount > 0 để báo cáo tiền thu có dữ liệu rõ ràng.

## 3. Công nợ

Số dư nhanh nằm tại:

```text
Customers.Debt
```

Công nợ chi tiết được lập từ:

- Sale Completed có DebtAmount.
- Return Completed làm giảm nợ.
- Payment làm giảm nợ.
- Cancel Sale đảo nợ.

Không chỉnh trực tiếp `Customer.Debt` từ UI.

## 4. Thanh toán

Form thanh toán:

- Customer.
- Sale document tùy chọn.
- Amount.
- PaymentDate.
- PaymentMethod.
- Remark.

Nếu có Sale document:

- Không cho Amount vượt `DebtAmount`.
- `Sale.PaidAmount += Amount`.
- `Sale.DebtAmount -= Amount`.

Luôn:

```text
Customer.Debt -= Amount
```

Không cho kết quả âm trừ trường hợp nghiệp vụ trả trước được bổ sung sau.

## 5. Trả hàng bán

Dùng `DocumentType = Return`.

Yêu cầu:

- CustomerId.
- ToWarehouseId.
- Các Item và số lượng trả.
- Giá trị hoàn/giảm nợ.

Khi Complete:

1. Cộng lại tồn.
2. Tính ReturnAmount.
3. Giảm Customer.Debt tối đa bằng dư nợ hiện tại.
4. Phần vượt dư nợ được coi là tiền phải hoàn; bản đầu có thể chỉ hiển thị cảnh báo và không xử lý hoàn tiền tự động.
5. Commit.

## 6. In hóa đơn

Route ví dụ:

```text
/Sales/Print/{id}
```

Chỉ in Sale đã lưu.

Nội dung:

- Logo và thông tin cửa hàng.
- Số hóa đơn.
- Ngày bán.
- Khách hàng/đại lý.
- Kho xuất.
- Danh sách:
  - SKU
  - Tên Product
  - Thuộc tính động
  - Quantity
  - Price
  - Amount
- TotalAmount.
- PaidAmount.
- DebtAmount.
- Ghi chú.
- Người lập.
- Chữ ký người bán/người mua nếu cần.

`Print.cshtml` dùng CSS riêng và nút:

```html
<button class="btn btn-primary d-print-none" onclick="window.print()">In hóa đơn</button>
```

## 7. Phiếu xuất và hóa đơn

Hóa đơn bán hàng là chứng từ tài chính gửi khách.

Phiếu xuất kho là góc nhìn kho. Bản đầu có thể dùng cùng Sale document và cung cấp hai View in:

- `PrintInvoice.cshtml`
- `PrintDelivery.cshtml`

`PrintDelivery` không bắt buộc hiển thị giá.

---

<!-- SOURCE: 10-Dashboard-And-Reports.md -->

# Dashboard và báo cáo

## 1. Dashboard

Bộ lọc thời gian:

- Hôm nay.
- Tháng này.
- Khoảng ngày tùy chọn.

Cards:

- Tổng số lượng tồn.
- Tổng giá trị tồn.
- Nhập trong kỳ.
- Xuất trong kỳ.
- Doanh thu bán hàng.
- Tiền đã thu.
- Tổng công nợ khách hàng.
- Số Item tồn thấp.

Bảng/biểu đồ:

- Top Item bán chạy.
- Doanh thu theo ngày.
- Tồn thấp theo kho.
- Chứng từ gần đây.

Không bắt buộc thư viện chart ngoài; có thể dùng Chart.js nếu cần.

## 2. Báo cáo tồn kho

Bộ lọc:

- Product.
- Keyword/SKU.
- Warehouse.
- Thuộc tính.

Cột:

- Product.
- ItemCode.
- Thuộc tính.
- Quantity theo từng kho.
- TotalQuantity.
- CostPrice.
- InventoryValue.

## 3. Báo cáo nhập–xuất–tồn

Theo khoảng ngày và kho.

Cột:

- Item.
- OpeningQuantity.
- ImportQuantity.
- ExportQuantity.
- TransferIn.
- TransferOut.
- SaleQuantity.
- ReturnQuantity.
- AdjustQuantity.
- ClosingQuantity.

OpeningQuantity có thể tính từ tồn hiện tại và các phát sinh sau ngày bắt đầu hoặc từ chứng từ lịch sử. Agent phải mô tả rõ cách tính trong code.

## 4. Báo cáo bán hàng

- Theo ngày.
- Theo khách hàng.
- Theo Item/Product.
- Theo kho.

Cột:

- DocumentNo.
- Date.
- Customer.
- Warehouse.
- TotalAmount.
- PaidAmount.
- DebtAmount.

## 5. Báo cáo công nợ

- Customer.
- OpeningDebt nếu có thể tính.
- SalesDebt.
- Payments.
- Returns.
- ClosingDebt.

Bản tối thiểu phải hiển thị:

- Customer.
- CurrentDebt.
- Các Sale còn nợ.
- Lịch sử Payment.

## 6. Xuất dữ liệu

Bản đầu không bắt buộc Excel/PDF, nhưng cấu trúc Controller/ViewModel phải thuận tiện bổ sung sau.

## 7. Hiệu năng

- Query chỉ lấy dữ liệu cần thiết.
- Dùng `AsNoTracking()` cho báo cáo.
- Phân trang danh sách lớn.
- Không gọi query lặp trong vòng lặp khi có thể join/group.

---

<!-- SOURCE: 11-Module-Specifications.md -->

# Đặc tả module

## 1. Products

Màn hình:

- Danh sách.
- Thêm/sửa.
- Chi tiết.
- Vô hiệu hóa.

Danh sách hiển thị:

- Code.
- Name.
- Category.
- Unit.
- Số Item.
- Trạng thái.

Validation:

- Code bắt buộc và unique.
- Name bắt buộc.

## 2. Attributes và AttributeValues

Màn hình master-detail:

- Danh sách Attribute.
- Chọn Attribute để xem giá trị.
- Thêm/sửa/xóa mềm.

Validation:

- Tên Attribute unique.
- Value unique trong cùng Attribute.

## 3. Items

Danh sách:

- ItemCode.
- Product.
- Thuộc tính.
- Barcode.
- CostPrice.
- SalePrice.
- Tổng tồn.
- Trạng thái.

Form:

- Product.
- Code.
- Barcode.
- Giá.
- Các nhóm thuộc tính động.

Validation:

- Code unique.
- Không trùng tổ hợp thuộc tính trong cùng Product.
- Không có hai giá trị cùng nhóm.

## 4. Warehouses

- CRUD.
- Code unique.
- Không xóa kho có chứng từ hoặc tồn khác 0.
- Có thể disable.

## 5. Inventory

- Ma trận tồn theo Item và kho.
- Lọc Product, Item, Warehouse.
- Link đến lịch sử chứng từ của Item.

## 6. Customers

- Code.
- Name.
- Retail/Dealer.
- Contact.
- Debt.
- Lịch sử giao dịch.
- Các hóa đơn còn nợ.

Không cho sửa trực tiếp Debt.

## 7. Import

- Danh sách phiếu nhập.
- Tạo Draft.
- Complete.
- Cancel.
- Chi tiết.
- In phiếu nhập nếu cần.

## 8. Export

Tương tự Import nhưng kiểm tra tồn kho nguồn.

## 9. Transfer

- Chọn kho nguồn và đích.
- Hiển thị tồn tại kho nguồn khi chọn Item.
- Complete trong transaction.

## 10. Sale

- Danh sách hóa đơn.
- Tạo đơn bán.
- Chọn khách.
- Chọn kho.
- Chọn Item.
- Nhập số lượng/giá.
- Thanh toán ngay.
- Hoàn tất.
- In hóa đơn.
- In phiếu giao hàng.
- Hủy.

## 11. Return

- Chọn khách.
- Có thể chọn hóa đơn gốc.
- Chọn Item và số lượng.
- Chọn kho nhận hàng.
- Giảm nợ hoặc ghi nhận giá trị trả.

## 12. Payments

- Danh sách phiếu thu.
- Tạo phiếu thu.
- Gắn với khách hàng và hóa đơn.
- In phiếu thu là tùy chọn.
- Xóa phải hoàn tác.

## 13. Dashboard

- Cards.
- Top selling.
- Low stock.
- Recent documents.
- Date filters.

## 14. Reports

- Inventory.
- Stock movement.
- Sales.
- Debt.

---

<!-- SOURCE: 12-Validation-Transaction-Security.md -->

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

---

<!-- SOURCE: 13-Acceptance-Criteria.md -->

# Tiêu chí nghiệm thu

## 1. Solution

- Có đúng hai project.
- Target .NET 10.
- Build thành công.
- `WareHouse.Web` chạy được.
- Connection string tên `xx`.

## 2. Product/Item/Attribute

- Tạo được Product.
- Tạo được Attribute và AttributeValue mới mà không sửa database.
- Tạo Item với nhiều thuộc tính.
- Không cho hai giá trị cùng nhóm trên một Item.
- Không cho trùng tổ hợp thuộc tính trong cùng Product.
- Sản phẩm không thuộc tính vẫn tạo được một Item.

## 3. Kho

- Có thể tạo ba kho.
- Có thể thêm kho thứ tư mà không sửa code cố định.
- Tồn kho hiển thị theo kho.
- Ma trận tồn sinh cột kho động.

## 4. Tình huống bắt buộc

Với:

```text
Product: Sản phẩm A
Item: Size=S, Color=X
```

Tồn kho:

```text
Kho A = 10
Kho B = 5
Kho C = 2
```

Hệ thống phải hiển thị chính xác:

```text
Sản phẩm A | Size: S, Color: X | 10 | 5 | 2 | Tổng 17
```

## 5. Nhập kho

- Nhập 5 vào Kho A làm tồn từ 10 lên 15.
- Chứng từ Draft không làm thay đổi tồn.
- Complete mới làm thay đổi tồn.
- Cancel đảo lại tồn nếu đủ điều kiện.

## 6. Xuất kho

- Xuất 3 từ Kho A làm tồn 15 xuống 12.
- Không cho xuất 20 khi chỉ còn 12.
- Transaction rollback khi có bất kỳ dòng nào thiếu tồn.

## 7. Chuyển kho

- Chuyển 2 từ A sang B:
  - A giảm 2.
  - B tăng 2.
- Không được chỉ cập nhật một phía.
- Không cho chuyển cùng kho.

## 8. Bán hàng

- Bán Item từ kho được chọn.
- Tồn giảm đúng.
- TotalAmount đúng.
- PaidAmount đúng.
- DebtAmount đúng.
- Customer.Debt tăng đúng.
- Có thể in hóa đơn.

## 9. Thanh toán

- Thanh toán làm Customer.Debt giảm.
- Nếu gắn Sale, DebtAmount của Sale giảm.
- Không cho thanh toán vượt số nợ của Sale.
- Xóa phiếu thu hoàn tác đúng.

## 10. Hóa đơn

- Có `Print.cshtml` hoặc `PrintInvoice.cshtml`.
- Hiển thị Product, SKU, thuộc tính động, giá, số lượng, thành tiền.
- Hiển thị đã trả và còn nợ.
- In được từ trình duyệt.

## 11. Dashboard và báo cáo

- Dashboard có dữ liệu thực.
- Báo cáo tồn theo kho.
- Báo cáo bán hàng.
- Báo cáo công nợ.
- Search và filter hoạt động.

## 12. UI

- Bootstrap 5.3.
- Responsive cơ bản.
- Có validation.
- Có thông báo thành công/thất bại.
- Có xác nhận khi hủy/xóa.

---

<!-- SOURCE: 99-Agent-Execution-Plan.md -->

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
