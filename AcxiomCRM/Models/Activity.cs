using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.Models;

public class Activity : IOwnedEntity
{
    public int ActivityId { get; set; }

    public ActivityType ActivityType { get; set; } = ActivityType.Call;

    [Required, MaxLength(150)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)] public string? Description { get; set; }

    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    [MaxLength(450)] public string? AssignedTo { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}