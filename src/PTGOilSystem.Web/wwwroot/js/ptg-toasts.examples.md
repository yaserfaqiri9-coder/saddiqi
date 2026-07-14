# PTG Toast + Confirm — usage examples

Global toast system. Partial `_ToastNotifications` is rendered once in `_Layout`.
No controller change is required for existing code: legacy `TempData["ok"]`,
`TempData["err"]`, `TempData["error"]` are auto-upgraded to toasts.

## Server side (Controller, TempData contract)

```csharp
// Create success
TempData["Toast.Success"] = "قرارداد جدید با موفقیت ثبت شد.";
return RedirectToAction(nameof(Index));

// Edit success
TempData["Toast.Success"] = "تغییرات مشتری ذخیره شد.";
return RedirectToAction(nameof(Details), new { id });

// Delete success
TempData["Toast.Success"] = "رکورد حذف شد.";
return RedirectToAction(nameof(Index));

// Delete blocked / error
TempData["Toast.Error"] = "این جنس در قراردادها استفاده شده و قابل حذف نیست.";
return RedirectToAction(nameof(Index));

// Warning before a destructive/late action
TempData["Toast.Warning"] = "نرخ ارز این روز قبلاً قفل شده است؛ با احتیاط ویرایش کنید.";

// Legacy keys still work (no change needed):
TempData["ok"]  = "...";   // -> success toast
TempData["err"] = "...";   // -> error toast
```

## Client side (programmatic)

```js
ptgToast("success", "ذخیره شد.");
ptgToast("error", "خطا در ارتباط با سرور.", "خطا");
ptgToast("warning", "اتصال ضعیف است.");
ptgToast("info", "نسخه جدید در دسترس است.", "اطلاع", { duration: 8000 });
```

## Delete / destructive confirm (replaces browser confirm)

```html
<!-- Form: dialog appears on submit; submits only after confirm -->
<form asp-action="Delete" asp-route-id="@item.Id" method="post"
      data-ptg-confirm="true"
      data-ptg-confirm-title="حذف رکورد"
      data-ptg-confirm-message="آیا از حذف این رکورد مطمئن هستید؟">
    @Html.AntiForgeryToken()
    <button type="submit" class="oa-icon-btn oa-icon-btn-deactivate">حذف</button>
</form>

<!-- Link -->
<a href="@Url.Action("Delete", new { id })"
   data-ptg-confirm="true"
   data-ptg-confirm-message="حذف شود؟">حذف</a>
```

If no dialog markup is present the behavior falls back to native `confirm()`.

> Validation errors stay inside the form near fields (asp-validation-*). Toasts are
> only for global operation-result messages — never hide field validation in a toast.
