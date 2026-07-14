using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;

namespace PTGOilSystem.Web.Services.AutoCode;

public sealed class AutoCodeService : IAutoCodeService
{
    private readonly ApplicationDbContext _db;

    public AutoCodeService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> NextAsync(AutoCodeKind kind, CancellationToken ct = default)
    {
        return kind switch
        {
            AutoCodeKind.Company => BuildNext("CO", await CodesAsync(_db.Companies.AsNoTracking().Select(x => (string?)x.Code), ct)),
            AutoCodeKind.Partner => BuildNext("PA", await CodesAsync(_db.Partners.AsNoTracking().Select(x => (string?)x.Code), ct)),
            AutoCodeKind.Customer => BuildNext("CU", await CodesAsync(_db.Customers.AsNoTracking().Select(x => x.Code), ct)),
            AutoCodeKind.Supplier => BuildNext("SU", await CodesAsync(_db.Suppliers.AsNoTracking().Select(x => x.Code), ct)),
            AutoCodeKind.Product => BuildNext("PR", await CodesAsync(_db.Products.AsNoTracking().Select(x => (string?)x.Code), ct)),
            AutoCodeKind.ExpenseType => BuildNext("EX", await CodesAsync(_db.ExpenseTypes.AsNoTracking().Select(x => (string?)x.Code), ct)),
            AutoCodeKind.Terminal => BuildNext("TE", await CodesAsync(_db.Terminals.AsNoTracking().Select(x => (string?)x.Code), ct)),
            AutoCodeKind.Location => BuildNext("LO", await CodesAsync(_db.Locations.AsNoTracking().Select(x => x.Code), ct)),
            AutoCodeKind.Vessel => BuildNext("VE", await CodesAsync(_db.Vessels.AsNoTracking().Select(x => x.Code), ct)),
            AutoCodeKind.CashAccount => BuildNext("CA", await CodesAsync(_db.CashAccounts.AsNoTracking().Select(x => (string?)x.Code), ct)),
            AutoCodeKind.ServiceProvider => BuildNext("SP", await CodesAsync(_db.ServiceProviders.AsNoTracking().Select(x => x.Code), ct)),
            AutoCodeKind.Employee => BuildNext("EMP", await CodesAsync(_db.Employees.AsNoTracking().Select(x => (string?)x.EmployeeCode), ct)),
            AutoCodeKind.StorageTank => BuildNext("TK", await CodesAsync(_db.StorageTanks.AsNoTracking().Select(x => (string?)x.TankCode), ct)),
            AutoCodeKind.OperationalAsset => BuildNext("AS", await CodesAsync(_db.OperationalAssets.AsNoTracking().Select(x => (string?)x.AssetCode), ct)),
            AutoCodeKind.Shipment => BuildNext("SH", await CodesAsync(_db.Shipments.AsNoTracking().Select(x => (string?)x.ShipmentCode), ct)),
            AutoCodeKind.SalesInvoice => BuildNext("INV", await CodesAsync(_db.SalesTransactions.AsNoTracking().Select(x => (string?)x.InvoiceNumber), ct)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static async Task<IReadOnlyCollection<string?>> CodesAsync(IQueryable<string?> query, CancellationToken ct)
        => await query.ToListAsync(ct);

    private static string BuildNext(string prefix, IReadOnlyCollection<string?> existingCodes)
    {
        var existing = existingCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var max = 0;
        foreach (var code in existing)
        {
            if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = code[prefix.Length..];
            if (int.TryParse(suffix, out var sequence) && sequence > max)
            {
                max = sequence;
            }
        }

        string candidate;
        do
        {
            max++;
            candidate = $"{prefix}{max:000}";
        }
        while (existing.Contains(candidate));

        return candidate;
    }
}
