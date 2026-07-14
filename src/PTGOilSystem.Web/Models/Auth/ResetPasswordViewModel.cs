using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Auth;

public class ResetPasswordViewModel
{
    public int UserId { get; set; }

    [Display(Name = "نام کاربری")]
    public string Username { get; set; } = "";

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
