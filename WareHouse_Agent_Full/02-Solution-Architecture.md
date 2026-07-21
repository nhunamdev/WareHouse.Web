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
    "DefaultConnection": "Host=210.245.90.227;Database=haphatvn_warehouse;Username=haphatvn;Password=Namhuyen93"
  },
}
```

Agent không được đổi tên `xx`.
