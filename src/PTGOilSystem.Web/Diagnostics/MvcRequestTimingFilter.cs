using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PTGOilSystem.Web.Diagnostics;

public sealed class MvcRequestTimingFilter : IAsyncResourceFilter
{
    private readonly ILogger<MvcRequestTimingFilter> _logger;
    private readonly MvcRequestTimingState _state;
    private readonly IHostEnvironment _environment;

    public MvcRequestTimingFilter(
        ILogger<MvcRequestTimingFilter> logger,
        MvcRequestTimingState state,
        IHostEnvironment environment)
    {
        _logger = logger;
        _state = state;
        _environment = environment;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!_environment.IsDevelopment())
        {
            await next();
            return;
        }

        _state.Reset();
        var stopwatch = Stopwatch.StartNew();
        ResourceExecutedContext? executedContext = null;
        Exception? exception = null;

        try
        {
            executedContext = await next();
            exception = executedContext.Exception;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var controller = descriptor?.ControllerName
                ?? context.RouteData.Values["controller"]?.ToString()
                ?? "Unknown";
            var action = descriptor?.ActionName
                ?? context.RouteData.Values["action"]?.ToString()
                ?? "Unknown";
            var request = context.HttpContext.Request;
            var response = context.HttpContext.Response;
            var path = request.Path.Value ?? "/";
            var statusCode = response.HasStarted ? response.StatusCode : executedContext?.HttpContext.Response.StatusCode ?? response.StatusCode;

            _logger.LogInformation(
                "MVC timing {Controller}/{Action} completed in {ElapsedMilliseconds} ms with {DbCommandCount} db commands for {Method} {Path} status {StatusCode}{ExceptionMarker}.",
                controller,
                action,
                stopwatch.ElapsedMilliseconds,
                _state.QueryCount,
                request.Method,
                path,
                statusCode,
                exception is null ? string.Empty : $" exception {exception.GetType().Name}");
        }
    }
}
