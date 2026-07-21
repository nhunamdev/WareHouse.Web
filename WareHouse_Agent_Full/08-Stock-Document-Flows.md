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
