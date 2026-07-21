using System.ComponentModel.DataAnnotations;

namespace WareHouse.Data;

public enum StockDocumentType
{
    Import = 1,
    Export = 2,
    Transfer = 3,
    Sale = 4,
    Return = 5,
    Adjust = 6
}

public enum DocumentStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3,
    Invoiced = 4
}

public enum CustomerType
{
    Retail = 1,
    Dealer = 2
}

public enum PaymentMethod
{
    [Display(Name = "Tiền mặt")]
    Cash = 1,
    [Display(Name = "Chuyển khoản")]
    BankTransfer = 2,
    [Display(Name = "Khác")]
    Other = 3
}

public enum PaymentStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3,
    Issued = 4
}
