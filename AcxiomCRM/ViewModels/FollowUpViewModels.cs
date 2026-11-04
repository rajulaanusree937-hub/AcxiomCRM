using AcxiomCRM.Models.Validation;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

// ===================== Create / Edit form (4.6) =====================
public class FollowUpFormViewModel : IValidatableObject
{
    public int FollowUpId { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Opportunity")]
    public int? OpportunityId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    [NotInPast("Follow-up date cannot be earlier than today.",
    DependentProperty = nameof(Status), InactiveValues = "Completed,Missed,Cancelled")]
    [Display(Name = "Follow-Up Date & Time")]
    public DateTime? FollowUpDate { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Follow-up type is required.")]
    [Display(Name = "Type")]

    [EnumDataType(typeof(FollowUpType), ErrorMessage = "Select a valid type.")]
    public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;


    [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
    public string? Remarks { get; set; }

    [Required(ErrorMessage = "Status is required.")]

    [EnumDataType(typeof(FollowUpStatus), ErrorMessage = "Select a valid status.")]
    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    [Display(Name = "Assigned User")]
    public string? AssignedTo { get; set; }

    // ---------- dropdowns (never posted) ----------
    [ValidateNever] public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> Leads { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> Opportunities { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> SalesExecutives { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> AllowedStatuses { get; set; } = new List<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (CustomerId is null && LeadId is null && OpportunityId is null)
            yield return new ValidationResult("Select a related customer, lead or opportunity.",
                                              new[] { nameof(CustomerId) });
    }
}

// ===================== List row =====================
public class FollowUpListItem
{
    public int FollowUpId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Subject { get; set; } = string.Empty;
    public FollowUpType FollowUpType { get; set; }

    public FollowUpStatus Status { get; set; }
    public string RelatedType { get; set; } = string.Empty;   // Customer / Lead / Opportunity
    public string RelatedName { get; set; } = string.Empty;
    public string RelatedController { get; set; } = string.Empty;
    public int RelatedId { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }

    public bool IsOverdue => Status == FollowUpStatus.Planned && FollowUpDate < DateTime.Now;
}

// ===================== List page: Date, Status, Assigned User, Related Customer/Lead (17.13) =====================
public class FollowUpListViewModel
{
    public string View { get; set; } = "all";       // all, upcoming, overdue, today
    public string? Search { get; set; }             // subject / remarks
    public FollowUpStatus? Status { get; set; }
    public string? AssignedTo { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }

    [DataType(DataType.Date)] public DateTime? DateFrom { get; set; }
    [DataType(DataType.Date)] public DateTime? DateTo { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public List<FollowUpListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int OverdueCount { get; set; }

    public IEnumerable<SelectListItem> AssignedUsers { get; set; } = new List<SelectListItem>();

    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> LeadOptions { get; set; } = new List<SelectListItem>();

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}