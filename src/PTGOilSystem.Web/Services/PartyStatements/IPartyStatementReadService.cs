using PTGOilSystem.Web.Models.PartyStatements;

namespace PTGOilSystem.Web.Services.PartyStatements;

public interface IPartyStatementReadService
{
    Task<PartyStatementResult> GetStatementAsync(
        PartyRef party,
        PartyStatementFilter filter,
        CancellationToken cancellationToken = default);
}

public interface IPartyStatementPolicyResolver
{
    PartyStatementPolicy Resolve(PartyStatementPartyType partyType);
}
