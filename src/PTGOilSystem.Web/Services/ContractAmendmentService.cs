using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Audit;
using PTGOilSystem.Web.Services.Exceptions;

namespace PTGOilSystem.Web.Services;

public class ContractAmendmentService : IContractAmendmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ContractAmendmentService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ContractAmendment> AddAmendmentAsync(
        int contractId,
        string changeSummary,
        decimal? newQuantityMt = null,
        decimal? newUnitPriceUsd = null,
        decimal? newPremiumUsd = null,
        DateTime? amendmentDate = null,
        int? actorUserId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(changeSummary))
        {
            throw new BusinessRuleException(
                "AMENDMENT_SUMMARY_REQUIRED",
                "خلاصهٔ تغییر برای امندمنت اجباری است.");
        }

        var contract = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == contractId)
            .Select(c => new { c.Id, c.ContractNumber })
            .FirstOrDefaultAsync(ct);

        if (contract is null)
        {
            throw new BusinessRuleException(
                "CONTRACT_NOT_FOUND",
                $"قرارداد با شناسهٔ {contractId} یافت نشد.");
        }

        // Auto-generate the next amendment number per contract: A-001, A-002, ...
        // (system rule #13: amendments are dated and traceable.)
        var existingCount = await _db.ContractAmendments
            .CountAsync(a => a.ContractId == contractId, ct);

        var nextNumber = $"A-{(existingCount + 1):D3}";

        var amendment = new ContractAmendment
        {
            ContractId = contractId,
            AmendmentNumber = nextNumber,
            AmendmentDate = amendmentDate ?? DateTime.UtcNow,
            ChangeSummary = changeSummary.Trim(),
            NewQuantityMt = newQuantityMt,
            NewUnitPriceUsd = newUnitPriceUsd,
            NewPremiumUsd = newPremiumUsd,
        };

        _db.ContractAmendments.Add(amendment);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            entityName: nameof(ContractAmendment),
            entityId: amendment.Id,
            action: AuditAction.Insert,
            actorUserId: actorUserId,
            diff: AuditDiffFormatter.ForCreate(
                ("ContractId", amendment.ContractId),
                ("AmendmentNumber", amendment.AmendmentNumber),
                ("AmendmentDate", amendment.AmendmentDate),
                ("ChangeSummary", amendment.ChangeSummary),
                ("NewQuantityMt", amendment.NewQuantityMt),
                ("NewUnitPriceUsd", amendment.NewUnitPriceUsd),
                ("NewPremiumUsd", amendment.NewPremiumUsd)),
            ct: ct);

        // Audit trail (system rule #11) — record on the Contract since the
        // amendment is the change-vehicle for that contract.
        await _audit.LogAsync(
            entityName: nameof(Contract),
            entityId: contractId,
            action: AuditAction.Update,
            actorUserId: actorUserId,
            diff: $"Amendment {nextNumber}: {amendment.ChangeSummary}",
            ct: ct);

        await _db.SaveChangesAsync(ct);

        return amendment;
    }
}
