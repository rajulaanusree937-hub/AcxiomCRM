using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class AuditLogListItem
{
    public int AuditLogId { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
}

// Filters: user, module, action, date range (4.9)
public class AuditLogListViewModel
{
    public string? UserId { get; set; }
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    [DataType(DataType.Date)] public DateTime? DateFrom { get; set; }
    [DataType(DataType.Date)] public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public List<AuditLogListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }


    public IEnumerable<SelectListItem> Users { get; set; } = new List<SelectListItem>();
    public IEnumerable<string> Modules { get; set; } = new List<string>();
    public IEnumerable<string> Actions { get; set; } = new List<string>();

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

}