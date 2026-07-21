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
