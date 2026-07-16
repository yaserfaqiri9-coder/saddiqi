using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

/// <summary>
/// The moving weighted average cost pool for one (company, product, terminal).
///
/// The journal alone cannot answer "what does a tonne cost right now": journal lines carry
/// money, not quantity. So the pool keeps both, and the average is always TotalValueUsd divided
/// by QuantityMt — never stored, so it can never drift from the two numbers it comes from.
///
/// A receipt adds quantity and value; a sale consumes quantity at the current average and takes
/// exactly that much value out. This is the only valuation authority: InventoryLineage tracks
/// where goods came from and how much, and deliberately does not value them.
/// </summary>
public class InventoryAverageCost : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int TerminalId { get; set; }
    public Terminal? Terminal { get; set; }

    public decimal QuantityMt { get; set; }
    public decimal TotalValueUsd { get; set; }

    /// <summary>
    /// Cost per MT, derived on read so it can never disagree with the pool it came from.
    /// Null when the pool is empty, which is what stops a sale from valuing against nothing.
    /// </summary>
    public decimal? AverageUnitCostUsd
        => QuantityMt > 0m
            ? decimal.Round(TotalValueUsd / QuantityMt, 6, MidpointRounding.AwayFromZero)
            : null;

    // Concurrency guard: two receipts landing on the same pool must not lose one another.
    public uint RowVersion { get; set; }
}
