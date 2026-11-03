using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.Models;

public class FollowUp : IOwnedEntity
{
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public DateTime FollowUpDate { get; set; }

    [Required, MaxLength(150)]
    public string Subject { get; set; } = string.Empty;

    public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

    [MaxLength(1000)] public string? Remarks { get; set; }

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    [MaxLength(450)] public string? AssignedTo { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}