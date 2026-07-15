using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public class Account : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = "";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public AccountType AccountType { get; set; }
    public NormalBalance NormalBalance { get; set; }

    public int? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }
    public ICollection<Account> ChildAccounts { get; set; } = [];

    public bool IsControlAccount { get; set; }
    public bool AllowManualPosting { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public uint RowVersion { get; set; }
}
