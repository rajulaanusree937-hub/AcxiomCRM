using AcxiomCRM.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace AcxiomCRM.Models;

public class Customer : IOwnedEntity
{
    public int CustomerId { get; set; }

    [Required, MaxLength(20)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(15)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(150)] public string? CompanyName { get; set; }
    [MaxLength(250)] public string? Address { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(100)] public string? State { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [MaxLength(1000)] public string? Notes { get; set; }

    [MaxLength(450)] public string? AssignedTo { get; set; }   // Sales Executive's UserId
    [MaxLength(450)] public string CreatedBy { get; set; } = string.Empty;


    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}