using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

// ===================== Create / Edit form (4.8, 17.7) =====================
public class OpportunityFormViewModel : IValidatableObject
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer is required.")]
    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Source Lead")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0, 100000000, ErrorMessage = "Amount cannot be negative or exceed 10,00,00,000.")]
    [Display(Name = "Amount (₹)")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    [Display(Name = "Probability (%)")]

    public int? Probability { get; set; } = 20;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime? ExpectedCloseDate { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Display(Name = "Source")]
    public LeadSource? Source { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    [Display(Name = "Owner")]
    public string? AssignedTo { get; set; }

    [ValidateNever] public IEnumerable<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> Leads { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> SalesExecutives { get; set; } = new List<SelectListItem>();
    [ValidateNever] public IEnumerable<SelectListItem> AllowedStages { get; set; } = new List<SelectListItem>();

    public bool IsActiveStage => Stage is not (OpportunityStage.Won or OpportunityStage.Lost);

    // Business rules that only apply to ACTIVE (open) opportunities
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!IsActiveStage) yield break;

        if (Amount is not null && Amount <= 0)

            yield return new ValidationResult("Opportunity Amount must be greater than 0.", new[] { nameof(Amount) });

        if (ExpectedCloseDate is not null && ExpectedCloseDate.Value.Date < DateTime.Today)
            yield return new ValidationResult("Expected Close Date cannot be in the past.", new[] { nameof(ExpectedCloseDate) });
    }
}

// ===================== List row =====================
public class OpportunityListItem
{
    public int OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public OpportunityStage Stage { get; set; }
    public OpportunityStatus Status { get; set; }
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string? AssignedToName { get; set; }

    public decimal WeightedAmount => Amount * Probability / 100m;   // 4.8
}

// ===================== List page: Name, Customer, Stage, Status (17.13) =====================
public class OpportunityListViewModel
{
    public string? Search { get; set; }
    public int? CustomerId { get; set; }
    public OpportunityStage? Stage { get; set; }
    public OpportunityStatus? Status { get; set; }
    public string? AssignedTo { get; set; }

    public string SortBy { get; set; } = "close_asc";   // close_asc, amount_desc, newest, name
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public List<OpportunityListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalWeighted { get; set; }

    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> AssignedUsers { get; set; } = new List<SelectListItem>();

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

// ===================== Sales Pipeline page (17.2) =====================
public class PipelineColumn
{
    public OpportunityStage Stage { get; set; }
    public List<OpportunityListItem> Items { get; set; } = new();
    public decimal TotalAmount => Items.Sum(i => i.Amount);
    public decimal TotalWeighted => Items.Sum(i => i.WeightedAmount);
}

public class PipelineViewModel
{
    public List<PipelineColumn> Columns { get; set; } = new();
    public decimal OpenPipelineAmount { get; set; }
    public decimal OpenWeightedAmount { get; set; }
}