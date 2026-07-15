namespace PTGOilSystem.Web.Services.Accounting;

public interface IAccountingJournalNumberGenerator
{
    string ForContractBalanceTransfer(int companyId, int transferId);
}

public sealed class AccountingJournalNumberGenerator : IAccountingJournalNumberGenerator
{
    public string ForContractBalanceTransfer(int companyId, int transferId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (transferId <= 0)
            throw new ArgumentOutOfRangeException(nameof(transferId));

        // Transfer ids are database-generated and globally unique. Including the
        // company keeps the number readable while remaining deterministic on retry.
        return $"CBT-{companyId:D6}-{transferId:D10}";
    }
}
