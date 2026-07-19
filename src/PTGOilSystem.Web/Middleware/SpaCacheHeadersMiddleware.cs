using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace PTGOilSystem.Web.Middleware;

/// <summary>
/// spa-nav.js درخواست‌هایش را با هدر <c>X-PTG-SPA: 1</c> می‌فرستد و _Layout برای
/// همان درخواست‌ها نسخهٔ سبک‌شده (بدون باندل CSS/JS پایه و بدنهٔ shell) رندر می‌کند.
/// هر دو نسخه زیر یک URL هستند؛ بدون <c>Vary</c> مرورگر می‌تواند در ناوبری
/// Back/Forward نسخهٔ سبک‌شده را از HTTP cache به‌جای صفحهٔ کامل نمایش دهد و
/// صفحه بدون استایل رندر شود.
/// - همهٔ پاسخ‌های HTML هدر <c>Vary: X-PTG-SPA</c> می‌گیرند (append؛ مقدار قبلی حفظ می‌شود).
/// - فقط پاسخ‌های سبک‌شده (درخواست دارای <c>X-PTG-SPA: 1</c>) هدر
///   <c>Cache-Control: no-store</c> می‌گیرند تا هرگز خارج از spa-nav بازاستفاده نشوند.
/// پاسخ‌های غیر HTML (CSS/JS/تصویر/فایل) دست‌نخورده می‌مانند.
/// </summary>
public sealed class SpaCacheHeadersMiddleware
{
    private const string SpaHeaderName = "X-PTG-SPA";

    private readonly RequestDelegate _next;

    public SpaCacheHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            var response = httpContext.Response;

            var isHtml = response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
            if (!isHtml)
            {
                return Task.CompletedTask;
            }

            var hasSpaVary = response.Headers.Vary.Any(value =>
                value is not null && value.Contains(SpaHeaderName, StringComparison.OrdinalIgnoreCase));
            if (!hasSpaVary)
            {
                response.Headers.Append(HeaderNames.Vary, SpaHeaderName);
            }

            var isSpaRequest = string.Equals(httpContext.Request.Headers[SpaHeaderName], "1", StringComparison.Ordinal);
            if (isSpaRequest && StringValues.IsNullOrEmpty(response.Headers.CacheControl))
            {
                response.Headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        }, context);

        return _next(context);
    }
}
