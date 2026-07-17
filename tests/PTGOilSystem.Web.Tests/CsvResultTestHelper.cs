using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// خروجی CSV به‌صورت جریانی روی Response.Body نوشته می‌شود (نه FileContentResult)،
/// پس برای بررسی محتوا باید نتیجه را واقعاً اجرا کرد.
/// </summary>
internal static class CsvResultTestHelper
{
    public static async Task<(byte[] Bytes, string? ContentType)> ExecuteAsync(IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        using var body = new MemoryStream();
        httpContext.Response.Body = body;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        await result.ExecuteResultAsync(actionContext);

        return (body.ToArray(), httpContext.Response.ContentType);
    }
}
