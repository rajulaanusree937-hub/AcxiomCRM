using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.ViewModels;

// One row in the Customers table
public class CustomerListItem
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? City { get; set; }
    public CustomerStatus Status { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedDate { get; set; }
}

// The whole Customers list page: filters in, results out
public class CustomerListViewModel
{
    // ---------- Filters (come from the query string, e.g. ?search=ravi&status=Active) ----------
    public string? Search { get; set; }          // matches Name, Email, Phone, Company (17.13)
    public CustomerStatus? Status { get; set; }
    public string SortBy { get; set; } = "newest"; // newest, oldest, name, name_desc
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    // ---------- Results ----------
    public List<CustomerListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }


    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}