using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class ReportDefinition
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "bi-table";
    public bool ManagerOrAdminOnly { get; set; }
    public bool AdminOnly { get; set; }
    public string? ExternalController { get; set; }   // e.g. Audit -> AuditLogs page
}

public class ReportColumn
{
    public string Key { get; set; } = "";
    public string Header { get; set; } = "";
    public string Kind { get; set; } = "text";   // text, number, money, percent, date, datetime
}

// One model for every report: filters in, columns + rows out
public class ReportViewModel
{
    // ---------- Filters (query string) ----------
    public string Key { get; set; } = "";
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Owner { get; set; }
    public string? Status { get; set; }

    public string? Search { get; set; }
    public string? GroupBy { get; set; }      // pipeline: stage | owner
    public string? SortBy { get; set; }
    public bool Desc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // ---------- Output ----------
    [ValidateNever] public string Title { get; set; } = "";
    [ValidateNever] public string Description { get; set; } = "";
    [ValidateNever] public string DateFieldLabel { get; set; } = "Created";
    [ValidateNever] public List<ReportColumn> Columns { get; set; } = new();
    [ValidateNever] public List<object?[]> Rows { get; set; } = new();
    [ValidateNever] public List<KeyValuePair<string, string>> Summary { get; set; } = new();
    [ValidateNever] public List<string> StatusOptions { get; set; } = new();
    [ValidateNever] public List<SelectListItem> OwnerOptions { get; set; } = new();
    public bool ShowOwnerFilter { get; set; }
    public bool ShowGroupBy { get; set; }
    public int TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}