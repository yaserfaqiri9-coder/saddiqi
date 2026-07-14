using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class AuthBootstrapperTests
{
    [Fact]
    public async Task EnsureSeedDataAsync_Creates_Roles_And_Admin_When_No_Users_Exist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var userService = new UserService(db);
        var bootstrapper = new AuthBootstrapper(db, userService, NullLogger<AuthBootstrapper>.Instance);

        await bootstrapper.EnsureSeedDataAsync(new BootstrapAdminOptions
        {
            Username = "bootstrap-admin",
            FullName = "Bootstrap Admin",
            Password = "StrongPass123!"
        });

        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
        var admin = await db.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal("bootstrap-admin", admin.Username);
        Assert.Equal(AuthRoles.Admin, admin.Role!.Name);
    }
}
