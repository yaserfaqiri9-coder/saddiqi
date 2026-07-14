using System;
using System.Threading;
using System.Threading.Tasks;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services;

/// <summary>
/// Adds new <see cref="ContractAmendment"/> rows to a contract while honouring
/// system rule #13 — amendments are immutable, dated, and traceable. The
/// service never edits or deletes prior amendments; new amendments only
/// stack on top.
/// </summary>
public interface IContractAmendmentService
{
    /// <summary>
    /// Records a new amendment for the given contract.
    /// </summary>
    /// <param name="contractId">Existing contract id. The contract must exist.</param>
    /// <param name="changeSummary">Required, free-form Persian summary of the change.</param>
    /// <param name="newQuantityMt">Optional new quantity (MT).</param>
    /// <param name="newUnitPriceUsd">Optional new unit price (USD/MT).</param>
    /// <param name="newPremiumUsd">Optional new premium (USD/MT).</param>
    /// <param name="amendmentDate">Optional date; defaults to UTC now.</param>
    /// <param name="actorUserId">Optional acting user for the audit trail.</param>
    /// <returns>The persisted <see cref="ContractAmendment"/> with its new id.</returns>
    Task<ContractAmendment> AddAmendmentAsync(
        int contractId,
        string changeSummary,
        decimal? newQuantityMt = null,
        decimal? newUnitPriceUsd = null,
        decimal? newPremiumUsd = null,
        DateTime? amendmentDate = null,
        int? actorUserId = null,
        CancellationToken ct = default);
}
