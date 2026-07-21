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
