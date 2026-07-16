using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record InventoryConsumption(bool Succeeded, decimal CostUsd, string? Reason);

public interface IInventoryValuationService
{
    /// <summary>
    /// Adds received goods to the pool, which moves the average.
    /// </summary>
    Task ApplyReceiptAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        decimal valueUsd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes goods out at the current average and returns what they cost. Fails without touching
    /// the pool when it holds less than asked for, so a sale can never value against stock that
    /// is not there.
    /// </summary>
    Task<InventoryConsumption> TryConsumeAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts back exactly what a consumption took, for reversals. The average moves to whatever
    /// the pool implies afterwards rather than being restored to its old value, which is what
    /// moving average means.
    /// </summary>
    Task ReturnAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        decimal costUsd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a receipt's quantity and value again, for when the receipt itself is reversed.
    /// Fails when the pool no longer holds them, which means the goods were already sold on.
    /// </summary>
    Task<InventoryConsumption> TryReverseReceiptAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        decimal valueUsd,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Moving weighted average valuation, kept per (company, product, terminal).
///
/// Terminal is part of the key because goods at different terminals genuinely cost different
/// amounts to have got there. The consequence is that moving stock between terminals must move
/// its cost too; until a transfer path calls this service, a transfer changes quantity in the
/// legacy stock view without changing either pool, and the sale at the destination values
/// against whatever that pool already held.
///
/// The average is never stored — it is always TotalValueUsd / QuantityMt — so it cannot drift
/// from the two figures it derives from. Rounding happens once, on the cost handed to the
/// journal, and the pool is reduced by exactly that rounded figure so pool and journal can
/// never disagree by a cent.
/// </summary>
public sealed class InventoryValuationService(ApplicationDbContext db) : IInventoryValuationService
{
    public async Task ApplyReceiptAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        decimal valueUsd,
        CancellationToken cancellationToken = default)
    {
        if (quantityMt <= 0m || valueUsd < 0m)
            return;

        var pool = await FindPoolAsync(companyId, productId, terminalId, tracking: true, cancellationToken);
        if (pool is null)
        {
            db.InventoryAverageCosts.Add(new InventoryAverageCost
            {
                CompanyId = companyId,
                ProductId = productId,
                TerminalId = terminalId,
                QuantityMt = quantityMt,
                TotalValueUsd = valueUsd
            });
        }
        else
        {
            pool.QuantityMt += quantityMt;
            pool.TotalValueUsd += valueUsd;
            pool.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<InventoryConsumption> TryConsumeAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        CancellationToken cancellationToken = default)
    {
        if (quantityMt <= 0m)
            return new InventoryConsumption(false, 0m, "INVALID_QUANTITY");

        var pool = await FindPoolAsync(companyId, productId, terminalId, tracking: true, cancellationToken);
        if (pool is null || pool.QuantityMt <= 0m)
            return new InventoryConsumption(false, 0m, "INVENTORY_NOT_VALUED");
        if (pool.QuantityMt < quantityMt)
            return new InventoryConsumption(false, 0m, "INVENTORY_NOT_VALUED");

        var averageUnitCost = pool.AverageUnitCostUsd!.Value;

        // Taking the last of the pool must take the last of its value too: deriving the cost
        // from the average would leave a rounding crumb behind and slowly poison the average.
        var costUsd = pool.QuantityMt == quantityMt
            ? pool.TotalValueUsd
            : decimal.Round(quantityMt * averageUnitCost, 4, MidpointRounding.AwayFromZero);
        if (costUsd > pool.TotalValueUsd)
            costUsd = pool.TotalValueUsd;

        pool.QuantityMt -= quantityMt;
        pool.TotalValueUsd -= costUsd;
        pool.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new InventoryConsumption(true, costUsd, null);
    }

    public async Task ReturnAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        decimal costUsd,
        CancellationToken cancellationToken = default)
        => await ApplyReceiptAsync(companyId, productId, terminalId, quantityMt, costUsd, cancellationToken);

    public async Task<InventoryConsumption> TryReverseReceiptAsync(
        int companyId,
        int productId,
        int terminalId,
        decimal quantityMt,
        decimal valueUsd,
        CancellationToken cancellationToken = default)
    {
        if (quantityMt <= 0m)
            return new InventoryConsumption(false, 0m, "INVALID_QUANTITY");

        var pool = await FindPoolAsync(companyId, productId, terminalId, tracking: true, cancellationToken);
        if (pool is null || pool.QuantityMt < quantityMt || pool.TotalValueUsd < valueUsd)
        {
            // The goods this receipt brought in are no longer all here, so undoing it would
            // drive the pool negative and misprice everything left.
            return new InventoryConsumption(false, 0m, "INVENTORY_ALREADY_CONSUMED");
        }

        pool.QuantityMt -= quantityMt;
        pool.TotalValueUsd -= valueUsd;
        pool.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new InventoryConsumption(true, valueUsd, null);
    }

    private async Task<InventoryAverageCost?> FindPoolAsync(
        int companyId,
        int productId,
        int terminalId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = tracking
            ? db.InventoryAverageCosts
            : db.InventoryAverageCosts.AsNoTracking();
        return await query.SingleOrDefaultAsync(
            x => x.CompanyId == companyId && x.ProductId == productId && x.TerminalId == terminalId,
            cancellationToken);
    }
}
