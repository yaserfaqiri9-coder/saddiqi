using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Auth;

public class UserCreateViewModel
{
    [Required(ErrorMessage = "نام کاربری اجباری است.")]
    [Display(Name = "نام کاربری")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "نام کامل اجباری است.")]
    [Display(Name = "نام کامل")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "نقش اجباری است.")]
    [Display(Name = "نقش")]
    public int? RoleId { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "رمز عبور اجباری است.")]
    [MinLength(8, ErrorMessage = "رمز عبور باید حداقل 8 کاراکتر باشد.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "تکرار رمز عبور اجباری است.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "تکرار رمز عبور با رمز عبور جدید یکسان نیست.")]
    [Display(Name = "تکرار رمز عبور")]
    public string ConfirmPassword { get; set; } = "";
}
