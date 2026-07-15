namespace PTGOilSystem.Web.Data;

public enum DatabaseSafetyOperation
{
    Migration,
    Seeder,
    IntegrationTestCreate,
    IntegrationTestUse,
    IntegrationTestDrop
}

public sealed class UnsafeDatabaseOperationException : InvalidOperationException
{
    public UnsafeDatabaseOperationException(string databaseName, DatabaseSafetyOperation operation, string reason)
        : base($"Database operation '{operation}' is not allowed for database '{databaseName}': {reason}")
    {
        DatabaseName = databaseName;
        Operation = operation;
    }

    public string DatabaseName { get; }
    public DatabaseSafetyOperation Operation { get; }
}

public static class DatabaseSafetyGuard
{
    public const string AccountingTestDatabasePrefix = "ptg_oil_accounting_test_";

    private static readonly HashSet<string> SystemDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgres",
        "template0",
        "template1"
    };

    public static void EnsureMigrationAllowed(string? databaseName)
        => EnsureAllowed(databaseName, DatabaseSafetyOperation.Migration);

    public static void EnsureSeederAllowed(string? databaseName)
        => EnsureAllowed(databaseName, DatabaseSafetyOperation.Seeder);

    public static void EnsureIntegrationTestCreateAllowed(
        string? databaseName,
        bool isDevelopment = false,
        bool allowNonPrefixedDevelopmentDatabase = false)
        => EnsureAllowed(
            databaseName,
            DatabaseSafetyOperation.IntegrationTestCreate,
            isDevelopment,
            allowNonPrefixedDevelopmentDatabase);

    public static void EnsureIntegrationTestUseAllowed(
        string? databaseName,
        bool isDevelopment = false,
        bool allowNonPrefixedDevelopmentDatabase = false)
        => EnsureAllowed(
            databaseName,
            DatabaseSafetyOperation.IntegrationTestUse,
            isDevelopment,
            allowNonPrefixedDevelopmentDatabase);

    public static void EnsureIntegrationTestDropAllowed(string? databaseName)
        => EnsureAllowed(databaseName, DatabaseSafetyOperation.IntegrationTestDrop);

    private static void EnsureAllowed(
        string? databaseName,
        DatabaseSafetyOperation operation,
        bool isDevelopment = false,
        bool allowNonPrefixedDevelopmentDatabase = false)
    {
        var normalizedName = databaseName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new UnsafeDatabaseOperationException(
                "<empty>",
                operation,
                "a database name is required");
        }

        // These names are never overridable, including in Development.
        if (SystemDatabaseNames.Contains(normalizedName))
        {
            throw new UnsafeDatabaseOperationException(
                normalizedName,
                operation,
                "PostgreSQL system databases are protected");
        }

        var isTestOperation = operation is
            DatabaseSafetyOperation.IntegrationTestCreate or
            DatabaseSafetyOperation.IntegrationTestUse or
            DatabaseSafetyOperation.IntegrationTestDrop;

        if (!isTestOperation)
            return;

        var hasTestPrefix = normalizedName.StartsWith(
            AccountingTestDatabasePrefix,
            StringComparison.OrdinalIgnoreCase);
        if (hasTestPrefix)
            return;

        // Drop is always prefix-restricted. Create/use can only be overridden by
        // an explicit Development-only call; production code never passes this.
        var developmentOverrideAllowed = operation != DatabaseSafetyOperation.IntegrationTestDrop
            && isDevelopment
            && allowNonPrefixedDevelopmentDatabase;
        if (developmentOverrideAllowed)
            return;

        throw new UnsafeDatabaseOperationException(
            normalizedName,
            operation,
            $"integration-test databases must start with '{AccountingTestDatabasePrefix}'");
    }
}
