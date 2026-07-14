using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Helpers;

public static class StorageTankDisplay
{
    public static string Build(StorageTank tank)
        => Build(tank.Id, tank.DisplayName, tank.TankCode);

    public static string? BuildOptional(StorageTank? tank)
        => tank is null ? null : Build(tank);

    public static string Build(int id, string? displayName, string? tankCode)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(tankCode))
        {
            return tankCode.Trim();
        }

        return $"مخزن #{id}";
    }

    public static async Task<List<StorageTankDisplayOption>> LoadOptionsAsync(
        IQueryable<StorageTank> query,
        CancellationToken cancellationToken = default)
    {
        var tanks = await query
            .Select(t => new
            {
                t.Id,
                t.TerminalId,
                t.DisplayName,
                t.TankCode
            })
            .ToListAsync(cancellationToken);

        return tanks
            .Select(t => new StorageTankDisplayOption(
                t.Id,
                t.TerminalId,
                Build(t.Id, t.DisplayName, t.TankCode),
                t.TankCode))
            .ToList();
    }

    public static async Task<IReadOnlyDictionary<int, string>> LoadNamesAsync(
        IQueryable<StorageTank> query,
        CancellationToken cancellationToken = default)
        => (await LoadOptionsAsync(query, cancellationToken))
            .ToDictionary(t => t.Id, t => t.Display);

    public static string? Resolve(
        IReadOnlyDictionary<int, string> namesById,
        int? storageTankId,
        string? fallbackCode)
        => storageTankId.HasValue && namesById.TryGetValue(storageTankId.Value, out var display)
            ? display
            : fallbackCode;
}

public sealed record StorageTankDisplayOption(
    int Id,
    int TerminalId,
    string Display,
    string TankCode);
