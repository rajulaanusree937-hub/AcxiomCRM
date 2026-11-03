using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    public int AuditLogId { get; set; }

    [MaxLength(450)] public string? UserId { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;      // Login, FailedLogin, Create, Update, Delete...

    [Required, MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;  // Customer, Lead, User...

    [MaxLength(50)] public string? RecordId { get; set; }

    public string? OldValue { get; set; }   // JSON snapshot before change
    public string? NewValue { get; set; }   // JSON snapshot after change

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(45)] public string? IpAddress { get; set; }
}