using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services;

namespace PTGOilSystem.Web.Middleware;

public sealed class ActivityLogMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogMiddleware> _logger;

    public ActivityLogMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityLogMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? pipelineException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            pipelineException = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (ShouldLog(context))
            {
                try
                {
                    await PersistAuditLogAsync(context, stopwatch.ElapsedMilliseconds, pipelineException);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Activity log write failed for {Method} {Path}.", context.Request.Method, context.Request.Path);
                }
            }
        }
    }

    private static bool ShouldLog(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var descriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (descriptor is null)
        {
            return false;
        }

        if (string.Equals(descriptor.ControllerName, "Auth", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return context.User.Identity?.IsAuthenticated == true;
    }

    private async Task PersistAuditLogAsync(HttpContext context, long durationMs, Exception? pipelineException)
    {
        using var scope = _scopeFactory.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var currentUser = scope.ServiceProvider.GetRequiredService<PTGOilSystem.Web.Security.ICurrentUserContext>();
        var endpoint = context.GetEndpoint();
        var descriptor = endpoint!.Metadata.GetMetadata<ControllerActionDescriptor>()!;
        var statusCode = context.Response.StatusCode;
        var metadataJson = BuildMetadataJson(context, descriptor, pipelineException);
        var actionLabel = ResolveRequestAction(context.Request.Method);

        await audit.LogActivityAndSaveAsync(new AuditLogEntryInput
        {
            Category = AuditLogCategories.Request,
            EntityName = descriptor.ControllerName,
            EntityId = 0,
            Action = actionLabel,
            ActorUserId = currentUser.UserId,
            ActorUsername = currentUser.Username,
            Module = descriptor.ControllerName,
            Description = BuildDescription(context, descriptor, pipelineException),
            HttpMethod = context.Request.Method,
            RequestPath = context.Request.Path.Value,
            ControllerName = descriptor.ControllerName,
            ActionName = descriptor.ActionName,
            StatusCode = statusCode,
            IsSuccess = pipelineException is null && statusCode is >= 200 and < 400,
            CorrelationId = context.TraceIdentifier,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            DurationMs = durationMs,
            MetadataJson = metadataJson,
        });
    }

    private static string ResolveRequestAction(string method)
        => method.ToUpperInvariant() switch
        {
            "GET" => "GET",
            "POST" => "POST",
            "PUT" => "PUT",
            "PATCH" => "PATCH",
            "DELETE" => "DELETE",
            _ => method.ToUpperInvariant()
        };

    private static string BuildDescription(HttpContext context, ControllerActionDescriptor descriptor, Exception? pipelineException)
    {
        var baseText = $"{context.Request.Method.ToUpperInvariant()} {descriptor.ControllerName}/{descriptor.ActionName}";
        if (pipelineException is null)
        {
            return baseText;
        }

        return $"{baseText} failed: {pipelineException.GetType().Name}";
    }

    private static string BuildMetadataJson(HttpContext context, ControllerActionDescriptor descriptor, Exception? pipelineException)
    {
        var metadata = new
        {
            RouteValues = context.Request.RouteValues
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value?.ToString()),
            Query = context.Request.Query.ToDictionary(
                item => item.Key,
                item => item.Value.ToString()),
            Endpoint = descriptor.DisplayName,
            Exception = pipelineException is null
                ? null
                : new
                {
                    Type = pipelineException.GetType().FullName,
                    pipelineException.Message
                }
        };

        return JsonSerializer.Serialize(metadata, JsonOptions);
    }
}