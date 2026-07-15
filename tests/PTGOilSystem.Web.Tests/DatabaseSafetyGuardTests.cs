using PTGOilSystem.Web.Data;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public sealed class DatabaseSafetyGuardTests
{
    [Theory]
    [InlineData("postgres")]
    [InlineData("template0")]
    [InlineData("template1")]
    public void PostgreSql_System_Databases_Are_Always_Rejected(string databaseName)
    {
        Assert.Throws<UnsafeDatabaseOperationException>(
            () => DatabaseSafetyGuard.EnsureMigrationAllowed(databaseName));
        Assert.Throws<UnsafeDatabaseOperationException>(
            () => DatabaseSafetyGuard.EnsureIntegrationTestCreateAllowed(
                databaseName,
                isDevelopment: true,
                allowNonPrefixedDevelopmentDatabase: true));
    }

    [Fact]
    public void Correct_Accounting_Test_Prefix_Is_Accepted()
        => DatabaseSafetyGuard.EnsureIntegrationTestCreateAllowed(
            $"{DatabaseSafetyGuard.AccountingTestDatabasePrefix}safe");

    [Fact]
    public void Test_Database_Without_Prefix_Is_Rejected()
        => Assert.Throws<UnsafeDatabaseOperationException>(
            () => DatabaseSafetyGuard.EnsureIntegrationTestUseAllowed("ptg_oil_production"));

    [Fact]
    public void Non_Test_Database_Drop_Is_Rejected()
        => Assert.Throws<UnsafeDatabaseOperationException>(
            () => DatabaseSafetyGuard.EnsureIntegrationTestDropAllowed("ptg_oil_production"));
}
