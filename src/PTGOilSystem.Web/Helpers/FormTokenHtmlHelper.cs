using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTGOilSystem.Web.Helpers;

/// <summary>
/// Renders the idempotency token hidden field inside a create/post form.
/// A fresh token is generated per GET render; a validation re-render simply gets
/// a new (still-unconsumed) token, so retries after fixing errors work normally.
/// </summary>
public static class FormTokenHtmlHelper
{
    /// <summary>POST field name carrying the idempotency token.</summary>
    public const string FieldName = "__FormToken";

    public static IHtmlContent FormToken(this IHtmlHelper html)
    {
        var token = Guid.NewGuid().ToString("N");
        return new HtmlString(
            $"<input type=\"hidden\" name=\"{FieldName}\" value=\"{token}\" autocomplete=\"off\" />");
    }
}
