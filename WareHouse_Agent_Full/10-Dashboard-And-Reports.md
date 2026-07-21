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
