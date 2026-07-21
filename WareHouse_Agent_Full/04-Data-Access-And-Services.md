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
