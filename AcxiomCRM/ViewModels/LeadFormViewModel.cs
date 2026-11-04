using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

// Create/Edit form for a Lead (4.5, 17.6)
public class LeadFormViewModel
{
    public int LeadId { get; set; }

    [Display(Name = "Lead Code")]
    public string? LeadCode { get; set; }

    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    // Optional, but must be valid when entered (17.6: "common validation rules")
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string? Email { get; set; }

    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid phone number (10-digit Indian mobile).")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Lead Source is required.")]
    [Display(Name = "Lead Source")]
    public LeadSource Source { get; set; } = LeadSource.Website;

    [Required(ErrorMessage = "Lead Status is required.")]
    [Display(Name = "Lead Status")]
    public LeadStatus Status { get; set; } = LeadStatus.New;

    [Required(ErrorMessage = "Priority is required.")]
    public Priority Priority { get; set; } = Priority.Medium;

    // Configured numeric range (17.6)
    [Range(0, 100000000, ErrorMessage = "Expected Value must be between 0 and 10,00,00,000.")]
    [Display(Name = "Expected Value (₹)")]
    public decimal ExpectedValue { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    [Display(Name = "Assigned Sales Executive")]
    public string? AssignedTo { get; set; }

    // ---------- Dropdown data (filled by the controller, never posted) ----------
    [ValidateNever]
    public IEnumerable<SelectListItem> SalesExecutives { get; set; } = new List<SelectListItem>();

    // Only the statuses allowed from the current one (workflow, 4.5)
    [ValidateNever]
    public IEnumerable<SelectListItem> AllowedStatuses { get; set; } = new List<SelectListItem>();
}