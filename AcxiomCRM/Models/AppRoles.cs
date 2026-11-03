namespace AcxiomCRM.Models;

// The three roles from Section 7 / 17.10.
// Using constants avoids typos like "Admn" in [Authorize(Roles = ...)].
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";

    public static readonly string[] All = { Admin, Manager, SalesExecutive };
}