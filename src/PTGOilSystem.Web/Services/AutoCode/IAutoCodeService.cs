namespace PTGOilSystem.Web.Services.AutoCode;

public interface IAutoCodeService
{
    Task<string> NextAsync(AutoCodeKind kind, CancellationToken ct = default);
}
