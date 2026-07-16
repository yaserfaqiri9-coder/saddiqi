using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IPaymentCompanyResolver
{
    /// <summary>
    /// Returns the payment's company when — and only when — it is provable. Never guesses.
    /// </summary>
    Task<int?> ResolveAsync(PaymentTransaction payment, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only company resolution for a payment, mirroring the provable order already used by
/// <see cref="PaymentCompanyBackfillSql"/> so the pilot and the backfill can never disagree:
///
///   0. The payment's own CompanyId (Stage 3 column — already proven).
///   1. The payment's own contract.
///   2. The linked sale's explicit company.
///   3. The linked sale's contract.
///   4. The linked expense's contract.
///   5. The payment's shipment, when every contract of that shipment belongs to one company.
///   6. The linked expense's shipment, under the same single-company condition.
///
/// Anything else stays unresolved (null). This service never writes: the legacy payment row is
/// left exactly as the operational code created it.
/// </summary>
public sealed class PaymentCompanyResolver(ApplicationDbContext db) : IPaymentCompanyResolver
{
    public async Task<int?> ResolveAsync(
        PaymentTransaction payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (payment.CompanyId.HasValue)
            return payment.CompanyId;

        if (payment.ContractId.HasValue)
        {
            var fromContract = await CompanyOfContractAsync(payment.ContractId.Value, cancellationToken);
            if (fromContract.HasValue)
                return fromContract;
        }

        if (payment.SalesTransactionId.HasValue)
        {
            var sale = await db.SalesTransactions
                .AsNoTracking()
                .Where(x => x.Id == payment.SalesTransactionId.Value)
                .Select(x => new { x.CompanyId, x.ContractId })
                .SingleOrDefaultAsync(cancellationToken);

            if (sale is not null)
            {
                if (sale.CompanyId.HasValue)
                    return sale.CompanyId;

                var fromSaleContract = await CompanyOfContractAsync(sale.ContractId, cancellationToken);
                if (fromSaleContract.HasValue)
                    return fromSaleContract;
            }
        }

        int? expenseShipmentId = null;
        if (payment.ExpenseTransactionId.HasValue)
        {
            var expense = await db.ExpenseTransactions
                .AsNoTracking()
                .Where(x => x.Id == payment.ExpenseTransactionId.Value)
                .Select(x => new { x.ContractId, x.ShipmentId })
                .SingleOrDefaultAsync(cancellationToken);

            if (expense is not null)
            {
                if (expense.ContractId.HasValue)
                {
                    var fromExpenseContract = await CompanyOfContractAsync(
                        expense.ContractId.Value,
                        cancellationToken);
                    if (fromExpenseContract.HasValue)
                        return fromExpenseContract;
                }

                expenseShipmentId = expense.ShipmentId;
            }
        }

        if (payment.ShipmentId.HasValue)
        {
            var fromShipment = await SingleCompanyOfShipmentAsync(payment.ShipmentId.Value, cancellationToken);
            if (fromShipment.HasValue)
                return fromShipment;
        }

        if (expenseShipmentId.HasValue)
        {
            var fromExpenseShipment = await SingleCompanyOfShipmentAsync(
                expenseShipmentId.Value,
                cancellationToken);
            if (fromExpenseShipment.HasValue)
                return fromExpenseShipment;
        }

        return null;
    }

    private async Task<int?> CompanyOfContractAsync(int? contractId, CancellationToken cancellationToken)
    {
        if (!contractId.HasValue)
            return null;

        return await db.Contracts
            .AsNoTracking()
            .Where(x => x.Id == contractId.Value)
            .Select(x => (int?)x.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> SingleCompanyOfShipmentAsync(int shipmentId, CancellationToken cancellationToken)
    {
        var junctionCompanies = await db.ShipmentContracts
            .AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId)
            .Join(db.Contracts.AsNoTracking(), sc => sc.ContractId, c => c.Id, (_, c) => c.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var primaryCompanies = await db.Shipments
            .AsNoTracking()
            .Where(x => x.Id == shipmentId && x.ContractId != null)
            .Join(db.Contracts.AsNoTracking(), s => s.ContractId, c => c.Id, (_, c) => c.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var companies = junctionCompanies.Concat(primaryCompanies).Distinct().ToArray();
        return companies.Length == 1 ? companies[0] : null;
    }
}
