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
