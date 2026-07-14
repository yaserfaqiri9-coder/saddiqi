using System.ComponentModel.DataAnnotations;
using PTGOilSystem.Web.Models.InventoryTransport;

namespace PTGOilSystem.Web.Models.Dispatch;

// مودال چندردیفیِ «ثبت مصرف / کرایه» برای ارسال با موتر — هم‌شکلِ مودال مصارفِ «حمل از موجودی».
// backend روی همان مسیر موجود ExpenseTransaction + TruckDispatchId + LedgerEntry سوار می‌شود؛
// هیچ Entity/Migration/Ledger جدیدی ندارد. مبالغ ردیف‌ها USD پایه‌اند (مثل مودال حمل).
public sealed class DispatchExpenseModalViewModel
{
    public int TruckDispatchId { get; set; }
    public string TransportReference { get; set; } = "";
    public decimal LoadedQuantityMt { get; set; }
    [StringLength(1000)]
    public string? ReturnUrl { get; set; }
    public List<InventoryTransportGroupExpenseModalRow> Lines { get; set; } = [];
    // مصارف فعالِ همین ارسال برای نمایش read-only و جلوگیری از تکرارِ سهوی.
    public IReadOnlyList<InventoryTransportFlowExpenseItemViewModel> ExistingExpenses { get; set; } = [];
}
