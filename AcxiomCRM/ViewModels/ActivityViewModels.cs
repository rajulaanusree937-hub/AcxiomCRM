using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class ActivityFormViewModel : IValidatableObject
{
    public int ActivityId { get; set; }

    [Required(ErrorMessage = "Activity type is required.")]
    [Display(Name = "Activity Type")]
    public ActivityType ActivityType { get; set; } = ActivityType.Call;

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
    public string Subject { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Activity date is required.")]
    [Display(Name = "Activity Date & Time")]
    public DateTime? ActivityDate { get; set; }

    [Display(Name = "Customer")] public int? CustomerId { get; set; }
    [Display(Name = "Lead")] public int? LeadId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    [Display(Name = "Assigned User")]
    public string? AssignedTo { get; set; }

    [ValidateNever] public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> Leads { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> SalesExecutives { get; set; } = new List<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (CustomerId is null && LeadId is null)
            yield return new ValidationResult("Select a related customer or lead.", new[] { nameof(CustomerId) });
    }
}

public class ActivityListItem
{
    public int ActivityId { get; set; }
    public ActivityType ActivityType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public ActivityStatus Status { get; set; }
    public string RelatedType { get; set; } = string.Empty;
    public string RelatedName { get; set; } = string.Empty;
    public string RelatedController { get; set; } = string.Empty;
    public int RelatedId { get; set; }
    public string? AssignedToName { get; set; }
}

// Filters: Activity Type, Date, Status, Assigned User (17.13)
public class ActivityListViewModel
{
    public string? Search { get; set; }
    public ActivityType? ActivityType { get; set; }
    public ActivityStatus? Status { get; set; }
    public string? AssignedTo { get; set; }
    [DataType(DataType.Date)] public DateTime? DateFrom { get; set; }
    [DataType(DataType.Date)] public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public List<ActivityListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public IEnumerable<SelectListItem> AssignedUsers { get; set; } = new List<SelectListItem>();

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
