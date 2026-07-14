namespace PTGOilSystem.Web.Security;

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";

    public const string ManageData = Admin + "," + Manager + "," + Operator;
    public const string All = ManageData + "," + Viewer;

    public static readonly string[] AllRoles = [Admin, Manager, Operator, Viewer];
}
