using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PTGOilSystem.Web.Security;

public sealed class RoleNavigationAuthorizationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var controller = context.RouteData.Values["controller"]?.ToString();
        if (string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (RoleAccessRules.CanAccessController(user, controller))
        {
            await next();
            return;
        }

        context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
    }
}
