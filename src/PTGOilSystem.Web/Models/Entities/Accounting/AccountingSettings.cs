using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public class AccountingSettings : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, MaxLength(10)]
    public string FunctionalCurrencyCode { get; set; } = "USD";

    public int CashBankControlAccountId { get; set; }
    public Account? CashBankControlAccount { get; set; }
    public int AccountsReceivableAccountId { get; set; }
    public Account? AccountsReceivableAccount { get; set; }
    public int AccountsPayableAccountId { get; set; }
    public Account? AccountsPayableAccount { get; set; }
    public int InventoryAccountId { get; set; }
    public Account? InventoryAccount { get; set; }
    public int InventoryInTransitAccountId { get; set; }
    public Account? InventoryInTransitAccount { get; set; }
    public int SupplierPrepaymentAccountId { get; set; }
    public Account? SupplierPrepaymentAccount { get; set; }
    public int CustomerAdvanceAccountId { get; set; }
    public Account? CustomerAdvanceAccount { get; set; }
    public int FreightPayableAccountId { get; set; }
    public Account? FreightPayableAccount { get; set; }
    public int CommissionPayableAccountId { get; set; }
    public Account? CommissionPayableAccount { get; set; }
    public int EmployeeAdvanceAccountId { get; set; }
    public Account? EmployeeAdvanceAccount { get; set; }
    public int EmployeePayableAccountId { get; set; }
    public Account? EmployeePayableAccount { get; set; }
    public int AccruedExpenseAccountId { get; set; }
    public Account? AccruedExpenseAccount { get; set; }
    public int SalesRevenueAccountId { get; set; }
    public Account? SalesRevenueAccount { get; set; }
    public int CostOfGoodsSoldAccountId { get; set; }
    public Account? CostOfGoodsSoldAccount { get; set; }
    public int GeneralExpenseAccountId { get; set; }
    public Account? GeneralExpenseAccount { get; set; }
    public int ExchangeGainAccountId { get; set; }
    public Account? ExchangeGainAccount { get; set; }
    public int ExchangeLossAccountId { get; set; }
    public Account? ExchangeLossAccount { get; set; }
    public int InventoryLossAccountId { get; set; }
    public Account? InventoryLossAccount { get; set; }
    public int CurrentYearProfitLossAccountId { get; set; }
    public Account? CurrentYearProfitLossAccount { get; set; }
    public int RetainedEarningsAccountId { get; set; }
    public Account? RetainedEarningsAccount { get; set; }

    public uint RowVersion { get; set; }
}
