using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace AcxiomCRM.Models;

public class Lead : IOwnedEntity
{
    public int LeadId { get; set; }

    [Required, MaxLength(20)]
    public string LeadCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(15)] public string? Phone { get; set; }
    [MaxLength(150)] public string? CompanyName { get; set; }

    public LeadSource Source { get; set; } = LeadSource.Website;
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public Priority Priority { get; set; } = Priority.Medium;

    [Precision(18, 2)]
    public decimal ExpectedValue { get; set; }

    [MaxLength(1000)] public string? Notes { get; set; }

    [MaxLength(450)] public string? AssignedTo { get; set; }
    public int? ConvertedCustomerId { get; set; }   // filled when lead is converted

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}