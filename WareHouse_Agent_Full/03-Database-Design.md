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
