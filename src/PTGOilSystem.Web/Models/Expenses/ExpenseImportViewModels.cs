using Microsoft.AspNetCore.Http;

namespace PTGOilSystem.Web.Models.Expenses;

/// <summary>
/// Bulk import of expense rows from an Excel file. Two-step flow:
/// 1) upload + preview (parse + validation, no save),
/// 2) confirm (save validated rows as ExpenseTransaction + LedgerEntry).
/// Logic mirrors the single-expense create path; no parallel financial logic.
/// </summary>
public class ExpenseImportViewModel
{
    public IFormFile? ImportFile { get; set; }

    public List<ExpenseImportRowViewModel> Rows { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }

    public bool HasRows => Rows.Count > 0;
    public bool HasErrors => ErrorCount > 0;
    public bool CanConfirm => HasRows && ErrorCount == 0;
}

/// <summary>
/// One imported expense row. Raw text fields are carried between the preview
/// and confirm steps via hidden inputs so the user does not re-upload the file.
/// All parsing + validation happens server-side in a single shared routine.
/// </summary>
public class ExpenseImportRowViewModel
{
    public int ExcelRowNumber { get; set; }

    // Raw (canonical) values carried in hidden inputs between preview and confirm.
    public string? ExpenseDateText { get; set; }
    public string? ExpenseTypeName { get; set; }
    public string? AmountText { get; set; }
    public string? Currency { get; set; }
    public string? RatePerUsdText { get; set; }
    public string? ContractNumber { get; set; }
    public string? Description { get; set; }

    // Parsed / resolved values (recomputed on every validation pass; not trusted from the client).
    public DateTime? ExpenseDate { get; set; }
    public decimal? Amount { get; set; }
    public decimal? RatePerUsd { get; set; }
    public decimal? AmountUsd { get; set; }

    public int? ResolvedExpenseTypeId { get; set; }
    public string? ResolvedExpenseTypeName { get; set; }
    public int? ResolvedContractId { get; set; }

    public List<string> Errors { get; set; } = new();

    public bool IsValid => Errors.Count == 0;
}
