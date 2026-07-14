using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Auth;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "رمز عبور فعلی اجباری است.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور فعلی")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "رمز عبور جدید اجباری است.")]
    [MinLength(8, ErrorMessage = "رمز عبور باید حداقل 8 کاراکتر باشد.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور جدید")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "تکرار رمز عبور جدید اجباری است.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "تکرار رمز عبور با رمز عبور جدید یکسان نیست.")]
    [Display(Name = "تکرار رمز عبور جدید")]
    public string ConfirmPassword { get; set; } = "";
}
