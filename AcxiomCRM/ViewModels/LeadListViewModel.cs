using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class LeadListItem
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; }
    public Priority Priority { get; set; }
    public decimal ExpectedValue { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedDate { get; set; }
}

// Leads list page — search by Lead Name, Company, Status, Assigned User (17.13)
public class LeadListViewModel
{
    public string? Search { get; set; }            // name, company, code, email, phone
    public LeadStatus? Status { get; set; }
    public string? AssignedTo { get; set; }        // Admin/Manager only
    public string SortBy { get; set; } = "newest"; // newest, oldest, name, value_desc
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public List<LeadListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }

    // For the "Assigned User" filter dropdown
    public IEnumerable<SelectListItem> AssignedUsers { get; set; } = new List<SelectListItem>();

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}