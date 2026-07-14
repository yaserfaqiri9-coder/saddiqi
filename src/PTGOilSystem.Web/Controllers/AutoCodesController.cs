using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services.AutoCode;

namespace PTGOilSystem.Web.Controllers;

[Authorize(Policy = AuthPolicies.ManageData)]
public sealed class AutoCodesController : Controller
{
    private readonly IAutoCodeService _autoCodes;

    public AutoCodesController(IAutoCodeService autoCodes)
    {
        _autoCodes = autoCodes;
    }

    [HttpGet]
    public async Task<IActionResult> Preview(string? kind)
    {
        if (!Enum.TryParse<AutoCodeKind>(kind, ignoreCase: true, out var resolved))
        {
            return BadRequest(new { message = "Unsupported auto code kind." });
        }

        Response.Headers.CacheControl = "no-store";
        var code = await _autoCodes.NextAsync(resolved, HttpContext.RequestAborted);
        return Json(new { code });
    }
}
