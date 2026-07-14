using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Auth;

public class UserEditViewModel
{
    public int Id { get; set; }

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
}
