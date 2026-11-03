using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Models;

// Extends ASP.NET Core Identity's user (17.4).
// Identity already provides: Id, Email, UserName, PasswordHash,
// AccessFailedCount (FailedLoginCount), LockoutEnd, LockoutEnabled.
public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}