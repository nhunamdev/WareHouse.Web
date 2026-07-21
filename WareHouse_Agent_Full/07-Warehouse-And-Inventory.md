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
