using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace PTGOilSystem.Web.Infrastructure.ModelBinding;

/// <summary>
/// تاریخ‌هایی که از query string یا فرم می‌آیند Kind=Unspecified دارند و Npgsql آن‌ها را
/// برای ستون‌های «timestamp with time zone» رد می‌کند. این binder همان نرمال‌سازی
/// ApplicationDbContext.NormalizeDateTime را روی ورودی‌های bind شده اعمال می‌کند تا
/// فیلترهای تاریخ در همهٔ صفحات بدون تغییر کنترلرها کار کنند.
/// </summary>
public sealed class UtcDateTimeModelBinder : IModelBinder
{
    private readonly IModelBinder _inner;

    public UtcDateTimeModelBinder(IModelBinder inner) => _inner = inner;

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        await _inner.BindModelAsync(bindingContext);

        if (!bindingContext.Result.IsModelSet || bindingContext.Result.Model is not DateTime value)
            return;

        var normalized = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        if (normalized != value || normalized.Kind != value.Kind)
            bindingContext.Result = ModelBindingResult.Success(normalized);
    }
}

public sealed class UtcDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.UnderlyingOrModelType;
        if (modelType != typeof(DateTime))
            return null;

        var loggerFactory = (ILoggerFactory)context.Services.GetService(typeof(ILoggerFactory))!;
        return new UtcDateTimeModelBinder(new SimpleTypeModelBinder(context.Metadata.ModelType, loggerFactory));
    }
}
