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
