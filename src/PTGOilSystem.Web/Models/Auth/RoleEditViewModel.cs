using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Auth;

public class RoleEditViewModel : RoleCreateViewModel
{
    public int Id { get; set; }

    [Display(Name = "نقش اصلی سیستم")]
    public bool IsBuiltInAdmin { get; set; }
}
