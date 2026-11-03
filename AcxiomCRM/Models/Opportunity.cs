using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Models;

public class Opportunity
{
    public int OpportunityId { get; set; }

    [Required, MaxLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;
    public int Probability { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;

    public LeadSource? Source { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }

    [MaxLength(450)] public string? AssignedTo { get; set; }


    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
}