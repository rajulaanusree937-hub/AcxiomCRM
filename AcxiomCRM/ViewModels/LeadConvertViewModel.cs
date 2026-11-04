using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

// Lead -> Customer (+ optional Opportunity) conversion (4.5, Section 8 workflow)
public class LeadConvertViewModel : IValidatableObject
{
    public int LeadId { get; set; }
    public string? LeadCode { get; set; }

    // ---------- New Customer (17.5 rules apply) ----------
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters.")]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid phone number (10-digit Indian mobile).")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    // ---------- Optional Opportunity ----------
    [Display(Name = "Also create an Opportunity")]
    public bool CreateOpportunity { get; set; } = true;


    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    [Display(Name = "Opportunity Name")]
    public string? OpportunityName { get; set; }

    [Range(0.01, 100000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    [Display(Name = "Amount (₹)")]
    public decimal? Amount { get; set; }

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    [Display(Name = "Probability (%)")]
    public int? Probability { get; set; } = 20;

    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime? ExpectedCloseDate { get; set; }

    // Rules that only apply when "Also create an Opportunity" is ticked (server-side)
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!CreateOpportunity) yield break;

        if (string.IsNullOrWhiteSpace(OpportunityName))
            yield return new ValidationResult("Opportunity Name is required.", new[] { nameof(OpportunityName) });

        if (Amount is null or <= 0)
            yield return new ValidationResult("Opportunity Amount must be greater than 0.", new[] { nameof(Amount) });

        if (Probability is null or < 0 or > 100)
            yield return new ValidationResult("Probability must be between 0 and 100.", new[] { nameof(Probability) });

        if (ExpectedCloseDate is null)

            yield return new ValidationResult("Expected Close Date is required.", new[] { nameof(ExpectedCloseDate) });
        else if (ExpectedCloseDate.Value.Date < DateTime.Today)
            yield return new ValidationResult("Expected Close Date cannot be in the past.", new[] { nameof(ExpectedCloseDate) });
    }
}