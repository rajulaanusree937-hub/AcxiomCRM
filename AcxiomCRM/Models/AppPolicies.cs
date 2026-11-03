namespace AcxiomCRM.Models;

// Authorization policies based on the permission matrix (7.1).
// Use like: [Authorize(Policy = AppPolicies.AdminOnly)]
public static class AppPolicies
{
    // User/Role management, Audit Log, system settings
    public const string AdminOnly = "AdminOnly";

    // Management reports, team pipeline
    public const string ManagerOrAdmin = "ManagerOrAdmin";

    // Customers, Leads, Opportunities, Follow-Ups, Activities, Dashboard
    public const string AllCrmUsers = "AllCrmUsers";
}