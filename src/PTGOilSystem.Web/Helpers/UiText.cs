using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace PTGOilSystem.Web.Helpers;

public static class UiText
{
    public static bool IsEn(HttpContext? ctx)
    {
        if (ctx is not null)
        {
            var lang = ctx.Request.Cookies["ptg-ui-lang"] ?? "fa";
            return string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);
    }

    public static string T(HttpContext? ctx, string fa, string en)
        => IsEn(ctx) ? en : fa;

    public static string T(string fa, string en)
        => IsEn(null) ? en : fa;
}
