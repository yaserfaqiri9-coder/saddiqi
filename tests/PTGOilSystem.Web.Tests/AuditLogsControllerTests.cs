using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Audit;
using PTGOilSystem.Web.Models.Entities;
using Xunit;

namespace PTGOilSystem.Web.Tests;

public class AuditLogsControllerTests
{
    [Fact]
    public async Task Index_Filters_Logs_By_Category_And_Success()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Category = AuditLogCategories.Request,
                EntityName = "Contracts",
                Action = "GET",
                Module = "Contracts",
                IsSuccess = true,
                RequestPath = "/Contracts",
                ActionAtUtc = new DateTime(2026, 5, 16, 8, 0, 0, DateTimeKind.Utc)
            },
            new AuditLog
            {
                Category = AuditLogCategories.Authentication,
                EntityName = nameof(User),
                Action = "LoginFailed",
                Module = "Auth",
                IsSuccess = false,
                RequestPath = "/Auth/Login",
                ActionAtUtc = new DateTime(2026, 5, 16, 9, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var controller = new AuditLogsController(db);

        var result = await controller.Index(category: AuditLogCategories.Authentication, success: "false", q: null, module: null, action: null, fromUtc: null, toUtc: null, limit: null, page: 1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ActivityLogIndexViewModel>(view.Model);
        var item = Assert.Single(model.Items);
        Assert.Equal("LoginFailed", item.Action);
        Assert.Equal(AuditLogCategories.Authentication, item.Category);
        Assert.False(item.IsSuccess);
    }

    [Fact]
    public async Task Details_Returns_Requested_Log()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var audit = new AuditLog
        {
            Category = AuditLogCategories.Request,
            EntityName = "Sales",
            Action = "POST",
            Description = "POST Sales/Create",
            ControllerName = "Sales",
            ActionName = "Create",
            RequestPath = "/Sales/Create",
            StatusCode = 200,
            IsSuccess = true,
            MetadataJson = "{}"
        };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();

        var controller = new AuditLogsController(db);

        var result = await controller.Details(audit.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ActivityLogDetailsViewModel>(view.Model);
        Assert.Equal("Sales", model.EntityName);
        Assert.Equal("POST", model.Action);
        Assert.Equal("/Sales/Create", model.RequestPath);
    }

    [Fact]
    public async Task Index_Filters_By_User_And_Severity_With_Human_Summary()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Category = AuditLogCategories.Entity,
                EntityName = "Contract",
                EntityId = 45,
                Action = "Update",
                Module = "Contracts",
                ActorUsername = "manager",
                Diff = "Update: Price: 610 -> 625",
                IsSuccess = true,
                ActionAtUtc = new DateTime(2026, 5, 16, 8, 0, 0, DateTimeKind.Utc)
            },
            new AuditLog
            {
                Category = AuditLogCategories.Request,
                EntityName = "Home",
                Action = "GET",
                Module = "Home",
                ActorUsername = "operator",
                IsSuccess = true,
                ActionAtUtc = new DateTime(2026, 5, 16, 9, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var controller = new AuditLogsController(db);

        var result = await controller.Index(user: "manager", severity: "sensitive");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ActivityLogIndexViewModel>(view.Model);
        var item = Assert.Single(model.Items);
        Assert.Equal("sensitive", item.Severity);
        Assert.Equal("حساس", item.SeverityLabel);
        Assert.Equal("Contracts", item.RelatedController);
        Assert.Contains("قیمت", item.HumanSummary);
        Assert.Contains("610", item.HumanSummary);
        Assert.Equal(1, model.SensitiveCount);
        Assert.Equal(1, model.ActiveUserCount);
    }

    [Fact]
    public async Task Details_Loads_User_Role_And_Field_Level_Changes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Roles.Add(new Role { Id = 1, Name = "Manager" });
        db.Users.Add(new User
        {
            Id = 7,
            Username = "manager",
            FullName = "System Manager",
            PasswordHash = "hash",
            RoleId = 1
        });
        var audit = new AuditLog
        {
            Category = AuditLogCategories.Entity,
            EntityName = "Contract",
            EntityId = 45,
            Action = "Update",
            Module = "Contracts",
            ActorUserId = 7,
            ActorUsername = "manager",
            Diff = "Update: Price: 610 -> 625 | Quantity: 100 -> 120",
            IsSuccess = true,
        };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();

        var controller = new AuditLogsController(db);

        var result = await controller.Details(audit.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ActivityLogDetailsViewModel>(view.Model);
        Assert.Equal("Manager", model.ActorRoleName);
        Assert.Equal("Contracts", model.RelatedController);
        Assert.Equal(45, model.RelatedId);
        Assert.Equal(2, model.Changes.Count);
        Assert.Contains(model.Changes, change => change.Field == "قیمت" && change.Before == "610" && change.After == "625");
        Assert.Contains("قیمت", model.HumanSummary);
    }

    [Fact]
    public async Task Index_Normalizes_Unspecified_Date_Filters_And_Includes_Entire_To_Date()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Category = AuditLogCategories.Request,
                EntityName = "Contracts",
                Action = "GET",
                RequestPath = "/Contracts",
                ActionAtUtc = new DateTime(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc)
            },
            new AuditLog
            {
                Category = AuditLogCategories.Request,
                EntityName = "Contracts",
                Action = "POST",
                RequestPath = "/Contracts/Create",
                ActionAtUtc = new DateTime(2026, 5, 16, 23, 59, 59, DateTimeKind.Utc)
            },
            new AuditLog
            {
                Category = AuditLogCategories.Request,
                EntityName = "Contracts",
                Action = "DELETE",
                RequestPath = "/Contracts/Delete",
                ActionAtUtc = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var controller = new AuditLogsController(db);

        var result = await controller.Index(
            q: null,
            category: null,
            module: null,
            action: null,
            success: null,
            fromUtc: new DateTime(2026, 5, 16),
            toUtc: new DateTime(2026, 5, 16),
            limit: null,
            page: 1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ActivityLogIndexViewModel>(view.Model);

        Assert.Equal(2, model.TotalCount);
        Assert.Equal(2, model.Items.Count);
        Assert.Equal(1, model.PageCount);
        Assert.All(model.Items, item => Assert.Equal(new DateTime(2026, 5, 16), item.ActionAtUtc.Date));
        Assert.Equal(DateTimeKind.Utc, model.Filter.FromUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, model.Filter.ToUtc!.Value.Kind);
    }

    [Fact]
    public async Task Index_Default_Request_Returns_Recent_Logs()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Category = AuditLogCategories.Request,
                EntityName = "AuditLogs",
                Action = "GET",
                Module = "AuditLogs",
                RequestPath = "/AuditLogs",
                IsSuccess = true,
                ActionAtUtc = new DateTime(2026, 5, 19, 8, 0, 0, DateTimeKind.Utc)
            },
            new AuditLog
            {
                Category = AuditLogCategories.Entity,
                EntityName = "Employee",
                Action = "Insert",
                Module = "Employee",
                IsSuccess = true,
                ActionAtUtc = new DateTime(2026, 5, 19, 7, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var controller = new AuditLogsController(db);

        var result = await controller.Index(
            q: null,
            category: null,
            module: null,
            action: null,
            success: null,
            fromUtc: null,
            toUtc: null,
            limit: null,
            page: 1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ActivityLogIndexViewModel>(view.Model);

        Assert.Equal(2, model.TotalCount);
        Assert.Equal(2, model.Items.Count);
        Assert.Equal(1, model.PageCount);
        Assert.Equal("AuditLogs", model.Items[0].Module);
        Assert.True((bool)controller.ViewData["HideSectionTabs"]!);
    }
}
